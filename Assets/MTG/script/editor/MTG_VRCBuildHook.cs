using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDKBase;
using VRC.SDKBase.Editor.BuildPipeline;

namespace MTG.Editor
{
    /// <summary>
    /// Hook de build VRChat (worlds uniquement) : calcule un checksum déterministe
    /// du monde, l'écrit dans le TMP field des MTG_HookBeforeBuild, et n'applique
    /// une nouvelle rotation aléatoire que si le checksum a changé depuis le
    /// build précédent (build sans modif = même checksum et même rotation).
    ///
    /// ⚠ Le hook s'exécute dans OnBuildRequested (IVRCSDKBuildRequestedCallback),
    /// le SEUL callback public du SDK pour les worlds : le SDK build un asset
    /// bundle, donc les callbacks Unity IPreprocessBuildWithReport /
    /// IPostprocessBuildWithReport ne se déclenchent JAMAIS, et les callbacks
    /// de scène du SDK (IVRCSDK*SceneCallback) sont internal (inaccessibles).
    ///
    /// Conséquence : pas de restauration après build — la rotation reste
    /// appliquée dans l'éditeur (WYSIWYG : la scène montre ce que le monde
    /// embarquera). Les transforms des objets cibles sont exclus du checksum
    /// pour éviter toute boucle de feedback.
    /// Ce fichier est dans un dossier editor/ : il n'est jamais compilé
    /// dans le monde VRChat (assembly Editor uniquement).
    /// </summary>
    public class MTG_VRCBuildHook : IVRCSDKBuildRequestedCallback
    {
        // Ordre de callback parmi les callbacks SDK.
        public int callbackOrder => 0;

        // ---------- SDK VRChat : build demandé (worlds uniquement) ----------
        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            // Avatars : rien à faire ici.
            if (requestedBuildType != VRCSDKRequestedBuildType.Scene)
                return true;

            try
            {
                var hooks = UnityEngine.Object.FindObjectsByType<MTG_HookBeforeBuild>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (hooks == null || hooks.Length == 0)
                {
                    Debug.Log("[MTG_VRCBuildHook] Aucun MTG_HookBeforeBuild dans la scène ouverte, rien à faire.");
                    return true;
                }

                // Cibles de rotation : leurs transforms sont exclus du checksum
                // (la rotation est gérée par le hook, elle ne doit pas changer
                // le checksum du monde).
                var excludedTargets = new HashSet<int>();
                foreach (var hook in hooks)
                {
                    if (hook.targetObject != null)
                        excludedTargets.Add(hook.targetObject.GetInstanceID());
                }

                string checksum = ComputeWorldChecksum(excludedTargets);
                Debug.Log($"[MTG_VRCBuildHook] Checksum du monde : {checksum}");

                int count = 0;
                foreach (var hook in hooks)
                {
                    if (hook.targetObject == null)
                    {
                        Debug.LogWarning($"[MTG_VRCBuildHook] targetObject null sur '{hook.name}', ignoré.", hook);
                        continue;
                    }

                    hook.ApplyForBuild(checksum);
                    EditorUtility.SetDirty(hook);
                    if (hook.checksumField != null)
                        EditorUtility.SetDirty(hook.checksumField);
                    count++;
                }

                Debug.Log($"[MTG_VRCBuildHook] Hook appliqué à {count} objet(s) (checksum + rotation).");

                // Persiste sur disque : le build embarque le checksum et l'état
                // survit aux reloads de scène/domaine.
                SaveActiveScene();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MTG_VRCBuildHook] Erreur au build : {e}");
            }

            return true;
        }

        // ---------- Persistance de la scène ----------
        // Écrit la scène active sur disque. Sans cela, les valeurs écrites en
        // mémoire (checksum du TMP field, _lastChecksum, rotation) seraient
        // perdues quand Unity recharge la scène après le build.
        private static void SaveActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("[MTG_VRCBuildHook] Scène non sauvegardée : la scène n'a pas de chemin (jamais enregistrée sur disque).");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
                Debug.Log("[MTG_VRCBuildHook] Scène sauvegardée : checksum + rotation persistés sur disque.");
            else
                Debug.LogWarning("[MTG_VRCBuildHook] Échec de la sauvegarde de la scène.");
        }

        // ---------- Checksum déterministe du monde ----------
        // MD5 de la description de la scène active (hiérarchie, transforms,
        // composants, valeurs sérialisées des objets proches de la racine) et
        // du code des scripts MTG. Deux builds sans modification donnent le
        // même checksum ; toute modification le change.
        private static string ComputeWorldChecksum(HashSet<int> _ExcludedTargets)
        {
            var sb = new StringBuilder();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            sb.Append("scene:").AppendLine(scene.path);

            foreach (var root in scene.GetRootGameObjects())
                AppendObject(sb, root, "", 0, _ExcludedTargets);

            // Code des scripts (Assets/MTG/script, récursif)
            sb.AppendLine("scripts:");
            string scriptDir = Path.Combine(Application.dataPath, "MTG", "script");
            if (Directory.Exists(scriptDir))
            {
                string[] files = Directory.GetFiles(scriptDir, "*.cs", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                using (var md5 = MD5.Create())
                {
                    foreach (string file in files)
                    {
                        string rel = file.Replace(Application.dataPath, "Assets");
                        byte[] bytes = File.ReadAllBytes(file);
                        sb.Append(rel).Append(':').Append(ToHex(md5.ComputeHash(bytes))).AppendLine();
                    }
                }
            }

            using (var md5 = MD5.Create())
                return ToHex(md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        private static void AppendObject(StringBuilder sb, GameObject go, string path, int depth, HashSet<int> _ExcludedTargets)
        {
            Transform t = go.transform;
            string fullPath = string.IsNullOrEmpty(path) ? go.name : path + "/" + go.name;

            sb.Append(fullPath).Append('|');

            // Transforms exclus pour les cibles du hook (rotation gérée par le hook)
            if (_ExcludedTargets == null || !_ExcludedTargets.Contains(go.GetInstanceID()))
            {
                sb.Append(t.localPosition.ToString("F4", CultureInfo.InvariantCulture)).Append('|')
                  .Append(t.localRotation.ToString("F4", CultureInfo.InvariantCulture)).Append('|')
                  .Append(t.localScale.ToString("F4", CultureInfo.InvariantCulture)).Append('|');
            }

            sb.Append(go.activeSelf).Append('|')
              .Append(go.layer).Append('|');

            Component[] comps = go.GetComponents<Component>();
            string[] typeNames = new string[comps.Length];
            for (int i = 0; i < comps.Length; i++)
                typeNames[i] = comps[i] == null ? "null" : comps[i].GetType().FullName;
            Array.Sort(typeNames, StringComparer.Ordinal);
            for (int i = 0; i < typeNames.Length; i++)
                sb.Append(typeNames[i]).Append(',');

            // Valeurs sérialisées des composants proches de la racine (config
            // gameplay : Manager, interfaces...). On exclut TMP et IEditorOnly
            // pour éviter les boucles de feedback (le champ checksum est écrit
            // à chaque build) et UdonBehaviour (trop volumineux).
            if (depth <= 2)
            {
                foreach (Component c in comps)
                {
                    if (c == null) continue;
                    Type type = c.GetType();
                    if (type.FullName == null ||
                        type.FullName.StartsWith("TMPro.") ||
                        c is IEditorOnly ||
                        type.Name == "UdonBehaviour")
                        continue;
                    try
                    {
                        sb.Append(type.FullName).Append('=');
                        sb.AppendLine(EditorJsonUtility.ToJson(c));
                    }
                    catch
                    {
                        // Composant non sérialisable : ignoré
                    }
                }
            }

            sb.AppendLine();

            for (int i = 0; i < t.childCount; i++)
                AppendObject(sb, t.GetChild(i).gameObject, fullPath, depth + 1, _ExcludedTargets);
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
