import EventEmitter from "node:events";
import express, { Application, Request, Response, NextFunction } from 'express';
import { router } from "@routes/route";
import cors from 'cors';
import * as cron from 'cron';
import Instance from '@/database/Instance';

export default class Main extends EventEmitter {
    private static _instance: Main;
    private app: Application;
    private port: number;

    private constructor() {
        super();
        this.app = express();
        this.port = process.env.PORT ? parseInt(process.env.PORT) : 3000;
        this.setupMiddleware();
        this.setupRoutes();
        this.setupImageDownloadScheduler();
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

        // Planifier un nettoyage périodique des images anciennes (toutes les heures)
        const cleanupJob = new cron.CronJob('0 * * * *', async () => {
            console.log('🧹 Running periodic cleanup of old instance images (> 24h)...');
            try {
                await Instance.cleanupOldInstanceImages();
                console.log('✅ Periodic cleanup completed');
            } catch (error) {
                console.error('❌ Error during periodic cleanup:', error);
            }
        });

        cleanupJob.start();
        console.log('📅 Image cleanup scheduler started (runs every hour)');
    }
}