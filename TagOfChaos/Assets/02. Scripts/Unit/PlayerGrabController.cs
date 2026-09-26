using System.Collections;
using Photon.Pun;
using UnityEngine;

// 쿠키↔쿠키 그랩/캐리 시작·해제(그랩버 측, GameRule.md §4.1). 소유권 이전 없이 carrySocket을
// 로컬로 추적하는 방식 — 들린 쿠키는 자기 자신의 PhotonView를 그대로 유지한 채, 자기 FixedUpdate()에서
// 그랩버의 carrySocket 위치를 따라간다(HideOrSeekPlayer.TryFollowCarrier() 참고).
[RequireComponent(typeof(PhotonView))]
public class PlayerGrabController : MonoBehaviour
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private HideOrSeekPlayer self;
    [SerializeField] private Transform carrySocket; // 캐릭터 등/어깨 위치 — 그랩한 쿠키가 붙는 지점
    [SerializeField] private float grabRange = 1.5f;
    [SerializeField] private LayerMask cookieLayer; // 프리팹에서 Cookie 레이어(쿠키 루트 캡슐)로 설정(research.md §8.6)

    // 내려놓은 직후에는 들렸던 쿠키의 원격 복사본이 아직 내 등(소켓) 위치에 겹쳐 있다 — 새 위치가 동기화되기
    // 전에 충돌을 되살리면 내 몸이 밀려나므로 잠시 뒤에 복원한다.
    private const float RestoreCollisionDelay = 0.5f;

    private HideOrSeekPlayer carriedPlayer;
    private readonly Collider[] overlapBuffer = new Collider[16]; // OverlapSphere 할당 제거

    public Transform CarrySocket => carrySocket;
    public bool IsCarrying => carriedPlayer != null;

    private void Update()
    {
        if (!pv.IsMine) return;

        // 들고 있던 쿠키가 파괴됐거나 방을 나가 사라졌으면 캐리 상태를 정리한다.
        if (carriedPlayer != null && (!carriedPlayer || IsOwnerBroken(carriedPlayer)))
            Release();

        if (!PlayerInput.GrabPressed) return;
        if (self != null && (self.IsBroken || self.IsMovementLocked)) return; // 파괴·들림·채팅 중에는 그랩 불가

        if (carriedPlayer == null) TryGrab();
        else Release();
    }

    private void TryGrab()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, grabRange, overlapBuffer, cookieLayer);
        for (int i = 0; i < count; i++)
        {
            var target = overlapBuffer[i].GetComponentInParent<HideOrSeekPlayer>();
            if (target == null || target.gameObject == gameObject) continue;
            if (IsOwnerBroken(target)) continue;

            var targetPv = target.GetComponent<PhotonView>();
            if (targetPv == null || targetPv.Owner == null) continue;

            targetPv.RPC(HideOrSeekPlayer.RpcOnGrabbedByOwner, targetPv.Owner, pv.ViewID);
            carriedPlayer = target;
            // 들고 있는 쿠키(이 클라이언트에서는 원격·키네마틱)가 내 몸을 밀어내지 않도록 서로의 충돌을 무시한다.
            SetCollisionIgnored(gameObject, target.gameObject, true);
            self?.SetCarryLayerWeight(1f);
            return;
        }
    }

    // withThrow: 놓아줄 때 살짝 앞으로 밀어낼지 여부(선택적 연출, 기본은 그냥 내려놓기)
    public void Release(bool withThrow = false)
    {
        if (carriedPlayer == null) return;

        if (carriedPlayer) // 방을 나가 파괴된 경우에는 RPC/충돌 복구를 건너뛴다
        {
            var targetPv = carriedPlayer.GetComponent<PhotonView>();
            if (targetPv != null && targetPv.Owner != null)
                targetPv.RPC(HideOrSeekPlayer.RpcOnReleased, targetPv.Owner, withThrow);
            StartCoroutine(RestoreCollisionLater(carriedPlayer.gameObject));
        }

        carriedPlayer = null;
        self?.SetCarryLayerWeight(0f);
    }

    private IEnumerator RestoreCollisionLater(GameObject released)
    {
        yield return new WaitForSeconds(RestoreCollisionDelay);
        if (released != null && carriedPlayer == null) // 그 사이 다시 들었다면 무시 상태를 유지한다
            SetCollisionIgnored(gameObject, released, false);
    }

    private static bool IsOwnerBroken(HideOrSeekPlayer player)
    {
        var targetPv = player.GetComponent<PhotonView>();
        return targetPv != null && targetPv.Owner != null
            && targetPv.Owner.CustomProperties.TryGetValue(NetKeys.HitCount, out object v)
            && v is int hitCount && hitCount >= 2;
    }

    // 두 캐릭터의 모든 콜라이더 쌍에 대해 충돌 무시 여부를 설정한다(들린 쪽/드는 쪽 양쪽에서 사용).
    public static void SetCollisionIgnored(GameObject a, GameObject b, bool ignore)
    {
        if (a == null || b == null) return;
        var aColliders = a.GetComponentsInChildren<Collider>();
        var bColliders = b.GetComponentsInChildren<Collider>();
        foreach (var ca in aColliders)
            foreach (var cb in bColliders)
                Physics.IgnoreCollision(ca, cb, ignore);
    }
}
