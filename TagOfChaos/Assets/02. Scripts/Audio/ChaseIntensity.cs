using UnityEngine;

// 추격음을 들을 수 있는 상황(DistanceFadePlan.md §9.3, C3 = 관전 중에는 듣지 않음).
public struct ChaseContext
{
    public GamePhase Phase;
    public bool LocalIsMonster;
    public bool LocalHasCookie;  // 내가 조작하는 쿠키가 맵에 있다
    public bool LocalBroken;     // 부서져 관전 중
    public bool LocalLeftMap;    // 탈출·탑승·탈출 대기
}

// 추격음 세기·북 단계 계산(DistanceFadePlan.md §9). 순수 계산이라 EditMode에서 시험한다.
public static class ChaseIntensity
{
    public static bool CanListen(ChaseContext c) =>
        (c.Phase == GamePhase.Hunt || c.Phase == GamePhase.TimeAttack)
        && !c.LocalIsMonster && c.LocalHasCookie && !c.LocalBroken && !c.LocalLeftMap;

    // 가까운 정도(0 = audibleRadius 경계 이상, 1 = 최대 음량 반경 안).
    public static float Closeness(float distance, ChaseAudioSettingsSO s)
    {
        float full = Mathf.Min(s.FullRadius, s.AudibleRadius - 0.01f);
        return 1f - Mathf.Clamp01(Mathf.InverseLerp(full, s.AudibleRadius, distance));
    }

    // 세기(0~1) = 곡선(가까운 정도) × 층 감쇠.
    public static float Evaluate(float distance, float heightDifference, ChaseAudioSettingsSO s)
    {
        if (distance >= s.AudibleRadius) return 0f;
        float floor = Mathf.Lerp(1f, s.OtherFloorGain,
            Mathf.InverseLerp(SpatialGain.FloorHeight, SpatialGain.FloorFadeEnd, Mathf.Abs(heightDifference)));
        return s.Curve(Closeness(distance, s)) * floor;
    }

    // 북 단계(0부터, 들리지 않으면 -1). 올라갈 때는 경계에서 바로, 내려갈 때는 여유만큼 더 멀어져야 한다.
    public static int Level(float distance, int current, ChaseAudioSettingsSO s)
    {
        int target = -1;
        for (int i = 0; i < s.LevelCount; i++)
            if (distance < s.LevelRadius(i)) target = i;
        if (target >= current || current < 0 || current >= s.LevelCount) return target;
        // 내려가는 중: 지금 단계의 경계 + 여유 밖으로 나가야 한 단계 내려간다
        return distance < s.LevelRadius(current) + s.LevelHysteresis ? current : target;
    }
}
