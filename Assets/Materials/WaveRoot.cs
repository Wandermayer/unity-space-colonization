using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*
 Add to root node with "SpaceWave" material attatched to set wave "origin" to root node's location at startup
 or call public function "WaveRoot.ResetOrigin()" to re-set origin on wave material as needed, eg. when resetting sim with new location

*/
public class WaveRoot : MonoBehaviour
{
    [SerializeField] Material mat;
    // Start is called before the first frame update
    void Start()
    {
        mat = GetComponent<Renderer>().material;
        mat.SetVector("_origin", transform.position); 
    }

    public void ResetOrigin()
    {
        mat.SetVector("_origin", transform.position);
    }
}
