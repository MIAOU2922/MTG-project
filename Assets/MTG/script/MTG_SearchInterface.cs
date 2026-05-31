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
    public class MTG_SearchInterface : MTG_Interface
    {

        [Header("=== INPUT FIELDS ===")]
        public TMPro.TMP_InputField CardNameInput;
        public TMPro.TMP_InputField CardTextInput;
        public TMPro.TMP_InputField TypeLineInput;
        public TMPro.TMP_InputField CmcInput;

        [Header("=== DROPDOWNS ===")]
        public TMPro.TMP_Dropdown SetDropdown;
        public string[] SetDropdownCodes; // index 0 = "" (pas de filtre), puis les codes set (ex: khm, m21...)
        public TMPro.TMP_Dropdown LangDropdown;
        public string[] LangDropdownCodes; // index 0 = "" (pas de filtre), puis les codes langue (ex: en, fr, es...)

        [Header("=== VRC URL INPUT ===")]
        public VRCUrlInputField ValidatedUrlField;
        public TMPro.TMP_InputField DisplayUrlField;

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

        [Header("=== OTHER BUTTONS ===")]
        public UnityEngine.UI.Button ClearSearch_Button;
        public UnityEngine.UI.Button SpawnCard_Button;
        public UnityEngine.UI.Button AddToDeck_Button;

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
            if (CardsPreview == null) return;
            _PreviewCard = CardsPreview.GetComponent<MTG_SearchCard>();
            if (_PreviewCard == null) return;
            _PreviewCard.CardKey = _CardKey;
            _PreviewCard.SetImageFromId();
        }

        protected override void GenerateUrl()
        {
            this.Log("GenerateSearchUrl called");
            if (Manager == null)        return;
            if (Manager.SearchURL == null) return;
            string        _Name        = TrimInput(CardNameInput);
            string        _Text        = TrimInput(CardTextInput);
            string        _Type        = TrimInput(TypeLineInput);
            string        _Cmc         = TrimInput(CmcInput);
            string        _Set         = GetDropdownCode(SetDropdown, SetDropdownCodes);
            string        _Lang        = GetDropdownCode(LangDropdown, LangDropdownCodes);
            string        _BaseUrl     = Manager.SearchURL.ToString();
            StringBuilder _Query       = new StringBuilder();
            StringBuilder _Colors      = new StringBuilder();
            int           _RarityCount = 0;
            bool          _First       = true;
            string        _FullUrl;

            // n:
            if (_Name != "")
            {
                _Query.Append("n:\"");
                _Query.Append(_Name);
                _Query.Append("\"");
            }

            // o:
            if (_Text != "")
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("o:\"");
                _Query.Append(_Text);
                _Query.Append("\"");
            }

            // t:
            if (_Type != "")
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("t:\"");
                _Query.Append(_Type);
                _Query.Append("\"");
            }

            // cmc:
            if (_Cmc != "")
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("cmc:");
                _Query.Append(_Cmc);
            }

            // s:
            if (_Set != "")
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("s:");
                _Query.Append(_Set);
            }

            // l:
            if (_Lang != "")
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("l:");
                _Query.Append(_Lang);
            }

            // c:
            if (ColorC_Selected)
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("c:c");
            }
            else
            {
                if (ColorW_Selected) _Colors.Append("w");
                if (ColorU_Selected) _Colors.Append("u");
                if (ColorB_Selected) _Colors.Append("b");
                if (ColorR_Selected) _Colors.Append("r");
                if (ColorG_Selected) _Colors.Append("g");
                if (_Colors.Length > 0)
                {
                    if (_Query.Length > 0) _Query.Append(" ");
                    _Query.Append("c:");
                    _Query.Append(_Colors);
                }
            }

            // r:
            if (RarityCommon_Selected)   _RarityCount++;
            if (RarityUncommon_Selected) _RarityCount++;
            if (RarityRare_Selected)     _RarityCount++;
            if (RarityMythic_Selected)   _RarityCount++;

            if (_RarityCount == 1)
            {
                if (_Query.Length > 0) _Query.Append(" ");
                if (RarityCommon_Selected)   _Query.Append("r:common");
                if (RarityUncommon_Selected) _Query.Append("r:uncommon");
                if (RarityRare_Selected)     _Query.Append("r:rare");
                if (RarityMythic_Selected)   _Query.Append("r:mythic");
            }
            else if (_RarityCount > 1)
            {
                if (_Query.Length > 0) _Query.Append(" ");
                _Query.Append("(");
                _First = true;
                if (RarityCommon_Selected)   { _Query.Append("r:common");                              _First = false; }
                if (RarityUncommon_Selected) { if (!_First) _Query.Append(" or "); _Query.Append("r:uncommon"); _First = false; }
                if (RarityRare_Selected)     { if (!_First) _Query.Append(" or "); _Query.Append("r:rare");     _First = false; }
                if (RarityMythic_Selected)   { if (!_First) _Query.Append(" or "); _Query.Append("r:mythic"); }
                _Query.Append(")");
            }

            _FullUrl = _BaseUrl + _Query.ToString();

            if (DisplayUrlField != null)
                DisplayUrlField.text = _FullUrl;

            this.Log("GenerateSearchUrl result: " + _FullUrl);
            ResetFocus();
        }

        private string TrimInput(TMP_InputField _Field)
        {
            if (_Field == null || _Field.text == null) return "";
            return _Field.text.Trim();
        }

        // Lit le code d'un dropdown via son tableau parallèle (index 0 = pas de filtre)
        private string GetDropdownCode(TMP_Dropdown _Dropdown, string[] _Codes)
        {
            if (_Dropdown == null || _Codes == null) return "";
            if (_Dropdown.value <= 0 || _Dropdown.value >= _Codes.Length) return "";
            string _Code = _Codes[_Dropdown.value];
            if (_Code == null) return "";
            return _Code.Trim();
        }

        public void ClearSearch()
        {
            this.Log("ClearSearch called");
            if (CardNameInput != null) CardNameInput.text = "";
            if (CardTextInput != null) CardTextInput.text = "";
            if (TypeLineInput != null) TypeLineInput.text = "";
            if (CmcInput != null) CmcInput.text = "";
            if (SetDropdown != null) SetDropdown.value = 0;
            if (LangDropdown != null) LangDropdown.value = 0;
            ColorW_Selected = false;
            ColorU_Selected = false;
            ColorB_Selected = false;
            ColorR_Selected = false;
            ColorG_Selected = false;
            ColorC_Selected = false;
            RarityCommon_Selected = false;
            RarityUncommon_Selected = false;
            RarityRare_Selected = false;
            RarityMythic_Selected = false;
            this.Log("Search parameters reset");
            GenerateUrl();
        }

        // reset le focus pour eviter les problemes d'interaction VR
        private void ResetFocus()
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
            if (SetDropdown != null)
            {
                SetDropdown.interactable = false;
                SetDropdown.interactable = true;
            }
            if (LangDropdown != null)
            {
                LangDropdown.interactable = false;
                LangDropdown.interactable = true;
            }
            if (ValidatedUrlField != null)
            {
                ValidatedUrlField.interactable = false;
                ValidatedUrlField.interactable = true;
            }
            if (DisplayUrlField != null)
            {
                DisplayUrlField.interactable = false;
                DisplayUrlField.interactable = true;
            }
        }

        // buttons methodes
        public void OnColorW_ButtonClicked()
        {
            this.Log("OnColorW_ButtonClicked called");
            ColorW_Selected = !ColorW_Selected;
            if (ColorW_Selected && ColorC_Selected)
                ColorC_Selected = false;
            UpdateColorButtons();
            GenerateUrl();
        }
        public void OnColorU_ButtonClicked()
        {
            this.Log("OnColorU_ButtonClicked called");
            ColorU_Selected = !ColorU_Selected;
            if (ColorU_Selected && ColorC_Selected)
                ColorC_Selected = false;
            UpdateColorButtons();
            GenerateUrl();
        }
        public void OnColorB_ButtonClicked()
        {
            this.Log("OnColorB_ButtonClicked called");
            ColorB_Selected = !ColorB_Selected;
            if (ColorB_Selected && ColorC_Selected)
                ColorC_Selected = false;
            UpdateColorButtons();
            GenerateUrl();
        }
        public void OnColorR_ButtonClicked()
        {
            this.Log("OnColorR_ButtonClicked called");
            ColorR_Selected = !ColorR_Selected;
            if (ColorR_Selected && ColorC_Selected)
                ColorC_Selected = false;
            UpdateColorButtons();
            GenerateUrl();
        }
        public void OnColorG_ButtonClicked()
        {
            this.Log("OnColorG_ButtonClicked called");
            ColorG_Selected = !ColorG_Selected;
            if (ColorG_Selected && ColorC_Selected)
                ColorC_Selected = false;
            UpdateColorButtons();
            GenerateUrl();
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
            UpdateColorButtons();
            GenerateUrl();
        }
        public void UpdateColorButtons()
        {
            SetButtonColor(ColorW_Button, ColorW_Selected);
            SetButtonColor(ColorU_Button, ColorU_Selected);
            SetButtonColor(ColorB_Button, ColorB_Selected);
            SetButtonColor(ColorR_Button, ColorR_Selected);
            SetButtonColor(ColorG_Button, ColorG_Selected);
            SetButtonColor(ColorC_Button, ColorC_Selected);
        }
        public void OnRarityCommon_ButtonClicked()
        {
            this.Log("OnRarityCommon_ButtonClicked called");
            RarityCommon_Selected = !RarityCommon_Selected;
            UpdateRarityButtons();
            GenerateUrl();
        }
        public void OnRarityUncommon_ButtonClicked()
        {
            this.Log("OnRarityUncommon_ButtonClicked called");
            RarityUncommon_Selected = !RarityUncommon_Selected;
            UpdateRarityButtons();
            GenerateUrl();
        }
        public void OnRarityRare_ButtonClicked()
        {
            this.Log("OnRarityRare_ButtonClicked called");
            RarityRare_Selected = !RarityRare_Selected;
            UpdateRarityButtons();
            GenerateUrl();
        }
        public void OnRarityMythic_ButtonClicked()
        {
            this.Log("OnRarityMythic_ButtonClicked called");
            RarityMythic_Selected = !RarityMythic_Selected;
            UpdateRarityButtons();
            GenerateUrl();
        }
        public void UpdateRarityButtons()
        {
            if (RarityCommon_Selected && RarityUncommon_Selected && RarityRare_Selected && RarityMythic_Selected)
            {
                RarityCommon_Selected = false;
                RarityUncommon_Selected = false;
                RarityRare_Selected = false;
                RarityMythic_Selected = false;
            }
            SetButtonColor(RarityCommon_Button, RarityCommon_Selected);
            SetButtonColor(RarityUncommon_Button, RarityUncommon_Selected);
            SetButtonColor(RarityRare_Button, RarityRare_Selected);
            SetButtonColor(RarityMythic_Button, RarityMythic_Selected);
        }
        public void SetButtonColor(UnityEngine.UI.Button _Button, bool _IsSelected)
        {
            if (_Button == null) return;
            UnityEngine.UI.ColorBlock _Colors = _Button.colors;
            if (!_IsSelected)
            {
                _Colors.normalColor = new Color(0.300f, 0.300f, 0.300f, 1.000f);
            }
            else
            {
                _Colors.normalColor = new Color(1.000f, 1.000f, 1.000f, 1.000f);
            }
            _Button.colors = _Colors;
        }
    }
}