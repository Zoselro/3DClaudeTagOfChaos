using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameLobbyController : MonoBehaviourPunCallbacks
{
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerListItem playerListItemPrefab;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button startGameButton;

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

    private int lastKnownPlayerCount = -1;
    private bool lastKnownIsMasterClient;

    private void Start()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return; // 방에 들어오지 않은 채 씬만 단독 실행된 경우 방어

        RefreshPlayerList();
        RefreshStartButton();
    }

    // AutomaticallySyncScene으로 뒤따라 입장하는 클라이언트는 Start() 시점에 룸 상태(인원수/방장 여부)를
    // 아직 완전히 따라잡지 못했을 수 있다 — 화면 값이 실제 값과 달라지면 다음 프레임에 스스로 바로잡는
    // 안전망. 이벤트 콜백(OnPlayerEnteredRoom 등)은 이미 정상 동작하므로 그대로 두고, 이 안전망은
    // 그 경로가 놓친 최초 상태만 보정한다.
    private void Update()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;

        int currentCount = PhotonNetwork.CurrentRoom.PlayerCount;
        bool currentIsMaster = PhotonNetwork.IsMasterClient;

        if (currentCount == lastKnownPlayerCount && currentIsMaster == lastKnownIsMasterClient) return;

        lastKnownPlayerCount = currentCount;
        lastKnownIsMasterClient = currentIsMaster;
        RefreshPlayerList();
        RefreshStartButton();
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

    // 게임 도중 방장이 나가서 마스터가 바뀌는 경우, 새 마스터에게 버튼을 노출/숨김
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshStartButton();
    }

    private void RefreshPlayerList()
    {
        foreach (Transform child in playerListContent) Destroy(child.gameObject);

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            var item = Instantiate(playerListItemPrefab, playerListContent);
            item.SetNickname(p.NickName);
        }

        statusText.text = $"{PhotonNetwork.CurrentRoom.PlayerCount} / {PhotonNetwork.CurrentRoom.MaxPlayers} 대기 중";
    }

    // 방장에게만 버튼을 보여주고, 정원이 찼을 때만 눌리게 함 (회색/흰색 전환은 Button 컴포넌트가 자동 처리)
    private void RefreshStartButton()
    {
        bool isOwner = PhotonNetwork.IsMasterClient;
        startGameButton.gameObject.SetActive(isOwner);
        if (!isOwner) return;

        startGameButton.interactable =
            PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers;
    }

    // 방장이 "게임 시작" 버튼을 눌렀을 때 (UI Button.onClick에 연결)
    public void OnStartGameButtonClicked()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < PhotonNetwork.CurrentRoom.MaxPlayers) return; // 방어적 재확인

        PhotonNetwork.CurrentRoom.IsOpen = false; // 씬 전환 도중 새로 입장하는 걸 방지
        PhotonNetwork.LoadLevel("GameScene"); // 방장만 호출, 나머지는 AutomaticallySyncScene으로 함께 이동
    }
}
