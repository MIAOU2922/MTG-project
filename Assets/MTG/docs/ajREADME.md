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

### **Succès (200)**
```json
{
  "time": 1728499200000,
  "uid": 12345,
  "iid": 42
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