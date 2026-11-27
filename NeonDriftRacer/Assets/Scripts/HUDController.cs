using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Cyberpunk HUD: speedometer + fill bar, lap/timer panels,
/// drift multiplier pop, live circuit minimap, and race state prompts.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("References")]
    public DriftCarController car;
    public LapManager lapManager;
    public GameLoop gameLoop;
    public TrackWaypoints trackWaypoints;

    [Header("UI Elements")]
    public TextMeshProUGUI speedText;
    public Image speedBarFill;
    public TextMeshProUGUI lapText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI bestLapText;
    public TextMeshProUGUI driftText;
    public TextMeshProUGUI countdownText;
    public TextMeshProUGUI messageText;

    [Header("Minimap")]
    public RectTransform minimapDot;
    public float minimapSize = 160f;
    Vector2 mapMin, mapMax;

    [Header("Drift Multiplier")]
    public float driftMultiplierGrowRate = 0.65f;
    public float driftMultiplierDecayDelay = 0.9f;

    float driftMultiplier = 1f;
    float driftDecayTimer;

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<DriftCarController>();
        if (lapManager == null) lapManager = FindAnyObjectByType<LapManager>();
        if (gameLoop == null) gameLoop = FindAnyObjectByType<GameLoop>();
        if (trackWaypoints == null) trackWaypoints = FindAnyObjectByType<TrackWaypoints>();

        ComputeMinimapBounds();
    }

    void ComputeMinimapBounds()
    {
        if (trackWaypoints == null || trackWaypoints.waypoints.Count == 0) return;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var wp in trackWaypoints.waypoints)
        {
            if (wp.pos.x < minX) minX = wp.pos.x;
            if (wp.pos.x > maxX) maxX = wp.pos.x;
            if (wp.pos.z < minZ) minZ = wp.pos.z;
            if (wp.pos.z > maxZ) maxZ = wp.pos.z;
        }
        float pad = 18f;
        mapMin = new Vector2(minX - pad, minZ - pad);
        mapMax = new Vector2(maxX + pad, maxZ + pad);
    }

    void Update()
    {
        if (car == null || lapManager == null || gameLoop == null) return;

        // Speed & Bar
        float absSpeed = Mathf.Abs(car.Speed);
        float kph = absSpeed * 3.6f;
        if (speedText) speedText.text = $"{kph:F0}<size=45%> KM/H</size>";

        if (speedBarFill)
        {
            float norm = Mathf.Clamp01(absSpeed / (car.maxSpeed * 1.3f));
            speedBarFill.fillAmount = norm;
            speedBarFill.color = car.IsBoosting
                ? Palette.ScoreGold
                : Color.Lerp(Palette.NeonCyan, Palette.NeonMagenta, norm);
        }

        // Lap & Timer
        if (lapText) lapText.text = $"LAP  <color=#00E5FF>{lapManager.CurrentLap}</color>/{lapManager.totalLaps}";
        if (timerText) timerText.text = FormatTime(lapManager.LapTime);

        if (bestLapText)
        {
            bestLapText.text = lapManager.BestLapTime < float.MaxValue
                ? $"BEST  {FormatTime(lapManager.BestLapTime)}"
                : "BEST  --:--.---";
        }

        // Drift multiplier
        if (car.IsDrifting)
        {
            driftMultiplier = Mathf.Min(driftMultiplier + driftMultiplierGrowRate * Time.deltaTime, 5f);
            driftDecayTimer = driftMultiplierDecayDelay;
        }
        else
        {
            driftDecayTimer -= Time.deltaTime;
            if (driftDecayTimer <= 0f)
                driftMultiplier = Mathf.Lerp(driftMultiplier, 1f, 3.5f * Time.deltaTime);
        }

        if (driftText)
        {
            if (driftMultiplier > 1.08f)
            {
                driftText.gameObject.SetActive(true);
                driftText.text = $"DRIFT  x{driftMultiplier:F1}";
                driftText.color = Color.Lerp(Palette.NeonCyan, Palette.NeonMagenta, (driftMultiplier - 1f) / 4f);
                float pulse = 1f + Mathf.Sin(Time.time * 12f) * 0.04f;
                driftText.transform.localScale = Vector3.one * pulse;
            }
            else
            {
                driftText.gameObject.SetActive(false);
            }
        }

        // Minimap Dot
        if (minimapDot != null && mapMax.x > mapMin.x)
        {
            float nx = Mathf.InverseLerp(mapMin.x, mapMax.x, car.transform.position.x);
            float ny = Mathf.InverseLerp(mapMin.y, mapMax.y, car.transform.position.z);
            minimapDot.anchoredPosition = new Vector2((nx - 0.5f) * minimapSize, (ny - 0.5f) * minimapSize);
        }

        // Countdown
        if (countdownText)
        {
            if (gameLoop.State == GameLoop.RunState.Countdown)
            {
                countdownText.gameObject.SetActive(true);
                int t = Mathf.CeilToInt(gameLoop.CountdownTimer);
                countdownText.text = t > 0 ? t.ToString() : "GO!";
                countdownText.color = t > 0 ? Palette.ScoreGold : Palette.NeonCyan;
            }
            else
            {
                countdownText.gameObject.SetActive(false);
            }
        }

        // Message
        if (messageText)
        {
            switch (gameLoop.State)
            {
                case GameLoop.RunState.Ready:
                    messageText.gameObject.SetActive(true);
                    messageText.text = "NEON DRIFT CIRCUIT\n<size=65%>PRESS <color=#00E5FF>ENTER</color> TO START RACE\n<color=#8FA3CC>WASD / ARROWS: DRIVE  •  SPACE: DRIFT</color></size>";
                    messageText.color = Palette.UIText;
                    break;
                case GameLoop.RunState.Won:
                    messageText.gameObject.SetActive(true);
                    messageText.text = $"RACE COMPLETE!\nTOTAL: {FormatTime(lapManager.TotalTime)}\n<size=65%>PRESS ENTER TO RESTART</size>";
                    messageText.color = Palette.ScoreGold;
                    break;
                case GameLoop.RunState.Lost:
                    messageText.gameObject.SetActive(true);
                    messageText.text = "TIME'S UP!\n<size=65%>PRESS ENTER TO RESTART</size>";
                    messageText.color = Palette.NeonMagenta;
                    break;
                default:
                    messageText.gameObject.SetActive(false);
                    break;
            }
        }
    }

    string FormatTime(float t)
    {
        int min = (int)(t / 60f);
        float sec = t % 60f;
        return $"{min:D2}:{sec:06.3F}";
    }
}
