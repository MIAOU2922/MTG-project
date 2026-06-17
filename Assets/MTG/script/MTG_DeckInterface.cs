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

        //methodes
        protected override void Start()
        {
            base.Start();
        }
        protected override void Update()
        {
            base.Update();
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
        }

        private void ProcessDeckResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("ProcessDeckResponse called");
            // TODO: implementer le traitement de la reponse de deck
            // Parse le JSON et affiche le deck
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