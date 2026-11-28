using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Arcade Drift Car Controller with authentic fishtailing physics:
/// Angular yaw momentum lets the rear end whip out smoothly into corners (especially with Space handbrake),
/// while front wheels visually counter-steer into the slide.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DriftCarController : MonoBehaviour
{
    [Header("Drivetrain")]
    public float maxSpeed = 58f;
    public float acceleration = 38f;
    public float brakeForce = 54f;
    public float reverseSpeed = 16f;

    [Header("Fishtail & Drift Physics")]
    public float steerSpeed = 118f;
    public float driftYawBoost = 1.42f;
    public float yawInertia = 9.5f;         // smooth angular momentum for fishtailing
    public float normalLateralGrip = 8.5f;
    public float driftLateralGrip = 1.85f;  // low rear grip = true tail swing!
    public float handbrakeGrip = 1.15f;

    [Header("Visuals & Suspension")]
    public float bodyRollAngle = 7.5f;
    public float bodyPitchAngle = 3.0f;
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
    float currentYawRate;
    float boostTimer;

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

        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float forwardSpeed = Vector3.Dot(horizontalVel, transform.forward);
        float lateralSpeed = Vector3.Dot(horizontalVel, transform.right);

        float topSpeed = boostTimer > 0f ? maxSpeed * 1.4f : maxSpeed;
        if (boostTimer > 0f) boostTimer -= dt;

        // Forward Drive / Brake
        if (boostTimer > 0f)
        {
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, topSpeed, acceleration * 1.9f * dt);
        }
        else if (ThrottleInput > 0.05f)
        {
            // Slight speed scrub while sliding hard, then snap acceleration on exit!
            float driftDrag = IsDrifting ? 0.96f : 1f;
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, topSpeed * ThrottleInput * driftDrag, acceleration * dt);
        }
        else if (ThrottleInput < -0.05f)
        {
            float target = forwardSpeed > 1f ? 0f : -reverseSpeed;
            float rate = forwardSpeed > 1f ? brakeForce : acceleration * 0.7f;
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, target, rate * dt);
        }
        else
        {
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, acceleration * 0.30f * dt);
        }

        // Compute slip angle between car's nose and actual velocity vector
        float rawAngle = horizontalVel.sqrMagnitude > 4f
            ? Vector3.SignedAngle(transform.forward, horizontalVel.normalized, Vector3.up)
            : 0f;

        // Fishtail / Drift detection: steering hard at speed, holding Space, or carrying >8 deg slip angle
        bool initiating = Mathf.Abs(forwardSpeed) > 10f && (HandbrakeInput || Mathf.Abs(SteerInput) > 0.5f);
        IsDrifting = (initiating && Mathf.Abs(SteerInput) > 0.1f) || (Mathf.Abs(rawAngle) > 8f && Mathf.Abs(forwardSpeed) > 8f);
        DriftAngle = rawAngle;

        // Smooth Yaw Rate with Angular Inertia -> creates natural fishtail whip & counter-steer!
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 7f);
        float dir = forwardSpeed >= 0f ? 1f : -1f;
        float yawMul = HandbrakeInput ? driftYawBoost * 1.15f : (IsDrifting ? driftYawBoost : 1f);
        float targetYawRate = SteerInput * steerSpeed * yawMul * speedFactor * dir;

        currentYawRate = Mathf.Lerp(currentYawRate, targetYawRate, yawInertia * dt);
        if (Mathf.Abs(currentYawRate) > 0.05f)
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, currentYawRate * dt, 0f));

        // Lateral Grip: low grip during drift / handbrake lets the tail swing wide!
        float grip = HandbrakeInput ? handbrakeGrip : (IsDrifting ? driftLateralGrip : normalLateralGrip);
        lateralSpeed = Mathf.Lerp(lateralSpeed, 0f, grip * dt);

        // Clamp max sideways slide so fishtails stay controllable & fun
        lateralSpeed = Mathf.Clamp(lateralSpeed, -28f, 28f);

        Vector3 newHorizontal = transform.forward * forwardSpeed + transform.right * lateralSpeed;
        rb.linearVelocity = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);
        Speed = forwardSpeed;

        // Visual Suspension Roll & Pitch
        if (bodyVisual != null)
        {
            float rollSource = IsDrifting ? Mathf.Clamp(DriftAngle / 28f, -1.2f, 1.2f) : SteerInput;
            float targetRoll = -rollSource * bodyRollAngle * speedFactor;
            float targetPitch = -ThrottleInput * bodyPitchAngle;
            if (IsBoosting) targetPitch = -bodyPitchAngle * 1.5f;

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

        // Front wheels visibly counter-steer into the fishtail slide!
        float targetSteer = IsDrifting && Mathf.Abs(SteerInput) < 0.2f
            ? Mathf.Clamp(DriftAngle * 0.85f, -32f, 32f)
            : SteerInput * 28f;
        steerVisualAngle = Mathf.Lerp(steerVisualAngle, targetSteer, 14f * dt);

        if (wheelFL != null) wheelFL.localRotation = Quaternion.Euler(wheelSpinAngle, steerVisualAngle, 0f);
        if (wheelFR != null) wheelFR.localRotation = Quaternion.Euler(wheelSpinAngle, steerVisualAngle, 0f);
        if (wheelRL != null) wheelRL.localRotation = Quaternion.Euler(wheelSpinAngle, 0f, 0f);
        if (wheelRR != null) wheelRR.localRotation = Quaternion.Euler(wheelSpinAngle, 0f, 0f);
    }

    public void ApplyBoost(float duration = 1.6f)
    {
        boostTimer = Mathf.Max(boostTimer, duration);
    }

    public void ResetCar(Vector3 pos, Quaternion rot)
    {
        boostTimer = 0f;
        currentYawRate = 0f;
        IsDrifting = false;
        transform.SetPositionAndRotation(pos, rot);
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
