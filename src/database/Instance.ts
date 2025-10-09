import { Instance as IInstance } from "prisma";
import Database from "@/database/Database";
import User from "@/database/User";
import fs from "fs";
import path from "path";
import https from "https";

export default class Instance implements IInstance {
    public static MAX_INSTANCES = 64;
    public readonly id: number;
    public readonly created_at: Date;
    public readonly last_seen_at: Date;
    public card_ids: string[];
    public user_ids: number[];

    // File d'attente pour limiter les téléchargements simultanés
    private static downloadQueue: string[] = [];
    private static activeDownloads = 0;
    private static readonly MAX_CONCURRENT_DOWNLOADS = 3;

    // Système de déclenchement différé pour les téléchargements récents
    private static recentDownloadTimeout: NodeJS.Timeout | null = null;
    private static readonly RECENT_DOWNLOAD_DELAY = 30000; // 30 secondes de délai

    constructor(data: IInstance) {
        this.id = data.id;
        this.created_at = data.created_at;
        this.last_seen_at = data.last_seen_at;
        this.card_ids = (data as any).card_ids || [];
        this.user_ids = (data as any).user_ids || [];
    }

    public static async findById(id: number): Promise<Instance | null> {
        const instance = await Database.prisma.instance.findUnique({ where: { id } });
        return instance ? new Instance(instance) : null;
    }

    public static async create(): Promise<Instance> {
        // Trouver le prochain ID disponible
        const existingInstances = await Database.prisma.instance.findMany({
            select: { id: true },
            orderBy: { id: 'asc' }
        });

        const usedIds = new Set(existingInstances.map(inst => inst.id));
        let nextId = 0;

        // Trouver le premier ID non utilisé
        while (usedIds.has(nextId)) {
            nextId++;
        }

        const instance = await Database.prisma.instance.create({
            data: { id: nextId }
        });
        return new Instance(instance);
    }

    public static async findAvailable(maxPlayersPerInstance: number = 4): Promise<Instance | null> {
        // Trouver une instance qui n'a pas atteint le nombre maximum de joueurs
        const instances = await Database.prisma.instance.findMany();

        // Chercher une instance avec moins de maxPlayersPerInstance joueurs
        for (const instanceData of instances) {
            const instance = new Instance(instanceData);
            if (instance.user_ids.length < maxPlayersPerInstance) {
                return instance;
            }
        }

        return null; // Aucune instance disponible
    }

    public async getUsers(): Promise<User[]> {
        const users = await Database.prisma.user.findMany({
            where: { id: { in: this.user_ids } }
        });
        return users.map(user => new User(user));
    }

    public async addUser(userId: number): Promise<void> {
        if (!this.user_ids.includes(userId)) {
            const newUserIds = [...this.user_ids, userId];
            await Database.prisma.instance.update({
                where: { id: this.id },
                data: { user_ids: newUserIds } as any
            });
            this.user_ids = newUserIds;
        }
    }

    public async removeUser(userId: number): Promise<void> {
        const newUserIds = this.user_ids.filter(id => id !== userId);
        await Database.prisma.instance.update({
            where: { id: this.id },
            data: { user_ids: newUserIds } as any
        });
        this.user_ids = newUserIds;
    }

    public hasUser(userId: number): boolean {
        return this.user_ids.includes(userId);
    }

    public getUserCount(): number {
        return this.user_ids.length;
    }

    public async updateLastSeen(): Promise<void> {
        await Database.prisma.instance.update({
            where: { id: this.id },
            data: { last_seen_at: new Date() }
        });
    }

    public async addCard(cardId: string): Promise<void> {
        const currentCardIds = this.card_ids || [];
        if (!currentCardIds.includes(cardId)) {
            await Database.prisma.instance.update({
                where: { id: this.id },
                data: { card_ids: [...currentCardIds, cardId] } as any
            });
            this.card_ids = [...currentCardIds, cardId];

            // Déclencher le téléchargement de l'image individuelle en arrière-plan
            this.downloadCardImage(cardId).catch(error => {
                console.error(`Failed to download image for card ${cardId}:`, error);
            });

            // Déclencher le téléchargement des images récentes après un délai
            this.scheduleRecentImagesDownload();
        }
    }

    public async removeCard(cardId: string): Promise<void> {
        const currentCardIds = this.card_ids || [];
        const newCardIds = currentCardIds.filter(id => id !== cardId);
        await Database.prisma.instance.update({
            where: { id: this.id },
            data: { card_ids: newCardIds } as any
        });
        this.card_ids = newCardIds;
    }

    public async clearCards(): Promise<void> {
        await Database.prisma.instance.update({
            where: { id: this.id },
            data: { card_ids: [] } as any
        });
        this.card_ids = [];
    }

    public hasCard(cardId: string): boolean {
        return this.card_ids.includes(cardId);
    }

    public getCardCount(): number {
        return this.card_ids.length;
    }

    public static async createWithRotation(user: User): Promise<Instance> {
        // Find the next available instance ID (0-63) using rotation
        let nextId = 0;
        
        // Get all existing instances and find the next ID
        const existingInstances = await Database.prisma.instance.findMany({
            orderBy: { id: 'asc' }
        });
        
        const usedIds = new Set(existingInstances.map(instance => instance.id));
        
        // Find first unused ID in range 0-63
        for (let i = 0; i <= Instance.MAX_INSTANCES - 1; i++) 
            if (!usedIds.has(i)) {
                nextId = i;
                break;
            }
        
        // If all IDs are used (64 instances), reuse the oldest one (rotation)
        if (usedIds.size >= Instance.MAX_INSTANCES) {
            // Find the oldest instance by created_at timestamp
            const oldestInstance = await Database.prisma.instance.findFirst({
                orderBy: { created_at: 'asc' }
            });

            if (oldestInstance) {
                nextId = oldestInstance.id;

                // Reset card_ids and user_ids, and update last_seen_at
                await Database.prisma.instance.update({
                    where: { id: nextId },
                    data: {
                        card_ids: [],
                        user_ids: [],
                        last_seen_at: new Date()
                    } as any
                });
            }
        }
        
        // Create new instance
        const instance = await Database.prisma.instance.create({
            data: { id: nextId }
        });
        
        return new Instance(instance);
    }

    /**
     * Télécharge une image de carte si elle n'existe pas localement
     */
    private async downloadCardImage(cardId: string): Promise<void> {
        // Ajouter à la file d'attente
        Instance.downloadQueue.push(cardId);
        await this.processDownloadQueue();
    }

    /**
     * Programme un téléchargement différé des images des instances récentes
     */
    private scheduleRecentImagesDownload(): void {
        // Annuler le timeout existant s'il y en a un
        if (Instance.recentDownloadTimeout) {
            clearTimeout(Instance.recentDownloadTimeout);
        }

        // Programmer un nouveau téléchargement dans 30 secondes
        Instance.recentDownloadTimeout = setTimeout(async () => {
            try {
                console.log('🔄 Triggered download of recent instance images (background)...');
                await Instance.downloadRecentInstanceImages();
                console.log('✅ Background download of recent images completed');
            } catch (error) {
                console.error('❌ Error during background download of recent images:', error);
            } finally {
                Instance.recentDownloadTimeout = null;
            }
        }, Instance.RECENT_DOWNLOAD_DELAY);
    }

    /**
     * Traite la file d'attente des téléchargements avec limitation du nombre de requêtes simultanées
     */
    private async processDownloadQueue(): Promise<void> {
        if (Instance.activeDownloads >= Instance.MAX_CONCURRENT_DOWNLOADS) {
            return; // Attendre qu'un téléchargement se termine
        }

        const cardId = Instance.downloadQueue.shift();
        if (!cardId) return;

        Instance.activeDownloads++;

        try {
            await this.downloadSingleCardImage(cardId);
        } finally {
            Instance.activeDownloads--;
            // Traiter le prochain élément de la file
            if (Instance.downloadQueue.length > 0) {
                setImmediate(() => this.processDownloadQueue());
            }
        }
    }

    /**
     * Télécharge une seule image de carte
     */
    private async downloadSingleCardImage(cardId: string): Promise<void> {
        const imageDir = path.join(process.cwd(), 'images', 'cards');
        const imagePath = path.join(imageDir, `${cardId}.jpg`);

        // Vérifier si l'image existe déjà
        if (fs.existsSync(imagePath)) {
            return;
        }

        try {
            // Créer le dossier s'il n'existe pas
            if (!fs.existsSync(imageDir)) {
                fs.mkdirSync(imageDir, { recursive: true });
            }

            // Récupérer l'URL de l'image depuis la base de données
            const face = await Database.prisma.face.findFirst({
                where: { card_id: cardId },
                select: { image_url: true }
            });

            if (!face?.image_url) {
                console.error(`No image URL found for card ${cardId}`);
                return;
            }

            // Télécharger l'image
            const imageBuffer = await Instance.downloadImageStatic(face.image_url);

            // Sauvegarder l'image localement
            fs.writeFileSync(imagePath, imageBuffer);

            console.log(`Downloaded image for card ${cardId}`);
        } catch (error: any) {
            console.error(`Error downloading image for card ${cardId}:`, error.message);
        }
    }

    /**
     * Télécharge toutes les images des cartes des instances actives récemment (< 24h)
     */
    public static async downloadRecentInstanceImages(): Promise<void> {
        console.log('🔄 Starting download of images for recent instances (< 24h)...');

        const twentyFourHoursAgo = new Date(Date.now() - 24 * 60 * 60 * 1000);

        // Trouver toutes les instances avec des interactions récentes
        const recentInstances = await Database.prisma.instance.findMany({
            where: {
                last_seen_at: {
                    gte: twentyFourHoursAgo
                },
                card_ids: {
                    isEmpty: false // Seulement les instances qui ont des cartes
                }
            },
            select: {
                id: true,
                card_ids: true,
                last_seen_at: true
            }
        });

        if (recentInstances.length === 0) {
            console.log('ℹ️ No recent instances found with cards');
            return;
        }

        console.log(`📊 Found ${recentInstances.length} recent instances with cards`);

        // Collecter tous les card_ids uniques
        const allCardIds = new Set<string>();
        for (const instance of recentInstances) {
            const cardIds = (instance as any).card_ids || [];
            cardIds.forEach((cardId: string) => allCardIds.add(cardId));
        }

        console.log(`🃏 Found ${allCardIds.size} unique cards to check for images`);

        // Télécharger les images manquantes
        const imageDir = path.join(process.cwd(), 'images', 'cards');
        if (!fs.existsSync(imageDir)) {
            fs.mkdirSync(imageDir, { recursive: true });
        }

        let downloaded = 0;
        let skipped = 0;
        let errors = 0;

        // Traiter les cartes par lots pour éviter de surcharger la base de données
        const cardIdsArray = Array.from(allCardIds);
        const batchSize = 50;

        for (let i = 0; i < cardIdsArray.length; i += batchSize) {
            const batch = cardIdsArray.slice(i, i + batchSize);
            console.log(`Processing batch ${Math.floor(i / batchSize) + 1}/${Math.ceil(cardIdsArray.length / batchSize)} (${batch.length} cards)...`);

            // Récupérer les URLs des images pour ce lot
            const faces = await Database.prisma.face.findMany({
                where: {
                    card_id: { in: batch },
                    image_url: { not: '' }
                },
                select: {
                    card_id: true,
                    image_url: true
                }
            });

            // Créer une map card_id -> image_url
            const imageUrls = new Map<string, string>();
            faces.forEach(face => {
                if (face.image_url) {
                    imageUrls.set(face.card_id, face.image_url);
                }
            });

            // Télécharger les images manquantes pour ce lot
            const downloadPromises = batch.map(async (cardId) => {
                const imagePath = path.join(imageDir, `${cardId}.jpg`);

                // Vérifier si l'image existe déjà
                if (fs.existsSync(imagePath)) {
                    skipped++;
                    return;
                }

                const imageUrl = imageUrls.get(cardId);
                if (!imageUrl) {
                    console.warn(`No image URL found for card ${cardId}`);
                    errors++;
                    return;
                }

                try {
                    const imageBuffer = await this.downloadImageStatic(imageUrl);
                    fs.writeFileSync(imagePath, imageBuffer);
                    downloaded++;
                    console.log(`✅ Downloaded image for card ${cardId}`);
                } catch (error: any) {
                    console.error(`❌ Failed to download image for card ${cardId}:`, error.message);
                    errors++;
                }
            });

            // Attendre que tous les téléchargements du lot soient terminés
            await Promise.allSettled(downloadPromises);

            // Petite pause entre les lots pour ne pas surcharger
            if (i + batchSize < cardIdsArray.length) {
                await new Promise(resolve => setTimeout(resolve, 100));
            }
        }

        console.log('🎉 Image download completed!');
        console.log(`📊 Summary: ${downloaded} downloaded, ${skipped} skipped, ${errors} errors`);
    }

    /**
     * Nettoie les images des instances anciennes (> 24h) pour libérer de l'espace
     */
    public static async cleanupOldInstanceImages(): Promise<void> {
        console.log('🧹 Starting cleanup of old instance images (> 24h)...');

        const twentyFourHoursAgo = new Date(Date.now() - 24 * 60 * 60 * 1000);

        // Trouver toutes les instances avec des interactions récentes (< 24h)
        const recentInstances = await Database.prisma.instance.findMany({
            where: {
                last_seen_at: {
                    gte: twentyFourHoursAgo
                },
                card_ids: {
                    isEmpty: false
                }
            },
            select: {
                card_ids: true
            }
        });

        // Collecter tous les card_ids des instances récentes
        const recentCardIds = new Set<string>();
        for (const instance of recentInstances) {
            const cardIds = (instance as any).card_ids || [];
            cardIds.forEach((cardId: string) => recentCardIds.add(cardId));
        }

        console.log(`📊 Found ${recentCardIds.size} cards used in recent instances (< 24h)`);

        // Lister toutes les images présentes dans le dossier
        const imageDir = path.join(process.cwd(), 'images', 'cards');
        if (!fs.existsSync(imageDir)) {
            console.log('ℹ️ Image directory does not exist');
            return;
        }

        const imageFiles = fs.readdirSync(imageDir).filter(file => file.endsWith('.jpg'));
        console.log(`📁 Found ${imageFiles.length} image files in directory`);

        // Identifier les images à supprimer (celles qui ne sont pas utilisées récemment)
        const imagesToDelete = imageFiles.filter(fileName => {
            // Extraire le card_id du nom de fichier (sans l'extension .jpg)
            const cardId = fileName.replace('.jpg', '');
            return !recentCardIds.has(cardId);
        });

        console.log(`🗑️ Will delete ${imagesToDelete.length} unused images`);

        if (imagesToDelete.length === 0) {
            console.log('✅ No unused images to delete');
            return;
        }

        // Supprimer les images inutilisées
        let deleted = 0;
        let errors = 0;

        for (const imageFile of imagesToDelete) {
            try {
                const imagePath = path.join(imageDir, imageFile);
                fs.unlinkSync(imagePath);
                deleted++;
            } catch (error) {
                console.error(`❌ Error deleting image ${imageFile}:`, error);
                errors++;
            }
        }

        console.log('🎉 Cleanup completed!');
        console.log(`📊 Summary: ${deleted} images deleted, ${errors} errors`);
    }

    /**
     * Télécharge une image depuis une URL (version statique)
     */
    private static async downloadImageStatic(url: string): Promise<Buffer> {
        return new Promise((resolve, reject) => {
            https.get(url, (res) => {
                if (res.statusCode !== 200) {
                    reject(new Error(`HTTP ${res.statusCode}`));
                    return;
                }

                const chunks: Buffer[] = [];
                res.on('data', (chunk) => chunks.push(chunk));
                res.on('end', () => resolve(Buffer.concat(chunks)));
                res.on('error', reject);
            }).on('error', reject);
        });
    }
}