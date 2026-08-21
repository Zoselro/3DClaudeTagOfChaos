using Photon.Pun;
using UnityEngine;

// Monster 프리팹에 부착. VoidKillZone.cs(research.md §2.3)의 "OnTriggerEnter로 HideOrSeekPlayer를
// 찾아낸다" 패턴을 그대로 재사용한다(GameRule.md §4.4). 전방 3m 이내로 쿠키가 들어오면 자동 발동,
// 회피/이동으로 벗어날 수 없는 확정 처형이다(사용자 확인).
[RequireComponent(typeof(Collider))]
public class MonsterGrabKillTrigger : MonoBehaviour
{
    [SerializeField] private PhotonView monsterPv;
    [SerializeField] private MonsterController monsterController; // GrabKill 애니메이션 트리거용

    // 쿨다운은 트리거 즉시 시작해 GrabKill 애니메이션 재생이 끝나는 시점까지 지속된다(사용자 확인,
    // GameRule.md v3.5) — ResetTrigger()는 MonsterController가 애니메이션 재생 완료를 감지해 호출한다.
    private bool onCooldown;

    private void OnTriggerEnter(Collider other)
    {
        if (!monsterPv.IsMine) return; // 판정 시도는 괴물 소유 클라이언트만
        if (onCooldown) return;

        var cookie = other.GetComponentInParent<HideOrSeekPlayer>();
        if (cookie == null) return;

        onCooldown = true;
        monsterController.PlayGrabKill(); // MonsterNetworkSync를 통해 전원에게 GrabKill 애니메이션 전파

        cookie.GetComponent<PhotonView>().RPC("RequestGrabKill", RpcTarget.All);
    }

    // GrabKill 애니메이션 재생이 끝나는 시점에 MonsterController가 호출한다(확정, GameRule.md v3.5).
    public void ResetTrigger() => onCooldown = false;
}
