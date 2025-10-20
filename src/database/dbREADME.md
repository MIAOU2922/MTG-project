# Database Module

## Overview
Le module `database/` contient toute la logique de gestion de base de données PostgreSQL via Prisma ORM. Il fournit une couche d'abstraction pour interagir avec les données MTG et les décks utilisateur.

## Structure

### Config.ts
Configuration et initialisation de la connexion PostgreSQL.
- **Responsabilités**:
  - Définir les variables d'environnement
  - Configurer les paramètres de connexion
  - Gérer les timeouts et pools de connexion

### Database.ts
Singleton Prisma pour accès global à la base de données.
- **Exports**:
  - `prisma`: Instance centralisée du client Prisma
  - `default`: Export par défaut
- **Usage**:
  ```typescript
  import Database from '@/database/Database';
  const users = await Database.prisma.user.findMany();
  ```

### Instance.ts
Gestion de l'instance/session serveur.
- **Responsabilités**:
  - Tracker la session courante
  - Gérer les identifiants de session
  - Stocker les données temporaires de session

### User.ts
Modèle et méthodes pour la gestion des utilisateurs.
- **Fonctionnalités principales**:
  - Création/mise à jour d'utilisateurs
  - Authentification et validation
  - Récupération des données utilisateur
  - Relation avec les decks

### Player.ts
Modèle spécifique pour les joueurs MTG.
- **Contient**:
  - Profil joueur
  - Statistiques MTG
  - Historique de jeu
  - Relation avec les decks et collections

### Deck.ts
Système complet de gestion des decks MTG.
- **Modèle**: Représente un deck MTG avec support de 6 zones
- **Zones supportées**:
  - `main`: Zone principale (60+ cartes en Constructed)
  - `sideboard`: Sideboard (15 cartes max)
  - `commander`: Commander (pour le format Commander)
  - `companion`: Companion (pour le format Companion)
  - `oathbreaker`: Oathbreaker (pour le format Oathbreaker)
  - `wishboard`: Wishboard (pour les decks avec wishes)

- **Méthodes principales**:
  1. **saveDeck(userId, deckData)**: Crée ou met à jour un deck
  2. **loadDeck(deckId)**: Charge un deck avec toutes ses cartes
  3. **deleteDeck(deckId, userId)**: Supprime un deck (propriétaire uniquement)
  4. **listDecks(userId, options?)**: Liste les decks d'un utilisateur
  5. **getDeckCards(deckId)**: Retourne les cartes groupées par zone
  6. **addCardToDeck(deckId, cardId, zone, quantity)**: Ajoute une carte
  7. **removeCardFromDeck(deckId, cardId)**: Supprime une carte
  8. **updateCardQuantity(deckId, cardId, quantity)**: Met à jour la quantité
  9. **validateDeck(deckData)**: Valide les règles du deck
  10. **exportDeck(deckId, format)**: Exporte au format demandé
  11. **getDeckStats(deckId)**: Retourne les statistiques du deck

## Schéma Prisma Principal

### Entités Core
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
