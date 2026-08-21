using Photon.Pun;
using UnityEngine;

// PlayerNetworkSync.cs와 완전히 동일한 구조(Write/Read/Interpolate), 타입만 MonsterMoveState로
// 교체. 코드 중복이지만, Unit/과 Monster/ 두 도메인을 서로 참조하게 만들지 않기 위한 의도적
// 선택 — Unit/과 ColorTag/가 이미 이 원칙을 지켜왔다(GameRule.md §4.3).
public class MonsterNetworkSync
{
    private bool isFirstUpdate = true;

    public Vector3 RemotePosition { get; private set; } = Vector3.zero;
    public Quaternion RemoteRotation { get; private set; } = Quaternion.identity;
    public MonsterMoveState RemoteState { get; private set; } = MonsterMoveState.Idle;

    public void Write(PhotonStream stream, Transform transform, MonsterMoveState state)
    {
        stream.SendNext(transform.position);
        stream.SendNext(transform.rotation);
        stream.SendNext((int)state);
    }

    public void Read(PhotonStream stream, Transform transform)
    {
        RemotePosition = (Vector3)stream.ReceiveNext();
        RemoteRotation = (Quaternion)stream.ReceiveNext();
        RemoteState = (MonsterMoveState)stream.ReceiveNext();

        if (isFirstUpdate)
        {
            transform.position = RemotePosition;
            transform.rotation = RemoteRotation;
            isFirstUpdate = false;
        }
    }

    public void Interpolate(Transform transform, float deltaTime, float lerpRate = 10.0f, float snapDistance = 10.0f)
    {
        if (snapDistance < (transform.position - RemotePosition).magnitude)
        {
            transform.position = RemotePosition;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, RemotePosition, deltaTime * lerpRate);
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, RemoteRotation, deltaTime * lerpRate);
    }
}
