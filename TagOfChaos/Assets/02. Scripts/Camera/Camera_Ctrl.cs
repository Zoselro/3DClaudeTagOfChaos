using UnityEngine;

public class Camera_Ctrl : MonoBehaviour
{
    [SerializeField] GameObject m_Player;
    Vector3 m_TargetPos = Vector3.zero;

    // --- 카메라 회전 관련 설정 ---
    float m_RotH = 0.0f;
    float m_RotV = 0.0f;
    float hSpeed = 5.0f;
    float vSpeed = 2.4f;
    float vMinLimit = -7.0f;
    float vMaxLimit = 80.0f;
    // --- 카메라 회전 관련 설정 ---
    // (줌 관련 필드 zoomSpeed/minDist/maxDist 삭제 — 마우스 휠은 이제 붓 크기 조절 전용)

    float m_DefaultRotH = 0.0f;
    float m_DefaultRotV = 25.0f;
    [SerializeField] float m_DefaultDist = 3.2f; // 이제 유일한 카메라 거리 고정값 (더 이상 휠로 바뀌지 않음)

    Quaternion m_CurrentRotation;
    Quaternion m_TargetRotation;
    Vector3 m_BasicPos = Vector3.zero;
    Vector3 m_BuffPos = Vector3.zero;
    float rotationSmoothTime = 0.08f;
    // (줌 스무딩 필드 m_CurDistance/m_TargetDistance/zoomSmoothTime/zoomVelocity 삭제)

    public void InitCamera(GameObject player)
    {
        m_Player = player;
    }

    void Start()
    {
        if (m_Player == null) return;

        m_TargetPos = m_Player.transform.position;
        m_TargetPos.y += 1.4f;

        m_RotH = m_DefaultRotH;
        m_RotV = m_DefaultRotV;

        m_CurrentRotation = Quaternion.Euler(m_RotV, m_RotH, 0.0f);
        m_BasicPos = new Vector3(0f, 0f, -m_DefaultDist);

        m_BuffPos = m_TargetPos + (m_CurrentRotation * m_BasicPos);
        transform.position = m_BuffPos;
        transform.LookAt(m_TargetPos);
    }

    void LateUpdate()
    {
        if (m_Player == null) return;

        m_TargetPos = m_Player.transform.position;
        m_TargetPos.y += 1.4f;

        if (Input.GetMouseButton(1)) // 우클릭 드래그로 시점 회전은 그대로 유지
        {
            m_RotH += Input.GetAxis("Mouse X") * hSpeed;
            m_RotV -= Input.GetAxis("Mouse Y") * vSpeed;
            m_RotV = ClampAngle(m_RotV, vMinLimit, vMaxLimit);
        }

        m_TargetRotation = Quaternion.Euler(m_RotV, m_RotH, 0.0f);
        m_CurrentRotation = Quaternion.Slerp(m_CurrentRotation, m_TargetRotation,
                             Mathf.Clamp(Time.deltaTime / rotationSmoothTime, 0.0f, 1.0f));

        // 마우스 휠 줌 입력 처리 블록 전체 삭제
        // (Input.GetAxis("Mouse ScrollWheel") 읽기 + m_TargetDistance 갱신 + SmoothDamp 보간)
        // → 휠 입력은 색상 라운드 중 PlayerPaintCanvas.HandleBrushSizeInput()이 전담 (GameScenePlan.md 5.2)

        m_BasicPos.z = -m_DefaultDist; // 고정 거리, 더 이상 보간 불필요

        m_BuffPos = m_TargetPos + (m_CurrentRotation * m_BasicPos);
        transform.position = m_BuffPos;
        transform.LookAt(m_TargetPos);
    }

    float ClampAngle(float angle, float min, float max)
    {
        angle = Mathf.DeltaAngle(0.0f, angle);
        return Mathf.Clamp(angle, min, max);
    }
}
