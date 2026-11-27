using UnityEngine;

/// <summary>
/// Synthesized audio for the neon drift racer: engine drone, drift screech, boost whoosh.
/// Uses AudioSource with runtime-generated clips.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public DriftCarController car;

    [Header("Sources")]
    public AudioSource engineSource;
    public AudioSource driftSource;
    public AudioSource boostSource;
    public AudioSource musicSource;

    [Header("Engine")]
    public float engineMinPitch = 0.6f;
    public float engineMaxPitch = 2.0f;
    public float engineVolume = 0.3f;

    [Header("Drift")]
    public float driftVolume = 0.25f;

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<DriftCarController>();

        // Generate engine drone clip
        if (engineSource != null)
        {
            engineSource.clip = GenerateTone(220f, 2f);
            engineSource.loop = true;
            engineSource.volume = engineVolume;
            engineSource.Play();
        }

        // Drift screech
        if (driftSource != null)
        {
            driftSource.clip = GenerateNoise(1f);
            driftSource.loop = true;
            driftSource.volume = 0;
            driftSource.Play();
        }

        // Music (simple bassline loop)
        if (musicSource != null)
        {
            musicSource.clip = GenerateBassline(8f);
            musicSource.loop = true;
            musicSource.volume = 0.15f;
            musicSource.Play();
        }
    }

    void Update()
    {
        if (car == null) return;

        // Engine pitch follows speed
        if (engineSource != null)
        {
            float speedNorm = Mathf.Clamp01(Mathf.Abs(car.Speed) / car.maxSpeed);
            engineSource.pitch = Mathf.Lerp(engineMinPitch, engineMaxPitch, speedNorm);
        }

        // Drift screech volume
        if (driftSource != null)
        {
            float targetVol = car.IsDrifting ? driftVolume * Mathf.Clamp01(Mathf.Abs(car.DriftAngle) / 30f) : 0f;
            driftSource.volume = Mathf.Lerp(driftSource.volume, targetVol, 10f * Time.deltaTime);
            driftSource.pitch = 0.8f + Mathf.Abs(car.DriftAngle) / 90f * 0.4f;
        }
    }

    public void PlayBoostSound()
    {
        if (boostSource != null)
        {
            boostSource.clip = GenerateSweep(200, 800, 0.5f);
            boostSource.volume = 0.4f;
            boostSource.PlayOneShot(boostSource.clip);
        }
    }

    public void PlayCheckpointBlip()
    {
        if (boostSource != null)
        {
            var clip = GenerateTone(880, 0.1f);
            boostSource.PlayOneShot(clip, 0.5f);
        }
    }

    public void PlayLapSting()
    {
        if (boostSource != null)
        {
            var clip = GenerateSweep(440, 1760, 0.3f);
            boostSource.PlayOneShot(clip, 0.6f);
        }
    }

    // --- Audio synthesis ---
    static AudioClip GenerateTone(float freq, float duration, int sampleRate = 44100)
    {
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            data[i] = (2f * (t * freq % 1f) - 1f) * 0.3f;
            data[i] += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.15f;
        }
        var clip = AudioClip.Create("tone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip GenerateNoise(float duration, int sampleRate = 44100)
    {
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        float prev = 0;
        for (int i = 0; i < samples; i++)
        {
            float raw = Random.Range(-1f, 1f);
            prev = prev * 0.7f + raw * 0.3f;
            data[i] = prev * 0.4f;
        }
        var clip = AudioClip.Create("noise", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip GenerateSweep(float freqStart, float freqEnd, float duration, int sampleRate = 44100)
    {
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float norm = t / duration;
            float freq = Mathf.Lerp(freqStart, freqEnd, norm);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * (1f - norm) * 0.5f;
        }
        var clip = AudioClip.Create("sweep", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip GenerateBassline(float duration, int sampleRate = 44100)
    {
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];
        float[] notes = { 55f, 65.4f, 73.4f, 55f };
        float noteLen = duration / notes.Length;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            int noteIdx = Mathf.Min((int)(t / noteLen), notes.Length - 1);
            float freq = notes[noteIdx];
            float phase = (t * freq) % 1f;
            data[i] = (phase < 0.5f ? 0.3f : -0.3f) * 0.4f;
        }
        var clip = AudioClip.Create("bass", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
