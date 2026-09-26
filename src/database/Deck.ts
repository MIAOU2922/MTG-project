import Database from "@/database/Database";

export interface DeckCardData {
    card_id: string;
    count: number;
    zone?: string; // main, sideboard, commander, companion, oathbreaker, wishboard
    is_commander?: boolean;
}

export interface DeckCardWithZone extends DeckCardData {
    zone: string;
    is_commander: boolean;
}

/** Regroupe des DeckCardData (entrée API) en lignes DeckZone (tableaux parallèles) */
function buildZones(cards: DeckCardData[]): Array<{ zone: string; card_ids: string[]; counts: number[] }> {
    const map = new Map<string, { card_ids: string[]; counts: number[] }>();
    for (const card of cards) {
        const zone = card.zone || 'main';
        let entry = map.get(zone);
        if (!entry) {
            entry = { card_ids: [], counts: [] };
            map.set(zone, entry);
        }
        const idx = entry.card_ids.indexOf(card.card_id);
        if (idx === -1) {
            entry.card_ids.push(card.card_id);
            entry.counts.push(card.count);
        } else {
            entry.counts[idx] += card.count;
        }
    }
    return [...map.entries()].map(([zone, entry]) => ({ zone, card_ids: entry.card_ids, counts: entry.counts }));
}

export interface IDeck {
    id: string;
    user_id: string;
    source: string | null;
    source_id: string | null;
    source_url: string | null;
    format: string | null;
    author: string | null;
    name: string;
    description: string | null;
    commander: string | null;
    created_at: Date;
    updated_at: Date;
}

export default class Deck implements IDeck {
    public readonly id: string;
    public readonly user_id: string;
    public readonly source: string | null;
    public readonly source_id: string | null;
    public readonly source_url: string | null;
    public readonly format: string | null;
    public readonly author: string | null;
    public readonly name: string;
    public readonly description: string | null;
    public readonly commander: string | null;
    public readonly created_at: Date;
    public readonly updated_at: Date;

    constructor(data: IDeck) {
        this.id = data.id;
        this.user_id = data.user_id;
        this.source = data.source ?? null;
        this.source_id = data.source_id ?? null;
        this.source_url = data.source_url ?? null;
        this.format = data.format ?? null;
        this.author = data.author ?? null;
        this.name = data.name;
        this.description = data.description;
        this.commander = data.commander;
        this.created_at = data.created_at;
        this.updated_at = data.updated_at;
    }

    /**
     * Create a new deck
     */
    public static async create(
        userId: string,
        deckName: string,
        cards: DeckCardData[],
        description?: string,
        commander?: string
    ): Promise<Deck> {
        const deckData = await (Database.prisma as any).deck.create({
            data: {
                user_id: userId,
                name: deckName,
                description: description || null,
                commander: commander || null,
                zones: {
                    create: buildZones(cards)
                }
            }
        });

        return new Deck(deckData);
    }

    /**
     * Find a deck by ID
     */
    public static async findById(deckId: string): Promise<Deck | null> {
        const deck = await (Database.prisma as any).deck.findUnique({ where: { id: deckId } });
        return deck ? new Deck(deck) : null;
    }

    /**
     * Find decks by user ID
     */
    public static async findByUserId(userId: string): Promise<Deck[]> {
        const decks = await (Database.prisma as any).deck.findMany({
            where: { user_id: userId },
            orderBy: { created_at: 'desc' }
        });
        return decks.map((deck: any) => new Deck(deck));
    }

    /**
     * Find a deck by name and user (user's own decks)
     */
    public static async findByNameAndUser(deckName: string, userId: string): Promise<Deck | null> {
        const deck = await (Database.prisma as any).deck.findFirst({
            where: {
                name: deckName,
                user_id: userId
            }
        });
        return deck ? new Deck(deck) : null;
    }

    /**
     * Find decks by name (public search - anyone can see)
     */
    public static async findByNamePublic(deckName: string): Promise<Deck[]> {
        const decks = await (Database.prisma as any).deck.findMany({
            where: {
                name: {
                    contains: deckName,
                    mode: 'insensitive'
                }
            },
            orderBy: { created_at: 'desc' }
        });
        return decks.map((deck: any) => new Deck(deck));
    }

    /**
     * Find an imported deck by its source + source_id (ex: "moxfield" + publicId)
     */
    public static async findBySource(source: string, sourceId: string): Promise<Deck | null> {
        const deck = await (Database.prisma as any).deck.findFirst({
            where: {
                source,
                source_id: sourceId
            }
        });
        return deck ? new Deck(deck) : null;
    }

    /**
     * Update a deck (with permission check)
     */
    public async update(
        userId: string,
        data: {
            name?: string;
            description?: string;
            commander?: string;
            cards?: DeckCardData[];
        }
    ): Promise<Deck> {
        // Check ownership
        if (this.user_id !== userId) {
            throw new Error('Only the deck owner can modify this deck');
        }

        // Delete existing cards if new cards are provided
        if (data.cards) {
            await (Database.prisma as any).deckZone.deleteMany({
                where: { deck_id: this.id }
            });
        }

        const updatedDeck = await (Database.prisma as any).deck.update({
            where: { id: this.id },
            data: {
                name: data.name || this.name,
                description: data.description !== undefined ? data.description : this.description,
                commander: data.commander !== undefined ? data.commander : this.commander,
                zones: data.cards
                    ? { create: buildZones(data.cards) }
                    : undefined,
                updated_at: new Date()
            }
        });

        return new Deck(updatedDeck);
    }

    /**
     * Delete a deck (with permission check)
     */
    public async delete(userId: string): Promise<void> {
        if (this.user_id !== userId) {
            throw new Error('Only the deck owner can delete this deck');
        }

        await (Database.prisma as any).deck.delete({
            where: { id: this.id }
        });
    }

    /**
     * Get all cards in this deck with their data
     */
    public async getCards(): Promise<
        Array<{
            card_id: string;
            count: number;
            zone: string;
            is_commander: boolean;
            card: {
                name: string;
                set_id: string;
                collector_number: string;
                rarity: string;
            };
        }>
    > {
        const deckCards = await this.getFlatCards();

        const cardsWithData = await Promise.all(
            deckCards.map(async (deckCard: any) => {
                const card = await Database.prisma.card.findUnique({
                    where: { id: deckCard.card_id },
                    select: {
                        name: true,
                        set_id: true,
                        collector_number: true,
                        rarity: true
                    }
                });

                return {
                    card_id: deckCard.card_id,
                    count: deckCard.count,
                    zone: deckCard.zone,
                    is_commander: deckCard.is_commander,
                    card: card || {
                        name: 'Unknown',
                        set_id: '',
                        collector_number: '',
                        rarity: 'unknown'
                    }
                };
            })
        );

        return cardsWithData;
    }

    /**
     * Get card IDs with repetitions based on count
     * Example: if a card has count 3, it will appear 3 times in the array
     */
    public async getCardIds(): Promise<string[]> {
        const deckCards = await this.getFlatCards();

        const cardIds: string[] = [];
        for (const deckCard of deckCards) {
            for (let i = 0; i < deckCard.count; i++) {
                cardIds.push(deckCard.card_id);
            }
        }

        return cardIds;
    }

    /**
     * Get unique card IDs (no repetitions)
     */
    public async getUniqueCardIds(): Promise<string[]> {
        const zones = await this.getZones();
        const ids = new Set<string>();
        for (const zone of zones) {
            for (const id of zone.card_ids) ids.add(id);
        }
        return [...ids];
    }

    /**
     * Get the total number of cards in the deck (counting duplicates)
     */
    public async getDeckSize(): Promise<number> {
        const zones = await this.getZones();
        return zones.reduce((sum, zone) => sum + zone.counts.reduce((a, b) => a + b, 0), 0);
    }

    /**
     * Get cards grouped by zone
     */
    public async getCardsByZone(): Promise<
        Record<
            string,
            Array<{
                card_id: string;
                count: number;
                is_commander: boolean;
            }>
        >
    > {
        const cardsByZone: Record<
            string,
            Array<{
                card_id: string;
                count: number;
                is_commander: boolean;
            }>
        > = {};

        for (const card of await this.getFlatCards()) {
            if (!cardsByZone[card.zone]) {
                cardsByZone[card.zone] = [];
            }
            cardsByZone[card.zone].push({
                card_id: card.card_id,
                count: card.count,
                is_commander: card.is_commander
            });
        }

        return cardsByZone;
    }

    /** Lit les DeckZone du deck */
    private async getZones(): Promise<Array<{ zone: string; card_ids: string[]; counts: number[] }>> {
        return (Database.prisma as any).deckZone.findMany({
            where: { deck_id: this.id }
        });
    }

    /** Aplatit les zones en cartes individuelles (card_id, count, zone, is_commander) */
    private async getFlatCards(): Promise<
        Array<{ card_id: string; count: number; zone: string; is_commander: boolean }>
    > {
        const zones = await this.getZones();
        const cards: Array<{ card_id: string; count: number; zone: string; is_commander: boolean }> = [];
        for (const zone of zones) {
            zone.card_ids.forEach((cardId, i) => {
                cards.push({
                    card_id: cardId,
                    count: zone.counts[i] ?? 1,
                    zone: zone.zone,
                    is_commander: zone.zone === 'commander'
                });
            });
        }
        return cards;
    }

    /**
     * Check if the user owns this deck
     */
    public isOwner(userId: string): boolean {
        return this.user_id === userId;
    }
}
