import Database from "@/database/Database";
import { ScryfallBulkDataClient } from "./bulk-client";
import { CoreCard, Ruling, StreamProcessingOptions } from "./types";

interface SyncOptions {
    syncCards?: boolean;
    syncRulings?: boolean;
    concurrency?: number; // Nombre de cartes/rulings à traiter en parallèle
}

export default class ScryFallSync {
    private concurrency: number;

    constructor(concurrency: number = 5) {
        this.concurrency = concurrency; // Par défaut, traiter 5 items en parallèle
    }

    public async start(options: SyncOptions = { syncCards: true, syncRulings: false }) {
        console.log("🃏 MTG Scryfall Sync Starting...");
        
        if (options.concurrency) {
            this.concurrency = options.concurrency;
        }
        
        console.log(`🔄 Syncing ${options.syncCards && options.syncRulings ? 'BOTH cards and rulings' : options.syncCards ? 'cards ONLY' : 'rulings ONLY'}`);
        console.log(`⚡ Concurrency level: ${this.concurrency}`);

        try {
            if (options.syncCards) {
                await this.syncCards();
            }
            
            if (options.syncRulings) {
                await this.syncRulings();
            }

            console.log("✅ All sync operations completed successfully!");
        } catch (error) {
            console.error("❌ Sync failed:", error);
            throw error;
        }
    }

    private async syncCards() {
        const bulk = new ScryfallBulkDataClient();

        console.log("\n📦 Starting bulk card sync...");

        let totalProcessed = 0;
        let totalErrors = 0;
        let batchErrors: { batchIndex: number; error: string; cardName?: string }[] = [];

        const startTime = Date.now();

        const processingOptions: StreamProcessingOptions<CoreCard> = {
            batchSize: 100, // Further reduce main batch size to 100 cards per batch
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
        
        // Traiter les cartes en parallèle avec limite de concurrence
        console.log(`Processing ${cards.length} cards with concurrency ${this.concurrency}...`);
        
        for (let i = 0; i < cards.length; i += this.concurrency) {
            const chunk = cards.slice(i, i + this.concurrency);
            
            // Traiter ce chunk en parallèle
            const results = await Promise.allSettled(
                chunk.map(card => this.upsertSingleCard(card))
            );
            
            // Compter les succès et échecs
            results.forEach((result, idx) => {
                if (result.status === 'fulfilled') {
                    batchSuccesses++;
                } else {
                    batchErrors++;
                    const card = chunk[idx];
                    console.error(`Failed to upsert card ${card.name} (${card.id}): ${result.reason}`);
                }
            });
            
            // Log progress
            const processed = Math.min(i + this.concurrency, cards.length);
            if (processed % 50 === 0 || processed === cards.length) {
                console.log(`  Processed ${processed}/${cards.length} cards (${Math.round((processed / cards.length) * 100)}%)`);
            }
            
            // Petit délai entre les chunks pour ne pas surcharger la DB
            if (i + this.concurrency < cards.length) {
                await new Promise(resolve => setTimeout(resolve, 10));
            }
        }
        
        console.log(`Batch completed: ${batchSuccesses} successes, ${batchErrors} errors`);
    }

    private async syncRulings() {
        const bulk = new ScryfallBulkDataClient();

        console.log("\n📜 Starting bulk rulings sync...");

        let totalProcessed = 0;
        let totalErrors = 0;
        let batchErrors: { batchIndex: number; error: string }[] = [];

        const startTime = Date.now();

        const processingOptions: StreamProcessingOptions<Ruling> = {
            batchSize: 200, // Process 200 rulings per batch
            onBatch: async (rulings: Ruling[], batchIndex: number) => {
                const batchStartTime = Date.now();
                console.log(`Processing batch ${batchIndex + 1} with ${rulings.length} rulings...`);

                try {
                    await this.upsertRulingsBatch(rulings);
                    totalProcessed += rulings.length;
                    const batchTime = Date.now() - batchStartTime;
                    const totalTime = Date.now() - startTime;
                    const avgTimePerBatch = totalTime / (batchIndex + 1);

                    console.log(`Batch ${batchIndex + 1} completed in ${batchTime}ms. Total: ${totalProcessed} rulings, ${totalErrors} errors`);
                    console.log(`Average batch time: ${Math.round(avgTimePerBatch)}ms`);
                } catch (error) {
                    totalErrors++;
                    const errorMessage = error instanceof Error ? error.message : String(error);
                    batchErrors.push({
                        batchIndex: batchIndex + 1,
                        error: errorMessage
                    });
                    console.error(`Batch ${batchIndex + 1} failed:`, errorMessage);
                }
            },
            onError: (error: Error, item?: any, index?: number) => {
                totalErrors++;
                console.error(`Error processing ruling at index ${index}:`, error.message);
            }
        };

        try {
            const metadata = await bulk.downloadBulkDataStream("rulings", processingOptions);
            const totalTime = Date.now() - startTime;

            console.log("=".repeat(50));
            console.log("RULINGS SYNC COMPLETED");
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
                    console.log(`  - Batch ${err.batchIndex}: ${err.error}`);
                });
            }

            console.log("=".repeat(50));

            return metadata;
        } catch (error) {
            const totalTime = Date.now() - startTime;
            console.error("=".repeat(50));
            console.error("RULINGS SYNC FAILED");
            console.error("=".repeat(50));
            console.error(`❌ Failed after ${Math.round(totalTime / 1000)}s`);
            console.error(`📊 Processed ${totalProcessed} rulings before failure`);
            console.error(`💥 Error:`, error);
            console.error("=".repeat(50));
            throw error;
        }
    }

    private async upsertRulingsBatch(rulings: Ruling[]): Promise<void> {
        let batchSuccesses = 0;
        let batchErrors = 0;
        
        // Traiter les rulings en parallèle avec limite de concurrence
        console.log(`Processing ${rulings.length} rulings with concurrency ${this.concurrency}...`);
        
        for (let i = 0; i < rulings.length; i += this.concurrency) {
            const chunk = rulings.slice(i, i + this.concurrency);
            
            // Traiter ce chunk en parallèle
            const results = await Promise.allSettled(
                chunk.map(ruling => this.upsertSingleRuling(ruling))
            );
            
            // Compter les succès et échecs
            results.forEach((result, idx) => {
                if (result.status === 'fulfilled') {
                    batchSuccesses++;
                } else {
                    batchErrors++;
                    const ruling = chunk[idx];
                    console.error(`Failed to upsert ruling (${ruling.oracle_id}): ${result.reason}`);
                }
            });
            
            // Log progress
            const processed = Math.min(i + this.concurrency, rulings.length);
            if (processed % 100 === 0 || processed === rulings.length) {
                console.log(`  Processed ${processed}/${rulings.length} rulings (${Math.round((processed / rulings.length) * 100)}%)`);
            }
        }
        
        console.log(`Batch completed: ${batchSuccesses} successes, ${batchErrors} errors`);
    }

    private async upsertSingleRuling(ruling: Ruling): Promise<void> {
        await Database.prisma.ruling.upsert({
            where: { 
                id: `${ruling.oracle_id}-${ruling.published_at}`
            },
            update: {
                comment: ruling.comment,
            },
            create: {
                id: `${ruling.oracle_id}-${ruling.published_at}`,
                oracle_id: ruling.oracle_id,
                published_at: new Date(ruling.published_at),
                comment: ruling.comment,
            },
        });
    }

    private async upsertSingleCard(card: CoreCard): Promise<void> {
        // Use a single transaction for one card to minimize connection usage
        await Database.prisma.$transaction(async (tx) => {
            // First, ensure the set exists for this card
            if (card.set_id && card.set_name) {
                await tx.set.upsert({
                    where: { id: card.set_id },
                    update: {
                        name: card.set_name,
                        type: card.set_type,
                    },
                    create: {
                        id: card.set_id,
                        name: card.set_name,
                        type: card.set_type,
                    },
                });
            }
            
            // Now process the card
            await this.upsertCardInTransaction(card, tx);
        }, {
            timeout: 30000, // 30 second timeout per card
        });
    }

    private async upsertCardInTransaction(card: CoreCard, tx: any): Promise<void> {
        // Validate required fields
        if (!card.id || !card.name || !card.set_id || !card.collector_number) {
            throw new Error(`Missing required fields for card: id=${card.id}, name=${card.name}, set_id=${card.set_id}, collector_number=${card.collector_number}`);
        }

        // Transform CoreCard to database format
        const cardData = {
            id: card.id,
            name: card.name,
            lang: card.lang || 'en', // Default to English if not specified
            set_id: card.set_id,
            collector_number: card.collector_number,
            rarity: card.rarity,
            image_url: card.image_uris?.normal || card.image_uris?.large || "",
            release_at: new Date(card.released_at),
        };

        // Upsert the main card (sets are already handled in sub-batch)
        const dbCard = await tx.card.upsert({
            where: { id: card.id },
            update: {
                name: cardData.name,
                lang: cardData.lang,
                set_id: cardData.set_id,
                collector_number: cardData.collector_number,
                rarity: cardData.rarity,
                image_url: cardData.image_url,
                release_at: cardData.release_at,
                legalities: card.legalities || {},
                updated_at: new Date(),
            },
            create: {
                ...cardData,
                legalities: card.legalities || {},
            },
        });

        // Handle Oracle record
        if (card.oracle_id) {
            await tx.oracle.upsert({
                where: { id: card.oracle_id },
                update: {
                    text: card.oracle_text || "",
                },
                create: {
                    id: card.oracle_id,
                    text: card.oracle_text || "",
                },
            });
        }

        // Delete existing faces to replace them
        await tx.face.deleteMany({
            where: { card_id: card.id },
        });

        // Handle card faces
        if (card.card_faces && card.card_faces.length > 0) {
            // Multi-faced card
            for (let i = 0; i < card.card_faces.length; i++) {
                const face = card.card_faces[i];
                await tx.face.create({
                    data: {
                        index: i,
                        name: face.name,
                        oracle_id: face.oracle_id || card.oracle_id,
                        layout: face.layout || card.layout,
                        card_id: card.id,
                        cmc: face.cmc || card.cmc,
                        type_line: face.type_line,
                        mana_cost: face.mana_cost,
                        power: face.power,
                        toughness: face.toughness,
                        loyalty: face.loyalty,
                        defense: face.defense,
                        flavor_text: face.flavor_text,
                        keywords: [], // Keywords are not available on individual faces
                        color_identities: face.color_indicator || [],
                        colors: face.colors || [],
                        flavor_name: card.flavor_name || null, // flavor_name is on the main card
                        image_url: face.image_uris?.normal || face.image_uris?.large || cardData.image_url,
                    },
                });
            }
        } else await tx.face.create({
            data: {
                index: 0,
                name: card.name,
                oracle_id: card.oracle_id,
                layout: card.layout,
                card_id: card.id,
                cmc: card.cmc,
                type_line: card.type_line,
                mana_cost: card.mana_cost,
                power: card.power,
                toughness: card.toughness,
                loyalty: card.loyalty,
                defense: card.defense,
                flavor_text: card.flavor_text,
                keywords: card.keywords,
                color_identities: card.color_identity,
                colors: card.colors || [],
                flavor_name: card.flavor_name || null,
                image_url: cardData.image_url,
            },
        });
    }
}