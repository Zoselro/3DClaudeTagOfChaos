public static class NetEventCodes
{
    public const byte PaintStroke = 1; // 붓 스트로크 1회를 다른 클라이언트에 전파
    public const byte ClaimMonster = 2; // GameRule.md §2.1 — 가마솥 진입, 괴물 자원 신청
    public const byte ClearColor = 3; // GameRule.md §3.5 — 지우개
    public const byte FillAll = 4; // GameRule.md §3.6 — 강제 도포
}
