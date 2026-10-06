using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 뒤틀린 소품·숨을 곳 프리팹 만들기(TwistedCandyPlan.md §3·V3·V4).
// 1) MT_* 머티리얼: 팔레트 색(MT_P0~P9)은 DefaultColorPalette 값 그대로, 나머지는 아래 표. 이름이 같은 FBX 슬롯에 연결된다.
// 2) FBX마다 프리팹(Assets/04. Prefabs/Twisted/<이름>.prefab): 오브젝트 이름 규칙으로 충돌체를 붙인다.
//    COL_*   → 보이지 않는 BoxCollider(숨을 곳 벽·바닥처럼 정확한 모양이 필요한 곳)
//    Decal_* → 충돌체 없음(바닥 자국)
//    그 밖   → COL_가 하나도 없으면 오브젝트마다 메시 경계에 맞춘 BoxCollider(높이는 최소 2.6 m)
// 모든 오브젝트는 정적(배칭)이다. 다시 실행하면 프리팹을 덮어쓴다.
public static class TwistedPropBuilder
{
    public const string PrefabFolder = "Assets/04. Prefabs/Twisted";
    public const string CollisionPrefix = "COL_";
    public const string DecalPrefix = "Decal_";
    public const float MinColliderHeight = 2.6f; // MapPassabilityCheck 쿠키 오르기 1.8 m보다 높게

    private static readonly Dictionary<string, Color> Neutrals = new Dictionary<string, Color>
    {
        ["MT_Pastel"] = new Color(0.82f, 0.78f, 0.80f), ["MT_Hollow"] = new Color(0.04f, 0.03f, 0.04f),
        ["MT_Button"] = new Color(0.03f, 0.03f, 0.03f), ["MT_Cookie"] = new Color(0.62f, 0.38f, 0.2f),
        ["MT_CookieDark"] = new Color(0.32f, 0.18f, 0.09f), ["MT_Crumb"] = new Color(0.82f, 0.62f, 0.4f),
        ["MT_Icing"] = new Color(0.9f, 0.87f, 0.8f), ["MT_EyeWhite"] = new Color(0.92f, 0.9f, 0.84f),
        ["MT_Wood"] = new Color(0.34f, 0.25f, 0.2f), ["MT_Gold"] = new Color(0.66f, 0.52f, 0.24f),
        ["MT_Metal"] = new Color(0.36f, 0.37f, 0.4f), ["MT_Choco"] = new Color(0.26f, 0.13f, 0.06f),
        ["MT_Dough"] = new Color(0.88f, 0.78f, 0.62f), ["MT_Flour"] = new Color(0.92f, 0.92f, 0.9f),
        ["MT_Glass"] = new Color(0.7f, 0.85f, 0.9f, 0.3f), ["MT_Mold"] = new Color(0.52f, 0.58f, 0.44f),
        ["MT_Frosting"] = new Color(0.86f, 0.72f, 0.78f),
        ["MT_Canvas_Torn"] = new Color(0.55f, 0.5f, 0.46f), ["MT_Gingerbread"] = new Color(0.58f, 0.34f, 0.17f),
        ["MT_FloorBoard"] = new Color(0.4f, 0.28f, 0.2f), ["MT_Exit"] = new Color(1f, 0.75f, 0.3f),
    };

    public static readonly string[] PaletteMaterials =
    {
        "MT_P0_Red", "MT_P1_Orange", "MT_P2_Yellow", "MT_P3_Lime", "MT_P4_Green",
        "MT_P5_Teal", "MT_P6_Blue", "MT_P7_Navy", "MT_P8_Purple", "MT_P9_Magenta",
    };

    [MenuItem("Tools/TagOfChaos/Maps/Build Twisted Props (Materials + Prefabs)")]
    public static void BuildAll()
    {
        int mats = EnsureMaterials();
        AssetDatabase.ImportAsset(TwistedModelImportPostprocessor.ModelFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        int prefabs = BuildPrefabs();
        Debug.Log($"[TwistedProps] materials {mats}, prefabs {prefabs} -> {PrefabFolder}");
    }

    public static int EnsureMaterials()
    {
        EnsureFolder(TwistedModelImportPostprocessor.MaterialFolder);
        var palette = AssetDatabase.LoadAssetAtPath<ColorPaletteSO>(PaletteCoverageMeter.PalettePath);
        int n = 0;
        for (int i = 0; i < PaletteMaterials.Length; i++) n += SaveMaterial(PaletteMaterials[i], palette.GetColor(i), 0.2f);
        foreach (KeyValuePair<string, Color> kv in Neutrals) n += SaveMaterial(kv.Key, kv.Value, kv.Key == "MT_Glass" ? 0.85f : 0.15f);
        AssetDatabase.SaveAssets();
        return n;
    }

    private static int SaveMaterial(string name, Color color, float gloss)
    {
        string path = $"{TwistedModelImportPostprocessor.MaterialFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool created = m == null;
        if (created) m = new Material(Shader.Find("Standard")) { name = name };
        m.color = color;
        m.SetFloat("_Glossiness", gloss);
        m.enableInstancing = true;
        if (color.a < 1f) MakeFade(m);
        if (name == "MT_Exit") // 출구 등 — 어두운 실내에서도 보이게 발광(블룸이 잡는다)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2.5f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        if (created) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return 1;
    }

    private static void MakeFade(Material m)
    {
        m.SetFloat("_Mode", 2f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    public static int BuildPrefabs()
    {
        EnsureFolder(PrefabFolder);
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { TwistedModelImportPostprocessor.ModelFolder }))
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = model.name;
            AddColliders(root);
            AddShelterParts(root);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{model.name}.prefab");
            Object.DestroyImmediate(root);
            n++;
        }
        AssetDatabase.SaveAssets();
        return n;
    }

    public const string LightPrefix = "Light_";
    public const string ZonePrefix = "Zone_";

    // 숨을 곳(V4): Light_* 표시 → 점광원(안쪽은 어둑한 따뜻한 빛, 출구는 주황 — 출구가 보이게 §2.3), Zone_* 표시(크기 = 상자) → 실내 소리 IndoorZone.
    public static void AddShelterParts(GameObject root)
    {
        var zones = new List<Bounds>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (t.name.StartsWith(LightPrefix))
            {
                var light = t.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                bool exit = t.name.Contains("Exit");
                light.color = exit ? new Color(1f, 0.6f, 0.25f) : new Color(1f, 0.78f, 0.55f);
                light.range = exit ? 7f : 13f;
                light.intensity = exit ? 2f : 1.3f;
                light.shadows = LightShadows.None;
                if (!exit) t.gameObject.AddComponent<FlickerLight>().EditorSetup(light.intensity, 0.1f, new Vector2(5f, 14f));
            }
            else if (t.name.StartsWith(ZonePrefix))
            {
                Vector3 size = t.localScale;
                zones.Add(new Bounds(t.localPosition, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z))));
                Object.DestroyImmediate(t.gameObject);
            }
        }
        if (zones.Count > 0) root.AddComponent<IndoorZone>().EditorSetup(zones.ToArray());
    }

    public static void AddColliders(GameObject root)
    {
        var parts = root.GetComponentsInChildren<MeshFilter>(true);
        bool explicitCollision = parts.Any(f => f.name.StartsWith(CollisionPrefix));
        foreach (MeshFilter f in parts)
        {
            if (f.sharedMesh == null || f.name.StartsWith(DecalPrefix)) continue;
            bool col = f.name.StartsWith(CollisionPrefix);
            if (explicitCollision && !col) continue;
            Bounds b = f.sharedMesh.bounds;
            if (!col && b.size.y < MinColliderHeight) // 낮은 소품 위에 쿠키만 올라가 괴물이 못 닿는 자리가 생기지 않게(쿠키는 1.8 m까지 오른다)
            {
                b.max = new Vector3(b.max.x, b.min.y + MinColliderHeight, b.max.z);
            }
            var box = f.gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
            if (!col) continue;
            Object.DestroyImmediate(f.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(f);
        }
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
