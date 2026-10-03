# API /ad - Recherche & Chargement de Decks

Recherche de decks en base et chargement dans l'instance. La route **n'écrit
plus directement dans la BDD** (actions `parse`/`save`/`delete` supprimées) :
la seule écriture est indirecte — les recherches `list` sont forwardées au
scraper Moxfield, qui met la BDD à jour en arrière-plan.

## Endpoint

```
GET /ad?q=action:param1:param2:param3...
```

Format unifié : un seul paramètre `q` avec séparateurs `:` (comme `/as?q=`).

## Authentification

L'utilisateur est résolu via le **hash SHA-256 de l'IP** (compte le plus
récemment actif). Non enregistré → `401 { error: 'unknown_user' }`
(faire `GET /aur` d'abord).

---

## Actions Disponibles

### 1. `load` - Charger un deck sauvegardé dans l'instance

**Description:** Charge un deck de la base de données (par UUID) et ajoute
toutes ses cartes à l'instance active de l'utilisateur. Requiert que
l'utilisateur soit dans une instance (sinon `400`).

**Syntaxe:**
```
/ad?q=load:deck_id
```

**Paramètres:**
- `load` - Action
- `deck_id` - UUID du deck (format: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`, requis)

**Permissions:**
- N'importe qui peut charger n'importe quel deck public sauvegardé

**Réponse:**
Retourne les cartes groupées par zone avec count et flag `is_commander` si
applicable.

```json
{
  "deck_type": "saved",
  "cards_by_zone": {
    "main": [
      { "count": 1, "name": "Lightning Bolt", "card_id": "..." }
    ],
    "sideboard": [],
    "commander": [
      { "count": 1, "name": "Winota, Joiner of Forces", "card_id": "...", "is_commander": true }
    ],
    "companion": [],
    "oathbreaker": [],
    "wishboard": []
  }
}
```

**Exemple:**
```
# Charger par UUID
GET /ad?q=load:550e8400-e29b-41d4-a716-446655440000
```

### 2. `list` - Lister les decks / rechercher les decks importés

**Description:** Liste les decks de l'utilisateur ou cherche dans les decks
publics importés (Moxfield) avec filtres par **nom**, **format**, **auteur**
et **commander**.

**Syntaxe:**
```
/ad?q=list
/ad?q=list:<search_name>
/ad?q=list::<format>
/ad?q=list:::<author>
/ad?q=list::::<commander>
/ad?q=list:<search_name>:<format>:<author>:<commander>
```

**Paramètres (tous optionnels, dans cet ordre):**
- `search_name` - recherche partielle sur le nom (insensible à la casse)
- `format` - format exact (ex: `commander`, `modern`, `standard`)
- `author` - pseudo Moxfield partiel (insensible à la casse)
- `commander` - nom du commander partiel (insensible à la casse)
- Si les 4 sont absents : liste les decks de l'utilisateur.

Chaque deck retourné contient désormais : `source`, `source_id`, `source_url`,
`format`, `author`, `cards_count`, `created_at`, `updated_at`, `is_owner`.
Limite : 200 résultats, triés par `created_at` décroissant.

**⚡ Live refresh (re-scraping automatique) :**
Toute recherche publique (au moins un filtre) est aussi **forwardée au scraper
Moxfield**, qui re-scrape en arrière-plan les decks correspondants pour mettre
la BDD à jour (les decks existants sont rafraîchis, les nouveaux sont importés).
- `commander` → re-scrape tous les decks avec ce commander
- `author` → re-scrape les decks publics de l'auteur
- `name` → re-scrape les decks Moxfield dont le nom correspond
- `format` → re-scrape le top du format (par vues)

La réponse contient un champ `refresh` : `job_id`, `type`, `query`, `status`
(`queued` = planifié, `running` = déjà en cours, `disabled` si désactivé).
Le crawl tourne en arrière-plan (un seul job à la fois) et ne bloque pas la
réponse. Les limites sont réglables : `MOXFIELD_LIVE_MAX_DECKS` (déf. 300),
`MOXFIELD_LIVE_MAX_PER_PARTITION` (déf. 200), `MOXFIELD_LIVE_ENABLED=0` pour
couper.

**Exemples:**
```
# Lister mes decks
GET /ad?q=list

# Chercher les decks publics contenant "Lightning"
GET /ad?q=list:Lightning

# Tous les decks commander importés
GET /ad?q=list::commander

# Les decks de l'auteur CoreyBMTG
GET /ad?q=list:::CoreyBMTG

# Decks commander de nom contenant "winota"
GET /ad?q=list:winota:commander:

# Decks ayant Winota en commander (re-scrape Moxfield en arrière-plan)
GET /ad?q=list::::Winota
```

### 3. `refresh` - État du re-scraping Moxfield en cours

**Description:** Renvoie l'état de la file de re-scraping déclenchée par les
recherches `list` : job en cours, file d'attente, 20 derniers jobs terminés
(avec stats `imported`/`updated`/`skipped`/`failed`).

**Syntaxe:**
```
GET /ad?q=refresh
```

**Exemple de réponse:**
```json
{
  "action": "refresh",
  "live_refresh": {
    "enabled": true,
    "running": { "id": 3, "type": "commander", "query": "Winota", "status": "running", ... },
    "queued": [],
    "history": [ ... ]
  }
}
```

---

## Tableau Récapitulatif

| Action | Syntaxe | Exemple |
|--------|---------|---------|
| **load** | `load:uuid` | `load:550e8400-e29b-41d4-a716-446655440000` |
| **list** | `list:name:format:author:commander` | `list` ou `list:Lightning` |
| **refresh** | `refresh` | `refresh` |

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
      "main": [ { "count": 4, "name": "Lightning Bolt", "card_id": "..." } ],
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
| 400 | Paramètres manquants, UUID invalide, ou utilisateur sans instance |
| 401 | Utilisateur non enregistré (`/aur` d'abord) |
| 404 | Deck inexistant |
| 500 | Erreur serveur |

---

## Permissions

- **Chargement** (`load`): N'importe quel utilisateur peut charger n'importe
  quel deck public sauvegardé
- **Listing** (`list`): l'utilisateur voit ses decks et peut chercher les
  decks publics importés
- **Écriture** : aucune — la route ne modifie plus la BDD ; seule la
  recherche `list` déclenche un re-scraping Moxfield en arrière-plan

---

## Encodage des Paramètres

Les filtres textuels doivent être encodés en URL :

**JavaScript:**
```javascript
const search = "Winota";
const encoded = encodeURIComponent(search); // "Winota"
const url = `/ad?q=list::::${encoded}`;
```

**Bash avec jq:**
```bash
ENCODED=$(echo -n "Winota" | jq -sRr @uri)
curl "http://localhost:3000/ad?q=list::::$ENCODED"
```

---

## Comment la BDD est alimentée (Moxfield)

La route `/ad` n'écrit plus directement en base. Pour importer/mettre à jour
des decks Moxfield, il suffit de faire une recherche `list` avec au moins un
filtre : la recherche est forwardée au scraper Moxfield (`liveRefresh`), qui
re-scrape les decks correspondants **en arrière-plan** (decks existants
rafraîchis, nouveaux importés). L'état de la file est visible via
`GET /ad?q=refresh`.

Avec le parser C# (helper de génération de queries) :

```csharp
// Recherche qui déclenche le re-scraping Moxfield en arrière-plan
string query = DeckListParser.ListDecks("", "commander", "", "Winota");
// Résultat: "list::commander::Winota"
string fullUrl = "https://mtg.hactazia.fr/ad?q=" + query;
```

---

## Notes d'Implémentation

- `load` accepte uniquement un UUID de deck sauvegardé
- Quand un deck est chargé, toutes ses cartes (avec répétitions) sont
  ajoutées à l'instance (`instance.addCard`)
- Le deck size inclut les répétitions (2x Lightning Bolt = 2 dans le total)
- `list` sans filtre → decks de l'utilisateur ; avec au moins un filtre →
  recherche publique (decks importés) + forward au scraper Moxfield
- `refresh` → état de la file de re-scraping (`liveRefresh.status()`)
- Retour du `load` : cartes groupées par zone avec `count` et flag
  `is_commander` optionnel
