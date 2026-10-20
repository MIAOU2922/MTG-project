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
        // - load:<uuid> : charge un deck sauvegarde par son UUID
        // - list[:name[:format]] : liste mes decks ou cherche les decks publics
        protected override void GenerateUrl()
        {
            this.Log("GenerateUrl called (DeckInterface)");
            if (Manager == null || Manager.DeckURL == null) return;

            string _BaseUrl = Manager.DeckURL.ToString(); // ".../ad?q="
            string _Name    = TrimInput(DeckNameInput);       // filtre : nom du deck
            string _Format  = TrimInput(DeckDescriptionInput); // filtre : format
            string _DeckId  = CardListInput != null ? CardListInput.text.Trim() : ""; // UUID pour load

            string _FullUrl;
            if (IsDeckUuid(_DeckId))
            {
                // Nouveau format : load:<uuid> (charge un deck sauvegarde)
                _FullUrl = _BaseUrl + "load:" + UrlEncode(_DeckId);
            }
            else
            {
                // Nouveau format : list[:name[:format]]
                _FullUrl = _BaseUrl + "list";
                if (!string.IsNullOrEmpty(_Name))
                {
                    _FullUrl += ":" + UrlEncode(_Name);
                    if (!string.IsNullOrEmpty(_Format))
                        _FullUrl += ":" + UrlEncode(_Format);
                }
                else if (!string.IsNullOrEmpty(_Format))
                {
                    _FullUrl += "::" + UrlEncode(_Format);
                }
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

        // Detecte un UUID de deck (format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
        private bool IsDeckUuid(string _Input)
        {
            if (string.IsNullOrEmpty(_Input)) return false;
            if (_Input.IndexOf("-") < 0) return false;
            if (_Input.Length < 8) return false;
            return true;
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
            // Traitement de la reponse de deck (list / load / refresh)
            ProcessDeckResponse(_Json);
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

            if (!_RootDict.TryGetValue("data", out _Token) || _Token.TokenType != TokenType.DataDictionary) return;
            DataDictionary _Data = _Token.DataDictionary;

            // L'action reelle est dans data.action (link_id peut etre l'index pour /adl{index})
            string _Action = "";
            if (_Data.TryGetValue("action", out _Token)) _Action = _Token.String;
            if (string.IsNullOrEmpty(_Action) && _RootDict.TryGetValue("link_id", out _Token))
                _Action = _Token.String;

            if (_Action == "load")
            {
                int _Added = 0;
                string _DeckName = "";
                if (_Data.TryGetValue("cards_added_to_instance", out _Token)) _Added = (int)_Token.Double;
                if (_Data.TryGetValue("deck_name", out _Token)) _DeckName = _Token.String;
                this.Log($"Deck loaded: '{_DeckName}', {_Added} cards added to instance");
                // L'instance a change : recharger cartes (at0) + oracle (at3) immediatement
                if (Manager != null)
                    Manager.UpdateAtlasInfo();
            }
            else if (_Action == "list")
            {
                int _Count = 0;
                int _Matching = 0;
                if (_Data.TryGetValue("decks_count", out _Token)) _Count = (int)_Token.Double;
                if (_Data.TryGetValue("matching_total_count", out _Token)) _Matching = (int)_Token.Double;
                this.Log($"Deck search: {_Count} decks returned ({_Matching} matching total)");
                ParseDeckList(_Data);
            }
            else if (_Action == "refresh")
            {
                this.Log("Deck refresh status received");
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
            ParseDeckList(_Token.DataDictionary);
        }

        // Parse la liste des decks (at1 OU /ad?q=list) dans DeckIds / DeckNames
        private void ParseDeckList(DataDictionary _Data)
        {
            DataToken _Token;
            if (_Data == null) return;
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