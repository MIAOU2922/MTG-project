import { Request, Response, Router } from "express";
import User from "@/database/User";
import Deck, { DeckCardData } from "@/database/Deck";
import Instance from "@/database/Instance";
import { uid } from "@/utils";
import Database from "@/database/Database";
import https from "https";

export const apiDeckRouter = Router();
apiDeckRouter.get('/ad', adHandler);

interface ParsedQuery {
    action: string;
    deckList?: string;
    deckName?: string;
    format?: string;
    lang?: string;
    deckId?: string;
    description?: string;
}

interface ParsedCard {
    count: number;
    name: string;
    set?: string;
    collector_number?: string;
    is_commander: boolean;
    zone?: string; // main, sideboard, commander, companion, oathbreaker, wishboard
}

interface DeckCard {
    count: number;
    name: string;
    set?: string;
    collector_number?: string;
    lang: string;
    card_id?: string;
    rarity?: string;
    type_line?: string | null;
    is_commander: boolean;
    zone?: string; // main, sideboard, commander, companion, oathbreaker, wishboard
    found: boolean;
    found_as: 'exact' | 'partial' | 'not_found';
    match_confidence: number;
}

/**
 * Parse the query parameter into action and parameters
 * Format: /ad?q=action:param1:param2:param3...
 * Examples:
 *  - /ad?q=parse:deck_list_encoded
 *  - /ad?q=save:Deck%20Name:deckstats:deck_list_encoded
 *  - /ad?q=load:deck_id
 *  - /ad?q=list
 */
function parseAdQuery(queryParam: string): ParsedQuery {
    const parts = queryParam.split(':');
    const action = parts[0]?.toLowerCase() || 'parse';
    
    const result: ParsedQuery = { action };
    
    switch (action) {
        case 'parse':
            // parse:deck_list_encoded:format:lang
            result.deckList = decodeURIComponent(parts[1] || '');
            result.format = parts[2] || 'auto';
            result.lang = parts[3] || 'en';
            break;
            
        case 'save':
            // save:Deck_Name:format:deck_list_encoded:lang:desc
            result.deckName = decodeURIComponent(parts[1] || '');
            result.format = parts[2] || 'auto';
            result.deckList = decodeURIComponent(parts[3] || '');
            result.lang = parts[4] || 'en';
            result.description = parts[5] ? decodeURIComponent(parts[5]) : undefined;
            break;
            
        case 'load':
            // load:deck_id
            result.deckId = parts[1];
            break;
            
        case 'delete':
            // delete:deck_id
            result.deckId = parts[1];
            break;
            
        case 'list':
            // list:search_name (optional)
            result.deckName = parts[1] ? decodeURIComponent(parts[1]) : undefined;
            break;
            
        default:
            break;
    }
    
    return result;
}

async function adHandler(req: Request, res: Response) {
    try {
        const userId = uid(req);
        const user = await User.findOrCreate(userId);
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
            case 'parse':
                return await handleParse(res, user, parsedQuery.deckList || '', parsedQuery.format || 'auto', parsedQuery.lang || 'en');
                
            case 'save':
                return await handleSave(res, user, parsedQuery.deckList || '', parsedQuery.format || 'auto', parsedQuery.lang || 'en', parsedQuery.deckName, undefined, parsedQuery.description);
                
            case 'load':
                if (!currentInstance) {
                    return res.status(400).json({
                        error: 'User is not in any instance'
                    });
                }
                return await handleLoad(res, user, currentInstance, parsedQuery.deckId, undefined);
                
            case 'delete':
                return await handleDelete(res, user, parsedQuery.deckId);
                
            case 'list':
                return await handleList(res, user, parsedQuery.deckName);
                
            default:
                return res.status(400).json({
                    error: `Invalid action: ${parsedQuery.action}. Supported: parse, save, load, delete, list`
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
 * URL action: Import deck from Moxfield or Deckstats URL
 */

/**
 * PARSE action: Parse and lookup cards without saving
 */
async function handleParse(
    res: Response,
    user: any,
    query: string | undefined,
    format: string,
    lang: string
): Promise<Response> {
    if (!query) {
        return res.status(400).json({
            error: 'Deck list parameter "q" is required for parse action'
        });
    }

    try {
        let parsedCards: ParsedCard[] = [];
        let detectedFormat = format;

        // Try all formats in sequence - be permissive
        if (format === 'deckstats' || format === 'auto') {
            const deckstatsCards = parseDeckstatsFormat(query);
            if (deckstatsCards.length > 0) {
                parsedCards = deckstatsCards;
                detectedFormat = 'deckstats';
            }
        }

        if (parsedCards.length === 0 && (format === 'moxfield' || format === 'auto')) {
            const moxfieldCards = parseMoxfieldFormat(query);
            if (moxfieldCards.length > 0) {
                parsedCards = moxfieldCards;
                detectedFormat = 'moxfield';
            }
        }

        // If no cards found with specific format, try generic parsing
        if (parsedCards.length === 0) {
            parsedCards = parseGenericFormat(query);
            detectedFormat = 'generic';
        }

        if (parsedCards.length === 0) {
            return res.status(400).json({
                error: 'No valid cards found in deck list'
            });
        }

        const deckCards: DeckCard[] = [];
        const errors: string[] = [];
        let foundCount = 0;
        let notFoundCount = 0;
        let partialMatches = 0;
        let commander: string | null = null;

        for (const parsedCard of parsedCards) {
            try {
                const result = await lookupCard(parsedCard, lang);

                if (result.found) {
                    foundCount++;
                    if (result.found_as === 'partial') {
                        partialMatches++;
                    }
                } else {
                    notFoundCount++;
                }

                if (result.is_commander) {
                    commander = result.name;
                }

                deckCards.push(result);
            } catch (lookupError) {
                notFoundCount++;
                errors.push(`Failed to lookup ${parsedCard.name}: ${lookupError}`);
                deckCards.push({
                    count: parsedCard.count,
                    name: parsedCard.name,
                    set: parsedCard.set,
                    collector_number: parsedCard.collector_number,
                    lang: lang,
                    is_commander: parsedCard.is_commander,
                    zone: parsedCard.zone || 'main',
                    found: false,
                    found_as: 'not_found',
                    match_confidence: 0
                });
            }
        }

        const deckCount = deckCards.reduce((sum, card) => sum + card.count, 0);

        return res.json({
            time: Date.now(),
            uid: user.id,
            action: 'parse',
            format: detectedFormat,
            deck_count: deckCount,
            commander: commander,
            cards: deckCards,
            stats: {
                found: foundCount,
                not_found: notFoundCount,
                partial_matches: partialMatches,
                errors: errors.length > 0 ? errors : []
            }
        });
    } catch (error) {
        console.error('Error in handleParse:', error);
        return res.status(500).json({
            error: 'Error parsing deck list'
        });
    }
}

async function handleSave(
    res: Response,
    user: any,
    query: string | undefined,
    format: string,
    lang: string,
    deckName: string | undefined,
    deckId: string | undefined,
    description: string | undefined
): Promise<Response> {
    if (!query || !deckName) {
        return res.status(400).json({
            error: 'Both deck list "q" and deck name "name" are required for save action'
        });
    }

    try {
        // Parse the deck list - try all formats
        let parsedCards: ParsedCard[] = [];

        if (format === 'deckstats' || format === 'auto') {
            const deckstatsCards = parseDeckstatsFormat(query);
            if (deckstatsCards.length > 0) {
                parsedCards = deckstatsCards;
            }
        }

        if (parsedCards.length === 0 && (format === 'moxfield' || format === 'auto')) {
            const moxfieldCards = parseMoxfieldFormat(query);
            if (moxfieldCards.length > 0) {
                parsedCards = moxfieldCards;
            }
        }

        // If no cards found, try generic format
        if (parsedCards.length === 0) {
            parsedCards = parseGenericFormat(query);
        }

        if (parsedCards.length === 0) {
            return res.status(400).json({
                error: 'No valid cards found in deck list'
            });
        }

        // Lookup all cards
        const deckCardData: DeckCardData[] = [];
        let commander: string | null = null;
        let foundCount = 0;
        let notFoundCount = 0;

        for (const parsedCard of parsedCards) {
            const result = await lookupCard(parsedCard, lang);

            if (result.found && result.card_id) {
                foundCount++;
                deckCardData.push({
                    card_id: result.card_id,
                    count: result.count,
                    zone: parsedCard.zone || 'main',
                    is_commander: result.is_commander
                });

                if (result.is_commander) {
                    commander = result.name;
                }
            } else {
                notFoundCount++;
            }
        }

        // Create or update deck
        let deck: Deck;

        if (deckId) {
            // Update existing deck
            const existingDeck = await Deck.findById(deckId);
            if (!existingDeck) {
                return res.status(404).json({
                    error: 'Deck not found'
                });
            }

            // Check permissions
            if (!existingDeck.isOwner(user.id)) {
                return res.status(403).json({
                    error: 'You can only modify your own decks'
                });
            }

            deck = await existingDeck.update(user.id, {
                name: deckName,
                description: description,
                commander: commander || undefined,
                cards: deckCardData
            });
        } else {
            // Create new deck
            deck = await Deck.create(user.id, deckName, deckCardData, description, commander || undefined);
        }

        return res.json({
            time: Date.now(),
            uid: user.id,
            action: 'save',
            deck_id: deck.id,
            deck_name: deck.name,
            commander: commander,
            cards_found: foundCount,
            cards_not_found: notFoundCount,
            total_cards: deckCardData.reduce((sum, card) => sum + card.count, 0),
            message: deckId ? 'Deck updated successfully' : 'Deck created successfully'
        });
    } catch (error) {
        console.error('Error in handleSave:', error);
        return res.status(500).json({
            error: 'Error saving deck'
        });
    }
}

/**
 * LOAD action: Load a deck and add its cards to the instance
 */
async function handleLoad(
    res: Response,
    user: any,
    instance: Instance,
    deckId: string | undefined,
    deckName: string | undefined
): Promise<Response> {
    if (!deckId && !deckName) {
        return res.status(400).json({
            error: 'Either deck ID "id" or deck name "name" is required for load action'
        });
    }

    try {
        let deck: Deck | null = null;

        if (deckId) {
            // Load by ID (anyone can load any public deck)
            deck = await Deck.findById(deckId);
        } else if (deckName) {
            // Load by name (search for user's decks first, then public)
            deck = await Deck.findByNameAndUser(deckName, user.id);
            if (!deck) {
                const publicDecks = await Deck.findByNamePublic(deckName);
                if (publicDecks.length > 0) {
                    deck = publicDecks[0];
                }
            }
        }

        if (!deck) {
            return res.status(404).json({
                error: 'Deck not found'
            });
        }

        // Get the card IDs (repeated by count)
        const cardIds = await deck.getCardIds();

        // Add cards to the instance
        for (const cardId of cardIds) {
            await instance.addCard(cardId);
        }

        // Fetch all cards grouped by zone
        const deckCards = await Database.prisma.deckCard.findMany({
            where: { deck_id: deck.id }
        });

        // Get card details
        const deckCardIds = deckCards.map(dc => dc.card_id);
        const cardsDetails = await Database.prisma.card.findMany({
            where: { id: { in: deckCardIds } },
            select: {
                id: true,
                name: true
            }
        });

        // Create a map for quick lookup
        const cardsMap = new Map(cardsDetails.map(c => [c.id, c]));

        // Group cards by zone
        interface CardByZone {
            [zone: string]: Array<{
                count: number;
                name: string;
                card_id: string;
                is_commander?: boolean;
            }>;
        }

        const cardsByZone: CardByZone = {
            main: [],
            sideboard: [],
            commander: [],
            companion: [],
            oathbreaker: [],
            wishboard: []
        };

        for (const deckCard of deckCards) {
            const card = cardsMap.get(deckCard.card_id);
            if (!card) continue;

            const zone = (deckCard.zone || 'main') as string;
            if (!cardsByZone[zone]) {
                cardsByZone[zone] = [];
            }

            const cardData: any = {
                count: deckCard.count,
                name: card.name,
                card_id: card.id
            };

            if (deckCard.is_commander) {
                cardData.is_commander = true;
            }

            cardsByZone[zone].push(cardData);
        }

        const uniqueCardIds = await deck.getUniqueCardIds();
        const deckSize = await deck.getDeckSize();

        return res.json({
            time: Date.now(),
            uid: user.id,
            iid: instance.id,
            action: 'load',
            deck_id: deck.id,
            deck_name: deck.name,
            commander: deck.commander,
            unique_cards: uniqueCardIds.length,
            total_cards: deckSize,
            cards_added_to_instance: cardIds.length,
            cards_by_zone: cardsByZone,
            message: 'Deck loaded successfully and cards added to instance'
        });
    } catch (error) {
        console.error('Error in handleLoad:', error);
        return res.status(500).json({
            error: 'Error loading deck'
        });
    }
}

/**
 * DELETE action: Delete a deck (only owner can delete)
 */
async function handleDelete(
    res: Response,
    user: any,
    deckId: string | undefined
): Promise<Response> {
    if (!deckId) {
        return res.status(400).json({
            error: 'Deck ID is required for delete action'
        });
    }

    try {
        const deck = await Deck.findById(deckId);

        if (!deck) {
            return res.status(404).json({
                error: 'Deck not found'
            });
        }

        // Check if user is the owner
        if (!deck.isOwner(user.id)) {
            return res.status(403).json({
                error: 'You can only delete your own decks'
            });
        }

        // Delete the deck
        await deck.delete(user.id);

        return res.json({
            time: Date.now(),
            uid: user.id,
            action: 'delete',
            deck_id: deck.id,
            deck_name: deck.name,
            message: 'Deck deleted successfully'
        });
    } catch (error) {
        console.error('Error in handleDelete:', error);
        return res.status(500).json({
            error: 'Error deleting deck'
        });
    }
}

/**
 * LIST action: List user's decks or search public decks
 */
async function handleList(
    res: Response,
    user: any,
    deckName: string | undefined
): Promise<Response> {
    try {
        let decks: Deck[] = [];

        if (deckName) {
            // Search public decks by name
            decks = await Deck.findByNamePublic(deckName);
        } else {
            // List user's decks
            decks = await Deck.findByUserId(user.id);
        }

        const deckList = await Promise.all(
            decks.map(async (deck) => ({
                id: deck.id,
                name: deck.name,
                description: deck.description,
                commander: deck.commander,
                owner_id: deck.user_id,
                is_owner: deck.user_id === user.id,
                cards_count: await deck.getDeckSize(),
                created_at: deck.created_at,
                updated_at: deck.updated_at
            }))
        );

        return res.json({
            time: Date.now(),
            uid: user.id,
            action: 'list',
            search_name: deckName || null,
            decks_count: decks.length,
            decks: deckList
        });
    } catch (error) {
        console.error('Error in handleList:', error);
        return res.status(500).json({
            error: 'Error listing decks'
        });
    }
}

/**
 * Parse any generic deck format
 * Handles: simple card names, UUIDs, any line with "count name"
 */
function parseGenericFormat(input: string): ParsedCard[] {
    const cards: ParsedCard[] = [];
    const lines = input.split('\n').filter(line => line.trim().length > 0);

    for (const line of lines) {
        const trimmed = line.trim();

        // Skip section headers and comments
        if (trimmed.startsWith('//') || !trimmed) {
            continue;
        }

        // Match: COUNT NAME (or COUNT UUID)
        const match = trimmed.match(/^(\d+)\s+(.+)$/);
        if (!match) {
            continue;
        }

        const count = parseInt(match[1], 10);
        const nameOrId = match[2].trim();

        // Skip if it looks like a section marker
        if (nameOrId.startsWith('//')) {
            continue;
        }

        cards.push({
            count,
            name: nameOrId,
            is_commander: false,
            zone: 'main'
        });
    }

    return cards;
}

/**
 * Parse Moxfield format: COUNT [optional: (SET) COLLECTOR_NUMBER] NAME
 */
function parseMoxfieldFormat(input: string): ParsedCard[] {
    const cards: ParsedCard[] = [];
    const lines = input.split('\n').filter(line => line.trim().length > 0);

    for (const line of lines) {
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('//')) {
            continue;
        }

        // Match: COUNT [optional: (SET) COLLECTOR_NUMBER] NAME
        const match = trimmed.match(/^(\d+)\s+(.+?)(?:\s+\(([A-Z0-9]+)\)\s+(\d+))?$/);
        if (!match) {
            continue;
        }

        const count = parseInt(match[1], 10);
        const name = match[2].trim();
        const set = match[3];
        const collector_number = match[4];

        cards.push({
            count,
            name,
            set,
            collector_number,
            is_commander: false
        });
    }

    return cards;
}

/**
 * Parse Deckstats format: COUNT [SET#COLLECTOR] NAME [#!Commander]
 * With zone detection via //Main, //Sideboard, //Commander sections
 * Example: 1 [ONC#114] Adriana, Captain of the Guard
 * Example: 1 [FIC#188] Tifa, Martial Artist #!Commander
 */
function parseDeckstatsFormat(input: string): ParsedCard[] {
    const cards: ParsedCard[] = [];
    const lines = input.split('\n');
    
    let currentZone = 'main'; // Default zone

    for (const line of lines) {
        const trimmed = line.trim();

        // Skip empty lines
        if (!trimmed) {
            continue;
        }

        // Detect zone markers
        if (trimmed === '//Main') {
            currentZone = 'main';
            continue;
        } else if (trimmed === '//Sideboard') {
            currentZone = 'sideboard';
            continue;
        } else if (trimmed === '//Commander') {
            currentZone = 'commander';
            continue;
        } else if (trimmed === '//Companion') {
            currentZone = 'companion';
            continue;
        } else if (trimmed === '//Oathbreaker') {
            currentZone = 'oathbreaker';
            continue;
        } else if (trimmed === '//Wishboard') {
            currentZone = 'wishboard';
            continue;
        }

        // Skip other comment lines
        if (trimmed.startsWith('//')) {
            continue;
        }

        // Check for commander marker
        const isCommander = trimmed.includes('#!Commander');
        const cleanLine = trimmed.replace('#!Commander', '').trim();

        // Match: COUNT [optional: [SET#COLLECTOR]] NAME
        const match = cleanLine.match(/^(\d+)\s+(?:\[([A-Z0-9]+)#(\d+)\]\s+)?(.+)$/);
        if (!match) {
            continue;
        }

        const count = parseInt(match[1], 10);
        const set = match[2];
        const collector_number = match[3];
        const name = match[4].trim();

        cards.push({
            count,
            name,
            set,
            collector_number,
            is_commander: isCommander,
            zone: currentZone
        });
    }

    return cards;
}

/**
 * Look up a card in the database
 */
async function lookupCard(
    parsedCard: ParsedCard,
    lang: string
): Promise<DeckCard> {
    // First, try exact match with set and collector_number
    if (parsedCard.set && parsedCard.collector_number) {
        const card = await Database.prisma.card.findFirst({
            where: {
                name: parsedCard.name,
                set_id: parsedCard.set.toLowerCase(),
                collector_number: parsedCard.collector_number,
                lang: lang
            },
            select: {
                id: true,
                name: true,
                rarity: true,
                faces: {
                    select: { type_line: true },
                    take: 1
                }
            }
        });

        if (card) {
            return {
                count: parsedCard.count,
                name: card.name,
                set: parsedCard.set,
                collector_number: parsedCard.collector_number,
                lang: lang,
                card_id: card.id,
                rarity: card.rarity,
                type_line: card.faces[0]?.type_line,
                is_commander: parsedCard.is_commander,
                zone: parsedCard.zone || 'main',
                found: true,
                found_as: 'exact',
                match_confidence: 1.0
            };
        }
    }

    // If set/collector not provided or not found, search by name in English first
    let card = await Database.prisma.card.findFirst({
        where: {
            name: {
                contains: parsedCard.name,
                mode: 'insensitive'
            },
            lang: 'en',
            set: { NOT: { type: 'alchemy' } } // Exclude alchemy sets
        },
        select: {
            id: true,
            name: true,
            rarity: true,
            set_id: true,
            collector_number: true,
            faces: {
                select: { type_line: true },
                take: 1
            }
        },
        take: 1
    });

    if (!card && lang !== 'en') {
        // If not found in English, try the requested language
        card = await Database.prisma.card.findFirst({
            where: {
                name: {
                    contains: parsedCard.name,
                    mode: 'insensitive'
                },
                lang: lang,
                set: { NOT: { type: 'alchemy' } }
            },
            select: {
                id: true,
                name: true,
                rarity: true,
                set_id: true,
                collector_number: true,
                faces: {
                    select: { type_line: true },
                    take: 1
                }
            },
            take: 1
        });
    }

    if (card) {
        return {
            count: parsedCard.count,
            name: card.name,
            set: card.set_id.toUpperCase(),
            collector_number: card.collector_number,
            lang: lang,
            card_id: card.id,
            rarity: card.rarity,
            type_line: card.faces[0]?.type_line,
            is_commander: parsedCard.is_commander,
            zone: parsedCard.zone || 'main',
            found: true,
            found_as: 'partial',
            match_confidence: 0.8
        };
    }

    // Card not found
    return {
        count: parsedCard.count,
        name: parsedCard.name,
        set: parsedCard.set,
        collector_number: parsedCard.collector_number,
        lang: lang,
        is_commander: parsedCard.is_commander,
        zone: parsedCard.zone || 'main',
        found: false,
        found_as: 'not_found',
        match_confidence: 0
    };
}
