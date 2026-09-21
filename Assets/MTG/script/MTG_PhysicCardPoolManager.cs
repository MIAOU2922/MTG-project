using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MTG
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class MTG_PhysicCardPoolManager : MTG_Base
    {
        [Header("=== POOL CONFIGURATION ===")]
        [Tooltip("Prefab MTG_PhysicCard pour instanciation (si la pool est vide dans l'inspecteur)")]
        public GameObject PhysicCardPrefab;
        [Tooltip("Parent des cartes instanciees")]
        public Transform CardsParent;
        [Tooltip("Taille max de la pool")]
        public int PoolSize = 8096;
        [Tooltip("Cartes pre-instanciées dans la scène (optionnel). Si vide, le prefab sera instancié au runtime.")]
        public GameObject[] PreInstantiatedCards;

        [Header("=== POOL DATA (RUNTIME) ===")]
        [SerializeField] private GameObject[] CardPool;
        [SerializeField] private int ActiveCount = 0;
        [SerializeField] private bool IsInitialized = false;

        [Header("=== NETWORK EVENTS (broadcast spawn/despawn) ===")]
        // Le pool manager est toujours actif : sa synchro reseau est fiable.
        // On y centralise les evenements spawn/despawn (slot + id + transform),
        // rejoues par tous les autres clients via OnDeserialization.
        private const int EVENT_BUFFER = 64;
        [UdonSynced, SerializeField] private int _EventHead = 0;                     // nb total d'evenements emis
        [UdonSynced, SerializeField] private int[] _EventOps = new int[EVENT_BUFFER]; // 1 = spawn, 2 = despawn
        [UdonSynced, SerializeField] private int[] _EventSlots = new int[EVENT_BUFFER];
        [UdonSynced, SerializeField] private string[] _EventKeys = new string[EVENT_BUFFER];
        [UdonSynced, SerializeField] private Vector3[] _EventPositions = new Vector3[EVENT_BUFFER];
        [UdonSynced, SerializeField] private Quaternion[] _EventRotations = new Quaternion[EVENT_BUFFER];
        private int _LastSeenHead = -1;

        protected override void Start()
        {
            base.Start();
            if (!IsInitialized) InitializePool();

            // Synchro de la pool : les cartes spawn par d'autres joueurs
            // (ou instantiees a runtime) doivent etre enregistrees ici aussi.
            // - immediat : cartes locales
            // - differe : objets reseau recus apres notre arrivee (late join)
            RebuildPoolFromScene();
            SendCustomEventDelayedSeconds(nameof(RebuildPoolFromScene), 2f);
        }

        private void InitializePool()
        {
            if (PoolSize <= 0) PoolSize = 50;
            CardPool = new GameObject[PoolSize];

            // Copier les cartes pre-instanciées depuis l'inspecteur
            if (PreInstantiatedCards != null)
            {
                int _Count = PreInstantiatedCards.Length;
                if (_Count > PoolSize) _Count = PoolSize;
                for (int i = 0; i < _Count; i++)
                {
                    if (PreInstantiatedCards[i] != null)
                    {
                        CardPool[i] = PreInstantiatedCards[i];
                        CardPool[i].SetActive(false);
                        // Pre-lier les refs : a son activation, la carte saura
                        // deja a quelle pool elle appartient (pas de scan)
                        MTG_PhysicCard _Card = PreInstantiatedCards[i].GetComponent<MTG_PhysicCard>();
                        if (_Card != null)
                        {
                            _Card.PoolManager = this;
                            _Card.PoolIndex = i;
                        }
                    }
                }
                if (_Count > 0)
                    this.Log($"Loaded {_Count} pre-instantiated cards into pool");
            }

            IsInitialized = true;
            this.Log($"Pool initialized with capacity: {PoolSize}, pre-loaded: {CountActive()}");
        }

        private int CountActive()
        {
            int _Count = 0;
            if (CardPool == null) return 0;
            for (int i = 0; i < CardPool.Length; i++)
                if (CardPool[i] != null && CardPool[i].activeSelf) _Count++;
            return _Count;
        }

        // Spawn une carte physique depuis la pool
        public MTG_PhysicCard SpawnCard(string _CardKey, Vector3 _Position, Quaternion _Rotation)
        {
            if (!IsInitialized) InitializePool();
            if (string.IsNullOrEmpty(_CardKey))
            {
                this.Error("SpawnCard: CardKey is null or empty");
                return null;
            }
            if (PhysicCardPrefab == null)
            {
                this.Error("SpawnCard: PhysicCardPrefab is null");
                return null;
            }

            // Chercher un slot libre (null ou desactive)
            for (int i = 0; i < PoolSize; i++)
            {
                if (CardPool[i] == null || !CardPool[i].activeSelf)
                {
                    MTG_PhysicCard _Card = ActivateCard(i, _CardKey, _Position, _Rotation);
                    if (_Card != null)
                    {
                        // Broadcast du spawn a tous les clients
                        QueueEvent(1, _Card.PoolIndex, _CardKey, _Position, _Rotation);
                        PublishEvents();
                    }
                    return _Card;
                }
            }

            // Pool pleine
            this.Error($"SpawnCard: Pool is full! ({ActiveCount}/{PoolSize})");
            return null;
        }

        private MTG_PhysicCard ActivateCard(int _Index, string _CardKey, Vector3 _Position, Quaternion _Rotation)
        {
            if (CardPool[_Index] == null)
            {
                // Instancier une nouvelle carte si pas de pre-instanciation
                if (PhysicCardPrefab == null)
                {
                    this.Error($"ActivateCard: No pre-instantiated card at index {_Index} and no prefab to instantiate");
                    return null;
                }
                GameObject _Obj = Instantiate(PhysicCardPrefab);
                if (_Obj == null)
                {
                    this.Error($"ActivateCard: Failed to instantiate card at index {_Index}");
                    return null;
                }
                CardPool[_Index] = _Obj;
                if (CardsParent != null)
                    _Obj.transform.SetParent(CardsParent, true);
            }

            GameObject _CardObj = CardPool[_Index];
            _CardObj.transform.position = _Position;
            _CardObj.transform.rotation = _Rotation;
            _CardObj.SetActive(true);

            MTG_PhysicCard _Card = _CardObj.GetComponent<MTG_PhysicCard>();
            if (_Card != null)
            {
                _Card.PoolIndex = _Index;
                _Card.PoolManager = this;
                // Enregistre + compte la carte comme active (idempotent)
                _Card.EnsurePoolRegistration();
                _Card.InitializeCard(_CardKey);
            }

            this.VerboseLog($"Spawned card '{_CardKey}' at index {_Index} ({ActiveCount}/{PoolSize})");
            return _Card;
        }

        // Despawn une carte et la retourne a la pool.
        // Le decompte est fait par MTG_PhysicCard.OnDisable (activation/desactivation
        // synchronisees par VRChat) : on met PoolIndex = -1 AVANT SetActive(false)
        // pour eviter tout double decompte.
        public void DespawnCard(MTG_PhysicCard _Card)
        {
            if (_Card == null) return;
            int _Index = _Card.PoolIndex;
            _Card.PoolIndex = -1;
            _Card.gameObject.SetActive(false);

            // Broadcast du despawn a tous les clients
            QueueEvent(2, _Index, "", Vector3.zero, Quaternion.identity);
            PublishEvents();

            this.VerboseLog($"Despawned card at index {_Index} ({ActiveCount}/{PoolSize})");
        }

        // Despawn toutes les cartes
        public void DespawnAll()
        {
            bool _Any = false;
            for (int i = 0; i < PoolSize; i++)
            {
                if (CardPool[i] != null && CardPool[i].activeSelf)
                {
                    MTG_PhysicCard _Card = CardPool[i].GetComponent<MTG_PhysicCard>();
                    if (_Card != null)
                        _Card.PoolIndex = -1; // OnDisable fait le decompte
                    CardPool[i].SetActive(false);
                    QueueEvent(2, i, "", Vector3.zero, Quaternion.identity);
                    _Any = true;
                }
            }
            if (_Any) PublishEvents(); // un seul envoi pour tout le batch
            ActiveCount = 0; // paranoia
            this.Log("All cards despawned");
        }

        public int GetActiveCount() { return ActiveCount; }
        public int GetPoolCapacity() { return PoolSize; }

        public string GetPoolStatus()
        {
            return $"Pool: {ActiveCount}/{PoolSize} active";
        }

        // === SYNC DE LA POOL ===

        // Enregistre une carte dans la pool (idempotent). Appele par les cartes
        // elles-memes (late join, spawn distant) et par RebuildPoolFromScene.
        public void RegisterCard(MTG_PhysicCard _Card)
        {
            if (_Card == null || CardPool == null) return;

            // Deja enregistree au bon endroit ?
            if (_Card.PoolManager == this && _Card.PoolIndex >= 0 && _Card.PoolIndex < CardPool.Length &&
                CardPool[_Card.PoolIndex] == _Card.gameObject)
                return;

            // Slot existant pour cet objet (objet reseau replique) ?
            for (int i = 0; i < CardPool.Length; i++)
            {
                if (CardPool[i] == _Card.gameObject)
                {
                    _Card.PoolManager = this;
                    _Card.PoolIndex = i;
                    return;
                }
            }

            // Premier slot libre (carte instantiee a runtime)
            for (int i = 0; i < CardPool.Length; i++)
            {
                if (CardPool[i] == null)
                {
                    CardPool[i] = _Card.gameObject;
                    _Card.PoolManager = this;
                    _Card.PoolIndex = i;
                    return;
                }
            }

            this.Warning($"RegisterCard: pool pleine, carte '{_Card.name}' non enregistree");
        }

        // Retire les refs de pool d'une carte (appele a la desactivation).
        // Le decompte ActiveCount est gere separement via MarkCardActive/Inactive.
        public void UnregisterCard(MTG_PhysicCard _Card)
        {
            if (_Card == null || CardPool == null) return;
            if (_Card.PoolIndex < 0 || _Card.PoolIndex >= CardPool.Length) return;
            if (CardPool[_Card.PoolIndex] != _Card.gameObject) return; // deja nettoye

            _Card.PoolIndex = -1;
            _Card.PoolManager = null;
            this.VerboseLog($"Unregistered card '{_Card.name}'");
        }

        // Comptage actif/inactif (appele par la carte via OnEnable/OnDisable)
        public void MarkCardActive(MTG_PhysicCard _Card)
        {
            if (_Card == null) return;
            ActiveCount++;
            this.VerboseLog($"Card '{_Card.name}' marked active ({ActiveCount}/{PoolSize})");
        }
        public void MarkCardInactive(MTG_PhysicCard _Card)
        {
            if (_Card == null || ActiveCount <= 0) return;
            ActiveCount--;
            this.VerboseLog($"Card '{_Card.name}' marked inactive ({ActiveCount}/{PoolSize})");
        }

        // Reconstruit la pool depuis la hierarchie de la scene : scanne CardsParent
        // (y compris les inactives) et enregistre toutes les MTG_PhysicCard connues.
        // Le comptage reste pilote par OnEnable/OnDisable des cartes.
        public void RebuildPoolFromScene()
        {
            if (!IsInitialized || CardPool == null)
            {
                InitializePool();
                if (CardPool == null) return;
            }

            Transform _Parent = CardsParent != null ? CardsParent : transform;
            MTG_PhysicCard[] _Cards = _Parent.GetComponentsInChildren<MTG_PhysicCard>(true);

            // Applique la visibilite reseau (VRChat ne sync pas le SetActive)
            // puis enregistre les cartes de la scene (idempotent)
            if (_Cards != null)
            {
                for (int i = 0; i < _Cards.Length; i++)
                {
                    if (_Cards[i] == null) continue;
                    _Cards[i].ApplyNetworkVisibility();
                    _Cards[i].EnsurePoolRegistration();
                }
            }

            this.Log($"RebuildPoolFromScene: {ActiveCount}/{PoolSize} active");
        }

        // Re-broadcaste l'etat de toutes les cartes spawn par le joueur local
        // (utile pour forcer la synchro apres un rebuild ou un late join).
        public void SyncAllSpawnedCards()
        {
            if (CardPool == null) return;
            int _Count = 0;
            for (int i = 0; i < CardPool.Length; i++)
            {
                if (CardPool[i] == null || !CardPool[i].activeSelf) continue;
                MTG_PhysicCard _Card = CardPool[i].GetComponent<MTG_PhysicCard>();
                if (_Card == null) continue;
                if (Networking.LocalPlayer != null && Networking.IsOwner(Networking.LocalPlayer, CardPool[i]))
                {
                    _Card.RequestSerialization();
                    _Count++;
                }
            }
            this.Log($"SyncAllSpawnedCards: {_Count} carte(s) re-broadcastee(s)");
        }

        // === BROADCAST DES EVENEMENTS (spawn/despawn) ===

        // Ajoute un evenement au ring buffer synced
        private void QueueEvent(int _Op, int _Index, string _Key, Vector3 _Pos, Quaternion _Rot)
        {
            int _Cursor = _EventHead % EVENT_BUFFER;
            _EventOps[_Cursor] = _Op;
            _EventSlots[_Cursor] = _Index;
            _EventKeys[_Cursor] = _Key;
            _EventPositions[_Cursor] = _Pos;
            _EventRotations[_Cursor] = _Rot;
            _EventHead++;
        }

        // Prend l'ownership et envoie le buffer a tous les clients
        private void PublishEvents()
        {
            if (Networking.LocalPlayer == null) return;
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();
        }

        // Recu un snapshot reseau : on rejoue les evenements manquants
        public override void OnDeserialization()
        {
            // On ne rejoue pas nos propres evenements
            if (Networking.LocalPlayer != null && Networking.IsOwner(Networking.LocalPlayer, gameObject))
                return;

            if (_LastSeenHead < 0)
            {
                // Premier snapshot (late join) : on rejoue tout le buffer
                // pour reconstruire l'etat des dernieres cartes
                int _Replay = _EventHead > EVENT_BUFFER ? EVENT_BUFFER : _EventHead;
                for (int k = _EventHead - _Replay; k < _EventHead; k++)
                    ReplayEvent(k % EVENT_BUFFER);
                _LastSeenHead = _EventHead;
                return;
            }

            int _Missing = _EventHead - _LastSeenHead;
            if (_Missing <= 0) return;
            if (_Missing > EVENT_BUFFER) _Missing = EVENT_BUFFER;
            for (int k = _EventHead - _Missing; k < _EventHead; k++)
                ReplayEvent(k % EVENT_BUFFER);
            _LastSeenHead = _EventHead;
        }

        private void ReplayEvent(int _Cursor)
        {
            if (_EventOps[_Cursor] == 1)
                ApplySpawnEvent(_EventSlots[_Cursor], _EventKeys[_Cursor], _EventPositions[_Cursor], _EventRotations[_Cursor]);
            else if (_EventOps[_Cursor] == 2)
                ApplyDespawnEvent(_EventSlots[_Cursor]);
        }

        // Applique un spawn recu du reseau sur notre copie locale
        private void ApplySpawnEvent(int _Index, string _Key, Vector3 _Pos, Quaternion _Rot)
        {
            if (_Index < 0 || _Index >= PoolSize || string.IsNullOrEmpty(_Key)) return;
            if (!IsInitialized || CardPool == null)
            {
                InitializePool();
                if (CardPool == null) return;
            }

            GameObject _Obj = CardPool[_Index];
            if (_Obj == null)
            {
                if (PhysicCardPrefab == null) return;
                _Obj = Instantiate(PhysicCardPrefab);
                if (_Obj == null) return;
                CardPool[_Index] = _Obj;
                if (CardsParent != null)
                    _Obj.transform.SetParent(CardsParent, true);
            }

            MTG_PhysicCard _Card = _Obj.GetComponent<MTG_PhysicCard>();
            if (_Card != null)
            {
                _Card.PoolIndex = _Index;
                _Card.PoolManager = this;
                _Card.ApplyRemoteSpawn(_Key, _Pos, _Rot);
            }
        }

        // Applique un despawn recu du reseau sur notre copie locale
        private void ApplyDespawnEvent(int _Index)
        {
            if (_Index < 0 || _Index >= PoolSize || CardPool == null) return;
            GameObject _Obj = CardPool[_Index];
            if (_Obj == null) return;
            MTG_PhysicCard _Card = _Obj.GetComponent<MTG_PhysicCard>();
            if (_Card != null)
                _Card.ApplyRemoteDespawn();
            else
                _Obj.SetActive(false);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR

        [ContextMenu("Rebuild Pool In Editor")]
        private void RebuildPoolInEditor()
        {
            if (PhysicCardPrefab == null)
            {
                Debug.LogError("[MTG_PhysicCardPoolManager] PhysicCardPrefab is null! Assign a prefab first.");
                return;
            }

            Transform _Parent = CardsParent != null ? CardsParent : transform;
            int _PoolSize = PoolSize > 0 ? PoolSize : 50;

            Undo.RegisterFullObjectHierarchyUndo(_Parent.gameObject, "Rebuild Pool");

            for (int i = _Parent.childCount - 1; i >= 0; i--)
            {
                GameObject _Child = _Parent.GetChild(i).gameObject;
                if (_Child != _Parent.gameObject)
                {
                    Undo.DestroyObjectImmediate(_Child);
                }
            }

            PreInstantiatedCards = new GameObject[_PoolSize];
            for (int i = 0; i < _PoolSize; i++)
            {
                GameObject _Card = (GameObject)PrefabUtility.InstantiatePrefab(PhysicCardPrefab, _Parent);
                _Card.name = $"PhysicCard_{i}";
                _Card.SetActive(false);
                PreInstantiatedCards[i] = _Card;

                if (_PoolSize > 100 && i % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar("Rebuilding Pool", $"Creating card {i}/{_PoolSize}...", (float)i / _PoolSize);
                }
            }

            if (_PoolSize > 100)
                EditorUtility.ClearProgressBar();

            EditorUtility.SetDirty(this);
            Debug.Log($"[MTG_PhysicCardPoolManager] Pool rebuilt: {_PoolSize} cards created under '{_Parent.name}'");
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (PoolSize <= 0) PoolSize = 50;
        }
#endif
    }
}