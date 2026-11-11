using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRC.SDK3.Components
{
    /// <summary>
    /// Variante "TMP" du <see cref="VRCUrlInputField"/>.
    /// Reste un vrai VRCUrlInputField (donc GetUrl()/SetUrl() et la validation VRChat
    /// fonctionnent), mais s'appuie sur un TMP_InputField pour la saisie et l'affichage,
    /// ce qui apporte toutes les fonctionnalités TextMeshPro (font asset, rich text,
    /// point size, auto-size...).
    ///
    /// Le <see cref="Text"/> legacy interne (exigé par VRCUrlInputField pour son rendu)
    /// est créé automatiquement et masqué : tout ce qui est visible passe par le TMP.
    /// </summary>
    [AddComponentMenu("VRChat/UI/FuckingWizard VRC Url Input Field (TMP)")]
    public class FuckingWizard_VRCUrlInputField : VRCUrlInputField
    {
        [Header("=== TMP ===")]
        [Tooltip("TMP_InputField qui sert d'interface de saisie ET d'affichage. " +
                 "Sa frappe est recopiée dans le champ VRC, et sa validation (Entrée) " +
                 "déclenche le submit du VRCUrlInputField (onEndEdit -> Udon).")]
        public TMP_InputField TmpInput;

        [Tooltip("TMP_Text d'affichage optionnel (en plus ou à la place de TmpInput).")]
        public TMP_Text TmpText;

        [Header("=== UDON ===")]
        [Tooltip("URL à appliquer (settable depuis UdonSharp). Consommée au prochain Update.")]
        public string PendingUrl;

        // Garde anti-réentrance pour la synchro bidirectionnelle VRC <-> TMP.
        private bool _SyncGuard;

        protected override void Awake()
        {
            base.Awake();

            EnsureInternalText();

            onValueChanged.AddListener(OnVrcValueChanged);

            if (TmpInput != null)
            {
                TmpInput.onValueChanged.AddListener(OnTmpValueChanged);
                // onSubmit ne se déclenche qu'à la validation (Entrée), pas à la simple
                // perte de focus : on l'utilise pour déclencher la validation VRC.
                TmpInput.onSubmit.AddListener(OnTmpSubmit);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // Le champ VRC est la source de vérité (SetUrl/PendingUrl) : on pousse vers le TMP.
            PushVrcToTmp();
        }

        private void Update()
        {
            // Udon ne peut pas créer de VRCUrl : on pousse la string via PendingUrl.
            if (!string.IsNullOrEmpty(PendingUrl))
            {
                text = PendingUrl;
                PendingUrl = null;
            }
        }

        // VRCUrlInputField a changé -> on met le TMP à jour.
        private void OnVrcValueChanged(string _Value)
        {
            if (_SyncGuard)
                return;
            PushVrcToTmp();
        }

        // L'utilisateur a tapé dans le TMP -> on met le champ VRC à jour.
        private void OnTmpValueChanged(string _Value)
        {
            if (_SyncGuard)
                return;
            _SyncGuard = true;
            text = _Value;
            _SyncGuard = false;
        }

        // L'utilisateur a validé dans le TMP (Entrée) -> on déclenche la validation VRC.
        private void OnTmpSubmit(string _Value)
        {
            if (!string.IsNullOrEmpty(_Value))
            {
                _SyncGuard = true;
                text = _Value;
                _SyncGuard = false;
            }

            // Equivalent du submit natif du VRCUrlInputField : appelle onEndEdit
            // (et donc le callback Udon branché dessus, ex: OnUrlValidated).
            SendOnSubmit();
        }

        private void PushVrcToTmp()
        {
            string _Url = text;

            _SyncGuard = true;
            if (TmpInput != null)
                TmpInput.text = _Url;

            if (TmpText != null && TmpText != (TmpInput != null ? TmpInput.textComponent : null))
                TmpText.text = _Url;
            _SyncGuard = false;
        }

        // VRCUrlInputField exige un Text (legacy) pour son rendu interne.
        // S'il n'y en a pas, on en crée un invisible : tout l'affichage passe par le TMP.
        private void EnsureInternalText()
        {
            if (textComponent != null)
                return;

            Text _Text = GetComponent<Text>();
            if (_Text == null)
                _Text = gameObject.AddComponent<Text>();

            _Text.text = string.Empty;
            _Text.raycastTarget = false;
            _Text.supportRichText = false;
            _Text.alignment = TextAnchor.MiddleLeft;
            _Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _Text.verticalOverflow = VerticalWrapMode.Overflow;
            _Text.color = new Color(0f, 0f, 0f, 0f); // invisible

            textComponent = _Text;
        }
    }
}