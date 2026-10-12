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

    // 같은 (색, 발광) 조합은 재질 하나를 같이 쓴다 — 예전에는 칠할 때마다 렌더러 수만큼 새 재질이 생겼다(F-15).
    // 씬이 내려가면 목록만 비워, 다음 씬 로드의 미사용 에셋 정리가 옛 재질을 거둬 가게 한다.
    private static readonly System.Collections.Generic.Dictionary<TintKey, Material> tintCache =
        new System.Collections.Generic.Dictionary<TintKey, Material>();
    private static bool tintCacheHooked;

    private struct TintKey : System.IEquatable<TintKey>
    {
        public Color Color;
        public float Emission;
        public bool Equals(TintKey o) => Color == o.Color && Emission.Equals(o.Emission);
        public override bool Equals(object obj) => obj is TintKey o && Equals(o);
        public override int GetHashCode() => Color.GetHashCode() * 31 + Emission.GetHashCode();
    }

    public static void Tint(GameObject go, Color color, float emission)
    {
        if (!tintCacheHooked)
        {
            tintCacheHooked = true;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += _ => tintCache.Clear();
        }
        var key = new TintKey { Color = color, Emission = emission };
        if (!tintCache.TryGetValue(key, out Material mat) || mat == null)
        {
            mat = NewMaterial(color, emission, fade: false);
            tintCache[key] = mat;
        }
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = mat;
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

    // ---------------- 코드로 만드는 파티클·승객 인형(AuditFixPlan.md F-14 — 6곳·3곳에 흩어져 있던 것을 하나로) ----------------

    // 파티클 모양 값. 지정하지 않은 값(0·null)은 ParticleSystem 기본값을 그대로 둔다.
    public struct ParticleStyle
    {
        public Vector2 Lifetime, Speed, Size; // (최소, 최대) — 같으면 상수
        public Color Color;
        public float Gravity;
        public int MaxParticles;
        public float? Rate;                    // 초당 방출(null = 기본값)
        public bool EmissionOff;               // 연속 방출 끔(Emit·한 번 터뜨리기용)
        public ParticleSystemShapeType Shape;
        public float Angle, Radius;
        public Vector3 BoxScale, ShapeRotation;
        public float GrowTo;                   // > 0: 수명 동안 0.6배 → GrowTo배로 커짐
        public Color FadeFrom, FadeTo;         // 수명 동안 색(시작 → 끝)
        public float AlphaFrom;                // 시작 알파(끝은 0)
        public bool Manual;                    // 비운 채 멈춰 두고 자동 재생하지 않음
        public bool Once;                      // 반복하지 않음
    }

    public static ParticleSystem CreateParticles(string name, Transform parent, ParticleStyle s)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        if (s.Manual) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        if (s.Manual) main.playOnAwake = false;
        if (s.Once) main.loop = false;
        main.startLifetime = Range(s.Lifetime);
        main.startSpeed = Range(s.Speed);
        main.startSize = Range(s.Size);
        main.startColor = s.Color;
        if (s.Gravity != 0f) main.gravityModifier = s.Gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        if (s.MaxParticles > 0) main.maxParticles = s.MaxParticles;
        var emission = ps.emission;
        if (s.EmissionOff) emission.enabled = false;
        if (s.Rate.HasValue) emission.rateOverTime = s.Rate.Value;
        var shape = ps.shape;
        shape.shapeType = s.Shape;
        if (s.Angle > 0f) shape.angle = s.Angle;
        if (s.Radius > 0f) shape.radius = s.Radius;
        if (s.BoxScale != Vector3.zero) shape.scale = s.BoxScale;
        if (s.ShapeRotation != Vector3.zero) shape.rotation = s.ShapeRotation;
        if (s.GrowTo > 0f)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, s.GrowTo));
        }
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(s.FadeFrom, 0f), new GradientColorKey(s.FadeTo, 1f) },
                     new[] { new GradientAlphaKey(s.AlphaFrom, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = grad;
        go.GetComponent<ParticleSystemRenderer>().material = SoftParticleMaterial(); // 둥근 부드러운 퍼프(텍스처 없으면 네모로 보인다)
        return ps;
    }

    private static ParticleSystem.MinMaxCurve Range(Vector2 v) =>
        Mathf.Approximately(v.x, v.y) ? new ParticleSystem.MinMaxCurve(v.x) : new ParticleSystem.MinMaxCurve(v.x, v.y);

    // 탈것·탑승 연출의 승객 인형(카탈로그 모델, 없으면 캡슐). seatedScale이 있으면 좌석 자리에 맞춰 앉힌다.
    public static GameObject CreatePassenger(Transform parent, float? seatedScale)
    {
        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        GameObject model = catalog != null && catalog.PassengerModel != null
            ? Object.Instantiate(catalog.PassengerModel, parent, false)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        if (model.transform.parent != parent) model.transform.SetParent(parent, false);
        if (seatedScale.HasValue)
        {
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * seatedScale.Value;
        }
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        return model;
    }
}
