using UnityEngine;

public class PlayerAnimationDriver
{
    private readonly Animator animator;
    private readonly float jumpFreezeNormalizedTime;

    private PlayerMoveState previousState = PlayerMoveState.Idle;
    private PlayerMoveState currentState = PlayerMoveState.Idle;
    private bool suppressHoldCheckOnce;

    public PlayerMoveState CurrentState => currentState;

    public PlayerAnimationDriver(Animator animator, float jumpFreezeNormalizedTime = 0.5f)
    {
        this.animator = animator;
        this.jumpFreezeNormalizedTime = jumpFreezeNormalizedTime;
    }

    public void ChangeState(PlayerMoveState newState)
    {
        if (animator == null)
            return;

        if (previousState == newState)
            return;

        animator.ResetTrigger(previousState.ToString());
        animator.SetTrigger(newState.ToString());

        previousState = newState;
        currentState = newState;
    }

    // 새로운 점프/낙하 이벤트가 시작될 때 항상 Jump 애니메이션을 처음부터 재생한다.
    // ChangeState()는 "코드상 상태 라벨이 이미 같으면 무시"하는 가드가 있어, 연타 등으로 착지·재점프가
    // Idle/Walk를 거치지 못한 채 연속 처리되면 SetTrigger가 건너뛰어져 애니메이터가 얼려뒀던 지점부터
    // 이어서 재생되는 버그가 있었다(Bug-fix-plan.md §15). ChangeState() 자체는 Idle/Walk/SneakWalk/
    // Dodge와 원격 동기화 등 다른 모든 호출부가 공유하므로 그쪽 가드는 그대로 두고, 점프만을 위한
    // 이 전용 메서드를 별도로 둬 항상 무조건 재트리거되게 한다.
    public void ReplayJump()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(previousState.ToString());
        animator.SetTrigger(PlayerMoveState.Jump.ToString());

        previousState = PlayerMoveState.Jump;
        currentState = PlayerMoveState.Jump;

        // SetTrigger는 다음 애니메이터 내부 평가(대략 다음 프레임)에야 실제로 반영된다. 그 사이
        // HandleJumpAnimationHold()가 "재트리거 이전"의 오래된 normalizedTime을 보고 즉시 다시
        // 얼려버리는 것을 막기 위해, 재생이 실제로 시작될 때까지 최소 한 번은 정지 판정을 건너뛴다
        // (Bug-fix-plan.md §16).
        suppressHoldCheckOnce = true;
    }

    // 착지 전에 Jump 애니메이션이 끝까지(착지 포즈까지) 재생되어 버리는 것을 막기 위해
    // 정점 부근에서 재생을 멈추고 공중 자세를 유지시킨다.
    public void HandleJumpAnimationHold()
    {
        if (animator == null || currentState != PlayerMoveState.Jump)
            return;

        if (suppressHoldCheckOnce)
        {
            suppressHoldCheckOnce = false;
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName("Jump"))
            return;

        if (animator.speed > 0f && state.normalizedTime >= jumpFreezeNormalizedTime)
        {
            animator.speed = 0f;
        }
    }

    public void ResumePlayback()
    {
        if (animator != null)
            animator.speed = 1f;
    }
}
