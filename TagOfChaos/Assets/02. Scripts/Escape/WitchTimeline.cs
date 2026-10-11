// 마녀가 오는 이유(Room Prop WitchMode·WitchStrike 값).
public enum WitchMode
{
    None = 0,
    TimeAttack = 1, // 스파이 로켓이 떠나고 쿠키가 남았다 — 타임어택이 끝나면 남은 생명체를 모두 없앤다
    Purge = 2,      // 쿠키가 모두 탈출했는데 스파이가 남았다 — 괴물과 스파이를 없앤다(Request1009Plan.md §19.4)
}

// 마녀 등장 시각표(Request1009Plan.md §19.2). 마녀·카메라·방장 판정이 같은 계산을 쓴다.
// 앞 샷(로켓이 날아감 / 쿠키 탈것이 떠남)을 보여 준 뒤 WitchAppearAt에 마녀가 나타나고, 그때부터 클로즈업(WitchRevealCamera)이 시작된다.
public static class WitchTimeline
{
    public const float RocketShotSeconds = 7f; // 로켓이 떠올라 하늘로 사라질 때까지(SpyRocket이 7초에 숨긴다)
    public const float LeadInSeconds = 2f;     // 앞 샷으로 카메라가 옮겨 가는 시간

    public static WitchMode Mode => RoomState.TryGetInt(NetKeys.WitchMode, out int m) ? (WitchMode)m : WitchMode.None;
    public static bool TryGetAppearAt(out double at) => RoomState.TryGetDouble(NetKeys.WitchAppearAt, out at);

    public static double RocketAppearAt(double launchedAt) => launchedAt + RocketShotSeconds;
    public static double TimeAttackEnd(double appearAt, int timeAttackSeconds) => appearAt + WitchRevealCamera.TotalSeconds + timeAttackSeconds;
    public static double PurgeAppearAt(double departedAt, float departureSeconds) => departedAt + departureSeconds;
    // 처단: 얼굴 클로즈업(3초 이동 + 1초) → 내리치는 장면으로 카메라가 옮겨 간(1.5초) 순간 내리친다 — 내리침이 클로즈업으로 보인다(§19.11)
    public static double PurgeStrikeAt(double appearAt) =>
        appearAt + WitchRevealCamera.MoveInSeconds + WitchRevealCamera.HoldSeconds + WitchRevealCamera.StrikeShotMoveSeconds;

    // 쿠키 탈것이 출발한 순간 처단을 시작하는지(§19.7 Q2): 남은 스파이가 있고 아직 마녀가 정해지지 않았을 때만.
    public static bool ShouldStartPurge(int activeSpies, WitchMode current) => activeSpies > 0 && current == WitchMode.None;

    // 이 화면이 연출(카메라)을 보는지: 로켓 = 모두(Q1), 처단 = 스파이·괴물(Q4 — 쿠키는 이미 탈출).
    public static bool Watches(WitchMode mode, bool localIsSpy, bool localIsMonster) =>
        mode == WitchMode.TimeAttack || (mode == WitchMode.Purge && (localIsSpy || localIsMonster));

    // 내리칠 때 이 쿠키가 죽는지: 타임어택은 남은 쿠키 모두, 처단은 스파이만(괴물은 늘 죽는다 — MonsterEscapeState).
    public static bool CookieDiesInStrike(int strike, bool isSpy) => strike != (int)WitchMode.Purge || isSpy;

    // 앞 샷 시작 시각: 로켓 발사 또는 쿠키 탈것 출발.
    public static bool TryGetLeadInStart(WitchMode mode, out double start)
    {
        start = 0;
        if (mode == WitchMode.TimeAttack) return RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out start);
        if (mode != WitchMode.Purge) return false;
        EscapeState state = EscapeManager.Instance != null ? EscapeManager.Instance.State : null;
        if (state == null || state.DepartedAt <= 0) return false;
        start = state.DepartedAt;
        return true;
    }

}
