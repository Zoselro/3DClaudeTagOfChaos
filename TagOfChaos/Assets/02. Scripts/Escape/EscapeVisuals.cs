using UnityEngine;

// 아이템·장치의 모양을 만든다. 아이템에 모델 프리팹이 있으면 그것을 쓰고, 없으면 색을 입힌 기본 도형으로 대신한다
// (Blender 모델이 들어오기 전의 임시 모양, EscapePlan.md §4). 만든 모양에는 충돌체가 없다.
public static class EscapeVisuals
{
    public static GameObject CreateItemModel(ItemSO item, Transform parent)
    {
        GameObject go;
        if (item != null && item.ModelPrefab != null)
        {
            go = Object.Instantiate(item.ModelPrefab, parent, false);
        }
        else
        {
            PrimitiveType shape = PrimitiveType.Cube;
            Vector3 scale = new Vector3(0.45f, 0.35f, 0.35f);
            if (item != null && item.IsTool)
            {
                switch (item.Tool.Kind)
                {
                    case ToolKind.WaterBalloon: shape = PrimitiveType.Sphere; scale = Vector3.one * 0.3f; break;
                    case ToolKind.StunGun: shape = PrimitiveType.Cube; scale = new Vector3(0.12f, 0.18f, 0.4f); break;
                    default: shape = PrimitiveType.Cylinder; scale = new Vector3(0.18f, 0.3f, 0.18f); break;
                }
            }
            go = GameObject.CreatePrimitive(shape);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            Tint(go, item != null ? item.Tint : Color.white, item != null && !item.IsTool ? 0.6f : 0f);
        }
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        go.name = item != null ? "Item_" + item.ItemId : "Item";
        return go;
    }

    private static Material softParticle;

    // 연기·빛 입자용: 가운데가 진하고 가장자리로 갈수록 투명한 둥근 점(64×64, 코드로 한 번 만든다).
    // 빌드에는 씬·에셋이 쓰는 셰이더만 들어가서 Shader.Find로 만든 파티클 재질은 분홍색으로 깨진다 — Resources의 재질 에셋을 쓴다.
    public const string SoftParticleResource = "Escape/SoftParticle";

    public static Material SoftParticleMaterial()
    {
        if (softParticle != null) return softParticle;
        softParticle = Resources.Load<Material>(SoftParticleResource);
        if (softParticle != null) return softParticle;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        // 셰이더가 빌드에서 빠졌으면 항상 포함되는 Sprites/Default로 대신한다 — null 셰이더로 만들면 예외가 나서 연출 전체가 멈춘다
        Shader shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        softParticle = new Material(shader) { mainTexture = tex };
        softParticle.SetFloat("_Mode", 2f); // Fade
        softParticle.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        softParticle.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        softParticle.SetInt("_ZWrite", 0);
        softParticle.EnableKeyword("_ALPHABLEND_ON");
        softParticle.renderQueue = 3000;
        return softParticle;
    }

    public static void Tint(GameObject go, Color color, float emission)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = NewMaterial(color, emission, fade: false);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // ---------------- Standard 재질 틀 ----------------
    // 빌드는 실제 재질 에셋이 쓰는 키워드 조합의 셰이더 변형만 넣는다(research.md R5-21). 코드로 켜는 _EMISSION·Fade(_ALPHABLEND_ON)
    // 조합이 빠지지 않게 Resources에 네 조합의 재질 에셋을 두고, 런타임 재질은 그 틀을 복제한다(에디터: Tools/TagOfChaos/Escape/Build Material Templates).
    public const string OpaqueTemplate = "Escape/StandardOpaque";
    public const string OpaqueGlowTemplate = "Escape/StandardOpaqueGlow";
    public const string FadeTemplate = "Escape/StandardFade";
    public const string FadeGlowTemplate = "Escape/StandardFadeGlow";
    public const string EmissionKeyword = "_EMISSION";
    public const string FadeKeyword = "_ALPHABLEND_ON";

    private static readonly Material[] templates = new Material[4];

    public static string TemplateName(bool fade, bool glow) =>
        fade ? (glow ? FadeGlowTemplate : FadeTemplate) : (glow ? OpaqueGlowTemplate : OpaqueTemplate);

    private static Material Template(bool fade, bool glow)
    {
        int index = (fade ? 2 : 0) + (glow ? 1 : 0);
        if (templates[index] != null) return templates[index];
        Material asset = Resources.Load<Material>(TemplateName(fade, glow));
        if (asset == null) // 에셋이 없으면(에디터에서 아직 안 만듦) 예전처럼 코드로 만든다 — 빌드에서는 변형이 빠질 수 있다
        {
            asset = new Material(Shader.Find("Standard"));
            ConfigureTemplate(asset, fade, glow);
        }
        return templates[index] = asset;
    }

    // 틀 재질의 렌더 설정. 에디터 빌더도 이것으로 에셋을 만든다(설정이 한 곳에만 있게).
    public static void ConfigureTemplate(Material m, bool fade, bool glow)
    {
        if (fade) ApplyFade(m);
        if (glow) m.EnableKeyword(EmissionKeyword);
        else m.DisableKeyword(EmissionKeyword);
    }

    // Built-in Standard 셰이더를 Fade 모드로(알파로 서서히 나타나게).
    public static void ApplyFade(Material m)
    {
        m.SetFloat("_Mode", 2f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword(FadeKeyword);
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    // 색(과 발광 세기)을 입힌 새 재질. 쓰는 쪽이 수명을 관리한다.
    public static Material NewMaterial(Color color, float emission, bool fade)
    {
        bool glow = emission > 0f;
        var m = new Material(Template(fade, glow)) { color = color };
        if (glow) m.SetColor("_EmissionColor", color * emission);
        return m;
    }
}
