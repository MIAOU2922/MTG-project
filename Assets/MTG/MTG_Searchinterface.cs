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
        
        public MTG_Manager manager;
        public VRCUrlInputField input;
        public GameObject CardPrefab;
        public Transform CardParent;
        public GameObject CardPreview;
        
    // Synchronisation réseau supprimée : tout est local
        
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
            input.SetUrl(manager.searchURL);
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
            return IsSearchResponse(json) || IsAtlasInfoResponse(json) || IsManagerAtlasUrl(json);
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
            
            Debug.Log(LOG_PREFIX + $"Sending validated search request to: {urlString}", this);
            // Recherche totalement locale : on ne fait que la requête locale
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

                    // Détruire tous les enfants restants dans CardParent
                    if (CardParent != null)
                    {
                        for (int i = CardParent.childCount - 1; i >= 0; i--)
                        {
                            GameObject child = CardParent.GetChild(i).gameObject;
                            Destroy(child);
                        }
                    }

                    Debug.Log(LOG_PREFIX + "All cards cleared (progressive)");
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
            
            Debug.Log(LOG_PREFIX + $"Processing atlas info for {instantiatedCardsCount} cards, {batches.Count} batches");
            
            // Pour chaque carte instanciée, trouver sa correspondance dans les batches
            for (int i = 0; i < instantiatedCardsCount; i++) 
            {
                string cardKey = cardKeys[i];
                if (string.IsNullOrEmpty(cardKey)) continue;
                
                Debug.Log(LOG_PREFIX + $"Looking for card {i}: {cardKey}");
                
                // Chercher la carte dans tous les batches
                bool found = false;
                for (int k = 0; k < batches.Count; k++)
                {
                    var batchToken = batches[k];
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
                            string atlasLink = batch["atlas_link"].String;
                            float x = (float)cardInfo["rect_x"].Double;
                            float y = (float)cardInfo["rect_y"].Double;
                            float width = (float)cardInfo["rect_width"].Double;
                            float height = (float)cardInfo["rect_height"].Double;
                            
                            Rect uvRect = new Rect(x, y, width, height);
                            
                            // Convertir l'atlas link en index pour tempURLs
                            int atlasIndex = ConvertAtlasLinkToIndex(atlasLink);
                            
                            Debug.Log(LOG_PREFIX + $"Found card {cardKey} in atlas {atlasLink} (index {atlasIndex})");
                            
                            // Assigner le manager puis demander à la carte de récupérer son image via son id
                            if (instantiatedCards[i] != null)
                            {
                                var searchCard = instantiatedCards[i].GetComponent<MTG_SearchCard>();
                                if (searchCard != null)
                                {
                                    searchCard.manager = manager;
                                    searchCard.SetImageFromId();
                                    Debug.Log(LOG_PREFIX + $"Called SetImageFromId on card {cardKey}");
                                }
                                else
                                {
                                    Debug.LogWarning(LOG_PREFIX + $"Card {i} has no MTG_SearchCard component!");
                                }
                            }
                            else
                            {
                                Debug.LogWarning(LOG_PREFIX + $"Card {i} is null!");
                            }
                            
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }
                
                if (!found)
                {
                    Debug.LogWarning(LOG_PREFIX + $"Card with key {cardKey} not found in atlas info");
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
                var card = Instantiate(CardPrefab);
                card.transform.SetParent(CardParent, false);
                var cardComp = card.GetComponent<MTG_SearchCard>();

                // Assigner le manager à la carte
                cardComp.manager = manager;
                cardComp.SetData(cardDict);

                // Assigner la référence à l'interface pour le callback bouton
                cardComp.searchInterface = this;

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
                Debug.Log(LOG_PREFIX + "No cards to clear");
                return;
            }
            clearAllRequested = true;
            isDeletingOldCards = true;
            deleteIndex = 0;
            nextDeleteTime = Time.time;
            Debug.Log(LOG_PREFIX + "Progressive clearAllCards started");
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
    }
}
