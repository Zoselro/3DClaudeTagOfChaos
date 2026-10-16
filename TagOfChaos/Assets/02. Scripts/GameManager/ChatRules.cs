using Photon.Realtime;

// 판 중 채팅 보이기 규칙(Request1009Plan.md §26.9 Q24): 탈출했거나 부서진 사람의 채팅은 판이 끝날 때까지
// 같은 처지(탈출·부서짐)인 사람에게만 보인다 — 남은 사람에게 정보를 흘리지 않게. 판이 끝나면(결과) 다시 모두 본다.
public static class ChatRules
{
    public static bool IsVisible(bool roundActive, bool senderOut, bool viewerOut) => !roundActive || !senderOut || viewerOut;

    public static bool RoundActive => EscapeManager.IsActive && GamePhaseState.Current != GamePhase.Result;

    // 탈출(탈출구·모자·로켓)했거나 부서진 사람.
    public static bool IsOut(Player player) =>
        player != null && (EscapeManager.HasLeftMap(player.ActorNumber) || RoomState.IsBroken(player));
}
