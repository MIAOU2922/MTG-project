# Route `/at` - Système de Liens Statiques

La route `/at` fournit un système de liens statiques encodés en base36 qui retournent du contenu dynamique basé sur l'instance de l'utilisateur connecté.

## 📋 Vue d'ensemble

**Endpoint :** `GET /at{linkId}`

**Paramètres :**
- `linkId` : ID du lien en base36 (caractères alphanumériques minuscules)

**Authentification :** Basée sur l'UID utilisateur (cookie/session)

## 🎯 Fonctionnement

### 1. **Validation de l'ID**
- L'ID doit être composé uniquement de caractères base36 (`[0-9a-z]`)
- Conversion de l'ID base36 vers un index numérique

### 2. **Gestion de l'utilisateur**
- Récupération/création automatique de l'utilisateur via UID
- Mise à jour automatique du `last_seen_at`

### 3. **Gestion de l'instance**
- Recherche de l'instance active de l'utilisateur
- Retourne une erreur 404 si aucune instance n'est trouvée
- Nécessite une création préalable via `/ac` ou un join via `/aj`

## 📊 Types de réponses

### **Liens 0-9 (base36) : JSON**
Retourne toujours des données JSON avec les informations de l'instance.

**Exemple :** `/at0`, `/at9`

#### **at0 : Cartes de l'instance (par défaut)**
```json
{
  "time": 1728499200000,
  "uid": 12345,
  "link_id": "0",
  "link_index": 0,
  "instance_id": 42,
  "player_id": "42-12345",
  "type": "json",
  "data": {
    "link_type": "special",
    "instance_id": 42,
    "player_id": "42-12345",
    "cards_loaded_count": 150,
    "batches": [
      {
        "batch_index": 0,
        "card_count": 24,
        "atlas_link": "/ata",
        "cards": [
          "abc123:0",
          "abc123:1",
          "def456:0",
          ...
        ]
      }
    ]
  }
}
```

**✨ Nouveautés:**
- Chaque batch contient maintenant un tableau `cards` avec la liste complète des IDs
- Format: `{card_id}:{face_index}` pour supporter les cartes double face
- Les coordonnées UV ont été supprimées (calculées côté client)
- `atlas_width` et `atlas_height` supprimés (toujours 2048px max)

#### **at1 : Liste des decks de l'utilisateur** ✨ NEW
Retourne uniquement l'ID et le nom des decks créés par l'utilisateur.

**Endpoint:** `GET /at1`

```json
{
  "time": 1728499200000,
  "uid": 12345,
  "link_id": "1",
  "link_index": 1,
  "instance_id": 42,
  "player_id": "42-12345",
  "type": "json",
  "data": {
    "type": "deck_list",
    "user_id": 12345,
    "total_decks": 3,
    "decks": [
      {
        "id": "deck-uuid-1",
        "name": "Red Aggro"
      },
      {
        "id": "deck-uuid-2",
        "name": "Control Blue"
      },
      {
        "id": "deck-uuid-3",
        "name": "Beatdown"
      }
    ]
  }
}
```

**Utilisation:**
- Afficher la liste des decks de l'utilisateur
- Chaque deck contient un `id` et un `name`
- Pas besoin d'instance active (ne requiert pas `/ac` ou `/aj` au préalable)

#### **at2 : Liste des sets MTG** ✨ NEW
Retourne la liste complète des sets MTG disponibles dans la base de données.

**Endpoint:** `GET /at2`

```json
{
  "time": 1728499200000,
  "uid": 12345,
  "link_id": "2",
  "link_index": 2,
  "instance_id": 42,
  "player_id": "42-12345",
  "type": "json",
  "data": {
    "type": "sets_list",
    "count": 542,
    "data": [
      "Kamigawa: Neon Dynasty (neo)",
      "Streets of New Capenna (snc)",
      "Dominaria United (dmu)",
      "The Brothers' War (bro)",
      "Phyrexia: All Will Be One (one)"
    ]
  }
}
```

**Structure des données:**
- **type**: `sets_list` - Identifiant du type de données
- **count**: Nombre total de sets disponibles
- **data**: Liste des sets avec format `"Name (CODE)"`
  - Triés par nom alphabétiquement
  - Format standard : `"Set Name (set_code)"`

**Utilisation:**
- Afficher tous les sets disponibles dans la base de données
- Interface de sélection de sets pour recherche avancée
- Dropdown/menu de sélection dans l'application client

#### **at3 : Cartes de l'instance avec légalités et rulings** ✨ NEW
Retourne la liste des cartes de l'instance avec leurs légalités et rulings, groupées par oracle_id.

**Endpoint:** `GET /at3`

```json
{
  "time": 1728499200000,
  "uid": 12345,
  "link_id": "3",
  "link_index": 3,
  "instance_id": 42,
  "player_id": "42-12345",
  "type": "json",
  "data": {
    "type": "instance_cards",
    "instance_id": 42,
    "total_cards": 6,
    "cards": [
      {
        "oracle_id": "8485cfaa-1dbf-432b-b5d0-92a6aa6a329b",
        "ids": [
          "14dc88ee-bba9-4625-af0d-89f3762a0ead",
          "529451ef-2c7e-4566-8627-a1f25e010829",
          "66904768-09d0-4836-a6a0-c474678f19c1",
          "9ff4efdf-c0bb-443a-a100-2f3850571e91"
        ],
        "legalities": {
          "standard": "not_legal",
          "pioneer": "legal",
          "modern": "legal",
          "legacy": "legal",
          "vintage": "legal",
          "commander": "legal",
          "brawl": "legal",
          "historic": "legal",
          "timeless": "legal"
        },
        "rulings": [
          {
            "id": "8485cfaa-1dbf-432b-b5d0-92a6aa6a329b_2021-02-05_02a90bc2",
            "published_at": "2021-02-05T00:00:00.000Z",
            "comment": "In the Commander variant, a double-faced card's color identity..."
          }
        ]
      }
    ]
  }
}
```

**Structure des données:**
- **type**: `instance_cards` - Identifiant du type de données
- **instance_id**: ID de l'instance
- **total_cards**: Nombre total de cartes groupées par oracle
- **cards**: Tableau de cartes groupées par oracle_id
  - **oracle_id**: Identifiant unique de la carte Oracle (partagé entre impressions)
  - **ids**: Liste des UUIDs de toutes les impressions de cette carte dans l'instance
  - **legalities**: Objet des formats légaux (standard, commander, etc.)
  - **rulings**: Tableau des rulings officiels pour cette carte

**Particularités:**
- Les cartes double face (MDFC) sont groupées ensemble (même oracle_id)
- Les différentes impressions d'une même carte sont listées sous `ids`
- Évite la duplication des légalités et rulings identiques
- Optimisé pour réduire la taille de la réponse JSON

**Utilisation:**
- Vérifier la légalité des cartes de l'instance pour un format donné
- Afficher les rulings officiels pour chaque carte unique
- Interface de gestion de deck avec validation de format
- Liste dédupliquée des cartes présentes dans l'instance

### **Liens 10+ (base36) : Atlas d'images**
Génère et retourne un atlas PNG contenant 24 cartes maximum.

**Exemple :** `/ata`, `/at1f` (10, 31 en décimal)

- **Format :** PNG
- **Dimensions :** Maximum 2048px (redimensionné automatiquement)
- **Layout :** 6 colonnes × 4 lignes = 24 cartes maximum
- **Téléchargement automatique :** Les images manquantes sont téléchargées en arrière-plan
- **Cache intelligent ✨ NEW :** 
  - Durée: 48 heures
  - Structure: `images/atlas/instance_{id}/batch_{index}_n{count}.png`
  - Un sous-dossier par instance pour une meilleure organisation
  - Invalidation automatique si le nombre de cartes change
  - Header `X-Cache: HIT|MISS` pour monitoring
  - Suppression automatique du dossier lors de la destruction de l'instance

## 🃏 Structure des données JSON

### **Informations générales**
- `link_type` : "special" (liens 0-9) ou "normal" (liens 10+)
- `instance_id` : ID de l'instance active
- `player_id` : "instance_id-user_id"
- `cards_loaded_count` : Nombre total de cartes dans l'instance

### **Batches de cartes**
Chaque batch représente un atlas de 24 cartes :

```json
{
  "batch_index": 0,
  "card_count": 24,
  "atlas_link": "/ata",
  "atlas_width": 1464,
  "atlas_height": 1360,
  "cards": [
    {
      "id": "card_uuid",
      "x": 0.0,
      "y": 0.0,
      "width": 0.1667,
      "height": 0.25
    }
  ]
}
```

### **Coordonnées normalisées**
- `x`, `y` : Position relative (0.0 à 1.0)
- `width`, `height` : Dimensions relatives (0.0 à 1.0)
- Calculées pour faciliter le découpage côté client

## 🎨 Génération d'atlas

### **Processus**
1. **Sélection du batch** : `linkIndex - 10` détermine le lot de 24 cartes
2. **Vérification des images** : Téléchargement automatique si manquant
3. **Composition** : Assemblage des cartes sur un canvas 6×4
4. **Redimensionnement** : Ajustement pour respecter la limite 2048px
5. **Export PNG** : Retour de l'image optimisée

### **Dimensions**
- **Carte individuelle :** 488×680px (standard MTG)
- **Atlas original :** 2928×2720px (6×4 cartes)
- **Atlas final :** Redimensionné pour max 2048px

## 🔄 Téléchargement automatique

### **Déclenchement**
- **Immédiat :** Lors de l'ajout de cartes via `/as`
- **Au démarrage :** Téléchargement des images des instances récentes
- **Différé :** 30 secondes après ajout de nouvelles cartes
- **Planifié :** Nettoyage horaire des images inutilisées (> 24h)

### **Optimisations**
- **File d'attente :** Maximum 3 téléchargements simultanés
- **Cache local :** Images stockées dans `images/cards/{cardId}.jpg`
- **Gestion d'erreurs :** Continuation même en cas d'échec individuel

## 🚨 Gestion d'erreurs

### **400 Bad Request**
```json
{
  "error": "Invalid link ID format. Must be base36 characters."
}
```

### **404 Not Found**
```json
{
  "error": "No instance available. Please create or join an instance first."
}
```

### **500 Internal Server Error**
```json
{
  "error": "Failed to generate atlas image"
}
```

## 📈 Métriques et monitoring

### **Logs automatiques**
```
🔄 Triggered download of recent instance images (background)
✅ Downloaded image for card abc123
🧹 Running periodic cleanup of old instance images (> 24h)
📊 Summary: 15 downloaded, 5 skipped, 0 errors
```

### **Performance**
- **Téléchargement :** Gestion optimisée des requêtes HTTP
- **Cache :** Images locales pour éviter les re-téléchargements
- **Nettoyage :** Suppression automatique des images obsolètes

## 🔗 Liens connexes

- **`/ac`** : Création d'instance
- **`/aj`** : Rejoindre une instance
- **`/as`** : Recherche et ajout de cartes
- **`/au`** : Upload d'images utilisateur

## 🎯 Cas d'usage

1. **Liens statiques** : Partage de contenu sans expiration
2. **Atlas optimisés** : Chargement groupé des images de cartes
3. **Cache intelligent** : Téléchargement automatique et nettoyage
4. **Performance** : Redimensionnement automatique pour le web

---

*Ce système permet de créer des milliers de liens statiques (4096+ en base36) qui retournent du contenu dynamique basé sur l'instance active de chaque utilisateur.*