using System;
using System.Text;
using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Data;
using VRC.SDK3.Components;
using TMPro;

namespace MTG
{
    public class MTG_SearchInterface : MTG_Interface
    {

        [Header("=== INPUT FIELDS ===")]
        public TMPro.TMP_InputField CardNameInput;
        public TMPro.TMP_InputField CardTextInput;
        public TMPro.TMP_InputField TypeLineInput;
        public TMPro.TMP_InputField CmcInput;

        [Header("=== DROPDOWNS ===")]
        public TMPro.TMP_Dropdown SetDropdown;
        public string[] SetDropdownCodes; // index 0 = "" (pas de filtre), puis les codes set (ex: khm, m21...)
        public TMPro.TMP_Dropdown LangDropdown;
        public string[] LangDropdownCodes; // index 0 = "" (pas de filtre), puis les codes langue (ex: en, fr, es...)

        [Header("=== VRC URL INPUT ===")]
        public VRCUrlInputField ValidatedUrlField;
        public TMPro.TMP_InputField DisplayUrlField;

        [Header("=== COLOR BUTTONS ===")]
        public UnityEngine.UI.Button ColorW_Button;
        public UnityEngine.UI.Button ColorU_Button;
        public UnityEngine.UI.Button ColorB_Button;
        public UnityEngine.UI.Button ColorR_Button;
        public UnityEngine.UI.Button ColorG_Button;
        public UnityEngine.UI.Button ColorC_Button;

        [Header("=== RARITY BUTTONS ===")]
        public UnityEngine.UI.Button RarityCommon_Button;
        public UnityEngine.UI.Button RarityUncommon_Button;
        public UnityEngine.UI.Button RarityRare_Button;
        public UnityEngine.UI.Button RarityMythic_Button;

        [Header("=== OTHER BUTTONS ===")]
        public UnityEngine.UI.Button ClearSearch_Button;
        public UnityEngine.UI.Button SpawnCard_Button;
        public UnityEngine.UI.Button AddToDeck_Button;

        [Header("=== POOL SETTINGS ===")]
        [Tooltip("Nombre maximum de cartes dans l'interface de recherche (pool reusable).")]
        public int MaxSearchCards = 256;

        [Header("=== PHYSIC CARD SPAWN ===")]
        [Tooltip("Point de spawn pour les cartes physiques. Si vide, spawn devant le joueur.")]
        public Transform PhysicCardSpawnPoint;

        //states
        private bool ColorW_Selected = false;
        private bool ColorU_Selected = false;
        private bool ColorB_Selected = false;
        private bool ColorR_Selected = false;
        private bool ColorG_Selected = false;
        private bool ColorC_Selected = false;
        private bool RarityCommon_Selected = false;
        private bool RarityUncommon_Selected = false;
        private bool RarityRare_Selected = false;
        private bool RarityMythic_Selected = false;

        // Progressive card loading
        private DataList CardsToLoad;
        private int CurrentLoadIndex = 0;
        private float NextLoadTime = 0f;
        private GameObject[] InstantiatedCards = new GameObject[256];
        private int InstantiatedCardsCount = 0;
        private string[] CardKeys = new string[256];
        private int CardKeysCount = 0;
        private const float LOAD_INTERVAL = 0.15f;
        private const int CARDS_PER_BATCH = 8;
        private GameObject[] CardPool;
        private bool PoolInitialized = false;
        private string PendingJsonData = null;

        // Progressive pool initialization (evite crash VM Udon)
        private bool PoolInitInProgress = false;
        private int PoolInitPhase = 0;      // 0=cleanup, 1=instantiate
        private int PoolInitIndex = 0;
        private float PoolInitNextTime = 0f;
        private const float POOL_INIT_INTERVAL = 0.1f;
        private const int POOL_INIT_BATCH = 32;

        // Progressive set loading for dropdown
        private DataList SetsToLoad;
        private int CurrentSetLoadIndex = 0;
        private float NextSetLoadTime = 0f;
        private bool IsLoadingSets = false;
        private const float SET_LOAD_INTERVAL = 0.05f;
        private const int SETS_PER_BATCH = 50;

        //methodes
#if !COMPILER_UDONSHARP && UNITY_EDITOR

        // validation dans l'editeur
        protected virtual void OnValidate()
        {
            base.OnValidate();
            //ForceInitializeCardPool();
        }
#endif
        protected override void Start()
        {
            base.Start();
            VRCStringDownloader.LoadUrl(Manager.TempURLs[2], (IUdonEventReceiver)this);
        }

        [ContextMenu("Force Initialize Card Pool")]
        private void ForceInitializeCardPool()
        {
            if (PoolInitialized) return;
            PoolInitInProgress = true;
            PoolInitPhase = 0;
            PoolInitIndex = 0;
            PoolInitNextTime = Time.time;
        }

        // Initialisation progressive du pool (appelee depuis Update)
        private void ProcessPoolInit()
        {
            if (!PoolInitInProgress || Time.time < PoolInitNextTime) return;

            // Phase 0: detruire les anciens enfants par lots
            if (PoolInitPhase == 0)
            {
                if (CardsParent == null)
                {
                    this.Error("CardsParent is null, cannot init pool");
                    PoolInitInProgress = false;
                    return;
                }
                if (CardsParent.childCount == 0)
                {
                    PoolInitPhase = 1;
                    PoolInitIndex = 0;
                    PoolInitNextTime = Time.time;
                    return;
                }
                GameObject _OldChild = CardsParent.GetChild(0).gameObject;
                Destroy(_OldChild);
                PoolInitNextTime = Time.time + 0.02f;
                return;
            }

            // Phase 1: instancier les cartes du pool par lots
            if (PoolInitPhase == 1)
            {
                if (CardPrefab == null)
                {
                    this.Error("CardPrefab is null, cannot init pool");
                    PoolInitInProgress = false;
                    return;
                }
                if (PoolInitIndex == 0)
                {
                    CardPool = new GameObject[MaxSearchCards];
                    if (CardPrefab.activeSelf)
                        CardPrefab.SetActive(false);
                }
                int _End = Mathf.Min(PoolInitIndex + POOL_INIT_BATCH, MaxSearchCards);
                for (int i = PoolInitIndex; i < _End; i++)
                {
                    CardPool[i] = Instantiate(CardPrefab, CardsParent, false);
                }
                PoolInitIndex = _End;
                if (PoolInitIndex >= MaxSearchCards)
                {
                    CardPrefab.SetActive(true);
                    PoolInitInProgress = false;
                    PoolInitialized = true;
                    this.Log($"Card pool initialized with {MaxSearchCards} cards (progressive)");
                }
                else
                {
                    PoolInitNextTime = Time.time + POOL_INIT_INTERVAL;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            // Initialisation progressive du pool de cartes
            if (PoolInitInProgress)
            {
                ProcessPoolInit();
                return; // ne rien faire d'autre tant que le pool n'est pas pret
            }

            // Chargement progressif des sets dans le dropdown
            if (IsLoadingSets && Time.time >= NextSetLoadTime)
            {
                LoadNextBatchOfSets();
            }

            if (!string.IsNullOrEmpty(PendingJsonData))
            {
                ParseAndQueueCards();
                return; // ne rien faire d'autre ce frame
            }

            // Chargement progressif des nouvelles cartes
            if (CardsToLoad != null && CurrentLoadIndex < CardsToLoad.Count && Time.time >= NextLoadTime)
            {
                LoadNextBatchOfCards();
            }
        }
        //placeholder pour GenerateUrl pour les element UI
        public void OnEndEdit()
        {
            this.Log("OnEndEdit called");
            GenerateUrl();
        }

        // Envoie la requete de recherche via le Manager (centralise)
        public void SendSearchRequest()
        {
            this.Log("SendSearchRequest called");

            VRCUrl _UrlToSend = null;

            // Essayer d'abord avec ValidatedUrlField (specifique a SearchInterface)
            if (ValidatedUrlField != null)
            {
                _UrlToSend = ValidatedUrlField.GetUrl();
            }
            // Sinon utiliser ValidatedInput (herite de MTG_Interface)
            if (_UrlToSend == null && ValidatedInput != null)
            {
                _UrlToSend = ValidatedInput.GetUrl();
            }

            if (_UrlToSend == null || string.IsNullOrEmpty(_UrlToSend.ToString()))
            {
                this.Error("No valid URL to send - please validate the URL first");
                return;
            }

            string _UrlString = _UrlToSend.ToString();
            this.Log($"Sending search request directly: {_UrlString}");

            // Envoi direct vers SearchInterface (evite le probleme de liaison Manager->SearchInterface)
            VRCStringDownloader.LoadUrl(_UrlToSend, (IUdonEventReceiver)this);
        }

        // OnStringLoadSuccess specifique a SearchInterface (recoit les reponses de recherche ET set list)
        public override void OnStringLoadSuccess(IVRCStringDownload _Json)
        {
            this.Log("MTG_SearchInterface.OnStringLoadSuccess called");
            if (_Json == null || _Json.Url == null || _Json.Result == null)
            {
                this.Error("Response or URL is null");
                return;
            }

            // Dispatch: TempURLs[2] = set list, sinon = search response
            if (Manager != null && Manager.TempURLs != null && _Json.Url == Manager.TempURLs[2])
            {
                this.Log("Dispatching to ProcessSetListResponse");
                ProcessSetListResponse(_Json);
            }
            else
            {
                this.Log("Dispatching to OnSearchResponse");
                OnSearchResponse(_Json);
            }
        }

        // Override de OnUrlValidated pour valider que l'URL est bien une URL de recherche
        public override void OnUrlValidated()
        {
            this.Log("OnUrlValidated called in SearchInterface");
            
            VRCUrl _ValidatedUrl = null;

            // Recuperer l'URL depuis le champ valide (ValidatedUrlField ou ValidatedInput)
            if (ValidatedUrlField != null)
                _ValidatedUrl = ValidatedUrlField.GetUrl();
            if (_ValidatedUrl == null && ValidatedInput != null)
                _ValidatedUrl = ValidatedInput.GetUrl();

            if (_ValidatedUrl == null)
            {
                this.Error("Validated URL is null");
                return;
            }
            
            string _UrlString = _ValidatedUrl.ToString();
            this.Log($"Validating search URL: {_UrlString}");
            
            // Verifier que l'URL est bien une URL de recherche
            if (Manager == null || Manager.SearchURL == null)
            {
                this.Error("Manager or SearchURL is null");
                return;
            }
            
            string _SearchUrlBase = Manager.SearchURL.ToString();
            if (!_UrlString.StartsWith(_SearchUrlBase))
            {
                this.Error($"Invalid search URL. Must start with: {_SearchUrlBase}");
                this.Error($"Current URL: {_UrlString}");
                return;
            }
            
            // Verifier qu'il y a bien des parametres de recherche
            if (!_UrlString.Contains("?"))
            {
                this.Error("Search URL must contain query parameters after '?'");
                return;
            }
            
            this.Log("Search URL validated, sending request directly...");
            
            // Envoi direct vers SearchInterface (evite le probleme de liaison Manager->SearchInterface)
            VRCStringDownloader.LoadUrl(_ValidatedUrl, (IUdonEventReceiver)this);
        }

        // callback pour les reponses de recherche
        public override void OnSearchResponse(IVRCStringDownload _Json)
        {
            this.Log("OnSearchResponse called in MTG_SearchInterface");
            if (_Json == null || _Json.Result == null)
            {
                this.Error("Search response is null");
                return;
            }
            // Traitement de la reponse de recherche
            ProcessSearchResponse(_Json);
        }

        private void ProcessSearchResponse(IVRCStringDownload _Json)
        {
            this.Log("ProcessSearchResponse called - deferring JSON parse");
            if (_Json == null || _Json.Result == null)
            {
                this.Error("Search response or result is null");
                return;
            }

            // Stocker le JSON brut pour parsing differe dans Update()
            // (evite depassement du budget temps VM Udon)
            PendingJsonData = _Json.Result;

            // Supprimer les anciennes cartes (operation legere, juste des flags)
            ClearAllCards();
        }

        // Traite la reponse de la liste des sets (TempURLs[2]) et remplit le SetDropdown
        // Format JSON: { "count": N, "sets": ["Nom du set (code)", ...] }
        private void ProcessSetListResponse(IVRCStringDownload _Json)
        {
            this.Log("ProcessSetListResponse called");
            if (_Json == null || _Json.Result == null)
            {
                this.Error("Set list response is null");
                return;
            }
            string _JsonData = _Json.Result;
            if (!VRCJson.TryDeserializeFromJson(_JsonData, out DataToken _Token))
            {
                this.Error("Error parsing set list JSON");
                return;
            }
            if (_Token.TokenType != TokenType.DataDictionary)
            {
                this.Error("Set list response is not a DataDictionary");
                return;
            }
            DataDictionary _RootDict = _Token.DataDictionary;

            // Naviguer: root → "data"
            if (!_RootDict.TryGetValue("data", out _Token) || _Token.TokenType != TokenType.DataDictionary)
            {
                this.Error("Invalid or missing 'data' in set list response");
                return;
            }
            DataDictionary _DataDict = _Token.DataDictionary;

            // Lire count et sets dans "data"
            int _TotalCount = 0;
            if (_DataDict.TryGetValue("count", out DataToken _CountToken))
                _TotalCount = (int)_CountToken.Double;

            if (!_DataDict.TryGetValue("sets", out _Token) || _Token.TokenType != TokenType.DataList)
            {
                this.Error("Invalid or missing 'sets' in set list response");
                return;
            }
            DataList _SetsList = _Token.DataList;
            this.Log($"Set list: {_SetsList.Count} entries (total: {_TotalCount})");

            if (SetDropdown == null)
            {
                this.Error("SetDropdown is null, cannot populate");
                return;
            }

            // Compter les entrees valides et preparer SetDropdownCodes
            int _ValidCount = 1; // +1 pour "(no filter)"
            for (int i = 0; i < _SetsList.Count; i++)
            {
                if (_SetsList[i].TokenType == TokenType.String)
                    _ValidCount++;
            }

            string[] _NewCodes = new string[_ValidCount];
            _NewCodes[0] = ""; // index 0 = pas de filtre

            // Extraire les codes pour SetDropdownCodes
            int _CodeIdx = 1;
            for (int i = 0; i < _SetsList.Count; i++)
            {
                if (_SetsList[i].TokenType != TokenType.String) continue;
                string _Entry = _SetsList[i].String;

                // Parser "Nom du set (code)"
                string _SetCode = "";
                int _ParenOpen = _Entry.LastIndexOf('(');
                int _ParenClose = _Entry.LastIndexOf(')');
                if (_ParenOpen >= 0 && _ParenClose > _ParenOpen)
                {
                    _SetCode = _Entry.Substring(_ParenOpen + 1, _ParenClose - _ParenOpen - 1);
                }
                _NewCodes[_CodeIdx] = _SetCode;
                _CodeIdx++;
            }
            SetDropdownCodes = _NewCodes;

            // Initialiser le dropdown avec juste "(no filter)"
            SetDropdown.ClearOptions();
            string[] _InitialOptions = new string[1];
            _InitialOptions[0] = "(no filter)";
            SetDropdown.AddOptions(_InitialOptions);

            // Demarrer le chargement progressif
            SetsToLoad = _SetsList;
            CurrentSetLoadIndex = 0;
            IsLoadingSets = true;
            NextSetLoadTime = Time.time + SET_LOAD_INTERVAL;

            this.Log($"Set dropdown init with {_ValidCount} total sets, loading progressively");
        }

        // Chargement progressif des sets dans le dropdown par batches
        private void LoadNextBatchOfSets()
        {
            if (SetsToLoad == null || CurrentSetLoadIndex >= SetsToLoad.Count)
            {
                IsLoadingSets = false;
                this.Log($"Set dropdown fully loaded with {SetDropdownCodes.Length} options");
                return;
            }

            int _SetsInBatch = Mathf.Min(SETS_PER_BATCH, SetsToLoad.Count - CurrentSetLoadIndex);

            // Compter les strings valides dans ce batch
            int _ValidInBatch = 0;
            for (int i = 0; i < _SetsInBatch; i++)
            {
                int _Idx = CurrentSetLoadIndex + i;
                if (_Idx < SetsToLoad.Count && SetsToLoad[_Idx].TokenType == TokenType.String)
                    _ValidInBatch++;
            }

            if (_ValidInBatch > 0)
            {
                string[] _BatchOptions = new string[_ValidInBatch];
                int _BatchIdx = 0;
                for (int i = 0; i < _SetsInBatch; i++)
                {
                    int _Idx = CurrentSetLoadIndex + i;
                    if (_Idx >= SetsToLoad.Count) break;
                    if (SetsToLoad[_Idx].TokenType != TokenType.String) continue;
                    _BatchOptions[_BatchIdx] = SetsToLoad[_Idx].String;
                    _BatchIdx++;
                }

                SetDropdown.AddOptions(_BatchOptions);
                this.Log($"Added {_BatchIdx} sets (progress: {CurrentSetLoadIndex + _SetsInBatch}/{SetsToLoad.Count})");
            }

            CurrentSetLoadIndex += _SetsInBatch;

            if (CurrentSetLoadIndex < SetsToLoad.Count)
            {
                NextSetLoadTime = Time.time + SET_LOAD_INTERVAL;
            }
            else
            {
                IsLoadingSets = false;
                this.Log($"Set dropdown fully loaded with {SetDropdownCodes.Length} options");
            }
        }

        // Parse le JSON et met en file les cartes a charger (appele depuis Update)
        private void ParseAndQueueCards()
        {
            this.Log("ParseAndQueueCards called");
            string _JsonData = PendingJsonData;
            PendingJsonData = null; // consommer immediatement

            if (!VRCJson.TryDeserializeFromJson(_JsonData, out DataToken _Token))
            {
                this.Error("Error parsing search response JSON");
                return;
            }

            if (_Token.TokenType != TokenType.DataDictionary)
            {
                this.Error("Search response is not a DataDictionary");
                return;
            }

            DataDictionary _RootDict = _Token.DataDictionary;

            // Naviguer: root → "data" → "results"
            if (!_RootDict.TryGetValue("data", out _Token) || _Token.TokenType != TokenType.DataDictionary)
            {
                this.Error("Invalid or missing 'data' in search response");
                return;
            }

            DataDictionary _DataDict = _Token.DataDictionary;
            if (!_DataDict.TryGetValue("results", out _Token) || _Token.TokenType != TokenType.DataList)
            {
                this.Error("Invalid or missing 'results' in search response data");
                return;
            }

            DataList _Results = _Token.DataList;
            this.Log($"Search returned {_Results.Count} results");

            // Stocker pour chargement progressif
            CardsToLoad = _Results;
            CurrentLoadIndex = 0;
            NextLoadTime = Time.time;
        }

        public void OnCardPreviewRequest(String _CardKey)
        {
            this.Log("OnCardPreviewRequest called: " + _CardKey);
            MTG_SearchCard _PreviewCard;
            if (CardsPreview == null) return;
            _PreviewCard = CardsPreview.GetComponent<MTG_SearchCard>();
            if (_PreviewCard == null) return;
            _PreviewCard.SetCardKey(_CardKey);
        }

        // Spawn une carte physique depuis la preview
        public void SpawnPhysicCardFromPreview()
        {
            this.Log("SpawnPhysicCardFromPreview called");

            if (CardsPreview == null)
            {
                this.Error("CardsPreview is null");
                return;
            }

            MTG_SearchCard _PreviewCard = CardsPreview.GetComponent<MTG_SearchCard>();
            if (_PreviewCard == null || string.IsNullOrEmpty(_PreviewCard.CardKey))
            {
                this.Error("Preview card has no CardKey");
                return;
            }

            string _CardKey = _PreviewCard.CardKey;

            // Déterminer la position de spawn
            Vector3 _SpawnPos;
            Quaternion _SpawnRot;

            if (PhysicCardSpawnPoint == null)
            {
                this.Error("PhysicCardSpawnPoint not set ");
            }
            _SpawnPos = PhysicCardSpawnPoint.position;
            _SpawnRot = PhysicCardSpawnPoint.rotation;

            // Spawn via le Manager → PhysicCardPool
            if (Manager == null)
            {
                this.Error("Manager is null, cannot spawn");
                return;
            }

            MTG_PhysicCard _Card = Manager.SpawnPhysicCard(_CardKey, _SpawnPos, _SpawnRot);
            if (_Card == null)
            {
                this.Error($"Failed to spawn physic card: {_CardKey}");
                if (Manager.PhysicCardPool != null)
                    this.Error(Manager.PhysicCardPool.GetPoolStatus());
                return;
            }

            this.Log($"Physic card spawned: {_CardKey}");
        }

        protected override void GenerateUrl()
        {
            this.Log("GenerateUrl called");
            if (Manager == null)        return;
            if (Manager.SearchURL == null) return;
            string        _Name        = TrimInput(CardNameInput);
            string        _Text        = TrimInput(CardTextInput);
            string        _Type        = TrimInput(TypeLineInput);
            string        _Cmc         = TrimInput(CmcInput);
            string        _Set         = GetDropdownCode(SetDropdown, SetDropdownCodes);
            string        _Lang        = GetDropdownCode(LangDropdown, LangDropdownCodes);
            string        _BaseUrl     = Manager.SearchURL.ToString();
            StringBuilder _Query       = new StringBuilder();
            StringBuilder _Colors      = new StringBuilder();
            int           _RarityCount = 0;
            bool          _First       = true;
            string        _FullUrl;

            // n:
            if (_Name != "")
            {
                _Query.Append("n:\"");
                _Query.Append(_Name);
                _Query.Append("\"");
            }

            // o:
            if (_Text != "")
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("o:\"");
                _Query.Append(_Text);
                _Query.Append("\"");
            }

            // t:
            if (_Type != "")
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("t:\"");
                _Query.Append(_Type);
                _Query.Append("\"");
            }

            // cmc:
            if (_Cmc != "")
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("cmc:");
                _Query.Append(_Cmc);
            }

            // s:
            if (_Set != "")
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("s:");
                _Query.Append(_Set);
            }

            // l:
            if (_Lang != "")
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("l:");
                _Query.Append(_Lang);
            }

            // c:
            if (ColorC_Selected)
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("c:c");
            }
            else
            {
                if (ColorW_Selected) _Colors.Append("w");
                if (ColorU_Selected) _Colors.Append("u");
                if (ColorB_Selected) _Colors.Append("b");
                if (ColorR_Selected) _Colors.Append("r");
                if (ColorG_Selected) _Colors.Append("g");
                if (_Colors.Length > 0)
                {
                    if (_Query.Length > 0) _Query.Append(" ");
                    _Query.Append("c:");
                    _Query.Append(_Colors);
                }
            }

            // r:
            if (RarityCommon_Selected)   _RarityCount++;
            if (RarityUncommon_Selected) _RarityCount++;
            if (RarityRare_Selected)     _RarityCount++;
            if (RarityMythic_Selected)   _RarityCount++;

            if (_RarityCount == 1)
            {
                if (_Query.Length > 0) _Query.Append(" ");
                if (RarityCommon_Selected)   _Query.Append("r:common");
                if (RarityUncommon_Selected) _Query.Append("r:uncommon");
                if (RarityRare_Selected)     _Query.Append("r:rare");
                if (RarityMythic_Selected)   _Query.Append("r:mythic");
            }
            else if (_RarityCount > 1)
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("(");
                _First = true;
                if (RarityCommon_Selected)   { _Query.Append("r:common");                              _First = false; }
                if (RarityUncommon_Selected) { if (!_First) _Query.Append(" or "); _Query.Append("r:uncommon"); _First = false; }
                if (RarityRare_Selected)     { if (!_First) _Query.Append(" or "); _Query.Append("r:rare");     _First = false; }
                if (RarityMythic_Selected)   { if (!_First) _Query.Append(" or "); _Query.Append("r:mythic"); }
                _Query.Append(")");
            }

            _FullUrl = _BaseUrl + _Query.ToString();

            // Afficher l'URL dans les champs de display
            if (DisplayUrlField != null)
                DisplayUrlField.text = _FullUrl;
            if (UrlDisplayText != null)
                UrlDisplayText.text = _FullUrl;

            // Configurer le VRCUrlInputField UNIQUEMENT si pas de query (reset)
            // Limitation VRChat: on ne peut pas creer de VRCUrl dynamiquement.
            // L'utilisateur doit copier l'URL affichee et la coller dans le VRCUrlInputField.
            if (string.IsNullOrEmpty(_Query.ToString()))
            {
                if (ValidatedInput != null && Manager != null && Manager.SearchURL != null)
                    ValidatedInput.SetUrl(Manager.SearchURL);
                if (ValidatedUrlField != null && Manager != null && Manager.SearchURL != null)
                    ValidatedUrlField.SetUrl(Manager.SearchURL);
            }

            this.Log("GenerateUrl result: " + _FullUrl);
            ResetFocus();
        }

        private string TrimInput(TMP_InputField _Field)
        {
            if (_Field == null || _Field.text == null) return "";
            return _Field.text.Trim();
        }

        // Lit le code d'un dropdown via son tableau parallèle (index 0 = pas de filtre)
        private string GetDropdownCode(TMP_Dropdown _Dropdown, string[] _Codes)
        {
            if (_Dropdown == null || _Codes == null) return "";
            if (_Dropdown.value <= 0 || _Dropdown.value >= _Codes.Length) return "";
            string _Code = _Codes[_Dropdown.value];
            if (_Code == null) return "";
            return _Code.Trim();
        }

        // Supprime toutes les cartes existantes (desactive et reset le pool)
        public void ClearAllCards()
        {
            this.Log("ClearAllCards called");

            // Desactiver et reset toutes les cartes du pool
            if (CardPool != null)
            {
                for (int i = 0; i < MaxSearchCards; i++)
                {
                    if (CardPool[i] != null)
                    {
                        MTG_SearchCard _CardComp = CardPool[i].GetComponent<MTG_SearchCard>();
                        if (_CardComp != null) _CardComp.ResetCardKey();
                        CardPool[i].SetActive(false);
                    }
                }
            }

            // Reset les tableaux de suivi
            for (int i = 0; i < InstantiatedCards.Length; i++)
                InstantiatedCards[i] = null;
            InstantiatedCardsCount = 0;
            for (int i = 0; i < CardKeys.Length; i++)
                CardKeys[i] = null;
            CardKeysCount = 0;

            CurrentLoadIndex = 0;

            this.Log("All cards cleared (pool reset)");
        }

        // Chargement progressif des cartes par lots (utilise le pool reusable)
        private void LoadNextBatchOfCards()
        {
            if (CardsToLoad == null || CurrentLoadIndex >= CardsToLoad.Count)
                return;

            if (!PoolInitialized || CardPool == null)
            {
                if (!PoolInitInProgress)
                {
                    this.Log("Card pool not ready, starting progressive init...");
                    ForceInitializeCardPool();
                }
                return; // reessayera au prochain Update
            }

            int _MaxToLoad = Mathf.Min(CardsToLoad.Count, MaxSearchCards);
            int _CardsInBatch = Mathf.Min(CARDS_PER_BATCH, _MaxToLoad - CurrentLoadIndex);

            for (int i = 0; i < _CardsInBatch; i++)
            {
                int _CardIndex = CurrentLoadIndex + i;
                if (_CardIndex >= _MaxToLoad) break;

                if (CardsToLoad[_CardIndex].TokenType != TokenType.DataDictionary) continue;
                DataDictionary _CardDict = CardsToLoad[_CardIndex].DataDictionary;

                // Prendre une carte du pool et l'activer
                GameObject _Card = CardPool[_CardIndex];
                if (_Card == null) continue;
                _Card.SetActive(true);

                MTG_SearchCard _CardComp = _Card.GetComponent<MTG_SearchCard>();
                if (_CardComp != null)
                {
                    _CardComp.Manager = Manager;
                    _CardComp.SearchInterface = this;

                    // Extraire l'ID depuis le JSON et utiliser SetCardKey pour lancer le chargement
                    string _CardId = "";
                    if (_CardDict.TryGetValue("id", out DataToken _IdToken))
                    {
                        _CardId = _IdToken.String;
                    }
                    _CardComp.SetCardKey(_CardId);
                }

                if (InstantiatedCardsCount < InstantiatedCards.Length)
                {
                    InstantiatedCards[InstantiatedCardsCount] = _Card;
                    InstantiatedCardsCount++;
                }
                if (_CardComp != null && CardKeysCount < CardKeys.Length)
                {
                    CardKeys[CardKeysCount] = _CardComp.CardKey;
                    CardKeysCount++;
                }
            }

            CurrentLoadIndex += _CardsInBatch;

            if (CurrentLoadIndex < _MaxToLoad)
            {
                NextLoadTime = Time.time + LOAD_INTERVAL;
            }
            else
            {
                this.Log($"{_MaxToLoad} cards loaded from pool");

                // Desactiver les cartes du pool non utilisees
                for (int i = _MaxToLoad; i < MaxSearchCards; i++)
                {
                    if (CardPool[i] != null)
                    {
                        MTG_SearchCard _CardComp = CardPool[i].GetComponent<MTG_SearchCard>();
                        if (_CardComp != null) _CardComp.ResetCardKey();
                        CardPool[i].SetActive(false);
                    }
                }

                CardsToLoad = null;

                // Rafraichir les donnees d'atlas pour que les cartes puissent charger leurs images
                if (Manager != null)
                {
                    Manager.UpdateAtlasInfo();
                }
            }
        }

        public void ClearSearch()
        {
            this.Log("ClearSearch called");
            if (CardNameInput != null) CardNameInput.text = "";
            if (CardTextInput != null) CardTextInput.text = "";
            if (TypeLineInput != null) TypeLineInput.text = "";
            if (CmcInput != null) CmcInput.text = "";
            if (SetDropdown != null) SetDropdown.value = 0;
            if (LangDropdown != null) LangDropdown.value = 0;
            ColorW_Selected = false;
            ColorU_Selected = false;
            ColorB_Selected = false;
            ColorR_Selected = false;
            ColorG_Selected = false;
            ColorC_Selected = false;
            RarityCommon_Selected = false;
            RarityUncommon_Selected = false;
            RarityRare_Selected = false;
            RarityMythic_Selected = false;
            this.Log("Search parameters reset");
            GenerateUrl();
        }

        // reset le focus pour eviter les problemes d'interaction VR
        private void ResetFocus()
        {
            if (CardNameInput != null)
            {
                CardNameInput.interactable = false;
                CardNameInput.interactable = true;
            }
            if (CardTextInput != null)
            {
                CardTextInput.interactable = false;
                CardTextInput.interactable = true;
            }
            if (TypeLineInput != null)
            {
                TypeLineInput.interactable = false;
                TypeLineInput.interactable = true;
            }
            if (CmcInput != null)
            {
                CmcInput.interactable = false;
                CmcInput.interactable = true;
            }
            if (SetDropdown != null)
            {
                SetDropdown.interactable = false;
                SetDropdown.interactable = true;
            }
            if (LangDropdown != null)
            {
                LangDropdown.interactable = false;
                LangDropdown.interactable = true;
            }
            if (ValidatedUrlField != null)
            {
                ValidatedUrlField.interactable = false;
                ValidatedUrlField.interactable = true;
            }
            if (DisplayUrlField != null)
            {
                DisplayUrlField.interactable = false;
                DisplayUrlField.interactable = true;
            }
        }

        // buttons methodes
        public void OnColorW_ButtonClicked()
        {
            this.Log("OnColorW_ButtonClicked called");
            ColorW_Selected = !ColorW_Selected;
            if (ColorW_Selected && ColorC_Selected)
                ColorC_Selected = false;
            UpdateColorButtons();
            GenerateUrl();
        }
        public void OnColorU_ButtonClicked()
        {
            this.Log("OnColorU_ButtonClicked called");
            ColorU_Selected = !ColorU_Selected;
            if (ColorU_Selected && ColorC_Selected)
                ColorC_Selected = false;
            UpdateColorButtons();
            GenerateUrl();
        }
        public void OnColorB_ButtonClicked()
        {
            this.Log("OnColorB_ButtonClicked called");
            ColorB_Selected = !ColorB_Selected;
            if (ColorB_Selected && ColorC_Selected)
                ColorC_Selected = false;
            UpdateColorButtons();
            GenerateUrl();
        }
        public void OnColorR_ButtonClicked()
        {
            this.Log("OnColorR_ButtonClicked called");
            ColorR_Selected = !ColorR_Selected;
            if (ColorR_Selected && ColorC_Selected)
                ColorC_Selected = false;
            UpdateColorButtons();
            GenerateUrl();
        }
        public void OnColorG_ButtonClicked()
        {
            this.Log("OnColorG_ButtonClicked called");
            ColorG_Selected = !ColorG_Selected;
            if (ColorG_Selected && ColorC_Selected)
                ColorC_Selected = false;
            UpdateColorButtons();
            GenerateUrl();
        }
        public void OnColorC_ButtonClicked()
        {
            this.Log("OnColorC_ButtonClicked called");
            ColorC_Selected = !ColorC_Selected;
            if (ColorC_Selected)
            {
                ColorW_Selected = false;
                ColorU_Selected = false;
                ColorB_Selected = false;
                ColorR_Selected = false;
                ColorG_Selected = false;
            }
            UpdateColorButtons();
            GenerateUrl();
        }
        public void UpdateColorButtons()
        {
            SetButtonColor(ColorW_Button, ColorW_Selected);
            SetButtonColor(ColorU_Button, ColorU_Selected);
            SetButtonColor(ColorB_Button, ColorB_Selected);
            SetButtonColor(ColorR_Button, ColorR_Selected);
            SetButtonColor(ColorG_Button, ColorG_Selected);
            SetButtonColor(ColorC_Button, ColorC_Selected);
        }
        public void OnRarityCommon_ButtonClicked()
        {
            this.Log("OnRarityCommon_ButtonClicked called");
            RarityCommon_Selected = !RarityCommon_Selected;
            UpdateRarityButtons();
            GenerateUrl();
        }
        public void OnRarityUncommon_ButtonClicked()
        {
            this.Log("OnRarityUncommon_ButtonClicked called");
            RarityUncommon_Selected = !RarityUncommon_Selected;
            UpdateRarityButtons();
            GenerateUrl();
        }
        public void OnRarityRare_ButtonClicked()
        {
            this.Log("OnRarityRare_ButtonClicked called");
            RarityRare_Selected = !RarityRare_Selected;
            UpdateRarityButtons();
            GenerateUrl();
        }
        public void OnRarityMythic_ButtonClicked()
        {
            this.Log("OnRarityMythic_ButtonClicked called");
            RarityMythic_Selected = !RarityMythic_Selected;
            UpdateRarityButtons();
            GenerateUrl();
        }
        public void UpdateRarityButtons()
        {
            if (RarityCommon_Selected && RarityUncommon_Selected && RarityRare_Selected && RarityMythic_Selected)
            {
                RarityCommon_Selected = false;
                RarityUncommon_Selected = false;
                RarityRare_Selected = false;
                RarityMythic_Selected = false;
            }
            SetButtonColor(RarityCommon_Button, RarityCommon_Selected);
            SetButtonColor(RarityUncommon_Button, RarityUncommon_Selected);
            SetButtonColor(RarityRare_Button, RarityRare_Selected);
            SetButtonColor(RarityMythic_Button, RarityMythic_Selected);
        }
        public void SetButtonColor(UnityEngine.UI.Button _Button, bool _IsSelected)
        {
            if (_Button == null) return;
            UnityEngine.UI.ColorBlock _Colors = _Button.colors;
            if (!_IsSelected)
            {
                _Colors.normalColor = new Color(0.300f, 0.300f, 0.300f, 1.000f);
            }
            else
            {
                _Colors.normalColor = new Color(1.000f, 1.000f, 1.000f, 1.000f);
            }
            _Button.colors = _Colors;
        }
    }
}