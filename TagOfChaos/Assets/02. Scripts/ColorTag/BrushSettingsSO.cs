using UnityEngine;

[CreateAssetMenu(menuName = "ColorTag/BrushSettings")]
public class BrushSettingsSO : ScriptableObject
{
    [SerializeField] private GameObject cursorPrefab; // 3D 붓 커서 프리팹 (GameScenePlan.md 12장)
    [SerializeField] private float cursorWorldScale = 0.05f; // FBX 원본 크기(약 2m)를 캐릭터 대비 붓 크기로 축소

    [Header("Brush Radius (UV 기준)")]
    [SerializeField] private float minRadius = 0.005f;
    [SerializeField] private float maxRadius = 0.1f;
    [SerializeField] private float defaultRadius = 0.02f;
    [SerializeField] private float wheelStep = 0.002f; // 마우스 휠 1틱당 변화량

    // 붓 커서를 표면 법선 방향으로 살짝 띄우는 값. 캐릭터 몸속으로 파고든 것처럼 보이는 현상을
    // 방지하는 보조 조치(Bug-fix-plan.md §20.7) — 콜라이더가 실시간 포즈를 정확히 반영하게 된
    // 뒤에도(§20.6) 모델 두께에 따라 표면에 딱 붙어 보일 수 있어 약간의 여유를 둔다.
    [SerializeField] private float cursorSurfaceOffset = 0.01f;

    public GameObject CursorPrefab => cursorPrefab;
    public float CursorWorldScale => cursorWorldScale;
    public float MinRadius => minRadius;
    public float MaxRadius => maxRadius;
    public float DefaultRadius => defaultRadius;
    public float WheelStep => wheelStep;
    public float CursorSurfaceOffset => cursorSurfaceOffset;
}
