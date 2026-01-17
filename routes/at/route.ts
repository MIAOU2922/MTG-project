import { Request, Response, Router } from "express";
import User from "@/database/User";
import Instance from "@/database/Instance";
import Deck from "@/database/Deck";
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
            const data = await generateJsonData(linkIndex, playerInstance, user.id);
            return res.json({
                link_type: 't',
                link_id: linkIndex.toString(),
                iid: playerInstance.id,
                uid: user.id,
                time: Date.now(),
                data: data
            });
        } else if (isAtlasResponse) {
            // Calculer l'index du batch
            const batchIndex = linkIndex - 10;
            const batchSize = 24;
            
            // Calculer le nombre de cartes dans ce batch spécifique
            const cards = playerInstance.card_ids;
            const startIndex = batchIndex * batchSize;
            const batchCards = cards.slice(startIndex, startIndex + batchSize);
            const currentCardCount = batchCards.length;

            // Vérifier si l'atlas est en cache ET correspond au nombre de cartes actuel
            if (Instance.hasAtlasCache(playerInstance.id, batchIndex, currentCardCount)) {
                console.log(`📦 Serving atlas from cache: instance ${playerInstance.id}, batch ${batchIndex}, ${currentCardCount} cards`);
                const cachedAtlas = Instance.readAtlasCache(playerInstance.id, batchIndex, currentCardCount);
                if (cachedAtlas) {
                    res.setHeader('Content-Type', 'image/png');
                    res.setHeader('X-Cache', 'HIT');
                    return res.send(cachedAtlas);
                }
            }

            // L'atlas n'existe pas OU le nombre de cartes a changé -> régénérer
            try {
                console.log(`🎨 Generating atlas: instance ${playerInstance.id}, batch ${batchIndex}, ${currentCardCount} cards`);
                const atlasImage = await generateAtlasImage(linkIndex, playerInstance);
                
                // Sauvegarder dans le cache avec le nombre de cartes
                Instance.saveAtlasCache(playerInstance.id, batchIndex, currentCardCount, atlasImage);
                
                res.setHeader('Content-Type', 'image/png');
                res.setHeader('X-Cache', 'MISS');
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
                const data = await generateJsonData(linkIndex, playerInstance, user.id);
                return res.json({
                    link_type: 't',
                    link_id: linkIndex.toString(),
                    iid: playerInstance.id,
                    uid: user.id,
                    time: Date.now(),
                    data: data
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
 * Génère la liste des decks de l'utilisateur
 * at1 : Retourne juste l'ID et le nom de chaque deck
 */
async function generateDeckListData(userId: number): Promise<any> {
    try {
        const userDecks = await Deck.findByUserId(userId);
        
        const decksList = userDecks.map((deck) => ({
            id: deck.id,
            name: deck.name
        }));

        return {
            total_decks: decksList.length,
            decks: decksList
        };
    } catch (error) {
        console.error('Error generating deck list:', error);
        return {
            total_decks: 0,
            decks: [],
            error: 'Failed to fetch decks'
        };
    }
}

/**
 * Génère la liste des sets MTG
 * at2 : Retourne les sets avec format "Name (SET)"
 */
async function generateSetsData(): Promise<any> {
    try {
        // Récupérer tous les sets de la base de données
        const allSets = await Database.prisma.set.findMany({
            orderBy: {
                name: 'asc'
            }
        });

        // Créer la liste des display_name
        const setsDisplayNames = allSets.map((set) => 
            `${set.name} (${set.set || set.id})`
        );

        return {
            count: setsDisplayNames.length,
            sets: setsDisplayNames
        };
    } catch (error) {
        console.error('Error generating sets data:', error);
        return {
            count: 0,
            sets: [],
            error: 'Failed to fetch sets'
        };
    }
}

/**
 * Génère la liste des cartes de l'instance avec leurs légalités et rulings
 * at3 : Retourne les cartes de l'instance avec leurs détails complets
 */
async function generateInstanceCardsData(instance: Instance): Promise<any> {
    try {
        const cardIds = instance.card_ids;

        if (cardIds.length === 0) {
            return {
                total_cards: 0,
                cards: []
            };
        }

        // Extraire les UUIDs de base (sans les suffixes :0 ou :1 pour les faces)
        const cardIdMap = new Map<string, { baseId: string, faceIndex: number | null }>();
        const baseCardIds = new Set<string>();

        cardIds.forEach(cardId => {
            const parts = cardId.split(':');
            const baseId = parts[0];
            const faceIndex = parts.length > 1 ? parseInt(parts[1]) : null;
            
            cardIdMap.set(cardId, { baseId, faceIndex });
            baseCardIds.add(baseId);
        });

        // Récupérer toutes les cartes de l'instance avec leurs faces
        const cards = await Database.prisma.card.findMany({
            where: {
                id: {
                    in: Array.from(baseCardIds)
                }
            },
            include: {
                faces: {
                    include: {
                        oracle: {
                            include: {
                                rulings: {
                                    orderBy: {
                                        published_at: 'desc'
                                    }
                                }
                            }
                        }
                    }
                },
                set: true
            }
        });

        // Créer un map pour préserver l'ordre des cartes dans l'instance
        const cardMap = new Map(cards.map(card => [card.id, card]));

        // Dédupliquer les cartes basées sur leur base_id (pour éviter les doublons des cartes double face)
        const seenBaseIds = new Set<string>();
        const uniqueCardIds = cardIds.filter(cardId => {
            const idInfo = cardIdMap.get(cardId);
            if (!idInfo) return true; // Garder les IDs invalides pour le message d'erreur
            
            if (seenBaseIds.has(idInfo.baseId)) {
                return false; // Carte déjà vue, ignorer
            }
            
            seenBaseIds.add(idInfo.baseId);
            return true;
        });

        // Grouper les cartes par oracle_id
        const oracleGroups = new Map<string, {
            ids: string[],
            legalities: any,
            rulings: Map<string, any>
        }>();

        uniqueCardIds.forEach(cardId => {
            const idInfo = cardIdMap.get(cardId);
            if (!idInfo) return;

            const card = cardMap.get(idInfo.baseId);
            if (!card) return;

            // Trouver l'oracle_id
            const oracleId = card.faces.find(f => f.oracle_id)?.oracle_id;
            if (!oracleId) return;

            // Créer ou récupérer le groupe pour cet oracle_id
            if (!oracleGroups.has(oracleId)) {
                oracleGroups.set(oracleId, {
                    ids: [],
                    legalities: card.legalities,
                    rulings: new Map()
                });
            }

            const group = oracleGroups.get(oracleId)!;
            
            // Ajouter l'ID de la carte
            group.ids.push(card.id);

            // Collecter les rulings
            card.faces.forEach(face => {
                if (face.oracle?.rulings) {
                    face.oracle.rulings.forEach(ruling => {
                        if (!group.rulings.has(ruling.id)) {
                            group.rulings.set(ruling.id, {
                                id: ruling.id,
                                published_at: ruling.published_at,
                                comment: ruling.comment
                            });
                        }
                    });
                }
            });
        });

        // Formater les données groupées par oracle
        const cardsData = Array.from(oracleGroups.entries()).map(([oracleId, group]) => ({
            oracle_id: oracleId,
            ids: group.ids,
            legalities: group.legalities,
            rulings: Array.from(group.rulings.values())
        }));

        return {
            total_cards: cardsData.length,
            cards: cardsData
        };
    } catch (error) {
        console.error('Error generating instance cards data:', error);
        return {
            total_cards: 0,
            cards: [],
            error: 'Failed to fetch instance cards'
        };
    }
}

/**
 * Génère les données JSON pour un lien donné
 * Utilise le même calcul de coordonnées UV que le script Python
 */
async function generateJsonData(linkIndex: number, instance: Instance, userId: number): Promise<any> {
    // at1 : Retourner la liste des decks de l'utilisateur
    if (linkIndex === 1) {
        return generateDeckListData(userId);
    }

    // at2 : Retourner la liste des sets MTG et des types de sets
    if (linkIndex === 2) {
        return generateSetsData();
    }

    // at3 : Retourner la liste des cartes de l'instance avec légalités et rulings
    if (linkIndex === 3) {
        return generateInstanceCardsData(instance);
    }

    const cards = instance.card_ids;
    const batchSize = 24;
    const batches = [];

    // Dimensions d'une carte MTG standard
    const cardWidth = 488;
    const cardHeight = 680;
    const cols = 6;
    const rows = 4;
    
    // Dimensions de l'atlas original (avant downscale)
    const originalWidth = cardWidth * cols;   // 2928px
    const originalHeight = cardHeight * rows;  // 2720px
    
    // Calculer le scale pour ne pas dépasser 2048px
    const maxSize = 2048;
    const scaleX = maxSize / originalWidth;
    const scaleY = maxSize / originalHeight;
    const scale = Math.min(scaleX, scaleY);
    
    // Dimensions finales de l'atlas (après downscale)
    const atlasWidth = Math.floor(originalWidth * scale);
    const atlasHeight = Math.floor(originalHeight * scale);
    
    // Dimensions d'une carte après downscale
    const scaledCardWidth = cardWidth * scale;
    const scaledCardHeight = cardHeight * scale;

    // Créer des lots de 24 cartes
    for (let i = 0; i < cards.length; i += batchSize) {
        const batchCards = cards.slice(i, i + batchSize);
        const batchIndex = Math.floor(i / batchSize);
        const atlasLinkId = batchIndex + 10;

        batches.push({
            batch_index: batchIndex,
            card_count: batchCards.length,
            atlas_link: `/at${atlasLinkId.toString(36)}`,
            cards: batchCards
        });
    }

    return {
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

    // Vérifier que toutes les images sont disponibles avant de générer l'atlas
    const missingImages: string[] = [];
    for (const cardId of batchCards) {
        const imagePath = path.join(process.cwd(), 'images', 'cards', `${cardId}.jpg`);
        if (!fs.existsSync(imagePath)) {
            missingImages.push(cardId);
        }
    }

    if (missingImages.length > 0) {
        throw new Error(`Cannot generate atlas: ${missingImages.length}/${batchCards.length} images not yet downloaded from Scryfall. Please wait for image download to complete.`);
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