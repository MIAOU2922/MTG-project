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
    public class MTG_Deckinterface : UdonSharpBehaviour
    {
        // Préfixe coloré pour les logs
        private const string LOG_PREFIX = "<color=#FF1493>[MTG_Deckinterface]</color> ";
        
        [Header("=== REFERENCES ===")]
        public MTG_Manager manager;
        public VRCUrlInputField input;
        public GameObject DeckCardPrefab;
        public Transform DeckCardParent;
        
        [Header("=== DECK MANAGEMENT UI ===")]
        public GameObject deckListPanel; // Panel pour afficher la liste des decks
        public GameObject deckListItemPrefab; // Prefab pour un item de liste de deck
        public Transform deckListParent; // Parent pour les items de liste
        public VRCUrlInputField deckNameInput; // Input pour le nom du deck à sauvegarder
        public VRCUrlInputField deckDescriptionInput; // Input pour la description du deck
        public TextMeshProUGUI urlDisplayText; // Text field pour afficher l'URL construite (comme dans MTG_Searchinterface)
        
        [Header("=== DECK ZONES ===")]
        public Transform mainDeckParent; // Parent pour les cartes du deck principal
        public Transform sideboardParent; // Parent pour le sideboard
        public Transform commanderParent; // Parent pour le(s) commander(s)
        public Transform companionParent; // Parent pour le companion
        
        // URLs de gestion de deck
        private VRCUrl deckListURL; // URL pour /at1 (liste des decks)
        // deckActionURL supprimé - on utilise directement manager.deckURL
        
        // Pas de CardPreview pour l'interface deck
        
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

        private void Start()
        {
            // Pré-remplir le champ avec l'URL de base du manager
            if (input != null && manager != null)
            {
                input.SetUrl(manager.searchURL);
            }
            
            // Initialiser les URLs de gestion de deck
            if (manager != null && manager.tempURLs != null && manager.tempURLs.Length > 1)
            {
                deckListURL = manager.tempURLs[1]; // /at1 pour la liste des decks
            }
        }

        private void Update()
        {
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
        
        private bool IsDeckListResponse(IVRCStringDownload json)
        {
            if (json == null || json.Url == null) return false;
            string url = json.Url.ToString();
            return url.Contains("at1"); // at1 pour la liste des decks
        }
        
        private bool IsDeckActionResponse(IVRCStringDownload json)
        {
            if (json == null || json.Url == null) return false;
            string url = json.Url.ToString();
            return url.Contains("ad?q="); // /ad?q= pour les actions de deck
        }
        
        private bool IsAtlasInfoResponse(IVRCStringDownload json)
        {
            if (json == null || json.Url == null) return false;
            string url = json.Url.ToString();
            return url.Contains("at0"); // at0 pour l'atlas info (sans slash)
        }
        
        private bool IsSearchUrl(VRCUrl url)
        {
            return url.ToString().StartsWith(manager.searchURL.ToString());
        }
        
        private bool IsValidResponse(IVRCStringDownload json)
        {
            return IsSearchResponse(json) || IsAtlasInfoResponse(json) || IsManagerAtlasUrl(json) 
                   || IsDeckListResponse(json) || IsDeckActionResponse(json);
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
                int digit = chars.IndexOf(value[i]);
                if (digit == -1)
                {
                    Debug.LogError(LOG_PREFIX + $"Invalid base36 character: {value[i]}");
                    return 0;
                }
                result += digit * multiplier;
                multiplier *= 36;
            }
            
            return result;
        }
        
        public void OnInputValidate()
        {
            // Récupérer l'URL complète saisie par l'utilisateur
            VRCUrl userUrl = input.GetUrl();
            string urlString = userUrl.ToString();
            
            // Vérifier que l'URL commence par la base requise (URL du manager)
            string requiredBase = manager.searchURL.ToString();
            if (!urlString.StartsWith(requiredBase))
            {
                Debug.LogError(LOG_PREFIX + $"Invalid URL. Must start with: {requiredBase}", this);
                Debug.Log(LOG_PREFIX + $"Current URL: {urlString}", this);
                return;
            }
            
            // Vérifier qu'il y a bien un terme de recherche après ?q=
            if (urlString.Length <= requiredBase.Length)
            {
                Debug.LogWarning(LOG_PREFIX + "No search term provided after ?q=", this);
                return;
            }
            
            Debug.Log(LOG_PREFIX + $"Sending deck search request to: {urlString}", this);
            
            // Recherche totalement locale : on ne fait que la requête locale
            VRCStringDownloader.LoadUrl(userUrl, (IUdonEventReceiver)this);
        }
        
        public override void OnStringLoadSuccess(IVRCStringDownload json)
        {
            // Validate url
            if (!IsValidResponse(json))
            {
                Debug.LogError(LOG_PREFIX + "Invalid response URL", this);
                Debug.LogError(LOG_PREFIX + $"URL received: {json.Url}", this);
                return;
            }
            
            // Si c'est une réponse de liste de decks
            if (IsDeckListResponse(json))
            {
                ProcessDeckList(json.Result);
                return;
            }
            
            // Si c'est une réponse d'action de deck (parse, save, load)
            if (IsDeckActionResponse(json))
            {
                ProcessDeckAction(json.Result);
                return;
            }
            
            // Si c'est une réponse d'atlas info
            if (IsAtlasInfoResponse(json))
            {
                ProcessAtlasInfo(json.Result);
                return;
            }
            
            // Parse JSON de recherche
            if (VRCJson.TryDeserializeFromJson(json.Result, out DataToken result))
            {
                // Validate JSON structure
                if (result.TokenType != TokenType.DataDictionary)
                {
                    Debug.LogError(LOG_PREFIX + $"Error parsing response: {json.Result}");
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

                // Annuler tout processus en cours
                isDeletingOldCards = false;
                deleteIndex = 0;
                currentLoadIndex = 0;
                
                // Démarrer la suppression progressive des anciennes cartes
                if (instantiatedCardsCount > 0)
                {
                    isDeletingOldCards = true;
                    deleteIndex = 0;
                    nextDeleteTime = Time.time;
                    cardsToLoad = list; // Stocker les nouvelles cartes à charger
                    currentLoadIndex = 0;
                }
                else
                {
                    // Pas d'anciennes cartes, charger directement les nouvelles
                    cardsToLoad = list;
                    currentLoadIndex = 0;
                    nextLoadTime = Time.time;
                    LoadNextBatchOfCards();
                }
            }
            else
            {
                Debug.LogError(LOG_PREFIX + $"Error parsing response: {json.Result}");
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

            if (deleteIndex >= instantiatedCardsCount || instantiatedCardsCount == 0)
            {
                if (clearAllRequested)
                {
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

                    // Détruire tous les enfants restants dans DeckCardParent
                    if (DeckCardParent != null)
                    {
                        for (int i = DeckCardParent.childCount - 1; i >= 0; i--)
                        {
                            GameObject child = DeckCardParent.GetChild(i).gameObject;
                            Destroy(child);
                        }
                    }

                    Debug.Log(LOG_PREFIX + "All deck cards cleared (progressive)");
                    return;
                }
                // Suppression progressive normale (recherche)
                isDeletingOldCards = false;
                ClearArraysProgressively();
                deleteIndex = 0;
                currentLoadIndex = 0;
                nextLoadTime = Time.time + LOAD_INTERVAL;
                if (cardsToLoad != null)
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
            
            var batches = data["batches"].DataList;
            
            Debug.Log(LOG_PREFIX + $"Processing atlas info for {instantiatedCardsCount} deck cards, {batches.Count} batches");
            
            // Pour chaque carte instanciée, trouver sa correspondance dans les batches
            for (int i = 0; i < instantiatedCardsCount; i++) 
            {
                string cardKey = (i < cardKeys.Length) ? cardKeys[i] : "";
                if (string.IsNullOrEmpty(cardKey)) continue;
                
                bool found = false;
                for (int batchIndex = 0; batchIndex < batches.Count && !found; batchIndex++)
                {
                    if (batches[batchIndex].TokenType != TokenType.DataDictionary) continue;
                    
                    var batch = batches[batchIndex].DataDictionary;
                    if (!batch.ContainsKey("id") || !batch.ContainsKey("cards")) continue;
                    
                    string atlasLink = batch["id"].String;
                    var cards = batch["cards"].DataList;
                    
                    for (int cardIndexInBatch = 0; cardIndexInBatch < cards.Count; cardIndexInBatch++)
                    {
                        if (cards[cardIndexInBatch].TokenType != TokenType.String) continue;
                        
                        string batchCardKey = cards[cardIndexInBatch].String;
                        if (batchCardKey == cardKey)
                        {
                            int atlasIndex = ConvertAtlasLinkToIndex(atlasLink);
                            Debug.Log(LOG_PREFIX + $"Found deck card {cardKey} in atlas {atlasLink} (index {atlasIndex})");
                            
                            // Assigner le manager puis demander à la carte de récupérer son image via son id
                            if (instantiatedCards[i] != null)
                            {
                                var deckCard = instantiatedCards[i].GetComponent<MTG_DeckCard>();
                                if (deckCard != null)
                                {
                                    deckCard.manager = manager;
                                    deckCard.SetImageFromId();
                                    Debug.Log(LOG_PREFIX + $"Called SetImageFromId on deck card {cardKey}");
                                }
                                else
                                {
                                    Debug.LogWarning(LOG_PREFIX + $"Deck card {i} has no MTG_DeckCard component!");
                                }
                            }
                            else
                            {
                                Debug.LogWarning(LOG_PREFIX + $"Deck card {i} is null!");
                            }
                            
                            found = true;
                            break;
                        }
                    }
                }
                
                if (!found)
                {
                    Debug.LogWarning(LOG_PREFIX + $"Could not find deck card {cardKey} in atlas info");
                }
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
                var card = Instantiate(DeckCardPrefab);
                card.transform.SetParent(DeckCardParent, false);
                var cardComp = card.GetComponent<MTG_DeckCard>();

                // Assigner le manager à la carte
                cardComp.manager = manager;
                cardComp.SetData(cardDict);

                // Assigner la référence à l'interface pour le callback bouton
                cardComp.deckInterface = this;

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

        // Supprime immédiatement toutes les cartes instanciées et réinitialise les tableaux
        public void ClearAllCards()
        {
            if (instantiatedCardsCount == 0)
            {
                Debug.Log(LOG_PREFIX + "No deck cards to clear");
                return;
            }
            clearAllRequested = true;
            isDeletingOldCards = true;
            deleteIndex = 0;
            nextDeleteTime = Time.time;
            Debug.Log(LOG_PREFIX + "Progressive clearAllCards started for deck");
        }

        // Méthode appelée par une carte lorsqu'on appuie sur son bouton (pas de preview pour deck)
        public void OnCardButtonPressed(string cardId)
        {
            Debug.Log(LOG_PREFIX + $"Deck card button pressed for {cardId}");
            // Pas de preview, juste log pour debug
        }
        
        // Callback quand la quantité d'une carte change
        public void OnCardCountChanged(string cardId, int newCount)
        {
            Debug.Log(LOG_PREFIX + $"Card {cardId} count changed to {newCount}");
            // Ici on pourrait mettre à jour des stats (nombre total de cartes, etc.)
        }
        
        // Callback quand une carte est retirée du deck
        public void OnCardRemoved(string cardId)
        {
            Debug.Log(LOG_PREFIX + $"Card {cardId} removed from deck");
            
            // Retirer des tableaux de tracking
            for (int i = 0; i < instantiatedCardsCount; i++)
            {
                if (instantiatedCards[i] != null)
                {
                    MTG_DeckCard card = instantiatedCards[i].GetComponent<MTG_DeckCard>();
                    if (card != null && card.cardKey == cardId)
                    {
                        instantiatedCards[i] = null;
                        break;
                    }
                }
            }
            
            for (int i = 0; i < cardKeysCount; i++)
            {
                if (cardKeys[i] == cardId)
                {
                    cardKeys[i] = null;
                    break;
                }
            }
        }
        
        // === NOUVELLES METHODES DE GESTION DE DECK ===
        
        // Charger la liste des decks de l'utilisateur via /at1
        public void LoadDeckList()
        {
            if (deckListURL == null)
            {
                Debug.LogError(LOG_PREFIX + "Deck list URL not initialized", this);
                return;
            }
            
            Debug.Log(LOG_PREFIX + "Loading deck list from /at1", this);
            VRCStringDownloader.LoadUrl(deckListURL, (IUdonEventReceiver)this);
        }
        
        // Traiter la réponse de liste de decks
        private void ProcessDeckList(string jsonResult)
        {
            if (!VRCJson.TryDeserializeFromJson(jsonResult, out DataToken result) || result.TokenType != TokenType.DataDictionary)
            {
                Debug.LogError(LOG_PREFIX + "Error parsing deck list: " + jsonResult);
                return;
            }
            
            var dict = result.DataDictionary;
            if (!dict.ContainsKey("data") || dict["data"].TokenType != TokenType.DataDictionary)
            {
                Debug.LogError(LOG_PREFIX + "Invalid deck list structure");
                return;
            }
            
            var data = dict["data"].DataDictionary;
            if (!data.ContainsKey("decks") || data["decks"].TokenType != TokenType.DataList)
            {
                Debug.LogError(LOG_PREFIX + "Missing decks in response");
                return;
            }
            
            var decks = data["decks"].DataList;
            Debug.Log(LOG_PREFIX + $"Received {decks.Count} decks");
            
            // Nettoyer la liste existante
            if (deckListParent != null)
            {
                for (int i = deckListParent.childCount - 1; i >= 0; i--)
                {
                    Destroy(deckListParent.GetChild(i).gameObject);
                }
            }
            
            // Créer un item pour chaque deck
            for (int i = 0; i < decks.Count; i++)
            {
                if (decks[i].TokenType != TokenType.DataDictionary) continue;
                var deckInfo = decks[i].DataDictionary;
                
                if (!deckInfo.ContainsKey("id") || !deckInfo.ContainsKey("name")) continue;
                
                string deckId = deckInfo["id"].String;
                string deckName = deckInfo["name"].String;
                
                Debug.Log(LOG_PREFIX + $"Deck found: {deckName} ({deckId})");
            }
            
            // Afficher le panel de liste
            if (deckListPanel != null)
            {
                deckListPanel.SetActive(true);
            }
        }
        
        // Charger un deck via /ad?q=load:deck_id
        public void LoadDeck(string deckId)
        {
            if (string.IsNullOrEmpty(deckId))
            {
                Debug.LogError(LOG_PREFIX + "Cannot load deck: empty deck ID", this);
                return;
            }
            
            if (manager == null || manager.deckURL == null)
            {
                Debug.LogError(LOG_PREFIX + "Manager or deckURL not initialized", this);
                return;
            }
            
            // Construire l'URL: /ad?q=load:deck_id
            string loadUrl = manager.deckURL.ToString() + "load:" + deckId;
            Debug.Log(LOG_PREFIX + $"Deck URL: {loadUrl}", this);
            
            // Afficher l'URL dans le champ de texte (approche MTG_Searchinterface)
            if (urlDisplayText != null)
            {
                urlDisplayText.text = loadUrl;
                Debug.Log(LOG_PREFIX + "URL displayed in urlDisplayText - user must copy/paste to input field");
            }
            else
            {
                Debug.LogWarning(LOG_PREFIX + "urlDisplayText not set - cannot display URL");
            }
        }
        
        // Charger un deck temporaire (inline) via /ad?q=load:format:deck_list:lang
        public void LoadDeckTemporary(string deckList, string format = "auto", string lang = "en")
        {
            if (string.IsNullOrEmpty(deckList))
            {
                Debug.LogError(LOG_PREFIX + "Cannot load temporary deck: empty deck list", this);
                return;
            }
            
            if (manager == null || manager.deckURL == null)
            {
                Debug.LogError(LOG_PREFIX + "Manager or deckURL not initialized", this);
                return;
            }
            
            // Construire l'URL: /ad?q=load:format:deck_list:lang
            string loadUrl = manager.deckURL.ToString() + "load:" + format + ":" + deckList + ":" + lang;
            
            Debug.Log(LOG_PREFIX + $"Temporary deck URL: {loadUrl}", this);
            
            // Afficher l'URL dans le champ de texte
            if (urlDisplayText != null)
            {
                urlDisplayText.text = loadUrl;
                Debug.Log(LOG_PREFIX + "URL displayed - user must copy/paste to input field");
            }
        }
        
        // Supprimer un deck via /ad?q=delete:deck_id
        public void DeleteDeck(string deckId)
        {
            if (string.IsNullOrEmpty(deckId))
            {
                Debug.LogError(LOG_PREFIX + "Cannot delete deck: empty deck ID", this);
                return;
            }
            
            if (manager == null || manager.deckURL == null)
            {
                Debug.LogError(LOG_PREFIX + "Manager or deckURL not initialized", this);
                return;
            }
            
            // Construire l'URL: /ad?q=delete:deck_id
            string deleteUrl = manager.deckURL.ToString() + "delete:" + deckId;
            Debug.Log(LOG_PREFIX + $"Delete URL: {deleteUrl}", this);
            
            // Afficher l'URL dans le champ de texte
            if (urlDisplayText != null)
            {
                urlDisplayText.text = deleteUrl;
                Debug.Log(LOG_PREFIX + "URL displayed - user must copy/paste to input field");
            }
        }
        
        // Lister les decks via /ad?q=list ou /ad?q=list:search_name
        public void ListDecks(string searchName = "")
        {
            if (manager == null || manager.deckURL == null)
            {
                Debug.LogError(LOG_PREFIX + "Manager or deckURL not initialized", this);
                return;
            }
            
            string listUrl;
            if (string.IsNullOrEmpty(searchName))
            {
                // Lister mes decks
                listUrl = manager.deckURL.ToString() + "list";
                Debug.Log(LOG_PREFIX + $"List URL: {listUrl}", this);
            }
            else
            {
                // Rechercher des decks publics
                listUrl = manager.deckURL.ToString() + "list:" + searchName;
                Debug.Log(LOG_PREFIX + $"Search URL: {listUrl}", this);
            }
            
            // Afficher l'URL dans le champ de texte
            if (urlDisplayText != null)
            {
                urlDisplayText.text = listUrl;
                Debug.Log(LOG_PREFIX + "URL displayed - user must copy/paste to input field");
            }
        }
        
        // Sauvegarder le deck actuel via /ad?q=save:deck_name:format:deck_list:lang:description
        public void SaveDeck()
        {
            if (manager == null || manager.deckURL == null)
            {
                Debug.LogError(LOG_PREFIX + "Manager or deckURL not initialized", this);
                return;
            }
            
            // Récupérer le nom et la description depuis les inputs
            string deckName = "";
            string deckDescription = "";
            
            if (deckNameInput != null)
            {
                deckName = deckNameInput.GetUrl().ToString();
            }
            
            if (deckDescriptionInput != null)
            {
                deckDescription = deckDescriptionInput.GetUrl().ToString();
            }
            
            if (string.IsNullOrEmpty(deckName))
            {
                Debug.LogWarning(LOG_PREFIX + "Deck name is empty", this);
                return;
            }
            
            // Construire la decklist à partir des cartes actuellement affichées
            string deckList = BuildDeckListFromCards();
            
            if (string.IsNullOrEmpty(deckList))
            {
                Debug.LogWarning(LOG_PREFIX + "No cards to save", this);
                return;
            }
            
            // Construire l'URL: /ad?q=save:deck_name:auto:deck_list:en:description
            string saveUrl = manager.deckURL.ToString() + "save:" + deckName + ":auto:" + deckList + ":en:" + deckDescription;
            
            Debug.Log(LOG_PREFIX + $"Save URL: {saveUrl}", this);
            
            // Afficher l'URL dans le champ de texte
            if (urlDisplayText != null)
            {
                urlDisplayText.text = saveUrl;
                Debug.Log(LOG_PREFIX + "URL displayed - user must copy/paste to input field");
            }
        }
        
        // Construire une decklist à partir des cartes instanciées
        private string BuildDeckListFromCards()
        {
            StringBuilder sb = new StringBuilder();
            
            // Main deck
            if (mainDeckParent != null && mainDeckParent.childCount > 0)
            {
                sb.Append("//Main\\n");
                AppendCardsFromParent(sb, mainDeckParent);
            }
            else if (DeckCardParent != null && DeckCardParent.childCount > 0)
            {
                // Fallback sur DeckCardParent si mainDeckParent n'est pas défini
                AppendCardsFromParent(sb, DeckCardParent);
            }
            
            // Sideboard
            if (sideboardParent != null && sideboardParent.childCount > 0)
            {
                sb.Append("\\n//Sideboard\\n");
                AppendCardsFromParent(sb, sideboardParent);
            }
            
            // Commander
            if (commanderParent != null && commanderParent.childCount > 0)
            {
                sb.Append("\\n//Commander\\n");
                AppendCardsFromParent(sb, commanderParent);
            }
            
            // Companion
            if (companionParent != null && companionParent.childCount > 0)
            {
                sb.Append("\\n//Companion\\n");
                AppendCardsFromParent(sb, companionParent);
            }
            
            return sb.ToString();
        }
        
        // Ajouter les cartes d'un parent au StringBuilder
        private void AppendCardsFromParent(StringBuilder sb, Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                GameObject cardObj = parent.GetChild(i).gameObject;
                if (cardObj == null) continue;
                
                MTG_DeckCard card = cardObj.GetComponent<MTG_DeckCard>();
                if (card == null) continue;
                
                int count = card.GetCount();
                string cardKey = card.cardKey;
                
                if (count > 0 && !string.IsNullOrEmpty(cardKey))
                {
                    sb.Append(count).Append(" ").Append(cardKey).Append("\\n");
                }
            }
        }
        
        // Parser un deck via /ad?q=parse:deck_list:format:lang
        public void ParseDeck(string deckList, string format = "auto", string lang = "en")
        {
            if (manager == null || manager.deckURL == null)
            {
                Debug.LogError(LOG_PREFIX + "Manager or deckURL not initialized", this);
                return;
            }
            
            if (string.IsNullOrEmpty(deckList))
            {
                Debug.LogWarning(LOG_PREFIX + "Deck list is empty", this);
                return;
            }
            
            // Construire l'URL: /ad?q=parse:deck_list:format:lang
            string parseUrl = manager.deckURL.ToString() + "parse:" + deckList + ":" + format + ":" + lang;
            
            Debug.Log(LOG_PREFIX + $"Parse URL: {parseUrl}", this);
            
            // Afficher l'URL dans le champ de texte
            if (urlDisplayText != null)
            {
                urlDisplayText.text = parseUrl;
                Debug.Log(LOG_PREFIX + "URL displayed - user must copy/paste to input field");
            }
        }
        
        // Traiter la réponse d'une action de deck (parse, save, load, delete, list)
        private void ProcessDeckAction(string jsonResult)
        {
            if (!VRCJson.TryDeserializeFromJson(jsonResult, out DataToken result) || result.TokenType != TokenType.DataDictionary)
            {
                Debug.LogError(LOG_PREFIX + "Error parsing deck action response: " + jsonResult);
                return;
            }
            
            var dict = result.DataDictionary;
            
            // Vérifier s'il y a une erreur
            if (dict.ContainsKey("error"))
            {
                Debug.LogError(LOG_PREFIX + "Deck action error: " + dict["error"].String);
                return;
            }
            
            // Vérifier si c'est une réponse de load (cards_by_zone)
            if (dict.ContainsKey("cards_by_zone") && dict["cards_by_zone"].TokenType == TokenType.DataDictionary)
            {
                // Déterminer le type de deck (saved ou temporary)
                string deckType = "unknown";
                if (dict.ContainsKey("deck_type"))
                {
                    deckType = dict["deck_type"].String;
                }
                
                Debug.Log(LOG_PREFIX + $"Loading deck of type: {deckType}");
                ProcessLoadedDeck(dict["cards_by_zone"].DataDictionary);
                return;
            }
            
            // Vérifier si c'est une réponse de parse (parsed_cards)
            if (dict.ContainsKey("parsed_cards") && dict["parsed_cards"].TokenType == TokenType.DataDictionary)
            {
                ProcessParsedDeck(dict["parsed_cards"].DataDictionary);
                return;
            }
            
            // Vérifier si c'est une réponse de save (deck_id)
            if (dict.ContainsKey("deck_id"))
            {
                string deckId = dict["deck_id"].String;
                Debug.Log(LOG_PREFIX + $"Deck saved successfully with ID: {deckId}");
                
                // Optionnel: afficher un message de succès
                return;
            }
            
            // Vérifier si c'est une réponse de delete (message)
            if (dict.ContainsKey("action") && dict["action"].String == "delete")
            {
                string message = dict.ContainsKey("message") ? dict["message"].String : "Deck deleted successfully";
                Debug.Log(LOG_PREFIX + message);
                
                // Optionnel: rafraîchir la liste des decks
                return;
            }
            
            // Vérifier si c'est une réponse de list (results)
            if (dict.ContainsKey("action") && dict["action"].String == "list")
            {
                ProcessDeckListResults(dict);
                return;
            }
            
            Debug.LogWarning(LOG_PREFIX + "Unknown deck action response format");
        }
        
        // Traiter les résultats de list (similaire à ProcessDeckList mais peut inclure des decks publics)
        private void ProcessDeckListResults(DataDictionary dict)
        {
            if (!dict.ContainsKey("decks") || dict["decks"].TokenType != TokenType.DataList)
            {
                Debug.LogError(LOG_PREFIX + "Missing decks in list response");
                return;
            }
            
            var decks = dict["decks"].DataList;
            Debug.Log(LOG_PREFIX + $"Received {decks.Count} decks from list action");
            
            // Nettoyer la liste existante
            if (deckListParent != null)
            {
                for (int i = deckListParent.childCount - 1; i >= 0; i--)
                {
                    Destroy(deckListParent.GetChild(i).gameObject);
                }
            }
            
            // Créer un item pour chaque deck
            for (int i = 0; i < decks.Count; i++)
            {
                if (decks[i].TokenType != TokenType.DataDictionary) continue;
                var deckInfo = decks[i].DataDictionary;
                
                if (!deckInfo.ContainsKey("id") || !deckInfo.ContainsKey("name")) continue;
                
                string deckId = deckInfo["id"].String;
                string deckName = deckInfo["name"].String;
                
                Debug.Log(LOG_PREFIX + $"Deck found: {deckName} ({deckId})");
            }
            
            // Afficher le panel de liste
            if (deckListPanel != null)
            {
                deckListPanel.SetActive(true);
            }
        }
        
        // Traiter un deck chargé (load)
        private void ProcessLoadedDeck(DataDictionary cardsByZone)
        {
            Debug.Log(LOG_PREFIX + "Processing loaded deck");
            
            // Charger toutes les zones disponibles
            LoadCardsFromZone(cardsByZone, "main", mainDeckParent != null ? mainDeckParent : DeckCardParent);
            LoadCardsFromZone(cardsByZone, "sideboard", sideboardParent);
            LoadCardsFromZone(cardsByZone, "commander", commanderParent);
            LoadCardsFromZone(cardsByZone, "companion", companionParent);
            // On peut aussi gérer: oathbreaker, wishboard si nécessaire
        }
        
        // Charger les cartes d'une zone spécifique
        private void LoadCardsFromZone(DataDictionary cardsByZone, string zoneName, Transform parent)
        {
            if (parent == null)
            {
                Debug.LogWarning(LOG_PREFIX + $"Parent for zone '{zoneName}' is not assigned, skipping");
                return;
            }
            
            if (!cardsByZone.ContainsKey(zoneName) || cardsByZone[zoneName].TokenType != TokenType.DataList)
            {
                Debug.Log(LOG_PREFIX + $"No cards in zone '{zoneName}'");
                return;
            }
            
            var zoneCards = cardsByZone[zoneName].DataList;
            Debug.Log(LOG_PREFIX + $"Loading {zoneCards.Count} cards in zone '{zoneName}'");
            
            // Nettoyer les cartes existantes dans cette zone
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
            
            // Charger les nouvelles cartes
            for (int i = 0; i < zoneCards.Count; i++)
            {
                if (zoneCards[i].TokenType != TokenType.DataDictionary) continue;
                var cardInfo = zoneCards[i].DataDictionary;
                
                if (!cardInfo.ContainsKey("card_id")) continue;
                
                string cardId = cardInfo["card_id"].String;
                int count = 1;
                bool isCommander = false;
                
                if (cardInfo.ContainsKey("count") && cardInfo["count"].TokenType == TokenType.Double)
                {
                    count = (int)cardInfo["count"].Double;
                }
                
                if (cardInfo.ContainsKey("is_commander") && cardInfo["is_commander"].TokenType == TokenType.Boolean)
                {
                    isCommander = cardInfo["is_commander"].Boolean;
                }
                
                // Créer la carte
                GameObject cardObj = Instantiate(DeckCardPrefab, parent);
                MTG_DeckCard cardComp = cardObj.GetComponent<MTG_DeckCard>();
                
                if (cardComp != null)
                {
                    cardComp.manager = manager;
                    cardComp.deckInterface = this;
                    
                    // Créer un dictionnaire temporaire pour SetData
                    DataDictionary tempDict = new DataDictionary();
                    tempDict.Add("id", cardId);
                    tempDict.Add("count", (double)count);
                    
                    cardComp.SetData(tempDict);
                    
                    // Marquer si c'est un commander (optionnel: affichage spécial)
                    if (isCommander)
                    {
                        Debug.Log(LOG_PREFIX + $"Card {cardId} is marked as commander");
                    }
                }
                
                // Ajouter aux tableaux de tracking (seulement pour main deck)
                if (zoneName == "main")
                {
                    if (instantiatedCardsCount < instantiatedCards.Length)
                    {
                        instantiatedCards[instantiatedCardsCount] = cardObj;
                        instantiatedCardsCount++;
                    }
                    if (cardKeysCount < cardKeys.Length)
                    {
                        cardKeys[cardKeysCount] = cardId;
                        cardKeysCount++;
                    }
                }
            }
            
            // Charger les infos d'atlas après avoir créé toutes les cartes
            if (zoneName == "main")
            {
                LoadAtlasInfo();
            }
        }
        
        // Traiter un deck parsé (parse)
        private void ProcessParsedDeck(DataDictionary parsedCards)
        {
            Debug.Log(LOG_PREFIX + "Processing parsed deck");
            
            // Afficher les résultats du parsing (validation)
            // TODO: afficher dans l'UI les cartes valides, invalides, etc.
            
            if (parsedCards.ContainsKey("valid_cards"))
            {
                var validCards = parsedCards["valid_cards"].DataList;
                Debug.Log(LOG_PREFIX + $"Valid cards: {validCards.Count}");
            }
            
            if (parsedCards.ContainsKey("invalid_cards"))
            {
                var invalidCards = parsedCards["invalid_cards"].DataList;
                Debug.Log(LOG_PREFIX + $"Invalid cards: {invalidCards.Count}");
            }
            
            if (parsedCards.ContainsKey("total_cards"))
            {
                int totalCards = (int)parsedCards["total_cards"].Double;
                Debug.Log(LOG_PREFIX + $"Total cards: {totalCards}");
            }
        }
    }
}
