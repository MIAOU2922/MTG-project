using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Data;
using VRC.SDK3.Image;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Buffers;

namespace MTG
{
    public class MTG_Base : UdonSharpBehaviour
    {
        [Header("=== GLOBAL DEBUG ===")]
        public bool DEBUG = false;
        public bool VERBOSE_DEBUG = false;

        [Header("=== MANAGER REFERENCE ===")]
        public MTG_Manager Manager;

        [Header("=== SCRIPT IDENTITY ===")]
        [SerializeField] private string _ScriptName = "";
        public string ScriptName => string.IsNullOrEmpty(_ScriptName) ? this.GetType().Name : _ScriptName;
        
        //methodes
        // recherche le manager dans la scene
        private void TryFindManager()
        {
            GameObject _ManagerObj = GameObject.Find("Manager");
            if (_ManagerObj == null) return;
            Manager = _ManagerObj.GetComponent<MTG_Manager>();
            if (Manager == null) return;
        }
#if !COMPILER_UDONSHARP && UNITY_EDITOR

        // validation dans l'editeur
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(_ScriptName))
                _ScriptName = this.GetType().Name;
            if (Manager == null) TryFindManager();
            SetDebugFlags();
        }
#endif
        protected virtual void Start()
        {
            if (Manager == null) TryFindManager();
            SetDebugFlags();
        }
        protected virtual void Update()
        {
            if (Manager == null) TryFindManager();
            if (Manager != null && (DEBUG != Manager.DEBUG || VERBOSE_DEBUG != Manager.VERBOSE_DEBUG))
            {
                SetDebugFlags();
            }
        }

        private void SetDebugFlags()
        {
            if (Manager == null || Manager == this.gameObject) return;
            DEBUG = Manager.DEBUG;
            VERBOSE_DEBUG = Manager.VERBOSE_DEBUG;
        }
    }
}