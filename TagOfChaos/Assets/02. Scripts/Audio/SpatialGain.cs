using UnityEngine;

// 3D 소리의 감쇠 곡선 종류(DistanceFadePlan.md §2.2). 카탈로그 항목마다 하나를 고른다.
public enum SoundFalloff
{
    Footstep, // 쿠키 발소리·점프 — 가까이서 또렷, 부드럽게 줄어 최대 거리에서 0
    Heavy,    // 괴물 발소리·돌진 — 멀리서도 존재감
    Action,   // 도구·상자·아이템 — 중간 거리까지 또렷, 끝에서 빠르게 0
    Landmark, // 공장 기계·회전목마·맵 연출 — 넓은 최소 거리 뒤 천천히
}

// 거리 기반 음량 감쇠(Distance-based Volume Fading, DistanceFadePlan.md §2.2~§2.3, §2.7). 순수 계산이라 EditMode에서 시험한다.
// 음량 = 거리 곡선 × 층 감쇠. 유니티 자체 감쇠는 끄고(AudioRuntime) 모든 3D 칸이 이 계산 하나를 쓴다.
public static class SpatialGain
{
    // 이 값보다 작게 들리면 재생 칸을 쓰지 않는다(§2.4).
    public const float AudibleThreshold = 0.01f;

    // 높이 차가 FloorHeight를 넘으면 다른 층으로 보고 FloorFadeEnd까지 서서히 OtherFloorGain으로(L2 — 끄지 않고 크게 줄임).
    // 4 m는 탈출 장치가 쓰는 "다른 층" 기준(EscapeDevice.MaxReachHeight)과 같다.
    public const float FloorHeight = 4f;
    public const float FloorFadeEnd = 7f;
    public const float OtherFloorGain = 0.15f;

    // 먼 소리 먹먹하게(L3): 가까우면 OpenCutoffHz(필터 없음과 같음), 최대 거리에서 FarCutoffHz, 다른 층은 OtherFloorCutoffHz 이하.
    public const float OpenCutoffHz = 22000f;
    public const float FarCutoffHz = 3000f;
    public const float OtherFloorCutoffHz = 1500f;

    // 곡선 = (최소 거리 / 거리)^Power × (1 − 진행^Taper). 앞은 실제 소리처럼 거리에 반비례해 줄고, 뒤는 최대 거리에서 정확히 0으로 닫는다.
    private static float Power(SoundFalloff f)
    {
        switch (f)
        {
            case SoundFalloff.Footstep: return 0.7f;
            case SoundFalloff.Heavy: return 0.5f;
            case SoundFalloff.Landmark: return 0.8f;
            default: return 0.6f;
        }
    }

    private static float Taper(SoundFalloff f)
    {
        switch (f)
        {
            case SoundFalloff.Footstep: return 4f;
            case SoundFalloff.Heavy: return 3f;
            case SoundFalloff.Landmark: return 2f;
            default: return 6f;
        }
    }

    // 거리 곡선만(0~1). minDistance 안 = 1, maxDistance 이상 = 0, 사이에서는 줄기만 한다.
    public static float Distance(SoundFalloff falloff, float distance, float minDistance, float maxDistance)
    {
        maxDistance = Mathf.Max(maxDistance, 0.01f);
        minDistance = Mathf.Clamp(minDistance, 0.01f, maxDistance * 0.999f);
        if (distance <= minDistance) return 1f;
        if (distance >= maxDistance) return 0f;
        float progress = (distance - minDistance) / (maxDistance - minDistance);
        return Mathf.Pow(minDistance / distance, Power(falloff)) * (1f - Mathf.Pow(progress, Taper(falloff)));
    }

    // 층 감쇠(0~1): 높이 차가 FloorHeight 이하면 1.
    public static float Floor(float heightDifference)
    {
        float t = Mathf.InverseLerp(FloorHeight, FloorFadeEnd, Mathf.Abs(heightDifference));
        return Mathf.Lerp(1f, OtherFloorGain, t);
    }

    public static float Evaluate(SoundFalloff falloff, Vector3 source, Vector3 listener, float minDistance, float maxDistance)
    {
        return Distance(falloff, Vector3.Distance(source, listener), minDistance, maxDistance) * Floor(source.y - listener.y);
    }

    // 저역 통과 차단 주파수(Hz). 귀는 주파수를 로그로 느끼므로 로그 보간.
    public static float Cutoff(Vector3 source, Vector3 listener, float minDistance, float maxDistance)
    {
        float distance = Vector3.Distance(source, listener);
        float far = Mathf.InverseLerp(minDistance, maxDistance, distance);
        float hz = LogLerp(OpenCutoffHz, FarCutoffHz, far);
        float otherFloor = Mathf.InverseLerp(FloorHeight, FloorFadeEnd, Mathf.Abs(source.y - listener.y));
        return Mathf.Min(hz, LogLerp(OpenCutoffHz, OtherFloorCutoffHz, otherFloor));
    }

    public static float LogLerp(float a, float b, float t)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), t));
    }
}
