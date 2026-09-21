using System;
using System.Text;
using System.Collections.Generic;
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
    public class  MTG_DeckInterface : MTG_Interface

    {
        [Header("=== INPUT FIELDS ===")]
        public TMPro.TMP_InputField DeckNameInput;
        public TMPro.TMP_InputField DeckDescriptionInput;
        public TMPro.TMP_InputField CardListInput;

        [Header("=== DECK LIST DATA (at1) ===")]
        public string[] DeckIds;
        public string[] DeckNames;

        //methodes
        protected override void Start()
        {
            base.Start();
        }

        // (Pas d'Update ici : interface purement evenementielle)

        // Construit l'URL deck au NOUVEAU format : /ad?q=action:param1:param2:...
        // save:name:format:list:lang:description | parse:list:format:lang
        // load:id | load:format:list:lang | delete:id | list[:search]
        protected override void GenerateUrl()
        {
            this.Log("GenerateUrl called (DeckInterface)");
            if (Manager == null || Manager.DeckURL == null) return;

            string _BaseUrl = Manager.DeckURL.ToString(); // ".../ad?q="
            string _Name    = TrimInput(DeckNameInput);
            string _Desc    = TrimInput(DeckDescriptionInput);
            string _List    = CardListInput != null ? CardListInput.text : "";

            string _FullUrl;
            if (string.IsNullOrEmpty(_Name) || string.IsNullOrEmpty(_List))
            {
                // Pas assez d'infos pour un save : URL de base uniquement
                _FullUrl = _BaseUrl;
            }
            else
            {
                // save:name:auto:list  (+ :en:description si description)
                _FullUrl = _BaseUrl + "save:" + UrlEncode(_Name) + ":auto:" + UrlEncode(_List);
                if (!string.IsNullOrEmpty(_Desc))
                    _FullUrl += ":en:" + UrlEncode(_Desc);
            }

            // Affichage pour copier/coller (VRCUrl dynamique impossible en Udon)
            if (UrlDisplayText != null)
                UrlDisplayText.text = _FullUrl;

            // Reset du champ valide uniquement si pas de query
            if (_FullUrl == _BaseUrl && ValidatedInput != null)
                ValidatedInput.SetUrl(Manager.DeckURL);

            this.Log("GenerateUrl result: " + _FullUrl);
            ResetFocusUrlInputFields();
        }

        // Encodeur URL minimal compatible Udon (UnityWebRequest.EscapeURL n'est pas expose)
        private string UrlEncode(string _Input)
        {
            if (string.IsNullOrEmpty(_Input)) return "";
            string _Result = _Input;
            _Result = _Result.Replace("%", "%25");
            _Result = _Result.Replace("\r", "%0D");
            _Result = _Result.Replace("\n", "%0A");
            _Result = _Result.Replace(" ", "%20");
            _Result = _Result.Replace("\"", "%22");
            _Result = _Result.Replace(":", "%3A");
            _Result = _Result.Replace("/", "%2F");
            _Result = _Result.Replace("&", "%26");
            _Result = _Result.Replace("?", "%3F");
            _Result = _Result.Replace("=", "%3D");
            _Result = _Result.Replace("#", "%23");
            _Result = _Result.Replace("+", "%2B");
            return _Result;
        }

        private string TrimInput(TMP_InputField _Field)
        {
            if (_Field == null || _Field.text == null) return "";
            return _Field.text.Trim();
        }

        // callback pour les reponses de deck
        public override void OnDeckResponse(IVRCStringDownload _Json)
        {
            this.Log("OnDeckResponse called in MTG_DeckInterface");
            if (_Json == null || _Json.Result == null)
            {
                this.Error("Deck response is null");
                return;
            }
            // Traitement de la reponse de deck
            ProcessDeckResponse(_Json);

            // Une modif de deck (save/delete) invalide la liste : re-fetch at1 (event only)
            if (_Json.Url != null)
            {
                string _UrlStr = _Json.Url.ToString();
                if (_UrlStr.IndexOf("q=save:") >= 0 || _UrlStr.IndexOf("q=delete:") >= 0)
                {
                    this.Log("Deck modification detected, re-fetching deck list (at1)");
                    if (Manager != null)
                        Manager.FetchDeckList();
                }
            }
        }

        // callback pour la liste des decks (at1) — forwarde par le Manager
        public override void OnDeckListResponse(IVRCStringDownload _Json)
        {
            this.Log("OnDeckListResponse called in MTG_DeckInterface");
            if (_Json == null || _Json.Result == null)
            {
                this.Error("Deck list response is null");
                return;
            }
            ProcessDeckListResponse(_Json);
        }

        private void ProcessDeckResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("ProcessDeckResponse called");

            DataToken _Token;
            if (!VRCJson.TryDeserializeFromJson(_Json.Result, out _Token)) return;
            if (_Token.TokenType != TokenType.DataDictionary) return;
            DataDictionary _RootDict = _Token.DataDictionary;

            // Erreur API (400/401/403/404/500)
            if (_RootDict.TryGetValue("error", out _Token))
            {
                this.Error($"Deck API error: {_Token.String}");
                return;
            }

            // Action depuis l'en-tete standardise (link_id = action)
            string _Action = "";
            if (_RootDict.TryGetValue("link_id", out _Token))
                _Action = _Token.String;

            if (!_RootDict.TryGetValue("data", out _Token) || _Token.TokenType != TokenType.DataDictionary) return;
            DataDictionary _Data = _Token.DataDictionary;

            if (_Action == "save")
            {
                string _DeckId = "";
                string _DeckName = "";
                if (_Data.TryGetValue("deck_id", out _Token)) _DeckId = _Token.String;
                if (_Data.TryGetValue("deck_name", out _Token)) _DeckName = _Token.String;
                this.Log($"Deck saved: '{_DeckName}' (id: {_DeckId})");
            }
            else if (_Action == "load")
            {
                int _Added = 0;
                string _DeckName = "";
                if (_Data.TryGetValue("cards_added_to_instance", out _Token)) _Added = (int)_Token.Double;
                if (_Data.TryGetValue("deck_name", out _Token)) _DeckName = _Token.String;
                this.Log($"Deck loaded: '{_DeckName}', {_Added} cards added to instance");
            }
            else if (_Action == "delete")
            {
                string _DeckName = "";
                if (_Data.TryGetValue("deck_name", out _Token)) _DeckName = _Token.String;
                this.Log($"Deck deleted: '{_DeckName}'");
            }
            else if (_Action == "parse")
            {
                int _Found = 0;
                int _NotFound = 0;
                if (_Data.TryGetValue("stats", out _Token) && _Token.TokenType == TokenType.DataDictionary)
                {
                    DataDictionary _Stats = _Token.DataDictionary;
                    if (_Stats.TryGetValue("found", out _Token)) _Found = (int)_Token.Double;
                    if (_Stats.TryGetValue("not_found", out _Token)) _NotFound = (int)_Token.Double;
                }
                this.Log($"Deck parsed: {_Found} found, {_NotFound} not found");
            }
            else if (_Action == "list")
            {
                int _Count = 0;
                if (_Data.TryGetValue("decks_count", out _Token)) _Count = (int)_Token.Double;
                this.Log($"Deck search: {_Count} decks");
            }
            else
            {
                this.Log($"Deck response action: {_Action}");
            }
        }

        private void ProcessDeckListResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("ProcessDeckListResponse called");

            DataToken _Token;
            if (!VRCJson.TryDeserializeFromJson(_Json.Result, out _Token)) return;
            if (_Token.TokenType != TokenType.DataDictionary) return;
            DataDictionary _RootDict = _Token.DataDictionary;

            // Format at1: { data: { total_decks, decks: [{ id, name }] } }
            if (!_RootDict.TryGetValue("data", out _Token) || _Token.TokenType != TokenType.DataDictionary) return;
            DataDictionary _Data = _Token.DataDictionary;

            if (!_Data.TryGetValue("decks", out _Token) || _Token.TokenType != TokenType.DataList) return;
            DataList _Decks = _Token.DataList;

            int _Count = _Decks.Count;
            DeckIds = new string[_Count];
            DeckNames = new string[_Count];

            for (int i = 0; i < _Count; i++)
            {
                if (_Decks[i].TokenType != TokenType.DataDictionary) continue;
                DataDictionary _Deck = _Decks[i].DataDictionary;

                string _Id = "";
                string _Name = "";
                if (_Deck.TryGetValue("id", out _Token)) _Id = _Token.String;
                if (_Deck.TryGetValue("name", out _Token)) _Name = _Token.String;
                DeckIds[i] = _Id;
                DeckNames[i] = _Name;
            }

            this.Log($"Deck list parsed: {_Count} decks");
            // TODO: brancher la liste UI (scroll view) quand les elements seront definis
        }

        public void OnCardPreviewRequest(string _CardKey)
        {
            this.VerboseLog("OnCardPreviewRequest: " + _CardKey);
            MTG_DeckCard _PreviewCard;
            if (CardsPreview == null) return;
            _PreviewCard = CardsPreview.GetComponent<MTG_DeckCard>();
            if (_PreviewCard == null) return;
            _PreviewCard.SetCardKey(_CardKey);
        }
        public void OnCardCountChanged(string _CardKey, int _Count)
        {
            this.VerboseLog($"OnCardCountChanged called for cardKey: {_CardKey} with count: {_Count}");
        }
        public void OnCardRemoved(string _CardKey)
        {
            this.VerboseLog($"OnCardRemoved called for cardKey: {_CardKey}");
        }
    }
}