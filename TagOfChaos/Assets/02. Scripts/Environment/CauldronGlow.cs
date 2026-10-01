using UnityEngine;

// 가마솥 보라색 액체가 은은하게 빛나며 맥동한다(GameFixPlan.md F5). 액체 머티리얼의 발광(Emission)과 액체 위 조명을
// 같은 박자로 천천히 오르내린다. 머티리얼 에셋은 바꾸지 않도록 MaterialPropertyBlock을 쓴다(액체 머티리얼은
// 빌더가 발광 키워드를 켜 둔다). 연출 전용이라 네트워크 동기화는 없다. 가마솥 빌더(CauldronBuilder)가 붙인다.
public class CauldronGlow : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer[] liquidRenderers;
    [SerializeField] private Light glowLight;
    [SerializeField, ColorUsage(false, true)] private Color emission = new Color(0.55f, 0.15f, 0.9f) * 2f;
    [SerializeField, Min(0f)] private float pulseSpeed = 1.2f;
    [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.25f;

    private MaterialPropertyBlock block;
    private float baseLightIntensity;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (glowLight != null) baseLightIntensity = glowLight.intensity;
    }

    private void Update()
    {
        float k = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        Color c = emission * k;
        foreach (Renderer r in liquidRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, c);
            r.SetPropertyBlock(block);
        }
        if (glowLight != null) glowLight.intensity = baseLightIntensity * k;
    }
}
