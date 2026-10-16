// 녹음기가 붙은 AI가 목소리를 내는 순간(Request1009Plan.md §26.3.1 Q10): 붙은 시각부터 5~20초를 쉬고 한 번 재생, 다시 5~20초 쉬고 재생 … AI가 사라질 때까지.
// 쉬는 시간은 씨앗값으로 정해 모든 화면이 같은 순간에 재생한다(위치·재생 신호를 보내지 않는다). 순수 계산.
public static class DecoyVoice
{
    public const float MinGap = 5f;
    public const float MaxGap = 20f;
    private const int MaxSteps = 4096;

    // k번째(0부터) 쉬는 시간.
    public static float Gap(int seed, int k)
    {
        unchecked
        {
            uint h = (uint)seed * 2654435761u ^ (uint)(k + 1) * 2246822519u;
            h ^= h >> 15; h *= 2654435761u; h ^= h >> 13;
            return MinGap + (MaxGap - MinGap) * ((h & 0xFFFFFF) / (float)0xFFFFFF);
        }
    }

    // 지금(now) 재생 중이거나 가장 최근에 시작한 재생의 시작 시각. 아직 첫 재생 전이면 false.
    public static bool TryCurrentPlay(double voiceAt, int seed, float clipSeconds, double now, out double playStart)
    {
        playStart = 0;
        if (voiceAt <= 0 || now < voiceAt) return false;
        double t = voiceAt;
        bool any = false;
        for (int k = 0; k < MaxSteps; k++)
        {
            double next = t + Gap(seed, k);
            if (next > now) break;
            playStart = next;
            any = true;
            t = next + System.Math.Max(0.1f, clipSeconds);
        }
        return any;
    }
}
