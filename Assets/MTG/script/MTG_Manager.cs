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
using VRC.SDK3.Persistence;

namespace MTG
{
    public class MTG_Manager : MTG_Tickable
    {

        [Header("=== MANAGER DATA ===")]
        [SerializeField] private bool IsSyncing = false;
        [SerializeField] private bool Agree = false;

        [UdonSynced, SerializeField] private int InstanceID = -1;

        [Header("=== INTERFACES ===")]
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

        [Header("=== USER KEY (identite stable) ===")]
        public VRCUrl RegisterURL; //aur : demande d'une nouvelle key au serveur
        public VRCUrl[] LoginURLs; //aul{3hex} : login par chunks (4096 urls statiques)

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

        // Liste des atlas peuples (optimisation des lookups de cartes : on ne scan plus les 4096)
        private int[] AtlasPopulatedIndices;
        private int AtlasPopulatedCount = 0;

        // File d'attente des cartes en attente d'image (mode evenementiel, sans polling).
        // Les cartes s'enregistrent quand leur image n'est pas encore disponible et le
        // Manager re-tente par petits lots quand les donnees/atlas changent.
        private MTG_Card[] PendingCardRefresh;
        private int PendingCardRefreshCount = 0;
        private bool CardRefreshSweepActive = false;
        private int CardRefreshSweepIndex = 0;
        private bool CardRefreshRerun = false;
        private const int CARD_REFRESH_PER_FRAME = 16;

        // Image downloader
        private VRCImageDownloader ImageDownloader;

        // User key state (identite stable au lieu de l'IP)
        private string UserKey = null;
        private int LoginChunkIndex = 0;
        private float NextLoginTime = 0f;
        private bool LoginInProgress = false;
        private int LoginRetryCount = 0;
        private bool KeyReady = false;          // register OK ou login chunks OK
        private bool KeyCheckStarted = false;   // InitUserKey deja lance
        private float NextJoinTime = 0f;        // delai avant auto-join / retry
        private const float LOGIN_INTERVAL = 5f;
        private const int LOGIN_CHUNKS = 4;
        private const int LOGIN_CHUNK_SIZE = 3;
        private const int MAX_LOGIN_RETRIES = 3;
        private const float JOIN_RETRY_DELAY = 5f;
        private const string USER_KEY_PLAYER_DATA = "mtg_key";

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
            RegisterURL = new VRCUrl($"{BaseURL}ur");
            LoginURLs = new VRCUrl[4096];
            for (int i = 0; i < LoginURLs.Length; i++)
                LoginURLs[i] = new VRCUrl($"{BaseURL}ul{ToHex3(i)}");
        }

        // Formatte un int 0..4095 en 3 caracteres hex (editor only)
        private string ToHex3(int _Value)
        {
            const string _Chars = "0123456789abcdef";
            return "" + _Chars[(_Value >> 8) & 0xF] + _Chars[(_Value >> 4) & 0xF] + _Chars[_Value & 0xF];
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
            AtlasPopulatedIndices = new int[MaxAtlas];
            AtlasPopulatedCount = 0;

            // File de cartes en attente : taille = pool physic + pool search + marge
            int _PoolCapacity = (PhysicCardPool != null) ? PhysicCardPool.GetPoolCapacity() : 0;
            if (_PoolCapacity < 1024) _PoolCapacity = 1024;
            PendingCardRefresh = new MTG_Card[_PoolCapacity + 512];
            PendingCardRefreshCount = 0;
            CardRefreshSweepActive = false;
            CardRefreshSweepIndex = 0;
            CardRefreshRerun = false;
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

            // User key : l'initialisation se fait dans OnPlayerRestored (donnees joueur chargees)
            UserKey = null;
            LoginInProgress = false;
            LoginChunkIndex = 0;
            NextLoginTime = 0f;
            LoginRetryCount = 0;
            KeyReady = false;
            KeyCheckStarted = false;
            NextJoinTime = 0f;
        }
        protected override void Update()
        {
            base.Update();

            // 0. Sweep progressif des cartes en attente d'image (mode evenementiel)
            if (CardRefreshSweepActive)
                ProcessCardRefreshBatch();

            // 1. Envoi progressif des chunks de login (4 requetes espacees de 5s)
            if (LoginInProgress && !string.IsNullOrEmpty(UserKey) && Time.time >= NextLoginTime)
            {
                if (LoginChunkIndex < LOGIN_CHUNKS)
                {
                    int _Start = LoginChunkIndex * LOGIN_CHUNK_SIZE;
                    int _ChunkIdx = HexChunkToInt(UserKey.Substring(_Start, LOGIN_CHUNK_SIZE));
                    if (_ChunkIdx >= 0 && _ChunkIdx < LoginURLs.Length)
                    {
                        VRCStringDownloader.LoadUrl(LoginURLs[_ChunkIdx], (IUdonEventReceiver)this);
                        this.Log($"Key login chunk {LoginChunkIndex + 1}/{LOGIN_CHUNKS} sent");
                    }
                    LoginChunkIndex++;
                    NextLoginTime = Time.time + LOGIN_INTERVAL;
                }
            }

            // 2. Une fois la key traitee (register ou login), join automatique :
            //    - InstanceID connu -> /aj pour rejoindre
            //    - InstanceID -1 + master -> /ac pour creer
            //    - InstanceID -1 + non-master -> on attend la sync du master
            if (KeyReady && !Agree && !IsSyncing && Time.time >= NextJoinTime)
            {
                bool _IsMaster = Networking.LocalPlayer == null || Networking.LocalPlayer.isMaster;
                if (InstanceID != -1 || _IsMaster)
                    JoinGame();
            }

            // 3. Boucle principale (apres join reussi)
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
            // Plus de validation utilisateur : l'auto-join dans Update reagit
            // a la sync de InstanceID quand la key est prete
        }
        public override void OnMasterTransferred(VRCPlayerApi _NewMaster)
        {
            this.Log("OnMasterTransferred called");
        }
        public override void OnPlayerJoined(VRCPlayerApi _Player)
        {
            this.Log("OnPlayerJoined called");
        }
        internal void JoinGame()
        {
            this.Log("JoinGame called");
            if (IsSyncing || Agree) return;
            if (InstanceID == -1)
                VRCStringDownloader.LoadUrl(CreateURL, (IUdonEventReceiver)this);
            else VRCStringDownloader.LoadUrl(JoinURLs[InstanceID], (IUdonEventReceiver)this);
            IsSyncing = true;
        }
        // Udon events for VRCStringDownloader
        public override void OnStringLoadSuccess(IVRCStringDownload _Json)
        {
            this.Log("OnStringLoadSuccess called");
            if (_Json == null || _Json.Url == null) return;

            // Dispatch de la key utilisateur (register / login chunks)
            if (_Json.Url == RegisterURL)
            {
                IsRegisterResponse(_Json);
                return;
            }
            else if (IsLoginURL(_Json.Url))
            {
                IsLoginChunkResponse(_Json);
                return;
            }

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
                if (!_IsValid) JoinGame();
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
            if (_Json != null && _Json.Url != null && (IsLoginURL(_Json.Url) || _Json.Url == RegisterURL))
            {
                // Erreur sur le flux de key : re-tenter sans toucher au flux de sync
                this.Warning($"Key request error: {_Json.Error}");
                if (_Json.Url == RegisterURL)
                {
                    SendCustomEventDelayedSeconds(nameof(RetryRegister), 5f);
                }
                else
                {
                    // Chunk echoue : renvoyer le chunk en cours (le serveur deduplique)
                    if (LoginChunkIndex > 0) LoginChunkIndex--;
                    NextLoginTime = Time.time + 3f;
                }
                return;
            }
            this.Error($"Error loading URL: {_Json.Url}");
            this.Error($"Error message: {_Json.Error}");
            IsSyncing = false;
            if (!Agree) NextJoinTime = Time.time + JOIN_RETRY_DELAY;
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
                NextJoinTime = Time.time + JOIN_RETRY_DELAY;
                return;
            }
            _Dict = result.DataDictionary;
            if (!_Dict.TryGetValue("iid", out DataToken instanceIdToken))
            {
                this.Error("Error: 'iid' not found in create response");
                IsSyncing = false;
                NextJoinTime = Time.time + JOIN_RETRY_DELAY;
                return;
            }
            _InstanceID = (int)instanceIdToken.Double;
            this.Log($"Created game {_InstanceID}");
            // uid (clé hex string) n'est pas utile ici : la clé locale fait foi
            Agree = true;
            IsSyncing = false;
            InitialFetch();
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
                NextJoinTime = Time.time + JOIN_RETRY_DELAY;
                return;
            }
            _Dict = result.DataDictionary;
            if (!_Dict.TryGetValue("iid", out DataToken instanceIdToken))
            {
                this.Error("Error: 'iid' not found in join response");
                IsSyncing = false;
                NextJoinTime = Time.time + JOIN_RETRY_DELAY;
                return;
            }
            if (InstanceID != (int)instanceIdToken.Double)
            {
                this.Error("Error: Instance ID mismatch");
                IsSyncing = false;
                NextJoinTime = Time.time + JOIN_RETRY_DELAY;
                return;
            }
            _IsValid = true;
            this.Log($"Joined game {InstanceID}");
            // uid (clé hex string) n'est pas utile ici : la clé locale fait foi
            Agree = true;
            IsSyncing = false;
            InitialFetch();
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

        // === USER KEY (identite stable au lieu de l'IP) ===

        // Appele par VRChat quand les donnees persistantes du joueur sont chargees.
        // On attend cet evenement avant de lire/ecrire PlayerData (sinon risque d'ecrasement).
        public override void OnPlayerRestored(VRCPlayerApi _Player)
        {
            if (_Player == null || !_Player.isLocal) return;
            InitUserKey();
        }

        // Lit la key stockee en PlayerData :
        // - presente : login par chunks pour re-associer l'IP courante au compte
        // - absente : demande d'enregistrement au serveur (/aur)
        private void InitUserKey()
        {
            if (KeyCheckStarted) return;
            KeyCheckStarted = true;
            this.Log("InitUserKey called");
            if (PlayerData.HasKey(Networking.LocalPlayer, USER_KEY_PLAYER_DATA))
            {
                UserKey = PlayerData.GetString(Networking.LocalPlayer, USER_KEY_PLAYER_DATA);
                if (!string.IsNullOrEmpty(UserKey) && UserKey.Length == LOGIN_CHUNKS * LOGIN_CHUNK_SIZE)
                {
                    this.Log($"User key found: {UserKey}, starting chunk login...");
                    StartKeyLogin();
                }
                else
                {
                    this.Error($"Invalid stored key: '{UserKey}', requesting new one");
                    VRCStringDownloader.LoadUrl(RegisterURL, (IUdonEventReceiver)this);
                }
            }
            else
            {
                this.Log("No user key, requesting new one from server...");
                VRCStringDownloader.LoadUrl(RegisterURL, (IUdonEventReceiver)this);
            }
        }

        // Demarre la sequence de login : envoie les 4 chunks dans Update()
        private void StartKeyLogin()
        {
            if (string.IsNullOrEmpty(UserKey) || UserKey.Length != LOGIN_CHUNKS * LOGIN_CHUNK_SIZE)
            {
                this.Error("Cannot start key login: invalid key");
                return;
            }
            LoginChunkIndex = 0;
            NextLoginTime = Time.time + 1f;
            LoginInProgress = true;
            this.Log("Key login sequence started");
        }

        private bool IsLoginURL(VRCUrl _Url)
        {
            if (_Url == null || LoginURLs == null) return false;
            string _UrlStr = _Url.ToString();
            return _UrlStr.IndexOf("/aul") >= 0;
        }

        // Reponse d'enregistrement : le serveur a genere une key, on la sauvegarde en PlayerData
        private void IsRegisterResponse(IVRCStringDownload _Json)
        {
            this.Log("IsRegisterResponse called");
            if (_Json == null || _Json.Result == null) return;
            if (!VRCJson.TryDeserializeFromJson(_Json.Result, out DataToken _Result)) return;
            DataDictionary _Dict = _Result.DataDictionary;
            if (_Dict.TryGetValue("key", out DataToken _KeyToken) && _KeyToken.TokenType == TokenType.String)
            {
                UserKey = _KeyToken.String;
                if (!string.IsNullOrEmpty(UserKey) && UserKey.Length == LOGIN_CHUNKS * LOGIN_CHUNK_SIZE)
                {
                    PlayerData.SetString(USER_KEY_PLAYER_DATA, UserKey);
                    this.Log($"User key registered and saved: {UserKey}");
                    // Le serveur a deja associe l'IP au compte : on peut joindre
                    KeyReady = true;
                    NextJoinTime = Time.time;
                }
                else
                {
                    this.Error($"Register returned invalid key: '{UserKey}'");
                }
            }
            else
            {
                this.Error("Register response missing 'key'");
            }
        }

        // Reponse d'un chunk de login :
        // - contient uid (nombre) : login termine, l'IP est re-associee au compte
        // - contient error : key inconnue, on retente puis on re-enregistre au besoin
        // - sinon : chunk recu, on attend les suivants
        private void IsLoginChunkResponse(IVRCStringDownload _Json)
        {
            this.Log("IsLoginChunkResponse called");
            if (_Json == null || _Json.Result == null) return;
            if (!VRCJson.TryDeserializeFromJson(_Json.Result, out DataToken _Result)) return;
            DataDictionary _Dict = _Result.DataDictionary;
            // Nouveau format serveur :
            // - login OK : uid = STRING (clé 12 hex) + key_confirmed
            // - intermédiaire : uid = 0 (nombre) + chunks_received → on attend
            // - clé inconnue : uid = 0 + error:"unknown_key"
            if (_Dict.TryGetValue("uid", out DataToken _UidToken) && _UidToken.TokenType == TokenType.String &&
                !string.IsNullOrEmpty(_UidToken.String))
            {
                LoginInProgress = false;
                LoginRetryCount = 0;
                this.Log($"Key login OK, uid = {_UidToken.String}");
                // Le serveur a re-associe l'IP au compte : on peut joindre
                KeyReady = true;
                NextJoinTime = Time.time;
            }
            else if (_Dict.TryGetValue("error", out DataToken _ErrToken) && _ErrToken.TokenType == TokenType.String)
            {
                this.Error($"Key login error: {_ErrToken.String}");
                LoginRetryCount++;
                if (LoginRetryCount >= MAX_LOGIN_RETRIES)
                {
                    // Key inconnue cote serveur de facon persistante : demander une nouvelle key
                    this.Error("Too many key login failures, requesting new key");
                    LoginInProgress = false;
                    LoginRetryCount = 0;
                    UserKey = null;
                    VRCStringDownloader.LoadUrl(RegisterURL, (IUdonEventReceiver)this);
                }
                else
                {
                    StartKeyLogin();
                }
            }
            // sinon : reponse intermediaire (chunks_received), on continue d'attendre
        }

        // Re-tente l'enregistrement apres une erreur reseau (appele via SendCustomEventDelayedSeconds)
        public void RetryRegister()
        {
            if (string.IsNullOrEmpty(UserKey))
                VRCStringDownloader.LoadUrl(RegisterURL, (IUdonEventReceiver)this);
        }

        // Premier fetch des donnees de l'instance apres le join : /at0..at3
        // (cartes, decks, sets, oracle). Ensuite la boucle UpdateAtlasInfo prend le relais.
        private void InitialFetch()
        {
            this.Log("InitialFetch called (/at0..at3)");
            if (TempURLs == null) return;
            for (int i = 0; i < 4 && i < TempURLs.Length; i++)
                VRCStringDownloader.LoadUrl(TempURLs[i], (IUdonEventReceiver)this);
            LastAtlasInfoUpdate = Time.time;
        }

        // Convertit 3 caracteres hex en index 0..4095 (Udon-safe, sans string.Format)
        private int HexChunkToInt(string _Hex)
        {
            if (string.IsNullOrEmpty(_Hex)) return -1;
            int _Value = 0;
            for (int i = 0; i < _Hex.Length; i++)
            {
                char _C = _Hex[i];
                int _Digit = -1;
                if (_C >= '0' && _C <= '9') _Digit = _C - '0';
                else if (_C >= 'a' && _C <= 'f') _Digit = _C - 'a' + 10;
                else if (_C >= 'A' && _C <= 'F') _Digit = _C - 'A' + 10;
                if (_Digit < 0) return -1;
                _Value = _Value * 16 + _Digit;
            }
            return _Value;
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
                MarkAtlasPopulated(_AtlasIndex);

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

            // Event : les donnees d'atlas ont change, on re-tente le chargement
            // des cartes en attente (mode evenementiel, tous types de cartes)
            TriggerCardRefreshSweep();
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
        // Enregistre un atlas comme peuple (pour les lookups rapides de cartes)
        private void MarkAtlasPopulated(int _AtlasIndex)
        {
            if (AtlasPopulatedIndices == null || _AtlasIndex < 0) return;
            for (int i = 0; i < AtlasPopulatedCount; i++)
                if (AtlasPopulatedIndices[i] == _AtlasIndex) return;
            if (AtlasPopulatedCount < AtlasPopulatedIndices.Length)
            {
                AtlasPopulatedIndices[AtlasPopulatedCount] = _AtlasIndex;
                AtlasPopulatedCount++;
            }
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

        // process deck list response (at1) : forward a la DeckInterface
        private void ProcessDeckListResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("ProcessDeckListResponse called");
            if (DeckInterface != null)
                DeckInterface.OnDeckListResponse(_Json);
        }
        // process set liste response (at2) : forward a la SearchInterface (dropdown sets)
        private void ProcessSetListResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("ProcessSetListResponse called");
            if (SearchInterface != null)
                SearchInterface.ProcessSetListResponse(_Json);
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

            // Event : l'image d'atlas est prete, les cartes en attente peuvent charger
            TriggerCardRefreshSweep();
            
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
        // Re-fetch de la liste des decks (at1) — appele en event only
        // (apres une modif de deck, pas en boucle)
        public void FetchDeckList()
        {
            this.Log("FetchDeckList called (at1)");
            if (TempURLs == null || TempURLs.Length < 2) return;
            VRCStringDownloader.LoadUrl(TempURLs[1], (IUdonEventReceiver)this);
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
        // Enregistre une carte en attente d'image (ajout en fin de file, dedupe)
        public void RequestCardRefresh(MTG_Card _Card)
        {
            if (_Card == null || PendingCardRefresh == null) return;
            for (int i = 0; i < PendingCardRefreshCount; i++)
                if (PendingCardRefresh[i] == _Card) return;
            if (PendingCardRefreshCount < PendingCardRefresh.Length)
            {
                PendingCardRefresh[PendingCardRefreshCount] = _Card;
                PendingCardRefreshCount++;
            }
        }

        // Comme RequestCardRefresh mais la carte est traitee en priorite
        // (deplacee en tete de file si deja enregistree)
        public void RequestCardRefreshPriority(MTG_Card _Card)
        {
            if (_Card == null || PendingCardRefresh == null) return;
            for (int i = 0; i < PendingCardRefreshCount; i++)
            {
                if (PendingCardRefresh[i] == _Card)
                {
                    if (i == 0) return;
                    for (int j = i; j > 0; j--)
                        PendingCardRefresh[j] = PendingCardRefresh[j - 1];
                    PendingCardRefresh[0] = _Card;
                    return;
                }
            }
            if (PendingCardRefreshCount >= PendingCardRefresh.Length) return;
            for (int i = PendingCardRefreshCount; i > 0; i--)
                PendingCardRefresh[i] = PendingCardRefresh[i - 1];
            PendingCardRefresh[0] = _Card;
            PendingCardRefreshCount++;
        }

        // Retire une carte de la file d'attente (entree null, compactee en fin de sweep)
        public void CancelCardRefresh(MTG_Card _Card)
        {
            if (_Card == null || PendingCardRefresh == null) return;
            for (int i = 0; i < PendingCardRefreshCount; i++)
            {
                if (PendingCardRefresh[i] == _Card)
                {
                    PendingCardRefresh[i] = null;
                    return;
                }
            }
        }

        // Declenche un sweep progressif (re-tente les cartes en attente)
        public void TriggerCardRefreshSweep()
        {
            if (PendingCardRefreshCount == 0) return;
            if (!CardRefreshSweepActive)
            {
                CardRefreshSweepActive = true;
                CardRefreshSweepIndex = 0;
            }
            else
            {
                // Deja en cours : re-boucler a la fin du sweep en cours
                CardRefreshRerun = true;
            }
        }

        // Traite un petit lot de cartes par frame (evite les pics de lag)
        private void ProcessCardRefreshBatch()
        {
            int _Processed = 0;
            while (CardRefreshSweepIndex < PendingCardRefreshCount && _Processed < CARD_REFRESH_PER_FRAME)
            {
                MTG_Card _Card = PendingCardRefresh[CardRefreshSweepIndex];
                CardRefreshSweepIndex++;
                if (_Card == null) continue;
                _Processed++;
                _Card.OnAtlasDataUpdated();
            }

            if (CardRefreshSweepIndex >= PendingCardRefreshCount)
            {
                CompactCardRefreshQueue();
                if (CardRefreshRerun && PendingCardRefreshCount > 0)
                {
                    // Un evenement est arrive pendant le sweep : on re-boucle
                    CardRefreshRerun = false;
                    CardRefreshSweepIndex = 0;
                }
                else
                {
                    CardRefreshSweepActive = false;
                    CardRefreshRerun = false;
                }
            }
        }

        // Compacte la file d'attente (retire les entrees null)
        private void CompactCardRefreshQueue()
        {
            int _Write = 0;
            for (int i = 0; i < PendingCardRefreshCount; i++)
            {
                MTG_Card _Card = PendingCardRefresh[i];
                if (_Card == null) continue;
                PendingCardRefresh[_Write] = _Card;
                _Write++;
            }
            PendingCardRefreshCount = _Write;
        }

        // Optimise : ne scanne que les atlas peuples (AtlasPopulatedIndices) au lieu des 4096
        public bool GetAtlasInfoForCard(string _CardId, out int _AtlasIndex, out Rect _UvRect)
        {
            this.VerboseLog($"GetAtlasInfoForCard called for cardId: {_CardId}");
            _AtlasIndex = -1;
            _UvRect = new Rect(0, 0, 1, 1);
            if (_CardId == "Debug") return true;

            if (AtlasPopulatedIndices == null || AtlasCardIds == null) return false;
            for (int _p = 0; _p < AtlasPopulatedCount; _p++)
            {
                int i = AtlasPopulatedIndices[_p];
                if (i < 0 || i >= AtlasCardIds.Length || AtlasCardIds[i] == null) continue;
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
            this.VerboseLog($"CardId: {_CardId} not found in any atlas ({AtlasPopulatedCount} populated)");
            return false;
        }

        public new int GetInstanceID()
        {
            return InstanceID;
        }

        // === GETTERS DEBUG ===
        public string GetUserKey()
        {
            if (string.IsNullOrEmpty(UserKey)) return "";
            return UserKey;
        }
        public bool IsAgreed()
        {
            return Agree;
        }
        public bool IsCurrentlySyncing()
        {
            return IsSyncing;
        }
        public int GetMaxAtlas()
        {
            return MaxAtlas;
        }
        // Nombre d'atlas avec une image chargee
        public int GetLoadedAtlasCount()
        {
            if (AtlasImages == null) return 0;
            int _Count = 0;
            for (int i = 0; i < AtlasImages.Length; i++)
                if (AtlasImages[i] != null) _Count++;
            return _Count;
        }
        // Nombre de cartes chargees (somme des ids non vides des atlas charges)
        public int GetLoadedCardsCount()
        {
            if (AtlasImages == null || AtlasCardIds == null) return 0;
            int _Count = 0;
            for (int i = 0; i < AtlasImages.Length; i++)
            {
                if (AtlasImages[i] == null || AtlasCardIds[i] == null) continue;
                for (int j = 0; j < AtlasCardIds[i].Length; j++)
                    if (!string.IsNullOrEmpty(AtlasCardIds[i][j])) _Count++;
            }
            return _Count;
        }
        // Nombre d'atlas en cours de telechargement
        public int GetLoadingAtlasCount()
        {
            if (AtlasLoading == null) return 0;
            int _Count = 0;
            for (int i = 0; i < AtlasLoading.Length; i++)
                if (AtlasLoading[i]) _Count++;
            return _Count;
        }
        // Nombre d'atlas en attente (ni charge ni en cours de telechargement)
        public int GetPendingAtlasCount()
        {
            if (AtlasLoaded == null || AtlasLoading == null) return 0;
            int _Count = 0;
            for (int i = 0; i < AtlasLoaded.Length && i < AtlasLoading.Length; i++)
                if (!AtlasLoaded[i] && !AtlasLoading[i]) _Count++;
            return _Count;
        }
        // Nombre total de rulings en cache
        public int GetRulingsCount()
        {
            if (CachedRulings == null) return 0;
            int _Count = 0;
            for (int i = 0; i < CachedRulings.Length; i++)
                if (CachedRulings[i] != null) _Count += CachedRulings[i].Count;
            return _Count;
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