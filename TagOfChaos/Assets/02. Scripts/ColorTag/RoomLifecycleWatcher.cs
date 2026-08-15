using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class RoomLifecycleWatcher : MonoBehaviourPunCallbacks
{
    private enum LeaveReason { None, Abnormal }

    private LeaveReason leaveReason = LeaveReason.None;





    // 술래 퇴장 / 인원 부족 감지 → 즉시 종료 (7.2)
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (leaveReason != LeaveReason.None) return; // 이미 종료 처리 중

        bool onlyOnePlayerLeft = PhotonNetwork.PlayerList.Length <= 1;
        bool taggerLeft = IsTagger(otherPlayer);

        if (onlyOnePlayerLeft || taggerLeft)
        {
            leaveReason = LeaveReason.Abnormal;
            PhotonNetwork.LeaveRoom();
        }
    }

    // 게임 정상 종료 20초 타이머 감지 (7.3) → 방을 유지한 채 대기실로 복귀 (0.2)
    private void Update()
    {
        if (leaveReason != LeaveReason.None) return; // 비정상 종료(7.2) 처리 중이면 건너뜀
        if (!PhotonNetwork.IsMasterClient) return; // 씬 전환은 마스터만 트리거 (ColorSelectionManager와 동일 패턴)
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.GameEndTime, out object endObj)) return;

        double gameEndTime = (double)endObj;
        if (PhotonNetwork.Time < gameEndTime) return;

        ReturnToGameLobby();
    }

    // 방은 나가지 않고, 같은 방을 그대로 유지한 채 대기실로 되돌아간다
private void ReturnToGameLobby()
    {
        var props = new Hashtable
        {
            { NetKeys.GameEndTime, null }, // 다음 게임에서 같은 조건이 또 걸리지 않도록 제거
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        PhotonNetwork.CurrentRoom.IsOpen = true; // 게임 시작 시 닫아뒀던 걸 다시 연다

        PhotonNetwork.LoadLevel(SceneNames.GameLobby); // AutomaticallySyncScene으로 전원 함께 이동
    }

    // OnLeftRoom은 이제 비정상 종료(7.2)에서만 호출된다
public override void OnLeftRoom()
    {
        SceneManager.LoadScene(SceneNames.Lobby);
    }

    private bool IsTagger(Player player)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return false;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(NetKeys.TaggerActorNumber, out object tagger)) return false;

        int taggerActorNumber = (int)tagger;
        return taggerActorNumber >= 0 && taggerActorNumber == player.ActorNumber;
    }
}
