using Photon.Pun;
using UnityEngine;

// 오프라인 개발 부트스트랩. 구 4라운드 색상 미니게임(ColorSelectionManager)이 GameRule.md v3.6로
// 완전히 대체되면서 자동 시작 기능은 일단 방 생성까지만 남겨뒀다 — 새 게임 룰(가마솥→색칠→GrabKill)
// 자동 시작은 §14.3 범위 밖의 별도 결정 사항.
public class OfflineModeBootstrap : MonoBehaviour
{
    [SerializeField] private string testRoomName = "OfflineTestRoom";
    [SerializeField] private bool autoCreateRoom = false;
    [SerializeField] private bool spawnAsMonster = false;

    // PlayerSpawner/MonsterTestSpawner가 참조하는 개발용 플래그 — 씬 배치 순서에 의존하지 않도록 static.
    public static bool SpawnAsMonster { get; private set; }

    private void Awake()
    {
        PhotonNetwork.OfflineMode = true;
        SpawnAsMonster = spawnAsMonster;
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
