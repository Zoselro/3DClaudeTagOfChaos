using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 맵 씬에 탈출 모드 배치를 만든다(EscapePlan.md §4, §5): EscapeSetup 루트(EscapeManager), 탈출 장치(ESC_<Map>_Root),
// 스파이 로켓(SPY_Rocket_<Map>_Root), 상자 자리 20곳(CHEST_Slot_nn), 마녀 위치(WITCH_Anchor).
// 걸을 수 있는 땅 위에서 주변이 트인 곳만 고른다: 장치는 맵 가운데 쪽, 로켓은 장치에서 먼 곳, 상자는 서로 멀리 흩어지게.
// 상자 주변은 2.5 m를 비워 괴물(통행 반지름 1.8 m)이 지나가는 통로를 좁히지 않는다. 배치는 맵 이름으로 시드를 정해 늘 같다.
// 맵 축소(MapCompactor.Compact)가 끝날 때 자동으로 다시 만든다. 모양은 Blender 모델(Escape/Models/*.fbx, escape_assets.py)을
// 쓰고, 모델이 없으면 같은 이름 규칙의 임시 도형으로 대신한다.
public static class EscapeMapSetup
{
    public const string RootName = "EscapeSetup";
    public const int ChestSlotCount = 20;
    private const string MaterialFolder = "Assets/09. Environment/Escape/Materials";
    private const float SampleStep = 2f;
    private const float DeviceClearance = 6.5f;  // 장치 반 길이(최대 3 m) + 괴물 통로 폭(3.6 m) — 장치 옆에 쿠키만 들어가는 틈이 생기지 않게
    private const float RocketClearance = 5f;    // 로켓 날개 반지름(1.4 m) + 괴물 통로 폭
    private const float ChestClearance = 2.5f;
    private const float MinChestSpacing = 9f;
    private const float WitchDistance = MapCompactor.NewHalf + 85f;

    [MenuItem("Tools/TagOfChaos/Escape/Setup Open Map Scene")]
    public static void SetupOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        string map = scene.name.Replace("Game_", string.Empty);
        Debug.Log(Apply(scene, map));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/TagOfChaos/Escape/Setup All Map Scenes")]
    public static void SetupAll()
    {
        foreach (string map in MapSceneBuilder.MapNames)
        {
            Scene scene = EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);
            Debug.Log(Apply(scene, map));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    public static string Apply(Scene scene, string map)
    {
        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToList()) Object.DestroyImmediate(old);
        Physics.SyncTransforms();

        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        EscapeRecipeSO recipe = catalog != null ? catalog.RecipeFor(map) : null;
        EscapeExitKind exitKind = recipe != null ? recipe.ExitKind : EscapeExitKind.CakeRocket;

        List<Vector3> ground = SampleWalkable();
        var avoid = new List<Vector3>();
        foreach (string spawn in new[] { SceneSpawnPoints.Cookie, SceneSpawnPoints.Monster })
        {
            GameObject sp = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == spawn)?.gameObject;
            if (sp != null) avoid.Add(sp.transform.position);
        }

        var rng = new System.Random(map.GetHashCode());
        Vector3? device = ground.Where(p => Far(p, avoid, 10f) && IsClear(p, DeviceClearance))
            .OrderBy(p => Flat(p).magnitude).Cast<Vector3?>().FirstOrDefault();
        if (device == null) return $"[EscapeMapSetup] {map}: no open spot for the escape device.";
        avoid.Add(device.Value);

        Vector3? rocket = ground.Where(p => Far(p, avoid, 12f) && IsClear(p, RocketClearance))
            .OrderByDescending(p => Mathf.Min(Dist(p, device.Value), avoid.Min(a => Dist(p, a)) * 1.5f)).Cast<Vector3?>().FirstOrDefault();
        if (rocket == null) return $"[EscapeMapSetup] {map}: no open spot for the spy rocket.";
        avoid.Add(rocket.Value);

        var chestCandidates = ground.Where(p => Far(p, avoid, 6f) && IsClear(p, ChestClearance)).ToList();
        var chests = new List<Vector3>();
        if (chestCandidates.Count > 0) chests.Add(chestCandidates[rng.Next(chestCandidates.Count)]);
        while (chests.Count < ChestSlotCount && chestCandidates.Count > 0)
        {
            Vector3 best = default;
            float bestScore = -1f;
            foreach (Vector3 c in chestCandidates)
            {
                float d = chests.Min(x => Dist(x, c));
                if (d > bestScore) { bestScore = d; best = c; }
            }
            if (bestScore < MinChestSpacing * 0.5f) break;
            chests.Add(best);
        }

        // ---- build ----
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        var manager = root.AddComponent<EscapeManager>();

        EscapeDevice deviceComp = BuildDevice(root.transform, map, exitKind, device.Value);
        SpyRocket rocketComp = BuildRocket(root.transform, map, rocket.Value, device.Value);
        var anchors = new Transform[chests.Count];
        for (int i = 0; i < chests.Count; i++) anchors[i] = BuildChest(root.transform, i, chests[i], device.Value);

        var witch = new GameObject("WITCH_Anchor");
        witch.transform.SetParent(root.transform, false);
        Vector3 away = Flat(rocket.Value - device.Value);
        Vector3 dir = away.sqrMagnitude > 1f ? away.normalized : Vector3.forward;
        witch.transform.position = dir * WitchDistance;
        GameObject witchModel = InstantiateModel("WITCH", witch.transform, "Model");
        if (witchModel != null) witchModel.transform.localRotation = Quaternion.identity;
        WitchPresenter witchComp = witch.AddComponent<WitchPresenter>();

        manager.EditorBind(anchors, deviceComp, rocketComp, witchComp);
        return $"[EscapeMapSetup] {map}: device {Fmt(device.Value)}, rocket {Fmt(rocket.Value)} ({Dist(device.Value, rocket.Value):F0} m apart), " +
               $"chest slots {chests.Count}/{ChestSlotCount} from {chestCandidates.Count} candidates, witch {Fmt(witch.transform.position)}";
    }

    // ---------------- sampling ----------------

    private static List<Vector3> SampleWalkable()
    {
        var list = new List<Vector3>();
        float half = MapCompactor.NewHalf - 5f;
        for (float x = -half; x <= half; x += SampleStep)
        for (float z = -half; z <= half; z += SampleStep)
        {
            if (!TryWalkable(x, z, out float y, out Vector3 normal)) continue;
            if (Vector3.Angle(normal, Vector3.up) > 12f) continue; // 평평한 곳만
            list.Add(new Vector3(x, y, z));
        }
        return list;
    }

    private static bool TryWalkable(float x, float z, out float y, out Vector3 normal)
    {
        y = 0f;
        normal = Vector3.up;
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, 300f, z), Vector3.down, 600f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return false;
        RaycastHit top = hits.OrderByDescending(h => h.point.y).First();
        if (!top.collider.name.Contains("Terrain_Walkable")) return false; // 지붕·소품 위가 아닌 땅
        y = top.point.y;
        normal = top.normal;
        return true;
    }

    // 주변 반지름 r 안(원기둥, 발목 0.3 m부터 4 m 높이까지)에 땅 말고 다른 충돌체가 없는지. 반지름이 커도 땅 가까이를 제대로 본다.
    private static bool IsClear(Vector3 p, float r)
    {
        const float bottom = 0.3f, top = 4f;
        Vector3 center = p + Vector3.up * ((bottom + top) * 0.5f);
        foreach (Collider c in Physics.OverlapBox(center, new Vector3(r, (top - bottom) * 0.5f, r), Quaternion.identity,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (c.name.Contains("Terrain") || c.name.Contains("Ground")) continue;
            Vector3 nearest = c.bounds.ClosestPoint(center); // 축 정렬 상자 기준이라 조금 보수적이다
            if (Flat(nearest - p).magnitude <= r) return false;
        }
        return true;
    }

    private static bool Far(Vector3 p, List<Vector3> others, float min) => others.All(o => Dist(p, o) >= min);
    private static float Dist(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    private static string Fmt(Vector3 v) => $"({v.x:F0}, {v.y:F1}, {v.z:F0})";

    // ---------------- placeholder visuals ----------------

    private static EscapeDevice BuildDevice(Transform parent, string map, EscapeExitKind kind, Vector3 pos)
    {
        GameObject model = InstantiateModel($"ESC_{map}", parent, $"ESC_{map}_Root");
        if (model != null)
        {
            model.transform.position = pos;
            foreach (string hidden in new[] { "ESC_Glow", "ESC_Cake_Rocket" })
            {
                Transform t = model.transform.Find(hidden);
                if (t != null) t.gameObject.SetActive(false); // 완성되면 EscapeExitFx가 켠다
            }
            AddMeshColliders(model, "Body", "ESC_Cake_Intact", "ESC_Cake_Rocket"); // 케이크가 부서진 뒤에는 로켓이 막는다
            return model.AddComponent<EscapeDevice>();
        }

        var root = new GameObject($"ESC_{map}_Root");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        Shape(root.transform, "ESC_Base", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(2.6f, 0.25f, 2.6f), Mat("ESC_Base", new Color(0.35f, 0.3f, 0.35f)), true);
        switch (kind)
        {
            case EscapeExitKind.CakeRocket:
                var cake = new GameObject("ESC_Cake_Intact").transform;
                cake.SetParent(root.transform, false);
                Shape(cake, "Layer1", PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(2.2f, 0.4f, 2.2f), Mat("ESC_Cake", new Color(1f, 0.85f, 0.9f)), true);
                Shape(cake, "Layer2", PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(1.5f, 0.3f, 1.5f), Mat("ESC_CakeCream", new Color(1f, 0.6f, 0.75f)), false);
                var cakeRocket = new GameObject("ESC_Cake_Rocket").transform;
                cakeRocket.SetParent(root.transform, false);
                Shape(cakeRocket, "Body", PrimitiveType.Capsule, new Vector3(0f, 2.2f, 0f), new Vector3(1f, 1.8f, 1f), Mat("ESC_CakeRocket", new Color(0.95f, 0.95f, 1f)), false);
                cakeRocket.gameObject.SetActive(false);
                break;
            case EscapeExitKind.RuneAltar:
                Shape(root.transform, "ESC_Altar", PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0f), new Vector3(1.6f, 0.6f, 1.6f), Mat("ESC_Stone", new Color(0.55f, 0.5f, 0.45f)), true);
                break;
            case EscapeExitKind.ChocolateTrain:
                Shape(root.transform, "ESC_Train", PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(1.8f, 1.6f, 3.2f), Mat("ESC_Chocolate", new Color(0.35f, 0.2f, 0.1f)), true);
                break;
            case EscapeExitKind.BakeryMachine:
                Shape(root.transform, "ESC_Machine", PrimitiveType.Cube, new Vector3(0f, 1.4f, 0f), new Vector3(2f, 2.2f, 1.6f), Mat("ESC_Iron", new Color(0.45f, 0.45f, 0.5f)), true);
                break;
            default:
                Shape(root.transform, "ESC_Cart", PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(1.6f, 1.2f, 2.6f), Mat("ESC_Cart", new Color(0.8f, 0.15f, 0.3f)), true);
                break;
        }
        return root.AddComponent<EscapeDevice>();
    }

    private static SpyRocket BuildRocket(Transform parent, string map, Vector3 pos, Vector3 device)
    {
        GameObject model = InstantiateModel($"SPY_Rocket_{map}", parent, $"SPY_Rocket_{map}_Root");
        if (model != null)
        {
            model.transform.position = pos;
            Vector3 toDevice = Flat(device - pos);
            if (toDevice.sqrMagnitude > 0.01f) model.transform.rotation = Quaternion.LookRotation(toDevice.normalized); // 끼우는 칸(앞면)이 맵 안쪽을 본다
            var capsule = model.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 2.1f, 0f);
            capsule.radius = 1f;
            capsule.height = 4.2f;
            return model.AddComponent<SpyRocket>();
        }

        var root = new GameObject($"SPY_Rocket_{map}_Root");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        Material body = Mat("SPY_Rocket", new Color(0.25f, 0.25f, 0.3f));
        Shape(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 2.2f, 0f), new Vector3(1.4f, 2f, 1.4f), body, true);
        Shape(root.transform, "FinA", PrimitiveType.Cube, new Vector3(0.9f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.9f), Mat("SPY_Fin", new Color(0.7f, 0.1f, 0.15f)), false);
        Shape(root.transform, "FinB", PrimitiveType.Cube, new Vector3(-0.9f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.9f), Mat("SPY_Fin", new Color(0.7f, 0.1f, 0.15f)), false);
        for (int i = 0; i < 2; i++)
        {
            var slot = new GameObject($"Slot_{i:00}").transform;
            slot.SetParent(root.transform, false);
            slot.localPosition = new Vector3(i == 0 ? -0.6f : 0.6f, 1.6f, 0.75f);
        }
        return root.AddComponent<SpyRocket>();
    }

    private static Transform BuildChest(Transform parent, int index, Vector3 pos, Vector3 device)
    {
        Vector3 face = Flat(device - pos);
        Quaternion facing = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face.normalized) : Quaternion.identity;
        GameObject model = InstantiateModel("CHEST", parent, $"CHEST_Slot_{index:00}");
        if (model != null)
        {
            model.transform.SetPositionAndRotation(pos, facing);
            var collider = model.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.35f, 0f);
            collider.size = new Vector3(1f, 0.7f, 0.7f);
            model.AddComponent<MaterialChest>();
            return model.transform;
        }

        var root = new GameObject($"CHEST_Slot_{index:00}");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        root.transform.rotation = facing;
        Material wood = Mat("CHEST_Wood", new Color(0.55f, 0.33f, 0.18f));
        Shape(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 0.7f), wood, false);
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.35f, 0f);
        box.size = new Vector3(1f, 0.7f, 0.7f);
        var lid = new GameObject("Lid").transform;
        lid.SetParent(root.transform, false);
        lid.localPosition = new Vector3(0f, 0.6f, -0.35f);
        Shape(lid, "LidMesh", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0.35f), new Vector3(1.04f, 0.12f, 0.74f), Mat("CHEST_Lid", new Color(0.45f, 0.26f, 0.14f)), false);
        root.AddComponent<MaterialChest>();
        return root.transform;
    }

    // ---------------- Blender models ----------------

    private static GameObject InstantiateModel(string unit, Transform parent, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{EscapeModelImportPostprocessor.ModelFolder}/{unit}.fbx");
        if (asset == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = name;
        return go;
    }

    // 장치 모양 그대로의 충돌체(경사진 받침은 괴물·쿠키 모두 오를 수 있고, 레일처럼 낮은 부분은 넘어갈 수 있다).
    private static void AddMeshColliders(GameObject root, params string[] parts)
    {
        foreach (string part in parts)
        {
            Transform t = root.transform.Find(part);
            if (t == null) continue;
            foreach (MeshFilter filter in t.GetComponentsInChildren<MeshFilter>(true))
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }
    }

    private static void Shape(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material material, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    private static Material Mat(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/09. Environment/Escape")) AssetDatabase.CreateFolder("Assets/09. Environment", "Escape");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/09. Environment/Escape", "Materials");
        string path = $"{MaterialFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
