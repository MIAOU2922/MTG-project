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
        
        public void SetImage(int atlasIndex, Rect uvRect)
        {
            if (manager == null || atlasIndex < 0)
            {
                Debug.LogWarning($"Invalid atlas index {atlasIndex} or missing manager");
                return;
            }
            
            this.atlasIndex = atlasIndex;
            this.uvRect = uvRect;
            
            // Essayer d'obtenir l'atlas depuis le cache
            Texture2D atlasTexture = manager.GetAtlasTexture(atlasIndex);
            if (atlasTexture != null)
            {
                // Atlas déjà disponible
                ApplyAtlasTexture(atlasTexture);
            }
            else
            {
                // Atlas pas encore chargé, il sera chargé automatiquement par le manager
                // On vérifiera périodiquement dans Update()
            }
        }
        
        private void Update()
        {
            // Vérifier si l'atlas est maintenant disponible
            if (atlasIndex >= 0 && cardImage.texture == null && manager != null)
            {
                Texture2D atlasTexture = manager.GetAtlasTexture(atlasIndex);
                if (atlasTexture != null)
                {
                    ApplyAtlasTexture(atlasTexture);
                }
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