// 네트워크 관련 공용 기본값(research.md §12 E7) — 예전에는 같은 값을 여러 클래스가 각자 상수로 들고 있었다.
public static class NetworkDefaults
{
    // PhotonNetwork.KeepAliveInBackground의 PUN 기본값(ConnectionHandler). 괴물 대기실 대기처럼 메시지 처리를 멈추는
    // 동안 늘렸던 값을 되돌릴 때 쓴다.
    public const float KeepAliveInBackgroundSeconds = 60f;
}
