using UnityEngine;

// 키·마우스 입력 설정(research.md §12 E5, CLAUDE.md: 전역 SO → Assets/Resources). 예전에는 7개 클래스가
// Input.GetKey(KeyCode.LeftShift) 같은 값을 각자 하드코딩해 키를 바꾸려면 여러 파일을 고쳐야 했다.
// 키 설정 UI가 생기면 이 에셋(또는 런타임 복제본)의 값만 바꾸면 된다.
[CreateAssetMenu(menuName = "TagOfChaos/InputBindings", fileName = "InputBindings")]
public class InputBindingsSO : ScriptableObject
{
    [Header("Movement (Input Manager axes)")]
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";

    [Header("Cookie")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode dodgeKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode grabKey = KeyCode.E;

    [Header("Monster")]
    [SerializeField] private KeyCode tentacleDashKey = KeyCode.LeftShift;

    [Header("Spectator / UI")]
    [SerializeField] private KeyCode spectateNextKey = KeyCode.Space;
    [SerializeField] private KeyCode chatSubmitKey = KeyCode.Return;

    [Header("Mouse")]
    [Tooltip("0=왼쪽, 1=오른쪽, 2=가운데")]
    [SerializeField, Range(0, 2)] private int paintButton = 0;
    [SerializeField, Range(0, 2)] private int cameraRotateButton = 1;
    [SerializeField] private string mouseXAxis = "Mouse X";
    [SerializeField] private string mouseYAxis = "Mouse Y";

    public string HorizontalAxis => horizontalAxis;
    public string VerticalAxis => verticalAxis;
    public KeyCode RunKey => runKey;
    public KeyCode JumpKey => jumpKey;
    public KeyCode DodgeKey => dodgeKey;
    public KeyCode GrabKey => grabKey;
    public KeyCode TentacleDashKey => tentacleDashKey;
    public KeyCode SpectateNextKey => spectateNextKey;
    public KeyCode ChatSubmitKey => chatSubmitKey;
    public int PaintButton => paintButton;
    public int CameraRotateButton => cameraRotateButton;
    public string MouseXAxis => mouseXAxis;
    public string MouseYAxis => mouseYAxis;
}
