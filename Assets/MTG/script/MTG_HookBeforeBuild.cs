using TMPro;
using UnityEngine;
using VRC.SDKBase;

namespace MTG
{
    /// <summary>
    /// Composant éditeur uniquement : il est retiré automatiquement du build
    /// grâce à l'interface IEditorOnly (rien ne part dans le monde VRChat).
    /// À placer sur un GameObject de la scène, avec le GameObject cible
    /// référencé dans <see cref="targetObject"/> et le champ TMP dans
    /// <see cref="checksumField"/>.
    ///
    /// Au build (via MTG_VRCBuildHook, dossier editor/, callback OnBuildRequested) :
    ///  1. Le checksum du monde est écrit dans le champ TMP (embarqué dans le build).
    ///  2. La rotation n'est re-randomisée QUE si le checksum a changé depuis le
    ///     build précédent : build sans modification = même checksum et même
    ///     rotation ; build avec modification = nouveau checksum et nouvelle rotation.
    ///
    /// ⚠ Pas de restauration après build (le SDK n'expose aucun callback public
    /// de fin de build pour les worlds) : la rotation reste appliquée dans
    /// l'éditeur. Le transform de la cible est exclu du checksum pour éviter
    /// toute boucle de feedback.
    /// </summary>
    public class MTG_HookBeforeBuild : MonoBehaviour, IEditorOnly
    {
        [Header("=== PRE-BUILD RANDOM ROTATION ===")]
        [Tooltip("GameObject qui recevra la rotation aléatoire juste avant le build/upload.")]
        public GameObject targetObject;

        [Tooltip("Randomise uniquement l'angle Y (yaw) au lieu d'un quaternion totalement aléatoire.")]
        public bool yawOnly = false;

        [Header("=== CHECKSUM ===")]
        [Tooltip("TextMeshProUGUI où écrire le checksum du monde au build. La valeur reste identique tant que le monde n'est pas modifié.")]
        public TextMeshProUGUI checksumField;

        [Tooltip("N'affiche que les 8 premiers caractères du checksum (plus lisible dans le monde).")]
        public bool shortChecksum = false;

        [Header("=== STATE (persisté dans la scène) ===")]
        [SerializeField] private string _lastChecksum = "";
        [SerializeField] private Quaternion _lastAppliedRotation;

        /// <summary>
        /// Appelé par MTG_VRCBuildHook juste avant la compilation du player.
        /// </summary>
        public void ApplyForBuild(string _Checksum)
        {
            // 1. Écriture du checksum dans le TMP input field (embarqué dans le build)
            if (checksumField != null)
            {
                checksumField.text = (shortChecksum && !string.IsNullOrEmpty(_Checksum) && _Checksum.Length > 8)
                    ? _Checksum.Substring(0, 8)
                    : _Checksum;
            }

            if (targetObject == null)
            {
                Debug.LogWarning($"[MTG_HookBeforeBuild] targetObject est null sur '{name}', rotation ignorée.", this);
                return;
            }

            // 2. Rotation : seulement si le monde a changé depuis le dernier build
            if (string.IsNullOrEmpty(_lastChecksum) || _lastChecksum != _Checksum)
            {
                Quaternion randomRotation = yawOnly
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                    : Random.rotationUniform;

                targetObject.transform.localRotation = randomRotation;
                _lastAppliedRotation = randomRotation;
                _lastChecksum = _Checksum;

                Debug.Log($"[MTG_HookBeforeBuild] Monde modifié (checksum {_Checksum}) : nouvelle rotation pour '{targetObject.name}' : " +
                          $"quaternion {randomRotation}, euler {randomRotation.eulerAngles}", this);
            }
            else
            {
                // Monde inchangé : on réapplique la rotation du build précédent (déterministe)
                targetObject.transform.localRotation = _lastAppliedRotation;

                Debug.Log($"[MTG_HookBeforeBuild] Monde inchangé (checksum {_Checksum}) : rotation du build précédent conservée pour '{targetObject.name}'.", this);
            }
        }
    }
}
