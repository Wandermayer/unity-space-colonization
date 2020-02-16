using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(GrowthManager))]
public class GrowthManagerEditor : Editor {
  // public override void OnInspectorGUI() {
  //   GrowthManager manager = (GrowthManager)target;

  //   manager.InputRootNode = (Transform)EditorGUILayout.ObjectField("Custom root node", manager.InputRootNode, typeof(Transform), true);

  //   manager.AttractionDistance = EditorGUILayout.Slider("Attraction distance", manager.AttractionDistance, 10f, 200f);
  //   manager.KillDistance = EditorGUILayout.Slider("Kill distance", manager.KillDistance, 1f, 50f);
  //   manager.SegmentLength = EditorGUILayout.Slider("Segment length", manager.SegmentLength, 1f, 100f);
  // }
}