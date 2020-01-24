using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DataStructures.ViliWonka.KDTree;

public class Network {
  float AttractionDistance = 50f;
  float KillDistance = 20f;
  float SegmentLength = 10;

  private List<Attractor> _attractors;
  private List<Node> _nodes;

  private List<GameObject> _nodeObjects;

  private KDTree _nodeTree;               // spatial index of vein nodes
  private KDTree _tipTree;                // spatial index of vein tip nodes
  private KDQuery query = new KDQuery();  // query object for spatial indices

  public Network() {
    // Initialize attractor variables
    _attractors = new List<Attractor>();

    // Populate attractors
    for (int i = 0; i < 5000; i++) {
      _attractors.Add(new Attractor(Random.insideUnitSphere * 500));
    }

    // int spacing = 30;
    // int rowResolution = 30;
    // int colResolution = 30;

    // for(int row = 0; row < rowResolution; row++) {
    //   for(int col = 0; col < colResolution; col++) {
    //     _attractors.Add(
    //       new Attractor(
    //         new Vector3(
    //           col * spacing + Random.Range(-spacing/2, spacing/2),
    //           row * spacing + Random.Range(-spacing/2, spacing/2),
    //           0
    //         )
    //       )
    //     );
    //   }
    // }

    // Initialize node variables
    _nodes = new List<Node>();

    // Add a single vein node at the origin to seed growth
    _nodes.Add(
      new Node(
        // new Vector3((colResolution*spacing)/2,(rowResolution*spacing)/2,0),
        Vector3.zero,
        null,
        true,
        1
      )
    );

    // Build the vein node spatial index for the first time
    BuildSpatialIndex();
  }

  public void Update() {
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
          Node closestNode = _nodes[nodesInAttractionZone[0]];

          // float smallestDistance = AttractionDistance;

          // foreach(int nodeID in nodesInAttractionZone) {
          //   float distance = Vector3.Distance(_attractorPositions[i], _nodePositions[nodeID]);

          //   if(distance < smallestDistance) {
          //     closestNodeID = nodeID;
          //     smallestDistance = distance;
          //   }
          // }

          closestNode.influencedBy.Add(attractor);

          if(Vector3.Distance(attractor.position, closestNode.position) > KillDistance) {
            attractor.isReached = false;
          } else {
            attractor.isReached = true;
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

        // Since this vein node is spawning a new one, it is no longer a tip
        node.isTip = false;

        newNodes.Add(
          new Node(
            newNodePosition,
            node,
            true,
            1
          )
        );

        // TODO: create a new sphere and add it to the physics engine
        // GameObject newNodeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        // newNodeObject.transform.position = newNodePosition;
      }
    }

    // Add in the new vein nodes that have been produced
    for(int i=0; i<newNodes.Count; i++) {
      _nodes.Add(newNodes[i]);
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

    // 4. Perform vein thickening ========================================================================================

    // 5. Rebuild vein node spatial index with latest vein nodes =========================================================
    BuildSpatialIndex();
  }

  public void DrawGizmos() {
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
}
