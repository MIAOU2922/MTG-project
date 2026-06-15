using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MTG
{
    public class MTG_CardReturnZone : MTG_Base
    {
        [Header("=== RETURN ZONE ===")]
        [Tooltip("Transform de reset (position/rotation). Si vide, garde la position actuelle.")]
        public Transform ResetTransform;

        private void OnTriggerEnter(Collider _Other)
        {
            if (_Other == null) return;

            MTG_PhysicCard _Card = _Other.GetComponent<MTG_PhysicCard>();
            if (_Card == null) return;

            this.VerboseLog($"Card returned to zone: {_Card.CardKey}");

            // Reset position si un transform de reset est defini
            if (ResetTransform != null)
            {
                _Card.transform.position = ResetTransform.position;
                _Card.transform.rotation = ResetTransform.rotation;
            }

            // Retour a la pool (desactive, reset CardKey, reset images)
            _Card.ReturnToPool();
        }
    }
}
