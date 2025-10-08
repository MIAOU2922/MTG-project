using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public class MTG_SearchCard : UdonSharpBehaviour
{
    public TextMeshProUGUI nameText;
    public int cardID;
    internal void SetData(DataDictionary cardDict)
    {
        if (cardDict == null) return;
        // Prioriser le nom traduit (printed_name) si disponible
        string cardName;
        if (cardDict.ContainsKey("printed_name") && cardDict["printed_name"].TokenType == TokenType.String && !string.IsNullOrEmpty(cardDict["printed_name"].String))
        {
            cardName = cardDict["printed_name"].String;
        }
        else
        {
            cardName = cardDict["name"].String;
        }
        nameText.text = cardName;
        // Le nouveau format n'a pas d'id, utiliser collector_number à la place
        if (cardDict.ContainsKey("collector_number") && cardDict["collector_number"].TokenType == TokenType.String)
        {
            if (!int.TryParse(cardDict["collector_number"].String, out cardID))
            {
                cardID = 0;
            }
        }
        else
        {
            cardID = 0;
        }
    }
}
