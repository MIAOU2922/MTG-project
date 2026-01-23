using UdonSharp;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MTG
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MTG_CardZone : UdonSharpBehaviour
    {
        private const string LOG_PREFIX = "<color=#FF1493>[MTG_CardZone]</color> ";


        [Header("=== PARAMÈTRES DE ZONE ===")]
        public string zoneName = "Battlefield";
        public Transform[] slots; // positions de base
        
        [Header("=== GESTION DYNAMIQUE DES SLOTS ===")]
        public Transform slotParent; // Parent contenant les slots (optionnel)
        public bool useDynamicSlotDetection = false; // Si true, cherche automatiquement les slots dans slotParent

        [Header("Stacking")]
        public Vector3 stackOffset = new Vector3(0, 0.01f, 0);
        public int maxStackPerSlot = 10; // -1 = illimité
        
        [Header("=== ÉTAT DES CARTES DANS LA ZONE ===")]
        public bool forceCardsTapped = false; // Force les cartes à être tappées dans cette zone
        public bool forceCardsFaceDown = false; // Force les cartes à être face down dans cette zone
        public bool allowPlayerToggleCardState = true; // Permet aux joueurs de changer l'état des cartes

        public MTG_bord_Manager manager;
        public ZoneCategory category = ZoneCategory.Board;

        public bool allowFallbackToNearestIfNoneWithinSlotCollider = true;

        [SerializeField] [VRC.Udon.Serialization.OdinSerializer.OdinSerialize] /* UdonSharp auto-upgrade: serialization */  public MTG_CardInstance[][] cardsBySlot; // initialisé dans Start()

        void Start()
        {
            // Détection dynamique des slots si activée
            if (useDynamicSlotDetection && slotParent != null)
            {
                RefreshSlotsFromParent();
            }
            
            if (slots == null || slots.Length == 0)
            {
                cardsBySlot = new MTG_CardInstance[0][];
            }
            else
            {
                MTG_CardInstance[][] newStorage = new MTG_CardInstance[slots.Length][];
                for (int i = 0; i < slots.Length; i++)
                {
                    newStorage[i] = (cardsBySlot != null && i < cardsBySlot.Length && cardsBySlot[i] != null)
                        ? cardsBySlot[i]
                        : new MTG_CardInstance[0];
                }
                cardsBySlot = newStorage;
            }

            if (manager != null)
            {
                manager.RegisterZone(this, category);
            }
            else
            {
                Debug.LogWarning(LOG_PREFIX +" manager not assigned on " + gameObject.name + ". Assign MTG_bord_Manager in inspector.");
            }

            UpdateSlotReferences();
        }

        void OnDisable()
        {
            if (manager != null) manager.UnregisterZone(this, category);
        }

        [ContextMenu("Refresh Slot Indices")]
        public void UpdateSlotReferences()
        {
            if (slots == null) return;

            if (cardsBySlot == null || cardsBySlot.Length != slots.Length)
            {
                MTG_CardInstance[][] newStorage = new MTG_CardInstance[slots.Length][];
                for (int i = 0; i < slots.Length; i++)
                {
                    newStorage[i] = (cardsBySlot != null && i < cardsBySlot.Length && cardsBySlot[i] != null)
                        ? cardsBySlot[i]
                        : new MTG_CardInstance[0];
                }
                cardsBySlot = newStorage;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                var slotComp = s.GetComponent<MTG_Slot>();
                if (slotComp != null)
                {
                    slotComp.parentZone = this;
                    slotComp.slotIndex = i;
                }
                else
                {
#if UNITY_EDITOR
                    Debug.LogWarning(LOG_PREFIX +" Slot GameObject '{s.name}' (index {i}) does not have MTG_Slot component. Add MTG_Slot to enable trigger-based detection.", s);
#else
                    Debug.LogWarning(LOG_PREFIX +" Slot missing MTG_Slot component: " + s.name);
#endif
                }
            }
        }

        [ContextMenu("Refresh Slots from Parent")]
        public void RefreshSlotsFromParent()
        {
            if (slotParent == null)
            {
                Debug.LogWarning(LOG_PREFIX +" slotParent not assigned, cannot refresh slots dynamically.");
                return;
            }

            // Compter les enfants qui ont un composant MTG_Slot
            int slotCount = 0;
            for (int i = 0; i < slotParent.childCount; i++)
            {
                Transform child = slotParent.GetChild(i);
                if (child.GetComponent<MTG_Slot>() != null)
                {
                    slotCount++;
                }
            }

            // Créer le nouveau tableau de slots
            Transform[] newSlots = new Transform[slotCount];
            int slotIndex = 0;
            for (int i = 0; i < slotParent.childCount; i++)
            {
                Transform child = slotParent.GetChild(i);
                if (child.GetComponent<MTG_Slot>() != null)
                {
                    newSlots[slotIndex] = child;
                    slotIndex++;
                }
            }

            slots = newSlots;
            Debug.Log(LOG_PREFIX +" Refreshed {slots.Length} slots from parent '{slotParent.name}'");
        }
        
        [ContextMenu("Tap All Cards")]
        public void TapAllCards()
        {
            SetAllCardsTapped(true);
        }
        
        [ContextMenu("Untap All Cards")]
        public void UntapAllCards()
        {
            SetAllCardsTapped(false);
        }
        
        [ContextMenu("Flip All Cards Face Down")]
        public void FlipAllCardsFaceDown()
        {
            SetAllCardsFaceDown(true);
        }
        
        [ContextMenu("Flip All Cards Face Up")]
        public void FlipAllCardsFaceUp()
        {
            SetAllCardsFaceDown(false);
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        void OnValidate()
        {
            // Détection dynamique des slots si activée
            if (useDynamicSlotDetection && slotParent != null)
            {
                RefreshSlotsFromParent();
            }
            
            UpdateSlotReferences();
        }
#endif

        // PUBLIC API: default AddCard uses a slot index or position-based selection.
        public virtual void AddCard(MTG_CardInstance card)
        {
            int slotIndex = SelectSlotForCard(card, card.transform.position);
            if (slotIndex == -1) return;

            AddCardToSlotSafely(slotIndex, card);

            // compute world position/rotation for the top of the stack (no parenting)
            int stackIndex = cardsBySlot[slotIndex].Length - 1;
            var slotTransform = slots[slotIndex];
            Quaternion slotRot = slotTransform.rotation;
            Vector3 worldPos = slotTransform.position + slotRot * (stackOffset * stackIndex);

            card.transform.SetPositionAndRotation(worldPos, slotRot);
            card.currentZone = this;
            
            // Appliquer l'état forcé de la zone
            ApplyZoneCardState(card);
        }

        // used by MTG_Slot trigger when a card enters the collider
        public void AddCardAtSlot(int slotIndex, MTG_CardInstance card)
        {
            if (slotIndex < 0 || slotIndex >= cardsBySlot.Length) return;
            
            // Vérifier si la carte est déjà dans cette zone
            if (card.currentZone == this)
            {
                // Si la carte est déjà dans cette zone, juste la repositionner
                RemoveCard(card);
            }
            else if (card.currentZone != null)
            {
                // Retirer la carte de sa zone actuelle
                card.currentZone.RemoveCard(card);
            }
            
            AddCardToSlotSafely(slotIndex, card);

            int stackIndex = cardsBySlot[slotIndex].Length - 1;
            var slotTransform = slots[slotIndex];
            Quaternion slotRot = slotTransform.rotation;
            Vector3 worldPos = slotTransform.position + slotRot * (stackOffset * stackIndex);

            card.transform.SetPositionAndRotation(worldPos, slotRot);
            card.currentZone = this;
            
            // Appliquer l'état forcé de la zone
            ApplyZoneCardState(card);
        }

        // ensure no duplicates
        public void AddCardToSlotSafely(int slotIndex, MTG_CardInstance card)
        {
            if (slotIndex < 0 || slotIndex >= cardsBySlot.Length) return;
            var stack = cardsBySlot[slotIndex];
            for (int i = 0; i < stack.Length; i++) if (stack[i] == card) return;
            AddCardToSlot(slotIndex, card);
        }

        // PUBLIC RemoveCard: visible to other scripts (MTG_CardInstance calls this)
        public virtual void RemoveCard(MTG_CardInstance card)
        {
            if (cardsBySlot == null) return;
            for (int i = 0; i < cardsBySlot.Length; i++)
            {
                MTG_CardInstance[] stack = cardsBySlot[i];
                int foundIdx = -1;
                for (int j = 0; j < stack.Length; j++)
                {
                    if (stack[j] == card) { foundIdx = j; break; }
                }

                if (foundIdx >= 0)
                {
                    RemoveCardFromSlot(i, card);
                    RepositionStack(i);
                    // clear zone reference on card
                    if (card != null) card.currentZone = null;
                    return;
                }
            }
        }

        public void RepositionStack(int slotIndex)
        {
            var stack = cardsBySlot[slotIndex];
            var slotTransform = slots[slotIndex];
            Quaternion slotRot = slotTransform.rotation;
            Vector3 slotPos = slotTransform.position;

            for (int i = 0; i < stack.Length; i++)
            {
                if (stack[i] == null) continue;
                Vector3 worldPos = slotPos + slotRot * (stackOffset * i);
                stack[i].transform.SetPositionAndRotation(worldPos, slotRot);
            }
        }

        public int FindSlotWithSpace()
        {
            for (int i = 0; i < cardsBySlot.Length; i++)
            {
                // Si maxStackPerSlot est -1, pas de limite
                if (maxStackPerSlot == -1 || cardsBySlot[i].Length < maxStackPerSlot)
                    return i;
            }
            return -1;
        }

        public MTG_CardInstance GetTopCard(int slotIndex)
        {
            var stack = cardsBySlot[slotIndex];
            if (stack == null || stack.Length == 0) return null;
            return stack[stack.Length - 1];
        }
        
        // Vérifie si un slot peut accepter une nouvelle carte
        public bool CanSlotAcceptCard(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= cardsBySlot.Length) return false;
            
            // Si maxStackPerSlot est -1, pas de limite
            if (maxStackPerSlot == -1) return true;
            
            return cardsBySlot[slotIndex].Length < maxStackPerSlot;
        }

        // Applique l'état forcé de la zone à une carte
        public void ApplyZoneCardState(MTG_CardInstance card)
        {
            if (card == null) return;
            
            if (forceCardsTapped)
            {
                card.TapCard(true);
            }
            
            if (forceCardsFaceDown)
            {
                card.FlipCard(true);
            }
        }
        
        // Méthodes publiques pour modifier l'état des cartes dans la zone
        public void SetAllCardsTapped(bool tapped)
        {
            if (!allowPlayerToggleCardState) return;
            
            for (int i = 0; i < cardsBySlot.Length; i++)
            {
                for (int j = 0; j < cardsBySlot[i].Length; j++)
                {
                    if (cardsBySlot[i][j] != null)
                    {
                        cardsBySlot[i][j].TapCard(tapped);
                    }
                }
            }
        }
        
        public void SetAllCardsFaceDown(bool faceDown)
        {
            if (!allowPlayerToggleCardState) return;
            
            for (int i = 0; i < cardsBySlot.Length; i++)
            {
                for (int j = 0; j < cardsBySlot[i].Length; j++)
                {
                    if (cardsBySlot[i][j] != null)
                    {
                        cardsBySlot[i][j].FlipCard(faceDown);
                    }
                }
            }
        }
        
        // Méthode pour modifier l'état d'une carte spécifique dans un slot
        public void SetCardStateAtSlot(int slotIndex, int cardIndex, bool tapped, bool faceDown)
        {
            if (!allowPlayerToggleCardState) return;
            if (slotIndex < 0 || slotIndex >= cardsBySlot.Length) return;
            if (cardIndex < 0 || cardIndex >= cardsBySlot[slotIndex].Length) return;
            
            var card = cardsBySlot[slotIndex][cardIndex];
            if (card != null)
            {
                card.TapCard(tapped);
                card.FlipCard(faceDown);
            }
        }
        
        // Méthode pour toggler l'état de la carte du dessus d'un slot
        public void ToggleTopCardStateAtSlot(int slotIndex)
        {
            if (!allowPlayerToggleCardState) return;
            var topCard = GetTopCard(slotIndex);
            if (topCard != null)
            {
                topCard.TapCard(!topCard.isTapped);
            }
        }

        public void AddCardToSlot(int slotIndex, MTG_CardInstance card)
        {
            MTG_CardInstance[] old = cardsBySlot[slotIndex];
            MTG_CardInstance[] next = new MTG_CardInstance[old.Length + 1];
            for (int i = 0; i < old.Length; i++) next[i] = old[i];
            next[old.Length] = card;
            cardsBySlot[slotIndex] = next;
        }

        public void RemoveCardFromSlot(int slotIndex, MTG_CardInstance card)
        {
            MTG_CardInstance[] old = cardsBySlot[slotIndex];
            int idx = -1;
            for (int i = 0; i < old.Length; i++) if (old[i] == card) { idx = i; break; }
            if (idx < 0) return;
            MTG_CardInstance[] next = new MTG_CardInstance[old.Length - 1];
            for (int i = 0, j = 0; i < old.Length; i++)
            {
                if (i == idx) continue;
                next[j++] = old[i];
            }
            cardsBySlot[slotIndex] = next;
        }

        // --- Slot selection logic used by all zones ---
        public virtual int SelectSlotForCard(MTG_CardInstance card, Vector3 dropWorldPosition)
        {
            if (slots == null || slots.Length == 0) return -1;

            int best = -1;
            float bestSqr = float.MaxValue;

            // 1) Priorité absolue : si la carte a une référence à un slot de cette zone
            if (card.currentSlotTrigger != null && card.currentSlotTrigger.parentZone == this)
            {
                int slotIdx = card.currentSlotTrigger.slotIndex;
                if (slotIdx >= 0 && slotIdx < cardsBySlot.Length)
                {
                    int stackCount = cardsBySlot[slotIdx].Length;
                    // Si maxStackPerSlot est -1, pas de limite
                    if (maxStackPerSlot == -1 || stackCount < maxStackPerSlot)
                    {
                        return slotIdx;
                    }
                }
            }

            // 2) Prefer slots with collider that contain the drop point
            for (int i = 0; i < slots.Length; i++)
            {
                int stackCount = (cardsBySlot != null && i < cardsBySlot.Length) ? cardsBySlot[i].Length : 0;
                // Si maxStackPerSlot est -1, pas de limite
                if (maxStackPerSlot != -1 && stackCount >= maxStackPerSlot) continue;
                var s = slots[i];
                if (s == null) continue;

                Collider col = s.GetComponent<Collider>();
                if (col != null)
                {
                    Vector3 closest = col.ClosestPoint(dropWorldPosition);
                    if ((closest - dropWorldPosition).sqrMagnitude <= 0.0001f)
                    {
                        float dSqr = (dropWorldPosition - s.position).sqrMagnitude;
                        if (dSqr < bestSqr)
                        {
                            bestSqr = dSqr;
                            best = i;
                        }
                    }
                }
            }

            if (best != -1) return best;

            // 3) fallback to nearest slot (ignoring collider containment)
            if (allowFallbackToNearestIfNoneWithinSlotCollider)
            {
                best = -1;
                bestSqr = float.MaxValue;
                for (int i = 0; i < slots.Length; i++)
                {
                    int stackCount = (cardsBySlot != null && i < cardsBySlot.Length) ? cardsBySlot[i].Length : 0;
                    // Si maxStackPerSlot est -1, pas de limite
                    if (maxStackPerSlot != -1 && stackCount >= maxStackPerSlot) continue;
                    var s = slots[i];
                    if (s == null) continue;

                    Collider col = s.GetComponent<Collider>();
                    float dSqr;
                    if (col != null)
                    {
                        Vector3 closest = col.ClosestPoint(dropWorldPosition);
                        dSqr = (dropWorldPosition - closest).sqrMagnitude;
                    }
                    else
                    {
                        dSqr = (dropWorldPosition - s.position).sqrMagnitude;
                    }

                    if (dSqr < bestSqr)
                    {
                        bestSqr = dSqr;
                        best = i;
                    }
                }
                return best;
            }

            return -1;
        }

        // Gizmos: draw slot collider bounds (preferred) or fallback sphere
        void OnDrawGizmos()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null) continue;

                Collider col = s.GetComponent<Collider>();
                if (col != null)
                {
                    Gizmos.color = new Color(0f, 1f, 0f, 0.12f);
                    Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
                }
                else
                {
                    Gizmos.color = new Color(0f, 1f, 0f, 0.12f);
                    Gizmos.DrawWireSphere(s.position, 0.25f);
                }

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(s.position, Vector3.one * 0.05f);
                Gizmos.color = new Color(1f, 0.8f, 0f, 1f);
                Gizmos.DrawLine(transform.position, s.position);
            }
        }

        // Retourne true si worldPoint est dans un collider de l'un des slots ou dans le collider de la zone.
        public bool ContainsPoint(Vector3 worldPoint)
        {
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var s = slots[i];
                    if (s == null) continue;
                    var col = s.GetComponent<Collider>();
                    if (col == null) continue;
                    Vector3 closest = col.ClosestPoint(worldPoint);
                    if ((closest - worldPoint).sqrMagnitude <= 0.0001f) return true;
                }
            }

            var zoneCol = GetComponent<Collider>();
            if (zoneCol != null)
            {
                Vector3 closestZone = zoneCol.ClosestPoint(worldPoint);
                if ((closestZone - worldPoint).sqrMagnitude <= 0.0001f) return true;
            }

            return false;
        }

        // Retourne le Transform du slot contenant worldPoint (ou null si aucun)
        public Transform GetSlotAtPoint(Vector3 worldPoint)
        {
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var s = slots[i];
                    if (s == null) continue;
                    var col = s.GetComponent<Collider>();
                    if (col == null) continue;
                    Vector3 closest = col.ClosestPoint(worldPoint);
                    if ((closest - worldPoint).sqrMagnitude <= 0.0001f) return s;
                }
            }
            return null;
        }
    }
}
