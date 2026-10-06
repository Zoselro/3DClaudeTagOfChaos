using UnityEngine;

// 고장 난 전구 깜빡임(TwistedCandyPlan.md §3·V1). 평소엔 잔떨림만 있다가 가끔 몇 번 꺼졌다 켜진다.
// 강도만 바꾼다(그림자·범위는 그대로 — 프레임 비용 없음). 연출 전용이라 네트워크 동기화는 없다.
// 분위기 적용 도구(TwistedAtmosphere)가 맵마다 정한 조명에 붙인다.
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [SerializeField, Min(0f)] private float baseIntensity = 1f;
    [Tooltip("평소 잔떨림 폭(기본 강도 대비 비율).")]
    [SerializeField, Range(0f, 0.5f)] private float jitter = 0.12f;
    [Tooltip("다음 끊김까지 기다리는 시간(초) 범위.")]
    [SerializeField] private Vector2 burstGap = new Vector2(3f, 9f);
    [Tooltip("한 번 끊길 때 꺼졌다 켜지는 횟수 범위.")]
    [SerializeField] private Vector2Int burstBlinks = new Vector2Int(2, 5);
    [Tooltip("꺼졌을 때 남는 밝기(기본 강도 대비 비율).")]
    [SerializeField, Range(0f, 1f)] private float offLevel = 0.08f;

    private const float BlinkStep = 0.07f; // 한 번 꺼짐/켜짐 길이(초)

    private Light lightComponent;
    private float seed;
    private float nextBurst;
    private int blinksLeft;
    private float nextBlink;
    private bool off;

    public float BaseIntensity => baseIntensity;

    public void EditorSetup(float intensity, float jitterAmount, Vector2 gap)
    {
        baseIntensity = intensity;
        jitter = jitterAmount;
        burstGap = gap;
    }

    private void Awake()
    {
        lightComponent = GetComponent<Light>();
        seed = Random.value * 100f;
        nextBurst = Time.time + Random.Range(burstGap.x, burstGap.y);
    }

    private void OnDisable()
    {
        if (lightComponent != null) lightComponent.intensity = baseIntensity;
    }

    private void Update()
    {
        float now = Time.time;
        if (blinksLeft == 0 && now >= nextBurst)
        {
            blinksLeft = Random.Range(burstBlinks.x, burstBlinks.y + 1) * 2; // 꺼짐+켜짐 한 쌍
            nextBlink = now;
        }
        if (blinksLeft > 0 && now >= nextBlink)
        {
            off = !off;
            blinksLeft--;
            nextBlink = now + BlinkStep * Random.Range(0.6f, 1.8f);
            if (blinksLeft == 0)
            {
                off = false;
                nextBurst = now + Random.Range(burstGap.x, burstGap.y);
            }
        }

        float k = off ? offLevel : 1f + (Mathf.PerlinNoise(seed, now * 6f) - 0.5f) * 2f * jitter;
        lightComponent.intensity = baseIntensity * k;
    }
}
