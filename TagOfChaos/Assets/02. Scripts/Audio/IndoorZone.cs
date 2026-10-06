using System.Collections.Generic;
using UnityEngine;

// 실내 소리(TwistedCandyPlan.md §5.4·V4). 숨을 곳(서커스 텐트·진저브레드 집) 안을 상자로 덮는다.
// - 듣는 사람과 소리가 벽을 사이에 두면(한쪽만 실내, 또는 서로 다른 실내) 3D 소리를 먹먹하게(저역 통과 WallCutoffHz) + 작게(WallGain).
//   같은 실내에 있으면 풀린다 — 괴물이 텐트 안으로 들어오면 발소리가 갑자기 또렷해진다.
// - 듣는 사람이 실내에 있으면 바탕 환경음(2D 환경음 묶음)도 먹먹하고 작게.
// 계산은 AudioRuntime이 매 프레임 한다(이 컴포넌트는 상자만 들고 등록한다). 상자는 이 오브젝트 기준(로컬) 좌표라 숨을 곳 프리팹을
// 어떤 방향으로 놓아도 맞다. 숨을 곳 프리팹을 만들 때(TwistedPropBuilder) Blender의 Zone_* 표시에서 채운다.
public class IndoorZone : MonoBehaviour
{
    public const float WallCutoffHz = 700f;
    public const float WallGain = 0.55f;
    public const float AmbienceCutoffHz = 900f;
    public const float AmbienceGain = 0.5f;
    public const float FadeSeconds = 0.5f; // 문을 지날 때 바뀌는 시간

    private static readonly List<IndoorZone> Active = new List<IndoorZone>();
    private static float listenerLevel;

    [SerializeField] private Bounds[] boxes = new Bounds[0];

    // 듣는 사람이 실내에 있는 정도(0~1, 서서히 바뀐다). 바탕 환경음 먹먹함에 쓴다.
    public static float ListenerLevel => listenerLevel;
    public static int ActiveCount => Active.Count;

    private void OnEnable() => Active.Add(this);

    private void OnDisable()
    {
        Active.Remove(this);
        if (Active.Count == 0) listenerLevel = 0f;
    }

    public bool Contains(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        foreach (Bounds b in boxes) if (b.Contains(local)) return true;
        return false;
    }

    public Bounds[] LocalBoxes => boxes;

    public static IndoorZone At(Vector3 point)
    {
        for (int i = 0; i < Active.Count; i++)
            if (Active[i].Contains(point)) return Active[i];
        return null;
    }

    // 벽을 사이에 뒀는지(바깥↔실내, 실내 A↔실내 B). 둘 다 바깥이거나 같은 실내면 false.
    public static bool Separated(Vector3 source, Vector3 listener) => Active.Count > 0 && At(source) != At(listener);

    public static void Tick(Vector3 listener, float deltaTime)
    {
        float target = Active.Count > 0 && At(listener) != null ? 1f : 0f;
        listenerLevel = Mathf.MoveTowards(listenerLevel, target, deltaTime / FadeSeconds);
    }

    public static float Step(float current, bool target, float deltaTime) =>
        Mathf.MoveTowards(current, target ? 1f : 0f, deltaTime / FadeSeconds);

#if UNITY_EDITOR
    public void EditorSetup(Bounds[] localBoxes) => boxes = localBoxes;
#endif
}
