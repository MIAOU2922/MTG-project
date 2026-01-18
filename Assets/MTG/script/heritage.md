# Hiérarchie d'héritage MTG

## Structure complète depuis MTG_Base

```
UdonSharpBehaviour
│
├── MTG_Base
│   │
│   ├── MTG_Card
│   │   ├── MTG_SearchCard
│   │   ├── MTG_DeckCard
│   │   └── MTG_PhysicCard
│   │
│   └── MTG_Manager
│
└── MTG_Debug
```

## Détails des classes

### MTG_Base
- **Hérite de:** `UdonSharpBehaviour`
- **Héritée par:** `MTG_Card`, `MTG_Manager`
- **Méthodes virtuelles:**
  - *(vide actuellement - à compléter si ajouté)*

---

### MTG_Card : MTG_Base
- **Hérite de:** `MTG_Base`
- **Héritée par:** `MTG_SearchCard`, `MTG_DeckCard`, `MTG_PhysicCard`
- **Méthodes virtuelles:**
  - `OnValidate()` - validation éditeur
  - `Start()` - initialisation
  - `Update()` - boucle de mise à jour
- **Fonctionnalités:**
  - Gestion des images de cartes (front/back)
  - Chargement via atlas
  - Flip des cartes double-face
  - Gestion du CardKey et CardOracleKey

---

### MTG_SearchCard : MTG_Card
- **Hérite de:** `MTG_Card`
- **Fonctionnalités spécifiques:**
  - Interface avec `MTG_Searchinterface`
  - Bouton pour demander l'aperçu de carte
  - Recherche automatique de l'interface de recherche

---

### MTG_DeckCard : MTG_Card
- **Hérite de:** `MTG_Card`
- **Fonctionnalités spécifiques:**
  - Interface avec `MTG_Deckinterface`
  - Gestion du compteur de cartes (`cardCount`)
  - Méthodes `AddOne()`, `RemoveOne()`, `SetCount()`
  - Affichage du nombre de cartes

---

### MTG_PhysicCard : MTG_Card
- **Hérite de:** `MTG_Card`
- **Fonctionnalités spécifiques:**
  - Gestion du pickup VRC (`VRC_Pickup`)
  - Zoom in/out avec `OnPickupUseDown/Up`
  - Événements `OnPickup()` et `OnDrop()`
  - État `IsHeld` pour tracking

---

### MTG_Manager : MTG_Base
- **Hérite de:** `MTG_Base`
- **Fonctionnalités:**
  - Gestion globale du système MTG
  - Gestion des atlas de textures
  - *(détails à compléter)*

---

### MTG_Debug : UdonSharpBehaviour
- **Hérite de:** `UdonSharpBehaviour` *(pas MTG_Base)*
- **Fonctionnalités:**
  - Système de logging global
  - Méthodes `Log()` et `Debug()`
  - Formatage des messages avec couleurs
  - Flags `DEBUG` et `VERBOSE_DEBUG`
  - Méthode `IsDebugEnabled()`
