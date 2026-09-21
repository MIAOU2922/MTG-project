using UdonSharp;

namespace MTG
{
    /// <summary>
    /// Base des composants qui doivent etre TICKES chaque frame par Udon (Update).
    ///
    /// N'heritez de cette classe QUE si le composant a reellement besoin d'un
    /// Update() par frame (progression, animation, polling UI...).
    ///
    /// Les composants purement EVENEMENTIELS doivent heriter directement de
    /// MTG_Base : ils n'auront alors AUCUN cout par frame (pas d'event Update
    /// dans le programme Udon).
    ///
    /// Si vous surchargez Update(), appelez base.Update() pour conserver le
    /// compteur de frames (ShouldUpdate()).
    /// </summary>
    public class MTG_Tickable : MTG_Base
    {
        protected virtual void Update()
        {
            TickBase();
        }
    }
}
