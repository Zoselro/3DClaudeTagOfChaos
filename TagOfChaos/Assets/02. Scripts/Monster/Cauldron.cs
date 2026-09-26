using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 가마솥 트리거 — 쿠키가 들어오면 ClaimMonster 이벤트를 마스터에게 보낸다(GameRule.md §2.1).
// 선착순 자진 입장: 마스터(MonsterAssignmentAuthority)가 이미 확정했으면 이후 요청은 전부 무시된다.
[RequireComponent(typeof(Collider))]
public class Cauldron : MonoBehaviour
{
    [SerializeField] private MonsterRevealController revealController; // 이미 확정된 경우 누가 괴물인지 다시 보여줌

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<HideOrSeekPlayer>();
        if (player == null || !player.IsMine) return;

        // 정원이 차기 전에는 아무 반응도 하지 않는다 — 신청도, 괴물 공지 재표시도 없다(Bug-fix-plan.md §30.2).
        // 예전에는 2명뿐인 방에서도 괴물이 확정돼 방 생성자가 괴물이 되면 방장이 넘어가 시작 버튼이 사라졌다.
        // 이미 안에 서 있다가 정원이 찬 경우는 다시 들어와야 신청된다(사용자 결정 §30.7-2).
        if (!RoomState.IsRoomFull()) return;

        // 이미 괴물이 정해졌으면 신청을 보내지 않고, 들어간 사람에게 누가 괴물인지 알려준다 — 예전에는 조용히
        // 무시돼 가마솥에 들어간 사람이 자기가 괴물이 된 줄 알았다(Bug-fix-plan.md §24.3 B3).
        if (RoomState.IsMonsterSelectionComplete() || RoomState.IsLocalMonster())
        {
            if (revealController != null) revealController.ShowAgain();
            return;
        }

        object content = PhotonNetwork.LocalPlayer.ActorNumber;
        var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(NetEventCodes.ClaimMonster, content, options, SendOptions.SendReliable);
    }
}
