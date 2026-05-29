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

        private void OnValidate()
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
            LastAtlasInfoUpdate = Time.time;
        }
        protected override void Update()
        {
            base.Update();
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
            String _Type = "";
            bool _IsValid = false;
            ReponseType(_Json, out _Type);
            switch (_Type)
            {
                case "c":
                    {
                        int _TempInstanceID;
                        IsCreateResponse(_Json, out _TempInstanceID);
                        if (_TempInstanceID != -1) InstanceID = _TempInstanceID;
                        break;
                    }
                case "d":
                    {
                        IsDeckResponse(_Json);
                        break;
                    }
                case "j":
                    {
                        IsJoinResponse(_Json, out _IsValid);
                        if (!_IsValid) JoinGame(true);
                        break;
                    }
                case "s":
                    {
                        IsSearchResponse(_Json);
                        break;
                    }
                case "t":
                    {
                        IsTempURLsResponse(_Json);
                        break;
                    }
                case "u":
                    {
                        IsUserResponse(_Json);
                        break;
                    }
                default:
                    {
                        this.Log($"Unknown response type: {_Type}");
                        break;
                    }
            }
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
            this.Log("ReponseType called");
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
            this.Log("IsCreateResponse called");
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
        // verifie la reponse de deck
        private void IsDeckResponse(IVRCStringDownload _Json)
        {
            this.Log("IsDeckResponse called");
            // rien pour l'instant
        }
        // verifie la reponse de join d'instance
        private void IsJoinResponse(IVRCStringDownload _Json, out bool _IsValid)
        {
            this.Log("IsJoinResponse called");
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
        // verifie la reponse de recherche
        private void IsSearchResponse(IVRCStringDownload _Json)
        {
            this.Log("IsSearchResponse called");
            // rien pour l'instant
        }
        // verifie la reponse des tempsurls si json
        private void IsTempURLsResponse(IVRCStringDownload _Json)
        {
            this.Log("IsTempURLsResponse called");
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
            this.Log("IsUserResponse called");
            // rien pour l'instant
        }
        // process card instance response (at0)
        private void ProcessCardInstanceResponse(IVRCStringDownload _Json)
        {
            this.Log("ProcessCardInstanceResponse called");
            String _JsonData = _Json.Result;
            DataToken _Token;
            DataDictionary _RootDict;
            DataDictionary _DataDict;
            DataDictionary _Batch;
            DataList _Batches;
            DataList _Cards;
            int _CardCount = 0;

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
                _CardCount = _Batch["card_count"].Int;

                AtlasCardIds[i] = new String[_CardCount];
                AtlasCardRects[i] = new Rect[_CardCount];

                _Cards = _Batch["cards"].DataList;
                for (int j = 0; j < _Cards.Count; j++)
                {
                    AtlasCardIds[i][j] = _Cards[j].String;
                    _Col = j % 6;
                    _Row = j / 6;
                    _uvX = _Col * _RectWidth;
                    _uvY = 0.7499324840691133f - (_Row * _RectHeight);
                    AtlasCardRects[i][j] = new Rect(_uvX, _uvY, _RectWidth, _RectHeight);
                }
            }
        }
        // process deck list response (at1)
        private void ProcessDeckListResponse(IVRCStringDownload _Json)
        {
            this.Log("ProcessDeckListResponse called");

        }
        // process set liste response (at2)
        private void ProcessSetListResponse(IVRCStringDownload _Json)
        {
            this.Log("ProcessSetListResponse called");

        }
        // process oracle data response (at3)
        private void ProcessOracleDataResponse(IVRCStringDownload _Json)
        {

            this.Log("ProcessOracleDataResponse called");
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
            this.Log("ProcessAtlasImage called");
            if (_Image == null || _Image.Result == null) return;
            if (_AtlasIndex < 0 || _AtlasIndex >= AtlasImages.Length) return;
            AtlasImages[_AtlasIndex] = _Image.Result;
            AtlasLoaded[_AtlasIndex] = true;
            AtlasLoading[_AtlasIndex] = false;
        }

        // methodes for atlas management
        // met a jour les infos des atlas
        private void UpdateAtlasInfo()
        {
            this.Log("UpdateAtlasInfo called");
            if (InstanceID == -1) return;
            VRCStringDownloader.LoadUrl(TempURLs[0], (IUdonEventReceiver)this);
            VRCStringDownloader.LoadUrl(TempURLs[3], (IUdonEventReceiver)this);
        }
        // obtient la texture de l'atlas
        public Texture2D GetAtlasTexture(int _AtlasIndex)
        {
            this.Log($"GetAtlasTexture called for atlasIndex: {_AtlasIndex}");
            if (_AtlasIndex < 0 || _AtlasIndex >= AtlasImages.Length) return null;
            if (!AtlasLoaded[_AtlasIndex] && !AtlasLoading[_AtlasIndex])
            {
                LoadAtlas(_AtlasIndex);
                return null;
            }
            if (!AtlasLoaded[_AtlasIndex]) return null;
            return AtlasImages[_AtlasIndex];
        }
        // charge l'atlas a partir de l'url
        private void LoadAtlas(int _AtlasIndex)
        {
            this.Log($"LoadAtlas called for atlasIndex: {_AtlasIndex}");
            if (_AtlasIndex < 0 || _AtlasIndex >= TempURLs.Length || AtlasLoading[_AtlasIndex]) return;
            AtlasLoading[_AtlasIndex] = true;
            ImageDownloader.DownloadImage(TempURLs[_AtlasIndex], null, (IUdonEventReceiver)this);
        }
        // obtient les infos d'une carte dans l'atlas
        public bool GetAtlasInfoForCard(string _CardId, out int _AtlasIndex, out Rect _UvRect)
        {
            this.Log($"GetAtlasInfoForCard called for cardId: {_CardId}");
            _AtlasIndex = -1;
            _UvRect = new Rect(0, 0, 1, 1);
            if (_CardId == "Debug") return true;
            for (int i = 0; i < AtlasCardIds.Length; i++)
            {
                for (int j = 0; j < 24; j++)
                {
                    if (AtlasCardIds[i][j] == null) continue;
                    if (AtlasCardIds[i][j] == _CardId)
                    {
                        _AtlasIndex = i;
                        _UvRect = AtlasCardRects[i][j];
                        return true;
                    }
                }
            }
            this.Log($"CardId: {_CardId} not found in any atlas");
            return false;
        }

        public int GetInstanceID()
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
    }
}