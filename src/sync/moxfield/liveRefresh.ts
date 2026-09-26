// =============================================================================
// LIVE REFRESH — file d'attente de re-scraping déclenchée par les recherches.
//
// Quand une recherche publique est faite sur /ad (list), le filtre est
// « forwardé » ici : un crawler Moxfield tourne en arrière-plan (jamais plus
// d'UN job à la fois) pour re-scraper et mettre à jour les decks correspondants
// (mode refresh) + importer les nouveaux.
//
// Stratégie choisie selon le filtre de la recherche :
//   commander  → crawlByCommander (partition commanderCardId)
//   author     → crawlByUser     (authorUserNames)
//   name       → crawlByName     (deckName)
//   format     → crawlTopDecks   (top du format par vues)
//
// Env :
//   MOXFIELD_LIVE_ENABLED=0            → désactive complètement
//   MOXFIELD_LIVE_MAX_DECKS (déf. 300) → plafond global de decks vus par job
//   MOXFIELD_LIVE_MAX_PER_PARTITION (déf. 200) → plafond par commander/auteur/nom
//   MOXFIELD_LIVE_DELAY_MS (déf. 150)  → délai additionnel entre 2 fetches deck
// =============================================================================

import { MoxfieldClient } from "./client";
import { DeckImporter } from "./importer";
import { MoxfieldCrawler } from "./crawler";
import type { CrawlStats } from "./types";

export type RefreshJobType = "commander" | "author" | "name" | "format";

export interface RefreshJob {
    id: number;
    type: RefreshJobType;
    /** Clé de déduplication (type + format + requête normalisée) */
    key: string;
    query: string;
    format: string | null;
    status: "queued" | "running" | "done" | "failed";
    stats?: CrawlStats;
    started_at?: string;
    finished_at?: string;
    error?: string;
}

const MAX_HISTORY = 50;

class LiveRefresh {
    private readonly enabled: boolean;
    private queue: RefreshJob[] = [];
    private running: RefreshJob | null = null;
    private history: RefreshJob[] = [];
    private nextId = 1;
    private client: MoxfieldClient | null = null;
    private importer: DeckImporter | null = null;

    constructor() {
        this.enabled = process.env.MOXFIELD_LIVE_ENABLED !== "0";
    }

    public isEnabled(): boolean {
        return this.enabled;
    }

    /**
     * Traduit une recherche /ad (list) en job de re-scraping.
     * Retourne le job (nouveau OU déjà planifié/en cours) ou null si rien
     * à faire / désactivé. Ne bloque jamais : le crawl tourne en arrière-plan.
     */
    public forwardSearch(params: {
        name?: string;
        format?: string;
        author?: string;
        commander?: string;
    }): RefreshJob | null {
        if (!this.enabled) return null;

        const { name, format, author, commander } = params;
        const fmt = format?.toLowerCase() || null;

        let type: RefreshJobType | null = null;
        let query: string | null = null;

        if (commander) {
            type = "commander";
            query = commander;
        } else if (author) {
            type = "author";
            query = author;
        } else if (name) {
            type = "name";
            query = name;
        } else if (format) {
            type = "format";
            query = format;
        }

        if (!type || !query) return null;

        const key = `${type}:${fmt ?? "all"}:${query.trim().toLowerCase()}`;

        // Dédup : même job déjà en attente ou en cours → on le retourne tel quel.
        const existing =
            this.queue.find(j => j.key === key) ?? (this.running?.key === key ? this.running : null);
        if (existing) return existing;

        const job: RefreshJob = {
            id: this.nextId++,
            type,
            key,
            query: query.trim(),
            format: fmt,
            status: "queued"
        };
        this.queue.push(job);
        void this.drain();
        return job;
    }

    /** État complet : job en cours, file d'attente, derniers jobs terminés. */
    public status(): {
        enabled: boolean;
        running: RefreshJob | null;
        queued: RefreshJob[];
        history: RefreshJob[];
    } {
        return {
            enabled: this.enabled,
            running: this.running,
            queued: this.queue,
            history: this.history.slice(-20)
        };
    }

    // -------------------------------------------------------------------------

    private ensureDeps(): { client: MoxfieldClient; importer: DeckImporter } {
        if (!this.client) {
            this.client = new MoxfieldClient();
        }
        if (!this.importer) {
            this.importer = new DeckImporter();
        }
        return { client: this.client, importer: this.importer };
    }

    /** Lance le prochain job si aucun n'est en cours (sérialisation stricte). */
    private async drain(): Promise<void> {
        if (this.running) return;
        const job = this.queue.shift();
        if (!job) return;

        this.running = job;
        job.status = "running";
        job.started_at = new Date().toISOString();
        console.log(`🔁 LiveRefresh #${job.id} démarre — ${job.type} "${job.query}" (fmt=${job.format ?? "all"})`);

        const maxDecks = Number(process.env.MOXFIELD_LIVE_MAX_DECKS ?? 300);
        const maxPerPartition = Number(process.env.MOXFIELD_LIVE_MAX_PER_PARTITION ?? 200);
        const deckDelayMs = Number(process.env.MOXFIELD_LIVE_DELAY_MS ?? 150);
        // "all" n'est pas un filtre accepté par l'API → undefined = tous formats
        const fmt = job.format && job.format !== "all" ? job.format : undefined;

        try {
            const { client, importer } = this.ensureDeps();
            // refresh=true : re-fetch de TOUT (mises à jour + nouveaux decks).
            // limitMode="discovered" : le plafond compte les decks vus, sinon
            // un refresh pur ne compterait aucun deck et ne s'arrêterait jamais.
            const crawler = new MoxfieldCrawler(client, importer, {
                refresh: true,
                limitMode: "discovered",
                deckDelayMs
            });

            let stats: CrawlStats;
            switch (job.type) {
                case "commander":
                    stats = await crawler.crawlByCommander({
                        commanders: [job.query],
                        fmt,
                        maxDecksPerCommander: maxPerPartition,
                        maxDecks
                    });
                    break;
                case "author":
                    stats = await crawler.crawlByUser({
                        usernames: [job.query],
                        maxDecksPerUser: maxPerPartition,
                        maxDecks
                    });
                    break;
                case "name":
                    stats = await crawler.crawlByName({
                        deckName: job.query,
                        fmt,
                        maxDecks: maxPerPartition
                    });
                    break;
                case "format":
                    stats = await crawler.crawlTopDecks({
                        fmt: job.query === "all" ? undefined : job.query,
                        maxDecks: maxPerPartition
                    });
                    break;
                default:
                    throw new Error(`Type de job inconnu : ${job.type}`);
            }

            job.stats = stats;
            job.status = "done";
            console.log(
                `✅ LiveRefresh #${job.id} terminé — ${job.type} "${job.query}" : ` +
                `${stats.imported} importé(s), ${stats.updated} mis à jour, ${stats.skipped} ignoré(s), ${stats.failed} échec(s)`
            );
        } catch (error) {
            job.status = "failed";
            job.error = error instanceof Error ? error.message : String(error);
            console.error(`❌ LiveRefresh #${job.id} échec — ${job.type} "${job.query}" : ${job.error}`);
        } finally {
            job.finished_at = new Date().toISOString();
            this.history.push(job);
            if (this.history.length > MAX_HISTORY) this.history.shift();
            this.running = null;
            void this.drain();
        }
    }
}

/** Singleton partagé par le serveur (une seule file par processus). */
export const liveRefresh = new LiveRefresh();
