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

        // SetTrigger 대신 Play()로 즉시 하드컷한다. Any State→Jump 트랜지션은 m_CanTransitionToSelf=1
        // + m_TransitionDuration=0.1이라, 착지로 얼려뒀던 Jump 포즈가 풀리자마자 SetTrigger로
        // 재점프하면 즉시 컷되지 않고 "방금 풀린 중간 점프 포즈"와 새 Jump 시작 포즈를 0.1초간
        // 블렌딩해버려 점프가 중간 Time에서 시작되는 것처럼 보이는 버그가 있었다(Bug-fix-plan.md §18).
        // Play(stateName, layer, normalizedTime)는 트랜지션 그래프를 우회해 그 프레임에 즉시
        // normalizedTime=0으로 전환하므로 이 블렌딩 자체가 발생하지 않는다.
        animator.Play("Jump", 0, 0f);

        previousState = PlayerMoveState.Jump;
        currentState = PlayerMoveState.Jump;

        // Play()도 같은 프레임에는 아직 반영되지 않을 수 있어(다음 애니메이터 내부 평가에야 실제로
        // 반영됨), 그 사이 HandleJumpAnimationHold()가 "재생 전"의 오래된 normalizedTime을 보고
        // 즉시 다시 얼려버리는 것을 막기 위해, 재생이 실제로 시작될 때까지 최소 한 번은 정지 판정을
        // 건너뛴다(Bug-fix-plan.md §16).
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

    private int carryLayerIndex = -1;

    // Carry Avatar Mask 레이어 가중치 제어(GameRule.md §4.1) — PlayerAnimator.controller에
    // \"Carry\" 레이어가 아직 없으면 GetLayerIndex가 -1을 반환해 조용히 무시된다.
    public void SetCarryLayerWeight(float weight)
    {
        if (animator == null) return;
        if (carryLayerIndex < 0) carryLayerIndex = animator.GetLayerIndex("Carry");
        if (carryLayerIndex < 0) return;
        animator.SetLayerWeight(carryLayerIndex, weight);
    }

    
public void ResumePlayback()
    {
        if (animator != null)
            animator.speed = 1f;
    }
}
