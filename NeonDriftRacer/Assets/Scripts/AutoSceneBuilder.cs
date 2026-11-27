using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Complete Runtime Auto-Builder for Neon Drift Racer:
/// Loads the sculpted Blender FBX car, builds the wet-asphalt neon circuit,
/// glowing boost pads, checkpoint gantries, lit skyscraper cityscape, synthwave grid,
/// URP HDR post-processing (Bloom + ACES), car headlights, drift VFX, audio, and cyberpunk HUD.
/// </summary>
public class AutoSceneBuilder : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        if (GameObject.Find("NeonTrack") != null && GameObject.Find("NeonDriftCar") != null)
            return;

        CyberMaterialFactory.ClearCache();

        // 1. Lighting, Skybox & Fog
        SetupLightingAndSky();

        // 2. Post Processing Volume (ACES Tonemapping + HDR Bloom)
        SetupPostProcessing();

        // 3. Track & Waypoints + Cyber Grid Floor
        var tw = SetupTrackAndWaypoints();

        // 4. Props (Checkpoint Gantries, Boost Pads, Lit Skyscrapers)
        SetupPropsAndCity(tw);

        // 5. Sculpted Blender Car + Headlights + Drift VFX
        var car = SetupCar(tw);

        // 6. Camera Rig (with renderPostProcessing enabled!)
        SetupCamera(car);

        // 7. Audio Synthesis Engine
        SetupAudio(car);

        // 8. GameLoop & Cyberpunk HUD + Minimap
        SetupSystemsAndHUD(car, tw);
    }

    static void SetupLightingAndSky()
    {
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights) Object.Destroy(l.gameObject);

        // Cool key light angled across the track
        var keyGO = new GameObject("Key Light");
        var key = keyGO.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(0.55f, 0.72f, 1.0f);
        key.intensity = 0.85f;
        key.shadows = LightShadows.Soft;
        keyGO.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

        // Gradient environment lighting so shadow sides are rich indigo, never flat gray
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.14f, 0.20f, 0.42f);
        RenderSettings.ambientEquatorColor = new Color(0.12f, 0.08f, 0.24f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.10f);

        // Synthwave Skybox
        var sky = CyberMaterialFactory.CreateCyberSkybox();
        if (sky != null) RenderSettings.skybox = sky;

        // Atmospheric distance fog
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.12f);
        RenderSettings.fogDensity = 0.0022f;
    }

    static void SetupPostProcessing()
    {
        var existing = GameObject.Find("Post Processing Volume");
        if (existing != null) Object.Destroy(existing);

        var ppGO = new GameObject("Post Processing Volume");
        var vol = ppGO.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var bloom = profile.Add<Bloom>();
        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.95f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 1.45f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.7f;

        var tone = profile.Add<Tonemapping>();
        tone.active = true;
        tone.mode.overrideState = true;
        tone.mode.value = TonemappingMode.ACES;

        var vig = profile.Add<Vignette>();
        vig.active = true;
        vig.intensity.overrideState = true;
        vig.intensity.value = 0.28f;
        vig.color.overrideState = true;
        vig.color.value = Palette.Abyss;

        var ca = profile.Add<ColorAdjustments>();
        ca.active = true;
        ca.contrast.overrideState = true;
        ca.contrast.value = 14f;
        ca.saturation.overrideState = true;
        ca.saturation.value = 16f;

        vol.profile = profile;
    }

    static TrackWaypoints SetupTrackAndWaypoints()
    {
        var twGO = new GameObject("TrackWaypoints");
        var tw = twGO.AddComponent<TrackWaypoints>();

        var jsonAsset = Resources.Load<TextAsset>("track_waypoints");
        if (jsonAsset != null)
            tw.LoadFromJson(jsonAsset.text);

        if (tw.waypoints != null && tw.waypoints.Count >= 3)
        {
            var trackRoot = TrackMeshGenerator.BuildTrack(tw.waypoints, tw.roadHalfWidth * 2f);
            // Upgrade road & curb materials to textured wet asphalt + HDR neon
            var road = trackRoot.transform.Find("RoadSurface")?.GetComponent<Renderer>();
            if (road != null) road.sharedMaterial = CyberMaterialFactory.RoadAsphaltMat();

            var leftCurb = trackRoot.transform.Find("LeftNeonCurb")?.GetComponent<Renderer>();
            if (leftCurb != null) leftCurb.sharedMaterial = CyberMaterialFactory.NeonCyanMat();

            var rightCurb = trackRoot.transform.Find("RightNeonCurb")?.GetComponent<Renderer>();
            if (rightCurb != null) rightCurb.sharedMaterial = CyberMaterialFactory.NeonMagentaMat();

            var barriers = trackRoot.transform.Find("TrackBarriers")?.GetComponent<Renderer>();
            if (barriers != null) barriers.sharedMaterial = CyberMaterialFactory.TrackBoundsMat();
        }

        // Synthwave Neon Grid Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "CyberGridFloor";
        floor.transform.position = new Vector3(0f, -0.35f, 0f);
        floor.transform.localScale = new Vector3(140f, 1f, 140f);
        var fRend = floor.GetComponent<Renderer>();
        if (fRend != null) fRend.sharedMaterial = CyberMaterialFactory.CyberGridFloorMat();

        return tw;
    }

    static void SetupPropsAndCity(TrackWaypoints tw)
    {
        var root = new GameObject("CyberPropsAndCity");

        // 1. Lit Cyberpunk Skyscrapers around perimeter + inner skyline
        var rng = new System.Random(42);
        int towerCount = 48;
        for (int i = 0; i < towerCount; i++)
        {
            float angle = (float)i / towerCount * Mathf.PI * 2f + (float)rng.NextDouble() * 0.08f;
            float dist = 168f + (float)rng.NextDouble() * 95f;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, -0.35f, Mathf.Sin(angle) * dist);

            var bldg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bldg.name = $"Skyscraper_{i}";
            bldg.transform.SetParent(root.transform);

            float w = 16f + (float)rng.NextDouble() * 18f;
            float h = 55f + (float)rng.NextDouble() * 105f;
            float d = 16f + (float)rng.NextDouble() * 18f;
            bldg.transform.localScale = new Vector3(w, h, d);
            bldg.transform.position = pos + Vector3.up * (h * 0.5f);
            bldg.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f);

            var rend = bldg.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = CyberMaterialFactory.BuildingFacadeMat(i);

            // Glowing neon crown trim
            var crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crown.name = "NeonCrown";
            crown.transform.SetParent(bldg.transform);
            crown.transform.localScale = new Vector3(1.03f, 0.035f, 1.03f);
            crown.transform.localPosition = new Vector3(0f, 0.48f, 0f);
            var cRend = crown.GetComponent<Renderer>();
            if (cRend != null)
            {
                cRend.sharedMaterial = (i % 2 == 0)
                    ? CyberMaterialFactory.NeonCyanMat()
                    : CyberMaterialFactory.NeonMagentaMat();
            }
        }

        // 2. 6 Checkpoint Gantries + 6 Glowing Boost Pads
        if (tw != null && tw.waypoints != null && tw.waypoints.Count >= 6)
        {
            int n = tw.waypoints.Count;
            int step = n / 6;
            float gateHalfW = tw.roadHalfWidth + 0.6f;

            for (int i = 0; i < 6; i++)
            {
                var wp = tw.waypoints[i * step];
                var gate = new GameObject($"CheckpointGate_{i}");
                gate.transform.SetParent(root.transform);
                gate.transform.position = wp.pos;
                gate.transform.rotation = Quaternion.LookRotation(wp.forward);

                var box = gate.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 4f, 0f);
                box.size = new Vector3(tw.roadHalfWidth * 2f + 2f, 9f, 3f);

                var cp = gate.AddComponent<CheckpointGate>();
                cp.checkpointIndex = i;

                Material gateMat = (i == 0)
                    ? CyberMaterialFactory.ScoreGoldMat()
                    : CyberMaterialFactory.NeonMagentaMat();

                // Left & Right Pillars + Top Beam
                for (int side = -1; side <= 1; side += 2)
                {
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pillar.transform.SetParent(gate.transform, false);
                    pillar.transform.localScale = new Vector3(0.75f, 8.5f, 0.75f);
                    pillar.transform.localPosition = new Vector3(side * gateHalfW, 4.25f, 0f);
                    Object.Destroy(pillar.GetComponent<Collider>());
                    pillar.GetComponent<Renderer>().sharedMaterial = CyberMaterialFactory.TrackBoundsMat();

                    var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    strip.transform.SetParent(gate.transform, false);
                    strip.transform.localScale = new Vector3(0.25f, 8.2f, 0.9f);
                    strip.transform.localPosition = new Vector3(side * gateHalfW, 4.1f, 0f);
                    Object.Destroy(strip.GetComponent<Collider>());
                    strip.GetComponent<Renderer>().sharedMaterial = gateMat;
                }

                var archTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                archTop.transform.SetParent(gate.transform, false);
                archTop.transform.localScale = new Vector3(gateHalfW * 2f + 1.2f, 0.9f, 1.0f);
                archTop.transform.localPosition = new Vector3(0f, 8.4f, 0f);
                Object.Destroy(archTop.GetComponent<Collider>());
                archTop.GetComponent<Renderer>().sharedMaterial = gateMat;

                // Boost Pad placed halfway between checkpoints
                int boostIdx = (i * step + step / 2) % n;
                var bwp = tw.waypoints[boostIdx];
                var padGO = new GameObject($"BoostPad_{i}");
                padGO.transform.SetParent(root.transform);
                padGO.transform.position = bwp.pos + Vector3.up * 0.08f;
                padGO.transform.rotation = Quaternion.LookRotation(bwp.forward);

                var padBox = padGO.AddComponent<BoxCollider>();
                padBox.isTrigger = true;
                padBox.size = new Vector3(5.5f, 2.5f, 6.5f);
                padGO.AddComponent<BoostPad>();

                var padVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                padVisual.transform.SetParent(padGO.transform, false);
                padVisual.transform.localScale = new Vector3(5.2f, 0.08f, 6.0f);
                Object.Destroy(padVisual.GetComponent<Collider>());
                padVisual.GetComponent<Renderer>().sharedMaterial = CyberMaterialFactory.BoostPadMat();
            }
        }
    }

    static DriftCarController SetupCar(TrackWaypoints tw)
    {
        var existing = GameObject.Find("NeonDriftCar");
        if (existing != null) Object.Destroy(existing);

        Vector3 spawnPos = new Vector3(0f, 3.4f, -106.6f);
        Quaternion spawnRot = Quaternion.Euler(0f, 250f, 0f);
        if (tw != null && tw.waypoints != null && tw.waypoints.Count > 0)
        {
            spawnPos = tw.waypoints[0].pos + Vector3.up * 0.65f;
            spawnRot = Quaternion.LookRotation(tw.waypoints[0].forward);
        }

        GameObject carGO = null;
        var fbxPrefab = Resources.Load<GameObject>("Models/NeonDriftCar");
        if (fbxPrefab != null)
        {
            carGO = Object.Instantiate(fbxPrefab);
            carGO.name = "NeonDriftCar";
            CyberMaterialFactory.RemapFBXMaterials(carGO);
        }
        else
        {
            carGO = new GameObject("NeonDriftCar");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "CarBodyVisual";
            body.transform.SetParent(carGO.transform);
            body.transform.localScale = new Vector3(2.1f, 0.8f, 4.4f);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.GetComponent<Renderer>().sharedMaterial = CyberMaterialFactory.CarBodyMat();
        }

        carGO.transform.SetPositionAndRotation(spawnPos, spawnRot);

        // Headlights + Rear Underglow PointLight
        for (int side = -1; side <= 1; side += 2)
        {
            var hlGO = new GameObject($"Headlight_{side}");
            hlGO.transform.SetParent(carGO.transform, false);
            hlGO.transform.localPosition = new Vector3(side * 0.65f, 0.55f, 2.15f);
            hlGO.transform.localRotation = Quaternion.Euler(10f, side * 4f, 0f);
            var spot = hlGO.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.75f, 0.95f, 1.0f);
            spot.intensity = 18f;
            spot.range = 42f;
            spot.spotAngle = 60f;
        }

        var glowGO = new GameObject("UnderglowLight");
        glowGO.transform.SetParent(carGO.transform, false);
        glowGO.transform.localPosition = new Vector3(0f, 0.35f, -0.5f);
        var pt = glowGO.AddComponent<Light>();
        pt.type = LightType.Point;
        pt.color = Palette.NeonMagenta;
        pt.intensity = 4.5f;
        pt.range = 8f;

        var col = carGO.GetComponent<BoxCollider>() ?? carGO.AddComponent<BoxCollider>();
        col.size = new Vector3(2.05f, 1.05f, 4.4f);
        col.center = new Vector3(0f, 0.6f, 0f);

        var car = carGO.GetComponent<DriftCarController>() ?? carGO.AddComponent<DriftCarController>();
        car.CacheModelParts();

        if (carGO.GetComponent<DriftVFX>() == null)
            carGO.AddComponent<DriftVFX>();

        return car;
    }

    static void SetupCamera(DriftCarController car)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGO = new GameObject("Main Camera");
            cam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();
        }

        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = Palette.Abyss;
        cam.fieldOfView = 66f;
        cam.farClipPlane = 1000f;

        // CRITICAL: Enable Post-Processing & AA on URP Camera Data so Bloom renders!
        var urpCam = cam.GetComponent<UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        urpCam.renderPostProcessing = true;
        urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        urpCam.antialiasingQuality = AntialiasingQuality.High;

        var rig = cam.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
        if (car != null) rig.SetTarget(car.transform);
    }

    static void SetupAudio(DriftCarController car)
    {
        var existing = GameObject.Find("AudioManager");
        if (existing != null) Object.Destroy(existing);

        var audioGO = new GameObject("AudioManager");
        var am = audioGO.AddComponent<AudioManager>();
        am.car = car;

        am.engineSource = CreateAudioSource(audioGO, "Engine");
        am.driftSource = CreateAudioSource(audioGO, "Drift");
        am.boostSource = CreateAudioSource(audioGO, "Boost");
        am.musicSource = CreateAudioSource(audioGO, "Music");
    }

    static AudioSource CreateAudioSource(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        return src;
    }

    static void SetupSystemsAndHUD(DriftCarController car, TrackWaypoints tw)
    {
        var lm = Object.FindAnyObjectByType<LapManager>();
        if (lm == null)
        {
            var lmGO = new GameObject("LapManager");
            lm = lmGO.AddComponent<LapManager>();
            lm.totalCheckpoints = 6;
            lm.totalLaps = 3;
        }

        var gl = Object.FindAnyObjectByType<GameLoop>();
        if (gl == null)
        {
            var glGO = new GameObject("GameLoop");
            gl = glGO.AddComponent<GameLoop>();
            gl.car = car;
            gl.lapManager = lm;
        }

        // Wire audio cues to checkpoints and laps
        var am = Object.FindAnyObjectByType<AudioManager>();
        if (am != null)
        {
            lm.onCheckpointHit.AddListener(_ => am.PlayCheckpointBlip());
            lm.onLapComplete.AddListener(_ => am.PlayLapSting());
        }

        var canvasGO = new GameObject("HUD Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var hud = canvasGO.AddComponent<HUDController>();
        hud.car = car;
        hud.lapManager = lm;
        hud.gameLoop = gl;
        hud.trackWaypoints = tw;

        // Bottom-Right Speedometer Panel
        var speedPanel = CreateNeonPanel(canvasGO.transform, "SpeedPanel",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 36f), new Vector2(320f, 118f),
            new Vector2(1f, 0f), Palette.NeonCyan);

        hud.speedText = CreateTMPText(speedPanel.transform, "SpeedText",
            new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(280f, 64f),
            "0 KM/H", 54f, Palette.UIText, TMPro.TextAlignmentOptions.Center);

        var barBg = new GameObject("SpeedBarBG");
        barBg.transform.SetParent(speedPanel.transform, false);
        var barBgRt = barBg.AddComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0.08f, 0.16f);
        barBgRt.anchorMax = new Vector2(0.92f, 0.28f);
        barBgRt.offsetMin = Vector2.zero;
        barBgRt.offsetMax = Vector2.zero;
        barBg.AddComponent<Image>().color = new Color(0.1f, 0.14f, 0.25f, 0.9f);

        var barFill = new GameObject("SpeedBarFill");
        barFill.transform.SetParent(barBg.transform, false);
        var bfRt = barFill.AddComponent<RectTransform>();
        bfRt.anchorMin = Vector2.zero;
        bfRt.anchorMax = Vector2.one;
        bfRt.offsetMin = Vector2.zero;
        bfRt.offsetMax = Vector2.zero;
        var fillImg = barFill.AddComponent<Image>();
        fillImg.color = Palette.NeonCyan;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;
        hud.speedBarFill = fillImg;

        // Top-Left Lap Panel
        var lapPanel = CreateNeonPanel(canvasGO.transform, "LapPanel",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -36f), new Vector2(260f, 70f),
            new Vector2(0f, 1f), Palette.NeonCyan);
        hud.lapText = CreateTMPText(lapPanel.transform, "LapText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230f, 50f),
            "LAP 1/3", 32f, Palette.UIText, TMPro.TextAlignmentOptions.Center);

        // Top-Right Timer Panel
        var timerPanel = CreateNeonPanel(canvasGO.transform, "TimerPanel",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -36f), new Vector2(310f, 96f),
            new Vector2(1f, 1f), Palette.NeonMagenta);
        hud.timerText = CreateTMPText(timerPanel.transform, "TimerText",
            new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f), new Vector2(280f, 44f),
            "00:00.000", 36f, Palette.UIText, TMPro.TextAlignmentOptions.Center);
        hud.bestLapText = CreateTMPText(timerPanel.transform, "BestLapText",
            new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(280f, 34f),
            "BEST --:--.---", 22f, Palette.ScoreGold, TMPro.TextAlignmentOptions.Center);

        // Bottom-Left Minimap Panel
        var mapPanel = CreateNeonPanel(canvasGO.transform, "MinimapPanel",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 36f), new Vector2(190f, 190f),
            new Vector2(0f, 0f), Palette.NeonCyan);
        hud.minimapSize = 156f;
        CreateMinimapTrackGraphic(mapPanel.transform, tw, 156f);

        var dotGO = new GameObject("PlayerDot");
        dotGO.transform.SetParent(mapPanel.transform, false);
        var dotRt = dotGO.AddComponent<RectTransform>();
        dotRt.sizeDelta = new Vector2(12f, 12f);
        var dotImg = dotGO.AddComponent<Image>();
        dotImg.color = Palette.ScoreGold;
        hud.minimapDot = dotRt;

        // Drift Multiplier Combo
        hud.driftText = CreateTMPText(canvasGO.transform, "DriftText",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(380f, 80f),
            "DRIFT x1.0", 48f, Palette.NeonCyan, TMPro.TextAlignmentOptions.Right);
        hud.driftText.rectTransform.anchoredPosition = new Vector2(-44f, 0f);

        // Countdown Center
        hud.countdownText = CreateTMPText(canvasGO.transform, "CountdownText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(500f, 220f),
            "3", 136f, Palette.ScoreGold, TMPro.TextAlignmentOptions.Center);

        // Center Message
        hud.messageText = CreateTMPText(canvasGO.transform, "MessageText",
            new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.36f), new Vector2(880f, 200f),
            "PRESS ENTER TO START", 40f, Palette.UIText, TMPro.TextAlignmentOptions.Center);
    }

    static GameObject CreateNeonPanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size,
        Vector2 pivot, Color borderColor)
    {
        var border = new GameObject(name);
        border.transform.SetParent(parent, false);
        var brt = border.AddComponent<RectTransform>();
        brt.anchorMin = anchorMin;
        brt.anchorMax = anchorMax;
        brt.pivot = pivot;
        brt.anchoredPosition = anchoredPos;
        brt.sizeDelta = size;
        border.AddComponent<Image>().color = new Color(borderColor.r, borderColor.g, borderColor.b, 0.75f);

        var inner = new GameObject("Inner");
        inner.transform.SetParent(border.transform, false);
        var irt = inner.AddComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(2f, 2f);
        irt.offsetMax = new Vector2(-2f, -2f);
        inner.AddComponent<Image>().color = Palette.UIPanel;
        return inner;
    }

    static void CreateMinimapTrackGraphic(Transform parent, TrackWaypoints tw, float size)
    {
        if (tw == null || tw.waypoints.Count == 0) return;
        int res = 128;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        var clear = new Color[res * res];
        tex.SetPixels(clear);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var wp in tw.waypoints)
        {
            if (wp.pos.x < minX) minX = wp.pos.x;
            if (wp.pos.x > maxX) maxX = wp.pos.x;
            if (wp.pos.z < minZ) minZ = wp.pos.z;
            if (wp.pos.z > maxZ) maxZ = wp.pos.z;
        }
        float pad = 18f;
        minX -= pad; maxX += pad; minZ -= pad; maxZ += pad;

        foreach (var wp in tw.waypoints)
        {
            int px = Mathf.Clamp((int)(Mathf.InverseLerp(minX, maxX, wp.pos.x) * (res - 1)), 1, res - 2);
            int py = Mathf.Clamp((int)(Mathf.InverseLerp(minZ, maxZ, wp.pos.z) * (res - 1)), 1, res - 2);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    tex.SetPixel(px + dx, py + dy, Palette.NeonCyan);
        }
        tex.Apply();

        var imgGO = new GameObject("TrackOutline");
        imgGO.transform.SetParent(parent, false);
        var rt = imgGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        var raw = imgGO.AddComponent<RawImage>();
        raw.texture = tex;
    }

    static TMPro.TextMeshProUGUI CreateTMPText(
        Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 size,
        string text, float fontSize, Color color,
        TMPro.TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        return tmp;
    }
}
