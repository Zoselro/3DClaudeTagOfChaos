using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 60초 색칠 페이즈(PaintPhaseEndTime)가 끝나면 괴물이 GameScene에 합류한다(GameRule.md §6.4).
// 합류 시각을 기준으로 GameEndTime(10분 후)을 마스터가 세팅하고, 괴물 소유 클라이언트 각자가
// 자기 몫의 MonsterPlayer를 로컬로 스폰한다(PlayerSpawner.cs와 동일한 InRoom 대기 관례는
// 이미 색칠 페이즈 진입 시점에 보장돼 있으므로 별도 대기 불필요).
public class MonsterJoinController : MonoBehaviourPunCallbacks
{
    private const float SurvivalDuration = 600f; // 10분
    private const string MonsterSpawnPointName = "MonsterSpawnPos";
    private const string MonsterPrefabName = "MonsterPlayer";

    private bool hasSpawnedLocally;
    private bool masterTriggered;

    private void Update()
    {
        if (PhotonNetwork.IsMasterClient) MasterTick();
        if (!hasSpawnedLocally) TryLocalSpawn();
    }

    private void MasterTick()
    {
        if (masterTriggered) return;
        if (!RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double paintEnd)) return;
        if (PhotonNetwork.Time < paintEnd) return;

        masterTriggered = true;
        double joinTime = PhotonNetwork.Time;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.MonsterJoined, 1 },
            { NetKeys.GameEndTime, joinTime + SurvivalDuration },
        });
    }

    private void TryLocalSpawn()
    {
        if (!RoomState.TryGetInt(NetKeys.MonsterJoined, out _)) return;
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        if (PhotonNetwork.LocalPlayer == null || !monsters.Contains(PhotonNetwork.LocalPlayer.ActorNumber)) return;

        hasSpawnedLocally = true;

        GameObject spawnPointObj = GameObject.Find(MonsterSpawnPointName);
        if (spawnPointObj == null)
        {
            Debug.LogWarning($"MonsterJoinController: \"{MonsterSpawnPointName}\" 오브젝트를 씬에서 찾을 수 없어 괴물을 스폰하지 못했습니다.");
            return;
        }

        PhotonNetwork.Instantiate(MonsterPrefabName, spawnPointObj.transform.position, Quaternion.identity, 0);
    }
}
