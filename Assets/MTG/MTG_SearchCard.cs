using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

namespace MTG
{
    public class MTG_SearchCard : UdonSharpBehaviour
    {
        // Préfixe coloré pour les logs
        private const string LOG_PREFIX = "<color=#FF1493>[MTG_SearchCard]</color> ";
        
        public RawImage cardImage;
        public GameObject loading;
        public string cardKey;

        [System.NonSerialized]
        public MTG_Manager manager;
        public MTG_Searchinterface searchInterface;

        
        public Rect uvRect;
        public int atlasIndex = -1;
        public float lastRetryTime = 0f;
        public const float RETRY_INTERVAL = 10f; // Retry toutes les 10 secondes
        private bool imageLoaded = false;
        
        internal void SetData(DataDictionary cardDict)
        {
            if (cardDict == null) return;
            
            // Récupérer l'ID unique de la carte
            if (cardDict.ContainsKey("id") && cardDict["id"].TokenType == TokenType.String)
            {
                cardKey = cardDict["id"].String;
                //Debug.Log(LOG_PREFIX + $"Card created with ID: {cardKey}");
            }
            else
            {
                cardKey = "";
                Debug.LogWarning(LOG_PREFIX + "Card created without ID!");
            }
            
            // Activer le loading au début
            if (loading != null)
            {
                loading.SetActive(true);
            }
            
            // Réinitialiser le flag et le temps pour permettre un retry immédiat
            imageLoaded = false;
            lastRetryTime = -RETRY_INTERVAL; // Permet un retry immédiat au prochain Update
        }
        
        public void SetImageFromId()
        {
            if (manager == null || string.IsNullOrEmpty(cardKey))
            {
                Debug.LogWarning(LOG_PREFIX + $"SetImageFromId: Missing manager ({manager != null}) or cardKey ({!string.IsNullOrEmpty(cardKey)})");
                return;
            }
            
            //Debug.Log(LOG_PREFIX + $"SetImageFromId: Searching for card {cardKey} in atlas cache");
            
            int foundAtlasIndex;
            Rect foundRect;
            if (manager.GetAtlasInfoForCard(cardKey, out foundAtlasIndex, out foundRect))
            {
                //Debug.Log(LOG_PREFIX + $"SetImageFromId: Found card {cardKey} in atlas {foundAtlasIndex}");
                this.atlasIndex = foundAtlasIndex;
                this.uvRect = foundRect;
                
                Texture2D atlasTexture = manager.GetAtlasTexture(atlasIndex);
                if (atlasTexture != null)
                {
                    //Debug.Log(LOG_PREFIX + $"SetImageFromId: Atlas texture ready for card {cardKey}, applying now");
                    ApplyAtlasTexture(atlasTexture);
                }
                else
                {
                    //Debug.Log(LOG_PREFIX + $"SetImageFromId: Atlas texture NOT ready for card {cardKey} (atlas {atlasIndex}), will retry in {RETRY_INTERVAL}s");
                }
            }
            else
            {
                Debug.LogWarning(LOG_PREFIX + $"SetImageFromId: Atlas info NOT found for card {cardKey}, will retry in {RETRY_INTERVAL}s");
            }
        }
        
        private void Update()
        {
            // Si on n'a pas encore chargé l'image, essayer de la récupérer via le manager
            if (!imageLoaded && manager != null && !string.IsNullOrEmpty(cardKey))
            {
                // Vérifier si assez de temps s'est écoulé depuis la dernière tentative
                if (Time.time - lastRetryTime >= RETRY_INTERVAL)
                {
                    lastRetryTime = Time.time;
                    //Debug.Log(LOG_PREFIX + $"Retrying image load for card {cardKey}");
                    SetImageFromId();
                }
            }
        }

        private void ApplyAtlasTexture(Texture2D atlasTexture)
        {
            cardImage.texture = atlasTexture;
            cardImage.uvRect = uvRect;

            // Marquer l'image comme chargée
            imageLoaded = true;

            // Désactiver le loading quand l'image est chargée
            if (loading != null)
            {
                loading.SetActive(false);
            }

            //Debug.Log(LOG_PREFIX + $"Atlas texture applied for card {cardKey}");
        }

        // Méthode appelée par le bouton sur la carte
        public void OnCardButtonPressed()
        {
            Debug.Log(LOG_PREFIX + $"Card button pressed for card {cardKey}");
            if (searchInterface != null)
            {
                searchInterface.OnCardPreviewRequest(cardKey);
            }
            else
            {
                Debug.LogWarning(LOG_PREFIX + "searchInterface n'est pas assigné sur la carte !");
            }
        }
    }
}