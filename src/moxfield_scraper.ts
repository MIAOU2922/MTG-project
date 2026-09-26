// =============================================================================
// CLI DU SCRAPER MOXFIELD
//
// Usage :
//   npm run scrape:moxfield                            → top decks commander
//   npm run scrape:moxfield -- --mode sweep            → balayage de TOUTES les
//     cartes légales en commander (reprise auto, tourne des heures)
//   npm run scrape:moxfield -- --mode sweep --only-commanders
//   npm run scrape:moxfield -- --mode commander --commanders "Atraxa, Praetors' Voice|Miirym, Sentinel Wyrm"
//   npm run scrape:moxfield -- --mode card --cards "Sol Ring|Rhystic Study"
//   npm run scrape:moxfield -- --mode user --users ComedIan
//   npm run scrape:moxfield -- --mode deck --public-id j-0aJlxuOUm9FnKRvJcfZw
//
// Options communes : --max-decks N --max-pages N --page-size N --delay MS
//                    --per-partition N --format commander|all
// Mode sweep : --only-commanders --progress-every N --reset --start-from NAME
// =============================================================================

import crypto from "crypto";
import Database from "@/database/Database";
import Config from "@/database/Config";
import { MoxfieldClient } from "./sync/moxfield/client";
import { DeckImporter } from "./sync/moxfield/importer";
import { MoxfieldCrawler } from "./sync/moxfield/crawler";

interface Args {
    mode: "top" | "commander" | "card" | "user" | "deck" | "sweep";
    format: string;
    maxDecks: number;
    maxDecksProvided: boolean;
    maxPages: number;
    pageSize: number;
    perPartition: number;
    delayMs: number;
    commanders: string[];
    commandersFromDb: number;
    cards: string[];
    users: string[];
    publicId: string;
    onlyCommanders: boolean;
    progressEvery: number;
    reset: boolean;
    startFrom: string;
    refresh: boolean;
    help: boolean;
}

function parseArgs(argv: string[]): Args {
    const args: Args = {
        mode: "top",
        format: "all",
        maxDecks: 100,
        maxDecksProvided: false,
        maxPages: 0, // 0 = toutes
        pageSize: 64,
        perPartition: 100,
        delayMs: 500,
        commanders: [],
        commandersFromDb: 0,
        cards: [],
        users: [],
        publicId: "",
        onlyCommanders: false,
        progressEvery: 1,
        reset: false,
        startFrom: "",
        refresh: false,
        help: false
    };

    const next = (i: number): string => {
        const value = argv[i + 1];
        if (value === undefined || value.startsWith("--")) {
            throw new Error(`Valeur manquante pour ${argv[i]}`);
        }
        return value;
    };

    for (let i = 0; i < argv.length; i++) {
        const arg = argv[i];
        switch (arg) {
            case "--help": args.help = true; break;
            case "--mode": args.mode = next(i++) as Args["mode"]; break;
            case "--format": args.format = next(i++); break;
            case "--max-decks": args.maxDecks = Number(next(i++)); args.maxDecksProvided = true; break;
            case "--max-pages": args.maxPages = Number(next(i++)); break;
            case "--page-size": args.pageSize = Number(next(i++)); break;
            case "--per-partition": args.perPartition = Number(next(i++)); break;
            case "--delay": args.delayMs = Number(next(i++)); break;
            case "--commanders": args.commanders = next(i++).split("|").map(s => s.trim()).filter(Boolean); break;
            case "--commanders-from-db": args.commandersFromDb = Number(next(i++)); break;
            case "--cards": args.cards = next(i++).split("|").map(s => s.trim()).filter(Boolean); break;
            case "--users": args.users = next(i++).split("|").map(s => s.trim()).filter(Boolean); break;
            case "--public-id": args.publicId = next(i++); break;
            case "--only-commanders": args.onlyCommanders = true; break;
            case "--progress-every": args.progressEvery = Number(next(i++)); break;
            case "--reset": args.reset = true; break;
            case "--start-from": args.startFrom = next(i++); break;
            case "--refresh": args.refresh = true; break;
            default:
                throw new Error(`Option inconnue : ${arg}`);
        }
    }
    return args;
}

async function commanderNamesFromDb(limit: number): Promise<string[]> {
    // Cartes légendaires de type "Legendary Creature" — type_line est sur Oracle.
    // On exclut les variantes Arena/Alchemy "A-…" (jamais jouées en paper).
    const faces = await Database.prisma.face.findMany({
        where: {
            type_line: { contains: "Legendary Creature" },
            name: { not: { startsWith: "A-" } }
        },
        distinct: ["name"],
        select: { name: true },
        take: limit,
        orderBy: { name: "asc" }
    });
    return faces.map(f => f.name);
}

// =============================================================================
// MODE SWEEP — plan de balayage + progression persistée dans `configs`
// =============================================================================

interface SweepProgress {
    planKey: string;
    /** Index de la dernière carte TERMINÉE (la reprise repart à +1) */
    lastIndex: number;
    total: number;
    format: string;
    onlyCommanders: boolean;
    updatedAt: string;
}

function sweepConfigKey(format: string, onlyCommanders: boolean): string {
    return `moxfield:sweep:progress:${format}:${onlyCommanders ? "legendary" : "all"}`;
}

function computePlanKey(plan: string[]): string {
    const summary = `${plan.length}:${plan[0] ?? ""}:${plan[plan.length - 1] ?? ""}`;
    return crypto.createHash("sha256").update(summary).digest("hex").slice(0, 16);
}

/**
 * Liste triée (déterministe) des noms de cartes à balayer : distincts, anglais,
 * hors tokens, hors variantes Arena « A- ». En commander : uniquement les
 * cartes légales. `--only-commanders` restreint aux légendaires.
 */
async function buildSweepPlan(format: string, onlyCommanders: boolean): Promise<string[]> {
    const isCommander = format !== "all";

    let sql: string;
    if (isCommander && onlyCommanders) {
        sql = `
            SELECT DISTINCT c.name
            FROM cards c
            JOIN faces f ON f.card_id = c.id
            JOIN oracles o ON o.id = f.oracle_id
            WHERE c.lang = 'en'
              AND o.legalities->>'commander' = 'legal'
              AND c.name NOT LIKE 'A-%'
              AND c.set_id NOT IN (SELECT id FROM sets WHERE type = 'token')
              AND f.type_line ILIKE '%legendary%'
            ORDER BY c.name
        `;
    } else if (isCommander) {
        sql = `
            SELECT DISTINCT c.name
            FROM cards c
            JOIN faces f ON f.card_id = c.id
            JOIN oracles o ON o.id = f.oracle_id
            WHERE c.lang = 'en'
              AND o.legalities->>'commander' = 'legal'
              AND c.name NOT LIKE 'A-%'
              AND c.set_id NOT IN (SELECT id FROM sets WHERE type = 'token')
            ORDER BY c.name
        `;
    } else {
        sql = `
            SELECT DISTINCT c.name
            FROM cards c
            WHERE c.lang = 'en'
              AND c.name NOT LIKE 'A-%'
              AND c.set_id NOT IN (SELECT id FROM sets WHERE type = 'token')
            ORDER BY c.name
        `;
    }

    const rows = await Database.prisma.$queryRawUnsafe<{ name: string }[]>(sql);
    return rows.map(r => r.name);
}

async function loadSweepProgress(key: string): Promise<SweepProgress | null> {
    const config = await Config.get(key, "");
    if (!config.value) return null;
    try {
        return JSON.parse(config.value) as SweepProgress;
    } catch {
        return null;
    }
}

async function saveSweepProgress(key: string, progress: SweepProgress): Promise<void> {
    progress.updatedAt = new Date().toISOString();
    await Config.set(key, JSON.stringify(progress));
}

function formatEta(seconds: number): string {
    const s = Math.max(0, Math.round(seconds));
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const sec = s % 60;
    return h > 0 ? `${h}h${String(m).padStart(2, "0")}m${String(sec).padStart(2, "0")}s` : `${m}m${String(sec).padStart(2, "0")}s`;
}

/** Timestamp local HH:MM:SS pour les logs du sweep */
function ts(): string {
    return new Date().toTimeString().slice(0, 8);
}

async function main(): Promise<void> {
    let args: Args;
    try {
        args = parseArgs(process.argv.slice(2));
    } catch (error) {
        console.error(`❌ ${(error as Error).message}\n`);
        args = { ...parseArgs([]), help: true };
    }

    if (args.help) {
        console.log(`🃏 Scraper Moxfield — importe des decks publics dans la BDD.

Usage : npm run scrape:moxfield -- [options]

Modes (--mode) :
  top         Recherche globale (tri par vues). Plafond API : 10 000 résultats.   [défaut]
  sweep       Balayage de TOUTES les cartes de la BDD (tous formats).
              Reprise automatique après arrêt. Tourne pendant des heures.
  commander   Partition par commander (dépasse le plafond des 10 000).
  card        Partition par carte contenue (idem).
  user        Decks publics d'un ou plusieurs auteurs.
  deck        Importe un seul deck (--public-id id_ou_url).

Options :
  --commanders "A|B"        Commanders à crawler (mode commander).
  --commanders-from-db N    Prend N légendaires de la BDD (mode commander).
  --cards "A|B"             Cartes à crawler (mode card).
  --users "a|b"             Auteurs à crawler (mode user).
  --public-id X             publicId ou URL de deck (mode deck).
  --format fmt              Filtre de format (défaut : all = tous les formats ; commander, modern…).
  --max-decks N             Max de NOUVEAUX decks importés (0 = illimité, défaut 100).
  --max-pages N             Max de pages de recherche (0 = toutes).
  --page-size N             Taille de page (défaut 64).
  --per-partition N         Max de decks vus par commander/carte/user (défaut 100).
  --delay MS                Délai mini entre requêtes API (défaut 500ms).
  --refresh                 Re-fetch et MET À JOUR les decks déjà en base
                            (au lieu de les skipper) — utiles pour rafraîchir
                            les listes qui ont changé sur Moxfield.

Mode sweep uniquement :
  --only-commanders         Ne balaye que les cartes légendaires (~4 000).
  --progress-every N        Sauvegarde la progression toutes les N cartes (défaut 1 = chaque carte).
  --reset                   Repart de zéro (ignore la progression sauvegardée).
  --start-from NAME         Commence à partir de ce nom de carte.

Env : MOXFIELD_USER_AGENT (accès dédié éventuel), MOXFIELD_API_URL,
      MOXFIELD_TRANSPORT (auto|curl|fetch), MOXFIELD_MIN_DELAY_MS,
      MOXFIELD_MAX_RETRIES.
`);
        return;
    }

    console.log("🃏 Moxfield scraper — démarrage");
    console.log(`   mode=${args.mode} format=${args.format} maxDecks=${args.maxDecks} delay=${args.delayMs}ms`);

    // "all" = tous formats → pas de paramètre fmt dans les recherches
    const searchFmt = args.format === "all" ? undefined : args.format;

    const client = new MoxfieldClient({ minDelayMs: args.delayMs });
    const importer = new DeckImporter();
    const crawler = new MoxfieldCrawler(client, importer, {
        quiet: args.mode === "sweep",
        refresh: args.refresh
    });

    const startTime = Date.now();
    try {
        switch (args.mode) {
            case "top":
                await crawler.crawlTopDecks({
                    fmt: searchFmt,
                    pageSize: args.pageSize,
                    maxDecks: args.maxDecks,
                    maxPages: args.maxPages > 0 ? args.maxPages : Number.POSITIVE_INFINITY
                });
                break;

            case "commander": {
                let commanders = args.commanders;
                if (args.commandersFromDb > 0) {
                    commanders = await commanderNamesFromDb(args.commandersFromDb);
                    console.log(`   📚 ${commanders.length} commander(s) depuis la BDD`);
                }
                if (commanders.length === 0) {
                    console.error("❌ Mode commander : fournir --commanders ou --commanders-from-db N");
                    process.exitCode = 1;
                    return;
                }
                await crawler.crawlByCommander({
                    commanders,
                    fmt: searchFmt,
                    pageSize: args.pageSize,
                    maxDecksPerCommander: args.perPartition,
                    maxDecks: args.maxDecks
                });
                break;
            }

            case "card": {
                if (args.cards.length === 0) {
                    console.error("❌ Mode card : fournir --cards \"A|B\"");
                    process.exitCode = 1;
                    return;
                }
                await crawler.crawlByCard({
                    cardNames: args.cards,
                    fmt: searchFmt,
                    pageSize: args.pageSize,
                    maxDecksPerCard: args.perPartition,
                    maxDecks: args.maxDecks
                });
                break;
            }

            case "user": {
                if (args.users.length === 0) {
                    console.error("❌ Mode user : fournir --users \"a|b\"");
                    process.exitCode = 1;
                    return;
                }
                await crawler.crawlByUser({
                    usernames: args.users,
                    pageSize: args.pageSize,
                    maxDecksPerUser: args.perPartition,
                    maxDecks: args.maxDecks
                });
                break;
            }

            case "sweep": {
                const fmt = args.format === "commander" ? "commander" : "all";
                const plan = await buildSweepPlan(fmt, args.onlyCommanders);
                if (plan.length === 0) {
                    console.error("❌ Plan de balayage vide");
                    process.exitCode = 1;
                    return;
                }
                const planKey = computePlanKey(plan);
                const configKey = sweepConfigKey(fmt, args.onlyCommanders);

                // Reprise
                let startIndex = 0;
                if (!args.reset) {
                    const progress = await loadSweepProgress(configKey);
                    if (progress && progress.planKey === planKey && progress.total === plan.length) {
                        startIndex = Math.min(plan.length, progress.lastIndex + 1);
                        console.log(
                            `   ♻️ Reprise à la carte #${startIndex + 1} « ${plan[startIndex] ?? "—"} » (sauvegardé ${progress.updatedAt})`
                        );
                    } else if (progress) {
                        console.log("   ⚠️ Le plan a changé (re-sync BDD ?) — reprise depuis le début.");
                    }
                }
                if (args.startFrom) {
                    const idx = plan.indexOf(args.startFrom);
                    if (idx === -1) {
                        console.error(`❌ Carte « ${args.startFrom} » introuvable dans le plan`);
                        process.exitCode = 1;
                        return;
                    }
                    startIndex = idx;
                    console.log(`   ▶️ Départ forcé à la carte #${idx + 1} « ${plan[idx]} »`);
                }

                const maxDecks =
                    args.maxDecksProvided && args.maxDecks > 0
                        ? args.maxDecks
                        : Number.POSITIVE_INFINITY;
                const searchFmt = fmt === "all" ? "" : fmt; // "" → pas de filtre fmt

                console.log(
                    `🧹 Balayage : ${plan.length} cartes à traiter (${fmt}), ` +
                    `max ${args.perPartition} decks/carte, ` +
                    `${Number.isFinite(maxDecks) ? `${maxDecks} nouveaux decks max` : "nouveaux decks illimités"}, ` +
                    `progression sauvegardée toutes les ${args.progressEvery} cartes`
                );

                let stop = false;
                const onSignal = () => {
                    stop = true;
                    console.log("\n🛑 Signal reçu — arrêt propre après la carte en cours (Ctrl+C à nouveau pour forcer)…");
                    process.once("SIGINT", () => process.exit(130));
                    process.once("SIGTERM", () => process.exit(143));
                };
                process.once("SIGINT", onSignal);
                process.once("SIGTERM", onSignal);

                const sweepStart = Date.now();
                let lastSavedIndex = startIndex - 1;
                let finishReason: "limit" | "interrupted" | "done" = "done";

                // Heartbeat : prouve que le processus vit même pendant les
                // cartes longues (la ligne de carte n'est loggée qu'à la fin)
                const heartbeat = setInterval(() => {
                    const stats = crawler.getStats();
                    const since = Math.round((Date.now() - sweepStart) / 1000);
                    console.log(
                        `⏳ [${ts()}] heartbeat ${formatEta(since)} — cumul importés=${stats.imported} ` +
                        `découverts=${stats.discovered} échecs=${stats.failed}`
                    );
                }, 60_000);

                for (let i = startIndex; i < plan.length && !stop; i++) {
                    const stats = crawler.getStats();
                    if (stats.imported >= maxDecks) {
                        finishReason = "limit";
                        break;
                    }

                    const before = stats.imported;
                    const cardStart = Date.now();
                    await crawler.crawlOneCard(plan[i], {
                        fmt: searchFmt,
                        pageSize: args.pageSize,
                        maxDecksPerCard: args.perPartition,
                        maxDecks: Number.isFinite(maxDecks) ? maxDecks : 0
                    });
                    const cardSecs = (Date.now() - cardStart) / 1000;

                    const done = i - startIndex + 1;
                    const avgMs = (Date.now() - sweepStart) / done;
                    const etaSeconds = (avgMs * (plan.length - i - 1)) / 1000;
                    const now = crawler.getStats();
                    console.log(
                        `[${ts()}] [${String(i + 1).padStart(String(plan.length).length)}/${plan.length}] ` +
                        `${plan[i]} — +${now.imported - before} (cumul ${now.imported}) ` +
                        `${cardSecs.toFixed(1)}s/carte | ETA ${formatEta(etaSeconds)}`
                    );

                    if (i - lastSavedIndex >= args.progressEvery || stop || i === plan.length - 1) {
                        await saveSweepProgress(configKey, {
                            planKey,
                            lastIndex: i,
                            total: plan.length,
                            format: fmt,
                            onlyCommanders: args.onlyCommanders,
                            updatedAt: ""
                        });
                        lastSavedIndex = i;
                    }
                }

                if (stop) finishReason = "interrupted";
                clearInterval(heartbeat);

                if (finishReason === "limit") {
                    console.log(`   ✅ Limite de ${maxDecks} nouveaux decks atteinte — stop.`);
                }
                const resumeCommand = `npm run scrape:moxfield -- --mode sweep${args.onlyCommanders ? " --only-commanders" : ""}${args.format !== "commander" ? ` --format ${args.format}` : ""}`;
                if (finishReason === "interrupted") {
                    console.log(`🧹 Balayage interrompu — reprendre avec : ${resumeCommand}`);
                } else if (finishReason === "done") {
                    console.log("🧹 Balayage terminé — toutes les cartes ont été traitées.");
                } else {
                    console.log(`🧹 Stop (limite atteinte) — relancer pour continuer : ${resumeCommand}`);
                }
                break;
            }

            case "deck": {
                if (!args.publicId) {
                    console.error("❌ Mode deck : fournir --public-id <id_ou_url>");
                    process.exitCode = 1;
                    return;
                }
                const deck = await client.getDeck(args.publicId);
                const { normalizeDeck, summarize } = await import("./sync/moxfield/normalize");
                const normalized = normalizeDeck(deck);
                const result = await importer.importDeck(normalized, { refresh: args.refresh });
                const icon = result === "imported" ? "✅" : result === "updated" ? "🔄" : "⏭️";
                console.log(`${icon} ${summarize(normalized)}`);
                break;
            }
        }
    } finally {
        const stats = crawler.getStats();
        const elapsed = Math.round((Date.now() - startTime) / 1000);
        console.log("=".repeat(60));
        console.log(
            `📊 Bilan en ${elapsed}s : découverts=${stats.discovered} ` +
            `importés=${stats.imported} mis_à_jour=${stats.updated} déjà_en_base=${stats.skipped} échecs=${stats.failed}`
        );
        if (stats.errors.length > 0) {
            console.log("\n❌ Erreurs :");
            for (const error of stats.errors.slice(0, 10)) console.log(`  - ${error}`);
            if (stats.errors.length > 10) console.log(`  … et ${stats.errors.length - 10} autres`);
        }
        console.log("=".repeat(60));
        await Database.prisma.$disconnect();
    }
}

main().catch(error => {
    console.error("❌ Erreur fatale :", error);
    process.exit(1);
});
