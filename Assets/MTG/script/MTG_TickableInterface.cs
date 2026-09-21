using UdonSharp;

namespace MTG
{
    /// <summary>
    /// Interface qui doit etre tickee chaque frame (ex : chargement progressif
    /// de la search interface). Les interfaces purement evenementielles restent
    /// sur MTG_Interface (aucun Update).
    /// </summary>
    public class MTG_TickableInterface : MTG_Interface
    {
        protected virtual void Update()
        {
            TickBase();
        }
    }
}
