using UnityEngine;

// 제자리에서 계속 나는 환경음(SoundPlan.md S7 — 2026-10-03 공장 기계·회전목마부터 먼저 넣음). 맵 오브젝트에 붙여 두면
// 켜져 있는 동안 3D 반복음을 낸다. 거리·음량은 카탈로그 항목에서 정한다. 재생 칸이 모자라 시작하지 못했으면 잠시 뒤 다시 시도한다.
public class AmbientEmitter : MonoBehaviour
{
    private const float RetrySeconds = 2f;

    [SerializeField] private SoundId sound = SoundId.None;

    private AudioHandle handle;
    private float retryAt;

    public SoundId Sound => sound;

    private void OnEnable() => TryStart();

    private void OnDisable()
    {
        handle.Stop();
        handle = default;
    }

    private void Update()
    {
        if (Time.unscaledTime >= retryAt && !handle.IsPlaying) TryStart();
    }

    private void TryStart()
    {
        retryAt = Time.unscaledTime + RetrySeconds;
        if (sound != SoundId.None) handle = GameAudio.StartLoop(sound, transform);
    }

#if UNITY_EDITOR
    public void EditorSetup(SoundId value) => sound = value;
#endif
}
