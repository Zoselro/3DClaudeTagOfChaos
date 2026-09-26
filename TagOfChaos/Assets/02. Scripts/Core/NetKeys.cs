// Room/Player CustomProperties 키. Lobby/GameManager/Unit/ColorTag/Monster 전 도메인이 참조하므로
// Core/에 둔다(research.md §7 — 원래 ColorTag/에 있었다). 구 4라운드 색상 미니게임 전용 키
// (RoundIndex/RoundEndTime/Color0~3/Tagger*/VoteColorIndex/CookiesDeparted)는 읽고 쓰는 곳이 없어
// 제거했다(research.md §8.21).
//
// 새 키를 추가하면 아래 Scopes 표에 대상(Room/Player)과 수명(Round/Session)을 함께 적는다. 판 초기화 목록
// (RoundRoomKeys/RoundPlayerKeys)은 이 표에서 자동으로 만들어진다 — 예전에는 초기화 목록을 따로 손으로 관리해
// 빠뜨리면 두 번째 판에서만 드러나는 버그가 됐다(research.md §12 E8). 표에서 빠진 키는 EditMode 테스트가 잡는다.
public static class NetKeys
{
    // 괴물 선정(GameRule.md §2)
    public const string MonsterActorNumbers = "MonsterActorNumbers"; // int[]
    public const string MonsterRevealTime = "MonsterRevealTime";
    public const string MonsterSelectDeadline = "MonsterSelectDeadline"; // double — 정원이 찬 시각 + 타임아웃(Bug-fix-plan.md §24.3 B1)

    // 페인트 페이즈(GameRule.md §3)
    public const string PaintPhaseEndTime = "PaintPhaseEndTime";
    public const string MonsterJoined = "MonsterJoined";
    public const string ForcedPaintActorNumbers = "ForcedPaintActorNumbers"; // int[]
    public const string ForcedPaintColors = "ForcedPaintColors"; // int[] — ForcedPaintActorNumbers와 인덱스로 1:1 대응

    // 술래잡기 본게임(GameRule.md §6.4)
    public const string GameEndTime = "GameEndTime";

    // 이탈/방장 위임(GameRule.md §7)
    public const string MonsterDepartedAt = "MonsterDepartedAt";

    // 승패(GameRule.md §8)
    public const string GameResult = "GameResult";

    // Player CustomProperties (GameRule.md §4.4 — hitCount는 0 또는 2만 실제로 쓰임, v3.6)
    public const string HitCount = "HitCount";
    public const string RegisteredSlotCount = "RegisteredSlotCount"; // GameRule.md §3.2
    public const string SkinIndex = "SkinIndex"; // int — SkinCatalogSO 인덱스, GameRule.md §1.5 (판이 바뀌어도 유지)

    public enum Target { Room, Player }

    public enum Lifetime
    {
        Round,   // 한 판이 끝나 GameLobbyScene으로 돌아올 때 지운다(RoundStateResetter)
        Session, // 방에 있는 동안 유지한다
    }

    public readonly struct Scope
    {
        public readonly string Key;
        public readonly Target Target;
        public readonly Lifetime Lifetime;

        public Scope(string key, Target target, Lifetime lifetime)
        {
            Key = key;
            Target = target;
            Lifetime = lifetime;
        }
    }

    // 모든 키의 대상·수명 선언(빌드에서 리플렉션 없이 쓰도록 명시적인 표로 둔다).
    public static readonly Scope[] Scopes =
    {
        new Scope(MonsterActorNumbers, Target.Room, Lifetime.Round),
        new Scope(MonsterRevealTime, Target.Room, Lifetime.Round),
        new Scope(MonsterSelectDeadline, Target.Room, Lifetime.Round),
        new Scope(PaintPhaseEndTime, Target.Room, Lifetime.Round),
        new Scope(MonsterJoined, Target.Room, Lifetime.Round),
        new Scope(ForcedPaintActorNumbers, Target.Room, Lifetime.Round),
        new Scope(ForcedPaintColors, Target.Room, Lifetime.Round),
        new Scope(GameEndTime, Target.Room, Lifetime.Round),
        new Scope(MonsterDepartedAt, Target.Room, Lifetime.Round),
        new Scope(GameResult, Target.Room, Lifetime.Round),
        new Scope(HitCount, Target.Player, Lifetime.Round),
        new Scope(RegisteredSlotCount, Target.Player, Lifetime.Round),
        new Scope(SkinIndex, Target.Player, Lifetime.Session),
    };

    // 한 판이 끝나 GameLobbyScene으로 돌아올 때 초기화해야 하는 키 목록(RoundStateResetter, research.md §8.7).
    public static readonly string[] RoundRoomKeys = Collect(Target.Room, Lifetime.Round);
    public static readonly string[] RoundPlayerKeys = Collect(Target.Player, Lifetime.Round);

    private static string[] Collect(Target target, Lifetime lifetime)
    {
        var keys = new System.Collections.Generic.List<string>();
        foreach (Scope scope in Scopes)
            if (scope.Target == target && scope.Lifetime == lifetime) keys.Add(scope.Key);
        return keys.ToArray();
    }
}
