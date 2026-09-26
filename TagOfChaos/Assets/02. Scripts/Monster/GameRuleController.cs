using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

// 승리 판정(마스터 전용, GameRule.md §8.1). 쿠키 전원 파괴 → 괴물 승, GameEndTime 경과 →
// 쿠키 승. 괴물 수와 무관하게 쿠키 쪽만 검사하면 되므로 다중 괴물 확장에도 그대로 동작한다.
public enum GameResult { CookiesWin, MonsterWins }

public class GameRuleController : MonoBehaviourPunCallbacks
{
    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        if (GamePhaseState.Current != GamePhase.Hunt) return; // 괴물 합류 전이거나 이미 판정됨

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

    private void Finish(GameResult result)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.GameResult, (int)result } });
    }
}
