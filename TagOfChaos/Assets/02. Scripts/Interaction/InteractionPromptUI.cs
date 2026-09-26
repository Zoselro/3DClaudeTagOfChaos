using TMPro;
using UnityEngine;

// 화면 하단 가운데 상호작용 안내 문구("Press the 'E'", GameLobbyScene.md §14). CharacterInteractor만 켜고 끈다.
// 프리팹: Resources/UI/Scene/InteractionPromptUI/InteractionPromptUI — 없으면 에디터의 InteractionPromptBuilder가 만든다.
// 게임 조작을 가리지 않도록 입력(레이캐스트)은 항상 통과시키고 알파만 바꾼다.
public class InteractionPromptUI : MonoBehaviour
{
    public const string ResourcePath = "UI/Scene/InteractionPromptUI/InteractionPromptUI";

    [SerializeField] private TMP_Text label;
    [SerializeField] private string format = "Press the '{0}'";

    private CanvasGroup group;
    private KeyCode shownKey = KeyCode.None;
    private bool visible = true;

    public static InteractionPromptUI Create(Transform parent)
    {
        var prefab = Resources.Load<InteractionPromptUI>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[InteractionPromptUI] Resources/{ResourcePath} not found. Run Tools/TagOfChaos/Build Interaction Prompt.");
            return null;
        }
        return Instantiate(prefab, parent);
    }

    private void Awake()
    {
        group = CanvasGroupVisibility.Ensure(gameObject);
        group.interactable = false;
        group.blocksRaycasts = false;
        Hide();
    }

    public void Show(KeyCode key)
    {
        if (key != shownKey && label != null)
        {
            label.text = string.Format(format, key);
            shownKey = key;
        }
        SetVisible(true);
    }

    public void Hide() => SetVisible(false);

    private void SetVisible(bool value)
    {
        if (visible == value) return;
        visible = value;
        group.alpha = value ? 1f : 0f;
    }
}
