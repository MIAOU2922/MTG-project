using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
/*
namespace MTG.Editor
{
    [CustomEditor(typeof(MTG_PhysicCardPoolManager))]
    public class MTG_PhysicCardPoolManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty managerProp;
        private SerializedProperty physicCardPrefabProp;
        private SerializedProperty poolSizeProp;
        private SerializedProperty cardsParentProp;
        private SerializedProperty cardPoolProp;
        private SerializedProperty cardIdsListProp;
        private SerializedProperty syncedCardCountProp;

        private Vector2 scrollPosition;
        private bool showCardsList = true;

        private void OnEnable()
        {
            managerProp = serializedObject.FindProperty("manager");
            physicCardPrefabProp = serializedObject.FindProperty("physicCardPrefab");
            poolSizeProp = serializedObject.FindProperty("poolSize");
            cardsParentProp = serializedObject.FindProperty("cardsParent");
            cardPoolProp = serializedObject.FindProperty("cardPool");
            cardIdsListProp = serializedObject.FindProperty("cardIdsList");
            syncedCardCountProp = serializedObject.FindProperty("syncedCardCount");
        }

        public override void OnInspectorGUI()
        {
            // Update
            serializedObject.Update();

            MTG_PhysicCardPoolManager poolManager = (MTG_PhysicCardPoolManager)target;

            // Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MTG Physical Card Pool Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // References
            EditorGUILayout.LabelField("=== POOL CONFIGURATION ===", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(physicCardPrefabProp);
            EditorGUILayout.PropertyField(poolSizeProp);
            EditorGUILayout.Space();
            
            // Bouton pour générer la pool
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Pool Generation", EditorStyles.boldLabel);
            
            if (physicCardPrefabProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a Physic Card Prefab first!", MessageType.Warning);
            }
            else
            {
                if (cardPoolProp.arraySize > 0)
                {
                    EditorGUILayout.HelpBox($"Pool already contains {cardPoolProp.arraySize} objects", MessageType.Info);
                    if (GUILayout.Button("Clear Pool", GUILayout.Height(30)))
                    {
                        if (EditorUtility.DisplayDialog("Clear Pool", 
                            "This will delete all pre-instantiated cards. Continue?", 
                            "Yes", "Cancel"))
                        {
                            ClearPool(poolManager);
                        }
                    }
                }
                
                if (GUILayout.Button("Generate Pool (" + poolSizeProp.intValue + " cards)", GUILayout.Height(40)))
                {
                    GeneratePool(poolManager);
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // References
            EditorGUILayout.LabelField("=== REFERENCES ===", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(managerProp);
            EditorGUILayout.PropertyField(cardsParentProp);
            EditorGUILayout.Space();

            // Pool array (read-only)
            EditorGUILayout.LabelField("=== PRE-INSTANTIATED POOL ===", EditorStyles.boldLabel);
            GUI.enabled = false;
            EditorGUILayout.PropertyField(cardPoolProp, true);
            GUI.enabled = true;
            EditorGUILayout.Space();

            // Stats
            EditorGUILayout.LabelField("=== POOL STATUS ===", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Pool Size: {cardPoolProp.arraySize}");
            EditorGUILayout.LabelField($"Active Cards: {syncedCardCountProp.intValue}");
            EditorGUILayout.LabelField($"Available: {cardPoolProp.arraySize - syncedCardCountProp.intValue}");
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // Active Cards List
            showCardsList = EditorGUILayout.BeginFoldoutHeaderGroup(showCardsList, "=== ACTIVE CARDS LIST ===");
            
            if (showCardsList)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                int count = syncedCardCountProp.intValue;
                
                if (count == 0)
                {
                    EditorGUILayout.LabelField("No cards active yet", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    // Scroll view
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(300));
                    
                    for (int i = 0; i < count; i++)
                    {
                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        
                        // Index
                        EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(35));
                        
                        // Card ID
                        string cardId = "";
                        if (cardIdsListProp.arraySize > i)
                        {
                            SerializedProperty cardIdProp = cardIdsListProp.GetArrayElementAtIndex(i);
                            cardId = cardIdProp.stringValue;
                        }
                        
                        if (string.IsNullOrEmpty(cardId))
                        {
                            EditorGUILayout.LabelField("<empty>", EditorStyles.miniLabel, GUILayout.Width(150));
                        }
                        else
                        {
                            EditorGUILayout.LabelField(cardId, GUILayout.Width(250));
                        }
                        
                        // GameObject
                        GameObject poolCard = null;
                        if (cardPoolProp.arraySize > i)
                        {
                            SerializedProperty cardObjProp = cardPoolProp.GetArrayElementAtIndex(i);
                            poolCard = cardObjProp.objectReferenceValue as GameObject;
                        }
                        
                        GUI.enabled = false;
                        EditorGUILayout.ObjectField(poolCard, typeof(GameObject), true);
                        GUI.enabled = true;
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    EditorGUILayout.EndScrollView();
                }
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Apply
            serializedObject.ApplyModifiedProperties();
        }
        
        private void GeneratePool(MTG_PhysicCardPoolManager poolManager)
        {
            GameObject prefab = poolManager.physicCardPrefab;
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Error", "No prefab assigned!", "OK");
                return;
            }
            
            // Créer un parent pour la pool si elle n'existe pas
            Transform poolParent = poolManager.transform.Find("CardPool");
            if (poolParent == null)
            {
                GameObject poolParentObj = new GameObject("CardPool");
                poolParentObj.transform.SetParent(poolManager.transform);
                poolParentObj.transform.localPosition = Vector3.zero;
                poolParent = poolParentObj.transform;
            }
            
            // Générer les cartes
            GameObject[] newPool = new GameObject[poolManager.poolSize];
            
            for (int i = 0; i < poolManager.poolSize; i++)
            {
                GameObject card = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                card.name = $"Card_{i:000}";
                card.transform.SetParent(poolParent);
                card.transform.localPosition = Vector3.zero;
                card.transform.localRotation = Quaternion.identity;
                card.SetActive(false);
                newPool[i] = card;
            }
            
            // Assigner le pool
            poolManager.cardPool = newPool;
            
            // Marquer comme modifié
            EditorUtility.SetDirty(poolManager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(poolManager.gameObject.scene);
            
            Debug.Log($"[MTG_PhysicCardPoolManager] Generated pool with {poolManager.poolSize} cards");
        }
        
        private void ClearPool(MTG_PhysicCardPoolManager poolManager)
        {
            if (poolManager.cardPool != null)
            {
                foreach (GameObject card in poolManager.cardPool)
                {
                    if (card != null)
                    {
                        DestroyImmediate(card);
                    }
                }
            }
            
            // Clear le parent pool
            Transform poolParent = poolManager.transform.Find("CardPool");
            if (poolParent != null)
            {
                DestroyImmediate(poolParent.gameObject);
            }
            
            poolManager.cardPool = new GameObject[0];
            
            // Marquer comme modifié
            EditorUtility.SetDirty(poolManager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(poolManager.gameObject.scene);
            
            Debug.Log("[MTG_PhysicCardPoolManager] Pool cleared");
        }
    }
}
*/