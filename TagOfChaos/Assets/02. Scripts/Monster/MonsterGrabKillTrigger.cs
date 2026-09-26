using Photon.Pun;
using UnityEngine;

// Monster 프리팹에 부착. VoidKillZone.cs의 "OnTriggerEnter로 HideOrSeekPlayer를 찾아낸다" 패턴을
// 그대로 재사용한다(GameRule.md §4.4). 전방 약 3m 이내로 쿠키가 들어오면 자동 발동, 회피/이동으로
// 벗어날 수 없는 확정 처형이다(사용자 확인).
// 촉수 돌진(TentacleDash) 중에는 한 물리 스텝에 1m 넘게 움직여 트리거가 쿠키를 건너뛸 수 있으므로,
// MonsterController가 돌진 경로를 직접 훑어 같은 판정(TryGrabKill)을 호출한다(Bug-fix-plan.md §34).
[RequireComponent(typeof(Collider))]
public class MonsterGrabKillTrigger : MonoBehaviour
{
    [SerializeField] private PhotonView monsterPv;
    [SerializeField] private MonsterController monsterController; // GrabKill 애니메이션 트리거용

    // 쿨다운은 트리거 즉시 시작해 GrabKill 애니메이션 재생이 끝나는 시점까지 지속된다(사용자 확인,
    // GameRule.md v3.5) — ResetTrigger()는 MonsterController가 재생 시간 경과 후 호출한다.
    private bool onCooldown;
    private SphereCollider reachSphere;

    public bool IsOnCooldown => onCooldown;

    // 처형 판정 범위(월드 기준) — 돌진 경로 검사가 트리거와 같은 크기를 쓰도록 공개한다.
    public Vector3 ReachCenter => reachSphere != null ? transform.TransformPoint(reachSphere.center) : transform.position;
    public float ReachRadius
    {
        get
        {
            if (reachSphere == null) return 0f;
            Vector3 s = transform.lossyScale;
            return reachSphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
    }

    private void Awake()
    {
        reachSphere = GetComponent<SphereCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryGrabKill(other);
    }

    // 쿨다운 중에 들어와 트리거 안에 계속 머무는 쿠키는 OnTriggerEnter가 다시 오지 않으므로,
    // 쿨다운이 풀린 뒤에도 처형되도록 Stay에서도 검사한다(쿨다운 중에는 즉시 반환해 비용이 거의 없다).
    private void OnTriggerStay(Collider other)
    {
        TryGrabKill(other);
    }

    private void TryGrabKill(Collider other)
    {
        if (onCooldown) return;
        var cookie = other.GetComponentInParent<HideOrSeekPlayer>();
        if (cookie != null) TryGrabKill(cookie);
    }

    // 처형 판정의 단일 진입점(트리거·돌진 경로 공용). 처형을 시작했으면 true.
    public bool TryGrabKill(HideOrSeekPlayer cookie)
    {
        if (onCooldown || cookie == null) return false;
        if (!monsterPv.IsMine) return false; // 판정 시도는 괴물 소유 클라이언트만

        var cookiePv = cookie.GetComponent<PhotonView>();
        if (cookiePv == null || cookiePv.Owner == null) return false;

        // 이미 파괴된 쿠키(렌더러만 꺼진 시체)가 쿨다운을 소모하지 않도록 제외한다(research.md §8.11).
        if (RoomState.IsBroken(cookiePv.Owner)) return false;

        onCooldown = true;
        monsterController.PlayGrabKill(); // 상태 동기화로 전원에게 GrabKill 애니메이션 전파(진행 중인 돌진은 여기서 끊긴다)

        // 파괴 확정은 피해자 본인 클라이언트만 하므로(소유권 원칙) 소유자에게만 보낸다.
        cookiePv.RPC(HideOrSeekPlayer.RpcRequestGrabKill, cookiePv.Owner);
        return true;
    }

    // GrabKill 애니메이션 재생이 끝나는 시점에 MonsterController가 호출한다(확정, GameRule.md v3.5).
    public void ResetTrigger() => onCooldown = false;
}
