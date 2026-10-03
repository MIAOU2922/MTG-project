import { Request, Response, Router } from "express";
import User from "@/database/User";
import Deck from "@/database/Deck";
import Instance from "@/database/Instance";
import { getUserId } from "@/utils";
import Database from "@/database/Database";
import { liveRefresh } from "@/sync/moxfield/liveRefresh";

export const apiDeckRouter = Router();
apiDeckRouter.get('/ad', adHandler);

interface ParsedQuery {
    action: string;
    deckName?: string;
    deckId?: string;
    /** Filtres de l'action list (recherche publique) */
    listFormat?: string;
    listAuthor?: string;
    listCommander?: string;
}

/**
 * Parse the query parameter into action and parameters
 * Format: /ad?q=action:param1:param2:param3...
 * Examples:
 *  - /ad?q=load:deck_id
 *  - /ad?q=list:name:format:author:commander
 *  - /ad?q=refresh
 */
function parseAdQuery(queryParam: string): ParsedQuery {
    const parts = queryParam.split(':');
    const action = parts[0]?.toLowerCase() || 'list';
    
    const result: ParsedQuery = { action };
    
    switch (action) {
        case 'load':
            // load:deck_id — seul un UUID de deck sauvegardé est accepté
            const firstParam = parts[1];
            if (firstParam && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(firstParam)) {
                result.deckId = firstParam;
            }
            break;
        case 'list':
            // list:search_name:format:author:commander
            //   - sans params : liste les decks de l'utilisateur
            //   - avec params : recherche publique (decks importés), filtres
            //     optionnels par nom (partiel), format exact, auteur (partiel)
            //     et commander (partiel). La recherche est forwardée au scraper
            //     Moxfield (re-scraping en arrière-plan pour rafraîchir la BDD).
            result.deckName = parts[1] ? decodeURIComponent(parts[1]) : undefined;
            result.listFormat = parts[2] ? decodeURIComponent(parts[2]) : undefined;
            result.listAuthor = parts[3] ? decodeURIComponent(parts[3]) : undefined;
            result.listCommander = parts[4] ? decodeURIComponent(parts[4]) : undefined;
            break;
        default:
            break;
    }
    
    return result;
}

async function adHandler(req: Request, res: Response) {
    try {
        const userId = await getUserId(req);
        if (userId === null) {
            return res.status(401).json({ error: 'unknown_user', hint: 'Register via /aur first' });
        }
        const user = await User.findById(userId);
        if (!user) {
            return res.status(401).json({ error: 'unknown_user' });
        }
        await user.updateLastSeen();

        // Get the current instance for this user
        const instances = await Database.prisma.instance.findMany({
            where: { user_ids: { has: userId } }
        });
        const currentInstance = instances.length > 0 ? new Instance(instances[0]) : null;

        // Parse the query parameter
        const queryParam = req.query.q as string;
        if (!queryParam) {
            return res.status(400).json({
                error: 'Query parameter "q" is required. Format: /ad?q=action:param1:param2...'
            });
        }

        const parsedQuery = parseAdQuery(queryParam);

        // Route to the appropriate handler
        switch (parsedQuery.action) {
            case 'load':
                if (!currentInstance) {
                    return res.status(400).json({
                        error: 'User is not in any instance'
                    });
                }
                return await handleLoad(res, user, currentInstance, parsedQuery.deckId);
            case 'list':
                return await handleList(res, user, parsedQuery.deckName, parsedQuery.listFormat, parsedQuery.listAuthor, parsedQuery.listCommander);

            case 'refresh':
                return handleRefreshStatus(res, user);

            default:
                return res.status(400).json({
                    error: `Invalid action: ${parsedQuery.action}. Supported: load, list, refresh`
                });
        }

    } catch (error) {
        console.error('Error in adHandler:', error);
        return res.status(500).json({
            error: 'Error processing deck operation'
        });
    }
}


/**
 * LOAD action: Load a saved deck from the DB and add its cards to the instance
 * Syntax: load:deck_uuid — anyone can load any public deck.
 */
async function handleLoad(
    res: Response,
    user: any,
    instance: Instance,
    deckId: string | undefined
): Promise<Response> {
    if (!deckId) {
        return res.status(400).json({
            error: 'Deck ID (UUID) is required for load action'
        });
    }

    try {
        // Load by ID — anyone can load any public deck
        const deck = await Deck.findById(deckId);

        if (!deck) {
            return res.status(404).json({
                error: 'Deck not found'
            });
        }

        // Get the card IDs (repeated by count)
        const cardIds = await deck.getCardIds();

        // Fetch zones (card_ids[i] ↔ counts[i]) + card details
        const savedZones = await Database.prisma.deckZone.findMany({
            where: { deck_id: deck.id }
        });

        const zoneCardIds = savedZones.flatMap(z => z.card_ids);
        const cardsDetails = await Database.prisma.card.findMany({
            where: { id: { in: zoneCardIds } },
            select: {
                id: true,
                name: true
            }
        });

        // Create a map for quick lookup
        const cardsMap = new Map(cardsDetails.map(c => [c.id, c]));

        // Group cards by zone (tableaux parallèles déroulés)
        const cardsByZone: any = {
            main: [],
            sideboard: [],
            commander: [],
            companion: [],
            oathbreaker: [],
            wishboard: []
        };
        for (const savedZone of savedZones) {
            const zone = savedZone.zone || 'main';
            if (!cardsByZone[zone]) {
                cardsByZone[zone] = [];
            }

            savedZone.card_ids.forEach((cardId, i) => {
                const card = cardsMap.get(cardId);
                if (!card) return;
                const cardData: any = {
                    count: savedZone.counts[i] ?? 1,
                    name: card.name,
                    card_id: card.id
                };
                if (zone === 'commander') {
                    cardData.is_commander = true;
                }
                cardsByZone[zone].push(cardData);
            });
        }

        // Add cards to the instance
        for (const cardId of cardIds) {
            await instance.addCard(cardId);
        }

        // Calculate deck statistics
        const uniqueCardIds = await deck.getUniqueCardIds();
        const deckSize = cardIds.length;

        return res.json({
            link_type: 'd',
            link_id: 'load',
            iid: instance.id,
            uid: user.id,
            time: Date.now(),
            data: {
                action: 'load',
                deck_type: 'saved',
                deck_id: deck.id,
                deck_name: deck.name,
                commander: deck.commander,
                format: deck.format,
                author: deck.author,
                source_url: deck.source_url,
                unique_cards: uniqueCardIds.length,
                total_cards: deckSize,
                cards_added_to_instance: cardIds.length,
                cards_by_zone: cardsByZone,
                message: 'Deck loaded successfully and cards added to instance'
            }
        });
    } catch (error) {
        console.error('Error in handleLoad:', error);
        return res.status(500).json({
            error: 'Error loading deck'
        });
    }
}


/**
 * REFRESH action: état de la file de re-scraping Moxfield (live refresh).
 * Renvoie le job en cours, la file d'attente et les 20 derniers jobs.
 */
function handleRefreshStatus(res: Response, user: any): Response {
    return res.json({
        link_type: 'd',
        link_id: 'refresh',
        iid: null,
        uid: user.id,
        time: Date.now(),
        data: {
            action: 'refresh',
            live_refresh: liveRefresh.status()
        }
    });
}

/**
 * LIST action: List user's decks or search public decks
 *
 * Formes :
 *   list                      → decks de l'utilisateur
 *   list:<nom>                → recherche publique par nom (partiel)
 *   list::<format>            → decks publics d'un format (ex: commander)
 *   list:::<auteur>           → decks publics d'un auteur Moxfield
 *   list::::<commander>       → decks publics avec ce commander (partiel)
 *   list:<nom>:<format>:<auteur>:<commander> → combinaison
 *
 * ⚡ Live refresh : toute recherche publique (au moins un filtre) est aussi
 *   forwardée au scraper Moxfield, qui re-scrape en arrière-plan les decks
 *   correspondants pour mettre la BDD à jour (voir `refresh` dans la réponse).
 */
async function handleList(
    res: Response,
    user: any,
    deckName: string | undefined,
    listFormat: string | undefined,
    listAuthor: string | undefined,
    listCommander: string | undefined
): Promise<Response> {
    try {
        let deckEntries: Array<Record<string, unknown>> = [];

        if (deckName || listFormat || listAuthor || listCommander) {
            // ==== Recherche publique (decks importés) avec filtres ====
            const where: any = { source: { not: null } };
            if (deckName) where.name = { contains: deckName, mode: 'insensitive' };
            if (listFormat) where.format = listFormat.toLowerCase();
            if (listAuthor) where.author = { contains: listAuthor, mode: 'insensitive' };
            if (listCommander) where.commander = { contains: listCommander, mode: 'insensitive' };

            const rows = await Database.prisma.deck.findMany({
                where,
                orderBy: { created_at: 'desc' },
                take: 200, // Limite de sécurité pour la réponse
                select: {
                    id: true,
                    name: true,
                    description: true,
                    commander: true,
                    user_id: true,
                    source: true,
                    source_id: true,
                    source_url: true,
                    format: true,
                    author: true,
                    created_at: true,
                    updated_at: true,
                    _count: { select: { zones: true } }
                }
            });

            // Nombre total d'exemplaires par deck (somme des counts des zones)
            const zoneRows = await Database.prisma.deckZone.findMany({
                where: { deck_id: { in: rows.map(r => r.id) } },
                select: { deck_id: true, counts: true }
            });
            const sizeMap = new Map<string, number>();
            for (const z of zoneRows) {
                sizeMap.set(z.deck_id, (sizeMap.get(z.deck_id) ?? 0) + z.counts.reduce((a, b) => a + b, 0));
            }

            deckEntries = rows.map((r: any) => ({
                id: r.id,
                name: r.name,
                description: r.description,
                commander: r.commander,
                owner_id: r.user_id,
                is_owner: r.user_id === user.id,
                source: r.source,
                source_id: r.source_id,
                source_url: r.source_url,
                format: r.format,
                author: r.author,
                cards_count: sizeMap.get(r.id) ?? 0,
                created_at: r.created_at,
                updated_at: r.updated_at
            }));
        } else {
            // ==== Decks de l'utilisateur ====
            const decks = await Deck.findByUserId(user.id);

            deckEntries = await Promise.all(
                decks.map(async (deck) => ({
                    id: deck.id,
                    name: deck.name,
                    description: deck.description,
                    commander: deck.commander,
                    owner_id: deck.user_id,
                    is_owner: deck.user_id === user.id,
                    source: deck.source,
                    source_id: deck.source_id,
                    source_url: deck.source_url,
                    format: deck.format,
                    author: deck.author,
                    cards_count: await deck.getDeckSize(),
                    created_at: deck.created_at,
                    updated_at: deck.updated_at
                }))
            );
        }

        // ⚡ Forward de la recherche au scraper Moxfield (re-scraping ciblé,
        // jamais bloquant : la réponse part immédiatement, le crawl tourne en
        // arrière-plan dans la file liveRefresh).
        const refreshJob = deckName || listFormat || listAuthor || listCommander
            ? liveRefresh.forwardSearch({
                name: deckName,
                format: listFormat,
                author: listAuthor,
                commander: listCommander
            })
            : null;

        return res.json({
            link_type: 'd',
            link_id: 'list',
            iid: null,
            uid: user.id,
            time: Date.now(),
            data: {
                action: 'list',
                search_name: deckName || null,
                format: listFormat || null,
                author: listAuthor || null,
                commander: listCommander || null,
                decks_count: deckEntries.length,
                decks: deckEntries,
                refresh: refreshJob
                    ? {
                        job_id: refreshJob.id,
                        type: refreshJob.type,
                        query: refreshJob.query,
                        format: refreshJob.format,
                        status: refreshJob.status,
                        message: refreshJob.status === 'queued'
                            ? 'Re-scraping Moxfield planifié en arrière-plan'
                            : 'Re-scraping Moxfield déjà en cours ou planifié'
                    }
                    : { status: 'disabled' }
            }
        });
    } catch (error) {
        console.error('Error in handleList:', error);
        return res.status(500).json({
            error: 'Error listing decks'
        });
    }
}



