import Database from "@/database/Database";
import { ScryfallBulkDataClient } from "./bulk-client";
import { CoreCard, StreamProcessingOptions } from "./types";

export default class ScryFallSync {

    public async start() {
        const bulk = new ScryfallBulkDataClient();

        console.log("Starting bulk card sync...");

        let totalProcessed = 0;
        let totalErrors = 0;
        let batchErrors: { batchIndex: number; error: string; cardName?: string }[] = [];

        const startTime = Date.now();

        const processingOptions: StreamProcessingOptions<CoreCard> = {
            batchSize: 500, // Augmenté de 100 à 500 pour de meilleures performances
            onBatch: async (cards: CoreCard[], batchIndex: number) => {
                const batchStartTime = Date.now();
                console.log(`Processing batch ${batchIndex + 1} with ${cards.length} cards...`);

                try {
                    await this.upsertCardsBatch(cards);
                    totalProcessed += cards.length;
                    const batchTime = Date.now() - batchStartTime;
                    const totalTime = Date.now() - startTime;
                    const avgTimePerBatch = totalTime / (batchIndex + 1);

                    console.log(`Batch ${batchIndex + 1} completed in ${batchTime}ms. Total: ${totalProcessed} cards, ${totalErrors} errors`);
                    console.log(`Average batch time: ${Math.round(avgTimePerBatch)}ms`);
                } catch (error) {
                    totalErrors++;
                    const errorMessage = error instanceof Error ? error.message : String(error);
                    batchErrors.push({
                        batchIndex: batchIndex + 1,
                        error: errorMessage,
                        cardName: cards[0]?.name
                    });
                    console.error(`Batch ${batchIndex + 1} failed:`, errorMessage);
                }
            },
            onError: (error: Error, item?: any, index?: number) => {
                totalErrors++;
                console.error(`Error processing item at index ${index}:`, error.message);
                if (item) {
                    console.error("Problematic item:", item.name || item.id || "Unknown");
                }
            }
        };

        try {
            const metadata = await bulk.downloadBulkDataStream("all_cards", processingOptions);
            const totalTime = Date.now() - startTime;

            console.log("=".repeat(50));
            console.log("BULK SYNC COMPLETED");
            console.log("=".repeat(50));
            console.log(`📊 Total items processed: ${metadata.totalItems}`);
            console.log(`✅ Successfully processed: ${totalProcessed}`);
            console.log(`❌ Total errors: ${totalErrors}`);
            console.log(`⏱️  Total time: ${Math.round(totalTime / 1000)}s`);
            console.log(`🚀 Average speed: ${Math.round(metadata.totalItems / (totalTime / 1000))} items/sec`);
            console.log(`📁 File size: ${Math.round(metadata.fileSize / (1024 * 1024))}MB`);
            console.log(`📅 Last updated: ${metadata.lastUpdated}`);

            if (batchErrors.length > 0) {
                console.log("\n❌ BATCH ERRORS SUMMARY:");
                batchErrors.forEach(err => {
                    console.log(`  - Batch ${err.batchIndex}: ${err.error} ${err.cardName ? `(first card: ${err.cardName})` : ''}`);
                });
            }

            if (metadata.stats) {
                console.log("\n📈 PROCESSING STATS:");
                Object.entries(metadata.stats).forEach(([key, count]) => {
                    console.log(`  - ${key}: ${count}`);
                });
            }

            console.log("=".repeat(50));

            return metadata;
        } catch (error) {
            const totalTime = Date.now() - startTime;
            console.error("=".repeat(50));
            console.error("BULK SYNC FAILED");
            console.error("=".repeat(50));
            console.error(`❌ Failed after ${Math.round(totalTime / 1000)}s`);
            console.error(`📊 Processed ${totalProcessed} cards before failure`);
            console.error(`💥 Error:`, error);
            console.error("=".repeat(50));
            throw error;
        }
    }

    private async upsertCardsBatch(cards: CoreCard[]): Promise<void> {
        let batchSuccesses = 0;
        let batchErrors = 0;

        console.log(`Processing ${cards.length} cards in optimized batch...`);

        try {
            // Process the entire batch in a single transaction for maximum performance
            await Database.prisma.$transaction(
                async (tx) => {
                    // 1. Extract unique sets and upsert them first
                    const uniqueSets = new Map<string, { id: string; name: string; type: string; set?: string }>();
                    for (const card of cards) {
                        if (card.set_id && card.set_name && !uniqueSets.has(card.set_id)) {
                            uniqueSets.set(card.set_id, {
                                id: card.set_id,
                                name: card.set_name,
                                type: card.set_type,
                                set: card.set // Ajouter l'abréviation du set
                            });
                        }
                    }

                    // Bulk upsert sets
                    for (const setData of uniqueSets.values()) {
                        await tx.set.upsert({
                            where: { id: setData.id },
                            update: { name: setData.name, type: setData.type, set: setData.set },
                            create: setData
                        });
                    }

                    // 2. Extract unique oracles and upsert them
                    const uniqueOracles = new Map<string, { id: string; text: string }>();
                    for (const card of cards) {
                        if (card.oracle_id && !uniqueOracles.has(card.oracle_id)) {
                            uniqueOracles.set(card.oracle_id, {
                                id: card.oracle_id,
                                text: card.oracle_text || ""
                            });
                        }
                    }

                    // Bulk upsert oracles
                    for (const oracleData of uniqueOracles.values()) {
                        await tx.oracle.upsert({
                            where: { id: oracleData.id },
                            update: { text: oracleData.text },
                            create: oracleData
                        });
                    }

                    // 3. Prepare all card data for bulk operations
                    const cardIds = cards.map(card => card.id);
                    
                    // Optimisation: seulement supprimer les faces des cartes qui existent déjà
                    const existingCards = await tx.card.findMany({
                        where: { id: { in: cardIds } },
                        select: { id: true }
                    });
                    const existingCardIds = existingCards.map(card => card.id);
                    
                    if (existingCardIds.length > 0) {
                        // Delete existing faces only for existing cards (legalities are now JSON in card table)
                        await tx.face.deleteMany({ where: { card_id: { in: existingCardIds } } });
                    }

                    // 4. Bulk upsert cards
                    for (const card of cards) {
                        if (!card.id || !card.name || !card.set_id || !card.collector_number) {
                            batchErrors++;
                            console.error(`Missing required fields for card: ${card.name || 'unknown'}`);
                            continue;
                        }

                        try {
                            const cardData = {
                                id: card.id,
                                name: card.name,
                                printed_name: card.printed_name || null,
                                lang: card.lang || 'en',
                                set_id: card.set_id,
                                collector_number: card.collector_number,
                                rarity: card.rarity,
                                image_url: card.image_uris?.normal || card.image_uris?.large || "",
                                release_at: new Date(card.released_at),
                                legalities: card.legalities ? JSON.parse(JSON.stringify(card.legalities)) : {}, // Convertir en objet JSON
                            };

                            await tx.card.upsert({
                                where: { id: card.id },
                                update: {
                                    name: cardData.name,
                                    printed_name: cardData.printed_name,
                                    lang: cardData.lang,
                                    set_id: cardData.set_id,
                                    collector_number: cardData.collector_number,
                                    rarity: cardData.rarity,
                                    image_url: cardData.image_url,
                                    release_at: cardData.release_at,
                                    legalities: cardData.legalities,
                                    updated_at: new Date(),
                                },
                                create: cardData,
                            });
                            batchSuccesses++;
                        } catch (cardError) {
                            batchErrors++;
                            console.error(`Failed to upsert card ${card.name}: ${cardError}`);
                        }
                    }

                    // 5. Bulk create faces
                    const facesToCreate: any[] = [];
                    for (const card of cards) {
                        if (card.card_faces && card.card_faces.length > 0) {
                            // Multi-faced card
                            for (let i = 0; i < card.card_faces.length; i++) {
                                const face = card.card_faces[i];
                                facesToCreate.push({
                                    index: i,
                                    name: face.name,
                                    oracle_id: face.oracle_id || card.oracle_id,
                                    layout: face.layout || card.layout,
                                    card_id: card.id,
                                    cmc: face.cmc || card.cmc,
                                    type_line: face.type_line,
                                    printed_type_line: face.printed_type_line || null,
                                    mana_cost: face.mana_cost,
                                    power: face.power,
                                    toughness: face.toughness,
                                    loyalty: face.loyalty,
                                    defense: face.defense,
                                    flavor_text: face.flavor_text,
                                    printed_text: face.printed_text || null,
                                    keywords: [], // Keywords are not available on individual faces
                                    color_identities: face.color_indicator || [],
                                    colors: face.colors || [],
                                    flavor_name: card.flavor_name || null,
                                    image_url: face.image_uris?.normal || face.image_uris?.large || (card.image_uris?.normal || card.image_uris?.large || ""),
                                });
                            }
                        } else {
                            facesToCreate.push({
                                index: 0,
                                name: card.name,
                                oracle_id: card.oracle_id,
                                layout: card.layout,
                                card_id: card.id,
                                cmc: card.cmc,
                                type_line: card.type_line,
                                printed_type_line: card.printed_type_line || null,
                                mana_cost: card.mana_cost,
                                power: card.power,
                                toughness: card.toughness,
                                loyalty: card.loyalty,
                                defense: card.defense,
                                flavor_text: card.flavor_text,
                                printed_text: card.printed_text || null,
                                keywords: card.keywords,
                                color_identities: card.color_identity,
                                colors: card.colors || [],
                                flavor_name: card.flavor_name || null,
                                image_url: card.image_uris?.normal || card.image_uris?.large || "",
                            });
                        }
                    }

                    // Create faces in chunks to avoid parameter limits
                    const chunkSize = 100;
                    for (let i = 0; i < facesToCreate.length; i += chunkSize) {
                        const chunk = facesToCreate.slice(i, i + chunkSize);
                        await tx.face.createMany({ data: chunk });
                    }

                    // Légalités sont maintenant stockées directement dans la table cards comme JSON
                },
                { 
                    timeout: 120000, // Increased timeout for large batches
                    maxWait: 10000
                }
            );
        } catch (error) {
            batchErrors = cards.length;
            console.error(`Batch transaction failed: ${error}`);
            throw error;
        }

        console.log(`Batch completed: ${batchSuccesses} successes, ${batchErrors} errors`);
    }

    // Anciennes méthodes supprimées - remplacées par le traitement en batch optimisé
}