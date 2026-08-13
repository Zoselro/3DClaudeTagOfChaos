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

    public GameObject CursorPrefab => cursorPrefab;
    public float CursorWorldScale => cursorWorldScale;
    public float MinRadius => minRadius;
    public float MaxRadius => maxRadius;
    public float DefaultRadius => defaultRadius;
    public float WheelStep => wheelStep;
}
