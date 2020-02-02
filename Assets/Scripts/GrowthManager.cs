﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DataStructures.ViliWonka.KDTree;

public class GrowthManager : MonoBehaviour {
  float AttractionDistance = 70f;
  float KillDistance = 10f;
  float SegmentLength = 10;
  float RadiusIncrement = .01f;

  float timeToRun = 3f;
  float meshingInterval = .5f;
  float lastMeshTime = 0;
  bool meshCompiled = false;

  private List<Attractor> _attractors;
  private List<Node> _nodes;
  private List<List<Vector3>> _branches;
  private List<List<float>> _radii;
  private List<CombineInstance> _branchMeshes = new List<CombineInstance>();

  private KDTree _nodeTree;               // spatial index of vein nodes
  private KDQuery query = new KDQuery();  // query object for spatial indices

  private MeshFilter filter;

  void Start() {
    // Set up a mesh filter on this GameObject
    gameObject.AddComponent<MeshRenderer>();
    filter = gameObject.AddComponent<MeshFilter>();
    filter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    GetComponent<Renderer>().material = new Material(Shader.Find("Diffuse"));
    // GetComponent<Renderer>().material = Resources.Load("Bark_20", typeof(Material)) as Material;

    CreateAttractors();
    CreateRootVeins();

    BuildSpatialIndex();
  }

    void CreateAttractors() {
      _attractors = new List<Attractor>();

      // Points in a sphere
      // for (int i = 0; i < 1000; i++) {
      //   _attractors.Add(new Attractor(Random.insideUnitSphere * 300));
      // }

      // Points in a 3D grid
      int spacing = 60;
      int rowResolution = 5;
      int colResolution = 15;
      int depthResolution = 5;

      for(int row = 0; row < rowResolution; row++) {
        for(int col = 0; col < colResolution; col++) {
          for(int depth = 0; depth < depthResolution; depth++) {
            _attractors.Add(
              new Attractor(
                new Vector3(
                  col * spacing + Random.Range(-spacing/2, spacing/2),
                  row * spacing + Random.Range(-spacing/2, spacing/2),
                  depth * spacing + Random.Range(-spacing/2, spacing/2)
                )
              )
            );
          }
        }
      }
    }

    void CreateRootVeins() {
      _nodes = new List<Node>();

      // Single root vein
      Node rootNode = new Node(
        new Vector3((15*60)/2, (5*60)/2, (5*60)/2),
        // Vector3.zero,
        null,
        true,
        5f
      );

      _nodes.Add(rootNode);
    }

  void Update() {
    // if(Time.time < timeToRun) {
      // Reset lists of attractors that vein nodes were attracted to last cycle
      foreach(Node node in _nodes) {
        node.influencedBy.Clear();
      }

      // 1. Associate attractors with vein nodes ===========================================================================
      AssociateAttractors();

      // 2. Add vein nodes onto every vein node that is being influenced. =================================================
      GrowNetwork();

      // 3. Remove attractors that have been reached by their vein nodes ==================================================
      PruneAttractors();

      // 5. Rebuild vein node spatial index with latest vein nodes =========================================================
      BuildSpatialIndex();


    // } else {
    //   if(!meshCompiled) {
      // if(Time.time >= lastMeshTime + meshingInterval) {
        // Generate tube mesh from iterative method
        filter.mesh.CombineMeshes(_branchMeshes.ToArray());

        // Generate tube mesh recursively - smoother, but slower
        // GenerateBranchMeshes();

        // meshCompiled = true;
        // lastMeshTime = Time.time;
      // }
    // }
  }

    void AssociateAttractors() {
      foreach(Attractor attractor in _attractors) {
        attractor.isInfluencing.Clear();
        attractor.isReached = false;
        List<int> nodesInAttractionZone = new List<int>();

        // a. Open venation = closest vein node only ---------------------------------------------------------------------
        query.ClosestPoint(_nodeTree, attractor.position, nodesInAttractionZone);

        // ii. If a vein node is found, associate it by pushing attractor ID to _nodeInfluencedBy
        if(nodesInAttractionZone.Count > 0) {
          Node closestNode = null;

          if(nodesInAttractionZone.Count == 1) {
            closestNode = _nodes[nodesInAttractionZone[0]];
          } else {
            float smallestDistanceSqr = AttractionDistance * AttractionDistance;

            // Find the nearest node
            foreach(int nodeID in nodesInAttractionZone) {
              float distance = (attractor.position - _nodes[nodeID].position).sqrMagnitude;

              if(distance < smallestDistanceSqr) {
                closestNode = _nodes[nodeID];
                smallestDistanceSqr = distance;
              }
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
    }

    void GrowNetwork() {
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

          // Create the new node
          Node newNode = new Node(
            newNodePosition,
            node,
            true,
            node.radius * .98f
          );

          node.children.Add(newNode);
          newNodes.Add(newNode);

          // Create a new tube mesh for this segment.
          CombineInstance combineInstance = new CombineInstance();
          TubeRenderer tube = new GameObject().AddComponent<TubeRenderer>();
          tube.points = new Vector3[2];
          tube.radiuses = new float[2];
          // tube.edgeCount = 5;

          // tube.points[0] = node.parent != null ? node.parent.position : new Vector3(0,0,0); // go back two nodes for smoother radius transitions and prevent joint gaps
          tube.points[0] = node.position;
          tube.points[1] = newNode.position;
          // tube.radiuses[0] = node.parent != null ? node.parent.radius : 5f;
          tube.radiuses[0] = node.radius;
          tube.radiuses[1] = newNode.radius;

          tube.ForceUpdate();

          combineInstance.mesh = tube.mesh;
          combineInstance.transform = tube.transform.localToWorldMatrix;

          _branchMeshes.Add(combineInstance);

          Destroy(tube.gameObject);
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
    }

    void PruneAttractors() {
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
    }

    void GenerateBranchMeshes() {
      _branches = new List<List<Vector3>>();
      _radii = new List<List<float>>();
      GetBranch(_nodes[0]);

      CombineInstance[] combineInstances = new CombineInstance[_branches.Count];

      int t = 0;
      foreach(List<Vector3> branch in _branches) {
        TubeRenderer tube = new GameObject().AddComponent<TubeRenderer>();
        tube.points = new Vector3[branch.Count];
        tube.radiuses = new float[branch.Count];
        // tube.edgeCount = 5;

        for(int j=0; j<branch.Count; j++) {
          tube.points[j] = branch[j];
          tube.radiuses[j] = _radii[t][j];
        }

        tube.ForceUpdate();

        combineInstances[t].mesh = tube.mesh;
        combineInstances[t].transform = tube.transform.localToWorldMatrix;

        Destroy(tube.gameObject);

        t++;
      }

      filter.mesh.CombineMeshes(combineInstances);

      GetComponent<Renderer>().material = new Material(Shader.Find("Diffuse"));
      // GetComponent<Renderer>().material.mainTexture = CreateTileTexture(2);
    }

  void OnDrawGizmos() {
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
  }

  private void BuildSpatialIndex() {
    // Create spatial index using _nodePositions
    List<Vector3> nodePositions = new List<Vector3>();

    foreach(Node node in _nodes) {
      nodePositions.Add(node.position);
    }

    _nodeTree = new KDTree(nodePositions.ToArray());
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
    List<float> thisRadii = new List<float>();
    Node currentNode = startingNode;

    if(currentNode.parent != null) {
      thisBranch.Add(currentNode.parent.position);
      thisRadii.Add(currentNode.parent.radius * .98f);
    }

    if(currentNode.parent != null) {
      thisRadii.Add(currentNode.parent.radius * .98f);
    } else {
      thisRadii.Add(5f);
    }

    thisBranch.Add(currentNode.position);

    while(currentNode != null && currentNode.children.Count > 0) {
      if(currentNode.children.Count == 1) {
        thisBranch.Add(currentNode.children[0].position);

        if(currentNode.parent != null) {
          thisRadii.Add(currentNode.parent.radius * .98f);
        } else {
          thisRadii.Add(5f);
        }

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
