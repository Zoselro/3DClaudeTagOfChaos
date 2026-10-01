using UnityEngine;
using UnityEngine.UI;

// ESC 메뉴(EscapeVisualPlan.md §1.2, 결정 Q4). 쿠키·스파이·괴물 모두 ESC로 연다. 열려 있는 동안(나가기 확인창 포함)
// 이동·시점·상호작용 입력을 막고 커서를 풀어 버튼을 누를 수 있게 한다 — 괴물 1인칭 카메라는 매 프레임 커서를 잠그므로 이 값을 본다.
// 게임은 멈추지 않는다(온라인). 버튼 목록은 Buttons 아래에 늘어서므로 나중에 "설정" 버튼(지금은 꺼 둠)을 켜기만 하면 된다.
// 문구는 프리팹·씬에서 입력한다(코드에 한글 금지).
public class EscMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private RoomExitController roomExit;
    [SerializeField] private GameObject confirmDialog;
    [Header("Room settings (GameLobbyScene, host only)")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private RoomSettingsMenu roomSettings; // 대기실에만 연결된다 — 없으면 설정 버튼을 숨긴다
    [SerializeField] private RectTransform box;             // 버튼이 하나 늘면 상자를 키운다
    [SerializeField, Min(0f)] private float settingsRowHeight = 70f;
    private float baseBoxHeight = -1f;

    public static EscMenu Instance { get; private set; }

    // 메뉴나 나가기 확인창이 떠 있으면 true.
    public static bool IsOpen => Instance != null && Instance.Visible;

    private bool Visible => (panel != null && panel.activeSelf) || (confirmDialog != null && confirmDialog.activeSelf)
                            || (roomSettings != null && roomSettings.IsOpen);

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (roomSettings != null)
        {
            roomSettings.gameObject.SetActive(false);
            roomSettings.Closed += Open; // 설정 창에서 뒤로 → 메뉴
        }
        if (box != null) baseBoxHeight = box.sizeDelta.y;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PlayerInput.IsMenuOpen = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Visible) Close();
            else Open();
        }
        bool open = Visible;
        if (PlayerInput.IsMenuOpen != open) PlayerInput.IsMenuOpen = open;
        if (open && Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
    }

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
        // 방 설정은 대기실의 방장만(로비에서 정한 값을 다시 바꾼다)
        bool canSet = roomSettings != null && Photon.Pun.PhotonNetwork.IsMasterClient;
        if (settingsButton != null) settingsButton.gameObject.SetActive(canSet);
        if (box != null && baseBoxHeight > 0f) box.sizeDelta = new Vector2(box.sizeDelta.x, baseBoxHeight + (canSet ? settingsRowHeight : 0f));
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        if (confirmDialog != null) confirmDialog.SetActive(false);
        if (roomSettings != null && roomSettings.IsOpen) roomSettings.gameObject.SetActive(false);
    }

    private void OnSettingsClicked()
    {
        if (roomSettings == null) return;
        if (panel != null) panel.SetActive(false);
        roomSettings.Open();
    }

    private void OnExitClicked()
    {
        if (panel != null) panel.SetActive(false);
        if (roomExit != null) roomExit.OnClickBackButtonPressed(); // 지금의 확인창 → 로비
    }
}
