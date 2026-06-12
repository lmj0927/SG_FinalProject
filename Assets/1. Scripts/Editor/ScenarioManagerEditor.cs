#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScenarioManager))]
public class ScenarioManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

        var manager = (ScenarioManager)target;

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 모드에서 마지막 노드 스킵을 사용할 수 있습니다.", MessageType.Info);
            }

            if (GUILayout.Button("Skip To Last Node", GUILayout.Height(28)))
            {
                manager.DebugSkipToLastNode();
            }
        }
    }
}
#endif
