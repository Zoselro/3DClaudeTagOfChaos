using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 상호작용 안내 아이콘 프리팹(InteractionPromptUI)을 만든다(GameFixPlan.md F4). 동그라미 안에 키 글자가 들어가고,
// 아래에 사물 이름이 작게 붙는다. 동그라미 그림(PromptCircle.png)도 코드로 만든다.
// 프리팹이 없거나 옛 모양(화면 아래 글자 띠)이면 에디터 로드 시 한 번 자동으로 다시 만들고, 이후에는 메뉴에서 다시 만들 수 있다.
// 모양(크기·색·폰트)은 만들어진 프리팹에서 바로 조정하면 된다 — 다시 빌드하면 기본값으로 덮어쓴다.
[InitializeOnLoad]
public static class InteractionPromptBuilder
{
    private const string MenuPath = "Tools/TagOfChaos/Build Interaction Prompt";
    private const string LogTag = "[InteractionPrompt]";
    private const string ParentFolder = "Assets/Resources/UI/Scene";
    private const string PrefabFolder = ParentFolder + "/InteractionPromptUI";
    private const string PrefabPath = PrefabFolder + "/InteractionPromptUI.prefab";
    private const string CirclePath = PrefabFolder + "/PromptCircle.png";
    private const string FontPath = "Assets/Fonts/NotoSansKR SDF.asset";
    private const int CircleSize = 128;
    private const float IconSize = 56f;

    static InteractionPromptBuilder()
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
        var existing = AssetDatabase.LoadAssetAtPath<InteractionPromptUI>(PrefabPath);
        if (existing != null && new SerializedObject(existing).FindProperty("iconRoot").objectReferenceValue != null) return;
        Build(); // 없거나 옛 모양(iconRoot 없음)
    }

    [MenuItem(MenuPath)]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder(ParentFolder, "InteractionPromptUI");
        Sprite circle = BuildCircleSprite();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("InteractionPromptUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            SceneManager.MoveGameObjectToScene(root, preview);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 동그라미(피벗 가운데 — 화면 좌표로 사물 위치를 따라간다)
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.zero;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite = circle;
            iconImage.raycastTarget = false;

            TextMeshProUGUI key = CreateText("Key", icon.transform, font, 32f, FontStyles.Bold);
            var keyRect = key.rectTransform;
            keyRect.anchorMin = Vector2.zero;
            keyRect.anchorMax = Vector2.one;
            keyRect.offsetMin = Vector2.zero;
            keyRect.offsetMax = Vector2.zero;
            key.text = "E";

            TextMeshProUGUI nameText = CreateText("Name", icon.transform, font, 22f, FontStyles.Normal);
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 0f);
            nameRect.anchorMax = new Vector2(0.5f, 0f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -6f);
            nameRect.sizeDelta = new Vector2(320f, 60f);
            nameText.alignment = TextAlignmentOptions.Top;
            nameText.outlineWidth = 0.2f;
            nameText.outlineColor = new Color32(0, 0, 0, 200);
            nameText.text = string.Empty;

            var prompt = root.AddComponent<InteractionPromptUI>();
            var serialized = new SerializedObject(prompt);
            serialized.FindProperty("iconRoot").objectReferenceValue = iconRect;
            serialized.FindProperty("keyLabel").objectReferenceValue = key;
            serialized.FindProperty("nameLabel").objectReferenceValue = nameText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (saved) Debug.Log($"{LogTag} Saved {PrefabPath}");
            else Debug.LogError($"{LogTag} Failed to save prefab: {PrefabPath}");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // 반투명 어두운 원 + 흰 테두리. 가장자리는 부드럽게(안티에일리어싱).
    private static Sprite BuildCircleSprite()
    {
        var tex = new Texture2D(CircleSize, CircleSize, TextureFormat.RGBA32, false);
        float center = (CircleSize - 1) / 2f;
        float outer = CircleSize / 2f - 1f;
        const float ring = 7f;
        var fill = new Color(0f, 0f, 0f, 0.6f);
        for (int y = 0; y < CircleSize; y++)
        for (int x = 0; x < CircleSize; x++)
        {
            float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
            float edge = Mathf.Clamp01(outer - d + 0.5f);          // 바깥 가장자리
            float ringMix = Mathf.Clamp01(d - (outer - ring) + 0.5f); // 테두리 안쪽 경계
            Color c = Color.Lerp(fill, Color.white, ringMix);
            c.a *= edge;
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        File.WriteAllBytes(CirclePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(CirclePath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(CirclePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
    }
}
