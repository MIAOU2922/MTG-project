using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;
using System;

namespace MTG
{
    // Classe principale pour les données de carte
    [System.Serializable]
    public class MTG_Card : UdonSharpBehaviour
    {

        [Header("=== IDENTIFICATION ===")]
        [SerializeField] public string id;
        [SerializeField] public string oracle_id;

        [Header("=== CARTE BASIQUE ===")]
        [SerializeField] public string name;
        [SerializeField] public string lang;
        [SerializeField] public string layout;
        [SerializeField] public string mana_cost;
        [SerializeField] public float cmc;
        [SerializeField] public string oracle_text;
        [SerializeField] public string flavor_text;

        [Header("=== CRÉATURE ===")]
        [SerializeField] public string power;
        [SerializeField] public string toughness;

        [Header("=== PLANESWALKER ===")]
        [SerializeField] public int loyalty; // Pour les planeswalkers

        [Header("=== COULEURS ===")]
        [SerializeField] public string[] colors;
        [SerializeField] public string[] color_identity;
        [SerializeField] public string[] keywords;

        [Header("=== CARTES DOUBLE-FACE ===")]
        [SerializeField] public MTG_CardFace[] card_faces;

        [Header("=== RULINGS ===")]
        [SerializeField] public MTG_Ruling[] rulings;

        [Header("=== SET INFO ===")]
        [SerializeField] public string set_code;
        [SerializeField] public string set_name;
        [SerializeField] public string set_type;
        [SerializeField] public string collector_number;
        [SerializeField] public string rarity;

        [Header("=== LÉGALITÉS ===")]
        [SerializeField] public MTG_Legalities legalities;

        // --- Constructeur ---
        public void InitializeFromJSON(string jsonData)
        {
            var cardData = jsonData;

            // Identification
            id = GetStringValue(cardData, "id");
            oracle_id = GetStringValue(cardData, "oracle_id");

            // Carte basique
            name = GetStringValue(cardData, "name");
            lang = GetStringValue(cardData, "lang");
            layout = GetStringValue(cardData, "layout");
            mana_cost = GetStringValue(cardData, "mana_cost");
            cmc = GetFloatValue(cardData, "cmc");
            oracle_text = GetStringValue(cardData, "oracle_text");
            flavor_text = GetStringValue(cardData, "flavor_text");

            // Créature ou planeswalker
            power = GetStringValue(cardData, "power");
            toughness = GetStringValue(cardData, "toughness");
            loyalty = GetIntValue(cardData, "loyalty");

            // Couleurs
            colors = GetStringArrayValue(cardData, "colors");
            color_identity = GetStringArrayValue(cardData, "color_identity");
            keywords = GetStringArrayValue(cardData, "keywords");

            // Cartes double-face
            var facesData = GetArrayValue(cardData, "card_faces");
            if (facesData != null && facesData.Length > 0)
            {
                card_faces = new MTG_CardFace[facesData.Length];
                for (int i = 0; i < facesData.Length; i++)
                {
                    ApplyFaceData(ref card_faces[i], facesData[i]);
                }
            }

            // Rulings
            var rulingsData = GetArrayValue(cardData, "rulings");
            if (rulingsData != null && rulingsData.Length > 0)
            {
                rulings = new MTG_Ruling[rulingsData.Length];
                for (int i = 0; i < rulingsData.Length; i++)
                {
                    ApplyRulingData(ref rulings[i], rulingsData[i]);
                }
            }

            // Set info
            set_code = GetStringValue(cardData, "set");
            set_name = GetStringValue(cardData, "set_name");
            set_type = GetStringValue(cardData, "set_type");
            collector_number = GetStringValue(cardData, "collector_number");
            rarity = GetStringValue(cardData, "rarity");

            // Légalités
            var legalitiesData = GetObjectValue(cardData, "legalities");
            if (legalitiesData != null)
            {
                ApplyLegalitiesData(ref legalities, legalitiesData);
            }
        }

        // --- Méthodes d'aide pour extraire les valeurs ---
        private string GetStringValue(string json, string key)
        {
            // Implémentation pour extraire une valeur string du JSON
            return ""; // Remplacer par l'implémentation réelle
        }
        private float GetFloatValue(string json, string key)
        {
            // Implémentation pour extraire une valeur float du JSON
            return 0f; // Remplacer par l'implémentation réelle
        }
        private int GetIntValue(string json, string key)
        {
            // Implémentation pour extraire une valeur int du JSON
            return 0; // Remplacer par l'implémentation réelle
        }
        private string[] GetStringArrayValue(string json, string key)
        {
            // Implémentation pour extraire un tableau de strings du JSON
            return new string[0]; // Remplacer par l'implémentation réelle
        }
        private object[] GetArrayValue(string json, string key)
        {
            // Implémentation pour extraire un tableau d'objets du JSON
            return null; // Remplacer par l'implémentation réelle
        }
        private object GetObjectValue(string json, string key)
        {
            // Implémentation pour extraire un objet du JSON
            return null; // Remplacer par l'implémentation réelle
        }

        private void ApplyFaceData(ref MTG_CardFace face, object data)
        {
            // Implémentation pour appliquer les données d'une face de carte
            // Remplacer par l'implémentation réelle
        }
        private void ApplyRulingData(ref MTG_Ruling ruling, object data)
        {
            // Implémentation pour appliquer les données d'un ruling
            // Remplacer par l'implémentation réelle
        }
        private void ApplyLegalitiesData(ref MTG_Legalities leg, object data)
        {
            // Implémentation pour appliquer les données de légalités
            // Remplacer par l'implémentation réelle
        }

    }
}