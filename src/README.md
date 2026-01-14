# MTG VRC - Source Code Documentation

## 🎯 Vue d'Ensemble du Projet

````markdown
# MTG VRC - Documentation du Code Source

[![TypeScript](https://img.shields.io/badge/TypeScript-5.x-blue.svg)](https://www.typescriptlang.org/)
[![Node.js](https://img.shields.io/badge/Node.js-18+-green.svg)](https://nodejs.org/)
[![Prisma](https://img.shields.io/badge/Prisma-6.16-blueviolet.svg)](https://www.prisma.io/)

---

## 🎯 Vue d'Ensemble du Projet

**MTG VRC** est un serveur backend Node.js/Express pour gérer une collection MTG (Magic: The Gathering) avec support pour les decks, les cartes, et la synchronisation avec l'API Scryfall.

### Objectifs Principaux

- ✅ **Sync automatique** - 25K+ cartes depuis Scryfall avec scheduler quotidien
- ✅ **Gestion de decks** - Support complet de 6 zones (main, sideboard, commander, etc.)
- ✅ **API RESTful** - 6 routes principales pour toutes les opérations
- ✅ **PostgreSQL + Prisma** - Base de données robuste avec ORM moderne
- ✅ **Logging détaillé** - Fichiers timestampés, non-bloquant, stack traces complètes
- ✅ **Batch processing** - 500 cartes/batch pour performance optimale

### Objectifs Principaux
- ✅ Sync automatique des cartes MTG depuis Scryfall (25K+ cartes)
- ✅ Gestion complète des decks avec 6 zones supportées
- ✅ API RESTful pour opérations sur les decks
- ✅ Persistance en PostgreSQL via Prisma ORM
- ✅ Logging détaillé et non-bloquant
- ✅ Scheduler automatique pour mise à jour quotidienne

## 📂 Structure du `/src`

```
src/
├── README.md                     # Ce fichier
├── Main.ts                       # Point d'entrée + schedulers
├── index.ts                      # Configuration Express
├── scryfall_sync.ts             # Script ancien (deprecated)
├── download-recent-images.ts    # Téléchargement images instances
├── utils.ts                      # Fonctions utilitaires globales
│
├── database/                    # 💾 Couche données PostgreSQL + Prisma
│   ├── dbREADME.md             # 📚 Documentation complète module database
│   ├── Config.ts               # Gestion configuration DB
│   ├── Database.ts             # Singleton Prisma client
│   ├── Instance.ts             # Gestion sessions utilisateur
│   ├── User.ts                 # Modèle utilisateur (findOrCreate, updateLastSeen)
│   ├── Player.ts               # Modèle joueur MTG (deprecated)
│   └── Deck.ts                 # ⭐ Système decks (11 méthodes, 6 zones)
│
└── sync/                        # 🔄 Synchronisation Scryfall
    ├── syncREADME.md           # 📚 Documentation complète module sync
    ├── index.ts                # ⭐ ScryFallSync - Orchestrateur principal
    ├── bulk-client.ts          # Client API Scryfall bulk data
    ├── types.ts                # Définitions TypeScript (CoreCard, Ruling, etc.)
    ├── constants.ts            # Constantes & URLs API
    └── example.ts              # Exemple d'utilisation sync manuelle
```

## 🔑 Modules Clés

### 1. **database/** - Gestion de Base de Données
[Documentation complète →](./database/dbREADME.md)

**Responsabilités**:
- Connexion PostgreSQL via Prisma
- Modèles: User, Player, Deck, DeckCard, Card, Oracle, Set, Ruling
- Opérations CRUD sur les decks
- Validation et gestion des permissions
- **✨ Nouveau:** Cache d'atlas 48h avec invalidation intelligente
- **✨ Nouveau:** Stockage format `{card_id}:{face_index}` pour cartes double face

**Fichiers importants**:
- `Deck.ts`: 11 méthodes pour gestion complète des decks
  - Sauvegarde/chargement
  - Suppression avec vérification permissions
  - Support 6 zones: main, sideboard, commander, companion, oathbreaker, wishboard
  - Export multi-format

### 2. **sync/** - Synchronisation MTG
[Documentation complète →](./sync/syncREADME.md)

**Responsabilités**:
- Téléchargement des données Scryfall (25K+ cartes)
- Parsing streaming (JSONL) sans blocage mémoire
- Traitement par batch (5000 cartes, 10000 rulings)
- Logging détaillé dans fichiers timestampés
- Erreurs non-bloquantes (continue processing)

**Fichiers importants**:
- `index.ts`: Classe ScryFallSync avec logging système complet
- `bulk-client.ts`: Client pour API bulk data Scryfall
- Logging: Crée `/logs/sync-YYYY-MM-DDTHH-MM-SS-MS.log` pour chaque exécution

### 3. **Routes API** - Endpoints HTTP
[Documentation →](../routes/ad/adREADME.md)

**Principaux endpoints** (`/ad` route):
- `parse`: Valider une liste de cartes
- `save`: Créer/mettre à jour un deck
- `load`: Charger un deck (retourne cartes par zone)
- `delete`: Supprimer un deck (propriétaire uniquement)
- `list`: Lister les decks

## 🚀 Architecture et Flux

### Flux de Démarrage
```
npm start
    ↓
Main.ts (point d'entrée)
    ├─→ Initialize Database connection
    ├─→ Setup Express app
    ├─→ Setup Image download scheduler (daily)
    ├─→ Setup Card sync scheduler (1:00 AM daily)
    └─→ Listen on port 3000
```

### Flux de Synchronisation (Automatique à 1:00 AM)
```
Cron Job (0 1 * * *)
    ↓
Main.setupSyncScheduler()
    ↓
ScryFallSync.start()
    ├─→ syncCards()
    │   ├─→ Download ~25 MB bulk data
    │   ├─→ Parse streaming JSONL
    │   ├─→ Process batches (5000 cards)
    │   ├─→ Create Sets/Oracles/Cards
    │   └─→ Log to /logs/sync-YYYY-MM-DD...log
    │
    └─→ syncRulings()
        ├─→ Download rulings bulk data
        ├─→ Parse streaming JSONL
        ├─→ Process batches (10000 rulings)
        └─→ Log to same file
```

### Flux de Création de Deck
```
User Request: POST /ad?q=save:...
    ↓
parseAdQuery() - Extract card list & zone info
    ↓
Deck.saveDeck()
    ├─→ Validate deck rules
    ├─→ Create/update Deck entry
    ├─→ For each card:
    │   ├─→ Validate (required fields)
    │   ├─→ Create DeckCard entry
    │   └─→ Set zone & quantity
    └─→ Return deck with zone breakdown
```

## 📊 Modèles de Données Principaux

### Deck Structure
```typescript
interface Deck {
  id: number;
  name: string;
  owner_id: number;           // User qui possède le deck
  
  cardsByZone: {
    main: CardInDeck[];       // 60+ cartes en Constructed
    sideboard: CardInDeck[];   // 15 cartes max
    commander: CardInDeck[];   // 1 carte exactement
    companion: CardInDeck[];   // 0 ou 1 carte
    oathbreaker: CardInDeck[]; // 1 ou 2 cartes
    wishboard: CardInDeck[];   // Variable selon rules
  };
}

interface CardInDeck {
  card_id: string;
  name: string;
  quantity: number;
  is_commander?: boolean;
  set_id: string;
  image_url: string;
}
```

### Card Structure (from Scryfall)
```typescript
interface Card {
  id: string;                // UUID Scryfall
  name: string;
  set_id: string;            // Code du set (e.g., "neo")
  collector_number: string;  // Numéro collecteur
  rarity: string;            // common, uncommon, rare, mythic
  legalities: {              // JSON avec légalité par format
    "standard": "legal" | "banned" | "restricted",
    "modern": "legal",
    "commander": "legal",
    ...
  };
  image_url: string;         // URL de l'image
}
```

## 🔧 Fonctionnalités Principales

### 1. Gestion des Decks
- ✅ **Créer**: Valider + sauvegarder avec cartes
- ✅ **Charger**: Retourner cartes groupées par zone
- ✅ **Lister**: Lister propres decks ou search publics
- ✅ **Supprimer**: Vérifier propriété avant suppression
- ✅ **Exporter**: Multi-format (Deckstats, Moxfield, etc.)

### 2. Synchronisation Scryfall
- ✅ **Non-bloquante**: Erreurs pas bloquantes, sync continue
- ✅ **Détaillée**: Logs complets avec stack traces dans fichiers
- ✅ **Scheduled**: Automatique à 1:00 AM chaque jour
- ✅ **Streaming**: Parsing JSONL sans débordement mémoire
- ✅ **Batch processing**: Traitement par batch pour performance

### 3. Logging Système
- ✅ **Fichiers timestampés**: `/logs/sync-YYYY-MM-DDTHH-MM-SS-MS.log`
- ✅ **Console + Fichier**: Messages dans console ET fichier
- ✅ **Détails complets**: Stack traces, card names, batch info
- ✅ **Non-bloquant**: WriteStream async, pas de blocage

### 4. Gestion des Permissions
- ✅ **Propriété**: Seul le propriétaire peut modifier/supprimer son deck
- ✅ **Vérification**: User ID comparé avec owner_id du deck
- ✅ **Erreur claire**: Message d'erreur si unauthorized

## 📝 Configuration Requise

### Environment Variables (`.env`)
```env
# Database
DATABASE_URL=postgresql://user:password@host:5432/mtgvrc

# Server
PORT=3000
NODE_ENV=development

# Scryfall (optionnel - utilise defaults)
SCRYFALL_API_BASE=https://api.scryfall.com
SCRYFALL_BULK_URL=https://api.scryfall.com/bulk-data

# Logging
LOG_LEVEL=info
```

### Dépendances Clés
```json
{
  "@prisma/client": "^6.16.3",
  "express": "^4.x",
  "node-cron": "^3.x",
  "node-fetch": "^2.x"
}
```

## 🏃 Commandes Courantes

### Démarrer le serveur
```bash
npm start
# Lance Main.ts, configure schedulers, écoute port 3000
```

### Tester la synchronisation
```bash
npx ts-node src/sync/example.ts
# Lance une sync manuelle avec logs
```

### Générer client Prisma
```bash
npx prisma generate
# Crée les types TypeScript depuis schema.prisma
```

### Migrer la base de données
```bash
npx prisma migrate dev --name description
# Crée une nouvelle migration et applique
```

### Voir les logs de sync
```bash
cat logs/sync-latest.log
# Affiche le dernier fichier log de sync
```

## 🐛 Troubleshooting

### Sync bloque le serveur
- **Ancien problème**: Utilisait `pauseIfErrors()` interactif
- **Solution**: Système non-bloquant implémenté
- **Vérifier**: Logs dans `/logs/` pour détails

### Cartes manquantes après sync
- Chercher dans les logs: `grep "Failed to upsert" logs/sync-*.log`
- Vérifier le timestamp du dernier sync: `ls -lt logs/sync-*.log | head -1`
- Voir l'erreur exacte (stack trace) dans le fichier log

### Permissions refusées sur décks
- Vérifier: `userId` matches `deck.owner_id`
- En base: `SELECT * FROM "Deck" WHERE id = <deck_id>;`

### Connexion PostgreSQL échoue
- Vérifier DATABASE_URL dans .env
- Test: `psql $DATABASE_URL -c "SELECT 1"`

### Scheduler ne s'exécute pas
- Vérifier: Cron expression `'0 1 * * *'` correcte
- Vérifier: Server running à 1:00 AM
- Logs: `grep "Scheduled daily sync" logs/*.log`

## 📚 Documentation Détaillée

- **[Database Module](./database/dbREADME.md)**: Schéma, modèles, méthodes Deck
- **[Sync Module](./sync/syncREADME.md)**: Synchronisation, logging, architecture
- **[Routes Module](../routes/ad/adREADME.md)**: API endpoints, formats deck

## 🔗 Fichiers Importants

### Configuration
- `prisma/schema.prisma` - Schéma base de données
- `.env` - Variables d'environnement
- `package.json` - Dépendances & scripts

### Code
- `src/Main.ts` - Point d'entrée
- `src/index.ts` - Configuration Express
- `src/database/Deck.ts` - Système de decks
- `src/sync/index.ts` - Orchestrateur sync

### Logs & Données
- `logs/` - Fichiers logs de synchronisation
- `generated/prisma/` - Client Prisma généré

## 🎓 Workflow de Développement

### Ajouter une nouvelle route
1. Créer handler dans `/routes/xx/route.ts`
2. Exporter dans `src/index.ts`
3. Tester avec curl/Postman
4. Documenter dans `/routes/xx/xxREADME.md`

### Modifier le schéma Prisma
1. Éditer `prisma/schema.prisma`
2. Lancer: `npx prisma migrate dev --name description`
3. Vérifier: `npx prisma generate`
4. Tester les modèles TypeScript

### Déboguer une sync
1. Chercher erreur dans logs: `grep "❌" logs/sync-*.log | tail -20`
2. Identifier la carte problématique
3. Tester le upsert manuellement
4. Voir la stack trace complète

## 📈 Performance & Limitations

### Limitations Actuelles
- Sync peut durer 1-2h (25K cartes)
- PostgreSQL avec pool limité (~10 connexions)
- Batch de 5000 cartes pour performance mémoire

### Optimisations Futures
- Cache Redis pour cartes fréquemment accédées
- GraphQL pour requêtes complexes
- Recherche elasticsearch pour texte
- CDN pour images cartes

## 🤝 Contribution

Pour contribuer:
1. Créer une branche feature
2. Faire les modifications
3. Tester avec `npm run test` (si disponible)
4. Mettre à jour la documentation
5. Créer un PR

## 📄 License & Attribution

- Données MTG: © Wizards of the Coast
- Images cartes: © Wizards of the Coast  
- Code: Project-specific

## 🆘 Support & Issues

Pour signaler un problème:
1. Vérifier les logs: `/logs/sync-*.log`
2. Reproduire l'erreur
3. Fournir: Logs, étapes reproduction, contexte
4. Vérifier les solutions dans la section Troubleshooting

---

---

**Dernière mise à jour**: 20 Octobre 2025  
**Auteur**: MIAOU.2922  
**Version**: 1.0.0  
**Status**: ✅ Production Ready

[← Retour au README principal](../README.md)
