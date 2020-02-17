using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(GrowthManager))]
public class GrowthManagerEditor : Editor {
  public override void OnInspectorGUI() {
    GrowthManager manager = (GrowthManager)target;

    DrawDefaultInspector();

    EditorGUILayout.BeginHorizontal();

      if(GUILayout.Button("Grow")) {
        manager.GrowInEditor();
      }

      if(GUILayout.Button("Fetch targets")) {
        manager.FetchTargetMeshes();
      }

      if(GUILayout.Button("Reset")) {
        manager.ResetScene();
      }

    EditorGUILayout.EndHorizontal();

    EditorGUILayout.BeginHorizontal();

      if(GUILayout.Button("1")) { manager.LoadPreset1(); }
      if(GUILayout.Button("2")) { manager.LoadPreset2(); }
      if(GUILayout.Button("3")) { manager.LoadPreset3(); }
      if(GUILayout.Button("4")) { manager.LoadPreset4(); }
      if(GUILayout.Button("5")) { manager.LoadPreset5(); }

    EditorGUILayout.EndHorizontal();
  }
}