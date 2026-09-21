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
            //this.Warning("OnValidate called");
        }
#endif
        [Header("=== FRAME SKIP (PERFORMANCE) ===")]
        [Tooltip("Nombre de frames entre chaque Update() effectif (pour les classes MTG_Tickable qui utilisent ShouldUpdate()). 1 = chaque frame, 50 = ~1x/sec @50fps.")]
        [SerializeField] protected int _updateEveryNFrames = 50;
        private int _frameCounter = 0;

        // Resync Manager EVENTUEL (plus aucun polling par-frame dans la base)
        private int _managerRetryCount = 0;
        private const int MAX_MANAGER_RETRIES = 3;

        /// <summary>
        /// Nombre de frames a sauter entre chaque execution effective de Update().
        /// Surchargez cette propriete dans les scripts critiques.
        /// </summary>
        protected virtual int FrameSkipCount => _updateEveryNFrames;

        protected virtual void Start()
        {
            if (string.IsNullOrEmpty(_ScriptName))
            _ScriptName = this.GetType().Name;
            _OnManagerFlagsChanged();
        }

        /// <summary>
        /// MTG_Base n'a PLUS d'Update : un composant purement evenementiel qui
        /// herite de MTG_Base n'a AUCUN cout par frame.
        /// Le travail par-frame de la base se limite au compteur de frames
        /// (TickBase), appele uniquement par MTG_Tickable.Update().
        /// </summary>
        protected void TickBase()
        {
            _frameCounter++;
        }

        /// <summary>
        /// A appeler dans Update() d'un composant heritant de MTG_Tickable :
        /// retourne true tous les N frames (FrameSkipCount).
        /// </summary>
        protected bool ShouldUpdate()
        {
            if (_frameCounter >= FrameSkipCount)
            {
                _frameCounter = 0;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Resync Manager + flags debug. EVENEMENTIEL : appele au Start() et
        /// planifie en retry differe tant que le Manager est introuvable
        /// (ordre de chargement de scene). Plus aucun polling par-frame.
        /// </summary>
        public void _OnManagerFlagsChanged()
        {
            if (Manager == null) TryFindManager();

            // Retry differe si le Manager n'est toujours pas dispo
            if (Manager == null && _managerRetryCount < MAX_MANAGER_RETRIES)
            {
                _managerRetryCount++;
                SendCustomEventDelayedFrames("_OnManagerFlagsChanged", 30 * _managerRetryCount);
                return;
            }

            SetDebugFlags();

            // Notifie les composants dependants du Manager (au Start ou au retry reussi)
            OnManagerReady();
        }

        /// <summary>
        /// Appele des que le Manager est disponible (au Start, ou via le retry
        /// differe si l'ordre de chargement de la scene l'avait retarde).
        /// </summary>
        protected virtual void OnManagerReady()
        {
        }

        // Appele quand l'objet est active (activation synchronisee par VRChat)
        protected virtual void OnEnable()
        {
        }

        // Appele quand l'objet est desactive ou detruit (pool, destroy...)
        protected virtual void OnDisable()
        {
        }

        private void SetDebugFlags()
        {
            if (Manager == null || Manager == this.gameObject) return;
            DEBUG = Manager.DEBUG;
            VERBOSE_DEBUG = Manager.VERBOSE_DEBUG;
        }
    }
}