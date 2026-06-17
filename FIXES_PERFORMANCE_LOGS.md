# Fixes pour les problèmes de performance et spam de logs

## Problème identifié

Les cartes MTG généraient un spam massif de logs au runtime, causant :
1. Des logs "[UdonBehaviour] LOG UpdateFlipVisibility called for cardKey:" répétés en boucle
2. Performance dégradée à cause du volume de logs
3. Difficulté à debugger à cause du bruit dans la console

## Causes racines

1. **Logs trop verbeux** : Beaucoup de méthodes utilisaient `this.Log()` au lieu de `this.VerboseLog()`
   - `this.Log()` s'affiche quand `DEBUG = true`
   - `this.VerboseLog()` s'affiche seulement quand `VERBOSE_DEBUG = true` ET `DEBUG = true`

2. **SetCardKey() appelée en boucle** : Pas de vérification si la carte est déjà chargée
   - OnDeserialization() appelait SetCardKey() même si la clé n'avait pas changé
   - Late join init appelait aussi SetCardKey() sans vérification
   - Chaque appel réinitialisait `ImageFrontLoaded = false`, déclenchant un rechargement

3. **Scripts UdonSharp compilés obsolètes** : Les fichiers .asset contenaient une ancienne version avec des logs

## Corrections appliquées

### 1. MTG_Card.cs
- ✅ Ajout d'une vérification dans SetCardKey() pour éviter le rechargement si déjà chargé
- ✅ Conversion des logs `this.Log()` → `this.VerboseLog()` pour :
  - SetCardKey()
  - GetCardKey()
  - SetCardOracleKey()
  - GetCardOracleKey()
  - FlipCard()

### 2. MTG_PhysicCard.cs
- ✅ Ajout de vérifications `CardKey != _SyncedCardKey` avant d'appeler SetCardKey()
  - Dans OnDeserialization() (ligne ~118)
  - Dans Update() pour late join init (ligne ~143)

### 3. MTG_DeckInterface.cs
- ✅ Conversion des logs en VerboseLog :
  - OnDeckResponse()
  - ProcessDeckResponse()
  - OnCardPreviewRequest()
  - OnCardCountChanged()
  - OnCardRemoved()

### 4. MTG_Interface.cs
- ✅ Conversion des logs en VerboseLog :
  - SendRequest()
  - OnUrlValidated()
  - OnSearchResponse()
  - OnDeckResponse()

### 5. MTG_Manager.cs
- ✅ Conversion des logs en VerboseLog :
  - OnMasterTransferred()
  - OnPlayerJoined()
  - JoinGame()
  - OnStringLoadSuccess()
  - Dispatch de IsSearchURL()

## Actions requises

### ⚠️ IMPORTANT : Recompiler les scripts UdonSharp

Les corrections dans le code source ne seront pas effectives tant que les scripts UdonSharp ne sont pas recompilés.

**Méthode 1 : Recompilation automatique dans Unity**
1. Ouvrir le projet dans Unity
2. Attendre que Unity détecte les changements de fichiers
3. UdonSharp devrait automatiquement recompiler les scripts modifiés
4. Vérifier dans la console qu'il n'y a pas d'erreurs de compilation

**Méthode 2 : Forcer la recompilation**
1. Dans Unity, aller dans le menu : `UdonSharp` → `Compile All UdonSharp Programs`
2. Attendre la fin de la compilation
3. Vérifier qu'il n'y a pas d'erreurs

**Méthode 3 : Recompilation manuelle**
1. Sélectionner tous les fichiers .asset dans `Assets/MTG/`
2. Clic droit → `UdonSharp` → `Force Recompile`

### Tester les corrections

1. Lancer le projet en mode Play dans Unity
2. Vérifier dans la console qu'il n'y a plus de spam de logs "UpdateFlipVisibility"
3. Pour activer les logs verbose si nécessaire :
   - Trouver le GameObject "Manager" dans la scène
   - Dans l'Inspector, activer `VERBOSE_DEBUG = true` sur le composant MTG_Manager
   
### Désactiver les logs en production

Pour un build de production (VRChat), s'assurer que :
- `DEBUG = false` sur MTG_Manager
- `VERBOSE_DEBUG = false` sur MTG_Manager

Cela désactivera tous les logs et optimisera les performances.

## Performance attendue après correction

- ✅ Plus de spam de logs dans la console
- ✅ Réduction du nombre d'appels à SetCardKey() et SetImageFromId()
- ✅ Meilleure performance au chargement de plusieurs cartes (late join)
- ✅ Logs visibles uniquement en mode debug explicite

## Notes techniques

### Système de logs MTG_Debug

Le système de logs utilise des méthodes d'extension dans `MTG_Debug.cs` :

```csharp
// Logs standards (affichés si DEBUG = true)
this.Log("message");       // Toujours affiché si DEBUG
this.Warning("message");   // Warning
this.Error("message");     // Erreur (toujours affiché)

// Logs verbose (affichés si DEBUG = true ET VERBOSE_DEBUG = true)
this.VerboseLog("message");     // Debug détaillé
this.VerboseWarning("message"); // Warning détaillé
this.VerboseError("message");   // Erreur détaillée
```

### Gestion du chargement de cartes

Le système de chargement a plusieurs niveaux :

1. **Spawn initial** : `InitializeCard()` est appelé par le Pool Manager
2. **Late join** : Les joueurs qui rejoignent reçoivent via `OnDeserialization()`
3. **Staggering** : Délai aléatoire de 0.15s × PoolIndex pour éviter le chargement simultané
4. **Retry** : Si l'image ne charge pas, retry toutes les 10 secondes via `Update()`

### Optimisations de SetCardKey()

```csharp
// Évite le rechargement si déjà chargé
if (CardKey == _CardKey && ImageFrontLoaded)
{
    this.VerboseLog($"SetCardKey: already loaded {_CardKey}, skipping");
    return;
}
```

Cette vérification évite :
- Rechargement d'images déjà en cache
- Appels répétés à UpdateFlipVisibility()
- Reset inutiles de ImageFrontLoaded/ImageBackLoaded

## Date de correction

2026-06-17

## Fichiers modifiés

- `Assets/MTG/script/MTG_Card.cs`
- `Assets/MTG/script/MTG_PhysicCard.cs`
- `Assets/MTG/script/MTG_DeckInterface.cs`
- `Assets/MTG/script/MTG_Interface.cs`
- `Assets/MTG/script/MTG_Manager.cs`
