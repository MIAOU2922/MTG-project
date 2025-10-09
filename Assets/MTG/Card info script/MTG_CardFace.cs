using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MTG
{
    // Classes de support pour les données complexes
    [System.Serializable]
    public struct MTG_CardFace
    {
        public string name;
        public string mana_cost;
        public float cmc;
        public string type_line;
        public string oracle_text;
        public string power;
        public string toughness;
        public int loyalty;
        public string flavor_text;
        public string[] keywords;

        public static MTG_CardFace Default()
        {
            MTG_CardFace f = new MTG_CardFace();
            f.name = "";
            f.mana_cost = "";
            f.cmc = 0f;
            f.type_line = "";
            f.oracle_text = "";
            f.power = "";
            f.toughness = "";
            f.loyalty = 0;
            f.flavor_text = "";
            return f;
        }
    }
}