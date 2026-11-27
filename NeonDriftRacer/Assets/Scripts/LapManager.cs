using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages checkpoints, lap counting, and lap timing.
/// </summary>
public class LapManager : MonoBehaviour
{
    public int totalCheckpoints = 6;
    public int totalLaps = 3;
    public float targetLapTime = 45f; // seconds for gold time

    [Header("Events")]
    public UnityEvent<int> onLapComplete;
    public UnityEvent<int> onCheckpointHit;
    public UnityEvent onRaceFinish;

    // --- state ---
    public int CurrentLap { get; private set; } = 1;
    public int NextCheckpoint { get; private set; }
    public float LapTime { get; private set; }
    public float TotalTime { get; private set; }
    public float BestLapTime { get; private set; } = float.MaxValue;
    public bool RaceFinished { get; private set; }

    bool raceStarted;

    public void StartRace()
    {
        CurrentLap = 1;
        NextCheckpoint = 0;
        LapTime = 0;
        TotalTime = 0;
        BestLapTime = float.MaxValue;
        RaceFinished = false;
        raceStarted = true;
    }

    void Update()
    {
        if (!raceStarted || RaceFinished) return;
        float dt = Time.deltaTime;
        LapTime += dt;
        TotalTime += dt;
    }

    public void OnCheckpoint(int index)
    {
        if (!raceStarted || RaceFinished) return;
        if (index != NextCheckpoint) return;

        onCheckpointHit?.Invoke(index);
        NextCheckpoint++;

        if (NextCheckpoint >= totalCheckpoints)
        {
            // lap complete
            if (LapTime < BestLapTime) BestLapTime = LapTime;
            onLapComplete?.Invoke(CurrentLap);

            CurrentLap++;
            LapTime = 0;
            NextCheckpoint = 0;

            if (CurrentLap > totalLaps)
            {
                RaceFinished = true;
                onRaceFinish?.Invoke();
            }
        }
    }
}
