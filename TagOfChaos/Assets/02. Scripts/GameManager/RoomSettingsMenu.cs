using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 대기실(GameLobbyScene)에서 방장이 ESC → 설정으로 여는 방 설정 창. 로비에서 방을 만들 때 고른 값(인원·제한시간·
// 타임어택 시간, EscapePlan.md §1.7)을 다시 바꾼다. 입력 칸은 로비와 같은 RoomSettingField(같은 허용 범위·경고)를 쓴다.
// 제한시간·타임어택은 방 속성(초)이라 다음 판부터 적용되고, 인원은 지금 들어와 있는 사람 수보다 적게 줄일 수 없다.
// 문구는 씬에서 입력한다(코드에 한글 금지).
public class RoomSettingsMenu : MonoBehaviour
{
    [SerializeField] private RoomSettingField playersField;
    [SerializeField] private RoomSettingField timeLimitField;
    [SerializeField] private RoomSettingField timeAttackField;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private string appliedMessage = "Saved";
    [SerializeField] private string playersRaisedMessage = "Players can't be fewer than the people in the room";

    public event System.Action Closed;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        if (applyButton != null) applyButton.onClick.AddListener(Apply);
        if (backButton != null) backButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        if (feedbackText != null) feedbackText.text = string.Empty;
        if (!PhotonNetwork.InRoom) return;
        // 지금 방의 값으로 채운다
        if (playersField != null) playersField.SubmitText(PhotonNetwork.CurrentRoom.MaxPlayers.ToString());
        if (timeLimitField != null && RoomState.TryGetInt(NetKeys.RoomTimeLimit, out int limit)) timeLimitField.SubmitText((limit / 60).ToString());
        if (timeAttackField != null && RoomState.TryGetInt(NetKeys.TimeAttackDuration, out int attack)) timeAttackField.SubmitText((attack / 60).ToString());
    }

    public void Close()
    {
        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    private void Apply()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        Photon.Realtime.Room room = PhotonNetwork.CurrentRoom;
        bool raised = false;
        if (playersField != null)
        {
            int players = playersField.Value;
            if (players < room.PlayerCount)
            {
                players = room.PlayerCount;
                playersField.SubmitText(players.ToString());
                raised = true;
            }
            room.MaxPlayers = players;
        }
        var props = new ExitGames.Client.Photon.Hashtable();
        if (timeLimitField != null) props[NetKeys.RoomTimeLimit] = timeLimitField.Value * 60;
        if (timeAttackField != null) props[NetKeys.TimeAttackDuration] = timeAttackField.Value * 60;
        if (props.Count > 0) room.SetCustomProperties(props);
        if (feedbackText != null) feedbackText.text = raised ? playersRaisedMessage : appliedMessage;
    }
}
