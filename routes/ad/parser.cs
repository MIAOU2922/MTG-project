using System;

/// <summary>
/// Helper pour l'API /ad — recherche & chargement de decks depuis la BDD.
/// Génère des queries API conformes au format /ad?q=action:param1:param2...
/// Actions supportées : load, list, refresh.
/// </summary>
public class DeckListParser
{

    /// <summary>
    /// Génère une query pour l'état du re-scraping Moxfield
    /// Format API: /ad?q=refresh
    /// </summary>
    public static string RefreshStatus()
    {
        return "refresh";
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
    /// Génère une query pour lister/rechercher les decks
    /// Format API: /ad?q=list ou /ad?q=list:name:format:author:commander
    /// Sans filtre → liste les decks de l'utilisateur.
    /// Avec au moins un filtre → recherche publique + forward au scraper
    /// Moxfield (re-scraping en arrière-plan pour mettre la BDD à jour).
    /// </summary>
    public static string ListDecks(string searchName = "", string format = "", string author = "", string commander = "")
    {
        if (string.IsNullOrWhiteSpace(searchName) &&
            string.IsNullOrWhiteSpace(format) &&
            string.IsNullOrWhiteSpace(author) &&
            string.IsNullOrWhiteSpace(commander))
        {
            return "list";
        }

        return "list:" +
            (searchName != null ? Uri.EscapeDataString(searchName) : "") + ":" +
            (format != null ? Uri.EscapeDataString(format) : "") + ":" +
            (author != null ? Uri.EscapeDataString(author) : "") + ":" +
            (commander != null ? Uri.EscapeDataString(commander) : "");
    }

    // ==================== EXEMPLES D'UTILISATION ====================

    /*
    // Charger un deck sauvegardé (par UUID)
    string q1 = DeckListParser.LoadDeck("550e8400-e29b-41d4-a716-446655440000");
    // Résultat: load:550e8400-e29b-41d4-a716-446655440000

    // Lister mes decks
    string q2 = DeckListParser.ListDecks();
    // Résultat: list

    // Recherche publique (forward au scraper Moxfield en arrière-plan)
    string q3 = DeckListParser.ListDecks("winota", "commander", "", "Winota");
    // Résultat: list:winota:commander::Winota

    // État de la file de re-scraping Moxfield
    string q4 = DeckListParser.RefreshStatus();
    // Résultat: refresh
    */
}
