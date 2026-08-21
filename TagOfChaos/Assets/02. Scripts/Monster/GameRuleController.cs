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
        if (!RoomState.TryGetInt(NetKeys.MonsterJoined, out _)) return;
        if (RoomState.TryGetInt(NetKeys.GameResult, out _)) return; // 이미 판정됨

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
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (monsters.Contains(p.ActorNumber)) continue;
            int hitCount = p.CustomProperties.TryGetValue(NetKeys.HitCount, out object v) ? (int)v : 0;
            if (hitCount < 2) return false;
        }
        return true;
    }

    private void Finish(GameResult result)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.GameResult, (int)result } });
    }
}
