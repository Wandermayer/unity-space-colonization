using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttractorGenerator : MonoBehaviour {
  public int NumAttractors;


  void Start() {

  }

  void Update() {

  }

  public void CreateAttractors() {
    GameObject attractorsContainer = new GameObject("Attractors");

    for(int i=0; i<NumAttractors; i++) {
      RaycastHit hit;

      /*
      bool bHit = Physics.Raycast(
        // pick a point outside the mesh
        // pick a center point inside the mesh
        out hit,
        // distance
        LayerMask.GetMask("Target") // layer mask
      );

      if(bHit) {
        GameObject attractor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        attractor.transform.SetParent(attractorsContainer);
        attractor.transform.position = hit.point;
      }
      */
    }
  }
}
