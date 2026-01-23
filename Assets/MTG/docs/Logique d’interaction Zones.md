# 🃏 MTG – Logique d’interaction Zones ↔ Cartes (VR / Udon)

## 🎯 Philosophie générale

- Le système **n’impose aucune règle de jeu**
- Le joueur est **responsable de l’application des règles**
- Les zones sont des **outils d’assistance ergonomique**
- Les cartes sont des **objets physiques grabbables**
- Certaines actions peuvent être **assistées automatiquement** (Draw, Tutor, Shuffle)

---

## 🧠 Rôles fondamentaux

### 👤 Joueur
- Grab / Drop des cartes
- Use / Click / Input
- Déplace physiquement les cartes
- Déclenche volontairement des actions de zone

➡️ **Agent principal**

---

### 🃏 Carte (MTG_PhysicCard : MTG_Card)
- Objet physique
- Ne connaît aucune règle MTG
- Expose uniquement des capacités d’interaction

Capacités possibles :
- Grab
- Zoom
- Use
- Visibilité
- Rotation / Tap

➡️ **Objet passif, contrôlé par le joueur**

---

### 🧱 Zone (MTG_Zone)
- Ne valide aucune règle
- Ne bloque pas le joueur
- Peut :
  - modifier les capacités d’interaction des cartes
  - gérer un ordre interne
  - proposer des actions assistées

➡️ **Assistant contextuel**

---

## 🧩 Capacités modifiables par une zone

Une zone peut uniquement agir sur :

| Capacité | Description |
|-------|------------|
| Grab | Autoriser ou non le pickup |
| Zoom | Autoriser ou non le zoom |
| Use | Autoriser ou non l’input Use |
| Visibilité | Face visible ou cachée |
| Snap | Alignement visuel (optionnel) |

❌ Une zone ne :
- bloque jamais un déplacement manuel
- n’impose jamais une règle
- ne valide jamais une action

---

## 🧠 Modèle Carte ↔ Zone

### Entrée d’une carte dans une zone
```

OnCardEnter(card):
card.ApplyZoneModifiers(zone)

```

### Sortie d’une carte
```

OnCardExit(card):
card.ResetModifiers()

```

➡️ La **carte reste maître de son état**
➡️ La zone **demande**, la carte **applique**

---

## 🧩 Catégories de zones

---

## 🟫 Deck

### Comportement passif
- ❌ Grab carte individuelle
- ❌ Zoom
- ❌ Visibilité
- Ordre interne conservé

### Actions assistées
| Action | Effet |
|-----|------|
| Draw | Deck → Hand |
| Tutor | Deck → ViewerZone |
| Shuffle | Mélange interne |

➡️ Déclenchées par :
- bouton
- Use sur la zone
- menu radial
- hotkey desktop

---

## ⚰️ Cimetière

### Comportement passif
- ✅ Visibilité
- ✅ Zoom
- Grab optionnel
- Ordre important (dernier en haut)

### Actions assistées
| Action | Effet |
|-----|------|
| View | Graveyard → ViewerZone |

---

## ❌ Exile

### Comportement passif
- ✅ Visibilité
- Grab optionnel
- Ordre peu critique

### Actions assistées
| Action | Effet |
|-----|------|
| View | Exile → ViewerZone |

---

## ✋ Main

### Comportement passif
- ✅ Grab
- ✅ Zoom
- Visibilité configurable (adversaire)

### Actions assistées
| Action | Effet |
|-----|------|
| Play | Main → Battlefield |
| Discard | Main → Graveyard |

---

## 🟩 Battlefield

### Comportement passif
- ✅ Grab
- ✅ Zoom
- ✅ Rotation / Tap
- Organisation spatiale

### Actions assistées
| Action | Effet |
|-----|------|
| Destroy | Battlefield → Graveyard |
| Exile | Battlefield → Exile |

---

## 🧩 Zone Temporaire (ViewerZone)

### Définition
Zone à slots générée dynamiquement pour :
- Tutor
- View Graveyard
- View Exile
- Peek Deck

### Propriétés
- Liée à une zone source
- Ne possède pas les cartes
- Référence temporaire

### Capacités typiques
- ❌ Grab (souvent)
- ❌ Sortie libre
- ✅ Zoom
- ✅ Sélection

### Fermeture
```

OnClose():

* cartes restantes retournent à la zone source
* shuffle optionnel
* destruction de la zone

```

---

## 🧠 Déplacements assistés (exception autorisée)

Les zones **peuvent déplacer des cartes sans grab** UNIQUEMENT pour :

- Draw
- Tutor
- Shuffle
- View
- Return ciblé

➡️ Toujours déclenché explicitement par le joueur  
➡️ Jamais automatique ou interprété

---

## 🧩 Schéma logique global

```

[Player]
↓ (Use / Button / Input)
[Zone Action Assistée]
↓
[Déplacement de carte]
↓
[Zone cible applique ses modificateurs]

```

---

## 🧠 Règles d’or du système

1. Le joueur reste toujours libre
2. Aucune action n’est bloquée par le système
3. Les zones assistent, elles n’arbitrent pas
4. Les cartes ne connaissent aucune règle
5. Les actions assistées sont explicites
6. Pas de moteur de règles MTG

---

## 🚀 Avantages de cette architecture

✔ Fidèle au jeu papier  
✔ VR-first, sans frustration  
✔ Aucun soft-lock  
✔ Facile à étendre  
✔ Compatible casual / RP / compétitif  
✔ Parfait pour Udon (réseau + perfs)

---

## 🔜 Extensions possibles

- Permissions par joueur
- Zone fantôme (highlight)
- Ghost slot preview
- Historique de mouvements
- Mode arbitre optionnel

---

**Ce document est la référence canonique de l’architecture.**