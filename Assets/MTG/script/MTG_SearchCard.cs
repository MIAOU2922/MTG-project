using System;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;
using TMPro;

namespace MTG
{
    public class MTG_SearchCard : MTG_Card
    {
    
        [Header("=== SEARCH CARD DATA ===")]
        public  MTG_SearchInterface SearchInterface;

        // methodes
        // recherche le search interface dans la scene
        private void TryFindSearchInterface()
        {
            GameObject _SearchInterfaceObj = GameObject.Find("Search Interface");
            if (_SearchInterfaceObj == null) return;
            SearchInterface = _SearchInterfaceObj.GetComponent< MTG_SearchInterface>();
            if (SearchInterface == null) return;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (SearchInterface == null) TryFindSearchInterface();
        }
#endif
        protected override void Start()
        {
            base.Start();
            if (SearchInterface == null) TryFindSearchInterface();
        }
        // methodes for buttons
        // demande l'affichage de l'apercu de la carte
        public void OnCardButtonPressed()
        {
            if (SearchInterface == null) return;
            SearchInterface.OnCardPreviewRequest(CardKey);
        }

        // Configure la carte a partir des donnees JSON de recherche
        // REPREND EXACTEMENT le comportement de l'ancien script qui fonctionnait
        public void SetData(DataDictionary _CardDict)
        {
            if (_CardDict == null) return;

            // Recuperer l'ID unique de la carte (UUID Scryfall)
            if (_CardDict.TryGetValue("id", out DataToken _IdToken))
            {
                CardKey = _IdToken.String;
            }
            else
            {
                CardKey = "";
            }

            // Recuperer l'oracle_id si present
            if (_CardDict.TryGetValue("oracle_id", out DataToken _OracleToken))
            {
                CardOracleKey = _OracleToken.String;
            }

            // Activer le loading
            if (Loading != null)
                Loading.SetActive(true);

            // Reinitialiser les flags
            ImageFrontLoaded = false;
            ImageBackLoaded = false;
            IsFlipped = false;
            IsDoubleFaced = false;
            LastRetryTime = -RETRY_INTERVAL;

            // Mettre a jour la visibilite initiale
            UpdateFlipVisibility();
        }

        // === OVERRIDE: charge l'image via CardKey uniquement (comportement ancien script) ===
        public override void SetImageFromId()
        {
            if (Manager == null || string.IsNullOrEmpty(CardKey)) return;

            // Extraire l'ID de base (sans suffixe :0/:1)
            string _BaseCardId = CardKey;
            if (_BaseCardId.Contains(":"))
            {
                string[] _Parts = _BaseCardId.Split(':');
                if (_Parts.Length == 2)
                    _BaseCardId = _Parts[0];
            }

            // Face avant (face 0)
            string _FrontFaceKey = _BaseCardId + ":0";
            if (!ImageFrontLoaded && Manager.GetAtlasInfoForCard(_FrontFaceKey, out int _FoundAtlasIndexFront, out Rect _FoundRectFront))
            {
                AtlasIndexFront = _FoundAtlasIndexFront;
                uvRectFront = _FoundRectFront;

                Texture2D _AtlasTexture = Manager.GetAtlasTexture(AtlasIndexFront);
                if (_AtlasTexture != null)
                    ApplyAtlasTextureFront(_AtlasTexture);
            }

            // Face arriere (face 1)
            string _BackFaceKey = _BaseCardId + ":1";
            if (!ImageBackLoaded && Manager.GetAtlasInfoForCard(_BackFaceKey, out int _FoundAtlasIndexBack, out Rect _FoundRectBack))
            {
                AtlasIndexBack = _FoundAtlasIndexBack;
                uvRectBack = _FoundRectBack;

                Texture2D _AtlasTextureBack = Manager.GetAtlasTexture(AtlasIndexBack);
                if (_AtlasTextureBack != null)
                {
                    IsDoubleFaced = true;
                    ApplyAtlasTextureBack(_AtlasTextureBack);
                }
            }
            else if (!ImageBackLoaded)
            {
                IsDoubleFaced = false;
            }

            UpdateFlipVisibility();
        }

        private void ApplyAtlasTextureFront(Texture2D _AtlasTexture)
        {
            if (CardImageFront == null) return;
            CardImageFront.texture = _AtlasTexture;
            CardImageFront.uvRect = uvRectFront;
            ImageFrontLoaded = true;

            if (Loading != null)
                Loading.SetActive(false);
        }

        private void ApplyAtlasTextureBack(Texture2D _AtlasTexture)
        {
            if (CardImageBack == null) return;
            CardImageBack.texture = _AtlasTexture;
            CardImageBack.uvRect = uvRectBack;
            ImageBackLoaded = true;
        }

        // === OVERRIDE: retry uniquement si face avant pas chargee (comportement ancien script) ===
        protected override void Update()
        {
            if (!ImageFrontLoaded && Manager != null && !string.IsNullOrEmpty(CardKey))
            {
                if (Time.time - LastRetryTime >= RETRY_INTERVAL)
                {
                    LastRetryTime = Time.time;
                    SetImageFromId();
                }
            }
        }

        // === OVERRIDE: visibilite sans log (comportement ancien script) ===
        public override void UpdateFlipVisibility()
        {
            if (CardImageFront != null)
                CardImageFront.gameObject.SetActive(!IsFlipped);

            if (CardImageBack != null)
                CardImageBack.gameObject.SetActive(IsFlipped);

            if (FlipButton != null)
                FlipButton.SetActive(IsDoubleFaced);
        }
    }
}