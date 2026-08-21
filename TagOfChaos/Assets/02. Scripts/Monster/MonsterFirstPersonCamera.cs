using UnityEngine;

// 괴물 1인칭 카메라(GameRule.md §6.2, 사용자 확인 — "3인칭으로 하기엔 힘들 것 같다"). 같은 Main
// Camera 오브젝트에 Camera_Ctrl(쿠키용 3인칭)과 나란히 부착 — 한 클라이언트는 쿠키 아니면 괴물
// 둘 중 하나만 플레이하므로 두 컴포넌트가 동시에 활성화될 일이 없다.
public class MonsterFirstPersonCamera : MonoBehaviour
{
    private Transform eyeSocket; // 괴물 머리 안쪽 시점 원점

    public void InitCamera(Transform monsterEyeSocket)
    {
        eyeSocket = monsterEyeSocket;
    }

    private void LateUpdate()
    {
        if (eyeSocket == null) return;

        // 3인칭(Camera_Ctrl)과 달리 거리/오프셋 계산이 없다 — 그냥 눈 위치·방향에 그대로 고정.
        transform.position = eyeSocket.position;
        transform.rotation = eyeSocket.rotation;
    }
}
