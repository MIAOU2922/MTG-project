# API Recherche de Cartes Magic: The Gathering

API REST pour rechercher des cartes MTG avec une **syntaxe identique à Scryfall** (https://scryfall.com/docs/syntax).

## Endpoint

```
GET /as?q={query}
```

## ⚠️ Important : Cartes Double Face

Lorsqu'une carte avec plusieurs faces est trouvée (transform, modal_dfc, etc.), **toutes les faces sont automatiquement ajoutées à l'instance**.

**Format de stockage:** `{card_id}:{face_index}`
- Face 0 : `abc123:0` (recto)
- Face 1 : `abc123:1` (verso)

Chaque face aura sa propre image téléchargée et sera incluse dans les atlas générés.

## Syntaxe de Recherche (Scryfall)

### Recherche basique (mots libres)

Un mot libre cherche dans : **nom** (`name` + `printed_name`), **type line** et **texte Oracle** (comportement Scryfall).

```
/as?q=Lightning Bolt
/as?q=draws a card
/as?q=dragon
```

### Filtres de texte

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `name:` | `n:` | Nom de la carte | `name:Lightning` |
| `oracle:` | `o:` | Texte d'oracle | `oracle:Flying` |
| `fulloracle:` | `fo:` | Texte d'oracle complet | `fulloracle:draw` |
| `keyword:` | `kw:` | Mot-clé | `keyword:flying` |
| `flavor:` | `ft:` | Texte de saveur | `flavor:designed` |
| `type:` | `t:` | Type line | `t:creature` |
| `mana:` | `m:` | Coût de mana | `mana:{2}{U}` |
| `!nom` | | Nom exact (carte **ou face**) | `!fire` |

### Statistiques (syntaxe opérateurs `=`, `>=`, `<=`, `>`, `<`, `!=`)

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `mv:` | `cmc:`, `manavalue:` | Valeur de mana | `mv=5`, `mv>=6` |
| `pow:` | `power:` | Force | `pow>=8` |
| `tou:` | `toughness:` | Endurance | `tou<=2` |
| `loy:` | `loyalty:` | Loyauté | `loy=3` |
| `cn:` | `number:` | Numéro de collectionneur | `cn:12` |

### Couleurs

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `c:` | `color:`, `colors:` | Couleurs de la carte | `c:uw`, `c>=wu`, `c<=rg`, `c:azorius` |
| `id:` | `identity:` | Identité colorielle | `id:esper` |

### Sets, rareté, langue, formats

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `set:` | `s:`, `e:`, `edition:` | Code du set | `set:m21` |
| `settype:` | `st:` | Type de set | `settype:expansion` |
| `rarity:` | `r:` | Rareté (codes `c/u/r/m/s/b`, comparaisons `r>=r`) | `r:rare`, `r>=r` |
| `lang:` | `l:`, `language:` | Langue (`lang:any` pour toutes) | `lang:fr` |
| `format:` | `f:` | Format légal | `f:pauper` |
| `banned:` | - | Bannies dans un format | `banned:legacy` |
| `restricted:` | - | Restreintes dans un format | `restricted:vintage` |
| `year:` | - | Année de sortie | `year<=1994` |
| `date:` | - | Date de sortie | `date>=2023-01-01` |
| `layout:` | - | Disposition | `layout:transform` |

### Filtres spéciaux

| Filtre | Description |
|--------|-------------|
| `is:` | `spell`, `permanent`, `vanilla`, `dfc`, `mdfc`, `split`, `flip`, `transform`/`tdfc`, `meld`, `leveler`, `adventure`, `saga`, `class`, `mutate`, `battle`, `funny`, `alchemy`, `promo`, `hires`, `historic`, `party`, `commander`, `partner`, `companion` |
| `not:` | Inverse de `is:` (`not:spell`, `not:permanent`) |
| `include:extras` | Révèle les cartes exclues par défaut (alchemy/alchenemy/funny/A-/memorabilia/scheme/vanguard/phenomenon/plane) |

### Syntaxe avancée

- **Négation** : `-` devant un terme ou filtre (`-t:creature`, `-word`)
- **OU** : `or` entre les termes (`t:fish or t:bird`)
- **Parenthèses** : `t:land (c:uw or c:ub)`
- **Guillemets** : termes avec espaces (`ft:"well done"`)

## Comportement par Défaut

- **Langue** : anglais (`lang:en`) sauf `lang:<code>` ou `lang:any`
- **Exclusions automatiques** (comme Scryfall) : sets `alchemy`/`alchenemy`/`funny` + cartes `A-`, sets `memorabilia`, types `scheme`/`vanguard`/`phenomenon`/`plane`. Utiliser `include:extras`, `settype:X`, `include:X` ou `set:<code>` ciblé pour les révéler.
- **Résultats** : maximum 240 cartes par recherche.

## Format de Réponse JSON

```json
{
  "link_type": "s",
  "link_id": "tergrid",
  "iid": 42,
  "uid": "330ea1cc96e9",
  "time": 1728499200000,
  "data": {
    "query": "tergrid",
    "count": 2,
    "results": [
      {
        "id": "595ae6ab-f0d4-489b-bb99-99a3f1b96e93",
        "name": "Tergrid, God of Fright // Tergrid's Lantern",
        "set": "khm",
        "collector_number": "112",
        "lang": "en",
        "oracle_id": "8485cfaa-1dbf-432b-b5d0-92a6aa6a329b",
        "faces": 2
      }
    ]
  }
}
```

### Champs d'en-tête

| Champ | Type | Description |
|-------|------|-------------|
| `link_type` | string | Type de route : `"s"` pour search |
| `link_id` | string | Requête de recherche originale |
| `iid` | number\|null | ID de l'instance active (null si aucune) |
| `uid` | string | ID utilisateur = clé 12 hex |
| `time` | number | Timestamp Unix en millisecondes |
| `data` | object | Données de la recherche |

### Champs de `data`

| Champ | Type | Description |
|-------|------|-------------|
| `data.query` | string | Requête de recherche |
| `data.count` | number | Nombre de résultats (après remplacement des images manquantes) |
| `data.results` | array | Liste des cartes trouvées (max 240) |

### Champs par carte (`results[]`)

| Champ | Type | Description |
|-------|------|-------------|
| `id` | string | ID unique de la carte |
| `name` | string | Nom de la carte |
| `set` | string | Code du set (ex: `khm`, `m21`) |
| `collector_number` | string | Numéro de collectionneur |
| `lang` | string | Code langue (`en`, `fr`, …) |
| `oracle_id` | string\|null | ID Oracle (première face avec `oracle_id`) |
| `faces` | number | Nombre de faces (1 pour cartes normales) |

### Codes d'Erreur

| Code | Réponse |
|------|---------|
| 400 | `{"error": "Search query parameter \"q\" is required"}` |
| 401 | `{"error": "unknown_user", "hint": "Register via /aur first"}` |
| 500 | `{"error": "Error performing search"}` |
