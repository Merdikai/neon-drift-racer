using UnityEngine;

/// <summary>
/// A checkpoint trigger. Reports to LapManager when the car passes through.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CheckpointGate : MonoBehaviour
{
    public int checkpointIndex;

    void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody == null) return;
        var car = other.attachedRigidbody.GetComponent<DriftCarController>();
        if (car == null) return;

        var lm = FindAnyObjectByType<LapManager>();
        if (lm != null) lm.OnCheckpoint(checkpointIndex);
    }
}
