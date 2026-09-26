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

    // 추적 대상별 시선 높이/거리 — 쿠키는 기본값, 괴물은 MonsterController가 자기 체격에 맞는 값을
    // 넘긴다(Bug-fix-plan.md §23.4, 괴물도 쿠키와 같은 3인칭 궤도 카메라를 재사용).
    const float DefaultTargetHeight = 1.5f; // Cookie 캐릭터 키가 옛 모델 대비 약 7% 커져 비례 조정(PlayerControllPlan.md §25.7)
    float m_TargetHeight = DefaultTargetHeight;
    float m_Distance = -1f; // 0 이하 = 인스펙터의 m_DefaultDist 사용
    float CurrentDistance => m_Distance > 0f ? m_Distance : m_DefaultDist;

    public const float CookieTargetHeight = DefaultTargetHeight;

    public GameObject FollowTarget => m_Player;

    public void InitCamera(GameObject player)
    {
        InitCamera(player, DefaultTargetHeight, -1f);
    }

    public void InitCamera(GameObject player, float targetHeight, float distance)
    {
        SetFollowTarget(player, targetHeight, distance, keepRotation: false);
    }

    // 추적 대상만 바꾼다. keepRotation=true면 우클릭 드래그로 돌려 둔 현재 각도를 유지해, 관전 대상을 순환할 때
    // 카메라가 기본 각도로 튀지 않는다(Bug-fix-plan.md §28.4). 조작하는 캐릭터(쿠키·괴물)와 관전 대상이 모두 이 한
    // 경로로 카메라를 쓰므로, 우클릭 회전·커서 잠금·회전 보간이 어디서든 똑같이 동작한다.
    public void SetFollowTarget(GameObject target, float targetHeight, float distance, bool keepRotation)
    {
        m_Player = target;
        m_TargetHeight = targetHeight;
        m_Distance = distance;
        if (!keepRotation) ResetToDefaultView(); // InitCamera가 Awake든 Start든 언제 호출되든 상관없이 항상 정확히 초기화됨
    }

    void Start()
    {
        // m_Player가 이미 연결되어 있다면(InitCamera가 이 시점 이전에 이미 호출된 경우) 정상 초기화.
        // 아직 연결 전이라면 아무 것도 하지 않고, InitCamera가 나중에 호출될 때 ResetToDefaultView()가
        // 대신 처리한다 — 두 호출 순서에 더 이상 의존하지 않는다.
        ResetToDefaultView();
    }

    private void ResetToDefaultView()
    {
        if (m_Player == null) return;

        m_TargetPos = m_Player.transform.position;
        m_TargetPos.y += m_TargetHeight;

        m_RotH = m_DefaultRotH;
        m_RotV = m_DefaultRotV;

        m_CurrentRotation = Quaternion.Euler(m_RotV, m_RotH, 0.0f);
        m_BasicPos = new Vector3(0f, 0f, -CurrentDistance);

        m_BuffPos = m_TargetPos + (m_CurrentRotation * m_BasicPos);
        transform.position = m_BuffPos;
        transform.LookAt(m_TargetPos);
    }

    void LateUpdate()
    {
        if (m_Player == null) return;

        m_TargetPos = m_Player.transform.position;
        m_TargetPos.y += m_TargetHeight;

        UpdateCursorLock();

        if (PlayerInput.CameraRotateHeld) // 우클릭 드래그로 시점 회전(버튼은 InputBindings)
        {
            Vector2 delta = PlayerInput.CameraRotateDelta;
            m_RotH += delta.x * hSpeed;
            m_RotV -= delta.y * vSpeed;
            m_RotV = ClampAngle(m_RotV, vMinLimit, vMaxLimit);
        }

        m_TargetRotation = Quaternion.Euler(m_RotV, m_RotH, 0.0f);
        m_CurrentRotation = Quaternion.Slerp(m_CurrentRotation, m_TargetRotation,
                             Mathf.Clamp(Time.deltaTime / rotationSmoothTime, 0.0f, 1.0f));

        // 마우스 휠 줌 입력 처리 블록 전체 삭제
        // (Input.GetAxis("Mouse ScrollWheel") 읽기 + m_TargetDistance 갱신 + SmoothDamp 보간)
        // → 휠 입력은 색상 라운드 중 PlayerPaintCanvas.HandleBrushSizeInput()이 전담 (GameScenePlan.md 5.2)

        m_BasicPos.z = -CurrentDistance; // 고정 거리, 더 이상 보간 불필요

        m_BuffPos = m_TargetPos + (m_CurrentRotation * m_BasicPos);
        transform.position = m_BuffPos;
        transform.LookAt(m_TargetPos);
    }

    // 우클릭으로 시점을 돌리는 동안에만 커서를 잠근다(research.md §8.8). 잠그지 않으면 드래그 중 커서가 창
    // 가장자리에 걸려 회전이 멈추거나 창 밖을 클릭하게 된다. 평소에는 풀어 둬야 색칠·UI 조작이 가능하다.
    void UpdateCursorLock()
    {
        if (PlayerInput.CameraRotatePressed) Cursor.lockState = CursorLockMode.Locked;
        else if (PlayerInput.CameraRotateReleased) Cursor.lockState = CursorLockMode.None;
    }

    void OnDisable()
    {
        // 관전 모드 전환 등으로 이 컴포넌트가 꺼질 때 커서가 잠긴 채 남지 않도록 푼다.
        Cursor.lockState = CursorLockMode.None;
    }

    float ClampAngle(float angle, float min, float max)
    {
        angle = Mathf.DeltaAngle(0.0f, angle);
        return Mathf.Clamp(angle, min, max);
    }
}
