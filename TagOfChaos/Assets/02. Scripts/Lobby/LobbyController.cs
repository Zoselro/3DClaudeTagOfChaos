using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyController : MonoBehaviourPunCallbacks
{
    private const string GameVersion = "1"; // 빌드가 바뀌면 올려서 이전 버전 클라이언트와 매치메이킹이 섞이지 않게 함

    [SerializeField] private TMP_InputField userIdInput;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private RoomListItem roomListItemPrefab;
    [SerializeField] private Button refreshButton;

    [Header("Room Settings (EscapePlan.md §1.7)")]
    [SerializeField] private RoomSettingField playersField;
    [SerializeField] private RoomSettingField timeLimitField;
    [SerializeField] private RoomSettingField timeAttackField;

    [Header("Region (EscapeVisualPlan.md §1.1)")]
    [Tooltip("로비에서 볼 지역 — 고르면 그 지역으로 다시 접속해 그 지역의 방 목록을 보여준다")]
    [SerializeField] private RegionSelector viewRegion;
    [Tooltip("방을 만들 지역 — 지금 접속한 지역과 다르면 다시 접속한 뒤 방을 만든다")]
    [SerializeField] private RegionSelector createRegion;

    [Header("Feedback Messages (set in inspector)")] // 코드에 한글을 넣지 않는 프로젝트 규칙 — 표시 문구는 프리팹에서 입력
    [SerializeField] private string enterRoomNameMessage = "Enter a room name.";
    [SerializeField] private string enterUserIdMessage = "Enter a user ID.";
    [SerializeField] private string roomNameExistsMessage = "A room with this name already exists.";
    [SerializeField] private string noRoomAvailableMessage = "No room is available to join.";
    [SerializeField] private string cannotJoinRoomMessage = "Cannot join this room.";

    private readonly Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();
    private readonly Dictionary<string, RoomListItem> roomListItems = new Dictionary<string, RoomListItem>();

    // 방 목록 새로고침(GameFixPlan.md F3). OnRoomListUpdate는 변경분만 주므로 한 번 놓치면 그 방이 다음 변경 때까지
    // 보이지 않는다. Photon은 로비에 새로 들어올 때 전체 목록을 다시 보내므로, 로비를 나갔다 다시 들어간다.
    // 연타로 로비를 계속 드나들지 않도록 잠시 버튼을 막는다.
    private const float RefreshCooldown = 2f;
    private float refreshReadyTime;
    private bool rejoinLobbyAfterLeave;

    // 지역 바꾸기: 끊고(OnDisconnected) → targetRegion으로 다시 접속. 다른 지역에 방을 만들 때는 접속 뒤 바로 만든다.
    private string targetRegion;
    private bool reconnectAfterDisconnect;
    private string pendingRoomName;
    private RoomOptions pendingRoomOptions;

    private static string CurrentRegion => PhotonNetwork.IsConnected ? PhotonRegions.Normalize(PhotonNetwork.CloudRegion) : null;

    private void Awake()
    {
        // #Critical: LoadLevel()이 방 전체에 씬 전환을 자동 동기화하게 함 (PunBasics-Tutorial 관례)
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = GameVersion;
    }


    private void Start()
    {
        userIdInput.text = "Player" + Random.Range(1000, 10000); // 기본값, 직접 수정 가능

        // 지역을 고르지 않고 접속하면 PC마다 핑이 좋은 지역에 따로 붙어 서로의 방이 보이지 않는다(문제 2). 늘 고른 지역으로 접속한다.
        // 지역 선택 UI가 없으면(지금 기본 구성) 모두 기본 지역(한국)에서 만난다 — 각자 다른 지역에 붙으면 방이 안 보인다.
        string region = viewRegion != null ? CurrentRegion ?? PhotonRegions.Saved : PhotonRegions.Default;
        if (viewRegion != null)
        {
            viewRegion.SetCode(region);
            viewRegion.Changed += OnViewRegionChanged;
        }
        if (createRegion != null) createRegion.SetCode(region);

        PhotonNetwork.SerializationRate = GameSettings.Current.CharacterSyncRate; // 캐릭터 동기화 빈도(research.md §12.4)
        SwitchRegion(region);
    }

    private void OnDestroy()
    {
        if (viewRegion != null) viewRegion.Changed -= OnViewRegionChanged;
    }

    private void OnViewRegionChanged(string code)
    {
        PhotonRegions.Saved = code;
        if (createRegion != null) createRegion.SetCode(code);
        SwitchRegion(code);
    }

    // 이미 그 지역에 붙어 있으면 로비만 들어가고, 아니면 끊고 다시 접속한다.
    private void SwitchRegion(string region)
    {
        targetRegion = region;
        if (PhotonNetwork.IsConnected && CurrentRegion == region)
        {
            if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby && !PhotonNetwork.InRoom) PhotonNetwork.JoinLobby();
            return;
        }
        cachedRoomList.Clear();
        ClearRoomListView();
        if (PhotonNetwork.IsConnected)
        {
            reconnectAfterDisconnect = true;
            PhotonNetwork.Disconnect();
        }
        else
        {
            ConnectToRegion(region);
        }
    }

    // 설정 에셋은 건드리지 않고 사본에 지역만 넣어 접속한다(ConnectToRegion은 PUN에서 사용 중단 예정).
    private static void ConnectToRegion(string region)
    {
        var settings = new AppSettings();
        PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(settings);
        settings.FixedRegion = region;
        PhotonNetwork.ConnectUsingSettings(settings);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (!reconnectAfterDisconnect) return;
        reconnectAfterDisconnect = false;
        ConnectToRegion(targetRegion);
    }

    private void Update()
    {
        if (refreshButton == null) return;
        bool connected = PhotonNetwork.IsConnectedAndReady && !reconnectAfterDisconnect;
        bool ready = Time.unscaledTime >= refreshReadyTime && connected && !rejoinLobbyAfterLeave;
        if (refreshButton.interactable != ready) refreshButton.interactable = ready;
        if (viewRegion != null) viewRegion.SetInteractable(connected && pendingRoomName == null);
    }

    public void OnRefreshButtonClicked()
    {
        if (Time.unscaledTime < refreshReadyTime || !PhotonNetwork.IsConnectedAndReady || rejoinLobbyAfterLeave) return;
        refreshReadyTime = Time.unscaledTime + RefreshCooldown;

        if (PhotonNetwork.InLobby)
        {
            rejoinLobbyAfterLeave = true;
            PhotonNetwork.LeaveLobby(); // OnLeftLobby에서 다시 들어간다
        }
        else
        {
            PhotonNetwork.JoinLobby();
        }
    }

    public override void OnLeftLobby()
    {
        if (!rejoinLobbyAfterLeave) return;
        rejoinLobbyAfterLeave = false;
        PhotonNetwork.JoinLobby(); // OnJoinedLobby가 목록을 비우고, 곧 전체 목록이 OnRoomListUpdate로 온다
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log($"[Lobby] Connected to region {PhotonNetwork.CloudRegion}.");
        if (pendingRoomName != null)
        {
            string name = pendingRoomName;
            RoomOptions options = pendingRoomOptions;
            pendingRoomName = null;
            pendingRoomOptions = null;
            PhotonNetwork.CreateRoom(name, options, TypedLobby.Default);
            return;
        }
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        cachedRoomList.Clear();
        ClearRoomListView();
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            if (info.RemovedFromList)
            {
                cachedRoomList.Remove(info.Name);
                continue;
            }

            cachedRoomList[info.Name] = info; // IsOpen==false여도 목록에는 남겨둔다 (0.1-5)
        }

        RefreshRoomListView();
    }

    private void RefreshRoomListView()
    {
        foreach (var kv in cachedRoomList)
        {
            if (!roomListItems.TryGetValue(kv.Key, out RoomListItem item))
            {
                item = Instantiate(roomListItemPrefab, roomListContent);
                roomListItems.Add(kv.Key, item);
            }
            item.Refresh(kv.Value, this); // 이름 / "N / 정원" / 입장 버튼 interactable 갱신
        }

        // 목록에서 사라진 방의 UI 항목 정리
        var toRemove = new List<string>();
        foreach (var kv in roomListItems)
        {
            if (!cachedRoomList.ContainsKey(kv.Key)) toRemove.Add(kv.Key);
        }
        foreach (string name in toRemove)
        {
            Destroy(roomListItems[name].gameObject);
            roomListItems.Remove(name);
        }
    }

    private void ClearRoomListView()
    {
        foreach (var kv in roomListItems)
            Destroy(kv.Value.gameObject);
        roomListItems.Clear();
    }

    public void OnMakeRoomButtonClicked()
    {
        if (!TryApplyNickname()) return;

        string roomName = roomNameInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            feedbackText.text = enterRoomNameMessage;
            return;
        }

        // 방장이 고른 인원·제한시간·타임어택 시간(EscapePlan.md §1.7). 입력칸 값은 RoomSettingField가 이미 허용 범위로 맞춰 둔다.
        // 제한시간과 타임어택 시간은 판이 바뀌어도 유지되는 Room Prop(초)으로 넣는다.
        int players = playersField != null ? playersField.Value : GameSettings.Current.DefaultPlayers;
        int timeLimitMinutes = timeLimitField != null ? timeLimitField.Value : GameSettings.Current.DefaultTimeLimitMinutes;
        int timeAttackMinutes = timeAttackField != null ? timeAttackField.Value : GameSettings.Current.DefaultTimeAttackMinutes;
        var options = new RoomOptions
        {
            MaxPlayers = players,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { NetKeys.RoomTimeLimit, timeLimitMinutes * 60 },
                { NetKeys.TimeAttackDuration, timeAttackMinutes * 60 },
            },
        };
        string region = createRegion != null ? createRegion.Code : CurrentRegion;
        if (region != null && region != CurrentRegion)
        {
            // 다른 지역에 만든다: 그 지역으로 다시 접속한 뒤(OnConnectedToMaster) 만든다. 로비의 볼 지역도 그 지역으로 맞춘다.
            pendingRoomName = roomName;
            pendingRoomOptions = options;
            PhotonRegions.Saved = region;
            if (viewRegion != null) viewRegion.SetCode(region);
            SwitchRegion(region);
            return;
        }
        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
    }

    public void OnRandomJoinButtonClicked()
    {
        if (!TryApplyNickname()) return;
        PhotonNetwork.JoinRandomRoom(null, 0, MatchmakingMode.RandomMatching, null, null);
    }

    public void JoinRoom(string roomName) // RoomListItem에서 호출
    {
        if (!TryApplyNickname()) return;
        PhotonNetwork.JoinRoom(roomName);
    }

    private bool TryApplyNickname()
    {
        string nickname = userIdInput.text.Trim();
        if (string.IsNullOrEmpty(nickname))
        {
            feedbackText.text = enterUserIdMessage;
            return false;
        }
        PhotonNetwork.NickName = nickname;
        return true;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        feedbackText.text = roomNameExistsMessage; // 대부분 이 케이스 (ErrorCode.GameIdAlreadyExists)
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        feedbackText.text = noRoomAvailableMessage;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        feedbackText.text = cannotJoinRoomMessage; // 방금 꽉 찼거나 방장이 이미 게임을 시작한 경우 등
    }

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
            PhotonNetwork.LoadLevel(SceneNames.GameLobby); // 방을 새로 만든 최초 1인만 로드, 나머지는 자동 동기화
    }
}
