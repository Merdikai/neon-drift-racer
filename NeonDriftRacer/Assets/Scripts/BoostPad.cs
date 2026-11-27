using UnityEngine;

/// <summary>
/// Trigger zone that applies a speed boost to the car.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class BoostPad : MonoBehaviour
{
    public float boostDuration = 1.5f;
    public float cooldown = 2f;

    float lastTriggerTime = -100f;

    void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastTriggerTime < cooldown) return;
        if (other.attachedRigidbody == null) return;

        var car = other.attachedRigidbody.GetComponent<DriftCarController>();
        if (car != null)
        {
            car.ApplyBoost(boostDuration);
            FindAnyObjectByType<AudioManager>()?.PlayBoostSound();
            lastTriggerTime = Time.time;
        }
    }
}
