public enum PlayerMoveState
{
    Idle,
    Walk,
    Run,
    Jump,
    Dodge,
    Held,   // 그랩당한 상태(GameRule.md §4.1)
    Broken, // GrabKill로 파괴된 상태(GameRule.md §4.4)
    Carried, // 다른 쿠키에게 두 팔로 안겨 들린 상태(Request1009Plan.md §16.3) — 괴물에게 잡힌 처형 연출은 Held 그대로
}
