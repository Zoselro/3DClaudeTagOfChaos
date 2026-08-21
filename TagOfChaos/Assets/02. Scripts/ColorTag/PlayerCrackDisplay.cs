using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// PlayerColorVoteIndicator와 완전히 동일한 구조: 소유권과 무관하게 "이 캐릭터가 파괴됐는지"를
// 그 캐릭터 자신의 시각 효과로 반영한다(GameRule.md §4.4).
// 균열(hitCount==1) 개념은 완전히 폐기되어(사용자 확인, GameRule.md v3.6 — "GrabKill로만 할
// 거고 타격은 하지 않을 것"), 파괴(hitCount==2) 전용으로 단순화했다. hitCount는 이제 0 또는 2
// 두 값만 존재하므로 "몇 대 맞았는지"가 아니라 "파괴됐는지 여부"만 표시하면 된다.
public class PlayerCrackDisplay : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Renderer bodyRenderer; // PlayerPaintCanvas.bodyRenderer와 같은 렌더러를 인스펙터에서 동일하게 연결
    [SerializeField] private GameObject breakVfxPrefab; // 로컬 Instantiate

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != pv.Owner) return;
        if (!changedProps.ContainsKey(NetKeys.HitCount)) return;

        int hitCount = (int)targetPlayer.CustomProperties[NetKeys.HitCount];
        if (hitCount >= 2) PlayBreakEffect(); // hitCount==1은 도달 불가능해 분기 자체가 없음
    }

    private void PlayBreakEffect()
    {
        if (bodyRenderer != null)
            bodyRenderer.enabled = false; // 원본 메시를 감추고 파편 연출로 대체
        if (breakVfxPrefab != null)
            Instantiate(breakVfxPrefab, transform.position, transform.rotation); // 전원 로컬 재생, 정밀 동기화 불필요
    }
}
