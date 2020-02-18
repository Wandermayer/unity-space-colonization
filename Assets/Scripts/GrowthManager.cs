﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using DataStructures.ViliWonka.KDTree;
using Valve.VR;

public class GrowthManager : MonoBehaviour {
  public SteamVR_Action_Boolean triggerClick;
  public Transform controller;

  public Material material;
  public Transform InputRootNode;

  public float AttractionDistance;
  public float KillDistance;
  public float SegmentLength;
  public int MaxAttractorAge;
  public bool EnableAttractorAging;

  public float MinimumRadius;
  public float MaximumRadius;
  public float RadiusIncrement;
  public bool EnableCanalization;

  public float MaxIterations;
  private int _numIterations;
  public bool EnableMaxIterations;

  public int AttractorRaycastAttempts;
  public float AttractorSurfaceOffset;
  public float AttractorGizmoRadius;

  public enum AttractorsType {NONE, MESH, GRID, SPHERE};
  public AttractorsType attractorsType;

  public enum AttractorRaycastingType {OUTWARDS, INWARDS};
  public AttractorRaycastingType attractorRaycastingType;

  public enum RootNodeType {INPUT, ORIGIN, MESH_SINGLE, MESH_THREE};
  public RootNodeType rootNodeType;

  // Bounds and obstacles
  private GameObject _bounds;
  private bool boundsEnabled;
  private List<GameObject> _obstacles;
  private RaycastHit[] hits;  // results of raycast hit-tests against bounds and obstacles

  public bool isPaused = false;

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

  /*
  =================================
    RESET
    Called when script component
    is reset in Editor
  =================================
  */
  void Reset() {
    AttractionDistance = .3f;
    KillDistance = .05f;
    SegmentLength = .04f;
    MaxAttractorAge = 10;
    EnableAttractorAging = false;

    MinimumRadius = .003f;
    MaximumRadius = .03f;
    RadiusIncrement = .00005f;
    EnableCanalization = true;

    MaxIterations = 10;
    _numIterations = 0;
    EnableMaxIterations = false;

    AttractorRaycastAttempts = 200000;
    AttractorSurfaceOffset = .01f;
    AttractorGizmoRadius = .05f;

    attractorsType = AttractorsType.MESH;
    attractorRaycastingType = AttractorRaycastingType.INWARDS;
    rootNodeType = RootNodeType.MESH_SINGLE;

    InputRootNode = GameObject.Find("Root node").transform;
  }


  /*
  ========================
    INITIAL SETUP
  ========================
  */
  void Start() {
    SetupBounds();        // retrieve any active bounds objects
    SetupObstacles();     // retrieve any active obstacle objects

    SetupMeshes();        // create child GameObjects for veins and TubeRenderer
    CreateAttractors();   // generate attractors based on mode
    CreateRootVeins();    // generate root vein nodes
    BuildSpatialIndex();  // initialize the node spatial index

    // If a trigger action has been, begin listening for it
    if(triggerClick != null) {
      triggerClick.AddOnStateDownListener(TriggerClick, SteamVR_Input_Sources.Any);
    }
  }


    /*
    ========================
      BOUNDS
    ========================
    */
    void SetupBounds() {
      _bounds = GetAllChildren(GameObject.Find("Bounds"))[0];
      boundsEnabled = _bounds == null ? false : true;
    }


    /*
    ========================
      OBSTACLES
    ========================
    */
    void SetupObstacles() {
      _obstacles = GetAllChildren(GameObject.Find("Obstacles"));
    }


    /*
    ========================
      MESHES
    ========================
    */
    void SetupMeshes() {
      Profiler.BeginSample("SetupMeshes");

      // Remove all children (veins/tube) that can build up when switching between Editor and Game modes
      while(transform.childCount > 0) {
        DestroyImmediate(transform.GetChild(0).gameObject);
      }

      // Set up a separate GameObject to render the veins to
      veinsObject = new GameObject("Veins");
      veinsObject.transform.SetParent(gameObject.transform);
      veinsObject.AddComponent<MeshRenderer>();
      filter = veinsObject.AddComponent<MeshFilter>();
      filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
      veinsObject.GetComponent<Renderer>().material = material;

      // Set up the tube renderer
      tube = new GameObject("(Temporary) Tubes").AddComponent<TubeRenderer>();
      tube.transform.SetParent(gameObject.transform);

      Profiler.EndSample();
    }


    /*
    ========================
      ATTRACTORS
    ========================
    */
    void CreateAttractors() {
      Profiler.BeginSample("CreateAttractors");

      _attractors.Clear();

      switch(attractorsType) {
        case AttractorsType.NONE:
          break;

        // Create attractors on target mesh(es) using raycasting ------------------------------------
        case AttractorsType.MESH:
          CreateAttractorsOnMeshSurface();
          break;

        // Points in a 3D grid ----------------------------------------------------------------------
        case AttractorsType.GRID:
          float width = 1.5f;
          float height = .8f;
          float depth = 2f;

          int xResolution = 8;
          int yResolution = 6;
          int zResolution = 8;
          float jitterAmount = .1f;

          for(int x = 0; x <= xResolution; x++) {
            for(int y = 0; y <= yResolution; y++) {
              for(int z = 0; z <= zResolution; z++) {
                _attractors.Add(
                  new Attractor(
                    new Vector3(
                      x * (width/xResolution) - width/2 + Random.Range(-jitterAmount, jitterAmount),
                      y * (height/yResolution) - height/2 + Random.Range(-jitterAmount, jitterAmount),
                      z * (depth/zResolution) - depth/2 + Random.Range(-jitterAmount, jitterAmount)
                    )
                  )
                );
              }
            }
          }

          break;

        // Points in a sphere ----------------------------------------------------------------------
        case AttractorsType.SPHERE:
          for (int i = 0; i < 1000; i++) {
            _attractors.Add(new Attractor(Random.insideUnitSphere * .75f));
          }

          break;
      }

      Profiler.EndSample();
    }

      public void CreateAttractorsOnMeshSurface() {
        Profiler.BeginSample("CreateAttractorsOnMeshSurface");

        int hitCount = 0;

        for(int i=0; i<AttractorRaycastAttempts; i++) {
          RaycastHit hitInfo;

          Vector3 startingPoint = Vector3.zero;
          Vector3 targetPoint = Vector3.zero;

          switch(attractorRaycastingType) {
            // Inside-out raycasting
            case AttractorRaycastingType.OUTWARDS:
              startingPoint = new Vector3(0f,.5f,0);
              targetPoint = Random.onUnitSphere * 100f;
              break;

            // Outside-in raycasting
            case AttractorRaycastingType.INWARDS:
              startingPoint = Random.onUnitSphere * 100f;
              targetPoint = Random.onUnitSphere * .1f;
              break;
          }

          bool bHit = Physics.Raycast(
            startingPoint,
            targetPoint,
            out hitInfo,
            Mathf.Infinity,
            LayerMask.GetMask("Targets"),
            QueryTriggerInteraction.Ignore
          );

          if(bHit) {
            // How much distance should there be between attractor and hit surface?
            // offset = Random.Range(.015f, .16f);
            _attractors.Add(new Attractor(hitInfo.point + (hitInfo.normal * AttractorSurfaceOffset)));

            // Color rayColor;
            // rayColor = Color.red;
            // Debug.DrawLine(startingPoint, targetPoint, rayColor, 10f);

            hitCount++;
          }
        }

        // Debug.Log(hitCount + " hits");

        Profiler.EndSample();
      }


    /*
    ========================
      ROOT VEINS
    ========================
    */
    void CreateRootVeins() {
      Profiler.BeginSample("CreateRootVeins");

      _nodes.Clear();
      _rootNodes.Clear();

      switch(rootNodeType) {
        // Root node from provided transform ------------------------------------------------------
        case RootNodeType.INPUT:
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

          break;

        // Root node at origin --------------------------------------------------------------------
        case RootNodeType.ORIGIN:
          _rootNodes.Add(
            new Node(
              Vector3.zero,
              null,
              true,
              MinimumRadius
            )
          );

          break;

        // Random point(s) on mesh ----------------------------------------------------------------
        case RootNodeType.MESH_SINGLE:
        case RootNodeType.MESH_THREE:
          int numRoots = -1;

          if(rootNodeType == RootNodeType.MESH_SINGLE) { numRoots = 1; }
          else if(rootNodeType == RootNodeType.MESH_THREE) { numRoots = 3; }

          bool isHit = false;
          RaycastHit hitInfo;

          for(int i=0; i<numRoots; i++) {
            do {
              Vector3 startingPoint = Random.onUnitSphere * 5;
              Vector3 targetPoint = Random.onUnitSphere * .5f;

              isHit = Physics.Raycast(
                startingPoint,
                targetPoint,
                out hitInfo,
                Mathf.Infinity,
                LayerMask.GetMask("Targets"),
                QueryTriggerInteraction.Ignore
              );

              if(isHit) {
                _rootNodes.Add(
                  new Node(
                    hitInfo.point,
                    null,
                    true,
                    MinimumRadius
                  )
                );
              }
            } while(!isHit);
          }

          break;
      }

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

      Profiler.EndSample();
    }


  /*
  ========================
    MAIN PROGRAM LOOP
  ========================
  */
  void Update() {
    // Load scenes based on number key presses
    if(Input.GetKeyUp("1")) { SceneManager.LoadScene(0); }
    if(Input.GetKeyUp("2")) { SceneManager.LoadScene(1); }
    if(Input.GetKeyUp("3")) { SceneManager.LoadScene(2); }
    if(Input.GetKeyUp("4")) { SceneManager.LoadScene(3); }
    if(Input.GetKeyUp("5")) { SceneManager.LoadScene(4); }

    // Automatically pause when max iterations reached (if enabled)
    if(EnableMaxIterations && _numIterations > MaxIterations) {
      isPaused = true;

    // Toggle pause on "space"
    } else if(Input.GetKeyUp("space")) {
      isPaused = !isPaused;
    }

    // Reload the scene when "r" is pressed
    if(Input.GetKeyUp("r")) { ResetScene(); }

    // Stop iterations when paused
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

    _numIterations++;
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
        if(EnableCanalization) {
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

      // Remove any attractors that were flagged
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

      filter.sharedMesh.CombineMeshes(allMeshes.ToArray());

      Profiler.EndSample();
    }


      /*
      ========================
        BRANCH MESHES
      ========================
      */
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


      /*
      ========================
        PATCH MESHES
      ========================
      */
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


  /*
  ========================
    GIZMOS
  ========================
  */
  void OnDrawGizmos() {
    Profiler.BeginSample("OnDrawGizmos");

    // Draw a spheres for all attractors
    Gizmos.color = Color.yellow;
    foreach(Attractor attractor in _attractors) {
      Gizmos.DrawSphere(attractor.position, AttractorGizmoRadius);
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

    Profiler.EndSample();
  }


  /*
  ========================
    SPATIAL INDEX
  ========================
  */
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


  /*
  ========================
    HELPER FUNCTIONS
  ========================
  */
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

  public void GrowInEditor() {
    for(int i=0; i<10; i++) {
      Update();
    }
  }


  /*
  ========================
    SCENE
  ========================
  */
  public void ResetScene() {
    Debug.Log("Reloading scene ...");

    // Restart iteration counter
    _numIterations = 0;

    // Reset nodes
    _nodes.Clear();
    _rootNodes.Clear();
    CreateRootVeins();
    BuildSpatialIndex();

    // Reset and generate new attractors
    CreateAttractors();

    // Setup meshes
    SetupMeshes();
    CreateMeshes();

    SetupBounds();
    SetupObstacles();
  }


  /*
  ========================
    VR INPUT
  ========================
  */
  private void TriggerClick(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource) {
    for(int i=0; i<10; i++) {
      Attractor newAttractor = new Attractor(controller.position + Random.onUnitSphere * 2);
      newAttractor.age = 0;

      _attractors.Add(newAttractor);
    }

    Debug.Log("Nodes: " + _nodes.Count);
    Debug.Log("Attractors: " + _attractors.Count);
  }
}
