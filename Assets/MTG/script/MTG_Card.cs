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
        public Texture2D CardImagePlaceholder;
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
        [SerializeField] protected const int MAX_RETRY_COUNT = 20; // Max 20 retries (3min20s pour 400 cartes)
        [SerializeField] protected int RetryCount = 0;
        [SerializeField] protected int AtlasLoadingRetryCount = 0; // Compteur separe pour atlas en chargement
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
                    
                    // Verifier si l'atlas est en cours de chargement
                    bool _AtlasIsLoading = IsAtlasCurrentlyLoading();
                    
                    if (_AtlasIsLoading)
                    {
                        // Atlas en cours de telechargement - ne pas compter comme retry
                        AtlasLoadingRetryCount++;
                        if (AtlasLoadingRetryCount % 3 == 0) // Log tous les 30 secondes
                        {
                            this.Log($"Waiting for atlas download... ({AtlasLoadingRetryCount * RETRY_INTERVAL}s) - {CardKey}");
                        }
                    }
                    else
                    {
                        // Atlas pas en chargement - compter comme retry normal
                        RetryCount++;
                        if (RetryCount >= MAX_RETRY_COUNT)
                        {
                            this.Error($"Failed to load card {CardKey} after {MAX_RETRY_COUNT} retries");
                            if (Loading != null) Loading.SetActive(false);
                            return;
                        }
                        this.Log($"Retry {RetryCount}/{MAX_RETRY_COUNT} loading card {CardKey}");
                    }
                    
                    SetImageFromId();
                }
            }
        }
        
        // Verifie si l'atlas de cette carte est en cours de telechargement
        private bool IsAtlasCurrentlyLoading()
        {
            if (!Manager || AtlasIndexFront < 0) return false;
            return Manager.IsAtlasLoading(AtlasIndexFront);
        }
        // definie la carte a afficher via son id ( CardKey )
        public void SetCardKey(String _CardKey)
        {
            this.Log($"SetCardKey called with key: {_CardKey}");
            // Eviter de recharger si c'est la meme carte
            if (CardKey == _CardKey && ImageFrontLoaded)
            {
                this.VerboseLog($"SetCardKey: already loaded {_CardKey}, skipping");
                return;
            }
            
            // Si la cle est vide ou DEBUG, afficher le placeholder
            if (string.IsNullOrEmpty(_CardKey) || _CardKey == "DEBUG")
            {
                CardKey = _CardKey;
                CardOracleKey = "";
                ImageFrontLoaded = true;  // Marque comme charge pour eviter les retry
                ImageBackLoaded = false;
                IsFlipped = false;
                IsDoubleFaced = false;
                LastRetryTime = -RETRY_INTERVAL;
                RetryCount = 0;
                AtlasLoadingRetryCount = 0;
                UpdateFlipVisibility();

                // Appliquer le placeholder
                if (CardImageFront != null)
                {
                    if (CardImagePlaceholder != null)
                    {
                        CardImageFront.texture = CardImagePlaceholder;
                    }
                    CardImageFront.uvRect = new Rect(0, 0, 1, 1);
                }
                if (Loading != null) Loading.SetActive(false);
                return;
            }
            
            CardKey = _CardKey;
            CardOracleKey = "";
            if (Loading != null)
                Loading.SetActive(true);
            
            ImageFrontLoaded = false;
            ImageBackLoaded = false;
            IsFlipped = false;
            IsDoubleFaced = false;
            LastRetryTime = -RETRY_INTERVAL;
            RetryCount = 0; // Reset retry counter
            AtlasLoadingRetryCount = 0; // Reset atlas loading counter
            UpdateFlipVisibility();
            
            // IMPORTANT: Demarrer le chargement de l'image immediatement
            if (!string.IsNullOrEmpty(CardKey) && Manager != null)
            {
                SetImageFromId();
            }
        }
        public String GetCardKey()
        {
            this.VerboseLog($"GetCardKey called for cardKey: {CardKey}");
            return CardKey;
        }
        public void ResetCardKey()
        {
            SetCardKey("");
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
            if (!Manager || String.IsNullOrEmpty(CardKey))
            {
                this.VerboseLog($"SetImageFromId: Manager or CardKey is null/empty");
                return;
            }

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
            if (!ImageFrontLoaded)
            {
                bool _FoundFront = Manager.GetAtlasInfoForCard(_FrontFaceKey, out int _FoundAtlasIndexFront, out Rect _FoundRectFront);
                if (_FoundFront)
                {
                    AtlasIndexFront = _FoundAtlasIndexFront;
                    uvRectFront = _FoundRectFront;
                    
                    Texture2D _AtlasTextureFront = Manager.GetAtlasTexture(AtlasIndexFront);
                    if (_AtlasTextureFront != null)
                    {
                        this.Log($"Loaded front face for {_FrontFaceKey} from atlas {_FoundAtlasIndexFront}");
                        ApplyAtlasTexture(_AtlasTextureFront, false);
                    }
                    else
                    {
                        // Atlas trouve mais texture pas encore telechargee
                        bool _IsLoading = Manager.IsAtlasLoading(AtlasIndexFront);
                        if (_IsLoading)
                        {
                            this.VerboseLog($"Atlas {AtlasIndexFront} downloading... (card {_FrontFaceKey})");
                        }
                        else
                        {
                            this.VerboseLog($"Atlas {AtlasIndexFront} queued for download (card {_FrontFaceKey})");
                        }
                    }
                }
            }

            // Charger la face arriere (face 1) - seulement si pas deja chargee
            String _BackFaceKey = _BaseCardId + ":1";
            if (!ImageBackLoaded)
            {
                bool _FoundBack = Manager.GetAtlasInfoForCard(_BackFaceKey, out int _FoundAtlasIndexBack, out Rect _FoundRectBack);
                if (_FoundBack)
                {
                    this.Log($"Found back face for {_BackFaceKey} in atlas {_FoundAtlasIndexBack}");
                    AtlasIndexBack = _FoundAtlasIndexBack;
                    uvRectBack = _FoundRectBack;
                    
                    Texture2D _AtlasTextureBack = Manager.GetAtlasTexture(AtlasIndexBack);
                    if (_AtlasTextureBack != null)
                    {
                        IsDoubleFaced = true;
                        ApplyAtlasTexture(_AtlasTextureBack, true);
                    }
                    else
                    {
                        this.VerboseLog($"Atlas texture {AtlasIndexBack} not loaded yet for back face");
                    }
                }
                else
                {
                    this.VerboseLog($"No back face found for {_BackFaceKey} (single-faced card)");
                    IsDoubleFaced = false;
                }
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