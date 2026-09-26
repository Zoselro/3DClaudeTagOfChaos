using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 뒤로가기 버튼(확인창 → 방 나가기 → 로비 씬 전환) 전담.
// GameManager.cs에서 분리됨(architecture-review.md §1.1 — GameManager는 채팅 전용으로 축소).
public class RoomExitController : MonoBehaviourPunCallbacks
{

    [SerializeField] private PhotonView pv; // "LogMsg" RPC 브로드캐스트용 — GameManager와 같은 오브젝트의 PhotonView를 연결
    [SerializeField] private Button m_BackBtn;
    [SerializeField] private ConfirmDialog confirmDialog;
    // 표시 문구는 씬별로 인스펙터에서 설정한다(코드에 한글을 넣지 않는 프로젝트 규칙). {0} = 닉네임.
    [SerializeField] private string leaveConfirmMessage = "Leave to the lobby?";
    [SerializeField] private string leaveLogFormat = "\n<color=#ff0000>[{0}] left the room</color>";

    private void Start()
    {
        if (m_BackBtn != null)
            m_BackBtn.onClick.AddListener(OnClickBackButtonPressed);
    }

    // Back 버튼 클릭 시: 곧바로 나가지 않고 확인창부터 띄운다
    public void OnClickBackButtonPressed()
    {
        if (confirmDialog != null)
            confirmDialog.Show(leaveConfirmMessage, OnClickBackBtn);
        else
            OnClickBackBtn(); // 확인창이 연결 안 돼 있으면 안전하게 기존 동작으로 폴백
    }

    public void OnClickBackBtn()
    {
        if (m_BackBtn != null) m_BackBtn.interactable = false;

        // 방 CustomProperties는 마지막 사람이 나가면 방과 함께 사라지므로 따로 지우지 않는다(예전의
        // CurrentRoom.CustomProperties.Clear()는 로컬 사본만 지워 아무 효과가 없었다, research.md §8.12).
        pv.RPC(GameManager.RpcLogMsg, RpcTarget.All, string.Format(leaveLogFormat, PhotonNetwork.LocalPlayer.NickName), false);

        // LocalPlayer의 CustomProperties는 다음에 들어가는 방으로 그대로 전송되므로, 판 단위 키(파괴 여부·
        // 슬롯 수)만 로컬에서 지운다. SkinIndex는 다음 방에서도 유지한다.
        foreach (string key in NetKeys.RoundPlayerKeys)
            PhotonNetwork.LocalPlayer.CustomProperties.Remove(key);

        Debug.Log("[RoomExit] Leaving room.");
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        // 괴물 대기(MonsterLobbyWaitController)나 GameScene 도착 전 이탈 등으로 바뀐 전역 네트워크 설정을 되돌린다.
        PhotonNetwork.IsMessageQueueRunning = true;
        PhotonNetwork.KeepAliveInBackground = NetworkDefaults.KeepAliveInBackgroundSeconds;

        Debug.Log("[RoomExit] Left room. Loading lobby scene.");
        SceneManager.LoadScene(SceneNames.Lobby);
    }
}
