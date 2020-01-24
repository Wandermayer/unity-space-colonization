using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GrowthManager : MonoBehaviour {
  Network network;

  /**
    Called once at start of program
    ===============================
  */
  void Start() {
    network = new Network();
  }


  /**
    Update is called once per frame
    ===============================
  */
  void Update() {
    network.Update();
  }

  void OnDrawGizmos() {
    if(Application.isPlaying) {
      network.DrawGizmos();
    }
  }
}
