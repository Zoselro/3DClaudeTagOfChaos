using System.Collections.Generic;

// 재생 칸(목소리) 배정 규칙(SoundPlan.md §2.3·§5). AudioSource와 분리한 순수 계산이라 EditMode에서 시험한다.
// 칸은 미리 정해진 수만큼만 있고(재생마다 생성·파괴 없음), 꽉 차면 "중요도가 같거나 낮은 것 중 가장 오래된 것"을 뺏는다.
// 모두 더 중요하면 새 소리를 버린다. 칸을 뺏거나 놓을 때마다 세대(generation)를 올려, 옛 핸들이 새 소리를 멈추지 못하게 한다.
public class AudioVoicePool
{
    private readonly bool[] busy;
    private readonly float[] startTime;
    private readonly SoundImportance[] importance;
    private readonly SoundId[] sound;
    private readonly int[] generation;

    public AudioVoicePool(int capacity)
    {
        busy = new bool[capacity];
        startTime = new float[capacity];
        importance = new SoundImportance[capacity];
        sound = new SoundId[capacity];
        generation = new int[capacity];
    }

    public int Capacity => busy.Length;

    public bool IsBusy(int voice) => busy[voice];
    public SoundId SoundAt(int voice) => sound[voice];
    public int GenerationOf(int voice) => generation[voice];

    // 칸 하나를 얻는다. 없으면 -1. stolen은 이미 재생 중이던 칸을 뺏었는지(호출한 쪽이 그 소리를 멈춘다).
    public int Acquire(SoundId id, SoundImportance level, float now, out bool stolen)
    {
        stolen = false;
        int victim = -1;
        for (int i = 0; i < busy.Length; i++)
        {
            if (!busy[i]) { victim = i; break; }
            if (importance[i] > level) continue;
            if (victim < 0 || importance[i] < importance[victim]
                || (importance[i] == importance[victim] && startTime[i] < startTime[victim]))
                victim = i;
        }
        if (victim < 0) return -1;

        stolen = busy[victim];
        busy[victim] = true;
        startTime[victim] = now;
        importance[victim] = level;
        sound[victim] = id;
        generation[victim]++;
        return victim;
    }

    public void Release(int voice)
    {
        if (!busy[voice]) return;
        busy[voice] = false;
        sound[voice] = SoundId.None;
        generation[voice]++;
    }

    public int CountPlaying(SoundId id)
    {
        int n = 0;
        for (int i = 0; i < busy.Length; i++) if (busy[i] && sound[i] == id) n++;
        return n;
    }

    public int CountBusy()
    {
        int n = 0;
        for (int i = 0; i < busy.Length; i++) if (busy[i]) n++;
        return n;
    }
}

// 같은 소리의 쿨다운 기록(SoundPlan.md §2.5 — UI 0.05초 등). 순수 계산.
public class SoundCooldowns
{
    private readonly Dictionary<SoundId, float> lastPlayed = new Dictionary<SoundId, float>();

    // 쿨다운이 지났으면 지금 시각을 기록하고 true.
    public bool TryUse(SoundId id, float cooldown, float now)
    {
        if (lastPlayed.TryGetValue(id, out float last) && now - last < cooldown) return false;
        lastPlayed[id] = now;
        return true;
    }

    public void Clear() => lastPlayed.Clear();
}
