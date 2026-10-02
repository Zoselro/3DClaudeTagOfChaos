using Photon.Pun;
using UnityEngine;

// 오프라인 개발 부트스트랩. 구 4라운드 색상 미니게임(ColorSelectionManager)이 GameRule.md v3.6로
// 완전히 대체되면서 자동 시작 기능은 일단 방 생성까지만 남겨뒀다 — 새 게임 룰(가마솥→색칠→GrabKill)
// 자동 시작은 §14.3 범위 밖의 별도 결정 사항.
public class OfflineModeBootstrap : MonoBehaviourPunCallbacks
{
    [SerializeField] private string testRoomName = "OfflineTestRoom";
    [SerializeField] private bool autoCreateRoom = false;
    [SerializeField] private bool spawnAsMonster = false;
    [Tooltip("혼자 테스트할 때 스파이로 시작한다(방에 들어가면 자기를 스파이 목록에 넣고 탈출 배치를 다시 짠다 — 스파이 상자·로켓 칸이 생긴다).")]
    [SerializeField] private bool spawnAsSpy = false;

    // PlayerSpawner/MonsterTestSpawner가 참조하는 개발용 플래그 — 씬 배치 순서에 의존하지 않도록 static.
    public static bool SpawnAsMonster { get; private set; }

    private void Awake()
    {
        PhotonNetwork.OfflineMode = true;
        SpawnAsMonster = spawnAsMonster;
    }

    // 혼자 스파이로 시험할 때: 방에 들어가면 자기를 스파이 목록에 넣는다.
    public override void OnJoinedRoom()
    {
        if (!spawnAsSpy || spawnAsMonster || !PhotonNetwork.OfflineMode) return;
        var props = new ExitGames.Client.Photon.Hashtable
        {
            [NetKeys.SpyActorNumbers] = new[] { PhotonNetwork.LocalPlayer.ActorNumber },
            [NetKeys.EscapeState] = null, // 스파이가 있는 판으로 탈출 배치를 다시 짠다
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // PhotonNetwork.OfflineMode와 SpawnAsMonster는 static이라 씬이 바뀌어도 남는다. 에디터에서 이 테스트 씬 다음에
    // 다른 씬을 실행할 때 오프라인 상태가 이어지지 않도록 되돌린다(research.md §8.23).
    private void OnDestroy()
    {
        SpawnAsMonster = false;
        if (PhotonNetwork.OfflineMode && !PhotonNetwork.InRoom) PhotonNetwork.OfflineMode = false;
    }

    private void Start()
    {
        if (!autoCreateRoom) return;
        if (!PhotonNetwork.OfflineMode) return;

        PhotonNetwork.CreateRoom(testRoomName);
    }
}
