using UnityEditor;
using UnityEngine;

// 술래 접근 추격음 설정 에셋(DistanceFadePlan.md §9.7 — 2026-10-03 사용자 선택: D 북소리 추격, 먼 단계 / 가까운 단계).
// Resources/Audio/ChaseAudioSettings.asset을 만들고 Assets/10. Audio/Chase/의 층 클립을 연결한다.
// 이미 있으면 비어 있는 클립만 채운다 — 인스펙터에서 바꾼 거리·비율·음량은 건드리지 않는다.
public static class ChaseAudioBuilder
{
    public const string AssetPath = "Assets/Resources/Audio/ChaseAudioSettings.asset";
    private const string ClipFolder = "Assets/10. Audio/Chase/";

    // 층 이름과 기본 시작 비율(먼 것부터). 바탕은 따로.
    public static readonly (string clip, float ratio)[] DefaultLevels = { ("Chase_Far", 1f), ("Chase_Near", 0.25f) };
    public const string BaseClip = "Chase_Base";

    [MenuItem("Tools/TagOfChaos/Audio/Build Chase Settings")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Audio")) AssetDatabase.CreateFolder("Assets/Resources", "Audio");
        var settings = AssetDatabase.LoadAssetAtPath<ChaseAudioSettingsSO>(AssetPath);
        bool created = settings == null;
        if (created)
        {
            settings = ScriptableObject.CreateInstance<ChaseAudioSettingsSO>();
            AssetDatabase.CreateAsset(settings, AssetPath);
        }

        AudioClip baseClip = settings.BaseLayer != null ? settings.BaseLayer : Clip(BaseClip);
        var levels = new ChaseAudioSettingsSO.DrumLevel[created || settings.LevelCount == 0 ? DefaultLevels.Length : settings.LevelCount];
        for (int i = 0; i < levels.Length; i++)
        {
            bool fresh = created || settings.LevelCount == 0;
            levels[i] = new ChaseAudioSettingsSO.DrumLevel
            {
                clip = !fresh && settings.LevelClip(i) != null ? settings.LevelClip(i) : (i < DefaultLevels.Length ? Clip(DefaultLevels[i].clip) : null),
                startRatio = fresh ? DefaultLevels[i].ratio : settings.LevelRadius(i) / settings.AudibleRadius,
            };
        }
        settings.EditorSetup(baseClip, levels);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Audio] Chase settings {(created ? "created" : "updated")}: base={(baseClip != null)}, levels={levels.Length}, radius={settings.AudibleRadius} m");
    }

    [MenuItem("Tools/TagOfChaos/Audio/Select Chase Settings")]
    public static void Select()
    {
        var settings = AssetDatabase.LoadAssetAtPath<ChaseAudioSettingsSO>(AssetPath);
        if (settings == null) { Build(); settings = AssetDatabase.LoadAssetAtPath<ChaseAudioSettingsSO>(AssetPath); }
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(ClipFolder + name + ".wav");
}

// 플레이 중 추격음 설정 에셋을 선택하면 씬 뷰에서 술래 둘레에 거리 원을 그린다(DistanceFadePlan.md §9.2 편의) —
// 빨강 = 들리기 시작(audibleRadius), 주황 = 북 단계 경계, 노랑 = 최대 음량.
[InitializeOnLoad]
public static class ChaseRadiusGizmo
{
    static ChaseRadiusGizmo()
    {
        SceneView.duringSceneGui -= Draw;
        SceneView.duringSceneGui += Draw;
    }

    private static void Draw(SceneView view)
    {
        if (!Application.isPlaying || !(Selection.activeObject is ChaseAudioSettingsSO s)) return;
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c) || c.Role != CharacterRole.Monster) continue;
            Vector3 p = c.gameObject.transform.position + Vector3.up * 0.1f;
            Handles.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            Handles.DrawWireDisc(p, Vector3.up, s.AudibleRadius);
            Handles.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            for (int i = 1; i < s.LevelCount; i++) Handles.DrawWireDisc(p, Vector3.up, s.LevelRadius(i));
            Handles.color = new Color(1f, 0.95f, 0.2f, 0.8f);
            Handles.DrawWireDisc(p, Vector3.up, s.FullRadius);
        }
    }
}
