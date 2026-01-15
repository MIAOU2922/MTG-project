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

    // Dossiers de cache
    private static readonly CARDS_DIR = path.join(process.cwd(), 'images', 'cards');
    private static readonly ATLAS_DIR = path.join(process.cwd(), 'images', 'atlas');
    private static readonly CACHE_THRESHOLD_HOURS = 48; // 48 heures pour le cache

    constructor(data: IInstance) {
        this.id = data.id;
        this.created_at = data.created_at;
        this.last_seen_at = data.last_seen_at;
        this.card_ids = (data as any).card_ids || [];
        this.user_ids = (data as any).user_ids || [];
    }

    /**
     * Initialise les dossiers de cache nécessaires
     */
    private static ensureCacheDirectories(): void {
        if (!fs.existsSync(this.CARDS_DIR)) {
            fs.mkdirSync(this.CARDS_DIR, { recursive: true });
        }
        if (!fs.existsSync(this.ATLAS_DIR)) {
            fs.mkdirSync(this.ATLAS_DIR, { recursive: true });
        }
    }

    /**
     * Initialise le dossier de cache pour une instance spécifique
     */
    private static ensureInstanceAtlasDirectory(instanceId: number): string {
        this.ensureCacheDirectories();
        const instanceDir = path.join(this.ATLAS_DIR, `instance_${instanceId}`);
        if (!fs.existsSync(instanceDir)) {
            fs.mkdirSync(instanceDir, { recursive: true });
        }
        return instanceDir;
    }

    /**
     * Génère le chemin du fichier atlas pour une instance et un batch donné
     * Inclut le nombre de cartes dans le nom pour détecter les changements
     */
    public static getAtlasPath(instanceId: number, batchIndex: number, cardCount: number): string {
        const instanceDir = this.ensureInstanceAtlasDirectory(instanceId);
        return path.join(instanceDir, `batch_${batchIndex}_n${cardCount}.png`);
    }

    /**
     * Vérifie si un atlas existe en cache pour le nombre de cartes donné
     */
    public static hasAtlasCache(instanceId: number, batchIndex: number, cardCount: number): boolean {
        const atlasPath = this.getAtlasPath(instanceId, batchIndex, cardCount);
        return fs.existsSync(atlasPath);
    }

    /**
     * Lit un atlas depuis le cache
     */
    public static readAtlasCache(instanceId: number, batchIndex: number, cardCount: number): Buffer | null {
        if (!this.hasAtlasCache(instanceId, batchIndex, cardCount)) {
            return null;
        }
        const atlasPath = this.getAtlasPath(instanceId, batchIndex, cardCount);
        return fs.readFileSync(atlasPath);
    }

    /**
     * Sauvegarde un atlas dans le cache
     * Nettoie d'abord les anciens atlas du même batch (avec un nombre de cartes différent)
     */
    public static saveAtlasCache(instanceId: number, batchIndex: number, cardCount: number, imageBuffer: Buffer): void {
        // Nettoyer les anciens atlas du même batch avec un nombre de cartes différent
        const instanceDir = this.ensureInstanceAtlasDirectory(instanceId);
        const files = fs.readdirSync(instanceDir);
        const oldAtlasPattern = new RegExp(`^batch_${batchIndex}_n\\d+\\.png$`);
        
        for (const file of files) {
            if (oldAtlasPattern.test(file) && !file.includes(`_n${cardCount}.png`)) {
                try {
                    fs.unlinkSync(path.join(instanceDir, file));
                    console.log(`🗑️ Removed outdated atlas: ${file}`);
                } catch (error) {
                    console.error(`❌ Error deleting outdated atlas ${file}:`, error);
                }
            }
        }
        
        // Sauvegarder le nouveau atlas
        const atlasPath = this.getAtlasPath(instanceId, batchIndex, cardCount);
        fs.writeFileSync(atlasPath, imageBuffer);
        console.log(`💾 Saved atlas cache: instance ${instanceId}, batch ${batchIndex}, ${cardCount} cards`);
    }

    /**
     * Supprime tous les atlas d'une instance donnée
     */
    public static clearInstanceAtlasCache(instanceId: number): void {
        const instanceDir = path.join(this.ATLAS_DIR, `instance_${instanceId}`);
        
        if (!fs.existsSync(instanceDir)) {
            return; // Aucun dossier à supprimer
        }
        
        try {
            // Supprimer tous les fichiers dans le dossier de l'instance
            const files = fs.readdirSync(instanceDir);
            for (const file of files) {
                fs.unlinkSync(path.join(instanceDir, file));
            }
            
            // Supprimer le dossier lui-même
            fs.rmdirSync(instanceDir);
            console.log(`🗑️ Cleared atlas cache directory for instance ${instanceId} (${files.length} files)`);
        } catch (error) {
            console.error(`❌ Error clearing atlas cache for instance ${instanceId}:`, error);
        }
    }

    /**
     * Remove a user from all instances in the database
     * This ensures a user can only be in one instance at a time
     */
    public static async removeUserFromAllInstances(userId: number): Promise<void> {
        // Find all instances containing this user
        const instances = await Database.prisma.instance.findMany({
            where: { user_ids: { has: userId } }
        });
        
        // Remove user from each instance
        for (const instanceData of instances) {
            const instance = new Instance(instanceData);
            await instance.removeUser(userId);
        }
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
        // Remove user from all other instances first to ensure exclusivity
        await Instance.removeUserFromAllInstances(userId);
        
        // Add user to this instance
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

            // Note: L'atlas sera automatiquement régénéré à la demande si le nombre de cartes a changé

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
        
        // Note: L'atlas sera automatiquement régénéré à la demande si le nombre de cartes a changé
    }

    public async clearCards(): Promise<void> {
        await Database.prisma.instance.update({
            where: { id: this.id },
            data: { card_ids: [] } as any
        });
        this.card_ids = [];
        
        // Note: Les atlas vides seront nettoyés par le cleanup automatique
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
        for (let i = 0; i <= Instance.MAX_INSTANCES - 1; i++) {
            if (!usedIds.has(i)) {
                nextId = i;
                break;
            }
        }
        
        // If all IDs are used (64 instances), reuse the least recently seen one (rotation)
        if (usedIds.size >= Instance.MAX_INSTANCES) {
            // Find the instance with the oldest last_seen_at timestamp
            const oldestInstance = await Database.prisma.instance.findFirst({
                orderBy: { last_seen_at: 'asc' }
            });

            if (oldestInstance) {
                nextId = oldestInstance.id;

                // Nettoyer le cache atlas de l'ancienne instance
                this.clearInstanceAtlasCache(nextId);

                // Reset card_ids and user_ids, and update last_seen_at
                const updatedInstance = await Database.prisma.instance.update({
                    where: { id: nextId },
                    data: {
                        card_ids: [],
                        user_ids: [],
                        last_seen_at: new Date()
                    } as any
                });
                
                console.log(`🔄 Instance ${nextId} recycled (was oldest) - atlas cache cleared`);
                return new Instance(updatedInstance);
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
        Instance.ensureCacheDirectories();
        
        // Parser le format card_id:face_index (ex: "abc123:0")
        const [actualCardId, faceIndexStr] = cardId.includes(':') ? cardId.split(':') : [cardId, '0'];
        const faceIndex = parseInt(faceIndexStr, 10);
        
        const imagePath = path.join(Instance.CARDS_DIR, `${cardId}.jpg`);

        // Vérifier si l'image existe déjà
        if (fs.existsSync(imagePath)) {
            return;
        }

        try {

            // Récupérer l'URL de l'image depuis la base de données pour la face spécifique
            const face = await Database.prisma.face.findUnique({
                where: { 
                    card_id_index: {
                        card_id: actualCardId,
                        index: faceIndex
                    }
                },
                select: { image_url: true }
            });

            if (!face?.image_url) {
                console.error(`No image URL found for card ${actualCardId} face ${faceIndex}`);
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
     * Télécharge toutes les images des cartes des instances actives récemment (< 48h)
     */
    public static async downloadRecentInstanceImages(): Promise<void> {
        console.log('🔄 Starting download of images for recent instances (< 48h)...');
        this.ensureCacheDirectories();

        const threshold = new Date(Date.now() - this.CACHE_THRESHOLD_HOURS * 60 * 60 * 1000);

        // Trouver toutes les instances avec des interactions récentes
        const recentInstances = await Database.prisma.instance.findMany({
            where: {
                last_seen_at: {
                    gte: threshold
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

        let downloaded = 0;
        let skipped = 0;
        let errors = 0;

        // Traiter les cartes par lots pour éviter de surcharger la base de données
        const cardIdsArray = Array.from(allCardIds);
        const batchSize = 50;

        for (let i = 0; i < cardIdsArray.length; i += batchSize) {
            const batch = cardIdsArray.slice(i, i + batchSize);
            console.log(`Processing batch ${Math.floor(i / batchSize) + 1}/${Math.ceil(cardIdsArray.length / batchSize)} (${batch.length} cards)...`);

            // Parser les IDs et récupérer les URLs des images pour ce lot
            const facesToFetch: Array<{ card_id: string; index: number; fullId: string }> = [];
            for (const cardId of batch) {
                const [actualCardId, faceIndexStr] = cardId.includes(':') ? cardId.split(':') : [cardId, '0'];
                const faceIndex = parseInt(faceIndexStr, 10);
                facesToFetch.push({ card_id: actualCardId, index: faceIndex, fullId: cardId });
            }

            // Récupérer les URLs des images pour toutes les faces de ce lot
            const faces = await Database.prisma.face.findMany({
                where: {
                    OR: facesToFetch.map(f => ({
                        card_id: f.card_id,
                        index: f.index
                    }))
                },
                select: {
                    card_id: true,
                    index: true,
                    image_url: true
                }
            });

            // Créer une map fullId -> image_url
            const imageUrls = new Map<string, string>();
            faces.forEach(face => {
                if (face.image_url) {
                    const fullId = `${face.card_id}:${face.index}`;
                    imageUrls.set(fullId, face.image_url);
                }
            });

            // Télécharger les images manquantes pour ce lot
            const downloadPromises = batch.map(async (cardId) => {
                const imagePath = path.join(this.CARDS_DIR, `${cardId}.jpg`);

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
     * Nettoie les images et atlas des instances anciennes (> 48h) pour libérer de l'espace
     */
    public static async cleanupOldInstanceImages(): Promise<void> {
        console.log('🧹 Starting cleanup of old instance data (> 48h)...');
        this.ensureCacheDirectories();

        const threshold = new Date(Date.now() - this.CACHE_THRESHOLD_HOURS * 60 * 60 * 1000);

        // Trouver toutes les instances avec des interactions récentes (< 48h)
        const recentInstances = await Database.prisma.instance.findMany({
            where: {
                last_seen_at: {
                    gte: threshold
                },
                card_ids: {
                    isEmpty: false
                }
            },
            select: {
                id: true,
                card_ids: true
            }
        });

        // Collecter tous les card_ids des instances récentes
        const recentCardIds = new Set<string>();
        const recentInstanceIds = new Set<number>();
        
        for (const instance of recentInstances) {
            recentInstanceIds.add(instance.id);
            const cardIds = (instance as any).card_ids || [];
            cardIds.forEach((cardId: string) => recentCardIds.add(cardId));
        }

        console.log(`📊 Found ${recentCardIds.size} cards used in recent instances (< 48h)`);
        console.log(`📊 Found ${recentInstanceIds.size} active instances (< 48h)`);

        // ===== NETTOYAGE DES IMAGES DE CARTES =====
        let deletedCards = 0;
        let cardErrors = 0;

        const imageFiles = fs.readdirSync(this.CARDS_DIR).filter(file => file.endsWith('.jpg'));
        console.log(`📁 Found ${imageFiles.length} card image files`);

        // Identifier les images à supprimer (celles qui ne sont pas utilisées récemment)
        const cardsToDelete = imageFiles.filter(fileName => {
            // Extraire le card_id du nom de fichier (sans l'extension .jpg)
            const cardId = fileName.replace('.jpg', '');
            return !recentCardIds.has(cardId);
        });

        console.log(`🗑️ Will delete ${cardsToDelete.length} unused card images`);

        for (const imageFile of cardsToDelete) {
            try {
                const imagePath = path.join(this.CARDS_DIR, imageFile);
                fs.unlinkSync(imagePath);
                deletedCards++;
            } catch (error) {
                console.error(`❌ Error deleting card image ${imageFile}:`, error);
                cardErrors++;
            }
        }

        // ===== NETTOYAGE DES ATLAS =====
        let deletedAtlas = 0;
        let atlasErrors = 0;

        const atlasFiles = fs.readdirSync(this.ATLAS_DIR).filter(file => file.endsWith('.png'));
        console.log(`📁 Found ${atlasFiles.length} atlas files`);

        // Extraire les IDs d'instance depuis les noms de fichiers atlas
        const atlasPattern = /^instance_(\d+)_batch_\d+_n\d+\.png$/;
        const atlasToDelete = atlasFiles.filter(fileName => {
            const match = fileName.match(atlasPattern);
            if (!match) return true; // Supprimer les fichiers mal formés
            
            const instanceId = parseInt(match[1], 10);
            return !recentInstanceIds.has(instanceId);
        });

        console.log(`🗑️ Will delete ${atlasToDelete.length} old atlas files`);

        for (const atlasFile of atlasToDelete) {
            try {
                const atlasPath = path.join(this.ATLAS_DIR, atlasFile);
                fs.unlinkSync(atlasPath);
                deletedAtlas++;
            } catch (error) {
                console.error(`❌ Error deleting atlas ${atlasFile}:`, error);
                atlasErrors++;
            }
        }

        console.log('🎉 Cleanup completed!');
        console.log(`📊 Card images: ${deletedCards} deleted, ${cardErrors} errors`);
        console.log(`📊 Atlas files: ${deletedAtlas} deleted, ${atlasErrors} errors`);
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