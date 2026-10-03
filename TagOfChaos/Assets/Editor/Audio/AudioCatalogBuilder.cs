using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 소리·배경음 카탈로그를 만든다(SoundPlan.md §2.2, S1). Resources/Audio/SoundCatalog·MusicCatalog.
// - ID마다 항목이 하나씩 있게 한다. 새 항목은 SoundDefaults의 기본값(묶음·공간·거리 — D3·§2.5)으로 만들고,
//   이미 있는 항목의 조정값(음량·거리·쿨다운 등)은 건드리지 않는다.
// - 클립은 매번 파일 이름으로 다시 채운다: SFX 폴더의 "ID" 또는 "ID_1", "ID_2" …(변형), BGM 폴더의 "ID".
public static class AudioCatalogBuilder
{
    private const string MenuPath = "Tools/TagOfChaos/Audio/Build Catalogs";
    private const string ResourcesFolder = "Assets/Resources/Audio";

    [MenuItem(MenuPath)]
    public static void Build()
    {
        EnsureFolder("Assets/Resources", "Audio");
        EnsureFolder("Assets", "10. Audio");
        EnsureFolder("Assets/10. Audio", "BGM");
        EnsureFolder("Assets/10. Audio", "SFX");

        int soundClips = BuildSounds();
        int musicClips = BuildMusic();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Audio] Catalogs built: sounds with clips {soundClips}/{SoundIds().Count()}, music with clips {musicClips}/{MusicIds().Count()}.");
    }

    private static int BuildSounds()
    {
        var catalog = LoadOrCreate<SoundCatalogSO>(ResourcesFolder + "/SoundCatalog.asset");
        var existing = catalog.Entries.Where(e => e != null).GroupBy(e => e.id).ToDictionary(g => g.Key, g => g.First());
        Dictionary<string, List<AudioClip>> clips = ClipsByBaseName(AudioImportPostprocessor.SoundFolder);

        var entries = new List<SoundCatalogSO.Entry>();
        int withClips = 0;
        foreach (SoundId id in SoundIds())
        {
            if (!existing.TryGetValue(id, out SoundCatalogSO.Entry entry)) entry = SoundDefaults.Create(id);
            entry.clips = clips.TryGetValue(id.ToString(), out List<AudioClip> list) ? list.ToArray() : new AudioClip[0];
            if (entry.clips.Length > 0) withClips++;
            entries.Add(entry);
        }
        catalog.EditorSetEntries(entries.ToArray());
        EditorUtility.SetDirty(catalog);
        return withClips;
    }

    private static int BuildMusic()
    {
        var catalog = LoadOrCreate<MusicCatalogSO>(ResourcesFolder + "/MusicCatalog.asset");
        var existing = catalog.Entries.Where(e => e != null).GroupBy(e => e.id).ToDictionary(g => g.Key, g => g.First());
        Dictionary<string, List<AudioClip>> clips = ClipsByBaseName(AudioImportPostprocessor.MusicFolder);

        var entries = new List<MusicCatalogSO.Entry>();
        int withClips = 0;
        foreach (MusicId id in MusicIds())
        {
            if (!existing.TryGetValue(id, out MusicCatalogSO.Entry entry))
                entry = new MusicCatalogSO.Entry { id = id, loop = MusicCatalogSO.IsLoopById(id) };
            entry.clip = clips.TryGetValue(id.ToString(), out List<AudioClip> list) ? list[0] : null;
            if (entry.clip != null) withClips++;
            entries.Add(entry);
        }
        catalog.EditorSetEntries(entries.ToArray());
        EditorUtility.SetDirty(catalog);
        return withClips;
    }

    public static IEnumerable<SoundId> SoundIds() => ((SoundId[])Enum.GetValues(typeof(SoundId))).Where(id => id != SoundId.None);
    public static IEnumerable<MusicId> MusicIds() => ((MusicId[])Enum.GetValues(typeof(MusicId))).Where(id => id != MusicId.None);

    private static readonly Regex VariantSuffix = new Regex(@"_\d+$");

    // "ChestOpen", "ChestOpen_1", "ChestOpen_2" → "ChestOpen" 묶음. 하위 폴더(UI·Escape 등)는 구분하지 않는다(이름이 곧 ID).
    private static Dictionary<string, List<AudioClip>> ClipsByBaseName(string folder)
    {
        var map = new Dictionary<string, List<AudioClip>>();
        if (!AssetDatabase.IsValidFolder(folder.TrimEnd('/'))) return map;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder.TrimEnd('/') }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip == null) continue;
            string key = VariantSuffix.Replace(clip.name, "");
            if (!map.TryGetValue(key, out List<AudioClip> list)) map[key] = list = new List<AudioClip>();
            list.Add(clip);
        }
        foreach (List<AudioClip> list in map.Values) list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return map;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}

// 새 카탈로그 항목의 기본값(SoundPlan.md D3·§2.5). 만든 뒤에는 에셋에서 조정한다(빌더가 덮어쓰지 않음).
public static class SoundDefaults
{
    public static SoundCatalogSO.Entry Create(SoundId id)
    {
        var e = new SoundCatalogSO.Entry { id = id, pitchRange = new Vector2(0.95f, 1.05f) };
        int group = (int)id / 100;
        switch (group)
        {
            case 1: // UI
                Flat(e, SoundBus.Ui);
                e.pitchRange = Vector2.one;
                e.maxInstances = 2;
                e.importance = SoundImportance.High;
                break;
            case 2: // 캐릭터
                World(e, 15f, 4);
                if (id == SoundId.CookieStep || id == SoundId.CookieJump || id == SoundId.CookieLand || id == SoundId.CookieDodge)
                {
                    e.maxDistance = 12f; // 쿠키는 작게(D3)
                    e.maxInstances = 8;
                    e.cooldown = 0f;
                    e.importance = SoundImportance.Low;
                }
                else if (id == SoundId.MonsterStep || id == SoundId.MonsterDash || id == SoundId.MonsterGrab || id == SoundId.MonsterSquash)
                {
                    e.maxDistance = 30f; // 괴물은 크게 — 쿠키에게 경고(D3)
                    e.cooldown = 0f;
                    e.importance = SoundImportance.High;
                }
                else if (id == SoundId.MonsterAimLock || id == SoundId.SpectateSwitch)
                {
                    Flat(e, SoundBus.Sfx);
                }
                break;
            case 3: // 색칠(본인)
                Flat(e, SoundBus.Sfx);
                e.maxInstances = 2;
                break;
            case 4: // 대기실
                if (id == SoundId.CauldronBubble) { World(e, 15f, 1); e.bus = SoundBus.Ambience; }
                else if (id == SoundId.CauldronSplash) World(e, 15f, 4);
                else Flat(e, SoundBus.Ui);
                break;
            case 5: // 탈출 모드
                World(e, 15f, 4);
                if (id == SoundId.RocketIgnite || id == SoundId.RocketLiftoff || id == SoundId.DeviceComplete)
                {
                    e.maxDistance = 60f;
                    e.maxInstances = 2;
                    e.importance = SoundImportance.High;
                }
                else if (id == SoundId.WitchAppear || id == SoundId.WitchSlam || id == SoundId.HeartBeat || id == SoundId.EscapeSuccessSelf)
                {
                    Flat(e, SoundBus.Sfx);
                    e.maxInstances = 1;
                    e.importance = SoundImportance.Critical;
                }
                break;
            case 6: // 도구
                World(e, 20f, 4);
                if (id == SoundId.StunAimHum) { Flat(e, SoundBus.Sfx); e.maxInstances = 1; }
                break;
            case 7: // 맵 연출
                World(e, 60f, 2);
                e.importance = SoundImportance.High;
                break;
            case 8: // 환경음(반복)
                World(e, 40f, 2);
                e.bus = SoundBus.Ambience;
                e.pitchRange = Vector2.one;
                e.cooldown = 0f;
                if (id == SoundId.AmbFactory) { e.maxDistance = 28f; e.maxInstances = 4; } // 기계 여러 곳(AmbientPlacer)
                else if (id == SoundId.AmbCave)
                {
                    e.bus = SoundBus.Sfx; // 지하에서는 이 소리가 주인공 — 환경음 묶음(−12 dB)보다 또렷하게
                    e.volume = 0.55f;
                    e.maxInstances = 1;
                }
                else if (id == SoundId.CarouselSpin)
                {
                    e.bus = SoundBus.Sfx; // 회전목마 음악은 환경음(−12 dB)보다 또렷하게
                    e.volume = 0.5f;
                    e.maxInstances = 1;
                }
                break;
        }
        return e;
    }

    private static void Flat(SoundCatalogSO.Entry e, SoundBus bus)
    {
        e.space = SoundSpace.Flat2D;
        e.bus = bus;
    }

    private static void World(SoundCatalogSO.Entry e, float maxDistance, int maxInstances)
    {
        e.space = SoundSpace.World3D;
        e.bus = SoundBus.Sfx;
        e.maxDistance = maxDistance;
        e.maxInstances = maxInstances;
    }
}
