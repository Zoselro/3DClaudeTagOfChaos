using UnityEngine;
using UnityEngine.SceneManagement;

// 마녀가 걸어오는 길(Request1009Plan.md §23.4 Q6). 맵을 바꿔도 동작하도록 이름 목록 대신 씬의 "Ground"(걷는 바닥 + 경계벽) 범위만 쓴다.
//   시작 지점 = 맵 중심에서 WITCH_Anchor 방향으로 StartDistance
//   내리칠 자리 = 맵 중심을 향해 걷다가 발끝이 Ground 사각형의 변 또는 모서리에 닿는 곳
public static class WitchPath
{
    public const string GroundName = "Ground";
    public const string FarBackdropName = "FarBackdrop"; // 마녀가 걸어오는 넓힌 배경(WitchBackdropBuilder) — 부수지도 가림막으로 숨기지도 않는다
    public const float StartDistance = 450f;
    public const float FootReachRatio = 0.12f;   // 발끝(몸 중심 앞) = 키 × 비율 — 90 m 마녀면 약 11 m
    public const float FallbackStrikeDistance = 155f; // Ground가 없는 맵: 예전 앵커 거리

    // 씬의 Ground 범위(수평, x·z). 맵 루트 "Map" 아래를 먼저, 없으면 씬 전체에서 이름이 Ground인 오브젝트.
    public static bool TryGetGroundRect(Scene scene, out Rect rect)
    {
        rect = default;
        Transform ground = FindGround(scene);
        if (ground == null) return false;
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in ground.GetComponentsInChildren<Renderer>(true)) Grow(ref b, ref any, r.bounds);
        foreach (Collider c in ground.GetComponentsInChildren<Collider>(true)) Grow(ref b, ref any, c.bounds);
        if (!any || b.size.x < 1f || b.size.z < 1f) return false;
        rect = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
        return true;
    }

    public static Transform FindGround(Scene scene)
    {
        if (!scene.IsValid()) return null;
        Transform any = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Map")
            {
                Transform t = root.transform.Find(GroundName);
                if (t != null) return t;
            }
            if (any == null) any = EscapeSequence.FindDeep(root.transform, GroundName); // 깊은 자식 찾기는 한 곳(F-14)
        }
        return any;
    }

    private static void Grow(ref Bounds b, ref bool any, Bounds add)
    {
        if (add.size == Vector3.zero) return;
        if (!any) { b = add; any = true; }
        else b.Encapsulate(add);
    }

    // 맵 중심 → 앵커 방향(수평 단위 벡터). 앵커가 중심과 겹치면 +Z.
    public static Vector3 OutwardDirection(Vector3 center, Vector3 anchor)
    {
        Vector3 d = anchor - center;
        d.y = 0f;
        return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
    }

    // 시작 지점에서 -outward 방향(맵 중심 쪽)으로 걸을 때 처음 사각형에 닿는 점(수평). 닿지 않으면 false.
    public static bool TryEntryPoint(Rect ground, Vector3 start, Vector3 outward, out Vector3 entry)
    {
        entry = start;
        Vector2 o = new Vector2(start.x, start.z);
        Vector2 d = -new Vector2(outward.x, outward.z);
        float tMin = 0f, tMax = float.MaxValue;
        if (!Slab(o.x, d.x, ground.xMin, ground.xMax, ref tMin, ref tMax)) return false;
        if (!Slab(o.y, d.y, ground.yMin, ground.yMax, ref tMin, ref tMax)) return false;
        Vector2 p = o + d * tMin;
        entry = new Vector3(p.x, start.y, p.y);
        return true;
    }

    private static bool Slab(float origin, float dir, float min, float max, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(dir) < 1e-6f) return origin >= min && origin <= max;
        float t1 = (min - origin) / dir, t2 = (max - origin) / dir;
        if (t1 > t2) { float s = t1; t1 = t2; t2 = s; }
        tMin = Mathf.Max(tMin, t1);
        tMax = Mathf.Min(tMax, t2);
        return tMin <= tMax;
    }

    // 내리칠 자리(마녀 발 위치): 발끝이 경계에 닿는 곳 = 닿는 점에서 바깥으로 발끝 거리만큼.
    public static Vector3 StrikePoint(Rect ground, Vector3 start, Vector3 outward, float footReach, out Vector3 contact)
    {
        if (!TryEntryPoint(ground, start, outward, out contact))
        {
            Vector3 center = new Vector3(ground.center.x, start.y, ground.center.y);
            contact = center + outward * (FallbackStrikeDistance - footReach);
            return center + outward * FallbackStrikeDistance;
        }
        return contact + outward * footReach;
    }
}
