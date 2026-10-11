using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 소리 설정 창(SoundPlan.md S9, 결정 D4): 전체·배경음·효과음·UI 막대 4개. 값은 AudioVolumeSettings가 기기에 바로 저장하고
// 재생 중인 소리에도 바로 반영한다(되돌리기 버튼 없음 — 움직이는 대로 들린다). 환경음은 효과음 막대를 따른다.
// 대기실·맵은 ESC 메뉴의 "소리" 버튼(모든 사람)이, 로비는 화면 위 "소리" 버튼이 연다. 닫기 버튼·ESC로 닫는다.
// 아래 줄: 출력 장치 드롭다운(Request1009Plan.md §17) — 이 PC의 실제 출력 장치(첫 줄 = 시스템 기본)를 고르면 이 게임만 그 장치로 소리를 낸다.
// 새로고침 버튼은 장치를 꽂거나 뺀 뒤 목록을 다시 읽는다(꽂고 빼면 저절로도 다시 읽는다). Windows가 아니면 시스템 기본만 보이고 잠긴다.
// 프리팹은 에디터 도구(SoundSettingsInstaller)가 만들고, 문구는 프리팹에서 입력한다(코드에 한글 금지).
public class SoundSettingsPanel : MonoBehaviour
{
    [Serializable]
    private struct Row
    {
        public AudioVolumeSettings.Slider kind;
        public Slider slider;
        public TMP_Text value;
    }

    private const float PreviewInterval = 0.15f;

    [SerializeField] private Row[] rows = new Row[0];
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Dropdown outputDropdown;
    [SerializeField] private Button outputRefreshButton;
    [Tooltip("드롭다운 첫 줄(시스템 기본 장치) 문구 — 프리팹에서 입력")]
    [SerializeField] private string systemDefaultLabel = "System default";
    [Tooltip("ESC 메뉴가 없는 화면(로비)에서는 창이 직접 ESC로 닫힌다")]
    [SerializeField] private bool closeOnEscape;

    private bool syncing;
    private float nextPreview;

    public event Action Closed;

    public bool IsOpen => gameObject.activeSelf;
    public int RowCount => rows.Length;
    public bool HasOutputDropdown => outputDropdown != null;
    public int OutputOptionCount => outputDropdown != null ? outputDropdown.options.Count : 0;

    // 드롭다운 줄 번호 → 장치 ID(0 = 시스템 기본 = 빈 값).
    private readonly List<string> outputIds = new List<string>();

    private void Awake()
    {
        foreach (Row row in rows)
        {
            if (row.slider == null) continue;
            AudioVolumeSettings.Slider kind = row.kind;
            row.slider.minValue = 0f;
            row.slider.maxValue = 1f;
            row.slider.wholeNumbers = false;
            row.slider.onValueChanged.AddListener(v => OnChanged(kind, v));
        }
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (outputDropdown != null) outputDropdown.onValueChanged.AddListener(OnOutputPicked);
        if (outputRefreshButton != null) outputRefreshButton.onClick.AddListener(SyncOutput);
        OutputDevices.Changed += OnDevicesChanged;
    }

    private void OnDestroy() => OutputDevices.Changed -= OnDevicesChanged;

    private void OnDevicesChanged()
    {
        if (isActiveAndEnabled) SyncOutput();
    }

    // 장치 목록을 다시 읽어 드롭다운을 채우고 지금 고른 장치를 표시한다. 고른 장치가 지금 없으면 시스템 기본을 표시한다.
    private void SyncOutput()
    {
        if (outputDropdown == null) return;
        bool wasSyncing = syncing;
        syncing = true;
        outputIds.Clear();
        var options = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData(systemDefaultLabel) };
        outputIds.Add(string.Empty);
        List<WindowsAudioEndpoints.Device> devices = OutputDevices.Available();
        foreach (WindowsAudioEndpoints.Device d in devices)
        {
            options.Add(new TMP_Dropdown.OptionData(d.Name));
            outputIds.Add(d.Id);
        }
        outputDropdown.ClearOptions();
        outputDropdown.AddOptions(options);
        int index = outputIds.IndexOf(OutputDevices.Resolve(OutputDevices.SelectedId, devices));
        outputDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
        outputDropdown.RefreshShownValue();
        outputDropdown.interactable = OutputDevices.IsSupported;
        if (outputRefreshButton != null) outputRefreshButton.interactable = OutputDevices.IsSupported;
        syncing = wasSyncing;
    }

    private void OnOutputPicked(int index)
    {
        if (syncing || index < 0 || index >= outputIds.Count) return;
        if (OutputDevices.Select(outputIds[index])) GameAudio.Play(SoundId.ItemPickup); // 새 장치에서 짧은 확인음
        else SyncOutput(); // 실패하면 원래 선택으로 되돌려 보인다
    }

    // 시험·자동 검증용: 드롭다운에서 그 장치를 고른 것처럼.
    public bool PickOutput(string deviceId)
    {
        int index = outputIds.IndexOf(deviceId ?? string.Empty);
        if (index < 0) return false;
        outputDropdown.value = index;
        return true;
    }

    private void OnEnable() => Sync();

    private void Update()
    {
        if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape)) { Close(); UiSoundCues.WindowClosed(); }
    }

    public void Open()
    {
        gameObject.SetActive(true);
        UiSoundCues.WindowOpened();
        Sync();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    // 다른 창이 함께 닫을 때(ESC 메뉴 전체 닫기) — 메뉴로 돌아가지 않는다.
    public void Hide() => gameObject.SetActive(false);

    private void Sync()
    {
        syncing = true;
        foreach (Row row in rows)
        {
            float v = AudioVolumeSettings.Get(row.kind);
            if (row.slider != null) row.slider.SetValueWithoutNotify(v);
            if (row.value != null) row.value.text = Percent(v);
        }
        SyncOutput();
        syncing = false;
    }

    private void OnChanged(AudioVolumeSettings.Slider kind, float value)
    {
        if (syncing) return;
        AudioVolumeSettings.Set(kind, value);
        foreach (Row row in rows)
            if (row.kind == kind && row.value != null) row.value.text = Percent(value);
        Preview(kind);
    }

    // 막대를 움직이면 그 묶음 소리를 짧게 들려준다(배경음은 이미 들리므로 없음).
    private void Preview(AudioVolumeSettings.Slider kind)
    {
        if (kind == AudioVolumeSettings.Slider.Music || Time.unscaledTime < nextPreview) return;
        nextPreview = Time.unscaledTime + PreviewInterval;
        GameAudio.Play(PreviewSound(kind));
    }

    public static SoundId PreviewSound(AudioVolumeSettings.Slider kind) =>
        kind == AudioVolumeSettings.Slider.Sfx ? SoundId.ItemPickup : SoundId.UiTick;

    public static string Percent(float value) => Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
}
