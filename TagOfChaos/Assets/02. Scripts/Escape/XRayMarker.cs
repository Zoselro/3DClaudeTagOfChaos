using System.Collections.Generic;
using UnityEngine;

// 적외선 투시처럼 벽 너머로도 보이게(Request1009Plan.md §25.5 F — 열린 탈출구, 스파이 로켓). 대상 렌더러에 투시 재질을 한 장 더 얹는다.
// 투시 재질은 가려진 부분만 빛나는 색으로 그린다(셰이더 TagOfChaos/XRaySilhouette, 재질 Resources/Escape/XRay). 누구에게 보일지는 부르는 쪽이 정한다.
public class XRayMarker : MonoBehaviour
{
    private const string MaterialPath = "Escape/XRay";
    private static Material sharedBase;

    private readonly List<Renderer> renderers = new List<Renderer>();
    private Material material;
    private bool visible;

    public void Init(Color color)
    {
        if (sharedBase == null) sharedBase = Resources.Load<Material>(MaterialPath);
        if (sharedBase == null) return;
        material = new Material(sharedBase);
        material.SetColor("_Color", color);
        renderers.Clear();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r is MeshRenderer || r is SkinnedMeshRenderer) renderers.Add(r);
    }

    public void SetVisible(bool value)
    {
        if (visible == value || material == null) return;
        visible = value;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            var mats = new List<Material>(r.sharedMaterials);
            if (value) { if (!mats.Contains(material)) mats.Add(material); }
            else mats.Remove(material);
            r.sharedMaterials = mats.ToArray();
        }
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
