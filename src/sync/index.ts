import Database from "@/database/Database";
import { ScryfallBulkDataClient } from "./bulk-client";
import { CoreCard, StreamProcessingOptions, Ruling } from "./types";
import * as fs from 'fs';
import * as path from 'path';

export default class ScryFallSync {
    private logFile: string;
    private logStream: fs.WriteStream;

    constructor() {
        // Create logs directory if it doesn't exist
        const logsDir = path.join(process.cwd(), 'logs');
        if (!fs.existsSync(logsDir)) {
            fs.mkdirSync(logsDir, { recursive: true });
        }

        // Create log file with timestamp
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        this.logFile = path.join(logsDir, `sync-${timestamp}.log`);
        this.logStream = fs.createWriteStream(this.logFile, { flags: 'a' });

        this.log(`\n${'='.repeat(60)}\nSync started at ${new Date().toISOString()}\n${'='.repeat(60)}`);
    }

    private log(message: string): void {
        const timestamp = new Date().toISOString();
        const logMessage = `[${timestamp}] ${message}`;
        console.log(message);
        this.logStream.write(logMessage + '\n');
    }

    private logError(message: string, error?: any): void {
        const timestamp = new Date().toISOString();
        const errorStack = error instanceof Error ? error.stack : String(error);
        const logMessage = `[${timestamp}] ❌ ${message}${errorStack ? '\n' + errorStack : ''}`;
        console.error(message, error);
        this.logStream.write(logMessage + '\n');
    }

    private logDetail(detail: string): void {
        this.logStream.write(detail + '\n');
    }

    public async start(options: { syncCards?: boolean; syncRulings?: boolean; startFromCard?: number } = {}) {
        const { syncCards = true, syncRulings = true, startFromCard } = options;
        const bulk = new ScryfallBulkDataClient();

        this.log("Starting bulk data sync (cards + rulings)...");

        try {
            // Synchroniser d'abord les cartes
            if (syncCards) {
                await this.syncCards(bulk, startFromCard);
            } else {
                this.log("⏭️ Skipping cards synchronization");
            }
            
            // Puis synchroniser les rulings
            if (syncRulings) {
                await this.syncRulings(bulk);
            } else {
                this.log("⏭️ Skipping rulings synchronization");
            }

            this.log("✅ Sync completed successfully");
        } catch (error) {
            this.logError("Fatal error during sync:", error);
            throw error;
        } finally {
            this.logStream.end();
        }
    }

    private async syncCards(bulk: ScryfallBulkDataClient, startFromCard: number = 0) {
        this.log("Starting bulk card sync...");
        if (startFromCard > 0) {
            this.log(`⏪ Resuming from card #${startFromCard}...`);
        }

        let totalProcessed = 0;
        let totalErrors = 0;
        let batchErrors: { batchIndex: number; error: string; cardName?: string }[] = [];
        let currentCardIndex = 0;
        let shouldSkip = startFromCard > 0;

        const startTime = Date.now();

        const processingOptions: StreamProcessingOptions<CoreCard> = {
            batchSize: 500, // Augmenté de 100 à 500 pour de meilleures performances
            onItem: async (card: CoreCard, index: number) => {
                // Skip les cartes avant le point de départ
                if (shouldSkip && index < startFromCard) {
                    currentCardIndex++;
                    if (index % 10000 === 0) {
                        this.log(`⏭️ Skipping card ${index}/${startFromCard}...`);
                    }
                    return;
                } else if (shouldSkip && index === startFromCard) {
                    shouldSkip = false;
                    this.log(`✅ Resuming at card #${startFromCard}`);
                }
                currentCardIndex++;
            },
            onBatch: async (cards: CoreCard[], batchIndex: number) => {
                // Filter pour ne garder que les cartes à traiter
                let cardsToProcess = cards;
                if (startFromCard > 0 && shouldSkip) {
                    cardsToProcess = cards.filter((_, idx) => {
                        const absoluteIndex = currentCardIndex - cards.length + idx;
                        return absoluteIndex >= startFromCard;
                    });
                }

                if (cardsToProcess.length === 0) {
                    return; // Skip ce batch s'il n'y a rien à traiter
                }

                const batchStartTime = Date.now();
                this.log(`Processing batch ${batchIndex + 1} with ${cardsToProcess.length} cards...`);

                try {
                    await this.upsertCardsBatch(cardsToProcess);
                    totalProcessed += cardsToProcess.length;
                    const batchTime = Date.now() - batchStartTime;
                    const totalTime = Date.now() - startTime;
                    const avgTimePerBatch = totalTime / (batchIndex + 1);

                    this.log(`Batch ${batchIndex + 1} completed in ${batchTime}ms. Total: ${totalProcessed} cards, ${totalErrors} errors`);
                    this.log(`Average batch time: ${Math.round(avgTimePerBatch)}ms`);
                } catch (error) {
                    totalErrors++;
                    const errorMessage = error instanceof Error ? error.message : String(error);
                    batchErrors.push({
                        batchIndex: batchIndex + 1,
                        error: errorMessage,
                        cardName: cards[0]?.name
                    });
                    this.logError(`Batch ${batchIndex + 1} failed:`, errorMessage);
                    // Continue with next batch (non-blocking)
                }
            },
            onError: (error: Error, item?: any, index?: number) => {
                totalErrors++;
                this.logError(`Error processing item at index ${index}:`, error.message);
                if (item) {
                    this.log("Problematic item: " + (item.name || item.id || "Unknown"));
                }
            }
        };

        try {
            const metadata = await bulk.downloadBulkDataStream("all_cards", processingOptions);
            const totalTime = Date.now() - startTime;

            this.log("=".repeat(50));
            this.log("BULK SYNC COMPLETED");
            this.log("=".repeat(50));
            this.log(`📊 Total items processed: ${metadata.totalItems}`);
            this.log(`✅ Successfully processed: ${totalProcessed}`);
            this.log(`❌ Total errors: ${totalErrors}`);
            this.log(`⏱️  Total time: ${Math.round(totalTime / 1000)}s`);
            this.log(`🚀 Average speed: ${Math.round(metadata.totalItems / (totalTime / 1000))} items/sec`);
            this.log(`📁 File size: ${Math.round(metadata.fileSize / (1024 * 1024))}MB`);
            this.log(`📅 Last updated: ${metadata.lastUpdated}`);

            if (batchErrors.length > 0) {
                this.log("\n❌ BATCH ERRORS SUMMARY:");
                batchErrors.forEach(err => {
                    this.logDetail(`  - Batch ${err.batchIndex}: ${err.error} ${err.cardName ? `(first card: ${err.cardName})` : ''}`);
                });
            }

            if (metadata.stats) {
                this.log("\n📈 PROCESSING STATS:");
                Object.entries(metadata.stats).forEach(([key, count]) => {
                    this.logDetail(`  - ${key}: ${count}`);
                });
            }

            this.log("=".repeat(50));

            return metadata;
        } catch (error) {
            const totalTime = Date.now() - startTime;
            this.logError("=".repeat(50));
            this.logError("BULK CARD SYNC FAILED");
            this.logError("=".repeat(50));
            this.logError(`❌ Failed after ${Math.round(totalTime / 1000)}s`);
            this.logError(`📊 Processed ${totalProcessed} cards before failure`, error);
            this.logError("=".repeat(50));
            throw error;
        }
    }

    private async syncRulings(bulk: ScryfallBulkDataClient) {
        this.log("Starting bulk rulings sync...");

        let totalProcessed = 0;
        let totalErrors = 0;
        let batchErrors: { batchIndex: number; error: string }[] = [];

        const startTime = Date.now();

        const processingOptions = {
            batchSize: 1000, // Plus grand batch pour les rulings qui sont plus simples
            onBatch: async (rulings: Ruling[], batchIndex: number) => {
                const batchStartTime = Date.now();
                this.log(`Processing rulings batch ${batchIndex + 1} with ${rulings.length} rulings...`);

                try {
                    await this.upsertRulingsBatch(rulings);
                    totalProcessed += rulings.length;
                    const batchTime = Date.now() - batchStartTime;
                    const totalTime = Date.now() - startTime;
                    const avgTimePerBatch = totalTime / (batchIndex + 1);

                    this.log(`Rulings batch ${batchIndex + 1} completed in ${batchTime}ms. Total: ${totalProcessed} rulings, ${totalErrors} errors`);
                    this.log(`Average rulings batch time: ${Math.round(avgTimePerBatch)}ms`);
                } catch (error) {
                    totalErrors++;
                    const errorMessage = error instanceof Error ? error.message : String(error);
                    batchErrors.push({
                        batchIndex: batchIndex + 1,
                        error: errorMessage
                    });
                    this.logError(`Rulings batch ${batchIndex + 1} failed:`, errorMessage);
                }
            },
            onError: (error: Error, item?: any, index?: number) => {
                totalErrors++;
                this.logError(`Error processing ruling at index ${index}:`, error.message);
                if (item) {
                    this.log("Problematic ruling: " + (item.oracle_id || "Unknown"));
                }
            }
        };

        try {
            const metadata = await bulk.downloadBulkDataStream("rulings", processingOptions);
            const totalTime = Date.now() - startTime;

            this.log("=".repeat(50));
            this.log("BULK RULINGS SYNC COMPLETED");
            this.log("=".repeat(50));
            this.log(`📊 Total rulings processed: ${metadata.totalItems}`);
            this.log(`✅ Successfully processed: ${totalProcessed}`);
            this.log(`❌ Total errors: ${totalErrors}`);
            this.log(`⏱️  Total time: ${Math.round(totalTime / 1000)}s`);
            this.log(`🚀 Average speed: ${Math.round(metadata.totalItems / (totalTime / 1000))} rulings/sec`);
            this.log(`📁 File size: ${Math.round(metadata.fileSize / (1024 * 1024))}MB`);
            this.log(`📅 Last updated: ${metadata.lastUpdated}`);

            if (batchErrors.length > 0) {
                this.log("\n❌ RULINGS BATCH ERRORS SUMMARY:");
                batchErrors.forEach(err => {
                    this.logDetail(`  - Batch ${err.batchIndex}: ${err.error}`);
                });
            }

            this.log("=".repeat(50));

            return metadata;
        } catch (error) {
            const totalTime = Date.now() - startTime;
            this.logError("=".repeat(50));
            this.logError("BULK RULINGS SYNC FAILED");
            this.logError("=".repeat(50));
            this.logError(`❌ Failed after ${Math.round(totalTime / 1000)}s`);
            this.logError(`📊 Processed ${totalProcessed} rulings before failure`, error);
            this.logError("=".repeat(50));
            throw error;
        }
    }

    private async upsertCardsBatch(cards: CoreCard[]): Promise<void> {
        let batchSuccesses = 0;
        let batchErrors = 0;
        const errorDetails: Array<{ cardId: string; cardName: string; error: string }> = [];

        this.log(`Processing ${cards.length} cards in optimized batch...`);

        try {
            // 1. First, upsert all sets (can be done in transaction as sets are independent)
            const uniqueSets = new Map<string, { id: string; name: string; type: string; set?: string }>();
            for (const card of cards) {
                if (card.set_id && card.set_name && !uniqueSets.has(card.set_id)) {
                    uniqueSets.set(card.set_id, {
                        id: card.set_id,
                        name: card.set_name,
                        type: card.set_type,
                        set: card.set
                    });
                }
            }

            for (const setData of uniqueSets.values()) {
                try {
                    await Database.prisma.set.upsert({
                        where: { id: setData.id },
                        update: { name: setData.name, type: setData.type, set: setData.set },
                        create: setData
                    });
                } catch (setError) {
                    this.logError(`Failed to upsert set ${setData.id}: ${setError}`);
                }
            }

            // 2. Then, upsert all oracles from cards AND card faces (can be done in transaction as oracles are independent)
            const uniqueOracles = new Map<string, { id: string; text: string }>();
            for (const card of cards) {
                // Add the main card's oracle if it exists
                if (card.oracle_id && !uniqueOracles.has(card.oracle_id)) {
                    uniqueOracles.set(card.oracle_id, {
                        id: card.oracle_id,
                        text: card.oracle_text || ""
                    });
                }
                
                // Add oracles from card faces (important for double-faced cards!)
                if (card.card_faces && card.card_faces.length > 0) {
                    for (const face of card.card_faces) {
                        if (face.oracle_id && !uniqueOracles.has(face.oracle_id)) {
                            uniqueOracles.set(face.oracle_id, {
                                id: face.oracle_id,
                                text: face.oracle_text || ""
                            });
                        }
                    }
                }
            }

            for (const oracleData of uniqueOracles.values()) {
                try {
                    await Database.prisma.oracle.upsert({
                        where: { id: oracleData.id },
                        update: { text: oracleData.text },
                        create: oracleData
                    });
                } catch (oracleError) {
                    this.logError(`Failed to upsert oracle ${oracleData.id}: ${oracleError}`);
                }
            }

            // 3. Process each card individually OUTSIDE of a transaction
            for (const card of cards) {
                if (!card.id || !card.name || !card.set_id || !card.collector_number) {
                    batchErrors++;
                    this.logError(`⚠️ Missing required fields for card: ${card.name || 'unknown'}`);
                    errorDetails.push({
                        cardId: card.id || 'unknown',
                        cardName: card.name || 'unknown',
                        error: 'Missing required fields'
                    });
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
                        legalities: card.legalities ? JSON.parse(JSON.stringify(card.legalities)) : {},
                    };

                    // Delete existing faces for this card if it exists
                    await Database.prisma.face.deleteMany({
                        where: { card_id: card.id }
                    }).catch(() => {}); // Ignore errors if no faces exist

                    // Upsert the card
                    await Database.prisma.card.upsert({
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

                    // Create faces for this card
                    const facesToCreate: any[] = [];
                    if (card.card_faces && card.card_faces.length > 0) {
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
                                keywords: [],
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

                    // Create all faces for this card
                    if (facesToCreate.length > 0) {
                        await Database.prisma.face.createMany({
                            data: facesToCreate
                        });
                    }

                    batchSuccesses++;
                } catch (cardError) {
                    batchErrors++;
                    const errorMessage = cardError instanceof Error ? cardError.message : String(cardError);
                    this.logError(`❌ Failed to upsert card ${card.name}:`, errorMessage);

                    if (cardError instanceof Error) {
                        this.logError(`   Stack:`, cardError.stack);
                    }

                    errorDetails.push({
                        cardId: card.id,
                        cardName: card.name,
                        error: errorMessage
                    });
                }
            }
        } catch (error) {
            batchErrors++;
            this.logError(`Batch processing error:`, error);
        }

        this.log(`\nCards batch completed: ${batchSuccesses} successes, ${batchErrors} errors`);

        // Log error summary if any
        if (errorDetails.length > 0) {
            this.log("\n📋 ERROR DETAILS (first 10):");
            errorDetails.slice(0, 10).forEach((detail, idx) => {
                this.logDetail(`  ${idx + 1}. ${detail.cardName} (${detail.cardId}): ${detail.error}`);
            });
            if (errorDetails.length > 10) {
                this.logDetail(`  ... and ${errorDetails.length - 10} more errors`);
            }
        }
    }

    private async upsertRulingsBatch(rulings: Ruling[]): Promise<void> {
        let batchSuccesses = 0;
        let batchErrors = 0;
        const errorDetails: Array<{ oracleId: string; error: string }> = [];

        this.log(`Processing ${rulings.length} rulings in optimized batch...`);

        // Process each ruling individually OUTSIDE of a transaction to prevent transaction abortion
        for (const ruling of rulings) {
            if (!ruling.oracle_id || !ruling.published_at || !ruling.comment) {
                batchErrors++;
                this.logError(`⚠️ Missing required fields for ruling: oracle_id=${ruling.oracle_id}, published_at=${ruling.published_at}`);
                errorDetails.push({
                    oracleId: ruling.oracle_id || 'unknown',
                    error: 'Missing required fields'
                });
                continue;
            }

            try {
                // Vérifier d'abord si l'oracle existe
                const oracleExists = await Database.prisma.oracle.findUnique({
                    where: { id: ruling.oracle_id },
                    select: { id: true }
                });

                if (!oracleExists) {
                    batchErrors++;
                    this.log(`⚠️ Oracle not found in database: ${ruling.oracle_id}`);
                    errorDetails.push({
                        oracleId: ruling.oracle_id,
                        error: 'Oracle ID not found in database'
                    });
                    continue;
                }

                // Créer un ID unique basé sur oracle_id + published_at + hash du commentaire
                const crypto = require('crypto');
                const hash = crypto.createHash('md5').update(ruling.comment).digest('hex').substring(0, 8);
                const rulingId = `${ruling.oracle_id}_${ruling.published_at}_${hash}`;

                const rulingData = {
                    id: rulingId,
                    oracle_id: ruling.oracle_id,
                    published_at: new Date(ruling.published_at),
                    comment: ruling.comment,
                };

                await Database.prisma.ruling.upsert({
                    where: { id: rulingData.id },
                    update: {
                        comment: rulingData.comment,
                        updated_at: new Date(),
                    },
                    create: rulingData,
                });
                batchSuccesses++;
            } catch (rulingError) {
                batchErrors++;
                const errorMessage = rulingError instanceof Error ? rulingError.message : String(rulingError);
                this.logError(`❌ Failed to upsert ruling for oracle ${ruling.oracle_id}:`);
                this.logError(`   Error: ${errorMessage}`);
                
                errorDetails.push({
                    oracleId: ruling.oracle_id,
                    error: errorMessage
                });
            }
        }

        this.log(`\nRulings batch completed: ${batchSuccesses} successes, ${batchErrors} errors`);
        
        // Log error summary if any
        if (errorDetails.length > 0) {
            this.log("\n📋 ERROR DETAILS (first 10):");
            errorDetails.slice(0, 10).forEach((detail, idx) => {
                this.logDetail(`  ${idx + 1}. Oracle ${detail.oracleId}: ${detail.error}`);
            });
            if (errorDetails.length > 10) {
                this.logDetail(`  ... and ${errorDetails.length - 10} more errors`);
            }
        }
    }

    // Anciennes méthodes supprimées - remplacées par le traitement en batch optimisé
}