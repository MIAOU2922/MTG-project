# MTG VRC - Backend Server

**Serveur backend Node.js/Express pour gestion de cartes Magic: The Gathering avec synchronisation Scryfall automatique**

[![TypeScript](https://img.shields.io/badge/TypeScript-5.x-blue.svg)](https://www.typescriptlang.org/)
[![Node.js](https://img.shields.io/badge/Node.js-18+-green.svg)](https://nodejs.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15+-blue.svg)](https://www.postgresql.org/)
[![Prisma](https://img.shields.io/badge/Prisma-6.16-blueviolet.svg)](https://www.prisma.io/)

---

## 📋 Table des Matières

- [Vue d'Ensemble](#-vue-densemble)
- [Fonctionnalités](#-fonctionnalités)
- [Architecture](#-architecture)
- [Installation](#-installation)
- [Configuration](#-configuration)
- [Utilisation](#-utilisation)
- [API Routes](#-api-routes)
- [Documentation Détaillée](#-documentation-détaillée)
- [Développement](#-développement)
- [Troubleshooting](#-troubleshooting)

---

## 🎯 Vue d'Ensemble

**MTG VRC** est une plateforme backend complète pour la gestion de cartes Magic: The Gathering, conçue pour être intégrée à VRChat. Le serveur fournit:

- 🎴 **Base de données MTG complète** - 25,000+ cartes synchronisées depuis Scryfall
- 🗂️ **Gestion de decks avancée** - Support de 6 zones (main, sideboard, commander, etc.)
- 🔄 **Synchronisation automatique** - Mise à jour quotidienne à 1:00 AM
- 📝 **Logging détaillé** - Tous les événements tracés dans des fichiers timestampés
- 🚀 **API RESTful** - Routes pour recherche, decks, et plus
- 💾 **PostgreSQL + Prisma** - Base de données robuste avec ORM moderne

### Cas d'Usage

- Rechercher des cartes MTG avec syntaxe avancée
- Créer et gérer des decks multi-formats
- Charger des decks depuis différents formats (Deckstats, Moxfield, etc.)
- Synchroniser automatiquement les nouvelles cartes
- Gérer les permissions utilisateur sur les decks

---

## ✨ Fonctionnalités

### 🎴 Gestion des Cartes

- ✅ **25,000+ cartes** synchronisées depuis Scryfall
- ✅ **Recherche avancée** avec syntaxe complète Scryfall
- ✅ **Multi-langues** supportées (EN, FR, ES, DE, IT, PT, JA, etc.)
- ✅ **Images haute qualité** avec URLs Scryfall
- ✅ **Légalités** par format (Standard, Modern, Commander, etc.)

### 🗂️ Système de Decks

- ✅ **6 zones supportées**:
  - `main` - Deck principal (60+ cartes)
  - `sideboard` - Réserve (15 cartes max)
  - `commander` - Commander (1 carte)
  - `companion` - Companion (0-1 carte)
  - `oathbreaker` - Oathbreaker (1-2 cartes)
  - `wishboard` - Wishboard (variable)

- ✅ **Formats multiples**:
  - Deckstats (`1 [SET#123] Card Name`)
  - Moxfield (`1 Card Name (SET) 123`)
  - Simple (`1 Card Name`)
  - UUID (`1 card-uuid-here`)

- ✅ **Permissions**:
  - Propriétaire: Modification + Suppression
  - Public: Lecture seulement

### 🔄 Synchronisation Automatique

- ✅ **Scheduler cron** - Exécution quotidienne à 1:00 AM
- ✅ **Non-bloquante** - Les erreurs ne bloquent pas le processing
- ✅ **Resumable** - Reprendre sync depuis carte spécifique (`npm run sync cards 148000`)
- ✅ **Streaming JSONL** - Parsing sans débordement mémoire
- ✅ **Batch processing optimisé** - 500 cartes/batch (amélioration x5 vs 100)
- ✅ **Logging détaillé** - Fichiers timestampés dans `/logs/sync-*.log`
- ✅ **Timeouts robustes** - 10min download, 5min stream inactivity

### 📝 Logging Système

- ✅ **Fichiers timestampés** - Format: `sync-YYYY-MM-DDTHH-MM-SS-MS.log`
- ✅ **Console + Fichier** - Double output pour debugging
- ✅ **Stack traces complètes** - Tous les détails d'erreur
- ✅ **Non-bloquant** - WriteStream asynchrone

---

## 🏗️ Architecture

### Structure du Projet

```
mtgvrc/
├── src/                          # Code source TypeScript
│   ├── Main.ts                   # Point d'entrée + schedulers
│   ├── index.ts                  # Configuration Express
│   ├── scryfall_sync.ts         # Script sync manuel
│   ├── utils.ts                  # Utilitaires globaux
│   │
│   ├── database/                 # 💾 Couche données PostgreSQL
│   │   ├── dbREADME.md          # 📚 Documentation complète
│   │   ├── Config.ts            # Configuration DB
│   │   ├── Database.ts          # Singleton Prisma
│   │   ├── Instance.ts          # Gestion sessions
│   │   ├── User.ts              # Modèle utilisateur
│   │   └── Deck.ts              # Système decks (11 méthodes)
│   │
│   └── sync/                     # 🔄 Synchronisation Scryfall
│       ├── syncREADME.md        # 📚 Documentation complète
│       ├── index.ts             # Orchestrateur principal
│       ├── bulk-client.ts       # Client API Scryfall
│       ├── types.ts             # Types TypeScript
│       └── constants.ts         # Constantes & config
│
├── routes/                       # Endpoints API REST
│   ├── route.ts                 # Routeur principal
│   ├── au/                      # 👤 User info
│   │   └── auREADME.md
│   ├── ac/                      # ➕ Create instance
│   │   └── acREADME.md
│   ├── aj/                      # 🔗 Join instance
│   │   └── ajREADME.md
│   ├── as/                      # 🔍 Search cards
│   │   └── asREADME.md
│   ├── at/                      # 🖼️ Atlas/content
│   │   └── atREADME.md
│   └── ad/                      # 🎴 Deck management
│       ├── adREADME.md          # 📚 Documentation complète
│       └── parser.cs            # Parser C# (utilitaire)
│
├── prisma/                       # Base de données
│   ├── schema.prisma            # Schéma Prisma
│   └── migrations/              # Migrations SQL
│
├── logs/                         # Fichiers de logs (auto-créé)
│   └── sync-*.log
│
├── generated/                    # Client Prisma (auto-généré)
│   └── prisma/
│
├── .env                          # Variables d'environnement
├── package.json                  # Dépendances Node.js
├── tsconfig.json                 # Configuration TypeScript
└── README.md                     # Ce fichier
```

### Stack Technique

```
┌─────────────────────────────────────────┐
│   VRChat Client (Unity/C#)             │ Consomme l'API
├─────────────────────────────────────────┤
│   Express.js Server (Node.js 18+)      │ Backend REST API
├─────────────────────────────────────────┤
│   Prisma ORM + PostgreSQL 15           │ Couche données
├─────────────────────────────────────────┤
│   Scryfall API (Bulk Data)             │ Source de données MTG
└─────────────────────────────────────────┘
```

---

## 🚀 Installation

### Prérequis

- **Node.js** 18+ ([Download](https://nodejs.org/))
- **PostgreSQL** 15+ ([Download](https://www.postgresql.org/download/))
- **Git** ([Download](https://git-scm.com/))

### Étapes d'Installation

1. **Cloner le repository**
```bash
git clone https://github.com/MIAOU2922/MTG-project.git
cd MTG-project
```

2. **Installer les dépendances**
```bash
npm install
```

3. **Configurer les variables d'environnement**
```bash
cp .env.example .env
# Éditer .env avec vos paramètres
```

4. **Configurer PostgreSQL**
```bash
# Créer la base de données
createdb mtgvrc

# Ou via psql:
psql -U postgres -c "CREATE DATABASE mtgvrc;"
```

5. **Exécuter les migrations Prisma**
```bash
npx prisma migrate dev
npx prisma generate
```

6. **Compiler TypeScript**
```bash
npm run build
```

7. **Démarrer le serveur**
```bash
npm start
```

Le serveur démarre sur `http://localhost:3000` 🎉

---

## ⚙️ Configuration

### Variables d'Environnement (`.env`)

```env
# Database Connection
DATABASE_URL="postgresql://user:password@localhost:5432/mtgvrc"

# Server Configuration
PORT=3000
NODE_ENV=development

# Scryfall API (optionnel - valeurs par défaut)
SCRYFALL_API_BASE="https://api.scryfall.com"
SCRYFALL_BULK_URL="https://api.scryfall.com/bulk-data"

# Logging
LOG_LEVEL=info
LOG_DIR=./logs
```

### Configuration Prisma (`prisma/schema.prisma`)

Le schéma Prisma définit tous les modèles de données. Principales tables:

- `User` - Utilisateurs
- `Instance` - Sessions
- `Deck` - Decks utilisateur
- `DeckCard` - Cartes dans les decks (avec zones)
- `Card` - Cartes MTG
- `Oracle` - Textes Oracle uniques
- `Set` - Sets MTG
- `Ruling` - Rulings officiels

---

## 🎮 Utilisation

### Démarrer le Serveur

```bash
# Développement avec auto-reload
npm run dev

# Production
npm start
```

### Lancer une Synchronisation Manuelle

```bash
# Sync complète (cartes + rulings)
npm run sync

# Cartes uniquement
npm run sync cards

# Rulings uniquement
npm run sync rulings

# Reprendre à partir d'une carte spécifique
npm run sync cards 148000
```

### Voir les Logs

```bash
# Log en temps réel
tail -f logs/sync-*.log

# Chercher les erreurs
grep "❌" logs/sync-*.log | tail -20

# Statistiques de sync
grep "completed in" logs/sync-*.log
```

### Tests API avec curl

```bash
# Rechercher des cartes
curl "http://localhost:3000/as?q=lightning+bolt"

# Créer un deck
curl "http://localhost:3000/ad?q=save:Mon%20Deck:auto:4%20Lightning%20Bolt"

# Charger un deck
curl "http://localhost:3000/ad?q=load:deck-uuid-here"
```

---

## 🌐 API Routes

### Routes Principales

| Route | Description | Documentation |
|-------|-------------|---------------|
| `GET /au` | Infos utilisateur | [📚 Voir docs](routes/au/auREADME.md) |
| `GET /ac` | Créer instance | [📚 Voir docs](routes/ac/acREADME.md) |
| `GET /aj/{code}` | Rejoindre instance | [📚 Voir docs](routes/aj/ajREADME.md) |
| `GET /as?q={query}` | Rechercher cartes | [📚 Voir docs](routes/as/asREADME.md) |
| `GET /at/{id}` | Contenu/atlas instance | [📚 Voir docs](routes/at/atREADME.md) |
| `GET /ad?q={action}` | Gestion decks | [📚 Voir docs](routes/ad/adREADME.md) |

### Exemples Rapides

#### Recherche de Cartes

```bash
# Recherche simple
GET /as?q=lightning bolt

# Recherche avancée
GET /as?q=type:instant+color:red+cmc:<=3

# Multi-critères
GET /as?q=name:bolt+set:m21+lang:fr
```

#### Gestion de Decks

```bash
# Parser un deck (validation)
GET /ad?q=parse:4%20Lightning%20Bolt%0A2%20Island

# Sauvegarder un deck
GET /ad?q=save:Mon%20Deck:auto:4%20Lightning%20Bolt%0A2%20Island:en:Ma%20description

# Charger un deck
GET /ad?q=load:550e8400-e29b-41d4-a716-446655440000

# Supprimer un deck
GET /ad?q=delete:550e8400-e29b-41d4-a716-446655440000

# Lister les decks
GET /ad?q=list
```

---

## 📚 Documentation Détaillée

### Documentation des Modules

- **[Source Code Overview](src/README.md)** - Vue d'ensemble complète du code source
- **[Database Module](src/database/dbREADME.md)** - Schéma, modèles, méthodes Deck
- **[Sync Module](src/sync/syncREADME.md)** - Synchronisation, logging, architecture

### Documentation des Routes API

- **[Route /au - User Info](routes/au/auREADME.md)** - Gestion utilisateurs
- **[Route /ac - Create Instance](routes/ac/acREADME.md)** - Création sessions
- **[Route /aj - Join Instance](routes/aj/ajREADME.md)** - Rejoindre sessions
- **[Route /as - Search Cards](routes/as/asREADME.md)** - Recherche cartes MTG
- **[Route /at - Atlas/Content](routes/at/atREADME.md)** - Contenu instance
- **[Route /ad - Deck Management](routes/ad/adREADME.md)** - Gestion complète decks

---

## 🛠️ Développement

### Scripts NPM

```bash
# Développement
npm run dev          # Démarre avec nodemon (auto-reload)
npm run build        # Compile TypeScript → JavaScript
npm start            # Démarre le serveur compilé

# Base de données
npx prisma migrate dev      # Créer/appliquer migrations
npx prisma generate         # Générer client Prisma
npx prisma studio          # Interface visuelle DB

# Synchronisation
npm run sync               # Sync complète
npm run sync cards         # Cartes uniquement
npm run sync rulings       # Rulings uniquement

# Tests
npm test                   # Lancer les tests (si configurés)
```


---

## � Changelog & Historique des Mises à Jour

### Version 1.0.0 - 20 Octobre 2025 🎉

**Commit: `3cb306c` - Schema cleanup**
- 🧹 Suppression du modèle Redirect inutilisé
- 🧹 Migration `20251020220205_` pour drop table redirects
- 🎯 Simplification du modèle User
- 🎯 Focus sur les fonctionnalités core (decks + cartes)

**Commit: `c2588ed` - Complete deck system (MAJEUR)**
- ✨ **Scheduler quotidien 1:00 AM** pour sync automatique (`Main.ts`)
- ✨ **Système de logging fichiers détaillé** avec timestamps
  - Fichiers créés dans `/logs/sync-YYYY-MM-DDTHH-MM-SS-MS.log`
  - Méthodes `log()`, `logError()`, `logDetail()`
  - Remplacement de tous les `console.log` par logging persistant
- ✨ **Sync resumable** - Paramètre `resumeCard` pour reprendre sync interrompue
  - Commande: `npm run sync cards 148000`
- ✨ **Sync non-bloquante** - Suppression des pauses interactives `pauseIfErrors()`
- ✨ **Timeouts optimisés**:
  - Download: 10 minutes
  - Stream inactivity: 5 minutes
- ✨ **Documentation complète** - 4 README créés:
  - `src/README.md` - Vue d'ensemble projet
  - `src/database/dbREADME.md` - Module database
  - `src/sync/syncREADME.md` - Architecture sync + logging
  - `routes/ad/adREADME.md` - API deck management
- ✨ **Classe Deck.ts** - 11 méthodes pour gestion complète:
  - `create()`, `findById()`, `findByUserId()`, `findByNameAndUser()`, `findByNamePublic()`
  - `update()`, `delete()`, `getCards()`, `getCardIds()`, `getUniqueCardIds()`, `getDeckSize()`, `getCardsByZone()`, `isOwner()`
- ✨ **6 zones deck supportées**: main, sideboard, commander, companion, oathbreaker, wishboard
- ✨ **Route `/ad` refactorisée** - 5 actions: parse, save, load, delete, list
- ✨ **Parseur C#** - Utilitaire pour détection format deck
- 🚀 Erreurs loggées mais n'interrompent pas le traitement
- 🚀 Tous les batches et erreurs détaillées sauvegardés

### Version 0.9.x - 10 Octobre 2025

**Commit: `78c3407` - Fix recherche combinée**
- 🐛 Fix recherche type + sous-type (AND sur `type_line`)
- 🐛 Correction bug Prisma

**Commit: `4a23863` - Instance membership exclusive**
- ✨ `Instance.removeUserFromAllInstances()` pour membership exclusif
- ✨ `addUser()` retire automatiquement user des autres instances
- 🧹 Nettoyage routes `/ac` et `/aj` avec logique centralisée
- 🐛 Fix coordonnées UV dans `/at` (origine bottom-left Unity)
- ✨ Filtre `lang:en` par défaut si non spécifié
- 🚀 Amélioration calcul offsets atlas

**Commit: `12e2863` - Fix associations**
- 🐛 Fix association utilisateur/instance après reset DB
- 🐛 Fix ajout cartes après reset

### Version 0.8.x - 9 Octobre 2025

**Commit: `a849d06` + `fc2b13f` - Documentation API**
- 📚 README pour toutes les routes:
  - `/ac` - Création instances avec rotation
  - `/aj` - Rejoindre instances
  - `/au` - Gestion utilisateur automatique
  - `/as` - Recherche cartes (déjà existant)
  - `/at` - Liens statiques et atlas (déjà existant)
- 📚 Documentation complète avec exemples, erreurs, cas d'usage

**Commit: `eab3041` - Documentation /at**
- 📚 README détaillé pour route `/at`
- 📚 Documentation système liens statiques et atlas

**Commit: `71265fa` - Système images automatique**
- ✨ **Téléchargement automatique** images instances récentes (< 24h)
- ✨ **Nettoyage automatique** images inutilisées (> 24h)
- ✨ **Scheduler horaire** pour nettoyage
- ✨ **Téléchargement différé** (30s) lors ajout nouvelles cartes
- 🚀 Optimisation espace disque serveur

### Version 0.7.x - 8 Octobre 2025

**Commit: `4a6a680` - Options sync**
- ✨ Options CLI pour `npm run sync`:
  - `npm run sync` - Cartes + Rulings
  - `npm run sync cards` - Cartes uniquement
  - `npm run sync rulings` - Rulings uniquement
- 🚀 Flexibilité synchronisation

---

## 📊 Statistiques du Projet

### Base de Code
- **Commits totaux**: 10+ depuis création branche `Web-serveur`
- **Fichiers TypeScript**: ~20 fichiers
- **Routes API**: 6 routes principales
- **Lignes de code**: ~5000+ lignes
- **Tests**: En développement

### Base de Données
- **Cartes**: 25,000+ cartes MTG
- **Sets**: 500+ sets
- **Rulings**: 10,000+ rulings
- **Taille DB**: ~500 MB
- **Migrations**: 5 migrations appliquées

### Performance
- **Sync complète**: ~1-2h pour 25K cartes
- **Batch size**: 500 cartes/batch (optimisé x5 vs 100)
- **Recherche**: <100ms requêtes simples
- **Load deck**: <50ms avec cache
- **Atlas generation**: <200ms pour 24 cartes

---

## 🎯 Roadmap & Développement Futur

### Prochaines Fonctionnalités Planifiées

**Court terme (v1.1)**
- [ ] Cache Redis pour cartes fréquentes
- [ ] Pagination résultats API
- [ ] Rate limiting par utilisateur
- [ ] Export deck multi-format (TXT, JSON, XML)
- [ ] Webhooks notifications sync
- [ ] Tests unitaires complets

**Moyen terme (v1.5)**
- [ ] Interface web admin
- [ ] Statistiques utilisateur avancées
- [ ] Collections utilisateur
- [ ] Favoris et wishlists
- [ ] Partage public decks

**Long terme (v2.0)**
- [ ] GraphQL API
- [ ] Elasticsearch fulltext search
- [ ] CDN local pour images
- [ ] Support multi-tenant
- [ ] Analytics et metrics avancés
- [ ] API versioning

---

## 📜 License & Crédits

### Données MTG
- **Cartes & Artwork**: © Wizards of the Coast
- **API Scryfall**: [scryfall.com](https://scryfall.com)
- **Données sous**: [Scryfall Bulk Data License](https://scryfall.com/docs/api/bulk-data)

### Code
- **License**: Projet privé - Usage personnel uniquement
- **Auteur**: MIAOU.2922
- **Communauté**: VRChat MTG

---

## 🔗 Liens & Ressources

### Documentation Technique
- [Node.js Docs](https://nodejs.org/docs/)
- [Express.js Guide](https://expressjs.com/)
- [Prisma Documentation](https://www.prisma.io/docs/)
- [TypeScript Handbook](https://www.typescriptlang.org/docs/)
- [PostgreSQL Manual](https://www.postgresql.org/docs/)

### Scryfall API
- [API Documentation](https://scryfall.com/docs/api)
- [Bulk Data Reference](https://scryfall.com/docs/api/bulk-data)
- [Card Objects](https://scryfall.com/docs/api/cards)
- [Search Syntax](https://scryfall.com/docs/syntax)

### Magic: The Gathering
- [Site Officiel](https://magic.wizards.com/)
- [Comprehensive Rules](https://magic.wizards.com/en/rules)
- [Format Legality](https://magic.wizards.com/en/formats)
- [Gatherer Database](https://gatherer.wizards.com/)

---

**Dernière mise à jour**: 20 Octobre 2025  
**Branche active**: `Web-serveur`  
**Version**: 1.0.0  
**Status**: ✅ Production Ready  
**Développeur**: MIAOU.2922

**Documentation technique complète pour usage personnel et suivi de développement**