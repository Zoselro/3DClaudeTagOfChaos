using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 소품 FBX마다 프리팹을 만들고, Blender 배치(Witch_Cookie_Layout.json)대로 과자집 + 소품을 조립한 환경 프리팹을 만든다.
// 환경 프리팹이 없으면 에디터 로드 시 한 번 자동 실행되고, 이후에는 메뉴에서 다시 실행할 수 있다.
[InitializeOnLoad]
public static class WitchCookiePropsBuilder
{
    private const string MenuBuild = "Tools/TagOfChaos/Build Witch Cookie Props";
    private const string MenuVerify = "Tools/TagOfChaos/Verify Witch Cookie Props";
    private const string LogTag = "[WitchCookieProps]";
    private const string PrefabRoot = "Assets/04. Prefabs/Environment/WitchCookieProps";
    private const string HousePrefabPath = "Assets/04. Prefabs/Environment/Witch_Cookie_House.prefab";
    private const string EnvironmentPrefabPath = "Assets/04. Prefabs/Environment/Witch_Cookie_Environment.prefab";
    private const string LayoutPath = WitchCookiePropsImportPostprocessor.PropsRoot + "/Witch_Cookie_Layout.json";

    // 표지를 여닫는 연출이 가능한 에셋은 Static으로 두지 않는다.
    private static readonly HashSet<string> NonStaticAssets = new HashSet<string> { "Spell_Book_Closed" };

    [Serializable]
    private class LayoutItem
    {
        public string name;
        public string zone;
        public string asset;
        public float[] pos;
        public float[] rot;
        public float scale;
    }

    [Serializable]
    private class Layout
    {
        public LayoutItem[] items;
    }

    static WitchCookiePropsBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    private static void BuildIfMissing()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildIfMissing;
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabPath) != null) return;
        if (AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath) == null) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(HousePrefabPath) == null) return;
        Build();
    }

    [MenuItem(MenuBuild)]
    public static void Build()
    {
        Dictionary<string, string> models = FindModels();
        foreach (string path in models.Values)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && WitchCookieHouseImportPostprocessor.RemapMaterials(importer, WitchCookiePropsImportPostprocessor.MaterialFolders))
                importer.SaveAndReimport();
        }

        var prefabs = new Dictionary<string, GameObject>();
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (KeyValuePair<string, string> kv in models)
            {
                string category = Path.GetFileName(Path.GetDirectoryName(kv.Value));
                string folder = $"{PrefabRoot}/{category}";
                EnsureFolder(folder);

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                if (!NonStaticAssets.Contains(kv.Key)) MarkStatic(instance.transform);

                string prefabPath = $"{folder}/{kv.Key}.prefab";
                prefabs[kv.Key] = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool saved);
                if (!saved) Debug.LogError($"{LogTag} Failed to save {prefabPath}");
                Object.DestroyImmediate(instance);
            }

            BuildEnvironment(preview, prefabs);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }

        AssetDatabase.SaveAssets();
        Verify();
    }

    private static void BuildEnvironment(Scene preview, Dictionary<string, GameObject> prefabs)
    {
        var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
        var house = AssetDatabase.LoadAssetAtPath<GameObject>(HousePrefabPath);
        if (layoutAsset == null || house == null)
        {
            Debug.LogError($"{LogTag} Layout or house prefab missing.");
            return;
        }

        Layout layout = JsonUtility.FromJson<Layout>(layoutAsset.text);
        var root = new GameObject("Witch_Cookie_Environment");
        SceneManager.MoveGameObjectToScene(root, preview);

        var houseInstance = (GameObject)PrefabUtility.InstantiatePrefab(house, preview);
        houseInstance.transform.SetParent(root.transform, false);

        var groups = new Dictionary<string, Transform>();
        int missing = 0;
        foreach (LayoutItem item in layout.items)
        {
            if (!prefabs.TryGetValue(item.asset, out GameObject prefab) || prefab == null)
            {
                missing++;
                continue;
            }
            if (!groups.TryGetValue(item.zone, out Transform group))
            {
                group = new GameObject(item.zone).transform;
                group.SetParent(root.transform, false);
                groups[item.zone] = group;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            go.name = item.name;
            go.transform.SetParent(group, false);
            go.transform.localPosition = new Vector3(item.pos[0], item.pos[1], item.pos[2]);
            go.transform.localRotation = new Quaternion(item.rot[0], item.rot[1], item.rot[2], item.rot[3]);
            go.transform.localScale = Vector3.one * item.scale;
        }
        if (missing > 0) Debug.LogError($"{LogTag} {missing} layout item(s) reference missing assets.");

        PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPrefabPath, out bool saved);
        if (!saved) Debug.LogError($"{LogTag} Failed to save {EnvironmentPrefabPath}");
    }

    // ---------------- verification ----------------

    [MenuItem(MenuVerify)]
    public static void Verify()
    {
        int errors = 0;
        Dictionary<string, string> models = FindModels();

        // 1) 소품 프리팹: 머티리얼 / 메시 / 충돌체
        int prefabCount = 0, renderers = 0, badMats = 0, emptyMeshes = 0, colliders = 0, colWithRenderer = 0;
        var perCategory = new SortedDictionary<string, int>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;
            prefabCount++;
            string category = Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid)));
            perCategory[category] = perCategory.TryGetValue(category, out int c) ? c + 1 : 1;

            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0) emptyMeshes++;
            foreach (MeshRenderer r in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                if (r.name.StartsWith(WitchCookieHouseImportPostprocessor.CollisionPrefix)) colWithRenderer++;
                foreach (Material m in r.sharedMaterials)
                    if (m == null || !IsProjectMaterial(m)) badMats++;
            }
            foreach (MeshCollider col in prefab.GetComponentsInChildren<MeshCollider>(true))
                if (col.sharedMesh != null && col.convex) colliders++;
        }
        bool propsOk = prefabCount == models.Count && badMats == 0 && emptyMeshes == 0 && colWithRenderer == 0 && colliders > 0;
        Debug.Log($"{LogTag} VERIFY prefabs={prefabCount}/{models.Count} {string.Join(", ", Describe(perCategory))} renderers={renderers} " +
                  $"badMaterialSlots={badMats} emptyMeshes={emptyMeshes} convexColliders={colliders} collisionWithRenderer={colWithRenderer} {(propsOk ? "OK" : "FAIL")}");
        if (!propsOk) errors++;

        // 2) 주요 소품 실측 크기 (m)
        errors += CheckSize("Interior/Witch_Table", 1, 0.85f, 0.97f);
        errors += CheckSize("Exterior/Outdoor_Table", 1, 0.72f, 0.85f);
        errors += CheckSize("Exterior/Candy_Parasol", 1, 3.2f, 3.6f);
        errors += CheckSize("Exterior/Candy_Fence_Straight", 0, 2.4f, 2.6f);
        errors += CheckSize("Interior/Potion_Shelf", 1, 2.4f, 2.6f);
        errors += CheckSize("Exterior/Twisted_Tree_C", 1, 8.0f, 13.0f);

        // 3) 환경 프리팹: 배치 수 / 누락 / 전체 크기
        var env = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabPath);
        var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
        if (env == null || layoutAsset == null)
        {
            Debug.LogError($"{LogTag} VERIFY environment prefab or layout missing");
            errors++;
        }
        else
        {
            Layout layout = JsonUtility.FromJson<Layout>(layoutAsset.text);
            int placed = 0;
            foreach (Transform zone in env.transform)
                if (zone.name == "Interior" || zone.name == "Exterior") placed += zone.childCount;
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (Renderer r in env.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
            bool envOk = placed == layout.items.Length;
            Debug.Log($"{LogTag} VERIFY environment placed={placed}/{layout.items.Length} bounds size={b.size} {(envOk ? "OK" : "FAIL")}");
            if (!envOk) errors++;
        }

        if (errors == 0) Debug.Log($"{LogTag} VERIFY ALL OK");
        else Debug.LogError($"{LogTag} VERIFY {errors} problem(s) found");
    }

    // axis: 0 = X(길이), 1 = Y(높이)
    private static int CheckSize(string relPath, int axis, float min, float max)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{relPath}.prefab");
        if (prefab == null)
        {
            Debug.LogError($"{LogTag} VERIFY size: {relPath} missing");
            return 1;
        }
        bool first = true;
        Bounds b = default;
        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds local = mf.sharedMesh.bounds;
            Matrix4x4 m = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            foreach (Vector3 corner in Corners(local))
            {
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
        }
        float v = axis == 0 ? b.size.x : b.max.y;
        bool ok = v >= min && v <= max;
        Debug.Log($"{LogTag} VERIFY size {relPath} {(axis == 0 ? "length" : "height")}={v:F2} m (expected {min}-{max}) {(ok ? "OK" : "FAIL")}");
        return ok ? 0 : 1;
    }

    private static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
    }

    private static IEnumerable<string> Describe(SortedDictionary<string, int> perCategory)
    {
        foreach (KeyValuePair<string, int> kv in perCategory) yield return $"{kv.Key}:{kv.Value}";
    }

    private static bool IsProjectMaterial(Material m)
    {
        string path = AssetDatabase.GetAssetPath(m);
        foreach (string folder in WitchCookiePropsImportPostprocessor.MaterialFolders)
            if (path.StartsWith(folder)) return true;
        return false;
    }

    // ---------------- helpers ----------------

    private static Dictionary<string, string> FindModels()
    {
        var models = new Dictionary<string, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { WitchCookiePropsImportPostprocessor.PropsRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (WitchCookiePropsImportPostprocessor.IsPropModel(path))
                models[Path.GetFileNameWithoutExtension(path)] = path;
        }
        return models;
    }

    private static void MarkStatic(Transform t)
    {
        GameObjectUtility.SetStaticEditorFlags(t.gameObject,
            StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        foreach (Transform child in t) MarkStatic(child);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
