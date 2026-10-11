using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 대기실 강퇴 화면(Request1009Plan.md §19.6)을 프리팹·씬에 넣는다. 여러 번 실행해도 같은 결과.
// 1) PlayerListItem 프리팹 오른쪽에 "강퇴" 버튼(KickButton)을 붙이고 이름 칸을 그만큼 줄인다.
// 2) GameLobbyScene의 강퇴 확인 문구, LobbyScene의 "강퇴되었습니다" 문구를 한글로 넣는다(코드에는 영어 기본값만).
public static class LobbyKickInstaller
{
    public const string PrefabPath = "Assets/Resources/UI/Scene/PlayerListItem/PlayerListItem.prefab";
    public const string GameLobbyScenePath = "Assets/Scenes/GameLobbyScene.unity";
    public const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
    public const string KickButtonName = "KickButton";
    private const float ButtonWidth = 90f;

    private const string KickLabel = "강퇴";                                                      // 강퇴
    private const string KickConfirm = "{0}님을 강퇴할까요?";                // {0}님을 강퇴할까요?
    private const string KickedMessage = "방장에 의해 강퇴되었습니다."; // 방장에 의해 강퇴되었습니다.

    [MenuItem("Tools/Lobby/Setup Kick Button")]
    public static void SetupMenu() => Debug.Log("[LobbyKickInstaller] " + SetupAll());

    public static string SetupAll()
    {
        string prefab = SetupPrefab();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        string texts = SetSceneString(GameLobbyScenePath, typeof(GameLobbyController), "kickConfirmFormat", KickConfirm)
                     + ", " + SetSceneString(LobbyScenePath, typeof(LobbyController), "kickedMessage", KickedMessage);
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        return prefab + "; " + texts;
    }

    private static string SetupPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var item = root.GetComponent<PlayerListItem>();
            var nickname = root.transform.Find("NicknameText") as RectTransform;
            TMP_Text nicknameText = nickname != null ? nickname.GetComponent<TMP_Text>() : null;
            if (nickname != null) nickname.offsetMax = new Vector2(-(ButtonWidth + 30f), nickname.offsetMax.y); // 버튼 자리만큼 이름 칸을 줄인다

            Transform old = root.transform.Find(KickButtonName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var buttonGo = new GameObject(KickButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rt = (RectTransform)buttonGo.transform;
            rt.SetParent(root.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(ButtonWidth, 36f);
            rt.anchoredPosition = new Vector2(-12f, 0f);
            var image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.62f, 0.16f, 0.18f, 0.95f);
            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var lrt = (RectTransform)labelGo.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = KickLabel;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 22f;
            label.color = Color.white;
            if (nicknameText != null) { label.font = nicknameText.font; label.fontSharedMaterial = nicknameText.fontSharedMaterial; }
            buttonGo.SetActive(false); // 호스트 화면에서만 켠다(GameLobbyController)

            var so = new SerializedObject(item);
            so.FindProperty("kickButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
            UiSoundInstaller.Attach(new[] { button }); // 다른 버튼과 같은 클릭 소리
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "prefab KickButton";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static string SetSceneString(string scenePath, System.Type type, string field, string value)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var target = Object.FindFirstObjectByType(type, FindObjectsInactive.Include);
        if (target == null) return System.IO.Path.GetFileNameWithoutExtension(scenePath) + " missing " + type.Name;
        var so = new SerializedObject(target);
        so.FindProperty(field).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return System.IO.Path.GetFileNameWithoutExtension(scenePath) + "." + field;
    }
}
