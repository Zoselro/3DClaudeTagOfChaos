using UnityEngine;

// 소리 재생의 실제 몸통(SoundPlan.md §2.3). 앱이 시작될 때 하나 만들어 씬이 바뀌어도 유지한다(DontDestroyOnLoad).
// AudioSource는 여기서 미리 만든 칸만 쓴다 — 3D 24칸 + 2D 8칸 + 배경음 3칸(교차 전환 2 + 스팅어 1). 재생마다 생성·파괴하지 않는다.
// 게임 코드는 이 클래스를 직접 쓰지 않고 GameAudio를 통한다.
public class AudioRuntime : MonoBehaviour
{
    public const int WorldVoiceCount = 24;
    public const int FlatVoiceCount = 8;

    // 3D 감쇠(§2.5): 가까이서는 또렷하고 멀어지면 빨리 작아져 최대 거리에서 0. x = 거리 / 최대 거리.
    private static readonly AnimationCurve Rolloff = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.1f, 0.62f), new Keyframe(0.25f, 0.33f), new Keyframe(0.5f, 0.12f), new Keyframe(1f, 0f));

    private class Voice
    {
        public AudioSource source;
        public Transform follow;
        public bool following;
        public SoundBus bus;
        public float baseVolume;
    }

    private static AudioRuntime instance;

    private Voice[] worldVoices;
    private Voice[] flatVoices;
    private AudioVoicePool worldPool;
    private AudioVoicePool flatPool;
    private readonly SoundCooldowns cooldowns = new SoundCooldowns();
    private MusicPlayer music;
    private MusicDirector director;

    public static AudioRuntime Instance
    {
        get
        {
            if (instance == null && Application.isPlaying && !quitting) Create();
            return instance;
        }
    }

    public MusicPlayer Music => music;
    public MusicDirector Director => director;
    public int BusyWorldVoices => worldPool.CountBusy();
    public int BusyFlatVoices => flatPool.CountBusy();

    private static bool quitting; // 종료 중에 다른 오브젝트의 OnDestroy가 소리를 내도 다시 만들지 않는다

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        quitting = false;
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
        Create();
    }

    private static void OnQuitting() => quitting = true;

    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("AudioRuntime");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AudioRuntime>();
        instance.Build();
    }

    private void Build()
    {
        worldVoices = MakeVoices("World", WorldVoiceCount, true);
        flatVoices = MakeVoices("Flat", FlatVoiceCount, false);
        worldPool = new AudioVoicePool(WorldVoiceCount);
        flatPool = new AudioVoicePool(FlatVoiceCount);
        music = new MusicPlayer(MakeSource("MusicA", false), MakeSource("MusicB", false), MakeSource("Stinger", false));
        director = new MusicDirector(music);
        AudioVolumeSettings.Changed += ApplyVolumes;
    }

    private void OnDestroy()
    {
        AudioVolumeSettings.Changed -= ApplyVolumes;
        if (instance == this) instance = null;
    }

    private Voice[] MakeVoices(string prefix, int count, bool world)
    {
        var voices = new Voice[count];
        for (int i = 0; i < count; i++) voices[i] = new Voice { source = MakeSource(prefix + i, world) };
        return voices;
    }

    private AudioSource MakeSource(string name, bool world)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.dopplerLevel = 0f;
        src.spread = 0f;
        src.spatialBlend = world ? 1f : 0f;
        if (world)
        {
            src.rolloffMode = AudioRolloffMode.Custom;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
        }
        return src;
    }

    // ---------------- 효과음 ----------------

    // world=false면 2D(위치 무시). follow가 있으면 그 Transform을 따라간다. loop는 핸들로 멈출 때까지 반복.
    public AudioHandle Play(SoundId id, bool world, Vector3 position, Transform follow, bool loop)
    {
        if (!SoundCatalogSO.Current.TryGet(id, out SoundCatalogSO.Entry entry) || !entry.HasClip) return default;
        world &= entry.space == SoundSpace.World3D;
        float now = Time.unscaledTime;
        AudioVoicePool pool = world ? worldPool : flatPool;
        if (pool.CountPlaying(id) >= entry.maxInstances) return default;
        if (!loop && !cooldowns.TryUse(id, entry.cooldown, now)) return default;

        int index = pool.Acquire(id, entry.importance, now, out _);
        if (index < 0) return default;

        Voice voice = (world ? worldVoices : flatVoices)[index];
        AudioSource src = voice.source;
        src.Stop();
        src.clip = entry.PickClip();
        src.pitch = entry.PickPitch();
        src.loop = loop;
        voice.bus = entry.bus;
        voice.baseVolume = entry.volume;
        voice.follow = follow;
        voice.following = follow != null;
        src.volume = voice.baseVolume * AudioVolumeSettings.Gain(voice.bus);
        if (world)
        {
            src.maxDistance = entry.maxDistance;
            src.transform.position = follow != null ? follow.position : position;
        }
        src.Play();
        return new AudioHandle(world, index, pool.GenerationOf(index));
    }

    public void Stop(AudioHandle handle)
    {
        if (!IsCurrent(handle)) return;
        Voice voice = (handle.World ? worldVoices : flatVoices)[handle.Voice];
        voice.source.Stop();
        Release(handle.World, handle.Voice);
    }

    public bool IsPlaying(AudioHandle handle) => IsCurrent(handle);

    private bool IsCurrent(AudioHandle handle)
    {
        if (!handle.IsValid) return false;
        AudioVoicePool pool = handle.World ? worldPool : flatPool;
        return pool.IsBusy(handle.Voice) && pool.GenerationOf(handle.Voice) == handle.Generation;
    }

    private void Release(bool world, int index)
    {
        Voice voice = (world ? worldVoices : flatVoices)[index];
        voice.follow = null;
        voice.following = false;
        voice.source.clip = null;
        (world ? worldPool : flatPool).Release(index);
    }

    private void LateUpdate()
    {
        Sweep(worldVoices, worldPool, true);
        Sweep(flatVoices, flatPool, false);
        director.Tick(Time.unscaledTime);
        music.Tick(Time.unscaledDeltaTime, AudioVolumeSettings.Gain(SoundBus.Music));
    }

    // 끝난 칸을 돌려받고, 따라가는 소리는 위치를 맞춘다. 따라가던 대상이 사라지면 반복음은 멈추고 한 번짜리는 그 자리에서 끝까지 난다.
    private void Sweep(Voice[] voices, AudioVoicePool pool, bool world)
    {
        for (int i = 0; i < voices.Length; i++)
        {
            if (!pool.IsBusy(i)) continue;
            Voice voice = voices[i];
            if (!voice.source.isPlaying)
            {
                Release(world, i);
                continue;
            }
            if (!voice.following) continue;
            if (voice.follow == null)
            {
                voice.following = false;
                if (voice.source.loop) { voice.source.Stop(); Release(world, i); }
                continue;
            }
            if (world) voice.source.transform.position = voice.follow.position;
        }
    }

    private void ApplyVolumes()
    {
        ApplyVolumes(worldVoices, worldPool);
        ApplyVolumes(flatVoices, flatPool);
    }

    private static void ApplyVolumes(Voice[] voices, AudioVoicePool pool)
    {
        for (int i = 0; i < voices.Length; i++)
            if (pool.IsBusy(i)) voices[i].source.volume = voices[i].baseVolume * AudioVolumeSettings.Gain(voices[i].bus);
    }
}

// 재생한 소리를 가리키는 표. 칸이 다른 소리에 넘어가면(세대가 바뀌면) 더 이상 그 칸을 건드리지 않는다.
public readonly struct AudioHandle
{
    public readonly bool World;
    public readonly int Voice;
    public readonly int Generation; // 0이면 빈 핸들(세대는 1부터)

    public AudioHandle(bool world, int voice, int generation)
    {
        World = world;
        Voice = voice;
        Generation = generation;
    }

    public bool IsValid => Generation > 0;
    public bool IsPlaying => IsValid && AudioRuntime.Instance != null && AudioRuntime.Instance.IsPlaying(this);
    public void Stop()
    {
        if (IsValid && AudioRuntime.Instance != null) AudioRuntime.Instance.Stop(this);
    }
}
