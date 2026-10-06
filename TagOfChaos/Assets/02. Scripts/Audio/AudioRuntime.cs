using UnityEngine;

// 소리 재생의 실제 몸통(SoundPlan.md §2.3). 앱이 시작될 때 하나 만들어 씬이 바뀌어도 유지한다(DontDestroyOnLoad).
// AudioSource는 여기서 미리 만든 칸만 쓴다 — 3D 24칸 + 2D 8칸 + 배경음 3칸(교차 전환 2 + 스팅어 1) + 추격음 층(ChaseDirector). 재생마다 생성·파괴하지 않는다.
// 소리를 듣는 AudioListener도 여기 하나만 둔다(AudioListenerAnchor — 캐릭터 머리 위치).
// 게임 코드는 이 클래스를 직접 쓰지 않고 GameAudio를 통한다.
public class AudioRuntime : MonoBehaviour
{
    public const int WorldVoiceCount = 24;
    public const int FlatVoiceCount = 8;

    // 거리 감쇠는 SpatialGain이 음량에 직접 곱한다(DistanceFadePlan.md §2.2 — 소리 종류별 곡선·최소 거리·층 감쇠). 유니티 감쇠는 평평하게 두고
    // 좌우 위치감(팬)만 유니티에 맡긴다.
    private static readonly AnimationCurve FlatRolloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));
    private const float UnityMaxDistance = 500f;

    private class Voice
    {
        public AudioSource source;
        public Transform follow;
        public bool following;
        public SoundBus bus;
        public float baseVolume;
        public float fade = 1f; // 반복음을 서서히 키우고 줄일 때(AmbientZone)
        public float spatial = 1f; // 거리 감쇠(SpatialGain, 2D는 1)
        public SoundFalloff falloff;
        public float minDistance;
        public float maxDistance;
        public AudioLowPassFilter filter; // 먼 소리 먹먹하게(L3) — 3D 칸, 2D 칸은 환경음 실내 먹먹함(IndoorZone)
        public float wall; // 벽 너머 정도(0~1, IndoorZone — 3D는 소리와 듣는 사람 사이, 2D 환경음은 듣는 사람이 실내)
    }

    private static AudioRuntime instance;

    private Voice[] worldVoices;
    private Voice[] flatVoices;
    private AudioVoicePool worldPool;
    private AudioVoicePool flatPool;
    private readonly SoundCooldowns cooldowns = new SoundCooldowns();
    private MusicPlayer music;
    private MusicDirector director;
    private ChaseDirector chase;
    private AudioListenerAnchor listener;

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
    public ChaseDirector Chase => chase;
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
        chase = new ChaseDirector(name => MakeSource(name, false, lowPass: true), music);
        listener = AudioListenerAnchor.Create(transform);
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
        for (int i = 0; i < count; i++)
        {
            voices[i] = new Voice { source = MakeSource(prefix + i, world, lowPass: true) };
            voices[i].filter = voices[i].source.GetComponent<AudioLowPassFilter>();
        }
        return voices;
    }

    // 꺼진 오브젝트에서 설정을 다 마친 뒤 켠다 — 켜진 채 AudioSource(기본 playOnAwake)에 필터를 붙이면 유니티가 "필터만 재생할 수 있다" 오류를 낸다.
    private AudioSource MakeSource(string name, bool world, bool lowPass = false)
    {
        var child = new GameObject(name);
        child.SetActive(false);
        child.transform.SetParent(transform, false);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.dopplerLevel = 0f;
        src.spread = 0f;
        src.spatialBlend = world ? 1f : 0f;
        if (world)
        {
            src.rolloffMode = AudioRolloffMode.Custom;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FlatRolloff);
            src.maxDistance = UnityMaxDistance;
        }
        if (lowPass) child.AddComponent<AudioLowPassFilter>().enabled = false; // 재생하는 동안만 켠다
        child.SetActive(true);
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
        Vector3 at = follow != null ? follow.position : position;
        float heard = world ? SpatialGain.Evaluate(entry.falloff, at, AudioListenerAnchor.Position, entry.minDistance, entry.maxDistance) : 1f;
        if (world && !loop && heard < SpatialGain.AudibleThreshold) return default; // 들리지 않는 한 번짜리 소리는 칸을 쓰지 않는다(§2.4)
        if (!loop && !cooldowns.TryUse(id, entry.cooldown, now)) return default;

        int index = pool.Acquire(id, entry.importance, now, heard, out _);
        if (index < 0) return default;

        Voice voice = (world ? worldVoices : flatVoices)[index];
        AudioSource src = voice.source;
        if (src.isPlaying) src.Stop();
        src.clip = entry.PickClip();
        src.pitch = entry.PickPitch();
        src.loop = loop;
        voice.bus = entry.bus;
        voice.baseVolume = entry.volume;
        voice.fade = 1f;
        voice.spatial = heard;
        voice.falloff = entry.falloff;
        voice.minDistance = entry.minDistance;
        voice.maxDistance = entry.maxDistance;
        voice.follow = follow;
        voice.following = follow != null;
        if (world)
        {
            src.transform.position = at;
            voice.wall = IndoorZone.Separated(at, AudioListenerAnchor.Position) ? 1f : 0f;
            voice.filter.cutoffFrequency = WorldCutoff(voice, at, AudioListenerAnchor.Position);
            voice.filter.enabled = true;
        }
        else if (voice.bus == SoundBus.Ambience)
        {
            voice.wall = IndoorZone.ListenerLevel;
            voice.filter.cutoffFrequency = SpatialGain.LogLerp(SpatialGain.OpenCutoffHz, IndoorZone.AmbienceCutoffHz, voice.wall);
            voice.filter.enabled = true;
        }
        else voice.wall = 0f;
        src.volume = VolumeOf(voice);
        src.Play();
        // 반복음은 아무 지점에서 시작한다 — 같은 소리를 내는 여러 곳(공장 기계 등)이 똑같이 겹쳐 울리지 않게
        if (loop && src.clip != null && src.clip.length > 0.1f) src.time = Random.Range(0f, src.clip.length * 0.95f);
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

    // 재생 중인 소리의 음량 배율(0~1). 카탈로그 음량·묶음 음량에 곱한다.
    public void SetFade(AudioHandle handle, float fade)
    {
        if (!IsCurrent(handle)) return;
        Voice voice = (handle.World ? worldVoices : flatVoices)[handle.Voice];
        voice.fade = Mathf.Clamp01(fade);
        voice.source.volume = VolumeOf(voice);
    }

    // 재생 중인 3D 소리의 지금 거리 감쇠(0~1). 시험·기록용.
    public float SpatialOf(AudioHandle handle) => IsCurrent(handle) ? (handle.World ? worldVoices : flatVoices)[handle.Voice].spatial : 0f;

    // 재생 중인 소리의 지금 벽 너머 정도(0~1, IndoorZone)와 저역 통과 차단 주파수. 시험·기록용.
    public float WallOf(AudioHandle handle) => IsCurrent(handle) ? (handle.World ? worldVoices : flatVoices)[handle.Voice].wall : 0f;
    public float CutoffOf(AudioHandle handle) => IsCurrent(handle) ? (handle.World ? worldVoices : flatVoices)[handle.Voice].filter.cutoffFrequency : 0f;

    private static float VolumeOf(Voice voice) =>
        voice.baseVolume * voice.fade * voice.spatial * AudioVolumeSettings.Gain(voice.bus) * Mathf.Lerp(1f, WallGainOf(voice), voice.wall);

    private static float WallGainOf(Voice voice) => voice.source.spatialBlend > 0f ? IndoorZone.WallGain : IndoorZone.AmbienceGain;

    private static float WorldCutoff(Voice voice, Vector3 at, Vector3 ear) =>
        Mathf.Min(SpatialGain.Cutoff(at, ear, voice.minDistance, voice.maxDistance), SpatialGain.LogLerp(SpatialGain.OpenCutoffHz, IndoorZone.WallCutoffHz, voice.wall));

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
        if (voice.filter != null) voice.filter.enabled = false;
        voice.source.clip = null;
        (world ? worldPool : flatPool).Release(index);
    }

    private void LateUpdate()
    {
        listener.Tick();
        IndoorZone.Tick(AudioListenerAnchor.Position, Time.unscaledDeltaTime);
        Sweep(worldVoices, worldPool, true);
        Sweep(flatVoices, flatPool, false);
        director.Tick(Time.unscaledTime);
        chase.Tick(Time.unscaledDeltaTime);
        music.Tick(Time.unscaledDeltaTime, AudioVolumeSettings.Gain(SoundBus.Music));
    }

    // 끝난 칸을 돌려받고, 따라가는 소리는 위치를 맞춘다. 따라가던 대상이 사라지면 반복음은 멈추고 한 번짜리는 그 자리에서 끝까지 난다.
    // 3D 칸은 매 프레임 듣는 위치와의 거리로 음량·먹먹함을 다시 정한다(듣는 사람이나 소리가 움직여도 맞게).
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
            if (voice.following)
            {
                if (voice.follow == null)
                {
                    voice.following = false;
                    if (voice.source.loop) { voice.source.Stop(); Release(world, i); continue; }
                }
                else if (world) voice.source.transform.position = voice.follow.position;
            }
            if (world) UpdateSpatial(voice, pool, i);
            else if (voice.bus == SoundBus.Ambience) UpdateIndoorAmbience(voice);
        }
    }

    // 듣는 사람이 실내에 들어가면 바탕 환경음이 서서히 먹먹하고 작아진다(IndoorZone).
    private static void UpdateIndoorAmbience(Voice voice)
    {
        if (Mathf.Approximately(voice.wall, IndoorZone.ListenerLevel)) return;
        voice.wall = IndoorZone.ListenerLevel;
        voice.filter.cutoffFrequency = SpatialGain.LogLerp(SpatialGain.OpenCutoffHz, IndoorZone.AmbienceCutoffHz, voice.wall);
        voice.source.volume = VolumeOf(voice);
    }

    private static void UpdateSpatial(Voice voice, AudioVoicePool pool, int index)
    {
        Vector3 at = voice.source.transform.position;
        Vector3 ear = AudioListenerAnchor.Position;
        voice.spatial = SpatialGain.Evaluate(voice.falloff, at, ear, voice.minDistance, voice.maxDistance);
        voice.wall = IndoorZone.Step(voice.wall, IndoorZone.Separated(at, ear), Time.unscaledDeltaTime);
        voice.source.volume = VolumeOf(voice);
        voice.filter.cutoffFrequency = WorldCutoff(voice, at, ear);
        pool.SetAudibility(index, voice.spatial);
    }

    private void ApplyVolumes()
    {
        ApplyVolumes(worldVoices, worldPool);
        ApplyVolumes(flatVoices, flatPool);
    }

    private static void ApplyVolumes(Voice[] voices, AudioVoicePool pool)
    {
        for (int i = 0; i < voices.Length; i++)
            if (pool.IsBusy(i)) voices[i].source.volume = VolumeOf(voices[i]);
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
