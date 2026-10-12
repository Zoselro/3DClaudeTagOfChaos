using Photon.Pun;
using UnityEngine;

// 네트워크 관련 공용 기본값(research.md §12 E7) — 예전에는 같은 값을 여러 클래스가 각자 상수로 들고 있었다.
public static class NetworkDefaults
{
    // 캐릭터 동기화 빈도(research.md §12.4)는 어느 씬에서 시작해도 같게 앱 시작 때 한 번 정한다 — 예전에는 로비 씬에서만 정해
    // 에디터에서 맵 씬을 바로 실행하면 기본값(10)으로 돌았다(AuditFixPlan.md F-22).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyRates() => PhotonNetwork.SerializationRate = GameSettings.Current.CharacterSyncRate;

    // PhotonNetwork.KeepAliveInBackground의 PUN 기본값(ConnectionHandler). 괴물 대기실 대기처럼 메시지 처리를 멈추는
    // 동안 늘렸던 값을 되돌릴 때 쓴다.
    public const float KeepAliveInBackgroundSeconds = 60f;
}
