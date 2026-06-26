using System;
using System.Collections;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.StringLoading;
using VRC.SDK3.Data;
using VRC.SDK3.Image;

namespace MTG
{
    public class MTG_Manager : MTG_Base
    {

        [Header("=== MANAGER DATA ===")]
        [SerializeField] private bool IsSyncing = false;
        [SerializeField] private bool Agree = false;

        [UdonSynced, SerializeField] private int InstanceID = -1;

        [Header("=== INTERFACES ===")]
        public MTG_SyncInterface SyncInterface;
        public MTG_SearchInterface SearchInterface;
        public MTG_DeckInterface DeckInterface;
        public MTG_PhysicCardPoolManager PhysicCardPool;

        [Header("=== URLS ===")]
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        [SerializeField] public static VRCUrl BaseURL = new VRCUrl("https://mtg.hactazia.fr/a");
#endif
        public VRCUrl CreateURL; //ac
        public VRCUrl SearchURL; //as?q
        public VRCUrl DeckURL; //ad?q
        public VRCUrl[] JoinURLs; //aj
        public VRCUrl[] TempURLs; //at

        [Header("=== ORACLE ===")]
        [VRC.Udon.Serialization.OdinSerializer.OdinSerialize] public String[][] CachedOracleIds;
        public DataDictionary[] CachedLegalities;
        public DataList[] CachedRulings;

        [Header("=== ATLAS ===")]
        public int MaxAtlas;
        public Texture2D[] AtlasImages;
        [VRC.Udon.Serialization.OdinSerializer.OdinSerialize] public String[][] AtlasCardIds; // [Atlas][slot]
        [VRC.Udon.Serialization.OdinSerializer.OdinSerialize] public Rect[][] AtlasCardRects; // [Atlas][slot]
        [SerializeField] private bool[] AtlasLoaded;
        [SerializeField] private bool[] AtlasLoading;
        [SerializeField] private float LastAtlasInfoUpdate = 0f;
        [SerializeField] private const float ATLAS_INFO_UPDATE_INTERVAL = 10f;

        // Image downloader
        private VRCImageDownloader ImageDownloader;

        //methodes
#if !COMPILER_UDONSHARP && UNITY_EDITOR

        protected override void OnValidate()
        {
            CreateURL = new VRCUrl($"{BaseURL}c");
            SearchURL = new VRCUrl($"{BaseURL}s?q=");
            DeckURL = new VRCUrl($"{BaseURL}d?q=");
            JoinURLs = new VRCUrl[64];
            for (int i = 0; i < JoinURLs.Length; i++)
                JoinURLs[i] = new VRCUrl($"{BaseURL}j{ToBase36(i)}");
            TempURLs = new VRCUrl[4096];
            for (int i = 0; i < TempURLs.Length; i++)
                TempURLs[i] = new VRCUrl($"{BaseURL}t{ToBase36(i)}");
        }
#endif
        protected override void Start()
        {
            base.Start();
            MaxAtlas = TempURLs.Length;
            AtlasImages = new Texture2D[MaxAtlas];
            AtlasCardIds = new String[MaxAtlas][];
            AtlasCardRects = new Rect[MaxAtlas][];
            AtlasLoaded = new bool[MaxAtlas];
            AtlasLoading = new bool[MaxAtlas];
            for (int i = 0; i < MaxAtlas; i++)
            {
                AtlasCardIds[i] = new String[24];
                AtlasCardRects[i] = new Rect[24];
                for (int j = 0; j < 24; j++)
                {
                    AtlasCardIds[i][j] = null;
                    AtlasCardRects[i][j] = new Rect(0, 0, 1, 1);
                }
                AtlasImages[i] = null;
            }
            CachedOracleIds = new string[0][];
            CachedLegalities = new DataDictionary[0];
            CachedRulings = new DataList[0];
            ImageDownloader = new VRCImageDownloader();
            // Initialiser LastAtlasInfoUpdate pour forcer un premier chargement rapide (apres 2 secondes)
            LastAtlasInfoUpdate = Time.time - ATLAS_INFO_UPDATE_INTERVAL + 2f;
        }
        protected override void Update()
        {
            base.Update();
            if (IsSyncing || !Agree) return;
            if (Time.time - LastAtlasInfoUpdate >= ATLAS_INFO_UPDATE_INTERVAL)
            {
                LastAtlasInfoUpdate = Time.time;
                UpdateAtlasInfo();
            }
        }
        // Udon events for networking
        public override void OnDeserialization()
        {
            if (IsSyncing || Agree) return;
            SyncInterface.Show();
        }
        public override void OnMasterTransferred(VRCPlayerApi _NewMaster)
        {
            this.Log("OnMasterTransferred called");
            if (IsSyncing || !_NewMaster.isLocal || Agree) return;
            SyncInterface.Show();
        }
        public override void OnPlayerJoined(VRCPlayerApi _Player)
        {
            this.Log("OnPlayerJoined called");
            if (IsSyncing || !_Player.isLocal || Agree) return;
            SyncInterface.Show();
        }
        internal void JoinGame(bool _Show)
        {
            this.Log("JoinGame called");
            if (IsSyncing || Agree) return;
            if (InstanceID == -1)
                VRCStringDownloader.LoadUrl(CreateURL, (IUdonEventReceiver)this);
            else VRCStringDownloader.LoadUrl(JoinURLs[InstanceID], (IUdonEventReceiver)this);
            if (_Show) SyncInterface.ShowLoading();
            IsSyncing = true;
        }
        // Udon events for VRCStringDownloader
        public override void OnStringLoadSuccess(IVRCStringDownload _Json)
        {
            this.Log("OnStringLoadSuccess called");
            if (_Json == null || _Json.Url == null) return;

            // Dispatch base sur l'URL (evite le parsing JSON couteux pour les grosses reponses)
            if (_Json.Url == CreateURL)
            {
                int _TempInstanceID;
                IsCreateResponse(_Json, out _TempInstanceID);
                if (_TempInstanceID != -1) InstanceID = _TempInstanceID;
            }
            else if (IsDeckURL(_Json.Url))
            {
                if (DeckInterface != null)
                    DeckInterface.OnDeckResponse(_Json);
            }
            else if (IsJoinURL(_Json.Url))
            {
                bool _IsValid;
                IsJoinResponse(_Json, out _IsValid);
                if (!_IsValid) JoinGame(true);
            }
            else if (IsSearchURL(_Json.Url))
            {
                this.VerboseLog("IsSearchURL returned TRUE, dispatching to SearchInterface...");
                if (SearchInterface != null)
                {
                    this.VerboseLog("Calling SearchInterface.OnSearchResponse...");
                    SearchInterface.OnSearchResponse(_Json);
                }
                else
                {
                    this.Error("SearchInterface is NULL! Cannot dispatch search response.");
                }
            }
            else if (IsTempURL(_Json.Url))
            {
                IsTempURLsResponse(_Json);
            }
            else
            {
                // Fallback: utiliser ReponseType pour "u" et autres types inconnus
                this.VerboseLog($"OnStringLoadSuccess: URL did not match any known type. Url='{_Json.Url}'");
                String _Type = "";
                ReponseType(_Json, out _Type);
                switch (_Type)
                {
                    case "u":
                        IsUserResponse(_Json);
                        break;
                    default:
                        this.Log($"Unknown response type: {_Type}");
                        break;
                }
            }
        }

        private bool IsJoinURL(VRCUrl _Url)
        {
            if (_Url == null || JoinURLs == null) return false;
            // Prefix guard: avoid iterating 64 URLs for non-join responses
            string _UrlStr = _Url.ToString();
            if (_UrlStr.IndexOf("/aj") < 0) return false;
            for (int i = 0; i < JoinURLs.Length; i++)
                if (_Url == JoinURLs[i]) return true;
            return false;
        }

        private bool IsSearchURL(VRCUrl _Url)
        {
            if (_Url == null || SearchURL == null)
            {
                this.VerboseLog($"IsSearchURL: null check failed - _Url={(_Url==null)}, SearchURL={(SearchURL==null)}");
                return false;
            }
            string _UrlStr = _Url.ToString();
            string _SearchStr = SearchURL.ToString();
            // Use IndexOf instead of StartsWith for Udon compatibility
            bool _Match = _UrlStr.IndexOf(_SearchStr) == 0;
            this.VerboseLog($"IsSearchURL: Url='{_UrlStr}' vs SearchURL='{_SearchStr}' = {_Match}");
            return _Match;
        }

        private bool IsDeckURL(VRCUrl _Url)
        {
            if (_Url == null || DeckURL == null) return false;
            string _UrlStr = _Url.ToString();
            string _DeckStr = DeckURL.ToString();
            return _UrlStr.IndexOf(_DeckStr) == 0;
        }

        private bool IsTempURL(VRCUrl _Url)
        {
            if (_Url == null || TempURLs == null) return false;
            // Prefix guard: check if the URL looks like a temp URL before iterating 4096 entries
            // TempURLs are /at[base36], so check if URL contains "/at"
            string _UrlStr = _Url.ToString();
            if (_UrlStr.IndexOf("/at") < 0) return false;
            for (int i = 0; i < TempURLs.Length; i++)
                if (_Url == TempURLs[i]) return true;
            return false;
        }
        public override void OnStringLoadError(IVRCStringDownload _Json)
        {
            this.Log("OnStringLoadError called");
            this.Error($"Error loading URL: {_Json.Url}");
            this.Error($"Error message: {_Json.Error}");
            IsSyncing = false;
            if (!Agree) SyncInterface.Show();
        }
        // methodes for response processing
        // obtient le type de reponse
        public void ReponseType(IVRCStringDownload _Json, out String _Type)
        {
            this.VerboseLog("ReponseType called");
            _Type = "";
            String _JsonData = "";
            DataDictionary _Dict;
            if (_Json == null || _Json.Result == null) return;
            _JsonData = _Json.Result;
            if (!VRCJson.TryDeserializeFromJson(_JsonData, out DataToken result)) return;
            _Dict = result.DataDictionary;
            if (!_Dict.TryGetValue("link_type", out DataToken linkTypeToken)) return;
            _Type = linkTypeToken.String;
        }
        // verifie la reponse de creation d'instance
        private void IsCreateResponse(IVRCStringDownload _Json, out int _InstanceID)
        {
            this.VerboseLog("IsCreateResponse called");
            String _JsonData = "";
            DataDictionary _Dict;
            _InstanceID = -1;
            if (_Json == null || _Json.Url == null) return;
            if (_Json.Url != CreateURL) return;
            _JsonData = _Json.Result;
            if (!VRCJson.TryDeserializeFromJson(_JsonData, out DataToken result))
            {
                this.Error("Error parsing create response");
                IsSyncing = false;
                if (!Agree) SyncInterface.Show();
                return;
            }
            _Dict = result.DataDictionary;
            if (!_Dict.TryGetValue("iid", out DataToken instanceIdToken))
            {
                this.Error("Error: 'iid' not found in create response");
                IsSyncing = false;
                if (!Agree) SyncInterface.Show();
                return;
            }
            _InstanceID = (int)instanceIdToken.Double;
            this.Log($"Created game {_InstanceID}");
            Agree = true;
            IsSyncing = false;
            SyncInterface.Hide();
        }
        // verifie la reponse de join d'instance
        private void IsJoinResponse(IVRCStringDownload _Json, out bool _IsValid)
        {
            this.VerboseLog("IsJoinResponse called");
            String _JsonData = "";
            bool urlFound = false;
            DataDictionary _Dict;
            _IsValid = false;
            if (_Json == null || _Json.Url == null) return;
            
            for (int i = 0; i < JoinURLs.Length; i++)
            {
                if (JoinURLs[i] == _Json.Url)
                {
                    urlFound = true;
                    break;
                }
            }
            if (!urlFound) return;
            _JsonData = _Json.Result;
            if (!VRCJson.TryDeserializeFromJson(_JsonData, out DataToken result))
            {
                this.Error("Error parsing join response");
                IsSyncing = false;
                if (!Agree) SyncInterface.Show();
                return;
            }
            _Dict = result.DataDictionary;
            if (!_Dict.TryGetValue("iid", out DataToken instanceIdToken))
            {
                this.Error("Error: 'iid' not found in join response");
                IsSyncing = false;
                if (!Agree) SyncInterface.Show();
                return;
            }
            if (InstanceID != (int)instanceIdToken.Double)
            {
                this.Error("Error: Instance ID mismatch");
                IsSyncing = false;
                if (!Agree) SyncInterface.Show();
                return;
            }
            _IsValid = true;
            this.Log($"Joined game {InstanceID}");
            Agree = true;
            IsSyncing = false;
            SyncInterface.Hide();
        }
        // verifie la reponse des tempsurls si json
        private void IsTempURLsResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("IsTempURLsResponse called");
            if (_Json == null || _Json.Url == null) return;
            if (_Json.Url == TempURLs[0])
            {
                ProcessCardInstanceResponse(_Json);
            }
            else if (_Json.Url == TempURLs[1])
            {
                ProcessDeckListResponse(_Json);
            }
            else if (_Json.Url == TempURLs[2])
            {
                ProcessSetListResponse(_Json);
            }
            else if (_Json.Url == TempURLs[3])
            {
                ProcessOracleDataResponse(_Json);
            }
        }
        // verifie la reponse de user
        private void IsUserResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("IsUserResponse called");
            // rien pour l'instant
        }
        // process card instance response (at0)
        private void ProcessCardInstanceResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("ProcessCardInstanceResponse called");
            String _JsonData = _Json.Result;
            DataToken _Token;
            DataDictionary _RootDict;
            DataDictionary _DataDict;
            DataDictionary _Batch;
            DataList _Batches;
            DataList _Cards;
            int _CardCount = 0;
            bool _AtlasChanged;
            int _AtlasIndex;

            int _Col , _Row ;
            float _RectWidth  = 1f / 6f;
            float _RectHeight = 0.25006751593088666f;
            float _uvX , _uvY ;

            if (!VRCJson.TryDeserializeFromJson(_JsonData, out _Token)) return;
            _RootDict = _Token.DataDictionary;
            if (!_RootDict.TryGetValue("data", out _Token)) return;
            _DataDict = _Token.DataDictionary;
            if (!_DataDict.TryGetValue("batches", out _Token)) return;
            _Batches = _Token.DataList;
            for (int i = 0; i < _Batches.Count; i++)
            {
                _Batch = _Batches[i].DataDictionary;
                _CardCount = (int)_Batch["card_count"].Double;

                // Lire atlas_link (ex: "/ata") et convertir en index TempURLs
                // "/ata" → "a" → FromBase36 → 10
                // Les URLs d'images commencent a TempURLs[10] (/ata, /atb, /atc...)
                _AtlasIndex = ConvertAtlasLinkToIndex(_Batch["atlas_link"].String);

                // Verifier si les donnees de cet atlas ont change
                _AtlasChanged = HasAtlasDataChanged(_AtlasIndex, _Batch, _CardCount);

                // Toujours creer les tableaux en taille 24 (taille fixe de la grille 6x4)
                AtlasCardIds[_AtlasIndex] = new String[24];
                AtlasCardRects[_AtlasIndex] = new Rect[24];

                _Cards = _Batch["cards"].DataList;
                for (int j = 0; j < _Cards.Count; j++)
                {
                    AtlasCardIds[_AtlasIndex][j] = _Cards[j].String;
                    _Col = j % 6;
                    _Row = j / 6;
                    _uvX = _Col * _RectWidth;
                    _uvY = 0.7499324840691133f - (_Row * _RectHeight);
                    AtlasCardRects[_AtlasIndex][j] = new Rect(_uvX, _uvY, _RectWidth, _RectHeight);
                }

                // Effacer les slots restants si moins de 24 cartes
                for (int j = _Cards.Count; j < 24; j++)
                {
                    AtlasCardIds[_AtlasIndex][j] = null;
                    AtlasCardRects[_AtlasIndex][j] = new Rect(0, 0, 1, 1);
                }

                // Declencher le telechargement de l'image d'atlas
                if (_AtlasChanged)
                {
                    this.Log($"Atlas {_AtlasIndex} ({_Batch["atlas_link"].String}) data changed, reloading image...");
                    AtlasLoaded[_AtlasIndex] = false;
                    AtlasLoading[_AtlasIndex] = false;
                    AtlasImages[_AtlasIndex] = null;
                }

                if (!AtlasLoaded[_AtlasIndex] && !AtlasLoading[_AtlasIndex])
                {
                    LoadAtlas(_AtlasIndex);
                }
            }
        }

        // Convertit un atlas_link (ex: "/ata") en index TempURLs
        // "/ata" → "a" → base36 → 10 (les images commencent apres les 10 URLs JSON at0-at9)
        private int ConvertAtlasLinkToIndex(string _AtlasLink)
        {
            if (string.IsNullOrEmpty(_AtlasLink) || !_AtlasLink.StartsWith("/at"))
                return 0;
            string _Base36 = _AtlasLink.Substring(3); // Enlever "/at"
            if (string.IsNullOrEmpty(_Base36))
                return 0;
            return FromBase36(_Base36);
        }

        // Verifie si les donnees d'un atlas ont change (nombre de cartes ou IDs differents)
        private bool HasAtlasDataChanged(int _AtlasIndex, DataDictionary _NewBatch, int _NewCount)
        {
            if (_AtlasIndex < 0 || _AtlasIndex >= AtlasCardIds.Length) return true;
            if (AtlasCardIds[_AtlasIndex] == null) return true;

            int _OldCount = 0;
            for (int i = 0; i < 24; i++)
            {
                if (AtlasCardIds[_AtlasIndex][i] != null)
                    _OldCount++;
                else
                    break;
            }
            if (_OldCount != _NewCount) return true;

            DataList _NewCards = _NewBatch["cards"].DataList;
            for (int i = 0; i < _NewCount; i++)
            {
                if (_NewCards[i].TokenType != TokenType.String) continue;
                if (AtlasCardIds[_AtlasIndex][i] != _NewCards[i].String)
                    return true;
            }
            return false;
        }

        // process deck list response (at1)
        private void ProcessDeckListResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("ProcessDeckListResponse called");

        }
        // process set liste response (at2)
        private void ProcessSetListResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("ProcessSetListResponse called");

        }
        // process oracle data response (at3)
        private void ProcessOracleDataResponse(IVRCStringDownload _Json)
        {

            this.VerboseLog("ProcessOracleDataResponse called");
            String _JsonData = _Json.Result;
            DataToken _Token;
            DataDictionary _RootDict;
            DataDictionary _DataDict;
            DataDictionary _Card;
            DataList _CardsList;
            DataList _IdsList;
            int _Count = 0;
            string[] _IdsArr;

            if (!VRCJson.TryDeserializeFromJson(_JsonData, out _Token)) return;
            _RootDict = _Token.DataDictionary;
            if (!_RootDict.TryGetValue("data", out _Token)) return;
            _DataDict = _Token.DataDictionary;
            if (!_DataDict.TryGetValue("cards", out _Token)) return;
            _CardsList = _Token.DataList;

            _Count = _CardsList.Count;
            CachedOracleIds = new string[_Count][];
            CachedLegalities = new DataDictionary[_Count];
            CachedRulings = new DataList[_Count];

            for (int i = 0; i < _Count; i++)
            {
                _Card = _CardsList[i].DataDictionary;

                if (_Card.TryGetValue("ids", out _Token))
                {
                    _IdsList = _Token.DataList;
                    _IdsArr = new string[_IdsList.Count];
                    for (int j = 0; j < _IdsList.Count; j++)
                        _IdsArr[j] = _IdsList[j].String;
                    CachedOracleIds[i] = _IdsArr;
                }
                else
                    CachedOracleIds[i] = new string[0];

                if (_Card.TryGetValue("legalities", out _Token))
                    CachedLegalities[i] = _Token.DataDictionary;
                else
                    CachedLegalities[i] = new DataDictionary();

                if (_Card.TryGetValue("rulings", out _Token))
                    CachedRulings[i] = _Token.DataList;
                else
                    CachedRulings[i] = new DataList();
            }

        }
        // Udon events for VRCImageDownloader
        public override void OnImageLoadSuccess(IVRCImageDownload _Image)
        {
            this.Log("OnImageLoadSuccess called");
            int _AtlasIndex;
            if (!IsAtlasImageResponse(_Image, out _AtlasIndex)) return;
            ProcessAtlasImage(_Image, _AtlasIndex);
        }
        public override void OnImageLoadError(IVRCImageDownload _Image)
        {
            this.Log("OnImageLoadError called");
            this.Log($"Error loading image from URL: {_Image.Url}");
            this.Log($"Error message: {_Image.Error}");
        }

        // verifie si l'image est une reponse d'atlas
        private bool IsAtlasImageResponse(IVRCImageDownload _Image, out int _AtlasIndex)
        {
            this.Log("IsAtlasImageResponse called");
            _AtlasIndex = -1;
            if (_Image == null || _Image.Url == null) return false;
            for (int i = 0; i < TempURLs.Length; i++)
            {
                if (TempURLs[i] == _Image.Url)
                {
                    _AtlasIndex = i;
                    return true;
                }
            }
            return false;
        }
        // met a jour l'image de l'atlas
        private void ProcessAtlasImage(IVRCImageDownload _Image, int _AtlasIndex)
        {
            this.VerboseLog("ProcessAtlasImage called");
            if (_Image == null || _Image.Result == null) return;
            if (_AtlasIndex < 0 || _AtlasIndex >= AtlasImages.Length) return;
            
            AtlasImages[_AtlasIndex] = _Image.Result;
            AtlasLoaded[_AtlasIndex] = true;
            AtlasLoading[_AtlasIndex] = false;
            
            // Compter les cartes disponibles dans cet atlas
            int _CardCount = 0;
            if (AtlasCardIds[_AtlasIndex] != null)
            {
                for (int i = 0; i < AtlasCardIds[_AtlasIndex].Length; i++)
                {
                    if (!string.IsNullOrEmpty(AtlasCardIds[_AtlasIndex][i]))
                        _CardCount++;
                }
            }
            
            // Compter le nombre total d'atlas charges
            int _LoadedCount = 0;
            int _TotalWithData = 0;
            for (int i = 0; i < AtlasLoaded.Length; i++)
            {
                if (AtlasCardIds[i] != null && AtlasCardIds[i].Length > 0)
                {
                    bool _HasCards = false;
                    for (int j = 0; j < AtlasCardIds[i].Length; j++)
                    {
                        if (!string.IsNullOrEmpty(AtlasCardIds[i][j]))
                        {
                            _HasCards = true;
                            break;
                        }
                    }
                    if (_HasCards)
                    {
                        _TotalWithData++;
                        if (AtlasLoaded[i]) _LoadedCount++;
                    }
                }
            }
            
            this.Log($"Atlas {_AtlasIndex} loaded ({_CardCount} cards) - Progress: {_LoadedCount}/{_TotalWithData} atlas loaded");
        }

        // methodes for atlas management
        // met a jour les infos des atlas
        public void UpdateAtlasInfo()
        {
            if (InstanceID == -1) return;
            this.Log($"UpdateAtlasInfo called (InstanceID={InstanceID})");
            VRCStringDownloader.LoadUrl(TempURLs[0], (IUdonEventReceiver)this);
            VRCStringDownloader.LoadUrl(TempURLs[3], (IUdonEventReceiver)this);
        }
        // obtient la texture de l'atlas
        public Texture2D GetAtlasTexture(int _AtlasIndex)
        {
            this.VerboseLog($"GetAtlasTexture called for atlasIndex: {_AtlasIndex}");
            if (_AtlasIndex < 0 || _AtlasIndex >= AtlasImages.Length) return null;
            if (!AtlasLoaded[_AtlasIndex] && !AtlasLoading[_AtlasIndex])
            {
                LoadAtlas(_AtlasIndex);
                return null;
            }
            if (!AtlasLoaded[_AtlasIndex]) return null;
            return AtlasImages[_AtlasIndex];
        }
        
        // Verifie si un atlas est en cours de chargement
        public bool IsAtlasLoading(int _AtlasIndex)
        {
            if (_AtlasIndex < 0 || _AtlasIndex >= AtlasLoading.Length) return false;
            return AtlasLoading[_AtlasIndex];
        }
        // charge l'atlas a partir de l'url
        private void LoadAtlas(int _AtlasIndex)
        {
            if (_AtlasIndex < 0 || _AtlasIndex >= TempURLs.Length || AtlasLoading[_AtlasIndex]) return;
            
            // Compter les cartes dans cet atlas
            int _CardCount = 0;
            if (AtlasCardIds[_AtlasIndex] != null)
            {
                for (int i = 0; i < AtlasCardIds[_AtlasIndex].Length; i++)
                {
                    if (!string.IsNullOrEmpty(AtlasCardIds[_AtlasIndex][i]))
                        _CardCount++;
                }
            }
            
            this.Log($"Downloading atlas {_AtlasIndex} ({_CardCount} cards)...");
            AtlasLoading[_AtlasIndex] = true;
            ImageDownloader.DownloadImage(TempURLs[_AtlasIndex], null, (IUdonEventReceiver)this);
        }
        // obtient les infos d'une carte dans l'atlas
        public bool GetAtlasInfoForCard(string _CardId, out int _AtlasIndex, out Rect _UvRect)
        {
            this.VerboseLog($"GetAtlasInfoForCard called for cardId: {_CardId}");
            _AtlasIndex = -1;
            _UvRect = new Rect(0, 0, 1, 1);
            if (_CardId == "Debug") return true;
            
            // Compter les atlas reellement peuples (avec au moins une carte)
            int _AtlasWithData = 0;
            for (int i = 0; i < AtlasCardIds.Length; i++)
            {
                if (AtlasCardIds[i] == null) continue;
                for (int j = 0; j < AtlasCardIds[i].Length; j++)
                {
                    if (!string.IsNullOrEmpty(AtlasCardIds[i][j]))
                    {
                        _AtlasWithData++;
                        break; // cet atlas compte, passer au suivant
                    }
                }
            }
            int _TotalAtlas = AtlasCardIds.Length;
            for (int i = 0; i < AtlasCardIds.Length; i++)
            {
                if (AtlasCardIds[i] == null) continue;
                for (int j = 0; j < AtlasCardIds[i].Length; j++)
                {
                    if (AtlasCardIds[i][j] == _CardId)
                    {
                        _AtlasIndex = i;
                        _UvRect = AtlasCardRects[i][j];
                        this.VerboseLog($"Found {_CardId} in atlas {i} slot {j}");
                        return true;
                    }
                }
            }
            this.VerboseLog($"CardId: {_CardId} not found in any atlas ({_AtlasWithData}/{_TotalAtlas} populated)");
            return false;
        }

        public new int GetInstanceID()
        {
            this.Log("GetInstanceID called");
            return InstanceID;
        }


        // utilitaires
        // base36 methodes
        private String ToBase36(int value)
        {
            const String chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (value == 0) return "0";
            String result = "";
            while (value > 0)
            {
                result = chars[value % chars.Length] + result;
                value /= chars.Length;
            }
            return result;
        }

        private int FromBase36(string base36)
        {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (string.IsNullOrEmpty(base36)) return 0;
            int result = 0;
            for (int i = 0; i < base36.Length; i++)
            {
                char c = char.ToLower(base36[i]);
                int val = chars.IndexOf(c);
                result = result * chars.Length + val;
            }
            return result;
        }

        // === PHYSIC CARD POOL ===
        public MTG_PhysicCard SpawnPhysicCard(string _CardKey, Vector3 _Position, Quaternion _Rotation)
        {
            if (PhysicCardPool == null)
            {
                this.Error("SpawnPhysicCard: PhysicCardPool is null");
                return null;
            }
            return PhysicCardPool.SpawnCard(_CardKey, _Position, _Rotation);
        }

        public void DespawnPhysicCard(MTG_PhysicCard _Card)
        {
            if (PhysicCardPool != null)
                PhysicCardPool.DespawnCard(_Card);
        }

        public void DespawnAllPhysicCards()
        {
            if (PhysicCardPool != null)
                PhysicCardPool.DespawnAll();
        }
    }
}