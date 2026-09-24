import Database from "@/database/Database";
import { ScryfallBulkDataClient } from "./bulk-client";
import { CoreCard, Ruling, StreamProcessingOptions } from "./types";

interface SyncOptions {
    syncCards?: boolean;
    syncRulings?: boolean;
    concurrency?: number; // Nombre de cartes/rulings à traiter en parallèle
    /** Ne ré-upserte que les cartes multi-faces (resync ciblé des DFC) */
    onlyDfc?: boolean;
}

export default class ScryFallSync {
    private concurrency: number;
    private onlyDfc: boolean = false;

    constructor(concurrency: number = 5) {
        this.concurrency = concurrency; // Par défaut, traiter 5 items en parallèle
    }

    public async start(options: SyncOptions = { syncCards: true, syncRulings: false }) {
        if (options.concurrency) {
            this.concurrency = options.concurrency;
        }
        this.onlyDfc = options.onlyDfc ?? false;
        
        console.log(`🔄 Syncing ${options.syncCards && options.syncRulings ? 'BOTH cards and rulings' : options.syncCards ? 'cards ONLY' : 'rulings ONLY'}${this.onlyDfc ? ' (DFC uniquement)' : ''}`);
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
                    const failedCards = await this.upsertCardsBatch(cards);
                    totalErrors += failedCards;
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

    private async upsertCardsBatch(cards: CoreCard[]): Promise<number> {
        let batchSuccesses = 0;
        let batchErrors = 0;
        
        // Mode DFC ciblé : on ne ré-upserte que les cartes multi-faces
        // (les cartes simple face sont déjà correctes en base)
        const toUpsert = this.onlyDfc
            ? cards.filter(c => (c.card_faces?.length ?? 0) > 1)
            : cards;
        if (toUpsert.length === 0) {
            return 0;
        }
        
        // Traiter les cartes en parallèle avec limite de concurrence
        console.log(`Processing ${toUpsert.length}/${cards.length} cards with concurrency ${this.concurrency}...`);
        
        for (let i = 0; i < toUpsert.length; i += this.concurrency) {
            const chunk = toUpsert.slice(i, i + this.concurrency);
            
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
            const processed = Math.min(i + this.concurrency, toUpsert.length);
            if (processed % 50 === 0 || processed === toUpsert.length) {
                console.log(`  Processed ${processed}/${toUpsert.length} cards (${Math.round((processed / toUpsert.length) * 100)}%)`);
            }
            
            // Petit délai entre les chunks pour ne pas surcharger la DB
            if (i + this.concurrency < toUpsert.length) {
                await new Promise(resolve => setTimeout(resolve, 10));
            }
        }
        
        console.log(`Batch completed: ${batchSuccesses} successes, ${batchErrors} errors`);
        return batchErrors;
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
        const publishedAt = new Date(ruling.published_at);
        await Database.prisma.ruling.upsert({
            // Un oracle peut avoir plusieurs rulings publiés le même jour,
            // donc l'unicité inclut le commentaire (clé composite)
            where: {
                oracle_id_published_at_comment: {
                    oracle_id: ruling.oracle_id,
                    published_at: publishedAt,
                    comment: ruling.comment,
                },
            },
            update: {
                comment: ruling.comment,
            },
            create: {
                oracle_id: ruling.oracle_id,
                published_at: publishedAt,
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
                        set: card.set || null, // code du set (ex: 'unf')
                        name: card.set_name,
                        type: card.set_type,
                    },
                    create: {
                        id: card.set_id,
                        set: card.set || null, // code du set (ex: 'unf')
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
            printed_name: card.printed_name || null,
            set_id: card.set_id,
            collector_number: card.collector_number,
            rarity: card.rarity,
            image_url: card.image_uris?.normal || card.image_uris?.large || "",
            image_status: card.image_status || null,
            release_at: new Date(card.released_at),
        };

        // Upsert the main card (sets are already handled in sub-batch)
        await tx.card.upsert({
            where: { id: card.id },
            update: {
                ...cardData,
                updated_at: new Date(),
            },
            create: cardData,
        });

        // =========================================================================
        // ORACLE : champs COMMUNS à toutes les faces/impressions (legalities,
        // cmc, layout, keywords, color_identities). Les champs qui varient par
        // face (type_line, mana_cost, power/toughness/loyalty/defense, colors)
        // restent sur Face : les cartes double face partagent un même oracle.
        // =========================================================================
        interface OracleSource {
            text: string;
            cmc?: number | null;
            layout?: string | null;
            keywords?: string[] | null;
            color_identities?: string[] | null;
        }

        // Les champs `undefined` ne sont pas touchés par l'update (upsert) ;
        // `create` reçoit toujours des valeurs (défauts explicites).
        const buildOracleData = (source: OracleSource) => {
            const data: Record<string, unknown> = {
                text: source.text,
                // Legalities card-level : identiques pour toutes les faces d'une
                // carte double face (Scryfall ne les donne qu'au niveau carte)
                legalities: card.legalities || {},
            };
            if (source.cmc !== undefined) data.cmc = source.cmc ?? null;
            if (source.layout !== undefined) data.layout = source.layout || null;
            if (source.keywords !== undefined) data.keywords = source.keywords || [];
            if (source.color_identities !== undefined) data.color_identities = source.color_identities || [];
            return data;
        };

        const upsertOracle = async (oracleId: string, source: OracleSource): Promise<void> => {
            const data = buildOracleData(source);
            await tx.oracle.upsert({
                where: { id: oracleId },
                update: data,
                create: {
                    id: oracleId,
                    text: source.text,
                    cmc: source.cmc ?? null,
                    layout: source.layout || null,
                    keywords: source.keywords || [],
                    color_identities: source.color_identities || [],
                    legalities: card.legalities || {},
                },
            });
        };

        // Oracle principal (cartes simple face)
        if (card.oracle_id) {
            await upsertOracle(card.oracle_id, {
                text: card.oracle_text || "",
                cmc: card.cmc,
                layout: card.layout,
                keywords: card.keywords,
                color_identities: card.color_identity,
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
                const faceOracleId = face.oracle_id || card.oracle_id;
                const isSharedOracle = faceOracleId === card.oracle_id;

                // Chaque face peut avoir son propre oracle (cartes réversibles) :
                // s'assurer qu'il existe pour la FK faces_oracle_id_fkey
                if (faceOracleId) {
                    await upsertOracle(faceOracleId, {
                        text: face.oracle_text || card.oracle_text || "",
                        cmc: face.cmc ?? card.cmc,
                        layout: face.layout || card.layout,
                        // Scryfall ne donne pas de keywords par face : card-level
                        // sur la face avant uniquement, sinon on ne touche pas
                        keywords: i === 0 ? card.keywords : undefined,
                        // Oracle partagé → identité card-level (déjà posée) ;
                        // oracle propre (réversible) → color_indicator de la face
                        color_identities: isSharedOracle
                            ? undefined
                            : face.color_indicator || card.color_identity,
                    });
                }

                await tx.face.create({
                    data: {
                        index: i,
                        name: face.name,
                        oracle_id: faceOracleId,
                        card_id: card.id,
                        type_line: face.type_line ?? card.type_line ?? null,
                        printed_type_line: face.printed_type_line || null,
                        mana_cost: face.mana_cost ?? null,
                        power: face.power ?? null,
                        toughness: face.toughness ?? null,
                        loyalty: face.loyalty ?? null,
                        defense: face.defense ?? null,
                        colors: face.colors || [],
                        flavor_text: face.flavor_text,
                        printed_text: face.printed_text || null,
                        flavor_name: card.flavor_name || null, // flavor_name est card-level
                        image_url: face.image_uris?.normal || face.image_uris?.large || cardData.image_url,
                    },
                });
            }
        } else {
            await tx.face.create({
                data: {
                    index: 0,
                    name: card.name,
                    oracle_id: card.oracle_id,
                    card_id: card.id,
                    type_line: card.type_line ?? null,
                    printed_type_line: card.printed_type_line || null,
                    mana_cost: card.mana_cost ?? null,
                    power: card.power ?? null,
                    toughness: card.toughness ?? null,
                    loyalty: card.loyalty ?? null,
                    defense: card.defense ?? null,
                    colors: card.colors || [],
                    flavor_text: card.flavor_text,
                    printed_text: card.printed_text || null,
                    flavor_name: card.flavor_name || null,
                    image_url: cardData.image_url,
                },
            });
        }
    }
}