# Route `/ac` - Création d'Instance

La route `/ac` permet de créer une nouvelle instance utilisateur avec un système de rotation intelligent.

## 📋 Vue d'ensemble

**Endpoint :** `GET /ac`

**Authentification :** Basée sur l'UID utilisateur (cookie/session)

**Méthode :** GET (pour faciliter l'intégration dans les URLs)

## 🎯 Fonctionnement

### 1. **Gestion de l'utilisateur**
- Récupération/création automatique de l'utilisateur via UID
- Mise à jour automatique du `last_seen_at`

### 2. **Création d'instance avec rotation**
- Utilise un système de rotation pour optimiser l'utilisation des instances
- Réutilise les anciennes instances quand toutes les 64 instances sont utilisées
- Nettoie automatiquement les données des instances réutilisées

## 📊 Réponse

### **Succès (200)**
```json
{
  "time": 1728499200000,
  "uid": 12345,
  "iid": 42
}
```

### **Erreur (500)**
```json
{
  "error": "Error creating instance"
}
```

## 🔄 Système de rotation

### **Logique de création**
1. **Recherche d'ID libre** : Trouve le premier ID non utilisé (0-63)
2. **Rotation automatique** : Si toutes les instances sont utilisées, réutilise l'instance la plus ancienne
3. **Nettoyage automatique** : Reset des `card_ids` et `user_ids` lors de la réutilisation

### **Limites**
- **Maximum 64 instances** simultanées (0-63)
- **Rotation basée sur** `created_at` (plus ancienne instance d'abord)

## 🔗 Utilisation typique

### **Workflow complet**
1. **Créer une instance** : `GET /ac` → `iid: 42`
2. **Rejoindre l'instance** : `GET /aj/2a` (42 en base36)
3. **Ajouter des cartes** : `GET /as?q=lightning`
4. **Accéder au contenu** : `GET /at/0` (JSON) ou `GET /at/a` (atlas)

## 🚨 Gestion d'erreurs

### **500 Internal Server Error**
- Erreur lors de la création de l'instance en base de données
- Problème de connexion à la base de données
- Erreur dans le système de rotation

## 📈 Métriques

### **Logs automatiques**
```
✅ Instance 42 created for user 12345
🔄 Instance rotation: reusing instance 5 (oldest)
```

## 🔗 Liens connexes

- **`/aj/{instanceId}`** : Rejoindre une instance existante
- **`/as`** : Recherche et ajout de cartes
- **`/at/{linkId}`** : Accès au contenu de l'instance

## 🎯 Cas d'usage

1. **Première visite** : Création d'une nouvelle session utilisateur
2. **Nouvelle session** : Démarrage d'une nouvelle instance de travail
3. **Rotation automatique** : Optimisation des ressources serveur
4. **Session isolée** : Chaque instance est indépendante

---

*Cette route est le point d'entrée pour créer de nouvelles sessions de travail avec un système de rotation qui optimise l'utilisation des ressources.*