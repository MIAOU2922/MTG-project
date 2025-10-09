using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MTG
{
    // Classes de support pour les données complexes
    [System.Serializable]
    public struct MTG_Ruling
    {
        public string published_at;
        public string comment;

        public static MTG_Ruling Default()
        {
            MTG_Ruling r = new MTG_Ruling();
            r.published_at = "";
            r.comment = "";
            return r;
        }
    }
}