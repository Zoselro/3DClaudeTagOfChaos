using UnityEngine;

public enum ToolKind
{
    Hammer,
    StunGun,
    WaterBalloon,
}

// 도구 하나의 수치(EscapePlan.md §1.8, D23). 코드를 고치지 않고 에셋 값만 바꿔 밸런스를 테스트한다.
[CreateAssetMenu(menuName = "TagOfChaos/Escape/Tool", fileName = "Tool_")]
public class ToolSO : ScriptableObject
{
    [SerializeField] private ToolKind kind;
    [SerializeField, Min(1)] private int charges = 3;
    [Tooltip("사거리(m). 뿅망치는 근접 거리, 스턴건은 사거리, 물풍선은 던지는 거리.")]
    [SerializeField, Min(0.5f)] private float range = 2f;
    [Tooltip("맞힘 판정 반지름(m). 물풍선은 터지는 범위.")]
    [SerializeField, Min(0.05f)] private float hitRadius = 0.6f;
    [SerializeField, Min(0f)] private float stunSecondsCookie = 1.5f;
    [SerializeField, Min(0f)] private float stunSecondsMonster = 1f;
    [SerializeField, Min(0f)] private float cooldownSeconds = 3f;
    [Tooltip("맞은 쿠키를 무작위 색으로 칠한다(물풍선).")]
    [SerializeField] private bool paintsTarget;

    public ToolKind Kind => kind;
    public int Charges => charges;
    public float Range => range;
    public float HitRadius => hitRadius;
    public float StunSecondsCookie => stunSecondsCookie;
    public float StunSecondsMonster => stunSecondsMonster;
    public float CooldownSeconds => cooldownSeconds;
    public bool PaintsTarget => paintsTarget;

#if UNITY_EDITOR
    public void EditorSetup(ToolKind toolKind, int count, float rangeMeters, float radius, float cookieStun, float monsterStun, float cooldown, bool paints)
    {
        kind = toolKind;
        charges = count;
        range = rangeMeters;
        hitRadius = radius;
        stunSecondsCookie = cookieStun;
        stunSecondsMonster = monsterStun;
        cooldownSeconds = cooldown;
        paintsTarget = paints;
    }
#endif
}
