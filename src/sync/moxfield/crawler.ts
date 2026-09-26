// =============================================================================
// CRAWLER MOXFIELD — découverte de decks par plusieurs stratégies :
//
//   1. crawlTopDecks      : recherche globale (fmt, tri) — plafonnée à 10 000
//                           résultats par l'API.
//   2. crawlByCommander   : partition par commander (nom → ID interne Moxfield
//                           via /v3/cards/named → recherche commanderCardId).
//                           Chaque commander a son propre espace de 10 000.
//   3. crawlByCard        : decks contenant une carte donnée (partition).
//   4. crawlByUser        : decks publics d'un auteur (graphe de découverte).
//
// Le publicId déduplique tout : en base via (source, source_id), en mémoire
// pendant la run.
// =============================================================================

import { MoxfieldClient, MoxfieldError, MoxfieldNotFoundError } from "./client";
import { DeckImporter } from "./importer";
import { summarize } from "./normalize";
import type { CrawlStats, ImportResult, MoxfieldDeckSearchResponse } from "./types";

export interface CrawlerOptions {
    /** Délai additionnel entre deux fetches de deck (en plus du throttle client) */
    deckDelayMs?: number;
    /** Log de progression tous les N decks */
    logEvery?: number;
    /** Silencieux : pas de log par deck importé (utile pour les longs balayages) */
    quiet?: boolean;
    /** Re-fetch et met à jour les decks déjà en base (au lieu de les skipper) */
    refresh?: boolean;
    /**
     * Comment compter maxDecks :
     * - "imported" (défaut) : s'arrête après N NOUVEAUX decks importés
     *   (en mode refresh rien n'est compté → risque de tout balayer).
     * - "discovered" : s'arrête après N decks VUS (importés, mis à jour
     *   ou échoués). À utiliser avec refresh pour borner un rafraîchissement.
     */
    limitMode?: "imported" | "discovered";
}

export interface CrawlTopOptions {
    fmt?: string;
    sortType?: 'colors' | 'comments' | 'created' | 'deckBracket' | 'format' | 'likes' | 'updated' | 'views';
    sortDirection?: 'ascending' | 'descending';
    pageSize?: number;
    /** Nombre max de decks importés (nouveaux) — défaut 100 */
    maxDecks?: number;
    /** Nombre max de pages à parcourir (défaut : toutes) */
    maxPages?: number;
}

export interface CrawlByCommanderOptions {
    commanders: string[];
    fmt?: string;
    pageSize?: number;
    maxDecksPerCommander?: number;
    maxDecks?: number;
}

export interface CrawlByCardOptions {
    cardNames: string[];
    fmt?: string;
    pageSize?: number;
    maxDecksPerCard?: number;
    maxDecks?: number;
}

export interface CrawlByUserOptions {
    usernames: string[];
    pageSize?: number;
    maxDecksPerUser?: number;
    maxDecks?: number;
}

export interface CrawlByNameOptions {
    deckName: string;
    fmt?: string;
    pageSize?: number;
    /** Nombre max de decks vus (0 = illimité jusqu'au plafond API) */
    maxDecks?: number;
}

export class MoxfieldCrawler {
    private readonly deckDelayMs: number;
    private readonly logEvery: number;
    private readonly quiet: boolean;
    private readonly refresh: boolean;
    private readonly limitMode: "imported" | "discovered";
    private readonly seen = new Set<string>();
    private stats: CrawlStats = {
        discovered: 0,
        imported: 0,
        updated: 0,
        skipped: 0,
        failed: 0,
        errors: []
    };

    constructor(
        private readonly client: MoxfieldClient,
        private readonly importer: DeckImporter,
        options: CrawlerOptions = {}
    ) {
        this.deckDelayMs = options.deckDelayMs ?? 150;
        this.logEvery = options.logEvery ?? 10;
        this.quiet = options.quiet ?? false;
        this.refresh = options.refresh ?? false;
        this.limitMode = options.limitMode ?? "imported";
    }

    public getStats(): CrawlStats {
        return { ...this.stats };
    }

    /** Compteur utilisé pour les limites maxDecks (voir limitMode). */
    private get limitCount(): number {
        return this.limitMode === "discovered" ? this.stats.discovered : this.stats.imported;
    }

    // -------------------------------------------------------------------------
    // Stratégie 1 : recherche globale paginée
    // -------------------------------------------------------------------------

    public async crawlTopDecks(options: CrawlTopOptions = {}): Promise<CrawlStats> {
        const {
            fmt,
            sortType = "views",
            sortDirection = "descending",
            pageSize = 64,
            maxDecks = 100,
            maxPages = Number.POSITIVE_INFINITY
        } = options;

        console.log(
            `🔍 Moxfield top decks — fmt=${fmt ?? "all"} sort=${sortType}/${sortDirection} ` +
            `pageSize=${pageSize} maxDecks=${maxDecks}`
        );

        for (let page = 1; page <= maxPages; page++) {
            const res = await this.client.searchDecks({
                pageNumber: page,
                pageSize,
                fmt,
                sortType,
                sortDirection
            });
            if (page === 1) {
                console.log(`   totalResults=${res.totalResults} (plafond API 10000), totalPages=${res.totalPages}`);
            }
            for (const item of res.data) {
                if (this.limitCount >= maxDecks) break;
                await this.importByPublicId(item.publicId, item.name);
            }
            if (this.limitCount >= maxDecks) break;
            if (page >= res.totalPages || res.data.length === 0) break;
        }
        return this.getStats();
    }

    // -------------------------------------------------------------------------
    // Stratégie 2 : partition par commander
    // -------------------------------------------------------------------------

    public async crawlByCommander(options: CrawlByCommanderOptions): Promise<CrawlStats> {
        const {
            commanders,
            fmt,
            pageSize = 64,
            maxDecksPerCommander = 100,
            maxDecks = 500
        } = options;

        console.log(
            `🎯 Moxfield par commander — ${commanders.length} commander(s), ` +
            `maxDecksPerCommander=${maxDecksPerCommander} maxDecks=${maxDecks}`
        );

        for (const commanderName of commanders) {
            if (this.limitCount >= maxDecks) break;

            const resolved = await this.resolveMoxfieldCardId(commanderName);
            if (!resolved) {
                console.warn(`  ⚠️ Commander introuvable chez Moxfield : ${commanderName}`);
                continue;
            }
            console.log(`  👑 ${commanderName} → id Moxfield ${resolved.id}`);

            const total = await this.paginateSearch(
                {
                    fmt,
                    commanderCardId: resolved.id,
                    sortType: "views",
                    sortDirection: "descending",
                    pageSize
                },
                maxDecksPerCommander,
                maxDecks
            );
            console.log(`  ✅ ${commanderName} : ${total} deck(s) vus`);
        }
        return this.getStats();
    }

    // -------------------------------------------------------------------------
    // Stratégie 3 : partition par carte contenue
    // -------------------------------------------------------------------------

    public async crawlByCard(options: CrawlByCardOptions): Promise<CrawlStats> {
        const {
            cardNames,
            fmt,
            pageSize = 64,
            maxDecksPerCard = 100,
            maxDecks = 500
        } = options;

        console.log(
            `🃏 Moxfield par carte contenue — ${cardNames.length} carte(s), ` +
            `maxDecksPerCard=${maxDecksPerCard} maxDecks=${maxDecks}`
        );

        for (const cardName of cardNames) {
            if (this.limitCount >= maxDecks) break;
            await this.crawlOneCard(cardName, { fmt, pageSize, maxDecksPerCard, maxDecks });
        }
        return this.getStats();
    }

    /**
     * Traite UNE carte : résout son ID Moxfield puis importe les decks qui la
     * contiennent. Ne lève jamais : les erreurs sont enregistrées dans stats
     * (un échec sur une carte ne doit pas interrompre un balayage de 30k cartes).
     */
    public async crawlOneCard(
        cardName: string,
        options: {
            fmt?: string; // undefined/"" = tous formats
            pageSize?: number;
            maxDecksPerCard?: number; // 0 = illimité (jusqu'au plafond API de 10 000)
            maxDecks?: number; // 0 = illimité
        } = {}
    ): Promise<void> {
        const { fmt, pageSize = 64, maxDecksPerCard = 100, maxDecks = 0 } = options;
        const maxTotal = maxDecks <= 0 ? Number.POSITIVE_INFINITY : maxDecks;
        const maxPerCard = maxDecksPerCard <= 0 ? Number.POSITIVE_INFINITY : maxDecksPerCard;

        const resolved = await this.resolveMoxfieldCardId(cardName);
        if (!resolved) return; // déjà loggé dans resolveMoxfieldCardId

        try {
            await this.paginateSearch(
                {
                    fmt: fmt || undefined,
                    cardId: resolved.id,
                    sortType: "views",
                    sortDirection: "descending",
                    pageSize
                },
                maxPerCard,
                maxTotal
            );
        } catch (error) {
            this.stats.failed++;
            const message = error instanceof Error ? error.message : String(error);
            this.stats.errors.push(`card(${cardName}): ${message}`);
            console.error(`  ❌ carte ${cardName} : ${message}`);
        }
    }

    // -------------------------------------------------------------------------
    // Stratégie 4 : decks publics d'un auteur
    // -------------------------------------------------------------------------

    public async crawlByUser(options: CrawlByUserOptions): Promise<CrawlStats> {
        const {
            usernames,
            pageSize = 100,
            maxDecksPerUser = 100,
            maxDecks = 500
        } = options;

        console.log(
            `👤 Moxfield par auteur — ${usernames.length} user(s), ` +
            `maxDecksPerUser=${maxDecksPerUser} maxDecks=${maxDecks}`
        );

        for (const username of usernames) {
            if (this.limitCount >= maxDecks) break;

            try {
                let total = 0;
                for (let page = 1; ; page++) {
                    if (this.limitCount >= maxDecks || total >= maxDecksPerUser) break;
                    const res = await this.client.getUserDecks(username, page, pageSize);
                    for (const item of res.data) {
                        if (this.limitCount >= maxDecks || total >= maxDecksPerUser) break;
                        total++;
                        await this.importByPublicId(item.publicId, item.name);
                    }
                    if (page >= res.totalPages || res.data.length === 0) break;
                }
                console.log(`  ✅ ${username} : ${total} deck(s) vus`);
            } catch (error) {
                this.stats.failed++;
                const message = error instanceof Error ? error.message : String(error);
                this.stats.errors.push(`user(${username}): ${message}`);
                console.error(`  ❌ user ${username} : ${message}`);
            }
        }
        return this.getStats();
    }

    // -------------------------------------------------------------------------
    // Stratégie 5 : recherche par nom de deck
    // -------------------------------------------------------------------------

    /**
     * Decks dont le NOM correspond (filtre deckName de l'API Moxfield).
     * Utile pour re-scraper les decks trouvés par une recherche /ad.
     */
    public async crawlByName(options: CrawlByNameOptions): Promise<CrawlStats> {
        const { deckName, fmt, pageSize = 64, maxDecks = 200 } = options;
        const maxTotal = maxDecks <= 0 ? Number.POSITIVE_INFINITY : maxDecks;

        console.log(
            `🔎 Moxfield par nom — "${deckName}" fmt=${fmt ?? "all"} maxDecks=${maxDecks <= 0 ? "∞" : maxDecks}`
        );

        try {
            await this.paginateSearch(
                {
                    fmt: fmt || undefined,
                    deckName,
                    sortType: "views",
                    sortDirection: "descending",
                    pageSize
                },
                maxTotal,
                maxTotal
            );
        } catch (error) {
            this.stats.failed++;
            const message = error instanceof Error ? error.message : String(error);
            this.stats.errors.push(`name(${deckName}): ${message}`);
            console.error(`  ❌ nom ${deckName} : ${message}`);
        }
        return this.getStats();
    }

    // -------------------------------------------------------------------------
    // Internes
    // -------------------------------------------------------------------------

    /** Importe un deck par publicId, en dédupliquant et en respectant les limites. */
    private async importByPublicId(publicId: string, name?: string): Promise<void> {
        if (this.seen.has(publicId)) return;
        this.seen.add(publicId);
        this.stats.discovered++;

        // Déjà en base ? En mode refresh on re-fetch quand même pour mettre à jour.
        if (!this.refresh && (await this.importer.exists("moxfield", publicId))) {
            this.stats.skipped++;
            return;
        }

        try {
            const deck = await this.client.getDeck(publicId);
            const { normalizeDeck } = await import("./normalize");
            const normalized = normalizeDeck(deck);
            const result = await this.importer.importDeck(normalized, { refresh: this.refresh });

            this.record(result);
            if (!this.quiet) {
                if (result === "imported") {
                    console.log(`  ✅ #${this.stats.imported} ${summarize(normalized)}`);
                } else if (result === "updated") {
                    console.log(`  🔄 ${summarize(normalized)}`);
                }
            }
        } catch (error) {
            if (error instanceof MoxfieldNotFoundError) {
                this.stats.skipped++; // deck supprimé / devenu privé
                return;
            }
            if (error instanceof MoxfieldError) {
                this.stats.failed++;
                this.stats.errors.push(`${publicId}${name ? ` (${name})` : ""}: ${error.message}`);
                console.error(`  ❌ ${publicId}${name ? ` (${name})` : ""}: ${error.message}`);
                return;
            }
            this.stats.failed++;
            const message = error instanceof Error ? error.message : String(error);
            this.stats.errors.push(`${publicId}${name ? ` (${name})` : ""}: ${message}`);
            console.error(`  ❌ ${publicId}${name ? ` (${name})` : ""}: ${message}`);
        } finally {
            if (this.deckDelayMs > 0) {
                await new Promise(resolve => setTimeout(resolve, this.deckDelayMs));
            }
        }
    }

    /** Pagine une recherche jusqu'à maxPerQuery items vus ou maxDecks importés. */
    private async paginateSearch(
        baseParams: Parameters<MoxfieldClient["searchDecks"]>[0],
        maxPerQuery: number,
        maxDecks: number
    ): Promise<number> {
        let total = 0;
        for (let page = 1; ; page++) {
            if (this.limitCount >= maxDecks || total >= maxPerQuery) break;
            const res: MoxfieldDeckSearchResponse = await this.client.searchDecks({
                ...baseParams,
                pageNumber: page
            });
            for (const item of res.data) {
                if (this.limitCount >= maxDecks || total >= maxPerQuery) break;
                total++;
                await this.importByPublicId(item.publicId, item.name);
            }
            if (page >= res.totalPages || res.data.length === 0) break;
        }
        return total;
    }

    /** Résout un nom de carte vers l'ID interne Moxfield (via /v3/cards/named). */
    private async resolveMoxfieldCardId(name: string): Promise<{ id: string } | null> {
        try {
            const res = await this.client.findCardsByName(name, 1);
            const card = res.cards?.[0];
            if (card?.id) return { id: card.id };
            return null; // résolu mais aucune carte Moxfield → silencieux (carte inconnue)
        } catch (error) {
            this.stats.failed++;
            this.stats.errors.push(`resolve(${name}): ${(error as Error).message}`);
            console.error(`  ❌ résolution ${name} : ${(error as Error).message}`);
            return null;
        }
    }

    private record(result: ImportResult): void {
        if (result === "imported") this.stats.imported++;
        else if (result === "updated") this.stats.updated++;
        else if (result === "skipped") this.stats.skipped++;
        else this.stats.failed++;
    }
}
