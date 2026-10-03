// =============================================================================
// AUTO-SWEEP (TEMPORAIRE) — relance le sweep de base au démarrage du serveur
//
// Tant que le balayage Moxfield « de base » (mode sweep, tous formats, toutes
// cartes) n'a PAS parcouru une fois toute la liste des cartes, le serveur le
// relance automatiquement :
//   - au démarrage du serveur (après ~15 s), puis
//   - toutes les CHECK_INTERVAL_MS minutes (auto-guérison si le sweep meurt).
//
// La progression est lue dans `configs` (clé `moxfield:sweep:progress:all:all`
// écrite par le CLI du scraper après chaque carte). Balayage considéré terminé
// quand lastIndex >= total - 1 → plus aucune relance automatique.
//
// Garde-fous :
//   - un seul sweep lancé par CE serveur (suivi du processus enfant) ;
//   - si l'enfant est vivant mais que la progression est figée depuis plus de
//     LIVE_TIMEOUT_MS, il est tué et relancé (hang/OOM/processus zombie).
//
// ⚠️ Le serveur étant désormais le seul gestionnaire du sweep, ne plus lancer
// de sweep manuel (nohup sur l'hôte) : il y aurait double crawl.
//
// Env :
//   MOXFIELD_AUTO_SWEEP=0                     → désactive tout (à faire quand le
//                                               balayage est terminé)
//   MOXFIELD_AUTO_SWEEP_DELAY_MS (déf. 500)   → --delay du sweep
//   MOXFIELD_AUTO_SWEEP_PER_PARTITION (déf. 300) → --per-partition du sweep
//   MOXFIELD_AUTO_SWEEP_MAX_OLD_SPACE (déf. 2048) → --max-old-space-size de node
// =============================================================================

import { spawn, type ChildProcess } from "child_process";
import fs from "fs";
import path from "path";
import { pathToFileURL } from "url";
import Config from "@/database/Config";

/** Clé de progression du sweep « de base » : tous formats, toutes cartes. */
const SWEEP_KEY = "moxfield:sweep:progress:all:all";
/** Intervalle entre deux vérifications (auto-guérison). */
const CHECK_INTERVAL_MS = 10 * 60_000;
/**
 * Si NOTRE enfant est vivant mais que la progression n'a pas bougé depuis
 * plus que ça (une carte peut prendre ~10 min au pire), on le tue et on
 * relance : il est bloqué (boucle d'événements gelée, curl figé, OOM…).
 */
const LIVE_TIMEOUT_MS = 20 * 60_000;
/** Délai avant la première vérification après le démarrage du serveur. */
const FIRST_CHECK_MS = 15_000;
/** Délai avant re-vérification après la mort (ou le kill) de l'enfant. */
const RESPAWN_DELAY_MS = 10_000;

interface SweepProgressRaw {
    planKey?: string;
    lastIndex?: number;
    total?: number;
    format?: string;
    onlyCommanders?: boolean;
    updatedAt?: string;
}

type ProgressState =
    | { kind: "none" } // jamais sauvegardée → rien n'a été fait
    | { kind: "complete" } // dernière carte traitée → terminé
    | { kind: "incomplete"; ageMs: number | null };

export class AutoSweep {
    private readonly enabled: boolean;
    private child: ChildProcess | null = null;
    private checking = false;

    constructor() {
        this.enabled = process.env.MOXFIELD_AUTO_SWEEP !== "0";
    }

    public start(): void {
        if (!this.enabled) {
            console.log("🤖 Auto-sweep : désactivé (MOXFIELD_AUTO_SWEEP=0)");
            return;
        }
        console.log(
            `🤖 Auto-sweep activé — relance le sweep de base tant qu'il n'a pas ` +
            `parcouru toute la liste (vérif toutes les ${CHECK_INTERVAL_MS / 60_000} min)`
        );
        setTimeout(() => void this.checkAndMaybeSpawn(), FIRST_CHECK_MS);
        setInterval(() => void this.checkAndMaybeSpawn(), CHECK_INTERVAL_MS);
    }

    // -------------------------------------------------------------------------

    private async checkAndMaybeSpawn(): Promise<void> {
        if (this.checking) return;
        this.checking = true;
        try {
            const progress = await this.readProgress();

            // Balayage terminé : plus rien à relancer (on arrête un éventuel enfant résiduel).
            if (progress.kind === "complete") {
                if (this.child && this.child.exitCode === null) {
                    console.log("🤖 Auto-sweep : balayage terminé — arrêt de l'enfant résiduel");
                    this.killChild("SIGTERM");
                } else {
                    console.log("🤖 Auto-sweep : balayage terminé (toutes les cartes traitées) — plus de relance");
                }
                return;
            }

            // Déjà lancé par ce serveur et toujours vivant ?
            if (this.child && this.child.exitCode === null) {
                const age = progress.kind === "incomplete" ? progress.ageMs : null;
                if (age !== null && age > LIVE_TIMEOUT_MS) {
                    console.warn(
                        `🤖 Auto-sweep : sweep vivant (pid ${this.child.pid}) mais progression figée ` +
                        `depuis ${Math.round(age / 60_000)} min → kill et relance`
                    );
                    this.killChild("SIGKILL");
                } else {
                    console.log(`🤖 Auto-sweep : sweep déjà lancé par le serveur (pid ${this.child.pid})`);
                }
                return;
            }

            // Pas d'enfant vivant → relance (le serveur est le seul gestionnaire
            // du sweep ; un sweep manuel sur l'hôte ferait double crawl).
            if (progress.kind === "none") {
                console.log("🤖 Auto-sweep : aucune progression enregistrée — lancement depuis le début");
            }

            this.spawnSweep();
        } catch (error) {
            console.error("🤖 Auto-sweep : erreur pendant la vérification :", error);
        } finally {
            this.checking = false;
        }
    }

    /** Lit la progression du sweep de base dans `configs`. */
    private async readProgress(): Promise<ProgressState> {
        const cfg = await Config.get(SWEEP_KEY, "");
        if (!cfg.value) return { kind: "none" };

        let progress: SweepProgressRaw;
        try {
            progress = JSON.parse(cfg.value) as SweepProgressRaw;
        } catch {
            console.warn("🤖 Auto-sweep : progression illisible (JSON corrompu ?) — relance");
            return { kind: "none" };
        }

        const { total, lastIndex, updatedAt } = progress;
        if (
            typeof total === "number" && total > 0 &&
            typeof lastIndex === "number" && lastIndex >= total - 1
        ) {
            return { kind: "complete" };
        }

        const ageMs = updatedAt ? Date.now() - new Date(updatedAt).getTime() : null;
        if (Number.isNaN(ageMs as number)) return { kind: "incomplete", ageMs: null };
        return { kind: "incomplete", ageMs };
    }

    /**
     * Lance le CLI du sweep directement avec node (pas de couche npm/sh : la
     * mort du processus est fiable et se propage à l'événement `exit`).
     * Logs dans logs/moxfield_sweep.log.
     */
    private spawnSweep(): void {
        const delay = process.env.MOXFIELD_AUTO_SWEEP_DELAY_MS ?? "500";
        const perPartition = process.env.MOXFIELD_AUTO_SWEEP_PER_PARTITION ?? "300";
        const maxOldSpace = process.env.MOXFIELD_AUTO_SWEEP_MAX_OLD_SPACE ?? "2048";
        const logPath = path.join(process.cwd(), "logs", "moxfield_sweep.log");

        let stdioOut: number | "ignore";
        try {
            stdioOut = fs.openSync(logPath, "a");
        } catch {
            stdioOut = "ignore";
        }

        // Même invocation que `npx tsx`, mais sans les intermédiaires :
        // node --require tsx/dist/preflight.cjs --import <loader.mjs> script.ts
        const node = process.execPath;
        const preflight = path.join(process.cwd(), "node_modules", "tsx", "dist", "preflight.cjs");
        const loader = path.join(process.cwd(), "node_modules", "tsx", "dist", "loader.mjs");
        const child = spawn(
            node,
            [
                `--max-old-space-size=${maxOldSpace}`,
                "--require", preflight,
                "--import", pathToFileURL(loader).href,
                "src/moxfield_scraper.ts",
                "--mode", "sweep",
                "--delay", delay,
                "--per-partition", perPartition
            ],
            {
                cwd: process.cwd(),
                detached: true, // groupe de processus dédié → kill(-pid) possible
                stdio: ["ignore", stdioOut, stdioOut],
                env: process.env
            }
        );
        this.child = child;
        console.log(`🤖 Auto-sweep : sweep relancé (pid ${child.pid}) → logs/moxfield_sweep.log`);

        const scheduleRecheck = () => {
            setTimeout(() => void this.checkAndMaybeSpawn(), RESPAWN_DELAY_MS);
        };

        child.on("error", (error) => {
            console.error(`🤖 Auto-sweep : échec de lancement du sweep : ${error.message}`);
            this.child = null;
            scheduleRecheck();
        });
        child.on("exit", (code, signal) => {
            console.log(
                `🤖 Auto-sweep : sweep terminé (code=${code} signal=${signal}) — ` +
                `re-vérification dans ${RESPAWN_DELAY_MS / 1000}s`
            );
            this.child = null;
            if (typeof stdioOut === "number") {
                try { fs.closeSync(stdioOut); } catch { /* déjà fermé */ }
            }
            scheduleRecheck();
        });
        child.unref();
    }

    /** Tue le sweep enfant (tout son groupe, car détaché). */
    private killChild(signal: NodeJS.Signals = "SIGKILL"): void {
        const child = this.child;
        if (!child || child.pid === undefined) return;
        try {
            process.kill(-child.pid, signal);
        } catch {
            try { child.kill(signal); } catch { /* déjà mort */ }
        }
    }
}
