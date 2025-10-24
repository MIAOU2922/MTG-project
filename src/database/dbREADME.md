````markdown
# Module Database - Documentation Complète

[![Prisma](https://img.shields.io/badge/Prisma-6.16-blueviolet.svg)](https://www.prisma.io/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15+-blue.svg)](https://www.postgresql.org/)

---

## 📋 Vue d'Ensemble

Le module `database/` contient toute la logique de gestion de base de données PostgreSQL via Prisma ORM. Il fournit une couche d'abstraction complète pour interagir avec les données MTG et les decks utilisateur.

### Composants Principaux

| Fichier | Description | Responsabilités |
|---------|-------------|-----------------|
| `Database.ts` | Singleton Prisma | Instance centralisée du client Prisma |
| `Config.ts` | Configuration DB | Paramètres connexion, timestamps |
| `User.ts` | Modèle utilisateur | Création/update utilisateurs, last_seen |
| `Instance.ts` | Sessions | Gestion instances, card_ids, user_ids |
| `Deck.ts` | ⭐ Système decks | 11 méthodes, 6 zones, permissions |

---

## 🎴 Deck.ts - Documentation Complète des 11 Méthodes

### Zones Supportées

Le système supporte **6 zones** pour les decks:

| Zone | Description | Limite typique | Formats |
|------|-------------|----------------|---------|
| `main` | Deck principal | 60+ cartes | Tous formats |
| `sideboard` | Réserve | 15 cartes max | Constructed |
| `commander` | Commandant | 1 carte | Commander, Brawl |
| `companion` | Compagnon | 0-1 carte | Tous formats (optionnel) |
| `oathbreaker` | Oathbreaker + Signature | 1-2 cartes | Oathbreaker |
| `wishboard` | Wishboard | Variable | Casual, certains formats |

---

### Méthodes Statiques (Factory)
```prisma
model User {
  id              Int
  username        String @unique
  email           String @unique
  decks           Deck[]
}

model Deck {
  id              Int
  name            String
  owner_id        Int
  owner           User
  cards           DeckCard[]
  created_at      DateTime
  updated_at      DateTime
}

model DeckCard {
  id              Int
  deck_id         Int
  card_id         String
  zone            String  // main, sideboard, commander, etc.
  quantity        Int
  is_commander    Boolean
}

model Card {
  id              String @id
  name            String
  set_id          String
  collector_number String
  rarity          String
  image_url       String
  legalities      Json  // Format: { "standard": "legal", "modern": "banned", ... }
}

model Oracle {
  id              String @id
  text            String
}

model Set {
  id              String @id
  name            String
  code            String @unique
  released_at     String
}
```

## Relations
- **User ↔ Deck**: Un utilisateur peut avoir plusieurs decks (1:N)
- **Deck ↔ DeckCard**: Un deck contient plusieurs cartes (1:N)
- **DeckCard → Card**: Une carte du deck référence le modèle Card (N:1)
- **Card → Oracle**: Une carte peut avoir un texte Oracle (N:1)
- **Card → Set**: Une carte appartient à un set (N:1)

## Migrations
Les migrations sont stockées dans `/prisma/migrations/`:
- `20251007155317_init`: Création initiale des tables
- `20251008130116_`: Ajout des données initiales
- `20251008130312_change_mapping`: Changements de structure
- `20251008201336_optimize_legalities_to_json`: Optimisation des légalités

## Types TypeScript

### DeckData
```typescript
interface DeckData {
  name: string;
  cards: DeckCardData[];
}

interface DeckCardData {
  card_id: string;
  quantity: number;
  zone: 'main' | 'sideboard' | 'commander' | 'companion' | 'oathbreaker' | 'wishboard';
  is_commander?: boolean;
}
```

### DeckResponse
```typescript
interface DeckResponse {
  id: number;
  name: string;
  owner_id: number;
  cardsByZone: Record<Zone, CardInDeck[]>;
  stats: DeckStats;
}
```

## Usage Examples

### Sauvegarder un deck
```typescript
import Deck from '@/database/Deck';

const deckData = {
  name: 'My Cedh Deck',
  cards: [
    { card_id: 'card1', quantity: 1, zone: 'commander', is_commander: true },
    { card_id: 'card2', quantity: 2, zone: 'main' },
    { card_id: 'card3', quantity: 1, zone: 'sideboard' }
  ]
};

const newDeck = await Deck.saveDeck(userId, deckData);
```

### Charger un deck
```typescript
const deck = await Deck.loadDeck(deckId);
console.log(deck.cardsByZone.main);    // Cartes du main
console.log(deck.cardsByZone.commander); // Commander
```

### Supprimer un deck
```typescript
await Deck.deleteDeck(deckId, userId); // userId doit être propriétaire
```

## Gestion des Permissions
- **Lecture**: Tout utilisateur peut lire les decks publics
- **Modification**: Seul le propriétaire peut modifier son deck
- **Suppression**: Seul le propriétaire peut supprimer son deck

## Transactions
Certaines opérations utilisent des transactions Prisma pour garantir la cohérence:
- Sauvegarde de deck avec toutes ses cartes
- Suppression de deck (supprime aussi les DeckCards)
- Mise à jour massive de cartes

## Performance
- **Indexation**: `user_id`, `card_id` indexés pour requêtes rapides
- **Pagination**: Les listes de decks supportent la pagination
- **Cache**: Considérer la mise en cache pour les decks fréquemment accédés

## Erreurs Courantes

### Erreur: Deck non trouvé
```typescript
// Solution: Vérifier l'ID et les permissions
const deck = await Deck.loadDeck(id);
if (!deck) throw new Error('Deck not found');
```

### Erreur: Permission refusée
```typescript
// Solution: Vérifier que userId match le owner_id
if (deck.owner_id !== userId) {
  throw new Error('Unauthorized: You do not own this deck');
}
```

### Erreur: Validation du deck
```typescript
// Solution: Vérifier les règles MTG
const validation = await Deck.validateDeck(deckData);
if (!validation.valid) {
  console.error(validation.errors); // Affiche les erreurs
}
```

## Voir Aussi
- [`/src/README.md`](../README.md) - Vue d'ensemble du projet
- [`/src/sync/syncREADME.md`](../sync/syncREADME.md) - Documentation du module de synchronisation
- [`/prisma/schema.prisma`](../../prisma/schema.prisma) - Schéma complet
