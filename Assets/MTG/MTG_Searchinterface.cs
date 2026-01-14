using System;
using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Data;
using VRC.SDK3.Components;
using TMPro;
using System.Collections.Generic;

namespace MTG
{
    public class MTG_Searchinterface : UdonSharpBehaviour
    {
        // Préfixe coloré pour les logs
        private const string LOG_PREFIX = "<color=#FF1493>[MTG_Searchinterface]</color> ";
        
        [Header("=== REFERENCES ===")]
        
        public MTG_Manager manager;
        public GameObject CardPrefab;
        public Transform CardParent;
        public GameObject CardPreview;
        
        [Header("=== PHYSIC CARD SPAWN ===")]
        public GameObject PhysicCardPrefab;
        public Transform PhysicCardSpawnPoint;
        public Transform PhysicCardsParent; // Parent pour organiser les cartes spawned
        public UnityEngine.UI.Button SpawnPhysicCardButton;
        public MTG_PhysicCardPoolManager physicCardPoolManager; // Pool manager pour synchroniser les cartes
        
        [Header("=== INPUT FIELD ===")]
            
        public VRCUrlInputField validatedInput;
        public TMPro.TMP_InputField urlDisplayText;  // Champ TMP pour afficher l'URL construite

        [Header("=== SEARCH FILTERS - INPUT FIELDS ===")]
        // TextMeshPro input fields
        public TMPro.TMP_InputField cardNameInput;   // Card Name
        public TMPro.TMP_InputField cardTextInput;   // Card Text
        public TMPro.TMP_InputField typeLineInput;   // Type line
        public TMPro.TMP_InputField cmcInput;        // CMC

        [Header("=== SEARCH FILTERS - DROPDOWNS ===")]
        public TMPro.TMP_Dropdown setDropdown;       // Set
        public TMPro.TMP_Dropdown langDropdown;      // Lang

        [Header("=== COLOR BUTTONS ===")]
        public UnityEngine.UI.Button colorW_Button;
        public UnityEngine.UI.Button colorU_Button;
        public UnityEngine.UI.Button colorB_Button;
        public UnityEngine.UI.Button colorR_Button;
        public UnityEngine.UI.Button colorG_Button;
        public UnityEngine.UI.Button colorC_Button;

        [Header("=== RARITY BUTTONS ===")]
        public UnityEngine.UI.Button rarityCommon_Button;
        public UnityEngine.UI.Button rarityUncommon_Button;
        public UnityEngine.UI.Button rarityRare_Button;
        public UnityEngine.UI.Button rarityMythic_Button;

        // États des boutons (sélectionnés ou non)
        private bool colorW_Selected = false;
        private bool colorU_Selected = false;
        private bool colorB_Selected = false;
        private bool colorR_Selected = false;
        private bool colorG_Selected = false;
        private bool colorC_Selected = false;
        
        private bool rarityCommon_Selected = false;
        private bool rarityUncommon_Selected = false;
        private bool rarityRare_Selected = false;
        private bool rarityMythic_Selected = false;

        private int currentLoadIndex = 0;
        private DataList cardsToLoad;
        private float nextLoadTime = 0f;
        private GameObject[] instantiatedCards = new GameObject[128];
        private int instantiatedCardsCount = 0;
        private string[] cardKeys = new string[128];
        private int cardKeysCount = 0;
        
        // Variables pour la suppression progressive des anciennes cartes
        private bool isDeletingOldCards = false;
        private int deleteIndex = 0;
        private float nextDeleteTime = 0f;
        private const float DELETE_INTERVAL = 0.1f; // Intervalle entre chaque suppression
        private const int CARDS_PER_DELETE_BATCH = 3; // Réduire à 3 cartes par lot pour éviter les surcharges
        
        private const float LOAD_INTERVAL = 0.15f; // Intervalle entre chaque chargement
        private const int CARDS_PER_LOAD_BATCH = 3; // Réduire à 3 cartes par lot
        private bool clearAllRequested = false;
        
        // Variable pour stocker la réponse serveur en attente de traitement
        private string pendingSearchResponse = null;
        private bool hasPendingResponse = false;
        
        // Données pour remplir les dropdowns
        private bool dropdownDataLoaded = false;
        private float dropdownLoadTime = 0f;
        private bool dropdownLoadScheduled = false;
        
        // Variables pour stocker les valeurs sélectionnées des dropdowns
        private string[] setDropdownOptions;
        [SerializeField] private string[] langDropdownOptions;
        
        // Variables pour le chargement progressif des sets
        private DataList setsToLoad;
        private int currentSetLoadIndex = 0;
        private float nextSetLoadTime = 0f;
        private const float SET_LOAD_INTERVAL = 0.05f; // Intervalle entre chaque batch de sets
        private const int SETS_PER_BATCH = 50; // Charger 50 sets à la fois
        private bool isLoadingSets = false;
        
        // Variables pour le traitement progressif de l'atlas info
        private DataList atlasBatches;
        private int currentAtlasCardIndex = 0;
        private float nextAtlasProcessTime = 0f;
        private const float ATLAS_PROCESS_INTERVAL = 0.05f;
        private const int CARDS_PER_ATLAS_BATCH = 5; // Traiter 5 cartes par batch
        private bool isProcessingAtlasInfo = false;

        #if !COMPILER_UDONSHARP && UNITY_EDITOR
        private void OnValidate()
        {
            // Remplir le tableau des langues depuis le dropdown dans l'éditeur uniquement
            if (langDropdown != null && langDropdown.options != null)
            {
                int optionCount = langDropdown.options.Count;
                langDropdownOptions = new string[optionCount];
                
                for (int i = 0; i < optionCount; i++)
                {
                    langDropdownOptions[i] = langDropdown.options[i].text;
                }
                
                Debug.Log(LOG_PREFIX + $"[Editor] Initialized langDropdownOptions with {optionCount} options");
            }
        }
        #endif

        private void Start()
        {
            Debug.Log(LOG_PREFIX + "Start() called - MTG_Searchinterface initialized");
            
            // Planifier le chargement des dropdowns après 10 secondes
            dropdownLoadTime = Time.time + 10f;
            dropdownLoadScheduled = true;
            
            // Initialiser langDropdownOptions au runtime
            InitializeLangOptions();
        }
        
        private void InitializeLangOptions()
        {
            // Créer le tableau manuellement avec les langues qu'on voit dans le dropdown (18 éléments)
            // Directement les codes langue, pas de "Select Language"
            langDropdownOptions = new string[18];
            langDropdownOptions[0] = "EN";
            langDropdownOptions[1] = "JA";
            langDropdownOptions[2] = "FR";
            langDropdownOptions[3] = "DE";
            langDropdownOptions[4] = "ES";
            langDropdownOptions[5] = "IT";
            langDropdownOptions[6] = "ZHS";
            langDropdownOptions[7] = "PT";
            langDropdownOptions[8] = "ZHT";
            langDropdownOptions[9] = "RU";
            langDropdownOptions[10] = "KO";
            langDropdownOptions[11] = "PH";
            langDropdownOptions[12] = "QYA";
            langDropdownOptions[13] = "GRC";
            langDropdownOptions[14] = "HE";
            langDropdownOptions[15] = "AR";
            langDropdownOptions[16] = "LA";
            langDropdownOptions[17] = "SA";
            
            Debug.Log(LOG_PREFIX + $"Initialized langDropdownOptions with {langDropdownOptions.Length} hardcoded options");
        }

        private void LoadDropdownData()
        {
            // tempURLs[2] correspond à /at2 qui devrait retourner les listes de sets et blocs
            if (manager.tempURLs != null && manager.tempURLs.Length > 2)
            {
                Debug.Log(LOG_PREFIX + "Loading dropdown data from /at2");
                VRCStringDownloader.LoadUrl(manager.tempURLs[2], (IUdonEventReceiver)this);
                dropdownLoadScheduled = false;
            }
            else
            {
                Debug.LogError(LOG_PREFIX + "tempURLs[2] not available for dropdown data");
            }
        }
        
        // Surcharge pour les événements OnEndEdit des TMP_InputField (qui passent une string)
        public void EditSearchUrl(string unused)
        {
            EditSearchUrl();
        }
        
        public void EditSearchUrl()
        {
            Debug.Log(LOG_PREFIX + "EditSearchUrl() called");
            
            // Désactiver temporairement l'interactabilité de tous les champs de texte pour enlever le focus
            DisableAndEnableInputFields();
            
            // Construire la requête de recherche à partir des champs
            StringBuilder queryBuilder = new StringBuilder();
            
            // Card Name
            if (cardNameInput != null && !string.IsNullOrEmpty(cardNameInput.text))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("name:\"");
                queryBuilder.Append(cardNameInput.text);
                queryBuilder.Append("\"");
            }
            
            // Card Text (oracle)
            if (cardTextInput != null && !string.IsNullOrEmpty(cardTextInput.text))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("o:\"");
                queryBuilder.Append(cardTextInput.text);
                queryBuilder.Append("\"");
            }
            
            // Type Line
            if (typeLineInput != null && !string.IsNullOrEmpty(typeLineInput.text))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("t:\"");
                queryBuilder.Append(typeLineInput.text);
                queryBuilder.Append("\"");
            }
            
            // CMC
            if (cmcInput != null && !string.IsNullOrEmpty(cmcInput.text))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("cmc:");
                queryBuilder.Append(cmcInput.text);
            }
            
            // Set (ignorer si première option)
            if (setDropdown != null && setDropdown.value > 0 && setDropdownOptions != null && setDropdown.value < setDropdownOptions.Length)
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("set:");
                
                // Extraire le code entre parenthèses (ex: "15th Anniversary Cards (p15a)" -> "p15a")
                string setOption = setDropdownOptions[setDropdown.value];
                int openParen = setOption.LastIndexOf('(');
                int closeParen = setOption.LastIndexOf(')');
                
                if (openParen != -1 && closeParen != -1 && closeParen > openParen)
                {
                    // Extraire le texte entre parenthèses
                    string setCode = setOption.Substring(openParen + 1, closeParen - openParen - 1);
                    queryBuilder.Append(setCode);
                }
                else
                {
                    // Pas de parenthèses, utiliser le texte complet
                    queryBuilder.Append(setOption);
                }
            }
            
            // Lang
            Debug.Log(LOG_PREFIX + "EditSearchUrl - Checking Lang dropdown...");
            if (langDropdown != null)
            {
                Debug.Log(LOG_PREFIX + $"EditSearchUrl - langDropdown.value = {langDropdown.value}");
                Debug.Log(LOG_PREFIX + $"EditSearchUrl - langDropdownOptions = {(langDropdownOptions == null ? "NULL" : langDropdownOptions.Length.ToString() + " items")}");
            }
            
            // Plus de vérification > 0 car index 0 est maintenant "EN" (pas de "Select Language")
            if (langDropdown != null && langDropdownOptions != null && langDropdown.value >= 0 && langDropdown.value < langDropdownOptions.Length)
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("l:");
                // Utiliser le tableau langDropdownOptions (pré-rempli dans Start)
                string selectedLang = langDropdownOptions[langDropdown.value].ToLower();
                Debug.Log(LOG_PREFIX + $"EditSearchUrl - Adding lang filter: l:{selectedLang}");
                queryBuilder.Append(selectedLang);
            }
            else
            {
                Debug.Log(LOG_PREFIX + "EditSearchUrl - Lang filter NOT added");
            }
            
            // Colors
            string colorQuery = BuildColorQuery();
            if (!string.IsNullOrEmpty(colorQuery))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append(colorQuery);
            }
            
            // Rarities
            string rarityQuery = BuildRarityQuery();
            if (!string.IsNullOrEmpty(rarityQuery))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append(rarityQuery);
            }
            
            // Construire l'URL complète
            string finalQuery = queryBuilder.ToString();
            if (string.IsNullOrEmpty(finalQuery))
            {
                finalQuery = ""; // Requête vide par défaut
            }
            
            string fullUrl = manager.searchURL.ToString() + finalQuery;
            
            Debug.Log(LOG_PREFIX + "Full URL to set: " + fullUrl);
            
            // Afficher l'URL dans le champ de texte séparé si disponible
            if (urlDisplayText != null)
            {
                urlDisplayText.text = fullUrl;
                Debug.Log(LOG_PREFIX + "URL displayed in urlDisplayText");
            }
            
            // Mettre à jour le VRCUrlInputField avec SetUrl (comme dans Start)
            // Note: On ne peut utiliser SetUrl qu'avec des VRCUrl pré-créés
            if (string.IsNullOrEmpty(finalQuery))
            {
                // Pas de filtres, réinitialiser à l'URL de base
                validatedInput.SetUrl(manager.searchURL);
                Debug.Log(LOG_PREFIX + "Reset VRCUrlInputField to base searchURL");
            }
            else
            {
                // LIMITATION: On ne peut pas créer de VRCUrl à runtime
                // L'URL est affichée dans urlDisplayText, l'utilisateur peut la copier
                Debug.Log(LOG_PREFIX + "URL with filters displayed in text field (VRCUrl limitation)");
            }
            
            Debug.Log(LOG_PREFIX + "Search URL update complete");
        }
        
        private void DisableAndEnableInputFields()
        {
            // Désactiver puis réactiver immédiatement tous les champs de texte pour enlever le focus
            if (cardNameInput != null)
            {
                cardNameInput.interactable = false;
                cardNameInput.interactable = true;
            }
            
            if (cardTextInput != null)
            {
                cardTextInput.interactable = false;
                cardTextInput.interactable = true;
            }
            
            if (typeLineInput != null)
            {
                typeLineInput.interactable = false;
                typeLineInput.interactable = true;
            }
            
            if (cmcInput != null)
            {
                cmcInput.interactable = false;
                cmcInput.interactable = true;
            }
        }
        
        private string BuildColorQuery()
        {
            Debug.Log(LOG_PREFIX + "BuildColorQuery() called");
            
            // Si colorless est sélectionné, retourner uniquement "c:c"
            if (colorC_Selected)
            {
                return "c:c";
            }
            
            // Construire la chaîne de couleurs
            StringBuilder colors = new StringBuilder();
            if (colorW_Selected) colors.Append("w");
            if (colorU_Selected) colors.Append("u");
            if (colorB_Selected) colors.Append("b");
            if (colorR_Selected) colors.Append("r");
            if (colorG_Selected) colors.Append("g");
            
            if (colors.Length > 0)
            {
                return "c:" + colors.ToString();
            }
            
            return "";
        }
        
        private string BuildRarityQuery()
        {
            Debug.Log(LOG_PREFIX + "BuildRarityQuery() called");
            
            StringBuilder rarities = new StringBuilder();
            
            if (rarityCommon_Selected || rarityUncommon_Selected || rarityRare_Selected || rarityMythic_Selected)
            {
                bool first = true;
                
                if (rarityCommon_Selected)
                {
                    if (!first) rarities.Append(" or ");
                    rarities.Append("r:common");
                    first = false;
                }
                if (rarityUncommon_Selected)
                {
                    if (!first) rarities.Append(" or ");
                    rarities.Append("r:uncommon");
                    first = false;
                }
                if (rarityRare_Selected)
                {
                    if (!first) rarities.Append(" or ");
                    rarities.Append("r:rare");
                    first = false;
                }
                if (rarityMythic_Selected)
                {
                    if (!first) rarities.Append(" or ");
                    rarities.Append("r:mythic");
                    first = false;
                }
            }
            
            return rarities.ToString();
        }
        
        // === FONCTIONS POUR LES BOUTONS DE COULEUR ===
        
        public void OnColorW_Clicked()
        {
            Debug.Log(LOG_PREFIX + "OnColorW_Clicked() called");
            colorW_Selected = !colorW_Selected;
            if (colorW_Selected && colorC_Selected)
            {
                colorC_Selected = false;
            }
            EditSearchUrl();
        }
        
        public void OnColorU_Clicked()
        {
            Debug.Log(LOG_PREFIX + "OnColorU_Clicked() called");
            colorU_Selected = !colorU_Selected;
            if (colorU_Selected && colorC_Selected)
            {
                colorC_Selected = false;
            }
            EditSearchUrl();
        }
        
        public void OnColorB_Clicked()
        {
            Debug.Log(LOG_PREFIX + "OnColorB_Clicked() called");
            colorB_Selected = !colorB_Selected;
            if (colorB_Selected && colorC_Selected)
            {
                colorC_Selected = false;
            }
            EditSearchUrl();
        }
        
        public void OnColorR_Clicked()
        {
            Debug.Log(LOG_PREFIX + "OnColorR_Clicked() called");
            colorR_Selected = !colorR_Selected;
            if (colorR_Selected && colorC_Selected)
            {
                colorC_Selected = false;
            }
            EditSearchUrl();
        }
        
        public void OnColorG_Clicked()
        {
            Debug.Log(LOG_PREFIX + "OnColorG_Clicked() called");
            colorG_Selected = !colorG_Selected;
            if (colorG_Selected && colorC_Selected)
            {
                colorC_Selected = false;
            }
            EditSearchUrl();
        }
        
        public void OnColorC_Clicked()
        {
            Debug.Log(LOG_PREFIX + "OnColorC_Clicked() called");
            colorC_Selected = !colorC_Selected;
            if (colorC_Selected)
            {
                // Désélectionner toutes les autres couleurs
                colorW_Selected = false;
                colorU_Selected = false;
                colorB_Selected = false;
                colorR_Selected = false;
                colorG_Selected = false;
            }
            EditSearchUrl();
        }
        
        // === FONCTIONS POUR LES BOUTONS DE RARETÉ ===
        
        public void OnRarityCommon_Clicked()
        {
            Debug.Log(LOG_PREFIX + "OnRarityCommon_Clicked() called");
            rarityCommon_Selected = !rarityCommon_Selected;
            EditSearchUrl();
        }
        
        public void OnRarityUncommon_Clicked()
        {
            Debug.Log(LOG_PREFIX + "OnRarityUncommon_Clicked() called");
            rarityUncommon_Selected = !rarityUncommon_Selected;
            EditSearchUrl();
        }
        
        public void OnRarityRare_Clicked()
        {
            Debug.Log(LOG_PREFIX + "OnRarityRare_Clicked() called");
            rarityRare_Selected = !rarityRare_Selected;
            EditSearchUrl();
        }
        
        public void OnRarityMythic_Clicked()
        {
            Debug.Log(LOG_PREFIX + "OnRarityMythic_Clicked() called");
            rarityMythic_Selected = !rarityMythic_Selected;
            EditSearchUrl();
        }
        
        // === FONCTION RESET ===
        
        public void OnResetFilters()
        {
            Debug.Log(LOG_PREFIX + "OnResetFilters() called");
            
            // Clear tous les champs de texte
            if (cardNameInput != null) cardNameInput.text = "";
            if (cardTextInput != null) cardTextInput.text = "";
            if (typeLineInput != null) typeLineInput.text = "";
            if (cmcInput != null) cmcInput.text = "";
            
            // Remettre les dropdowns à la première valeur
            if (setDropdown != null) setDropdown.value = 0;
            if (langDropdown != null) langDropdown.value = 0;
            
            // Désélectionner tous les boutons de couleur
            colorW_Selected = false;
            colorU_Selected = false;
            colorB_Selected = false;
            colorR_Selected = false;
            colorG_Selected = false;
            colorC_Selected = false;
            
            // Désélectionner tous les boutons de rareté
            rarityCommon_Selected = false;
            rarityUncommon_Selected = false;
            rarityRare_Selected = false;
            rarityMythic_Selected = false;
            
            // Mettre à jour l'URL
            EditSearchUrl();
            
            Debug.Log(LOG_PREFIX + "All filters reset");
        }
        
        // === FONCTION POUR VIDER LE CHAMP QUAND SÉLECTIONNÉ ===
        
        public void OnValidatedInputSelected()
        {
            Debug.Log(LOG_PREFIX + "OnValidatedInputSelected() called");
            
            // Créer une URL vide pour permettre au joueur de coller facilement
            // Note: On ne peut pas créer new VRCUrl("") à runtime, donc on utilise SetUrl avec une URL vide pré-créée
            // Alternative: Ne rien faire et laisser le joueur sélectionner tout + coller
            
            Debug.Log(LOG_PREFIX + "VRCUrlInputField selected - ready for paste");
        }
        
        private string BuildSearchUrl()
        {
            // Construire la requête de recherche à partir des champs
            StringBuilder queryBuilder = new StringBuilder();
            
            // Card Name
            if (cardNameInput != null && !string.IsNullOrEmpty(cardNameInput.text))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("name:\"");
                queryBuilder.Append(cardNameInput.text);
                queryBuilder.Append("\"");
            }
            
            // Card Text (oracle)
            if (cardTextInput != null && !string.IsNullOrEmpty(cardTextInput.text))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("o:\"");
                queryBuilder.Append(cardTextInput.text);
                queryBuilder.Append("\"");
            }
            
            // Type Line
            if (typeLineInput != null && !string.IsNullOrEmpty(typeLineInput.text))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("t:\"");
                queryBuilder.Append(typeLineInput.text);
                queryBuilder.Append("\"");
            }
            
            // CMC
            if (cmcInput != null && !string.IsNullOrEmpty(cmcInput.text))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("cmc:");
                queryBuilder.Append(cmcInput.text);
            }
            
            // Set (ignorer si première option)
            if (setDropdown != null && setDropdown.value > 0 && setDropdownOptions != null && setDropdown.value < setDropdownOptions.Length)
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("set:");
                
                // Extraire le code entre parenthèses
                string setOption = setDropdownOptions[setDropdown.value];
                int openParen = setOption.LastIndexOf('(');
                int closeParen = setOption.LastIndexOf(')');
                
                if (openParen != -1 && closeParen != -1 && closeParen > openParen)
                {
                    string setCode = setOption.Substring(openParen + 1, closeParen - openParen - 1);
                    queryBuilder.Append(setCode);
                }
                else
                {
                    queryBuilder.Append(setOption);
                }
            }
            
            // Lang - plus de vérification > 0 car index 0 contient maintenant "EN"
            if (langDropdown != null && langDropdownOptions != null && langDropdown.value >= 0 && langDropdown.value < langDropdownOptions.Length)
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append("l:");
                // Utiliser le tableau langDropdownOptions (pré-rempli dans Start)
                string selectedLang = langDropdownOptions[langDropdown.value].ToLower();
                queryBuilder.Append(selectedLang);
            }
            
            // Colors
            string colorQuery = BuildColorQuery();
            if (!string.IsNullOrEmpty(colorQuery))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append(colorQuery);
            }
            
            // Rarities
            string rarityQuery = BuildRarityQuery();
            if (!string.IsNullOrEmpty(rarityQuery))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append(" ");
                queryBuilder.Append(rarityQuery);
            }
            
            // Construire l'URL complète
            string finalQuery = queryBuilder.ToString();
            string fullUrl = manager.searchURL.ToString() + finalQuery;
            
            return fullUrl;
        }

        private void Update()
        {
            // Charger les dropdowns après le délai
            if (dropdownLoadScheduled && Time.time >= dropdownLoadTime)
            {
                LoadDropdownData();
            }
            
            // Charger progressivement les sets dans le dropdown
            if (isLoadingSets && Time.time >= nextSetLoadTime)
            {
                LoadNextBatchOfSets();
            }
            
            // Traiter progressivement l'atlas info
            if (isProcessingAtlasInfo && Time.time >= nextAtlasProcessTime)
            {
                ProcessNextAtlasBatch();
            }
            
            if ((isDeletingOldCards || clearAllRequested) && Time.time >= nextDeleteTime)
            {
                DeleteNextBatchOfCards();
            }
            else if (cardsToLoad != null && currentLoadIndex < cardsToLoad.Count && Time.time >= nextLoadTime)
            {
                LoadNextBatchOfCards();
            }
        }

        private bool IsSearchResponse(IVRCStringDownload json)
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

            return IsSearchUrl(json.Url);
        }
        
        private bool IsAtlasInfoResponse(IVRCStringDownload json)
        {
            if (json == null || json.Url == null) return false;
            string url = json.Url.ToString();
            return url.Contains("at0"); // at0 pour l'atlas info (sans slash)
        }
        
        private bool IsDropdownDataResponse(IVRCStringDownload json)
        {
            if (json == null || json.Url == null) return false;
            string url = json.Url.ToString();
            return url.Contains("at2"); // at2 pour les données dropdown (sets et blocs)
        }
        
        private bool IsSearchUrl(VRCUrl url)
        {
            return url.ToString().StartsWith(manager.searchURL.ToString());
        }
        
        private bool IsValidResponse(IVRCStringDownload json)
        {
            return IsSearchResponse(json) || IsAtlasInfoResponse(json) || IsDropdownDataResponse(json) || IsManagerAtlasUrl(json);
        }
        
        private bool IsManagerAtlasUrl(IVRCStringDownload json)
        {
            if (json == null || json.Url == null || manager.tempURLs == null) return false;
            
            for (int i = 0; i < manager.tempURLs.Length; i++)
            {
                if (json.Url == manager.tempURLs[i])
                {
                    return true;
                }
            }
            return false;
        }
        
        private bool IsStoredSearchResponse(IVRCStringDownload json)
        {
            if (json == null || json.Url == null || manager.tempURLs == null) return false;
            
            // Vérifier si c'est le slot tempURL[4095] (réservé pour les recherches)
            if (manager.tempURLs.Length > 4095 && json.Url == manager.tempURLs[4095])
            {
                return true;
            }
            return false;
        }
        
        private int ConvertAtlasLinkToIndex(string atlasLink)
        {
            // Les atlas links sont de la forme "/ata", "/atb", "/atc", etc.
            // On extrait la partie après "/at" et on convertit de base36 vers int
            if (string.IsNullOrEmpty(atlasLink) || !atlasLink.StartsWith("/at"))
            {
                Debug.LogError(LOG_PREFIX + $"Invalid atlas link format: {atlasLink}");
                return 0;
            }
            
            string base36Suffix = atlasLink.Substring(3); // Enlever "/at"
            if (string.IsNullOrEmpty(base36Suffix))
            {
                return 0; // "/at0" -> index 0
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
                    Debug.LogError(LOG_PREFIX + $"Invalid base36 character: {c}");
                    return 0;
                }
                result += digit * multiplier;
                multiplier *= 36;
            }
            
            return result;
        }
        public void OnInputValidate()
        {
            Debug.Log(LOG_PREFIX + "OnInputValidate() called");
            
            // Récupérer l'URL complète saisie par l'utilisateur
            VRCUrl userUrl = validatedInput.GetUrl();
            string urlString = userUrl.ToString();
            
            Debug.Log(LOG_PREFIX + $"User entered URL: {urlString}");
            
            // Vérifier que l'URL commence par https://mtg.hactazia.fr/as
            string requiredPrefix = "https://mtg.hactazia.fr/as";
            if (!urlString.StartsWith(requiredPrefix))
            {
                Debug.LogError(LOG_PREFIX + $"Invalid URL. Must start with: {requiredPrefix}");
                Debug.LogError(LOG_PREFIX + $"Current URL: {urlString}");
                return;
            }
            
            // Vérifier qu'il y a bien ?q= dans l'URL
            if (!urlString.Contains("?q="))
            {
                Debug.LogWarning(LOG_PREFIX + "URL must contain '?q=' with search parameters");
                return;
            }
            
            Debug.Log(LOG_PREFIX + $"Starting search, clearing existing cards first: {urlString}");
            
            // Supprimer toutes les cartes existantes
            ClearAllCards();
            
            // Lancer la recherche directement - les résultats seront stockés dans cardsToLoad
            // et chargés automatiquement après le clear dans DeleteNextBatchOfCards()
            VRCStringDownloader.LoadUrl(userUrl, (IUdonEventReceiver)this);
        }
        
        // Suppression de la synchronisation réseau : plus de OnDeserialization
        public override void OnStringLoadSuccess(IVRCStringDownload json)
        {
            // Validate url
            if (!IsValidResponse(json))
            {
                Debug.LogError(LOG_PREFIX + "Invalid response URL", this);
                Debug.LogError(LOG_PREFIX + $"URL received: {json.Url}", this);
                return;
            }
            
            // Si c'est une réponse de données dropdown (/at2)
            if (IsDropdownDataResponse(json))
            {
                ProcessDropdownData(json.Result);
                return;
            }
            
            // Si c'est une réponse d'atlas info
            if (IsAtlasInfoResponse(json))
            {
                ProcessAtlasInfo(json.Result);
                return;
            }
            
            // Si on est en train de clear des cartes, stocker la réponse pour la traiter après
            if (isDeletingOldCards || clearAllRequested)
            {
                Debug.Log(LOG_PREFIX + "Search response received during clear, storing for later processing");
                pendingSearchResponse = json.Result;
                hasPendingResponse = true;
                return;
            }
            
            // Traiter la réponse de recherche immédiatement si pas de clear en cours
            ProcessSearchResponse(json.Result);
        }
        
        private void ProcessSearchResponse(string jsonResult)
        {
            // Parse JSON de recherche
            if (VRCJson.TryDeserializeFromJson(jsonResult, out DataToken result))
            {
                // Validate JSON structure
                if (result.TokenType != TokenType.DataDictionary)
                {
                    Debug.LogError(LOG_PREFIX + $"Error parsing response: {jsonResult}");
                    return;
                }

                // get dictionary
                var dict = result.DataDictionary;

                if (!dict.ContainsKey("results") || dict["results"].TokenType != TokenType.DataList)
                {
                    Debug.LogError(LOG_PREFIX + "Invalid or missing results in response", this);
                    return;
                }

                // get results list
                var list = dict["results"].DataList;

                // Charger directement les cartes (le clear a déjà été fait avant l'appel)
                cardsToLoad = list;
                currentLoadIndex = 0;
                nextLoadTime = Time.time;
                LoadNextBatchOfCards();
            }
            else
            {
                Debug.LogError(LOG_PREFIX + $"Error parsing response: {jsonResult}");
            }
        }
        public override void OnStringLoadError(IVRCStringDownload result)
        {
            if (!IsValidResponse(result))
            {
                Debug.LogError(LOG_PREFIX + "Invalid response URL", this);
                return;
            }
            Debug.LogError(LOG_PREFIX + $"Error loading string: {result.ErrorCode} - {result.Error}");
        }
        private void LoadAtlasInfo()
        {
            // Utiliser l'URL préconfigurée dans le manager pour l'atlas 0
            if (manager.tempURLs != null && manager.tempURLs.Length > 0)
            {
                VRCStringDownloader.LoadUrl(manager.tempURLs[0], (IUdonEventReceiver)this);
            }
        }
        
        private void DeleteNextBatchOfCards()
        {
            if (!isDeletingOldCards && !clearAllRequested)
                return;

            // Si clearAllRequested, on supprime directement les enfants de CardParent
            if (clearAllRequested)
            {
                if (CardParent == null || CardParent.childCount == 0)
                {
                    // Plus d'enfants, réinitialiser tout
                    for (int i = 0; i < instantiatedCards.Length; i++)
                        instantiatedCards[i] = null;
                    instantiatedCardsCount = 0;
                    for (int i = 0; i < cardKeys.Length; i++)
                        cardKeys[i] = null;
                    cardKeysCount = 0;
                    isDeletingOldCards = false;
                    clearAllRequested = false;
                    deleteIndex = 0;
                    currentLoadIndex = 0;
                    cardsToLoad = null;

                    Debug.Log(LOG_PREFIX + "All cards cleared (progressive)");
                    
                    // Si une réponse de recherche est en attente, la traiter maintenant
                    if (hasPendingResponse && pendingSearchResponse != null)
                    {
                        Debug.Log(LOG_PREFIX + "Clear complete, processing pending search response");
                        ProcessSearchResponse(pendingSearchResponse);
                        hasPendingResponse = false;
                        pendingSearchResponse = null;
                    }
                    
                    return;
                }

                // Supprimer un lot de 6 enfants
                int childrenToDelete = Mathf.Min(6, CardParent.childCount);
                for (int i = 0; i < childrenToDelete; i++)
                {
                    if (CardParent.childCount > 0)
                    {
                        GameObject child = CardParent.GetChild(CardParent.childCount - 1).gameObject;
                        Destroy(child);
                    }
                }

                Debug.Log(LOG_PREFIX + $"Deleted {childrenToDelete} cards, {CardParent.childCount} remaining");
                nextDeleteTime = Time.time + DELETE_INTERVAL;
                return;
            }

            // Suppression normale pour une nouvelle recherche
            if (deleteIndex >= instantiatedCardsCount || instantiatedCardsCount == 0)
            {
                isDeletingOldCards = false;
                ClearArraysProgressively();
                deleteIndex = 0;
                currentLoadIndex = 0;
                nextLoadTime = Time.time + LOAD_INTERVAL;
                
                // Si une réponse de recherche est en attente, la traiter maintenant
                if (hasPendingResponse && pendingSearchResponse != null)
                {
                    Debug.Log(LOG_PREFIX + "Clear complete, processing pending search response");
                    ProcessSearchResponse(pendingSearchResponse);
                    hasPendingResponse = false;
                    pendingSearchResponse = null;
                }
                else if (cardsToLoad != null)
                {
                    LoadNextBatchOfCards();
                }
                return;
            }

            int cardsToDelete = Mathf.Min(CARDS_PER_DELETE_BATCH, instantiatedCardsCount - deleteIndex);

            for (int i = 0; i < cardsToDelete; i++)
            {
                int cardIndex = deleteIndex + i;
                if (cardIndex >= 0 && cardIndex < instantiatedCards.Length && instantiatedCards[cardIndex] != null)
                {
                    Destroy(instantiatedCards[cardIndex]);
                    instantiatedCards[cardIndex] = null;
                }
            }

            deleteIndex += cardsToDelete;
            nextDeleteTime = Time.time + DELETE_INTERVAL;
        }
        
        private void ClearArraysProgressively()
        {
            // Réinitialiser en plusieurs étapes pour éviter les boucles trop longues
            int clearBatchSize = 10;
            
            for (int i = 0; i < Mathf.Min(clearBatchSize, instantiatedCards.Length); i++)
            {
                instantiatedCards[i] = null;
            }
            instantiatedCardsCount = 0;
            
            for (int i = 0; i < Mathf.Min(clearBatchSize, cardKeys.Length); i++)
            {
                cardKeys[i] = null;
            }
            cardKeysCount = 0;
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
                Debug.LogError(LOG_PREFIX + "Invalid atlas info structure");
                return;
            }
            
            var data = dict["data"].DataDictionary;
            if (!data.ContainsKey("batches"))
            {
                Debug.LogError(LOG_PREFIX + "Missing batches in atlas info");
                return;
            }
            
            atlasBatches = data["batches"].DataList;
            
            Debug.Log(LOG_PREFIX + $"Starting progressive atlas processing for {instantiatedCardsCount} cards, {atlasBatches.Count} batches");
            
            // Démarrer le traitement progressif
            currentAtlasCardIndex = 0;
            isProcessingAtlasInfo = true;
            nextAtlasProcessTime = Time.time;
        }
        
        private void ProcessNextAtlasBatch()
        {
            if (atlasBatches == null || currentAtlasCardIndex >= instantiatedCardsCount)
            {
                // Traitement terminé
                isProcessingAtlasInfo = false;
                Debug.Log(LOG_PREFIX + "Atlas info processing complete");
                return;
            }
            
            int cardsInThisBatch = Mathf.Min(CARDS_PER_ATLAS_BATCH, instantiatedCardsCount - currentAtlasCardIndex);
            
            for (int i = 0; i < cardsInThisBatch; i++)
            {
                int cardIdx = currentAtlasCardIndex + i;
                string cardKey = cardKeys[cardIdx];
                
                if (string.IsNullOrEmpty(cardKey)) continue;
                
                // Chercher la carte dans tous les batches
                bool found = false;
                for (int k = 0; k < atlasBatches.Count && !found; k++)
                {
                    var batchToken = atlasBatches[k];
                    if (batchToken.TokenType != TokenType.DataDictionary) continue;
                    var batch = batchToken.DataDictionary;
                    
                    if (!batch.ContainsKey("atlas_link") || !batch.ContainsKey("cards")) continue;
                    
                    var cardsInBatch = batch["cards"].DataList;
                    for (int j = 0; j < cardsInBatch.Count; j++)
                    {
                        var cardInfo = cardsInBatch[j].DataDictionary;
                        if (cardInfo.ContainsKey("id") && cardInfo["id"].String == cardKey)
                        {
                            // Trouvé !
                            if (instantiatedCards[cardIdx] != null)
                            {
                                var searchCard = instantiatedCards[cardIdx].GetComponent<MTG_SearchCard>();
                                if (searchCard != null)
                                {
                                    searchCard.manager = manager;
                                    searchCard.SetImageFromId();
                                }
                            }
                            
                            found = true;
                            break;
                        }
                    }
                }
            }
            
            currentAtlasCardIndex += cardsInThisBatch;
            
            // Planifier le prochain batch
            if (currentAtlasCardIndex < instantiatedCardsCount)
            {
                nextAtlasProcessTime = Time.time + ATLAS_PROCESS_INTERVAL;
            }
            else
            {
                isProcessingAtlasInfo = false;
                Debug.Log(LOG_PREFIX + "Atlas info processing complete");
            }
        }
        
        private void ProcessDropdownData(string jsonResult)
        {
            Debug.Log(LOG_PREFIX + "ProcessDropdownData called with JSON: " + jsonResult.Substring(0, Mathf.Min(200, jsonResult.Length)));
            
            if (!VRCJson.TryDeserializeFromJson(jsonResult, out DataToken result) || result.TokenType != TokenType.DataDictionary)
            {
                Debug.LogError(LOG_PREFIX + "Error parsing dropdown data: " + jsonResult);
                return;
            }
            
            var dict = result.DataDictionary;
            Debug.Log(LOG_PREFIX + $"Root dictionary has {dict.Count} keys");
            
            // Nouvelle structure JSON : {"data": {"type": "sets_list", "count": 1004, "data": [...]}}
            if (!dict.ContainsKey("data") || dict["data"].TokenType != TokenType.DataDictionary)
            {
                Debug.LogError(LOG_PREFIX + "No 'data' dictionary found in dropdown response");
                return;
            }
            
            var data = dict["data"].DataDictionary;
            Debug.Log(LOG_PREFIX + $"Data dictionary has {data.Count} keys");
            
            // Vérifier le type
            if (!data.ContainsKey("type") || data["type"].TokenType != TokenType.String)
            {
                Debug.LogError(LOG_PREFIX + "No 'type' field found in data");
                return;
            }
            
            string dataType = data["type"].String;
            Debug.Log(LOG_PREFIX + $"Data type: {dataType}");
            
            // Parser les sets si c'est "sets_list"
            if (dataType == "sets_list" && data.ContainsKey("data") && data["data"].TokenType == TokenType.DataList)
            {
                var setsList = data["data"].DataList;
                int count = data.ContainsKey("count") && data["count"].TokenType == TokenType.Double ? (int)data["count"].Double : setsList.Count;
                Debug.Log(LOG_PREFIX + $"Found {count} sets ({setsList.Count} in array), starting progressive loading");
                
                if (setDropdown != null)
                {
                    // Compter d'abord combien d'options valides on a
                    int validSetCount = 1; // +1 pour "All Sets"
                    for (int i = 0; i < setsList.Count; i++)
                    {
                        if (setsList[i].TokenType == TokenType.String)
                            validSetCount++;
                    }
                    
                    // Créer un tableau de strings pour toutes les options
                    setDropdownOptions = new string[validSetCount];
                    
                    // Ajouter "All Sets" en premier
                    setDropdownOptions[0] = "All Sets";
                    
                    // Copier tous les sets valides dans le tableau
                    int currentIndex = 1;
                    for (int i = 0; i < setsList.Count; i++)
                    {
                        if (setsList[i].TokenType == TokenType.String)
                        {
                            setDropdownOptions[currentIndex] = setsList[i].String;
                            currentIndex++;
                        }
                    }
                    
                    // Initialiser le dropdown avec juste "All Sets"
                    setDropdown.ClearOptions();
                    string[] initialOptions = new string[1];
                    initialOptions[0] = "All Sets";
                    setDropdown.AddOptions(initialOptions);
                    
                    // Démarrer le chargement progressif
                    setsToLoad = setsList;
                    currentSetLoadIndex = 0;
                    isLoadingSets = true;
                    nextSetLoadTime = Time.time + SET_LOAD_INTERVAL;
                    
                    Debug.Log(LOG_PREFIX + $"Initialized dropdown with {validSetCount} total sets, will load progressively");
                }
            }
            
            dropdownDataLoaded = true;
            Debug.Log(LOG_PREFIX + "Dropdown data processing complete");
        }
        
        private void LoadNextBatchOfSets()
        {
            if (setsToLoad == null || currentSetLoadIndex >= setsToLoad.Count)
            {
                // Chargement terminé
                if (isLoadingSets)
                {
                    isLoadingSets = false;
                    setDropdown.RefreshShownValue();
                    Debug.Log(LOG_PREFIX + $"Set dropdown fully loaded with {setDropdownOptions.Length} options");
                }
                return;
            }
            
            // Calculer combien de sets charger dans ce batch
            int setsInThisBatch = Mathf.Min(SETS_PER_BATCH, setsToLoad.Count - currentSetLoadIndex);
            
            // Créer un tableau temporaire pour ce batch
            string[] batchOptions = new string[setsInThisBatch];
            int batchIndex = 0;
            
            for (int i = 0; i < setsInThisBatch && (currentSetLoadIndex + i) < setsToLoad.Count; i++)
            {
                int setIndex = currentSetLoadIndex + i;
                if (setsToLoad[setIndex].TokenType == TokenType.String)
                {
                    batchOptions[batchIndex] = setsToLoad[setIndex].String;
                    batchIndex++;
                }
            }
            
            // Ajouter ce batch d'options au dropdown
            if (batchIndex > 0)
            {
                // Créer un tableau de la bonne taille si nécessaire
                if (batchIndex < batchOptions.Length)
                {
                    string[] trimmedBatch = new string[batchIndex];
                    for (int i = 0; i < batchIndex; i++)
                    {
                        trimmedBatch[i] = batchOptions[i];
                    }
                    setDropdown.AddOptions(trimmedBatch);
                }
                else
                {
                    setDropdown.AddOptions(batchOptions);
                }
                
                Debug.Log(LOG_PREFIX + $"Added {batchIndex} sets to dropdown (progress: {currentSetLoadIndex + setsInThisBatch}/{setsToLoad.Count})");
            }
            
            currentSetLoadIndex += setsInThisBatch;
            
            // Planifier le prochain batch
            if (currentSetLoadIndex < setsToLoad.Count)
            {
                nextSetLoadTime = Time.time + SET_LOAD_INTERVAL;
            }
            else
            {
                isLoadingSets = false;
                setDropdown.RefreshShownValue();
                Debug.Log(LOG_PREFIX + $"Set dropdown fully loaded with {setDropdownOptions.Length} options");
            }
        }

        private void LoadNextBatchOfCards()
        {
            if (cardsToLoad == null || currentLoadIndex >= cardsToLoad.Count)
                return;

            // Ne pas charger si on est en train de supprimer
            if (isDeletingOldCards)
                return;

            // Charger moins de cartes par lot pour éviter les surcharges VM
            int cardsInThisBatch = Mathf.Min(CARDS_PER_LOAD_BATCH, cardsToLoad.Count - currentLoadIndex);

            for (int i = 0; i < cardsInThisBatch; i++)
            {
                int cardIndex = currentLoadIndex + i;

                // verify card is dictionary and has required fields
                if (cardsToLoad[cardIndex].TokenType != TokenType.DataDictionary) continue;
                var cardDict = cardsToLoad[cardIndex].DataDictionary;

                // instantiate card prefab and set data
                var card = Instantiate(CardPrefab);
                card.transform.SetParent(CardParent, false);
                var cardComp = card.GetComponent<MTG_SearchCard>();

                // Assigner le manager et l'interface AVANT SetData
                cardComp.manager = manager;
                cardComp.searchInterface = this;
                
                // Ensuite appeler SetData
                cardComp.SetData(cardDict);

                // Ajouter à la liste des cartes instanciées et clés
                if (instantiatedCardsCount < instantiatedCards.Length)
                {
                    instantiatedCards[instantiatedCardsCount] = card;
                    instantiatedCardsCount++;
                }
                if (cardKeysCount < cardKeys.Length)
                {
                    cardKeys[cardKeysCount] = cardComp.cardKey;
                    cardKeysCount++;
                }
            }

            currentLoadIndex += cardsInThisBatch;

            // Intervalle plus long pour éviter les surcharges
            if (currentLoadIndex < cardsToLoad.Count)
            {
                nextLoadTime = Time.time + LOAD_INTERVAL;
            }
            else
            {
                // Toutes les cartes chargées, récupérer les infos d'atlas
                LoadAtlasInfo();
            }
        }

        // Supprime progressivement toutes les cartes instanciées par lot de 6
        public void ClearAllCards()
        {
            if (CardParent == null)
            {
                Debug.LogWarning(LOG_PREFIX + "CardParent is not assigned");
                return;
            }
            
            if (CardParent.childCount == 0)
            {
                Debug.Log(LOG_PREFIX + "No cards to clear");
                // Réinitialiser les tableaux même s'il n'y a pas d'enfants
                for (int i = 0; i < instantiatedCards.Length; i++)
                    instantiatedCards[i] = null;
                instantiatedCardsCount = 0;
                for (int i = 0; i < cardKeys.Length; i++)
                    cardKeys[i] = null;
                cardKeysCount = 0;
                return;
            }
            
            clearAllRequested = true;
            isDeletingOldCards = true;
            deleteIndex = 0;
            nextDeleteTime = Time.time;
            Debug.Log(LOG_PREFIX + $"Progressive clearAllCards started - {CardParent.childCount} children to delete");
        }

        // Méthode appelée par une carte lorsqu'on appuie sur son bouton
        public void OnCardPreviewRequest(string cardId)
        {
            if (CardPreview == null)
            {
                Debug.LogWarning(LOG_PREFIX + "CardPreview n'est pas assigné !");
                return;
            }
            var previewCard = CardPreview.GetComponent<MTG_SearchCard>();
            if (previewCard == null)
            {
                Debug.LogWarning(LOG_PREFIX + "CardPreview n'a pas de composant MTG_SearchCard !");
                return;
            }
            previewCard.cardKey = cardId;
            previewCard.SetImageFromId();
            Debug.Log(LOG_PREFIX + $"CardPreview mis à jour avec l'id {cardId}");
        }
        
        // Méthode pour spawner une carte physique avec l'ID de la carte preview
        public void SpawnPhysicCardFromPreview()
        {
            Debug.Log(LOG_PREFIX + "SpawnPhysicCardFromPreview called");

            if (CardPreview == null)
            {
                Debug.LogError(LOG_PREFIX + "CardPreview n'est pas assigné !");
                return;
            }
            
            var previewCard = CardPreview.GetComponent<MTG_SearchCard>();
            if (previewCard == null)
            {
                Debug.LogError(LOG_PREFIX + "CardPreview n'a pas de composant MTG_SearchCard !");
                return;
            }
            
            string cardId = previewCard.cardKey;
            if (string.IsNullOrEmpty(cardId))
            {
                Debug.LogError(LOG_PREFIX + "CardPreview n'a pas de cardKey défini !");
                return;
            }
            
            if (physicCardPoolManager == null)
            {
                Debug.LogError(LOG_PREFIX + "physicCardPoolManager n'est pas assigné !");
                return;
            }
            
            // Déterminer la position de spawn
            Vector3 spawnPosition;
            Quaternion spawnRotation;
            
            if (PhysicCardSpawnPoint != null)
            {
                spawnPosition = PhysicCardSpawnPoint.position;
                spawnRotation = PhysicCardSpawnPoint.rotation;
            }
            else
            {
                // Par défaut, spawn devant le joueur local
                VRCPlayerApi localPlayer = Networking.LocalPlayer;
                if (localPlayer != null)
                {
                    Vector3 playerPos = localPlayer.GetPosition();
                    Vector3 playerForward = localPlayer.GetRotation() * Vector3.forward;
                    spawnPosition = playerPos + playerForward * 1.5f + Vector3.up * 1.0f;
                    spawnRotation = Quaternion.LookRotation(playerForward);
                }
                else
                {
                    spawnPosition = Vector3.zero;
                    spawnRotation = Quaternion.identity;
                }
            }
            
            // Spawn via le pool manager (création dynamique)
            GameObject spawnedCard = physicCardPoolManager.SpawnCard(cardId, spawnPosition, spawnRotation);
            
            if (spawnedCard == null)
            {
                Debug.LogError(LOG_PREFIX + "Failed to spawn card!");
                Debug.LogError(LOG_PREFIX + physicCardPoolManager.GetPoolStatus());
                return;
            }
            
            Debug.Log(LOG_PREFIX + $"Card spawned successfully with ID: {cardId}");
            Debug.Log(LOG_PREFIX + physicCardPoolManager.GetPoolStatus());
        }
    }
}
