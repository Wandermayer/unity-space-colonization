using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using DataStructures.ViliWonka.KDTree;

public class GrowthManager : MonoBehaviour {
  public float AttractionDistance = 120f;
  public float KillDistance = 10f;
  public float SegmentLength = 10;

  public float MinimumRadius = 1f;
  public float MaximumRadius = 9f;
  public float RadiusIncrement = .05f;
  public bool canalizationEnabled;

  public GameObject AttractorsContainer;
  public GameObject Bounds;
  public GameObject Obstacles;

  private List<GameObject> _obstacles;
  private List<GameObject> _attractorObjects;

  private RaycastHit[] hits;

  bool isPaused = true;

  TubeRenderer tube;

  private List<Attractor> _attractors = new List<Attractor>();
  private List<Attractor> _attractorsToRemove = new List<Attractor>();
  private List<Node> _rootNodes = new List<Node>();
  private List<Node> _nodes = new List<Node>();
  private List<int> _nodesInAttractionZone = new List<int>();
  private List<Node> _nodesToAdd = new List<Node>();
  private List<List<Vector3>> _branches = new List<List<Vector3>>();
  private List<List<float>> _radii = new List<List<float>>();
  private List<CombineInstance> _branchMeshes = new List<CombineInstance>();

  private KDTree _nodeTree;               // spatial index of vein nodes
  private KDQuery query = new KDQuery();  // query object for spatial indices

  private GameObject veinsObject;
  private MeshFilter filter;

  void Start() {
    // Set up a mesh filter on this GameObject
    veinsObject = new GameObject("Veins");
    veinsObject.AddComponent<MeshRenderer>();
    filter = veinsObject.AddComponent<MeshFilter>();
    filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    veinsObject.GetComponent<Renderer>().material = Resources.Load<Material>("Bark_18");

    // Set up the tube renderer
    tube = new GameObject("(Temporary) Tubes").AddComponent<TubeRenderer>();

    // Get GameObjects provided through Inspector interface
    // _attractorObjects = GetAllChildren(AttractorsContainer);
    _obstacles = GetAllChildren(Obstacles);

    CreateAttractors();
    CreateRootVeins();

    BuildSpatialIndex();

    canalizationEnabled = true;
  }

    void CreateAttractors() {
      _attractors.Clear();

      // Points in a sphere
      for (int i = 0; i < 1500; i++) {
        _attractors.Add(new Attractor(Random.insideUnitSphere * 300));
      }

      // Points in a 3D grid
      // int spacing = 70;
      // int rowResolution = 8;
      // int colResolution = 15;
      // int depthResolution = 8;

      // for(int row = 0; row < rowResolution; row++) {
      //   for(int col = 0; col < colResolution; col++) {
      //     for(int depth = 0; depth < depthResolution; depth++) {
      //       _attractors.Add(
      //         new Attractor(
      //           new Vector3(
      //             col * spacing + Random.Range(-spacing/2, spacing/2),
      //             row * spacing + Random.Range(-spacing/2, spacing/2),
      //             depth * spacing + Random.Range(-spacing/2, spacing/2)
      //           )
      //         )
      //       );
      //     }
      //   }
      // }
    }

    void CreateRootVeins() {
      _nodes.Clear();

      // Single root vein
      _rootNodes.Add(
        new Node(
          // new Vector3((15*70)/2, (8*70)/2, (8*70)/2),
          Vector3.zero,
          null,
          true,
          4f
        )
      );

      // for(int i=0; i<3; i++) {
      //   _rootNodes.Add(
      //     new Node(
      //       Random.insideUnitSphere * 100,
      //       null,
      //       true,
      //       5f
      //     )
      //   );
      // }

      foreach(Node rootNode in _rootNodes) {
        _nodes.Add(rootNode);
      }
    }

  void Update() {
    if(Input.GetKeyUp("space")) { isPaused = !isPaused; }
    if(isPaused) { return; }

    // Reset lists of attractors that vein nodes were attracted to last cycle
    foreach(Node node in _nodes) {
      node.influencedBy.Clear();
    }

    // 1. Associate attractors with vein nodes =============================================================================
    AssociateAttractors();

    // 2. Add vein nodes onto every vein node that is being influenced ====================================================
    GrowNetwork();

    // 3. Remove attractors that have been reached by their vein nodes =====================================================
    PruneAttractors();

    // 4. Rebuild vein node spatial index with latest vein nodes ===========================================================
    BuildSpatialIndex();

    // 5. Generate tube meshes to render the vein network ==================================================================
    GenerateBranchMeshes();
  }

    void AssociateAttractors() {
      Profiler.BeginSample("AssociateAttractors");

      foreach(Attractor attractor in _attractors) {
        attractor.isInfluencing.Clear();
        attractor.isReached = false;
        _nodesInAttractionZone.Clear();

        // a. Open venation = closest vein node only ---------------------------------------------------------------------
        query.ClosestPoint(_nodeTree, attractor.position, _nodesInAttractionZone);

        // ii. If a vein node is found, associate it by pushing attractor ID to _nodeInfluencedBy
        if(_nodesInAttractionZone.Count > 0) {
          Node closestNode = _nodes[_nodesInAttractionZone[0]];
          float distance = (attractor.position - closestNode.position).sqrMagnitude;

          if(distance <= AttractionDistance * AttractionDistance) {
            closestNode.influencedBy.Add(attractor);

            if(distance > KillDistance * KillDistance) {
              attractor.isReached = false;
            } else {
              attractor.isReached = true;
            }
          }
        }

        // b. Closed venation = all vein nodes in relative neighborhood

      }

      Profiler.EndSample();
    }

    void GrowNetwork() {
      Profiler.BeginSample("GrowNetwork");

      _nodesToAdd.Clear();

      foreach(Node node in _nodes) {
        if(node.influencedBy.Count > 0) {
          // Calculate the average direction of the influencing attractors
          Vector3 averageDirection = GetAverageDirection(node, node.influencedBy);

          // Calculate a new node position
          Vector3 newNodePosition = node.position + averageDirection * SegmentLength;

          // Add a random jitter to reduce split sources
          newNodePosition += new Vector3(Random.Range(-1,1), Random.Range(-1,1), Random.Range(-1,1));

          // Bounds check --------------------------------------------------------------------------------------------------
          bool isInsideBounds = false;

          // Cast a ray from the new node's position to the center of the bounds mesh
          hits = Physics.RaycastAll(
            newNodePosition,  // starting point
            (Bounds.transform.position - newNodePosition).normalized,  // direction
            (int)Mathf.Round(Vector3.Distance(newNodePosition, Bounds.transform.position)), // maximum distance
            LayerMask.GetMask("Bounds") // layer containing colliders
          );

          // 0 = point is inside the bounds
          if(hits.Length == 0) {
            isInsideBounds = true;
          }

          // Obstacles check -----------------------------------------------------------------------------------------------
          bool isInsideAnyObstacles = false;

          foreach(GameObject obstacle in _obstacles) {
            if(obstacle.activeInHierarchy) {
              // Cast a ray rom the new node's position to the center of this obstacle mesh
              hits = Physics.RaycastAll(
                newNodePosition,
                (obstacle.transform.position - newNodePosition).normalized,
                (int)Mathf.Round(Vector3.Distance(newNodePosition, obstacle.transform.position)),
                LayerMask.GetMask("Obstacles")
              );

              // 0 = point is inside the obstacle
              if(hits.Length == 0) {
                isInsideAnyObstacles = true;
              }
            }
          }

          if(isInsideBounds && !isInsideAnyObstacles) {
            // Since this vein node is spawning a new one, it is no longer a tip
            node.isTip = false;

            // Create the new node
            Node newNode = new Node(
              newNodePosition,
              node,
              true,
              MinimumRadius
            );

            node.children.Add(newNode);
            _nodesToAdd.Add(newNode);
          }
        }
      }

      // Add in the new vein nodes that have been produced
      for(int i=0; i<_nodesToAdd.Count; i++) {
        Node currentNode = _nodesToAdd[i];

        _nodes.Add(currentNode);

        // Thicken the radius of every parent Node
        if(canalizationEnabled) {
          Profiler.BeginSample("Canalization");

          while(currentNode.parent != null) {
            if(currentNode.parent.radius + RadiusIncrement <= MaximumRadius) {
              currentNode.parent.radius += RadiusIncrement;
            }

            currentNode = currentNode.parent;
          }

          Profiler.EndSample();
        }
      }

      Profiler.EndSample();
    }

    void PruneAttractors() {
      Profiler.BeginSample("PruneAttractors");

      _attractorsToRemove.Clear();

      foreach(Attractor attractor in _attractors) {
        // a. Open venation = as soon as the closest vein node enters KillDistance
        if(attractor.isReached) {
          _attractorsToRemove.Add(attractor);
        }

        // b. Closed venation = only when all vein nodes in relative neighborhood enter KillDistance
      }

      foreach(Attractor attractor in _attractorsToRemove) {
        _attractors.Remove(attractor);
      }

      Profiler.EndSample();
    }

    void GenerateBranchMeshes() {
      Profiler.BeginSample("GenerateBranchMeshes");

      _branches.Clear();
      _radii.Clear();

      foreach(Node rootNode in _rootNodes) {
        GetBranch(rootNode);
      }

      CombineInstance[] combineInstances = new CombineInstance[_branches.Count];

      int t = 0;
      foreach(List<Vector3> branch in _branches) {
        tube.points = new Vector3[branch.Count];
        tube.radiuses = new float[branch.Count];

        for(int j=0; j<branch.Count; j++) {
          tube.points[j] = branch[j];
          tube.radiuses[j] = _radii[t][j];
        }

        tube.ForceUpdate();

        combineInstances[t].mesh = Instantiate(tube.mesh);
        combineInstances[t].transform = tube.transform.localToWorldMatrix;

        t++;
      }

      filter.mesh.CombineMeshes(combineInstances);

      Profiler.EndSample();
    }

      private void GetBranch(Node startingNode) {
        Profiler.BeginSample("GetBranch");

        List<Vector3> thisBranch = new List<Vector3>();
        List<float> thisRadii = new List<float>();
        Node currentNode = startingNode;

        if(currentNode.parent != null) {
          thisBranch.Add(currentNode.parent.position);
          thisRadii.Add(currentNode.parent.radius);
        }

        thisBranch.Add(currentNode.position);
        thisRadii.Add(currentNode.radius);

        while(currentNode != null && currentNode.children.Count > 0) {
          if(currentNode.children.Count == 1) {
            thisBranch.Add(currentNode.children[0].position);
            thisRadii.Add(currentNode.children[0].radius);

            currentNode = currentNode.children[0];
          } else {
            foreach(Node childNode in currentNode.children) {
              GetBranch(childNode);
            }

            currentNode = null;
          }
        }

        _branches.Add(thisBranch);
        _radii.Add(thisRadii);

        Profiler.EndSample();
      }

  void OnDrawGizmos() {
    Profiler.BeginSample("OnDrawGizmos");

    if(Application.isPlaying) {
      // Draw a spheres for all attractors
      Gizmos.color = Color.yellow;
      foreach(Attractor attractor in _attractors) {
        Gizmos.DrawSphere(attractor.position, 1);
      }

      // Draw lines to connect each vein node
      // Gizmos.color = Random.ColorHSV();
      Gizmos.color = Color.white;
      foreach(Node node in _nodes) {
        if(node.parent != null) {
          Gizmos.DrawLine(node.parent.position, node.position);
        }

        // Gizmos.DrawSphere(node.position, 1);
      }
    }

    // TODO: draw bounding box

    Profiler.EndSample();
  }

  private void BuildSpatialIndex() {
    Profiler.BeginSample("BuildSpatialIndex");

    // Create spatial index using _nodePositions
    List<Vector3> nodePositions = new List<Vector3>();

    foreach(Node node in _nodes) {
      nodePositions.Add(node.position);
    }

    _nodeTree = new KDTree(nodePositions.ToArray());

    Profiler.EndSample();
  }

  private Vector3 GetAverageDirection(Node node, List<Attractor> attractors) {
    Profiler.BeginSample("GetAverageDirection");

    Vector3 averageDirection = new Vector3(0,0,0);

    foreach(Attractor attractor in attractors) {
      Vector3 direction = attractor.position - node.position;
      direction.Normalize();

      averageDirection += direction;
    }

    averageDirection /= attractors.Count;
    averageDirection.Normalize();

    Profiler.EndSample();

    return averageDirection;
  }

  private List<GameObject> GetAllChildren(GameObject parentObject) {
    List<GameObject> children = new List<GameObject>();

    for(int i=0; i<parentObject.transform.childCount; i++) {
      children.Add(parentObject.transform.GetChild(i).gameObject);
    }

    return children;
  }
}
