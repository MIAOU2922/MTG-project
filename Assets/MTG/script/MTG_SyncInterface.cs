using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MTG
{
    public class MTG_SyncInterface : MTG_Base
    {
        [Header("=== SYNC INTERFACE ===")]
        public GameObject Panel;
        public GameObject Create;
        public GameObject Join;
        public GameObject Waiting;
        public GameObject Loading;
        public void OnClicked()
        {
            Manager.JoinGame(true);
        }
        internal void ShowCreate()
        {
            Panel.SetActive(true);
            Create.SetActive(true);
            Join.SetActive(false);
            Waiting.SetActive(false);
            Loading.SetActive(false);
        }
        internal void ShowJoin()
        {
            Panel.SetActive(true);
            Create.SetActive(false);
            Join.SetActive(true);
            Waiting.SetActive(false);
            Loading.SetActive(false);
        }
        internal void ShowWaiting()
        {
            Panel.SetActive(true);
            Create.SetActive(false);
            Join.SetActive(false);
            Waiting.SetActive(true);
            Loading.SetActive(false);
        }
        internal void ShowLoading()
        {
            Panel.SetActive(true);
            Create.SetActive(false);
            Join.SetActive(false);
            Waiting.SetActive(false);
            Loading.SetActive(true);
        }
        internal void Hide()
        {
            Panel.SetActive(false);
        }
        internal void Show()
        {
            if (Networking.LocalPlayer != null && Networking.LocalPlayer.isMaster && Manager.GetInstanceID() == -1)
                ShowCreate();
            else if (Networking.LocalPlayer != null && !Networking.LocalPlayer.isMaster && Manager.GetInstanceID() == -1)
                ShowWaiting();
            else if (Networking.LocalPlayer != null && !Networking.LocalPlayer.isMaster && Manager.GetInstanceID() != -1)
                ShowJoin();
            else
                ShowLoading();
            
        }
    }
}