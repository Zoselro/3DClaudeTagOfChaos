using Photon.Pun;
using UnityEngine;

// 쿠키 동기화 — 공용 NetworkTransformSync에 캐리 여부 한 필드를 덧붙인다(research.md §12 E1).
public class PlayerNetworkSync : NetworkTransformSync<PlayerMoveState>
{
    // 드는 쪽의 캐리(상체 Carry 레이어) 여부 — 원격 화면에서도 들고 있는 자세가 보이도록 동기화한다.
    // (예전 4번째 필드 isJump는 수신만 하고 아무도 쓰지 않던 죽은 값이라 대체했다, research.md §8.21)
    public bool RemoteIsCarrying { get; private set; }

    public void Write(PhotonStream stream, Transform transform, PlayerMoveState state, bool isCarrying)
    {
        Write(stream, transform, state);
        stream.SendNext(isCarrying);
    }

    public override void Read(PhotonStream stream, Transform transform)
    {
        base.Read(stream, transform);
        RemoteIsCarrying = (bool)stream.ReceiveNext();
    }
}
