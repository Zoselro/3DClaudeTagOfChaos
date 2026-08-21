using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

using UnityEngine;

// 가마솥 트리거 — 쿠키가 들어오면 ClaimMonster 이벤트를 마스터에게 보낸다(GameRule.md §2.1).
// 선착순 자진 입장: 마스터(MonsterAssignmentAuthority)가 이미 확정됐으면 이후 요청은 전부 무시한다.
[RequireComponent(typeof(Collider))]
public class Cauldron : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<HideOrSeekPlayer>();
        if (player == null || !player.IsMine) return;

        object content = PhotonNetwork.LocalPlayer.ActorNumber;
        var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(NetEventCodes.ClaimMonster, content, options, SendOptions.SendReliable);
    }
}
