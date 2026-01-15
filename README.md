# 🎴 MTG Project - VRChat Magic: The Gathering

Un système complet pour jouer à Magic: The Gathering dans VRChat, construit avec UdonSharp et optimisé pour la performance en VR.

## 📋 Vue d'ensemble

Ce projet permet aux joueurs de VRChat de :
- 🔍 Rechercher des cartes MTG parmi toute la base de données Scryfall
- 🎯 Créer et gérer des decks
- 🃏 Spawn des cartes physiques interactives en 3D
- 🖼️ Visualiser les cartes avec un système d'atlas optimisé
- ⚖️ Consulter les légalités et rulings des cartes
- 🔄 Afficher les cartes double face (MDFC)

## 🏗️ Architecture

### Core Components

#### **MTG_Manager** 
Le cerveau du système qui gère :
- 🌐 Connexion à l'API backend (https://mtg.hactazia.fr)
- 💾 Cache des atlas d'images (système de batches 6×4)
- 📊 Cache des légalités et rulings (groupés par oracle_id)
- 🔄 Mise à jour périodique des données (toutes les 10 secondes)
- 🎮 Gestion des instances de jeu multi-joueurs

#### **MTG_PhysicCard**
Carte physique interactive avec :
- 🎮 Système de pickup VRChat
- 🔍 Zoom (trigger VR / clic gauche Desktop)
- 🌐 Synchronisation réseau de la position
- 🔄 Support des cartes double face avec bouton flip
- 📍 Auto-retry du chargement d'image

#### **MTG_SearchCard**
Carte de résultat de recherche :
- 🖼️ Affichage dans l'interface de recherche
- 🔄 Support des cartes double face
- 👆 Bouton pour preview/spawn

#### **MTG_DeckCard**
Carte dans l'interface de deck :
- 🔢 Affichage de la quantité
- ➕➖ Boutons pour ajouter/retirer des exemplaires
- 🔄 Support des cartes double face
- 💾 Synchronisation avec le backend

### Interfaces Utilisateur

#### **MTG_Searchinterface**
- 🔍 Barre de recherche avec syntaxe Scryfall
- 📜 Scroll view des résultats
- 🎯 Preview de carte en grand
- 🎲 Bouton "Spawn" pour créer une carte physique

#### **MTG_Deckinterface**
- 📋 Liste des decks sauvegardés
- ➕ Création/édition de decks
- 🔢 Gestion des quantités
- 💾 Sauvegarde sur le serveur

#### **MTG_SyncInterface**
- 🔄 Synchronisation des instances multi-joueurs
- 👥 Join/Create game

## 🔧 API Backend

### Endpoints Utilisés

#### `/ac` - Create Instance
Crée une nouvelle instance de jeu.
```json
Response: {"time": 1768496186853, "uid": -1645995041, "iid": 41}
```

#### `/as?q={query}` - Search Cards
Recherche de cartes avec syntaxe Scryfall.
```
Exemples:
- /as?q=Lightning Bolt
- /as?q=t:creature c:red cmc:<=3
- /as?q=name:"Tergrid" l:en
```

#### `/at0` - Atlas Info
Récupère les informations des cartes de l'instance et leur placement dans les atlas.
```json
{
  "batches": [
    {
      "batch_index": 0,
      "card_count": 24,
      "atlas_link": "/ata",
      "cards": ["card_id:0", "card_id:1", ...]
    }
  ]
}
```

#### `/at3` - Legalities & Rulings
Récupère les légalités et rulings groupés par oracle_id.
```json
{
  "cards": [
    {
      "oracle_id": "8485cfaa-...",
      "ids": ["14dc88ee-...", "529451ef-..."],
      "legalities": {"commander": "legal", ...},
      "rulings": [{"comment": "...", "published_at": "..."}]
    }
  ]
}
```

#### `/ata`, `/atb`, ... - Atlas Images
Images PNG contenant 24 cartes (grille 6×4, max 2048px).

#### `/ad?q=` - Deck Management
Gestion complète des decks (save, load, delete, list).

## 🎨 Système d'Atlas

### Format des Cartes
- Format par défaut : `card_id:face_index`
  - `abc123:0` = face avant
  - `abc123:1` = face arrière (si double face)

### Structure des Atlas
- **Grille** : 6 colonnes × 4 lignes = 24 cartes max
- **Résolution** : 2048px max (redimensionné automatiquement)
- **Format** : PNG avec cache côté serveur (48h)
- **UV Mapping** : Calculé automatiquement en grille uniforme

### Cache Local
```
Stockage: atlasImages[atlasIndex]
Métadonnées: atlasCardIds[atlas][slot] -> "card_id:face"
UV Rects: atlasCardRects[atlas][slot] -> Rect
```

## 🔄 Cartes Double Face

### Détection Automatique
Le système détecte automatiquement si une carte a deux faces en cherchant :
- `card_id:0` (face avant)
- `card_id:1` (face arrière)

### Fonctionnalités
- ✅ Chargement automatique des deux faces
- ✅ Bouton flip visible seulement pour les cartes MDFC
- ✅ Méthode `FlipCard()` pour basculer
- ✅ Gestion de visibilité (une seule face visible à la fois)

### Configuration Unity
Pour chaque prefab de carte :
```
- Card Image (RawImage) -> Face avant
- Card Image Back (RawImage) -> Face arrière
- Flip Button (GameObject) -> Appelle FlipCard()
```

## 📊 Légalités et Rulings

### Cache Manager
```csharp
// Charger les données (appelé automatiquement toutes les 10s)
manager.LoadLegalitiesAndRulings();

// Vérifier la légalité
bool isLegal = manager.IsCardLegalInFormat(oracleId, "commander", out string status);

// Récupérer les légalités
DataDictionary legalities;
if (manager.GetLegalitiesForOracle(oracleId, out legalities)) {
    // Accéder aux formats
}

// Récupérer les rulings
DataList rulings;
if (manager.GetRulingsForOracle(oracleId, out rulings)) {
    // Parcourir les rulings
}
```

### Formats Supportés
`standard`, `pioneer`, `modern`, `legacy`, `vintage`, `commander`, `brawl`, `historic`, `timeless`, `pauper`, `duel`, `gladiator`, `oathbreaker`, etc.

## 🚀 Installation

### Prérequis
- Unity 2022.3+ (version VRChat)
- VRChat SDK3 (Worlds)
- UdonSharp 1.1+
- TextMeshPro

### Setup
1. Importer le VRChat SDK3
2. Importer UdonSharp
3. Copier le dossier `Assets/MTG/` dans votre projet
4. Configurer le MTG_Manager :
   - BaseURL : `https://mtg.hactazia.fr/a`
   - Créer 64 joinURLs (base36: j0-j1z)
   - Créer 4096 tempURLs (base36: t0-t2lr)

### Configuration Scene
1. Créer un GameObject "MTG_Manager"
2. Ajouter le script `MTG_Manager.cs` (UdonBehaviour)
3. Assigner `MTG_SyncInterface` pour la synchronisation
4. Placer les prefabs d'interface dans la scène

## 🎮 Utilisation

### Recherche de Cartes
1. Ouvrir l'interface de recherche
2. Taper une requête (syntaxe Scryfall)
3. Cliquer sur une carte pour preview
4. Utiliser "Spawn" pour créer une carte physique

### Création de Deck
1. Ouvrir l'interface de deck
2. Créer un nouveau deck
3. Ajouter des cartes avec +/-
4. Sauvegarder (sync automatique)

### Interaction avec les Cartes
- **VR** : Trigger pour zoom
- **Desktop** : Clic gauche/E pour zoom
- **Flip** : Bouton sur les cartes double face
- **Pickup** : Grab pour déplacer (sync réseau)

## 🔧 Configuration Avancée

### Intervalles de Mise à Jour
```csharp
ATLAS_INFO_UPDATE_INTERVAL = 10f; // Secondes entre les updates
RETRY_INTERVAL = 10f; // Secondes entre les retry d'images
```

### Limites
- Maximum 4096 atlas (base36: 0-2lr)
- 24 cartes par atlas
- Résolution max 2048px par atlas

## 📝 Logs et Debug

Tous les logs utilisent des préfixes colorés :
- 🩷 `[MTG_manager]` - Manager principal
- 🩷 `[MTG_PhysicCard]` - Cartes physiques
- 🩷 `[MTG_SearchCard]` - Cartes de recherche
- 🩷 `[MTG_DeckCard]` - Cartes de deck

## 🐛 Troubleshooting

### Les images ne chargent pas
- Vérifier que `MTG_Manager` est dans la scène
- Attendre 10 secondes (retry automatique)
- Vérifier les logs pour les erreurs d'URL

### Les cartes ne se synchronisent pas
- Vérifier que le joueur est Owner
- Utiliser l'interface de sync pour join/create
- Vérifier `instanceID != -1`

### Le bouton flip ne s'affiche pas
- Vérifier que la carte a réellement deux faces
- Assigner `cardImageBack` et `flipButton` dans l'Inspector
- Vérifier les logs pour la détection de `card_id:1`

## 🤝 Contribution

### Structure du Code
- Tous les scripts dans `Assets/MTG/`
- Documentation API dans `Assets/MTG/docs/`
- Préfabs dans le dossier approprié

### Guidelines
- Utiliser UdonSharp (pas C# standard)
- Commenter les méthodes publiques
- Logger avec le préfixe coloré approprié
- Tester en VR ET Desktop

## 📚 Documentation API

- [asREADME.md](Assets/MTG/docs/asREADME.md) - Recherche de cartes
- [atREADME.md](Assets/MTG/docs/atREADME.md) - Système d'atlas
- [adREADME.md](Assets/MTG/docs/adREADME.md) - Gestion de decks

## 📄 Licence

Ce projet utilise :
- **Scryfall API** pour les données de cartes
- **VRChat SDK** sous licence VRChat
- **UdonSharp** sous licence MIT

## 🙏 Remerciements

- Scryfall pour l'API de cartes MTG
- VRChat pour le SDK et UdonSharp
- La communauté MTG VRChat

---

**Version:** 1.0.0  
**Auteur:** Hactazia  
**API Backend:** https://mtg.hactazia.fr
