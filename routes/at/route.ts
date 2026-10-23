import { Request, Response, Router } from "express";
import User from "@/database/User";
import Instance from "@/database/Instance";
import { getUserId } from "@/utils";
import Database from "@/database/Database";
import fs from "fs";
import path from "path";
import https from "https";
import { createCanvas, loadImage } from "canvas";
import { createHash } from "crypto";

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

        // Récupérer l'utilisateur (IP déjà liée via /aur ou login par clé)
        const userId = await getUserId(req);
        if (userId === null) {
            return res.status(401).json({ error: 'unknown_user', hint: 'Register via /aur first' });
        }
        const user = await User.findById(userId);
        if (!user) {
            return res.status(401).json({ error: 'unknown_user' });
        }
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
            const data = await generateJsonData(linkIndex, playerInstance, user);
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
                const data = await generateJsonData(linkIndex, playerInstance, user);
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
async function cleanupOldPlayerAssociations(currentUserId: string): Promise<void> {
    // Cette logique peut être étendue selon les besoins
    // Pour l'instant, on ne fait rien de spécial
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
 * Construit un id stable pour un ruling à partir de sa clé naturelle
 * (oracle_id + date + hash court du commentaire), l'id autoincrement
 * ayant été supprimé au profit de la clé primaire composite.
 */
function buildRulingId(oracleId: string, publishedAt: Date, comment: string): string {
    const date = publishedAt.toISOString().slice(0, 10);
    const hash = createHash('sha256').update(comment).digest('hex').slice(0, 8);
    return `${oracleId}_${date}_${hash}`;
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
            const oracleFace = card.faces.find(f => f.oracle_id);
            if (!oracleFace) return;
            const oracleId = oracleFace.oracle_id!;

            // Créer ou récupérer le groupe pour cet oracle_id
            if (!oracleGroups.has(oracleId)) {
                oracleGroups.set(oracleId, {
                    ids: [],
                    legalities: oracleFace.oracle?.legalities ?? {},
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
                        const id = buildRulingId(ruling.oracle_id, ruling.published_at, ruling.comment);
                        if (!group.rulings.has(id)) {
                            group.rulings.set(id, {
                                id,
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
 * Génère les données de jeu localisées de l'instance, groupées par oracle
 * (comme at3 pour les rulings). Pour chaque oracle unique :
 *  - ids : les cartes de l'instance qui portent cet oracle
 *  - name / type_line / mana_cost / cmc / oracle_text / power / toughness /
 *    loyalty / colors / color_identity : dans la langue préférée du user
 *    (retombée sur l'anglais si aucun print dans cette langue n'existe).
 * at1 : Retourne les cartes de l'instance prêtes à jouer, dans la langue du user
 */
async function generatePlayData(instance: Instance, lang: string): Promise<any> {
    try {
        const cardIds = instance.card_ids;

        if (cardIds.length === 0) {
            return {
                total_cards: 0,
                lang,
                cards: []
            };
        }

        // UUIDs de base (sans le suffixe :N des faces)
        const baseCardIds = new Set<string>();
        cardIds.forEach(cardId => baseCardIds.add(cardId.split(':')[0]));

        // 1) Les prints exacts de l'instance avec leurs faces + oracles
        const cards = await Database.prisma.card.findMany({
            where: { id: { in: Array.from(baseCardIds) } },
            include: {
                faces: { include: { oracle: true } },
            },
        });
        const cardMap = new Map(cards.map(card => [card.id, card]));

        // 2) Grouper par oracle_id : chaque face compte (une carte split
        //    appartient à autant de groupes qu'elle a de faces/oracles)
        const groupIds = new Map<string, Set<string>>();  // oracleId → card ids
        const primaryFaces = new Map<string, { card: any, face: any }>(); // oracleId → face du print de l'instance

        for (const cardId of baseCardIds) {
            const card = cardMap.get(cardId);
            if (!card) continue;
            for (const face of card.faces) {
                const oracleId = face.oracle_id;
                if (!oracleId) continue;
                if (!groupIds.has(oracleId)) groupIds.set(oracleId, new Set());
                groupIds.get(oracleId)!.add(card.id);
                if (!primaryFaces.has(oracleId)) {
                    primaryFaces.set(oracleId, { card, face });
                }
            }
        }

        // 3) Prints localisés : pour chaque oracle, trouver un print dans la
        //    langue du user (retombée : print anglais)
        const langPrefs = lang === 'en' ? ['en'] : [lang, 'en'];
        const oracleIdsArr = Array.from(groupIds.keys());

        const localizedPrints = oracleIdsArr.length === 0 ? [] : await Database.prisma.card.findMany({
            where: {
                lang: { in: langPrefs },
                faces: { some: { oracle_id: { in: oracleIdsArr } } },
            },
            include: {
                faces: { where: { oracle_id: { in: oracleIdsArr } } },
            },
        });

        const langScore = (l: string) => (l === lang ? 2 : l === 'en' ? 1 : 0);
        const localizedByOracle = new Map<string, { card: any, face: any, lang: string }>();
        for (const print of localizedPrints) {
            for (const face of print.faces) {
                const oracleId = face.oracle_id;
                if (!oracleId) continue;
                const existing = localizedByOracle.get(oracleId);
                if (!existing || langScore(print.lang) > langScore(existing.lang)) {
                    localizedByOracle.set(oracleId, { card: print, face, lang: print.lang });
                }
            }
        }

        // 4) Construire la réponse, une entrée par oracle
        const cardsData = Array.from(groupIds.entries()).map(([oracleId, idsSet]) => {
            const loc = localizedByOracle.get(oracleId);
            const primary = primaryFaces.get(oracleId);

            const card = loc?.card ?? primary?.card;
            const face = loc?.face ?? primary?.face;
            // L'oracle (cmc, color_identity, texte anglais) vient TOUJOURS du
            // print de l'instance : les prints localisés sont inclus sans oracle.
            const oracle = primary?.face?.oracle ?? face?.oracle;

            // Nom : face localisée → nom imprimé card-level (simple face) → anglais
            const name = face?.printed_name
                ?? card?.printed_name
                ?? primary?.face?.name
                ?? face?.name
                ?? card?.name
                ?? null;

            return {
                oracle_id: oracleId,
                ids: Array.from(idsSet),
                lang: loc?.lang ?? card?.lang ?? lang,
                name,
                type_line: face?.printed_type_line ?? face?.type_line ?? null,
                mana_cost: face?.mana_cost ?? null,
                cmc: oracle?.cmc ?? null,
                oracle_text: face?.printed_text ?? oracle?.text ?? '',
                power: face?.power ?? null,
                toughness: face?.toughness ?? null,
                loyalty: face?.loyalty ?? null,
                colors: face?.colors ?? [],
                color_identity: oracle?.color_identities ?? [],
            };
        });

        // Ordre stable : ordre d'apparition dans l'instance
        const order = new Map<string, number>();
        let idx = 0;
        for (const cardId of baseCardIds) {
            const card = cardMap.get(cardId);
            if (!card) continue;
            for (const face of card.faces) {
                const oracleId = face.oracle_id;
                if (oracleId && !order.has(oracleId)) order.set(oracleId, idx++);
            }
        }
        cardsData.sort((a, b) => (order.get(a.oracle_id) ?? 0) - (order.get(b.oracle_id) ?? 0));

        return {
            total_cards: cardsData.length,
            lang,
            cards: cardsData
        };
    } catch (error) {
        console.error('Error generating localized play data:', error);
        return {
            total_cards: 0,
            lang,
            cards: [],
            error: 'Failed to fetch card play data'
        };
    }
}

/**
 * Génère les données JSON pour un lien donné
 * Utilise le même calcul de coordonnées UV que le script Python
 */
async function generateJsonData(linkIndex: number, instance: Instance, user: User): Promise<any> {
    // at1 : Cartes de l'instance avec les textes de jeu dans la langue du user
    if (linkIndex === 1) {
        return generatePlayData(instance, user.lang);
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