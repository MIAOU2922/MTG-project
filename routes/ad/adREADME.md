# API /ad - Deck Management

Gestion complète des decks MTG : parsing, sauvegarde, chargement, et listing.

## Endpoint

```
GET /ad?q=action:param1:param2:param3...
```

Format unifié : un seul paramètre `q` avec séparateurs `:` (comme `/as?q=`).

## Authentification

L'utilisateur est résolu via le **hash SHA-256 de l'IP** (compte le plus récemment actif). Non enregistré → `401 { error: 'unknown_user' }` (faire `GET /aur` d'abord). L'`uid` est la clé 12 hex et identifie le propriétaire des decks (`save`, `delete`, `list`).

---

## Actions Disponibles

### 1. `parse` - Parser et valider un deck

**Description:** Parse un deck depuis n'importe quel format et valide les cartes sans sauvegarder.

**Syntaxe:**
```
/ad?q=parse:deck_list_encoded:format:lang
```

**Paramètres:**
- `parse` - Action
- `deck_list_encoded` - Contenu encodé en URL (requis)
- `format` - `deckstats`, `moxfield`, `auto` (optionnel, défaut: `auto`)
- `lang` - Code langue `en`, `fr`, etc. (optionnel, défaut: `en`)

**Exemples:**
```
# Minimal
GET /ad?q=parse:1%20Lightning%20Bolt%0A2%20Island

# Complet avec zones Deckstats
GET /ad?q=parse:%2F%2FMain%0A4%20Lightning%20Bolt%0A%2F%2FSideboard%0A2%20Island:deckstats:en
```

### 2. `save` - Créer ou modifier un deck

**Description:** Parse un deck et le sauvegarde en base de données. Seul le propriétaire peut modifier.

**Syntaxe:**
```
/ad?q=save:deck_name:format:deck_list_encoded:lang:description
```

**Paramètres:**
- `save` - Action
- `deck_name` - Nom du deck (requis, encodé)
- `format` - `deckstats`, `moxfield`, `auto` (optionnel, défaut: `auto`)
- `deck_list_encoded` - Contenu encodé (requis)
- `lang` - Code langue (optionnel, défaut: `en`)
- `description` - Description du deck (optionnel, encodée)

**Exemples:**
```
# Minimal (format auto-détecté)
GET /ad?q=save:Mon%20Deck:auto:4%20Lightning%20Bolt%0A2%20Island

# Avec zones Deckstats
GET /ad?q=save:Mon%20Deck:deckstats:%2F%2FMain%0A4%20Lightning%20Bolt%0A%2F%2FSideboard%0A1%20Countermagic:en:Mon%20premier%20deck
```

### 3. `load` - Charger un deck et ajouter ses cartes à l'instance

**Description:** Charge un deck et ajoute toutes ses cartes à l'instance active de l'utilisateur. Supporte deux modes :
- **Mode sauvegardé** : Charge un deck depuis la base de données (créé par l'utilisateur ou public)
- **Mode temporaire** : Parse une deck list inline sans la sauvegarder (✨ **NEW**)

**Syntaxe (Mode sauvegardé):**
```
/ad?q=load:deck_id
```

**Syntaxe (Mode temporaire):**
```
/ad?q=load:format:deck_list_encoded:lang
```

**Paramètres (Mode sauvegardé):**
- `load` - Action
- `deck_id` - ID unique du deck (UUID, format: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`)

**Paramètres (Mode temporaire):**
- `load` - Action
- `format` - `deckstats`, `moxfield`, `auto` (optionnel, défaut: `auto`)
- `deck_list_encoded` - Contenu encodé en URL (requis)
- `lang` - Code langue `en`, `fr`, etc. (optionnel, défaut: `en`)

**Détection automatique du mode:**
Le système détecte automatiquement si le premier paramètre après `load:` est un UUID (mode sauvegardé) ou un format/deck list (mode temporaire).

**Permissions:**
- N'importe qui peut charger n'importe quel deck public sauvegardé
- Les decks temporaires sont accessibles à tous (pas de propriété)

**Réponse:**
Retourne les cartes groupées par zone avec count et flag `is_commander` si applicable.

```json
{
  "deck_type": "saved",  // ou "temporary"
  "cards_by_zone": {
    "main": [
      { "count": 1, "name": "Lightning Bolt", "card_id": "..." },
      { "count": 1, "name": "Commander Card", "card_id": "...", "is_commander": true }
    ],
    "sideboard": [],
    "commander": [],
    "companion": [],
    "oathbreaker": [],
    "wishboard": []
  }
}
```

**Exemples:**
```
# Mode sauvegardé - Charger par UUID
GET /ad?q=load:550e8400-e29b-41d4-a716-446655440000

# Mode temporaire - Charger une deck list inline
GET /ad?q=load:auto:4%20Lightning%20Bolt%0A2%20Island:en

# Mode temporaire - Avec zones Deckstats
GET /ad?q=load:deckstats:%2F%2FMain%0A4%20Lightning%20Bolt%0A%2F%2FSideboard%0A2%20Island:en
```

**Différences entre modes:**
- **Sauvegardé** : Détecté automatiquement si le premier paramètre est un UUID valide
- **Temporaire** : Utilisé si le premier paramètre n'est pas un UUID (format de deck)
- Les cartes temporaires sont ajoutées à l'instance mais le deck n'est pas sauvegardé en DB

### 4. `delete` - Supprimer un deck

**Description:** Supprime un deck de la base de données. Seul le propriétaire peut supprimer son propre deck.

**Syntaxe:**
```
/ad?q=delete:deck_id
```

**Paramètres:**
- `delete` - Action
- `deck_id` - ID unique du deck à supprimer (requis, UUID)

**Permissions:**
- Seul le propriétaire du deck peut le supprimer
- Retourne une erreur 403 si l'utilisateur n'est pas le propriétaire

**Exemple:**
```
GET /ad?q=delete:550e8400-e29b-41d4-a716-446655440000
```

### 5. `list` - Lister les decks

**Description:** Liste les decks de l'utilisateur ou cherche des decks publics par nom.

**Syntaxe:**
```
/ad?q=list:search_name
```

**Paramètres:**
- `list` - Action
- `search_name` - Terme de recherche (optionnel, encodé)
  - Si absent : liste les decks de l'utilisateur
  - Si présent : cherche les decks publics par nom

**Exemples:**
```
# Lister mes decks
GET /ad?q=list

# Chercher les decks publics contenant "Lightning"
GET /ad?q=list:Lightning
```

---

## Formats Supportés

L'API est flexible et accepte **n'importe quel format**. Détection automatique ou spécification explicite.

### Deckstats

```
//Main
COUNT [SET#COLLECTOR] NAME
1 [ONC#114] Adriana, Captain of the Guard
1 [FIC#188] Tifa, Martial Artist #!Commander
2 Island

//Sideboard
1 [FIC#188] Tifa, Martial Artist
```

### Moxfield

```
COUNT NAME (SET) COLLECTOR_NUMBER
1 Norman Osborn / Green Goblin (SPM) 220
2 Island (LTR) 274
1 Lightning Bolt
```

### Format Simple (Générique)

```
1 Lightning Bolt
2 Island
4 Mountain
```

### Format UUID/Database

```
1 32494237-9d3f-4624-8762-2641fffc2115
3 0000a54c-a511-4925-92dc-01b937f9afad
1 0000579f-7b35-4ed3-b44c-db2a538066fe
```

### Zones Supportées
- `//Main` - Deck principal (défaut)
- `//Sideboard` - Réserve
- `//Commander` - Zone commandant
- `//Companion` - Zone compagnon
- `//Oathbreaker` - Zone Oathbreaker
- `//Wishboard` - Wishboard

### Marqueurs Spéciaux
- `#!Commander` (dans Deckstats) : Marque une carte comme commandant

---

## Tableau Récapitulatif

| Action | Syntaxe | Exemple |
|--------|---------|---------|
| **parse** | `parse:list:format:lang` | `parse:4%20Card:auto:en` |
| **save** | `save:name:format:list:lang:desc` | `save:Mon%20Deck:auto:4%20Card:en` |
| **load** | `load:id` | `load:abc-123-def-456` |
| **delete** | `delete:id` | `delete:abc-123-def-456` |
| **list** | `list:search` | `list` ou `list:Lightning` |

## Réponses JSON

Toutes les réponses partagent l'en-tête standardisé :

```json
{
  "link_type": "d",
  "link_id": "<action>",
  "iid": <number|null>,
  "uid": "330ea1cc96e9",
  "time": 1728499200000,
  "data": { ... }
}
```

### `parse`

```json
{
  "link_type": "d",
  "link_id": "parse",
  "iid": null,
  "uid": "330ea1cc96e9",
  "time": 1728499200000,
  "data": {
    "action": "parse",
    "format": "deckstats",
    "deck_count": 60,
    "commander": "Tifa, Martial Artist",
    "cards": [
      {
        "count": 4,
        "name": "Lightning Bolt",
        "set": "M21",
        "collector_number": "154",
        "lang": "en",
        "card_id": "595ae6ab-f0d4-489b-bb99-99a3f1b96e93",
        "rarity": "common",
        "type_line": "Instant",
        "oracle_id": "8485cfaa-1dbf-432b-b5d0-92a6aa6a329b",
        "is_commander": false,
        "zone": "main",
        "found": true,
        "found_as": "exact",
        "match_confidence": 1
      }
    ],
    "stats": {
      "found": 55,
      "not_found": 5,
      "partial_matches": 3,
      "errors": []
    }
  }
}
```

Champs par carte : `count`, `name`, `set`, `collector_number`, `lang`, `card_id` (si trouvée), `rarity`, `type_line`, `oracle_id`, `is_commander`, `zone`, `found` (booléen), `found_as` (`exact` / `partial` / `not_found`), `match_confidence` (1.0 exact, 0.8 partial, 0 non trouvée).

### `save`

```json
{
  "link_type": "d",
  "link_id": "save",
  "iid": null,
  "uid": "330ea1cc96e9",
  "time": 1728499200000,
  "data": {
    "action": "save",
    "deck_id": "550e8400-e29b-41d4-a716-446655440000",
    "deck_name": "Mon Deck",
    "commander": "Tifa, Martial Artist",
    "cards_found": 55,
    "cards_not_found": 5,
    "total_cards": 60,
    "message": "Deck created successfully"
  }
}
```

### `load`

```json
{
  "link_type": "d",
  "link_id": "load",
  "iid": 42,
  "uid": "330ea1cc96e9",
  "time": 1728499200000,
  "data": {
    "action": "load",
    "deck_type": "saved",
    "deck_id": "550e8400-e29b-41d4-a716-446655440000",
    "deck_name": "Mon Deck",
    "commander": "Tifa, Martial Artist",
    "unique_cards": 35,
    "total_cards": 60,
    "cards_added_to_instance": 60,
    "cards_by_zone": {
      "main": [ { "count": 4, "name": "Lightning Bolt", "card_id": "...", "is_commander": false } ],
      "sideboard": [],
      "commander": [ { "count": 1, "name": "Tifa, Martial Artist", "card_id": "...", "is_commander": true } ],
      "companion": [],
      "oathbreaker": [],
      "wishboard": []
    },
    "message": "Deck loaded successfully and cards added to instance"
  }
}
```

### `delete`

```json
{
  "link_type": "d",
  "link_id": "delete",
  "iid": null,
  "uid": "330ea1cc96e9",
  "time": 1728499200000,
  "data": {
    "action": "delete",
    "deck_id": "550e8400-e29b-41d4-a716-446655440000",
    "deck_name": "Mon Deck",
    "message": "Deck deleted successfully"
  }
}
```

### `list`

```json
{
  "link_type": "d",
  "link_id": "list",
  "iid": null,
  "uid": "330ea1cc96e9",
  "time": 1728499200000,
  "data": {
    "action": "list",
    "search_name": null,
    "decks_count": 3,
    "decks": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "name": "Red Aggro",
        "description": "Fast red deck",
        "commander": null,
        "owner_id": "330ea1cc96e9",
        "is_owner": true,
        "cards_count": 60,
        "created_at": "2026-09-19T10:00:00.000Z",
        "updated_at": "2026-09-19T10:00:00.000Z"
      }
    ]
  }
}
```

---

## Codes d'Erreur

| Code | Description |
|------|-------------|
| 400 | Paramètres manquants, invalides ou aucune carte trouvée |
| 403 | Permissions insuffisantes (ex: modifier un deck d'une autre personne) |
| 404 | Ressource non trouvée (deck inexistant) |
| 500 | Erreur serveur |

---

## Permissions

- **Création** (`save`): N'importe quel utilisateur
- **Modification** (`save` avec changements): Seul le propriétaire
- **Chargement** (`load`): N'importe quel utilisateur peut charger n'importe quel deck public
- **Suppression** (`delete`): Seul le propriétaire du deck
- **Listing** (`list`): Utilisateur voit ses decks, peut chercher les decks publics

---

## Encodage des Paramètres

Tous les paramètres doivent être encodés en URL :

**JavaScript:**
```javascript
const deckName = "Mon Deck";
const encoded = encodeURIComponent(deckName); // "Mon%20Deck"
const url = `/ad?q=save:${encoded}:...`;
```

**Bash avec jq:**
```bash
ENCODED=$(echo -n "Mon Deck" | jq -sRr @uri)
curl "http://localhost:3000/ad?q=save:$ENCODED:..."
```

---

## Comment Importer depuis Deckstats ou Moxfield

Puisque l'import d'URL n'est pas supporté, voici comment faire :

### Option 1 : Copier-coller le contenu brut

1. Ouvre https://deckstats.net/decks/... ou https://moxfield.com/decks/...
2. **Copie tout le contenu du deck** (les cartes avec leur zone/set)
3. **Utilise un client C# (parser.cs)** ou encode manuellement avec `encodeURIComponent()`
4. **Appelle `/ad?q=save:...`** avec le contenu encodé

### Avec le parser C# (Recommandé)

Utilise la fonction `DeckListParser.ParseDeckList()` du projet C# :

```csharp
string deckList = /* contenu copié depuis Deckstats ou Moxfield */;
string query = DeckListParser.ParseDeckList(deckList, "Mon Deck");
// Résultat: "save:Mon%20Deck:deckstats:%2F%2FMain%0A..."
string fullUrl = "https://mtg.hactazia.fr/ad?q=" + query;
```

---

## Notes d'Implémentation

- **Format flexible** : L'API accepte Deckstats, Moxfield, format simple, UUID, ou n'importe quel format
- **Détection automatique** : Par défaut, essaie tous les formats et prend le premier qui fonctionne
- Les cartes de set type "alchemy" sont exclues automatiquement
- Recherche multi-langue: English par défaut, fallback sur la langue demandée
- Quand un deck est chargé, toutes ses cartes (avec répétitions) sont ajoutées à `instance.card_ids`
- Le deck size inclut les répétitions (2x Lightning Bolt = 2 dans le total)
- Support des cartes à double face avec zones détectées automatiquement
- Les zones non mentionnées sont assignées à `main` par défaut
- Retour du `load` : cartes groupées par zone avec `count` et flag `is_commander` optionnel
