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
    public class MTG_PhysicCard : MTG_Card
    {
    
        [Header("=== PHYSIC CARD DATA ===")]
        public VRC_Pickup Pickup;

        public bool IsHeld;

        [Header("=== ZOOM SETTINGS ===")]
        private float ZoomScale = 2.0f;
        private Vector3 OriginalScale;
        private bool IsZoomed = false;

        // methodes
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
        }
#endif
        protected override void Start()
        {
            base.Start();
            OriginalScale = transform.localScale;
        }
        private void ZoomIn()
        {
            this.Log($"ZoomIn called for cardKey: {CardKey}");
            if (IsZoomed) return;
            transform.localScale = OriginalScale * ZoomScale;
            IsZoomed = true;
        }
        private void ZoomOut()
        {
            this.Log($"ZoomOut called for cardKey: {CardKey}");
            if (!IsZoomed) return;
            transform.localScale = OriginalScale;
            IsZoomed = false;
        }
        
        // pickup metodes
        public override void OnPickupUseDown()
        {
            if (!IsHeld) return;
            ZoomIn();
        }
        public override void OnPickupUseUp()
        {
            if (!IsHeld) return;
            ZoomOut();
        }
        public override void OnPickup()
        {
            this.Log($"OnPickup called for cardKey: {CardKey}");
            if (Networking.LocalPlayer == null) return;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            IsHeld = true;
        }
        
        public override void OnDrop()
        {
            ZoomOut();
            IsHeld = false;
        }
    }
}