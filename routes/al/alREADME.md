# Route `/al` - Langue de l'Utilisateur

La route `/al` permet de lire et de définir la langue préférée d'un utilisateur.
La langue est liée au **user** (pas à l'instance) et sert notamment à `/at1`,
qui renvoie les textes de jeu des cartes dans cette langue.

## 📋 Vue d'ensemble

**Authentification :** Résolution via le hash SHA-256 de l'IP (compte le plus
récemment actif). Non enregistré → `401 { error: 'unknown_user' }` (faire
`GET /aur` d'abord). L'`uid` est la clé 12 hex.

## 🗺️ Endpoints

### `GET /al` — langue actuelle

Retourne la langue actuelle de l'utilisateur et la liste des langues supportées.

```json
{
  "link_type": "l",
  "link_id": "en",
  "iid": null,
  "uid": "330ea1cc96e9",
  "time": 1728499200000,
  "data": {
    "lang": "en",
    "supported": ["en", "fr", "it", "es", "de", "pt", "ja", "ko", "ru", "zhs", "zht", "he", "la", "grc", "ar", "sa", "px"]
  }
}
```

### `GET /al{code}` — définir la langue

Définit la langue préférée de l'utilisateur. Udon ne peut pas construire
d'URLs dynamiques → le client utilise des **liens statiques** : `/alen`,
`/alfr`, `/alit`, `/ales`, `/alde`, `/alpt`, …

```json
{
  "link_type": "l",
  "link_id": "fr",
  "iid": null,
  "uid": "330ea1cc96e9",
  "time": 1728499200000,
  "data": {
    "lang": "fr"
  }
}
```

**Paramètres :**
- `code` : code de langue Scryfall (`en`, `fr`, `it`, …), insensible à la casse

## 🗣️ Langues supportées

`en` (anglais), `fr`, `it`, `es`, `de`, `pt`, `ja`, `ko`, `ru`, `zhs`
(chinois simplifié), `zht` (chinois traditionnel), `he`, `la`, `grc`, `ar`,
`sa`, `px`.

## 🚨 Gestion d'erreurs

### 400 Bad Request
```json
{
  "error": "unsupported_language",
  "supported": ["en", "fr", "it", "es", "de", "pt", "..."]
}
```

### 401 Unauthorized
```json
{
  "error": "unknown_user",
  "hint": "Register via /aur first"
}
```

## 🔗 Liens connexes

- **`/at1`** : cartes de l'instance avec les textes de jeu dans la langue du user
- **`/au`** : identité utilisateur (enregistrement `/aur`, login `/aul…`)
