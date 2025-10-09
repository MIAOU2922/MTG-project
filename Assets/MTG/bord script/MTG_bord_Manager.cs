using UdonSharp;
using UnityEngine;

namespace MTG
{
    public enum ZoneCategory { Board = 0, Hand = 1, Deck = 2 }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MTG_bord_Manager : UdonSharpBehaviour
    {
        public MTG_CardZone[] BoardZones = new MTG_CardZone[0];
        public MTG_CardZone[] HandZones = new MTG_CardZone[0];
        public MTG_CardZone[] DeckZones = new MTG_CardZone[0];

        public void RegisterZone(MTG_CardZone z, ZoneCategory cat)
        {
            MTG_CardZone[] old;
            if (cat == ZoneCategory.Board) old = BoardZones;
            else if (cat == ZoneCategory.Hand) old = HandZones;
            else old = DeckZones;

            MTG_CardZone[] next = new MTG_CardZone[old.Length + 1];
            for (int i = 0; i < old.Length; i++) next[i] = old[i];
            next[old.Length] = z;

            if (cat == ZoneCategory.Board) BoardZones = next;
            else if (cat == ZoneCategory.Hand) HandZones = next;
            else DeckZones = next;
        }

        public void UnregisterZone(MTG_CardZone z, ZoneCategory cat)
        {
            MTG_CardZone[] old;
            if (cat == ZoneCategory.Board) old = BoardZones;
            else if (cat == ZoneCategory.Hand) old = HandZones;
            else old = DeckZones;

            int idx = -1;
            for (int i = 0; i < old.Length; i++) if (old[i] == z) { idx = i; break; }
            if (idx < 0) return;

            MTG_CardZone[] next = new MTG_CardZone[old.Length - 1];
            for (int i = 0, j = 0; i < old.Length; i++)
            {
                if (i == idx) continue;
                next[j++] = old[i];
            }

            if (cat == ZoneCategory.Board) BoardZones = next;
            else if (cat == ZoneCategory.Hand) HandZones = next;
            else DeckZones = next;
        }

        public MTG_CardZone[] GetZones(ZoneCategory cat)
        {
            if (cat == ZoneCategory.Board) return BoardZones;
            if (cat == ZoneCategory.Hand) return HandZones;
            return DeckZones;
        }
    }
}
