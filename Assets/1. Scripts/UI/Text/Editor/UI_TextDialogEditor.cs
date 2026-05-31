#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UI_TextDialog))]
public class UI_TextDialogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var dialog = (UI_TextDialog)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Preview Controls", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 모드에서 Show / Hide 버튼으로 등장·퇴장 연출을 미리 볼 수 있습니다.", MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Show", GUILayout.Height(28)))
            {
                dialog.InspectorPreviewShow();
            }

            if (GUILayout.Button("Hide", GUILayout.Height(28)))
            {
                dialog.InspectorPreviewHide();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
