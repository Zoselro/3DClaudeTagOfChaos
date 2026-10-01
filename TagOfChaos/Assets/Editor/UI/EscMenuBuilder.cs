using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// ESC 메뉴 UI 만들기(EscapeVisualPlan.md §1.2). GameSceneCore 프리팹(게임 씬 5개 공용)과 대기실 씬에 같은 메뉴를 넣는다.
// 버튼 모양은 나가기 확인창의 버튼을 복제해 맞추고, 메뉴와 확인창은 탈출 HUD(정렬 40)보다 위에 그린다.
public static class EscMenuBuilder
{
    private const string PrefabPath = "Assets/04. Prefabs/Scene/GameSceneCore.prefab";
    private const string GameLobbyScenePath = "Assets/Scenes/GameLobbyScene.unity";
    private const int MenuSortingOrder = 100;
    private const int DialogSortingOrder = 110;

    [MenuItem("Tools/TagOfChaos/UI/Build ESC Menu")]
    public static void BuildAll() => Debug.Log(Build());

    public static string Build()
    {
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        string a;
        try
        {
            a = BuildInto(prefab.GetComponentInChildren<RoomExitController>(true), prefab.GetComponentInChildren<ConfirmDialog>(true));
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        var scene = EditorSceneManager.OpenScene(GameLobbyScenePath, OpenSceneMode.Single);
        // 대기실은 나가기 담당(GameManager)과 확인창(Canvas)이 서로 다른 루트에 있다.
        string b = BuildInto(Object.FindFirstObjectByType<RoomExitController>(FindObjectsInactive.Include),
                             Object.FindFirstObjectByType<ConfirmDialog>(FindObjectsInactive.Include));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"[EscMenu] GameSceneCore: {a} / GameLobbyScene: {b}";
    }

    private static string BuildInto(RoomExitController exit, ConfirmDialog dialog)
    {
        if (exit == null || dialog == null) return "missing RoomExitController/ConfirmDialog";
        // 확인창에는 자체 Canvas(정렬용)가 붙으므로 그 위 부모의 Canvas에 메뉴를 둔다.
        Canvas canvas = dialog.transform.parent.GetComponentInParent<Canvas>(true);

        // 다시 만들 때는 씬·프리팹에 입력해 둔 문구(한글)를 그대로 옮긴다. 처음 만들 때는 영어 기본값(코드에 한글 금지).
        Transform old = canvas.transform.Find("EscMenu");
        foreach (EscMenu stray in canvas.GetComponentsInChildren<EscMenu>(true))   // 이전 빌드가 다른 자리에 남긴 메뉴
            if (old == null || stray.transform != old) Object.DestroyImmediate(stray.gameObject);
        string titleLabel = Label(old, "Panel/Box/Title", "Menu");
        string resumeLabel = Label(old, "Panel/Box/Buttons/ResumeButton", "Resume");
        string settingsLabel = Label(old, "Panel/Box/Buttons/SettingsButton", "Settings");
        string exitLabel = Label(old, "Panel/Box/Buttons/ExitButton", "Leave");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var menuGo = new GameObject("EscMenu", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var menuRt = (RectTransform)menuGo.transform;
        menuRt.SetParent(canvas.transform, false);
        Stretch(menuRt);
        var menuCanvas = menuGo.GetComponent<Canvas>();
        menuCanvas.overrideSorting = true;
        menuCanvas.sortingOrder = MenuSortingOrder;

        // 확인창도 HUD 위에 그린다.
        var dialogCanvas = dialog.GetComponent<Canvas>();
        if (dialogCanvas == null) dialogCanvas = dialog.gameObject.AddComponent<Canvas>(); // ?? 는 Unity의 가짜 null을 거르지 못한다
        dialogCanvas.overrideSorting = true;
        dialogCanvas.sortingOrder = DialogSortingOrder;
        if (dialog.GetComponent<GraphicRaycaster>() == null) dialog.gameObject.AddComponent<GraphicRaycaster>();

        var dim = NewImage("Panel", menuRt, new Color(0f, 0f, 0f, 0.55f));
        Stretch(dim.rectTransform);

        var dialogImage = dialog.GetComponent<Image>();
        var box = NewImage("Box", dim.rectTransform, dialogImage != null ? dialogImage.color : new Color(0.15f, 0.1f, 0.2f, 0.95f));
        if (dialogImage != null) { box.sprite = dialogImage.sprite; box.type = dialogImage.type; }
        box.rectTransform.sizeDelta = new Vector2(440f, 270f); // 설정 버튼을 켜면 높이를 74 늘린다

        var messageText = dialog.transform.Find("MessageText")?.GetComponent<TMP_Text>();
        var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        title.rectTransform.SetParent(box.rectTransform, false);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -20f);
        title.rectTransform.sizeDelta = new Vector2(0f, 56f);
        if (messageText != null) { title.font = messageText.font; title.color = messageText.color; }
        title.fontSize = 40;
        title.alignment = TextAlignmentOptions.Center;
        title.text = titleLabel;

        var list = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        list.SetParent(box.rectTransform, false);
        list.anchorMin = new Vector2(0f, 0f);
        list.anchorMax = new Vector2(1f, 1f);
        list.offsetMin = new Vector2(40f, 30f);
        list.offsetMax = new Vector2(-40f, -96f);
        var layout = list.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        Button template = dialog.transform.Find("YesButton").GetComponent<Button>();
        Button resume = CloneButton(template, list, "ResumeButton", resumeLabel);
        Button settings = CloneButton(template, list, "SettingsButton", settingsLabel);
        settings.gameObject.SetActive(false); // 설정 화면은 나중에 추가한다(결정 Q4)
        Button exitButton = CloneButton(template, list, "ExitButton", exitLabel);

        var menu = menuGo.AddComponent<EscMenu>();
        var so = new SerializedObject(menu);
        so.FindProperty("panel").objectReferenceValue = dim.gameObject;
        so.FindProperty("resumeButton").objectReferenceValue = resume;
        so.FindProperty("exitButton").objectReferenceValue = exitButton;
        so.FindProperty("roomExit").objectReferenceValue = exit;
        so.FindProperty("confirmDialog").objectReferenceValue = dialog.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        dim.gameObject.SetActive(false);
        return "built";
    }

    private static string Label(Transform menuRoot, string path, string fallback)
    {
        Transform t = menuRoot != null ? menuRoot.Find(path) : null;
        TMP_Text text = t != null ? t.GetComponentInChildren<TMP_Text>(true) : null;
        return text != null && !string.IsNullOrEmpty(text.text) ? text.text : fallback;
    }

    private static Button CloneButton(Button template, Transform parent, string name, string label)
    {
        var go = Object.Instantiate(template.gameObject, parent);
        go.name = name;
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 60f;
        var text = go.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
        var button = go.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        return button;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.color = color;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
