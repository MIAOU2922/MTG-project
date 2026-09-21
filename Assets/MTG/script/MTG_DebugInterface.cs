using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace MTG
{
    public class MTG_DebugInterface : MTG_Tickable
    {
        [Header("=== DEBUG TEXT (TMP) ===")]
        public TextMeshProUGUI DebugText;

        [Header("=== FRAME SKIP ===")]
        [Tooltip("Nombre de frames entre chaque refresh du texte debug. 50 = ~1x/sec @50fps.")]
        [SerializeField] private int _debugFrameSkip = 50;
        protected override int FrameSkipCount => _debugFrameSkip;

        protected override void Update()
        {
            base.Update();
            if (!ShouldUpdate()) return;
            if (Manager == null) return;
            if (DebugText == null) return;

            // Ligne 1 : titre, puis une ligne vide, puis le contenu (max 32 lignes)
            string _Text = "<align=center><size=150%>Debug</size></align>\n\n";

            _Text += "instance id: " + Manager.GetInstanceID().ToString() + "\n";

            _Text += "user key: " + Manager.GetUserKey() + "\n";

            _Text += "joueurs: " + VRCPlayerApi.GetPlayerCount().ToString() + "\n";

            _Text += "spawn: " + (Manager.PhysicCardPool != null ? Manager.PhysicCardPool.GetActiveCount().ToString() + "/" + Manager.PhysicCardPool.GetPoolCapacity().ToString() : "0/0") + "\n";

            _Text += "atlases dl: " + Manager.GetLoadedAtlasCount().ToString() + "/" + Manager.GetMaxAtlas().ToString() + "\n";

            _Text += "atlases pending: " + Manager.GetPendingAtlasCount().ToString() + "\n";

            _Text += "atlases loading: " + Manager.GetLoadingAtlasCount().ToString() + "\n";

            _Text += "cartes: " + Manager.GetLoadedCardsCount().ToString() + "\n";

            _Text += "rulings: " + Manager.GetRulingsCount().ToString() + "\n";

            _Text += "sync: " + (Manager.IsCurrentlySyncing() ? "true" : "false") + "\n";

            _Text += "agree: " + (Manager.IsAgreed() ? "true" : "false") + "\n";

            DebugText.text = _Text;
        }
    }
}
