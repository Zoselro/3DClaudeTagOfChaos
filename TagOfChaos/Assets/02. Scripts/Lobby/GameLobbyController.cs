using System.Linq;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 대기실 패널: 참가자 목록, 상태 문구(대기 사유), 호스트 전용 시작 버튼.
// 시작 버튼 주인(호스트 = 가장 먼저 들어온 사람, 괴물 여부 무관)과 게임 진행 권한(Photon 방장 = 괴물이 아닌 사람)을
// 분리했다(Bug-fix-plan.md §33 A안). 실제 시작 절차는 GameStartAuthority가 방장 쪽에서 실행한다.
public class GameLobbyController : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerListItem playerListItemPrefab;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button startGameButton;

    // 상태 문구는 상황별로 인스펙터에서 입력한다(코드에 한글 금지). 예전에는 "4 / 4 대기 중" 하나뿐이라, 정원이
    // 찼는데도 시작 버튼이 꺼져 있는 이유(괴물 선정 대기)를 알 수 없었다(Bug-fix-plan.md §26.4 S1).
    [Header("Status Messages (set in inspector)")]
    [SerializeField] private string waitingPlayersFormat = "{0} / {1} waiting for players";          // {0}=인원, {1}=정원
    [SerializeField] private string selectingMonsterFormat = "Choosing the monster... enter the cauldron or wait {0}s"; // {0}=남은 초
    [SerializeField] private string selectingMonsterNoTimerText = "Choosing the monster... enter the cauldron";
    [SerializeField] private string readyForMasterFormat = "Monster: {0} - you can start the game";   // {0}=괴물 닉네임 목록
    [SerializeField] private string readyForOthersFormat = "Monster: {0} - waiting for host {1} to start"; // {1}=방장 닉네임

    // 시작 요청 후 방장 응답(씬 전환)을 기다리는 동안 버튼을 잠시 막는다 — 방장 교체 순간에 요청이 무시됐으면 다시 누를 수 있다.
    private const float StartRequestCooldown = 2f;
    private float startRequestPendingUntil = float.NegativeInfinity;
    private bool wasStartRequestPending;

    private int lastKnownPlayerCount = -1;
    private int lastKnownHostActor = -1;
    private int lastShownSeconds = int.MinValue;
    private readonly StringBuilder nameBuilder = new StringBuilder();

    private void Start()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return; // 방에 들어오지 않은 채 씬만 단독 실행된 경우 방어

        RefreshPlayerList();
        RefreshStartButton();
    }

    // AutomaticallySyncScene으로 뒤따라 입장하는 클라이언트는 Start() 시점에 룸 상태(인원수/방장 여부)를
    // 아직 완전히 따라잡지 못했을 수 있다 — 화면 값이 실제 값과 달라지면 다음 프레임에 스스로 바로잡는
    // 안전망. 괴물 선정 남은 시간은 초가 바뀔 때만 문구를 갱신한다.
    private void Update()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;

        int currentCount = PhotonNetwork.CurrentRoom.PlayerCount;
        int currentHost = RoomState.HostActor();

        if (currentCount != lastKnownPlayerCount || currentHost != lastKnownHostActor)
        {
            lastKnownPlayerCount = currentCount;
            lastKnownHostActor = currentHost;
            RefreshPlayerList();
            RefreshStartButton();
        }

        bool isPending = IsStartRequestPending();
        if (isPending != wasStartRequestPending)
        {
            wasStartRequestPending = isPending;
            RefreshStartButton(); // 요청 대기가 끝나면 버튼을 다시 활성화
        }

        if (RoomState.TryGetDouble(NetKeys.MonsterSelectDeadline, out double deadline))
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt((float)(deadline - PhotonNetwork.Time)));
            if (seconds != lastShownSeconds) RefreshStatus();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerList();
        RefreshStartButton();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerList();
        RefreshStartButton();
    }

    // 진행 권한(Photon 방장)이 바뀌어도 시작 버튼 주인(호스트)은 그대로다. 상태 문구만 갱신한다.
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshStartButton();
    }

    // 방장 쪽: 호스트의 시작 요청을 받아 GameStartAuthority로 실행한다(요청자·조건은 그 안에서 다시 확인).
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.StartGameRequest) return;
        if (!PhotonNetwork.IsMasterClient) return;
        GameStartAuthority.Result result = GameStartAuthority.TryStart(photonEvent.Sender);
        Debug.Log($"[GameStart] Start request from actor {photonEvent.Sender}: {result}");
    }

    // 괴물 확정 여부와 선정 기준 시각이 시작 조건·상태 문구에 들어가므로 바뀌는 순간 갱신한다 — Update()의
    // 안전망은 인원수/방장 여부만 감시해 Room Props 변화는 잡지 못한다.
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(NetKeys.MonsterActorNumbers)
            || propertiesThatChanged.ContainsKey(NetKeys.MonsterSelectDeadline))
            RefreshStartButton();
    }

    private void RefreshPlayerList()
    {
        foreach (Transform child in playerListContent) Destroy(child.gameObject);

        foreach (Player p in PhotonNetwork.PlayerList.OrderBy(pl => pl.ActorNumber)) // 입장 순서대로 표시
        {
            var item = Instantiate(playerListItemPrefab, playerListContent);
            item.SetNickname(p.NickName);
        }
    }

    // 시작 버튼은 호스트에게만 보이고(사용자 요구: "시작 버튼이 보이는 사람은 항상 방장(방을 만든 사람)"), 시작 조건을
    // 만족할 때만 눌린다(회색/흰색 전환은 Button 컴포넌트가 자동 처리). 호스트가 괴물이어도 버튼은 그대로다(§33).
    private void RefreshStartButton()
    {
        bool isHost = RoomState.IsLocalHost();
        startGameButton.gameObject.SetActive(isHost);
        if (isHost) startGameButton.interactable = CanStartGame();
        RefreshStatus();
    }

    private static bool IsRoomFull() => RoomState.IsRoomFull();

    private bool IsStartRequestPending() => Time.unscaledTime < startRequestPendingUntil;

    private bool CanStartGame() => GameStartAuthority.IsReady() && !IsStartRequestPending();

    private void RefreshStatus()
    {
        if (statusText == null || !RoomState.IsInRoom()) return;
        Room room = PhotonNetwork.CurrentRoom;

        if (!IsRoomFull())
        {
            lastShownSeconds = int.MinValue;
            statusText.text = string.Format(waitingPlayersFormat, room.PlayerCount, room.MaxPlayers);
            return;
        }

        if (!RoomState.IsMonsterSelectionComplete())
        {
            if (RoomState.TryGetDouble(NetKeys.MonsterSelectDeadline, out double deadline))
            {
                lastShownSeconds = Mathf.Max(0, Mathf.CeilToInt((float)(deadline - PhotonNetwork.Time)));
                statusText.text = string.Format(selectingMonsterFormat, lastShownSeconds);
            }
            else
            {
                statusText.text = selectingMonsterNoTimerText;
            }
            return;
        }

        lastShownSeconds = int.MinValue;
        string monsterNames = BuildMonsterNames();
        if (RoomState.IsLocalHost())
        {
            statusText.text = string.Format(readyForMasterFormat, monsterNames);
            return;
        }

        // 시작할 사람 = 호스트(가장 먼저 들어온 사람).
        statusText.text = string.Format(readyForOthersFormat, monsterNames, DisplayName(room.GetPlayer(RoomState.HostActor())));
    }

    // 닉네임이 비어 있으면(오프라인 테스트 등) 입장 번호로 대신 표시한다.
    private static string DisplayName(Player player)
    {
        if (player == null) return "-";
        return string.IsNullOrEmpty(player.NickName) ? "#" + player.ActorNumber : player.NickName;
    }

    private string BuildMonsterNames()
    {
        nameBuilder.Clear();
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return string.Empty;
        foreach (int actor in monsters)
        {
            Player p = PhotonNetwork.CurrentRoom.GetPlayer(actor);
            if (p == null) continue;
            if (nameBuilder.Length > 0) nameBuilder.Append(", ");
            nameBuilder.Append(DisplayName(p));
        }
        return nameBuilder.ToString();
    }

    // 호스트가 "게임 시작" 버튼을 눌렀을 때 (UI Button.onClick에 연결). 진행 권한을 가진 방장이면 바로 시작하고,
    // 아니면 방장에게 요청한다(GameStartAuthority).
    public void OnStartGameButtonClicked()
    {
        if (!RoomState.IsLocalHost() || !CanStartGame()) return; // 방어적 재확인

        startRequestPendingUntil = Time.unscaledTime + StartRequestCooldown;
        RefreshStartButton();
        GameStartAuthority.RequestStart();
    }
}
