using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MTG
{
    public class MTG_SyncInterface : UdonSharpBehaviour
    {
        public MTG_Manager Manager;

        public GameObject panel;
        public GameObject create;
        public GameObject loading;
        public GameObject join;

        internal void ShowCreate()
        {
            panel.SetActive(true);
            join.SetActive(false);
            create.SetActive(true);
            loading.SetActive(false);
        }

        internal void ShowJoin()
        {
            panel.SetActive(true);
            join.SetActive(true);
            create.SetActive(false);
            loading.SetActive(false);
        }

        internal void Show()
        {
            if (Networking.LocalPlayer.isMaster && Manager.GetInstanceID() == -1)
                ShowCreate();
            else if (Manager.GetInstanceID() != -1)
                ShowJoin();
        }

        public void OnClicked()
        {
            Manager.JoinGame(true);
        }

        internal void ShowLoading()
        {
            panel.SetActive(true);
            join.SetActive(false);
            create.SetActive(false);
            loading.SetActive(true);
        }

        internal void Hide()
        {
            panel.SetActive(false);
        }
    }
}