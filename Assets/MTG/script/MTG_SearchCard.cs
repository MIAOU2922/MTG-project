using System;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;
using TMPro;

namespace MTG
{
    public class MTG_SearchCard : MTG_Card
    {
    
        [Header("=== SEARCH CARD DATA ===")]
        public MTG_Searchinterface SearchInterface;

        // methodes
        // recherche le search interface dans la scene
        private void TryFindSearchInterface()
        {
            GameObject _SearchInterfaceObj = GameObject.Find("Search Interface");
            if (_SearchInterfaceObj == null) return;
            SearchInterface = _SearchInterfaceObj.GetComponent<MTG_Searchinterface>();
            if (SearchInterface == null) return;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (SearchInterface == null) TryFindSearchInterface();
        }
#endif
        protected override void Start()
        {
            base.Start();
            if (SearchInterface == null) TryFindSearchInterface();
        }
        // methodes for buttons
        // demande l'affichage de l'aperçu de la carte
        public void OnCardButtonPressed()
        {
            this.Log($"OnCardButtonPressed called for cardKey: {CardKey}");
            if (SearchInterface == null) return;
            SearchInterface.OnCardPreviewRequest(CardKey);
        }
    }
}