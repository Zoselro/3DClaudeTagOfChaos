// 스파이가 장치에서 뺀 재료 알림 하나(Request1003bPlan.md §2). 방장이 정한 시각(PhotonNetwork.Time)에 띄우고,
// 그때 재료가 다시 장치에 끼워져 있으면 취소한다. 판단은 순수 계산이라 EditMode에서 시험한다.
// 로켓에 넣은 재료가 다시 생긴 알림(Respawned, Request1009Plan.md §22.6 M4)도 같은 규칙으로 기다렸다 띄운다.
public struct StolenNotice
{
    public enum Decision { Wait, Show, Cancel }

    public string ItemId;
    public int ItemIndex;
    public double ShowAt;
    public bool Respawned; // false = 장치에서 훔침, true = 로켓에 넣어 다른 곳에 다시 생김
    public bool OnGround;  // Respawned일 때: 빈 상자가 없어 장치 옆 바닥에 놓였다

    public Decision Decide(double now, ItemLocation itemLocation)
    {
        if (now < ShowAt) return Decision.Wait;
        return itemLocation == ItemLocation.Device ? Decision.Cancel : Decision.Show;
    }

    // 띄울 문장 틀({0} = 재료 이름). 받침 여부는 부르는 쪽이 KoreanText로 정한다.
    public string Template(EscapeTextsSO t, bool finalConsonant)
    {
        if (!Respawned) return finalConsonant ? t.stolenFromDeviceWithFinal : t.stolenFromDeviceNoFinal;
        if (OnGround) return finalConsonant ? t.respawnedOnGroundWithFinal : t.respawnedOnGroundNoFinal;
        return finalConsonant ? t.respawnedInChestWithFinal : t.respawnedInChestNoFinal;
    }
}
