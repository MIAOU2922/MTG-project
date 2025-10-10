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
        public MTG_Manager manager;
        public VRCUrlInputField input;
        public GameObject CardPrefab;
        public Transform CardParent;
        public int previousSearch;
        public int nextSearch;
        
        private int currentLoadIndex = 0;
        private DataList cardsToLoad;
        private float nextLoadTime = 0f;
        private GameObject[] instantiatedCards = new GameObject[128];
        private int instantiatedCardsCount = 0;
        private string[] cardKeys = new string[128];
        private int cardKeysCount = 0;

        private void Start()
        {
            // Pré-remplir le champ avec l'URL de base du manager
            input.SetUrl(manager.searchURL);
        }

        private void Update()
        {
            if (cardsToLoad != null && currentLoadIndex < cardsToLoad.Count && Time.time >= nextLoadTime)
            {
                LoadNextBatchOfCards();
            }
        }

        private bool IsSearchResponse(IVRCStringDownload json)
        {
            if (json == null)
            {
                Debug.LogError("Null JSON in response");
                return false;
            }

            if (json.Url == null)
            {
                Debug.LogError("Null URL in response");
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
                Debug.LogError($"Invalid atlas link format: {atlasLink}");
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
                    Debug.LogError($"Invalid base36 character: {c}");
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
                Debug.LogError($"Invalid URL. Must start with: {requiredBase}", this);
                Debug.Log($"Current URL: {urlString}", this);
                return;
            }
            
            // Vérifier qu'il y a bien un terme de recherche après ?q=
            if (urlString.Length <= requiredBase.Length)
            {
                Debug.LogWarning("No search term provided after ?q=", this);
                return;
            }
            
            Debug.Log($"[MTG] Sending validated search request to: {urlString}", this);
            VRCStringDownloader.LoadUrl(userUrl, (IUdonEventReceiver)this);
        }
        public override void OnStringLoadSuccess(IVRCStringDownload json)
        {
            // Validate url
            if (!IsValidResponse(json))
            {
                Debug.LogError("Invalid response URL", this);
                Debug.LogError($"URL received: {json.Url}", this);
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
                    Debug.LogError($"Error parsing response: {json.Result}");
                    return;
                }

                // get dictionary
                var dict = result.DataDictionary;

                // get next page and previous page
                // if -1, there is no next/previous page
                if (dict.ContainsKey("next") && dict["next"].TokenType == TokenType.Double)
                    nextSearch = dict["next"].Int;
                else nextSearch = -1;
                if (dict.ContainsKey("previous") && dict["previous"].TokenType == TokenType.Double)
                    previousSearch = dict["previous"].Int;
                else previousSearch = -1;

                if (!dict.ContainsKey("results") || dict["results"].TokenType != TokenType.DataList)
                {
                    Debug.LogError("Invalid or missing results in response", this);
                    return;
                }

                // get results list
                var list = dict["results"].DataList;

                // Clear previous cards
                foreach (Transform child in CardParent)
                    Destroy(child.gameObject);

                for (int i = 0; i < instantiatedCardsCount; i++) instantiatedCards[i] = null;
                instantiatedCardsCount = 0;
                for (int i = 0; i < cardKeysCount; i++) cardKeys[i] = null;
                cardKeysCount = 0;

                // Démarrer le chargement progressif des cartes
                cardsToLoad = list;
                currentLoadIndex = 0;
                nextLoadTime = Time.time;
                LoadNextBatchOfCards();
            }
            else
            {
                Debug.LogError($"Error parsing response: {json.Result}");
            }
        }
        public override void OnStringLoadError(IVRCStringDownload result)
        {
            if (!IsValidResponse(result))
            {
                Debug.LogError("Invalid response URL", this);
                return;
            }
            Debug.LogError($"Error loading string: {result.ErrorCode} - {result.Error}");
        }
        private void LoadAtlasInfo()
        {
            // Utiliser l'URL préconfigurée dans le manager pour l'atlas 0
            if (manager.tempURLs != null && manager.tempURLs.Length > 0)
            {
                VRCStringDownloader.LoadUrl(manager.tempURLs[0], (IUdonEventReceiver)this);
            }
        }
        
        private void ProcessAtlasInfo(string jsonResult)
        {
            if (!VRCJson.TryDeserializeFromJson(jsonResult, out DataToken result) || result.TokenType != TokenType.DataDictionary)
            {
                Debug.LogError("Error parsing atlas info: " + jsonResult);
                return;
            }
            
            var dict = result.DataDictionary;
            if (!dict.ContainsKey("data") || dict["data"].TokenType != TokenType.DataDictionary)
            {
                Debug.LogError("Invalid atlas info structure");
                return;
            }
            
            var data = dict["data"].DataDictionary;
            if (!data.ContainsKey("batches"))
            {
                Debug.LogError("Missing batches in atlas info");
                return;
            }
            
            var batches = data["batches"].DataList;
            
            // Pour chaque carte instanciée, trouver sa correspondance dans les batches
            for (int i = 0; i < instantiatedCardsCount; i++) 
            {
                string cardKey = cardKeys[i];
                if (string.IsNullOrEmpty(cardKey)) continue;
                
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
                            float x = (float)cardInfo["x"].Double;
                            float y = (float)cardInfo["y"].Double;
                            float width = (float)cardInfo["width"].Double;
                            float height = (float)cardInfo["height"].Double;
                            
                            Rect uvRect = new Rect(x, y, width, height);
                            
                            // Convertir l'atlas link en index pour tempURLs
                            int atlasIndex = ConvertAtlasLinkToIndex(atlasLink);
                            
                            // Assigner le manager puis demander à la carte de récupérer son image via son id
                            var searchCard = instantiatedCards[i].GetComponent<MTG_SearchCard>();
                            searchCard.manager = manager;
                            searchCard.SetImageFromId();
                            
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }
                
                if (!found)
                {
                    Debug.LogWarning($"Card with key {cardKey} not found in atlas info");
                }
            }
        }
        
        private void LoadNextBatchOfCards()
        {
            if (cardsToLoad == null || currentLoadIndex >= cardsToLoad.Count)
                return;
            
            // Charger 6 cartes maximum par lot
            int cardsInThisBatch = Mathf.Min(6, cardsToLoad.Count - currentLoadIndex);
            
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
                
                // Ajouter à la liste des cartes instanciées et clés
                if (instantiatedCardsCount < instantiatedCards.Length) {
                    instantiatedCards[instantiatedCardsCount] = card;
                    instantiatedCardsCount++;
                }
                if (cardKeysCount < cardKeys.Length) {
                    cardKeys[cardKeysCount] = cardComp.cardKey;
                    cardKeysCount++;
                }
            }
            
            currentLoadIndex += cardsInThisBatch;
            
            // Si il reste des cartes à charger, programmer le prochain lot dans 0.1 seconde
            if (currentLoadIndex < cardsToLoad.Count)
            {
                nextLoadTime = Time.time + 0.1f;
            }
            else
            {
                // Toutes les cartes chargées, récupérer les infos d'atlas
                LoadAtlasInfo();
            }
        }
    }
}
