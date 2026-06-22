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

        protected override void Start()
        {
            base.Start();
            if (!IsInitialized) InitializePool();
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
                    return ActivateCard(i, _CardKey, _Position, _Rotation);
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
                _Card.InitializeCard(_CardKey);
            }

            ActiveCount++;
            this.VerboseLog($"Spawned card '{_CardKey}' at index {_Index} ({ActiveCount}/{PoolSize})");
            return _Card;
        }

        // Despawn une carte et la retourne a la pool
        public void DespawnCard(MTG_PhysicCard _Card)
        {
            if (_Card == null) return;
            int _Index = _Card.PoolIndex;
            if (_Index < 0 || _Index >= PoolSize) return;

            _Card.gameObject.SetActive(false);
            _Card.PoolIndex = -1;
            _Card.PoolManager = null;
            ActiveCount--;
            this.VerboseLog($"Despawned card at index {_Index} ({ActiveCount}/{PoolSize})");
        }

        // Despawn toutes les cartes
        public void DespawnAll()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                if (CardPool[i] != null && CardPool[i].activeSelf)
                {
                    MTG_PhysicCard _Card = CardPool[i].GetComponent<MTG_PhysicCard>();
                    if (_Card != null)
                    {
                        _Card.PoolIndex = -1;
                        _Card.PoolManager = null;
                    }
                    CardPool[i].SetActive(false);
                }
            }
            ActiveCount = 0;
            this.Log("All cards despawned");
        }

        public int GetActiveCount() { return ActiveCount; }
        public int GetPoolCapacity() { return PoolSize; }

        public string GetPoolStatus()
        {
            return $"Pool: {ActiveCount}/{PoolSize} active";
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