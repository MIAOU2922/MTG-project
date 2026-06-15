using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UdonSharpEditor;
using UdonSharp;

namespace M2922.Editor
{
    /// <summary>
    /// Générateur automatique de Udon C# Program Assets
    /// Scanne tous les scripts héritant de UdonSharpBehaviour et crée les assets correspondants
    /// </summary>
    public class M2922_UdonProgramAssetGenerator : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool includeSubfolders = true;
        private string targetFolder = "Assets/_MIAOU_/Script";
        private string outputFolder = "Assets/_MIAOU_/UdonPrograms";
        
        private List<ScriptInfo> foundScripts = new List<ScriptInfo>();
        private bool hasScanned = false;
        
        private class ScriptInfo
        {
            public MonoScript script;
            public string path;
            public string className;
            public bool hasAsset;
            public UdonSharpProgramAsset existingAsset;
        }
        
        [MenuItem("M2922/Udon/Generate Program Assets")]
        public static void ShowWindow()
        {
            var window = GetWindow<M2922_UdonProgramAssetGenerator>("M2922 Udon Program Generator");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Générateur de Udon C# Program Assets", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            // === SETTINGS ===
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Paramètres", EditorStyles.boldLabel);
            
            targetFolder = EditorGUILayout.TextField("Dossier source:", targetFolder);
            outputFolder = EditorGUILayout.TextField("Dossier de sortie:", outputFolder);
            includeSubfolders = EditorGUILayout.Toggle("Inclure sous-dossiers", includeSubfolders);
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            
            // === ACTIONS ===
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Scanner les Scripts", GUILayout.Height(30)))
            {
                ScanForUdonSharpScripts();
            }
            
            if (GUILayout.Button("Générer tous les Assets", GUILayout.Height(30)))
            {
                if (!hasScanned)
                {
                    ScanForUdonSharpScripts();
                }
                GenerateAllAssets();
            }
            
            if (GUILayout.Button("Recompiler tout", GUILayout.Height(30)))
            {
                RecompileAllAssets();
            }
            
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            
            // === RESULTS ===
            if (hasScanned && foundScripts.Count > 0)
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label($"Scripts trouvés: {foundScripts.Count}", EditorStyles.boldLabel);
                
                int withAssets = foundScripts.Count(s => s.hasAsset);
                int withoutAssets = foundScripts.Count - withAssets;
                
                EditorGUILayout.LabelField("Avec assets:", withAssets.ToString());
                EditorGUILayout.LabelField("Sans assets:", withoutAssets.ToString());
                
                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
                
                // Liste des scripts
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                
                foreach (var scriptInfo in foundScripts)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    
                    // Icône status
                    if (scriptInfo.hasAsset)
                    {
                        EditorGUILayout.LabelField("✓", GUILayout.Width(20));
                    }
                    else
                    {
                        EditorGUILayout.LabelField("✗", GUILayout.Width(20));
                    }
                    
                    // Nom du script
                    EditorGUILayout.LabelField(scriptInfo.className, GUILayout.Width(200));
                    
                    // Path
                    EditorGUILayout.LabelField(scriptInfo.path, EditorStyles.miniLabel);
                    
                    GUILayout.FlexibleSpace();
                    
                    // Bouton pour créer l'asset
                    if (!scriptInfo.hasAsset)
                    {
                        if (GUILayout.Button("Créer Asset", GUILayout.Width(100)))
                        {
                            CreateProgramAsset(scriptInfo);
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Recompiler", GUILayout.Width(100)))
                        {
                            RecompileAsset(scriptInfo.existingAsset);
                        }
                        
                        if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        {
                            EditorGUIUtility.PingObject(scriptInfo.existingAsset);
                        }
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
            }
            else if (hasScanned)
            {
                EditorGUILayout.HelpBox("Aucun script UdonSharpBehaviour trouvé dans le dossier spécifié.", MessageType.Info);
            }
        }
        
        private void ScanForUdonSharpScripts()
        {
            foundScripts.Clear();
            
            // Trouver tous les MonoScripts dans le dossier cible
            string searchPattern = includeSubfolders ? $"{targetFolder}/**/*.cs" : $"{targetFolder}/*.cs";
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { targetFolder });
            
            Debug.Log($"[M2922] Scanning for UdonSharp scripts in: {targetFolder}");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Ignorer les scripts dans Editor
                if (path.Contains("/Editor/")) continue;
                
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) continue;
                
                System.Type scriptType = script.GetClass();
                if (scriptType == null) continue;
                
                // Vérifier si c'est un UdonSharpBehaviour
                if (typeof(UdonSharpBehaviour).IsAssignableFrom(scriptType))
                {
                    var info = new ScriptInfo
                    {
                        script = script,
                        path = path,
                        className = scriptType.Name
                    };
                    
                    // Chercher si un asset existe déjà
                    info.existingAsset = FindExistingAsset(scriptType);
                    info.hasAsset = info.existingAsset != null;
                    
                    foundScripts.Add(info);
                }
            }
            
            hasScanned = true;
            Debug.Log($"[M2922] Found {foundScripts.Count} UdonSharp scripts");
        }
        
        private UdonSharpProgramAsset FindExistingAsset(System.Type scriptType)
        {
            // Chercher tous les UdonSharpProgramAssets
            string[] assetGuids = AssetDatabase.FindAssets("t:UdonSharpProgramAsset");
            
            foreach (string guid in assetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UdonSharpProgramAsset asset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
                
                if (asset != null && asset.sourceCsScript != null)
                {
                    var assetType = asset.sourceCsScript.GetClass();
                    if (assetType == scriptType)
                    {
                        return asset;
                    }
                }
            }
            
            return null;
        }
        
        private void CreateProgramAsset(ScriptInfo scriptInfo)
        {
            // Créer le dossier de sortie si nécessaire
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                string parentFolder = Path.GetDirectoryName(outputFolder).Replace('\\', '/');
                string folderName = Path.GetFileName(outputFolder);
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
            
            // Nom du fichier asset
            string assetPath = Path.Combine(outputFolder, $"{scriptInfo.className}.asset").Replace('\\', '/');
            
            // Vérifier si un asset existe déjà à cet emplacement
            if (File.Exists(assetPath))
            {
                Debug.LogWarning($"[M2922] Asset already exists at: {assetPath}");
                return;
            }
            
            // Créer l'asset
            UdonSharpProgramAsset newAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            newAsset.sourceCsScript = scriptInfo.script;
            
            AssetDatabase.CreateAsset(newAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"[M2922] Created Udon Program Asset: {assetPath}");
            
            // Mettre à jour l'info
            scriptInfo.existingAsset = newAsset;
            scriptInfo.hasAsset = true;
        }
        
        private void GenerateAllAssets()
        {
            int created = 0;
            
            foreach (var scriptInfo in foundScripts)
            {
                if (!scriptInfo.hasAsset)
                {
                    CreateProgramAsset(scriptInfo);
                    created++;
                }
            }
            
            if (created > 0)
            {
                EditorUtility.DisplayDialog("Génération terminée", 
                    $"{created} Udon Program Asset(s) créé(s) avec succès!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Génération terminée", 
                    "Tous les scripts ont déjà leurs assets.", "OK");
            }
            
            // Rescanner pour mettre à jour
            ScanForUdonSharpScripts();
        }
        
        private void RecompileAsset(UdonSharpProgramAsset asset)
        {
            if (asset == null) return;
            
            // Marquer comme modifié pour forcer la recompilation
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"[M2922] Recompiled: {asset.name}");
        }
        
        private void RecompileAllAssets()
        {
            if (!hasScanned)
            {
                ScanForUdonSharpScripts();
            }
            
            int recompiled = 0;
            
            foreach (var scriptInfo in foundScripts)
            {
                if (scriptInfo.hasAsset && scriptInfo.existingAsset != null)
                {
                    RecompileAsset(scriptInfo.existingAsset);
                    recompiled++;
                }
            }
            
            EditorUtility.DisplayDialog("Recompilation terminée", 
                $"{recompiled} Udon Program Asset(s) recompilé(s) avec succès!", "OK");
        }
    }
}
