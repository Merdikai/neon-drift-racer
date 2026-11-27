using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Arcade Drift Car Controller with real lateral slip physics, handbrake drift (Space),
/// animated spinning/steering wheels, and chassis pitch/roll suspension feel.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DriftCarController : MonoBehaviour
{
    [Header("Drivetrain")]
    public float maxSpeed = 56f;
    public float acceleration = 36f;
    public float brakeForce = 52f;
    public float reverseSpeed = 16f;

    [Header("Steering & Drift")]
    public float steerSpeed = 128f;
    public float driftSteerBonus = 1.32f;
    public float normalLateralGrip = 7.5f;
    public float driftLateralGrip = 2.4f;

    [Header("Visuals & Suspension")]
    public float bodyRollAngle = 6.5f;
    public float bodyPitchAngle = 2.8f;
    public float suspensionSpeed = 9f;

    // --- State Properties ---
    public float Speed { get; private set; }
    public float SteerInput { get; private set; }
    public float ThrottleInput { get; private set; }
    public bool HandbrakeInput { get; private set; }
    public bool IsDrifting { get; private set; }
    public float DriftAngle { get; private set; }
    public bool IsBoosting => boostTimer > 0f;

    Rigidbody rb;
    Transform bodyVisual;
    Transform wheelFL, wheelFR, wheelRL, wheelRR;
    float wheelSpinAngle;
    float steerVisualAngle;
    float boostTimer;
    Vector3 horizontalVel;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1200f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        CacheModelParts();
    }

    public void CacheModelParts()
    {
        bodyVisual = transform.Find("CarBodyVisual") ?? transform.Find("CarHull");
        wheelFL = transform.Find("Wheel_FL");
        wheelFR = transform.Find("Wheel_FR");
        wheelRL = transform.Find("Wheel_RL");
        wheelRR = transform.Find("Wheel_RR");

        if (bodyVisual == null && transform.childCount > 0)
        {
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Wheel_") && !child.name.Contains("Light") && !child.name.Contains("VFX"))
                {
                    bodyVisual = child;
                    break;
                }
            }
        }
    }

    void Update()
    {
        ThrottleInput = 0f;
        SteerInput = 0f;
        HandbrakeInput = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) ThrottleInput += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) ThrottleInput -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) SteerInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) SteerInput += 1f;
            if (kb.spaceKey.isPressed) HandbrakeInput = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (ThrottleInput == 0f)
        {
            ThrottleInput = Input.GetAxisRaw("Vertical");
            if (ThrottleInput == 0f)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ThrottleInput += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ThrottleInput -= 1f;
            }
        }
        if (SteerInput == 0f)
        {
            SteerInput = Input.GetAxisRaw("Horizontal");
            if (SteerInput == 0f)
            {
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) SteerInput -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) SteerInput += 1f;
            }
        }
        if (!HandbrakeInput && Input.GetKey(KeyCode.Space))
            HandbrakeInput = true;
#endif

        AnimateWheels();
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Read actual horizontal velocity from Rigidbody (respects wall collisions!)
        horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float forwardSpeed = Vector3.Dot(horizontalVel, transform.forward);
        float lateralSpeed = Vector3.Dot(horizontalVel, transform.right);

        float topSpeed = boostTimer > 0f ? maxSpeed * 1.38f : maxSpeed;
        if (boostTimer > 0f) boostTimer -= dt;

        // Accelerate / Brake along forward axis
        if (boostTimer > 0f)
        {
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, topSpeed, acceleration * 1.8f * dt);
        }
        else if (ThrottleInput > 0.05f)
        {
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, topSpeed * ThrottleInput, acceleration * dt);
        }
        else if (ThrottleInput < -0.05f)
        {
            float target = forwardSpeed > 1f ? 0f : -reverseSpeed;
            float rate = forwardSpeed > 1f ? brakeForce : acceleration * 0.65f;
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, target, rate * dt);
        }
        else
        {
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, acceleration * 0.32f * dt);
        }

        // Determine Drift State
        bool initiatingDrift = Mathf.Abs(forwardSpeed) > 12f && (HandbrakeInput || Mathf.Abs(SteerInput) > 0.6f);
        float rawAngle = horizontalVel.sqrMagnitude > 4f
            ? Vector3.SignedAngle(transform.forward, horizontalVel.normalized, Vector3.up)
            : 0f;

        IsDrifting = (initiatingDrift && Mathf.Abs(SteerInput) > 0.1f) || (Mathf.Abs(rawAngle) > 10f && Mathf.Abs(forwardSpeed) > 10f);
        DriftAngle = rawAngle;

        // Steering yaw
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 7f);
        if (Mathf.Abs(forwardSpeed) > 0.4f && Mathf.Abs(SteerInput) > 0.01f)
        {
            float dir = forwardSpeed >= 0f ? 1f : -1f;
            float turnMul = IsDrifting ? driftSteerBonus : 1f;
            float turn = SteerInput * steerSpeed * turnMul * speedFactor * dir * dt;
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
        }

        // Lateral grip: low grip during drift lets the tail slide out smoothly!
        float grip = IsDrifting ? driftLateralGrip : normalLateralGrip;
        if (HandbrakeInput) grip = driftLateralGrip * 0.65f;
        lateralSpeed = Mathf.Lerp(lateralSpeed, 0f, grip * dt);

        // Reconstruct velocity in world space
        Vector3 newHorizontal = transform.forward * forwardSpeed + transform.right * lateralSpeed;
        rb.linearVelocity = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);
        Speed = forwardSpeed;

        // Visual Suspension Roll & Pitch
        if (bodyVisual != null)
        {
            float targetRoll = -SteerInput * bodyRollAngle * speedFactor;
            float targetPitch = -ThrottleInput * bodyPitchAngle;
            if (IsBoosting) targetPitch = -bodyPitchAngle * 1.4f;

            Vector3 cur = bodyVisual.localEulerAngles;
            float roll = Mathf.LerpAngle(cur.z, targetRoll, suspensionSpeed * dt);
            float pitch = Mathf.LerpAngle(cur.x, targetPitch, suspensionSpeed * dt);
            bodyVisual.localEulerAngles = new Vector3(pitch, cur.y, roll);
        }
    }

    void AnimateWheels()
    {
        float dt = Time.deltaTime;
        wheelSpinAngle += (Speed / 0.36f) * Mathf.Rad2Deg * dt;
        steerVisualAngle = Mathf.Lerp(steerVisualAngle, SteerInput * 26f, 12f * dt);

        if (wheelFL != null) wheelFL.localRotation = Quaternion.Euler(wheelSpinAngle, steerVisualAngle, 0f);
        if (wheelFR != null) wheelFR.localRotation = Quaternion.Euler(wheelSpinAngle, steerVisualAngle, 0f);
        if (wheelRL != null) wheelRL.localRotation = Quaternion.Euler(wheelSpinAngle, 0f, 0f);
        if (wheelRR != null) wheelRR.localRotation = Quaternion.Euler(wheelSpinAngle, 0f, 0f);
    }

    public void ApplyBoost(float duration = 1.5f)
    {
        boostTimer = Mathf.Max(boostTimer, duration);
    }

    public void ResetCar(Vector3 pos, Quaternion rot)
    {
        boostTimer = 0f;
        IsDrifting = false;
        transform.SetPositionAndRotation(pos, rot);
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
