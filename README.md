# 🎴 MTG Project - Unity/VRChat Implementation

Un système complet pour jouer à Magic: The Gathering dans VRChat, construit avec UdonSharp et optimisé pour la performance en VR.

## 📋 Vue d'ensemble

Ce projet Unity permet aux joueurs de VRChat de :
- 🔍 Rechercher des cartes MTG parmi toute la base de données Scryfall (175,000+ cartes)
- 🎯 Créer et gérer des decks personnalisés
- 🃏 Spawn des cartes physiques interactives en 3D
- 🖼️ Visualiser les cartes avec un système d'atlas optimisé
- ⚖️ Consulter les légalités et rulings des cartes
- 🔄 Afficher les cartes double face (MDFC)
- 👥 Jouer en multijoueur avec synchronisation réseau

## 🏗️ Structure du Projet Unity

```
Assets/
├── MTG/
│   ├── script/                      # Scripts UdonSharp principaux
│   │   ├── MTG_Manager.cs          # Manager principal du système
│   │   ├── MTG_Manager.asset       # Asset UdonSharp compilé
│   │   ├── MTG_Base.cs             # Classe de base avec utilitaires
│   │   ├── MTG_PhysicCard.cs       # Cartes physiques VR
│   │   ├── MTG_SearchCard.cs       # Cartes dans UI de recherche
│   │   ├── MTG_DeckCard.cs         # Cartes dans UI de deck
│   │   ├──  MTG_SearchInterface.cs  # Interface de recherche
│   │   ├──  MTG_DeckInterface.cs    # Interface de deck
│   │   └── MTG_SyncInterface.cs    # Synchronisation multijoueur
│   ├── bord script/                 # Scripts de gestion du plateau
│   │   ├── MTG_CardInstance.cs     # Instances de cartes sur plateau
│   │   └── MTG_Slot.cs             # Emplacements organisés
│   ├── docs/                        # Documentation API backend
│   │   ├── README.md
│   │   ├── ac/                      # Create instance endpoint
│   │   ├── aj/                      # Join instance endpoint
│   │   ├── as/                      # Search cards endpoint
│   │   ├── ad/                      # Deck management endpoint
│   │   ├── at/                      # Atlas system endpoint
│   │   └── au/                      # User info endpoint
│   └── old/                         # Implémentations legacy
└── Scenes/
    └── MTG dev.unity                # Scène principale de développement
```

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

#### ** MTG_SearchInterface**
- 🔍 Barre de recherche avec syntaxe Scryfall
- 📜 Scroll view des résultats
- 🎯 Preview de carte en grand
- 🎲 Bouton "Spawn" pour créer une carte physique

#### ** MTG_DeckInterface**
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

## 🚀 Installation et Configuration Unity

### Prérequis
- **Unity 2022.3 LTS** (version compatible VRChat)
- **VRChat SDK3 (Worlds)** - Dernière version
- **UdonSharp 1.1+** - Pour la programmation des comportements
- **TextMeshPro** - Pour l'interface utilisateur
- **Cinemachine** - Inclus dans le projet pour les caméras

### Installation Pas-à-Pas

#### 1. Configuration VRChat SDK
```
1. Télécharger VRChat SDK3 - Worlds depuis vrchat.com/home/download
2. Importer le package dans Unity (Assets > Import Package)
3. Accepter les dépendances (TextMeshPro, etc.)
```

#### 2. Installation UdonSharp
```
1. Télécharger UdonSharp 1.1+ depuis GitHub
2. Importer dans Unity
3. Attendre la compilation
4. Vérifier : Menu "UdonSharp" doit apparaître
```

#### 3. Configuration MTG_Manager
Ouvrir la scène principale : `Assets/Scenes/MTG dev.unity`

**Configuration du GameObject MTG_Manager :**
```yaml
MTG_Manager (UdonBehaviour):
  BaseURL: "https://mtg.hactazia.fr/a"
  
  # URLs de jointure (64 entrées, base36)
  joinURLs[0-63]: "/aj0" à "/aj1z"
  
  # URLs temporaires (4096 entrées, base36)  
  TempURLs[0-4095]: "/at0" à "/at2lr"
  
  # Interface de synchronisation
  SyncInterface: [Référence MTG_SyncInterface]
  
  # Configuration des pools
  cardListParent: [Objet parent "card list"]
  maxPhysicalCards: 2500
```

#### 4. Organisation de la Scène
La scène `MTG dev.unity` contient :
- **MTG_Manager** : GameObject principal avec tous les scripts
- **card list** : Parent de ~2500 cartes pré-instanciées
- **Interfaces UI** : Canvas pour recherche, deck, sync
- **Plateau de jeu** : Zones et slots pour les cartes

#### 5. Configuration des Prefabs
Chaque prefab de carte physique doit avoir :
```
Card Prefab:
├─ MTG_PhysicCard (UdonBehaviour)
│  ├─ manager: [Référence MTG_Manager]
│  ├─ cardImage: [RawImage - Face avant]
│  ├─ cardImageBack: [RawImage - Face arrière]
│  ├─ flipButton: [GameObject - Bouton flip]
│  └─ VRC_Pickup: [Component VRChat pour grab]
```

## 📊 Optimisation des Performances Unity

### Card Pooling System
- **Pool pré-instancié** : 2500+ cartes physiques créées au démarrage
- **Activation/Désactivation** : Réutilisation des objets via SetActive()
- **Évite l'Instantiate** : Pas de création dynamique en runtime

### Atlas System Performance
```csharp
// Optimisations clés
- Batching: 24 cartes par atlas (grille 6×4)
- Max Resolution: 2048px (limite VRChat)
- Compression: PNG avec cache serveur (48h)
- UV Mapping: Précalculé et stocké en mémoire
- Draw Calls: Réduits grâce au batching
```

### Memory Management
```yaml
Mémoire Estimée:
  - Atlas Images (100 atlas × 2048px): ~400MB
  - Card Metadata Cache: ~10MB
  - Legalities/Rulings Cache: ~5MB
  - Physical Cards Pool (2500): ~50MB
  Total: ~465MB (acceptable pour VRChat)
```

### Network Optimization
- **Synchronisation sélective** : Seulement position/rotation des cartes actives
- **Throttling** : Updates limités à 10Hz
- **Instance IDs** : Système compact base36 (64 slots + 4096 temp)

### LOD Recommendations
```csharp
// Dans Unity, configurer les LOD groups
LOD 0 (0-5m):   Full resolution texture
LOD 1 (5-15m):  Half resolution
LOD 2 (15m+):   Low poly billboard
```

## 🔧 Configuration Avancée Unity

### Intervalles de Mise à Jour
```csharp
// Dans MTG_Manager.cs
ATLAS_INFO_UPDATE_INTERVAL = 10f;  // Secondes entre updates atlas
RETRY_INTERVAL = 10f;               // Retry automatique des images
MAX_RETRIES = 3;                    // Nombre max de tentatives
```

### Limites Système
```yaml
Technical Limits:
  - Atlas Maximum: 4096 (base36: 0-2lr)
  - Cartes par Atlas: 24 (6×4 grid)
  - Résolution Max: 2048px
  - Physical Cards Pool: 2500 (ajustable)
  - Concurrent Downloads: 10 (VRChat limit)
```

### Build Settings VRChat
```yaml
Unity Build Settings:
  Platform: PC, Mac & Linux Standalone + Android
  Architecture: x86_64 (PC) + ARMv7/ARM64 (Quest)
  Compression: LZ4 (faster loading)
  
VRChat Content Manager:
  Layers: Default, PlayerLocal, Pickup
  Physics: Collision matrix configurée
  Avatars: Station support optionnel
```

## 🎮 Utilisation en Jeu

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

## �️ Développement et Extension

### Ajouter de Nouvelles Fonctionnalités

#### Créer un Nouveau Script UdonSharp
```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class MTG_CustomFeature : MTG_Base
{
    [Header("References")]
    public MTG_Manager manager;
    
    void Start()
    {
        // Initialisation
        LogInfo("Custom feature initialized");
    }
    
    // Votre code ici
}
```

#### Intégration avec MTG_Manager
```csharp
// Dans votre script custom
public override void OnImageDownloadSuccess()
{
    manager.NotifyImageLoaded(cardId);
}

// Accès au cache
if (manager.GetLegalitiesForOracle(oracleId, out DataDictionary legalities))
{
    // Utiliser les légalités
}
```

### Testing en Unity Editor

#### Mode Play Testing
```
1. Ouvrir MTG dev.unity
2. Cliquer Play dans Unity
3. Tester avec la simulation VRChat Desktop
4. Vérifier les logs dans Console (filtrer "MTG")
```

#### Debug Utilities
```csharp
// Activer les logs détaillés
MTG_Base.DEBUG_MODE = true;

// Forcer le rechargement d'un atlas
manager.ForceReloadAtlas(atlasIndex);

// Vider le cache
manager.ClearAllCaches();
```

### Build et Publication VRChat

#### Préparation du Build
```
1. VRChat SDK > Show Control Panel
2. Authentication (login VRChat)
3. Builder tab
4. Vérifier les erreurs/warnings
5. Build & Test (local testing)
6. Build & Upload (publication)
```

#### Checklist Pre-Upload
- ✅ Tous les scripts UdonSharp compilés (.asset générés)
- ✅ Pas d'erreurs dans Console
- ✅ Performance rank: Good ou Excellent
- ✅ Download size < 100MB recommandé
- ✅ MTG_Manager configuré avec bonnes URLs
- ✅ Test en mode Desktop ET VR

## 📝 Logs et Debug Unity

### Système de Logging
Tous les logs utilisent des préfixes colorés (visibles dans Unity Console) :
```
🩷 [MTG_manager]      - Manager principal, cache, networking
🩷 [MTG_PhysicCard]   - Cartes physiques, pickup, sync
🩷 [MTG_SearchCard]   - Interface recherche, résultats
🩷 [MTG_DeckCard]     - Interface deck, gestion quantités
🩷 [MTG_Interface]    - UI générale
```

### Filtres Unity Console
```
Pour voir seulement les logs MTG:
  Filtrer par: "[MTG_"
  
Par type d'opération:
  Atlas: "atlas" ou "[at"
  Recherche: "search" ou "[as"
  Deck: "deck" ou "[ad"
```

### Debug Commands (en jeu)
```csharp
// Ouvrir la console VRChat: Right Ctrl + ` (backtick)

// Commandes disponibles:
reload_atlas <index>     - Recharge un atlas
clear_cache              - Vide tous les caches
show_stats               - Affiche stats mémoire
list_cards               - Liste cartes actives
```

## 🐛 Troubleshooting Unity

### Problèmes Courants et Solutions

#### ❌ "UdonSharp: Could not compile script"
```
Solutions:
1. Menu > UdonSharp > Compile All UdonSharp Programs
2. Vérifier que toutes les dépendances sont importées
3. Redémarrer Unity si persistant
4. Vérifier version UdonSharp compatible avec SDK
```

#### ❌ Les images ne chargent pas
```
Diagnostic:
✓ MTG_Manager est dans la scène
✓ BaseURL configuré correctement
✓ VRChat permet les downloads (Remote Image Loading)
✓ Attendre 10s (retry automatique)
✓ Vérifier Unity Console pour erreurs HTTP

Solution:
- En Editor: Build & Test required (pas de download en Play mode)
- En VRChat: Vérifier Safety Settings > Allow Untrusted URLs
```

#### ❌ Les cartes ne se synchronisent pas
```
Vérifications:
1. Le joueur est Owner de l'instance (Master)
2. MTG_SyncInterface assigné dans MTG_Manager
3. instanceID != -1 (visible dans logs)
4. VRC_Pickup ownership transfer enabled

Debug:
- Logs: Chercher "Sync" ou "Network"
- Vérifier UdonBehaviourSyncMode = Continuous
```

#### ❌ "NullReferenceException" dans MTG_PhysicCard
```
Causes fréquentes:
- manager non assigné dans Inspector
- cardImage ou cardImageBack null
- flipButton non assigné (pour MDFCs)

Fix:
1. Sélectionner le prefab
2. Vérifier toutes les références dans Inspector
3. Réassigner si nécessaire
4. Apply to Prefab
```

#### ❌ Le bouton flip ne s'affiche pas
```
Checklist:
✓ La carte a vraiment deux faces (MDFC)
✓ cardImageBack assigné dans Inspector
✓ flipButton GameObject existe
✓ Méthode DetectDoubleFaced() appelée

Logs à chercher:
"Double-faced card detected" - OK
"Flip button activated" - OK
```

#### ❌ Performance Issues / Frame Drops
```
Optimisations:
1. Réduire maxPhysicalCards (défaut 2500)
2. Limiter atlas simultanés (config manager)
3. Vérifier Profiler: Window > Analysis > Profiler
4. Désactiver cartes non utilisées
5. Optimiser textures (compression, mipmap)

Profiler Targets:
- CPU: Scripting < 5ms
- GPU: Rendering < 8ms  
- Memory: < 500MB
```

#### ❌ Build Errors VRChat
```
Erreurs communes:

"Udon does not support X":
- Utiliser seulement types supportés par Udon
- Voir: https://udonsharp.docs.vrchat.com

"Missing Script References":
- Tous les .asset UdonSharp doivent être générés
- Recompiler tous les scripts

"Scene contains errors":
- Vérifier Unity Console avant build
- Résoudre tous les warnings critiques
```

### Support et Resources

#### Documentation Officielle
- **VRChat Udon**: https://creators.vrchat.com/worlds/udon/
- **UdonSharp**: https://udonsharp.docs.vrchat.com/
- **Scryfall API**: https://scryfall.com/docs/api

#### Logs Importants
```bash
# Unity Editor Console
%LOCALAPPDATA%\Unity\Editor\Editor.log

# VRChat Client
%APPDATA%\..\LocalLow\VRChat\VRChat\output_log.txt

# Filtrer les logs MTG
grep -i "mtg_" output_log.txt
```

## 🤝 Contribution et Guidelines de Développement

### Structure du Code UdonSharp

#### Organisation des Fichiers
```
Assets/MTG/
├── script/           # Scripts UdonSharp principaux (.cs + .asset)
├── bord script/      # Scripts spécifiques au plateau
├── docs/             # Documentation API backend
└── old/              # Archive des anciennes versions
```

#### Naming Conventions
```csharp
// Classes: PascalCase avec préfixe MTG_
public class MTG_MyNewFeature : MTG_Base { }

// Variables publiques: camelCase
public MTG_Manager cardManager;
public string baseURL;

// Variables privées: camelCase avec underscore
private string _cachedData;
private int _retryCount;

// Constantes: UPPER_SNAKE_CASE
private const float UPDATE_INTERVAL = 10f;
```

### Coding Guidelines

#### 1. Toujours Hériter de MTG_Base
```csharp
using UdonSharp;
using UnityEngine;

public class MTG_NewScript : MTG_Base
{
    // Accès automatique aux méthodes utilitaires
    // LogInfo(), LogError(), LogWarning()
    // ParseJSON(), SafeGet(), etc.
}
```

#### 2. Documentation des Méthodes
```csharp
/// <summary>
/// Charge les données de légalité pour un oracle_id donné
/// </summary>
/// <param name="oracleId">L'oracle_id de la carte (format Scryfall)</param>
/// <returns>True si les données ont été trouvées, False sinon</returns>
public bool LoadLegalities(string oracleId)
{
    // Implémentation
}
```

#### 3. Error Handling
```csharp
// TOUJOURS vérifier les null
if (manager == null)
{
    LogError("Manager reference is null!");
    return;
}

// TOUJOURS catcher les exceptions critiques
try
{
    ProcessCardData(jsonData);
}
catch (System.Exception e)
{
    LogError($"Failed to process card: {e.Message}");
}
```

#### 4. Logging Approprié
```csharp
// Info: Pour tracking normal
LogInfo($"Loaded {count} cards from atlas {index}");

// Warning: Pour situations inhabituelles mais gérables
LogWarning($"Card {cardId} not found in cache, retrying...");

// Error: Pour erreurs critiques
LogError($"Failed to download atlas {url}: {error}");
```

#### 5. Performance Considerations
```csharp
// ✅ BON: Réutiliser objets
card.SetActive(true);
card.UpdateTexture(newTexture);

// ❌ MAUVAIS: Instantiate en boucle
for (int i = 0; i < 100; i++)
{
    GameObject card = Instantiate(cardPrefab); // Très coûteux!
}

// ✅ BON: Cache les résultats
if (!_cachedLegalities.ContainsKey(oracleId))
{
    _cachedLegalities[oracleId] = FetchLegalities(oracleId);
}
```

### Testing Checklist

#### Avant Commit
```
✅ Code compile sans erreurs ni warnings
✅ Tous les UdonSharp .asset générés
✅ Tests en Unity Editor (Play mode)
✅ Tests en VRChat Desktop build
✅ Tests en VRChat VR (si modif interactions)
✅ Logs vérifiés (pas de spam inutile)
✅ Performance acceptable (Profiler)
✅ Documentation mise à jour
```

#### Pull Request Template
```markdown
## Description
Brève description des changements

## Type de changement
- [ ] Bug fix
- [ ] Nouvelle fonctionnalité
- [ ] Breaking change
- [ ] Documentation

## Tests effectués
- [ ] Unity Editor Play mode
- [ ] VRChat Desktop build
- [ ] VRChat VR build
- [ ] Performance profiling

## Screenshots/Videos
Si applicable
```

### Architecture Patterns

#### Singleton Pattern (MTG_Manager)
```csharp
// MTG_Manager agit comme singleton accessible
public static MTG_Manager Instance;

void Start()
{
    if (Instance == null)
        Instance = this;
}
```

#### Observer Pattern (Events)
```csharp
// Manager notifie les cartes quand atlas charge
public void NotifyAtlasLoaded(int atlasIndex)
{
    foreach (MTG_PhysicCard card in activeCards)
    {
        if (card.atlasIndex == atlasIndex)
            card.OnAtlasReady();
    }
}
```

#### Object Pooling (Card Pool)
```csharp
// Pool de cartes réutilisables
private MTG_PhysicCard[] cardPool;
private int nextCardIndex = 0;

public MTG_PhysicCard GetCard()
{
    MTG_PhysicCard card = cardPool[nextCardIndex];
    nextCardIndex = (nextCardIndex + 1) % cardPool.Length;
    return card;
}
```

## 📚 Documentation API Backend

Voir la documentation complète dans [`Assets/MTG/docs/`](Assets/MTG/docs/):

### Endpoints Principaux
- **[`/ac`](Assets/MTG/docs/ac/README.md)** - Create Instance - Création d'instances de jeu
- **[`/aj`](Assets/MTG/docs/aj/README.md)** - Join Instance - Rejoindre une instance
- **[`/as`](Assets/MTG/docs/as/asREAME.md)** - Search Cards - Recherche de cartes Scryfall
- **[`/ad`](Assets/MTG/docs/ad/README.md)** - Deck Management - Gestion complète des decks
- **[`/at`](Assets/MTG/docs/at/README.md)** - Atlas System - Système d'images par batch
- **[`/au`](Assets/MTG/docs/au/README.md)** - User Information - Informations utilisateur

### API Base URL
```
Production: https://mtg.hactazia.fr/a
```

## 📊 Spécifications Techniques Unity

### Versions Testées
```yaml
Unity: 2022.3.6f1 LTS
VRChat SDK: 3.5.2
UdonSharp: 1.1.9
TextMeshPro: 3.0.6
Cinemachine: 2.9.7

Platforms Supportées:
  - Windows (PC Standalone)
  - Android (Quest 2/3/Pro)
```

### Composants Unity Utilisés
- **VRC_Pickup** - Système de grab VRChat
- **VRC_SyncedObjects** - Synchronisation réseau
- **TextMeshPro** - UI text rendering
- **RawImage** - Affichage textures atlas
- **Canvas** - Interface utilisateur
- **EventSystem** - Gestion input UI

### Layers et Physics
```
Layers requis:
  - Default (0)
  - UI (5)
  - PlayerLocal (10)
  - Pickup (13)
  
Collision Matrix:
  - Pickup × Pickup: Enabled
  - Pickup × Default: Enabled
  - PlayerLocal × Pickup: Disabled
```

## 🎯 Roadmap et Futures Features

### Version Actuelle: 1.0.0
- ✅ Recherche complète Scryfall
- ✅ Système de decks
- ✅ Cartes physiques VR
- ✅ Atlas optimisé
- ✅ Support MDFC
- ✅ Légalités et rulings
- ✅ Synchronisation multijoueur

### Planifié v1.1
- 🔄 Commander damage tracking
- 🔄 Life counter système
- 🔄 Token generator
- 🔄 Mulligan system
- 🔄 Dice roller integration

### Planifié v2.0
- 📋 Draft simulator
- 🎲 Randomizer pour limited
- 🏆 Tournament bracket système
- 📊 Statistiques de partie
- 💾 Replay system

## 📄 Licences et Crédits

### Technologies Utilisées
```yaml
Scryfall API:
  - Licence: CC0 (données)
  - Images: © Wizards of the Coast
  - URL: https://scryfall.com

VRChat SDK:
  - Licence: Propriétaire VRChat
  - URL: https://creators.vrchat.com

UdonSharp:
  - Licence: MIT
  - Auteur: MerlinVR
  - URL: https://udonsharp.docs.vrchat.com

TextMeshPro:
  - Licence: Unity Companion License
  - Copyright: Unity Technologies
```

### Crédits Projet
- **Développeur Principal**: Hactazia
- **API Backend**: https://mtg.hactazia.fr
- **Données Cartes**: Scryfall.com
- **Images Cartes**: © Wizards of the Coast LLC

### Disclaimer
```
Magic: The Gathering et toutes les cartes associées sont © Wizards of the Coast LLC.
Ce projet est un fan project non-commercial pour VRChat.
Scryfall fournit l'API de données sous licence CC0.
Les images des cartes restent la propriété de Wizards of the Coast.
```

## 🆘 Support et Contact

### Obtenir de l'Aide

#### GitHub Issues
Pour bugs et feature requests:
- Créer une issue avec label approprié
- Fournir logs Unity Console
- Inclure steps to reproduce

#### Discord
Pour questions et discussions:
- Serveur VRChat MTG (lien TBD)
- Channel #support pour aide
- Channel #development pour contributions

#### Email
Contact direct: contact@hactazia.fr

### Rapporter un Bug

Template à utiliser:
```markdown
**Description**
Description claire du bug

**Reproduction**
1. Étapes pour reproduire
2. Le comportement observé
3. Le comportement attendu

**Environment**
- Unity Version: 
- VRChat SDK Version:
- UdonSharp Version:
- OS: Windows/Mac

**Logs**
```
Coller les logs pertinents ici
```

**Screenshots**
Si applicable
```

---

**Version:** 1.0.0  
**Dernière mise à jour:** Janvier 2026  
**Auteur:** Hactazia  
**API Backend:** https://mtg.hactazia.fr  
**Licence Projet:** MIT (code), Propriétaire (assets WotC)
