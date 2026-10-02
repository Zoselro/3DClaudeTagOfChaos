// 버튼이 아닌 UI 소리(SoundPlan.md S2): 창 열기·닫기, 저장, 경고, 토스트, 카운트다운 째깍.
// 화면 코드가 "일어난 일"의 이름으로 부른다 — 어떤 소리 ID인지는 여기서만 정한다.
public static class UiSoundCues
{
    public const int TickFromSeconds = 10;  // 마지막 10초부터 째깍
    public const int FinalFromSeconds = 3;  // 마지막 3초는 다른 소리

    public static void WindowOpened() => GameAudio.Play(SoundId.UiOpen);
    public static void WindowClosed() => GameAudio.Play(SoundId.UiClose);
    public static void Saved() => GameAudio.Play(SoundId.UiSaved);
    public static void Error() => GameAudio.Play(SoundId.UiError);
    public static void Toast(bool alert) => GameAudio.Play(alert ? SoundId.UiToastAlert : SoundId.UiToastInfo);

    // 화면의 남은 초가 바뀔 때마다 부른다. 같은 프레임에 두 화면이 같은 초를 보여도 쿨다운으로 한 번만 난다.
    public static void CountdownTick(int secondsLeft)
    {
        SoundId id = TickSound(secondsLeft);
        if (id != SoundId.None) GameAudio.Play(id);
    }

    public static SoundId TickSound(int secondsLeft)
    {
        if (secondsLeft <= 0 || secondsLeft > TickFromSeconds) return SoundId.None;
        return secondsLeft <= FinalFromSeconds ? SoundId.UiTickFinal : SoundId.UiTick;
    }
}
