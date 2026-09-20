import { Request, Response, Router } from "express";
import User from "@/database/User";
import Instance from "@/database/Instance";
import { getUserId } from "@/utils";

export const apiSearchRouter = Router();
apiSearchRouter.get('/as', asHandler);

async function asHandler(req: Request, res: Response) {
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
            // Pour chaque carte trouvée, ajouter toutes ses faces à l'instance
            for (const card of searchResults.items as any[]) {
                if (!card.id) continue;
                
                // Récupérer toutes les faces de cette carte pour avoir leurs indices
                const facesWithIndex = await Database.prisma.face.findMany({
                    where: { card_id: card.id },
                    select: { index: true },
                    orderBy: { index: 'asc' }
                });
                
                // Ajouter chaque face individuellement à l'instance
                for (const face of facesWithIndex) {
                    const faceId = `${card.id}:${face.index}`;
                    await playerInstance.addCard(faceId);
                }
            }
            // Mise à jour du last_seen fait automatiquement dans user.updateLastSeen()
        }

        return res.json({
            link_type: 's',
            link_id: query,
            iid: playerInstance?.id || null,
            uid: user.id,
            time: Date.now(),
            data: {
                query: query,
                count: searchResults.count,
                results: (searchResults.items as any[]).map((item: any) => {
                    const oracleId = (item.faces as any[] || []).find((f: any) => f.oracle_id)?.oracle_id || null;
                    
                    return {
                        id: item.id,
                        name: item.name,
                        set: item.set?.set,
                        collector_number: item.collector_number,
                        lang: item.lang,
                        oracle_id: oracleId,
                        faces: (item.faces as any[])?.length || 0
                    };
                })
            }
        });
    } catch (error) {
        console.error('Error in asHandler:', error);
        return res.status(500).json({ 
            error: 'Error performing search' 
        });
    }
}
import Database from "@/database/Database";

// =============================================================================
// Moteur de recherche — syntaxe Scryfall (https://scryfall.com/docs/syntax)
// =============================================================================

/** Types de sets exclus par défaut (révélés par include:extras ou demande explicite) */
const BLACKLISTED_SET_TYPES = ['alchenemy', 'alchemy', 'funny'];

/** Types de cartes "extra" cachés par défaut, comme sur Scryfall */
const HIDDEN_CARD_TYPES = ['scheme', 'vanguard', 'phenomenon'];

const RARITY_ORDER = ['common', 'uncommon', 'rare', 'mythic'];

const COLOR_COMBOS: Record<string, string> = {
    azorius: 'WU', orzhov: 'WB', boros: 'RW', selesnya: 'GW', dimir: 'UB',
    izzet: 'UR', simic: 'GU', rakdos: 'RB', golgari: 'BG', gruul: 'RG',
    bant: 'GWU', esper: 'WUB', grixis: 'UBR', jund: 'BRG', naya: 'RGW',
    abzan: 'WBG', jeskai: 'URW', sultai: 'BGU', mardu: 'RWB', temur: 'GUR',
    chaos: 'UBRG', aggression: 'BRGW', altruism: 'RGWU', growth: 'GWUB', artifice: 'WUBR',
};

const LANG_CODES: Record<string, string> = {
    english: 'en', japanese: 'ja', korean: 'ko', russian: 'ru', french: 'fr',
    german: 'de', spanish: 'es', italian: 'it', portuguese: 'pt',
    chinese: 'zhs', simplified: 'zhs', simplifiedchinese: 'zhs', 'simplified-chinese': 'zhs',
    traditional: 'zht', traditionalchinese: 'zht', 'traditional-chinese': 'zht',
};

/** Clés qui acceptent la syntaxe opérateur Scryfall (=, >=, <=, >, <, !=) */
const OP_KEYS = new Set([
    'mv', 'manavalue', 'cmc', 'pow', 'power', 'tou', 'toughness', 'loy', 'loyalty',
    'cn', 'number', 'r', 'rarity', 'year', 'date',
    'c', 'color', 'colors', 'id', 'identity',
]);

/** Condition de recherche normalisée */
interface SearchCond { key: string; value: string; negated: boolean; }

/** Alias Scryfall → clé canonique (syntaxe identique à Scryfall) */
const KEY_ALIASES: Record<string, string> = {
    name: 'name', n: 'name',
    oracle: 'oracle', o: 'oracle',
    fulloracle: 'fulloracle', fo: 'fulloracle',
    keyword: 'keyword', kw: 'keyword',
    flavor: 'flavor', ft: 'flavor',
    type: 'type', t: 'type',
    mana: 'mana', m: 'mana',
    manavalue: 'cmc', mv: 'cmc', cmc: 'cmc',
    power: 'power', pow: 'power',
    toughness: 'toughness', tou: 'toughness',
    loyalty: 'loyalty', loy: 'loyalty',
    powtou: 'powtou', pt: 'powtou',
    colors: 'colors', color: 'colors', c: 'colors',
    identity: 'identity', id: 'identity',
    commander: 'identity',
    layout: 'layout',
    set: 'set', s: 'set', edition: 'set', e: 'set',
    cn: 'cn', number: 'cn',
    lang: 'lang', language: 'lang', l: 'lang',
    rarity: 'rarity', r: 'rarity',
    settype: 'settype', st: 'settype',
    block: 'block', b: 'block',
    format: 'format', f: 'format',
    banned: 'banned',
    restricted: 'restricted',
    year: 'year',
    date: 'date',
    game: 'game',
    artist: 'artist', a: 'artist',
    watermark: 'watermark', wm: 'watermark',
    art: 'art', atag: 'art', arttag: 'art',
    function: 'function', otag: 'function', oracletag: 'function',
    in: 'in',
    include: 'include',
    is: 'is',
    not: 'not',
};

/**
 * Recherche syntaxe Scryfall.
 * - Recherche basique (mots libres) : nom, type line et texte Oracle
 * - Recherches avancées : champs de texte dédiés (o:, t:, ft:, kw:, ...)
 */
async function performSearch(query: string) {
    const branches = parseQuery(query); // SearchCond[][] (OR de AND)

    // Ce que la requête demande explicitement (pour skipper les exclusions par défaut)
    const meta = collectMeta(branches);

    // Type du set ciblé explicitement (set:<code>) → révèle les cartes de ce set
    if (meta.setCode) {
        const setInfo = await Database.prisma.set.findFirst({
            where: { set: meta.setCode },
            select: { type: true },
        });
        meta.setTypeFromDb = setInfo?.type ?? null;
    }

    const defaults = buildDefaultExclusions(meta);
    const branchWheres = branches.map(branch => buildBranchWhere(branch, defaults));

    let where: any;
    if (branchWheres.length === 1) {
        where = branchWheres[0];
    } else {
        where = { OR: branchWheres };
    }

    // Langue : anglais par défaut (sauf lang: spécifié ou lang:any)
    if (meta.lang) {
        where = { AND: [where, { lang: meta.lang }] };
    } else if (!meta.anyLang) {
        where = { AND: [where, { lang: 'en' }] };
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
            image_status: true,
            set_id: true,
            faces: {
                select: {
                    name: true,
                    oracle_id: true,
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

    // Remplacer les cartes avec image_status problématique par leurs versions alternatives
    const processedCards = await replaceProblematicCards(cards);

    return {
        query,
        count: processedCards.length,
        items: processedCards,
    };
}

/** Agrège ce que la requête demande explicitement */
function collectMeta(branches: SearchCond[][]): any {
    const meta: any = {
        setCode: null,
        settype: null,
        include: null,
        lang: null,
        anyLang: false,
        explicitTypes: new Set<string>(),
        explicitIsTypes: new Set<string>(),
        setTypeFromDb: null,
    };

    for (const branch of branches) {
        for (const c of branch) {
            const k = KEY_ALIASES[c.key] || c.key;
            if (!c.negated) {
                if (k === 'set') meta.setCode = c.value.toLowerCase();
                if (k === 'settype') meta.settype = c.value.toLowerCase();
                if (k === 'include') meta.include = c.value.toLowerCase();
                if (k === 'type') meta.explicitTypes.add(c.value.toLowerCase());
                if (k === 'is') meta.explicitIsTypes.add(c.value.toLowerCase());
            }
            if (k === 'lang') {
                const lv = c.value.toLowerCase();
                if (lv === 'any') meta.anyLang = true;
                else meta.lang = LANG_CODES[lv] || lv.toLowerCase();
            }
        }
    }
    return meta;
}

/** Exclusions par défaut (types de sets blacklistés, cartes extra, noms A-, mémorabilia) */
function buildDefaultExclusions(meta: any): { cardNot: any[]; faceNot: any[] } {
    const cardNot: any[] = [];
    const faceNot: any[] = [];
    const extras = meta.include === 'extras';

    for (const type of BLACKLISTED_SET_TYPES) {
        const included =
            extras ||
            meta.settype === type ||
            meta.include === type ||
            meta.explicitIsTypes.has(type) ||
            meta.setTypeFromDb === type;
        if (!included) cardNot.push({ set: { type } });
    }

    const alchemyIncluded =
        extras ||
        meta.settype === 'alchemy' ||
        meta.include === 'alchemy' ||
        meta.explicitIsTypes.has('alchemy') ||
        meta.setTypeFromDb === 'alchemy';
    if (!alchemyIncluded) {
        cardNot.push({ name: { startsWith: 'A-' } });
    }

    for (const type of HIDDEN_CARD_TYPES) {
        if (extras || meta.explicitTypes.has(type)) continue;
        faceNot.push({ type_line: { contains: type, mode: 'insensitive' } });
    }
    if (!extras && !meta.explicitTypes.has('plane')) {
        // Prisma n'accepte pas `mode` dans un `not` imbriqué : on utilise la casse canonique Scryfall
        faceNot.push({
            AND: [
                { type_line: { contains: 'plane', mode: 'insensitive' } },
                { type_line: { not: { contains: 'Planeswalker' } } },
            ],
        });
    }

    const memIncluded = extras || meta.settype === 'memorabilia' || meta.setTypeFromDb === 'memorabilia';
    if (!memIncluded) cardNot.push({ set: { type: 'memorabilia' } });

    return { cardNot, faceNot };
}

/** Construit la clause Prisma d'une branche (AND de conditions + exclusions par défaut) */
function buildBranchWhere(branch: SearchCond[], defaults: { cardNot: any[]; faceNot: any[] }): any {
    const cardAnd: any[] = [];
    const faceAnd: any[] = [];
    const cardNot: any[] = [...defaults.cardNot];
    const faceNot: any[] = [...defaults.faceNot];

    for (const cond of branch) {
        applyCond(cond, { cardAnd, faceAnd, cardNot, faceNot });
    }

    const and: any[] = [...cardAnd];
    if (faceAnd.length > 0 || faceNot.length > 0) {
        const face: any = {};
        if (faceAnd.length > 0) face.AND = faceAnd;
        if (faceNot.length > 0) face.NOT = faceNot;
        and.push({ faces: { some: face } });
    }

    const where: any = {};
    if (and.length > 0) where.AND = and;
    if (cardNot.length > 0) where.NOT = cardNot;
    return where;
}

/** Applique une condition normalisée dans le contexte de la branche */
function applyCond(cond: SearchCond, ctx: any): void {
    const canonical = KEY_ALIASES[cond.key] || cond.key;
    const value = stripQuotes(cond.value);
    const neg = cond.negated;

    const pushCard = (obj: any) => (neg ? ctx.cardNot : ctx.cardAnd).push(obj);
    const pushFace = (obj: any) => (neg ? ctx.faceNot : ctx.faceAnd).push(obj);

    if (cond.key === '~') {
        // Mot libre : recherche basique Scryfall = nom + type line + texte Oracle
        const word = { contains: value, mode: 'insensitive' };
        if (neg) {
            ctx.cardNot.push({
                OR: [
                    { name: word },
                    { printed_name: word },
                    { faces: { some: { OR: [{ type_line: word }, { oracle: { text: word } }] } } },
                ],
            });
        } else {
            ctx.cardAnd.push({
                OR: [
                    { name: word },
                    { printed_name: word },
                    { faces: { some: { OR: [{ type_line: word }, { oracle: { text: word } }] } } },
                ],
            });
        }
        return;
    }

    if (cond.key === '!') {
        // Nom exact (insensible à la casse) — nom de carte OU nom de face (split cards)
        const eq = {
            OR: [
                { name: { equals: value, mode: 'insensitive' } },
                { printed_name: { equals: value, mode: 'insensitive' } },
                { faces: { some: { name: { equals: value, mode: 'insensitive' } } } },
            ],
        };
        (neg ? ctx.cardNot : ctx.cardAnd).push(eq);
        return;
    }

    const ci = { contains: value, mode: 'insensitive' };

    switch (canonical) {
        case 'name':
            pushCard({ OR: [{ name: ci }, { printed_name: ci }] });
            break;
        case 'oracle':
        case 'fulloracle':
            pushFace({ oracle: { text: ci } });
            break;
        case 'keyword': {
            const kw = value.toLowerCase();
            const capKw = kw.charAt(0).toUpperCase() + kw.slice(1); // base stockée en casse Scryfall ("Flying")
            pushFace({
                OR: [
                    { keywords: { has: kw } },
                    { keywords: { has: capKw } },
                    { oracle: { text: ci } },
                    { printed_text: ci },
                ],
            });
            break;
        }
        case 'flavor':
            pushFace({ flavor_text: ci });
            break;
        case 'type':
            pushFace({ type_line: ci });
            break;
        case 'mana':
            pushFace({ mana_cost: ci });
            break;
        case 'cmc': {
            const c = numericCond(value, 'cmc', false);
            if (c) pushFace(c);
            break;
        }
        case 'power': {
            const c = numericListCond(value, 'power');
            if (c) pushFace(c);
            break;
        }
        case 'toughness': {
            const c = numericListCond(value, 'toughness');
            if (c) pushFace(c);
            break;
        }
        case 'loyalty': {
            const c = numericListCond(value, 'loyalty');
            if (c) pushFace(c);
            break;
        }
        case 'powtou':
            break; // non supporté (nécessite de l'arithmétique)
        case 'colors':
            applyColorCond(ctx, value, neg, 'colors');
            break;
        case 'identity':
            applyColorCond(ctx, value, neg, 'color_identities');
            break;
        case 'layout':
            pushFace({ layout: value.toLowerCase() });
            break;
        case 'set':
            pushCard({ set: { set: value.toLowerCase() } });
            break;
        case 'cn': {
            const c = numericListCond(value, 'collector_number');
            if (c) pushCard(c);
            break;
        }
        case 'lang':
            break; // géré par collectMeta
        case 'rarity': {
            const c = rarityCond(value);
            if (c) pushCard(c);
            break;
        }
        case 'settype':
            pushCard({ set: { type: value.toLowerCase() } });
            break;
        case 'format': {
            const fmt = value.toLowerCase();
            pushCard({ legalities: { path: [fmt], equals: 'legal' } });
            break;
        }
        case 'banned': {
            const fmt = value.toLowerCase();
            pushCard({ legalities: { path: [fmt], equals: 'banned' } });
            break;
        }
        case 'restricted': {
            const fmt = value.toLowerCase();
            pushCard({ legalities: { path: [fmt], equals: 'restricted' } });
            break;
        }
        case 'year':
        case 'date': {
            const c = dateCond(value);
            if (c) pushCard(c);
            break;
        }
        case 'is':
            applyIsCond(ctx, value, neg);
            break;
        case 'not':
            // not:x est l'inverse de is:x
            applyIsCond(ctx, value, !neg);
            break;
        case 'include':
            break; // géré par collectMeta
        default:
            break; // non supporté (artist, game, in:, watermark, tags, prix, ...)
    }
}

/** Condition numérique (mv:, pow:, tou:, loy:, cn:) */
function numericCond(value: string, field: string, asString: boolean): any | null {
    const op = numericOp(value);
    if (!op) return null;
    const num = asString ? String(op.num) : op.num;
    const f: any = {};
    switch (op.op) {
        case 'equals': f[field] = num; break;
        case 'gte': f[field] = { gte: num }; break;
        case 'lte': f[field] = { lte: num }; break;
        case 'gt': f[field] = { gt: num }; break;
        case 'lt': f[field] = { lt: num }; break;
        case 'not': f[field] = { not: num }; break;
        default: return null;
    }
    return f;
}

/** Parse un opérateur numérique (>=, <=, !=, >, <, =) */
function numericOp(value: string): { op: string; num: number } | null {
    const ops: [string, string][] = [['>=', 'gte'], ['<=', 'lte'], ['!=', 'not'], ['>', 'gt'], ['<', 'lt'], ['=', 'equals']];
    for (const [sym, op] of ops) {
        if (value.startsWith(sym)) {
            const n = parseFloat(value.slice(sym.length));
            return isNaN(n) ? null : { op, num: n };
        }
    }
    const n = parseFloat(value);
    return isNaN(n) ? null : { op: 'equals', num: n };
}

/**
 * Condition numérique sur un champ STRING (power, toughness, loyalty, cn).
 * Postgres comparerait les chaînes lexicographiquement ("10" < "8"), donc on
 * génère une liste `in` des représentations entières 0..999.
 */
function numericListCond(value: string, field: string): any | null {
    const op = numericOp(value);
    if (!op) return null;
    const MAX = 999;
    const range = (a: number, b: number): string[] => {
        const from = Math.max(0, Math.ceil(a));
        const to = Math.min(MAX, Math.floor(b));
        const out: string[] = [];
        for (let i = from; i <= to; i++) out.push(String(i));
        return out;
    };
    switch (op.op) {
        case 'equals': return { [field]: String(op.num) };
        case 'gte': return { [field]: { in: range(op.num, MAX) } };
        case 'gt': return { [field]: { in: range(op.num + 1, MAX) } };
        case 'lte': return { [field]: { in: range(0, op.num) } };
        case 'lt': return { [field]: { in: range(0, op.num - 1) } };
        case 'not': return { [field]: { not: String(op.num) } };
        default: return null;
    }
}

/** Condition de rareté (r: et comparaisons r>=r, avec codes courts c/u/r/m/s/b) */
function rarityCond(value: string): any | null {
    const v = value.toLowerCase();
    const m = v.match(/^(>=|<=|>|<|!=|=)?(.+)$/);
    if (!m) return null;
    const sym = m[1] || '=';
    const rarityMap: Record<string, string> = { c: 'common', u: 'uncommon', r: 'rare', m: 'mythic', s: 'special', b: 'bonus' };
    const target = rarityMap[m[2]] || m[2];
    const VALID = ['common', 'uncommon', 'rare', 'mythic', 'special', 'bonus'];
    if (!VALID.includes(target)) return null;
    if (sym === '=') return { rarity: target };
    if (sym === '!=') return { NOT: { rarity: target } };
    // Comparaisons : ordre Scryfall common < uncommon < rare < mythic (special/bonus non comparables)
    if (!RARITY_ORDER.includes(target)) return null;
    const idx = RARITY_ORDER.indexOf(target);
    switch (sym) {
        case '>=': return { rarity: { in: RARITY_ORDER.slice(idx) } };
        case '>': return { rarity: { in: RARITY_ORDER.slice(idx + 1) } };
        case '<=': return { rarity: { in: RARITY_ORDER.slice(0, idx + 1) } };
        case '<': return { rarity: { in: RARITY_ORDER.slice(0, idx) } };
        default: return null;
    }
}

/** Condition de date (year:, date:) — retourne un filtre card-level complet */
function dateCond(value: string): any | null {
    const m = value.toLowerCase().match(/^(>=|<=|>|<|!=|=)?(\d{4}(?:-\d{2}-\d{2})?|now|today)$/);
    if (!m) return null;
    const sym = m[1] || '=';
    const raw = m[2];
    const isYear = /^\d{4}$/.test(raw);
    let date: Date;
    if (raw === 'now' || raw === 'today') date = new Date();
    else date = new Date(isYear ? `${raw}-01-01T00:00:00Z` : `${raw}T00:00:00Z`);
    const nextYear = isYear ? new Date(`${parseInt(raw) + 1}-01-01T00:00:00Z`) : null;
    const nextDay = !isYear ? new Date(date.getTime() + 86400000) : null;
    switch (sym) {
        case '=': return { release_at: { gte: date, lt: nextYear || nextDay } };
        case '>': return { release_at: { gt: date } };
        case '>=': return { release_at: { gte: date } };
        case '<': return { release_at: { lt: date } };
        case '<=': return { release_at: isYear ? { lt: nextYear } : { lte: date } };
        case '!=': return { NOT: { release_at: { gte: date, lt: nextYear || nextDay } } };
        default: return null;
    }
}

/** Condition de couleurs / identité de couleur (c:, id:, avec modes et nicknames) */
function applyColorCond(ctx: any, rawValue: string, neg: boolean, field: 'colors' | 'color_identities'): void {
    const { mode, letters } = parseColorValue(rawValue);
    const push = (obj: any) => (neg ? ctx.faceNot : ctx.faceAnd).push(obj);

    if (mode === 'unsupported') return;

    if (!letters) {
        const v = rawValue.trim().toLowerCase();
        if (v === 'colorless' || v === 'c') {
            push({ [field]: { equals: [] } });
        }
        return; // m/multicolor/nombres non supportés
    }

    const list = letters.split('');
    switch (mode) {
        case 'exactly':
            push({ [field]: { equals: list } });
            break;
        case 'including':
            push({ [field]: { hasEvery: list } });
            break;
        case 'atmost': {
            const complement = 'WUBRG'.split('').filter(c => !list.includes(c));
            push({ NOT: { [field]: { hasSome: complement } } });
            break;
        }
        case 'not':
            push({ [field]: { equals: list } });
            break;
    }
}

/** Conditions is: / not: (sous-ensemble supporté de Scryfall) */
function applyIsCond(ctx: any, raw: string, neg: boolean): void {
    const v = raw.toLowerCase();
    const pushFace = (obj: any) => (neg ? ctx.faceNot : ctx.faceAnd).push(obj);
    const pushCard = (obj: any) => (neg ? ctx.cardNot : ctx.cardAnd).push(obj);

    const hasType = (t: string) => ({ type_line: { contains: t, mode: 'insensitive' } });
    const permanentTypes = ['creature', 'artifact', 'enchantment', 'planeswalker', 'land'];

    switch (v) {
        case 'spell':
            // is:spell = aucun type permanent ; not:spell = au moins un type permanent
            // (Prisma n'accepte pas `not: { contains, mode }` imbriqué, on passe par un bloc NOT)
            pushFace({ NOT: permanentTypes.map(hasType) });
            break;
        case 'permanent':
            pushFace({ OR: ['creature', 'artifact', 'enchantment', 'planeswalker', 'land', 'battle', 'kindred'].map(hasType) });
            break;
        case 'vanilla':
            pushFace({ AND: [hasType('creature'), { printed_text: null }] });
            break;
        case 'dfc':
            pushFace({ layout: { in: ['transform', 'modal_dfc', 'meld', 'reversible_card'] } });
            break;
        case 'mdfc':
            pushFace({ layout: 'modal_dfc' });
            break;
        case 'split':
            pushFace({ layout: 'split' });
            break;
        case 'flip':
            pushFace({ layout: 'flip' });
            break;
        case 'transform':
        case 'tdfc':
            pushFace({ layout: 'transform' });
            break;
        case 'meld':
            pushFace({ layout: 'meld' });
            break;
        case 'leveler':
            pushFace({ layout: 'leveler' });
            break;
        case 'adventure':
            pushFace({ layout: 'adventure' });
            break;
        case 'saga':
            pushFace({ layout: 'saga' });
            break;
        case 'class':
            pushFace({ layout: 'class' });
            break;
        case 'mutate':
            pushFace({ layout: 'mutate' });
            break;
        case 'battle':
            pushFace(hasType('battle'));
            break;
        case 'funny':
            pushCard({ set: { type: 'funny' } });
            break;
        case 'alchemy':
            pushCard({ OR: [{ set: { type: 'alchemy' } }, { name: { startsWith: 'A-' } }] });
            break;
        case 'promo':
            pushCard({ set: { type: 'promo' } });
            break;
        case 'hires':
            pushCard({ image_status: 'highres_scan' });
            break;
        case 'historic':
            pushFace({ OR: [hasType('artifact'), hasType('legendary'), hasType('saga')] });
            break;
        case 'party':
            pushFace({ OR: ['cleric', 'rogue', 'warrior', 'wizard'].map(hasType) });
            break;
        case 'commander':
            pushFace({
                OR: [
                    { type_line: { contains: 'legendary creature', mode: 'insensitive' } },
                    hasType('planeswalker'),
                    { oracle: { text: { contains: 'can be your commander', mode: 'insensitive' } } },
                ],
            });
            break;
        case 'partner':
            pushFace({ oracle: { text: { contains: 'partner', mode: 'insensitive' } } });
            break;
        case 'companion':
            pushFace({ oracle: { text: { contains: 'companion', mode: 'insensitive' } } });
            break;
        default:
            break; // non supporté (reprint, reserved, full, foil, lands shortcuts, ...)
    }
}

/**
 * Remplace les cartes avec image_status "missing" ou "placeholder" par des versions alternatives
 * Priorité: anglais > première version disponible avec image valide
 * Supprime la carte si aucune version valide n'existe
 */
async function replaceProblematicCards(cards: any[]): Promise<any[]> {
    const processedCards: any[] = [];
    
    for (const card of cards) {
        const imageStatus = card.image_status;
        
        // Si l'image est OK, garder la carte telle quelle
        if (imageStatus && imageStatus !== 'missing' && imageStatus !== 'placeholder') {
            processedCards.push(card);
            continue;
        }
        
        // Chercher une version alternative avec le même set et collector_number
        const alternatives = await Database.prisma.card.findMany({
            where: {
                set_id: card.set_id,
                collector_number: card.collector_number,
                image_status: {
                    notIn: ['missing', 'placeholder']
                }
            },
            select: {
                id: true,
                name: true,
                printed_name: true,
                set: true,
                collector_number: true,
                lang: true,
                rarity: true,
                image_status: true,
                set_id: true,
                faces: {
                    select: {
                        name: true,
                        oracle_id: true,
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
            orderBy: [
                // Priorité à l'anglais
                { lang: 'asc' }
            ]
        });
        
        if (alternatives.length > 0) {
            // Chercher d'abord une version en anglais
            const englishVersion = alternatives.find(alt => alt.lang === 'en');
            processedCards.push(englishVersion || alternatives[0]);
        }
        // Si aucune alternative valide, on ne retourne pas la carte (elle est omise)
    }
    
    return processedCards;
}

function parseQuery(query: string): SearchCond[][] {
    return parseDNF(tokenizeQuery(query));
}

/** Forme Normale Disjonctive : OR de branches, chaque branche = AND de conditions */
function parseDNF(tokens: string[]): SearchCond[][] {
    const orParts = splitTopLevelOr(tokens);
    if (orParts.length > 1) {
        return orParts.flatMap(part => parseDNF(part));
    }

    let branches: SearchCond[][] = [[]];
    let i = 0;
    while (i < tokens.length) {
        const token = tokens[i];
        if (token === '(') {
            const close = findMatchingParen(tokens, i);
            const inner = parseDNF(tokens.slice(i + 1, close));
            branches = cartesian(branches, inner);
            i = close + 1;
        } else if (token === ')') {
            i++;
        } else {
            const cond = parseCond(token);
            if (cond) branches = cartesian(branches, [[cond]]);
            i++;
        }
    }
    return branches.length ? branches : [[]];
}

function splitTopLevelOr(tokens: string[]): string[][] {
    const parts: string[][] = [];
    let current: string[] = [];
    let depth = 0;
    for (const t of tokens) {
        if (t === '(') depth++;
        if (t === ')') depth--;
        if (t.toLowerCase() === 'or' && depth === 0) {
            parts.push(current);
            current = [];
        } else {
            current.push(t);
        }
    }
    parts.push(current);
    return parts;
}

function findMatchingParen(tokens: string[], openIndex: number): number {
    let depth = 0;
    for (let i = openIndex; i < tokens.length; i++) {
        if (tokens[i] === '(') depth++;
        else if (tokens[i] === ')') {
            depth--;
            if (depth === 0) return i;
        }
    }
    return tokens.length - 1;
}

function cartesian(a: SearchCond[][], b: SearchCond[][]): SearchCond[][] {
    const out: SearchCond[][] = [];
    for (const x of a) for (const y of b) out.push([...x, ...y]);
    return out;
}

function parseCond(token: string): SearchCond | null {
    const t = token.trim();
    if (!t) return null;

    let negated = false;
    let body = t;

    // "!phrase" : nom exact
    if (body.startsWith('!')) {
        return { key: '!', value: stripQuotes(body.slice(1)), negated: false };
    }

    // Négation "-"
    if (body.startsWith('-')) {
        negated = true;
        body = body.slice(1);
    }

    // Syntaxe opérateurs Scryfall : mv=5, pow>=8, r>=r, year<=1994, c=uw, ...
    const opMatch = body.match(/^([a-z]+)(>=|<=|!=|=|>|<)(.+)$/);
    if (opMatch && OP_KEYS.has(opMatch[1].toLowerCase())) {
        return { key: opMatch[1].toLowerCase(), value: opMatch[2] + opMatch[3], negated };
    }

    if (body.includes(':')) {
        const [key, value] = body.split(':', 2);
        const k = key.toLowerCase();
        if (KEY_ALIASES[k]) {
            return { key: k, value: stripQuotes(value), negated };
        }
        // Clé inconnue : traitée comme mot libre (comme Scryfall)
        return { key: '~', value: body, negated };
    }

    if (body.startsWith('/') && body.endsWith('/') && body.length > 2) {
        // Regex brute : non supportée, traitée comme mot libre
        return { key: '~', value: body.slice(1, -1), negated };
    }

    // Mot libre
    return { key: '~', value: stripQuotes(body), negated };
}

function stripQuotes(s: string): string {
    const t = s.trim();
    if (t.length >= 2 && ((t.startsWith('"') && t.endsWith('"')) || (t.startsWith("'") && t.endsWith("'")))) {
        return t.slice(1, -1);
    }
    return t;
}

function tokenizeQuery(query: string): string[] {
    const tokens: string[] = [];
    let current = '';
    let inQuotes = false;
    let quoteChar = '';

    const flush = () => {
        if (current.trim()) {
            tokens.push(current.trim());
            current = '';
        }
    };

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
            flush();
            tokens.push('(');
        } else if (char === ')') {
            flush();
            tokens.push(')');
        } else if (char === ' ') {
            flush();
        } else {
            current += char;
        }
    }
    flush();

    return tokens;
}

/**
 * Parse une valeur de couleur Scryfall : modes =/>=/<=/!=, lettres WUBRG,
 * noms complets (white, blue, ...) et nicknames (azorius, bant, ...).
 */
function parseColorValue(value: string): { mode: string; letters: string } {
    let mode = 'exactly';
    let v = value.trim().toLowerCase();

    if (v.startsWith('>=')) { mode = 'including'; v = v.slice(2); }
    else if (v.startsWith('<=')) { mode = 'atmost'; v = v.slice(2); }
    else if (v.startsWith('!=')) { mode = 'not'; v = v.slice(2); }
    else if (v.startsWith('=')) { v = v.slice(1); }
    else if (v.startsWith('>') || v.startsWith('<')) { return { mode: 'unsupported', letters: '' }; }

    let letters = '';
    for (const part of v.split(/[^a-z]+/)) {
        if (!part) continue;
        if (part.length === 1 && 'wubrg'.includes(part)) {
            letters += part.toUpperCase();
        } else if (COLOR_COMBOS[part]) {
            letters += COLOR_COMBOS[part];
        } else if (part === 'white') letters += 'W';
        else if (part === 'blue') letters += 'U';
        else if (part === 'black') letters += 'B';
        else if (part === 'red') letters += 'R';
        else if (part === 'green') letters += 'G';
    }

    return { mode, letters: [...new Set(letters)].join('') };
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