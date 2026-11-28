using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Complete Runtime Auto-Builder for Neon Drift Racer:
/// Assembles the hero-banner cyberpunk world: wet mirror-reflective asphalt + Realtime ReflectionProbe,
/// alternating cyan/magenta rumble curbs, neon-railed barriers, dense inner & outer lit skyscrapers,
/// elevated monorail skybridge, atmospheric rain, sculpted Blender car with cyan underglow & headlights,
/// fishtail VFX, procedural audio, and cyberpunk HUD + live minimap.
/// </summary>
public class AutoSceneBuilder : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        if (GameObject.Find("NeonTrack") != null && GameObject.Find("NeonDriftCar") != null)
            return;

        CyberMaterialFactory.ClearCache();

        SetupLightingAndSky();
        SetupPostProcessing();
        var tw = SetupTrackAndWaypoints();
        SetupPropsAndCity(tw);
        var car = SetupCar(tw);
        SetupCamera(car);
        SetupReflectionProbe();
        SetupAudio(car);
        SetupSystemsAndHUD(car, tw);
    }

    static void SetupLightingAndSky()
    {
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights) Object.Destroy(l.gameObject);

        // Cool moonlight / neon key angled across the circuit
        var keyGO = new GameObject("Key Light");
        var key = keyGO.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(0.55f, 0.72f, 1.0f);
        key.intensity = 0.75f;
        key.shadows = LightShadows.Soft;
        keyGO.transform.rotation = Quaternion.Euler(38f, -35f, 0f);

        // Violet-indigo rim fill so dark surfaces stay rich
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.18f, 0.16f, 0.42f);
        RenderSettings.ambientEquatorColor = new Color(0.16f, 0.08f, 0.28f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.11f);

        var sky = CyberMaterialFactory.CreateCyberSkybox();
        if (sky != null) RenderSettings.skybox = sky;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.08f, 0.05f, 0.18f);
        RenderSettings.fogDensity = 0.0024f;
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
        bloom.threshold.value = 0.88f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 1.65f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.74f;

        var tone = profile.Add<Tonemapping>();
        tone.active = true;
        tone.mode.overrideState = true;
        tone.mode.value = TonemappingMode.ACES;

        var vig = profile.Add<Vignette>();
        vig.active = true;
        vig.intensity.overrideState = true;
        vig.intensity.value = 0.30f;
        vig.color.overrideState = true;
        vig.color.value = new Color(0.03f, 0.02f, 0.08f);

        var ca = profile.Add<ColorAdjustments>();
        ca.active = true;
        ca.contrast.overrideState = true;
        ca.contrast.value = 16f;
        ca.saturation.overrideState = true;
        ca.saturation.value = 18f;

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
            TrackMeshGenerator.BuildTrack(tw.waypoints, tw.roadHalfWidth * 2f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "CyberGridFloor";
        floor.transform.position = new Vector3(0f, -0.35f, 0f);
        floor.transform.localScale = new Vector3(160f, 1f, 160f);
        var fRend = floor.GetComponent<Renderer>();
        if (fRend != null) fRend.sharedMaterial = CyberMaterialFactory.CyberGridFloorMat();

        return tw;
    }

    static void SetupPropsAndCity(TrackWaypoints tw)
    {
        var root = new GameObject("CyberPropsAndCity");
        var rng = new System.Random(42);

        // 1. Outer Skyline (56 towers)
        for (int i = 0; i < 56; i++)
        {
            float angle = (float)i / 56f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.07f;
            float dist = 152f + (float)rng.NextDouble() * 95f;
            SpawnSkyscraper(root.transform, i, angle, dist, 55f, 165f, rng);
        }

        // 2. Inner Circuit Skyline (16 towers safely inside radius 0..56m; track inner edge >= 72m)
        for (int i = 0; i < 16; i++)
        {
            float angle = (float)i / 16f * Mathf.PI * 2f + 0.15f;
            float dist = 12f + (float)rng.NextDouble() * 44f;
            SpawnSkyscraper(root.transform, i + 100, angle, dist, 50f, 125f, rng);
        }

        // 3. Elevated Monorail Skybridge crossing over the track
        SpawnMonorailBridge(root.transform);

        // 4. 6 Checkpoint Gantries + 6 Glowing Boost Pads
        if (tw != null && tw.waypoints != null && tw.waypoints.Count >= 6)
        {
            int n = tw.waypoints.Count;
            int step = n / 6;
            float gateHalfW = tw.roadHalfWidth + 0.8f;

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

                for (int side = -1; side <= 1; side += 2)
                {
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pillar.transform.SetParent(gate.transform, false);
                    pillar.transform.localScale = new Vector3(0.8f, 8.8f, 0.8f);
                    pillar.transform.localPosition = new Vector3(side * gateHalfW, 4.4f, 0f);
                    Object.Destroy(pillar.GetComponent<Collider>());
                    pillar.GetComponent<Renderer>().sharedMaterial = CyberMaterialFactory.CarBodyMat();

                    var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    strip.transform.SetParent(gate.transform, false);
                    strip.transform.localScale = new Vector3(0.28f, 8.4f, 0.95f);
                    strip.transform.localPosition = new Vector3(side * gateHalfW, 4.2f, 0f);
                    Object.Destroy(strip.GetComponent<Collider>());
                    strip.GetComponent<Renderer>().sharedMaterial = gateMat;
                }

                var archTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                archTop.transform.SetParent(gate.transform, false);
                archTop.transform.localScale = new Vector3(gateHalfW * 2f + 1.4f, 0.95f, 1.1f);
                archTop.transform.localPosition = new Vector3(0f, 8.6f, 0f);
                Object.Destroy(archTop.GetComponent<Collider>());
                archTop.GetComponent<Renderer>().sharedMaterial = gateMat;

                // Boost Pad halfway between checkpoints
                int boostIdx = (i * step + step / 2) % n;
                var bwp = tw.waypoints[boostIdx];
                var padGO = new GameObject($"BoostPad_{i}");
                padGO.transform.SetParent(root.transform);
                padGO.transform.position = bwp.pos + Vector3.up * 0.08f;
                padGO.transform.rotation = Quaternion.LookRotation(bwp.forward);

                var padBox = padGO.AddComponent<BoxCollider>();
                padBox.isTrigger = true;
                padBox.size = new Vector3(5.8f, 2.5f, 7.0f);
                padGO.AddComponent<BoostPad>();

                var padVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                padVisual.transform.SetParent(padGO.transform, false);
                padVisual.transform.localScale = new Vector3(5.5f, 0.08f, 6.5f);
                Object.Destroy(padVisual.GetComponent<Collider>());
                padVisual.GetComponent<Renderer>().sharedMaterial = CyberMaterialFactory.BoostPadMat();
            }
        }
    }

    static void SpawnSkyscraper(Transform parent, int idx, float angle, float dist, float minH, float maxH, System.Random rng)
    {
        Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, -0.35f, Mathf.Sin(angle) * dist);
        var bldg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bldg.name = $"Skyscraper_{idx}";
        bldg.transform.SetParent(parent);

        float w = 15f + (float)rng.NextDouble() * 16f;
        float h = minH + (float)rng.NextDouble() * (maxH - minH);
        float d = 15f + (float)rng.NextDouble() * 16f;
        bldg.transform.localScale = new Vector3(w, h, d);
        bldg.transform.position = pos + Vector3.up * (h * 0.5f);
        bldg.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f);

        var rend = bldg.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = CyberMaterialFactory.BuildingFacadeMat(idx);

        var crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crown.name = "NeonCrown";
        crown.transform.SetParent(bldg.transform);
        crown.transform.localScale = new Vector3(1.03f, 0.03f, 1.03f);
        crown.transform.localPosition = new Vector3(0f, 0.485f, 0f);
        var cRend = crown.GetComponent<Renderer>();
        if (cRend != null)
        {
            cRend.sharedMaterial = (idx % 2 == 0)
                ? CyberMaterialFactory.NeonCyanMat()
                : CyberMaterialFactory.NeonMagentaMat();
        }
    }

    static void SpawnMonorailBridge(Transform parent)
    {
        var bridge = new GameObject("MonorailSkybridge");
        bridge.transform.SetParent(parent);
        bridge.transform.position = new Vector3(0f, 24f, 0f);
        bridge.transform.rotation = Quaternion.Euler(0f, 28f, 0f);

        var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.transform.SetParent(bridge.transform, false);
        deck.transform.localScale = new Vector3(4.5f, 1.6f, 380f);
        Object.Destroy(deck.GetComponent<Collider>());
        deck.GetComponent<Renderer>().sharedMaterial = CyberMaterialFactory.CarBodyMat();

        for (int side = -1; side <= 1; side += 2)
        {
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.transform.SetParent(bridge.transform, false);
            rail.transform.localScale = new Vector3(0.35f, 0.45f, 380f);
            rail.transform.localPosition = new Vector3(side * 2.35f, 0.5f, 0f);
            Object.Destroy(rail.GetComponent<Collider>());
            rail.GetComponent<Renderer>().sharedMaterial = CyberMaterialFactory.NeonCyanMat();
        }

        // Illuminated Monorail Train silhouette
        var train = GameObject.CreatePrimitive(PrimitiveType.Cube);
        train.transform.SetParent(bridge.transform, false);
        train.transform.localScale = new Vector3(3.2f, 2.6f, 34f);
        train.transform.localPosition = new Vector3(0f, 2.1f, -35f);
        Object.Destroy(train.GetComponent<Collider>());
        train.GetComponent<Renderer>().sharedMaterial = CyberMaterialFactory.NeonCyanMat();
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

        // Headlight SpotLights + Additive Volumetric Light Cones
        for (int side = -1; side <= 1; side += 2)
        {
            var hlGO = new GameObject($"Headlight_{side}");
            hlGO.transform.SetParent(carGO.transform, false);
            hlGO.transform.localPosition = new Vector3(side * 0.66f, 0.56f, 2.12f);
            hlGO.transform.localRotation = Quaternion.Euler(8f, side * 3f, 0f);

            var spot = hlGO.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.55f, 0.95f, 1.0f);
            spot.intensity = 28f;
            spot.range = 48f;
            spot.spotAngle = 62f;

            // Subtle additive light beam cone
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "HeadlightCone";
            beam.transform.SetParent(hlGO.transform, false);
            beam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            beam.transform.localPosition = new Vector3(0f, 0f, 3.6f);
            beam.transform.localScale = new Vector3(1.15f, 3.6f, 0.55f);
            Object.Destroy(beam.GetComponent<Collider>());
            beam.GetComponent<Renderer>().sharedMaterial = CyberMaterialFactory.HeadlightBeamMat();
        }

        // Bright Electric Cyan Underglow PointLight casting onto wet asphalt
        var underGO = new GameObject("UnderglowLight");
        underGO.transform.SetParent(carGO.transform, false);
        underGO.transform.localPosition = new Vector3(0f, 0.32f, 0f);
        var underLight = underGO.AddComponent<Light>();
        underLight.type = LightType.Point;
        underLight.color = Palette.NeonCyan;
        underLight.intensity = 10f;
        underLight.range = 10f;

        // Rear Magenta Taillight PointLight
        var tailGO = new GameObject("TaillightGlow");
        tailGO.transform.SetParent(carGO.transform, false);
        tailGO.transform.localPosition = new Vector3(0f, 0.65f, -2.35f);
        var tailLight = tailGO.AddComponent<Light>();
        tailLight.type = LightType.Point;
        tailLight.color = Palette.NeonMagenta;
        tailLight.intensity = 7f;
        tailLight.range = 9f;

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

        var urpCam = cam.GetComponent<UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        urpCam.renderPostProcessing = true;
        urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        urpCam.antialiasingQuality = AntialiasingQuality.High;

        var rig = cam.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
        if (car != null) rig.SetTarget(car.transform);

        // Atmospheric Cyberpunk Rain attached to Camera
        var rainGO = new GameObject("CyberRain");
        rainGO.transform.SetParent(cam.transform, false);
        rainGO.transform.localPosition = new Vector3(0f, 10f, 14f);
        var ps = rainGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.65f;
        main.startSpeed = 28f;
        main.startSize = 0.06f;
        main.startColor = new Color(0.55f, 0.85f, 1.0f, 0.25f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 160f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(36f, 2f, 36f);
        rainGO.transform.localRotation = Quaternion.Euler(82f, 0f, 0f);

        var pRend = rainGO.GetComponent<ParticleSystemRenderer>();
        if (pRend != null)
        {
            pRend.renderMode = ParticleSystemRenderMode.Stretch;
            pRend.lengthScale = 6f;
            pRend.material = CyberMaterialFactory.HeadlightBeamMat();
        }
    }

    static void SetupReflectionProbe()
    {
        var probeGO = new GameObject("CyberReflectionProbe");
        probeGO.transform.position = new Vector3(0f, 12f, 0f);
        var probe = probeGO.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.resolution = 256;
        probe.size = new Vector3(450f, 140f, 450f);
        probe.intensity = 1.15f;
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
        dotGO.AddComponent<Image>().color = Palette.ScoreGold;
        hud.minimapDot = dotRt;

        hud.driftText = CreateTMPText(canvasGO.transform, "DriftText",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(380f, 80f),
            "DRIFT x1.0", 48f, Palette.NeonCyan, TMPro.TextAlignmentOptions.Right);
        hud.driftText.rectTransform.anchoredPosition = new Vector2(-44f, 0f);

        hud.countdownText = CreateTMPText(canvasGO.transform, "CountdownText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(500f, 220f),
            "3", 136f, Palette.ScoreGold, TMPro.TextAlignmentOptions.Center);

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
        tex.SetPixels(new Color[res * res]);

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
        imgGO.AddComponent<RawImage>().texture = tex;
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
