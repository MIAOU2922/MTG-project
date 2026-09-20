# Routes `/au*` - Identité Utilisateur

Les routes `/au*` gèrent l'identité des utilisateurs. **L'uid n'est plus dérivé de l'IP** : le serveur émet une clé aléatoire que le client stocke dans le PlayerData VRChat, et le compte reste stable même si l'IP change.

## 📋 Endpoints

| Endpoint | Rôle |
|---|---|
| `GET /aur` | Enregistrement : crée le compte et renvoie la clé |
| `GET /aul{3hex}` | Login par chunks : un quart de la clé par requête (Udon ne peut pas construire d'URL dynamiques) |
| `GET /aul{12hex}` | Login direct : la clé complète en une seule requête (ex: `/aul47d286cb3fd8`) |

## 🎯 Fonctionnement

### 1. Enregistrement (`/aur`)
- Nouveau compte : **l'id EST la clé** (12 hex générés aléatoirement) et le **hash SHA-256** de l'IP courante est mémorisé dans la colonne `ip_hash` (**jamais d'IP en clair**).
- La clé est renvoyée au client pour être stockée dans le PlayerData VRChat.

### 2. Login par chunks (`/aul{3hex}`)
- La clé est découpée en **4 chunks de 3 hex**, envoyés via des URLs statiques (`/aul18e`, `/aul691`, …).
- Les chunks n'arrivent pas avec leur ordre → le serveur les bufférise par hash d'IP (TTL 90 s, dédupe) puis reconstitue la clé **par permutation** (24 candidats) contre les clés connues.
- Succès → le hash de l'IP est lié au compte et `{ uid, key_confirmed: true }` est renvoyé.

### 3. Autres routes
Toutes les autres routes (`/ac`, `/aj`, `/as`, `/at`, `/ad`) résolvent l'utilisateur via le **hash SHA-256 de l'IP** → compte le plus récemment actif. Si l'IP n'est pas liée (client pas encore enregistré), elles renvoient `401 { error: 'unknown_user' }`.

## 📊 Réponses

### `/aur`
```json
{ "link_type": "u", "link_id": "", "iid": null, "uid": "330ea1cc96e9",
  "key": "330ea1cc96e9", "time": 1789803548465,
  "data": { "last_seen_at": 1789803548460 } }
```

### `/aul{3hex}` — incomplet
```json
{ "link_type": "u", "uid": 0, "chunks_received": 2, "chunks_total": 4, "time": 1789803557959 }
```

### `/aul{3hex}` — complet (login OK)
```json
{ "link_type": "u", "link_id": "", "iid": null, "uid": "330ea1cc96e9",
  "key_confirmed": true, "time": 1789803557983,
  "data": { "last_seen_at": 1789803557981 } }
```

### `/aul{3hex}` — clé inconnue
```json
{ "link_type": "u", "uid": 0, "error": "unknown_key", "time": 1789803567713 }
```

## 🗄️ Stockage (table `users` — tout dans une seule table)

| Colonne | Type | Rôle |
|---|---|---|
| `id` | `String` (PK) | **La clé 12 hex, utilisée directement comme identifiant** |
| `ip_hash` | `String?` | Hash SHA-256 de la dernière IP connue — **jamais d'IP en clair, non unique** : plusieurs users peuvent partager la même IP |
| `created_at` | `timestamp` | Date de création |
| `last_seen_at` | `timestamp` | Dernière activité (résolution IP → user = dernier actif gagnant) |

## ⚠️ Limites connues
- Plusieurs users sur la même IP : le serveur résout les requêtes vers le **plus récemment actif** (chaque login par chunks ou `/aur` rebascule le `ip_hash` sur son compte).
- Le client Udon doit faire 4 requêtes espacées de ~5 s ; retries → `chunks_received` ne bouge pas si doublon.
