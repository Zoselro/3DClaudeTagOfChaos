using System.Collections.Generic;

// 탈출 모드의 게임 끝 판정(EscapePlan.md §1.2, §5.8). 네트워크와 분리한 순수 계산이라 테스트할 수 있다.
// 방장의 GameRuleController가 매 프레임 부른다.
public static class EscapeRules
{
    public struct PlayerInfo
    {
        public bool IsMonster;
        public bool IsSpy;
        public bool Escaped;  // 쿠키 탈출구 또는 로켓 탑승
        public bool Broken;   // 괴물에게 잡힘 또는 마녀
    }

    public enum Decision
    {
        None,
        StrikeWitch,   // 타임어택이 끝났다 — 마녀가 내리친다
        EndAllResolved,
        EndTimeUp,
        EndWitchStrike,
    }

    public const float WitchSlamSeconds = 2.5f; // 내리치는 연출을 보여준 뒤 결과

    // timeAttack: 스파이가 떠났는가. witchStruckFor: 마녀가 내리친 뒤 지난 시간(초, 안 내리쳤으면 음수).
    // departing: 쿠키 탈것이 출발 연출 중 — 연출이 끝날 때까지 끝 판정을 미룬다(EscapeVisualPlan.md §4.3).
    public static Decision Evaluate(IReadOnlyList<PlayerInfo> players, bool timeAttack, double now, double gameEnd, bool hasGameEnd,
        double timeAttackEnd, float witchStruckFor, bool departing = false)
    {
        if (departing && witchStruckFor < 0f) return Decision.None;

        int activeCookies = 0, activeSpies = 0;
        foreach (PlayerInfo p in players)
        {
            if (p.IsMonster || p.Escaped || p.Broken) continue;
            if (p.IsSpy) activeSpies++;
            else activeCookies++;
        }

        if (timeAttack)
        {
            if (witchStruckFor >= WitchSlamSeconds) return Decision.EndWitchStrike;
            if (witchStruckFor >= 0f) return Decision.None;          // 내리치는 중
            if (activeCookies == 0) return Decision.EndAllResolved; // 괴물이 남은 쿠키를 다 잡았거나 모두 탈출(D10 포함)
            if (now >= timeAttackEnd) return Decision.StrikeWitch;
            return Decision.None;                                   // 타임어택 중에는 방의 제한시간이 멈춘다(D15)
        }

        if (activeCookies == 0 && activeSpies == 0) return Decision.EndAllResolved;
        if (hasGameEnd && now >= gameEnd) return Decision.EndTimeUp; // 탈출 못 한 쿠키·스파이는 탈출 실패(D8)
        return Decision.None;
    }
}
