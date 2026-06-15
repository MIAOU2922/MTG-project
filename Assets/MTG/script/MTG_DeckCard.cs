using System;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;
using TMPro;
using System.Diagnostics;

namespace MTG
{
    public class MTG_DeckCard : MTG_Card
    {
    
        [Header("=== DECK CARD DATA ===")]
        public  MTG_DeckInterface DeckInterface;
        public int cardCount = 1;
        public TextMeshProUGUI countText;

        // methodes
        // recherche le deck interface dans la scene
        private void TryFindDeckInterface()
        {
            this.VerboseLog($"TryFindDeckInterface called for cardKey: {CardKey}");
            GameObject _DeckInterfaceObj = GameObject.Find("Deck Interface");
            if (_DeckInterfaceObj == null) return;
            DeckInterface = _DeckInterfaceObj.GetComponent< MTG_DeckInterface>();
            if (DeckInterface == null) return;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (DeckInterface == null) TryFindDeckInterface();
        }
#endif
        protected override void Start()
        {
            base.Start();
            if (DeckInterface == null) TryFindDeckInterface();
        }
        public void SetCount(int count)
        {
            cardCount = Mathf.Max(0, count);
            UpdateCountDisplay();
        }
        
        public int GetCount()
        {
            this.VerboseLog($"GetCount called for cardKey: {CardKey}");
            return cardCount;
        }
        // met a jour l'affichage du compteur
        private void UpdateCountDisplay()
        {
            this.VerboseLog($"UpdateCountDisplay called for cardKey: {CardKey}");
            if (countText == null) return;
            countText.text = cardCount.ToString();
        }

        //methodes for buttons
        public void AddOne()
        {
            this.VerboseLog($"AddOne called for cardKey: {CardKey}");
            cardCount++;
            UpdateCountDisplay();
            if (DeckInterface == null) return;
            DeckInterface.OnCardCountChanged(CardKey, cardCount);
        }
        public void RemoveOne()
        {
            this.VerboseLog($"RemoveOne called for cardKey: {CardKey}");
            if (cardCount > 0)
            {
                cardCount--;
                UpdateCountDisplay();
                if (DeckInterface == null) return;
                DeckInterface.OnCardCountChanged(CardKey, cardCount);
            }
            if (cardCount == 0)
            {
                if (DeckInterface != null)
                    DeckInterface.OnCardRemoved(CardKey);
                Destroy(this.gameObject);
            }
        }
        // methodes for buttons
        // demande l'affichage de l'aperçu de la carte
        public void OnCardButtonPressed()
        {
            this.VerboseLog($"OnCardButtonPressed called for cardKey: {CardKey}");
            if (DeckInterface == null) return;
            DeckInterface.OnCardPreviewRequest(CardKey);
        }
    }
}