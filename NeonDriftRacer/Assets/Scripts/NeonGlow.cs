using UnityEngine;

/// <summary>
/// Pulses emissive intensity on neon materials for the cyberpunk feel.
/// </summary>
public class NeonGlow : MonoBehaviour
{
    public Renderer targetRenderer;
    public int materialIndex;
    public Color baseEmission = Palette.NeonCyanHDR;

    [Header("Pulse")]
    public float pulseSpeed = 2f;
    public float pulseMin = 0.6f;
    public float pulseMax = 1.2f;

    MaterialPropertyBlock mpb;

    void Start()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (targetRenderer == null) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(pulseMin, pulseMax, t);

        targetRenderer.GetPropertyBlock(mpb, materialIndex);
        mpb.SetColor("_EmissionColor", baseEmission * intensity);
        targetRenderer.SetPropertyBlock(mpb, materialIndex);
    }
}
