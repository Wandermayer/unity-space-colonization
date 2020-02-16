﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using DataStructures.ViliWonka.KDTree;

public class GrowthManager : MonoBehaviour {
  public Transform InputRootNode;

  public float AttractionDistance;
  public float KillDistance;
  public float SegmentLength;
  public int MaxAttractorAge;
  public bool EnableAttractorAging;

  public float MinimumRadius;
  public float MaximumRadius;
  public float RadiusIncrement;
  public bool canalizationEnabled;

  private GameObject _bounds;
  private List<GameObject> _obstacles;
  private List<GameObject> _attractorObjects;

  private RaycastHit[] hits;

  bool isPaused = true;
  bool boundsEnabled;

  // Attractors
  private List<Attractor> _attractors = new List<Attractor>();
  private List<Attractor> _attractorsToRemove = new List<Attractor>();

  // Nodes
  private List<Node> _rootNodes = new List<Node>();
  private List<Node> _nodes = new List<Node>();
  private List<int> _nodesInAttractionZone = new List<int>();
  private List<Node> _nodesToAdd = new List<Node>();

  private KDTree _nodeTree;               // spatial index of vein nodes
  private KDQuery query = new KDQuery();  // query object for spatial indices

  // Branch meshes and data
  private List<List<Vector3>> _branches = new List<List<Vector3>>();
  private List<List<float>> _branchRadii = new List<List<float>>();
  private List<CombineInstance> _branchMeshes = new List<CombineInstance>();

  // Patch meshes and data
  private List<List<Vector3>> _patches = new List<List<Vector3>>();
  private List<List<float>> _patchRadii = new List<List<float>>();
  private List<CombineInstance> _patchMeshes = new List<CombineInstance>();

  // Tube renderer and output mesh
  private TubeRenderer tube;
  private GameObject veinsObject;
  private MeshFilter filter;

  void Start() {
    AttractionDistance = .3f;
    KillDistance = .05f;
    SegmentLength = .04f;
    MaxAttractorAge = 1000;
    EnableAttractorAging = false;

    MinimumRadius = .003f;
    MaximumRadius = .015f;
    RadiusIncrement = .00005f;

    canalizationEnabled = true;

    // Set up a separate GameObject to render the veins to
    veinsObject = new GameObject("Veins");
    veinsObject.AddComponent<MeshRenderer>();
    filter = veinsObject.AddComponent<MeshFilter>();
    filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    veinsObject.GetComponent<Renderer>().material = Resources.Load<Material>("Bark_18");

    // Set up the tube renderer
    tube = new GameObject("(Temporary) Tubes").AddComponent<TubeRenderer>();

    // Retrieve any active bounds meshes
    _bounds = GetAllChildren(GameObject.Find("Bounds"))[0];
    boundsEnabled = _bounds == null ? false : true;

    // Retrieve any externally-generated attractors or obstacles
    _attractorObjects = GetAllChildren(GameObject.Find("Attractors"));
    _obstacles = GetAllChildren(GameObject.Find("Obstacles"));

    CreateAttractors();
    CreateRootVeins();

    BuildSpatialIndex();
  }

    void CreateAttractors() {
      _attractors.Clear();

      // Points in a sphere
      // for (int i = 0; i < 1000; i++) {
      //   _attractors.Add(new Attractor(Random.insideUnitSphere * .75f));
      // }

      // Points in a 3D grid
      // int rowResolution = 6;
      // int colResolution = 12;
      // int depthResolution = 6;
      // float jitterAmount = .025f;

      // for(int row = 0; row <= rowResolution; row++) {
      //   for(int col = 0; col <= colResolution; col++) {
      //     for(int depth = 0; depth <= depthResolution; depth++) {
      //       _attractors.Add(
      //         new Attractor(
      //           new Vector3(
      //             col * (1f/colResolution) - .5f + Random.Range(-jitterAmount, jitterAmount),
      //             row * (.5f/rowResolution) - .25f + Random.Range(-jitterAmount, jitterAmount),
      //             depth * (.5f/depthResolution) - .25f + Random.Range(-jitterAmount, jitterAmount)
      //           )
      //         )
      //       );
      //     }
      //   }
      // }

      // Create Attractors from GameObjects created by AttractorGenerator script
      foreach(GameObject attractorObject in _attractorObjects) {
        Attractor attractor = new Attractor(attractorObject.transform.position);
        attractor.age = EnableAttractorAging ? 0 : -1;

        _attractors.Add(attractor);
        Destroy(attractorObject);
      }
    }

    void CreateRootVeins() {
      _nodes.Clear();
      _rootNodes.Clear();

      // ROOT NODE FROM PROVIDED TRANSFORM ----------------------
      if(InputRootNode != null) {
        _rootNodes.Add(
          new Node(
            InputRootNode.position,
            null,
            true,
            MinimumRadius
          )
        );
      }

      // RANDOM POINT ON MESH -----------------------------------
      // bool isHit = false;
      // RaycastHit hitInfo;

      // for(int i=0; i<4; i++) {
        // do {
        //   Vector3 startingPoint = Random.onUnitSphere * 5;
        //   Vector3 targetPoint = Random.onUnitSphere * .5f;

        //   isHit = Physics.Raycast(
        //     startingPoint,
        //     targetPoint,
        //     out hitInfo,
        //     Mathf.Infinity,
        //     LayerMask.GetMask("Targets"),
        //     QueryTriggerInteraction.Ignore
        //   );

        //   if(isHit) {
        //     _rootNodes.Add(
        //       new Node(
        //         hitInfo.point,
        //         null,
        //         true,
        //         MinimumRadius
        //       )
        //     );
        //   }
        // } while(!isHit);
      // }

      // ORIGIN -------------------------------------------------
      // _rootNodes.Add(
      //   new Node(
      //     Vector3.zero,
      //     null,
      //     true,
      //     MinimumRadius
      //   )
      // );

      // ROCKS --------------------------------------------------
      // _rootNodes.Add(
      //   new Node(
      //     new Vector3(.7f,0f,0f),
      //     null,
      //     true,
      //     MinimumRadius
      //   )
      // );

      // HEAD ---------------------------------------------------
      // On surface
      // _rootNodes.Add(
      //   new Node(
      //     // new Vector3(0f,.4f,0f), // top of skull
      //     new Vector3(-.4f, -1f, -.2f), // cheek
      //     null,
      //     true,
      //     MinimumRadius
      //   )
      // );

      // Inside
      // _rootNodes.Add(
      //   new Node(
      //     new Vector3(-1.33f,0f,0f),
      //     null,
      //     true,
      //     MinimumRadius
      //   )
      // );

      // _rootNodes.Add(
      //   new Node(
      //     new Vector3(1.33f,0f,0f),
      //     null,
      //     true,
      //     MinimumRadius
      //   )
      // );

      // SPHERE ----------------------------------------------
      // for(int i=0; i<3; i++) {
      //   _rootNodes.Add(
      //     new Node(
      //       Random.insideUnitSphere,
      //       null,
      //       true,
      //       MinimumRadius
      //     )
      //   );
      // }

      // Add root nodes to node tree
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
    CreateMeshes();
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
          // newNodePosition += new Vector3(Random.Range(-.0001f,.0001f), Random.Range(-.0001f,.0001f), Random.Range(-.0001f,.0001f));

          // Bounds check --------------------------------------------------------------------------------------------------
          bool isInsideBounds = boundsEnabled ? false : true;

          if(boundsEnabled) {
            // Cast a ray from the new node's position to the center of the bounds mesh
            hits = Physics.RaycastAll(
              newNodePosition,  // starting point
              (_bounds.transform.position - newNodePosition).normalized,  // direction
              (int)Mathf.Round(Vector3.Distance(newNodePosition, _bounds.transform.position)),  // maximum distance
              LayerMask.GetMask("Bounds") // layer containing colliders
            );

            // 0 = point is inside the bounds
            if(hits.Length == 0) {
              isInsideBounds = true;
            }
          }

          // Obstacles check -----------------------------------------------------------------------------------------------
          bool isInsideAnyObstacles = false;

          foreach(GameObject obstacle in _obstacles) {
            if(obstacle.activeInHierarchy) {
              // Cast a ray from the new node's position to the center of this obstacle mesh
              hits = Physics.RaycastAll(
                newNodePosition,  // starting point
                (obstacle.transform.position - newNodePosition).normalized,   // direction
                (int)Mathf.Ceil(Vector3.Distance(newNodePosition, obstacle.transform.position)),  // maximum distance
                LayerMask.GetMask("Obstacles")  // layer containing obstacles
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
        // Increment attractor age
        if(attractor.age != -1) {
          attractor.age += 1;
        }

        // a. Open venation = as soon as the closest vein node enters KillDistance
        if(attractor.isReached || (attractor.age != -1 && attractor.age > MaxAttractorAge)) {
          _attractorsToRemove.Add(attractor);
        }

        // b. Closed venation = only when all vein nodes in relative neighborhood enter KillDistance
      }

      foreach(Attractor attractor in _attractorsToRemove) {
        _attractors.Remove(attractor);
      }

      Profiler.EndSample();
    }

    void CreateMeshes() {
      Profiler.BeginSample("CreateMeshes");

      List<CombineInstance> branchMeshes = GetBranchMeshes();
      List<CombineInstance> patchMeshes = GetPatchMeshes();
      List<CombineInstance> allMeshes = new List<CombineInstance>();

      allMeshes.AddRange(branchMeshes);
      allMeshes.AddRange(patchMeshes);

      filter.mesh.CombineMeshes(allMeshes.ToArray());

      Profiler.EndSample();
    }

    List<CombineInstance> GetBranchMeshes() {
      Profiler.BeginSample("GetBranchMeshes");

      _branches.Clear();
      _branchRadii.Clear();

      // Recursively populate the _branches array
      foreach(Node rootNode in _rootNodes) {
        GetBranch(rootNode);
      }

      List<CombineInstance> combineInstances = new List<CombineInstance>();
      int t = 0;

      // Create continuous tube meshes for each branch
      foreach(List<Vector3> branch in _branches) {
        tube.points = new Vector3[branch.Count];
        tube.radiuses = new float[branch.Count];

        for(int j=0; j<branch.Count; j++) {
          tube.points[j] = branch[j];
          tube.radiuses[j] = _branchRadii[t][j];
        }

        tube.ForceUpdate();

        CombineInstance cb = new CombineInstance();
        cb.mesh = Instantiate(tube.mesh);  // Instantiate is expensive AF - needs improvement!
        cb.transform = tube.transform.localToWorldMatrix;
        combineInstances.Add(cb);

        t++;
      }

      Profiler.EndSample();

      return combineInstances;
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
        _branchRadii.Add(thisRadii);

        Profiler.EndSample();
      }

    List<CombineInstance> GetPatchMeshes() {
      Profiler.BeginSample("GetPatchMeshes");

      _patches.Clear();
      _patchRadii.Clear();

      // Recursively populate the _patches array
      foreach(Node rootNode in _rootNodes) {
        GetPatches(rootNode);
      }

      List<CombineInstance> combineInstances = new List<CombineInstance>();
      int t = 0;

      // Create continuous tube meshes for each patch
      foreach(List<Vector3> patch in _patches) {
        tube.points = new Vector3[patch.Count];
        tube.radiuses = new float[patch.Count];

        for(int j=0; j<patch.Count; j++) {
          tube.points[j] = patch[j];
          tube.radiuses[j] = _patchRadii[t][j];
        }

        tube.ForceUpdate();

        CombineInstance cb = new CombineInstance();
        cb.mesh = Instantiate(tube.mesh);  // Instantiate is expensive AF - needs improvement!
        cb.transform = tube.transform.localToWorldMatrix;
        combineInstances.Add(cb);

        t++;
      }

      Profiler.EndSample();

      return combineInstances;
    }

      private void GetPatches(Node startingNode) {
        Profiler.BeginSample("GetPatches");

        Node currentNode = startingNode;
        List<Vector3> thisPatch;
        List<float> thisRadii;

        while(currentNode != null && currentNode.children.Count > 0) {
          if(currentNode.children.Count == 1) {
            currentNode = currentNode.children[0];

          } else if(currentNode.children.Count > 1) {
            Node previousNode = currentNode.parent == null ? currentNode : currentNode.parent;

            foreach(Node nextNode in currentNode.children) {
              thisPatch = new List<Vector3>();
              thisRadii = new List<float>();

              thisPatch.Add(previousNode.position);
              thisPatch.Add(currentNode.position);
              thisPatch.Add(nextNode.position);

              thisRadii.Add(previousNode.radius);
              thisRadii.Add(currentNode.radius);
              thisRadii.Add(nextNode.radius);

              _patches.Add(thisPatch);
              _patchRadii.Add(thisRadii);

              GetPatches(nextNode);
            }

            currentNode = null;
          }
        }

        Profiler.EndSample();
      }

  void OnDrawGizmos() {
    Profiler.BeginSample("OnDrawGizmos");

    if(Application.isPlaying) {
      // Draw a spheres for all attractors
      Gizmos.color = Color.yellow;
      foreach(Attractor attractor in _attractors) {
        Gizmos.DrawSphere(attractor.position, .005f);
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
