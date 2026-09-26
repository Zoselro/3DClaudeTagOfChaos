using Photon.Pun;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

// 캐릭터(쿠키·괴물) 공용 위치·회전·이동 상태 동기화(research.md §12 E1). 예전에는 PlayerNetworkSync와
// MonsterNetworkSync가 상태 enum 타입만 다른 같은 코드였다. 새 캐릭터 종류는 상태 enum만 정하면 이 클래스를
// 그대로 쓰고, 추가 필드가 필요하면 상속해서 Write/Read 뒤에 이어 붙인다(PlayerNetworkSync 참고).
// 직렬화 순서(위치 → 회전 → 상태 int)는 예전과 같아 기존 빌드와 호환된다.
public class NetworkTransformSync<TState> where TState : struct, System.Enum
{
    private bool isFirstUpdate = true;

    public Vector3 RemotePosition { get; private set; } = Vector3.zero;
    public Quaternion RemoteRotation { get; private set; } = Quaternion.identity;
    public TState RemoteState { get; private set; }

    static NetworkTransformSync()
    {
        // 상태는 int로 보낸다 — 다른 크기의 enum을 쓰면 비트 변환이 어긋나므로 초기에 알린다.
        if (System.Enum.GetUnderlyingType(typeof(TState)) != typeof(int))
            Debug.LogError($"[NetworkTransformSync] {typeof(TState).Name} must use int as its underlying type.");
    }

    public void Write(PhotonStream stream, Transform transform, TState state)
    {
        stream.SendNext(transform.position);
        stream.SendNext(transform.rotation);
        stream.SendNext(UnsafeUtility.As<TState, int>(ref state));
    }

    public virtual void Read(PhotonStream stream, Transform transform)
    {
        RemotePosition = (Vector3)stream.ReceiveNext();
        RemoteRotation = (Quaternion)stream.ReceiveNext();
        int rawState = (int)stream.ReceiveNext();
        RemoteState = UnsafeUtility.As<int, TState>(ref rawState);

        if (isFirstUpdate)
        {
            transform.position = RemotePosition;
            transform.rotation = RemoteRotation;
            isFirstUpdate = false;
        }
    }

    // 마지막으로 수신한 위치에서 너무 멀리 떨어져 있으면(디싱크) 스냅시키고,
    // 그렇지 않으면 고정 비율로 부드럽게 보간한다.
    public void Interpolate(Transform transform, float deltaTime, float lerpRate = 10.0f, float snapDistance = 10.0f)
    {
        if (snapDistance < (transform.position - RemotePosition).magnitude)
            transform.position = RemotePosition;
        else
            transform.position = Vector3.Lerp(transform.position, RemotePosition, deltaTime * lerpRate);

        transform.rotation = Quaternion.Slerp(transform.rotation, RemoteRotation, deltaTime * lerpRate);
    }
}
