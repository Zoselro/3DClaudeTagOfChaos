using UnityEngine;

// 맵 바깥으로 떨어진 로컬 플레이어를 스폰 지점으로 되돌린다(PlayerControllPlan.md §18.4).
// 씬 하단(플레이 영역보다 한참 아래)에 이 컴포넌트가 붙은 큰 트리거 콜라이더를 배치해서 사용한다.
[RequireComponent(typeof(Collider))]
public class VoidKillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<HideOrSeekPlayer>();
        if (player != null && player.IsMine)
            player.RespawnToSpawnPoint();
    }
}
