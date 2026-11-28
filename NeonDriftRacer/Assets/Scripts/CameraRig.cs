using UnityEngine;

/// <summary>
/// Hero-angle third-person chase camera for the drift car.
/// Lowers near the wet asphalt, tracks velocity during fishtails so you see the car's
/// side profile, cyan underglow, and sweeping magenta taillight trails, and expands FOV on speed.
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Transform target;

    [Header("Follow Settings")]
    public float distance = 6.4f;
    public float height = 2.35f;
    public float positionDamping = 9f;
    public float rotationDamping = 11f;
    public float driftSwingOffset = 1.6f;

    [Header("Drift Dynamic FOV")]
    public float baseFOV = 66f;
    public float maxFOV = 84f;

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
        float driftSwing = 0f;
        if (car != null && car.IsDrifting)
        {
            // Swing camera slightly outside the fishtail so the car's side & taillight trails frame dramatically
            driftSwing = Mathf.Clamp(car.DriftAngle / 30f, -1f, 1f) * driftSwingOffset;
        }

        Vector3 desiredPosition = target.position
            - target.forward * distance
            + target.right * driftSwing
            + Vector3.up * height;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionDamping * Time.deltaTime);

        Vector3 aimPoint = target.position + target.forward * 3.0f + Vector3.up * 1.0f;
        Vector3 dirToAim = aimPoint - transform.position;
        if (dirToAim.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dirToAim, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationDamping * Time.deltaTime);
        }

        if (cam != null && car != null)
        {
            float speedRatio = Mathf.Clamp01(Mathf.Abs(car.Speed) / 55f);
            float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedRatio) + (car.IsBoosting ? 5f : 0f);
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
            transform.LookAt(t.position + Vector3.up * 1.0f);
        }
    }
}
