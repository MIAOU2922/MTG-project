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
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;

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

        [Header("=== ATLAS ===")]
        public int MaxAtlas;
        public Texture2D[] AtlasImages;
        [VRC.Udon.Serialization.OdinSerializer.OdinSerialize] /* UdonSharp auto-upgrade: serialization */ public String[][] AtlasCardIds; // [Atlas][slot]
        [VRC.Udon.Serialization.OdinSerializer.OdinSerialize] /* UdonSharp auto-upgrade: serialization */ public Rect[][] AtlasCardRects; // [Atlas][slot]
        [SerializeField] private bool[] AtlasLoaded;
        [SerializeField] private bool[] AtlasLoading;
        [SerializeField] private float LastAtlasInfoUpdate = 0f;
        [SerializeField] private const float ATLAS_INFO_UPDATE_INTERVAL = 10f;

        [Header("=== ORACLE ===")]
        [SerializeField] private String[] CachedOracleIds;
        [SerializeField] private DataDictionary[] CachedLegalities;
        [SerializeField] private DataList[] CachedRulings;

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
            CachedOracleIds = new String[0];
            CachedLegalities = new DataDictionary[0];
            CachedRulings = new DataList[0];
            ImageDownloader = new VRCImageDownloader();
            LastAtlasInfoUpdate = Time.time;
        }
        private void Update()
        {
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
                        IsCreateResponse(_Json, out InstanceID);
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
            this.Log($"Error loading URL: {_Json.Url}");
            this.Log($"Error message: {_Json.Error}");
            IsSyncing = false;
        }
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
            if (!VRCJson.TryDeserializeFromJson(_JsonData, out DataToken result)) return;
            _Dict = result.DataDictionary;
            if (!_Dict.TryGetValue("iid", out DataToken instanceIdToken)) return;
            _InstanceID = instanceIdToken.Int;
        }
        // verifie la reponse de deck
        private void IsDeckResponse(IVRCStringDownload _Json)
        {
            this.Log("IsDeckResponse called");
            // to be implemented
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
            if (!VRCJson.TryDeserializeFromJson(_JsonData, out DataToken result)) return;
            _Dict = result.DataDictionary;
            if (!_Dict.TryGetValue("iid", out DataToken instanceIdToken)) return;
            if (InstanceID != instanceIdToken.Int) return;
            _IsValid = true;
        }
        // verifie la reponse de recherche
        private void IsSearchResponse(IVRCStringDownload _Json)
        {
            this.Log("IsSearchResponse called");
            // to be implemented
        }
        // verifie la reponse des tempsurls
        private void IsTempURLsResponse(IVRCStringDownload _Json)
        {
            this.Log("IsTempURLsResponse called");
            // to be implemented
        }
        // verifie la reponse de user
        private void IsUserResponse(IVRCStringDownload _Json)
        {
            this.Log("IsUserResponse called");
            // to be implemented
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
    }
}