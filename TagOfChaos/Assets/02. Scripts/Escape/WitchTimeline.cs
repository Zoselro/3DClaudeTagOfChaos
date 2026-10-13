// 마녀가 오는 이유(Room Prop WitchMode·WitchStrike 값).
public enum WitchMode
{
    None = 0,
    TimeAttack = 1, // 스파이 로켓이 떠나고 쿠키가 남았다 — 타임어택이 끝나면 남은 생명체를 모두 없앤다
    Purge = 2,      // 쿠키가 모두 탈출했는데 스파이가 남았다 — 괴물과 스파이를 없앤다(Request1009Plan.md §19.4)
}

// 마녀 연출의 카메라 장면(Request1009Plan.md §23.5 X5, 도착 뒤 3테이크 §24.10). 시각표(WitchTimeline.ShotAt)가 정하고 WitchRevealCamera가 보여 준다.
public enum WitchShot
{
    None,           // 각자 시점
    Lead,           // 로켓(타임어택) 또는 떠나는 탈출구(처단)
    Reveal,         // 마녀가 나타나는 동안 마녀 쪽으로 — 타임어택은 등진 전신, 처단은 얼굴
    Turn,           // 타임어택: 전신을 본 채 마녀가 뒤돌아섬
    Face,           // 타임어택: 얼굴 클로즈업
    Return,         // 타임어택: 각자 시점으로 돌아옴
    Approach,       // 처단: 맵 가장자리에서 다가오는 마녀를 올려다봄
    FinaleLowAngle, // 도착 테이크 1: 검은 화면 → 땅 높이에서 쿠키·괴물 → 로우 앵글로 뒤로 빠지며 아래를 보는 마녀 얼굴까지
    FinaleStomp,    // 도착 테이크 2: 쿠키·괴물 뒤 땅에서 올려다봄 — 밑창이 서서히 내려와 화면을 덮음
    FinaleEnd,      // 도착 테이크 3: 검은 화면 뒤 정면 위에서 밟고 선 마녀와 박살 → 결과(End)
}

// 마녀 시각표(Request1009Plan.md §19.2, §23.3). 마녀·카메라·방장 판정이 같은 계산을 쓴다. 시각은 모두 공통 시계(PhotonNetwork.Time)와
// 방 속성(WitchAppearAt·TimeAttackEndTime)만 써서, 모든 화면이 같고 늦게 들어온 사람·방장이 바뀐 경우도 맞다.
//   타임어택: 로켓 3초 → 등장 3초(등진 전신) → 돌아섬 2초 → 얼굴 2초 → 복귀 3초 → 타임어택 동안 걸어옴 → 도착(타임어택 끝) 3테이크
//   처단:     탈출구 3초 → 등장 3초(얼굴) → 10초 동안 걸어옴(다가오는 장면) → 도착 3테이크
//   도착 3테이크(§24.10): 로우 앵글 6초 → 밑에서 본 밟기 4초(끝에 밟음 = 충격·죽음) → 검은 화면 0.4초 → 박살 4.6초 → 결과(도착 + 15초)
public static class WitchTimeline
{
    public const float LeadShotSeconds = 3f;     // 로켓·탈출구를 보여 준 뒤 마녀가 나타난다
    public const float LeadInSeconds = 2f;       // 그 안에서 카메라가 로켓·탈출구 쪽으로 옮겨 가는 시간
    public const float RevealSeconds = 3f;       // 마녀가 서서히 나타나는(일어서는) 동안 카메라가 마녀 쪽으로
    public const float TurnSeconds = 2f;         // 타임어택: 뒤돌아섬(동작 TurnBack과 같은 길이)
    public const float FaceSeconds = 2f;         // 타임어택: 얼굴 클로즈업
    public const float FaceMoveSeconds = 1f;     //   그중 얼굴 앞으로 다가가는 시간(나머지는 머묾)
    public const float ReturnSeconds = 3f;       // 타임어택: 각자 시점으로
    public const float IntroSeconds = RevealSeconds + TurnSeconds + FaceSeconds + ReturnSeconds; // 타임어택 시작 연출(조작이 멈춤)
    public const float PurgeWalkSeconds = 10f;   // 처단: 걸어오는 시간
    public const float FinaleLowAngleSeconds = 6f;   // 도착 테이크 1(로우 앵글)
    public const float FinaleStompSeconds = 4f;      // 도착 테이크 2(밟기) — 끝에 발이 땅에 닿는다
    public const float FinaleImpactSeconds = FinaleLowAngleSeconds + FinaleStompSeconds; // 도착(= 내리침 기록)부터 밟는 순간 — 죽음 확정·박살
    public const float FinaleBlackSeconds = 0.4f;    // 밟은 뒤 검은 화면
    public const float FinaleEndSeconds = 5f;        // 밟은 뒤 결과까지(검은 화면 포함) — EscapeRules.WitchSlamSeconds = 충격 + 이것
    public const float FinaleMaxSeconds = 30f;       // 도착 연출에 머무는 최대 시간(결과가 오지 않을 때의 안전장치)

    public static WitchMode Mode => RoomState.TryGetInt(NetKeys.WitchMode, out int m) ? (WitchMode)m : WitchMode.None;
    public static bool TryGetAppearAt(out double at) => RoomState.TryGetDouble(NetKeys.WitchAppearAt, out at);

    public static double RocketAppearAt(double launchedAt) => launchedAt + LeadShotSeconds;
    public static double PurgeAppearAt(double departedAt) => departedAt + LeadShotSeconds; // 탈것이 떠나는 연출은 뒤에서 이어진다
    // 타임어택 끝: 시작 연출(조작이 멈추는 10초)만큼 더 준다
    public static double TimeAttackEnd(double appearAt, int timeAttackSeconds) => appearAt + IntroSeconds + timeAttackSeconds;
    public static double PurgeStrikeAt(double appearAt) => appearAt + RevealSeconds + PurgeWalkSeconds;
    public static double TurnStart(double appearAt) => appearAt + RevealSeconds;

    // 걷기: 타임어택은 복귀가 끝난 때부터 타임어택 끝까지, 처단은 나타난 뒤 10초. 걷는 속도는 따로 없다 — 걷는 시간이 바뀌면(방 설정) 속도가 바뀐다.
    public static double WalkStart(WitchMode mode, double appearAt) => appearAt + (mode == WitchMode.Purge ? RevealSeconds : IntroSeconds);
    public static double WalkEnd(WitchMode mode, double appearAt, double timeAttackEnd) => mode == WitchMode.Purge ? PurgeStrikeAt(appearAt) : timeAttackEnd;

    public static float WalkProgress(double now, double start, double end)
    {
        if (end <= start) return now >= end ? 1f : 0f;
        double k = (now - start) / (end - start);
        return k <= 0 ? 0f : k >= 1 ? 1f : (float)k;
    }

    // 이 판의 걷기 끝 시각(타임어택은 방 속성, 없으면 기본 시간).
    public static double WalkEnd(WitchMode mode, double appearAt)
    {
        double end = RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out double t) ? t : TimeAttackEnd(appearAt, RoomState.TimeAttackSeconds());
        return WalkEnd(mode, appearAt, end);
    }

    // 쿠키 탈것이 출발한 순간 처단을 시작하는지(§19.7 Q2): 남은 스파이가 있고 아직 마녀가 정해지지 않았을 때만.
    public static bool ShouldStartPurge(int activeSpies, WitchMode current) => activeSpies > 0 && current == WitchMode.None;

    // 이 화면이 연출(카메라)을 보는지: 두 경우 모두 모두가 본다 — 처단도 탈출한 쿠키까지(§23.1 B).
    public static bool Watches(WitchMode mode) => mode != WitchMode.None;

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

    // 도착 뒤 장면(순수 계산, since = 도착부터 초).
    public static WitchShot FinaleShotAt(double since)
    {
        if (since < 0 || since >= FinaleMaxSeconds) return WitchShot.None;
        if (since < FinaleLowAngleSeconds) return WitchShot.FinaleLowAngle;
        return since < FinaleImpactSeconds ? WitchShot.FinaleStomp : WitchShot.FinaleEnd;
    }

    // 도착 연출 중인지(조작 정지·대역·마녀 밟기가 함께 쓴다). 결과가 나오면 끝.
    public static bool InFinale(double now, double walkEnd, bool resultDecided) => !resultDecided && FinaleShotAt(now - walkEnd) != WitchShot.None;

    // 지금 보여 줄 장면(순수 계산). 결과가 나왔으면 결과 화면이라 None.
    public static WitchShot ShotAt(WitchMode mode, double now, bool hasLead, double leadStart, double appearAt, double walkEnd, bool resultDecided)
    {
        if (mode == WitchMode.None || resultDecided) return WitchShot.None;
        if (now < appearAt) return hasLead && now >= leadStart ? WitchShot.Lead : WitchShot.None;
        if (now >= walkEnd) return FinaleShotAt(now - walkEnd);
        double t = now - appearAt;
        if (t < RevealSeconds) return WitchShot.Reveal;
        if (mode == WitchMode.Purge) return WitchShot.Approach;
        t -= RevealSeconds;
        if (t < TurnSeconds) return WitchShot.Turn;
        t -= TurnSeconds;
        if (t < FaceSeconds) return WitchShot.Face;
        t -= FaceSeconds;
        return t < ReturnSeconds ? WitchShot.Return : WitchShot.None;
    }
}
