using UnityEngine;

// 캐릭터(쿠키·괴물 공용) 지면·벽 접촉 질의(예전 이름 PlayerGroundDetector — 괴물도 쓰게 되어 Core로 옮기고 개명, Bug-fix-plan.md §41 D4).
// 조정자(MonoBehaviour)가 소유하는 순수 C# 클래스다. 중력 적분은 물리 엔진(Rigidbody)이 맡고, 이 클래스는 질의만 한다.
//
// 벽 접촉: 수평 속도를 매 물리 스텝 직접 대입하는 캐릭터가 공중에서 벽을 향해 키를 누르고 있으면, 벽을 미는 수직항력에 비례한
// 마찰이 중력을 이겨 벽에 붙어 버린다(Bug-fix-plan.md §41 ㊴). OnCollisionStay에서 받은 벽 법선으로 벽을 향한 속도 성분을
// 빼 주면 수직항력 자체가 생기지 않는다. 지면 마찰은 그대로 두므로 경사에 가만히 서 있어도 미끄러지지 않는다.
public class CharacterGroundDetector
{
    private const int MaxContacts = 16;

    private readonly LayerMask groundLayer;
    private readonly float checkDistance;
    private readonly float wallNormalMaxY; // 접촉 법선의 y가 이보다 작으면 벽(설 수 없는 면)으로 본다

    private readonly ContactPoint[] contactBuffer = new ContactPoint[MaxContacts];
    private readonly Vector3[] wallNormals = new Vector3[MaxContacts];
    private int wallCount;
    private float wallContactsTime = float.NegativeInfinity; // 벽 접촉을 기록한 물리 스텝의 Time.fixedTime

    // maxWalkableSlope: 이 각도(도)보다 가파른 접촉면은 벽으로 본다.
    public CharacterGroundDetector(LayerMask groundLayer, float checkDistance, float maxWalkableSlope = 50f)
    {
        this.groundLayer = groundLayer;
        this.checkDistance = checkDistance;
        wallNormalMaxY = Mathf.Cos(maxWalkableSlope * Mathf.Deg2Rad);
    }

    public bool IsGrounded(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * 0.1f;
        return Physics.Raycast(rayOrigin, Vector3.down, checkDistance + 0.1f, groundLayer);
    }

    // 몸 아래 지면을 구로 훑어 법선까지 얻는다(경사를 따라 이동하는 스킬용). origin에서 시작해 겹친 면은 잡지 않으므로
    // origin은 몸 안쪽(발바닥보다 위)에 두고 radius는 몸 반지름보다 조금 작게 준다. 트리거는 무시한다.
    public bool TryGetGround(Vector3 origin, float radius, float maxDistance, out RaycastHit hit)
    {
        return Physics.SphereCast(origin, radius, Vector3.down, out hit, maxDistance, groundLayer, QueryTriggerInteraction.Ignore);
    }

    // 캐릭터의 OnCollisionStay에서 호출한다. 같은 물리 스텝의 여러 충돌체 통지를 모으고, 새 스텝이면 이전 기록을 비운다.
    public void RecordContacts(Collision collision)
    {
        if (collision == null) return;
        if (!Mathf.Approximately(wallContactsTime, Time.fixedTime))
        {
            wallContactsTime = Time.fixedTime;
            wallCount = 0;
        }

        int count = collision.GetContacts(contactBuffer);
        for (int i = 0; i < count && wallCount < MaxContacts; i++)
        {
            Vector3 normal = contactBuffer[i].normal; // 상대 표면에서 이 캐릭터 쪽을 향한다
            if (normal.y >= wallNormalMaxY) continue; // 바닥·완만한 경사

            Vector3 horizontal = new Vector3(normal.x, 0f, normal.z);
            if (horizontal.sqrMagnitude < 1e-4f) continue;
            wallNormals[wallCount++] = horizontal.normalized;
        }
    }

    // 직전 물리 스텝에 닿아 있던 벽들을 향한 성분을 수평 속도에서 뺀다(벽을 따라 옆으로 미는 성분은 남는다).
    // 기록이 오래됐으면(벽에서 떨어짐) 그대로 돌려준다.
    public Vector3 RemoveWallPush(Vector3 horizontalVelocity)
    {
        if (Time.fixedTime - wallContactsTime > Time.fixedDeltaTime * 1.5f) return horizontalVelocity;

        for (int i = 0; i < wallCount; i++)
        {
            float into = Vector3.Dot(horizontalVelocity, wallNormals[i]);
            if (into < 0f) horizontalVelocity -= into * wallNormals[i];
        }
        return horizontalVelocity;
    }
}
