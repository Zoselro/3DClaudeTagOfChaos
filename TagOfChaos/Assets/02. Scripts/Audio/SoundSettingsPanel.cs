using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 소리 설정 창(SoundPlan.md S9, 결정 D4): 전체·배경음·효과음·UI 막대 4개. 값은 AudioVolumeSettings가 기기에 바로 저장하고
// 재생 중인 소리에도 바로 반영한다(되돌리기 버튼 없음 — 움직이는 대로 들린다). 환경음은 효과음 막대를 따른다.
// 대기실·맵은 ESC 메뉴의 "소리" 버튼(모든 사람)이, 로비는 화면 위 "소리" 버튼이 연다. 닫기 버튼·ESC로 닫는다.
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
    [Tooltip("ESC 메뉴가 없는 화면(로비)에서는 창이 직접 ESC로 닫힌다")]
    [SerializeField] private bool closeOnEscape;

    private bool syncing;
    private float nextPreview;

    public event Action Closed;

    public bool IsOpen => gameObject.activeSelf;
    public int RowCount => rows.Length;

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
