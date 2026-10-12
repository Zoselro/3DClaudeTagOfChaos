using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// 처형 확정(방장 전용, AuditFixPlan.md §8.1 — S-04·S-06·S-14). 예전에는 쿠키 본인이 자기 파괴(HitCount)를 쓰고 괴물 본인이 잡은 수를 썼다 —
// 조작된 쿠키는 잡혀도 죽지 않고, 조작된 괴물은 거리와 관계없이 처형할 수 있었다. 이제 방장이 자기 화면의 위치로 검증한 뒤
// 피해자의 HitCount·DeathCause와 괴물의 CatchCount를 쓴다(쓰기는 AuthorityProps — 남이 바꾸면 RoomPropertyGuard가 되돌린다).
public static class GrabKillAuthority
{
    private static readonly Dictionary<int, float> lastConfirmAt = new Dictionary<int, float>();
    private static readonly Dictionary<int, int> catches = new Dictionary<int, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lastConfirmAt.Clear();
        catches.Clear();
    }

    // 방장: RequestGrabKill RPC를 받은 즉시(처형 연출이 몸을 소켓으로 옮기기 전) 부른다. 확정했으면 true.
    public static bool TryConfirm(HideOrSeekPlayer cookie, MonsterController monster, Player sender)
    {
        if (!PhotonNetwork.IsMasterClient || cookie == null || monster == null || sender == null) return false;
        Player victim = cookie.View != null ? cookie.View.Owner : null;
        if (victim == null) return false;

        int monsterActor = sender.ActorNumber;
        GamePhase phase = GamePhaseState.Current;
        bool hunt = phase == GamePhase.Hunt || phase == GamePhase.TimeAttack;
        bool alive = !RoomState.IsBroken(victim) && !RoomState.IsMonster(victim.ActorNumber);
        bool onMap = !EscapeManager.HasLeftMap(victim.ActorNumber);
        float since = lastConfirmAt.TryGetValue(monsterActor, out float at) ? Time.time - at : float.MaxValue;
        var trigger = monster.GetComponent<MonsterGrabKillTrigger>();
        GameSettingsSO settings = GameSettings.Current;
        float reach = (trigger != null ? trigger.BodyFrontOffset : 0f) + settings.GrabReachFromFront;

        if (!NetSecurityRules.CanConfirmGrabKill(RoomState.IsMonster(monsterActor), hunt, alive, onMap,
                monster.transform.position, cookie.transform.position, reach, settings.MaxGrabHeight, since, monster.GrabKillDuration * 0.8f))
        {
            Debug.LogWarning($"[GrabKill] Rejected: monster actor {monsterActor} -> actor {victim.ActorNumber} " +
                             $"(phase {phase}, alive {alive}, onMap {onMap}, distance {Vector3.Distance(monster.transform.position, cookie.transform.position):F1} m, since {since:F1}s).");
            return false;
        }

        lastConfirmAt[monsterActor] = Time.time;
        AuthorityProps.SetPlayer(victim, new Hashtable { { NetKeys.HitCount, 2 }, { NetKeys.DeathCause, (int)DeathCause.Monster } });

        // 잡은 횟수(결과 화면): 방장이 센다. 방장이 바뀌었으면 지금까지 기록된 값에서 이어 센다.
        int known = RoomState.TryGetPlayerInt(sender, NetKeys.CatchCount, out int c) ? c : 0;
        int next = Mathf.Max(known, catches.TryGetValue(monsterActor, out int counted) ? counted : 0) + 1;
        catches[monsterActor] = next;
        AuthorityProps.SetPlayer(sender, new Hashtable { { NetKeys.CatchCount, next } });
        return true;
    }
}
