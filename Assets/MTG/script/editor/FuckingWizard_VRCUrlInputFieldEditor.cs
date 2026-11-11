using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;

namespace VRC.SDK3.Components.Editor
{
    [CustomEditor(typeof(FuckingWizard_VRCUrlInputField))]
    public class FuckingWizard_VRCUrlInputFieldEditor : UnityEditor.Editor
    {
        private SerializedProperty _TmpInput;
        private SerializedProperty _TmpText;
        private SerializedProperty _PendingUrl;

        private void OnEnable()
        {
            _TmpInput   = serializedObject.FindProperty("TmpInput");
            _TmpText    = serializedObject.FindProperty("TmpText");
            _PendingUrl = serializedObject.FindProperty("PendingUrl");
        }

        public override void OnInspectorGUI()
        {
            var _Comp = (FuckingWizard_VRCUrlInputField)target;

            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Champ VRC URL rendu via un TMP_InputField. Le Text legacy interne " +
                "(exigé par VRCUrlInputField) est créé automatiquement et masqué : " +
                "tout l'affichage et la saisie passent par le TMP.",
                MessageType.Info);

            EditorGUILayout.PropertyField(_TmpInput, new GUIContent("TMP Input Field"));
            EditorGUILayout.PropertyField(_TmpText, new GUIContent("TMP Text (optionnel)"));
            EditorGUILayout.PropertyField(_PendingUrl, new GUIContent("Pending Url (Udon)"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto Setup"))
                {
                    Undo.RecordObject(_Comp, "Auto Setup FuckingWizard VRCUrlInputField");
                    AutoSetup(_Comp);
                    EditorUtility.SetDirty(_Comp);
                }

                if (GUILayout.Button("Find TMP Input Field"))
                {
                    Undo.RecordObject(_Comp, "Find TMP Input Field");
                    _Comp.TmpInput = _Comp.GetComponentInChildren<TMP_InputField>(true);
                    EditorUtility.SetDirty(_Comp);
                }
            }

            if (_Comp.TmpInput == null)
            {
                EditorGUILayout.HelpBox(
                    "Aucun TMP_InputField assigné. Ajoutez un TMP_InputField (enfant ou même objet), " +
                    "puis cliquez sur \"Find TMP Input Field\" ou \"Auto Setup\".",
                    MessageType.Warning);
            }

            DrawDefaultInspectorNote();
        }

        private static void AutoSetup(FuckingWizard_VRCUrlInputField _Comp)
        {
            // 1. Champ de saisie TMP.
            if (_Comp.TmpInput == null)
                _Comp.TmpInput = _Comp.GetComponentInChildren<TMP_InputField>(true);

            if (_Comp.TmpInput != null)
            {
                _Comp.TmpInput.lineType = TMP_InputField.LineType.SingleLine;
                _Comp.TmpInput.contentType = TMP_InputField.ContentType.Standard;
                EditorUtility.SetDirty(_Comp.TmpInput);
            }

            // 2. Text legacy interne (invisible) exigé par VRCUrlInputField.
            EnsureHiddenInternalText(_Comp);
        }

        private static void EnsureHiddenInternalText(FuckingWizard_VRCUrlInputField _Comp)
        {
            Text _Text = _Comp.textComponent;
            if (_Text == null)
            {
                _Text = _Comp.GetComponent<Text>();
                if (_Text == null)
                    _Text = Undo.AddComponent<Text>(_Comp.gameObject);

                _Comp.textComponent = _Text;
            }

            _Text.text = string.Empty;
            _Text.raycastTarget = false;
            _Text.supportRichText = false;
            _Text.alignment = TextAnchor.MiddleLeft;
            _Text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _Text.verticalOverflow = VerticalWrapMode.Overflow;
            _Text.color = new Color(0f, 0f, 0f, 0f);

            EditorUtility.SetDirty(_Text);
        }

        // Le reste des champs natifs de VRCUrlInputField est masqué volontairement :
        // ils sont gérés automatiquement (Text Component, Placeholder, etc.).
        private void DrawDefaultInspectorNote()
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Les champs natifs (Text Component, Placeholder...) sont masqués et gérés " +
                "automatiquement pour rester compatibles VRChat tout en affichant via TMP.",
                MessageType.None);
        }
    }
}
