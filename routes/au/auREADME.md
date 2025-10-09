# Route `/au` - Gestion Utilisateur

La route `/au` gère la création et la récupération automatique des utilisateurs basée sur l'adresse IP.

## 📋 Vue d'ensemble

**Endpoint :** `GET /au`

**Authentification :** Automatique basée sur l'adresse IP (hashée)

**Méthode :** GET

## 🎯 Fonctionnement

### 1. **Identification automatique**
- Utilise l'adresse IP du client comme identifiant unique
- Hash de l'IP pour l'anonymisation et la sécurité
- Création automatique d'un nouvel utilisateur si inexistant

### 2. **Gestion des sessions**
- Mise à jour automatique du `last_seen_at`
- Suivi des activités utilisateur
- Maintenance des statistiques d'usage

## 📊 Réponse

### **Succès (200)**
```json
{
  "time": 1728499200000,
  "user_id": 12345,
  "last_seen_at": 1728499200000
}
```

## 🔐 Sécurité et anonymisation

### **Hash de l'IP**
- Utilise `strToHash()` pour anonymiser l'adresse IP
- Protection de la vie privée des utilisateurs
- Identifiant unique mais non traçable

### **Gestion automatique**
- Pas de cookies ou sessions persistantes
- Identification basée sur l'IP uniquement
- Création transparente d'utilisateurs

## 🔗 Utilisation typique

### **Workflow utilisateur**
1. **Première visite** : `GET /au` → Création automatique d'utilisateur
2. **Actions utilisateur** : Toutes les routes mettent à jour `last_seen_at`
3. **Suivi d'activité** : Monitoring des utilisateurs actifs

### **Intégration transparente**
- Appel automatique par le frontend
- Pas de gestion manuelle requise
- Fonctionnement en arrière-plan

## 🚨 Gestion d'erreurs

### **500 Internal Server Error**
- Erreur de base de données lors de la création/récupération
- Problème avec le hash de l'IP
- Erreur lors de la mise à jour du `last_seen_at`

## 📈 Métriques et analytics

### **Données collectées**
- `user_id` : Identifiant unique hashé
- `last_seen_at` : Dernière activité connue
- `time` : Timestamp de la requête

### **Utilisation des données**
- Statistiques d'utilisation
- Nettoyage des instances inactives
- Optimisation des ressources

## 🔗 Liens connexes

- **`/ac`** : Création d'instance (utilise l'utilisateur créé)
- **`/aj/{instanceId}`** : Rejoindre une instance
- **Toutes les routes** : Met à jour automatiquement `last_seen_at`

## 🎯 Cas d'usage

1. **Analytics** : Suivi des utilisateurs actifs
2. **Session management** : Gestion automatique des sessions
3. **Resource cleanup** : Nettoyage basé sur l'activité
4. **Privacy-first** : Anonymisation des adresses IP

## ⚠️ Considérations

### **Limites de l'approche IP-based**
- **VPN/Proxy** : Peut créer plusieurs utilisateurs pour le même utilisateur réel
- **IP dynamique** : Changement d'IP peut créer de nouveaux utilisateurs
- **Partage de réseau** : Plusieurs utilisateurs derrière la même IP

### **Avantages**
- **Simple** : Pas de gestion de cookies/sessions
- **Privacy** : Adresses IP hashées, pas stockées en clair
- **Automatique** : Fonctionne sans intervention utilisateur

---

*Cette route fournit une gestion utilisateur transparente et respectueuse de la vie privée, idéale pour des applications web sans authentification complexe.*