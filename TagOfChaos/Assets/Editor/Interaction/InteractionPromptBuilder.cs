using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 화면 하단 가운데 상호작용 안내 문구 프리팹(InteractionPromptUI)을 만든다(GameLobbyScene.md §14).
// 프리팹이 없으면 에디터 로드 시 한 번 자동 실행되고, 이후에는 메뉴에서 다시 만들 수 있다.
// 모양(위치·크기·색·폰트)은 만들어진 프리팹에서 바로 조정하면 된다 — 다시 빌드하면 기본값으로 덮어쓴다.
[InitializeOnLoad]
public static class InteractionPromptBuilder
{
    private const string MenuPath = "Tools/TagOfChaos/Build Interaction Prompt";
    private const string LogTag = "[InteractionPrompt]";
    private const string ParentFolder = "Assets/Resources/UI/Scene";
    private const string PrefabFolder = ParentFolder + "/InteractionPromptUI";
    private const string PrefabPath = PrefabFolder + "/InteractionPromptUI.prefab";
    private const string FontPath = "Assets/Fonts/NotoSansKR SDF.asset";

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
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        Build();
    }

    [MenuItem(MenuPath)]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder(ParentFolder, "InteractionPromptUI");

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

            // 하단 가운데 반투명 띠
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = new Vector2(0.5f, 0f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0f);
            backgroundRect.pivot = new Vector2(0.5f, 0f);
            backgroundRect.anchoredPosition = new Vector2(0f, 120f);
            backgroundRect.sizeDelta = new Vector2(360f, 64f);
            var image = background.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            image.raycastTarget = false;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(background.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            label.text = "Press the 'E'";
            label.fontSize = 32f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            var prompt = root.AddComponent<InteractionPromptUI>();
            var serialized = new SerializedObject(prompt);
            serialized.FindProperty("label").objectReferenceValue = label;
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
}
