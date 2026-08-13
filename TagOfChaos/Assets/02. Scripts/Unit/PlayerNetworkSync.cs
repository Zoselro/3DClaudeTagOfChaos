using Photon.Pun;
using UnityEngine;

public class PlayerNetworkSync
{
    private bool isFirstUpdate = true;

    public Vector3 RemotePosition { get; private set; } = Vector3.zero;
    public Quaternion RemoteRotation { get; private set; } = Quaternion.identity;
    public PlayerMoveState RemoteState { get; private set; } = PlayerMoveState.Idle;
    public bool RemoteIsJump { get; private set; }

    public void Write(PhotonStream stream, Transform transform, PlayerMoveState state, bool isJump)
    {
        stream.SendNext(transform.position);
        stream.SendNext(transform.rotation);
        stream.SendNext((int)state);
        stream.SendNext(isJump);
    }

    public void Read(PhotonStream stream, Transform transform)
    {
        RemotePosition = (Vector3)stream.ReceiveNext();
        RemoteRotation = (Quaternion)stream.ReceiveNext();
        RemoteState = (PlayerMoveState)stream.ReceiveNext();
        RemoteIsJump = (bool)stream.ReceiveNext();

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
