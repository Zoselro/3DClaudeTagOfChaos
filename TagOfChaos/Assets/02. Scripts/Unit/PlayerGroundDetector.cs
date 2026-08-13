using UnityEngine;

public class PlayerGroundDetector
{
    private readonly LayerMask groundLayer;
    private readonly float groundCheckOffset;
    private readonly float gravity;

    private float yVelocity;

    public float YVelocity => yVelocity;

    public PlayerGroundDetector(LayerMask groundLayer, float groundCheckOffset, float gravity = -9.81f)
    {
        this.groundLayer = groundLayer;
        this.groundCheckOffset = groundCheckOffset;
        this.gravity = gravity;
    }

    public void StartJump(float jumpPower)
    {
        yVelocity = jumpPower;
    }

    // 상승 중에는 착지 판정 없이 중력만 누적하고, 하강 중에는 이번 프레임에 떨어질 거리만큼
    // 스윕 레이캐스트를 쏴서 빠른 낙하 속도에서 얇은 바닥을 뚫고 지나가는 것을 방지한다.
    public bool Tick(Transform transform, float deltaTime, out float landedHeight)
    {
        landedHeight = transform.position.y;

        yVelocity += gravity * deltaTime;

        if (yVelocity >= 0f)
            return false;

        Vector3 rayOrigin = transform.position + Vector3.up * groundCheckOffset;
        float rayDist = groundCheckOffset + Mathf.Abs(yVelocity) * deltaTime;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDist, groundLayer))
        {
            landedHeight = hit.point.y;
            yVelocity = 0f;
            return true;
        }

        return false;
    }
}
