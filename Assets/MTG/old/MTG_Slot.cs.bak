using UdonSharp;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MTG
{
    // Attacher ce script sur l'objet slot (avec un Collider — de préférence isTrigger).
    public class MTG_Slot : UdonSharpBehaviour
    {
        public MTG_CardZone parentZone;
        public int slotIndex = 0;
        
        [Header("=== ÉTAT DU SLOT ===")]
        public MTG_CardInstance currentCardInTrigger; // carte actuellement dans le trigger

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        void OnValidate()
        {
            // assurer qu'il y a un collider et (optionnel) le marquer isTrigger pour la détection "OnTriggerEnter"
            var col = GetComponent<Collider>();
            if (col == null)
            {
                // ne pas ajouter automatiquement un composant UdonSharp program ; juste avertir
                Debug.LogWarning($"MTG_Slot: Add a Collider to '{gameObject.name}' to enable slot collider detection.", this);
                return;
            }
            // si vous voulez forcer isTrigger en éditeur :
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                EditorUtility.SetDirty(gameObject);
            }
        }
#endif

        void OnTriggerEnter(Collider other)
        {
            if (parentZone == null) return;
            var card = other.GetComponent<MTG_CardInstance>();
            if (card == null) return;
            
            // Stocker la référence de la carte dans le trigger
            currentCardInTrigger = card;
            
            // Notifier la carte qu'elle est dans un slot
            card.currentSlotTrigger = this;
        }

        void OnTriggerExit(Collider other)
        {
            var card = other.GetComponent<MTG_CardInstance>();
            if (card == null) return;
            
            // Nettoyer les références quand la carte sort du trigger
            if (currentCardInTrigger == card)
            {
                currentCardInTrigger = null;
            }
            
            // IMPORTANT : Effacer la référence au slot quand la carte sort du trigger
            // Cela empêche le snap automatique si la carte est lâchée ailleurs
            if (card.currentSlotTrigger == this)
            {
                card.currentSlotTrigger = null;
            }
        }
        
        // Méthode pour faire snapper une carte sur ce slot
        public void SnapCardToSlot(MTG_CardInstance card)
        {
            if (parentZone == null || card == null) return;
            
            // Vérifier si le slot peut accepter la carte
            if (!parentZone.CanSlotAcceptCard(slotIndex))
            {
                Debug.LogWarning($"[MTG_Slot] Slot {slotIndex} is full, cannot snap card");
                return;
            }
            
            parentZone.AddCardAtSlot(slotIndex, card);
        }
        
        // Méthodes d'interaction pour les joueurs
        [ContextMenu("Toggle Top Card Tapped")]
        public void ToggleTopCardTapped()
        {
            if (parentZone == null) return;
            // Taper toutes les cartes du slot
            var stack = parentZone.cardsBySlot != null && slotIndex < parentZone.cardsBySlot.Length ? parentZone.cardsBySlot[slotIndex] : null;
            if (stack == null) return;
            for (int i = 0; i < stack.Length; i++)
            {
                if (stack[i] != null && parentZone.allowPlayerToggleCardState)
                {
                    stack[i].TapCard(!stack[i].isTapped);
                }
            }
        }
        
        [ContextMenu("Flip Top Card")]
        public void FlipTopCard()
        {
            if (parentZone != null)
            {
                var topCard = parentZone.GetTopCard(slotIndex);
                if (topCard != null && parentZone.allowPlayerToggleCardState)
                {
                    topCard.FlipCard(!topCard.isFaceDown);
                }
            }
        }
        
        // Méthode appelée par interaction VRC (exemple: clic droit)
        public override void Interact()
        {
            ToggleTopCardTapped();
        }
    }
}
