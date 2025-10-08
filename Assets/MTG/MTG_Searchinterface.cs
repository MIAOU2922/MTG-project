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

        private void Start()
        {
            // Pré-remplir le champ avec l'URL de base du manager
            input.SetUrl(manager.searchURL);
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
            if (!IsSearchResponse(json))
            {
                Debug.LogError("Invalid response URL", this);
                return;
            }
            // Parse JSON
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

                // Démarrer le chargement progressif des cartes
                cardsToLoad = list;
                currentLoadIndex = 0;
                LoadNextBatchOfCards();
            }
            else
            {
                Debug.LogError($"Error parsing response: {json.Result}");
            }
        }
        public override void OnStringLoadError(IVRCStringDownload result)
        {
            if (!IsSearchResponse(result))
            {
                Debug.LogError("Invalid response URL", this);
                return;
            }
            Debug.LogError($"Error loading string: {result.ErrorCode} - {result.Error}");
        }
        private bool IsSearchUrl(VRCUrl url)
        {
            return url.ToString().StartsWith(manager.searchURL.ToString());
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
                cardComp.SetData(cardDict);
            }
            
            currentLoadIndex += cardsInThisBatch;
            
            // Si il reste des cartes à charger, programmer le prochain lot dans 0.1 seconde
            if (currentLoadIndex < cardsToLoad.Count)
            {
                SendCustomEventDelayedSeconds(nameof(LoadNextBatchOfCards), 0.1f);
            }
        }
    }
}
