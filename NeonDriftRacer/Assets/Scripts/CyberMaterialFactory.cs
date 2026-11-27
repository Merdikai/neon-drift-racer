using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates crisp procedural PBR textures, URP materials, and a synthwave night skybox
/// so every surface has rich detail, reflections, and neon glow.
/// </summary>
public static class CyberMaterialFactory
{
    static Dictionary<string, Material> cache = new Dictionary<string, Material>();

    public static void ClearCache() => cache.Clear();

    static Shader LitShader() =>
        Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");

    public static Material GetOrCreate(string key, System.Func<Material> builder)
    {
        if (cache.TryGetValue(key, out var m) && m != null) return m;
        m = builder();
        m.name = key;
        cache[key] = m;
        return m;
    }

    public static Material CarBodyMat() => GetOrCreate("M_CarBody", () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", Palette.CarBody);
        m.SetFloat("_Metallic", 0.85f);
        m.SetFloat("_Smoothness", 0.86f);
        return m;
    });

    public static Material CarAccentMat() => GetOrCreate("M_CarAccent", () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", Palette.CarAccent);
        m.SetFloat("_Metallic", 0.65f);
        m.SetFloat("_Smoothness", 0.72f);
        return m;
    });

    public static Material GlassCanopyMat() => GetOrCreate("M_GlassCanopy", () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", new Color(0.03f, 0.06f, 0.14f, 1f));
        m.SetFloat("_Metallic", 0.4f);
        m.SetFloat("_Smoothness", 0.96f);
        return m;
    });

    public static Material TireRubberMat() => GetOrCreate("M_TireRubber", () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", Palette.TireRubber);
        m.SetFloat("_Metallic", 0.05f);
        m.SetFloat("_Smoothness", 0.2f);
        return m;
    });

    public static Material WheelRimMat() => GetOrCreate("M_WheelRim", () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", Palette.WheelRim);
        m.SetFloat("_Metallic", 0.9f);
        m.SetFloat("_Smoothness", 0.82f);
        return m;
    });

    public static Material TrackBoundsMat() => GetOrCreate("M_TrackBounds", () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", Palette.BoundsMidnight);
        m.SetFloat("_Metallic", 0.45f);
        m.SetFloat("_Smoothness", 0.62f);
        return m;
    });

    public static Material EmissiveMat(string key, Color color, float intensity) => GetOrCreate(key, () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", 0.1f);
        m.SetFloat("_Smoothness", 0.5f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return m;
    });

    public static Material NeonCyanMat() => EmissiveMat("M_NeonCyan", Palette.NeonCyan, 4.2f);
    public static Material NeonMagentaMat() => EmissiveMat("M_NeonMagenta", Palette.NeonMagenta, 4.2f);
    public static Material ScoreGoldMat() => EmissiveMat("M_ScoreGold", Palette.ScoreGold, 3.6f);

    public static Material RoadAsphaltMat() => GetOrCreate("M_RoadAsphalt", () =>
    {
        var tex = GenerateRoadTexture(512, 512);
        var m = new Material(LitShader());
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0.15f);
        m.SetFloat("_Smoothness", 0.72f);

        // Subtle emissive lane stripes
        var emTex = GenerateRoadEmissionTexture(512, 512);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", emTex);
        m.SetColor("_EmissionColor", Color.white * 1.4f);
        return m;
    });

    public static Material CyberGridFloorMat() => GetOrCreate("M_CyberGridFloor", () =>
    {
        var tex = GenerateGridTexture(512, 512);
        var m = new Material(LitShader());
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0.35f);
        m.SetFloat("_Smoothness", 0.78f);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", new Color(0.25f, 0.7f, 1.0f) * 1.1f);
        m.mainTextureScale = new Vector2(90f, 90f);
        return m;
    });

    public static Material BuildingFacadeMat(int variant) => GetOrCreate($"M_Building_{variant}", () =>
    {
        Color winCol = (variant % 3 == 0) ? Palette.NeonCyan :
                       (variant % 3 == 1) ? Palette.NeonMagenta : Palette.ScoreGold;
        var tex = GenerateBuildingTexture(256, 512, winCol, variant * 17 + 5, out var emTex);
        var m = new Material(LitShader());
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0.4f);
        m.SetFloat("_Smoothness", 0.65f);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", emTex);
        m.SetColor("_EmissionColor", Color.white * 2.2f);
        return m;
    });

    public static Material BoostPadMat() => GetOrCreate("M_BoostPad", () =>
    {
        var tex = GenerateBoostChevronTexture(256, 256);
        var m = new Material(LitShader());
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", Color.white * 3.2f);
        return m;
    });

    /// <summary>
    /// Remaps imported Blender FBX materials to calibrated URP HDR materials.
    /// </summary>
    public static void RemapFBXMaterials(GameObject root)
    {
        if (root == null) return;
        foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
        {
            var src = rend.sharedMaterials;
            var dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                string n = src[i] != null ? src[i].name : "";
                if (n.Contains("NeonCyan"))           dst[i] = NeonCyanMat();
                else if (n.Contains("NeonMagenta"))   dst[i] = NeonMagentaMat();
                else if (n.Contains("ScoreGold"))     dst[i] = ScoreGoldMat();
                else if (n.Contains("CarBody"))       dst[i] = CarBodyMat();
                else if (n.Contains("CarAccent"))     dst[i] = CarAccentMat();
                else if (n.Contains("GlassCanopy"))   dst[i] = GlassCanopyMat();
                else if (n.Contains("TireRubber"))    dst[i] = TireRubberMat();
                else if (n.Contains("WheelRim"))      dst[i] = WheelRimMat();
                else if (n.Contains("RoadAsphalt"))   dst[i] = RoadAsphaltMat();
                else if (n.Contains("TrackBounds"))   dst[i] = TrackBoundsMat();
                else if (n.Contains("Building"))      dst[i] = BuildingFacadeMat(0);
                else                                  dst[i] = CarAccentMat();
            }
            rend.sharedMaterials = dst;
        }
    }

    public static Material CreateCyberSkybox()
    {
        var skyShader = Shader.Find("Skybox/6 Sided");
        if (skyShader == null) return null;

        var sideTex = GenerateSkyGradientTexture(128, 256, true);
        var topTex = GenerateSolidTexture(16, 16, new Color(0.015f, 0.02f, 0.055f));
        var botTex = GenerateSolidTexture(16, 16, new Color(0.03f, 0.04f, 0.09f));

        var sky = new Material(skyShader);
        sky.name = "M_CyberSkybox";
        sky.SetTexture("_FrontTex", sideTex);
        sky.SetTexture("_BackTex", sideTex);
        sky.SetTexture("_LeftTex", sideTex);
        sky.SetTexture("_RightTex", sideTex);
        sky.SetTexture("_UpTex", topTex);
        sky.SetTexture("_DownTex", botTex);
        sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));
        sky.SetFloat("_Exposure", 1.1f);
        return sky;
    }

    // --- Procedural Texture Generators ---

    static Texture2D GenerateRoadTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        var cols = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / h;
            bool centerDash = (v % 0.25f) < 0.14f;
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w;
                float grain = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.04f;
                // Subtle dark wet tire tracks
                float laneDist = Mathf.Min(Mathf.Abs(u - 0.32f), Mathf.Abs(u - 0.68f));
                float wear = Mathf.Exp(-laneDist * laneDist * 140f) * -0.025f;

                Color c = new Color(0.065f + grain + wear, 0.085f + grain + wear, 0.145f + grain + wear, 1f);

                // Edge stripes
                if (u < 0.035f || u > 0.965f)
                    c = Color.Lerp(c, Palette.NeonCyan, 0.65f);
                // Center dashed line
                else if (Mathf.Abs(u - 0.5f) < 0.012f && centerDash)
                    c = Color.Lerp(c, new Color(0.35f, 0.85f, 1f), 0.7f);

                cols[y * w + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateRoadEmissionTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        var cols = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / h;
            bool centerDash = (v % 0.25f) < 0.14f;
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w;
                Color c = Color.black;
                if (u < 0.03f) c = Palette.NeonCyan * 0.8f;
                else if (u > 0.97f) c = Palette.NeonMagenta * 0.8f;
                else if (Mathf.Abs(u - 0.5f) < 0.01f && centerDash) c = Palette.NeonCyan * 0.45f;
                cols[y * w + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateGridTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.anisoLevel = 8;
        var cols = new Color[w * h];
        Color bg = new Color(0.025f, 0.035f, 0.075f, 1f);
        Color glowLine = new Color(0.05f, 0.35f, 0.65f, 1f);
        Color coreLine = new Color(0.0f, 0.85f, 1.0f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int dx = Mathf.Min(x, w - 1 - x);
                int dy = Mathf.Min(y, h - 1 - y);
                int d = Mathf.Min(dx, dy);

                if (d < 3) cols[y * w + x] = coreLine;
                else if (d < 10) cols[y * w + x] = Color.Lerp(glowLine, bg, (d - 3) / 7f);
                else cols[y * w + x] = bg;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateBuildingTexture(int w, int h, Color accent, int seed, out Texture2D emissionTex)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        emissionTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        emissionTex.wrapMode = TextureWrapMode.Repeat;

        var cols = new Color[w * h];
        var ems = new Color[w * h];
        var rng = new System.Random(seed);

        int colsGrid = 8;
        int rowsGrid = 24;
        bool[,] lit = new bool[colsGrid, rowsGrid];
        float[,] bright = new float[colsGrid, rowsGrid];
        for (int gx = 0; gx < colsGrid; gx++)
        {
            for (int gy = 0; gy < rowsGrid; gy++)
            {
                lit[gx, gy] = rng.NextDouble() < 0.32;
                bright[gx, gy] = 0.4f + (float)rng.NextDouble() * 0.6f;
            }
        }

        Color wall = new Color(0.045f, 0.065f, 0.125f, 1f);
        Color darkWin = new Color(0.02f, 0.03f, 0.06f, 1f);

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / h;
            int gy = Mathf.Min((int)(v * rowsGrid), rowsGrid - 1);
            float fy = (v * rowsGrid) - gy;

            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w;
                int gx = Mathf.Min((int)(u * colsGrid), colsGrid - 1);
                float fx = (u * colsGrid) - gx;

                bool isWindow = fx > 0.18f && fx < 0.82f && fy > 0.25f && fy < 0.78f;
                if (isWindow)
                {
                    if (lit[gx, gy])
                    {
                        Color wc = accent * bright[gx, gy];
                        cols[y * w + x] = wc;
                        ems[y * w + x] = wc;
                    }
                    else
                    {
                        cols[y * w + x] = darkWin;
                        ems[y * w + x] = Color.black;
                    }
                }
                else
                {
                    cols[y * w + x] = wall;
                    ems[y * w + x] = Color.black;
                }
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        emissionTex.SetPixels(ems);
        emissionTex.Apply();
        return tex;
    }

    static Texture2D GenerateBoostChevronTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        var cols = new Color[w * h];
        Color bg = new Color(0.04f, 0.08f, 0.16f, 1f);

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / h;
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w;
                float centerDist = Mathf.Abs(u - 0.5f);
                float pattern = (v * 3f + centerDist * 1.6f) % 1f;
                bool chevron = pattern < 0.35f && centerDist < 0.42f;
                cols[y * w + x] = chevron ? Palette.NeonCyan : bg;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateSkyGradientTexture(int w, int h, bool horizonGlow)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var cols = new Color[w * h];
        Color zenith = new Color(0.015f, 0.02f, 0.055f, 1f);
        Color mid = new Color(0.07f, 0.05f, 0.18f, 1f);
        Color horizon = new Color(0.18f, 0.06f, 0.28f, 1f);
        Color cyanBand = new Color(0.04f, 0.22f, 0.36f, 1f);

        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color c;
            if (t < 0.48f)
                c = new Color(0.02f, 0.03f, 0.07f, 1f);
            else if (t < 0.54f)
                c = Color.Lerp(cyanBand, horizon, (t - 0.48f) / 0.06f);
            else if (t < 0.70f)
                c = Color.Lerp(horizon, mid, (t - 0.54f) / 0.16f);
            else
                c = Color.Lerp(mid, zenith, (t - 0.70f) / 0.30f);

            for (int x = 0; x < w; x++)
                cols[y * w + x] = c;
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateSolidTexture(int w, int h, Color c)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var cols = new Color[w * h];
        for (int i = 0; i < cols.Length; i++) cols[i] = c;
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }
}
