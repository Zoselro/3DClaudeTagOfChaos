using System.Collections.Generic;

// 탈출 모드의 게임 끝 판정(EscapePlan.md §1.2, Request1009Plan.md §25.5 D). 네트워크와 분리한 순수 계산이라 테스트할 수 있다.
// 방장의 GameRuleController가 매 프레임 부른다. 게임 제한시간은 없다 — 끝나는 길은 셋:
//   1) 괴물이 아닌 사람(쿠키·스파이)이 모두 탈출하거나 잡혔다 → 바로 끝(마녀 타이머 중이어도)
//   2) 첫 탈출로 시작된 마녀 타이머가 끝났다 → 마녀가 밟는다 → 박살을 보여 준 뒤 끝
//   3) (안전 상한) 아무도 탈출하지 못한 채 오래 지나면 방장이 마녀 타이머를 강제로 시작한다 — 2)로 이어진다
public static class EscapeRules
{
    public struct PlayerInfo
    {
        public bool IsMonster;
        public bool IsSpy;
        public bool Escaped;  // 쿠키 탈출구·헬리콥터 모자·스파이 로켓
        public bool Broken;   // 괴물에게 잡힘 또는 마녀
    }

    public enum Decision
    {
        None,
        StrikeWitch,   // 마녀 타이머가 끝났다 — 마녀가 밟는다
        EndAllResolved,
        EndTimeUp,     // 쓰지 않음(게임 제한시간을 없앴다). 값 보존
        EndWitchStrike,
    }

    // 내리침(도착) 기록 뒤 결과까지: 도착 연출 3테이크 — 밟는 순간(FinaleImpactSeconds)까지 + 박살을 보여 주는 5초(Request1009Plan.md §24.10)
    public const float WitchSlamSeconds = WitchTimeline.FinaleImpactSeconds + WitchTimeline.FinaleEndSeconds;

    // witchTimer: 마녀 타이머가 돌고 있는가(첫 탈출 뒤). witchStruckFor: 마녀가 밟은 뒤 지난 시간(초, 안 밟았으면 음수).
    // departing: 로켓 발사 연출 중 — 연출이 끝날 때까지 끝 판정을 미룬다(EscapeVisualPlan.md §4.3).
    public static Decision Evaluate(IReadOnlyList<PlayerInfo> players, bool witchTimer, double now, double witchTimerEnd, float witchStruckFor, bool departing = false)
    {
        if (witchStruckFor >= WitchSlamSeconds) return Decision.EndWitchStrike;
        if (witchStruckFor >= 0f) return Decision.None; // 밟는 중
        if (departing) return Decision.None;

        int active = 0;
        foreach (PlayerInfo p in players)
            if (!p.IsMonster && !p.Escaped && !p.Broken) active++;
        if (active == 0) return Decision.EndAllResolved;

        if (witchTimer && now >= witchTimerEnd) return Decision.StrikeWitch;
        return Decision.None;
    }

    // 안전 상한(§25.3 Q4): 준비가 끝난 뒤 capSeconds가 지났는데 마녀 타이머가 아직 없으면 true.
    public static bool ShouldForceWitchTimer(double now, double prepareEnd, double capSeconds, bool witchTimer) =>
        !witchTimer && now >= prepareEnd + capSeconds;
}
