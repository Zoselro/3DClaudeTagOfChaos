using System;
using System.Collections.Generic;
using UnityEngine;

// 게임 소리를 낼 출력 장치 선택(Request1009Plan.md §17). 설정 창에서 이 PC의 실제 출력 장치를 고르면 이 게임만 그 장치로 소리를 낸다
// (Windows "앱 볼륨 및 장치 기본 설정"과 같은 기능 — PC 전체 기본 장치와 다른 프로그램은 그대로). Windows 전용, 다른 OS는 늘 시스템 기본.
// 고른 장치는 기기에 저장하고(PlayerPrefs, 빈 값 = 시스템 기본) 다음 실행 때 다시 적용한다. 그 장치가 없으면(뽑힘) 시스템 기본으로 난다.
public static class OutputDevices
{
    public const string PrefsKey = "TOC_OutputDevice";
    private const string OldModeKey = "TOC_OutputMode"; // N6의 스피커·헤드폰·헤드셋(지움) 저장값

    private static string selectedId;
    private static bool loaded;

    // 선택이나 장치 구성이 바뀌었을 때(설정 창이 목록을 다시 그린다).
    public static event Action Changed;

    public static bool IsSupported => WindowsAudioEndpoints.IsSupported;

    // 고른 장치 ID(빈 값 = 시스템 기본).
    public static string SelectedId
    {
        get { EnsureLoaded(); return selectedId; }
    }

    public static List<WindowsAudioEndpoints.Device> Available() => WindowsAudioEndpoints.ListOutputs();

    // 저장된 장치가 지금 목록에 있으면 그 ID, 없으면 빈 값(시스템 기본). 순수 계산.
    public static string Resolve(string savedId, IList<WindowsAudioEndpoints.Device> available)
    {
        if (string.IsNullOrEmpty(savedId) || available == null) return string.Empty;
        foreach (WindowsAudioEndpoints.Device d in available)
            if (d.Id == savedId) return savedId;
        return string.Empty;
    }

    // 장치를 고른다(빈 값 = 시스템 기본). 저장하고 바로 적용한다. 적용에 실패하면 false(저장은 하지 않는다).
    public static bool Select(string deviceId)
    {
        EnsureLoaded();
        deviceId = deviceId ?? string.Empty;
        if (!Apply(deviceId)) return false;
        selectedId = deviceId;
        PlayerPrefs.SetString(PrefsKey, selectedId);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    // 시작할 때(AudioRuntime.Build) 저장된 장치를 다시 적용한다. 시스템 기본이면 아무것도 건드리지 않는다.
    public static void ApplySaved()
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(selectedId) || !IsSupported) return;
        if (Resolve(selectedId, Available()).Length == 0) return; // 지금 없는 장치 — 저장값은 두고(다시 꽂으면 쓰도록) 시스템 기본으로 난다
        Apply(selectedId);
    }

    // 장치를 꽂거나 뽑았을 때(AudioSettings.OnAudioConfigurationChanged) 설정 창을 새로 그리게 알린다.
    public static void NotifyDevicesChanged() => Changed?.Invoke();

    private static bool Apply(string deviceId)
    {
        if (!IsSupported) return deviceId.Length == 0;
        if (!WindowsAudioEndpoints.SetAppOutput(deviceId.Length == 0 ? null : deviceId)) return false;
        AudioRuntime runtime = AudioRuntime.Instance;
        if (runtime != null) runtime.ReopenOutput();
        else AudioSettings.Reset(AudioSettings.GetConfiguration());
        return true;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        selectedId = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (PlayerPrefs.HasKey(OldModeKey)) { PlayerPrefs.DeleteKey(OldModeKey); PlayerPrefs.Save(); }
    }

    // 저장값을 다시 읽는다(시험용).
    public static void Reload() => loaded = false;
}
