using UnityEngine;

// 순수 C# 클래스(Unity 생명주기 없음), Unit/ 도메인의 PlayerGroundDetector/PlayerAnimationDriver와
// 동일한 "조정자(MonoBehaviour)가 소유하는 협력 클래스" 스타일을 그대로 따른다(research.md §2.4).
// 쿨타임 15초, 사거리 20m 돌진 스킬(GameRule.md §4.3, 사용자 지정값).
public class MonsterTentacleDash
{
    private const float DashDistance = 20f;
    private const float DashDuration = 0.25f; // 20m를 0.25초에 주파 = 80m/s
    private const float CooldownDuration = 15f;
    private const float DashRadius = 0.4f; // SphereCast 반경(캐릭터 대략 두께)

    private float cooldownTimer;
    private bool isDashing;
    private float dashTimer;
    private Vector3 dashDirection;
    private float actualDashDistance;

    public bool IsDashing => isDashing;
    public float CooldownRemaining01 => Mathf.Clamp01(cooldownTimer / CooldownDuration); // 쿨다운 게이지 UI용

    public bool TryStartDash(Vector3 forward, Vector3 origin, LayerMask obstructionMask)
    {
        if (isDashing || cooldownTimer > 0f) return false;

        dashDirection = forward;
        actualDashDistance = DashDistance;

        // 벽 등 장애물을 뚫고 지나가지 않도록 시작 시점에 사거리를 미리 클램프한다.
        if (Physics.SphereCast(origin, DashRadius, forward, out RaycastHit hit, DashDistance, obstructionMask))
            actualDashDistance = Mathf.Max(0f, hit.distance - DashRadius);

        isDashing = true;
        dashTimer = DashDuration;
        cooldownTimer = CooldownDuration;
        return true;
    }

    // 매 FixedUpdate 호출 — 이번 스텝에 이동해야 할 변위(delta)만 반환. 실제 위치 갱신은 호출부 책임.
    public Vector3 TickDash(float deltaTime)
    {
        if (!isDashing) return Vector3.zero;

        float step = (actualDashDistance / DashDuration) * deltaTime;
        dashTimer -= deltaTime;
        if (dashTimer <= 0f) isDashing = false;

        return dashDirection * step;
    }

    public void TickCooldown(float deltaTime)
    {
        if (cooldownTimer > 0f) cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);
    }
}
