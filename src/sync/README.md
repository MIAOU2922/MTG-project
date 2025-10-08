# Interfaces TypeScript pour l'API Scryfall

Ce dossier contient les définitions TypeScript complètes pour l'API Scryfall, permettant d'interagir de manière type-safe avec l'API officielle de Scryfall.

## 📁 Structure des fichiers

- `types.ts` - Définitions complètes des interfaces et types
- `example.ts` - Exemple d'implémentation et d'utilisation
- `README.md` - Cette documentation

## 🚀 Utilisation rapide

### Import des types

```typescript
import { 
  CoreCard, 
  CardList, 
  CardSearchParams, 
  ScryfallClient,
  ScryfallUtils
} from './sync/types';
```

### Recherche de cartes

```typescript
const client = new BasicScryfallClient();

// Recherche avec paramètres
const result = await client.searchCards({
  q: 'Lightning Bolt',
  order: 'released',
  unique: 'prints'
});

if (ScryfallUtils.isError(result)) {
  console.error('Erreur:', result.details);
} else {
  console.log(`Trouvé ${result.data.length} cartes`);
}
```

### Récupération d'une carte spécifique

```typescript
// Par nom
const card = await client.getCardByName('Black Lotus');

// Par ID
const cardById = await client.getCard('uuid-de-la-carte');

// Carte aléatoire
const randomCard = await client.getRandomCard();
```

## 🏗️ Types principaux

### CoreCard
Interface principale représentant une carte Magic complète avec tous ses champs :
- **Champs Core** : id, oracle_id, name, lang, etc.
- **Champs Gameplay** : mana_cost, cmc, colors, legalities, etc.
- **Champs Print** : artist, set, rarity, prices, etc.

### CardFace
Pour les cartes multifaces (double-face, split, etc.) :
```typescript
interface CardFace {
  name: string;
  mana_cost: string;
  oracle_text?: string;
  power?: string;
  toughness?: string;
  // ... autres propriétés
}
```

### Set
Représente un set Magic :
```typescript
interface Set {
  id: UUID;
  code: string;
  name: string;
  set_type: SetType;
  released_at?: Date;
  card_count: number;
  // ... autres propriétés
}
```

### Ruling
Représente une règle ou clarification :
```typescript
interface Ruling {
  oracle_id: UUID;
  source: 'wotc' | 'scryfall';
  published_at: Date;
  comment: string;
}
```

## 📊 Types énumérés

### Couleurs
```typescript
type Colors = Array<'W' | 'U' | 'B' | 'R' | 'G'>; // Blanc, Bleu, Noir, Rouge, Vert
```

### Formats de jeu
```typescript
type GameFormat = 
  | 'standard' | 'modern' | 'legacy' | 'vintage' 
  | 'commander' | 'pauper' | 'pioneer' | ...;
```

### Légalité
```typescript
type Legality = 'legal' | 'not_legal' | 'restricted' | 'banned';
```

### Raretés
```typescript
type Rarity = 'common' | 'uncommon' | 'rare' | 'mythic' | 'special' | 'bonus';
```

### Types de sets
```typescript
type SetType = 
  | 'core' | 'expansion' | 'masters' | 'commander'
  | 'draft_innovation' | 'funny' | 'promo' | ...;
```

## 🔍 Recherche avancée

### Paramètres de recherche
```typescript
interface CardSearchParams {
  q: string;                    // Requête de recherche
  order?: 'name' | 'set' | 'released' | 'rarity' | ...;
  dir?: 'auto' | 'asc' | 'desc';
  unique?: 'cards' | 'art' | 'prints';
  include_extras?: boolean;
  page?: number;
}
```

### Exemples de requêtes
```typescript
// Toutes les cartes bleues avec CMC 3
await client.searchCards({ q: 'c:blue cmc:3' });

// Cartes légales en Modern
await client.searchCards({ q: 'f:modern' });

// Cartes d'un artiste spécifique
await client.searchCards({ q: 'artist:"Rebecca Guay"' });

// Cartes avec un mot dans le nom
await client.searchCards({ q: 'dragon' });
```

## 🛠️ Utilitaires

### ScryfallUtils
Classe avec des méthodes utilitaires :

```typescript
// Vérifier si une réponse est une erreur
ScryfallUtils.isError(response);

// Extraire les couleurs d'une carte
ScryfallUtils.getCardColors(card);

// Vérifier la légalité dans un format
ScryfallUtils.isLegalInFormat(card, 'modern');

// Formater un prix
ScryfallUtils.formatPrice(card.prices.usd, 'USD');

// Obtenir l'URL d'image
ScryfallUtils.getCardImageUrl(card, 'normal');
```

## 🎯 Gestion des erreurs

L'API peut retourner des erreurs. Utilisez la fonction utilitaire pour les détecter :

```typescript
const result = await client.searchCards({ q: 'invalid query' });

if (ScryfallUtils.isError(result)) {
  console.error(`Erreur ${result.status}: ${result.details}`);
  return;
}

// result est maintenant typé comme CardList
console.log(`Trouvé ${result.data.length} cartes`);
```

## 📋 Structure d'erreur
```typescript
interface ScryfallError {
  object: 'error';
  status: number;        // Code HTTP
  code: string;          // Code d'erreur Scryfall
  details: string;       // Message d'erreur
  warnings?: string[];   // Avertissements optionnels
}
```

## 🔗 Liens utiles

- [Documentation officielle Scryfall](https://scryfall.com/docs/api)
- [Syntaxe de recherche](https://scryfall.com/docs/syntax)
- [Référence des formats](https://scryfall.com/docs/api/colors)

## 📝 Notes importantes

1. **Rate Limiting** : Respectez les limites de taux de l'API Scryfall (50-100ms entre les requêtes)
2. **Caching** : Considérez mettre en cache les réponses pour améliorer les performances
3. **Images** : Les URLs d'images peuvent changer, téléchargez-les si nécessaire
4. **Nullable** : Beaucoup de champs sont optionnels/nullable, vérifiez toujours leur existence
5. **Multiface** : Les cartes multifaces ont leurs données dans `card_faces` plutôt qu'au niveau racine

## 🚀 Exemple complet

Voir `example.ts` pour un exemple complet d'implémentation avec :
- Client API complet
- Gestion d'erreurs
- Fonctions utilitaires
- Exemples d'utilisation pratique

## 📦 Installation des dépendances

Si vous utilisez ce code dans un projet Node.js, ajoutez :

```json
{
  "devDependencies": {
    "@types/node": "^20.0.0",
    "typescript": "^5.0.0"
  }
}
```

Pour le navigateur, aucune dépendance supplémentaire n'est nécessaire.