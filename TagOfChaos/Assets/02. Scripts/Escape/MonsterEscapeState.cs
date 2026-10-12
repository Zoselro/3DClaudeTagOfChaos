using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 괴물의 탈출 모드 상태: 마녀가 내리치면 괴물도 죽는다(EscapePlan.md §1.2, 결과 화면 "마녀에 의해 사망").
// 사망은 방장이 주먹이 닿는 순간에 확정해 DeathCause=Witch로 쓴다(GameRuleController, AuditFixPlan.md §8.1) — 이 값을 받으면 모든 화면에서 숨긴다.
// 예전에는 각 화면이 WitchStrike를 보고 스스로 숨고 괴물 본인이 사망 원인을 써, 조작된 WitchStrike 하나로 괴물이 멈췄다.
public class MonsterEscapeState : MonoBehaviourPunCallbacks
{
    private MonsterController monster;
    private bool dead;

    public void Init(MonsterController owner)
    {
        monster = owner;
        ApplyIfDead();
    }

    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changed)
    {
        if (monster != null && monster.View != null && target == monster.View.Owner && changed.ContainsKey(NetKeys.DeathCause)) ApplyIfDead();
    }

    private void ApplyIfDead()
    {
        if (dead || monster == null || monster.View == null || monster.View.Owner == null) return;
        if (!RoomState.TryGetPlayerInt(monster.View.Owner, NetKeys.DeathCause, out int cause) || cause != (int)DeathCause.Witch) return;
        dead = true;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false; // 모든 화면에서 사라진다
        monster.enabled = false;
    }
}
