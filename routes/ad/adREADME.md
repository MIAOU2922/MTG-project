# API /ad - Recherche & Chargement de Decks

Recherche de decks en base et chargement dans l'instance. Les actions
`parse`/`save`/`delete` n'existent plus. Les recherches publiques déclenchent
un re-scraping Moxfield en arrière-plan ; les chargements tentent aussi un
re-scraping individuel avant d'utiliser le deck, avec repli sur la dernière
copie en BDD si la source est indisponible. L'API ne crée ni ne modifie de
decks enregistrés : elle expose uniquement les decks importés et permet de
rafraîchir leurs données depuis Moxfield. L'action `load` ajoute les cartes à
l'instance active, sans enregistrer un deck utilisateur.

## Endpoint

```
GET /ad?q=action:param1:param2:param3...
```

Format unifié : un seul paramètre `q` avec séparateurs `:` (comme `/as?q=`).

Endpoints statiques complémentaires :

```
GET /adrefresh  # rejoue la dernière recherche authentifiée contre la BDD
GET /adl{index} # charge le deck à l'index base36 de la dernière liste (0-based)
```

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
  "bracket": 3,
  "auto_bracket": 3,
  "tags": ["cedh", "combo"],
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

**Description:** Liste uniquement les decks importés depuis Moxfield avec
filtres optionnels par **nom**, **format**, **auteur** et **commander**.
Sans filtre, retourne les decks importés les plus récemment ajoutés.

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
- Si les 4 sont absents : liste les decks importés, sans filtre.

Chaque deck retourné contient désormais : `source`, `source_id`, `source_url`,
`format`, `author`, `bracket`, `auto_bracket`, `tags`, `cards_count`,
`created_at`, `updated_at`, `is_owner`. `bracket` et `auto_bracket` sont
`null` si Moxfield ne les fournit pas ; `tags` est une liste vide si aucun tag
n'est disponible.
Limite : 200 résultats, triés par `created_at` décroissant.
La réponse conserve `decks_count` et ajoute `returned_count` (nombre de decks
dans la réponse) et `matching_total_count` (nombre total de decks correspondant
aux filtres en BDD, avant la limite de 200).

Chaque appel à `list` mémorise les filtres et l'ordre des résultats par
utilisateur authentifié. Cet état est conservé en BDD afin que les endpoints
statiques fonctionnent également après un redémarrage du serveur.

**⚡ Live refresh (re-scraping automatique) :**
Toute recherche filtrée (au moins un filtre) est aussi **forwardée au scraper
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
# Lister les decks importés depuis Moxfield
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

### 3. `/adrefresh` - Rejouer la dernière recherche

**Description:** Rejoue contre la BDD les filtres de la dernière requête `list`
de l'utilisateur et retourne les decks importés actuels, sans créer un nouveau job
de scraping. L'application peut rappeler cette route après quelques secondes
(par exemple après 30 secondes) pour récupérer les decks mis à jour par le
scraping déclenché lors de la recherche initiale.

**Syntaxe:**
```
GET /adrefresh
```

Retourne les mêmes données et compteurs que `list`, et actualise aussi l'ordre
mémorisé pour les chargements par index. Sans recherche précédente, retourne
`404 { "error": "no_previous_search" }`.

### 4. `/adl{index}` - Charger par index de la dernière liste

**Description:** Charge dans l'instance active le deck à l'index demandé dans
la dernière recherche `list` (ou `/adrefresh`) de l'utilisateur. L'index est
**base36 et 0-based** : `/adl0` charge le premier deck, `/adl1` le second et
`/adla` le onzième.

**Syntaxe:**
```
GET /adl0
GET /adla
```

Le deck est re-scrapé juste avant son chargement si sa source est Moxfield.
Si le scraping échoue ou si la source ne peut pas être rafraîchie, la dernière
copie en BDD est chargée. La réponse indique `data_freshness` (`updated`,
`unavailable` ou `not_supported`). L'index inexistant ou l'absence de liste
précédente produit une erreur `404`.

### 5. `refresh` - État du re-scraping Moxfield en cours

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
| **refresh list** | `/adrefresh` | `/adrefresh` |
| **load by index** | `/adl{index-base36}` | `/adl0`, `/adla` |
| **refresh status** | `refresh` | `GET /ad?q=refresh` |

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
    "data_freshness": "updated",
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
    "decks_count": 3,
    "returned_count": 3,
    "matching_total_count": 8,
    "search_name": null,
    "searched_at": "2026-10-10T00:00:00.000Z",
    "decks": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "name": "Red Aggro",
        "description": "Fast red deck",
        "commander": null,
        "source": "moxfield",
        "source_id": "fwq-lH-fckSltIk_I_Thrg",
        "source_url": "https://moxfield.com/decks/fwq-lH-fckSltIk_I_Thrg",
        "format": "commander",
        "author": "ExampleAuthor",
        "bracket": 3,
        "auto_bracket": 3,
        "tags": ["cedh", "combo"],
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
- **Listing** (`list`): uniquement les decks importés depuis une source,
  sans exposer les decks utilisateur sans source
- **Écriture** : aucun deck utilisateur n'est créé ou modifié par l'API ;
  une recherche filtrée peut déclencher un re-scraping Moxfield, qui met à
  jour les decks importés. `load` ajoute les cartes du deck à l'instance active.

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

## Comment la BDD est actualisée (Moxfield)

Une recherche `list` publique avec au moins un filtre est forwardée au scraper
Moxfield (`liveRefresh`), qui re-scrape les decks correspondants **en
arrière-plan**. Le chargement par UUID ou par index tente également de
re-scraper immédiatement le deck demandé. Si cette tentative échoue, le deck
déjà présent en BDD est utilisé. L'état de la file est visible via
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
- `GET /adl{index}` accepte un index base36 0-based provenant de la dernière
  liste de cet utilisateur
- `GET /adrefresh` relit cette même liste depuis la BDD, sans relancer son
  scraping
- Quand un deck est chargé, toutes ses cartes (avec répétitions) sont
  ajoutées à l'instance (`instance.addCard`)
- Chaque chargement re-scrape les decks Moxfield si possible et retombe sur
  la version en BDD si la source est indisponible
- Le deck size inclut les répétitions (2x Lightning Bolt = 2 dans le total)
- `list` sans filtre → decks importés ; avec au moins un filtre → recherche
  ciblée parmi les decks importés + forward au scraper Moxfield
- `refresh` → état de la file de re-scraping (`liveRefresh.status()`)
- Retour du `load` : cartes groupées par zone avec `count` et flag
  `is_commander` optionnel
