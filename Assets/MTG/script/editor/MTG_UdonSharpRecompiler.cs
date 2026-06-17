// Script d'éditeur pour recompiler les scripts UdonSharp MTG
// Place ce fichier dans Assets/MTG/script/editor/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UdonSharpEditor;

namespace MTG
{
    public class MTG_UdonSharpRecompiler : Editor
    {
        [MenuItem("MTG/Recompile All UdonSharp Scripts")]
        public static void RecompileAllUdonSharp()
        {
            Debug.Log("[MTG] Starting recompilation of all UdonSharp scripts...");
            
            try
            {
                // Force la recompilation de tous les programmes UdonSharp
                UdonSharpProgramAsset[] allPrograms = Resources.FindObjectsOfTypeAll<UdonSharpProgramAsset>();
                
                int count = 0;
                foreach (var program in allPrograms)
                {
                    if (program != null && program.sourceCsScript != null)
                    {
                        // Vérifier si c'est un script MTG
                        string scriptPath = AssetDatabase.GetAssetPath(program.sourceCsScript);
                        if (scriptPath.Contains("/MTG/"))
                        {
                            UdonSharpEditorUtility.CompileSync(program.sourceCsScript);
                            count++;
                            Debug.Log($"[MTG] Recompiled: {program.sourceCsScript.name}");
                        }
                    }
                }
                
                Debug.Log($"[MTG] ✅ Recompilation complete! {count} MTG scripts recompiled.");
                EditorUtility.DisplayDialog("MTG Recompilation", 
                    $"Successfully recompiled {count} MTG UdonSharp scripts!", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MTG] ❌ Error during recompilation: {e.Message}");
                EditorUtility.DisplayDialog("MTG Recompilation Error", 
                    $"Error during recompilation:\n{e.Message}", "OK");
            }
        }
        
        [MenuItem("MTG/Clear All Card Data (Reset)")]
        public static void ClearAllCardData()
        {
            if (EditorUtility.DisplayDialog("MTG Reset", 
                "This will clear all card data and reset the pool. Continue?", "Yes", "Cancel"))
            {
                Debug.Log("[MTG] Clearing all card data...");
                
                // Trouver tous les PhysicCard et les réinitialiser
                MTG_PhysicCard[] allCards = FindObjectsOfType<MTG_PhysicCard>();
                foreach (var card in allCards)
                {
                    if (card != null)
                    {
                        card.gameObject.SetActive(false);
                        Debug.Log($"[MTG] Reset card: {card.name}");
                    }
                }
                
                Debug.Log($"[MTG] ✅ Cleared {allCards.Length} cards.");
                EditorUtility.DisplayDialog("MTG Reset", 
                    $"Cleared {allCards.Length} cards successfully!", "OK");
            }
        }
        
        [MenuItem("MTG/Force Reimport All MTG Scripts")]
        public static void ForceReimportAllScripts()
        {
            Debug.Log("[MTG] Force reimporting all MTG scripts...");
            
            string[] guids = AssetDatabase.FindAssets("t:Script", new[] { "Assets/MTG/script" });
            int count = 0;
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
                Debug.Log($"[MTG] Reimported: {path}");
            }
            
            Debug.Log($"[MTG] ✅ Force reimport complete! {count} scripts reimported.");
            EditorUtility.DisplayDialog("MTG Reimport", 
                $"Successfully reimported {count} MTG scripts!", "OK");
        }
    }
}
#endif
