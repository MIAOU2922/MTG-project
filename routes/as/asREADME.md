# API Recherche de Cartes Magic: The Gathering

Une API REST complète pour rechercher des cartes Magic: The Gathering avec une syntaxe inspirée de Scryfall.

## Endpoint

```
GET /as?q={query}
```

## Syntaxe de Recherche

### Recherche Simple

Les recherches sans filtres spécifiques cherchent dans tous les champs de texte :
- Nom anglais (`name`)
- Nom traduit (`printed_name`)
- Texte d'oracle anglais (`oracle_text`)
- Texte d'oracle traduit (`printed_text`)
- Texte de saveur (`flavor_text`)

**Exemples :**
```
/as?q=Lightning Bolt
/as?q=Cycle alimentaire aérien
/as?q=vol
```

### Recherche avec Filtres

Utilisez la syntaxe `clé:valeur` pour filtrer précisément.

#### Filtres de Base

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `name:` | `n:` | Nom de la carte | `name:Lightning` |
| `set:` | `s:`, `e:` | Code du set | `set:m21` |
| `lang:` | `l:` | Langue (toutes les langues si non spécifié) | `lang:en` |
| `rarity:` | `r:` | Rareté | `rarity:rare` |
| `collector_number:` | `cn:`, `number:` | Numéro de collectionneur | `cn:1` |


#### Types, Sous-types et Texte

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `type:` | `t:` | Type de carte | `type:Creature` |
| `subtype:` | `st:` | Sous-type de carte (ex: rat, goblin, angel...) | `subtype:rat` |
| *(mot seul)* |  | Si un mot n'est pas un filtre connu, il est traité comme un sous-type | `rat` |
| `oracle:` | `o:` | Texte d'oracle | `oracle:Flying` |
| `fulloracle:` | `fo:` | Texte d'oracle complet | `fulloracle:draw` |
| `keyword:` | `kw:` | Mot-clé | `keyword:Flying` |
| `flavor:` | `ft:` | Texte de saveur | `flavor:designed` |

#### Statistiques (avec opérateurs)

Utilisez `>`, `<`, `>=`, `<=`, `=` pour les comparaisons.

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `cmc:` | `mv:`, `manavalue:` | Coût de mana converti | `cmc:>=3`, `cmc:<5` |
| `power:` | `pow:` | Force | `power:>=4` |
| `toughness:` | `tou:` | Endurance | `toughness:<=2` |
| `loyalty:` | `loy:` | Loyauté | `loyalty:3` |
| `powtou:` | `pt:` | Puissance + Endurance | `pt:>=6` |

#### Couleurs

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `color:` | `c:` | Couleurs de la carte | `c:wu`, `c:>=wub`, `c:<=rg` |
| `identity:` | `id:` | Identité colorielle | `id:esper` |
| `commander:` | - | Couleurs du commandant | `commander:wub` |

#### Mana et Coûts

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `mana:` | `m:` | Coût de mana exact | `mana:{2}{U}` |
| `devotion:` | - | Dévotion | `devotion:{u/b}` |
| `produces:` | - | Mana produit | `produces:{G}` |

#### Layout et Types Spéciaux

| Filtre | Description | Exemple |
|--------|-------------|---------|
| `layout:` | Disposition | `layout:transform` |
| `is:` | Filtres spéciaux | `is:spell`, `is:permanent`, `is:vanilla` |
| `not:` | Négation | `not:reprint` |

#### Métadonnées

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `artist:` | `a:` | Artiste | `artist:"proce"` |
| `watermark:` | `wm:` | Filigrane | `watermark:orzhov` |
| `border:` | - | Bordure | `border:black` |
| `frame:` | - | Cadre | `frame:2015` |
| `stamp:` | - | Tampon | `stamp:oval` |
| `game:` | - | Jeu | `game:arena` |

#### Formats et Légalité

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `format:` | `f:` | Format | `f:standard`, `f:modern` |
| `banned:` | - | Bannies | `banned:legacy` |
| `restricted:` | - | Restreintes | `restricted:vintage` |

#### Sets et Organisation

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `block:` | `b:` | Bloc | `block:wwk` |
| `settype:` | `st:` | Type de set | `settype:expansion` |
| `cube:` | - | Cube | `cube:vintage` |

#### Dates et Historique

| Filtre | Description | Exemple |
|--------|-------------|---------|
| `year:` | Année | `year:>=2020` |
| `date:` | Date exacte | `date:>=2023-01-01` |
| `prints:` | Nombre d'impressions | `prints:>=5` |
| `sets:` | Nombre de sets | `sets:1` |

#### Tags (Tagger)

| Filtre | Alias | Description | Exemple |
|--------|-------|-------------|---------|
| `art:` | `atag:`, `arttag:` | Tags d'art | `art:squirrel` |
| `function:` | `otag:`, `oracletag:` | Tags de fonction | `function:removal` |

## Syntaxe Avancée

### Noms Exacts
Préfixez avec `!` pour une recherche exacte :
```
/as?q=!"Lightning Bolt"
```

### Négation
Préfixez avec `-` pour exclure :
```
/as?q=fire -t:creature
/as?q=-c:red
```

### Logique OU
Utilisez `or` entre les termes :
```
/as?q=t:fish or t:bird
```

### Expressions Régulières
Utilisez `//` pour les regex :
```
/as?q=name:/\bbolt\b/
/as?q=t:creature o:/^{T}:/
```

### Guillemets
Utilisez des guillemets pour les termes avec espaces :
```
/as?q=artist:"Vincent Proce"
```

## Exemples Complets


### Recherche Simple et Sous-type

Les recherches sans filtres spécifiques cherchent dans tous les champs de texte :
- Nom anglais (`name`)
- Nom traduit (`printed_name`)
- Texte d'oracle anglais (`oracle_text`)
- Texte d'oracle traduit (`printed_text`)
- Texte de saveur (`flavor_text`)

Si le terme n'est pas un filtre connu, il est aussi interprété comme un sous-type (ex: `rat` retournera toutes les créatures de sous-type rat).

**Exemples :**
```
/as?q=Lightning Bolt
/as?q=Cycle alimentaire aérien
/as?q=vol
/as?q=rat
/as?q=subtype:angel
```
```

### Combinaisons Complexes
```
/as?q=(t:goblin or t:elf) power:>=2
/as?q=mana:{2}{U} type:Instant not:reprint
/as?q=color:>=wub -c:red type:creature
```

## Format de Réponse JSON

```json
{
  "time": 1728345600000,
  "uid": "user123",
  "query": "cmc:3 type:Instant",
  "count": 42,
  "results": [
    {
      "name": "Lightning Bolt",
      "printed_name": "Éclair",
      "set": "m21",
      "collector_number": "153",
      "lang": "fr",
      "rarity": "uncommon",
      "faces": [
        {
          "name": "Lightning Bolt",
          "type_line": "Instant",
          "printed_type_line": "Éphémère",
          "mana_cost": "{R}",
          "cmc": 1,
          "power": null,
          "toughness": null,
          "loyalty": null,
          "colors": ["R"],
          "color_identities": ["R"],
          "keywords": ["Flying"],
          "oracle_text": "Lightning Bolt deals 3 damage to any target.",
          "printed_text": "L'Éclair inflige 3 blessures à une cible, créature ou joueur.",
          "flavor_text": null
        }
      ]
    }
  ]
}
```

### Champs de Réponse

| Champ | Type | Description |
|-------|------|-------------|
| `time` | number | Timestamp de la réponse |
| `uid` | string | ID utilisateur |
| `query` | string | Requête originale |
| `count` | number | Nombre total de résultats |
| `results` | array | Liste des cartes |

### Champs par Carte

| Champ | Type | Description |
|-------|------|-------------|
| `name` | string | Nom anglais |
| `printed_name` | string\|null | Nom traduit |
| `set` | string | Code du set |
| `collector_number` | string | Numéro de collectionneur |
| `lang` | string | Code langue |
| `rarity` | string | Rareté |
| `faces` | array | Faces de la carte |

### Champs par Face

| Champ | Type | Description |
|-------|------|-------------|
| `name` | string | Nom de la face |
| `type_line` | string | Type anglais |
| `printed_type_line` | string\|null | Type traduit |
| `mana_cost` | string\|null | Coût de mana |
| `cmc` | number\|null | Coût de mana converti |
| `power` | string\|null | Force |
| `toughness` | string\|null | Endurance |
| `loyalty` | string\|null | Loyauté |
| `colors` | array | Couleurs |
| `color_identities` | array | Identité colorielle |
| `keywords` | array | Mots-clés |
| `oracle_text` | string\|null | Texte d'oracle anglais |
| `printed_text` | string\|null | Texte d'oracle traduit |
| `flavor_text` | string\|null | Texte de saveur |

## Limites

- Maximum 600 résultats par requête
- Tous les résultats dans toutes les langues disponibles (sauf si `lang:` spécifié)
- Certains filtres avancés peuvent ne pas être implémentés

## Codes d'Erreur

| Code | Description |
|------|-------------|
| 400 | Paramètre `q` manquant |
| 500 | Erreur interne du serveur |

## Notes

- La recherche est insensible à la casse
- Les langues non-anglaises incluent les champs `printed_*` quand disponibles
- La syntaxe est largement compatible avec Scryfall
- Les filtres non reconnus sont traités comme recherche de nom
- Contrairement à Scryfall, cette API retourne toutes les langues par défaut plutôt que de privilégier l'anglais