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
    public class  MTG_SearchInterface : MTG_Interface
    {

        [Header("=== INPUT FIELDS ===")]
        public TMPro.TMP_InputField CardNameInput;
        public TMPro.TMP_InputField CardTextInput;
        public TMPro.TMP_InputField TypeLineInput;
        public TMPro.TMP_InputField CmcInput;

        [Header("=== DROPDOWNS ===")]
        public TMPro.TMP_Dropdown SetDropdown;
        public TMPro.TMP_Dropdown LangDropdown;

        [Header("=== COLOR BUTTONS ===")]
        public UnityEngine.UI.Button ColorW_Button;
        public UnityEngine.UI.Button ColorU_Button;
        public UnityEngine.UI.Button ColorB_Button;
        public UnityEngine.UI.Button ColorR_Button;
        public UnityEngine.UI.Button ColorG_Button;
        public UnityEngine.UI.Button ColorC_Button;

        [Header("=== RARITY BUTTONS ===")]
        public UnityEngine.UI.Button RarityCommon_Button;
        public UnityEngine.UI.Button RarityUncommon_Button;
        public UnityEngine.UI.Button RarityRare_Button;
        public UnityEngine.UI.Button RarityMythic_Button;

        //states
        private bool ColorW_Selected = false;
        private bool ColorU_Selected = false;
        private bool ColorB_Selected = false;
        private bool ColorR_Selected = false;
        private bool ColorG_Selected = false;
        private bool ColorC_Selected = false;
        private bool RarityCommon_Selected = false;
        private bool RarityUncommon_Selected = false;
        private bool RarityRare_Selected = false;
        private bool RarityMythic_Selected = false;

        //methodes
        protected override void Start()
        {
            base.Start();
        }
        protected override void Update()
        {
            base.Update();
        }
        public void OnCardPreviewRequest(String _CardKey)
        {
            this.Log("OnCardPreviewRequest called: " + _CardKey);
            MTG_SearchCard _PreviewCard;
            if (CardsPreview == null ) return;
            _PreviewCard = CardsPreview.GetComponent<MTG_SearchCard>();
            if (_PreviewCard == null) return;
            _PreviewCard.CardKey = _CardKey;
            _PreviewCard.SetImageFromId();
        }

        protected override void GenerateSearchUrl()
        {
            this.Log("GenerateSearchUrl called");
            ResetFocusInputFields();

        }







        // reset le focus des input fields pour eviter les problemes d'interaction VR
        private void ResetFocusInputFields()
        {
            if (CardNameInput != null)
            {
                CardNameInput.interactable = false;
                CardNameInput.interactable = true;
            }
            if (CardTextInput != null)
            {
                CardTextInput.interactable = false;
                CardTextInput.interactable = true;
            }
            if (TypeLineInput != null)
            {
                TypeLineInput.interactable = false;
                TypeLineInput.interactable = true;
            }
            if (CmcInput != null)
            {
                CmcInput.interactable = false;
                CmcInput.interactable = true;
            }
            ResetFocusUrlInputFields();
        }






        // buttons methodes
        public void OnColorW_ButtonClicked()
        {
            this.Log("OnColorW_ButtonClicked called");
            ColorW_Selected = !ColorW_Selected;
            if (ColorW_Selected && ColorC_Selected)
                ColorC_Selected = false;
            GenerateSearchUrl();
        }
        public void OnColorU_ButtonClicked()
        {
            this.Log("OnColorU_ButtonClicked called");
            ColorU_Selected = !ColorU_Selected;
            if (ColorU_Selected && ColorC_Selected)
                ColorC_Selected = false;
            GenerateSearchUrl();
        }
        public void OnColorB_ButtonClicked()
        {
            this.Log("OnColorB_ButtonClicked called");
            ColorB_Selected = !ColorB_Selected;
            if (ColorB_Selected && ColorC_Selected)
                ColorC_Selected = false;
            GenerateSearchUrl();
        }
        public void OnColorR_ButtonClicked()
        {
            this.Log("OnColorR_ButtonClicked called");
            ColorR_Selected = !ColorR_Selected;
            if (ColorR_Selected && ColorC_Selected)
                ColorC_Selected = false;
            GenerateSearchUrl();
        }
        public void OnColorG_ButtonClicked()
        {
            this.Log("OnColorG_ButtonClicked called");
            ColorG_Selected = !ColorG_Selected;
            if (ColorG_Selected && ColorC_Selected)
                ColorC_Selected = false;
            GenerateSearchUrl();
        }
        public void OnColorC_ButtonClicked()
        {
            this.Log("OnColorC_ButtonClicked called");
            ColorC_Selected = !ColorC_Selected;
            if (ColorC_Selected)
            {
                ColorW_Selected = false;
                ColorU_Selected = false;
                ColorB_Selected = false;
                ColorR_Selected = false;
                ColorG_Selected = false;
            }
            GenerateSearchUrl();
        }
    }
}