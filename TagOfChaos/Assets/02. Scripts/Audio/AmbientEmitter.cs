using UnityEngine;

// 제자리에서 계속 나는 환경음(SoundPlan.md S7 — 2026-10-03 공장 기계·회전목마부터 먼저 넣음). 맵 오브젝트에 붙여 두면
// 듣는 위치가 가까울 때만 3D 반복음을 낸다(DistanceFadePlan.md §2.4): 최대 거리 + StartMargin 안에 들어오면 0.5초에 걸쳐 키우며 시작하고,
// 최대 거리 + StopMargin 밖으로 나가면 멈춰 재생 칸을 돌려준다(경계에서 깜빡이지 않게 여유를 둔다). 거리·음량은 카탈로그 항목에서 정한다.
// 재생 칸이 모자라 시작하지 못했으면 잠시 뒤 다시 시도한다.
public class AmbientEmitter : MonoBehaviour
{
    private const float RetrySeconds = 2f;
    private const float CheckSeconds = 0.25f;
    private const float FadeInSeconds = 0.5f;
    public const float StartMargin = 5f;
    public const float StopMargin = 10f;

    [SerializeField] private SoundId sound = SoundId.None;

    private AudioHandle handle;
    private float nextCheck;
    private float retryAt;
    private float fade;

    public SoundId Sound => sound;
    public bool IsPlaying => handle.IsPlaying;

    // 지금 울려야 하는지(순수 계산). 울리는 중이면 더 멀리까지 유지한다.
    public static bool ShouldPlay(bool playing, float distance, float maxDistance) =>
        distance <= maxDistance + (playing ? StopMargin : StartMargin);

    private void OnDisable() => Stop();

    private void Stop()
    {
        handle.Stop();
        handle = default;
        fade = 0f;
    }

    private void Update()
    {
        if (handle.IsPlaying && fade < 1f)
        {
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / FadeInSeconds);
            GameAudio.SetLoopFade(handle, fade);
        }
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckSeconds;
        if (sound == SoundId.None || !SoundCatalogSO.Current.TryGet(sound, out SoundCatalogSO.Entry entry)) return;

        bool playing = handle.IsPlaying;
        bool want = ShouldPlay(playing, Vector3.Distance(transform.position, AudioListenerAnchor.Position), entry.maxDistance);
        if (want && !playing && Time.unscaledTime >= retryAt) Begin();
        else if (!want && playing) Stop();
    }

    private void Begin()
    {
        retryAt = Time.unscaledTime + RetrySeconds;
        handle = GameAudio.StartLoop(sound, transform);
        fade = 0f;
        GameAudio.SetLoopFade(handle, 0f);
    }

#if UNITY_EDITOR
    public void EditorSetup(SoundId value) => sound = value;
#endif
}
