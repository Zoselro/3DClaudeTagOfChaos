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
    [SerializeField] private string leaveConfirmMessage = "로비로 나가시겠습니까?"; // 씬별로 인스펙터에서 다르게 설정

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

        string msg = "\n<color=#ff0000>]" + PhotonNetwork.LocalPlayer.NickName + "] 방 나감</color>";

        if (PhotonNetwork.PlayerList != null && PhotonNetwork.PlayerList.Length <= 1)
        {
            Debug.Log("마지막 사람이 방 나감");
            if (PhotonNetwork.CurrentRoom != null)
            {
                PhotonNetwork.CurrentRoom.CustomProperties.Clear();
                Debug.Log("방의 CustomProperties 초기화 완료!");
            }
        }

        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg, false);

        if (PhotonNetwork.LocalPlayer != null)
        {
            PhotonNetwork.LocalPlayer.CustomProperties.Clear();
            Debug.Log("나가는 유저의 CustomProperties 초기화 완료!");
        }

        Debug.Log("방 나가기 버튼 클릭!");
        PhotonNetwork.LeaveRoom();
        Debug.Log("PhotonNetwork.LeaveRoom() 호출 완료!");
    }

    public override void OnLeftRoom()
    {
        Debug.Log("방 나가기 완료! OnLeftRoom 콜백함수 호출!");
        SceneManager.LoadScene(SceneNames.Lobby);
    }
}
