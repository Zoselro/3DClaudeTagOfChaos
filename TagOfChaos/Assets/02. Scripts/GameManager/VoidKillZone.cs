using UnityEngine;

// 맵 바깥으로 떨어진 로컬 캐릭터(쿠키·괴물)를 스폰 지점으로 되돌린다(PlayerControllPlan.md §18.4, Bug-fix-plan.md §30.4).
// 씬 하단(플레이 영역보다 한참 아래)에 이 컴포넌트가 붙은 큰 트리거 콜라이더를 배치해서 사용한다.
// 캐릭터 종류는 IRespawnable로만 판단한다 — 예전에는 쿠키(HideOrSeekPlayer)만 찾아 괴물은 무시됐다.
[RequireComponent(typeof(Collider))]
public class VoidKillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var respawnable = other.GetComponentInParent<IRespawnable>();
        if (respawnable != null) FallGuard.TryRespawn(respawnable);
    }
}
