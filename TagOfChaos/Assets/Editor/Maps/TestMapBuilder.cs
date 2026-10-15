using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 개편 규칙을 시험하는 단순 맵(Request1009Plan.md §25.5 O, 단계 G1). Blender 모델(testmaps.py → Assets/09. Environment/TestMaps/TestMap_<Map>.fbx)을
// 지금 맵 씬(Game_<Map>)을 복사한 Test_<Map> 씬에 넣는다. 지금 씬에서 쓰는 것(카메라·관리자·조명·배경·FarBackdrop·마녀 앵커)은 그대로 두고,
// 놀이 공간(Map/Ground·구조물·소품·탈출 장치·상자)만 테스트 맵으로 바꾼다. 여러 번 실행해도 같은 결과.
// 배치는 5개 맵이 모두 같고, 바닥·소품 색만 맵 테마를 따른다(§25 결정).
public static class TestMapBuilder
{
    public const string SceneFolder = "Assets/Scenes/Maps/Test";
    public const string ScenePrefix = "Test_";
    private const string ModelFolder = "Assets/09. Environment/TestMaps";
    private const string MaterialFolder = ModelFolder + "/Materials";

    public static string ScenePath(string map) => $"{SceneFolder}/{ScenePrefix}{map}.unity";
    public static string SceneName(string map) => ScenePrefix + map;

    // 놀이 공간을 이루는 테스트 모델의 루트 자식(이 이름들만 Map 아래로 옮긴다)
    private static readonly string[] ModelParts = { "Ground", "MonsterRoom", "Building_Q1", "Building_Q2", "Building_Q3", "Building_Q4", "Exits", "Dividers", "Markers", "Decoration" };
    // 지금 씬의 Map 아래에서 남기는 것(놀이 공간 밖 배경)
    private static readonly string[] KeepInMap = { "Background", WitchPath.FarBackdropName };
    private static readonly string[] RemoveRoots = { "TwistedProps", "MapColliders", "MapCompactInfo" };

    private struct Theme
    {
        public Color Ground, Wall, PropA, PropB, Building, Trim;
        public Theme(Color ground, Color wall, Color propA, Color propB, Color building, Color trim)
        {
            Ground = ground; Wall = wall; PropA = propA; PropB = propB; Building = building; Trim = trim;
        }
    }

    private static readonly Dictionary<string, Theme> Themes = new Dictionary<string, Theme>
    {
        { "CandyForest", new Theme(new Color(0.45f, 0.62f, 0.42f), new Color(0.22f, 0.32f, 0.24f), new Color(0.95f, 0.4f, 0.65f), new Color(0.95f, 0.92f, 0.88f), new Color(0.98f, 0.82f, 0.88f), new Color(0.7f, 0.3f, 0.55f)) },
        { "GingerbreadVillage", new Theme(new Color(0.78f, 0.66f, 0.5f), new Color(0.45f, 0.3f, 0.2f), new Color(0.62f, 0.38f, 0.2f), new Color(0.97f, 0.95f, 0.92f), new Color(0.7f, 0.45f, 0.25f), new Color(0.98f, 0.97f, 0.95f)) },
        { "ChocolateFactory", new Theme(new Color(0.42f, 0.36f, 0.33f), new Color(0.22f, 0.17f, 0.15f), new Color(0.38f, 0.24f, 0.16f), new Color(0.55f, 0.55f, 0.58f), new Color(0.5f, 0.4f, 0.35f), new Color(0.3f, 0.3f, 0.33f)) },
        { "CursedCandyCarnival", new Theme(new Color(0.4f, 0.45f, 0.58f), new Color(0.2f, 0.18f, 0.3f), new Color(0.85f, 0.2f, 0.25f), new Color(0.96f, 0.94f, 0.9f), new Color(0.95f, 0.78f, 0.45f), new Color(0.55f, 0.2f, 0.45f)) },
        { "HauntedBakery", new Theme(new Color(0.82f, 0.75f, 0.62f), new Color(0.4f, 0.3f, 0.25f), new Color(0.8f, 0.5f, 0.28f), new Color(0.3f, 0.25f, 0.22f), new Color(0.92f, 0.85f, 0.72f), new Color(0.55f, 0.35f, 0.22f)) },
    };

    [MenuItem("Tools/TagOfChaos/Test Maps/Build All Test Map Scenes")]
    public static void BuildAllMenu() => Debug.Log("[TestMapBuilder] " + BuildAll());

    public static string BuildAll()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var report = new List<string>();
        foreach (string map in MapSceneBuilder.MapNames) report.Add(Build(map));
        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        return string.Join(" | ", report);
    }

    public static string Build(string map)
    {
        string modelPath = $"{ModelFolder}/TestMap_{map}.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) return $"{map}: no model at {modelPath}";

        EnsureFolder(SceneFolder);
        string dst = ScenePath(map);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(dst) != null) AssetDatabase.DeleteAsset(dst);
        if (!AssetDatabase.CopyAsset(MapSceneBuilder.ScenePath(map), dst)) return $"{map}: copy failed";
        Scene scene = EditorSceneManager.OpenScene(dst, OpenSceneMode.Single);

        foreach (GameObject root in scene.GetRootGameObjects())
            if (RemoveRoots.Contains(root.name)) Object.DestroyImmediate(root);

        GameObject mapRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Map");
        if (mapRoot == null) { mapRoot = new GameObject("Map"); SceneManager.MoveGameObjectToScene(mapRoot, scene); }
        foreach (Transform child in mapRoot.transform.Cast<Transform>().ToList())
            if (!KeepInMap.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
        RemoveBackgroundInsidePlayArea(mapRoot.transform);

        // 지금 탈출 장치·로켓·상자는 지우고 마녀 앵커만 남긴다(새 배치는 EscapeMapSetup이 다시 만든다)
        GameObject escape = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EscapeMapSetup.RootName);
        if (escape != null)
            foreach (Transform child in escape.transform.Cast<Transform>().ToList())
                if (child.name != "WITCH_Anchor") Object.DestroyImmediate(child.gameObject);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (string part in ModelParts)
        {
            Transform t = instance.transform.Find(part);
            if (t != null) t.SetParent(mapRoot.transform, true);
        }
        Object.DestroyImmediate(instance);

        Theme theme = Themes.TryGetValue(map, out Theme found) ? found : Themes["CandyForest"];
        int renderers = 0;
        foreach (MeshRenderer r in mapRoot.GetComponentsInChildren<MeshRenderer>(true))
        {
            Transform part = r.transform;
            if (IsUnder(part, KeepInMap)) continue;
            string key = MaterialKey(part);
            r.sharedMaterial = MaterialFor(map, key, theme);
            renderers++;
            bool moving = key == "TM_ExitDoor" || key == "TM_MonsterDoor" || key == "TM_Glow";
            if (key != "TM_Glow" && key != "TM_RoomFloor" && r.GetComponent<Collider>() == null) r.gameObject.AddComponent<MeshCollider>();
            if (!moving) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        MoveSpawn(scene, SceneSpawnPoints.Cookie, mapRoot.transform, "Spawn_Cookie_Q1");
        MoveSpawn(scene, SceneSpawnPoints.Monster, mapRoot.transform, "Spawn_Monster");

        // 지금 맵에 맞춰 놓인 점광원은 테스트 맵과 맞지 않아 끈다(방향광·반사 프로브는 둔다)
        GameObject lighting = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MapLighting");
        Transform points = lighting != null ? lighting.transform.Find("PointLights") : null;
        if (points != null) points.gameObject.SetActive(false);

        string escapeReport = EscapeMapSetup.Apply(scene, map); // 건물·탈출구·술래문·상자 40·로켓·무지개 상자(§25.5 O)

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{map}: {dst} renderers={renderers} {escapeReport}";
    }

    // 지금 맵의 배경(Map/Background)에는 놀이 공간 안까지 들어온 나무·소품이 있다 — 테스트 맵 경계(±PlayHalf) 안에 중심이 있는 렌더러는 지운다.
    private const float PlayHalf = 74f;

    private static void RemoveBackgroundInsidePlayArea(Transform mapRoot)
    {
        Transform background = mapRoot.Find("Background");
        if (background == null) return;
        foreach (Renderer r in background.GetComponentsInChildren<Renderer>(true).ToList())
        {
            if (r == null) continue;
            Bounds b = r.bounds;
            bool huge = b.size.x > PlayHalf * 2f || b.size.z > PlayHalf * 2f; // 배경 바닥처럼 맵 전체를 덮는 것은 남긴다
            if (!huge && Mathf.Abs(b.center.x) < PlayHalf && Mathf.Abs(b.center.z) < PlayHalf) Object.DestroyImmediate(r.gameObject);
        }
    }

    private static bool IsUnder(Transform t, string[] names)
    {
        for (Transform p = t; p != null; p = p.parent) if (names.Contains(p.name)) return true;
        return false;
    }

    // 테스트 모델의 이름 규칙 → 재질(testmaps.py와 같은 이름)
    public static string MaterialKey(Transform t)
    {
        string n = System.Text.RegularExpressions.Regex.Replace(t.name, @"\.\d+$", ""); // Blender가 겹친 이름에 붙이는 .001
        if (n == "Floor") return "TM_Ground";
        if (n.StartsWith("Wall_")) return "TM_Wall";
        if (n.StartsWith("RoomWall_") || n.StartsWith("RoomCorner_")) return "TM_Room";
        if (n == "RoomFloorMark") return "TM_RoomFloor";
        if (n.StartsWith("MonsterDoor_")) return "TM_MonsterDoor";
        if (n.StartsWith("BWall_")) return "TM_Building";
        if (n.StartsWith("BPillar_") || n.StartsWith("BRoofRing_")) return "TM_BuildingTrim";
        if (n.StartsWith("Socket_")) return "TM_Socket";
        if (n == "ESC_Glow") return "TM_Glow";
        if (n.StartsWith("ExitPillar_") || n.StartsWith("ExitLintel_")) return "TM_ExitFrame";
        if (n.StartsWith("Door_")) return "TM_ExitDoor";
        if (n.StartsWith("ExitPad_")) return "TM_ExitPad";
        if (n.EndsWith("_Stick") || n.EndsWith("_Roof") || n.EndsWith("_Barrel") || n.EndsWith("_Chimney") || n.EndsWith("_Top")) return "TM_PropB";
        return "TM_PropA";
    }

    private static Material MaterialFor(string map, string key, Theme theme)
    {
        string folder = $"{MaterialFolder}/{map}";
        EnsureFolder(folder);
        string path = $"{folder}/{key}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        Color c = ColorFor(key, theme);
        m.color = c;
        m.SetFloat("_Glossiness", 0.15f);
        if (key == "TM_Glow" || key == "TM_MonsterDoor" || key == "TM_ExitDoor")
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c * (key == "TM_Glow" ? 2.5f : 0.6f));
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Color ColorFor(string key, Theme t)
    {
        switch (key)
        {
            case "TM_Ground": return t.Ground;
            case "TM_Wall": return t.Wall;
            case "TM_Room": return new Color(0.28f, 0.12f, 0.24f);
            case "TM_RoomFloor": return new Color(0.2f, 0.08f, 0.16f);
            case "TM_MonsterDoor": return new Color(0.85f, 0.12f, 0.2f);
            case "TM_Building": return t.Building;
            case "TM_BuildingTrim": return t.Trim;
            case "TM_Socket": return new Color(0.12f, 0.1f, 0.12f);
            case "TM_Glow": return new Color(1f, 0.95f, 0.45f);
            case "TM_ExitFrame": return new Color(0.3f, 0.3f, 0.34f);
            case "TM_ExitDoor": return new Color(0.25f, 0.55f, 0.95f);
            case "TM_ExitPad": return new Color(0.45f, 0.45f, 0.48f);
            case "TM_PropB": return t.PropB;
            default: return t.PropA;
        }
    }

    private static void MoveSpawn(Scene scene, string spawnName, Transform mapRoot, string markerName)
    {
        GameObject spawn = scene.GetRootGameObjects().FirstOrDefault(g => g.name == spawnName);
        Transform marker = mapRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == markerName);
        if (spawn != null && marker != null) spawn.transform.position = marker.position + Vector3.up * 0.5f;
    }

    // 테스트 맵 씬을 빌드 목록에 넣고, 판마다 고르는 맵 목록(GameSettings.gameMapScenes)을 테스트 맵으로 바꾼다(§25.7 — 시험 기간 동안).
    public static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (string map in MapSceneBuilder.MapNames)
        {
            string path = ScenePath(map);
            if (!scenes.Any(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        // 예전 맵 씬(Game_)은 지운 탈출 장치 스크립트를 쓰므로 빌드에서 뺀다(씬 파일은 둔다 — 테스트 맵의 원본)
        foreach (EditorBuildSettingsScene s in scenes)
            if (s.path.StartsWith("Assets/Scenes/Maps/" + SceneNames.MapScenePrefix)) s.enabled = false;
        EditorBuildSettings.scenes = scenes.ToArray();

        var settings = AssetDatabase.LoadAssetAtPath<GameSettingsSO>("Assets/Resources/GameSettings.asset");
        if (settings == null) return;
        var so = new SerializedObject(settings);
        SerializedProperty list = so.FindProperty("gameMapScenes");
        list.arraySize = MapSceneBuilder.MapNames.Length;
        for (int i = 0; i < MapSceneBuilder.MapNames.Length; i++) list.GetArrayElementAtIndex(i).stringValue = SceneName(MapSceneBuilder.MapNames[i]);
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
