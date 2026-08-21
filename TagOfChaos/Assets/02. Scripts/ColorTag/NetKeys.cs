public static class NetKeys
{
    public const string RoundIndex = "RoundIndex";
    public const string RoundEndTime = "RoundEndTime";
    public const string ColorPrefix = "Color"; // Color0 ~ Color3
    public const string TaggerActorNumber = "TaggerActorNumber";
    public const string TaggerVariantSet = "TaggerVariantSet";
    public const string VoteColorIndex = "VoteColorIndex";
    public const string GameEndTime = "GameEndTime";

    // 괴물 선정(GameRule.md §2)
    public const string MonsterActorNumbers = "MonsterActorNumbers"; // int[]
    public const string MonsterRevealTime = "MonsterRevealTime";
    public const string CookiesDeparted = "CookiesDeparted";

    // 페인트 페이즈(GameRule.md §3)
    public const string PaintPhaseEndTime = "PaintPhaseEndTime";
    public const string MonsterJoined = "MonsterJoined";
    public const string ForcedPaintActorNumbers = "ForcedPaintActorNumbers"; // int[]
    public const string ForcedPaintColors = "ForcedPaintColors"; // int[] — ForcedPaintActorNumbers와 인덱스로 1:1 대응

    // 이탈/방장 위임(GameRule.md §7)
    public const string MonsterDepartedAt = "MonsterDepartedAt";

    // 승패(GameRule.md §8)
    public const string GameResult = "GameResult";

    // Player CustomProperties (GameRule.md §4.4 — hitCount는 0 또는 2만 실제로 쓰임, v3.6)
    public const string HitCount = "HitCount";
    public const string RegisteredSlotCount = "RegisteredSlotCount"; // GameRule.md §3.2
    public const string SkinIndex = "SkinIndex"; // int(0~2, A/B/C) — GameRule.md §1.5
}
