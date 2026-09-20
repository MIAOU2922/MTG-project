import EventEmitter from "node:events";
import express, { Application, Request, Response, NextFunction } from 'express';
import { router } from "@routes/route";
import cors from 'cors';
import * as cron from 'cron';
import Instance from '@/database/Instance';
import Config from '@/database/Config';
import ScryFallSync from '@/sync/index';
import { ScryfallBulkDataClient } from '@/sync/bulk-client';
import { BulkData } from '@/sync/types';

/** Clés Config pour mémoriser le dernier bulk synchronisé */
const LAST_CARDS_SYNC_KEY = 'sync:last_cards_bulk_updated_at';
const LAST_RULINGS_SYNC_KEY = 'sync:last_rulings_bulk_updated_at';

export default class Main extends EventEmitter {
    private static _instance: Main;
    private app: Application;
    private port: number;
    private syncInProgress: boolean = false;

    private constructor() {
        super();
        this.app = express();
        this.port = process.env.PORT ? parseInt(process.env.PORT) : 3000;
        this.setupMiddleware();
        this.setupRoutes();
        this.setupImageDownloadScheduler();
        this.setupSyncScheduler();
    }

    public static getInstance(): Main {
        if (!Main._instance)
            Main._instance = new Main();
        return Main._instance;
    }

    private setupMiddleware(): void {
        this.app.use(cors());
        this.app.use(express.json());
        this.app.use(express.urlencoded({ extended: true }));
        this.app.use((req: Request, res: Response, next: NextFunction) => {
            console.log(`${new Date().toISOString()} - ${req.method} ${req.url}`);
            next();
        });
    }

    private setupRoutes(): void {
        this.app.use(router);
        this.app.use((req: Request, res: Response) => {
            res.status(404).json({
                error: 'Not Found',
                message: `Route ${req.originalUrl} not found`
            });
        });
    }

    public start(): void {
        this.app.listen(this.port, () => {
            console.log(`🚀 Server is running on http://localhost:${this.port}`);
            console.log(`📊 Health check available at http://localhost:${this.port}/h`);
            console.log(`🔗 API routes available at http://localhost:${this.port}/a`);
            this.emit('server:started', { port: this.port });
        });
    }

    public getApp(): Application {
        return this.app;
    }

    private setupImageDownloadScheduler(): void {
        // Téléchargement initial au démarrage du serveur
        console.log('🚀 Running initial image download for recent instances...');
        Instance.downloadRecentInstanceImages().catch(error => {
            console.error('❌ Error during initial image download:', error);
        });

        // Planifier un nettoyage périodique des images et atlas anciens (toutes les heures)
        const cleanupJob = new cron.CronJob('0 * * * *', async () => {
            console.log('🧹 Running periodic cleanup of old data (> 48h)...');
            try {
                await Instance.cleanupOldInstanceImages();
                console.log('✅ Periodic cleanup completed');
            } catch (error) {
                console.error('❌ Error during periodic cleanup:', error);
            }
        });

        cleanupJob.start();
        console.log('📅 Image and atlas cleanup scheduler started (runs every hour, 48h threshold)');
    }

    private setupSyncScheduler(): void {
        // Mise à jour hebdomadaire via les bulks Scryfall : tous les lundis à 1h du matin (heure locale)
        // Ordre : d'abord les cartes, puis les rulings
        const syncJob = new cron.CronJob('0 1 * * 1', async () => {
            if (this.syncInProgress) {
                console.log('⏭️  Sync already in progress, skipping scheduled sync');
                return;
            }

            console.log('🔄 Starting scheduled weekly sync (Monday 1:00 AM Europe/Paris)...');
            this.syncInProgress = true;

            try {
                const bulk = new ScryfallBulkDataClient();

                // 1) Cartes
                try {
                    const cardsInfo = await bulk.getLatestBulkDataInfo('all_cards');
                    if (await this.needsSync('all_cards', cardsInfo, LAST_CARDS_SYNC_KEY)) {
                        await this.runSync({ syncCards: true, syncRulings: false });
                        if (cardsInfo) {
                            await Config.set(LAST_CARDS_SYNC_KEY, String(cardsInfo.updated_at));
                            console.log(`💾 Saved last cards sync timestamp: ${cardsInfo.updated_at}`);
                        }
                    }
                } catch (error) {
                    console.error('❌ Cards sync failed:', error);
                }

                // 2) Rulings (tentés même si la synchro cartes a échoué)
                try {
                    const rulingsInfo = await bulk.getLatestBulkDataInfo('rulings');
                    if (await this.needsSync('rulings', rulingsInfo, LAST_RULINGS_SYNC_KEY)) {
                        await this.runSync({ syncCards: false, syncRulings: true });
                        if (rulingsInfo) {
                            await Config.set(LAST_RULINGS_SYNC_KEY, String(rulingsInfo.updated_at));
                            console.log(`💾 Saved last rulings sync timestamp: ${rulingsInfo.updated_at}`);
                        }
                    }
                } catch (error) {
                    console.error('❌ Rulings sync failed:', error);
                }

                console.log('✅ Scheduled weekly sync finished');
            } finally {
                this.syncInProgress = false;
            }
        }, null, false, 'Europe/Paris'); // start=false, timezone Europe/Paris (1h du matin locale)

        syncJob.start();
        console.log('📅 Weekly sync scheduler started (runs every Monday at 1:00 AM Europe/Paris, cards then rulings)');
    }

    /** Lance une synchro Scryfall avec la concurrence par défaut */
    private async runSync(options: { syncCards: boolean; syncRulings: boolean }): Promise<void> {
        const sync = new ScryFallSync(5);
        await sync.start(options);
    }

    /**
     * Détermine si un bulk doit être synchronisé :
     * - true si le bulk est plus récent que la dernière synchro mémorisée
     * - true si on ne peut pas vérifier (API indisponible) pour ne pas sauter de mise à jour
     */
    private async needsSync(type: 'all_cards' | 'rulings', info: BulkData | null, configKey: string): Promise<boolean> {
        if (!info) {
            console.warn(`⚠️  Unable to fetch bulk info for ${type}, running sync anyway`);
            return true;
        }

        const cfg = await Config.get(configKey, '');
        if (cfg.value) {
            const lastSync = new Date(cfg.value);
            if (!isNaN(lastSync.getTime()) && new Date(info.updated_at) <= lastSync) {
                console.log(`⏭️  ${type} bulk unchanged since last sync (${info.updated_at}), skipping`);
                return false;
            }
        }

        console.log(`🆕 ${type} bulk updated (${info.updated_at}), syncing...`);
        return true;
    }
}