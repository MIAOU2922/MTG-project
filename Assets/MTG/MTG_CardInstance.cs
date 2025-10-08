using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MTG
{
    public class MTG_CardInstance : UdonSharpBehaviour
    {
        [Header("=== LIENS ===")]
        public MTG_Card cardData; // Les données de la carte (scriptable ou instance)
        public MTG_CardZone currentZone;
        public VRC_Pickup pickup;

        [Header("=== ÉTAT ===")]
        public bool isFaceDown = false;
        public bool isTapped = false;
        
        [Header("=== INTERACTION AVEC SLOTS ===")]
        public MTG_Slot currentSlotTrigger; // slot dans lequel la carte est actuellement

        public MTG_bord_Manager manager;

        private void Start()
        {
            if (!pickup) pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
        }

        public override void OnPickup()
        {
            if (currentZone != null)
            {
                currentZone.RemoveCard(this);
                currentZone = null;
            }
            
            // IMPORTANT : Effacer immédiatement la référence au slot lors du pickup
            // Cela empêche le snap automatique si la carte sort du trigger
            if (currentSlotTrigger != null)
            {
                if (currentSlotTrigger.currentCardInTrigger == this)
                {
                    currentSlotTrigger.currentCardInTrigger = null;
                }
                currentSlotTrigger = null;
            }
        }

        public override void OnDrop()
        {
            // Priorité 1: Si la carte est dans un slot trigger ET que le slot confirme sa présence, snap dessus
            if (currentSlotTrigger != null && currentSlotTrigger.currentCardInTrigger == this)
            {
                currentSlotTrigger.SnapCardToSlot(this);
                return;
            }
            
            // Si on arrive ici, soit pas de slot trigger, soit la carte n'est plus dans le trigger
            // Nettoyer la référence au cas où
            currentSlotTrigger = null;
            
            // La carte reste simplement là où elle a été lâchée, pas de snap automatique
            // Nettoyer la zone actuelle si elle en avait une
            if (currentZone != null)
            {
                currentZone.RemoveCard(this);
                currentZone = null;
            }
        }

        // Find the nearest zone using slot colliders (no radius fields used)
        private MTG_CardZone FindNearestZone()
        {
            MTG_CardZone[] zones = GetAllKnownZones();
            if (zones == null || zones.Length == 0) return null;

            float bestSqr = float.MaxValue;
            MTG_CardZone bestZone = null;
            Vector3 pos = transform.position;

            for (int zi = 0; zi < zones.Length; zi++)
            {
                var z = zones[zi];
                if (z == null || z.slots == null) continue;

                for (int si = 0; si < z.slots.Length; si++)
                {
                    var s = z.slots[si];
                    if (s == null) continue;
                    Collider col = s.GetComponent<Collider>();
                    if (col != null)
                    {
                        Vector3 closest = col.ClosestPoint(pos);
                        float dSqr = (pos - closest).sqrMagnitude;
                        if (dSqr < bestSqr)
                        {
                            bestSqr = dSqr;
                            bestZone = z;
                        }
                    }
                    else
                    {
                        float dSqr = (pos - s.position).sqrMagnitude;
                        if (dSqr < bestSqr)
                        {
                            bestSqr = dSqr;
                            bestZone = z;
                        }
                    }
                }
            }

            return bestZone;
        }

        private MTG_CardZone[] GetAllKnownZones()
        {
            if (manager == null) return new MTG_CardZone[0];
            MTG_CardZone[] b = manager.GetZones(ZoneCategory.Board);
            MTG_CardZone[] h = manager.GetZones(ZoneCategory.Hand);
            MTG_CardZone[] d = manager.GetZones(ZoneCategory.Deck);

            int total = (b != null ? b.Length : 0) + (h != null ? h.Length : 0) + (d != null ? d.Length : 0);
            MTG_CardZone[] all = new MTG_CardZone[total];
            int idx = 0;
            if (b != null) for (int i = 0; i < b.Length; i++) all[idx++] = b[i];
            if (h != null) for (int i = 0; i < h.Length; i++) all[idx++] = h[i];
            if (d != null) for (int i = 0; i < d.Length; i++) all[idx++] = d[i];
            return all;
        }

        // Example replacement for nearest-zone logic that used zoneCaptureRadius.
        // Replace existing GetNearestZone (or equivalent) with this collider-based version.

        private MTG_CardZone GetNearestZone(MTG_CardZone[] zones)
        {
            if (zones == null || zones.Length == 0) return null;
            MTG_CardZone best = null;
            float bestDistSqr = float.MaxValue;
            Vector3 pos = transform.position;

            for (int i = 0; i < zones.Length; i++)
            {
                var z = zones[i];
                if (z == null) continue;

                // If the point is inside the zone collider OR inside one of the zone's slots, consider it.
                bool insideZone = z.ContainsPoint(pos);
                var slotHit = z.GetSlotAtPoint(pos);

                if (insideZone || slotHit != null)
                {
                    float distSqr = (pos - z.transform.position).sqrMagnitude;
                    if (distSqr < bestDistSqr)
                    {
                        best = z;
                        bestDistSqr = distSqr;
                    }
                }
            }

            return best;
        }

        public void TapCard(bool tap)
        {
            isTapped = tap;
            var e = transform.localEulerAngles;
            e.z = tap ? 90f : 0f;
            transform.localEulerAngles = e;
        }

        public void FlipCard(bool faceDown)
        {
            isFaceDown = faceDown;
            var e = transform.localEulerAngles;
            e.x = faceDown ? 180f : 0f;
            transform.localEulerAngles = e;
        }

        // Exemple : remplacer tout appel FindObjectsOfType(typeof(MTG_CardZone)) par :
        public void Example_FindZones()
        {
            MTG_CardZone[] boardZones = (manager != null) ? manager.GetZones(ZoneCategory.Board) : new MTG_CardZone[0];
            MTG_CardZone[] handZones  = (manager != null) ? manager.GetZones(ZoneCategory.Hand)  : new MTG_CardZone[0];
            MTG_CardZone[] deckZones  = (manager != null) ? manager.GetZones(ZoneCategory.Deck)  : new MTG_CardZone[0];

            // utiliser boardZones/... au lieu de FindObjectsOfType
        }
    }
}
