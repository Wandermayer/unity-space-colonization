using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour {
  void Start() {}

  void Update() {
    if (Input.GetKeyUp("1")) { SceneManager.LoadScene(0); }
    if (Input.GetKeyUp("2")) { SceneManager.LoadScene(1); }
    if (Input.GetKeyUp("3")) { SceneManager.LoadScene(2); }
    if (Input.GetKeyUp("4")) { SceneManager.LoadScene(3); }
    if (Input.GetKeyUp("5")) { SceneManager.LoadScene(4); }
  }
}
