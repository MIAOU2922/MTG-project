using UdonSharp;

namespace MTG
{
    /// <summary>
    /// Carte qui doit etre tickee chaque frame (ex : cartes physiques avec
    /// late-join differe et sync de position). Les cartes search/deck restent
    /// sur MTG_Card (purement evenementielles, aucun Update).
    /// </summary>
    public class MTG_TickableCard : MTG_Card
    {
        protected virtual void Update()
        {
            TickBase();
        }
    }
}
