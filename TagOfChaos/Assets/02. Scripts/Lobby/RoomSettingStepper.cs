// 방 만들기 설정 값 하나(인원·제한시간·타임어택 시간)의 규칙(EscapePlan.md §1.7). UI와 분리한 순수 클래스라 테스트할 수 있다.
// 화살표로 올리고 내리거나 숫자를 직접 입력한다. 범위를 벗어나면 가장 가까운 허용값으로 되돌리고 OutOfRange를 알린다.
public sealed class RoomSettingStepper
{
    public enum Outcome
    {
        Ok,         // 범위 안
        OutOfRange, // 범위 밖 — 가장 가까운 허용값으로 되돌림(빨간 경고)
        Empty,      // 입력칸이 비었음 — 기본값으로 되돌림
    }

    public int Min { get; }
    public int Max { get; }
    public int Default { get; }
    public int Value { get; private set; }

    public RoomSettingStepper(int min, int max, int defaultValue)
    {
        Min = min;
        Max = max < min ? min : max;
        Default = Clamp(defaultValue);
        Value = Default;
    }

    public Outcome Step(int delta) => Set(Value + delta);

    // 입력칸 문자열. 숫자 외 문자는 입력칸(IntegerNumber)이 막지만, 만일을 위해 숫자만 골라 읽는다.
    public Outcome SetFromText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Value = Default;
            return Outcome.Empty;
        }

        long parsed = 0;
        bool any = false;
        foreach (char c in text)
        {
            if (c < '0' || c > '9') continue;
            any = true;
            parsed = parsed * 10 + (c - '0');
            if (parsed > int.MaxValue) { parsed = int.MaxValue; break; }
        }
        if (!any)
        {
            Value = Default;
            return Outcome.Empty;
        }
        return Set((int)parsed);
    }

    private Outcome Set(int wanted)
    {
        int clamped = Clamp(wanted);
        Value = clamped;
        return clamped == wanted ? Outcome.Ok : Outcome.OutOfRange;
    }

    private int Clamp(int v) => v < Min ? Min : (v > Max ? Max : v);
}
