using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

// 뒤틀린 과자 동화 분위기(TwistedCandyPlan.md §2.1·§2.2·V1). 맵 빌더(MapSceneBuilder)가 만든 씬 위에 덧칠한다:
// 안개·주변광·방향광·카메라 배경색, 맵 PostFX 프로필의 색 보정·비네트, 점광원 색 빼기, 고장 난 전구(FlickerLight).
// 대기실은 PostFX를 새로 붙이고, 로비는 배경 그림을 해 질 녘 판으로 바꾼다.
// 맵을 다시 만들면(MapSceneBuilder) 이 값이 사라지므로 다시 실행한다. 여러 번 실행해도 같은 결과다 —
// 점광원 색·강도는 FlickerLight가 아직 없는 씬에서만 바꾼다(처음 한 번).
// 진저브레드는 GingerbreadNeonDimmer가 정한 블룸을 건드리지 않는다.
public static class TwistedAtmosphere
{
    private const string MenuRoot = "Tools/TagOfChaos/Maps/";
    private const string MapSceneFolder = "Assets/Scenes/Maps";
    public const string GameLobbyScene = "Assets/Scenes/GameLobbyScene.unity";
    public const string LobbyScene = "Assets/Scenes/LobbyScene.unity";
    public const string GameLobbyProfile = "Assets/Maps/GameLobby/GameLobby_PostFX.asset";
    public const string LobbyBackground = "Assets/08. Resources/LobbySceneBackGround_Twisted.png";

    public sealed class Look
    {
        public Color Fog;
        public float FogDensity;
        public float AmbientScale = 1f;
        public bool OverrideSun;
        public Color SunColor;
        public float SunIntensity;
        public float Saturation, Temperature, Tint, Contrast, PostExposure, Vignette;
        public float PointDesaturate;        // 점광원 색을 회색 쪽으로(0~1)
        public float PointScale = 1f;        // 점광원 강도 배율
        public string[] FlickerPrefixes = new string[0];
        public int FlickerEvery = 1;         // 이름이 맞는 조명 중 n개마다 하나
        public string[] BoostPrefixes = new string[0]; // 더 밝게(대기실 가마솥 빛)
        public float BoostScale = 1f;
    }

    // §2.1 목업 값을 게임용으로 다듬은 값 — 안개는 목업의 약 0.8배, 밤 맵은 노출을 올려(+0.6~1.0) 화면 평균 밝기를
    // 원래의 60~70%로 맞췄다(목업 그대로면 30~40%라 길·괴물이 안 보인다, §7 C5).
    public static readonly Dictionary<string, Look> Maps = new Dictionary<string, Look>
    {
        ["CandyForest"] = new Look
        {
            Fog = new Color(0.42f, 0.44f, 0.42f), FogDensity = 0.018f, AmbientScale = 0.75f,
            OverrideSun = true, SunColor = new Color(0.8f, 0.85f, 0.9f), SunIntensity = 0.6f,
            Saturation = -35f, Temperature = -8f, Tint = -14f, Contrast = 18f, PostExposure = 0f, Vignette = 0.42f,
            PointDesaturate = 0.5f, PointScale = 0.8f, FlickerPrefixes = new[] { "CAN_", "LGT_Fill" }, FlickerEvery = 25,
        },
        ["GingerbreadVillage"] = new Look
        {
            Fog = new Color(0.12f, 0.14f, 0.2f), FogDensity = 0.016f,
            Saturation = -45f, Temperature = -8f, Tint = -4f, Contrast = 20f, PostExposure = 0.9f, Vignette = 0.45f,
            PointDesaturate = 0.3f, PointScale = 0.85f, FlickerPrefixes = new[] { "GIN_Lamp" }, FlickerEvery = 2,
        },
        ["ChocolateFactory"] = new Look
        {
            Fog = new Color(0.3f, 0.24f, 0.18f), FogDensity = 0.022f,
            Saturation = -30f, Temperature = 12f, Tint = 0f, Contrast = 20f, PostExposure = 0.5f, Vignette = 0.45f,
            PointDesaturate = 0.2f, PointScale = 0.9f, FlickerPrefixes = new[] { "LGT_CandyMachine", "LGT_ArchWindow" }, FlickerEvery = 3,
        },
        ["CursedCandyCarnival"] = new Look
        {
            Fog = new Color(0.08f, 0.12f, 0.1f), FogDensity = 0.02f,
            Saturation = -50f, Temperature = -4f, Tint = -22f, Contrast = 25f, PostExposure = 1.0f, Vignette = 0.5f,
            PointDesaturate = 0.25f, FlickerPrefixes = new[] { "LGT_CarnivalLamp", "LGT_Booth", "LGT_CircusTent" }, FlickerEvery = 2,
        },
        ["HauntedBakery"] = new Look
        {
            Fog = new Color(0.15f, 0.17f, 0.22f), FogDensity = 0.016f,
            Saturation = -40f, Temperature = -18f, Tint = 0f, Contrast = 18f, PostExposure = 0.6f, Vignette = 0.45f,
            PointDesaturate = 0.3f, FlickerPrefixes = new[] { "LGT_WallLantern", "LGT_Table" }, FlickerEvery = 3,
        },
    };

    // 대기실(★★): 이미 어두운 밤 — 등불은 줄이고 깜빡이게, 가마솥 빛만 강하게, 안개 조금 더.
    public static readonly Look GameLobby = new Look
    {
        Fog = new Color(0.08f, 0.07f, 0.12f), FogDensity = 0.03f,
        Saturation = -25f, Temperature = -6f, Tint = 0f, Contrast = 15f, PostExposure = 0.3f, Vignette = 0.42f,
        PointDesaturate = 0.25f, PointScale = 0.75f, FlickerPrefixes = new[] { "Light" }, FlickerEvery = 3,
        BoostPrefixes = new[] { "FireLight", "LiquidGlowLight" }, BoostScale = 1.6f,
    };

    public static string MapScenePath(string map) => $"{MapSceneFolder}/Game_{map}.unity";

    [MenuItem(MenuRoot + "Apply Twisted Atmosphere (All)")]
    public static void ApplyAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var report = new List<string>();
        foreach (KeyValuePair<string, Look> kv in Maps)
        {
            var scene = EditorSceneManager.OpenScene(MapScenePath(kv.Key), OpenSceneMode.Single);
            report.Add($"{kv.Key}: {ApplyToOpenMap(kv.Key)}");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        var lobby = EditorSceneManager.OpenScene(GameLobbyScene, OpenSceneMode.Single);
        report.Add($"GameLobby: {ApplyToOpenGameLobby()}");
        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);

        var menu = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);
        report.Add($"Lobby: {ApplyToOpenLobby()}");
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        AssetDatabase.SaveAssets();
        Debug.Log("[TwistedAtmosphere]\n" + string.Join("\n", report));
    }

    public static string ApplyToOpenMap(string map)
    {
        Look look = Maps[map];
        string env = ApplyEnvironment(look);
        var volume = Object.FindFirstObjectByType<PostProcessVolume>();
        if (volume == null || volume.sharedProfile == null) return env + ", no PostFX";
        ApplyGrading(volume.sharedProfile, look);
        return env + $", grading -> {AssetDatabase.GetAssetPath(volume.sharedProfile)}";
    }

    public static string ApplyToOpenGameLobby()
    {
        string env = ApplyEnvironment(GameLobby);
        PostProcessProfile profile = LoadOrCreateProfile(GameLobbyProfile);
        if (!profile.TryGetSettings(out Bloom bloom)) bloom = AddSetting<Bloom>(profile);
        bloom.intensity.Override(0.9f);
        bloom.threshold.Override(1.1f);
        bloom.diffusion.Override(7f);
        ApplyGrading(profile, GameLobby);

        int layer = LayerMask.NameToLayer("PostProcessing");
        var volume = Object.FindFirstObjectByType<PostProcessVolume>();
        if (volume == null)
        {
            volume = new GameObject("PostFX") { layer = layer < 0 ? 0 : layer }.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
        }
        volume.sharedProfile = profile;
        Camera cam = Camera.main;
        if (cam != null)
        {
            var postLayer = cam.GetComponent<PostProcessLayer>();
            if (postLayer == null)
            {
                postLayer = cam.gameObject.AddComponent<PostProcessLayer>();
                string resGuid = AssetDatabase.FindAssets("t:PostProcessResources").FirstOrDefault();
                if (resGuid != null) postLayer.Init(AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(resGuid)));
            }
            postLayer.volumeLayer = layer < 0 ? 1 : 1 << layer;
            postLayer.volumeTrigger = cam.transform;
            postLayer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
            cam.allowHDR = true;
            EditorUtility.SetDirty(cam.gameObject);
        }
        return env + $", PostFX -> {GameLobbyProfile}";
    }

    // 로비(★): 같은 그림의 해 질 녘 판(채도 −42%, 회녹색, 바닥 안개, 비네트 — 원본은 그대로 둔다).
    public static string ApplyToOpenLobby()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LobbyBackground);
        if (sprite == null) return $"missing {LobbyBackground}";
        foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (image.name != "BackGround") continue;
            image.sprite = sprite;
            EditorUtility.SetDirty(image);
            return $"background -> {sprite.name}";
        }
        return "no BackGround image";
    }

    private static string ApplyEnvironment(Look look)
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = look.Fog;
        RenderSettings.fogDensity = look.FogDensity;
        bool firstTime = Object.FindFirstObjectByType<FlickerLight>(FindObjectsInactive.Include) == null;

        Light sun = null;
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(l => l.name).ThenBy(l => l.transform.position.x).ToArray();
        foreach (Light l in lights) if (l.type == LightType.Directional) sun = l;
        if (look.OverrideSun && sun != null)
        {
            sun.color = look.SunColor;
            sun.intensity = look.SunIntensity;
            EditorUtility.SetDirty(sun);
        }
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = look.Fog; // 안개와 이어지는 지평선
            EditorUtility.SetDirty(cam);
        }
        if (!firstTime) return $"fog {look.FogDensity} (lights already twisted)";

        RenderSettings.ambientLight *= look.AmbientScale;
        RenderSettings.ambientEquatorColor *= look.AmbientScale;
        RenderSettings.ambientGroundColor *= look.AmbientScale;
        int changed = 0, flicker = 0;
        var counters = new Dictionary<string, int>();
        foreach (Light l in lights)
        {
            if (l.type != LightType.Point && l.type != LightType.Spot) continue;
            float gray = l.color.grayscale;
            Color c = Color.Lerp(l.color, new Color(gray, gray, gray), look.PointDesaturate);
            c.a = 1f;
            l.color = c;
            float scale = look.PointScale;
            if (look.BoostPrefixes.Any(p => l.name.StartsWith(p))) scale = look.BoostScale;
            l.intensity *= scale;
            changed++;
            string prefix = look.FlickerPrefixes.FirstOrDefault(p => l.name.StartsWith(p));
            if (prefix != null)
            {
                counters.TryGetValue(prefix, out int n);
                counters[prefix] = n + 1;
                if (n % look.FlickerEvery == 0)
                {
                    var f = l.gameObject.AddComponent<FlickerLight>();
                    f.EditorSetup(l.intensity, 0.12f, new Vector2(3f, 9f));
                    flicker++;
                }
            }
            EditorUtility.SetDirty(l);
        }
        return $"fog {look.FogDensity}, points {changed}, flicker {flicker}";
    }

    private static void ApplyGrading(PostProcessProfile profile, Look look)
    {
        if (!profile.TryGetSettings(out ColorGrading grading))
        {
            grading = AddSetting<ColorGrading>(profile);
            grading.gradingMode.Override(GradingMode.HighDefinitionRange);
            grading.tonemapper.Override(Tonemapper.ACES);
        }
        grading.saturation.Override(look.Saturation);
        grading.temperature.Override(look.Temperature);
        grading.tint.Override(look.Tint);
        grading.contrast.Override(look.Contrast);
        grading.postExposure.Override(look.PostExposure);
        if (!profile.TryGetSettings(out Vignette vignette)) vignette = AddSetting<Vignette>(profile);
        vignette.intensity.Override(look.Vignette);
        vignette.smoothness.Override(0.55f);
        EditorUtility.SetDirty(profile);
    }

    private static PostProcessProfile LoadOrCreateProfile(string path)
    {
        var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
        if (profile != null) return profile;
        string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    private static T AddSetting<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
    {
        T s = profile.AddSettings<T>();
        s.name = typeof(T).Name;
        s.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(s, profile);
        return s;
    }
}
