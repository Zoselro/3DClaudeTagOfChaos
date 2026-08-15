using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerColorVoteIndicator : MonoBehaviourPunCallbacks
{




    [SerializeField] private PhotonView pv;
    [SerializeField] private SpriteRenderer indicator;
    [SerializeField] private ColorPaletteSO palette;

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != pv.Owner) return;
        if (!changedProps.ContainsKey(NetKeys.VoteColorIndex)) return;

        int index = (int)targetPlayer.CustomProperties[NetKeys.VoteColorIndex];
        indicator.enabled = index >= 0;
        if (index >= 0)
            indicator.color = palette.GetColor(index);
    }

private void LateUpdate()
    {
        // 카메라를 향하도록 빌보드 처리 — 캐릭터 전체가 아니라 투표색 스프라이트(indicator)만 회전시킨다
        if (Camera.main != null && indicator != null)
            indicator.transform.forward = Camera.main.transform.forward;
    }
}
