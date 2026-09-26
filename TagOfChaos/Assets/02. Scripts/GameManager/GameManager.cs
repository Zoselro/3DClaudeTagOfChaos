using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// 채팅 전담(스폰은 PlayerSpawner, 퇴장은 RoomExitController로 분리됨). 외부에서 참조하는 곳이 없던 싱글톤
// Inst와 Is_Conversating은 제거했다(research.md §8.21).
public class GameManager : MonoBehaviourPunCallbacks
{
    const int MAX_CHAT = 50; // 채팅 최대 갯수

    [SerializeField] private PhotonView pv;
    [SerializeField] private TMP_InputField InputFdChat; // 채팅 입력 필드
    [SerializeField] private TextMeshProUGUI txtLogMsg;

    private List<string> m_MsgList = new List<string>();
    private bool bEnter = false;

    private readonly StringBuilder logBuilder = new StringBuilder();

    private HideOrSeekPlayer localPlayer;

    private void Start()
    {
        Time.timeScale = 1.0f; // 일시정지 풀어주기
        PhotonNetwork.IsMessageQueueRunning = true; // 포톤 메시지 큐를 활성화하여 RPC 호출을 받을 수 있도록 설정

        // Start()가 실행됐다고 해서 PhotonNetwork.InRoom이 true라는 보장은 없다(Bug-fix-plan.md §12).
        // 그 상태에서 RPC를 보내면 로컬에서는 아무 에러 없이 지나가지만 실제 전송(RaiseEvent)은
        // 조용히 실패해 다른 클라이언트에게 전달되지 않는다 — InRoom이 true가 될 때까지 기다린 뒤 보낸다.
        StartCoroutine(SendConnectedMessageWhenInRoom());
    }

    private IEnumerator SendConnectedMessageWhenInRoom()
    {
        while (!PhotonNetwork.InRoom)
            yield return null;

        // 로그 메시지에 출력할 문자열 생성
        string msg = "\n<color=#33ff33>[" +
                        PhotonNetwork.LocalPlayer.NickName +
                        "] Connected</color>";

        // AllBuffered는 방 이벤트 캐시에 무한히 쌓이고, 씬마다 PhotonView ID가 달라 다른 씬의 오브젝트로
        // 재생되기도 했다(research.md §8.19, Bug-fix-plan.md §24.6 ⑰-D). 현재 방에 있는 사람에게만 보낸다.
        pv.RPC(RpcLogMsg, RpcTarget.All, msg, false);
    }

    private void Update()
    {
        //--- 채팅 구현 텍스트
        if (PlayerInput.ChatSubmitReleased)
        {// 엔터키를 누르면 인풋 필드 활성화
            bEnter = !bEnter;
            if (bEnter)
            {
                InputFdChat.gameObject.SetActive(true);
                InputFdChat.ActivateInputField(); // <--- 키보드 커서 입력 상자 쪽으로 가게 만들어 줌
                SetLocalPlayerMovementLocked(true);
            }
            else
            {
                InputFdChat.gameObject.SetActive(false);
                SetLocalPlayerMovementLocked(false);
                if (!string.IsNullOrEmpty(InputFdChat.text.Trim()))
                {
                    BroadcastingChat();
                }
            }
        }
    }

    // 중계 하기 위함
    // 다른 클래스(RoomExitController)도 보내는 RPC 이름 — 메서드 이름 변경 시 컴파일 단계에서 함께 바뀐다(research.md §12 E3).
    public const string RpcLogMsg = nameof(LogMsg);

    [PunRPC]
    private void LogMsg(string msg, bool isChatMsg, PhotonMessageInfo info)
    {
        //로컬에서 내가 보낸 메시지인 경우만
        //채팅 메시지인지?
        //info.Sender.IsLocal == true // 로컬에서 보낸 메시지
        //info.Sender.IsLocal == false // PhotonNetwork.LocalPlayer.ActorNumber(IsMine의 고유번호)
        if (info.Sender.IsLocal == true && isChatMsg == true)
        {
            // 방장이 말을 한 경우는 "#00ffff"로 들어 오니까 방장이 한 말은 자신도 그냥 하늘 색으로 보일 것
            msg = msg.Replace("#ffffff", "#ffff00"); // 문자열을 찾아서, 바꿔주는 역할
        }

        m_MsgList.Add(msg);

        if(m_MsgList.Count > MAX_CHAT)
        {
            m_MsgList.RemoveAt(0);
        }

        // 로그 메시지 Text UI에 텍스트를 누적시켜 표시
        logBuilder.Clear();
        for (int i = 0; i < m_MsgList.Count; i++)
            logBuilder.Append(m_MsgList[i]);
        txtLogMsg.text = logBuilder.ToString();
    }

    //채팅 내용을 중계하는 함수
    private void BroadcastingChat()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        string msg = "\n<color=#ffffff>[" +
                    PhotonNetwork.LocalPlayer.NickName + "] " +
                    InputFdChat.text + "</color>";

        pv.RPC(RpcLogMsg, RpcTarget.All, msg, true);

        InputFdChat.text = "";
    }


// 채팅 입력창이 열려있는 동안 로컬 플레이어의 이동 입력을 잠근다
    // (research.md §6.3 — is_Conversating과 IsMovementLocked가 서로 연결되지 않았던 문제를 복구)
    private void SetLocalPlayerMovementLocked(bool locked)
    {
        if (localPlayer == null)
        {
            localPlayer = CharacterRegistry.FindLocal<HideOrSeekPlayer>();
        }

        if (localPlayer != null)
            localPlayer.IsMovementLocked = locked;
    }
}
