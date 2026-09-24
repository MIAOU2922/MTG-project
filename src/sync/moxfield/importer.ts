// =============================================================================
// IMPORTER : NormalizedDeck → Deck + DeckCard dans PostgreSQL.
//
// Correspondance des cartes (par ordre de priorité) :
//   1. scryfall_id (UUID Scryfall) === cards.id      → chemin principal
//   2. name + set code + collector_number             → même impression
//   3. name seul (préférence anglais, n'importe quelle impression)
// =============================================================================

import Database from "@/database/Database";
import { Prisma } from "prisma";
import type { ImportResult, NormalizedDeck, RawDeckCard } from "./types";

/** Pseudo-utilisateur technique propriétaire des decks importés (pas de FK). */
export const IMPORT_USER_ID = "import:moxfield";

interface ResolvedCard {
    card_id: string;
    count: number;
    zone: string;
    is_commander: boolean;
}

export class DeckImporter {
    /** scryfall_id → cards.id (null = introuvable) */
    private readonly scryfallCache = new Map<string, string | null>();
    /** `${name}\u0000${set}\u0000${cn}` → cards.id */
    private readonly printCache = new Map<string, string | null>();
    /** name → cards.id */
    private readonly nameCache = new Map<string, string | null>();

    /**
     * Importe un deck normalisé. 'skipped' si (source, source_id) existe déjà.
     */
    public async importDeck(deck: NormalizedDeck): Promise<ImportResult> {
        const prisma = Database.prisma;

        // Déduplication (source, source_id) avant tout travail
        const existing = await prisma.deck.findFirst({
            where: { source: deck.source, source_id: deck.sourceId },
            select: { id: true }
        });
        if (existing) return "skipped";

        const resolved: ResolvedCard[] = [];
        const unresolvedNames = new Set<string>();

        for (const card of deck.cards) {
            const cardId = await this.resolveCard(card);
            if (!cardId) {
                unresolvedNames.add(card.name);
                continue;
            }
            const existingRow = resolved.find(r => r.card_id === cardId && r.zone === card.zone);
            if (existingRow) {
                existingRow.count += card.quantity;
            } else {
                resolved.push({
                    card_id: cardId,
                    count: card.quantity,
                    zone: card.zone,
                    is_commander: card.isCommander
                });
            }
        }

        try {
            await prisma.deck.create({
                data: {
                    user_id: IMPORT_USER_ID,
                    source: deck.source,
                    source_id: deck.sourceId,
                    source_url: deck.url,
                    format: deck.format?.slice(0, 64) ?? null,
                    author: deck.author?.slice(0, 255) ?? null,
                    name: deck.name.slice(0, 255),
                    description: deck.description?.slice(0, 10_000) ?? null,
                    commander: deck.commander?.slice(0, 255) ?? null,
                    cards: { create: resolved }
                }
            });
        } catch (error) {
            // P2002 = violation d'unicité → un import concurrent a déjà gagné
            if (error instanceof Prisma.PrismaClientKnownRequestError && error.code === "P2002") {
                return "skipped";
            }
            throw error;
        }

        if (unresolvedNames.size > 0) {
            console.warn(
                `  ⚠️ ${unresolvedNames.size} carte(s) non résolue(s) dans ${deck.name} : ` +
                [...unresolvedNames].slice(0, 5).join(", ") +
                (unresolvedNames.size > 5 ? ", …" : "")
            );
        }

        return "imported";
    }

    /** Vérifie si un deck (source, source_id) est déjà en base. */
    public async exists(source: string, sourceId: string): Promise<boolean> {
        const deck = await Database.prisma.deck.findFirst({
            where: { source, source_id: sourceId },
            select: { id: true }
        });
        return deck !== null;
    }

    // -------------------------------------------------------------------------

    private async resolveCard(card: RawDeckCard): Promise<string | null> {
        // 1) scryfall_id direct
        if (card.scryfallId) {
            let id = this.scryfallCache.get(card.scryfallId);
            if (id === undefined) {
                const dbCard = await Database.prisma.card.findUnique({
                    where: { id: card.scryfallId },
                    select: { id: true }
                });
                id = dbCard?.id ?? null;
                this.scryfallCache.set(card.scryfallId, id);
            }
            if (id) return id;
        }

        // 2) nom + impression exacte
        if (card.setCode && card.collectorNumber) {
            const key = `${card.name}\u0000${card.setCode}\u0000${card.collectorNumber}`;
            let id = this.printCache.get(key);
            if (id === undefined) {
                const dbCard = await Database.prisma.card.findFirst({
                    where: {
                        name: card.name,
                        collector_number: card.collectorNumber,
                        set: { set: card.setCode }
                    },
                    select: { id: true },
                    orderBy: { release_at: "desc" }
                });
                id = dbCard?.id ?? null;
                this.printCache.set(key, id);
            }
            if (id) return id;
        }

        // 3) nom seul (anglais en priorité, puis n'importe quelle langue)
        let id = this.nameCache.get(card.name);
        if (id === undefined) {
            let dbCard = await Database.prisma.card.findFirst({
                where: { name: card.name, lang: "en" },
                select: { id: true },
                orderBy: { release_at: "desc" }
            });
            if (!dbCard) {
                dbCard = await Database.prisma.card.findFirst({
                    where: { name: card.name },
                    select: { id: true },
                    orderBy: { release_at: "desc" }
                });
            }
            id = dbCard?.id ?? null;
            this.nameCache.set(card.name, id);
        }
        return id;
    }
}
