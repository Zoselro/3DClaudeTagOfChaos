using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 대기실(GameLobbyScene)의 목소리 녹음(§25.3.1 R2, §26 H11): "목소리 녹음(5초)" — 녹음한 뒤에는 "다시 녹음" — 과 "들어보기" 버튼.
// 판이 시작되기 전까지 몇 번이든 다시 녹음할 수 있고 마지막 녹음을 쓴다. 녹음하면 HasVoice(플레이어 속성)를 켜 목록에 표시되고 녹음기를 쓸 수 있다.
// 마이크는 설정 창에서 고른 장치(InputDevices). 녹음하는 동안 음성 채팅은 보내지 않는다(VoiceChat이 IsRecording을 본다).
// 방장이 판을 시작하면 숨긴다(§26 H3). 씬을 고치지 않도록 대기실에 들어올 때 코드로 만든다. 문구는 EscapeTextsSO.
public class VoiceRecorderPanel : MonoBehaviour
{
    private Text recordLabel, listenLabel;
    private Button listenButton;
    private Canvas canvas;
    private string device;
    private AudioClip recording;
    private float recordUntil = -1f;
    private AudioSource preview;

    private static VoiceRecorderPanel instance;
    public static bool IsRecording => instance != null && instance.recordUntil > 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneNames.GameLobby || FindFirstObjectByType<VoiceRecorderPanel>() != null) return;
        new GameObject(nameof(VoiceRecorderPanel)).AddComponent<VoiceRecorderPanel>();
    }

    private void Awake()
    {
        instance = this;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();

        recordLabel = NewButton("RecordVoice", new Vector2(24f, 24f), new Vector2(300f, 56f), StartRecording).GetComponentInChildren<Text>();
        listenButton = NewButton("ListenVoice", new Vector2(336f, 24f), new Vector2(170f, 56f), Listen);
        listenLabel = listenButton.GetComponentInChildren<Text>();
        preview = gameObject.AddComponent<AudioSource>();
        preview.playOnAwake = false;
        preview.spatialBlend = 0f;
        ShowIdle();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (recordUntil > 0f && device != null) Microphone.End(device);
    }

    private Button NewButton(string name, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var buttonGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)buttonGo.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        buttonGo.GetComponent<Image>().color = new Color(0.15f, 0.1f, 0.2f, 0.85f);
        var button = buttonGo.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var trt = (RectTransform)textGo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var label = textGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        return button;
    }

    private void ShowIdle()
    {
        EscapeTextsSO t = EscapeTextsSO.Current;
        recordLabel.text = VoiceStore.HasLocal ? t.reRecordVoice : t.recordVoice;
        listenLabel.text = t.listenVoice;
        listenButton.interactable = VoiceStore.HasLocal;
    }

    private void StartRecording()
    {
        if (recordUntil > 0f) return;
        device = InputDevices.Selected;
        if (device == null)
        {
            recordLabel.text = EscapeTextsSO.Current.noMicrophone;
            return;
        }
        preview.Stop();
        recordUntil = Time.unscaledTime + VoiceStore.Seconds; // 먼저 켜서 음성 채팅이 마이크를 놓게 한다
        Microphone.End(device);
        recording = Microphone.Start(device, false, VoiceStore.Seconds, VoiceStore.SampleRate);
        recordLabel.text = EscapeTextsSO.Current.recording;
        listenButton.interactable = false;
    }

    private void Listen()
    {
        if (recordUntil > 0f || !VoiceStore.HasLocal || PhotonNetwork.LocalPlayer == null) return;
        AudioClip clip = VoiceStore.ClipFor(PhotonNetwork.LocalPlayer.ActorNumber);
        if (clip == null) return;
        preview.clip = clip;
        preview.Play();
    }

    private void Update()
    {
        // 방장이 판을 시작하면(준비 끝 시각이 기록되면) 버튼을 숨긴다 — 대기실에서만 녹음한다(§26 H3).
        bool started = RoomState.TryGetDouble(NetKeys.PrepareEndTime, out _);
        if (canvas.enabled == started) canvas.enabled = !started;
        if (recordUntil < 0f || Time.unscaledTime < recordUntil) return;
        recordUntil = -1f;
        Microphone.End(device);
        if (recording != null)
        {
            var samples = new float[recording.samples * recording.channels];
            recording.GetData(samples, 0);
            VoiceStore.SetLocal(samples);
            // 녹음이 있는 사람만 녹음기를 쓸 수 있다 — 방장이 이 값을 본다(§26 H7).
            if (PhotonNetwork.LocalPlayer != null)
                PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { NetKeys.HasVoice, true } });
        }
        ShowIdle();
    }

    // 자동 검증용: 녹음 없이 이미 녹음한 것처럼(짧은 소리).
    public static void EditorFakeRecording()
    {
        var samples = new float[VoiceStore.SampleRate];
        for (int i = 0; i < samples.Length; i++) samples[i] = 0.2f * Mathf.Sin(i * 0.15f);
        VoiceStore.SetLocal(samples);
        if (PhotonNetwork.LocalPlayer != null)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { NetKeys.HasVoice, true } });
        if (instance != null) instance.ShowIdle();
    }
}
