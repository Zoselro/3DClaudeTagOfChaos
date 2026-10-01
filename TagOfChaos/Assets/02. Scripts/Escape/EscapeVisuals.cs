using UnityEngine;

// 아이템·장치의 모양을 만든다. 아이템에 모델 프리팹이 있으면 그것을 쓰고, 없으면 색을 입힌 기본 도형으로 대신한다
// (Blender 모델이 들어오기 전의 임시 모양, EscapePlan.md §4). 만든 모양에는 충돌체가 없다.
public static class EscapeVisuals
{
    private static Material baseMaterial;

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
        if (baseMaterial == null)
        {
            Shader shader = Shader.Find("Standard");
            baseMaterial = new Material(shader);
        }
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            var m = new Material(baseMaterial) { color = color };
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
            }
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
