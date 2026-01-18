# Documentation des Routes API

Cette documentation présente toutes les routes disponibles de l'API avec leur structure JSON standardisée.

## 📋 Structure JSON Standardisée

Toutes les routes suivent une structure JSON cohérente pour faciliter l'intégration client :

```json
{
  "link_type": "<type>",   // Type de route (c, j, s, d, t, u)
  "link_id": "<id>",       // ID ou paramètre spécifique
  "iid": <number|null>,    // Instance ID (null si non applicable)
  "uid": <number>,         // User ID
  "time": <timestamp>,     // Timestamp Unix en millisecondes
  "data": { ... }          // Données spécifiques (optionnel)
}
```

### **Champs standardisés**

| Champ | Type | Description | Présent |
|-------|------|-------------|---------|
| `link_type` | string | Type de route (voir tableau ci-dessous) | Toujours |
| `link_id` | string | ID du lien ou paramètre de requête | Toujours |
| `iid` | number\|null | ID de l'instance active (0-63 ou null) | Toujours |
| `uid` | number | ID de l'utilisateur | Toujours |
| `time` | number | Timestamp Unix en millisecondes | Toujours |
| `data` | object | Données spécifiques à la route | Selon route |

## 🗺️ Liste des Routes

### Routes Principales

| Route | Type | Description | Contient `data` |
|-------|------|-------------|-----------------|
| [/ac](ac/acREADME.md) | `c` (create) | Créer une nouvelle instance | Non |
| [/aj](aj/ajREADME.md) | `j` (join) | Rejoindre une instance existante | Non |
| [/as](as/asREADME.md) | `s` (search) | Rechercher des cartes MTG | Oui |
| [/ad](ad/adREADME.md) | `d` (deck) | Gestion des decks | Oui |
| [/at](at/atREADME.md) | `t` (texture) | Liens statiques & atlas | Oui |
| [/au](au/auREADME.md) | `u` (user) | Informations utilisateur | Oui |

## 📊 Détails par Route

### `/ac` - Créer une Instance

**Type:** `c` (create)

```json
{
  "link_type": "c",
  "link_id": "",
  "iid": 42,
  "uid": 12345,
  "time": 1728499200000
}
```

**Usage:** Créer une nouvelle instance avec rotation automatique.

---

### `/aj/{instanceCode}` - Rejoindre une Instance

**Type:** `j` (join)

```json
{
  "link_type": "j",
  "link_id": "2a",
  "iid": 42,
  "uid": 12345,
  "time": 1728499200000
}
```

**Paramètres:**
- `instanceCode`: ID d'instance en base36 (ex: `2a` = 42)

**Usage:** Rejoindre une instance existante par son code.

---

### `/as?q={query}` - Rechercher des Cartes

**Type:** `s` (search)

```json
{
  "link_type": "s",
  "link_id": "lightning bolt",
  "iid": 42,
  "uid": 12345,
  "time": 1728499200000,
  "data": {
    "query": "lightning bolt",
    "count": 15,
    "results": [...]
  }
}
```

**Paramètres:**
- `q`: Requête de recherche (syntaxe Scryfall)

**Usage:** Rechercher des cartes et les ajouter automatiquement à l'instance active.

---

### `/ad?q={action:params}` - Gestion des Decks

**Type:** `d` (deck)

```json
{
  "link_type": "d",
  "link_id": "parse",
  "iid": null,
  "uid": 12345,
  "time": 1728499200000,
  "data": {
    "action": "parse",
    "deck_count": 60,
    "cards": [...]
  }
}
```

**Actions disponibles:**
- `parse`: Parser un deck
- `save`: Sauvegarder un deck
- `load`: Charger un deck dans l'instance
- `delete`: Supprimer un deck
- `list`: Lister les decks

**Usage:** Gestion complète des decks (parsing, sauvegarde, chargement).

---

### `/at{linkId}` - Liens Statiques & Atlas

**Type:** `t` (texture/atlas)

**JSON (at0-at9):**
```json
{
  "link_type": "t",
  "link_id": "0",
  "iid": 42,
  "uid": 12345,
  "time": 1728499200000,
  "data": {
    "cards_loaded_count": 150,
    "batches": [...]
  }
}
```

**PNG (at10+):**
Retourne une image PNG d'atlas (6×4 cartes, max 2048px).

**Liens spéciaux:**
- `at0`: Liste des cartes de l'instance avec batches
- `at1`: Liste des decks de l'utilisateur
- `at2`: Liste des sets MTG
- `at3`: Cartes avec légalités et rulings

**Usage:** Système de liens statiques pour accéder au contenu dynamique.

---

### `/au` - Informations Utilisateur

**Type:** `u` (user)

```json
{
  "link_type": "u",
  "link_id": "",
  "iid": null,
  "uid": 12345,
  "time": 1728499200000,
  "data": {
    "last_seen_at": 1728499200000
  }
}
```

**Usage:** Récupérer les informations utilisateur (création automatique si inexistant).

---

## 🔄 Workflow Typique

### 1. Démarrer une Session

```
GET /ac → { "iid": 42, ... }
```

### 2. Rechercher des Cartes

```
GET /as?q=lightning → { "data": { "count": 15, ... } }
```

### 3. Accéder au Contenu

```
GET /at0 → { "data": { "batches": [...] } }
GET /ata → [PNG atlas image]
```

### 4. Partager avec d'Autres

```
GET /aj/2a → { "iid": 42, ... }
```

## 🎯 Avantages de la Structure Standardisée

### **Cohérence**
- Même structure pour toutes les routes
- Prévisibilité du format de réponse
- Facilite le parsing côté client

### **Traçabilité**
- `time`: Timestamp de chaque réponse
- `uid`: Identification de l'utilisateur
- `iid`: Contexte de l'instance

### **Flexibilité**
- `link_type`: Identifie rapidement le type de route
- `link_id`: Conserve les paramètres originaux
- `data`: Contenu variable selon les besoins

### **Simplicité**
- Routes sans données complexes (`ac`, `aj`) n'ont pas de champ `data`
- Structure plate pour les métadonnées
- Imbrication uniquement pour les données métier

## 📝 Notes de Migration

Si vous utilisez l'ancienne structure JSON, voici les changements :

### Ancien Format
```json
{
  "time": 1728499200000,
  "uid": 12345,
  "instance_id": 42,
  "results": [...]
}
```

### Nouveau Format
```json
{
  "link_type": "s",
  "link_id": "query",
  "iid": 42,
  "uid": 12345,
  "time": 1728499200000,
  "data": {
    "results": [...]
  }
}
```

### Changements Clés
- `instance_id` → `iid`
- Ajout de `link_type` et `link_id`
- Données métier dans `data` (sauf routes simples)
- Ordre cohérent des champs

---

## 📚 Documentation Complète

Consultez les README individuels pour chaque route :

- [/ac - Création d'Instance](ac/acREADME.md)
- [/aj - Rejoindre une Instance](aj/ajREADME.md)
- [/as - Recherche de Cartes](as/asREADME.md)
- [/ad - Gestion des Decks](ad/adREADME.md)
- [/at - Liens Statiques & Atlas](at/atREADME.md)
- [/au - Informations Utilisateur](au/auREADME.md)

---

*Documentation mise à jour : Janvier 2026*
