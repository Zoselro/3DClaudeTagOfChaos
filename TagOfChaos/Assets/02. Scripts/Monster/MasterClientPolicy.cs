using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// GameLobbyScene·GameScene 배치. 방장을 항상 "괴물이 아닌 사람 중 입장 순서가 가장 빠른 사람"으로 맞춘다
// (사용자 결정, Bug-fix-plan.md §26.8 — 방 생성자 → 두 번째 입장자 → 세 번째 입장자 …, RoomState.DesiredMasterActor).
//
// - 괴물은 색칠 페이즈 동안 대기실에 남아 게임 진행 로직(방장 전용)을 돌릴 수 없으므로 방장이 될 수 없다(§23.3).
// - 판이 끝나 대기실로 돌아오면 괴물 확정이 초기화돼 방장이 자동으로 방 생성자(최소 ActorNumber)에게 돌아온다.
// - 방장이 나가면 서버가 새 방장을 정하는데, 누가 되든 이 정책으로 다시 수렴시킨다.
// PhotonNetwork.SetMasterClient는 현재 방장만 호출할 수 있으므로 정책 적용도 현재 방장이 한다.
// (예전 이름 MasterHandoffGuard — "방장이 괴물이면 넘긴다"만 하던 것을 일반화했다. .meta GUID는 유지해 씬 참조 보존.)
public class MasterClientPolicy : MonoBehaviourPunCallbacks
{
    private int requestedActor = -1; // 서버 응답(OnMasterClientSwitched) 전 같은 요청을 반복하지 않기 위한 기록
    private bool appliedOnEnter;

    private void Update()
    {
        // 씬 로드 직후 InRoom이 아직 false일 수 있어(Bug-fix-plan.md §12) 방에 들어온 첫 프레임에 한 번 적용한다.
        if (appliedOnEnter || !RoomState.IsInRoom()) return;
        appliedOnEnter = true;
        EnforcePolicy();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        requestedActor = -1;
        EnforcePolicy();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        EnforcePolicy();
    }

    // 새 입장자는 항상 가장 큰 번호라 방장이 바뀌지 않지만, 괴물 혼자 방에 있던 상태에서 첫 쿠키가 들어온 경우
    // (방장이 괴물) 넘겨줘야 한다.
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        EnforcePolicy();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(NetKeys.MonsterActorNumbers)) EnforcePolicy();
    }

    private void EnforcePolicy()
    {
        if (!PhotonNetwork.IsMasterClient || !RoomState.IsInRoom()) return;

        int desired = RoomState.DesiredMasterActor();
        if (desired < 0 || desired == PhotonNetwork.LocalPlayer.ActorNumber || desired == requestedActor) return;

        Player target = PhotonNetwork.CurrentRoom.GetPlayer(desired);
        if (target == null) return;

        requestedActor = desired;
        PhotonNetwork.SetMasterClient(target);
        Debug.Log($"[MasterClientPolicy] Master {PhotonNetwork.LocalPlayer.ActorNumber} -> {desired} (earliest joined non-monster).");
    }
}
