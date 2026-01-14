using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

/// <summary>
/// Parser pour decks MTG - Génère des queries API conformes au format /ad?q=action:param1:param2...
/// Compatible avec: Deckstats, Moxfield, Simple format, UUID format
/// </summary>
public class DeckListParser
{
    /// <summary>
    /// Parse un deck list et retourne la query complète prête à être envoyée
    /// Format API attendu: /ad?q=save:DeckName:format:deck_list_encoded:lang:description
    /// </summary>
    /// <param name="deckListInput">Contenu du deck (texte brut avec zones optionnelles)</param>
    /// <param name="deckName">Nom du deck</param>
    /// <param name="description">Description optionnelle du deck</param>
    /// <param name="lang">Langue des cartes (défaut: en)</param>
    /// <returns>Query string complète: save:Name:format:content:lang:desc</returns>
    public static string ParseDeckList(string deckListInput, string deckName = "My Deck", string description = "", string lang = "en")
    {
        if (string.IsNullOrWhiteSpace(deckListInput))
            return "";

        var lines = deckListInput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var detectedFormat = DetectFormat(lines);

        // Conserver TOUT le contenu original (y compris les zones //Main, //Sideboard, etc.)
        // L'API va parser les zones elle-même
        var deckListContent = deckListInput.Trim();

        // Encoder chaque partie séparément
        var encodedDeckName = Uri.EscapeDataString(deckName);
        var encodedDeckList = Uri.EscapeDataString(deckListContent);
        var encodedDescription = string.IsNullOrWhiteSpace(description) ? "" : Uri.EscapeDataString(description);

        // Format: save:DeckName:format:deck_list_encoded:lang:description
        var query = $"save:{encodedDeckName}:{detectedFormat}:{encodedDeckList}:{lang}";
        
        if (!string.IsNullOrWhiteSpace(encodedDescription))
        {
            query += $":{encodedDescription}";
        }

        return query;
    }

    /// <summary>
    /// Parse pour action PARSE (validation sans sauvegarde)
    /// Format API: /ad?q=parse:deck_list_encoded:format:lang
    /// </summary>
    public static string ParseDeckListForValidation(string deckListInput, string lang = "en")
    {
        if (string.IsNullOrWhiteSpace(deckListInput))
            return "";

        var lines = deckListInput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var detectedFormat = DetectFormat(lines);

        var deckListContent = deckListInput.Trim();
        var encodedDeckList = Uri.EscapeDataString(deckListContent);

        // Format: parse:deck_list_encoded:format:lang
        return $"parse:{encodedDeckList}:{detectedFormat}:{lang}";
    }

    /// <summary>
    /// Détecte le format du deck list
    /// Formats supportés:
    /// - deckstats: "1 [SET#123] Card Name" avec zones //Main, //Sideboard, #!Commander
    /// - moxfield: "1 Card Name (SET) 123" 
    /// - auto: Simple "1 Card Name" ou UUIDs "1 uuid-here"
    /// </summary>
    private static string DetectFormat(string[] lines)
    {
        bool hasZoneSections = false;
        bool hasSquareBrackets = false;
        bool hasParentheses = false;
        bool hasUuids = false;
        bool hasCommanderMarker = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Détection zones Deckstats: //Main, //Sideboard, //Commander, etc.
            if (trimmed.StartsWith("//") && (
                trimmed.Equals("//Main", StringComparison.OrdinalIgnoreCase) || 
                trimmed.Equals("//Sideboard", StringComparison.OrdinalIgnoreCase) || 
                trimmed.Equals("//Commander", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("//Companion", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("//Oathbreaker", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("//Wishboard", StringComparison.OrdinalIgnoreCase)))
            {
                hasZoneSections = true;
                continue;
            }

            // Skip autres commentaires
            if (trimmed.StartsWith("//"))
                continue;

            // Détection marqueur #!Commander (Deckstats)
            if (trimmed.Contains("#!Commander"))
            {
                hasCommanderMarker = true;
            }

            // Détection format Deckstats: "1 [SET#123] Card Name"
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\s+\[[A-Z0-9]+#\d+\]\s+"))
            {
                hasSquareBrackets = true;
            }

            // Détection format Moxfield: "1 Card Name (SET) 123"
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\s+.+\s+\([A-Z0-9]+\)\s+\d+"))
            {
                hasParentheses = true;
            }

            // Détection format UUID: "1 00011897-9c8b-482f-8d64-9f2cd8403b6a"
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\s+[0-9a-f\-]{36}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                hasUuids = true;
            }
        }

        // Priorité de détection
        if (hasZoneSections || hasSquareBrackets || hasCommanderMarker)
            return "deckstats";

        if (hasUuids)
            return "auto"; // UUID format géré comme auto

        if (hasParentheses)
            return "moxfield";

        // Par défaut: auto-détection serveur
        return "auto";
    }

    /// <summary>
    /// Génère une query pour charger un deck existant (sauvegardé en base)
    /// Format API: /ad?q=load:deck_id
    /// </summary>
    public static string LoadDeck(string deckId)
    {
        if (string.IsNullOrWhiteSpace(deckId))
            return "";

        return $"load:{deckId}";
    }

    /// <summary>
    /// Génère une query pour charger un deck temporaire (non sauvegardé)
    /// Format API: /ad?q=load:format:deck_list_encoded:lang
    /// Le deck est parsé et chargé dans l'instance sans être sauvegardé en base
    /// </summary>
    /// <param name="deckListInput">Contenu du deck (texte brut avec zones optionnelles)</param>
    /// <param name="lang">Langue des cartes (défaut: en)</param>
    /// <returns>Query string complète: load:format:content:lang</returns>
    public static string LoadDeckTemporary(string deckListInput, string lang = "en")
    {
        if (string.IsNullOrWhiteSpace(deckListInput))
            return "";

        var lines = deckListInput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var detectedFormat = DetectFormat(lines);

        var deckListContent = deckListInput.Trim();
        var encodedDeckList = Uri.EscapeDataString(deckListContent);

        // Format: load:format:deck_list_encoded:lang
        return $"load:{detectedFormat}:{encodedDeckList}:{lang}";
    }

    /// <summary>
    /// Génère une query pour supprimer un deck
    /// Format API: /ad?q=delete:deck_id
    /// </summary>
    public static string DeleteDeck(string deckId)
    {
        if (string.IsNullOrWhiteSpace(deckId))
            return "";

        return $"delete:{deckId}";
    }

    /// <summary>
    /// Génère une query pour lister les decks
    /// Format API: /ad?q=list ou /ad?q=list:search_name
    /// </summary>
    public static string ListDecks(string searchName = "")
    {
        if (string.IsNullOrWhiteSpace(searchName))
            return "list";

        var encodedSearch = Uri.EscapeDataString(searchName);
        return $"list:{encodedSearch}";
    }

    // ==================== EXEMPLES D'UTILISATION ====================

    /*
    
    // EXEMPLE 1: Parser un deck Deckstats avec zones
    string deckstatsInput = @"//Main
1 [UNF#508] Captain Rex Nebula #!Commander
1 [SLD#806] Command Tower
20 [UNF#494] Mountain

//Sideboard
1 [M21#123] Lightning Bolt
2 [M21#150] Island";

    string query1 = DeckListParser.ParseDeckList(
        deckstatsInput, 
        "Captain Rex Commander", 
        "My first commander deck", 
        "en"
    );
    // Résultat: save:Captain%20Rex%20Commander:deckstats:...contenu encodé...:en:My%20first%20commander%20deck
    
    string fullUrl1 = "https://mtg.hactazia.fr/ad?q=" + query1;
    // URL complète prête à envoyer


    // EXEMPLE 2: Parser un deck Moxfield simple
    string moxfieldInput = @"1 Lightning Bolt (M21) 123
4 Island (M21) 270
1 Counterspell (M21) 55";

    string query2 = DeckListParser.ParseDeckList(
        moxfieldInput,
        "Blue Control"
    );
    // Résultat: save:Blue%20Control:moxfield:...contenu encodé...:en


    // EXEMPLE 3: Validation sans sauvegarde (action parse)
    string testDeck = @"4 Lightning Bolt
20 Mountain
16 Forest";

    string query3 = DeckListParser.ParseDeckListForValidation(testDeck, "fr");
    // Résultat: parse:...contenu encodé...:auto:fr
    
    string fullUrl3 = "https://mtg.hactazia.fr/ad?q=" + query3;
    // Teste la validité du deck sans le sauvegarder


    // EXEMPLE 4: Charger un deck existant (sauvegardé)
    string query4 = DeckListParser.LoadDeck("550e8400-e29b-41d4-a716-446655440000");
    // Résultat: load:550e8400-e29b-41d4-a716-446655440000


    // EXEMPLE 4b: Charger un deck temporaire (non sauvegardé)
    string tempDeckInput = @"4 Lightning Bolt
20 Mountain
16 Forest";

    string query4b = DeckListParser.LoadDeckTemporary(tempDeckInput, "en");
    // Résultat: load:auto:4%20Lightning%20Bolt%0A20%20Mountain%0A16%20Forest:en
    
    string fullUrl4b = "https://mtg.hactazia.fr/ad?q=" + query4b;
    // Charge le deck directement sans le sauvegarder en base
    // Utile pour tester rapidement un deck ou partager une liste


    // EXEMPLE 4c: Charger un deck temporaire avec zones Deckstats
    string tempDeckWithZones = @"//Main
4 Lightning Bolt
20 Mountain

//Sideboard
2 Negate
1 Counterspell";

    string query4c = DeckListParser.LoadDeckTemporary(tempDeckWithZones, "en");
    // Résultat: load:deckstats:...contenu encodé avec zones...:en
    // Les zones seront parsées automatiquement par le serveur


    // EXEMPLE 5: Supprimer un deck
    string query5 = DeckListParser.DeleteDeck("550e8400-e29b-41d4-a716-446655440000");
    // Résultat: delete:550e8400-e29b-41d4-a716-446655440000


    // EXEMPLE 6: Lister les decks
    string query6a = DeckListParser.ListDecks(); // Tous mes decks
    // Résultat: list

    string query6b = DeckListParser.ListDecks("Commander"); // Recherche publique
    // Résultat: list:Commander


    // EXEMPLE 7: Format UUID
    string uuidInput = @"1 0000579f-7b35-4ed3-b44c-db2a538066fe
4 00006596-1166-4a79-8443-ca9f82e6db4e
2 0000a54c-a511-4925-92dc-01b937f9afad";

    string query7 = DeckListParser.ParseDeckList(uuidInput, "UUID Deck");
    // Résultat: save:UUID%20Deck:auto:...uuids encodés...:en


    // EXEMPLE 8: Deck complet avec toutes les zones
    string fullDeckInput = @"//Main
1 [MH3#123] Ulamog, the Ceaseless Hunger #!Commander
35 [MH3#999] Wastes
10 [M21#250] Sol Ring

//Sideboard
15 [M21#300] Mountain

//Companion
1 [IKO#250] Lurrus of the Dream-Den";

    string query8 = DeckListParser.ParseDeckList(
        fullDeckInput,
        "Eldrazi Commander",
        "Colorless commander deck with Ulamog",
        "en"
    );
    // Le serveur parsera les zones //Main, //Sideboard, //Companion automatiquement
    

    // UTILISATION DANS UNITY C#:
    using UnityEngine;
    using UnityEngine.Networking;
    using System.Collections;

    public class DeckUploader : MonoBehaviour
    {
        private const string API_BASE = "https://mtg.hactazia.fr/ad?q=";

        // Sauvegarder un deck de manière permanente
        public IEnumerator UploadDeck(string deckContent, string deckName)
        {
            // Générer la query
            string query = DeckListParser.ParseDeckList(deckContent, deckName, "", "en");
            string fullUrl = API_BASE + query;

            // Envoyer la requête
            using (UnityWebRequest request = UnityWebRequest.Get(fullUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Deck créé: " + request.downloadHandler.text);
                    // Parser la réponse JSON pour obtenir deck_id
                }
                else
                {
                    Debug.LogError("Erreur: " + request.error);
                }
            }
        }

        // Charger un deck temporaire (sans sauvegarder)
        public IEnumerator LoadTemporaryDeck(string deckContent)
        {
            // Générer la query pour load temporaire
            string query = DeckListParser.LoadDeckTemporary(deckContent, "en");
            string fullUrl = API_BASE + query;

            using (UnityWebRequest request = UnityWebRequest.Get(fullUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Deck temporaire chargé: " + request.downloadHandler.text);
                    // Les cartes sont ajoutées à l'instance sans sauvegarder le deck
                    // Response contient: deck_type: "temporary"
                }
                else
                {
                    Debug.LogError("Erreur: " + request.error);
                }
            }
        }

        // Charger un deck sauvegardé existant
        public IEnumerator LoadSavedDeck(string deckId)
        {
            string query = DeckListParser.LoadDeck(deckId);
            string fullUrl = API_BASE + query;

            using (UnityWebRequest request = UnityWebRequest.Get(fullUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Deck sauvegardé chargé: " + request.downloadHandler.text);
                    // Response contient: deck_type: "saved", deck_id, deck_name, etc.
                }
                else
                {
                    Debug.LogError("Erreur: " + request.error);
                }
            }
        }
    }

    */
}
