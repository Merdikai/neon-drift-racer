using UnityEngine;

/// <summary>
/// Hero-style Drift VFX: sweeping hot-magenta taillight light-ribbons,
/// electric-cyan rear tire skid trails, and stylized drift smoke puffs.
/// </summary>
public class DriftVFX : MonoBehaviour
{
    public DriftCarController car;
    public TrailRenderer[] skidTrails;
    public TrailRenderer[] taillightRibbons;
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

        // 1. Hot Magenta Taillight Light-Ribbons (always stream at speed, flare on drift/boost!)
        var ribbonMat = new Material(unlitShader);
        ribbonMat.SetColor("_BaseColor", Palette.NeonMagenta * 3.5f);
        ribbonMat.color = Palette.NeonMagenta;

        taillightRibbons = new TrailRenderer[2];
        Vector3[] tailOffsets = { new Vector3(-0.76f, 0.64f, -2.16f), new Vector3(0.76f, 0.64f, -2.16f) };
        for (int i = 0; i < 2; i++)
        {
            var rGO = new GameObject($"TaillightRibbon_{i}");
            rGO.transform.SetParent(transform, false);
            rGO.transform.localPosition = tailOffsets[i];

            var tr = rGO.AddComponent<TrailRenderer>();
            tr.time = 0.55f;
            tr.startWidth = 0.16f;
            tr.endWidth = 0.0f;
            tr.minVertexDistance = 0.15f;
            tr.material = ribbonMat;
            tr.emitting = false;
            taillightRibbons[i] = tr;
        }

        // 2. Electric Cyan Rear Tire Skid Trails + Drift Smoke
        var skidMat = new Material(unlitShader);
        skidMat.SetColor("_BaseColor", Palette.NeonCyan * 2.8f);
        skidMat.color = Palette.NeonCyan;

        skidTrails = new TrailRenderer[2];
        tireSmoke = new ParticleSystem[2];
        Vector3[] rearOffsets = { new Vector3(-0.98f, 0.06f, -1.38f), new Vector3(0.98f, 0.06f, -1.38f) };

        for (int i = 0; i < 2; i++)
        {
            var tGO = new GameObject($"SkidTrail_{i}");
            tGO.transform.SetParent(transform, false);
            tGO.transform.localPosition = rearOffsets[i];

            var tr = tGO.AddComponent<TrailRenderer>();
            tr.time = 1.6f;
            tr.startWidth = 0.26f;
            tr.endWidth = 0.04f;
            tr.minVertexDistance = 0.18f;
            tr.material = skidMat;
            tr.emitting = false;
            skidTrails[i] = tr;

            // Stylized drift smoke
            var pGO = new GameObject($"TireSmoke_{i}");
            pGO.transform.SetParent(transform, false);
            pGO.transform.localPosition = rearOffsets[i];

            var ps = pGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.65f;
            main.startSpeed = 3.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.05f);
            main.startColor = new Color(0.55f, 0.92f, 1.0f, 0.32f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var em = ps.emission;
            em.rateOverTime = 36f;
            em.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.18f;

            var pRend = pGO.GetComponent<ParticleSystemRenderer>();
            if (pRend != null) pRend.material = skidMat;

            tireSmoke[i] = ps;
        }
    }

    void Update()
    {
        if (car == null) return;

        bool drifting = car.IsDrifting;
        bool movingFast = Mathf.Abs(car.Speed) > 16f;
        bool boosting = car.IsBoosting;

        if (taillightRibbons != null)
        {
            foreach (var tr in taillightRibbons)
            {
                if (tr == null) continue;
                tr.emitting = movingFast || drifting || boosting;
                tr.time = boosting ? 0.9f : (drifting ? 0.7f : 0.38f);
                tr.startWidth = boosting ? 0.28f : (drifting ? 0.20f : 0.11f);
            }
        }

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
    }
}
