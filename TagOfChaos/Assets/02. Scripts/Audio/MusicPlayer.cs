using UnityEngine;

// 배경음 재생기(SoundPlan.md §2.3). AudioRuntime이 소유한다.
// - 두 채널을 번갈아 써서 곡을 교차 전환한다(새 곡은 0 → 1, 이전 곡은 1 → 0).
// - 스팅어·징글은 별도 채널에서 한 번 재생하고, 그동안 배경음을 DuckDb만큼 낮춘다(§2.5, 0.3초에 걸쳐).
public class MusicPlayer
{
    public const float DuckDb = -6f;
    private const float DuckFadeSeconds = 0.3f;

    private readonly AudioSource[] decks;
    private readonly float[] level = new float[2];      // 교차 전환 진행(0~1)
    private readonly float[] target = new float[2];
    private readonly float[] entryVolume = new float[2];
    private readonly AudioSource stinger;
    private float stingerVolume;
    private int active;
    private float fadeSeconds = 2f;
    private float duck = 1f;

    // 장소에 따른 배경음 배율(0~1) — 진저브레드 지하에 들어가면 동굴 소리가 들리게 낮춘다(AmbientZone).
    public float ZoneGain { get; set; } = 1f;

    public MusicId Current { get; private set; }

    public MusicPlayer(AudioSource deckA, AudioSource deckB, AudioSource stingerSource)
    {
        decks = new[] { deckA, deckB };
        stinger = stingerSource;
    }

    public bool IsStingerPlaying => stinger.isPlaying;

    // 같은 곡이면 아무것도 하지 않는다(단계가 같으면 매번 불려도 끊기지 않게). 클립이 없는 곡이면 지금 곡만 줄인다.
    public void Play(MusicId id, float fade)
    {
        if (id == Current) return;
        Current = id;
        fadeSeconds = Mathf.Max(0.01f, fade);
        target[active] = 0f;
        if (id == MusicId.None || !MusicCatalogSO.Current.TryGet(id, out MusicCatalogSO.Entry entry) || entry.clip == null) return;

        int next = 1 - active;
        AudioSource deck = decks[next];
        deck.Stop();
        deck.clip = entry.clip;
        deck.loop = entry.loop;
        deck.volume = 0f;
        deck.Play();
        level[next] = 0f;
        target[next] = 1f;
        entryVolume[next] = entry.volume;
        active = next;
    }

    public void Stop(float fade) => Play(MusicId.None, fade);

    public void Stinger(MusicId id)
    {
        if (!MusicCatalogSO.Current.TryGet(id, out MusicCatalogSO.Entry entry) || entry.clip == null) return;
        stinger.Stop();
        stinger.clip = entry.clip;
        stinger.loop = false;
        stingerVolume = entry.volume;
        stinger.Play();
    }

    // 매 프레임: 교차 전환·낮춤 진행과 음량 반영. gain = 배경음 묶음 이득(AudioVolumeSettings).
    public void Tick(float deltaTime, float gain)
    {
        float duckTarget = stinger.isPlaying ? AudioVolumeSettings.FromDecibels(DuckDb) : 1f;
        duck = Mathf.MoveTowards(duck, duckTarget, deltaTime / DuckFadeSeconds);

        for (int i = 0; i < decks.Length; i++)
        {
            level[i] = Mathf.MoveTowards(level[i], target[i], deltaTime / fadeSeconds);
            AudioSource deck = decks[i];
            if (level[i] <= 0f && target[i] <= 0f)
            {
                if (deck.isPlaying) deck.Stop();
                continue;
            }
            deck.volume = level[i] * entryVolume[i] * gain * duck * ZoneGain;
        }
        stinger.volume = stingerVolume * gain;
    }
}
