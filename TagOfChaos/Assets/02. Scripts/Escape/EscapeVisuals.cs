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
