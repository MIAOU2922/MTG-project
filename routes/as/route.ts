import { Request, Response, Router } from "express";
import User from "@/database/User";
import Instance from "@/database/Instance";
import { uid } from "@/utils";

export const apiSearchRouter = Router();
apiSearchRouter.get('/as', asHandler);

async function asHandler(req: Request, res: Response) {
    try {
        const userId = uid(req);
        const user = await User.findOrCreate(userId);
        await user.updateLastSeen();

        const query = req.query.q as string;
        
        if (!query) {
            return res.status(400).json({
                error: 'Search query parameter "q" is required'
            });
        }


        // Récupérer l'instance du joueur (comme dans /at)
        let playerInstance = await managePlayerInstance(user);

        // If the user is not in any instance, try to find or create one and add the user
        if (!playerInstance) {
            // Try to find an available instance (reuse logic from Instance.findAvailable if needed)
            playerInstance = await Instance.findAvailable();
            if (!playerInstance) {
                // If no available instance, create a new one
                playerInstance = await Instance.createWithRotation(user);
            }
            // Add user to the instance if not already present
            if (playerInstance && !playerInstance.hasUser(user.id)) {
                await playerInstance.addUser(user.id);
            }
        }
        const searchResults = await performSearch(query);

        // Ajouter les IDs des cartes trouvées à l'instance si elle existe
        if (playerInstance) {
            const cardIds = (searchResults.items as { id: string }[]).map((card: { id: string }) => card.id).filter((id: string) => id);
            for (const cardId of cardIds) {
                await playerInstance.addCard(cardId);
            }
            // Mise à jour du last_seen fait automatiquement dans user.updateLastSeen()
        }

        return res.json({
            time: Date.now(),
            uid: user.id,
            query: query,
            count: searchResults.count,
            instance_id: playerInstance?.id,
            results: (searchResults.items as any[]).map((item: any) => ({
                id: item.id,
                name: item.name,
                printed_name: item.printed_name,
                set: item.set?.set,
                collector_number: item.collector_number,
                lang: item.lang,
                rarity: item.rarity,
                faces: (item.faces as any[]).map((face: any) => ({
                    name: face.name,
                    type_line: face.type_line,
                    printed_type_line: face.printed_type_line,
                    mana_cost: face.mana_cost,
                    cmc: face.cmc,
                    power: face.power,
                    toughness: face.toughness,
                    loyalty: face.loyalty,
                    colors: face.colors,
                    color_identities: face.color_identities,
                    keywords: face.keywords,
                    oracle_text: face.oracle?.text,
                    printed_text: face.printed_text,
                    flavor_text: face.flavor_text,
                })),
            })),
        });
    } catch (error) {
        console.error('Error in asHandler:', error);
        return res.status(500).json({ 
            error: 'Error performing search' 
        });
    }
}
import Database from "@/database/Database";

// Recherche les cartes dont le nom contient le texte recherché (insensible à la casse)
async function performSearch(query: string) {
    const filters = parseQuery(query);
    
    const where: any = {};
    const faceConditions: any = {};
    
    // Handle exact name match
    if (filters.exact_name) {
        where.name = filters.exact_name;
    }
    

    // Handle negated terms
    if (filters.negated_terms) {
        where.NOT = filters.negated_terms.map((term: string) => ({
            name: { contains: term, mode: 'insensitive' }
        }));
    }

    // Exclure les sets de type 'alchemy' sauf si explicitement demandé
    const settypeFilter = (filters.settype || filters.st || '').toLowerCase();
    if (settypeFilter !== 'alchemy') {
        where.NOT = [
            ...(where.NOT || []),
            { set: { type: 'alchemy' } }
        ];
    }
    
    // Recherche simple uniquement si aucun filtre n'est présent ET qu'il y a du texte libre
    if (filters.name) {
        where.name = { contains: filters.name, mode: 'insensitive' };
    } else if (
        Object.keys(filters).length === 0 ||
        (Object.keys(filters).length === 1 && filters.lang && query.trim() !== '')
    ) {
        // Recherche simple : uniquement dans le nom anglais et le nom traduit
        where.OR = [
            { name: { contains: query, mode: 'insensitive' } },
            { printed_name: { contains: query, mode: 'insensitive' } }
        ];
    }
    
    if (filters.set) {
        where.set = { set: filters.set };
    }
    
    // Filtre de langue : par défaut en anglais si non spécifié
    if (filters.lang) {
        where.lang = filters.lang;
    } else {
        // Si aucune langue n'est spécifiée, filtrer par défaut sur l'anglais
        where.lang = 'en';
    }
    
    if (filters.rarity) {
        where.rarity = filters.rarity;
    }
    
    if (filters.collector_number) {
        where.collector_number = filters.collector_number;
    }
    
    if (filters.artist) {
        // Artist search - this would need to be implemented in faces or a separate table
        // For now, skip or implement basic search
    }
    
    if (filters.border) {
        // Border search - not in our schema yet
    }
    
    if (filters.frame) {
        // Frame search - not in our schema yet
    }
    
    if (filters.game) {
        // Game availability - not in our schema yet
    }
    
    if (filters.year) {
        if (typeof filters.year === 'object') {
            const { operator, value } = filters.year;
            where.release_at = { [operator]: new Date(`${value}-01-01`) };
        } else {
            where.release_at = { gte: new Date(`${filters.year}-01-01`), lt: new Date(`${parseInt(filters.year) + 1}-01-01`) };
        }
    }
    
    // Filters applied to Face table

    // Gestion correcte de type et subtype (ex: t:creature st:rat)
    if (filters.type && filters.subtype) {
        faceConditions.AND = [
            { type_line: { contains: filters.type, mode: 'insensitive' } },
            { type_line: { contains: filters.subtype, mode: 'insensitive' } }
        ];
    } else if (filters.type) {
        faceConditions.type_line = { contains: filters.type, mode: 'insensitive' };
    } else if (filters.subtype) {
        faceConditions.type_line = { contains: filters.subtype, mode: 'insensitive' };
    }
    
    if (filters.oracle) {
        faceConditions.oracle = { text: { contains: filters.oracle, mode: 'insensitive' } };
    }
    
    if (filters.fulloracle) {
        // Full oracle includes reminder text - for now same as oracle
        faceConditions.oracle = { text: { contains: filters.fulloracle, mode: 'insensitive' } };
    }
    
    if (filters.keyword) {
        // Recherche insensible à la casse dans keywords, oracle_text et printed_text
        const kw = filters.keyword.toLowerCase();
        faceConditions.OR = [
            { keywords: { has: kw } },
            { oracle: { text: { contains: kw, mode: 'insensitive' } } },
            { printed_text: { contains: kw, mode: 'insensitive' } }
        ];
    }
    
    if (filters.flavor_text) {
        faceConditions.flavor_text = { contains: filters.flavor_text, mode: 'insensitive' };
    }
    
    if (filters.mana_cost) {
        faceConditions.mana_cost = { contains: filters.mana_cost };
    }
    
    if (filters.cmc) {
        if (typeof filters.cmc === 'object') {
            const { operator, value } = filters.cmc;
            faceConditions.cmc = { [operator]: value };
        } else {
            faceConditions.cmc = filters.cmc;
        }
    }
    
    if (filters.power) {
        if (typeof filters.power === 'object') {
            const { operator, value } = filters.power;
            faceConditions.power = { [operator]: value.toString() };
        } else {
            faceConditions.power = filters.power.toString();
        }
    }
    
    if (filters.toughness) {
        if (typeof filters.toughness === 'object') {
            const { operator, value } = filters.toughness;
            faceConditions.toughness = { [operator]: value.toString() };
        } else {
            faceConditions.toughness = filters.toughness.toString();
        }
    }
    
    if (filters.loyalty) {
        if (typeof filters.loyalty === 'object') {
            const { operator, value } = filters.loyalty;
            faceConditions.loyalty = { [operator]: value.toString() };
        } else {
            faceConditions.loyalty = filters.loyalty.toString();
        }
    }
    
    if (filters.layout) {
        faceConditions.layout = filters.layout;
    }
    
    if (filters.colors) {
        const { mode, colors } = filters.colors;
        switch (mode) {
            case 'exactly':
                faceConditions.colors = { equals: colors };
                break;
            case 'including':
                faceConditions.colors = { hasEvery: colors };
                break;
            case 'atmost':
                faceConditions.colors = { hasSome: colors };
                break;
        }
    }
    
    if (filters.color_identity) {
        const { mode, colors } = filters.color_identity;
        switch (mode) {
            case 'exactly':
                faceConditions.color_identities = { equals: colors };
                break;
            case 'including':
                faceConditions.color_identities = { hasEvery: colors };
                break;
            case 'atmost':
                faceConditions.color_identities = { hasSome: colors };
                break;
        }
    }
    
    // Handle is: filters
    if (filters.is) {
        const isValue = filters.is.toLowerCase();
        switch (isValue) {
            case 'spell':
                // Les "spells" sont toutes les cartes sauf les permanents (créature, artefact, enchantement, planeswalker, terrain)
                faceConditions.AND = [
                    { type_line: { not: { contains: 'creature', mode: 'insensitive' } } },
                    { type_line: { not: { contains: 'artifact', mode: 'insensitive' } } },
                    { type_line: { not: { contains: 'enchantment', mode: 'insensitive' } } },
                    { type_line: { not: { contains: 'planeswalker', mode: 'insensitive' } } },
                    { type_line: { not: { contains: 'land', mode: 'insensitive' } } }
                ];
                break;
            case 'permanent':
                // Les permanents sont : créature, artefact, enchantement, planeswalker, terrain
                faceConditions.OR = [
                    { type_line: { contains: 'creature', mode: 'insensitive' } },
                    { type_line: { contains: 'artifact', mode: 'insensitive' } },
                    { type_line: { contains: 'enchantment', mode: 'insensitive' } },
                    { type_line: { contains: 'planeswalker', mode: 'insensitive' } },
                    { type_line: { contains: 'land', mode: 'insensitive' } }
                ];
                break;
            case 'vanilla':
                // Creatures with no text
                faceConditions.AND = [
                    { type_line: { contains: 'creature', mode: 'insensitive' } },
                    { printed_text: null }
                ];
                break;
            case 'reprint':
                // This would need a more complex query
                break;
            case 'foil':
            case 'nonfoil':
            case 'etched':
                // Not in our schema
                break;
            // Add more is: cases as needed
        }
    }
    
    // Handle negated is: filters
    if (filters.not) {
        const notValue = filters.not.toLowerCase();
        switch (notValue) {
            case 'reprint':
                // This would need a more complex query
                break;
            // Add more not: cases as needed
        }
    }
    
    // Apply face conditions if any exist
    if (Object.keys(faceConditions).length > 0) {
        where.faces = { some: faceConditions };
    }
    
    // Handle OR logic if specified
    if (filters.or && where.OR) {
        // This is a simplified implementation
        // Full OR logic would require more complex parsing
    }
    
    const cards = await Database.prisma.card.findMany({
        where,
        select: {
            id: true,
            name: true,
            printed_name: true,
            set: true,
            collector_number: true,
            lang: true,
            rarity: true,
            faces: {
                select: {
                    name: true,
                    type_line: true,
                    printed_type_line: true,
                    mana_cost: true,
                    cmc: true,
                    power: true,
                    toughness: true,
                    loyalty: true,
                    colors: true,
                    color_identities: true,
                    keywords: true,
                    oracle: {
                        select: {
                            text: true,
                        }
                    },
                    printed_text: true,
                    flavor_text: true,
                }
            }
        },
        take: 240, // Limite de sécurité pour éviter les réponses trop volumineuses
    });
    
    return {
        query,
        count: cards.length,
        items: cards,
    };
}

function parseQuery(query: string): Record<string, any> {
    const filters: Record<string, any> = {};
    const nameParts: string[] = [];
    
    // Split by spaces but handle quoted strings and parentheses
    const tokens = tokenizeQuery(query);
    
    for (const token of tokens) {
        if (token.includes(':')) {
            const [key, value] = token.split(':', 2);
            const cleanValue = value.replace(/^["']|["']$/g, ''); // Remove quotes
            
            switch (key.toLowerCase()) {
                // Basic filters
                case 'name':
                case 'n':
                    filters.name = cleanValue;
                    break;
                case 'set':
                case 's':
                case 'edition':
                case 'e':
                    filters.set = cleanValue;
                    break;
                case 'lang':
                case 'language':
                case 'l':
                    filters.lang = cleanValue;
                    break;
                case 'rarity':
                case 'r':
                    filters.rarity = cleanValue;
                    break;
                case 'collector_number':
                case 'cn':
                case 'number':
                    filters.collector_number = cleanValue;
                    break;
                case 'type':
                case 't':
                    filters.type = cleanValue;
                    break;
                case 'subtype':
                case 'st':
                    filters.subtype = cleanValue;
                    break;
                case 'oracle':
                case 'o':
                    filters.oracle = cleanValue;
                    break;
                case 'fulloracle':
                case 'fo':
                    filters.fulloracle = cleanValue;
                    break;
                case 'keyword':
                case 'kw':
                    filters.keyword = cleanValue;
                    break;
                case 'flavor':
                case 'ft':
                    filters.flavor_text = cleanValue;
                    break;
                case 'artist':
                case 'a':
                    filters.artist = cleanValue;
                    break;
                case 'watermark':
                case 'wm':
                    filters.watermark = cleanValue;
                    break;
                case 'border':
                    filters.border = cleanValue;
                    break;
                case 'frame':
                    filters.frame = cleanValue;
                    break;
                case 'stamp':
                    filters.stamp = cleanValue;
                    break;
                case 'game':
                    filters.game = cleanValue;
                    break;
                case 'format':
                case 'f':
                    filters.format = cleanValue;
                    break;
                case 'banned':
                    filters.banned = cleanValue;
                    break;
                case 'restricted':
                    filters.restricted = cleanValue;
                    break;
                case 'cube':
                    filters.cube = cleanValue;
                    break;
                case 'block':
                case 'b':
                    filters.block = cleanValue;
                    break;
                case 'settype':
                case 'st':
                    filters.settype = cleanValue;
                    break;
                case 'year':
                    filters.year = parseNumericValue(cleanValue);
                    break;
                case 'date':
                    filters.date = cleanValue;
                    break;
                case 'prints':
                    filters.prints = parseNumericValue(cleanValue);
                    break;
                case 'sets':
                    filters.sets = parseNumericValue(cleanValue);
                    break;
                case 'paperprints':
                    filters.paperprints = parseNumericValue(cleanValue);
                    break;
                case 'papersets':
                    filters.papersets = parseNumericValue(cleanValue);
                    break;
                case 'usd':
                    // Prix non supporté - ignoré
                    break;
                case 'eur':
                    // Prix non supporté - ignoré
                    break;
                case 'tix':
                    // Prix non supporté - ignoré
                    break;
                case 'cheapest':
                    // Prix non supporté - ignoré
                    break;
                case 'art':
                case 'atag':
                case 'arttag':
                    filters.art_tag = cleanValue;
                    break;
                case 'function':
                case 'otag':
                case 'oracletag':
                    filters.oracle_tag = cleanValue;
                    break;
                case 'mana':
                case 'm':
                    filters.mana_cost = cleanValue;
                    break;
                case 'manavalue':
                case 'mv':
                case 'cmc':
                    filters.cmc = parseNumericValue(cleanValue);
                    break;
                case 'power':
                case 'pow':
                    filters.power = parseNumericValue(cleanValue);
                    break;
                case 'toughness':
                case 'tou':
                    filters.toughness = parseNumericValue(cleanValue);
                    break;
                case 'loyalty':
                case 'loy':
                    filters.loyalty = parseNumericValue(cleanValue);
                    break;
                case 'powtou':
                case 'pt':
                    filters.powtou = parseNumericValue(cleanValue);
                    break;
                case 'colors':
                case 'color':
                case 'c':
                    filters.colors = parseColorValue(cleanValue);
                    break;
                case 'coloridentity':
                case 'identity':
                case 'id':
                    filters.color_identity = parseColorValue(cleanValue);
                    break;
                case 'commander':
                    filters.commander = parseColorValue(cleanValue);
                    break;
                case 'devotion':
                    filters.devotion = cleanValue;
                    break;
                case 'produces':
                    filters.produces = cleanValue;
                    break;
                case 'layout':
                    filters.layout = cleanValue;
                    break;
                case 'is':
                    filters.is = cleanValue;
                    break;
                case 'not':
                    filters.not = cleanValue;
                    break;
                case 'has':
                    filters.has = cleanValue;
                    break;
                case 'include':
                    filters.include = cleanValue;
                    break;
                case 'in':
                    filters.in = cleanValue;
                    break;
                case 'new':
                    filters.new = cleanValue;
                    break;
                case 'unique':
                    filters.unique = cleanValue;
                    break;
                case 'display':
                    filters.display = cleanValue;
                    break;
                case 'order':
                    filters.order = cleanValue;
                    break;
                case 'direction':
                case 'dir':
                    filters.direction = cleanValue;
                    break;
                case 'prefer':
                    filters.prefer = cleanValue;
                    break;
                default:
                    // If unknown filter, treat as name search
                    nameParts.push(token);
                    break;
            }
        } else if (token.startsWith('!')) {
            // Exact name match
            filters.exact_name = token.slice(1).replace(/^['"]|['"]$/g, '');
        } else if (token.startsWith('-')) {
            // Negated term
            const negatedToken = token.slice(1);
            if (negatedToken.includes(':')) {
                const [key, value] = negatedToken.split(':', 2);
                filters[`-${key}`] = value.replace(/^['"]|['"]$/g, '');
            } else {
                filters.negated_terms = filters.negated_terms || [];
                filters.negated_terms.push(negatedToken);
            }
        } else if (token.toLowerCase() === 'or') {
            filters.or = true;
        } else if (token.match(/^\/.*\/$/)) {
            // Regular expression
            filters.regex = token.slice(1, -1);
        } else {
            // Si le token n'est pas un filtre connu, on le traite comme un sous-type potentiel
            // (ex: "rat" tout seul => filtre sur le sous-type)
            if (!filters.name && !filters.subtype) {
                filters.subtype = token;
            } else {
                nameParts.push(token);
            }
        }
    }
    
    if (nameParts.length > 0) {
        filters.name = nameParts.join(' ');
    }
    
    return filters;
}

function tokenizeQuery(query: string): string[] {
    const tokens: string[] = [];
    let current = '';
    let inQuotes = false;
    let quoteChar = '';
    let inParens = 0;
    
    for (let i = 0; i < query.length; i++) {
        const char = query[i];
        
        if (inQuotes) {
            current += char;
            if (char === quoteChar) {
                inQuotes = false;
                quoteChar = '';
            }
        } else if (char === '"' || char === "'") {
            inQuotes = true;
            quoteChar = char;
            current += char;
        } else if (char === '(') {
            inParens++;
            current += char;
        } else if (char === ')') {
            inParens--;
            current += char;
        } else if (char === ' ' && inParens === 0) {
            if (current.trim()) {
                tokens.push(current.trim());
            }
            current = '';
        } else {
            current += char;
        }
    }
    
    if (current.trim()) {
        tokens.push(current.trim());
    }
    
    return tokens;
}

function parseNumericValue(value: string): { operator: string; value: number } | number {
    if (value.startsWith('>=')) {
        return { operator: 'gte', value: parseFloat(value.slice(2)) };
    } else if (value.startsWith('<=')) {
        return { operator: 'lte', value: parseFloat(value.slice(2)) };
    } else if (value.startsWith('>')) {
        return { operator: 'gt', value: parseFloat(value.slice(1)) };
    } else if (value.startsWith('<')) {
        return { operator: 'lt', value: parseFloat(value.slice(1)) };
    } else if (value.startsWith('=')) {
        return parseFloat(value.slice(1));
    } else {
        return parseFloat(value);
    }
}

function parseColorValue(value: string): { mode: string; colors: string[] } {
    const colors = [];
    let mode = 'including'; // Default mode
    
    if (value.startsWith('>=')) {
        mode = 'including';
        value = value.slice(2);
    } else if (value.startsWith('<=')) {
        mode = 'atmost';
        value = value.slice(2);
    } else if (value.startsWith('=')) {
        mode = 'exactly';
        value = value.slice(1);
    }
    
    // Parse color letters (WUBRG)
    for (const char of value.toUpperCase()) {
        if (['W', 'U', 'B', 'R', 'G', 'C'].includes(char)) {
            colors.push(char);
        }
    }
    
    return { mode, colors };
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