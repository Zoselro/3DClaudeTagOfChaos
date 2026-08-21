// PlayerMoveState와 동일한 계약(Animator 파라미터명과 정확히 일치해야 함, research.md §2.4).
// MonsterAnimator.controller의 실제 트리거 파라미터 4개와 이름이 일치해야 한다. GrabKill로
// 오타 정정 확정(GameRule.md v3.6) — Animator Controller 파라미터도 Unity 에디터에서
// GrapKill(오타)에서 GrabKill로 함께 리네임해야 실제로 트리거가 걸린다.
public enum MonsterMoveState
{
    Idle,
    Walk,
    TentacleDash,
    GrabKill,
}
