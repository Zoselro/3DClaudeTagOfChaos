using System;
using UnityEngine;

// 소리 묶음(SoundPlan.md §2.5). 믹서 에셋 대신 코드로 음량을 곱한다 — Unity는 AudioMixer를 코드로 만드는 공개 API가 없어
// 에디터 도구로 다시 만들 수 없고, 지금 필요한 것은 묶음별 음량과 배경음 낮춤뿐이라 코드가 더 단순하다.
public enum SoundBus { Music, Sfx, Ui, Ambience }

// 소리 출력 장치(Request1009Plan.md §16.6). 게임이 실제 장치를 알 수 없어 사용자가 고르고, 고른 장치에 맞게 방향감·음색을 바꾼다.
public enum OutputMode { Speakers, Headphones, Headset }

// 출력 장치별 값(순수 데이터). Spread = 3D 소리의 좌우 퍼짐(0이면 한쪽 귀로 완전히 나뉨), LowCutHz = 아주 낮은 저음 자르기(0이면 끔),
// SfxDb = 효과음·환경음 묶음 추가 이득.
public readonly struct OutputProfile
{
    public readonly float Spread;
    public readonly float LowCutHz;
    public readonly float SfxDb;

    public OutputProfile(float spread, float lowCutHz, float sfxDb)
    {
        Spread = spread;
        LowCutHz = lowCutHz;
        SfxDb = sfxDb;
    }

    // 스피커: 두 귀가 다 들으므로 좌우를 넓게 섞는다. 헤드폰: 지금과 같음(방향이 가장 또렷 — 괴물 위치 듣기). 헤드셋: 작은 진동판이
    // 뭉개지지 않게 아주 낮은 저음을 자르고 효과음을 조금 낮추며 좌우를 약간 섞는다.
    public static OutputProfile For(OutputMode mode)
    {
        switch (mode)
        {
            case OutputMode.Speakers: return new OutputProfile(60f, 0f, 0f);
            case OutputMode.Headset: return new OutputProfile(20f, 90f, -1f);
            default: return new OutputProfile(0f, 0f, 0f);
        }
    }
}

// 묶음별 기본 이득(dB, §2.5)과 사용자 음량 막대 4개(전체·배경음·효과음·UI). 환경음은 효과음 막대를 따른다.
// 값은 기기에 저장한다(PlayerPrefs). 최종 음량 = 전체 × 묶음 막대 × 묶음 기본 이득.
public static class AudioVolumeSettings
{
    public enum Slider { Master, Music, Sfx, Ui }

    public const float SilentDb = -80f;
    private const string PrefsPrefix = "TOC_Volume_";
    private const string OutputModeKey = "TOC_OutputMode";
    public const OutputMode DefaultOutputMode = OutputMode.Headphones; // 예전 소리와 같은 값

    private static readonly float[] sliders = { 1f, 1f, 1f, 1f };
    private static OutputMode outputMode = DefaultOutputMode;
    private static bool loaded;

    // 어느 막대든 바뀌면 재생 중인 소리에 다시 반영한다(AudioRuntime이 구독).
    public static event Action Changed;

    public static float BaseDb(SoundBus bus)
    {
        switch (bus)
        {
            case SoundBus.Music: return -8f;
            case SoundBus.Ui: return -4f;
            case SoundBus.Ambience: return -12f;
            default: return 0f;
        }
    }

    public static float Get(Slider slider)
    {
        EnsureLoaded();
        return sliders[(int)slider];
    }

    public static void Set(Slider slider, float value)
    {
        EnsureLoaded();
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(sliders[(int)slider], value)) return;
        sliders[(int)slider] = value;
        PlayerPrefs.SetFloat(PrefsPrefix + slider, value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static OutputMode Output
    {
        get { EnsureLoaded(); return outputMode; }
    }

    public static void SetOutput(OutputMode mode)
    {
        EnsureLoaded();
        if (outputMode == mode) return;
        outputMode = mode;
        PlayerPrefs.SetInt(OutputModeKey, (int)mode);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static OutputProfile CurrentOutput => OutputProfile.For(Output);

    // 묶음의 최종 선형 이득(0~1).
    public static float Gain(SoundBus bus)
    {
        EnsureLoaded();
        float master = sliders[(int)Slider.Master];
        float user = sliders[(int)SliderFor(bus)];
        float outputDb = bus == SoundBus.Sfx || bus == SoundBus.Ambience ? OutputProfile.For(outputMode).SfxDb : 0f;
        return master * user * FromDecibels(BaseDb(bus) + outputDb);
    }

    public static Slider SliderFor(SoundBus bus)
    {
        switch (bus)
        {
            case SoundBus.Music: return Slider.Music;
            case SoundBus.Ui: return Slider.Ui;
            default: return Slider.Sfx; // 효과음·환경음
        }
    }

    public static float ToDecibels(float linear) => linear <= 0.0001f ? SilentDb : 20f * Mathf.Log10(linear);

    public static float FromDecibels(float db) => db <= SilentDb ? 0f : Mathf.Pow(10f, db / 20f);

    // 저장된 값을 다시 읽는다(테스트·설정 창 되돌리기용).
    public static void Reload()
    {
        loaded = false;
        EnsureLoaded();
        Changed?.Invoke();
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        foreach (Slider s in (Slider[])Enum.GetValues(typeof(Slider)))
            sliders[(int)s] = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsPrefix + s, 1f));
        int savedMode = PlayerPrefs.GetInt(OutputModeKey, (int)DefaultOutputMode);
        outputMode = Enum.IsDefined(typeof(OutputMode), savedMode) ? (OutputMode)savedMode : DefaultOutputMode;
    }
}
