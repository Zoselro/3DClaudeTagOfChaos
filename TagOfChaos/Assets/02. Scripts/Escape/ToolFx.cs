using UnityEngine;

// 도구 명중 효과(물풍선 물보라, 스턴건 섬광, 뿅망치 충격). 짧게 커졌다 사라지는 구 하나로 표현한다(임시 효과).
public class ToolFx : MonoBehaviour
{
    private float age;
    private float life = 0.5f;
    private float maxScale = 2f;
    private Renderer rend;
    private Color color;

    public static void Play(ToolKind kind, Vector3 point, Color tint)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "ToolFx_" + kind;
        go.transform.position = point;
        var fx = go.AddComponent<ToolFx>();
        fx.color = kind == ToolKind.StunGun ? new Color(0.6f, 0.9f, 1f) : kind == ToolKind.WaterBalloon ? tint : new Color(1f, 0.85f, 0.3f);
        fx.maxScale = kind == ToolKind.WaterBalloon ? 3f : 1.2f;
        fx.life = kind == ToolKind.StunGun ? 0.25f : 0.5f;
        EscapeVisuals.Tint(go, fx.color, 2f);
        fx.rend = go.GetComponent<Renderer>();
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = age / life;
        transform.localScale = Vector3.one * Mathf.Lerp(0.2f, maxScale, t);
        if (t >= 1f) Destroy(gameObject);
    }
}
