// RaiseEvent 코드. 여러 도메인이 공유하므로 Core/에 둔다(research.md §7).
public static class NetEventCodes
{
    public const byte PaintStroke = 1;  // 붓 스탬프 묶음(PlayerPaintCanvas.FlushStrokes)을 다른 클라이언트에 전파
    public const byte ClaimMonster = 2; // GameRule.md §2.1 — 가마솥 진입, 괴물 자원 신청
    public const byte ClearColor = 3;   // GameRule.md §3.4 — 리셋(캔버스 전체 지우기)
    // 4(FillAll)는 쓰이지 않아 제거 — 강제 도포는 PaintStroke의 ForceFill 종류로 전달된다(research.md §8.21).
    // 옛 빌드와 혼동하지 않도록 4는 비워 둔다.
    public const byte StartGameRequest = 5; // Bug-fix-plan.md §33 — 호스트가 진행 권한을 가진 방장에게 판 시작을 요청
    public const byte DoorStateRequest = 6; // GameLobbyScene.md §14 — 상호작용한 클라이언트가 방장에게 문 상태 변경을 요청
}
