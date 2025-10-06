using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MTG
{
    // Classe pour les données de légalités
    [System.Serializable]
    public struct MTG_Legalities
    {
        public string standard;
        public string future;
        public string historic;
        public string timeless;
        public string gladiator;
        public string pioneer;
        public string modern;
        public string legacy;
        public string pauper;
        public string vintage;
        public string penny;
        public string commander;
        public string oathbreaker;
        public string standardbrawl;
        public string brawl;
        public string alchemy;
        public string paupercommander;
        public string duel;
        public string oldschool;
        public string premodern;
        public string predh;

        public static MTG_Legalities Default()
        {
            MTG_Legalities l = new MTG_Legalities();
            l.standard = "not_legal";
            l.future = "not_legal";
            l.historic = "not_legal";
            l.timeless = "not_legal";
            l.gladiator = "not_legal";
            l.pioneer = "not_legal";
            l.modern = "not_legal";
            l.legacy = "not_legal";
            l.pauper = "not_legal";
            l.vintage = "not_legal";
            l.penny = "not_legal";
            l.commander = "not_legal";
            l.oathbreaker = "not_legal";
            l.standardbrawl = "not_legal";
            l.brawl = "not_legal";
            l.alchemy = "not_legal";
            l.paupercommander = "not_legal";
            l.duel = "not_legal";
            l.oldschool = "not_legal";
            l.premodern = "not_legal";
            l.predh = "not_legal";
            return l;
        }
    }
}