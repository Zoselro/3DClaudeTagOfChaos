using UnityEngine;

// 녹음기가 붙은 AI의 걷는 길(Request1009Plan.md §25.5 L). 모든 화면과 방장이 같은 씨앗값으로 같은 길을 계산한다 — 위치를 보내지 않는다.
// 시작 자리에서 6~10 m씩 무작위 방향으로 걷는 꺾은선(초속 Speed). 맵 사각형 밖으로 나가는 방향은 안쪽으로 접는다. 순수 계산.
public static class DecoyWalk
{
    public const float Speed = 1.6f;
    private const int MaxLegs = 16;
    private const float MinLeg = 6f, MaxLeg = 10f;
    private const float EdgeInset = 6f;

    // t초 걸은 뒤의 수평 위치(높이는 시작 높이 — 화면이 바닥에 붙인다).
    public static Vector3 PositionAt(Vector3 start, int seed, float t, Rect map) => Sample(start, seed, t, map, out _);

    // 지금 바라보는 방향(도).
    public static float YawAt(Vector3 start, int seed, float t, Rect map)
    {
        Sample(start, seed, t, map, out Vector3 dir);
        return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }

    private static Vector3 Sample(Vector3 start, int seed, float t, Rect map, out Vector3 direction)
    {
        var rng = new System.Random(seed);
        Vector3 p = start;
        float remaining = Mathf.Max(0f, t) * Speed;
        direction = Vector3.forward;
        for (int leg = 0; leg < MaxLegs; leg++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float length = Mathf.Lerp(MinLeg, MaxLeg, (float)rng.NextDouble());
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 end = p + dir * length;
            if (map.width > 1f && !Inside(end, map)) // 맵 밖이면 가운데 쪽으로
            {
                dir = new Vector3(map.center.x - p.x, 0f, map.center.y - p.z).normalized;
                end = p + dir * length;
            }
            direction = dir;
            if (remaining <= length) return p + dir * remaining;
            remaining -= length;
            p = end;
        }
        return p;
    }

    private static bool Inside(Vector3 p, Rect map) =>
        p.x > map.xMin + EdgeInset && p.x < map.xMax - EdgeInset && p.z > map.yMin + EdgeInset && p.z < map.yMax - EdgeInset;
}
