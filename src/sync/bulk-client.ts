/**
 * Client pour l'API Bulk Data de Scryfall
 * Permet de télécharger et traiter les gros fichiers de données
 */

const JSONStream = require('JSONStream');
import { Readable } from 'stream';
import { pipeline } from 'stream/promises';

import {
  BulkData,
  BulkDataList,
  BulkDataType,
  BulkDataContent,
  BulkDataContentMap,
  BulkDataDownloadOptions,
  DownloadProgress,
  ProgressCallback,
  StreamProcessingOptions,
  BulkFileMetadata,
  ScryfallError,
  ApiResponse,
  CoreCard,
  Ruling
} from './types';

import {
  SCRYFALL_API_BASE,
  ENDPOINTS,
  BULK_DATA_TYPES,
  DEFAULT_STREAM_BUFFER_SIZE,
  getRecommendedBufferSize,
  ERROR_MESSAGES
} from './constants';

// =============================================================================
// INTERFACES DU CLIENT BULK DATA
// =============================================================================

export interface BulkDataClient {
  // Méthodes pour récupérer les informations sur les fichiers bulk
  getAllBulkData(): Promise<ApiResponse<BulkDataList>>;
  getBulkDataById(id: string): Promise<ApiResponse<BulkData>>;
  getBulkDataByType(type: BulkDataType): Promise<ApiResponse<BulkData>>;

  // Méthodes pour télécharger les données
  downloadBulkData<T extends BulkDataType>(
    type: T,
    options?: BulkDataDownloadOptions
  ): Promise<BulkDataContentMap[T]>;

  downloadBulkDataStream<T extends BulkDataType>(
    type: T,
    processingOptions: StreamProcessingOptions<any>,
    downloadOptions?: BulkDataDownloadOptions
  ): Promise<BulkFileMetadata>;

  // Utilitaires
  getLatestBulkDataInfo(type: BulkDataType): Promise<BulkData | null>;
  isUpdateAvailable(type: BulkDataType, lastUpdate: Date): Promise<boolean>;
}

// =============================================================================
// IMPLÉMENTATION DU CLIENT
// =============================================================================

export class ScryfallBulkDataClient implements BulkDataClient {
  private readonly baseUrl = SCRYFALL_API_BASE;

  /**
   * Récupère tous les objets bulk data disponibles
   */
  async getAllBulkData(): Promise<ApiResponse<BulkDataList>> {
    try {
      const response = await fetch(`${this.baseUrl}${ENDPOINTS.BULK_DATA}`);
      const data = await response.json();

      if (!response.ok) {
        return data as ScryfallError;
      }

      return data as BulkDataList;
    } catch (error) {
      throw new Error(`${ERROR_MESSAGES.NETWORK_ERROR}: ${error}`);
    }
  }

  /**
   * Récupère un bulk data par son ID
   */
  async getBulkDataById(id: string): Promise<ApiResponse<BulkData>> {
    try {
      const response = await fetch(`${this.baseUrl}${ENDPOINTS.BULK_DATA_BY_ID(id)}`);
      const data = await response.json();

      if (!response.ok) {
        return data as ScryfallError;
      }

      return data as BulkData;
    } catch (error) {
      throw new Error(`${ERROR_MESSAGES.NETWORK_ERROR}: ${error}`);
    }
  }

  /**
   * Récupère un bulk data par son type
   */
  async getBulkDataByType(type: BulkDataType): Promise<ApiResponse<BulkData>> {
    try {
      const response = await fetch(`${this.baseUrl}${ENDPOINTS.BULK_DATA_BY_TYPE(type)}`);
      const data = await response.json();

      if (!response.ok) {
        return data as ScryfallError;
      }

      return data as BulkData;
    } catch (error) {
      throw new Error(`${ERROR_MESSAGES.NETWORK_ERROR}: ${error}`);
    }
  }

  /**
   * Télécharge et parse un fichier bulk data complet en mémoire
   * ATTENTION: À utiliser uniquement pour les petits fichiers (< 500MB)
   */
  async downloadBulkData<T extends BulkDataType>(
    type: T,
    options: BulkDataDownloadOptions = {}
  ): Promise<BulkDataContentMap[T]> {
    // Récupérer les infos du fichier
    const bulkDataInfo = await this.getBulkDataByType(type);

    if ('object' in bulkDataInfo && bulkDataInfo.object === 'error') {
      throw new Error(`Impossible de récupérer les infos du fichier ${type}: ${bulkDataInfo.details}`);
    }

    const downloadUrl = bulkDataInfo.download_uri;
    const fileSize = bulkDataInfo.size;

    // Vérifier si le fichier n'est pas trop volumineux
    if (fileSize > 1024 * 1024 * 1024) { // > 1GB
      throw new Error(`${ERROR_MESSAGES.BULK_FILE_TOO_LARGE}: ${Math.round(fileSize / (1024 * 1024))}MB`);
    }

    try {
      const response = await fetch(downloadUrl);

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      let downloadedBytes = 0;
      const startTime = Date.now();

      // Setup du tracking de progression si demandé
      if (options.showProgress && response.body) {
        const reader = response.body.getReader();
        const chunks: Uint8Array[] = [];

        while (true) {
          const { done, value } = await reader.read();

          if (done) break;

          chunks.push(value);
          downloadedBytes += value.length;

          if (options.showProgress) {
            const elapsed = Date.now() - startTime;
            const speed = downloadedBytes / (elapsed / 1000);
            const remaining = fileSize > downloadedBytes ?
              ((fileSize - downloadedBytes) / speed) * 1000 : undefined;

            const progress: DownloadProgress = {
              downloaded: downloadedBytes,
              total: fileSize,
              percentage: (downloadedBytes / fileSize) * 100,
              speed,
              elapsed,
              remaining
            };

            // On pourrait émettre un événement ou appeler un callback ici
            console.log(`Progression: ${progress.percentage.toFixed(1)}% (${Math.round(downloadedBytes / (1024 * 1024))}MB/${Math.round(fileSize / (1024 * 1024))}MB)`);
          }
        }

        // Reconstituer le contenu
        const totalLength = chunks.reduce((acc, chunk) => acc + chunk.length, 0);
        const result = new Uint8Array(totalLength);
        let offset = 0;
        for (const chunk of chunks) {
          result.set(chunk, offset);
          offset += chunk.length;
        }

        const jsonString = new TextDecoder().decode(result);
        return JSON.parse(jsonString) as BulkDataContentMap[T];
      } else {
        // Téléchargement simple sans progression
        const jsonString = await response.text();
        return JSON.parse(jsonString) as BulkDataContentMap[T];
      }

    } catch (error) {
      if (error instanceof SyntaxError) {
        throw new Error(`${ERROR_MESSAGES.BULK_PARSE_ERROR}: ${error.message}`);
      }
      throw new Error(`${ERROR_MESSAGES.BULK_DOWNLOAD_FAILED}: ${error}`);
    }
  }

  /**
   * Télécharge et traite un fichier bulk data en streaming
   * Recommandé pour les gros fichiers (> 500MB)
   * Utilise JSONStream pour un parsing efficace sans accumulation mémoire
   */
  async downloadBulkDataStream<T extends BulkDataType>(
    type: T,
    processingOptions: StreamProcessingOptions<any>,
    downloadOptions: BulkDataDownloadOptions = {}
  ): Promise<BulkFileMetadata> {
    // Récupérer les infos du fichier
    const bulkDataInfo = await this.getBulkDataByType(type);

    if ('object' in bulkDataInfo && bulkDataInfo.object === 'error') {
      throw new Error(`Impossible de récupérer les infos du fichier ${type}: ${bulkDataInfo.details}`);
    }

    const downloadUrl = bulkDataInfo.download_uri;
    const fileSize = bulkDataInfo.size;
    const batchSize = processingOptions.batchSize || getRecommendedBufferSize(type);

    let totalItems = 0;
    let processedItems = 0;
    const startTime = Date.now();
    const stats: Record<string, number> = {};
    let batch: any[] = [];
    let batchIndex = 0;

    try {
      // Créer un AbortController avec timeout de 10 minutes
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 10 * 60 * 1000); // 10 minutes
      
      const response = await fetch(downloadUrl, {
        signal: controller.signal,
        headers: {
          'User-Agent': 'MTG-VRC/1.0'
        }
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      if (!response.body) {
        throw new Error('Pas de contenu dans la réponse');
      }

      // Convertir le ReadableStream en Node.js Readable
      const nodeStream = Readable.fromWeb(response.body as any);

      // Créer le parser JSONStream pour parser un tableau d'objets JSON
      const jsonParser = JSONStream.parse('*');

      return new Promise<BulkFileMetadata>((resolve, reject) => {
        let streamError: Error | null = null;
        let streamTimeout: NodeJS.Timeout | null = null;

        const resetTimeout = () => {
          if (streamTimeout) clearTimeout(streamTimeout);
          // Timeout de 5 minutes sans activité (au cas où le stream s'arrête)
          streamTimeout = setTimeout(() => {
            reject(new Error('Stream inactif pendant 5 minutes - timeout'));
            jsonParser.destroy();
            nodeStream.destroy();
          }, 5 * 60 * 1000);
        };

        jsonParser.on('data', async (item: any) => {
          resetTimeout(); // Reset le timeout à chaque donnée
          jsonParser.pause(); // Pause pour synchroniser le parsing avec le traitement
          
          if (streamError) {
            jsonParser.resume();
            return;
          }
          
          try {
            totalItems++;

            // Statistiques
            const objType = item.object || 'unknown';
            stats[objType] = (stats[objType] || 0) + 1;

            // Traitement individuel
            if (processingOptions.onItem) {
              try {
                await processingOptions.onItem(item, totalItems - 1);
              } catch (error) {
                if (processingOptions.onError) {
                  processingOptions.onError(error as Error, item, totalItems - 1);
                }
              }
            }

            // Ajout au batch
            batch.push(item);

            if (batch.length >= batchSize) {
              if (processingOptions.onBatch) {
                try {
                  const t0 = Date.now();
                  await processingOptions.onBatch([...batch], batchIndex);
                  const t1 = Date.now();
                  console.log(`Batch ${batchIndex} processed in ${t1 - t0} ms`);
                  
                  processedItems += batch.length;
                  batch = [];
                  batchIndex++;

                  // Force garbage collection hint périodiquement
                  if (batchIndex % 5 === 0 && global.gc) {
                    global.gc();
                  }
                } catch (error) {
                  if (processingOptions.onError) {
                    processingOptions.onError(error as Error);
                  }
                }
              } else {
                processedItems += batch.length;
                batch = [];
                batchIndex++;
              }
            }
          } catch (error) {
            streamError = error as Error;
            jsonParser.emit('error', error);
          }

          jsonParser.resume(); // Resume après traitement
        });

        jsonParser.on('end', async () => {
          try {
            if (streamTimeout) clearTimeout(streamTimeout);
            
            // Traiter le dernier batch
            if (batch.length > 0 && processingOptions.onBatch) {
              await processingOptions.onBatch([...batch], batchIndex);
              processedItems += batch.length;
            }

            const processingTime = Date.now() - startTime;

            resolve({
              type,
              totalItems,
              fileSize,
              lastUpdated: bulkDataInfo.updated_at,
              processingTime,
              stats
            });
          } catch (error) {
            reject(error);
          }
        });

        jsonParser.on('error', (error: Error) => {
          if (streamTimeout) clearTimeout(streamTimeout);
          reject(new Error(`${ERROR_MESSAGES.BULK_PARSE_ERROR}: ${error.message}`));
        });

        // Connecter les streams avec gestion d'erreur
        nodeStream.on('error', (error: Error) => {
          if (streamTimeout) clearTimeout(streamTimeout);
          reject(new Error(`${ERROR_MESSAGES.BULK_DOWNLOAD_FAILED}: ${error.message}`));
        });

        // Démarrer le timeout
        resetTimeout();
        // Piping avec backpressure management
        nodeStream.pipe(jsonParser);
      });

    } catch (error) {
      throw new Error(`${ERROR_MESSAGES.BULK_DOWNLOAD_FAILED}: ${error}`);
    }
  }

  /**
   * Récupère les dernières informations d'un type de bulk data
   */
  async getLatestBulkDataInfo(type: BulkDataType): Promise<BulkData | null> {
    const result = await this.getBulkDataByType(type);

    if ('object' in result && result.object === 'error') {
      return null;
    }

    return result;
  }

  /**
   * Vérifie si une mise à jour est disponible
   */
  async isUpdateAvailable(type: BulkDataType, lastUpdate: Date): Promise<boolean> {
    const info = await this.getLatestBulkDataInfo(type);

    if (!info) return false;

    const remoteUpdate = new Date(info.updated_at);
    return remoteUpdate > lastUpdate;
  }
}

// =============================================================================
// UTILITAIRES POUR LE TRAITEMENT BULK
// =============================================================================

export class BulkDataUtils {
  /**
   * Filtre les cartes par critères
   */
  static filterCards(cards: CoreCard[], filters: {
    colors?: ('W' | 'U' | 'B' | 'R' | 'G')[];
    rarity?: string[];
    setTypes?: string[];
    legal?: string; // format de légalité
    lang?: string;
  }): CoreCard[] {
    return cards.filter(card => {
      if (filters.colors && filters.colors.length > 0) {
        const cardColors = card.colors || [];
        if (!filters.colors.some(color => cardColors.includes(color))) {
          return false;
        }
      }

      if (filters.rarity && filters.rarity.length > 0) {
        if (!filters.rarity.includes(card.rarity)) {
          return false;
        }
      }

      if (filters.setTypes && filters.setTypes.length > 0) {
        if (!filters.setTypes.includes(card.set_type)) {
          return false;
        }
      }

      if (filters.legal) {
        const legality = card.legalities[filters.legal as keyof typeof card.legalities];
        if (legality !== 'legal') {
          return false;
        }
      }

      if (filters.lang) {
        if (card.lang !== filters.lang) {
          return false;
        }
      }

      return true;
    });
  }

  /**
   * Groupe les cartes par critère
   */
  static groupCards<K extends keyof CoreCard>(
    cards: CoreCard[],
    groupBy: K
  ): Record<string, CoreCard[]> {
    const groups: Record<string, CoreCard[]> = {};

    for (const card of cards) {
      const key = String(card[groupBy]);
      if (!groups[key]) {
        groups[key] = [];
      }
      groups[key].push(card);
    }

    return groups;
  }

  /**
   * Calcule des statistiques sur un ensemble de cartes
   */
  static calculateCardStats(cards: CoreCard[]) {
    const stats = {
      total: cards.length,
      byRarity: {} as Record<string, number>,
      bySetType: {} as Record<string, number>,
      byColors: {} as Record<string, number>,
      byLang: {} as Record<string, number>,
      avgCmc: 0,
      totalCmc: 0
    };

    let totalCmc = 0;

    for (const card of cards) {
      // Rareté
      stats.byRarity[card.rarity] = (stats.byRarity[card.rarity] || 0) + 1;

      // Type de set
      stats.bySetType[card.set_type] = (stats.bySetType[card.set_type] || 0) + 1;

      // Couleurs
      const colors = card.colors || [];
      if (colors.length === 0) {
        stats.byColors['Colorless'] = (stats.byColors['Colorless'] || 0) + 1;
      } else {
        const colorKey = colors.join('');
        stats.byColors[colorKey] = (stats.byColors[colorKey] || 0) + 1;
      }

      // Langue
      stats.byLang[card.lang] = (stats.byLang[card.lang] || 0) + 1;

      // CMC
      totalCmc += card.cmc;
    }

    stats.totalCmc = totalCmc;
    stats.avgCmc = cards.length > 0 ? totalCmc / cards.length : 0;

    return stats;
  }

  /**
   * Trouve les doublons par Oracle ID
   */
  static findDuplicatesByOracleId(cards: CoreCard[]): Record<string, CoreCard[]> {
    const byOracleId: Record<string, CoreCard[]> = {};

    for (const card of cards) {
      if (card.oracle_id) {
        if (!byOracleId[card.oracle_id]) {
          byOracleId[card.oracle_id] = [];
        }
        byOracleId[card.oracle_id].push(card);
      }
    }

    // Retourner seulement ceux qui ont des doublons
    const duplicates: Record<string, CoreCard[]> = {};
    for (const [oracleId, cardList] of Object.entries(byOracleId)) {
      if (cardList.length > 1) {
        duplicates[oracleId] = cardList;
      }
    }

    return duplicates;
  }

  /**
   * Groupe les cartes par couleur (alias pour compatibilité)
   */
  static groupCardsByColor(cards: CoreCard[]): Record<string, CoreCard[]> {
    const groups: Record<string, CoreCard[]> = {};

    cards.forEach(card => {
      const colors = card.colors || [];
      const colorKey = colors.length === 0 ? 'Incolore' : colors.join('');

      if (!groups[colorKey]) {
        groups[colorKey] = [];
      }
      groups[colorKey].push(card);
    });

    return groups;
  }

  /**
   * Groupe les cartes par rareté
   */
  static groupCardsByRarity(cards: CoreCard[]): Record<string, CoreCard[]> {
    const groups: Record<string, CoreCard[]> = {};

    cards.forEach(card => {
      const rarity = card.rarity;
      if (!groups[rarity]) {
        groups[rarity] = [];
      }
      groups[rarity].push(card);
    });

    return groups;
  }

  /**
   * Calcule des statistiques sur les cartes (alias pour compatibilité)
   */
  static getCardStatistics(cards: CoreCard[]) {
    const base = this.calculateCardStats(cards);
    return {
      total: base.total,
      averageCmc: base.avgCmc,
      byColor: base.byColors,
      byRarity: base.byRarity,
      byType: {} as Record<string, number>, // Simplifié pour l'exemple
    };
  }
}