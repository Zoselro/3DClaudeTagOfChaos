using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 대기실(GameLobbyScene)의 "목소리 녹음(5초)" 버튼(§25.3.1 R2). 씬을 고치지 않도록 대기실에 들어올 때 코드로 만든다. 문구는 EscapeTextsSO.
public class VoiceRecorderPanel : MonoBehaviour
{
    private Text label;
    private string device;
    private AudioClip recording;
    private float recordUntil = -1f;

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
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();

        var buttonGo = new GameObject("RecordVoice", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)buttonGo.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(24f, 24f);
        rt.sizeDelta = new Vector2(300f, 56f);
        buttonGo.GetComponent<Image>().color = new Color(0.15f, 0.1f, 0.2f, 0.85f);
        buttonGo.GetComponent<Button>().onClick.AddListener(StartRecording);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var trt = (RectTransform)textGo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        label = textGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        ShowIdle();
    }

    private void ShowIdle()
    {
        EscapeTextsSO t = EscapeTextsSO.Current;
        label.text = VoiceStore.HasLocal ? t.recorded : t.recordVoice;
    }

    private void StartRecording()
    {
        if (recordUntil > 0f) return;
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            label.text = EscapeTextsSO.Current.noMicrophone;
            return;
        }
        device = Microphone.devices[0];
        recording = Microphone.Start(device, false, VoiceStore.Seconds, VoiceStore.SampleRate);
        recordUntil = Time.unscaledTime + VoiceStore.Seconds;
        label.text = EscapeTextsSO.Current.recording;
    }

    private void Update()
    {
        if (recordUntil < 0f || Time.unscaledTime < recordUntil) return;
        recordUntil = -1f;
        Microphone.End(device);
        if (recording != null)
        {
            var samples = new float[recording.samples * recording.channels];
            recording.GetData(samples, 0);
            VoiceStore.SetLocal(samples);
        }
        ShowIdle();
    }
}
