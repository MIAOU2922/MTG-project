// =============================================================================
// NORMALISATION : MoxfieldDeck (JSON API) → NormalizedDeck (format interne)
// La résolution des cartes vers notre table `cards` se fait dans l'importer
// (elle nécessite la BDD). Ici on ne fait que mettre en forme.
// =============================================================================

import type {
    MoxfieldBoard,
    MoxfieldBoardCard,
    MoxfieldDeck,
    NormalizedDeck,
    RawDeckCard
} from "./types";

export const SOURCE_NAME = "moxfield";

/** Boards Moxfield → zones de notre table deck_cards */
const BOARD_ZONE_MAP: Record<string, string> = {
    commanders: "commander",
    companions: "companion",
    signatureSpells: "oathbreaker",
    mainboard: "main",
    sideboard: "sideboard",
    maybeboard: "maybeboard"
};

/** Boards exotiques (attractions, contraptions, planes, schemes, stickers) :
 *  conservés sous leur propre nom de zone. */
const EXOTIC_BOARDS = new Set([
    "attractions",
    "contraptions",
    "planes",
    "schemes",
    "stickers"
]);

/** Boards à ignorer entièrement (jetons générés par Moxfield) */
const SKIPPED_BOARDS = new Set(["tokens"]);

/** Formats où la carte "à la une" d'un deck est bien le commander */
const COMMANDER_FORMATS = /commander|brawl|oathbreaker|edh/i;

export function normalizeDeck(deck: MoxfieldDeck): NormalizedDeck {
    const cards: RawDeckCard[] = [];

    for (const [boardName, board] of Object.entries(deck.boards ?? {})) {
        if (SKIPPED_BOARDS.has(boardName)) continue;
        const zone = BOARD_ZONE_MAP[boardName] ?? (EXOTIC_BOARDS.has(boardName) ? boardName : "main");
        const isCommander = boardName === "commanders";
        for (const entry of Object.values(board.cards ?? {})) {
            cards.push(normalizeBoardCard(entry, zone, isCommander));
        }
    }

    const isCommanderFormat = COMMANDER_FORMATS.test(deck.format ?? "");
    // Carte "à la une" du deck si présente (utile quand boards.commanders est vide)
    if (deck.main && isCommanderFormat && !cards.some(c => c.isCommander)) {
        cards.push(normalizeMainCard(deck.main));
    }

    const commander =
        cards.find(c => c.isCommander)?.name ?? (isCommanderFormat ? deck.main?.name ?? null : null);

    return {
        source: SOURCE_NAME,
        sourceId: deck.publicId,
        url: deck.publicUrl,
        name: deck.name ?? deck.publicId,
        description: deck.description?.trim() || null,
        commander,
        format: deck.format ?? null,
        author: deck.createdByUser?.userName ?? deck.authors?.[0]?.userName ?? null,
        cards
    };
}

function normalizeBoardCard(entry: MoxfieldBoardCard, zone: string, isCommander: boolean): RawDeckCard {
    return {
        scryfallId: entry.card?.scryfall_id || undefined,
        name: entry.card?.name ?? "Unknown",
        setCode: entry.card?.set || undefined,
        collectorNumber: entry.card?.cn || undefined,
        quantity: Math.max(1, entry.quantity ?? 1),
        zone,
        isCommander
    };
}

function normalizeMainCard(card: MoxfieldDeck["main"] & object): RawDeckCard {
    const c = card as NonNullable<MoxfieldDeck["main"]>;
    return {
        scryfallId: c.scryfall_id || undefined,
        name: c.name ?? "Unknown",
        setCode: c.set || undefined,
        collectorNumber: c.cn || undefined,
        quantity: 1,
        zone: "commander",
        isCommander: true
    };
}

/** Résumé lisible d'un deck normalisé (pour les logs) */
export function summarize(n: NormalizedDeck): string {
    const byZone = new Map<string, number>();
    for (const c of n.cards) byZone.set(c.zone, (byZone.get(c.zone) ?? 0) + 1);
    const zones = [...byZone.entries()]
        .map(([z, k]) => `${z}:${k}`)
        .join(" ");
    return `${n.name} [${n.sourceId}] (${n.format ?? "?"}) ${zones}`;
}
