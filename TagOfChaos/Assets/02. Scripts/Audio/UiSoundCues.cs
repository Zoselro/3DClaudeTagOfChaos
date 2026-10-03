using UnityEngine;

// 버튼이 아닌 UI 소리(SoundPlan.md S2): 창 열기·닫기, 저장, 경고, 토스트, 카운트다운 째깍.
// 화면 코드가 "일어난 일"의 이름으로 부른다 — 어떤 소리 ID인지는 여기서만 정한다.
public static class UiSoundCues
{
    public const int TickFromSeconds = 10;  // 마지막 10초부터 째깍
    public const int FinalFromSeconds = 3;  // 마지막 3초는 다른 소리

    public static void WindowOpened() => GameAudio.Play(SoundId.UiOpen);
    public static void WindowClosed() => GameAudio.Play(SoundId.UiClose);
    // 저장·오류는 버튼을 누른 "결과"라서, 같은 프레임의 버튼 클릭음을 대신한다(두 소리가 겹치지 않게 — 2026-10-03 보고:
    // 방 이름 없이 방 만들기를 누르면 확인음과 오류음이 함께 났다). 버튼 리스너와 결과 코드 중 어느 쪽이 먼저 돌아도 하나만 난다.
    public static void Saved() => PlayResult(SoundId.UiSaved);
    public static void Error() => PlayResult(SoundId.UiError);

    private static int resultFrame = -1;
    private static int clickFrame = -1;
    private static AudioHandle clickHandle;

    // UiSound(버튼)가 부른다.
    public static void ButtonClick(SoundId id)
    {
        if (id == SoundId.None || resultFrame == Time.frameCount) return;
        clickHandle = GameAudio.Play(id);
        clickFrame = Time.frameCount;
    }

    private static void PlayResult(SoundId id)
    {
        if (clickFrame == Time.frameCount) clickHandle.Stop();
        resultFrame = Time.frameCount;
        GameAudio.Play(id);
    }
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
