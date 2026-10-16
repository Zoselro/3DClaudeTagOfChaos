using UnityEngine;
using UnityEngine.UI;

// LobbyScene의 ESC 메뉴(§27 K6): 계속하기 · 설정 · 게임 종료(확인창 예/아니오). 위쪽 "설정" 버튼도 같은 설정 창을 연다.
// 방에 들어가기 전 화면이라 게임 입력을 막을 필요는 없다. 문구는 씬에서 입력한다.
public class LobbyEscMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject quitConfirm;
    [SerializeField] private Button quitYesButton;
    [SerializeField] private Button quitNoButton;
    [SerializeField] private SettingsMenu settingsMenu;

    private bool returnToMenu; // 메뉴에서 연 설정 창이면 닫을 때 메뉴로 돌아간다

    public bool IsOpen => (panel != null && panel.activeSelf) || (quitConfirm != null && quitConfirm.activeSelf);

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (quitConfirm != null) quitConfirm.SetActive(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettingsFromMenu);
        if (quitButton != null) quitButton.onClick.AddListener(() => { if (panel != null) panel.SetActive(false); if (quitConfirm != null) quitConfirm.SetActive(true); });
        if (quitYesButton != null) quitYesButton.onClick.AddListener(Quit);
        if (quitNoButton != null) quitNoButton.onClick.AddListener(() => { if (quitConfirm != null) quitConfirm.SetActive(false); Open(); });
        if (settingsMenu != null)
        {
            settingsMenu.gameObject.SetActive(false);
            settingsMenu.Closed += OnSettingsClosed;
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || KeyBindingsPage.ConsumesEscape) return;
        if (settingsMenu != null && settingsMenu.IsOpen) { settingsMenu.Close(); UiSoundCues.WindowClosed(); }
        else if (IsOpen) { Close(); UiSoundCues.WindowClosed(); }
        else { Open(); UiSoundCues.WindowOpened(); }
    }

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        if (quitConfirm != null) quitConfirm.SetActive(false);
    }

    // 위쪽 "설정" 버튼 — 닫으면 로비 화면으로.
    public void OpenSettings()
    {
        returnToMenu = false;
        Close();
        if (settingsMenu != null) settingsMenu.Open();
    }

    private void OpenSettingsFromMenu()
    {
        returnToMenu = true;
        if (panel != null) panel.SetActive(false);
        if (settingsMenu != null) settingsMenu.Open();
    }

    private void OnSettingsClosed()
    {
        if (returnToMenu) Open();
        returnToMenu = false;
    }

    private void Quit()
    {
        Debug.Log("[LobbyEscMenu] Quit requested.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
