using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 술래 접근 추격음(DistanceFadePlan.md §9.3). 내 쿠키 화면에서만, 가장 가까운 술래와의 거리로 세기·북 단계를 정한다.
// AudioRuntime이 소유하고 매 프레임 Tick을 부른다(네트워크 없음 — 모두가 이미 아는 위치만 본다).
// 층(바탕 + 북 단계)은 효과음 칸이 아니라 전용 AudioSource로 같은 DSP 시각에 시작해 샘플 단위로 박자를 맞춘다.
// 방향은 알려 주지 않는다(2D) — 방향은 술래 발소리(3D)로 듣는다.
public class ChaseDirector
{
    private const double StartDelaySeconds = 0.05; // 모든 층을 같은 DSP 시각에 시작하기 위한 여유
    private const float StopAfterSilentSeconds = 1f;

    private readonly Func<string, AudioSource> makeSource;
    private readonly MusicPlayer music;
    private readonly List<AudioSource> layers = new List<AudioSource>();   // 0 = 바탕, 1.. = 북 단계
    private readonly List<AudioLowPassFilter> filters = new List<AudioLowPassFilter>();
    private readonly List<float> weights = new List<float>();              // 북 단계 교차 전환(0~1)

    private float level;        // 서서히 바뀌는 세기(0~1)
    private float silentFor;
    private bool playing;

    public ChaseDirector(Func<string, AudioSource> makeSource, MusicPlayer music)
    {
        this.makeSource = makeSource;
        this.music = music;
    }

    public bool Enabled { get; set; } = true;
    public bool IsPlaying => playing;
    public float Intensity => level;   // 지금 세기(서서히 바뀌는 값)
    public float Target { get; private set; }
    public int DrumLevel { get; private set; } = -1;
    public float MonsterDistance { get; private set; } = float.PositiveInfinity;
    public AudioSource LayerSource(int i) => i < layers.Count ? layers[i] : null;

    public void Tick(float deltaTime)
    {
        ChaseAudioSettingsSO s = ChaseAudioSettingsSO.Current;
        Target = 0f;
        float closeness = 0f;
        MonsterDistance = float.PositiveInfinity;
        if (Enabled && RoomState.IsInRoom() && ChaseIntensity.CanListen(Context(out Vector3 me)))
        {
            if (NearestMonster(me, out float distance, out float heightDifference))
            {
                MonsterDistance = distance;
                Target = ChaseIntensity.Evaluate(distance, heightDifference, s);
                closeness = ChaseIntensity.Closeness(distance, s);
                DrumLevel = ChaseIntensity.Level(distance, DrumLevel, s);
            }
        }

        float rate = Target > level ? 1f / s.FadeInSeconds : 1f / s.FadeOutSeconds;
        level = Mathf.MoveTowards(level, Target, deltaTime * rate);
        music.ChaseGain = Mathf.Lerp(1f, s.MusicDuckAtFull, level);

        if (level > 0f)
        {
            silentFor = 0f;
            if (!playing) Start(s);
        }
        else if (playing && (silentFor += deltaTime) >= StopAfterSilentSeconds) Stop();

        if (playing) Mix(s, deltaTime, closeness);
    }

    private static ChaseContext Context(out Vector3 me)
    {
        me = AudioListenerAnchor.Position;
        bool hasCookie = false;
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c) || c.Role != CharacterRole.Cookie || c.View == null || !c.View.IsMine) continue;
            hasCookie = true;
            break;
        }
        var local = PhotonNetwork.LocalPlayer;
        return new ChaseContext
        {
            Phase = GamePhaseState.Current,
            LocalIsMonster = RoomState.IsLocalMonster(),
            LocalHasCookie = hasCookie,
            LocalBroken = local != null && RoomState.IsBroken(local),
            LocalLeftMap = local != null && (RoomState.HasEscaped(local) || EscapeManager.HasLeftMap(local.ActorNumber) || EscapeManager.IsWaiting(local.ActorNumber)),
        };
    }

    private static bool NearestMonster(Vector3 me, out float distance, out float heightDifference)
    {
        distance = float.PositiveInfinity;
        heightDifference = 0f;
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c) || c.Role != CharacterRole.Monster) continue;
            Vector3 p = c.gameObject.transform.position;
            float d = new Vector2(p.x - me.x, p.z - me.z).magnitude;
            if (d >= distance) continue;
            distance = d;
            heightDifference = p.y - (me.y - Camera_Ctrl.CookieTargetHeight); // 듣는 위치는 쿠키 머리, 술래 위치는 발 — 발끼리 비교
        }
        return !float.IsPositiveInfinity(distance);
    }

    private void Start(ChaseAudioSettingsSO s)
    {
        EnsureLayers(s.LevelCount + 1);
        double at = AudioSettings.dspTime + StartDelaySeconds;
        for (int i = 0; i < layers.Count; i++)
        {
            AudioClip clip = i == 0 ? s.BaseLayer : (i - 1 < s.LevelCount ? s.LevelClip(i - 1) : null);
            AudioSource src = layers[i];
            src.Stop();
            src.clip = clip;
            src.volume = 0f;
            weights[i] = 0f;
            filters[i].enabled = clip != null;
            if (clip != null) src.PlayScheduled(at);
        }
        playing = true;
    }

    private void Stop()
    {
        for (int i = 0; i < layers.Count; i++)
        {
            layers[i].Stop();
            filters[i].enabled = false;
            layers[i].clip = null;
        }
        playing = false;
        DrumLevel = -1;
        music.ChaseGain = 1f;
    }

    private void Mix(ChaseAudioSettingsSO s, float deltaTime, float closeness)
    {
        float master = level * s.MaxVolume * AudioVolumeSettings.Gain(SoundBus.Sfx);
        float cutoff = s.MuffleWhenFar ? SpatialGain.LogLerp(s.FarCutoffHz, SpatialGain.OpenCutoffHz, closeness) : SpatialGain.OpenCutoffHz;
        float step = deltaTime / s.LevelCrossfadeSeconds;
        for (int i = 0; i < layers.Count; i++)
        {
            float want = i == 0 ? 1f : (i - 1 == DrumLevel ? 1f : 0f);
            weights[i] = Mathf.MoveTowards(weights[i], want, step);
            layers[i].volume = master * weights[i];
            filters[i].cutoffFrequency = cutoff;
        }
    }

    private void EnsureLayers(int count)
    {
        while (layers.Count < count)
        {
            AudioSource src = makeSource("Chase" + layers.Count);
            src.loop = true;
            src.spatialBlend = 0f;
            src.priority = 0; // 다른 소리에 밀려 끊기지 않게(박자가 어긋남)
            layers.Add(src);
            filters.Add(src.GetComponent<AudioLowPassFilter>()); // makeSource가 꺼 둔 채 붙여 준다(재생하는 동안만 켠다)
            weights.Add(0f);
        }
    }
}
