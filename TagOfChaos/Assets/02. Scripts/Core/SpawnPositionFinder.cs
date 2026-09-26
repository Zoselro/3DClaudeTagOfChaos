using UnityEngine;

// 스폰 지점 주변의 무작위 위치 중 다른 콜라이더(트리거 포함)와 겹치지 않는 곳을 고른다. 예전에는 ±5m를
// 무작위로 골라, GameLobbyScene에서 스폰 지점 3m 옆의 가마솥 트리거와 겹쳐 스폰되면 그 즉시 괴물 신청이
// 들어갔다(Play Mode 실측: 스폰 한 번에 약 3~4%, Bug-fix-plan.md §25 P10). PlayerSpawner(최초 스폰)와
// HideOrSeekPlayer.RespawnToSpawnPoint(낙하 리스폰)가 함께 쓴다.
public static class SpawnPositionFinder
{
    private const int MaxAttempts = 16;
    private const float CapsuleRadius = 0.5f;  // 쿠키 루트 캡슐 반지름(0.46) + 여유
    private const float CapsuleBottom = 0.6f;  // 지면(바닥 콜라이더)에 닿지 않도록 띄운 캡슐 하단 높이
    private const float CapsuleTop = 1.6f;

    private static readonly Collider[] OverlapBuffer = new Collider[8];

    public static Vector3 FindClearPosition(Vector3 center, float range)
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            Vector3 candidate = center + new Vector3(Random.Range(-range, range), 0f, Random.Range(-range, range));
            if (IsClear(candidate)) return candidate;
        }
        return center; // 모든 후보가 막혀 있으면 스폰 지점 자체를 쓴다(씬에서 스폰 지점은 비워 두는 전제)
    }

    private static bool IsClear(Vector3 position)
    {
        int count = Physics.OverlapCapsuleNonAlloc(
            position + Vector3.up * CapsuleBottom,
            position + Vector3.up * CapsuleTop,
            CapsuleRadius,
            OverlapBuffer,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide); // 가마솥·VoidKillZone 같은 트리거도 피한다
        return count == 0;
    }
}
