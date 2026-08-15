using UnityEngine;

// 물리 엔진(Rigidbody) 도입 이후: 중력 적분은 더 이상 이 클래스가 하지 않는다(PlayerControllPlan.md §18.3-2).
// 점프 여부와 무관하게 매 FixedUpdate 호출되는 순수 접지 여부 질의 클래스로 축소됨.
public class PlayerGroundDetector
{
    private readonly LayerMask groundLayer;
    private readonly float checkDistance;

    public PlayerGroundDetector(LayerMask groundLayer, float checkDistance)
    {
        this.groundLayer = groundLayer;
        this.checkDistance = checkDistance;
    }

    public bool IsGrounded(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * 0.1f;
        return Physics.Raycast(rayOrigin, Vector3.down, checkDistance + 0.1f, groundLayer);
    }
}
