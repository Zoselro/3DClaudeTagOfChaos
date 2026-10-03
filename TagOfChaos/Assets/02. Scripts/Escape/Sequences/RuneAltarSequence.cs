using System.Collections.Generic;
using UnityEngine;

// 진저브레드 탈출 장치: 시계탑 아래 쿠키 유적의 룬 제단 → 포탈(EscapeVisualPlan.md §5.5).
//   평소       : 제단 빛은 희미한 보라, 낡은 등불(Lantern_nn)이 깜빡인다, 돌 유적은 푸른 채움 빛(CaveLight_nn)
//   완성 0~2초 : 제단이 무지개빛으로 빛나며 점점 밝아진다
//   2~4초      : 제단 위에 포탈(무지개 고리 + 소용돌이)이 열린다 → 탈 수 있음
//   탑승        : 쿠키가 포탈 속으로 빨려 들어가(BoardingFx.Suck) 포탈 안에 둥둥 떠서 기다린다
//   출발        : 포탈이 무지개빛으로 크게 번쩍이며 닫힌다
// 모델 이름 규칙은 escape_devices.py(device_gingerbread)와 같다.
public class RuneAltarSequence : EscapeSequence
{
    private const float GlowSeconds = 2f;
    private const float PortalOpenSeconds = 2f;
    private const float FlashSeconds = 1.2f;
    private const float CloseSeconds = 1.2f;
    private const float HueSpeed = 0.35f;        // 무지개가 한 바퀴 도는 빠르기(초당)
    private const float FloatScale = 0.55f;

    private Transform ring, swirl, mouth;
    private Renderer[] glowRenderers = new Renderer[0];
    private Renderer[] portalRenderers = new Renderer[0];
    private readonly List<Light> lanterns = new List<Light>();
    private readonly List<float> lanternSeed = new List<float>();
    private readonly List<Transform> floats = new List<Transform>();
    private readonly List<GameObject> floating = new List<GameObject>();
    private readonly List<float> floatReadyAt = new List<float>();
    private Light altarLight;
    private MaterialPropertyBlock block;
    private int hopsPlayed;

    public override float BoardReadySeconds => GlowSeconds + PortalOpenSeconds + 0.2f;
    public override float DepartureSeconds => FlashSeconds + CloseSeconds + 0.4f;
    public override Transform DepartureFocus => mouth != null ? mouth : BoardPoint;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        ring = FindDeep(transform, "Portal_Ring");
        swirl = FindDeep(transform, "Portal_Swirl");
        mouth = FindDeep(transform, "Mouth") ?? transform;
        Transform glow = FindDeep(transform, "Altar_Glow");
        if (glow != null) glowRenderers = glow.GetComponentsInChildren<Renderer>(true);
        var portal = new List<Renderer>();
        foreach (Transform t in new[] { ring, swirl })
            if (t != null) portal.AddRange(t.GetComponentsInChildren<Renderer>(true));
        portalRenderers = portal.ToArray();

        for (int i = 0; ; i++)
        {
            Transform anchor = FindDeep(transform, $"Lantern_{i:00}");
            if (anchor == null) break;
            var light = anchor.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = 11f;
            light.intensity = 1.6f;
            lanterns.Add(light);
            lanternSeed.Add(i * 1.7f);
        }
        // 돌 유적의 은은한 푸른 채움 빛(2026-10-03) — 깜빡이지 않는다. 길과 돌 색이 보일 만큼만.
        for (int i = 0; ; i++)
        {
            Transform anchor = FindDeep(transform, $"CaveLight_{i:00}");
            if (anchor == null) break;
            var fill = anchor.gameObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.62f, 0.72f, 0.95f);
            fill.range = 17f;
            fill.intensity = 1.3f;
            fill.shadows = LightShadows.None;
        }
        for (int i = 0; ; i++)
        {
            Transform f = FindDeep(transform, $"Float_{i:00}");
            if (f == null) break;
            floats.Add(f);
        }

        altarLight = new GameObject("AltarLight").AddComponent<Light>();
        altarLight.transform.SetParent(transform, false);
        altarLight.transform.position = mouth.position;
        altarLight.type = LightType.Point;
        altarLight.range = 22f;
    }

    public override void Tick(EscapeState state, double now)
    {
        float c = state != null ? Since(state.CompletedAt, now) : -1f;
        float d = state != null ? Since(state.DepartedAt, now) : -1f;

        // 낡은 등불: 저마다 다른 박자로 깜빡인다
        for (int i = 0; i < lanterns.Count; i++)
        {
            float n = Mathf.PerlinNoise(Time.time * 3f + lanternSeed[i], lanternSeed[i]);
            lanterns[i].intensity = n < 0.25f ? 0.3f : 1.2f + n;
        }

        // 제단 빛: 평소 희미한 보라 → 완성되면 무지개빛으로 밝아짐
        float power = c < 0f ? 0f : Mathf.Clamp01(c / GlowSeconds);
        Color rainbow = Color.HSVToRGB(Mathf.Repeat(Time.time * HueSpeed, 1f), 0.75f, 1f);
        Color altar = Color.Lerp(new Color(0.45f, 0.25f, 0.6f), rainbow, power);
        float intensity = Mathf.Lerp(0.6f, 3f, power);
        Tint(glowRenderers, altar, intensity);

        // 포탈: 열림(크기 0 → 1) → 출발 때 크게 번쩍인 뒤 닫힘
        float open = c < GlowSeconds ? 0f : Ease((c - GlowSeconds) / PortalOpenSeconds);
        float flash = d >= 0f ? Mathf.Sin(Mathf.Clamp01(d / FlashSeconds) * Mathf.PI) : 0f;
        if (d >= FlashSeconds) open *= 1f - Ease((d - FlashSeconds) / CloseSeconds);
        bool portalShown = open > 0.01f;
        foreach (Transform t in new[] { ring, swirl })
        {
            if (t == null) continue;
            if (t.gameObject.activeSelf != portalShown) t.gameObject.SetActive(portalShown);
            t.localScale = Vector3.one * (open * (1f + 0.25f * flash));
        }
        if (swirl != null) swirl.localRotation = Quaternion.Euler(Time.time * 120f, 0f, 0f);
        Color portalColor = Color.HSVToRGB(Mathf.Repeat(Time.time * HueSpeed + 0.5f, 1f), 0.6f, 1f);
        Tint(portalRenderers, Color.Lerp(portalColor, Color.white, flash), 1.1f + flash * 3f);

        altarLight.color = Color.Lerp(altar, Color.white, flash);
        altarLight.intensity = power * 1.8f + open * 1.5f + flash * 4f;
        altarLight.range = 20f + flash * 10f;

        UpdateFloating(state != null ? state.Waiting.Count : 0, portalShown);
    }

    public override void OnCookieBoarded(Vector3 from)
    {
        int slot = Mathf.Min(hopsPlayed, floats.Count - 1);
        hopsPlayed++;
        while (slot >= 0 && floatReadyAt.Count <= slot) floatReadyAt.Add(0f);
        if (slot >= 0) floatReadyAt[slot] = Time.time + 0.9f;
        BoardingFx.Play(from, mouth.position, BoardingFx.Style.Suck);
    }

    // 탄 쿠키는 포탈 안에서 둥둥 떠 기다린다(작게, 천천히 돌며).
    private void UpdateFloating(int count, bool portalShown)
    {
        count = portalShown ? Mathf.Min(count, floats.Count) : 0;
        if (count == 0 && !portalShown) hopsPlayed = 0;
        for (int i = 0; i < floats.Count; i++)
        {
            bool show = i < count && (i >= floatReadyAt.Count || Time.time >= floatReadyAt[i]);
            while (floating.Count <= i) floating.Add(null);
            if (show && floating[i] == null) floating[i] = CreateFloater(floats[i]);
            else if (!show && floating[i] != null) { Destroy(floating[i]); floating[i] = null; }
            if (floating[i] != null)
            {
                floating[i].transform.localPosition = Vector3.up * (0.15f * Mathf.Sin(Time.time * 2f + i));
                floating[i].transform.localRotation = Quaternion.Euler(0f, Time.time * 40f + i * 50f, 10f * Mathf.Sin(Time.time + i));
            }
        }
    }

    private static GameObject CreateFloater(Transform anchor)
    {
        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        GameObject model = catalog != null && catalog.PassengerModel != null
            ? Instantiate(catalog.PassengerModel, anchor, false)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        model.transform.SetParent(anchor, false);
        model.transform.localScale = Vector3.one * FloatScale;
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
        return model;
    }

    private void Tint(Renderer[] renderers, Color color, float intensity)
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * intensity);
            r.SetPropertyBlock(block);
        }
    }
}
