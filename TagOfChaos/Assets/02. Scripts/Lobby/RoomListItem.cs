using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button joinButton;

    private string roomName;
    private LobbyController lobby;

    private void Awake()
    {
        joinButton.onClick.AddListener(() => lobby.JoinRoom(roomName));
    }

    public void Refresh(RoomInfo info, LobbyController owner)
    {
        lobby = owner;
        roomName = info.Name;
        roomNameText.text = info.Name;
        playerCountText.text = $"{info.PlayerCount} / {info.MaxPlayers}"; // 1/4, 2/4 ...
        joinButton.interactable = info.IsOpen; // 방장이 이미 게임을 시작했으면 비활성화 (0.1-5)
    }
}
