using UnityEngine;

// 씬에 배치된 스폰 지점 오브젝트 이름과 조회를 한 곳에 모은다(research.md §12 E3). 예전에는 스폰·리스폰·개발용
// 스포너가 같은 이름을 각자 문자열로 들고 있어 한 곳만 바뀌면 조용히 스폰이 실패했다.
public static class SceneSpawnPoints
{
    public const string Cookie = "PlayerSpawnPos";
    public const string Monster = "MonsterSpawnPos";

    // 스폰 지점 주변에서 다른 캐릭터·오브젝트와 겹치지 않는 위치를 찾는다. 지점이 없으면 false.
    public static bool TryFindClearPosition(string spawnPointName, float range, out Vector3 position)
    {
        GameObject spawnPoint = GameObject.Find(spawnPointName);
        if (spawnPoint == null)
        {
            position = default;
            return false;
        }
        position = SpawnPositionFinder.FindClearPosition(spawnPoint.transform.position, range);
        return true;
    }
}
