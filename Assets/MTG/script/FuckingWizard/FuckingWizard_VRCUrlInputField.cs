using UnityEngine;

namespace VRC.SDK3.Components
{
    public class FuckingWizard_VRCUrlInputField : VRCUrlInputField
    {
        // Champ public que UdonSharp peut setter (les champs sont toujours exposes)
        public string PendingUrl;

        void Update()
        {
            if (!string.IsNullOrEmpty(PendingUrl))
            {
                text = PendingUrl;   // text = ... fonctionne en interne
                PendingUrl = null;
            }
        }
    }
}