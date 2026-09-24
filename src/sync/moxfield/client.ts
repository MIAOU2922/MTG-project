// =============================================================================
// CLIENT HTTP MOXFIELD — isolé ici pour que le reste du projet ne dépende pas
// d'une API non officielle. Si Moxfield bloque/bloque l'API, on remplace ce
// module sans toucher au reste.
//
// Transport : Cloudflare bloque le fetch de Node (empreinte TLS) sur certains
// endpoints → on passe par `curl` par défaut quand il est disponible, avec
// repli sur fetch. Surcharge : MOXFIELD_TRANSPORT=fetch|curl|auto.
// =============================================================================

import { spawn } from "child_process";
import type {
    MoxfieldCardsNamedResponse,
    MoxfieldDeck,
    MoxfieldDeckSearchResponse
} from "./types";

const DEFAULT_API_URL = "https://api2.moxfield.com";
const DEFAULT_USER_AGENT =
    "mtgvrc-deck-crawler/0.1 (personal project, low volume; respects robots/ToS)";

export class MoxfieldError extends Error {
    constructor(
        message: string,
        public readonly status?: number,
        public readonly retryable: boolean = false
    ) {
        super(message);
        this.name = "MoxfieldError";
    }
}

export class MoxfieldNotFoundError extends MoxfieldError {
    constructor(message: string) {
        super(message, 404, false);
        this.name = "MoxfieldNotFoundError";
    }
}

export interface MoxfieldClientOptions {
    /** User-Agent à envoyer (cf_clearance / accès sur demande chez Moxfield) */
    userAgent?: string;
    /** Délai minimum entre deux requêtes, en ms (politesse) */
    minDelayMs?: number;
    /** Nombre de tentatives max par requête (429/5xx) */
    maxRetries?: number;
    /** Transport : "auto" (curl si dispo, sinon fetch), "curl" ou "fetch" */
    transport?: "auto" | "curl" | "fetch";
}

export interface MoxfieldSearchParams {
    pageNumber?: number;
    pageSize?: number;
    fmt?: string;
    deckName?: string;
    q?: string;
    hubName?: string;
    authorUserNames?: string | string[];
    cardId?: string;
    commanderCardId?: string;
    partnerCardId?: string;
    companionCardId?: string;
    board?: string;
    sortType?: 'colors' | 'comments' | 'created' | 'deckBracket' | 'format' | 'likes' | 'updated' | 'views';
    sortDirection?: 'ascending' | 'descending';
    minBracket?: number;
    maxBracket?: number;
    includePinned?: boolean;
    showIllegal?: boolean;
}

export class MoxfieldClient {
    private readonly baseUrl: string;
    private readonly userAgent: string;
    private readonly minDelayMs: number;
    private readonly maxRetries: number;
    private readonly transport: "auto" | "curl" | "fetch";
    private useCurl: boolean;
    private lastRequestAt = 0;

    constructor(options: MoxfieldClientOptions = {}) {
        this.baseUrl = (process.env.MOXFIELD_API_URL || DEFAULT_API_URL).replace(/\/+$/, "");
        this.userAgent = options.userAgent ?? process.env.MOXFIELD_USER_AGENT ?? DEFAULT_USER_AGENT;
        this.minDelayMs = options.minDelayMs ?? Number(process.env.MOXFIELD_MIN_DELAY_MS ?? 500);
        this.maxRetries = options.maxRetries ?? Number(process.env.MOXFIELD_MAX_RETRIES ?? 4);
        this.transport =
            options.transport ?? (process.env.MOXFIELD_TRANSPORT as "auto" | "curl" | "fetch" | undefined) ?? "auto";
        this.useCurl = this.transport !== "fetch";
    }

    /**
     * Recherche de decks paginée.
     * Attention : totalResults est plafonné à 10000 par l'API → pour dépasser
     * cette limite il faut partitionner (par commander, format, carte, auteur…).
     */
    public async searchDecks(params: MoxfieldSearchParams): Promise<MoxfieldDeckSearchResponse> {
        return this.getJson<MoxfieldDeckSearchResponse>("/v2/decks/search", params);
    }

    /** Récupère un deck complet (tous les boards) par publicId ou URL. */
    public async getDeck(publicIdOrUrl: string): Promise<MoxfieldDeck> {
        const publicId = extractPublicId(publicIdOrUrl);
        return this.getJson<MoxfieldDeck>(`/v3/decks/all/${encodeURIComponent(publicId)}`);
    }

    /** Résout des cartes par nom (renvoie l'ID interne Moxfield + scryfall_id). */
    public async findCardsByName(q: string, count = 3): Promise<MoxfieldCardsNamedResponse> {
        return this.getJson<MoxfieldCardsNamedResponse>("/v3/cards/named", { q, count });
    }

    /**
     * Decks publics d'un auteur.
     * NOTE : l'endpoint /v2/users/{username}/decks renvoie 404 côté serveur
     * depuis 2026 → on passe par la recherche avec authorUserNames.
     */
    public async getUserDecks(
        username: string,
        pageNumber = 1,
        pageSize = 100
    ): Promise<MoxfieldDeckSearchResponse> {
        return this.searchDecks({
            pageNumber,
            pageSize,
            authorUserNames: username,
            sortType: "views",
            sortDirection: "descending"
        });
    }

    // -------------------------------------------------------------------------

    private async getJson<T>(path: string, params?: Record<string, unknown>): Promise<T> {
        const url = new URL(this.baseUrl + path);
        if (params) {
            for (const [key, value] of Object.entries(params)) {
                if (value === undefined || value === null) continue;
                if (Array.isArray(value)) {
                    if (value.length === 0) continue;
                    url.searchParams.set(key, value.join(","));
                } else {
                    url.searchParams.set(key, String(value));
                }
            }
        }

        let attempt = 0;
        for (;;) {
            await this.throttle();

            let response: { status: number; body: string };
            try {
                response = await this.performRequest(url.toString());
            } catch (error) {
                attempt++;
                if (attempt > this.maxRetries) {
                    throw new MoxfieldError(`Network error on ${url.pathname}: ${(error as Error).message}`);
                }
                await sleep(this.backoffMs(attempt));
                continue;
            }

            const { status, body } = response;

            if (status >= 200 && status < 300) {
                return JSON.parse(body) as T;
            }

            // 404 = deck introuvable / supprimé → pas retry
            if (status === 404) {
                throw new MoxfieldNotFoundError(`Not found: ${url.pathname}${url.search}`);
            }

            // 403 = challenge Cloudflare. Si on était en fetch, on bascule sur curl une fois.
            if (status === 403) {
                if (!this.useCurl && this.transport === "auto") {
                    console.warn(`  ⚠️ 403 (Cloudflare) avec fetch → bascule sur curl pour ${url.pathname}`);
                    this.useCurl = true;
                    attempt++;
                    if (attempt <= this.maxRetries) continue;
                }
                throw new MoxfieldError(
                    `HTTP 403 (Cloudflare ?) sur ${url.pathname}. ` +
                        "L'API Moxfield est non officielle : un accès en lecture peut être demandé à Moxfield " +
                        "(User-Agent dédié) via la variable MOXFIELD_USER_AGENT.",
                    403,
                    false
                );
            }

            // 429 / 5xx → retry avec backoff
            const retryable = status === 429 || status >= 500;
            if (!retryable) {
                throw new MoxfieldError(`HTTP ${status} on ${url.pathname}${url.search}`, status, false);
            }

            attempt++;
            if (attempt > this.maxRetries) {
                throw new MoxfieldError(
                    `HTTP ${status} after ${this.maxRetries} retries on ${url.pathname}`,
                    status,
                    true
                );
            }
            const waitMs = this.backoffMs(attempt);
            console.warn(
                `  ⚠️ HTTP ${status} sur ${url.pathname} — retry ${attempt}/${this.maxRetries} dans ${Math.round(waitMs / 1000)}s`
            );
            await sleep(waitMs);
        }
    }

    /** Exécute la requête via curl (préféré) ou fetch. Retourne status + body. */
    private async performRequest(url: string): Promise<{ status: number; body: string }> {
        if (this.useCurl) {
            try {
                return await this.performCurl(url);
            } catch (error) {
                if (this.transport === "curl") throw error; // curl imposé → pas de repli
                console.warn(`  ⚠️ curl a échoué (${(error as Error).message}) → repli sur fetch`);
                this.useCurl = false;
            }
        }
        return this.performFetch(url);
    }

    private async performCurl(url: string): Promise<{ status: number; body: string }> {
        const args = [
            "-sS",
            "--max-time", "30",
            "--compressed",
            "-w", "\n%{http_code}",
            "-H", `User-Agent: ${this.userAgent}`,
            "-H", "Accept: application/json",
            url
        ];
        const raw = await new Promise<string>((resolve, reject) => {
            const child = spawn("curl", args, { stdio: ["ignore", "pipe", "pipe"] });
            let stdout = "";
            let stderr = "";
            child.stdout.on("data", chunk => (stdout += chunk));
            child.stderr.on("data", chunk => (stderr += chunk));
            child.on("error", reject);
            child.on("close", code => {
                if (code === 0) resolve(stdout);
                else reject(new Error(`curl exit ${code}: ${stderr.trim().slice(0, 200)}`));
            });
        });

        // curl -w "\n%{http_code}" ajoute le code HTTP sur la dernière ligne
        const parts = raw.split(/\n(?=\d{3}$)/m);
        const body = parts[0] ?? "";
        const status = Number((parts[1] ?? "0").trim()) || 0;
        return { status, body };
    }

    private async performFetch(url: string): Promise<{ status: number; body: string }> {
        const response = await fetch(url, {
            method: "GET",
            headers: {
                Accept: "application/json",
                "User-Agent": this.userAgent
            },
            signal: AbortSignal.timeout(30_000)
        });
        return { status: response.status, body: await response.text() };
    }

    /** Throttle global : espace les requêtes d'au moins minDelayMs. */
    private async throttle(): Promise<void> {
        const elapsed = Date.now() - this.lastRequestAt;
        if (elapsed < this.minDelayMs) {
            await sleep(this.minDelayMs - elapsed);
        }
        this.lastRequestAt = Date.now();
    }

    private backoffMs(attempt: number): number {
        return Math.min(60_000, 1000 * 2 ** attempt);
    }
}

function extractPublicId(publicIdOrUrl: string): string {
    const trimmed = publicIdOrUrl.trim();
    if (!trimmed) throw new MoxfieldError("publicId vide");
    if (/^https?:\/\//i.test(trimmed)) {
        try {
            const url = new URL(trimmed);
            const match = url.pathname.match(/\/decks\/([^/?#]+)/i);
            if (match) return match[1];
        } catch {
            /* on retombe sur la valeur brute */
        }
        throw new MoxfieldError(`URL de deck Moxfield invalide : ${trimmed}`);
    }
    return trimmed;
}

function sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
}
