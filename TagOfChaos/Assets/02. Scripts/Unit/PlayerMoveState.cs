public enum PlayerMoveState
{
    Idle,
    Walk,
    Run,
    Jump,
    Dodge,
    Held,   // 그랩당한 상태(GameRule.md §4.1)
    Broken, // GrabKill로 파괴된 상태(GameRule.md §4.4)
}
