using System;
using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Data;
using VRC.SDK3.Components;
using TMPro;
using System.Collections.Generic;

namespace MTG
{
    public class MTG_Deckinterface : UdonSharpBehaviour
    {
        // Préfixe coloré pour les logs
        private const string LOG_PREFIX = "<color=#FF1493>[MTG_Deckinterface]</color> ";
        
        [Header("=== REFERENCES ===")]
        public MTG_Manager manager;
        public GameObject DeckCardPrefab;
        public Transform DeckCardParent;
        
        [Header("=== PHYSIC CARD SPAWN ===")]
        public GameObject PhysicCardPrefab;
        public Transform PhysicCardSpawnPoint;
        public Transform PhysicCardsParent; // Parent pour organiser les cartes spawned
        public UnityEngine.UI.Button SpawnPhysicCardButton;
        public MTG_PhysicCardPoolManager physicCardPoolManager; // Pool manager pour synchroniser les cartes

        [Header("=== INPUT FIELD ===")]
        public VRCUrlInputField validatedInput;
        public TMPro.TMP_InputField urlDisplayText;  // Champ TMP pour afficher l'URL construite
        
        [Header("=== INPUT FIELDS ===")]
        // TextMeshPro input fields
        public TMPro.TMP_InputField DeckNameInput;
        public TMPro.TMP_InputField DeckDescriptionInput;
        public TMPro.TMP_InputField CardListInput;




        void Start()
        {
            // Initialisation si nécessaire
        }

        public void OnCardButtonPressed(string cardKey)
        {
            // Placeholder for future functionality
            Debug.Log(LOG_PREFIX + $"Card button pressed: {cardKey}");
        }

        public void OnCardCountChanged(string cardKey, int newCount)
        {
            // Placeholder for future functionality
            Debug.Log(LOG_PREFIX + $"Card count changed: {cardKey} = {newCount}");
        }

        public void OnCardRemoved(string cardKey)
        {
            // Placeholder for future functionality
            Debug.Log(LOG_PREFIX + $"Card removed: {cardKey}");
        }

        public void OnCardPreviewRequest(string cardKey)
        {
            // Placeholder for future functionality
            Debug.Log(LOG_PREFIX + $"Card preview requested: {cardKey}");
        }
    }
}
