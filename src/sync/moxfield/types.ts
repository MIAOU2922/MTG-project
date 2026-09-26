// =============================================================================
// TYPES API MOXFIELD (non officielle — https://api2.moxfield.com)
// Forme observée des réponses JSON (avril 2026). L'API peut changer à tout
// moment : tout le code qui en dépend est isolé dans src/sync/moxfield/.
// =============================================================================

/** Élément de la réponse de /v2/decks/search */
export interface MoxfieldDeckSearchItem {
    id: string; // ID court interne (ex: "Y8dZ7")
    publicId: string; // ID public dans l'URL (ex: "j-0aJlxuOUm9FnKRvJcfZw")
    publicUrl: string;
    name: string;
    format: string;
    visibility: 'public' | 'unlisted' | 'private' | string;
    likeCount?: number;
    viewCount?: number;
    commentCount?: number;
    mainboardCount?: number;
    sideboardCount?: number;
    maybeboardCount?: number;
    colors?: string[];
    colorIdentity?: string[];
    bracket?: number | null;
    autoBracket?: number | null;
    hasPrimer?: boolean;
    isLegal?: boolean;
    isShared?: boolean;
    mainCardId?: string | null;
    mainCardIdIsBackFace?: boolean;
    createdAtUtc?: string;
    lastUpdatedAtUtc?: string;
    createdByUser?: MoxfieldAuthor;
    authors?: MoxfieldAuthor[];
    hubNames?: string[];
    commanders?: unknown[];
    signatureSpells?: unknown[];
    matchedCards?: unknown[];
    matchTypes?: string[];
}

export interface MoxfieldDeckSearchResponse {
    pageNumber: number;
    pageSize: number;
    totalResults: number; // plafonné à 10000 par l'API
    totalPages: number;
    data: MoxfieldDeckSearchItem[];
}

export interface MoxfieldAuthor {
    userName: string;
    displayName?: string;
    profileImageUrl?: string | null;
    badges?: string[];
}

/** Carte telle que renvoyée par l'API (champs utilisés uniquement) */
export interface MoxfieldCard {
    id: string; // ID interne Moxfield (ex: "E5bmd")
    uniqueCardId?: string;
    scryfall_id: string; // UUID Scryfall = PK de notre table `cards`
    name: string;
    set?: string; // code set (ex: "iko")
    cn?: string; // collector number
    lang?: string;
}

/** Entrée d'un board : { card, quantity, ... } */
export interface MoxfieldBoardCard {
    boardType?: string;
    card: MoxfieldCard;
    quantity: number;
    isCompanion?: boolean | null;
    isFoil?: boolean;
    isAlter?: boolean;
    isProxy?: boolean;
    excludedFromColor?: boolean;
    useCmcOverride?: boolean;
    useManaCostOverride?: boolean;
    useColorIdentityOverride?: boolean;
}

/** Board : { count, cards: { [uniqueCardId]: MoxfieldBoardCard } } */
export interface MoxfieldBoard {
    count: number;
    cards: Record<string, MoxfieldBoardCard>;
}

/** Réponse de /v3/decks/all/{publicId} */
export interface MoxfieldDeck {
    id: string;
    publicId: string;
    publicUrl: string;
    name: string;
    description?: string | null;
    format?: string;
    visibility?: string;
    version?: number;
    createdAtUtc?: string;
    lastUpdatedAtUtc?: string;
    createdByUser?: MoxfieldAuthor;
    authors?: MoxfieldAuthor[];
    ownerUserId?: string;
    boards: Record<string, MoxfieldBoard>;
    main?: MoxfieldCard | null; // carte "à la une" du deck
    tokens?: MoxfieldCard[]; // jetons générés — jamais importés
}

/** Réponse de /v3/cards/named?q=...&count=... */
export interface MoxfieldCardsNamedResponse {
    cards: MoxfieldCard[];
}

// =============================================================================
// FORMAT INTERNE NORMALISÉ (commun aux futurs scrapers Archidekt/Deckstats)
// =============================================================================

/** Carte extraite, avant résolution vers notre table `cards` */
export interface RawDeckCard {
    /** UUID Scryfall si disponible — chemin de correspondance principal */
    scryfallId?: string;
    name: string;
    /** Code set (ex: "iko") — fallback avec cn */
    setCode?: string;
    /** Collector number — fallback avec setCode */
    collectorNumber?: string;
    quantity: number;
    zone: string; // main, sideboard, commander, companion, oathbreaker, maybeboard, ...
    isCommander: boolean;
}

/** Deck extrait et normalisé, prêt à être importé */
export interface NormalizedDeck {
    source: string; // "moxfield"
    sourceId: string; // publicId
    url: string;
    name: string;
    description: string | null;
    commander: string | null;
    format: string | null;
    author: string | null;
    cards: RawDeckCard[];
}

/** Résultat d'un import en base */
export type ImportResult = 'imported' | 'updated' | 'skipped' | 'failed';

export interface CrawlStats {
    discovered: number;
    imported: number;
    updated: number;
    skipped: number;
    failed: number;
    errors: string[];
}
