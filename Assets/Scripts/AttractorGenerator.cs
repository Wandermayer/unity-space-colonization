using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttractorGenerator : MonoBehaviour {
  public int NumAttempts;
  private int hitCount;

  void Start() {
    NumAttempts = 100000;
    hitCount = 0;

    CreateAttractors();
  }

  public void CreateAttractors() {
    GameObject attractorsContainer = new GameObject("Attractors");
    float offset;

    for(int i=0; i<NumAttempts; i++) {
      RaycastHit hit;

      // Outside in raycasting
      Vector3 startingPoint = Random.onUnitSphere * 5;
      Vector3 targetPoint = Random.onUnitSphere * .5f;

      // Inside out raycasting
      // Vector3 startingPoint = new Vector3(0f,.5f,0);
      // Vector3 targetPoint = Random.onUnitSphere * 10f;

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

        // How much distance should there be between attractor and hit surface?
        // offset = Random.Range(.015f, .16f);
        // offset = .015f;
        offset = 0f;

        // Create a GameObject for this attractor, which will be consumed and destroyed by GrowthManager
        GameObject attractor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        attractor.transform.SetParent(attractorsContainer.transform);
        attractor.transform.localScale = new Vector3(.01f,.01f,.01f);
        attractor.transform.position = hit.point + (hit.normal * offset);

        // Color rayColor;
        // rayColor = Color.red;
        // Debug.DrawLine(startingPoint, targetPoint, rayColor, 100f);
      }
    }

    // Debug.Log(hitCount + " hits");
  }
}
