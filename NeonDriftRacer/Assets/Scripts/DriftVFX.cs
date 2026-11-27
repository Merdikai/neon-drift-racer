using UnityEngine;

/// <summary>
/// Runtime-configured Drift VFX: glowing neon tire trails, drift smoke puffs,
/// and thruster boost trails attached directly to the car's wheels and rear.
/// </summary>
public class DriftVFX : MonoBehaviour
{
    public DriftCarController car;
    public TrailRenderer[] skidTrails;
    public TrailRenderer[] boostTrails;
    public ParticleSystem[] tireSmoke;

    void Start()
    {
        if (car == null) car = GetComponent<DriftCarController>();
        if (skidTrails == null || skidTrails.Length == 0)
            BuildDefaultVFX();
    }

    public void BuildDefaultVFX()
    {
        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");

        // Rear tire skid trails (glowing magenta)
        var trailMat = new Material(unlitShader);
        trailMat.SetColor("_BaseColor", Palette.NeonMagenta * 2.2f);
        trailMat.color = Palette.NeonMagenta;

        skidTrails = new TrailRenderer[2];
        tireSmoke = new ParticleSystem[2];
        Vector3[] rearOffsets = { new Vector3(-0.98f, 0.06f, -1.38f), new Vector3(0.98f, 0.06f, -1.38f) };

        for (int i = 0; i < 2; i++)
        {
            var tGO = new GameObject($"SkidTrail_{i}");
            tGO.transform.SetParent(transform, false);
            tGO.transform.localPosition = rearOffsets[i];

            var tr = tGO.AddComponent<TrailRenderer>();
            tr.time = 1.4f;
            tr.startWidth = 0.24f;
            tr.endWidth = 0.05f;
            tr.minVertexDistance = 0.2f;
            tr.material = trailMat;
            tr.emitting = false;
            skidTrails[i] = tr;

            // Tire smoke particle system
            var pGO = new GameObject($"TireSmoke_{i}");
            pGO.transform.SetParent(transform, false);
            pGO.transform.localPosition = rearOffsets[i];

            var ps = pGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.55f;
            main.startSpeed = 2.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            main.startColor = new Color(0.0f, 0.9f, 1.0f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var em = ps.emission;
            em.rateOverTime = 28f;
            em.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.15f;

            var pRend = pGO.GetComponent<ParticleSystemRenderer>();
            if (pRend != null) pRend.material = trailMat;

            tireSmoke[i] = ps;
        }

        // Boost thruster trails (electric cyan)
        var boostMat = new Material(unlitShader);
        boostMat.SetColor("_BaseColor", Palette.NeonCyan * 3.0f);
        boostMat.color = Palette.NeonCyan;

        boostTrails = new TrailRenderer[2];
        Vector3[] exhaustOffsets = { new Vector3(-0.36f, 0.38f, -2.18f), new Vector3(0.36f, 0.38f, -2.18f) };
        for (int i = 0; i < 2; i++)
        {
            var bGO = new GameObject($"BoostTrail_{i}");
            bGO.transform.SetParent(transform, false);
            bGO.transform.localPosition = exhaustOffsets[i];

            var tr = bGO.AddComponent<TrailRenderer>();
            tr.time = 0.35f;
            tr.startWidth = 0.18f;
            tr.endWidth = 0.0f;
            tr.material = boostMat;
            tr.emitting = false;
            boostTrails[i] = tr;
        }
    }

    void Update()
    {
        if (car == null) return;

        bool drifting = car.IsDrifting;
        bool boosting = car.IsBoosting || car.Speed > car.maxSpeed * 0.92f;

        if (skidTrails != null)
        {
            foreach (var tr in skidTrails)
                if (tr != null) tr.emitting = drifting;
        }

        if (tireSmoke != null)
        {
            foreach (var ps in tireSmoke)
            {
                if (ps == null) continue;
                var em = ps.emission;
                em.enabled = drifting;
            }
        }

        if (boostTrails != null)
        {
            foreach (var tr in boostTrails)
                if (tr != null) tr.emitting = boosting;
        }
    }
}
