using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 괴물의 탈출 모드 상태: 마녀가 내리치면 괴물도 죽는다(EscapePlan.md §1.2, 결과 화면 "마녀에 의해 사망").
// 주먹이 맵에 닿는 순간(WitchPresenter.Impacted)에 죽는다(Request1009Plan.md §18.2 Q1).
public class MonsterEscapeState : MonoBehaviourPunCallbacks
{
    private MonsterController monster;
    private bool handled;

    public void Init(MonsterController owner) => monster = owner;

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!changed.ContainsKey(NetKeys.WitchStrike) || handled) return;
        handled = true;
        WitchPresenter.Impacted += DieByWitch;
        Invoke(nameof(DieByWitch), WitchPresenter.StrikeImpactSeconds + WitchPresenter.ImpactGraceSeconds); // 마녀가 없거나 멈춘 화면 대비
    }

    private void OnDestroy() => WitchPresenter.Impacted -= DieByWitch;

    private void DieByWitch()
    {
        WitchPresenter.Impacted -= DieByWitch;
        CancelInvoke(nameof(DieByWitch));
        if (monster != null && monster.View != null && monster.View.IsMine)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.DeathCause, (int)DeathCause.Witch } });
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false; // 모든 화면에서 사라진다
        if (monster != null) monster.enabled = false;
    }
}
