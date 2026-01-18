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
    public class MTG_DeckCard_old : UdonSharpBehaviour
    {
        // Préfixe coloré pour les logs
        private const string LOG_PREFIX = "<color=#FF1493>[MTG_DeckCard]</color> ";
        
        public RawImage cardImage; // Face avant
        public RawImage cardImageBack; // Face arrière
        public GameObject loading;
        public GameObject flipButton; // Bouton pour flip la carte
        public string cardKey;
        public MTG_Manager manager;
        public MTG_Deckinterface deckInterface;
        public TextMeshProUGUI countText; // Texte pour afficher la quantité

        public Rect uvRect;
        public int atlasIndex = -1;
        public Rect uvRectBack; // UV pour la face arrière
        public int atlasIndexBack = -1; // Atlas pour la face arrière
        public float lastRetryTime = 0f;
        public const float RETRY_INTERVAL = 10f; // Retry toutes les 10 secondes
        private bool imageLoaded = false;
        private bool imageBackLoaded = false;
        private bool isFlipped = false; // État actuel de la carte
        private bool isDoubleFaced = false; // La carte a-t-elle deux faces?
        private int cardCount = 1; // Nombre de cette carte dans le deck
        
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
            imageBackLoaded = false;
            isFlipped = false;
            lastRetryTime = -RETRY_INTERVAL; // Permet un retry immédiat au prochain Update
            
            // Initialiser le count si présent dans les données
            if (cardDict.ContainsKey("count") && cardDict["count"].TokenType == TokenType.Double)
            {
                cardCount = (int)cardDict["count"].Double;
            }
            else
            {
                cardCount = 1; // Par défaut, 1 exemplaire
            }
            
            UpdateCountDisplay();
            
            // Mettre à jour la visibilité
            UpdateFlipVisibility();
        }
        
        public void SetImageFromId()
        {
            if (manager == null || string.IsNullOrEmpty(cardKey))
            {
                Debug.LogWarning(LOG_PREFIX + $"SetImageFromId: Missing manager ({manager != null}) or cardKey ({!string.IsNullOrEmpty(cardKey)})");
                return;
            }
            
            //Debug.Log(LOG_PREFIX + $"SetImageFromId: Searching for card {cardKey} in atlas cache");
            
            // Extraire l'ID de base et l'index de face depuis le cardKey (format: "cardId:0" ou "cardId:1")
            string baseCardId = cardKey;
            int faceIndex = 0;
            if (cardKey.Contains(":"))
            {
                string[] parts = cardKey.Split(':');
                if (parts.Length == 2)
                {
                    baseCardId = parts[0];
                    int.TryParse(parts[1], out faceIndex);
                }
            }
            
            // Charger la face avant (face 0)
            string frontFaceKey = baseCardId + ":0";
            int foundAtlasIndex;
            Rect foundRect;
            if (manager.GetAtlasInfoForCard(frontFaceKey, out foundAtlasIndex, out foundRect))
            {
                //Debug.Log(LOG_PREFIX + $"SetImageFromId: Found front face {frontFaceKey} in atlas {foundAtlasIndex}");
                this.atlasIndex = foundAtlasIndex;
                this.uvRect = foundRect;
                
                Texture2D atlasTexture = manager.GetAtlasTexture(atlasIndex);
                if (atlasTexture != null)
                {
                    //Debug.Log(LOG_PREFIX + $"SetImageFromId: Atlas texture ready for front face, applying now");
                    ApplyAtlasTexture(atlasTexture, false);
                }
            }
            
            // Tenter de charger la face arrière (face 1)
            string backFaceKey = baseCardId + ":1";
            int foundAtlasIndexBack;
            Rect foundRectBack;
            if (manager.GetAtlasInfoForCard(backFaceKey, out foundAtlasIndexBack, out foundRectBack))
            {
                //Debug.Log(LOG_PREFIX + $"SetImageFromId: Found back face {backFaceKey} in atlas {foundAtlasIndexBack}");
                this.atlasIndexBack = foundAtlasIndexBack;
                this.uvRectBack = foundRectBack;
                this.isDoubleFaced = true;
                
                Texture2D atlasTextureBack = manager.GetAtlasTexture(atlasIndexBack);
                if (atlasTextureBack != null)
                {
                    //Debug.Log(LOG_PREFIX + $"SetImageFromId: Atlas texture ready for back face, applying now");
                    ApplyAtlasTexture(atlasTextureBack, true);
                }
            }
            else
            {
                // Pas de face arrière = carte simple face
                this.isDoubleFaced = false;
            }
            
            // Mettre à jour la visibilité du bouton flip
            UpdateFlipVisibility();
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

        private void ApplyAtlasTexture(Texture2D atlasTexture, bool isBackFace)
        {
            if (isBackFace)
            {
                if (cardImageBack == null) return;
                
                cardImageBack.texture = atlasTexture;
                cardImageBack.uvRect = uvRectBack;
                imageBackLoaded = true;
            }
            else
            {
                if (cardImage == null) return;
                
                cardImage.texture = atlasTexture;
                cardImage.uvRect = uvRect;
                imageLoaded = true;
            }

            // Désactiver le loading quand au moins la face avant est chargée
            if (imageLoaded && loading != null)
            {
                loading.SetActive(false);
            }

            //Debug.Log(LOG_PREFIX + $"Atlas texture applied for {(isBackFace ? "back" : "front")} face");
        }
        
        // Méthode pour flip la carte (appelée par le bouton)
        public void FlipCard()
        {
            if (!isDoubleFaced)
            {
                //Debug.Log(LOG_PREFIX + "Cannot flip: card is single-faced");
                return;
            }
            
            isFlipped = !isFlipped;
            UpdateFlipVisibility();
            //Debug.Log(LOG_PREFIX + $"Card flipped to {(isFlipped ? "back" : "front")} face");
        }
        
        // Met à jour la visibilité des faces et du bouton
        private void UpdateFlipVisibility()
        {
            if (cardImage != null)
            {
                cardImage.gameObject.SetActive(!isFlipped);
            }
            
            if (cardImageBack != null)
            {
                cardImageBack.gameObject.SetActive(isFlipped);
            }
            
            // Afficher le bouton flip seulement si la carte a deux faces
            if (flipButton != null)
            {
                flipButton.SetActive(isDoubleFaced);
            }
        }

        // Méthode appelée par le bouton sur la carte
        public void OnCardButtonPressed()
        {
            Debug.Log(LOG_PREFIX + $"Deck card button pressed for card {cardKey}");
            if (deckInterface != null)
            {
                deckInterface.OnCardButtonPressed(cardKey);
            }
            else
            {
                Debug.LogWarning(LOG_PREFIX + "deckInterface n'est pas assigné sur la carte !");
            }
        }
        
        // Ajouter 1 exemplaire de cette carte
        public void AddOne()
        {
            cardCount++;
            UpdateCountDisplay();
            Debug.Log(LOG_PREFIX + $"Added one {cardKey}, now {cardCount}");
            
            // Notifier le deck interface du changement
            if (deckInterface != null)
            {
                deckInterface.OnCardCountChanged(cardKey, cardCount);
            }
        }
        
        // Retirer 1 exemplaire de cette carte
        public void RemoveOne()
        {
            if (cardCount > 0)
            {
                cardCount--;
                UpdateCountDisplay();
                Debug.Log(LOG_PREFIX + $"Removed one {cardKey}, now {cardCount}");
                
                // Notifier le deck interface du changement
                if (deckInterface != null)
                {
                    deckInterface.OnCardCountChanged(cardKey, cardCount);
                }
            }

            // Si le count atteint 0, supprimer le GameObject
            if (cardCount == 0)
            {
                Debug.Log(LOG_PREFIX + $"Card {cardKey} count reached 0, destroying GameObject");
                
                // Notifier le deck interface avant destruction
                if (deckInterface != null)
                {
                    deckInterface.OnCardRemoved(cardKey);
                }
                
                Destroy(gameObject);
            }
        }
        
        // Définir le nombre exact
        public void SetCount(int count)
        {
            cardCount = Mathf.Max(0, count);
            UpdateCountDisplay();
        }
        
        // Obtenir le nombre actuel
        public int GetCount()
        {
            return cardCount;
        }
        
        // Mettre à jour l'affichage du nombre
        private void UpdateCountDisplay()
        {
            if (countText != null)
            {
                countText.text = cardCount.ToString();
            }
        }
    }
}
