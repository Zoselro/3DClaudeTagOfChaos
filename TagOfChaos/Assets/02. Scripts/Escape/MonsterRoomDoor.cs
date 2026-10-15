using UnityEngine;

// 술래방 문(Request1009Plan.md §25.5 C, 테스트 맵 MonsterDoor_0~3). 쿠키·스파이 몸은 막고 괴물만 지나간다 — 각자 화면이 자기 캐릭터만
// 움직이므로, 괴물 본인 화면에서 문 충돌체와 괴물 충돌체의 충돌만 끈다. 준비 시간(PrepareEndTime 전)에는 괴물도 막혀 술래방에 갇혀 있다.
public class MonsterRoomDoor : MonoBehaviour
{
    private const float CheckSeconds = 0.25f;

    private Collider door;
    private Renderer look;
    private MonsterController ignoredFor;
    private float nextCheck;

    private void Awake()
    {
        door = GetComponent<Collider>();
        look = GetComponent<Renderer>();
    }

    private void Update()
    {
        if (door == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckSeconds;
        bool open = GamePhaseState.Current != GamePhase.Prepare && GamePhaseState.Current != GamePhase.Lobby;
        MonsterController monster = LocalMonster();
        if (monster != ignoredFor && ignoredFor != null) SetIgnored(ignoredFor, false);
        if (monster != null) SetIgnored(monster, open);
        ignoredFor = monster;
        // 괴물에게 열린 문은 흐리게(지나갈 수 있음), 닫힌 문·쿠키에게는 그대로
        if (look != null) look.enabled = !(monster != null && open);
    }

    private void SetIgnored(MonsterController monster, bool ignore)
    {
        foreach (Collider c in monster.GetComponentsInChildren<Collider>(true))
            if (c != null && !c.isTrigger) Physics.IgnoreCollision(c, door, ignore);
    }

    private static MonsterController LocalMonster()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.Role == CharacterRole.Monster && c.View != null && c.View.IsMine && c is MonsterController m) return m;
        return null;
    }
}
