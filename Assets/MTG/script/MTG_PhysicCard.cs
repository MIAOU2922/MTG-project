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
    public class MTG_PhysicCard : MTG_TickableCard
    {
    
        [Header("=== PHYSIC CARD DATA ===")]
        public RawImage PlaceholderImage;
        public VRC_Pickup Pickup;
        public bool IsHeld;

        [Header("=== POOL REFERENCES ===")]
        [HideInInspector] public int PoolIndex = -1;
        [HideInInspector] public MTG_PhysicCardPoolManager PoolManager;
        [SerializeField] private bool _CountedActive = false;

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

            // S'enregistrer dans la pool (late join / objet reseau replique)
            EnsurePoolRegistration();
        }

        // Activation (activation synchronisee par VRChat) : enregistrement + comptage.
        // Necessaire car un GO inactif = script inactif (pas de Start ni Update).
        protected override void OnEnable()
        {
            base.OnEnable();
            EnsurePoolRegistration();
        }

        // Applique la visibilite dictee par l'etat reseau : VRChat ne synchronise
        // pas SetActive, donc on se base sur la cle synced pour activer/desactiver.
        public void ApplyNetworkVisibility()
        {
            if (!string.IsNullOrEmpty(_SyncedCardKey))
            {
                if (!gameObject.activeSelf)
                    gameObject.SetActive(true);
            }
            else if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        // Spawn recu du reseau (broadcast par le pool manager) : on affiche la
        // carte sans prendre l'ownership ni serialiser.
        public void ApplyRemoteSpawn(string _Key, Vector3 _Pos, Quaternion _Rot)
        {
            transform.position = _Pos;
            transform.rotation = _Rot;
            // Coherence locale avec le reseau (evite une desactivation ulterieure)
            _SyncedCardKey = _Key;
            _LastDeserializedKey = _Key;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            EnsurePoolRegistration();
            base.SetCardKey(_Key);
        }

        // Despawn recu du reseau (broadcast par le pool manager)
        public void ApplyRemoteDespawn()
        {
            IsHeld = false;
            ZoomOut();
            _SyncedCardKey = "";
            _LastDeserializedKey = "";
            _PendingLateJoinInit = false;
            base.SetCardKey("");
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        // Retrouve la pool via le Manager, s'y enregistre et se compte active si besoin.
        // Idempotent : appelable depuis OnEnable, OnDeserialization, Start et la pool.
        public void EnsurePoolRegistration()
        {
            if (PoolManager == null)
            {
                if (Manager != null && Manager.PhysicCardPool != null)
                    PoolManager = Manager.PhysicCardPool;
                else
                    return;
            }
            if (PoolIndex < 0)
                PoolManager.RegisterCard(this);

            // La carte est active mais pas encore comptee dans la pool
            if (!_CountedActive && PoolIndex >= 0 && gameObject.activeSelf)
            {
                _CountedActive = true;
                PoolManager.MarkCardActive(this);
            }
        }

        // Desactivation (despawn synchronise par VRChat) : decompte + desinscription
        protected override void OnDisable()
        {
            if (_CountedActive)
            {
                _CountedActive = false;
                if (PoolManager != null) PoolManager.MarkCardInactive(this);
            }
            if (PoolManager != null && PoolIndex >= 0)
                PoolManager.UnregisterCard(this);
            base.OnDisable();
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
            // Visibilite : VRChat ne synchronise PAS le SetActive des GameObjects.
            // La cle synced fait foi : non vide = carte spawn (on active),
            // vide = carte despawn (on desactive).
            ApplyNetworkVisibility();
            if (string.IsNullOrEmpty(_SyncedCardKey))
                return; // carte despawn : rien d'autre a traiter

            // S'assurer que la carte est connue de la pool locale
            // (spawn distant ou late join)
            EnsurePoolRegistration();

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

                // Delai plafonne pour stagger le chargement (max ~15s, la pool
                // peut contenir des milliers de slots)
                _LateJoinInitTime = Time.time + Mathf.Min(PoolIndex, 100) * 0.15f;
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

            // (Pas de retry ici : MTG_Card gere le chargement en mode evenementiel
            // via la file d'attente du Manager.)

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
            _PendingLateJoinInit = false;

            // Notifie les autres clients du despawn (cle vide = carte a desactiver)
            if (Networking.LocalPlayer != null && Networking.IsOwner(Networking.LocalPlayer, gameObject))
                RequestSerialization();

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