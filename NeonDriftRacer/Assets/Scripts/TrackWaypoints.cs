using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Loads track waypoints from JSON and provides lap-progress queries.
/// </summary>
public class TrackWaypoints : MonoBehaviour
{
    public TextAsset waypointJson;
    public float roadHalfWidth = 9f;

    [System.Serializable]
    public class WP
    {
        public Vector3 pos;
        public Vector3 forward;
    }

    public List<WP> waypoints = new List<WP>();
    public float trackLength;

    void Awake()
    {
        if (waypointJson != null)
            LoadFromJson(waypointJson.text);
    }

    public void LoadFromJson(string json)
    {
        var data = JsonUtility.FromJson<WPData>(json);
        if (data == null || data.waypoints == null) return;
        waypoints.Clear();
        trackLength = data.trackLengthMeters;
        roadHalfWidth = data.roadHalfWidth;
        foreach (var w in data.waypoints)
        {
            waypoints.Add(new WP
            {
                pos = new Vector3(w.pos[0], w.pos[1], w.pos[2]),
                forward = new Vector3(w.forward[0], w.forward[1], w.forward[2]).normalized
            });
        }
    }

    public int NearestWaypoint(Vector3 worldPos)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < waypoints.Count; i++)
        {
            float d = (waypoints[i].pos - worldPos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    public float LapProgress(Vector3 worldPos)
    {
        if (waypoints.Count == 0) return 0;
        int idx = NearestWaypoint(worldPos);
        return (float)idx / waypoints.Count;
    }

    [System.Serializable]
    class WPData
    {
        public float trackLengthMeters;
        public float roadHalfWidth;
        public WPEntry[] waypoints;
    }

    [System.Serializable]
    class WPEntry
    {
        public float[] pos;
        public float[] forward;
        public float[] right;
        public float[] up;
    }
}
