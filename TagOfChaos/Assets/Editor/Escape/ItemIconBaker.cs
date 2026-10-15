using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 아이템 칸 그림 굽기(Request1009Plan.md §16.2). 각 ItemSO의 모델을 빈 미리보기 장면에서 3/4 각도로 찍어
// 투명 배경 PNG(Icon_{ItemId}.png)로 저장하고 ItemSO.icon에 연결한다. 맵의 어두운 조명과 상관없이 아이콘 전용 조명을 쓴다.
// 다시 실행하면 덮어쓴다(모델이 바뀌었을 때).
public static class ItemIconBaker
{
    public const string IconFolder = "Assets/08. Resources/ItemIcons";
    private const int Size = 256;
    private const int Supersample = 2;
    private const float FieldOfView = 24f;
    private const float Padding = 1.08f;
    private const int OutlinePixels = 3;
    private static readonly Color OutlineColor = new Color(0.05f, 0.03f, 0.06f, 0.85f);
    // 뒤쪽 3/4과 앞쪽 3/4 중 색이 더 많이 보이는 쪽을 고른다(룬처럼 무늬가 한 면에만 있는 모델).
    private static readonly Quaternion[] ViewAngles = { Quaternion.Euler(24f, -35f, 0f), Quaternion.Euler(24f, 145f, 0f) };

    public static string IconPath(ItemSO item) => IconFolder + "/Icon_" + item.ItemId + ".png";

    [MenuItem("Tools/Escape/Bake Item Icons")]
    public static void BakeAllMenu()
    {
        int n = BakeAll();
        Debug.Log($"[ItemIconBaker] Baked {n} item icons into {IconFolder}.");
    }

    public static int BakeAll()
    {
        if (!AssetDatabase.IsValidFolder(IconFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(IconFolder).Replace('\\', '/'), Path.GetFileName(IconFolder));
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ItemSO"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;
            string path = IconPath(item);
            // 모델이 아직 없는 새 아이템(Request1009Plan.md §25 G17)은 게임에서 보이는 색 도형으로 굽는다 — 모델이 오면 다시 굽는다
            GameObject placeholder = item.ModelPrefab == null ? EscapeVisuals.CreateItemModel(item, null) : null;
            try { File.WriteAllBytes(path, Render(placeholder != null ? placeholder : item.ModelPrefab).EncodeToPNG()); }
            finally { if (placeholder != null) Object.DestroyImmediate(placeholder); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(path);
            var so = new SerializedObject(item);
            so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            count++;
        }
        AssetDatabase.SaveAssets();
        return count;
    }

    private static void ConfigureImporter(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = Size;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    public static Texture2D Render(GameObject prefab)
    {
        Texture2D best = null;
        float bestScore = -1f;
        foreach (Quaternion view in ViewAngles)
        {
            Texture2D tex = Render(prefab, view);
            float score = Colorfulness(tex);
            if (score > bestScore + 0.002f) { if (best != null) Object.DestroyImmediate(best); best = tex; bestScore = score; }
            else Object.DestroyImmediate(tex);
        }
        return best;
    }

    // 보이는 픽셀의 평균 채도(순수 계산).
    public static float Colorfulness(Texture2D tex)
    {
        float sum = 0f, weight = 0f;
        foreach (Color c in tex.GetPixels())
        {
            if (c.a < 0.5f) continue;
            Color.RGBToHSV(c, out _, out float s, out float v);
            sum += s * v;
            weight += 1f;
        }
        return weight > 0f ? sum / weight : 0f;
    }

    private static Texture2D Render(GameObject prefab, Quaternion viewAngle)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        var rt = new RenderTexture(Size * Supersample, Size * Supersample, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        try
        {
            var model = (GameObject)Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(model, scene);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Bounds b = RendererBounds(model);

            AddLight(scene, Quaternion.Euler(40f, -30f, 0f), 1.25f, new Color(1f, 0.96f, 0.9f));
            AddLight(scene, Quaternion.Euler(15f, 140f, 0f), 0.55f, new Color(0.75f, 0.8f, 1f));
            AddLight(scene, Quaternion.Euler(-30f, 20f, 0f), 0.35f, Color.white);

            var camGo = new GameObject("IconCamera");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = FieldOfView;
            float radius = b.extents.magnitude * Padding;
            float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad);
            camGo.transform.position = b.center + viewAngle * Vector3.back * distance;
            camGo.transform.LookAt(b.center);
            cam.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            cam.farClipPlane = distance + radius * 2f;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var big = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            big.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            big.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Texture2D icon = Downsample(big, Supersample);
            Object.DestroyImmediate(big);
            AddOutline(icon);
            return icon;
        }
        finally
        {
            rt.Release();
            Object.DestroyImmediate(rt);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void AddLight(Scene scene, Quaternion rotation, float intensity, Color color)
    {
        var go = new GameObject("IconLight");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.rotation = rotation;
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
    }

    private static Bounds RendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.5f);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // 카메라가 투명 배경에 그린 색은 알파가 곱해져 있지 않으므로 알파 가중 평균으로 줄인다(가장자리 검은 테 방지).
    private static Texture2D Downsample(Texture2D src, int factor)
    {
        int w = src.width / factor, h = src.height / factor;
        Color[] s = src.GetPixels();
        var d = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float r = 0, g = 0, bl = 0, a = 0;
            for (int j = 0; j < factor; j++)
            for (int i = 0; i < factor; i++)
            {
                Color c = s[(y * factor + j) * src.width + x * factor + i];
                r += c.r * c.a; g += c.g * c.a; bl += c.b * c.a; a += c.a;
            }
            d[y * w + x] = a > 0f ? new Color(r / a, g / a, bl / a, a / (factor * factor)) : new Color(0f, 0f, 0f, 0f);
        }
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        dst.SetPixels(d);
        dst.Apply();
        return dst;
    }

    // 어두운 칸 배경에서도 모양이 보이도록 둘레에 어두운 테를 두른다.
    private static void AddOutline(Texture2D tex)
    {
        int w = tex.width, h = tex.height;
        Color[] p = tex.GetPixels();
        var result = (Color[])p.Clone();
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (p[y * w + x].a >= 0.99f) continue;
            float near = 0f;
            for (int j = -OutlinePixels; j <= OutlinePixels; j++)
            for (int i = -OutlinePixels; i <= OutlinePixels; i++)
            {
                int xx = x + i, yy = y + j;
                if (xx < 0 || yy < 0 || xx >= w || yy >= h || i * i + j * j > OutlinePixels * OutlinePixels) continue;
                near = Mathf.Max(near, p[yy * w + xx].a);
            }
            if (near <= 0f) continue;
            Color src = p[y * w + x];
            float outA = OutlineColor.a * near;
            float a = src.a + outA * (1f - src.a);
            Color rgb = a > 0f ? (src * src.a + OutlineColor * outA * (1f - src.a)) / a : OutlineColor;
            result[y * w + x] = new Color(rgb.r, rgb.g, rgb.b, a);
        }
        tex.SetPixels(result);
        tex.Apply();
    }
}
