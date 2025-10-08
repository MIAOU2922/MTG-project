/**
 * Constantes et utilitaires pour l'API Scryfall
 */

// =============================================================================
// CONSTANTES DE L'API
// =============================================================================

/** URL de base de l'API Scryfall */
export const SCRYFALL_API_BASE = 'https://api.scryfall.com';

/** Endpoints principaux */
export const ENDPOINTS = {
  // Cartes
  CARDS_SEARCH: '/cards/search',
  CARDS_NAMED: '/cards/named',
  CARDS_AUTOCOMPLETE: '/cards/autocomplete',
  CARDS_RANDOM: '/cards/random',
  CARDS_COLLECTION: '/cards/collection',
  
  // Sets
  SETS: '/sets',
  
  // Rulings
  RULINGS: (cardId: string) => `/cards/${cardId}/rulings`,
  
  // Symboles
  SYMBOLOGY: '/symbology',
  
  // Catalogues
  CATALOG_CARD_NAMES: '/catalog/card-names',
  CATALOG_ARTIST_NAMES: '/catalog/artist-names',
  CATALOG_WORD_BANK: '/catalog/word-bank',
  
  // Données bulk
  BULK_DATA: '/bulk-data',
  BULK_DATA_BY_ID: (id: string) => `/bulk-data/${id}`,
  BULK_DATA_BY_TYPE: (type: string) => `/bulk-data/${type}`,
} as const;

/** Délai recommandé entre les requêtes (en ms) */
export const RATE_LIMIT_DELAY = 100;

/** Taille maximale d'une requête de collection */
export const MAX_COLLECTION_SIZE = 75;

/** Délai de mise à jour des données bulk (12 heures) */
export const BULK_DATA_UPDATE_INTERVAL = 12 * 60 * 60 * 1000;

/** Taille de buffer par défaut pour le streaming (4MB pour de meilleures performances) */
export const DEFAULT_STREAM_BUFFER_SIZE = 4 * 1024 * 1024;

// =============================================================================
// MAPPINGS DE COULEURS
// =============================================================================

/** Mapping des couleurs Magic vers leurs noms */
export const COLOR_NAMES = {
  W: 'White',
  U: 'Blue', 
  B: 'Black',
  R: 'Red',
  G: 'Green'
} as const;

/** Mapping des couleurs vers leurs noms français */
export const COLOR_NAMES_FR = {
  W: 'Blanc',
  U: 'Bleu',
  B: 'Noir', 
  R: 'Rouge',
  G: 'Vert'
} as const;

/** Mapping des couleurs vers leurs symboles Unicode */
export const COLOR_SYMBOLS = {
  W: '⚪',
  U: '🔵',
  B: '⚫',
  R: '🔴',
  G: '🟢'
} as const;

// =============================================================================
// FORMATS DE JEU
// =============================================================================

/** Formats Standard et rotationnels */
export const ROTATING_FORMATS = [
  'standard',
  'future',
  'historic',
  'timeless',
  'alchemy'
] as const;

/** Formats éternels */
export const ETERNAL_FORMATS = [
  'vintage',
  'legacy', 
  'modern',
  'pioneer'
] as const;

/** Formats casual/multijoueur */
export const CASUAL_FORMATS = [
  'commander',
  'oathbreaker',
  'brawl',
  'standardbrawl'
] as const;

/** Formats compétitifs */
export const COMPETITIVE_FORMATS = [
  'standard',
  'pioneer', 
  'modern',
  'legacy',
  'vintage'
] as const;

// =============================================================================
// RARETÉS ET SYMBOLES
// =============================================================================

/** Ordre des raretés (de la plus commune à la plus rare) */
export const RARITY_ORDER = [
  'common',
  'uncommon', 
  'rare',
  'mythic',
  'special',
  'bonus'
] as const;

/** Symboles de rareté */
export const RARITY_SYMBOLS = {
  common: 'C',
  uncommon: 'U',
  rare: 'R', 
  mythic: 'M',
  special: 'S',
  bonus: 'B'
} as const;

// =============================================================================
// TYPES DE SETS
// =============================================================================

/** Sets standards */
export const STANDARD_SET_TYPES = [
  'core',
  'expansion'
] as const;

/** Sets de réimpression */
export const REPRINT_SET_TYPES = [
  'masters',
  'eternal',
  'draft_innovation'
] as const;

/** Sets produits spéciaux */
export const SPECIAL_SET_TYPES = [
  'commander',
  'duel_deck',
  'from_the_vault',
  'spellbook',
  'premium_deck'
] as const;

/** Sets promotionnels */
export const PROMO_SET_TYPES = [
  'promo',
  'token',
  'box',
  'memorabilia'
] as const;

// =============================================================================
// REGEX ET VALIDATION
// =============================================================================

/** Regex pour valider un UUID */
export const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Regex pour valider un code de set */  
export const SET_CODE_REGEX = /^[a-z0-9]{3,6}$/i;

/** Regex pour valider un coût de mana */
export const MANA_COST_REGEX = /^(\{[^}]+\})*$/;

// =============================================================================
// FONCTIONS UTILITAIRES
// =============================================================================

/**
 * Valide si une chaîne est un UUID valide
 */
export function isValidUUID(uuid: string): boolean {
  return UUID_REGEX.test(uuid);
}

/**
 * Valide si une chaîne est un code de set valide
 */
export function isValidSetCode(code: string): boolean {
  return SET_CODE_REGEX.test(code);
}

/**
 * Extrait les couleurs d'un coût de mana
 */
export function extractColorsFromManaCost(manaCost: string): string[] {
  const colors = new Set<string>();
  const matches = manaCost.match(/\{[^}]+\}/g) || [];
  
  for (const match of matches) {
    const symbol = match.slice(1, -1);
    
    // Couleurs simples
    if (['W', 'U', 'B', 'R', 'G'].includes(symbol)) {
      colors.add(symbol);
    }
    // Coûts hybrides (ex: {W/U})
    else if (symbol.includes('/')) {
      const hybridColors = symbol.split('/');
      for (const color of hybridColors) {
        if (['W', 'U', 'B', 'R', 'G'].includes(color)) {
          colors.add(color);
        }
      }
    }
    // Coûts Phyrexians (ex: {W/P})
    else if (symbol.endsWith('/P')) {
      const color = symbol.slice(0, -2);
      if (['W', 'U', 'B', 'R', 'G'].includes(color)) {
        colors.add(color);
      }
    }
  }
  
  return Array.from(colors);
}

/**
 * Calcule le CMC d'un coût de mana
 */
export function calculateCMC(manaCost: string): number {
  let cmc = 0;
  const matches = manaCost.match(/\{[^}]+\}/g) || [];
  
  for (const match of matches) {
    const symbol = match.slice(1, -1);
    
    // Coût générique (nombre)
    if (/^\d+$/.test(symbol)) {
      cmc += parseInt(symbol);
    }
    // Coût coloré ou hybride = 1
    else if (['W', 'U', 'B', 'R', 'G', 'C'].includes(symbol) || symbol.includes('/')) {
      cmc += 1;
    }
    // Coût X, Y, Z = 0 (variable)
    else if (['X', 'Y', 'Z'].includes(symbol)) {
      // X costs don't add to CMC
    }
  }
  
  return cmc;
}

/**
 * Formate un coût de mana pour l'affichage
 */
export function formatManaCost(manaCost: string): string {
  if (!manaCost) return '';
  
  return manaCost
    .replace(/\{([^}]+)\}/g, '[$1]')  // {W} → [W]
    .replace(/\[(\d+)\]/g, '($1)')    // [3] → (3)
    .replace(/\[([WUBRG])\]/g, '$1')  // [W] → W
    .replace(/\[([^/]+)\/([^/]+)\]/g, '$1/$2'); // [W/U] → W/U
}

/**
 * Détermine la légalité générale d'une carte
 */
export function getOverallLegality(legalities: Record<string, string>): 'legal' | 'restricted' | 'banned' | 'not_legal' {
  const values = Object.values(legalities);
  
  if (values.includes('legal')) return 'legal';
  if (values.includes('restricted')) return 'restricted'; 
  if (values.includes('banned')) return 'banned';
  return 'not_legal';
}

/**
 * Convertit une date Scryfall en objet Date
 */
export function parseScryfallDate(dateString: string): Date {
  return new Date(dateString + 'T00:00:00Z');
}

/**
 * Formate une date pour l'affichage
 */
export function formatDate(date: Date | string, locale: string = 'fr-FR'): string {
  const dateObj = typeof date === 'string' ? parseScryfallDate(date) : date;
  return dateObj.toLocaleDateString(locale, {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  });
}

// =============================================================================
// MESSAGES D'ERREUR COURANTS
// =============================================================================

/** Messages d'erreur traduits */
export const ERROR_MESSAGES = {
  CARD_NOT_FOUND: 'Carte non trouvée',
  SET_NOT_FOUND: 'Set non trouvé',
  INVALID_QUERY: 'Requête de recherche invalide',
  RATE_LIMIT_EXCEEDED: 'Limite de taux dépassée, veuillez patienter',
  NETWORK_ERROR: 'Erreur réseau, vérifiez votre connexion',
  INVALID_UUID: 'UUID invalide',
  INVALID_SET_CODE: 'Code de set invalide',
  TOO_MANY_RESULTS: 'Trop de résultats, affinez votre recherche',
  BULK_DOWNLOAD_FAILED: 'Échec du téléchargement des données bulk',
  BULK_PARSE_ERROR: 'Erreur lors du parsing des données bulk',
  BULK_FILE_TOO_LARGE: 'Fichier bulk trop volumineux pour la mémoire disponible'
} as const;

// =============================================================================
// CONSTANTES BULK DATA
// =============================================================================

/** Types de données bulk disponibles */
export const BULK_DATA_TYPES = {
  ORACLE_CARDS: 'oracle_cards',
  UNIQUE_ARTWORK: 'unique_artwork', 
  DEFAULT_CARDS: 'default_cards',
  ALL_CARDS: 'all_cards',
  RULINGS: 'rulings'
} as const;

/** Descriptions des types de données bulk */
export const BULK_DATA_DESCRIPTIONS = {
  [BULK_DATA_TYPES.ORACLE_CARDS]: 'Une carte par Oracle ID (version la plus récente)',
  [BULK_DATA_TYPES.UNIQUE_ARTWORK]: 'Toutes les illustrations uniques',
  [BULK_DATA_TYPES.DEFAULT_CARDS]: 'Toutes les cartes en anglais ou langue d\'origine',
  [BULK_DATA_TYPES.ALL_CARDS]: 'Toutes les cartes dans toutes les langues',
  [BULK_DATA_TYPES.RULINGS]: 'Tous les rulings et clarifications'
} as const;

/** Tailles approximatives des fichiers bulk (en MB) */
export const BULK_DATA_APPROXIMATE_SIZES = {
  [BULK_DATA_TYPES.ORACLE_CARDS]: 157,
  [BULK_DATA_TYPES.UNIQUE_ARTWORK]: 226,
  [BULK_DATA_TYPES.DEFAULT_CARDS]: 491,
  [BULK_DATA_TYPES.ALL_CARDS]: 2280, // 2.23 GB
  [BULK_DATA_TYPES.RULINGS]: 23
} as const;

/**
 * Détermine si un fichier bulk est considéré comme volumineux
 */
export function isBulkDataLarge(type: string): boolean {
  const size = BULK_DATA_APPROXIMATE_SIZES[type as keyof typeof BULK_DATA_APPROXIMATE_SIZES];
  return size ? size > 500 : false; // > 500MB = volumineux
}

/**
 * Calcule la taille de buffer recommandée selon le type de fichier
 */
export function getRecommendedBufferSize(type: string): number {
  const isLarge = isBulkDataLarge(type);
  return isLarge ? DEFAULT_STREAM_BUFFER_SIZE * 4 : DEFAULT_STREAM_BUFFER_SIZE;
}