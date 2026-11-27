using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

/// <summary>
/// Sets up the complete scene: materials, lighting, track, car, gates, props, audio, camera, HUD.
/// </summary>
public static class SceneBuilder
{
    [MenuItem("NeonDrift/Build Complete Scene")]
    public static void Build()
    {
        Debug.Log("[SceneBuilder] Starting scene build...");

        string scenePath = "Assets/Scenes/NeonDriftRacer.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

        // 1. Materials
        CreateMaterials();

        // 2. Lighting & Atmosphere
        SetupLighting();

        // 3. Post Processing Volume
        SetupPostProcessing();

        // 4. Track Instance
        SetupTrack();

        // 5. Waypoints & Lap Management
        SetupWaypointsAndLaps();

        // 6. Car Instance
        var car = SetupCar();

        // 7. Camera Rig
        SetupCamera(car);

        // 8. Audio Manager
        SetupAudio(car);

        // 9. Props (Boost pads, Gates, Skyline)
        SetupProps();

        // 10. HUD Canvas
        SetupHUD();

        // 11. GameLoop
        SetupGameLoop(car);

        // Save scene explicitly
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SceneBuilder] Scene build completed successfully and saved to " + scenePath);
    }

    static Material CreateURPMaterial(string name, Color color, bool emissive = false, float emission = 0f)
    {
        string dir = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = $"{dir}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            mat = new Material(shader);
            mat.name = name;
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);

        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void CreateMaterials()
    {
        CreateURPMaterial("M_RoadAsphalt", Palette.FloorInk);
        CreateURPMaterial("M_TrackBounds", Palette.BoundsMidnight);
        CreateURPMaterial("M_CarBody", Palette.CarBody);
        CreateURPMaterial("M_CarAccent", Palette.CarAccent);
        CreateURPMaterial("M_NeonCyan", Palette.NeonCyan, true, 3f);
        CreateURPMaterial("M_NeonMagenta", Palette.NeonMagenta, true, 3f);
        CreateURPMaterial("M_ScoreGold", Palette.ScoreGold, true, 2.5f);
        CreateURPMaterial("M_TireRubber", Palette.TireRubber);
        CreateURPMaterial("M_WheelRim", Palette.WheelRim);
        CreateURPMaterial("M_Building", Palette.Building);
        CreateURPMaterial("M_Abyss", Palette.Abyss);
        AssetDatabase.SaveAssets();
    }

    static void SetupLighting()
    {
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights) Object.DestroyImmediate(l.gameObject);

        // Key light (cool directional)
        var keyGO = new GameObject("Key Light");
        var key = keyGO.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(0.35f, 0.45f, 0.65f);
        key.intensity = 0.5f;
        key.shadows = LightShadows.Soft;
        keyGO.transform.rotation = Quaternion.Euler(50, -35, 0);

        // Ambient & Fog
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.04f, 0.05f, 0.09f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Palette.Abyss;
        RenderSettings.fogDensity = 0.005f;
        RenderSettings.skybox = null;
    }

    static void SetupPostProcessing()
    {
        var existing = GameObject.Find("Post Processing Volume");
        if (existing != null) Object.DestroyImmediate(existing);

        var ppGO = new GameObject("Post Processing Volume");
        var vol = ppGO.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1;

        string profDir = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(profDir))
            AssetDatabase.CreateFolder("Assets", "Settings");

        string profilePath = $"{profDir}/NeonDriftPostProcess.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        // Bloom
        Bloom bloom;
        if (!profile.TryGet(out bloom)) bloom = profile.Add<Bloom>();
        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.85f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 1.6f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.7f;

        // Tonemapping
        Tonemapping tone;
        if (!profile.TryGet(out tone)) tone = profile.Add<Tonemapping>();
        tone.active = true;
        tone.mode.overrideState = true;
        tone.mode.value = TonemappingMode.ACES;

        // Vignette
        Vignette vig;
        if (!profile.TryGet(out vig)) vig = profile.Add<Vignette>();
        vig.active = true;
        vig.intensity.overrideState = true;
        vig.intensity.value = 0.35f;
        vig.color.overrideState = true;
        vig.color.value = Palette.Abyss;

        // Color adjustments
        ColorAdjustments ca;
        if (!profile.TryGet(out ca)) ca = profile.Add<ColorAdjustments>();
        ca.active = true;
        ca.contrast.overrideState = true;
        ca.contrast.value = 15f;
        ca.saturation.overrideState = true;
        ca.saturation.value = 10f;

        vol.profile = profile;
        EditorUtility.SetDirty(profile);
    }

    static void SetupTrack()
    {
        var old = GameObject.Find("NeonTrack");
        if (old != null) Object.DestroyImmediate(old);

        var trackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/NeonTrack.fbx");
        if (trackPrefab != null)
        {
            var track = (GameObject)PrefabUtility.InstantiatePrefab(trackPrefab);
            track.name = "NeonTrack";
            track.transform.position = Vector3.zero;

            // Add mesh colliders to children
            foreach (var mf in track.GetComponentsInChildren<MeshFilter>())
            {
                var col = mf.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
            }
        }
    }

    static void SetupWaypointsAndLaps()
    {
        var old = GameObject.Find("TrackWaypoints");
        if (old != null) Object.DestroyImmediate(old);

        var twGO = new GameObject("TrackWaypoints");
        var tw = twGO.AddComponent<TrackWaypoints>();
        var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/track_waypoints.json");
        tw.waypointJson = jsonAsset;
        if (jsonAsset != null) tw.LoadFromJson(jsonAsset.text);

        var oldLM = GameObject.Find("LapManager");
        if (oldLM != null) Object.DestroyImmediate(oldLM);
        var lmGO = new GameObject("LapManager");
        var lm = lmGO.AddComponent<LapManager>();
        lm.totalCheckpoints = 6;
        lm.totalLaps = 3;
    }

    static DriftCarController SetupCar()
    {
        var old = GameObject.Find("NeonDriftCar");
        if (old != null) Object.DestroyImmediate(old);

        var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/NeonDriftCar.fbx");
        GameObject carGO;
        if (carPrefab != null)
        {
            carGO = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
            carGO.name = "NeonDriftCar";
        }
        else
        {
            carGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carGO.name = "NeonDriftCar";
            carGO.transform.localScale = new Vector3(2f, 0.8f, 4.2f);
        }

        // Spawn at first waypoint or near track start
        carGO.transform.position = new Vector3(0, 0.5f, 108f);
        carGO.transform.rotation = Quaternion.Euler(0, 90f, 0);

        var car = carGO.GetComponent<DriftCarController>();
        if (car == null) car = carGO.AddComponent<DriftCarController>();

        var boxCol = carGO.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = carGO.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(2.1f, 1.2f, 4.4f);
        boxCol.center = new Vector3(0, 0.6f, 0);

        // VFX
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

        cam.backgroundColor = Palette.Abyss;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1200f;

        var rig = cam.GetComponent<CameraRig>();
        if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
        if (car != null) rig.SetTarget(car.transform);
    }

    static void SetupAudio(DriftCarController car)
    {
        var old = GameObject.Find("AudioManager");
        if (old != null) Object.DestroyImmediate(old);

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
        src.spatialBlend = 0;
        return src;
    }

    static void SetupProps()
    {
        var propsRoot = GameObject.Find("Props");
        if (propsRoot != null) Object.DestroyImmediate(propsRoot);
        propsRoot = new GameObject("Props");

        var propsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/NeonProps.fbx");
        if (propsPrefab != null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(propsPrefab);
            inst.transform.SetParent(propsRoot.transform);
        }

        // Setup 6 checkpoint trigger gates around track
        var tw = Object.FindAnyObjectByType<TrackWaypoints>();
        if (tw != null && tw.waypoints.Count >= 6)
        {
            int step = tw.waypoints.Count / 6;
            for (int i = 0; i < 6; i++)
            {
                var wp = tw.waypoints[i * step];
                var gateGO = new GameObject($"Checkpoint_{i}");
                gateGO.transform.SetParent(propsRoot.transform);
                gateGO.transform.position = wp.pos + Vector3.up * 3f;
                gateGO.transform.rotation = Quaternion.LookRotation(wp.forward);

                var box = gateGO.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(tw.roadHalfWidth * 2f + 4f, 8f, 3f);

                var cp = gateGO.AddComponent<CheckpointGate>();
                cp.checkpointIndex = i;
            }
        }
    }

    static void SetupHUD()
    {
        var old = GameObject.Find("HUD Canvas");
        if (old != null) Object.DestroyImmediate(old);

        var canvasGO = new GameObject("HUD Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var hud = canvasGO.AddComponent<HUDController>();

        // Speed
        hud.speedText = CreateTMPText(canvasGO.transform, "SpeedText",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(400, 80),
            "0 KM/H", 48, Palette.NeonCyan, TMPro.TextAlignmentOptions.Center);
        hud.speedText.rectTransform.anchoredPosition = new Vector2(0, 60);

        // Lap
        hud.lapText = CreateTMPText(canvasGO.transform, "LapText",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, 50),
            "LAP 1/3", 32, Palette.UIText, TMPro.TextAlignmentOptions.TopLeft);
        hud.lapText.rectTransform.anchoredPosition = new Vector2(30, -30);

        // Timer
        hud.timerText = CreateTMPText(canvasGO.transform, "TimerText",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 50),
            "00:00.000", 36, Palette.UIText, TMPro.TextAlignmentOptions.Top);
        hud.timerText.rectTransform.anchoredPosition = new Vector2(0, -30);

        // Best Lap
        hud.bestLapText = CreateTMPText(canvasGO.transform, "BestLapText",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(300, 50),
            "BEST --:--.---", 28, Palette.ScoreGold, TMPro.TextAlignmentOptions.TopRight);
        hud.bestLapText.rectTransform.anchoredPosition = new Vector2(-30, -30);

        // Drift
        hud.driftText = CreateTMPText(canvasGO.transform, "DriftText",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(300, 60),
            "DRIFT x1.0", 42, Palette.NeonCyan, TMPro.TextAlignmentOptions.Right);
        hud.driftText.rectTransform.anchoredPosition = new Vector2(-40, 0);

        // Countdown
        hud.countdownText = CreateTMPText(canvasGO.transform, "CountdownText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(500, 200),
            "3", 120, Palette.ScoreGold, TMPro.TextAlignmentOptions.Center);

        // Message
        hud.messageText = CreateTMPText(canvasGO.transform, "MessageText",
            new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.4f), new Vector2(700, 180),
            "PRESS ENTER TO START", 36, Palette.UIText, TMPro.TextAlignmentOptions.Center);
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

        return tmp;
    }

    static void SetupGameLoop(DriftCarController car)
    {
        var old = GameObject.Find("GameLoop");
        if (old != null) Object.DestroyImmediate(old);

        var glGO = new GameObject("GameLoop");
        var gl = glGO.AddComponent<GameLoop>();
        gl.car = car;
        gl.lapManager = Object.FindAnyObjectByType<LapManager>();
    }
}
