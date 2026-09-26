using UnityEngine;

// 게임 입력을 읽는 유일한 창구(research.md §12 E5). 게임 코드는 "의도"(이동·점프·회피·그랩·색칠·시점 회전)만
// 묻고, 어떤 키인지와 어떤 입력 API인지는 여기서만 안다. 레거시 Input Manager를 쓰고 있으며, Input System
// 패키지로 옮길 때는 이 파일만 바꾸면 된다. 키 값은 Resources/InputBindings 에셋(InputBindingsSO)에서 읽는다.
public static class PlayerInput
{
    private const string ResourcePath = "InputBindings";
    private static InputBindingsSO bindings;

    public static InputBindingsSO Bindings
    {
        get
        {
            if (bindings == null)
            {
                bindings = Resources.Load<InputBindingsSO>(ResourcePath);
                if (bindings == null)
                {
                    Debug.LogWarning($"[PlayerInput] Resources/{ResourcePath} not found. Using default bindings.");
                    bindings = ScriptableObject.CreateInstance<InputBindingsSO>();
                }
            }
            return bindings;
        }
    }

    // 이동 입력(x=좌우, y=앞뒤), 각 축 -1~1.
    public static Vector2 Move => new Vector2(Input.GetAxisRaw(Bindings.HorizontalAxis), Input.GetAxisRaw(Bindings.VerticalAxis));

    // 카메라가 바라보는 방향 기준의 수평 이동 방향(정규화, 입력이 없으면 0). 쿠키·괴물 공용(3인칭 궤도 카메라 전제).
    public static Vector3 CameraRelativeMove(Transform cameraTransform)
    {
        if (cameraTransform == null) return Vector3.zero;
        Vector2 move = Move;
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        return (forward * move.y + right * move.x).normalized;
    }

    public static bool RunHeld => Input.GetKey(Bindings.RunKey);
    public static bool JumpPressed => Input.GetKeyDown(Bindings.JumpKey);
    public static bool DodgePressed => Input.GetKeyDown(Bindings.DodgeKey);
    public static bool GrabPressed => Input.GetKeyDown(Bindings.GrabKey);
    public static bool TentacleDashPressed => Input.GetKeyDown(Bindings.TentacleDashKey);
    public static bool SpectateNextPressed => Input.GetKeyDown(Bindings.SpectateNextKey);
    public static bool ChatSubmitReleased => Input.GetKeyUp(Bindings.ChatSubmitKey);

    public static Vector2 PointerPosition => Input.mousePosition;
    public static bool PaintHeld => Input.GetMouseButton(Bindings.PaintButton);
    public static float BrushSizeDelta => Input.mouseScrollDelta.y;

    public static bool CameraRotateHeld => Input.GetMouseButton(Bindings.CameraRotateButton);
    public static bool CameraRotatePressed => Input.GetMouseButtonDown(Bindings.CameraRotateButton);
    public static bool CameraRotateReleased => Input.GetMouseButtonUp(Bindings.CameraRotateButton);
    public static Vector2 CameraRotateDelta => new Vector2(Input.GetAxis(Bindings.MouseXAxis), Input.GetAxis(Bindings.MouseYAxis));
}
