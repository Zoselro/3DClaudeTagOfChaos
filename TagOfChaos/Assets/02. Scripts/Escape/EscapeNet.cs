// 탈출 모드 요청 종류(EscapePlan.md §5.2). 누른 클라이언트가 방장에게 보내고, 방장(EscapeAuthority)만 상태를 바꾼다.
// 요청 값 보조(설치할 칸 번호: 0은 "아무 빈 칸", 칸 i는 i + 1 — 예전 요청과 섞여도 뜻이 같게).
public static class EscapeNet
{
    public static int EncodeSlot(int slot) => slot + 1;
    public static int DecodeSlot(int value) => value - 1;
}

public enum EscapeOp : byte
{
    OpenChest = 1,    // A = 상자 번호
    PickUp = 2,       // A = 아이템 번호(바닥)
    Drop = 3,         // A = 인벤토리 칸, Pos = 떨어뜨릴 위치
    DropAll = 4,      // Pos = 떨어뜨릴 위치(잡힘·탈출)
    Install = 5,      // A = 인벤토리 칸 → 탈출 장치, B = EncodeSlot(고른 칸)(0 = 아무 빈 칸)
    Steal = 6,        // A = 탈출 장치 칸
    RocketInsert = 7, // A = 인벤토리 칸 → 스파이 로켓
    RocketBoard = 8,
    Exit = 9,         // 쿠키 탈출구
    ToolUse = 10,     // A = 인벤토리 칸, B = 물풍선 색, Pos = 맞힌 지점, Targets = 맞은 캐릭터 ViewID
}

public enum EscapeNoticeKind : byte
{
    SpyCaught = 1,
    StolenToRocket = 2, // 내용: 아이템 ID
    DeviceComplete = 3,
}

// 게임이 끝난 이유(NetKeys.EscapeEndReason).
public enum EscapeEndReason
{
    AllResolved = 1, // 모두 탈출했거나 잡혔다
    TimeUp = 2,      // 제한시간이 끝났다(D8)
    WitchStrike = 3, // 마녀가 내리쳤다
}

// 죽은 이유(NetKeys.DeathCause, 결과 화면).
public enum DeathCause
{
    None = 0,
    Monster = 1,
    Witch = 2,
}

public struct EscapeRequest
{
    public EscapeOp Op;
    public int Sender;
    public int A;
    public int B;
    public UnityEngine.Vector3 Pos;
    public int[] Targets;

    public object[] ToPayload() => new object[] { (byte)Op, A, B, Pos, Targets ?? new int[0] };

    public static bool TryParse(object data, int sender, out EscapeRequest request)
    {
        request = default;
        if (!(data is object[] d) || d.Length < 5) return false;
        if (!(d[0] is byte op) || !(d[1] is int a) || !(d[2] is int b) || !(d[3] is UnityEngine.Vector3 pos)) return false;
        request = new EscapeRequest { Op = (EscapeOp)op, Sender = sender, A = a, B = b, Pos = pos, Targets = d[4] as int[] ?? new int[0] };
        return true;
    }
}
