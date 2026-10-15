using UnityEngine;

// 연출 소리 경계·깊은 자식 찾기(예전 EscapeSequence의 정적 도움 함수 — 맵별 탈것 연출은 Request1009Plan.md §25에서 빠졌다, Legacy~).
public static class EscapeCue
{
    public const float LateTolerance = 1.5f; // 상태가 네트워크로 늦게 오거나 프레임이 끊겨도 0초 소리가 빠지지 않을 만큼

    // 시간표 소리를 지금 내야 하는지(순수 계산): 직전 프레임엔 at 전, 이번 프레임엔 at 이후, 그리고 지난 지 얼마 안 됨.
    public static bool ShouldCue(float at, float before, float now) => before < at && now >= at && now - at <= LateTolerance;

    // 이름으로 자식을 깊이 찾는다(움직이는 부품 아래에 있는 자리도 찾게).
    public static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
