using System;
using UnityEngine;

// 소리 묶음(SoundPlan.md §2.5). 믹서 에셋 대신 코드로 음량을 곱한다 — Unity는 AudioMixer를 코드로 만드는 공개 API가 없어
// 에디터 도구로 다시 만들 수 없고, 지금 필요한 것은 묶음별 음량과 배경음 낮춤뿐이라 코드가 더 단순하다.
public enum SoundBus { Music, Sfx, Ui, Ambience }

// 묶음별 기본 이득(dB, §2.5)과 사용자 음량 막대 4개(전체·배경음·효과음·UI). 환경음은 효과음 막대를 따른다.
// 값은 기기에 저장한다(PlayerPrefs). 최종 음량 = 전체 × 묶음 막대 × 묶음 기본 이득.
public static class AudioVolumeSettings
{
    public enum Slider { Master, Music, Sfx, Ui }

    public const float SilentDb = -80f;
    private const string PrefsPrefix = "TOC_Volume_";

    private static readonly float[] sliders = { 1f, 1f, 1f, 1f };
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

    // 묶음의 최종 선형 이득(0~1).
    public static float Gain(SoundBus bus)
    {
        EnsureLoaded();
        float master = sliders[(int)Slider.Master];
        float user = sliders[(int)SliderFor(bus)];
        return master * user * FromDecibels(BaseDb(bus));
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
    }
}
