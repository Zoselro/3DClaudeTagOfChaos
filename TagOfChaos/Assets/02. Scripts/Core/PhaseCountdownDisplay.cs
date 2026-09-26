using Photon.Pun;
using TMPro;
using UnityEngine;

// 게임 페이즈의 남은 시간을 mm:ss로 표시하는 공용 카운트다운. 어떤 페이즈를 셀지는 인스펙터에서 고른다
// (예전 이름 SurvivalTimerDisplay — 생존 시간 전용이던 것을 일반화, .meta GUID 유지로 씬 참조 보존).
// 단계 판정은 GamePhaseState 한 곳에 맡긴다(research.md §12 E2). 새 페이즈에 카운트다운이 필요하면
// CountdownPhase 항목과 ToGamePhase의 대응만 추가하면 된다(Bug-fix-plan.md §28.2).
// 자기 GameObject를 끄지 않고 CanvasGroup으로만 숨겨 Update()가 계속 돌게 한다(§23.2.1과 같은 이유).
public class PhaseCountdownDisplay : MonoBehaviour
{
    // 씬에 직렬화된 값이므로 항목 순서를 바꾸지 않는다.
    public enum CountdownPhase
    {
        Paint,    // 쿠키 변장(색칠) 시간
        Survival, // 괴물 합류 후 쿠키 생존 시간
    }

    [SerializeField] private CountdownPhase phase = CountdownPhase.Survival;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private string format = "{0:00}:{1:00}"; // {0}=분, {1}=초 — 앞뒤 문구는 인스펙터에서 입력

    private CanvasGroup canvasGroup;
    private bool? lastVisible;
    private int lastShownSeconds = -1;

    private void Awake()
    {
        canvasGroup = CanvasGroupVisibility.Ensure(gameObject);
        SetVisible(false);
    }

    private void Update()
    {
        bool running = GamePhaseState.TryGetActiveEndTime(ToGamePhase(phase), out double endTime) && PhotonNetwork.Time < endTime;
        SetVisible(running);
        if (!running) return;

        int seconds = Mathf.Max(0, Mathf.CeilToInt((float)(endTime - PhotonNetwork.Time)));
        if (seconds == lastShownSeconds || timerText == null) return; // 초가 바뀔 때만 문자열을 만든다

        lastShownSeconds = seconds;
        timerText.text = string.Format(format, seconds / 60, seconds % 60);
    }

    private static GamePhase ToGamePhase(CountdownPhase countdownPhase)
    {
        switch (countdownPhase)
        {
            case CountdownPhase.Paint: return GamePhase.Paint;
            default: return GamePhase.Hunt;
        }
    }

    private void SetVisible(bool visible)
    {
        if (lastVisible == visible) return;
        lastVisible = visible;
        CanvasGroupVisibility.Set(canvasGroup, visible);
    }
}
