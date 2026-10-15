using System.Linq;
using Photon.Pun;
using UnityEngine;

// 괴물 스폰(Request1009Plan.md §25.5 C). 예전에는 괴물이 대기실에서 색칠 시간(60초)을 기다렸다가 혼자 맵으로 와 합류했지만,
// 이제는 모두와 함께 맵으로 오고 술래방(MonsterSpawnPos) 안에서 시작한다 — 술래문(MonsterRoomDoor)이 준비 시간 뒤 열린다.
// 맵 씬에 1개 배치. 괴물 소유 클라이언트가 자기 몫의 MonsterPlayer를 만든다(PlayerSpawner와 같은 InRoom 대기 관례).
public class MonsterJoinController : MonoBehaviour
{
    private const string MonsterSpawnPointName = SceneSpawnPoints.Monster;
    private const string MonsterPrefabName = "MonsterPlayer";

    private bool hasSpawnedLocally;

    private void Update()
    {
        if (!hasSpawnedLocally) TryLocalSpawn();
    }

    private void TryLocalSpawn()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        if (!monsters.Contains(PhotonNetwork.LocalPlayer.ActorNumber)) return;

        hasSpawnedLocally = true;

        GameObject spawnPointObj = GameObject.Find(MonsterSpawnPointName);
        if (spawnPointObj == null)
        {
            Debug.LogWarning($"[MonsterJoinController] Spawn point \"{MonsterSpawnPointName}\" not found in scene. Monster was not spawned.");
            return;
        }

        Vector3 spawnPos = SpawnPositionFinder.FindClearPosition(spawnPointObj.transform.position, GameSettings.Current.MonsterSpawnRange);
        PhotonNetwork.Instantiate(MonsterPrefabName, spawnPos, Quaternion.identity, 0);
    }
}
