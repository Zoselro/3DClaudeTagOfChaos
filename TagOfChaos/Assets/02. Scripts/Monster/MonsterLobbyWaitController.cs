using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameLobbyScene 배치(MonsterManagers). 괴물 클라이언트만 대기실에 남겨 두었다가, 색칠 페이즈가 끝나는
// 시각에 혼자 GameScene으로 이동시킨다(Bug-fix-plan.md §23.3). 괴물이 아닌 클라이언트에서는 아무 일도
// 하지 않는다.
//
// 흐름: 괴물 확정 → AutomaticallySyncScene=false(방장의 LoadLevel에 끌려가지 않음) → 방장이 시작 버튼으로
// PaintPhaseEndTime을 새로 기록 → 이 변경을 받는 순간 메시지 큐를 멈춰 이후 이벤트(쿠키 Instantiate,
// 색칠 스트로크, Room Props)를 전부 쌓아 둔다 → 로컬 카운트다운 종료 시 PhotonNetwork.LoadLevel(Game)
// (방장이 아니므로 혼자만 로드, 로드가 끝나면 PUN이 큐를 재개해 쌓인 이벤트가 GameScene 안에서 재생된다).
// 큐를 멈추지 않으면 쿠키들이 GameScene에서 스폰한 캐릭터가 이 대기실에 생성됐다가 씬 전환 때 파괴돼,
// 괴물은 GameScene에서 쿠키를 하나도 볼 수 없게 된다(§23.3.2 F4).
public class MonsterLobbyWaitController : MonoBehaviourPunCallbacks
{
    private const float KeepAliveMarginSeconds = 30f;

    [SerializeField] private GameObject waitPanelRoot; // 반드시 이 컴포넌트와 다른 오브젝트(자기 자신을 끄는 함정 회피)
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private string countdownFormat = "{0}"; // 표시 문구는 인스펙터에서 입력
    [SerializeField] private Button[] buttonsToDisableWhileWaiting;      // Back 버튼 등
    [SerializeField] private Behaviour[] behavioursToDisableWhileWaiting; // GameManager(채팅) 등

    private bool isWaiting;
    private double departAtLocalTime; // Time.realtimeSinceStartupAsDouble 기준 출발 시각

    public bool IsWaiting => isWaiting;

    private void Start()
    {
        if (waitPanelRoot != null) waitPanelRoot.SetActive(false);
        ApplySceneSyncPolicy(); // 이미 괴물로 확정된 상태로 대기실에 들어온 경우
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(NetKeys.MonsterActorNumbers)) ApplySceneSyncPolicy();

        // 시작 신호: 방장이 시작 버튼을 누르며 새로 쓴 PaintPhaseEndTime. 대기실 입장 시 이미 있던(이전 판)
        // 값은 "변경"이 아니므로 여기로 들어오지 않는다.
        if (propertiesThatChanged.ContainsKey(NetKeys.PaintPhaseEndTime) && !isWaiting && RoomState.IsLocalMonster())
            EnterWaiting();
    }

    // 괴물이면 방장의 LoadLevel에 끌려가지 않도록 끄고, 아니면(판 초기화로 괴물 목록이 지워진 경우 포함) 다시 켠다.
    // 예전에는 끄기만 해서, 이전 판 괴물이 대기실로 돌아온 직후 옛 값을 보고 꺼진 채 남으면 다음 판에
    // 방장을 따라가지 못했다(Bug-fix-plan.md §24.5). 값이 바뀔 때만 대입한다 — true 대입은 PUN이 즉시
    // LoadLevelIfSynced를 호출하기 때문이다.
    private void ApplySceneSyncPolicy()
    {
        bool shouldSync = !RoomState.IsLocalMonster();
        if (PhotonNetwork.AutomaticallySyncScene == shouldSync) return;

        PhotonNetwork.AutomaticallySyncScene = shouldSync;
        Debug.Log($"[MonsterLobbyWait] AutomaticallySyncScene={shouldSync} (local actor {PhotonNetwork.LocalPlayer?.ActorNumber}, isMonster={!shouldSync}).");
    }

    private void EnterWaiting()
    {
        if (!RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double endTime)) return;

        // 큐가 멈춘 동안에는 서버 시간 보정을 받지 못하므로, 남은 시간은 지금 한 번만 계산하고
        // 이후로는 timeScale 영향이 없는 로컬 실시간 시계로만 센다.
        double remaining = System.Math.Max(0, endTime - PhotonNetwork.Time);
        departAtLocalTime = Time.realtimeSinceStartupAsDouble + remaining;
        isWaiting = true;

        RemoveOtherPlayersAvatars();
        SetWaitingUi(true);

        // 큐를 멈추면 송신도 멈추고 백그라운드 스레드가 ACK만 보내며 연결을 유지하는데, 기본 유지 시간(60초)이
        // 대기 시간보다 짧으므로 늘려 둔다. GameScene 도착 후 MonsterJoinController가 기본값으로 되돌린다.
        PhotonNetwork.KeepAliveInBackground = (float)remaining + KeepAliveMarginSeconds;
        Debug.Log($"[MonsterLobbyWait] Monster (actor {PhotonNetwork.LocalPlayer.ActorNumber}) waits in lobby for {remaining:F1}s. Message queue paused.");
        PhotonNetwork.IsMessageQueueRunning = false;
    }

    private void Update()
    {
        if (!isWaiting) return;

        double remaining = departAtLocalTime - Time.realtimeSinceStartupAsDouble;
        if (countdownText != null)
            countdownText.text = string.Format(countdownFormat, Mathf.CeilToInt((float)System.Math.Max(0, remaining)));

        if (remaining > 0) return;

        isWaiting = false;
        PhotonNetwork.LoadLevel(SceneNames.Game); // 방장이 아니므로 혼자만 로드, 로드 후 큐 자동 재개
    }

    // 다른 플레이어들은 이미 GameScene으로 떠나 이 오브젝트들은 더 이상 갱신되지 않는다(대기실에 굳은 채
    // 남음). 원래 주인 쪽에서는 이미 씬 전환으로 파괴됐으므로 로컬에서만 치운다.
    private void RemoveOtherPlayersAvatars()
    {
        // 목록을 순회하며 Destroy하면 OnDisable의 등록 해제로 목록이 바뀌므로 먼저 모은다.
        var others = new System.Collections.Generic.List<GameObject>();
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.Role == CharacterRole.Cookie && CharacterRegistry.IsAlive(c) && c.View != null && !c.View.IsMine) others.Add(c.gameObject);
        foreach (GameObject go in others) Destroy(go);
    }

    // 큐가 멈춘 동안에는 LeaveRoom/채팅 RPC 송신도 막혀 있으므로 해당 입력을 막는다(§23.3.4-①, D3).
    private void SetWaitingUi(bool waiting)
    {
        if (waitPanelRoot != null) waitPanelRoot.SetActive(waiting);

        if (buttonsToDisableWhileWaiting != null)
        {
            foreach (var button in buttonsToDisableWhileWaiting)
                if (button != null) button.interactable = !waiting;
        }

        if (behavioursToDisableWhileWaiting != null)
        {
            foreach (var behaviour in behavioursToDisableWhileWaiting)
                if (behaviour != null) behaviour.enabled = !waiting;
        }
    }
}
