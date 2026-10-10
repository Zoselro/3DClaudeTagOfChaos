using TMPro;
using UnityEngine;

// 상호작용 안내 아이콘(GameFixPlan.md F4). 상호작용할 수 있는 사물의 위치(IInteractable.InteractionPoint)에
// 동그라미 안에 키 글자(기본 E)가 든 아이콘을 띄우고, 사물이 이름을 주면(IInteractionLabel) 아래에 작게 붙인다.
// 예전에는 화면 아래 가운데에 "Press the 'E'" 글자를 띄웠다. CharacterInteractor만 켜고 끈다.
// 프리팹: Resources/UI/Scene/InteractionPromptUI/InteractionPromptUI — 없거나 옛 모양이면 에디터의 InteractionPromptBuilder가 만든다.
// 게임 조작을 가리지 않도록 입력(레이캐스트)은 항상 통과시키고 알파만 바꾼다.
public class InteractionPromptUI : MonoBehaviour
{
    public const string ResourcePath = "UI/Scene/InteractionPromptUI/InteractionPromptUI";

    [SerializeField] private RectTransform iconRoot; // 동그라미(화면 좌표로 사물 위치를 따라간다)
    [SerializeField] private TMP_Text keyLabel;      // 동그라미 안 키 글자
    [SerializeField] private TMP_Text nameLabel;     // 아이콘 아래 사물 이름(선택)
    [Tooltip("길게 누르기 진행 링(Image — Filled·Radial360). 0이면 숨긴다(Request1009Plan.md §1).")]
    [SerializeField] private UnityEngine.UI.Image progressRing;
    [Tooltip("사물 기준점(InteractionPoint)에서 아이콘을 띄울 높이(m). 문은 기준점이 문짝 바닥이라 올려 준다.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);

    private CanvasGroup group;
    private KeyCode shownKey = KeyCode.None;
    private string shownName;
    private bool visible = true;
    private Vector3 worldAnchor;

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

    // worldPoint: 사물 기준점(월드). displayName: 아이콘 아래 이름, 없으면 null.
    public void Show(KeyCode key, Vector3 worldPoint, string displayName)
    {
        if (key != shownKey && keyLabel != null)
        {
            keyLabel.text = KeyToLabel(key);
            shownKey = key;
        }
        if (displayName != shownName && nameLabel != null)
        {
            nameLabel.text = displayName ?? string.Empty;
            shownName = displayName;
        }
        worldAnchor = worldPoint + worldOffset;
        visible = true;
        UpdatePosition();
    }

    // 길게 누르기 진행(0~1). 0이면 링을 숨긴다.
    public void SetProgress(float progress)
    {
        if (progressRing == null) return;
        bool show = progress > 0f;
        if (progressRing.enabled != show) progressRing.enabled = show;
        if (show) progressRing.fillAmount = UnityEngine.Mathf.Clamp01(progress);
    }

    public void Hide()
    {
        visible = false;
        if (group != null) group.alpha = 0f;
    }

    private void LateUpdate()
    {
        if (visible) UpdatePosition();
    }

    // 사물이 카메라 뒤에 있으면 숨긴다.
    private void UpdatePosition()
    {
        Camera cam = Camera.main;
        if (cam == null || iconRoot == null)
        {
            group.alpha = 0f;
            return;
        }
        Vector3 screen = cam.WorldToScreenPoint(worldAnchor);
        bool inFront = screen.z > 0f;
        group.alpha = inFront ? 1f : 0f;
        if (inFront) iconRoot.position = new Vector3(screen.x, screen.y, 0f);
    }

    // 동그라미 안에는 한 글자만 들어간다: 알파벳·숫자 키는 그 글자, 나머지는 키 이름의 첫 글자.
    private static string KeyToLabel(KeyCode key)
    {
        if (key >= KeyCode.A && key <= KeyCode.Z) return key.ToString();
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
        string name = key.ToString();
        return name.Length > 0 ? name.Substring(0, 1) : string.Empty;
    }
}
