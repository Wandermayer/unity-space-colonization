using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(GrowthManager))]
public class GrowthManagerEditor : Editor {
  // private SerializedProperty AttractionDistance;
  // private SerializedProperty KillDistance;
  // private SerializedProperty SegmentLength;
  // private SerializedProperty attractorContainer;

  private void OnEnable() {
    // AttractionDistance = serializedObject.FindProperty("AttractionDistance");
    // KillDistance = serializedObject.FindProperty("KillDistance");
    // SegmentLength = serializedObject.FindProperty("SegmentLength");
    // attractorContainer = serializedObject.FindProperty("attractorContainer");
  }

  public override void OnInspectorGUI() {
    serializedObject.UpdateIfDirtyOrScript();

    // AttractionDistance = EditorGUILayout.Slider("Attraction distance", 50f, 1f, 100f);
    // KillDistance = EditorGUILayout.Slider("Kill distance", 50f, 1f, 100f);
    // SegmentLength = EditorGUILayout.Slider("Segment length", 50f, 1f, 100f);
    // attractorContainer = EditorGUILayout.ObjectField(attractorContainer, "Attractor container");

    serializedObject.ApplyModifiedProperties();
  }
}