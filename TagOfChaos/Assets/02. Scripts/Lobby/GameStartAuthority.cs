using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 판 시작 절차의 단일 실행 지점(Bug-fix-plan.md §33 A안). 시작 버튼은 호스트(RoomState.HostActor — 괴물 여부와
// 무관하게 가장 먼저 들어온 사람)에게 보이지만, 실제 시작은 게임 진행 권한을 가진 Photon 방장(괴물이 아닌 사람,
// MasterClientPolicy)이 실행한다. 호스트가 방장이 아니면 시작 요청 이벤트(NetEventCodes.StartGameRequest)를 방장에게
// 보내고, 방장이 이 클래스로 요청자·조건을 다시 확인해 실행한다.
//
// 예전에는 시작 버튼 주인과 Photon 방장이 같은 개념이라, 방을 만든 사람이 괴물이 되면(괴물은 진행 권한을 가질 수
// 없음) 시작 버튼까지 두 번째 입장자에게 넘어갔다(㉙).
public static class GameStartAuthority
{
    public enum Result { Started, NotMaster, NotHost, NotReady, AlreadyStarted }

    // 방장이 시작을 연달아 처리하지 않도록 막는 간격 — PaintPhaseEndTime은 서버 응답 후에야 로컬에 보이므로
    // (Room Props 반영 지연) 그 사이 도착한 중복 요청을 이 시간으로 거른다.
    private const float DuplicateGuardSeconds = 3f;
    private static float lastStartTime = float.NegativeInfinity;

    // 시작 조건: 정원이 모두 찼고(사용자 결정 D1), 이번 판에 필요한 괴물이 모두 정해졌다(괴물 선정은 GameLobbyScene에만
    // 있으므로, 괴물 없이 시작하면 GameScene에서 괴물이 영영 정해지지 않는다).
    public static bool IsReady() => RoomState.IsRoomFull() && RoomState.IsMonsterSelectionComplete();

    // 호스트 화면에서 호출: 내가 진행 권한을 가진 방장이면 바로 실행하고, 아니면 방장에게 요청을 보낸다.
    public static void RequestStart()
    {
        if (!RoomState.IsInRoom() || PhotonNetwork.LocalPlayer == null) return;

        if (IsIntendedMaster())
        {
            Result result = TryStart(PhotonNetwork.LocalPlayer.ActorNumber);
            Debug.Log($"[GameStart] Local start: {result}");
            return;
        }

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(NetEventCodes.StartGameRequest, null, options, SendOptions.SendReliable);
        Debug.Log($"[GameStart] Start requested to master (actor {PhotonNetwork.CurrentRoom.MasterClientId}).");
    }

    // 방장 쪽 실행. 요청자가 호스트인지, 조건을 만족하는지 다시 확인한다.
    public static Result TryStart(int requesterActor)
    {
        // 방장이 괴물로 정해져 진행 권한이 곧 넘어갈 순간(서버 응답 전)에는 실행하지 않는다 — 괴물 클라이언트가 씬을
        // 넘기면 괴물이 대기실에 남지 못한다(§26.8.3 ②). 호스트 화면의 버튼은 잠시 뒤 다시 눌러 새 방장에게 보낸다.
        if (!IsIntendedMaster()) return Result.NotMaster;
        if (requesterActor != RoomState.HostActor()) return Result.NotHost;
        if (RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out _) || Time.unscaledTime - lastStartTime < DuplicateGuardSeconds)
            return Result.AlreadyStarted;
        if (!IsReady()) return Result.NotReady;

        lastStartTime = Time.unscaledTime;
        GameSettingsSO settings = GameSettings.Current;

        // 색칠 종료 시각을 GameScene이 아니라 여기서 먼저 기록한다 — 대기실에 남는 괴물
        // (MonsterLobbyWaitController)이 이 값을 시작 신호 겸 카운트다운 기준으로 쓰므로, 반드시 아래
        // LoadLevel(= curScn 변경)보다 먼저 보내야 한다(같은 클라이언트의 Props 변경은 보낸 순서대로 도착).
        // 판마다 새 값을 쓰므로 이전 판의 값이 남아 색칠 페이즈가 시작되지 않던 문제도 함께 막는다.
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.PaintPhaseEndTime, PhotonNetwork.Time + settings.SceneLoadGrace + settings.PaintPhaseDuration },
        });

        PhotonNetwork.CurrentRoom.IsOpen = false; // 씬 전환 도중 새로 입장하는 걸 방지(대기실 복귀 시 RoundStateResetter가 다시 연다)
        RoomSceneTransition.LoadLevelForRoom(SceneNames.Game); // 방장만 호출, 나머지는 AutomaticallySyncScene으로 함께 이동
        return Result.Started;
    }

    // 지금 Photon 방장이고, 방장 정책상 진행 권한을 가져야 하는 사람(괴물이 아닌 최선 입장자)도 나인지.
    private static bool IsIntendedMaster() =>
        PhotonNetwork.IsMasterClient && RoomState.DesiredMasterActor() == PhotonNetwork.LocalPlayer.ActorNumber;
}
