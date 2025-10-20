using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

public class DeckListParser
{
    /// <summary>
    /// Parse a deck list in various formats and return URL-encoded query string for the API
    /// Supports: Deckstats format, Moxfield format, Simple card names, Set (SET) Number format, and UUID format
    /// </summary>
    public static string ParseDeckList(string deckListInput, string deckName = "Deck")
    {
        if (string.IsNullOrWhiteSpace(deckListInput))
            return "";

        var lines = deckListInput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var cardLines = new List<string>();
        var detectedFormat = DetectFormat(lines);

        // Parse based on detected format
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//"))
                continue;

            cardLines.Add(trimmed);
        }

        // Build the deck list string
        var deckListBuilder = new StringBuilder();
        foreach (var cardLine in cardLines)
        {
            if (!string.IsNullOrWhiteSpace(cardLine))
            {
                deckListBuilder.AppendLine(cardLine);
            }
        }

        var deckListContent = deckListBuilder.ToString().TrimEnd();

        // Encode the deck list
        var encodedDeckList = HttpUtility.UrlEncode(deckListContent);
        var encodedDeckName = HttpUtility.UrlEncode(deckName);
        var detectedFormatStr = detectedFormat == "deckstats" ? "deckstats" : "moxfield";

        // Return the query string (without the base URL and /ad?q= prefix)
        return $"save:{encodedDeckName}:{detectedFormatStr}:{encodedDeckList}:en";
    }

    /// <summary>
    /// Detect the format of the deck list
    /// Formats:
    /// - Deckstats: "1 [SET#123] Card Name" or "//Main" sections
    /// - Moxfield: "1 Card Name (SET) 123" or "1 Card Name"
    /// - Database/UUID: "1 00011897-9c8b-482f-8d64-9f2cd8403b6a"
    /// </summary>
    private static string DetectFormat(string[] lines)
    {
        bool hasZoneSections = false;
        bool hasSquareBrackets = false;
        bool hasParentheses = false;
        bool hasUuids = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Check for zone sections (Deckstats indicator)
            if (trimmed.StartsWith("//") && (trimmed.Contains("Main") || trimmed.Contains("Sideboard") || 
                trimmed.Contains("Commander") || trimmed.Contains("Lands") || trimmed.Contains("Spells") || 
                trimmed.Contains("Creatures")))
            {
                hasZoneSections = true;
                continue;
            }

            // Skip pure comment lines
            if (trimmed.StartsWith("//"))
                continue;

            // Check for Deckstats format: "1 [SET#123] Card Name"
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\s+\[[A-Z0-9★*]+#\d+\]"))
            {
                hasSquareBrackets = true;
            }

            // Check for Set notation format: "1 Card Name (SET) 123"
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\s+.+\s+\([A-Z0-9]+\)\s+\d+"))
            {
                hasParentheses = true;
            }

            // Check for UUID format: "1 00011897-9c8b-482f-8d64-9f2cd8403b6a"
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\s+[0-9a-f\-]{36}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                hasUuids = true;
            }
        }

        // Determine format based on detections
        if (hasUuids)
            return "moxfield"; // UUID format is treated as moxfield

        if (hasSquareBrackets || hasZoneSections)
            return "deckstats";

        if (hasParentheses)
            return "moxfield";

        // Default to moxfield for simple formats
        return "moxfield";
    }

    /// <summary>
    /// Parse a deck list from UUIDs
    /// Input format: "1 00011897-9c8b-482f-8d64-9f2cd8403b6a"
    /// Returns URL-encoded query string
    /// </summary>
    public static string ParseDeckListFromUuids(string uuidListInput, string deckName = "Deck")
    {
        if (string.IsNullOrWhiteSpace(uuidListInput))
            return "";

        var lines = uuidListInput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var cardLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Validate UUID format
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\s+[0-9a-f\-]{36}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                cardLines.Add(trimmed);
            }
        }

        var deckListContent = string.Join("\n", cardLines);
        var encodedDeckList = HttpUtility.UrlEncode(deckListContent);
        var encodedDeckName = HttpUtility.UrlEncode(deckName);

        // Return the query string
        return $"save:{encodedDeckName}:moxfield:{encodedDeckList}:en";
    }

    // Example usage:
    /*
    string deckstatsFormat = @"//Main
1 [UNF#508] Captain Rex Nebula #!Commander
1 [SLD#806] Command Tower
20 [UNF#494] Mountain";

    string query = DeckListParser.ParseDeckList(deckstatsFormat, "My Captain Rex Deck");
    // Result: save:My%20Captain%20Rex%20Deck:deckstats:...

    string fullUrl = "https://mtg.hactazia.fr/ad?q=" + query;
    Console.WriteLine(fullUrl);
    */
}
