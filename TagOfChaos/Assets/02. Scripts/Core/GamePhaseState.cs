using Photon.Pun;

// 한 판의 진행 단계(research.md §12 E2). 단계는 여전히 Room Props 조합(PaintPhaseEndTime·MonsterJoined·GameResult)으로
// 동기화되지만(네트워크 프로토콜 불변), 그 조합을 "지금 어느 단계인가"로 해석하는 곳을 여기 한 곳으로 모았다 —
// 예전에는 색칠 UI·붓 커서·캔버스·카운트다운·승패 판정이 같은 조건을 각자 다시 썼다. 새 단계를 넣을 때는
// GamePhase 항목과 Evaluate의 조건부터 추가한다.
public enum GamePhase
{
    Lobby,           // 판 시작 전(대기실)
    Paint,           // 쿠키 변장(색칠) 시간
    AwaitingMonster, // 색칠이 끝나고 괴물 합류 확정 전(짧은 전환 구간)
    Hunt,            // 괴물 합류 후 쿠키 생존 시간
    Result,          // 승패 판정 완료
}

public static class GamePhaseState
{
    public static GamePhase Current
    {
        get
        {
            bool hasPaintEnd = RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double paintEnd);
            bool monsterJoined = RoomState.TryGetInt(NetKeys.MonsterJoined, out _);
            bool hasResult = RoomState.TryGetInt(NetKeys.GameResult, out _);
            return Evaluate(hasPaintEnd, paintEnd, monsterJoined, hasResult, PhotonNetwork.Time);
        }
    }

    public static bool IsPaintActive => Current == GamePhase.Paint;

    // 네트워크 상태와 분리한 순수 해석(테스트용).
    public static GamePhase Evaluate(bool hasPaintEnd, double paintEndTime, bool monsterJoined, bool hasResult, double now)
    {
        if (hasResult) return GamePhase.Result;
        if (monsterJoined) return GamePhase.Hunt;
        if (hasPaintEnd) return now < paintEndTime ? GamePhase.Paint : GamePhase.AwaitingMonster;
        return GamePhase.Lobby;
    }

    // 지금 해당 단계이고 그 단계의 종료 시각이 있으면 true(카운트다운 표시용).
    public static bool TryGetActiveEndTime(GamePhase phase, out double endTime)
    {
        endTime = 0;
        if (Current != phase) return false;
        switch (phase)
        {
            case GamePhase.Paint: return RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out endTime);
            case GamePhase.Hunt: return RoomState.TryGetDouble(NetKeys.GameEndTime, out endTime);
            default: return false;
        }
    }
}
