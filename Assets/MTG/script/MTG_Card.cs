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
        [SerializeField] private float LastRetryTime = 0f;
        [SerializeField] private const float RETRY_INTERVAL = 10f;
        [SerializeField] private bool ImageFrontLoaded = false;
        [SerializeField] private bool ImageBackLoaded = false;
        [SerializeField] private bool IsFlipped = false;
        [SerializeField] private bool IsDoubleFaced = false;
        
        //methodes
        protected override void Update()
        {
            if (!ImageFrontLoaded || (IsDoubleFaced && !ImageBackLoaded))
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
            this.Log($"SetCardKey called with key: {_CardKey}");
            CardKey = _CardKey;
            if (Loading != null)
                Loading.SetActive(true);
            
            ImageFrontLoaded = false;
            ImageBackLoaded = false;
            IsFlipped = false;
            LastRetryTime = -RETRY_INTERVAL;
            SetImageFromId();
        }
        public String GetCardKey()
        {
            this.Log($"GetCardKey called for cardKey: {CardKey}");
            return CardKey;
        }
        // definie l'oracle_id de la carte ( CardOracleKey )
        public void SetCardOracleKey(String _CardOracleKey)
        {
            this.Log($"SetCardOracleKey called with key: {_CardOracleKey}");
            CardOracleKey = _CardOracleKey;
        }
        public String GetCardOracleKey()
        {
            this.Log($"GetCardOracleKey called for cardKey: {CardKey}");
            return CardOracleKey;
        }
        // charge l'image de la carte a partir de son id ( CardKey )
        public void SetImageFromId()
        {
            this.Log($"SetImageFromId called for cardKey: {CardKey}");
            if (!Manager || String.IsNullOrEmpty(CardKey)) return;
            String[] _Parts;
            String _FrontFaceKey,_FackFaceKey;
            String _BaseCardId = CardKey;
            int _FoundAtlasIndexFront,_FoundAtlasIndexBack;
            int _FaceIndex = 0;
            Rect _FoundRectFront,_FoundRectBack;
            Texture2D _AtlasTextureFront ,_AtlasTextureBack;
            if (CardKey.Contains(":"))
            {
                _Parts = CardKey.Split(':');
                if (_Parts.Length == 2)
                {
                    _BaseCardId = _Parts[0];
                    int.TryParse(_Parts[1], out _FaceIndex);
                }
            }
            // try load front face (face 0)
            _FrontFaceKey = _BaseCardId + ":0";
            if (Manager.GetAtlasInfoForCard(_FrontFaceKey, out _FoundAtlasIndexFront, out _FoundRectFront))
            {
                AtlasIndexFront = _FoundAtlasIndexFront;
                uvRectFront = _FoundRectFront;
                
                _AtlasTextureFront = Manager.GetAtlasTexture(AtlasIndexFront);
                if (_AtlasTextureFront == null) return;
                ApplyAtlasTexture(_AtlasTextureFront, false);
            }
            // try load back face (face 1)
            _FackFaceKey = _BaseCardId + ":1";
            if (Manager.GetAtlasInfoForCard(_FackFaceKey, out _FoundAtlasIndexBack, out _FoundRectBack))
            {
                AtlasIndexBack = _FoundAtlasIndexBack;
                uvRectBack = _FoundRectBack;
                IsDoubleFaced = true;
                
                _AtlasTextureBack = Manager.GetAtlasTexture(AtlasIndexBack);
                if (_AtlasTextureBack == null) return;
                ApplyAtlasTexture(_AtlasTextureBack, true);
            }
            else
            {
                IsDoubleFaced = false;
            }
            UpdateFlipVisibility();
        }
        // applique la texture de l'atlas a l'image de la carte
        private void ApplyAtlasTexture(Texture2D _atlasTexture, bool _isBackFace)
        {
            this.Log($"ApplyAtlasTexture called for cardKey: {CardKey}");
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
        public void UpdateFlipVisibility()
        {
            this.Log($"UpdateFlipVisibility called for cardKey: {CardKey}");
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
            this.Log($"FlipCard called for cardKey: {CardKey}");
            if (!IsDoubleFaced) return;
            IsFlipped = !IsFlipped;
            UpdateFlipVisibility();
        }

    }
}