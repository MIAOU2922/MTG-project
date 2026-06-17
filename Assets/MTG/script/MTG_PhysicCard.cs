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
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class MTG_PhysicCard : MTG_Card
    {
    
        [Header("=== PHYSIC CARD DATA ===")]
        public RawImage PlaceholderImage;
        public VRC_Pickup Pickup;
        public bool IsHeld;

        [Header("=== POOL REFERENCES ===")]
        [HideInInspector] public int PoolIndex = -1;
        [HideInInspector] public MTG_PhysicCardPoolManager PoolManager;

        [Header("=== NETWORK SYNC ===")]
        [UdonSynced, SerializeField] private string _SyncedCardKey = "";
        [UdonSynced, SerializeField] private Vector3 _SyncedPosition;
        [UdonSynced, SerializeField] private Quaternion _SyncedRotation;
        [UdonSynced, SerializeField] private bool _SyncedIsFlipped;
        private string _LastDeserializedKey = "";

        // Late-join stagger (evite que toutes les cartes s'initialisent en meme temps)
        private bool _PendingLateJoinInit = false;
        private float _LateJoinInitTime = 0f;

        [Header("=== ZOOM SETTINGS ===")]
        private float ZoomScale = 2.0f;
        private Vector3 OriginalScale;
        public bool AllowZoom = true;
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

        // Initialise la carte avec synchro reseau
        public void InitializeCard(string _CardKey)
        {
            this.VerboseLog($"InitializeCard called with key: {_CardKey}");
            
            // Set la cle locale (heritee de MTG_Card)
            base.SetCardKey(_CardKey);
            
            // Set la cle synced pour le reseau
            _SyncedCardKey = _CardKey;
            _SyncedPosition = transform.position;
            _SyncedRotation = transform.rotation;
            _LastDeserializedKey = _CardKey;
            
            // Prendre ownership et sync
            if (Networking.LocalPlayer != null)
            {
                if (!Networking.IsOwner(Networking.LocalPlayer, gameObject))
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);
                RequestSerialization();
            }
        }

        public override void OnDeserialization()
        {
            // Eviter les boucles de deserialisation
            if (_SyncedCardKey == _LastDeserializedKey && !_PendingLateJoinInit)
            {
                // Juste sync position si pas owner
                if (!Networking.IsOwner(Networking.LocalPlayer, gameObject))
                {
                    transform.position = _SyncedPosition;
                    transform.rotation = _SyncedRotation;
                }
                return;
            }

            // Late join : carte deja active pour l'owner, on doit l'initialiser
            // avec un delai aleatoire pour eviter de tout charger d'un coup
            if (!_PendingLateJoinInit && string.IsNullOrEmpty(_LastDeserializedKey) && !string.IsNullOrEmpty(_SyncedCardKey))
            {
                // Premier OnDeserialization pour cette carte (late join)
                _LastDeserializedKey = _SyncedCardKey;
                _PendingLateJoinInit = true;

                // Delai aleatoire 0 a 5 secondes pour stagger le chargement
                _LateJoinInitTime = Time.time + (PoolIndex * 0.15f);
                this.VerboseLog($"Late join queued: {_SyncedCardKey}, delay={_LateJoinInitTime - Time.time:F2}s (index={PoolIndex})");
                return;
            }

            // Sync normale (pas un late join)
            if (!_PendingLateJoinInit)
            {
                _LastDeserializedKey = _SyncedCardKey;
                this.VerboseLog($"OnDeserialization: synced cardKey = {_SyncedCardKey}");

                // Sync position pour non-owners
                if (!Networking.IsOwner(Networking.LocalPlayer, gameObject))
                {
                    transform.position = _SyncedPosition;
                    transform.rotation = _SyncedRotation;
                }

                // Appliquer la nouvelle cle de carte seulement si differente
                if (!string.IsNullOrEmpty(_SyncedCardKey) && CardKey != _SyncedCardKey)
                {
                    base.SetCardKey(_SyncedCardKey);
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            // Traitement differe du late join (evite de tout charger d'un coup)
            if (_PendingLateJoinInit && Time.time >= _LateJoinInitTime)
            {
                _PendingLateJoinInit = false;
                this.VerboseLog($"Late join init: {_SyncedCardKey}");

                if (!Networking.IsOwner(Networking.LocalPlayer, gameObject))
                {
                    transform.position = _SyncedPosition;
                    transform.rotation = _SyncedRotation;
                }

                // Appliquer la cle seulement si differente
                if (!string.IsNullOrEmpty(_SyncedCardKey) && CardKey != _SyncedCardKey)
                {
                    base.SetCardKey(_SyncedCardKey);
                }
            }

            // Sync position si owner et carte tenue
            if (IsHeld && Networking.IsOwner(Networking.LocalPlayer, gameObject))
            {
                if (Vector3.Distance(transform.position, _SyncedPosition) > 0.01f ||
                    Quaternion.Angle(transform.rotation, _SyncedRotation) > 0.5f)
                {
                    _SyncedPosition = transform.position;
                    _SyncedRotation = transform.rotation;
                    RequestSerialization();
                }
            }
        }

        private void ZoomIn()
        {
            this.VerboseLog($"ZoomIn called for cardKey: {CardKey}");
            if (IsZoomed) return;
            transform.localScale = OriginalScale * ZoomScale;
            IsZoomed = true;
        }
        private void ZoomOut()
        {
            this.VerboseLog($"ZoomOut called for cardKey: {CardKey}");
            if (!IsZoomed) return;
            transform.localScale = OriginalScale;
            IsZoomed = false;
        }
        
        // pickup metodes
        public override void OnPickupUseDown()
        {
            if (!IsHeld || !AllowZoom) return;
            ZoomIn();
        }
        public override void OnPickupUseUp()
        {
            if (!IsHeld) return;
            ZoomOut();
        }
        public override void OnPickup()
        {
            this.VerboseLog($"OnPickup called for cardKey: {CardKey}");
            if (Networking.LocalPlayer == null) return;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            IsHeld = true;
        }
        
        public override void OnDrop()
        {
            ZoomOut();
            IsHeld = false;
            // Update position quand on lache
            if (Networking.IsOwner(Networking.LocalPlayer, gameObject))
            {
                _SyncedPosition = transform.position;
                _SyncedRotation = transform.rotation;
                RequestSerialization();
            }
        }

        // Retourne la carte a la pool (reset complet)
        public void ReturnToPool()
        {
            // Reset zoom
            ZoomOut();

            // Reset held/zoom flags
            IsHeld = false;
            IsZoomed = false;

            // Reset synced data
            _SyncedCardKey = "";
            _LastDeserializedKey = "";

            // Reset la carte (SetCardKey vide → reset ImageFrontLoaded/BackLoaded/IsFlipped)
            base.SetCardKey("");

            // Reset scale au cas ou
            transform.localScale = OriginalScale;

            if (PoolManager != null)
            {
                PoolManager.DespawnCard(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}