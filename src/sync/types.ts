// =============================================================================
// TYPES UTILITAIRES ET COMMUNS
// =============================================================================

/** UUID au format standard */
export type UUID = string;

/** URI vers une ressource */
export type URI = string;

/** Date au format ISO 8601 */
export type Date = string;

/** Couleurs Magic: The Gathering */
export type Colors = Array<'W' | 'U' | 'B' | 'R' | 'G'>;

/** Types de formats de jeu */
export type GameFormat = 
  | 'standard' | 'future' | 'historic' | 'timeless' | 'gladiator' | 'pioneer'
  | 'modern' | 'legacy' | 'pauper' | 'vintage' | 'penny' | 'commander'
  | 'oathbreaker' | 'standardbrawl' | 'brawl' | 'alchemy' | 'paupercommander'
  | 'duel' | 'oldschool' | 'premodern' | 'predh';

/** Statut légal d'une carte dans un format */
export type Legality = 'legal' | 'not_legal' | 'restricted' | 'banned';

/** Raretés des cartes */
export type Rarity = 'common' | 'uncommon' | 'rare' | 'special' | 'mythic' | 'bonus';

/** Layouts des cartes */
export type Layout = 
  | 'normal' | 'split' | 'flip' | 'transform' | 'modal_dfc' | 'meld'
  | 'leveler' | 'class' | 'saga' | 'adventure' | 'mutate' | 'prototype'
  | 'battle' | 'planar' | 'scheme' | 'vanguard' | 'token' | 'double_faced_token'
  | 'emblem' | 'augment' | 'host' | 'art_series' | 'reversible_card';

/** Couleurs de bordure */
export type BorderColor = 'black' | 'white' | 'borderless' | 'silver' | 'gold';

/** Frame layouts */
export type Frame = '1993' | '1997' | '2003' | '2015' | 'future';

/** Frame effects */
export type FrameEffect = 
  | 'legendary' | 'miracle' | 'nyxtouched' | 'draft' | 'devoid' | 'tombstone'
  | 'colorshifted' | 'inverted' | 'snow' | 'miracle' | 'mooneldrazimoon'
  | 'waxingandwaningmoon' | 'compasslanddfc' | 'originpwdfc' | 'mooneldrazidfc'
  | 'moonreversemoondfc' | 'showcase' | 'extendedart' | 'companion'
  | 'etched' | 'snow' | 'lesson' | 'shatterglass' | 'convertdfc'
  | 'fandfc' | 'upsidedowndfc';

/** Finitions possibles */
export type Finish = 'foil' | 'nonfoil' | 'etched';

/** Jeux disponibles */
export type Game = 'paper' | 'arena' | 'mtgo';

/** Status d'image */
export type ImageStatus = 'missing' | 'placeholder' | 'lowres' | 'highres_scan';

/** Security stamps */
export type SecurityStamp = 'oval' | 'triangle' | 'acorn' | 'circle' | 'arena' | 'heart';

/** Types de sets */
export type SetType = 
  | 'core' | 'expansion' | 'masters' | 'eternal' | 'alchemy' | 'masterpiece'
  | 'arsenal' | 'from_the_vault' | 'spellbook' | 'premium_deck' | 'duel_deck'
  | 'draft_innovation' | 'treasure_chest' | 'commander' | 'planechase'
  | 'archenemy' | 'vanguard' | 'funny' | 'starter' | 'box' | 'promo'
  | 'token' | 'memorabilia' | 'minigame';

/** Sources de rulings */
export type RulingSource = 'wotc' | 'scryfall';

// =============================================================================
// INTERFACES PRINCIPALES
// =============================================================================

/** Object de légalité pour tous les formats */
export interface Legalities {
  standard: Legality;
  future: Legality;
  historic: Legality;
  timeless: Legality;
  gladiator: Legality;
  pioneer: Legality;
  modern: Legality;
  legacy: Legality;
  pauper: Legality;
  vintage: Legality;
  penny: Legality;
  commander: Legality;
  oathbreaker: Legality;
  standardbrawl: Legality;
  brawl: Legality;
  alchemy: Legality;
  paupercommander: Legality;
  duel: Legality;
  oldschool: Legality;
  premodern: Legality;
  predh: Legality;
}

/** Prix d'une carte sur différentes plateformes */
export interface Prices {
  usd?: string | null;
  usd_foil?: string | null;
  usd_etched?: string | null;
  eur?: string | null;
  eur_foil?: string | null;
  eur_etched?: string | null;
  tix?: string | null;
}

/** URIs d'achat sur différentes plateformes */
export interface PurchaseUris {
  tcgplayer?: string;
  cardmarket?: string;
  cardhoarder?: string;
}

/** URIs relatifs à une carte */
export interface RelatedUris {
  gatherer?: string;
  tcgplayer_infinite_articles?: string;
  tcgplayer_infinite_decks?: string;
  edhrec?: string;
}

/** URIs d'images d'une carte */
export interface ImageUris {
  small?: string;
  normal?: string;
  large?: string;
  png?: string;
  art_crop?: string;
  border_crop?: string;
}

/** Information de preview */
export interface Preview {
  source?: string;
  source_uri?: string;
  previewed_at?: Date;
}

// =============================================================================
// INTERFACES CARTES
// =============================================================================

/** Carte liée (pour all_parts) */
export interface RelatedCard {
  /** Identifiant unique */
  id: UUID;
  /** Type d'objet (toujours 'related_card') */
  object: 'related_card';
  /** Rôle de cette carte dans la relation */
  component: 'token' | 'meld_part' | 'meld_result' | 'combo_piece';
  /** Nom de la carte liée */
  name: string;
  /** Type line de la carte */
  type_line: string;
  /** URI vers l'objet carte complet */
  uri: URI;
}

/** Face d'une carte multiface */
export interface CardFace {
  /** Type d'objet (toujours 'card_face') */
  object: 'card_face';
  /** Nom de cette face */
  name: string;
  /** Coût de mana de cette face */
  mana_cost: string;
  /** Coût de mana converti (pour les cartes réversibles) */
  cmc?: number;
  /** Couleurs de cette face */
  colors?: Colors;
  /** Indicateur de couleur */
  color_indicator?: Colors;
  /** Type line de cette face */
  type_line?: string;
  /** Texte d'Oracle de cette face */
  oracle_text?: string;
  /** Puissance */
  power?: string;
  /** Endurance */
  toughness?: string;
  /** Loyauté */
  loyalty?: string;
  /** Défense */
  defense?: string;
  /** Texte de flavor */
  flavor_text?: string;
  /** Nom imprimé (localisé) */
  printed_name?: string;
  /** Texte imprimé (localisé) */
  printed_text?: string;
  /** Type line imprimé (localisé) */
  printed_type_line?: string;
  /** Oracle ID de cette face (pour cartes réversibles) */
  oracle_id?: UUID;
  /** Layout de cette face */
  layout?: Layout;
  /** Nom de l'artiste */
  artist?: string;
  /** ID de l'artiste */
  artist_id?: UUID;
  /** ID d'illustration */
  illustration_id?: UUID;
  /** URIs d'images pour cette face */
  image_uris?: ImageUris;
  /** Filigrane */
  watermark?: string;
}

/** Interface principale pour une carte Magic */
export interface CoreCard {
  // =============================================================================
  // CHAMPS CORE
  // =============================================================================
  
  /** Type d'objet (toujours 'card') */
  object: 'card';
  /** Identifiant unique sur Scryfall */
  id: UUID;
  /** Code de langue */
  lang: string;
  /** ID Arena (nullable) */
  arena_id?: number | null;
  /** ID Magic Online (nullable) */
  mtgo_id?: number | null;
  /** ID Magic Online foil (nullable) */
  mtgo_foil_id?: number | null;
  /** IDs Multiverse (nullable) */
  multiverse_ids?: number[] | null;
  /** ID TCGPlayer (nullable) */
  tcgplayer_id?: number | null;
  /** ID TCGPlayer etched (nullable) */
  tcgplayer_etched_id?: number | null;
  /** ID Cardmarket (nullable) */
  cardmarket_id?: number | null;
  /** Layout de la carte */
  layout: Layout;
  /** Oracle ID (nullable pour layout reversible_card) */
  oracle_id?: UUID | null;
  /** URI de recherche des réimpressions */
  prints_search_uri: URI;
  /** URI des rulings */
  rulings_uri: URI;
  /** URI Scryfall de la carte */
  scryfall_uri: URI;
  /** URI de l'API */
  uri: URI;

  // =============================================================================
  // CHAMPS GAMEPLAY
  // =============================================================================
  
  /** Parties liées (nullable) */
  all_parts?: RelatedCard[] | null;
  /** Faces de carte (pour cartes multifaces) */
  card_faces?: CardFace[] | null;
  /** Coût de mana converti */
  cmc: number;
  /** Identité de couleur */
  color_identity: Colors;
  /** Indicateur de couleur (nullable) */
  color_indicator?: Colors | null;
  /** Couleurs (nullable si sur card_faces) */
  colors?: Colors | null;
  /** Défense (nullable) */
  defense?: string | null;
  /** Rang EDHREC (nullable) */
  edhrec_rank?: number | null;
  /** Sur la liste Game Changer (nullable) */
  game_changer?: boolean | null;
  /** Modificateur de main Vanguard (nullable) */
  hand_modifier?: string | null;
  /** Mots-clés */
  keywords: string[];
  /** Légalité dans les formats */
  legalities: Legalities;
  /** Modificateur de vie Vanguard (nullable) */
  life_modifier?: string | null;
  /** Loyauté (nullable) */
  loyalty?: string | null;
  /** Coût de mana (nullable) */
  mana_cost?: string | null;
  /** Nom de la carte */
  name: string;
  /** Texte d'Oracle (nullable) */
  oracle_text?: string | null;
  /** Rang Penny Dreadful (nullable) */
  penny_rank?: number | null;
  /** Puissance (nullable) */
  power?: string | null;
  /** Couleurs de mana produit (nullable) */
  produced_mana?: Colors | null;
  /** Sur la Reserved List */
  reserved: boolean;
  /** Endurance (nullable) */
  toughness?: string | null;
  /** Type line */
  type_line: string;

  // =============================================================================
  // CHAMPS PRINT
  // =============================================================================
  
  /** Nom de l'artiste (nullable) */
  artist?: string | null;
  /** IDs des artistes (nullable) */
  artist_ids?: UUID[] | null;
  /** Lumières d'attraction Unfinity (nullable) */
  attraction_lights?: number[] | null;
  /** Disponible en boosters */
  booster: boolean;
  /** Couleur de bordure */
  border_color: BorderColor;
  /** ID du dos de carte */
  card_back_id: UUID;
  /** Numéro de collection */
  collector_number: string;
  /** Avertissement de contenu (nullable) */
  content_warning?: boolean | null;
  /** Carte digitale uniquement */
  digital: boolean;
  /** Finitions disponibles */
  finishes: Finish[];
  /** Nom fun/alternatif (nullable) */
  flavor_name?: string | null;
  /** Texte de flavor (nullable) */
  flavor_text?: string | null;
  /** Effets de frame (nullable) */
  frame_effects?: FrameEffect[] | null;
  /** Frame */
  frame: Frame;
  /** Full art */
  full_art: boolean;
  /** Jeux disponibles */
  games: Game[];
  /** Image haute résolution */
  highres_image: boolean;
  /** ID d'illustration (nullable) */
  illustration_id?: UUID | null;
  /** Status de l'image */
  image_status: ImageStatus;
  /** URIs d'images (nullable) */
  image_uris?: ImageUris | null;
  /** Carte surdimensionnée */
  oversized: boolean;
  /** Prix */
  prices: Prices;
  /** Nom imprimé (nullable) */
  printed_name?: string | null;
  /** Texte imprimé (nullable) */
  printed_text?: string | null;
  /** Type line imprimé (nullable) */
  printed_type_line?: string | null;
  /** Carte promotionnelle */
  promo: boolean;
  /** Types de promo (nullable) */
  promo_types?: string[] | null;
  /** URIs d'achat (nullable) */
  purchase_uris?: PurchaseUris | null;
  /** Rareté */
  rarity: Rarity;
  /** URIs relatifs */
  related_uris: RelatedUris;
  /** Date de sortie */
  released_at: Date;
  /** Réimpression */
  reprint: boolean;
  /** URI du set sur Scryfall */
  scryfall_set_uri: URI;
  /** Tampon de sécurité (nullable) */
  security_stamp?: SecurityStamp | null;
  /** Nom complet du set */
  set_name: string;
  /** URI de recherche du set */
  set_search_uri: URI;
  /** Type de set */
  set_type: SetType;
  /** URI du set dans l'API */
  set_uri: URI;
  /** Code du set */
  set: string;
  /** UUID du set */
  set_id: UUID;
  /** Story Spotlight */
  story_spotlight: boolean;
  /** Carte sans texte */
  textless: boolean;
  /** Variation d'une autre impression */
  variation: boolean;
  /** ID de l'impression dont c'est une variation (nullable) */
  variation_of?: UUID | null;
  /** Filigrane (nullable) */
  watermark?: string | null;
  /** Information de preview (nullable) */
  preview?: Preview | null;
}

// =============================================================================
// INTERFACES LISTES ET RECHERCHE
// =============================================================================

/** Objet liste générique pour les réponses paginées */
export interface ListObject<T> {
  /** Type d'objet (toujours 'list') */
  object: 'list';
  /** Nombre total d'éléments disponibles */
  total_cards?: number;
  /** Y a-t-il plus de données disponibles */
  has_more: boolean;
  /** URI pour la page suivante (si has_more = true) */
  next_page?: URI | null;
  /** Données de la page actuelle */
  data: T[];
  /** Avertissements éventuels */
  warnings?: string[];
}

/** Liste de cartes */
export type CardList = ListObject<CoreCard>;

/** Paramètres de recherche de cartes */
export interface CardSearchParams {
  /** Requête de recherche */
  q: string;
  /** Tri des résultats */
  order?: 'name' | 'set' | 'released' | 'rarity' | 'color' | 'usd' | 'eur' | 'cmc' | 'power' | 'toughness' | 'edhrec' | 'penny' | 'artist' | 'review';
  /** Direction du tri */
  dir?: 'auto' | 'asc' | 'desc';
  /** Unicité des résultats */
  unique?: 'cards' | 'art' | 'prints';
  /** Format d'inclusion */
  include_extras?: boolean;
  /** Inclure multilingue */
  include_multilingual?: boolean;
  /** Inclure les variations */
  include_variations?: boolean;
  /** Page de résultats */
  page?: number;
}

/** Paramètres pour recherche par nom */
export interface CardNamedParams {
  /** Nom exact */
  exact?: string;
  /** Nom fuzzy */
  fuzzy?: string;
  /** Code de set */
  set?: string;
}

/** Paramètres pour collection de cartes */
export interface CardCollectionParams {
  /** Identifiants des cartes à récupérer */
  identifiers: Array<{
    id?: UUID;
    mtgo_id?: number;
    multiverse_id?: number;
    oracle_id?: UUID;
    illustration_id?: UUID;
    name?: string;
    set?: string;
    collector_number?: string;
  }>;
}

/** Réponse d'autocomplétion */
export interface AutocompleteResponse {
  /** Type d'objet (toujours 'catalog') */
  object: 'catalog';
  /** URI de l'objet */
  uri: URI;
  /** Nombre total d'éléments */
  total_values: number;
  /** Données d'autocomplétion */
  data: string[];
}

// =============================================================================
// INTERFACES SETS
// =============================================================================

/** Interface pour un set Magic */
export interface Set {
  /** Type d'objet (toujours 'set') */
  object: 'set';
  /** Identifiant unique */
  id: UUID;
  /** Code unique du set (3-6 lettres) */
  code: string;
  /** Code MTGO (nullable) */
  mtgo_code?: string | null;
  /** Code Arena (nullable) */
  arena_code?: string | null;
  /** ID TCGPlayer (nullable) */
  tcgplayer_id?: number | null;
  /** Nom anglais du set */
  name: string;
  /** Type de set */
  set_type: SetType;
  /** Date de sortie (nullable) */
  released_at?: Date | null;
  /** Code de bloc (nullable) */
  block_code?: string | null;
  /** Nom de bloc (nullable) */
  block?: string | null;
  /** Code du set parent (nullable) */
  parent_set_code?: string | null;
  /** Nombre de cartes dans le set */
  card_count: number;
  /** Taille imprimée (nullable) */
  printed_size?: number | null;
  /** Set digital uniquement */
  digital: boolean;
  /** Set foil uniquement */
  foil_only: boolean;
  /** Set non-foil uniquement */
  nonfoil_only: boolean;
  /** URI Scryfall du set */
  scryfall_uri: URI;
  /** URI de l'API */
  uri: URI;
  /** URI de l'icône SVG */
  icon_svg_uri: URI;
  /** URI de recherche des cartes */
  search_uri: URI;
}

/** Liste de sets */
export type SetList = ListObject<Set>;

// =============================================================================
// INTERFACES RULINGS
// =============================================================================

/** Interface pour un ruling */
export interface Ruling {
  /** Type d'objet (toujours 'ruling') */
  object: 'ruling';
  /** Oracle ID de la carte associée */
  oracle_id: UUID;
  /** Source du ruling */
  source: RulingSource;
  /** Date de publication */
  published_at: Date;
  /** Texte du ruling */
  comment: string;
}

/** Liste de rulings */
export type RulingList = ListObject<Ruling>;

// =============================================================================
// INTERFACES ERREURS
// =============================================================================

/** Interface pour les erreurs de l'API Scryfall */
export interface ScryfallError {
  /** Type d'objet (toujours 'error') */
  object: 'error';
  /** Code d'erreur HTTP */
  status: number;
  /** Code d'erreur Scryfall */
  code: string;
  /** Message d'erreur lisible */
  details: string;
  /** Type d'erreur (optionnel) */
  type?: string;
  /** Avertissements (optionnel) */
  warnings?: string[];
}

// =============================================================================
// INTERFACES SYMBOLIQUES ET CATALOGUES
// =============================================================================

/** Symbole de carte */
export interface CardSymbol {
  /** Type d'objet (toujours 'card_symbol') */
  object: 'card_symbol';
  /** Symbole textuel */
  symbol: string;
  /** Coût de mana converti en loose */
  loose_variant?: string | null;
  /** Nom anglais */
  english: string;
  /** Transpose to */
  transposable: boolean;
  /** Représente du mana */
  represents_mana: boolean;
  /** Coût de mana converti */
  cmc?: number | null;
  /** Apparaît dans les coûts de mana */
  appears_in_mana_costs: boolean;
  /** Drôle */
  funny: boolean;
  /** Couleurs */
  colors: Colors;
  /** Hybrid */
  hybrid: boolean;
  /** Phyrexian */
  phyrexian: boolean;
  /** Gatherer alternates */
  gatherer_alternates?: string[] | null;
  /** SVG URI */
  svg_uri?: string | null;
}

/** Catalogue générique */
export interface Catalog {
  /** Type d'objet (toujours 'catalog') */
  object: 'catalog';
  /** URI de l'objet */
  uri: URI;
  /** Nombre total de valeurs */
  total_values: number;
  /** Données du catalogue */
  data: string[];
}

// =============================================================================
// INTERFACES DATA BULK
// =============================================================================

/** Types de données bulk disponibles */
export type BulkDataType = 
  | 'oracle_cards'      // Une carte par Oracle ID (version la plus récente)
  | 'unique_artwork'    // Toutes les illustrations uniques
  | 'default_cards'     // Toutes les cartes en anglais (ou langue d'origine)
  | 'all_cards'         // Toutes les cartes dans toutes les langues
  | 'rulings';          // Tous les rulings

/** Objet de données bulk */
export interface BulkData {
  /** Type d'objet (toujours 'bulk_data') */
  object: 'bulk_data';
  /** Identifiant unique */
  id: UUID;
  /** URI de l'API Scryfall pour ce fichier */
  uri: URI;
  /** Type de données */
  type: BulkDataType;
  /** Nom du dump */
  name: string;
  /** Description */
  description: string;
  /** Taille en bytes */
  size: number;
  /** URI de téléchargement */
  download_uri: URI;
  /** Date de mise à jour */
  updated_at: Date;
  /** Type de contenu MIME */
  content_type: string;
  /** Encodage du contenu */
  content_encoding: string;
}

/** Liste de données bulk */
export type BulkDataList = ListObject<BulkData>;

// =============================================================================
// CONTENU DES FICHIERS BULK
// =============================================================================

/** 
 * Contenu du fichier All Cards
 * Tableau de toutes les cartes dans toutes les langues
 */
export type AllCardsBulkData = CoreCard[];

/** 
 * Contenu du fichier Oracle Cards
 * Tableau avec une carte par Oracle ID (version la plus récente)
 */
export type OracleCardsBulkData = CoreCard[];

/** 
 * Contenu du fichier Default Cards  
 * Tableau de toutes les cartes en anglais (ou langue d'origine si pas d'anglais)
 */
export type DefaultCardsBulkData = CoreCard[];

/** 
 * Contenu du fichier Unique Artwork
 * Tableau de cartes avec toutes les illustrations uniques
 */
export type UniqueArtworkBulkData = CoreCard[];

/** 
 * Contenu du fichier Rulings
 * Tableau de tous les rulings
 */
export type RulingsBulkData = Ruling[];

/** Union de tous les types de contenu bulk possibles */
export type BulkDataContent = 
  | AllCardsBulkData
  | OracleCardsBulkData  
  | DefaultCardsBulkData
  | UniqueArtworkBulkData
  | RulingsBulkData;

/** Mapping des types vers leur contenu */
export interface BulkDataContentMap {
  'all_cards': AllCardsBulkData;
  'oracle_cards': OracleCardsBulkData;
  'default_cards': DefaultCardsBulkData;
  'unique_artwork': UniqueArtworkBulkData;
  'rulings': RulingsBulkData;
}

// =============================================================================
// INTERFACES POUR LE STREAMING ET TRAITEMENT BULK
// =============================================================================

/** Options pour le téléchargement de données bulk */
export interface BulkDataDownloadOptions {
  /** Afficher la progression */
  showProgress?: boolean;
  /** Timeout en millisecondes */
  timeout?: number;
  /** Traiter en streaming (pour gros fichiers) */
  streaming?: boolean;
  /** Taille du buffer pour le streaming */
  bufferSize?: number;
}

/** Informations de progression du téléchargement */
export interface DownloadProgress {
  /** Bytes téléchargés */
  downloaded: number;
  /** Taille totale */
  total: number;
  /** Pourcentage */
  percentage: number;
  /** Vitesse (bytes/sec) */
  speed: number;
  /** Temps écoulé (ms) */
  elapsed: number;
  /** Temps estimé restant (ms) */
  remaining?: number;
}

/** Callback pour la progression */
export type ProgressCallback = (progress: DownloadProgress) => void;

/** Options pour le traitement en streaming */
export interface StreamProcessingOptions<T> {
  /** Taille du batch pour traiter les éléments */
  batchSize?: number;
  /** Fonction appelée pour chaque élément */
  onItem?: (item: T, index: number) => void | Promise<void>;
  /** Fonction appelée pour chaque batch */
  onBatch?: (batch: T[], batchIndex: number) => void | Promise<void>;
  /** Fonction appelée en cas d'erreur */
  onError?: (error: Error, item?: T, index?: number) => void;
}

/** Métadonnées d'un fichier bulk traité */
export interface BulkFileMetadata {
  /** Type de données */
  type: BulkDataType;
  /** Nombre total d'éléments */
  totalItems: number;
  /** Taille du fichier */
  fileSize: number;
  /** Date de dernière mise à jour */
  lastUpdated: string;
  /** Temps de traitement */
  processingTime: number;
  /** Statistiques par type d'objet */
  stats?: Record<string, number>;
}

// =============================================================================
// TYPES D'UNION ET UTILITAIRES
// =============================================================================

/** Tout objet Scryfall possible */
export type ScryfallObject = 
  | CoreCard
  | Set
  | Ruling
  | CardSymbol
  | Catalog
  | BulkData
  | ScryfallError;

/** Réponse d'API générique */
export type ApiResponse<T> = T | ScryfallError;

/** Options de requête communes */
export interface RequestOptions {
  /** Format de réponse souhaité */
  format?: 'json';
  /** Version de l'API */
  version?: string;
  /** User-Agent personnalisé */
  userAgent?: string;
}