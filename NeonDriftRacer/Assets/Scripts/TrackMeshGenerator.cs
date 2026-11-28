using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates a smooth, wide 3D neon drift circuit mesh from the 180 track waypoints,
/// complete with UV-mapped wet asphalt, alternating cyan/magenta rumble curbs, and neon-railed barriers.
/// </summary>
public static class TrackMeshGenerator
{
    public static GameObject BuildTrack(List<TrackWaypoints.WP> wps, float roadWidth = 20f)
    {
        if (wps == null || wps.Count < 3) return null;

        var root = new GameObject("NeonTrack");

        // 1. Asphalt Road Mesh
        var roadGO = new GameObject("RoadSurface");
        roadGO.transform.SetParent(root.transform);
        var roadMF = roadGO.AddComponent<MeshFilter>();
        var roadMR = roadGO.AddComponent<MeshRenderer>();
        var roadMC = roadGO.AddComponent<MeshCollider>();
        roadMR.sharedMaterial = CyberMaterialFactory.RoadAsphaltMat();

        // 2. Left Neon Rumble Curb
        var leftCurbGO = new GameObject("LeftNeonCurb");
        leftCurbGO.transform.SetParent(root.transform);
        var leftMF = leftCurbGO.AddComponent<MeshFilter>();
        var leftMR = leftCurbGO.AddComponent<MeshRenderer>();
        leftMR.sharedMaterial = CyberMaterialFactory.NeonRumbleCurbMat(false);

        // 3. Right Neon Rumble Curb
        var rightCurbGO = new GameObject("RightNeonCurb");
        rightCurbGO.transform.SetParent(root.transform);
        var rightMF = rightCurbGO.AddComponent<MeshFilter>();
        var rightMR = rightCurbGO.AddComponent<MeshRenderer>();
        rightMR.sharedMaterial = CyberMaterialFactory.NeonRumbleCurbMat(true);

        // 4. Outer Neon-Railed Barriers
        var barrierGO = new GameObject("TrackBarriers");
        barrierGO.transform.SetParent(root.transform);
        var barrierMF = barrierGO.AddComponent<MeshFilter>();
        var barrierMR = barrierGO.AddComponent<MeshRenderer>();
        var barrierMC = barrierGO.AddComponent<MeshCollider>();
        barrierMR.sharedMaterial = CyberMaterialFactory.TrackBarriersMat();

        int n = wps.Count;
        float halfW = roadWidth * 0.5f;

        var roadVerts = new List<Vector3>();
        var roadUVs = new List<Vector2>();
        var roadTris = new List<int>();

        var leftVerts = new List<Vector3>();
        var leftUVs = new List<Vector2>();
        var leftTris = new List<int>();

        var rightVerts = new List<Vector3>();
        var rightUVs = new List<Vector2>();
        var rightTris = new List<int>();

        var barrierVerts = new List<Vector3>();
        var barrierUVs = new List<Vector2>();
        var barrierTris = new List<int>();

        for (int i = 0; i <= n; i++)
        {
            var wp = wps[i % n];
            Vector3 fwd = wp.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 center = wp.pos;

            Vector3 lRoad = center - right * halfW;
            Vector3 rRoad = center + right * halfW;

            float vRoad = (float)i / n * 48f;
            float vCurb = (float)i / n * 64f;
            float vWall = (float)i / n * 36f;

            // Road
            roadVerts.Add(lRoad);
            roadVerts.Add(rRoad);
            roadUVs.Add(new Vector2(0f, vRoad));
            roadUVs.Add(new Vector2(1f, vRoad));

            // Curbs (1.2m wide rumble strips with slight cant)
            Vector3 lCurbOuter = lRoad - right * 1.25f + Vector3.up * 0.16f;
            leftVerts.Add(lRoad);
            leftVerts.Add(lCurbOuter);
            leftUVs.Add(new Vector2(0f, vCurb));
            leftUVs.Add(new Vector2(1f, vCurb));

            Vector3 rCurbOuter = rRoad + right * 1.25f + Vector3.up * 0.16f;
            rightVerts.Add(rRoad);
            rightVerts.Add(rCurbOuter);
            rightUVs.Add(new Vector2(0f, vCurb));
            rightUVs.Add(new Vector2(1f, vCurb));

            // Barriers (2.1m high walls with vertical V=0..1 for horizontal neon rails!)
            Vector3 lBarrierBot = lCurbOuter;
            Vector3 lBarrierTop = lCurbOuter + Vector3.up * 2.1f;
            Vector3 rBarrierBot = rCurbOuter;
            Vector3 rBarrierTop = rCurbOuter + Vector3.up * 2.1f;

            barrierVerts.Add(lBarrierBot); // 4*i + 0
            barrierVerts.Add(lBarrierTop); // 4*i + 1
            barrierVerts.Add(rBarrierBot); // 4*i + 2
            barrierVerts.Add(rBarrierTop); // 4*i + 3

            barrierUVs.Add(new Vector2(vWall, 0f));
            barrierUVs.Add(new Vector2(vWall, 1f));
            barrierUVs.Add(new Vector2(vWall, 0f));
            barrierUVs.Add(new Vector2(vWall, 1f));

            if (i < n)
            {
                int rBase = i * 2;
                roadTris.Add(rBase); roadTris.Add(rBase + 1); roadTris.Add(rBase + 2);
                roadTris.Add(rBase + 1); roadTris.Add(rBase + 3); roadTris.Add(rBase + 2);

                leftTris.Add(rBase); leftTris.Add(rBase + 1); leftTris.Add(rBase + 2);
                leftTris.Add(rBase + 1); leftTris.Add(rBase + 3); leftTris.Add(rBase + 2);

                rightTris.Add(rBase); rightTris.Add(rBase + 2); rightTris.Add(rBase + 1);
                rightTris.Add(rBase + 1); rightTris.Add(rBase + 2); rightTris.Add(rBase + 3);

                int bBase = i * 4;
                barrierTris.Add(bBase); barrierTris.Add(bBase + 4); barrierTris.Add(bBase + 1);
                barrierTris.Add(bBase + 1); barrierTris.Add(bBase + 4); barrierTris.Add(bBase + 5);

                barrierTris.Add(bBase + 2); barrierTris.Add(bBase + 3); barrierTris.Add(bBase + 6);
                barrierTris.Add(bBase + 3); barrierTris.Add(bBase + 7); barrierTris.Add(bBase + 6);
            }
        }

        Mesh roadMesh = new Mesh { name = "RoadMesh" };
        roadMesh.SetVertices(roadVerts);
        roadMesh.SetUVs(0, roadUVs);
        roadMesh.SetTriangles(roadTris, 0);
        roadMesh.RecalculateNormals();
        roadMesh.RecalculateBounds();
        roadMF.mesh = roadMesh;
        roadMC.sharedMesh = roadMesh;

        Mesh leftMesh = new Mesh { name = "LeftCurbMesh" };
        leftMesh.SetVertices(leftVerts);
        leftMesh.SetUVs(0, leftUVs);
        leftMesh.SetTriangles(leftTris, 0);
        leftMesh.RecalculateNormals();
        leftMF.mesh = leftMesh;

        Mesh rightMesh = new Mesh { name = "RightCurbMesh" };
        rightMesh.SetVertices(rightVerts);
        rightMesh.SetUVs(0, rightUVs);
        rightMesh.SetTriangles(rightTris, 0);
        rightMesh.RecalculateNormals();
        rightMF.mesh = rightMesh;

        Mesh barrierMesh = new Mesh { name = "BarrierMesh" };
        barrierMesh.SetVertices(barrierVerts);
        barrierMesh.SetUVs(0, barrierUVs);
        barrierMesh.SetTriangles(barrierTris, 0);
        barrierMesh.RecalculateNormals();
        barrierMF.mesh = barrierMesh;
        barrierMC.sharedMesh = barrierMesh;

        return root;
    }
}
