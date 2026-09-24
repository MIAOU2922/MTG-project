# Scraper Moxfield (`src/sync/moxfield/`)

Importe des **decklists publiques** de [Moxfield](https://moxfield.com) dans la
BDD (`Deck` + `DeckCard`). Tout le code dépendant de l'API non officielle est
isolé ici : si Moxfield bloque/bloque l'API, on remplace ce module sans toucher
au reste du projet (les adaptateurs Archidekt / Deckstats viendront s'ajouter à
côté avec la même sortie normalisée).

## Architecture

```
Moxfield API (api2.moxfield.com, non officielle)
      │
      ▼
client.ts      → transport HTTP (curl par défaut, retry/backoff, throttle)
      │
      ▼
crawler.ts     → 4 stratégies de découverte (top / commander / carte / auteur)
      │
      ▼
normalize.ts   → MoxfieldDeck JSON → NormalizedDeck (format interne commun)
      │
      ▼
importer.ts    → résolution des cartes + upsert Deck/DeckCard (Prisma)
```

## Endpoints utilisés

| Endpoint | Usage |
|---|---|
| `GET /v2/decks/search` | recherche paginée — **`totalResults` plafonné à 10 000** |
| `GET /v3/decks/all/{publicId}` | deck complet (tous les boards) |
| `GET /v3/cards/named?q=...` | résolution nom → ID interne Moxfield (`commanderCardId`, `cardId`) |
| `GET /v2/users/{username}/decks` | ⚠️ **404 côté serveur depuis 2026** → le mode user passe par `authorUserNames` sur la recherche |

## Dépasser la limite des 10 000 résultats

La recherche renvoie au plus 10 000 résultats par requête. Pour dépasser, on
**partitionne l'espace de recherche** :

- **Par commander** : chaque commander donne son propre espace de 10 000.
  Les noms viennent de la BDD (`--commanders-from-db N` prend N légendaires,
  variantes Arena « A-… » exclues).
- **Par carte contenue** : `cardId` (ex. Sol Ring).
- **Par auteur** : `authorUserNames`.
- Le `publicId` déduplique tout : en base via `@@unique([source, source_id])`
  et en mémoire pendant la run.

## Utilisation

```bash
npm run scrape:moxfield                                       # top decks TOUS formats (vues ↓)
npm run scrape:moxfield -- --mode sweep                       # balayage de TOUTES les cartes (tous formats)
npm run scrape:moxfield -- --mode sweep --format commander    # restreindre au commander
npm run scrape:moxfield -- --mode sweep --only-commanders     # idem, uniquement les légendaires (~4 000)
npm run scrape:moxfield -- --mode commander --commanders "Atraxa, Praetors' Voice|Miirym, Sentinel Wyrm"
npm run scrape:moxfield -- --mode card --cards "Sol Ring|Rhystic Study"
npm run scrape:moxfield -- --mode user --users ComedIan
npm run scrape:moxfield -- --mode deck --public-id j-0aJlxuOUm9FnKRvJcfZw
```

Options : `--format` (**défaut `all` = tous les formats** ; `commander`,
`modern`…), `--max-decks` (0 = illimité), `--max-pages`, `--page-size`,
`--per-partition` (défaut 100, 0 = jusqu'au plafond API de 10 000 par carte),
`--delay` (ms).

### Données stockées

En plus des cartes : `decks.source/source_id/source_url`, **`decks.format`**
(commander, modern, standard, commanderPrecons, none…), **`decks.author`**
(pseudo Moxfield du créateur), `decks.commander` (nom, uniquement pour les
formats à commander).

### Mode sweep (longue durée)

- Balaye **toutes les cartes** de la BDD (noms anglais distincts, hors tokens
  et variantes Arena « A- ») : ~38 000 cartes tous formats (~32 000 en
  commander). Pour chaque carte : résolution nom → ID Moxfield puis recherche
  des decks qui la contiennent (tri par vues). Le `publicId` déduplique les
  chevauchements.
- **Reprise automatique** : la progression est sauvegardée dans la table
  `configs` (clé `moxfield:sweep:progress:…`) après **chaque carte**
  (`--progress-every`, défaut 1) et sur SIGINT/SIGTERM. Relancer la même
  commande reprend là où ça s'était arrêté (si le plan a changé après un re-sync
  BDD, il repart de zéro).
- Durée : ~2 s/carte à `--delay 500` → **~18 h** pour les 32 000 cartes avec
  `--per-partition 100`. Monter `--per-partition` (ex. 500) importe plus de
  decks par carte au même coût de recherche. `--only-commanders` (~4 000 cartes)
  est le meilleur rapport couverture/temps pour les decks commander.
- `--start-from NAME` force le départ à une carte ; `--reset` ignore la
  progression sauvegardée.

```bash
npm run scrape:moxfield -- --mode sweep --delay 500 --per-partition 300
npm run scrape:moxfield -- --mode sweep --only-commanders --delay 400
```

## Environnement

| Variable | Rôle |
|---|---|
| `MOXFIELD_USER_AGENT` | User-Agent dédié (Moxfield peut en fournir un sur demande) |
| `MOXFIELD_TRANSPORT` | `auto` (défaut), `curl` ou `fetch` |
| `MOXFIELD_MIN_DELAY_MS` | throttle entre requêtes (défaut 500 ms) |
| `MOXFIELD_MAX_RETRIES` | retries 429/5xx (défaut 4) |
| `MOXFIELD_API_URL` | base URL de l'API (défaut `https://api2.moxfield.com`) |

**Important — transport** : Cloudflare bloque le `fetch` de Node (empreinte TLS)
sur `/v2/decks/search` et `/v3/cards/named`. Le client utilise donc **curl par
défaut** (détection automatique, repli fetch).

## Correspondance des cartes

Dans la réponse deck, chaque carte a un `scryfall_id` (UUID Scryfall) qui est
exactement la PK `id` de notre table `cards`. Chemin de résolution :

1. `scryfall_id` = `cards.id` (principal)
2. `name + set + collector_number` (même impression)
3. `name` seul (anglais en priorité, impression la plus récente)

## Zones

| Board Moxfield | zone `deck_cards` |
|---|---|
| commanders | `commander` (+ `is_commander`) |
| companions | `companion` |
| signatureSpells | `oathbreaker` |
| mainboard | `main` |
| sideboard | `sideboard` |
| maybeboard | `maybeboard` |
| attractions/contraptions/planes/schemes/stickers | conservées telles quelles |
| tokens | ignorées (jetons générés) |

## Notes

- `Deck.user_id` des imports = `import:moxfield` (pseudo-utilisateur technique,
  aucune ligne `User` créée — pas de FK).
- Un deck déjà présent (`source` + `source_id`) n'est jamais re-fetché.
- Déduplication/versions/hashes : phase 2 (cf. conversation d'origine).
- Volume : garder `--delay` ≥ 500 ms ; l'API est non officielle et peut changer
  sans préavis.
