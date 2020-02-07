using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(GrowthManager))]
public class GrowthManagerEditor : Editor {
  public override void OnInspectorGUI() {
    GrowthManager manager = (GrowthManager)target;

    manager.AttractorsContainer = (GameObject)EditorGUILayout.ObjectField("Attractors container", manager.AttractorsContainer, typeof(GameObject), true);
    manager.Bounds = (GameObject)EditorGUILayout.ObjectField("Bounds object", manager.Bounds, typeof(GameObject), true);
    manager.Obstacles = (GameObject)EditorGUILayout.ObjectField("Obstacles container", manager.Obstacles, typeof(GameObject), true);

    if(GUILayout.Button("Generate attractors")) {
      Debug.Log("test");
    }

    manager.AttractionDistance = EditorGUILayout.Slider("Attraction distance", manager.AttractionDistance, 10f, 200f);
    manager.KillDistance = EditorGUILayout.Slider("Kill distance", manager.KillDistance, 1f, 50f);
    manager.SegmentLength = EditorGUILayout.Slider("Segment length", manager.SegmentLength, 1f, 100f);
  }
}