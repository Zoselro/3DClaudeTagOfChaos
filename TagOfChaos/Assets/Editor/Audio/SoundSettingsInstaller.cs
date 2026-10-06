using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 소리 설정 창 설치(SoundPlan.md S9). Tools/TagOfChaos/Audio/Install Sound Settings.
// 1) 프리팹 Assets/04. Prefabs/UI/SoundSettingsPanel.prefab 을 만든다(이미 있으면 그대로 — 프리팹에서 고친 문구·배치를 지킨다).
//    모양(상자·버튼·글꼴·색)은 GameSceneCore의 ESC 메뉴에서 가져와 같은 느낌을 낸다.
// 2) ESC 메뉴(GameSceneCore 프리팹 = 맵 5개, GameLobbyScene)에 "소리" 버튼과 창을 넣고 EscMenu에 연결한다.
// 3) LobbyScene 오른쪽 위에 "소리" 버튼과 창을 넣는다(ESC로 닫힘).
// 이미 들어가 있으면 건너뛴다. 영어 임시 문구로 만들고, 한글 문구는 프리팹·씬에서 입력한다(코드에 한글 금지).
public static class SoundSettingsInstaller
{
    public const string PrefabPath = "Assets/04. Prefabs/UI/SoundSettingsPanel.prefab";
    public const string CorePrefabPath = "Assets/04. Prefabs/Scene/GameSceneCore.prefab";
    public const string GameLobbyScenePath = "Assets/Scenes/GameLobbyScene.unity";
    public const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
    public const string PanelName = "SoundSettings";
    public const string ButtonName = "SoundButton";
    private const float RowHeight = 70f;

    private static readonly (AudioVolumeSettings.Slider kind, string name, string label)[] Rows =
    {
        (AudioVolumeSettings.Slider.Master, "Row_Master", "Master"),
        (AudioVolumeSettings.Slider.Music, "Row_Music", "Music"),
        (AudioVolumeSettings.Slider.Sfx, "Row_Sfx", "Effects"),
        (AudioVolumeSettings.Slider.Ui, "Row_Ui", "UI"),
    };

    private class Style
    {
        public Sprite boxSprite; public Color boxColor = new Color(0.15f, 0.12f, 0.2f, 0.95f); public Color dimColor = new Color(0f, 0f, 0f, 0.55f);
        public Sprite buttonSprite; public Color buttonColor = Color.white; public ColorBlock buttonColors = ColorBlock.defaultColorBlock;
        public TMP_FontAsset font; public Color textColor = Color.white; public Color labelColor = Color.black;
        public GameObject buttonTemplate;
    }

    [MenuItem("Tools/TagOfChaos/Audio/Install Sound Settings")]
    public static void InstallAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

        GameObject core = PrefabUtility.LoadPrefabContents(CorePrefabPath);
        Style style = ReadStyle(core.GetComponentInChildren<EscMenu>(true));
        SoundSettingsPanel prefab = EnsurePanelPrefab(style);
        string coreResult = InstallInEscMenu(core.GetComponentInChildren<EscMenu>(true), prefab);
        PrefabUtility.SaveAsPrefabAsset(core, CorePrefabPath);
        PrefabUtility.UnloadPrefabContents(core);

        var gameLobby = EditorSceneManager.OpenScene(GameLobbyScenePath, OpenSceneMode.Single);
        EscMenu lobbyEsc = Object.FindFirstObjectByType<EscMenu>(FindObjectsInactive.Include);
        string gameLobbyResult = lobbyEsc != null ? InstallInEscMenu(lobbyEsc, prefab) : "no EscMenu";
        EditorSceneManager.MarkSceneDirty(gameLobby);
        EditorSceneManager.SaveScene(gameLobby);

        var lobby = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        string lobbyResult = InstallInLobby(prefab, style);
        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);

        AssetDatabase.SaveAssets();
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log($"[Audio] Sound settings installed — GameSceneCore: {coreResult}, GameLobbyScene: {gameLobbyResult}, LobbyScene: {lobbyResult}");
    }

    // ---------------- style ----------------

    private static Style ReadStyle(EscMenu esc)
    {
        var s = new Style();
        if (esc == null) return s;
        Transform panel = esc.transform.Find("Panel");
        if (panel != null && panel.TryGetComponent(out Image dim)) s.dimColor = dim.color;
        Transform box = panel != null ? panel.Find("Box") : null;
        if (box != null && box.TryGetComponent(out Image boxImage)) { s.boxSprite = boxImage.sprite; s.boxColor = boxImage.color; }
        Transform title = box != null ? box.Find("Title") : null;
        if (title != null && title.TryGetComponent(out TMP_Text t)) { s.font = t.font; s.textColor = t.color; }
        Transform resume = box != null ? box.Find("Buttons/ResumeButton") : null;
        if (resume != null)
        {
            s.buttonTemplate = resume.gameObject;
            if (resume.TryGetComponent(out Image bi)) { s.buttonSprite = bi.sprite; s.buttonColor = bi.color; }
            if (resume.TryGetComponent(out Button b)) s.buttonColors = b.colors;
            TMP_Text label = resume.GetComponentInChildren<TMP_Text>(true);
            if (label != null) s.labelColor = label.color;
        }
        return s;
    }

    // ---------------- prefab ----------------

    private static SoundSettingsPanel EnsurePanelPrefab(Style style)
    {
        var existing = AssetDatabase.LoadAssetAtPath<SoundSettingsPanel>(PrefabPath);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder("Assets/04. Prefabs/UI")) AssetDatabase.CreateFolder("Assets/04. Prefabs", "UI");

        var root = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
        Stretch((RectTransform)root.transform);
        root.GetComponent<Image>().color = new Color(style.dimColor.r, style.dimColor.g, style.dimColor.b, Mathf.Max(0.6f, style.dimColor.a)); // 뒤 화면 클릭 막기 + 어둡게

        var box = Child(root.transform, "Box", typeof(Image));
        SetRect(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 500f));
        var boxImage = box.GetComponent<Image>(); boxImage.sprite = style.boxSprite; boxImage.color = new Color(style.boxColor.r, style.boxColor.g, style.boxColor.b, 1f); // 불투명 — 로비에서 뒤 글자가 비치지 않게 boxImage.type = style.boxSprite != null ? Image.Type.Sliced : Image.Type.Simple;

        TMP_Text title = Text(box, "Title", "Sound", 40f, style.font, style.textColor, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -20f), new Vector2(0f, 56f), new Vector2(0.5f, 1f));

        var panel = root.AddComponent<SoundSettingsPanel>();
        var so = new SerializedObject(panel);
        SerializedProperty rows = so.FindProperty("rows");
        rows.arraySize = Rows.Length;
        for (int i = 0; i < Rows.Length; i++)
        {
            var row = Child(box, Rows[i].name);
            SetRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -100f - i * RowHeight), new Vector2(-60f, 56f), new Vector2(0.5f, 1f));
            TMP_Text label = Text(row, "Label", Rows[i].label, 28f, style.font, style.textColor, TextAlignmentOptions.MidlineLeft);
            SetRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 0f), new Vector2(150f, 0f), new Vector2(0f, 0.5f));
            Slider slider = CreateSlider(row, style);
            SetRect((RectTransform)slider.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(35f, 0f), new Vector2(-310f, 26f)); // 이름 칸(10~170)과 값 칸(오른쪽 110) 사이
            TMP_Text value = Text(row, "Value", "100%", 26f, style.font, style.textColor, TextAlignmentOptions.MidlineRight);
            SetRect(value.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-10f, 0f), new Vector2(100f, 0f), new Vector2(1f, 0.5f));

            SerializedProperty e = rows.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("kind").enumValueIndex = (int)Rows[i].kind;
            e.FindPropertyRelative("slider").objectReferenceValue = slider;
            e.FindPropertyRelative("value").objectReferenceValue = value;
        }

        Button close = CreateButton(box, "BackButton", "Close", style); // 이름 Back → 취소 소리(UiSoundInstaller 표)
        SetRect((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(240f, 60f), new Vector2(0.5f, 0f));
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<SoundSettingsPanel>(PrefabPath);
    }

    private static Slider CreateSlider(Transform parent, Style style)
    {
        var res = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        };
        GameObject go = DefaultControls.CreateSlider(res);
        go.name = "Slider";
        go.transform.SetParent(parent, false);
        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
        Transform bg = go.transform.Find("Background"), fill = go.transform.Find("Fill Area/Fill"), knob = go.transform.Find("Handle Slide Area/Handle");
        if (bg != null) bg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);
        if (fill != null) fill.GetComponent<Image>().color = new Color(1f, 0.78f, 0.35f); // 캐러멜색
        if (knob != null) knob.GetComponent<Image>().color = Color.white;
        return slider;
    }

    // ---------------- install ----------------

    private static string InstallInEscMenu(EscMenu esc, SoundSettingsPanel prefab)
    {
        if (esc == null) return "no EscMenu";
        var so = new SerializedObject(esc);
        Transform buttons = esc.transform.Find("Panel/Box/Buttons");
        Transform resume = buttons != null ? buttons.Find("ResumeButton") : null;
        if (buttons == null || resume == null) return "no Buttons/ResumeButton";

        bool added = false;
        Transform soundButton = buttons.Find(ButtonName);
        if (soundButton == null)
        {
            GameObject copy = Object.Instantiate(resume.gameObject, buttons);
            copy.name = ButtonName;
            copy.transform.SetSiblingIndex(resume.GetSiblingIndex() + 1);
            copy.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
            TMP_Text label = copy.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "Sound";
            soundButton = copy.transform;
            var box = (RectTransform)esc.transform.Find("Panel/Box");
            if (box != null) box.sizeDelta += new Vector2(0f, RowHeight); // 버튼 한 줄만큼 상자를 키운다
            added = true;
        }

        SoundSettingsPanel existing = esc.GetComponentInChildren<SoundSettingsPanel>(true); // 이름은 프리팹 이름이 될 수 있어 컴포넌트로 찾는다
        Transform panelTr = existing != null ? existing.transform : null;
        if (panelTr == null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, esc.transform);
            Stretch((RectTransform)inst.transform);
            inst.transform.SetAsLastSibling();
            inst.SetActive(false);
            panelTr = inst.transform;
            added = true;
        }

        so.FindProperty("soundButton").objectReferenceValue = soundButton.GetComponent<Button>();
        so.FindProperty("soundSettings").objectReferenceValue = panelTr.GetComponent<SoundSettingsPanel>();
        so.ApplyModifiedPropertiesWithoutUndo();
        return added ? "added" : "already";
    }

    private static string InstallInLobby(SoundSettingsPanel prefab, Style style)
    {
        GameObject canvasGo = GameObject.Find("LobbyUICanvas");
        if (canvasGo == null) return "no LobbyUICanvas";
        Transform canvas = canvasGo.transform;
        bool added = false;

        SoundSettingsPanel existing = canvas.GetComponentInChildren<SoundSettingsPanel>(true);
        Transform panelTr = existing != null ? existing.transform : null;
        if (panelTr == null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, canvas);
            Stretch((RectTransform)inst.transform);
            inst.transform.SetAsLastSibling();
            panelTr = inst.transform;
            added = true;
        }
        var panel = panelTr.GetComponent<SoundSettingsPanel>();
        var pso = new SerializedObject(panel);
        pso.FindProperty("closeOnEscape").boolValue = true; // 로비에는 ESC 메뉴가 없다
        pso.ApplyModifiedPropertiesWithoutUndo();
        panelTr.gameObject.SetActive(false);

        Transform buttonTr = canvas.Find(ButtonName);
        if (buttonTr == null)
        {
            Button b = CreateButton(canvas, ButtonName, "Sound", style);
            SetRect((RectTransform)b.transform, Vector2.one, Vector2.one, new Vector2(-30f, -30f), new Vector2(160f, 56f), Vector2.one);
            b.transform.SetSiblingIndex(panelTr.GetSiblingIndex()); // 창보다 뒤(아래)에 그린다
            buttonTr = b.transform;
            added = true;
        }
        var button = buttonTr.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, panel.Open);
        return added ? "added" : "already";
    }

    // ---------------- helpers ----------------

    private static Button CreateButton(Transform parent, string name, string text, Style style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.sprite = style.buttonSprite; img.color = style.buttonColor; img.type = style.buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        var b = go.GetComponent<Button>(); b.colors = style.buttonColors; b.targetGraphic = img;
        TMP_Text label = Text(go.transform, "Label", text, 26f, style.font, style.labelColor, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return b;
    }

    private static TMP_Text Text(Transform parent, string name, string text, float size, TMP_FontAsset font, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
        if (font != null) t.font = font;
        return t;
    }

    private static RectTransform Child(Transform parent, string name, params System.Type[] extra)
    {
        var types = new System.Type[extra.Length + 1]; types[0] = typeof(RectTransform); extra.CopyTo(types, 1);
        var go = new GameObject(name, types);
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetRect(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot ?? new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }
}
