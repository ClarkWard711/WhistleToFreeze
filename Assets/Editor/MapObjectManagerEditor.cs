using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MapObjectManager))]
public class MapObjectManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editor Controls", EditorStyles.boldLabel);

        var mgr = (MapObjectManager)target;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate All"))
        {
            mgr.GenerateAll();
            // mark scene dirty if in editor
            if (!Application.isPlaying) EditorUtility.SetDirty(mgr);
        }
        if (GUILayout.Button("Clear All"))
        {
            mgr.ClearAll();
            if (!Application.isPlaying) EditorUtility.SetDirty(mgr);
        }
        if (GUILayout.Button("Regenerate All"))
        {
            mgr.RegenerateAll();
            if (!Application.isPlaying) EditorUtility.SetDirty(mgr);
        }
        EditorGUILayout.EndHorizontal();
    }
}
