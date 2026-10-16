using Photon.Pun;
using TMPro;
using UnityEngine;

// 캐릭터 머리 위 닉네임 빌보드. 이 스크립트 자신이 빌보드 자식 오브젝트에 직접 붙어
// this.transform을 다루므로, PlayerColorVoteIndicator가 겪었던 "간접 참조 대상을 잘못 회전시키는"
// 버그(Bug-fix-plan.md §10)가 구조적으로 재발할 수 없다.
public class PlayerBillBoard : MonoBehaviour
{
    [SerializeField] private PhotonView pv; // 부모(캐릭터 루트)의 PhotonView
    [SerializeField] private TextMeshPro nameText;

    private void Start()
    {
        if (pv != null && pv.Owner != null)
            nameText.text = pv.Owner.NickName;
    }

    private const float VisibilityCheckSeconds = 0.25f;
    private float nextVisibilityCheck;

    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextVisibilityCheck)
        {
            nextVisibilityCheck = Time.unscaledTime + VisibilityCheckSeconds;
            bool show = ShouldShow();
            if (nameText.enabled != show) nameText.enabled = show;
        }
        if (nameText.enabled && Camera.main != null)
            transform.forward = Camera.main.transform.forward;
    }

    // 판 중 괴물 화면에는 쿠키·스파이 이름표가 보이지 않는다(괴물끼리는 보인다, §26 H6). 대기실에서는 모두 보인다.
    private bool ShouldShow()
    {
        if (!EscapeManager.IsActive || !RoomState.IsLocalMonster()) return true;
        return pv != null && pv.Owner != null && RoomState.IsMonster(pv.Owner.ActorNumber);
    }
}
