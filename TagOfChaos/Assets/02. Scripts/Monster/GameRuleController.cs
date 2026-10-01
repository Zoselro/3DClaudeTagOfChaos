using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 승리 판정(마스터 전용, GameRule.md §8.1). 쿠키 전원 파괴 → 괴물 승, GameEndTime 경과 →
// 쿠키 승. 괴물 수와 무관하게 쿠키 쪽만 검사하면 되므로 다중 괴물 확장에도 그대로 동작한다.
// EscapeEnded: 탈출 모드가 끝났다 — 이유는 NetKeys.EscapeEndReason, 사람마다 결과는 Player Props로 계산한다(EscapePlan.md §5.8).
public enum GameResult { CookiesWin, MonsterWins, EscapeEnded }

public class GameRuleController : MonoBehaviourPunCallbacks
{
    // 온라인에서는 SetCustomProperties가 서버 응답 전까지 로컬 캐시에 반영되지 않는다. 응답 전 프레임마다 결과를 다시 보내거나,
    // 그 사이 다른 결과(파괴 완료 → 시간 종료)를 보내 나중 값이 이기지 않도록 한 판에 한 번만 보낸다(Bug-fix-plan.md §41 ㊹).
    private bool resultRequested;
    private bool witchRequested;
    private float witchStruckAt = -1f;
    private readonly System.Collections.Generic.List<EscapeRules.PlayerInfo> escapePlayers = new System.Collections.Generic.List<EscapeRules.PlayerInfo>();

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || resultRequested) return;
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        GamePhase phase = GamePhaseState.Current;
        if (EscapeManager.IsActive)
        {
            if (phase == GamePhase.Hunt || phase == GamePhase.TimeAttack) UpdateEscape(monsters, phase == GamePhase.TimeAttack);
            return;
        }
        if (phase != GamePhase.Hunt) return; // 괴물 합류 전이거나 이미 판정됨

        if (AllCookiesBroken(monsters))
        {
            Finish(GameResult.MonsterWins);
            return;
        }

        if (RoomState.TryGetDouble(NetKeys.GameEndTime, out double endTime) && PhotonNetwork.Time >= endTime)
        {
            Finish(GameResult.CookiesWin);
        }
    }

    // 탈출 모드 판정(EscapePlan.md §1.2). 규칙은 EscapeRules(순수 계산)에 있다.
    private void UpdateEscape(int[] monsters, bool timeAttack)
    {
        EscapeState state = EscapeManager.Instance.State;
        escapePlayers.Clear();
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            int actor = p.ActorNumber;
            bool escaped = RoomState.HasEscaped(p) || (state != null && (state.Escaped.Contains(actor) || state.Boarded.Contains(actor)));
            escapePlayers.Add(new EscapeRules.PlayerInfo
            {
                IsMonster = monsters.Contains(actor),
                IsSpy = RoomState.IsSpy(actor),
                Escaped = escaped,
                Broken = RoomState.IsBroken(p),
            });
        }

        bool struck = RoomState.TryGetInt(NetKeys.WitchStrike, out _);
        if (struck && witchStruckAt < 0f) witchStruckAt = Time.time;
        bool hasEnd = RoomState.TryGetDouble(NetKeys.GameEndTime, out double gameEnd);
        RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out double timeAttackEnd);

        switch (EscapeRules.Evaluate(escapePlayers, timeAttack, PhotonNetwork.Time, gameEnd, hasEnd, timeAttackEnd,
                     struck ? Time.time - witchStruckAt : -1f))
        {
            case EscapeRules.Decision.StrikeWitch:
                if (!witchRequested)
                {
                    witchRequested = true;
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.WitchStrike, 1 } });
                }
                break;
            case EscapeRules.Decision.EndAllResolved: FinishEscape(EscapeEndReason.AllResolved); break;
            case EscapeRules.Decision.EndTimeUp: FinishEscape(EscapeEndReason.TimeUp); break;
            case EscapeRules.Decision.EndWitchStrike: FinishEscape(EscapeEndReason.WitchStrike); break;
        }
    }

    // 끝난 이유와 함께, 결과 화면이 스파이를 표시할 수 있도록 스파이 번호를 공개한다.
    private void FinishEscape(EscapeEndReason reason)
    {
        resultRequested = true;
        RoomState.TryGetIntArray(NetKeys.SpyActorNumbers, out int[] spies);
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.GameResult, (int)GameResult.EscapeEnded },
            { NetKeys.EscapeEndReason, (int)reason },
            { NetKeys.RevealedSpies, spies ?? new int[0] },
        });
        Debug.Log($"[GameRule] Escape mode ended: {reason}");
    }

    private bool AllCookiesBroken(int[] monsters)
    {
        // 방에 남은 쿠키가 한 명도 없어도 true(괴물 승)가 된다 — 쿠키 전원이 나가면 더 이상 술래잡기가
        // 성립하지 않으므로 괴물 승리로 끝내는 것을 의도된 규칙으로 명시한다(research.md §8.15).
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (monsters.Contains(p.ActorNumber)) continue;
            if (!RoomState.IsBroken(p)) return false;
        }
        return true;
    }

    // 결과가 도착하면(내가 보낸 것이든 이전 방장이 보낸 것이든) 단계 판정이 Result가 되므로 플래그는 그대로 둔다.
    // 방장이 바뀌면 새 방장은 캐시(GamePhaseState)로 다시 판단한다 — 응답 전에 이전 방장이 나갔으면 다시 보낸다.
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        resultRequested = false;
        witchRequested = false;
    }

    private void Finish(GameResult result)
    {
        resultRequested = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.GameResult, (int)result } });
    }
}
