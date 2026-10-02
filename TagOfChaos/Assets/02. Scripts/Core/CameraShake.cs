using UnityEngine;

// 화면 흔들림(research.md R4.7-9). 흔들고 싶은 쪽(마녀 내리치기 등)은 Add만 부르고, 카메라(3인칭 Camera_Ctrl·괴물 1인칭)가
// 자기 LateUpdate 끝에서 Offset을 더한다. 예전에는 마녀가 Update에서 카메라 위치를 직접 흔들었는데, 같은 프레임 카메라 LateUpdate가
// 위치를 다시 써서 흔들림이 한 번도 그려지지 않았다. 흔들림은 시간이 지나면 선형으로 줄어든다.
public static class CameraShake
{
    private static float strength;
    private static float startTime;
    private static float duration = 1f;

    // amount: 처음 흔들림 크기(m), seconds: 0까지 줄어드는 시간. 이미 흔들리는 중이면 남은 세기와 비교해 더 센 쪽을 쓴다.
    public static void Add(float amount, float seconds)
    {
        if (amount <= 0f || seconds <= 0f) return;
        if (amount < CurrentStrength) return;
        strength = amount;
        duration = seconds;
        startTime = Time.time;
    }

    public static float CurrentStrength
    {
        get
        {
            float left = 1f - (Time.time - startTime) / duration;
            return left > 0f ? strength * left : 0f;
        }
    }

    public static Vector3 Offset
    {
        get
        {
            float s = CurrentStrength;
            return s > 0f ? Random.insideUnitSphere * s : Vector3.zero;
        }
    }
}
