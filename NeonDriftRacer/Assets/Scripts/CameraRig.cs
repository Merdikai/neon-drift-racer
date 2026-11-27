using UnityEngine;

/// <summary>
/// Bulletproof third-person follow camera for the drift car.
/// Smoothly glides behind the car, dynamically zooms out on boost, and eliminates LookRotation errors.
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Transform target;

    [Header("Follow Settings")]
    public float distance = 7.5f;
    public float height = 3.2f;
    public float positionDamping = 8f;
    public float rotationDamping = 10f;

    [Header("Drift Dynamic FOV")]
    public float baseFOV = 65f;
    public float maxFOV = 80f;

    Camera cam;
    float currentFOV;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            cam.fieldOfView = baseFOV;
            currentFOV = baseFOV;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        var car = target.GetComponent<DriftCarController>();

        // Desired position directly behind the target
        Vector3 desiredPosition = target.position - target.forward * distance + Vector3.up * height;

        // Apply smooth interpolation
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionDamping * Time.deltaTime);

        // Aim smoothly at the car + slight forward offset
        Vector3 aimPoint = target.position + target.forward * 2f + Vector3.up * 1.2f;
        Vector3 dirToAim = aimPoint - transform.position;

        if (dirToAim.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dirToAim, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationDamping * Time.deltaTime);
        }

        // Dynamic FOV based on speed and drift
        if (cam != null && car != null)
        {
            float speedRatio = Mathf.Clamp01(Mathf.Abs(car.Speed) / 45f);
            float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedRatio);
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, 5f * Time.deltaTime);
            cam.fieldOfView = currentFOV;
        }
    }

    public void SetTarget(Transform t)
    {
        target = t;
        if (t != null)
        {
            transform.position = t.position - t.forward * distance + Vector3.up * height;
            transform.LookAt(t.position + Vector3.up * 1.2f);
        }
    }
}
