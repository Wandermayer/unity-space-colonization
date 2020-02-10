using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttractorGenerator : MonoBehaviour {
  public int NumAttempts;
  private int hitCount;

  void Start() {
    NumAttempts = 25000;
    hitCount = 0;
    CreateAttractors();
  }

  void Update() {

  }

  public void CreateAttractors() {
    GameObject attractorsContainer = new GameObject("Attractors");

    for(int i=0; i<NumAttempts; i++) {
      RaycastHit hit;
      Vector3 startingPoint = Random.onUnitSphere * 2;
      Vector3 targetPoint = Random.onUnitSphere * .5f;

      bool bHit = Physics.Raycast(
        startingPoint,
        targetPoint,
        out hit,
        Mathf.Infinity,
        LayerMask.GetMask("Targets"),
        QueryTriggerInteraction.Ignore
      );

      if(bHit) {
        hitCount++;
        GameObject attractor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        attractor.transform.SetParent(attractorsContainer.transform);
        attractor.transform.localScale = new Vector3(.01f,.01f,.01f);
        attractor.transform.position = hit.point;

        // Color rayColor;
        // rayColor = Color.red;
        // Debug.DrawLine(startingPoint, targetPoint, rayColor, 100f);
      }
    }

    Debug.Log(hitCount + " hits");
  }
}
