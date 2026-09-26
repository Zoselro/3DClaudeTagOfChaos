using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class LobbyController : MonoBehaviourPunCallbacks
{
    private const string GameVersion = "1"; // 빌드가 바뀌면 올려서 이전 버전 클라이언트와 매치메이킹이 섞이지 않게 함

    [SerializeField] private TMP_InputField userIdInput;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private RoomListItem roomListItemPrefab;

    [Header("Feedback Messages (set in inspector)")] // 코드에 한글을 넣지 않는 프로젝트 규칙 — 표시 문구는 프리팹에서 입력
    [SerializeField] private string enterRoomNameMessage = "Enter a room name.";
    [SerializeField] private string enterUserIdMessage = "Enter a user ID.";
    [SerializeField] private string roomNameExistsMessage = "A room with this name already exists.";
    [SerializeField] private string noRoomAvailableMessage = "No room is available to join.";
    [SerializeField] private string cannotJoinRoomMessage = "Cannot join this room.";

    private readonly Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();
    private readonly Dictionary<string, RoomListItem> roomListItems = new Dictionary<string, RoomListItem>();

    private void Awake()
    {
        // #Critical: LoadLevel()이 방 전체에 씬 전환을 자동 동기화하게 함 (PunBasics-Tutorial 관례)
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = GameVersion;
    }


    private void Start()
    {
        userIdInput.text = "Player" + Random.Range(1000, 10000); // 기본값, 직접 수정 가능

        if (PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby();
        }
        else
        {
            PhotonNetwork.SerializationRate = GameSettings.Current.CharacterSyncRate; // 캐릭터 동기화 빈도(research.md §12.4)
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
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

        // 정원은 전역 설정(Resources/GameSettings)에서 읽는다 — 인원을 늘릴 때 코드 수정 없이 에셋 값만 바꾼다.
        var options = new RoomOptions { MaxPlayers = GameSettings.Current.MaxPlayers };
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
