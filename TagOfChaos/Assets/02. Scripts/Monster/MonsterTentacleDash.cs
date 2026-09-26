using UnityEngine;

// 순수 C# 클래스(Unity 생명주기 없음), Unit/ 도메인의 PlayerGroundDetector/PlayerAnimationDriver와
// 동일한 "조정자(MonoBehaviour)가 소유하는 협력 클래스" 스타일을 그대로 따른다(research.md §2.4).
// 쿨타임 15초, 사거리 20m 돌진 스킬(GameRule.md §4.3, 사용자 지정값). 수치는 GameSettingsSO의
// Monster Tentacle Dash 항목에서 조정한다(research.md §12 E7) — 괴물 종류가 늘면 설정만 나누면 된다.
public class MonsterTentacleDash
{
    private float cooldownTimer;
    private bool isDashing;
    private float dashTimer;
    private float dashDuration;
    private Vector3 dashDirection;
    private float actualDashDistance;

    public bool IsDashing => isDashing;

    public bool TryStartDash(Vector3 forward, Vector3 origin, LayerMask obstructionMask)
    {
        if (isDashing || cooldownTimer > 0f) return false;

        GameSettingsSO settings = GameSettings.Current;
        float distance = settings.TentacleDashDistance;
        float radius = settings.TentacleDashRadius;

        dashDirection = forward;
        actualDashDistance = distance;

        // 벽 등 장애물을 뚫고 지나가지 않도록 시작 시점에 사거리를 미리 클램프한다.
        if (Physics.SphereCast(origin, radius, forward, out RaycastHit hit, distance, obstructionMask))
            actualDashDistance = Mathf.Max(0f, hit.distance - radius);

        isDashing = true;
        dashDuration = settings.TentacleDashDuration;
        dashTimer = dashDuration;
        cooldownTimer = settings.TentacleDashCooldown;
        return true;
    }

    // 매 FixedUpdate 호출 — 이번 스텝에 이동해야 할 변위(delta)만 반환. 실제 위치 갱신은 호출부 책임.
    public Vector3 TickDash(float deltaTime)
    {
        if (!isDashing) return Vector3.zero;

        float step = (actualDashDistance / dashDuration) * deltaTime;
        dashTimer -= deltaTime;
        if (dashTimer <= 0f) isDashing = false;

        return dashDirection * step;
    }

    // 리스폰 등 순간이동 직후 남은 돌진이 계속 밀지 않도록 멈춘다. 쿨다운은 유지한다(Bug-fix-plan.md §30.4).
    public void Cancel()
    {
        isDashing = false;
        dashTimer = 0f;
    }

    public void TickCooldown(float deltaTime)
    {
        if (cooldownTimer > 0f) cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);
    }
}
