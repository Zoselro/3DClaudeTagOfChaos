using System.Collections;
using Photon.Pun;
using UnityEngine;

// PlayerTestScene 전용 개발 도구 — OfflineModeBootstrap.SpawnAsMonster가 켜져 있으면
// PlayerSpawner 대신 이 스크립트가 MonsterPlayer를 스폰해 몬스터를 직접 플레이테스트할 수 있게 한다.
public class MonsterTestSpawner : MonoBehaviour
{
    private const string SpawnPointName = "MonsterSpawnPos";
    private const string MonsterPrefabName = "MonsterPlayer";

    private void Start()
    {
        StartCoroutine(SpawnWhenInRoom());
    }

    private IEnumerator SpawnWhenInRoom()
    {
        while (!PhotonNetwork.InRoom)
            yield return null;

        if (!OfflineModeBootstrap.SpawnAsMonster) yield break;

        GameObject spawnPointObj = GameObject.Find(SpawnPointName);
        if (spawnPointObj == null)
        {
            Debug.LogWarning($"MonsterTestSpawner: \"{SpawnPointName}\" 오브젝트를 씬에서 찾을 수 없어 몬스터를 스폰하지 못했습니다.");
            yield break;
        }

        PhotonNetwork.Instantiate(MonsterPrefabName, spawnPointObj.transform.position, Quaternion.identity, 0);
    }
}
