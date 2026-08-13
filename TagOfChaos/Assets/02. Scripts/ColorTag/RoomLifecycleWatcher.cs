using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class RoomLifecycleWatcher : MonoBehaviourPunCallbacks
{
    private enum LeaveReason { None, Abnormal, NormalGameEnd }

    private LeaveReason leaveReason = LeaveReason.None;

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.RemoveCallbackTarget(this);
    }

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

    // 게임 정상 종료 20초 타이머 감지 (7.3)
    private void Update()
    {
        if (leaveReason != LeaveReason.None) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.GameEndTime, out object endObj)) return;

        double gameEndTime = (double)endObj;
        if (PhotonNetwork.Time < gameEndTime) return;

        leaveReason = LeaveReason.NormalGameEnd;
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        string targetScene = leaveReason == LeaveReason.NormalGameEnd ? "GameLobbyScene" : "LobbyScene";
        SceneManager.LoadScene(targetScene);
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
