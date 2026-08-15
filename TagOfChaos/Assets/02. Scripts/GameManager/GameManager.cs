using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviourPunCallbacks
{
    static public GameManager Inst;

    const int MAX_CHAT = 50; // 채팅 최대 갯수

    [SerializeField] private PhotonView pv;
    [SerializeField] private TMP_InputField InputFdChat; // 채팅 입력 필드
    [SerializeField] private TextMeshProUGUI txtLogMsg;

    private List<string> m_MsgList = new List<string>();
    private bool bEnter = false;

    private bool is_Conversating; // 채팅 중인지 여부를 나타내는 변수
    public bool Is_Conversating => is_Conversating;

    private HideOrSeekPlayer localPlayer;

    private void Awake()
    {
        Inst = this;
    }

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

        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg, false);
    }

private void Update()
    {
        //--- 채팅 구현 텍스트
        if (Input.GetKeyUp(KeyCode.Return))
        {// 엔터키를 누르면 인풋 필드 활성화
            bEnter = !bEnter;
            if (bEnter)
            {
                is_Conversating = true;
                InputFdChat.gameObject.SetActive(true);
                InputFdChat.ActivateInputField(); // <--- 키보드 커서 입력 상자 쪽으로 가게 만들어 줌
                SetLocalPlayerMovementLocked(true);
            }
            else
            {
                InputFdChat.gameObject.SetActive(false);
                is_Conversating = false;
                SetLocalPlayerMovementLocked(false);
                if (!string.IsNullOrEmpty(InputFdChat.text.Trim()))
                {
                    BroadcastingChat();
                }
            }
        }
    }

    // 중계 하기 위함
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
        txtLogMsg.text = "";
        for (int i = 0; i < m_MsgList.Count; i++)
        {
            txtLogMsg.text += m_MsgList[i];
        }
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

        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg, true);

        InputFdChat.text = "";
    }


// 채팅 입력창이 열려있는 동안 로컬 플레이어의 이동 입력을 잠근다
    // (research.md §6.3 — is_Conversating과 IsMovementLocked가 서로 연결되지 않았던 문제를 복구)
    private void SetLocalPlayerMovementLocked(bool locked)
    {
        if (localPlayer == null)
        {
            foreach (var p in FindObjectsByType<HideOrSeekPlayer>(FindObjectsSortMode.None))
            {
                if (p.IsMine) { localPlayer = p; break; }
            }
        }

        if (localPlayer != null)
            localPlayer.IsMovementLocked = locked;
    }
}
