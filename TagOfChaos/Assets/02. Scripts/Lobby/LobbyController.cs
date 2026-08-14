using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class LobbyController : MonoBehaviourPunCallbacks
{
    private const int MaxPlayers = 4; // GameScenePlan.md 0.1-5와 동일 (인원수는 게임 룰에 고정)
    private const string GameVersion = "1"; // 빌드가 바뀌면 올려서 이전 버전 클라이언트와 매치메이킹이 섞이지 않게 함

    [SerializeField] private TMP_InputField userIdInput;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private RoomListItem roomListItemPrefab;

    private readonly Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();
    private readonly Dictionary<string, RoomListItem> roomListItems = new Dictionary<string, RoomListItem>();

    private void Awake()
    {
        // #Critical: LoadLevel()이 방 전체에 씬 전환을 자동 동기화하게 함 (PunBasics-Tutorial 관례)
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = GameVersion;
    }

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

    private void Start()
    {
        userIdInput.text = "Player" + Random.Range(1000, 10000); // 기본값, 직접 수정 가능

        if (PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby();
        }
        else
        {
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
            item.Refresh(kv.Value, this); // 이름 / "N / 4" / 입장 버튼 interactable 갱신
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
            feedbackText.text = "방 이름을 입력하세요.";
            return;
        }

        var options = new RoomOptions { MaxPlayers = MaxPlayers };
        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
    }

    public void OnRandomJoinButtonClicked()
    {
        if (!TryApplyNickname()) return;
        PhotonNetwork.JoinRandomRoom();
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
            feedbackText.text = "UserID를 입력하세요.";
            return false;
        }
        PhotonNetwork.NickName = nickname;
        return true;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        feedbackText.text = "이미 존재하는 방 이름입니다."; // 대부분 이 케이스 (ErrorCode.GameIdAlreadyExists)
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        feedbackText.text = "참가 가능한 방이 없습니다.";
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        feedbackText.text = "입장할 수 없는 방입니다."; // 방금 꽉 찼거나 방장이 이미 게임을 시작한 경우 등
    }

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
            PhotonNetwork.LoadLevel("GameLobbyScene"); // 방을 새로 만든 최초 1인만 로드, 나머지는 자동 동기화
    }
}
