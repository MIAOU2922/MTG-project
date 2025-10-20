# Sync Module

## Overview
Le module `sync/` gère la synchronisation des données MTG depuis l'API Scryfall. Il télécharge les cartes, les rulings (règlements), et les met à jour dans PostgreSQL de manière fiable et sans blocage.

## Structure

### bulk-client.ts
Client pour télécharger les données en masse depuis Scryfall.
- **Responsabilités**:
  - Récupérer les fichiers de données bulk de Scryfall
  - Parser le format JSONL (JSON Lines)
  - Streamer les données pour éviter les débordements mémoire
  - Gérer les retries et erreurs réseau

- **Classes principales**:
  - `ScryfallBulkDataClient`: Orchestrateur principal
  - Callbacks: `onBatch`, `onError`, `onComplete`

### types.ts
Définitions TypeScript pour les types Scryfall.
- **Types principaux**:
  - `CoreCard`: Données brutes d'une carte Scryfall
  - `Ruling`: Texte d'un ruling MTG
  - `StreamProcessingOptions`: Options de traitement des streams
  - `CardImageUri`: URLs des images (normal, large, etc.)

### index.ts
Logique principale de synchronisation avec système de logging détaillé.
- **Classe**: `ScryFallSync`
- **Responsabilités**:
  - Télécharger et traiter les cartes
  - Télécharger et traiter les rulings
  - Logger toutes les opérations dans des fichiers
  - Gérer les erreurs sans bloquer le process

### constants.ts
Constantes et configuration de Scryfall.
- **Contient**:
  - URLs de l'API Scryfall
  - IDs des fichiers bulk
  - Délais et timeouts
  - Paramètres de traitement par batch

### example.ts
Exemple d'utilisation du module de sync.
- Montre comment:
  - Initialiser la synchronisation
  - Traiter les événements
  - Gérer les erreurs

## Architecture de Synchronisation

### Flux Global
```
┌─────────────────────────────────────────────────────────────┐
│ ScryFallSync.start()                                        │
└──────────────────┬──────────────────────────────────────────┘
                   │
        ┌──────────┴──────────┐
        ▼                     ▼
   syncCards()          syncRulings()
        │                     │
        ├─────────────────────┤
        │                     │
        ▼                     ▼
   Download              Download
   Bulk Data             Rulings
        │                     │
        ▼                     ▼
   Parse JSONL           Parse JSONL
   (streaming)           (streaming)
        │                     │
        ▼                     ▼
   Process by             Process by
   Batch (5000)           Batch (10000)
        │                     │
        ├─► Create Sets ◄─────┤
        │
        ├─► Create Oracles
        │
        ├─► Upsert Cards
        │
        └─► Upsert Rulings
```

### Système de Logging

Le système de logging détaillé sauvegarde TOUT dans des fichiers timestampés:

#### Structure des logs
```
/logs/
├── sync-2025-10-20T12-30-45-123.log
├── sync-2025-10-20T13-00-00-456.log
└── sync-2025-10-20T14-15-30-789.log
```

#### Contenu des logs
```
[2025-10-20T12:30:45.123Z] ============================================================
[2025-10-20T12:30:45.123Z] Sync started at 2025-10-20T12:30:45.123Z
[2025-10-20T12:30:45.123Z] ============================================================
[2025-10-20T12:30:45.150Z] Starting bulk data sync (cards + rulings)...
[2025-10-20T12:30:45.200Z] Starting bulk card sync...
[2025-10-20T12:30:45.250Z] Downloading bulk card data...
[2025-10-20T12:31:15.000Z] Downloaded: 25.4 MB
[2025-10-20T12:31:15.100Z] Processing 5000 cards (batch 1)...
[2025-10-20T12:31:20.500Z] Cards batch completed: 4998 successes, 2 errors
[2025-10-20T12:31:20.600Z] ❌ Failed to upsert card Lightning Bolt:
[2025-10-20T12:31:20.600Z] Stack: Error: Unique constraint failed...
...
[2025-10-20T12:35:00.000Z] ✅ Card sync completed successfully
[2025-10-20T12:35:00.100Z] Starting bulk ruling sync...
[2025-10-20T12:35:30.000Z] Ruling sync completed successfully
[2025-10-20T12:35:30.100Z] ✅ Sync completed successfully
```

#### Méthodes de logging
```typescript
private log(message: string): void
// - Affiche dans console
// - Sauvegarde dans fichier avec timestamp
// - Réutiliser pour: logs info, progrès, résumés

private logError(message: string, error?: any): void
// - Affiche dans console.error
// - Sauvegarde l'erreur complète avec stack trace
// - Réutiliser pour: erreurs, exceptions, problèmes

private logDetail(detail: string): void
// - Sauvegarde uniquement dans fichier (pas de console)
// - Réutiliser pour: statistiques, résumés détaillés
```

## Méthodes Principales

### ScryFallSync.start(options)
Lance la synchronisation complète.

**Paramètres**:
```typescript
interface SyncOptions {
  syncCards?: boolean;      // Défaut: true
  syncRulings?: boolean;    // Défaut: true
}
```

**Usage**:
```typescript
const sync = new ScryFallSync();
await sync.start({ syncCards: true, syncRulings: true });
```

**Résultat**:
- Télécharge, parse, et traite les cartes
- Télécharge, parse, et traite les rulings
- Crée un fichier log détaillé
- Lance les opérations de manière non-bloquante

### syncCards()
Synchronise uniquement les cartes MTG.

**Processus**:
1. Télécharge le fichier bulk de Scryfall (~25 MB)
2. Parse les données en streaming (JSONL)
3. Traite par batch de 5000 cartes
4. Crée les Sets/Oracles/Cartes dans Prisma
5. Log tous les succès et erreurs

**Gestion des erreurs**:
- Les cartes individuelles qui échouent ne bloquent pas les autres
- Les erreurs sont loggées avec détails complets
- Continue au batch suivant même si batch courant a erreurs

### syncRulings()
Synchronise les rulings (règlements MTG).

**Processus**:
1. Télécharge le fichier bulk des rulings
2. Parse en streaming (JSONL)
3. Traite par batch de 10000 rulings
4. Crée les entries dans la table Ruling
5. Log chaque étape

### upsertCardsBatch(cards, batchIndex)
Traite un batch de cartes.

**Étapes**:
1. Extrait les Sets uniques → crée dans DB
2. Extrait les Oracles uniques → crée dans DB
3. Pour chaque carte:
   - Valide les champs requis
   - Crée/met à jour la carte
   - Sauvegarde les légalités en JSON
   - Enregistre les images

**Gestion des erreurs**:
```typescript
try {
    // Traiter la carte
} catch (cardError) {
    batchErrors++;
    this.logError(`Failed to upsert card ${card.name}:`, cardError);
    // Continue au next - NE BLOQUE PAS
}
```

### upsertRulingsBatch(rulings, batchIndex)
Traite un batch de rulings.

**Étapes**:
1. Groupe par card_id
2. Pour chaque carte:
   - Récupère les oracles
   - Crée les entrées Ruling
   - Sauvegarde le texte Oracle

## Gestion des Erreurs (Non-Bloquante)

### Ancien système (SUPPRIMÉ)
```typescript
// ❌ Ancien code - BLOQUANT
await this.pauseIfErrors(true, batchIndex);
// → Pause l'application
// → Demande interaction utilisateur
// → Bloque TOUT le sync
```

### Nouveau système (IMPLÉMENTÉ)
```typescript
// ✅ Nouveau code - NON-BLOQUANT
try {
    // Traiter...
} catch (error) {
    this.logError(`Error:`, error);
    // Continue sans pause
    continue;
}
```

**Avantages**:
- Sync continue même si une carte échoue
- Pas d'interaction utilisateur requise
- Tous les détails sauvegardés dans logs
- Pas de blocage du serveur

## Scheduling Automatique

Le sync est automatiquement déclenché chaque jour à **1:00 AM**:

```typescript
// Dans /src/Main.ts
setupSyncScheduler() {
    cron.schedule('0 1 * * *', async () => {
        const sync = new ScryFallSync();
        await sync.start({ syncCards: true, syncRulings: true });
    });
}
```

**Fonctionnalités**:
- Cron job tous les jours à 1:00 AM (UTC)
- Flag `syncInProgress` empêche exécutions concurrentes
- Erreurs loggées mais ne bloquent pas le serveur
- Logs timestampés pour troubleshooting

## Structure des Données

### Card (Après upsert)
```typescript
{
  id: string;              // UUID Scryfall
  name: string;
  set_id: string;          // Référence à Set
  collector_number: string;
  rarity: string;
  image_url: string;
  legalities: {            // JSON
    "standard": "legal",
    "modern": "banned",
    "commander": "legal"
  };
}
```

### Ruling
```typescript
{
  id?: number;
  card_id: string;         // Référence à Card
  published_at: string;
  comment: string;
}
```

### Oracle
```typescript
{
  id: string;              // UUID Scryfall
  text: string;            // Le texte complet
}
```

## Performance et Optimisations

### Streaming
- Parse les données en streaming (JSONL) pour éviter débordement mémoire
- Traite par batch plutôt que tout à la fois
- Évite charger 1 million de cartes en mémoire

### Batch Processing
- Cartes: 5000 par batch
- Rulings: 10000 par batch
- Permet transactions groupées efficaces

### Transactions Prisma
Chaque batch est une transaction:
```typescript
await Database.prisma.$transaction([...upserts])
```

### Indexation
Database indexe les colonnes clés pour requêtes rapides:
- `card.id` (PK)
- `card.set_id` (FK)
- `ruling.card_id` (FK)
- `oracle.id` (PK)

## Troubleshooting

### Les logs ne sont pas créés
- Vérifier le répertoire `/logs/` existe
- Vérifier les permissions d'écriture
- Chercher l'erreur dans `this.log()` et `this.logError()`

### Sync bloque le serveur
- C'était le problème ancien (pauseIfErrors)
- Nouveau système ne bloque pas
- Chercher les erreurs dans les logs files

### Une carte n'est pas importée
- Chercher son nom dans les logs
- Voir l'erreur exacte (stack trace)
- Potentiellement: champ manquant, conflit d'ID, etc.

### Runnings concurrents
- Flag `syncInProgress` prévient les exécutions concurrentes
- Si une sync dure > 24h, une nouvelle tentera de se lancer
- Voir les logs pour identifier les bottlenecks

## Configuration

Variables d'environnement (`.env`):
```env
DATABASE_URL=postgresql://user:password@localhost:5432/mtg_db
SCRYFALL_BULK_URL=https://api.scryfall.com/bulk-data
SYNC_BATCH_SIZE_CARDS=5000
SYNC_BATCH_SIZE_RULINGS=10000
```

## Voir Aussi
- [`/src/README.md`](../README.md) - Vue d'ensemble du projet
- [`/src/database/dbREADME.md`](../database/dbREADME.md) - Module de base de données
- [`/src/sync/example.ts`](./example.ts) - Exemple d'utilisation
- [`/src/sync/types.ts`](./types.ts) - Types TypeScript
