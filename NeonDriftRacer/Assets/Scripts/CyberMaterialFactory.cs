using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates procedural wet-asphalt PBR textures, alternating cyan/magenta rumble curbs,
/// neon-striped barrier walls, glowing skyscraper facades, and a synthwave night skybox.
/// </summary>
public static class CyberMaterialFactory
{
    static Dictionary<string, Material> cache = new Dictionary<string, Material>();

    public static void ClearCache() => cache.Clear();

    static Shader LitShader() =>
        Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");

    static Shader UnlitShader() =>
        Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");

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
        m.SetColor("_BaseColor", new Color(0.10f, 0.13f, 0.22f, 1f));
        m.SetFloat("_Metallic", 0.92f);
        m.SetFloat("_Smoothness", 0.90f);
        return m;
    });

    public static Material CarAccentMat() => GetOrCreate("M_CarAccent", () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", Palette.CarAccent);
        m.SetFloat("_Metallic", 0.72f);
        m.SetFloat("_Smoothness", 0.78f);
        return m;
    });

    public static Material GlassCanopyMat() => GetOrCreate("M_GlassCanopy", () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", new Color(0.02f, 0.05f, 0.12f, 1f));
        m.SetFloat("_Metallic", 0.55f);
        m.SetFloat("_Smoothness", 0.97f);
        return m;
    });

    public static Material TireRubberMat() => GetOrCreate("M_TireRubber", () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", Palette.TireRubber);
        m.SetFloat("_Metallic", 0.05f);
        m.SetFloat("_Smoothness", 0.25f);
        return m;
    });

    public static Material WheelRimMat() => GetOrCreate("M_WheelRim", () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", Palette.WheelRim);
        m.SetFloat("_Metallic", 0.92f);
        m.SetFloat("_Smoothness", 0.85f);
        return m;
    });

    public static Material EmissiveMat(string key, Color color, float intensity) => GetOrCreate(key, () =>
    {
        var m = new Material(LitShader());
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", 0.1f);
        m.SetFloat("_Smoothness", 0.6f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return m;
    });

    public static Material NeonCyanMat() => EmissiveMat("M_NeonCyan", Palette.NeonCyan, 4.8f);
    public static Material NeonMagentaMat() => EmissiveMat("M_NeonMagenta", Palette.NeonMagenta, 4.8f);
    public static Material ScoreGoldMat() => EmissiveMat("M_ScoreGold", Palette.ScoreGold, 4.0f);

    /// <summary>
    /// Wet mirror-slick dark asphalt with subtle puddle streaks & glowing lane lines.
    /// </summary>
    public static Material RoadAsphaltMat() => GetOrCreate("M_RoadAsphalt", () =>
    {
        var tex = GenerateRoadTexture(512, 512, out var emTex);
        var m = new Material(LitShader());
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0.28f);
        m.SetFloat("_Smoothness", 0.91f); // wet mirror reflections!
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", emTex);
        m.SetColor("_EmissionColor", Color.white * 1.8f);
        return m;
    });

    /// <summary>
    /// Alternating Electric Cyan & Hot Magenta rumble-strip curbs.
    /// </summary>
    public static Material NeonRumbleCurbMat(bool flipOrder = false) => GetOrCreate($"M_RumbleCurb_{flipOrder}", () =>
    {
        var tex = GenerateRumbleCurbTexture(128, 256, flipOrder);
        var m = new Material(LitShader());
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0.2f);
        m.SetFloat("_Smoothness", 0.7f);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", Color.white * 3.2f);
        return m;
    });

    /// <summary>
    /// Dark metallic track barrier walls with dual horizontal neon light rails.
    /// </summary>
    public static Material TrackBarriersMat() => GetOrCreate("M_TrackBarriers", () =>
    {
        var tex = GenerateBarrierTexture(256, 256, out var emTex);
        var m = new Material(LitShader());
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0.65f);
        m.SetFloat("_Smoothness", 0.82f);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", emTex);
        m.SetColor("_EmissionColor", Color.white * 3.8f);
        return m;
    });

    public static Material CyberGridFloorMat() => GetOrCreate("M_CyberGridFloor", () =>
    {
        var tex = GenerateGridTexture(512, 512);
        var m = new Material(LitShader());
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0.45f);
        m.SetFloat("_Smoothness", 0.85f);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", new Color(0.25f, 0.75f, 1.0f) * 1.15f);
        m.mainTextureScale = new Vector2(110f, 110f);
        return m;
    });

    public static Material BuildingFacadeMat(int variant) => GetOrCreate($"M_Building_{variant % 6}", () =>
    {
        int v = variant % 6;
        Color primary = (v % 2 == 0) ? Palette.NeonCyan : Palette.NeonMagenta;
        Color secondary = (v % 2 == 0) ? Palette.NeonMagenta : Palette.NeonCyan;
        var tex = GenerateBuildingTexture(256, 512, primary, secondary, v * 31 + 7, out var emTex);
        var m = new Material(LitShader());
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0.5f);
        m.SetFloat("_Smoothness", 0.75f);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", emTex);
        m.SetColor("_EmissionColor", Color.white * 2.8f);
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
        m.SetColor("_EmissionColor", Color.white * 3.8f);
        return m;
    });

    public static Material HeadlightBeamMat() => GetOrCreate("M_HeadlightBeam", () =>
    {
        var m = new Material(UnlitShader());
        m.SetColor("_BaseColor", new Color(0.0f, 0.90f, 1.0f, 0.16f));
        // Configure URP Unlit Transparent
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 1f); // Additive
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        return m;
    });

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
                else if (n.Contains("TrackBounds"))   dst[i] = TrackBarriersMat();
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

        var sideTex = GenerateSkyGradientTexture(128, 256);
        var topTex = GenerateSolidTexture(16, 16, new Color(0.02f, 0.015f, 0.08f));
        var botTex = GenerateSolidTexture(16, 16, new Color(0.025f, 0.03f, 0.08f));

        var sky = new Material(skyShader);
        sky.name = "M_CyberSkybox";
        sky.SetTexture("_FrontTex", sideTex);
        sky.SetTexture("_BackTex", sideTex);
        sky.SetTexture("_LeftTex", sideTex);
        sky.SetTexture("_RightTex", sideTex);
        sky.SetTexture("_UpTex", topTex);
        sky.SetTexture("_DownTex", botTex);
        sky.SetColor("_Tint", new Color(0.55f, 0.52f, 0.65f, 1f));
        sky.SetFloat("_Exposure", 1.25f);
        return sky;
    }

    // --- Procedural Textures ---

    static Texture2D GenerateRoadTexture(int w, int h, out Texture2D emissionTex)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        emissionTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        emissionTex.wrapMode = TextureWrapMode.Repeat;
        tex.anisoLevel = 8;

        var cols = new Color[w * h];
        var ems = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / h;
            bool centerDash = (v % 0.25f) < 0.13f;
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w;
                float grain = Mathf.PerlinNoise(x * 0.25f, y * 0.25f) * 0.035f;
                float puddle = Mathf.PerlinNoise(x * 0.04f, y * 0.02f) * 0.025f;
                float laneDist = Mathf.Min(Mathf.Abs(u - 0.30f), Mathf.Abs(u - 0.70f));
                float wear = Mathf.Exp(-laneDist * laneDist * 130f) * -0.02f;

                Color c = new Color(0.05f + grain + puddle + wear, 0.065f + grain + puddle + wear, 0.12f + grain + puddle + wear, 1f);
                Color e = Color.black;

                if (u < 0.028f)
                {
                    c = Palette.NeonCyan;
                    e = Palette.NeonCyan * 1.1f;
                }
                else if (u > 0.972f)
                {
                    c = Palette.NeonMagenta;
                    e = Palette.NeonMagenta * 1.1f;
                }
                else if (Mathf.Abs(u - 0.5f) < 0.011f && centerDash)
                {
                    c = Palette.NeonCyan;
                    e = Palette.NeonCyan * 0.65f;
                }

                cols[y * w + x] = c;
                ems[y * w + x] = e;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        emissionTex.SetPixels(ems);
        emissionTex.Apply();
        return tex;
    }

    static Texture2D GenerateRumbleCurbTexture(int w, int h, bool flip)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.anisoLevel = 8;
        var cols = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / h;
            bool band = (v % 0.5f) < 0.25f;
            if (flip) band = !band;
            Color c = band ? Palette.NeonCyan : Palette.NeonMagenta;
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w;
                // Slight dark border at edge
                cols[y * w + x] = (u < 0.1f || u > 0.9f) ? new Color(0.04f, 0.05f, 0.1f, 1f) : c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateBarrierTexture(int w, int h, out Texture2D emissionTex)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        emissionTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        emissionTex.wrapMode = TextureWrapMode.Repeat;

        var cols = new Color[w * h];
        var ems = new Color[w * h];
        Color metal = new Color(0.07f, 0.09f, 0.18f, 1f);

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / h; // vertical height along barrier wall (0 = bottom, 1 = top)
            bool cyanRail = Mathf.Abs(v - 0.36f) < 0.075f;
            bool magRail = Mathf.Abs(v - 0.78f) < 0.055f;

            for (int x = 0; x < w; x++)
            {
                Color c = metal;
                Color e = Color.black;
                if (cyanRail) { c = Palette.NeonCyan; e = Palette.NeonCyan; }
                else if (magRail) { c = Palette.NeonMagenta; e = Palette.NeonMagenta * 0.9f; }

                cols[y * w + x] = c;
                ems[y * w + x] = e;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        emissionTex.SetPixels(ems);
        emissionTex.Apply();
        return tex;
    }

    static Texture2D GenerateGridTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.anisoLevel = 8;
        var cols = new Color[w * h];
        Color bg = new Color(0.02f, 0.03f, 0.07f, 1f);
        Color glowLine = new Color(0.06f, 0.38f, 0.68f, 1f);
        Color coreLine = new Color(0.0f, 0.90f, 1.0f, 1f);

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

    static Texture2D GenerateBuildingTexture(int w, int h, Color primary, Color secondary, int seed, out Texture2D emissionTex)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        emissionTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        emissionTex.wrapMode = TextureWrapMode.Repeat;

        var cols = new Color[w * h];
        var ems = new Color[w * h];
        var rng = new System.Random(seed);

        int colsGrid = 8;
        int rowsGrid = 22;
        bool[,] lit = new bool[colsGrid, rowsGrid];
        bool[,] useSec = new bool[colsGrid, rowsGrid];
        float[,] bright = new float[colsGrid, rowsGrid];

        for (int gx = 0; gx < colsGrid; gx++)
        {
            for (int gy = 0; gy < rowsGrid; gy++)
            {
                lit[gx, gy] = rng.NextDouble() < 0.42;
                useSec[gx, gy] = rng.NextDouble() < 0.25;
                bright[gx, gy] = 0.55f + (float)rng.NextDouble() * 0.45f;
            }
        }

        Color wall = new Color(0.04f, 0.055f, 0.12f, 1f);
        Color darkWin = new Color(0.02f, 0.028f, 0.06f, 1f);

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

                // Vertical neon billboard strip on one edge
                if (u > 0.88f && u < 0.94f && v > 0.25f && v < 0.72f)
                {
                    cols[y * w + x] = secondary;
                    ems[y * w + x] = secondary * 1.35f;
                    continue;
                }

                bool isWindow = fx > 0.15f && fx < 0.85f && fy > 0.24f && fy < 0.76f;
                if (isWindow)
                {
                    if (lit[gx, gy])
                    {
                        Color wc = (useSec[gx, gy] ? secondary : primary) * bright[gx, gy];
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

    static Texture2D GenerateSkyGradientTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var cols = new Color[w * h];
        Color zenith = new Color(0.04f, 0.02f, 0.12f, 1f);
        Color mid = new Color(0.14f, 0.07f, 0.28f, 1f);
        Color horizon = new Color(0.28f, 0.08f, 0.38f, 1f);
        Color cyanBand = new Color(0.05f, 0.28f, 0.45f, 1f);

        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color c;
            if (t < 0.48f)
                c = new Color(0.02f, 0.03f, 0.07f, 1f);
            else if (t < 0.54f)
                c = Color.Lerp(cyanBand, horizon, (t - 0.48f) / 0.06f);
            else if (t < 0.72f)
                c = Color.Lerp(horizon, mid, (t - 0.54f) / 0.18f);
            else
                c = Color.Lerp(mid, zenith, (t - 0.72f) / 0.28f);

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
