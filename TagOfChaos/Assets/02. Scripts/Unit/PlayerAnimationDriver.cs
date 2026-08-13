using UnityEngine;

public class PlayerAnimationDriver
{
    private readonly Animator animator;
    private readonly float jumpFreezeNormalizedTime;

    private PlayerMoveState previousState = PlayerMoveState.Idle;
    private PlayerMoveState currentState = PlayerMoveState.Idle;

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

    // 착지 전에 Jump 애니메이션이 끝까지(착지 포즈까지) 재생되어 버리는 것을 막기 위해
    // 정점 부근에서 재생을 멈추고 공중 자세를 유지시킨다.
    public void HandleJumpAnimationHold()
    {
        if (animator == null || currentState != PlayerMoveState.Jump)
            return;

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
