using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DataStructures.ViliWonka.KDTree;

public class GrowthManager : MonoBehaviour {
  float AttractionDistance = 50f;
  float KillDistance = 10f;
  float SegmentLength = 10;
  float RadiusIncrement = .01f;

  int timeToRun = 10;
  bool branchesCompiled = false;

  GameObject attractorContainer;

  private List<Attractor> _attractors;
  private List<Node> _nodes;
  private List<List<Vector3>> _branches;

  private List<GameObject> _nodeObjects;

  private KDTree _nodeTree;               // spatial index of vein nodes
  private KDTree _tipTree;                // spatial index of vein tip nodes
  private KDQuery query = new KDQuery();  // query object for spatial indices

  private MeshFilter filter;

  void Start() {
    gameObject.AddComponent<MeshRenderer>();
    filter = gameObject.AddComponent<MeshFilter>();

    // Initialize attractor variables
    _attractors = new List<Attractor>();

    // Populate attractors
    // for (int i = 0; i < 3000; i++) {
    //   _attractors.Add(new Attractor(Random.insideUnitSphere * 400));
    // }

    int spacing = 40;
    int rowResolution = 30;
    int colResolution = 100;

    for(int row = 0; row < rowResolution; row++) {
      for(int col = 0; col < colResolution; col++) {
        _attractors.Add(
          new Attractor(
            new Vector3(
              col * spacing + Random.Range(-spacing/2, spacing/2),
              row * spacing + Random.Range(-spacing/2, spacing/2),
              0
            )
          )
        );
      }
    }

    // Initialize node variables
    _nodes = new List<Node>();

    // Add a single vein node at the origin to seed growth
    _nodes.Add(
      new Node(
        // new Vector3((colResolution*spacing)/2,(rowResolution*spacing)/2,0),
        new Vector3(0,(rowResolution*spacing)/2,0),
        // Vector3.zero,
        null,
        true,
        1
      )
    );

    // Build the vein node spatial index for the first time
    BuildSpatialIndex();
  }

  void Update() {
    // if(Time.time < timeToRun) {

      // Reset lists of attractors that vein nodes were attracted to last cycle
      foreach(Node node in _nodes) {
        node.influencedBy.Clear();
      }

      // 1. Associate attractors with vein nodes ===========================================================================
      foreach(Attractor attractor in _attractors) {
        attractor.isInfluencing.Clear();
        attractor.isReached = false;
        List<int> nodesInAttractionZone = new List<int>();

        // a. Open venation = closest vein node only

          // i. Query the vein node spatial index for nearest vein node within AttractionDistance (will return index)
          // query.Radius(_nodeTree, attractorPosition, AttractionDistance, nodesInAttractionZone);
          query.ClosestPoint(_nodeTree, attractor.position, nodesInAttractionZone);

          // TODO: get the nearby nodes using the physics engine
          // Collider[] nearbyNodes = Physics.OverlapSphere(attractor.position, AttractionDistance);

          // ii. If a vein node is found, associate it by pushing attractor ID to _nodeInfluencedBy
          if(nodesInAttractionZone.Count > 0) {
            Node closestNode = null;

            float smallestDistanceSqr = AttractionDistance * AttractionDistance;

            foreach(int nodeID in nodesInAttractionZone) {
              float distance = (attractor.position - _nodes[nodeID].position).sqrMagnitude;

              if(distance < smallestDistanceSqr) {
                closestNode = _nodes[nodeID];
                smallestDistanceSqr = distance;
              }
            }

            if(closestNode != null) {
              closestNode.influencedBy.Add(attractor);

              if((attractor.position - closestNode.position).sqrMagnitude > KillDistance * KillDistance) {
                attractor.isReached = false;
              } else {
                attractor.isReached = true;
              }
            }
          }

        // b. Closed venation = all vein nodes in relative neighborhood

      }

      // 2. Add vein nodes onto every vein node that is being influenced. =================================================
      List<Node> newNodes = new List<Node>();

      foreach(Node node in _nodes) {
        if(node.influencedBy.Count > 0) {
          // Calculate the average direction of the influencing attractors
          Vector3 averageDirection = GetAverageDirection(node, node.influencedBy);

          // Calculate a new node position
          Vector3 newNodePosition = node.position + averageDirection * SegmentLength;

          // Add a random jitter to reduce split sources
          // newNodePosition += new Vector3(Random.Range(-1,1), Random.Range(-1,1), Random.Range(-1,1));

          // Since this vein node is spawning a new one, it is no longer a tip
          node.isTip = false;

          Node newNode = new Node(newNodePosition, node, true, 1);

          node.children.Add(newNode);

          newNodes.Add(newNode);
        }
      }

      // Add in the new vein nodes that have been produced
      for(int i=0; i<newNodes.Count; i++) {
        Node currentNode = newNodes[i];

        _nodes.Add(currentNode);

        // Thicken the radius of every parent Node
        while(currentNode.parent != null) {
          currentNode.parent.radius += RadiusIncrement;
          currentNode = currentNode.parent;
        }
      }

      // 3. Remove attractors that have been reached by their vein nodes ==================================================
      List<Attractor> attractorsToRemove = new List<Attractor>();

      foreach(Attractor attractor in _attractors) {
        // a. Open venation = as soon as the closest vein node enters KillDistance
        if(attractor.isReached) {
          attractorsToRemove.Add(attractor);
        }

        // b. Closed venation = only when all vein nodes in relative neighborhood enter KillDistance
      }

      foreach(Attractor attractor in attractorsToRemove) {
        _attractors.Remove(attractor);
      }

      // 5. Rebuild vein node spatial index with latest vein nodes =========================================================
      BuildSpatialIndex();

    // } else {
    //   if(!branchesCompiled) {
        // 4. Perform vein thickening ========================================================================================
        _branches = new List<List<Vector3>>();
        GetBranch(_nodes[0]);

			  CombineInstance[] combineInstances = new CombineInstance[_branches.Count];

        int t = 0;
        foreach(List<Vector3> branch in _branches) {
          TubeRenderer tube = new GameObject().AddComponent<TubeRenderer>();
          tube.points = new Vector3[branch.Count];
			  	tube.radiuses = new float[branch.Count];

          for(int j=0; j<branch.Count; j++) {
            tube.points[j] = branch[j];
            tube.radiuses[j] = 10f;
          }

          tube.ForceUpdate();

          combineInstances[t].mesh = tube.mesh;
          combineInstances[t].transform = tube.transform.localToWorldMatrix;

          Destroy(tube.gameObject);

          t++;
        }

        filter.mesh.CombineMeshes(combineInstances);

        // GetComponent<Renderer>().material = new Material( Shader.Find( "Diffuse" ) );
			  // GetComponent<Renderer>().material.mainTexture = CreateTileTexture(2);

        branchesCompiled = true;
      // }
    // }
  }

  void OnDrawGizmos() {
    if(Application.isPlaying) {
      // Draw a spheres for all attractors
      foreach(Attractor attractor in _attractors) {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(attractor.position, 1);
      }

      // Draw lines to connect each vein node
      foreach(Node node in _nodes) {
        if(node.parent != null) {
          // Gizmos.color = Random.ColorHSV();
          Gizmos.color = Color.white;
          Gizmos.DrawLine(node.parent.position, node.position);
        }
      }
    }
  }

  private void BuildSpatialIndex() {
    // Create spatial index using _nodePositions
    List<Vector3> nodePositions = new List<Vector3>();

    foreach(Node node in _nodes) {
      nodePositions.Add(node.position);
    }

    _nodeTree = new KDTree(nodePositions.ToArray());

    // TODO: try only indexing the tips
    // Vector3[] test = _nodePositions.Where(node => _nodeIsTip[_nodePositions.IndexOf(node)]).ToArray();
    // _tipTree = new KDTree(test);
  }

  private Vector3 GetAverageDirection(Node node, List<Attractor> attractors) {
    Vector3 averageDirection = new Vector3(0,0,0);

    foreach(Attractor attractor in attractors) {
      Vector3 direction = attractor.position - node.position;
      direction.Normalize();

      averageDirection += direction;
    }

    averageDirection /= attractors.Count;
    averageDirection.Normalize();

    return averageDirection;
  }

  private Vector3 GetNodePositions(Node node) {
    return node.position;
  }

  private void GetBranch(Node startingNode) {
    List<Vector3> thisBranch = new List<Vector3>();
    Node currentNode = startingNode;

    if(currentNode.parent != null) {
      thisBranch.Add(currentNode.parent.position);
    }

    thisBranch.Add(currentNode.position);

    while(currentNode != null && currentNode.children.Count > 0) {
      if(currentNode.children.Count == 1) {
        thisBranch.Add(currentNode.children[0].position);
        currentNode = currentNode.children[0];
      } else {
        foreach(Node childNode in currentNode.children) {
          GetBranch(childNode);
        }

        currentNode = null;
      }
    }

    _branches.Add(thisBranch);
  }

  public static Texture2D CreateTileTexture( int sqrTileCount ) {
    Texture2D texture = new Texture2D( 256, 256 );
    Color32[] px = new Color32[ texture.width * texture.height ];
    int p = 0;
    for( int y=0; y<texture.height; y++ ){
      float yNorm = y / (float) texture.height;
      for( int x=0; x<texture.width; x++ ){
        float xNorm = x / (float) texture.width;
        bool isWhite = (int) (yNorm*sqrTileCount) % 2 == 0;
        if( (int) (xNorm*sqrTileCount) % 2 == 0 ) isWhite = !isWhite;
        px[p++] = isWhite ? new Color32(255,255,255,255) : new Color32(0,0,0,255);
      }
    }
    texture.SetPixels32(px);
    texture.wrapMode = TextureWrapMode.Clamp;
    texture.Apply();
    texture.hideFlags = HideFlags.HideAndDontSave;
    return texture;
  }
}
