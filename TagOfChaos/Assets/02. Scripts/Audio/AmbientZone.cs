using UnityEngine;

// 영역 환경음(2026-10-03, Request1003Plan.md §4): 듣는 사람(소리를 듣는 카메라)이 상자 안에 들어오면 2D 반복음을 서서히 키우고
// 배경음을 낮춘다. 나가면 반대로. 진저브레드 지하(나선 통로·터널·유적 홀)의 바람·물방울 소리에 쓴다.
// 상자는 월드 좌표(여러 개 = 합집합). 배치는 에디터 도구(AmbientPlacer)가 한다.
public class AmbientZone : MonoBehaviour
{
    [SerializeField] private SoundId sound = SoundId.None;
    [SerializeField] private Bounds[] boxes = new Bounds[0];
    [SerializeField, Min(0.05f)] private float fadeSeconds = 1.5f;
    [Tooltip("영역 안에서 배경음 배율(0 = 끔)")]
    [SerializeField, Range(0f, 1f)] private float musicGainInside = 0.15f;

    private AudioHandle handle;
    private float level;

    public bool Contains(Vector3 point)
    {
        foreach (Bounds b in boxes) if (b.Contains(point)) return true;
        return false;
    }

    private void Update()
    {
        Camera listener = Camera.main;
        bool inside = listener != null && Contains(listener.transform.position);
        float target = inside ? 1f : 0f;
        if (Mathf.Approximately(level, target) && (target == 0f || handle.IsPlaying)) return;

        level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / fadeSeconds);
        if (level > 0f && !handle.IsPlaying) handle = GameAudio.StartLoop(sound, null);
        GameAudio.SetLoopFade(handle, level);
        GameAudio.SetMusicZoneGain(Mathf.Lerp(1f, musicGainInside, level));
        if (level <= 0f) Stop();
    }

    private void OnDisable()
    {
        level = 0f;
        Stop();
        GameAudio.SetMusicZoneGain(1f);
    }

    private void Stop()
    {
        handle.Stop();
        handle = default;
    }

#if UNITY_EDITOR
    public void EditorSetup(SoundId value, Bounds[] worldBoxes)
    {
        sound = value;
        boxes = worldBoxes;
    }
#endif
}
