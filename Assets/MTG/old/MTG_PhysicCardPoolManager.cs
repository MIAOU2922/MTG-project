using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MTG
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class MTG_PhysicCardPoolManager : UdonSharpBehaviour
    {
        private const string LOG_PREFIX = "<color=#00FFFF>[MTG_PhysicCardPoolManager]</color> ";
        
        public MTG_Manager manager;
        public Transform cardsParent;
        
        [Header("=== POOL CONFIGURATION ===")]
        [Tooltip("Prefab Card v2 pour générer la pool")]
        public GameObject physicCardPrefab;
        [Tooltip("Nombre d'objets à pré-créer dans la pool")]
        public int poolSize = 100;
        
        [Header("=== PRE-INSTANTIATED POOL ===")]
        [Tooltip("Tous les objets Card v2 pré-créés dans la scène (désactivés au départ)")]
        public GameObject[] cardPool; // Pool d'objets pré-instanciés dans la scène
        
        [Header("=== SYNCED DATA - LISTE DES IDS ===")]
        [UdonSynced,SerializeField] public string[] cardIdsList; // Liste des IDs synced
        [UdonSynced,SerializeField] public int syncedCardCount = 0; // Nombre de cartes actives
        
        private bool isInitialized = false;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        private void OnValidate()
        {
            // Initialiser les arrays dans l'éditeur
            if (poolSize <= 0) poolSize = 100;
            
            // Mettre à jour la taille du array si elle a changé
            if (cardIdsList == null || cardIdsList.Length != poolSize)
            {
                cardIdsList = new string[poolSize];
                for (int i = 0; i < poolSize; i++)
                {
                    cardIdsList[i] = "";
                }
            }
        }
#endif
        
        private void Start()
        {
            if (!isInitialized)
            {
                InitializePool();
                isInitialized = true;
                Debug.Log(LOG_PREFIX + "Pool manager started");
            }
        }
        
        private void InitializePool()
        {
            // Désactiver tous les objets de la pool au départ
            if (cardPool != null)
            {
                for (int i = 0; i < cardPool.Length; i++)
                {
                    if (cardPool[i] != null)
                    {
                        cardPool[i].SetActive(false);
                    }
                }
                Debug.Log(LOG_PREFIX + $"Pool initialized with {cardPool.Length} pre-instantiated cards");
            }
            else
            {
                Debug.LogError(LOG_PREFIX + "cardPool is null! Assign pre-instantiated cards in the inspector");
            }
        }
        
        public GameObject SpawnCard(string cardKey, Vector3 position, Quaternion rotation)
        {
            if (cardPool == null || cardPool.Length == 0)
            {
                Debug.LogError(LOG_PREFIX + "cardPool is empty! Assign pre-instantiated cards in the inspector");
                return null;
            }
            
            if (syncedCardCount >= cardPool.Length)
            {
                Debug.LogError(LOG_PREFIX + $"Pool is full! Max: {cardPool.Length}");
                return null;
            }
            
            SetOwnerIfNeeded();
            
            // Trouver le prochain objet disponible (désactivé)
            GameObject cardObject = cardPool[syncedCardCount];
            
            if (cardObject == null)
            {
                Debug.LogError(LOG_PREFIX + $"Card at index {syncedCardCount} is null!");
                return null;
            }
            
            // Ajouter l'ID à la liste synced
            cardIdsList[syncedCardCount] = cardKey;
            syncedCardCount++;
            
            Debug.Log(LOG_PREFIX + $"Activating card {syncedCardCount}: {cardKey}");
            
            // Activer et configurer la carte
            cardObject.SetActive(true);
            cardObject.transform.position = position;
            cardObject.transform.rotation = rotation;
            
            if (cardsParent != null)
            {
                cardObject.transform.SetParent(cardsParent, true);
            }
            
            // Initialiser la carte avec son ID
            MTG_PhysicCard physicCard = cardObject.GetComponent<MTG_PhysicCard>();
            if (physicCard != null)
            {
                //physicCard.InitializeCard(cardKey);
            }
            
            // Synchroniser la liste pour que les autres joueurs activent leur carte correspondante
            Debug.Log(LOG_PREFIX + $"Requesting serialization with {syncedCardCount} cards");
            RequestSerialization();
            
            return cardObject;
        }
        
        public override void OnDeserialization()
        {
            Debug.Log(LOG_PREFIX + $"OnDeserialization called! syncedCardCount = {syncedCardCount}");
            
            // S'assurer qu'on est initialisé
            if (!isInitialized)
            {
                Debug.Log(LOG_PREFIX + "OnDeserialization called before Start, initializing now");
                InitializePool();
                isInitialized = true;
            }
            
            // Vérifier que la pool existe
            if (cardPool == null || cardIdsList == null)
            {
                Debug.LogError(LOG_PREFIX + "cardPool or cardIdsList is null!");
                return;
            }
            
            if (syncedCardCount == 0)
            {
                Debug.Log(LOG_PREFIX + "No cards to sync (syncedCardCount = 0)");
                return;
            }
            
            Debug.Log(LOG_PREFIX + $"Received {syncedCardCount} card IDs from network");
            
            // Afficher les IDs reçus pour debug
            for (int i = 0; i < syncedCardCount && i < cardIdsList.Length; i++)
            {
                Debug.Log(LOG_PREFIX + $"  Card [{i}]: {cardIdsList[i]}");
            }
            
            // Activer les cartes correspondantes pour ce joueur
            for (int i = 0; i < syncedCardCount && i < cardPool.Length; i++)
            {
                GameObject cardObject = cardPool[i];
                string cardKey = cardIdsList[i];
                
                if (cardObject != null && !string.IsNullOrEmpty(cardKey))
                {
                    // Si la carte n'est pas déjà active, l'activer et initialiser
                    if (!cardObject.activeSelf)
                    {
                        Debug.Log(LOG_PREFIX + $"Remote player activating card {i}: {cardKey}");
                        cardObject.SetActive(true);
                        
                        // Initialiser la carte avec son ID
                        MTG_PhysicCard physicCard = cardObject.GetComponent<MTG_PhysicCard>();
                        if (physicCard != null)
                        {
                            //physicCard.InitializeCard(cardKey);
                        }
                    }
                }
            }
        }
        
        private void SetOwnerIfNeeded()
        {
            if (Networking.LocalPlayer != null && !Networking.IsOwner(Networking.LocalPlayer, gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                Debug.Log(LOG_PREFIX + "Took ownership of pool manager");
            }
        }
        
        public int GetCardCount()
        {
            return syncedCardCount;
        }
        
        public string GetPoolStatus()
        {
            int syncedCount = GetCardCount();
            int available = cardPool.Length - syncedCount;
            return $"Pool: {syncedCount}/{cardPool.Length} cards active, {available} available";
        }
    }
}
