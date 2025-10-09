import { Request, Response, Router } from "express";
import User from "@/database/User";
import Instance from "@/database/Instance";
import { uid } from "@/utils";
import Database from "@/database/Database";
import fs from "fs";
import path from "path";
import https from "https";
import { createCanvas, loadImage } from "canvas";

export const atRouter = Router();
atRouter.get('/at:linkId', atHandler);

async function atHandler(req: Request, res: Response) {
    try {
        const linkId = req.params.linkId;

        // Valider que l'ID est valide (base36)
        if (!/^[0-9a-z]+$/.test(linkId)) {
            return res.status(400).json({
                error: 'Invalid link ID format. Must be base36 characters.'
            });
        }

        // Convertir l'ID base36 en nombre
        const linkIndex = parseInt(linkId, 36);

        // Récupérer ou créer l'utilisateur
        const userId = uid(req);
        const user = await User.findOrCreate(userId);
        await user.updateLastSeen();

        // Gérer l'instance du joueur (utilise les instances existantes créées via ac/aj)
        const playerInstance = await managePlayerInstance(user);

        if (!playerInstance) {
            return res.status(404).json({
                error: 'No instance available. Please create or join an instance first.'
            });
        }

        // Actualiser la date de dernière interaction de l'instance (fait automatiquement dans updateLastSeen)

        // Déterminer le type de réponse basé sur l'index du lien
        const isJsonResponse = linkIndex < 10; // Les 10 premiers liens (0-9 en base36) retournent toujours JSON
        const isAtlasResponse = linkIndex >= 10; // Les liens 10+ sont des atlas d'images

        if (isJsonResponse) {
            return res.json({
                time: Date.now(),
                uid: user.id,
                link_id: linkId,
                link_index: linkIndex,
                instance_id: playerInstance.id,
                player_id: `${playerInstance.id}-${user.id}`,
                type: 'json',
                data: generateJsonData(linkIndex, playerInstance, user.id)
            });
        } else if (isAtlasResponse) {
            // Générer et retourner l'atlas d'images
            try {
                const atlasImage = await generateAtlasImage(linkIndex, playerInstance);
                res.setHeader('Content-Type', 'image/png');
                return res.send(atlasImage);
            } catch (error) {
                console.error('Error generating atlas:', error);
                return res.status(500).json({
                    error: 'Failed to generate atlas image'
                });
            }
        } else {
            // Pour les autres liens, décider aléatoirement entre JSON et image
            const returnJson = Math.random() < 0.5;

            if (returnJson) {
                return res.json({
                    time: Date.now(),
                    uid: user.id,
                    link_id: linkId,
                    link_index: linkIndex,
                    instance_id: playerInstance.id,
                    player_id: `${playerInstance.id}-${user.id}`,
                    type: 'json',
                    data: generateJsonData(linkIndex, playerInstance, user.id)
                });
            } else {
                // Retourner une image
                // Pour l'instant, on simule avec une réponse texte
                // TODO: Implémenter la génération/récupération d'image réelle
                res.setHeader('Content-Type', 'image/png');
                return res.send(Buffer.from(`Fake PNG image for link ${linkId}, instance ${playerInstance.id}`));
            }
        }

    } catch (error) {
        console.error('Error in atHandler:', error);
        return res.status(500).json({
            error: 'Internal server error'
        });
    }
}

/**
 * Gère l'instance du joueur :
 * - Si le joueur est déjà dans une instance, la retourne
 * - Sinon, retourne null (le joueur doit d'abord rejoindre une instance via aj)
 */
async function managePlayerInstance(user: User): Promise<Instance | null> {
    // Vérifier si l'utilisateur est déjà dans une instance
    const instance = await user.getInstance();

    if (instance) {
        // Vérifier si quelqu'un d'autre utilise cette instance (nettoyage)
        await cleanupOldPlayerAssociations(user.id);

        return instance;
    }

    // L'utilisateur n'est pas dans une instance
    return null;
}

/**
 * Nettoie les anciennes associations joueur-instance si nécessaire
 */
async function cleanupOldPlayerAssociations(currentUserId: number): Promise<void> {
    // Cette logique peut être étendue selon les besoins
    // Pour l'instant, on ne fait rien de spécial
}

/**
 * Génère les données JSON pour un lien donné
 */
function generateJsonData(linkIndex: number, instance: Instance, userId: number): any {
    const cards = instance.card_ids;
    const batchSize = 24;
    const batches = [];

    // Dimensions pour calculer les offsets
    const cardWidth = 488;
    const cardHeight = 680;
    const cols = 6;
    const rows = 4;
    const originalWidth = cardWidth * cols;
    const originalHeight = cardHeight * rows;
    const maxSize = 2048;
    const scaleX = maxSize / originalWidth;
    const scaleY = maxSize / originalHeight;
    const scale = Math.min(scaleX, scaleY);
    const finalCardWidth = cardWidth * scale;
    const finalCardHeight = cardHeight * scale;

    // Créer des lots de 24 cartes
    for (let i = 0; i < cards.length; i += batchSize) {
        const batchCards = cards.slice(i, i + batchSize);
        const batchIndex = Math.floor(i / batchSize);
        const atlasLinkId = batchIndex + 10;

        // Calculer les offsets pour chaque carte du batch
        const cardsWithOffsets = batchCards.map((cardId, cardIndex) => {
            const col = cardIndex % cols;
            const row = Math.floor(cardIndex / cols);
            const x = Math.floor(col * finalCardWidth);
            const y = Math.floor(row * finalCardHeight);
            const width = Math.floor(finalCardWidth);
            const height = Math.floor(finalCardHeight);

            // Normaliser les coordonnées entre 0 et 1
            const atlasWidth = Math.floor(originalWidth * scale);
            const atlasHeight = Math.floor(originalHeight * scale);

            return {
                id: cardId,
                x: x / atlasWidth,
                y: y / atlasHeight,
                width: width / atlasWidth,
                height: height / atlasHeight
            };
        });

        batches.push({
            batch_index: batchIndex,
            card_count: batchCards.length,
            atlas_link: `/at${atlasLinkId.toString(36)}`,
            atlas_width: Math.floor(originalWidth * scale),
            atlas_height: Math.floor(originalHeight * scale),
            cards: cardsWithOffsets
        });
    }

    return {
        link_type: linkIndex < 10 ? 'special' : 'normal',
        instance_id: instance.id,
        player_id: `${instance.id}-${userId}`,
        cards_loaded_count: instance.getCardCount(),
        batches: batches
    };
}

/**
 * Génère un atlas d'images pour un lot de 24 cartes
 */
async function generateAtlasImage(linkIndex: number, instance: Instance): Promise<Buffer> {
    // L'index du lot correspond simplement à linkIndex - 10
    const batchIndex = linkIndex - 10;

    const cards = instance.card_ids;
    const batchSize = 24;
    const startIndex = batchIndex * batchSize;
    const batchCards = cards.slice(startIndex, startIndex + batchSize);

    if (batchCards.length === 0) {
        throw new Error('No cards in this batch');
    }

    // Dimensions d'une carte MTG standard
    const cardWidth = 488;
    const cardHeight = 680;
    const cols = 6;
    const rows = 4;

    // Dimensions de l'atlas original
    const originalWidth = cardWidth * cols;  // 2928px
    const originalHeight = cardHeight * rows; // 2720px

    // Dimensions finales (downscale pour être le plus proche de 2048 sans dépasser)
    const maxSize = 2048;
    const scaleX = maxSize / originalWidth;
    const scaleY = maxSize / originalHeight;
    const scale = Math.min(scaleX, scaleY); // Prendre le plus petit scale pour que rien ne dépasse 2048

    const finalWidth = Math.floor(originalWidth * scale);
    const finalHeight = Math.floor(originalHeight * scale);

    // Créer le canvas pour l'atlas final (redimensionné)
    const canvas = createCanvas(finalWidth, finalHeight);
    const ctx = canvas.getContext('2d');

    // Remplir de blanc
    ctx.fillStyle = 'white';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Créer un canvas temporaire pour l'atlas original
    const tempCanvas = createCanvas(originalWidth, originalHeight);
    const tempCtx = tempCanvas.getContext('2d');
    tempCtx.fillStyle = 'white';
    tempCtx.fillRect(0, 0, tempCanvas.width, tempCanvas.height);

    // Traiter chaque carte du lot sur le canvas temporaire
    for (let i = 0; i < batchCards.length; i++) {
        try {
            const cardId = batchCards[i];
            const imagePath = path.join(process.cwd(), 'images', 'cards', `${cardId}.jpg`);

            // Vérifier si l'image existe
            if (fs.existsSync(imagePath)) {
                const img = await loadImage(imagePath);
                const col = i % cols;
                const row = Math.floor(i / cols);
                const x = col * cardWidth;
                const y = row * cardHeight;

                // Dessiner l'image sur le canvas temporaire
                tempCtx.drawImage(img, x, y, cardWidth, cardHeight);
            } else {
                console.warn(`Image not found for card ${cardId}, skipping`);
            }
        } catch (error) {
            console.error(`Error processing card ${batchCards[i]}:`, error);
            // Continuer avec les autres cartes
        }
    }

    // Redimensionner l'atlas temporaire vers le canvas final
    ctx.drawImage(tempCanvas, 0, 0, originalWidth, originalHeight, 0, 0, finalWidth, finalHeight);

    // Retourner l'image PNG
    return canvas.toBuffer('image/png');
}