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
    public class MTG_Interface : MTG_Base
    {
        [Header("=== URL FIELD ===")]
        public VRCUrlInputField ValidatedInput;
        public TMPro.TMP_InputField UrlDisplayText;

        [Header("=== CARDS REFERENCES ===")]
        public GameObject CardPrefab;
        public GameObject CardsPreview;
        public Transform CardsParent;

        //methodes

        protected override void Start()
        {
            base.Start();
        }
        protected override void Update()
        {
            base.Update();
        }

        protected virtual void GenerateSearchUrl()
        {
            
        }






        // reset le focus des input fields pour eviter les problemes d'interaction VR
        protected void ResetFocusUrlInputFields()
        {
            if (ValidatedInput != null)
            {
                ValidatedInput.interactable = false;
                ValidatedInput.interactable = true;
            }
            if (UrlDisplayText != null)
            {
                UrlDisplayText.interactable = false;
                UrlDisplayText.interactable = true;
            }
        }
    }
}