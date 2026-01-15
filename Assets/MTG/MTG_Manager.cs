using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Data;
using VRC.SDK3.Image;

namespace MTG
{
    public class MTG_Manager : UdonSharpBehaviour
    {
        // Préfixe coloré pour les logs
        private const string LOG_PREFIX = "<color=#FF1493>[MTG_manager]</color> ";

        [UdonSynced, SerializeField]
        public int instanceID = -1;
        [SerializeField] public MTG_SyncInterface syncInterface;
        [SerializeField] private bool _isSyncing = false;
        [SerializeField] private bool agree = false;
        [SerializeField] public VRCUrl createURL;
        [SerializeField] public VRCUrl searchURL;
        [SerializeField] public VRCUrl deckURL;
        [SerializeField] public VRCUrl[] joinURLs;
        [SerializeField] public VRCUrl[] tempURLs;

        // Nouveau système de cache d'atlas compatible UdonSharp
        [SerializeField] public Texture2D[] atlasImages;
        [SerializeField] public string[][] atlasCardIds; // [atlas][slot]
        [SerializeField] public Rect[][] atlasCardRects; // [atlas][slot]
        [SerializeField] public bool[] atlasLoaded;
        [SerializeField] public bool[] atlasLoading;
        [SerializeField] public float lastAtlasInfoUpdate = 0f;
        [SerializeField] public const float ATLAS_INFO_UPDATE_INTERVAL = 10f; // 10 secondes
        
        // Cache des légalités et rulings (groupés par oracle_id)
        [SerializeField] private string[] cachedOracleIds; // Liste des oracle_ids
        [SerializeField] private DataDictionary[] cachedLegalities; // Légalités pour chaque oracle_id
        [SerializeField] private DataList[] cachedRulings; // Rulings pour chaque oracle_id
        
        // VRCImageDownloader pour les atlas
        private VRCImageDownloader imageDownloader;


#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public VRCUrl BaseURL = new VRCUrl("https://mtg.hactazia.fr/a");

        private void OnValidate()
        {
            createURL = new VRCUrl($"{BaseURL}c");
            searchURL = new VRCUrl($"{BaseURL}s?q=");
            deckURL = new VRCUrl($"{BaseURL}d?q=");
            joinURLs = new VRCUrl[64];
            for (int i = 0; i < joinURLs.Length; i++)
                joinURLs[i] = new VRCUrl($"{BaseURL}j{ToBase36(i)}");
            tempURLs = new VRCUrl[4096];
            for (int i = 0; i < tempURLs.Length; i++)
                tempURLs[i] = new VRCUrl($"{BaseURL}t{ToBase36(i)}");
        }

        private string ToBase36(int value)
        {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (value == 0) return "0";
            string result = "";
            while (value > 0)
            {
                result = chars[value % chars.Length] + result;
                value /= chars.Length;
            }
            return result;
        }
#endif

        private void Start()
        {
            // Initialiser le cache d'atlas
            int maxAtlas = tempURLs.Length;
            atlasImages = new Texture2D[maxAtlas];
            atlasCardIds = new string[maxAtlas][];
            atlasCardRects = new Rect[maxAtlas][];
            atlasLoaded = new bool[maxAtlas];
            atlasLoading = new bool[maxAtlas];
            for (int i = 0; i < maxAtlas; i++) {
                atlasCardIds[i] = new string[24];
                atlasCardRects[i] = new Rect[24];
                for (int j = 0; j < 24; j++) {
                    atlasCardIds[i][j] = null;
                    atlasCardRects[i][j] = new Rect(0, 0, 1, 1);
                }
                atlasImages[i] = null;
            }
            
            // Initialiser le cache des légalités et rulings
            cachedOracleIds = new string[0];
            cachedLegalities = new DataDictionary[0];
            cachedRulings = new DataList[0];
            
            // Créer VRCImageDownloader
            imageDownloader = new VRCImageDownloader();
            
            // Démarrer la mise à jour périodique des infos d'atlas
            // Udon: utiliser un système manuel avec Update()
            lastAtlasInfoUpdate = Time.time;
        }
        
        private void Update()
        {
            // Mise à jour périodique des atlas info
            if (Time.time - lastAtlasInfoUpdate >= ATLAS_INFO_UPDATE_INTERVAL)
            {
                lastAtlasInfoUpdate = Time.time;
                UpdateAtlasInfo();
            }
        }

        public override void OnDeserialization()
        {
            if (_isSyncing || agree) return;
            syncInterface.Show();
        }

        public override void OnMasterTransferred(VRCPlayerApi newMaster)
        {
            if (_isSyncing || !newMaster.isLocal || agree) return;
            syncInterface.Show();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (_isSyncing || !player.isLocal || agree) return;
            syncInterface.Show();
        }

        internal void JoinGame(bool show)
        {
            if (_isSyncing || agree) return;
            if (instanceID == -1)
                VRCStringDownloader.LoadUrl(createURL, (IUdonEventReceiver)this);
            else VRCStringDownloader.LoadUrl(joinURLs[instanceID], (IUdonEventReceiver)this);
            if (show) syncInterface.ShowLoading();
            _isSyncing = true;
        }

        private bool IsJoinOrCreateResponse(IVRCStringDownload json)
        {
            if (json == null)
            {
                Debug.LogError(LOG_PREFIX + "Null JSON in response");
                return false;
            }

            if (json.Url == null)
            {
                Debug.LogError(LOG_PREFIX + "Null URL in response");
                return false;
            }

            var url = json.Url.ToString();
            if (url == createURL.ToString())
                return true;
            for (int i = 0; i < joinURLs.Length; i++)
                if (url == joinURLs[i].ToString())
                    return true;
            return false;
        }

        public override void OnStringLoadSuccess(IVRCStringDownload json)
        {
            // Vérifier si c'est une réponse de légalités/rulings
            if (IsLegalitiesResponse(json))
            {
                ProcessLegalitiesAndRulings(json.Result);
                return;
            }
            
            // Vérifier si c'est une réponse d'atlas info
            if (IsAtlasInfoResponse(json))
            {
                ProcessAtlasInfo(json.Result);
                return;
            }
            
            // Vérifier si c'est une réponse d'atlas
            if (IsAtlasResponse(json, out int atlasIndex))
            {
                ProcessAtlasImage(json, atlasIndex);
                return;
            }
            
            // Sinon, traiter comme réponse de join/create
            if (!IsJoinOrCreateResponse(json)) return;

            if (VRCJson.TryDeserializeFromJson(json.Result, out DataToken result))
            {
                if (result.TokenType != TokenType.DataDictionary)
                {
                    Debug.LogError(LOG_PREFIX + $"Error parsing response: {json.Result}");
                    _isSyncing = false;
                    if (!agree)
                        syncInterface.Show();
                    return;
                }

                var dict = result.DataDictionary;
                
                // Vérifier si l'objet "instance" existe (ancien format)
                if (dict.ContainsKey("instance") && dict["instance"].TokenType == TokenType.DataDictionary)
                {
                    var instanceDict = dict["instance"].DataDictionary;
                    
                    // Vérifier si l'ID existe dans l'instance
                    if (!instanceDict.ContainsKey("id") || instanceDict["id"].TokenType != TokenType.Double)
                    {
                        Debug.LogError(LOG_PREFIX + $"Error parsing response: missing 'id' in instance object: {json.Result}");
                        _isSyncing = false;
                        if (!agree)
                            syncInterface.Show();
                        return;
                    }

                    // SOLUTION : Utiliser .Double puis convertir en int
                    instanceID = (int)instanceDict["id"].Double;
                    
                    // OU alternative plus sûre :
                    // instanceID = Mathf.RoundToInt((float)instanceDict["id"].Double);

                    Debug.Log(LOG_PREFIX + $"Joined game {instanceID}");
                    agree = true;
                    _isSyncing = false;
                    syncInterface.Hide();
                    return;
                }
                // Nouveau format avec "iid"
                else if (dict.ContainsKey("iid") && dict["iid"].TokenType == TokenType.Double)
                {
                    instanceID = (int)dict["iid"].Double;
                    
                    Debug.Log(LOG_PREFIX + $"Joined game {instanceID}");
                    agree = true;
                    _isSyncing = false;
                    syncInterface.Hide();
                    return;
                }
                else
                {
                    Debug.LogError(LOG_PREFIX + $"Error parsing response: neither 'instance.id' nor 'iid' found: {json.Result}");
                    _isSyncing = false;
                    if (!agree)
                        syncInterface.Show();
                    return;
                }
            }

            Debug.LogError(LOG_PREFIX + $"Error parsing response: {json.Result}");
            _isSyncing = false;
            if (!agree)
                syncInterface.Show();
        }


        public override void OnStringLoadError(IVRCStringDownload result)
        {
            if (!IsJoinOrCreateResponse(result)) return;

            Debug.LogError(LOG_PREFIX + $"Error loading URL: {result.Error}");
            _isSyncing = false;
            if (!agree)
                syncInterface.Show();
        }
        
        public int GetGameInstanceID()
        {
            return instanceID;
        }
        
        // Méthodes de gestion d'atlas
        public void UpdateAtlasInfo()
        {
            if (instanceID == -1) return;
            
            // Utiliser l'URL pré-configurée pour /at/0 (index 0)
            if (tempURLs.Length > 0)
            {
                VRCStringDownloader.LoadUrl(tempURLs[0], (IUdonEventReceiver)this);
            }
            
            // Charger aussi les légalités et rulings depuis /at3 (tempURLs[3])
            if (tempURLs.Length > 3)
            {
                VRCStringDownloader.LoadUrl(tempURLs[3], (IUdonEventReceiver)this);
            }
        }
        
        public Texture2D GetAtlasTexture(int atlasIndex)
        {
            if (atlasIndex < 0 || atlasIndex >= atlasImages.Length)
            {
                Debug.LogWarning(LOG_PREFIX + $"GetAtlasTexture: Invalid atlas index {atlasIndex}");
                return null;
            }
                
            if (!atlasLoaded[atlasIndex] && !atlasLoading[atlasIndex])
            {
                Debug.Log(LOG_PREFIX + $"GetAtlasTexture: Atlas {atlasIndex} not loaded yet, triggering download");
                // Déclencher le téléchargement de l'atlas
                LoadAtlas(atlasIndex);
            }
            else if (atlasLoading[atlasIndex])
            {
                Debug.Log(LOG_PREFIX + $"GetAtlasTexture: Atlas {atlasIndex} currently loading...");
            }
            else if (atlasLoaded[atlasIndex])
            {
                Debug.Log(LOG_PREFIX + $"GetAtlasTexture: Atlas {atlasIndex} already loaded, returning texture");
            }
            
            return atlasImages[atlasIndex];
        }
        


        private void LoadAtlas(int atlasIndex)
        {
            if (atlasIndex < 0 || atlasIndex >= tempURLs.Length || atlasLoading[atlasIndex])
            {
                Debug.LogWarning(LOG_PREFIX + $"LoadAtlas: Cannot load atlas {atlasIndex} (index valid: {atlasIndex >= 0 && atlasIndex < tempURLs.Length}, already loading: {atlasIndex >= 0 && atlasIndex < atlasLoading.Length && atlasLoading[atlasIndex]})");
                return;
            }

            atlasLoading[atlasIndex] = true;
            
            Debug.Log(LOG_PREFIX + $"LoadAtlas: Starting download for atlas {atlasIndex} from URL: {tempURLs[atlasIndex]}");
            
            // Utiliser VRCImageDownloader pour télécharger l'image
            imageDownloader.DownloadImage(tempURLs[atlasIndex], null, (IUdonEventReceiver)this);
        }
        
        private bool IsAtlasInfoResponse(IVRCStringDownload json)
        {
            if (json == null || json.Url == null) return false;
            string url = json.Url.ToString();
            return url.Contains("at0"); // at0 pour l'atlas info
        }
        
        private bool IsAtlasResponse(IVRCStringDownload result, out int atlasIndex)
        {
            atlasIndex = -1;
            if (result == null || result.Url == null) return false;
            
            for (int i = 0; i < tempURLs.Length; i++)
            {
                if (result.Url == tempURLs[i])
                {
                    atlasIndex = i;
                    return true;
                }
            }
            return false;
        }
        
        private void ProcessAtlasInfo(string jsonResult)
        {
            if (!VRCJson.TryDeserializeFromJson(jsonResult, out DataToken result) || result.TokenType != TokenType.DataDictionary)
            {
                Debug.LogError(LOG_PREFIX + "Error parsing atlas info: " + jsonResult);
                return;
            }
            
            var dict = result.DataDictionary;
            if (!dict.ContainsKey("data") || dict["data"].TokenType != TokenType.DataDictionary)
            {
                return;
            }
            
            var data = dict["data"].DataDictionary;
            if (!data.ContainsKey("batches"))
            {
                return;
            }
            
            var batches = data["batches"].DataList;
            
            Debug.Log(LOG_PREFIX + $"Processing {batches.Count} atlas batches in manager");
            
            // Remplir le cache d'atlas (ids et rects) et marquer les atlas nécessaires pour téléchargement
            for (int i = 0; i < batches.Count; i++)
            {
                if (batches[i].TokenType != TokenType.DataDictionary) continue;
                var batch = batches[i].DataDictionary;
                if (!batch.ContainsKey("atlas_link") || !batch.ContainsKey("cards")) continue;
                string atlasLink = batch["atlas_link"].String;
                int atlasIndex = ConvertAtlasLinkToIndex(atlasLink);
                if (atlasIndex < 0 || atlasIndex >= atlasImages.Length) continue;

                var cardsInBatch = batch["cards"].DataList;
                int count = cardsInBatch.Count;
                // Clamp à 24 pour éviter overflow
                if (count > 24) count = 24;
                
                // Vérifier si les données de l'atlas ont changé
                bool atlasDataChanged = HasAtlasDataChanged(atlasIndex, cardsInBatch, count);
                
                // Remplir les nouvelles données
                for (int j = 0; j < count; j++)
                {
                    // Les cartes sont maintenant de simples strings: "card_id:face"
                    if (cardsInBatch[j].TokenType != TokenType.String) continue;
                    string id = cardsInBatch[j].String;
                    
                    // Calculer automatiquement les rect transforms (grille 6x4)
                    // Les cartes sont toujours dans le même ordre: ligne par ligne, de gauche à droite
                    int col = j % 6;  // Colonne (0-5)
                    int row = j / 6;  // Ligne (0-3)
                    
                    float rectWidth = 1f / 6f;  // 0.16666666666666666
                    float rectHeight = 0.25006751593088666f;  // Hauteur fixe
                    float x = col * rectWidth;
                    float y = 0.7499324840691133f - (row * rectHeight);  // Y inversé, commence en haut
                    
                    atlasCardIds[atlasIndex][j] = id;
                    atlasCardRects[atlasIndex][j] = new Rect(x, y, rectWidth, rectHeight);
                }
                
                // Effacer les slots restants si moins de 24 cartes
                for (int j = count; j < 24; j++)
                {
                    atlasCardIds[atlasIndex][j] = null;
                    atlasCardRects[atlasIndex][j] = new Rect(0, 0, 1, 1);
                }

                // Re-télécharger l'atlas si les données ont changé
                if (atlasDataChanged)
                {
                    Debug.Log(LOG_PREFIX + $"Atlas {atlasIndex} data changed, reloading...");
                    atlasLoaded[atlasIndex] = false;
                    atlasLoading[atlasIndex] = false;
                    atlasImages[atlasIndex] = null;
                    LoadAtlas(atlasIndex);
                }
                // Sinon, marquer l'atlas pour téléchargement si pas encore chargé
                else if (!atlasLoaded[atlasIndex] && !atlasLoading[atlasIndex])
                {
                    LoadAtlas(atlasIndex);
                }
            }
        }
        
        // Vérifie si les données d'un atlas ont changé (nombre de cartes ou IDs différents)
        private bool HasAtlasDataChanged(int atlasIndex, DataList newCards, int newCount)
        {
            // Si l'atlas n'a jamais été chargé, pas de changement à détecter
            if (!atlasLoaded[atlasIndex])
                return false;
            
            // Compter le nombre de cartes actuellement dans l'atlas
            int oldCount = 0;
            for (int i = 0; i < 24; i++)
            {
                if (atlasCardIds[atlasIndex][i] != null)
                    oldCount++;
                else
                    break; // Les cartes sont stockées de manière contiguë
            }
            
            // Si le nombre de cartes a changé, c'est un changement
            if (oldCount != newCount)
                return true;
            
            // Vérifier si les IDs ont changé
            for (int i = 0; i < newCount; i++)
            {
                if (newCards[i].TokenType != TokenType.String)
                    continue;
                string newId = newCards[i].String;
                string oldId = atlasCardIds[atlasIndex][i];
                
                if (oldId != newId)
                    return true;
            }
            
            return false;
        }
        
        private void ProcessAtlasImage(IVRCStringDownload result, int atlasIndex)
        {
            // Cette méthode n'est plus utilisée car on utilise VRCImageDownloader
            // Les callbacks OnImageLoadSuccess/OnImageLoadError sont utilisés à la place
        }
        
        // Callbacks pour VRCImageDownloader
        public override void OnImageLoadSuccess(IVRCImageDownload result)
        {
            // Trouver l'index de l'atlas depuis l'URL
            if (IsAtlasImageResponse(result, out int atlasIndex))
            {
                atlasImages[atlasIndex] = result.Result;
                atlasLoaded[atlasIndex] = true;
                atlasLoading[atlasIndex] = false;
                Debug.Log(LOG_PREFIX + $"Atlas {atlasIndex} loaded successfully via VRCImageDownloader");
                // Notifier les cartes que l'atlas est disponible
                NotifyAtlasLoaded(atlasIndex);
            }
        }
        
        public override void OnImageLoadError(IVRCImageDownload result)
        {
            // Trouver l'index de l'atlas depuis l'URL
            if (IsAtlasImageResponse(result, out int atlasIndex))
            {
                atlasLoading[atlasIndex] = false;
                Debug.LogError(LOG_PREFIX + $"Failed to load atlas {atlasIndex}: {result.Error}");
            }
        }
        
        private bool IsAtlasImageResponse(IVRCImageDownload result, out int atlasIndex)
        {
            atlasIndex = -1;
            if (result == null || result.Url == null) return false;
            
            for (int i = 0; i < tempURLs.Length; i++)
            {
                if (result.Url == tempURLs[i])
                {
                    atlasIndex = i;
                    return true;
                }
            }
            return false;
        }
        
        private void NotifyAtlasLoaded(int atlasIndex)
        {
            // Cette méthode sera appelée par les cartes pour être notifiées
            // Nous utiliserons un système d'événements plus tard si nécessaire
        }
        
        private int ConvertAtlasLinkToIndex(string atlasLink)
        {
            if (string.IsNullOrEmpty(atlasLink) || !atlasLink.StartsWith("/at"))
            {
                return 0;
            }
            
            string base36Suffix = atlasLink.Substring(3); // Enlever "/at"
            if (string.IsNullOrEmpty(base36Suffix))
            {
                return 0;
            }
            
            return FromBase36(base36Suffix);
        }

        private int FromBase36(string value)
        {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            int result = 0;
            int multiplier = 1;

            for (int i = value.Length - 1; i >= 0; i--)
            {
                char c = value[i];
                int digit = chars.IndexOf(c);
                if (digit == -1)
                {
                    return 0;
                }
                result += digit * multiplier;
                multiplier *= 36;
            }

            return result;
        }
        // Permet à une carte de retrouver son atlas et son rect à partir de son id
        public bool GetAtlasInfoForCard(string cardId, out int atlasIndex, out Rect uvRect)
        {
            atlasIndex = -1;
            uvRect = new Rect(0, 0, 1, 1);
            
            //Debug.Log(LOG_PREFIX + $"GetAtlasInfoForCard called for: {cardId}");
            
            for (int i = 0; i < atlasCardIds.Length; i++)
            {
                for (int j = 0; j < 24; j++)
                {
                    if (atlasCardIds[i][j] == null) continue;
                    if (atlasCardIds[i][j] == cardId)
                    {
                        atlasIndex = i;
                        uvRect = atlasCardRects[i][j];
                        //Debug.Log(LOG_PREFIX + $"Found card {cardId} in atlas {i}, rect: {uvRect}");
                        return true;
                    }
                }
            }
            Debug.LogWarning(LOG_PREFIX + $"Card {cardId} NOT FOUND in any atlas!");
            return false;
        }
        
        // === Méthodes pour les légalités et rulings ===
        
        /// <summary>
        /// Charge les légalités et rulings depuis /at3 (tempURLs[3])
        /// Appelée automatiquement toutes les 10 secondes via UpdateAtlasInfo()
        /// </summary>
        public void LoadLegalitiesAndRulings()
        {
            if (instanceID == -1)
            {
                Debug.LogWarning(LOG_PREFIX + "Cannot load legalities: no instance");
                return;
            }
            
            if (tempURLs == null || tempURLs.Length <= 3)
            {
                Debug.LogError(LOG_PREFIX + "tempURLs not initialized or too short");
                return;
            }
            
            Debug.Log(LOG_PREFIX + "Loading legalities and rulings from /at3...");
            VRCStringDownloader.LoadUrl(tempURLs[3], (IUdonEventReceiver)this);
        }
        
        private bool IsLegalitiesResponse(IVRCStringDownload json)
        {
            if (json == null || json.Url == null) return false;
            if (tempURLs == null || tempURLs.Length <= 3) return false;
            return json.Url.ToString() == tempURLs[3].ToString();
        }
        
        private void ProcessLegalitiesAndRulings(string jsonResult)
        {
            if (!VRCJson.TryDeserializeFromJson(jsonResult, out DataToken result) || result.TokenType != TokenType.DataDictionary)
            {
                Debug.LogError(LOG_PREFIX + "Error parsing legalities response: " + jsonResult);
                return;
            }
            
            var dict = result.DataDictionary;
            if (!dict.ContainsKey("data") || dict["data"].TokenType != TokenType.DataDictionary)
            {
                Debug.LogError(LOG_PREFIX + "Missing 'data' in legalities response");
                return;
            }
            
            var data = dict["data"].DataDictionary;
            if (!data.ContainsKey("cards") || data["cards"].TokenType != TokenType.DataList)
            {
                Debug.LogError(LOG_PREFIX + "Missing 'cards' in legalities data");
                return;
            }
            
            var cards = data["cards"].DataList;
            int cardCount = cards.Count;
            
            Debug.Log(LOG_PREFIX + $"Processing legalities for {cardCount} unique cards");
            
            // Initialiser les nouveaux tableaux
            cachedOracleIds = new string[cardCount];
            cachedLegalities = new DataDictionary[cardCount];
            cachedRulings = new DataList[cardCount];
            
            // Parser chaque carte
            for (int i = 0; i < cardCount; i++)
            {
                if (cards[i].TokenType != TokenType.DataDictionary) continue;
                
                var card = cards[i].DataDictionary;
                
                // Récupérer oracle_id
                if (!card.ContainsKey("oracle_id") || card["oracle_id"].TokenType != TokenType.String)
                    continue;
                    
                string oracleId = card["oracle_id"].String;
                cachedOracleIds[i] = oracleId;
                
                // Récupérer légalités
                if (card.ContainsKey("legalities") && card["legalities"].TokenType == TokenType.DataDictionary)
                {
                    cachedLegalities[i] = card["legalities"].DataDictionary;
                }
                else
                {
                    cachedLegalities[i] = new DataDictionary();
                }
                
                // Récupérer rulings
                if (card.ContainsKey("rulings") && card["rulings"].TokenType == TokenType.DataList)
                {
                    cachedRulings[i] = card["rulings"].DataList;
                }
                else
                {
                    cachedRulings[i] = new DataList();
                }
            }
            
            Debug.Log(LOG_PREFIX + $"✅ Legalities and rulings loaded for {cardCount} cards");
        }
        
        /// <summary>
        /// Récupère les légalités pour un oracle_id donné
        /// </summary>
        public bool GetLegalitiesForOracle(string oracleId, out DataDictionary legalities)
        {
            legalities = new DataDictionary();
            
            if (cachedOracleIds == null || cachedOracleIds.Length == 0)
            {
                return false;
            }
            
            for (int i = 0; i < cachedOracleIds.Length; i++)
            {
                if (cachedOracleIds[i] == oracleId)
                {
                    legalities = cachedLegalities[i];
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Récupère les rulings pour un oracle_id donné
        /// </summary>
        public bool GetRulingsForOracle(string oracleId, out DataList rulings)
        {
            rulings = new DataList();
            
            if (cachedOracleIds == null || cachedOracleIds.Length == 0)
            {
                return false;
            }
            
            for (int i = 0; i < cachedOracleIds.Length; i++)
            {
                if (cachedOracleIds[i] == oracleId)
                {
                    rulings = cachedRulings[i];
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Vérifie si une carte est légale dans un format donné
        /// </summary>
        public bool IsCardLegalInFormat(string oracleId, string format, out string legalityStatus)
        {
            legalityStatus = "not_legal";
            
            if (GetLegalitiesForOracle(oracleId, out DataDictionary legalities))
            {
                if (legalities.ContainsKey(format) && legalities[format].TokenType == TokenType.String)
                {
                    legalityStatus = legalities[format].String;
                    return legalityStatus == "legal";
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Retourne le nombre de cartes uniques (par oracle) en cache
        /// </summary>
        public int GetCachedOracleCount()
        {
            return cachedOracleIds != null ? cachedOracleIds.Length : 0;
        }
        
    }
}