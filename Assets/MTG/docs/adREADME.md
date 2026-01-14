# API /ad - Deck Management

Gestion complète des decks MTG : parsing, sauvegarde, chargement, et listing.

## Endpoint

```
GET /ad?q=action:param1:param2:param3...
```

Format unifié : un seul paramètre `q` avec séparateurs `:` (comme `/as?q=`).

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
- **Mode temporaire** : Parse une deck list inline sans la sauvegarder

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
- `deck_id` - ID unique du deck (UUID)

**Paramètres (Mode temporaire):**
- `load` - Action
- `format` - `deckstats`, `moxfield`, `auto` (optionnel, défaut: `auto`)
- `deck_list_encoded` - Contenu encodé en URL (requis)
- `lang` - Code langue `en`, `fr`, etc. (optionnel, défaut: `en`)

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
