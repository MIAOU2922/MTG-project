/**
 * Exemple d'utilisation des interfaces Scryfall
 * Ce fichier montre comment utiliser les types définis pour interagir avec l'API Scryfall
 */

import { 
  CoreCard, 
  CardList, 
  CardSearchParams, 
  SetList, 
  Ruling, 
  RulingList,
  ScryfallError,
  ApiResponse
} from './types';
import type { Set as ScryfallSet } from './types';

// =============================================================================
// EXEMPLES D'UTILISATION DES INTERFACES
// =============================================================================

/**
 * Interface pour un client Scryfall
 */
export interface ScryfallClient {
  // Méthodes pour les cartes
  searchCards(params: CardSearchParams): Promise<ApiResponse<CardList>>;
  getCard(id: string): Promise<ApiResponse<CoreCard>>;
  getCardByName(name: string, set?: string): Promise<ApiResponse<CoreCard>>;
  getRandomCard(): Promise<ApiResponse<CoreCard>>;
  autocomplete(query: string): Promise<string[]>;
  
  // Méthodes pour les sets
  getAllSets(): Promise<ApiResponse<SetList>>;
  getSet(code: string): Promise<ApiResponse<ScryfallSet>>;
  
  // Méthodes pour les rulings
  getCardRulings(cardId: string): Promise<ApiResponse<RulingList>>;
}

/**
 * Exemple d'implémentation basique d'un client Scryfall
 */
export class BasicScryfallClient implements ScryfallClient {
  private readonly baseUrl = 'https://api.scryfall.com';
  
  async searchCards(params: CardSearchParams): Promise<ApiResponse<CardList>> {
    const url = new URL(`${this.baseUrl}/cards/search`);
    
    // Ajouter les paramètres de recherche
    if (params.q) url.searchParams.set('q', params.q);
    if (params.order) url.searchParams.set('order', params.order);
    if (params.dir) url.searchParams.set('dir', params.dir);
    if (params.unique) url.searchParams.set('unique', params.unique);
    if (params.include_extras) url.searchParams.set('include_extras', 'true');
    if (params.page) url.searchParams.set('page', params.page.toString());
    
    try {
      const response = await fetch(url.toString());
      const data = await response.json();
      
      if (!response.ok) {
        return data as ScryfallError;
      }
      
      return data as CardList;
    } catch (error) {
      throw new Error(`Erreur lors de la recherche de cartes: ${error}`);
    }
  }
  
  async getCard(id: string): Promise<ApiResponse<CoreCard>> {
    try {
      const response = await fetch(`${this.baseUrl}/cards/${id}`);
      const data = await response.json();
      
      if (!response.ok) {
        return data as ScryfallError;
      }
      
      return data as CoreCard;
    } catch (error) {
      throw new Error(`Erreur lors de la récupération de la carte: ${error}`);
    }
  }
  
  async getCardByName(name: string, set?: string): Promise<ApiResponse<CoreCard>> {
    const url = new URL(`${this.baseUrl}/cards/named`);
    url.searchParams.set('exact', name);
    if (set) url.searchParams.set('set', set);
    
    try {
      const response = await fetch(url.toString());
      const data = await response.json();
      
      if (!response.ok) {
        return data as ScryfallError;
      }
      
      return data as CoreCard;
    } catch (error) {
      throw new Error(`Erreur lors de la recherche par nom: ${error}`);
    }
  }
  
  async getRandomCard(): Promise<ApiResponse<CoreCard>> {
    try {
      const response = await fetch(`${this.baseUrl}/cards/random`);
      const data = await response.json();
      
      if (!response.ok) {
        return data as ScryfallError;
      }
      
      return data as CoreCard;
    } catch (error) {
      throw new Error(`Erreur lors de la récupération d'une carte aléatoire: ${error}`);
    }
  }
  
  async autocomplete(query: string): Promise<string[]> {
    const url = new URL(`${this.baseUrl}/cards/autocomplete`);
    url.searchParams.set('q', query);
    
    try {
      const response = await fetch(url.toString());
      const data = await response.json();
      
      if (!response.ok) {
        throw new Error(`Erreur d'autocomplétion: ${data.details}`);
      }
      
      return data.data;
    } catch (error) {
      throw new Error(`Erreur lors de l'autocomplétion: ${error}`);
    }
  }
  
  async getAllSets(): Promise<ApiResponse<SetList>> {
    try {
      const response = await fetch(`${this.baseUrl}/sets`);
      const data = await response.json();
      
      if (!response.ok) {
        return data as ScryfallError;
      }
      
      return data as SetList;
    } catch (error) {
      throw new Error(`Erreur lors de la récupération des sets: ${error}`);
    }
  }
  
  async getSet(code: string): Promise<ApiResponse<ScryfallSet>> {
    try {
      const response = await fetch(`${this.baseUrl}/sets/${code}`);
      const data = await response.json();
      
      if (!response.ok) {
        return data as ScryfallError;
      }
      
      return data as ScryfallSet;
    } catch (error) {
      throw new Error(`Erreur lors de la récupération du set: ${error}`);
    }
  }
  
  async getCardRulings(cardId: string): Promise<ApiResponse<RulingList>> {
    try {
      const response = await fetch(`${this.baseUrl}/cards/${cardId}/rulings`);
      const data = await response.json();
      
      if (!response.ok) {
        return data as ScryfallError;
      }
      
      return data as RulingList;
    } catch (error) {
      throw new Error(`Erreur lors de la récupération des rulings: ${error}`);
    }
  }
}

// =============================================================================
// EXEMPLES D'USAGE
// =============================================================================

/**
 * Exemple d'utilisation du client Scryfall
 */
export async function exempleUsage() {
  const client = new BasicScryfallClient();
  
  try {
    // Rechercher des cartes avec "Lightning Bolt"
    const searchResult = await client.searchCards({
      q: 'Lightning Bolt',
      order: 'released',
      unique: 'prints'
    });
    
    if ('object' in searchResult && searchResult.object === 'error') {
      console.error('Erreur de recherche:', searchResult.details);
      return;
    }
    
    console.log(`Trouvé ${searchResult.data.length} cartes`);
    
    // Récupérer une carte spécifique par nom
    const card = await client.getCardByName('Black Lotus');
    
    if ('object' in card && card.object === 'error') {
      console.error('Erreur:', card.details);
      return;
    }
    
    console.log(`Carte trouvée: ${card.name}`);
    console.log(`Prix USD: ${card.prices.usd || 'N/A'}`);
    console.log(`Rareté: ${card.rarity}`);
    console.log(`Set: ${card.set_name} (${card.set})`);
    
    // Récupérer les rulings de la carte
    const rulings = await client.getCardRulings(card.id);
    
    if ('object' in rulings && rulings.object === 'error') {
      console.error('Erreur rulings:', rulings.details);
      return;
    }
    
    console.log(`Rulings (${rulings.data.length}):`);
    rulings.data.forEach((ruling, index) => {
      console.log(`${index + 1}. ${ruling.comment}`);
    });
    
    // Récupérer tous les sets
    const sets = await client.getAllSets();
    
    if ('object' in sets && sets.object === 'error') {
      console.error('Erreur sets:', sets.details);
      return;
    }
    
    console.log(`Trouvé ${sets.data.length} sets`);
    
    // Exemple d'autocomplétion
    const suggestions = await client.autocomplete('Jace');
    console.log('Suggestions pour "Jace":', suggestions.slice(0, 5));
    
  } catch (error) {
    console.error('Erreur générale:', error);
  }
}

/**
 * Fonctions utilitaires pour travailler avec les données Scryfall
 */
export class ScryfallUtils {
  /**
   * Vérifie si une réponse est une erreur
   */
  static isError<T>(response: ApiResponse<T>): response is ScryfallError {
    return (response as any).object === 'error';
  }
  
  /**
   * Extrait les couleurs d'une carte
   */
  static getCardColors(card: CoreCard): string[] {
    if (card.colors && card.colors.length > 0) {
      return card.colors;
    }
    
    if (card.card_faces && card.card_faces.length > 0) {
      const colors = new Set<string>();
      card.card_faces.forEach(face => {
        if (face.colors) {
          face.colors.forEach(color => colors.add(color));
        }
      });
      return Array.from(colors);
    }
    
    return [];
  }
  
  /**
   * Détermine si une carte est légale dans un format
   */
  static isLegalInFormat(card: CoreCard, format: keyof typeof card.legalities): boolean {
    return card.legalities[format] === 'legal';
  }
  
  /**
   * Formate le prix d'une carte
   */
  static formatPrice(priceStr: string | null | undefined, currency: string = 'USD'): string {
    if (!priceStr) return 'N/A';
    const price = parseFloat(priceStr);
    return `${price.toFixed(2)} ${currency}`;
  }
  
  /**
   * Obtient l'URL de l'image d'une carte
   */
  static getCardImageUrl(card: CoreCard, size: 'small' | 'normal' | 'large' = 'normal'): string | null {
    if (card.image_uris && card.image_uris[size]) {
      return card.image_uris[size];
    }
    
    if (card.card_faces && card.card_faces[0]?.image_uris?.[size]) {
      return card.card_faces[0].image_uris[size];
    }
    
    return null;
  }
}