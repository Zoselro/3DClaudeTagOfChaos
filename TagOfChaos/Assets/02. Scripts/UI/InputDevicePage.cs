using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 창 "입력 장치" 탭(§27 K3): 마이크 드롭다운 + 새로고침 + 소리 크기 막대(이 탭이 열린 동안만 마이크를 듣는다),
// 말하기 방식(누르면서 / 항상) 드롭다운과 말하기 키 안내. 문구는 프리팹에서 입력한다.
public class InputDevicePage : MonoBehaviour
{
    private const int MeterSampleRate = 16000;
    private const int MeterWindow = 512;

    [SerializeField] private TMP_Dropdown micDropdown;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Image levelFill;
    [SerializeField] private TMP_Dropdown talkModeDropdown;
    [SerializeField] private TMP_Text keyHint;
    [SerializeField] private string noMicrophoneLabel = "No microphone";
    [SerializeField] private string[] talkModeLabels = { "Push to talk", "Always on" };
    [SerializeField] private string keyHintFormat = "Talk key: {0}";

    private readonly List<string> names = new List<string>();
    private AudioClip meterClip;
    private string meterDevice;
    private readonly float[] samples = new float[MeterWindow];
    private bool syncing;
    private float shownLevel;

    private void Awake()
    {
        if (micDropdown != null) micDropdown.onValueChanged.AddListener(OnMicPicked);
        if (refreshButton != null) refreshButton.onClick.AddListener(Refresh);
        if (talkModeDropdown != null) talkModeDropdown.onValueChanged.AddListener(OnTalkModePicked);
    }

    private void OnEnable()
    {
        Refresh();
        KeyBindings.Changed += RefreshHint;
    }

    private void OnDisable()
    {
        KeyBindings.Changed -= RefreshHint;
        StopMeter();
    }

    private void Refresh()
    {
        syncing = true;
        names.Clear();
        names.AddRange(InputDevices.Available());
        if (micDropdown != null)
        {
            micDropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>();
            if (names.Count == 0) options.Add(new TMP_Dropdown.OptionData(noMicrophoneLabel));
            foreach (string n in names) options.Add(new TMP_Dropdown.OptionData(n));
            micDropdown.AddOptions(options);
            micDropdown.SetValueWithoutNotify(Mathf.Max(0, names.IndexOf(InputDevices.Selected)));
            micDropdown.RefreshShownValue();
            micDropdown.interactable = names.Count > 0;
        }
        if (talkModeDropdown != null)
        {
            talkModeDropdown.ClearOptions();
            var modes = new List<TMP_Dropdown.OptionData>();
            foreach (string l in talkModeLabels) modes.Add(new TMP_Dropdown.OptionData(l));
            talkModeDropdown.AddOptions(modes);
            talkModeDropdown.SetValueWithoutNotify((int)VoiceChatSettings.Mode);
            talkModeDropdown.RefreshShownValue();
        }
        RefreshHint();
        syncing = false;
        StartMeter();
    }

    private void RefreshHint()
    {
        if (keyHint != null) keyHint.text = string.Format(keyHintFormat, KeyBindings.DisplayName(KeyBindings.Get(KeyAction.PushToTalk)));
    }

    private void OnMicPicked(int index)
    {
        if (syncing || index < 0 || index >= names.Count) return;
        InputDevices.Select(names[index]);
        StartMeter();
    }

    private void OnTalkModePicked(int index)
    {
        if (syncing) return;
        VoiceChatSettings.Mode = index == 1 ? VoiceChatSettings.TalkMode.Always : VoiceChatSettings.TalkMode.PushToTalk;
    }

    // 막대: 음성 채팅이 이미 마이크를 쓰고 있으면 그 값을, 아니면 이 탭이 직접 짧게 반복 녹음해 잰다.
    private void StartMeter()
    {
        StopMeter();
        if (VoiceChat.IsCapturing) return;
        string device = InputDevices.Selected;
        if (device == null) return;
        meterDevice = device;
        meterClip = Microphone.Start(device, true, 1, MeterSampleRate);
    }

    private void StopMeter()
    {
        if (meterDevice != null && Microphone.IsRecording(meterDevice)) Microphone.End(meterDevice);
        meterDevice = null;
        meterClip = null;
    }

    private void Update()
    {
        float level = 0f;
        if (VoiceChat.IsCapturing) level = VoiceChat.InputLevel;
        else if (meterClip != null && meterDevice != null)
        {
            int pos = Microphone.GetPosition(meterDevice) - MeterWindow;
            if (pos >= 0 && meterClip.GetData(samples, pos)) level = Peak(samples);
        }
        shownLevel = Mathf.Max(level, shownLevel - Time.unscaledDeltaTime * 1.5f);
        if (levelFill != null) levelFill.fillAmount = Mathf.Clamp01(shownLevel * 2f);
    }

    public static float Peak(float[] data)
    {
        float max = 0f;
        foreach (float s in data) { float a = Mathf.Abs(s); if (a > max) max = a; }
        return max;
    }
}
