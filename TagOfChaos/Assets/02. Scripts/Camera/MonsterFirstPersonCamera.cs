using UnityEngine;

// 괴물 1인칭 카메라(GameFixPlan.md F6). Main Camera에 Camera_Ctrl과 함께 붙고, 둘 중 하나만 켜진다
// (MonsterViewSwitcher가 켜고 끈다). 눈 위치(EyeSocket)에서 마우스 이동만으로 시점을 돌린다 — 3인칭처럼
// 우클릭을 누를 필요가 없으므로 켜져 있는 동안 커서를 잠근다. 결과 화면에서는 버튼을 눌러야 하므로 커서를 푼다.
public class MonsterFirstPersonCamera : MonoBehaviour
{
    [SerializeField] private float sensitivityX = 3f;
    [SerializeField] private float sensitivityY = 2f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 70f;

    private Transform eye;
    private float yaw;
    private float pitch;

    // 몸이 바라볼 수평 방향(MonsterController가 1인칭일 때 이 방향으로 몸을 돌린다).
    public Quaternion YawRotation => Quaternion.Euler(0f, yaw, 0f);

    public void Attach(Transform bodyRoot, Transform eyeSocket)
    {
        eye = eyeSocket;
        yaw = bodyRoot.eulerAngles.y;
        pitch = 0f;
    }

    private void OnEnable()
    {
        if (!IsResultShown()) Cursor.lockState = CursorLockMode.Locked;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
    }

    private void LateUpdate()
    {
        if (eye == null) return;

        if (IsResultShown())
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            Vector2 delta = PlayerInput.LookDelta;
            yaw += delta.x * sensitivityX;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivityY, minPitch, maxPitch);
        }

        transform.SetPositionAndRotation(eye.position, Quaternion.Euler(pitch, yaw, 0f));
    }

    // 3인칭에서 1인칭으로 돌아올 때 3인칭 카메라가 보던 수평 방향을 이어받는다.
    public void SyncYaw(float worldYaw) => yaw = worldYaw;

    private static bool IsResultShown() => GamePhaseState.Current == GamePhase.Result;
}
