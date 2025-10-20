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

export interface IDeck {
    id: string;
    user_id: number;
    name: string;
    description: string | null;
    commander: string | null;
    created_at: Date;
    updated_at: Date;
}

export default class Deck implements IDeck {
    public readonly id: string;
    public readonly user_id: number;
    public readonly name: string;
    public readonly description: string | null;
    public readonly commander: string | null;
    public readonly created_at: Date;
    public readonly updated_at: Date;

    constructor(data: IDeck) {
        this.id = data.id;
        this.user_id = data.user_id;
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
        userId: number,
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
                cards: {
                    create: cards.map((card) => ({
                        card_id: card.card_id,
                        count: card.count,
                        zone: card.zone || 'main',
                        is_commander: card.is_commander || false
                    }))
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
    public static async findByUserId(userId: number): Promise<Deck[]> {
        const decks = await (Database.prisma as any).deck.findMany({
            where: { user_id: userId },
            orderBy: { created_at: 'desc' }
        });
        return decks.map((deck: any) => new Deck(deck));
    }

    /**
     * Find a deck by name and user (user's own decks)
     */
    public static async findByNameAndUser(deckName: string, userId: number): Promise<Deck | null> {
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
     * Update a deck (with permission check)
     */
    public async update(
        userId: number,
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
            await (Database.prisma as any).deckCard.deleteMany({
                where: { deck_id: this.id }
            });
        }

        const updatedDeck = await (Database.prisma as any).deck.update({
            where: { id: this.id },
            data: {
                name: data.name || this.name,
                description: data.description !== undefined ? data.description : this.description,
                commander: data.commander !== undefined ? data.commander : this.commander,
                cards: data.cards
                    ? {
                        create: data.cards.map((card) => ({
                            card_id: card.card_id,
                            count: card.count,
                            zone: card.zone || 'main',
                            is_commander: card.is_commander || false
                        }))
                    }
                    : undefined,
                updated_at: new Date()
            }
        });

        return new Deck(updatedDeck);
    }

    /**
     * Delete a deck (with permission check)
     */
    public async delete(userId: number): Promise<void> {
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
        const deckCards = await (Database.prisma as any).deckCard.findMany({
            where: { deck_id: this.id },
            include: {
                deck: {
                    select: {
                        id: true,
                        name: true
                    }
                }
            }
        });

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
        const deckCards = await (Database.prisma as any).deckCard.findMany({
            where: { deck_id: this.id },
            select: { card_id: true, count: true }
        });

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
        const deckCards = await (Database.prisma as any).deckCard.findMany({
            where: { deck_id: this.id },
            select: { card_id: true }
        });

        return deckCards.map((card: any) => card.card_id);
    }

    /**
     * Get the total number of cards in the deck (counting duplicates)
     */
    public async getDeckSize(): Promise<number> {
        const deckCards = await (Database.prisma as any).deckCard.findMany({
            where: { deck_id: this.id },
            select: { count: true }
        });

        return deckCards.reduce((sum: number, card: any) => sum + card.count, 0);
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
        const deckCards = await (Database.prisma as any).deckCard.findMany({
            where: { deck_id: this.id }
        });

        const cardsByZone: Record<
            string,
            Array<{
                card_id: string;
                count: number;
                is_commander: boolean;
            }>
        > = {};

        for (const deckCard of deckCards) {
            if (!cardsByZone[deckCard.zone]) {
                cardsByZone[deckCard.zone] = [];
            }
            cardsByZone[deckCard.zone].push({
                card_id: deckCard.card_id,
                count: deckCard.count,
                is_commander: deckCard.is_commander
            });
        }

        return cardsByZone;
    }

    /**
     * Check if the user owns this deck
     */
    public isOwner(userId: number): boolean {
        return this.user_id === userId;
    }
}
