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

        protected virtual void GenerateUrl()
        {
            
        }

        // Envoie la requete au serveur via VRCUrlInputField (necessite validation utilisateur)
        public virtual void SendRequest()
        {
            this.Log("SendRequest called (base implementation)");
            if (ValidatedInput == null)
            {
                this.Error("ValidatedInput is null - cannot send request");
                return;
            }
            
            // L'utilisateur doit valider l'URL dans le VRCUrlInputField
            // Quand il appuie sur Entree ou clique sur le bouton de validation,
            // la methode OnUrlValidated() sera appelee automatiquement
        }

        // Callback appele automatiquement par VRCUrlInputField quand l'utilisateur valide l'URL
        public virtual void OnUrlValidated()
        {
            this.Log("OnUrlValidated called (base implementation)");
            if (ValidatedInput == null)
            {
                this.Error("ValidatedInput is null");
                return;
            }
            
            VRCUrl _ValidatedUrl = ValidatedInput.GetUrl();
            if (_ValidatedUrl == null)
            {
                this.Error("Validated URL is null");
                return;
            }
            
            string _UrlString = _ValidatedUrl.ToString();
            this.Log($"User validated URL: {_UrlString}");
            
            // Envoyer la requete au serveur via le Manager
            if (Manager != null)
            {
                VRCStringDownloader.LoadUrl(_ValidatedUrl, (IUdonEventReceiver)Manager);
                this.Log("Request sent to server via Manager");
            }
            else
            {
                this.Error("Manager is null - cannot send request");
            }
        }

        // Methodes virtuelles pour les callbacks de reponse
        public virtual void OnSearchResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("OnSearchResponse called (base implementation)");
        }

        public virtual void OnDeckResponse(IVRCStringDownload _Json)
        {
            this.VerboseLog("OnDeckResponse called (base implementation)");
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