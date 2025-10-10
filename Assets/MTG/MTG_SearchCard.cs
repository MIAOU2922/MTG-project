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
        public TextMeshProUGUI nameText;
        public RawImage cardImage;
        public int cardID;
        public string setCode;
        public string cardKey;
        
        [System.NonSerialized]
        public MTG_Manager manager;
        
        private Rect uvRect;
        private int atlasIndex = -1;
        
        internal void SetData(DataDictionary cardDict)
        {
            if (cardDict == null) return;
            
            // Récupérer l'ID unique de la carte
            if (cardDict.ContainsKey("id") && cardDict["id"].TokenType == TokenType.String)
            {
                cardKey = cardDict["id"].String;
            }
            else
            {
                cardKey = "";
            }
            
            // Récupérer set et collector_number pour debug/info si besoin
            if (cardDict.ContainsKey("set") && cardDict["set"].TokenType == TokenType.String)
            {
                setCode = cardDict["set"].String;
            }
            else
            {
                setCode = "";
            }
            
            if (cardDict.ContainsKey("collector_number") && cardDict["collector_number"].TokenType == TokenType.String)
            {
                if (!int.TryParse(cardDict["collector_number"].String, out cardID))
                {
                    cardID = 0;
                }
            }
            else
            {
                cardID = 0;
            }
        }
        
        public void SetImageFromId()
        {
            if (manager == null || string.IsNullOrEmpty(cardKey))
            {
                Debug.LogWarning($"Missing manager or cardKey");
                return;
            }
            int foundAtlasIndex;
            Rect foundRect;
            if (manager.GetAtlasInfoForCard(cardKey, out foundAtlasIndex, out foundRect))
            {
                this.atlasIndex = foundAtlasIndex;
                this.uvRect = foundRect;
                Texture2D atlasTexture = manager.GetAtlasTexture(atlasIndex);
                if (atlasTexture != null)
                {
                    ApplyAtlasTexture(atlasTexture);
                }
            }
            else
            {
                Debug.LogWarning($"Atlas info not found for card {cardKey}");
            }
        }
        
        private void Update()
        {
            // Si on n'a pas encore d'image, essayer de la récupérer via le manager
            if (cardImage.texture == null && manager != null)
            {
                SetImageFromId();
            }
        }
        
        private void ApplyAtlasTexture(Texture2D atlasTexture)
        {
            cardImage.texture = atlasTexture;
            cardImage.uvRect = uvRect;
            Debug.Log($"Atlas texture applied for card {cardKey}");
        }
    }
}