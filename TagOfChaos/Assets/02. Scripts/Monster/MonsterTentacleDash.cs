using UnityEngine;

// 순수 C# 클래스(Unity 생명주기 없음), CharacterGroundDetector/PlayerAnimationDriver와
// 동일한 "조정자(MonoBehaviour)가 소유하는 협력 클래스" 스타일을 그대로 따른다(research.md §2.4).
// 쿨타임 15초, 사거리 20m 돌진 스킬(GameRule.md §4.3, 사용자 지정값). 수치는 GameSettingsSO의
// Monster Tentacle Dash 항목에서 조정한다(research.md §12 E7) — 괴물 종류가 늘면 설정만 나누면 된다.
//
// 이동은 속도로 한다(Bug-fix-plan.md §41 ㊳). 예전에는 시작할 때 발 높이에서 수평으로 한 번 장애물을 재고, 매 스텝 수평으로
// 1.6m씩 MovePosition 했다 — 오르막에서는 순간이동이 오목 지형 면을 뚫고 지형 아래로 들어갔고(실측 14회 중 8회), 내리막에서는
// 지면을 떠나 최대 13m 허공으로 날아갔으며, 발 높이의 낮은 장식에 시작 검사가 걸리면 제자리에 멈췄다. 이제 매 스텝 지면 법선을
// 따라 방향을 꺾은 속도를 넘기고, 충돌·연속 충돌 판정은 물리 엔진이 맡는다. 벽에 막혀 거의 못 움직이면 이동을 끝낸다.
public class MonsterTentacleDash
{
    private const float BlockedProgressRatio = 0.3f; // 기대 이동량의 이 비율도 못 움직이면 막힌 스텝으로 본다
    private const int BlockedStepsToStop = 2;        // 막힌 스텝이 이만큼 이어지면 이동을 끝낸다(한 스텝짜리 모서리 걸림은 봐준다)

    private float cooldownTimer;
    private bool isDashing;
    private float dashTimer;
    private float dashSpeed;
    private Vector3 dashDirection; // 수평 단위 벡터
    private int blockedSteps;

    public bool IsDashing => isDashing;
    public float DashSpeed => dashSpeed;

    public bool TryStartDash(Vector3 forward)
    {
        if (isDashing || cooldownTimer > 0f) return false;

        Vector3 horizontal = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (horizontal.sqrMagnitude < 1e-6f) return false;

        GameSettingsSO settings = GameSettings.Current;
        dashDirection = horizontal.normalized;
        dashSpeed = settings.TentacleDashDistance / settings.TentacleDashDuration;
        dashTimer = settings.TentacleDashDuration;
        cooldownTimer = settings.TentacleDashCooldown;
        blockedSteps = 0;
        isDashing = true;
        return true;
    }

    // 이번 물리 스텝의 돌진 속도. 접지 중이고 경사가 maxSlope(도) 이하이면 지면을 따라 꺾는다(오르막은 위로, 내리막은 아래로).
    // 공중이거나 발밑이 더 가파르면 수평 방향 그대로 — 가파른 면은 물리 충돌로 막히고 ReportProgress가 이동을 끝낸다.
    public Vector3 DashVelocity(bool grounded, Vector3 groundNormal, float maxSlope)
    {
        if (!isDashing) return Vector3.zero;
        return DirectionOnGround(dashDirection, grounded, groundNormal, maxSlope) * dashSpeed;
    }

    // 네트워크·물리와 분리한 순수 계산(테스트용).
    public static Vector3 DirectionOnGround(Vector3 horizontalDirection, bool grounded, Vector3 groundNormal, float maxSlope)
    {
        if (!grounded || Vector3.Angle(groundNormal, Vector3.up) > maxSlope) return horizontalDirection;
        Vector3 along = Vector3.ProjectOnPlane(horizontalDirection, groundNormal);
        return along.sqrMagnitude > 1e-6f ? along.normalized : horizontalDirection;
    }

    // 매 FixedUpdate 호출 — 돌진 시간을 줄인다.
    public void TickDash(float deltaTime)
    {
        if (!isDashing) return;
        dashTimer -= deltaTime;
        if (dashTimer <= 0f) isDashing = false;
    }

    // 직전 스텝에 기대한 이동량과 실제 이동량을 알려 준다. 벽에 막혀 제자리에서 미는 동안 남은 시간을 쓰지 않게 끝낸다.
    public void ReportProgress(float expected, float actual)
    {
        if (!isDashing || expected <= 0f) return;
        if (actual < expected * BlockedProgressRatio)
        {
            if (++blockedSteps >= BlockedStepsToStop) Cancel();
        }
        else
        {
            blockedSteps = 0;
        }
    }

    // 리스폰·처형 등으로 남은 돌진이 계속 밀지 않도록 멈춘다. 쿨다운은 유지한다(Bug-fix-plan.md §30.4).
    public void Cancel()
    {
        isDashing = false;
        dashTimer = 0f;
        blockedSteps = 0;
    }

    public void TickCooldown(float deltaTime)
    {
        if (cooldownTimer > 0f) cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);
    }
}
