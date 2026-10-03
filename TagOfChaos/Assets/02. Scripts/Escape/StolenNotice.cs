// 스파이가 장치에서 뺀 재료 알림 하나(Request1003bPlan.md §2). 방장이 정한 시각(PhotonNetwork.Time)에 띄우고,
// 그때 재료가 다시 장치에 끼워져 있으면 취소한다. 판단은 순수 계산이라 EditMode에서 시험한다.
public struct StolenNotice
{
    public enum Decision { Wait, Show, Cancel }

    public string ItemId;
    public int ItemIndex;
    public double ShowAt;

    public Decision Decide(double now, ItemLocation itemLocation)
    {
        if (now < ShowAt) return Decision.Wait;
        return itemLocation == ItemLocation.Device ? Decision.Cancel : Decision.Show;
    }
}
