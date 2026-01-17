# Route `/aj` - Rejoindre une Instance

La route `/aj` permet à un utilisateur de rejoindre une instance existante en utilisant un ID encodé en base36.

## 📋 Vue d'ensemble

**Endpoint :** `GET /aj/{instanceCode}`

**Paramètres :**
- `instanceCode` : ID de l'instance en base36 (caractères alphanumériques minuscules)

**Authentification :** Basée sur l'UID utilisateur (cookie/session)

## 🎯 Fonctionnement

### 1. **Validation de l'ID d'instance**
- Conversion du code base36 vers un ID numérique
- Vérification des limites (0-63 pour 64 instances maximum)
- Validation de l'existence de l'instance

### 2. **Gestion de l'utilisateur**
- Récupération/création automatique de l'utilisateur via UID
- Mise à jour automatique du `last_seen_at`

### 3. **Gestion des associations**
- Vérification si l'utilisateur est déjà dans l'instance cible
- Nettoyage automatique des anciennes associations utilisateur-instance
- Ajout de l'utilisateur à la nouvelle instance

## 📊 Réponse

### **Structure JSON standardisée**

Toutes les réponses suivent la structure standardisée suivante :

```json
{
  "link_type": "j",      // Type de route (j=join)
  "link_id": "2a",        // ID d'instance en base36
  "iid": 42,             // Instance ID rejointe
  "uid": 12345,          // User ID
  "time": 1728499200000  // Timestamp de la réponse
}
```

### **Champs de réponse**

| Champ | Type | Description |
|-------|------|-------------|
| `link_type` | string | Type de route : `"j"` pour join |
| `link_id` | string | Code de l'instance en base36 (ex: "2a" pour 42) |
| `iid` | number | ID de l'instance rejointe (0-63) |
| `uid` | number | ID de l'utilisateur |
| `time` | number | Timestamp Unix en millisecondes |

### **Succès (200)**
```json
{
  "link_type": "j",
  "link_id": "2a",
  "iid": 42,
  "uid": 12345,
  "time": 1728499200000
}
```

### **Erreur (400)**
```json
{
  "error": "Invalid instance ID (must be between 0 and 63)"
}
```

### **Erreur (404)**
```json
{
  "error": "Instance not found"
}
```

### **Erreur (500)**
```json
{
  "error": "Error joining instance"
}
```

## 🔄 Logique de nettoyage

### **Gestion des associations multiples**
1. **Vérification d'appartenance** : L'utilisateur peut-il rejoindre cette instance ?
2. **Nettoyage automatique** : Suppression des anciennes associations
3. **Ajout sécurisé** : Ajout à la nouvelle instance uniquement si pas déjà présent

### **Prévention des conflits**
- Un utilisateur ne peut être que dans une instance à la fois
- Nettoyage automatique des associations obsolètes
- Vérification de l'existence de l'instance cible

## 🔗 Utilisation typique

### **Workflow de partage**
1. **Utilisateur A crée une instance** : `GET /ac` → `iid: 42`
2. **Utilisateur A partage le lien** : Instance 42 = `2a` en base36
3. **Utilisateur B rejoint** : `GET /aj/2a` (rejoint l'instance 42)
4. **Collaboration** : Les deux utilisateurs partagent la même instance

### **Conversion base36**
```
Instance ID → Base36
42         → 2a
31         → v
15         → f
```

## 🚨 Gestion d'erreurs

### **400 Bad Request**
- ID d'instance invalide (non base36)
- ID hors limites (négatif ou > 63)
- Format incorrect du paramètre

### **404 Not Found**
- Instance inexistante
- Instance supprimée ou expirée

### **500 Internal Server Error**
- Erreur de base de données
- Problème lors du nettoyage des associations
- Erreur lors de l'ajout de l'utilisateur

## 📈 Métriques

### **Logs automatiques**
```
✅ User 12345 joined instance 42
🔄 Cleaned old associations for user 12345
```

## 🔗 Liens connexes

- **`/ac`** : Créer une nouvelle instance
- **`/as`** : Recherche et ajout de cartes (partagé avec l'instance)
- **`/at/{linkId}`** : Accès au contenu partagé de l'instance

## 🎯 Cas d'usage

1. **Collaboration** : Plusieurs utilisateurs sur la même instance
2. **Partage de liens** : URLs courtes pour partager des sessions
3. **Sessions de groupe** : Travail collaboratif sur des collections
4. **Reprise de session** : Rejoindre une instance sauvegardée

---

*Cette route permet le partage et la collaboration en permettant à plusieurs utilisateurs de travailler sur la même instance via des liens base36 courts.*