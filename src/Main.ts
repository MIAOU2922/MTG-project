import EventEmitter from "node:events";
import express, { Application, Request, Response, NextFunction } from 'express';
import { router } from "@routes/route";
import cors from 'cors';
import * as cron from 'cron';
import Instance from '@/database/Instance';
import ScryFallSync from '@/sync/index';

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
        // Planifier la sync des cartes tous les jours à 1h du matin
        // Cron format: minute hour day-of-month month day-of-week
        // "0 1 * * *" = 01:00 AM tous les jours
        const syncJob = new cron.CronJob('0 1 * * *', async () => {
            if (this.syncInProgress) {
                console.log('⏭️  Sync already in progress, skipping scheduled sync');
                return;
            }

            console.log('🔄 Starting scheduled daily sync at 1:00 AM...');
            this.syncInProgress = true;

            try {
                const sync = new ScryFallSync(5); // 5 cartes/rulings en parallèle
                await sync.start({ syncCards: true, syncRulings: true });
                console.log('✅ Scheduled daily sync completed successfully');
            } catch (error) {
                console.error('❌ Error during scheduled daily sync:', error);
            } finally {
                this.syncInProgress = false;
            }
        });

        syncJob.start();
        console.log('📅 Daily sync scheduler started (runs at 1:00 AM every day)');
    }
}