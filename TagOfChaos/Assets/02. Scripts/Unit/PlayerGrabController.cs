using Photon.Pun;
using UnityEngine;

// 쿠키↔쿠키 그랩/캐리 시작·해제(그랩버 측, GameRule.md §4.1). 소유권 이전 없이 carrySocket을
// 로컬로 추적하는 방식 — 들린 쿠키는 자기 자신의 PhotonView를 그대로 유지한 채, 자기 Update()에서
// 그랩버의 carrySocket 위치를 따라간다(HideOrSeekPlayer.FollowCarrier() 참고).
[RequireComponent(typeof(PhotonView))]
public class PlayerGrabController : MonoBehaviour
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private HideOrSeekPlayer self;
    [SerializeField] private Transform carrySocket; // 캐릭터 등/어깨 위치 — 그랩한 쿠키가 붙는 지점
    [SerializeField] private float grabRange = 1.5f;
    [SerializeField] private LayerMask cookieLayer;

    private HideOrSeekPlayer carriedPlayer;

    public Transform CarrySocket => carrySocket;

    private void Update()
    {
        if (!pv.IsMine) return;
        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (carriedPlayer == null) TryGrab();
        else Release();
    }

    private void TryGrab()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, grabRange, cookieLayer);
        foreach (var hit in hits)
        {
            var target = hit.GetComponentInParent<HideOrSeekPlayer>();
            if (target == null || target.gameObject == gameObject) continue;

            var targetPv = target.GetComponent<PhotonView>();
            if (targetPv == null || targetPv.Owner == null) continue;

            targetPv.RPC("OnGrabbedByOwner", targetPv.Owner, pv.ViewID);
            carriedPlayer = target;
            self?.SetCarryLayerWeight(1f);
            return;
        }
    }

    // withThrow: 놓아줄 때 살짝 앞으로 밀어낼지 여부(선택적 연출, 기본은 그냥 내려놓기)
    public void Release(bool withThrow = false)
    {
        if (carriedPlayer == null) return;

        var targetPv = carriedPlayer.GetComponent<PhotonView>();
        if (targetPv != null && targetPv.Owner != null)
            targetPv.RPC("OnReleased", targetPv.Owner, withThrow);

        carriedPlayer = null;
        self?.SetCarryLayerWeight(0f);
    }
}
