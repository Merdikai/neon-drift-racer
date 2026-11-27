using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates a smooth, wide 3D neon drift circuit mesh directly from the 180 track waypoints:
/// Includes asphalt road with collision, glowing neon cyan inner curbs, glowing neon magenta outer curbs,
/// and barrier walls so the car stays on track.
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

        var roadMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit"));
        roadMat.name = "Mat_RoadSurface";
        roadMat.SetColor("_BaseColor", Palette.FloorInk);
        roadMat.SetFloat("_Roughness", 0.45f);
        roadMR.material = roadMat;

        // 2. Left Neon Curb (Cyan)
        var leftCurbGO = new GameObject("LeftNeonCurb");
        leftCurbGO.transform.SetParent(root.transform);
        var leftMF = leftCurbGO.AddComponent<MeshFilter>();
        var leftMR = leftCurbGO.AddComponent<MeshRenderer>();
        var leftMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit"));
        leftMat.name = "Mat_NeonCyanCurb";
        leftMat.SetColor("_BaseColor", Palette.NeonCyan);
        leftMat.EnableKeyword("_EMISSION");
        leftMat.SetColor("_EmissionColor", Palette.NeonCyan * 3.5f);
        leftMR.material = leftMat;

        // 3. Right Neon Curb (Magenta)
        var rightCurbGO = new GameObject("RightNeonCurb");
        rightCurbGO.transform.SetParent(root.transform);
        var rightMF = rightCurbGO.AddComponent<MeshFilter>();
        var rightMR = rightCurbGO.AddComponent<MeshRenderer>();
        var rightMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit"));
        rightMat.name = "Mat_NeonMagentaCurb";
        rightMat.SetColor("_BaseColor", Palette.NeonMagenta);
        rightMat.EnableKeyword("_EMISSION");
        rightMat.SetColor("_EmissionColor", Palette.NeonMagenta * 3.5f);
        rightMR.material = rightMat;

        // 4. Outer Barriers
        var barrierGO = new GameObject("TrackBarriers");
        barrierGO.transform.SetParent(root.transform);
        var barrierMF = barrierGO.AddComponent<MeshFilter>();
        var barrierMR = barrierGO.AddComponent<MeshRenderer>();
        var barrierMC = barrierGO.AddComponent<MeshCollider>();
        var barrierMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit"));
        barrierMat.name = "Mat_TrackBarriers";
        barrierMat.SetColor("_BaseColor", Palette.BoundsMidnight);
        barrierMR.material = barrierMat;

        // Build vertex ribbons
        int n = wps.Count;
        float halfW = roadWidth * 0.5f;

        List<Vector3> roadVerts = new List<Vector3>();
        List<Vector2> roadUVs = new List<Vector2>();
        List<int> roadTris = new List<int>();

        List<Vector3> leftVerts = new List<Vector3>();
        List<int> leftTris = new List<int>();

        List<Vector3> rightVerts = new List<Vector3>();
        List<int> rightTris = new List<int>();

        List<Vector3> barrierVerts = new List<Vector3>();
        List<int> barrierTris = new List<int>();

        for (int i = 0; i <= n; i++)
        {
            var wp = wps[i % n];
            Vector3 fwd = wp.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 center = wp.pos;

            Vector3 lRoad = center - right * halfW;
            Vector3 rRoad = center + right * halfW;

            // Road
            roadVerts.Add(lRoad);
            roadVerts.Add(rRoad);
            float uProgress = (float)i / n * 30f;
            roadUVs.Add(new Vector2(0, uProgress));
            roadUVs.Add(new Vector2(1, uProgress));

            // Curbs (0.7m width, 0.15m height)
            Vector3 lCurbInner = lRoad;
            Vector3 lCurbOuter = lRoad - right * 0.9f + Vector3.up * 0.2f;
            leftVerts.Add(lCurbInner);
            leftVerts.Add(lCurbOuter);

            Vector3 rCurbInner = rRoad;
            Vector3 rCurbOuter = rRoad + right * 0.9f + Vector3.up * 0.2f;
            rightVerts.Add(rCurbInner);
            rightVerts.Add(rCurbOuter);

            // Barriers (1.6m high walls outside curbs)
            Vector3 lBarrierBot = lCurbOuter;
            Vector3 lBarrierTop = lCurbOuter + Vector3.up * 2.0f;
            Vector3 rBarrierBot = rCurbOuter;
            Vector3 rBarrierTop = rCurbOuter + Vector3.up * 2.0f;

            barrierVerts.Add(lBarrierBot); // 4*i + 0
            barrierVerts.Add(lBarrierTop); // 4*i + 1
            barrierVerts.Add(rBarrierBot); // 4*i + 2
            barrierVerts.Add(rBarrierTop); // 4*i + 3

            if (i < n)
            {
                // Road Triangles
                int rBase = i * 2;
                roadTris.Add(rBase);
                roadTris.Add(rBase + 1);
                roadTris.Add(rBase + 2);
                roadTris.Add(rBase + 1);
                roadTris.Add(rBase + 3);
                roadTris.Add(rBase + 2);

                // Left Curb Triangles
                leftTris.Add(rBase);
                leftTris.Add(rBase + 1);
                leftTris.Add(rBase + 2);
                leftTris.Add(rBase + 1);
                leftTris.Add(rBase + 3);
                leftTris.Add(rBase + 2);

                // Right Curb Triangles
                rightTris.Add(rBase);
                rightTris.Add(rBase + 2);
                rightTris.Add(rBase + 1);
                rightTris.Add(rBase + 1);
                rightTris.Add(rBase + 2);
                rightTris.Add(rBase + 3);

                // Barrier Triangles (Left wall facing inward)
                int bBase = i * 4;
                barrierTris.Add(bBase);
                barrierTris.Add(bBase + 4);
                barrierTris.Add(bBase + 1);
                barrierTris.Add(bBase + 1);
                barrierTris.Add(bBase + 4);
                barrierTris.Add(bBase + 5);

                // Barrier Triangles (Right wall facing inward)
                barrierTris.Add(bBase + 2);
                barrierTris.Add(bBase + 3);
                barrierTris.Add(bBase + 6);
                barrierTris.Add(bBase + 3);
                barrierTris.Add(bBase + 7);
                barrierTris.Add(bBase + 6);
            }
        }

        // Assign meshes
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
        leftMesh.SetTriangles(leftTris, 0);
        leftMesh.RecalculateNormals();
        leftMF.mesh = leftMesh;

        Mesh rightMesh = new Mesh { name = "RightCurbMesh" };
        rightMesh.SetVertices(rightVerts);
        rightMesh.SetTriangles(rightTris, 0);
        rightMesh.RecalculateNormals();
        rightMF.mesh = rightMesh;

        Mesh barrierMesh = new Mesh { name = "BarrierMesh" };
        barrierMesh.SetVertices(barrierVerts);
        barrierMesh.SetTriangles(barrierTris, 0);
        barrierMesh.RecalculateNormals();
        barrierMF.mesh = barrierMesh;
        barrierMC.sharedMesh = barrierMesh;

        return root;
    }
}
