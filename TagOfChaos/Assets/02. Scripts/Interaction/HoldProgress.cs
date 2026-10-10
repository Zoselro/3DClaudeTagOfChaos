// 길게 누르기 진행(Request1009Plan.md §1). 시간과 입력을 밖에서 받는 순수 계산이라 EditMode에서 시험한다.
// Begin으로 시작 → 매 프레임 Tick(지금 초점 사물, 키를 누르고 있는지, 쓸 수 있는지, 지금 시각).
// 떼거나 · 초점 사물이 바뀌거나 · 쓸 수 없게 되면 취소(진행 0), 시간이 다 차면 Completed 한 번.
public sealed class HoldProgress
{
    public enum Result { Idle, Holding, Completed, Canceled }

    private object target;
    private float startTime;
    private float seconds;

    public bool Active => target != null;
    public object Target => target;
    public float Progress { get; private set; }

    public void Begin(object holdTarget, float holdSeconds, float now)
    {
        target = holdTarget;
        seconds = holdSeconds > 0f ? holdSeconds : 0.0001f;
        startTime = now;
        Progress = 0f;
    }

    public Result Tick(object focused, bool held, bool canInteract, float now)
    {
        if (target == null) return Result.Idle;
        if (!held || !ReferenceEquals(focused, target) || !canInteract)
        {
            Cancel();
            return Result.Canceled;
        }
        Progress = (now - startTime) / seconds;
        if (Progress < 1f) return Result.Holding;
        Cancel();
        return Result.Completed;
    }

    public void Cancel()
    {
        target = null;
        Progress = 0f;
    }
}
