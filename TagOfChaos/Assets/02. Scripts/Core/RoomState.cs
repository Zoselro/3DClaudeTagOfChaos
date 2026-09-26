using Photon.Pun;

// Room CustomProperties를 안전하게 읽는 조회 헬퍼. ColorSelectionManager/ColorSelectionPanel/
// PlayerPaintCanvas/PlayerColorDisplay가 각자 반복 구현하던 조회 로직을 통합한다
// (architecture-review.md §11.1). ColorTag 도메인 전용으로, 이 도메인 밖에서는 쓰지 않는다.
public static class RoomState
{
    public static bool IsInRoom() => PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;

    public static bool TryGetInt(string key, out int value)
    {
        value = default;
        if (!IsInRoom()) return false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object raw)) return false;
        value = (int)raw;
        return true;
    }

    public static bool TryGetDouble(string key, out double value)
    {
        value = default;
        if (!IsInRoom()) return false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object raw)) return false;
        value = (double)raw;
        return true;
    }

    public static int GetRoundIndex() => TryGetInt(NetKeys.RoundIndex, out int value) ? value : -1;

    public static bool TryGetIntArray(string key, out int[] value)
    {
        value = null;
        if (!IsInRoom()) return false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object raw)) return false;
        value = (int[])raw;
        return true;
    }

}
