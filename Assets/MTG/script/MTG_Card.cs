using System;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;
using TMPro;
using System.Diagnostics;

namespace MTG
{
    public class MTG_Card : MTG_Base
    {
        [Header("=== REFERENCES ===")]
        public RawImage CardImageFront;
        public RawImage CardImageBack;
        public GameObject Loading;
        public GameObject FlipButton;
        public String CardKey = "";
        public String CardOracleKey = "";

        [Header("=== ATLAS DATA ===")]
        public Rect uvRectFront;
        public int AtlasIndexFront = -1;
        public Rect uvRectBack;
        public int AtlasIndexBack = -1;

        [Header("=== OTHER DATA ===")]
        [SerializeField] protected float LastRetryTime = 0f;
        [SerializeField] protected const float RETRY_INTERVAL = 10f;
        [SerializeField] protected bool ImageFrontLoaded = false;
        [SerializeField] protected bool ImageBackLoaded = false;
        [SerializeField] protected bool IsFlipped = false;
        [SerializeField] protected bool IsDoubleFaced = false;
        
        //methodes
        protected override void Update()
        {
            if (!ImageFrontLoaded && !String.IsNullOrEmpty(CardKey))
            {
                if (Time.time - LastRetryTime >= RETRY_INTERVAL)
                {
                    LastRetryTime = Time.time;
                    SetImageFromId();
                }
            }
        }
        // definie la carte a afficher via son id ( CardKey )
        public void SetCardKey(String _CardKey)
        {
            // Eviter de recharger si c'est la meme carte
            if (CardKey == _CardKey && ImageFrontLoaded)
            {
                this.VerboseLog($"SetCardKey: already loaded {_CardKey}, skipping");
                return;
            }
            
            this.VerboseLog($"SetCardKey called with key: {_CardKey}");
            CardKey = _CardKey;
            CardOracleKey = "";
            if (Loading != null)
                Loading.SetActive(true);
            
            ImageFrontLoaded = false;
            ImageBackLoaded = false;
            IsFlipped = false;
            IsDoubleFaced = false;
            LastRetryTime = -RETRY_INTERVAL;
            UpdateFlipVisibility();
        }
        public String GetCardKey()
        {
            this.VerboseLog($"GetCardKey called for cardKey: {CardKey}");
            return CardKey;
        }
        // definie l'oracle_id de la carte ( CardOracleKey )
        public void SetCardOracleKey(String _CardOracleKey)
        {
            this.VerboseLog($"SetCardOracleKey called with key: {_CardOracleKey}");
            CardOracleKey = _CardOracleKey;
        }
        public String GetCardOracleKey()
        {
            this.VerboseLog($"GetCardOracleKey called for cardKey: {CardKey}");
            return CardOracleKey;
        }
        // charge l'image de la carte a partir de son id ( CardKey )
        public virtual void SetImageFromId()
        {
            this.VerboseLog($"SetImageFromId called for cardKey: {CardKey}");
            if (!Manager || String.IsNullOrEmpty(CardKey)) return;

            // Extraire l'ID de base (sans le suffixe :0/:1 si present)
            String _BaseCardId = CardKey;
            if (_BaseCardId.Contains(":"))
            {
                String[] _Parts = _BaseCardId.Split(':');
                if (_Parts.Length == 2)
                    _BaseCardId = _Parts[0];
            }

            // Charger la face avant (face 0) - seulement si pas deja chargee
            String _FrontFaceKey = _BaseCardId + ":0";
            if (!ImageFrontLoaded && Manager.GetAtlasInfoForCard(_FrontFaceKey, out int _FoundAtlasIndexFront, out Rect _FoundRectFront))
            {
                AtlasIndexFront = _FoundAtlasIndexFront;
                uvRectFront = _FoundRectFront;
                
                Texture2D _AtlasTextureFront = Manager.GetAtlasTexture(AtlasIndexFront);
                if (_AtlasTextureFront != null)
                    ApplyAtlasTexture(_AtlasTextureFront, false);
            }

            // Charger la face arriere (face 1) - seulement si pas deja chargee
            String _BackFaceKey = _BaseCardId + ":1";
            if (!ImageBackLoaded && Manager.GetAtlasInfoForCard(_BackFaceKey, out int _FoundAtlasIndexBack, out Rect _FoundRectBack))
            {
                AtlasIndexBack = _FoundAtlasIndexBack;
                uvRectBack = _FoundRectBack;
                
                Texture2D _AtlasTextureBack = Manager.GetAtlasTexture(AtlasIndexBack);
                if (_AtlasTextureBack != null)
                {
                    IsDoubleFaced = true;
                    ApplyAtlasTexture(_AtlasTextureBack, true);
                }
            }
            else if (!ImageBackLoaded)
            {
                IsDoubleFaced = false;
            }

            UpdateFlipVisibility();
        }
        // applique la texture de l'atlas a l'image de la carte
        private void ApplyAtlasTexture(Texture2D _atlasTexture, bool _isBackFace)
        {
            this.VerboseLog($"ApplyAtlasTexture called for cardKey: {CardKey}");
            if (_isBackFace)
            {
                if (CardImageBack == null) return;
                
                CardImageBack.texture = _atlasTexture;
                CardImageBack.uvRect = uvRectBack;
                ImageBackLoaded = true;
            }
            else
            {
                if (CardImageFront == null) return;
                
                CardImageFront.texture = _atlasTexture;
                CardImageFront.uvRect = uvRectFront;
                ImageFrontLoaded = true;
            }
            if (ImageFrontLoaded && Loading != null)
            {
                Loading.SetActive(false);
            }
        }
        // met a jour la visibilite des images et du bouton flip
        public virtual void UpdateFlipVisibility()
        {
            if (CardImageFront != null)
                CardImageFront.gameObject.SetActive(!IsFlipped);

            if (CardImageBack != null)
                CardImageBack.gameObject.SetActive(IsFlipped);

            if (FlipButton != null)
                FlipButton.SetActive(IsDoubleFaced);
        }

        //methodes for buttons
        public void FlipCard()
        {
            this.VerboseLog($"FlipCard called for cardKey: {CardKey}");
            if (!IsDoubleFaced) return;
            IsFlipped = !IsFlipped;
            UpdateFlipVisibility();
        }

    }
}