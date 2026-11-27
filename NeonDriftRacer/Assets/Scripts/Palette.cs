using UnityEngine;

/// <summary>
/// Single source of truth for every color in Neon Drift Racer.
/// Art bible: "Flat-shaded neon arcade on near-black asphalt; everything that scores, glows."
/// </summary>
public static class Palette
{
    // --- surfaces ---
    public static readonly Color FloorInk      = Hex("#111626");
    public static readonly Color BoundsMidnight = Hex("#1E2747");
    public static readonly Color CarBody        = Hex("#141824");
    public static readonly Color CarAccent      = Hex("#1F2842");

    // --- neon ---
    public static readonly Color NeonCyan    = Hex("#00E5FF");
    public static readonly Color NeonMagenta = Hex("#FF2E93");
    public static readonly Color ScoreGold   = Hex("#FFE14D");

    // --- vehicle ---
    public static readonly Color TireRubber = Hex("#0B0D12");
    public static readonly Color WheelRim   = Hex("#232C44");

    // --- environment ---
    public static readonly Color Building = Hex("#0C1222");
    public static readonly Color Abyss    = Hex("#080A14");

    // --- UI ---
    public static readonly Color UIBackground = Hex("#0A0E1A");
    public static readonly Color UIPanel       = new Color(0.06f, 0.08f, 0.15f, 0.85f);
    public static readonly Color UIText        = Hex("#E0E8FF");

    // --- neon HDR (emissive, intensity 3) ---
    public static readonly Color NeonCyanHDR    = Hex("#00E5FF") * 3f;
    public static readonly Color NeonMagentaHDR = Hex("#FF2E93") * 3f;
    public static readonly Color ScoreGoldHDR   = Hex("#FFE14D") * 3f;

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
