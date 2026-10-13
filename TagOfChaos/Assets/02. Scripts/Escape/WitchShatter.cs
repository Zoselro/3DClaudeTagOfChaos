using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 마녀가 내리칠 때 맵이 박살 난다(Request1009Plan.md §23.5 X7, 결정 Q4 a). 맵 씬의 WitchPresenter 옆에 붙는다(없으면 마녀가 붙인다).
// - 주먹이 닿은 곳(WitchPresenter.ImpactPoint)에서 충격파가 초속 WaveSpeed로 퍼지며, 도착한 물체는 숨기고 그 크기·재질의 조각을 튕겨 낸다.
// - 대상은 이름 목록이 아니라 일반 규칙(맵을 바꿔도 그대로): 중심이 "Ground" 사각형 안에 있는 MeshRenderer 묶음. Ground 자신·Background·
//   FarBackdrop·캐릭터·마녀·물 같은 넓은 평면·아주 작은 것은 뺀다(IsBreakable).
// - 판이 끝나는 장면이라 네트워크로 맞추지 않는다 — 각 화면이 같은 순간(주먹이 닿은 순간)에 같은 규칙으로 보여 준다.
// - 마녀가 나타나면(걸어오는 동안) 대상을 미리 고르고 조각을 프레임마다 조금씩 만들어 둔다 — 주먹이 닿는 순간 끊기지 않게(상한 MaxPieces).
//   조각 레이어는 Ignore Raycast — 바닥에는 떨어지고 검사(레이캐스트)에는 걸리지 않는다.
// 쿠키·괴물의 몸도 같은 조각으로 부순다(ShatterBody — CookieLifeStatePresenter·MonsterEscapeState).
public class WitchShatter : MonoBehaviour
{
    public const float WaveSpeed = 60f;          // 충격파 속도(m/s) — 맵 끝까지(약 140 m) 2초 남짓
    public const int MaxPieces = 400;
    public const float PieceLifeSeconds = 3.2f;
    public const float MinBreakSize = 0.3f;      // 이보다 작은 것은 그대로
    public const float FlatHeight = 0.3f;        // 이보다 얇고 넓은 것(물·바닥 무늬)은 그대로
    public const float FlatWidth = 4f;
    private static readonly string[] KeepRoots = { WitchPath.GroundName, "Background", "FarBackdrop" };

    public static WitchShatter Current { get; private set; }

    private struct Target
    {
        public Transform Root;
        public Renderer[] Renderers;
        public Bounds Bounds;
        public float At;
    }

    private struct Piece
    {
        public Transform T;
        public Rigidbody Body;
        public Renderer Renderer;
        public float Born;
        public Vector3 Scale;
    }

    private readonly List<Target> pending = new List<Target>();
    private readonly List<Piece> pieces = new List<Piece>();
    private readonly Stack<Piece> pool = new Stack<Piece>();
    private ParticleSystem dust;
    private int budget = MaxPieces;         // 남은 조각 수(죽음 처리가 주먹보다 먼저 와도 몸 조각을 낼 수 있게 처음부터 채워 둔다)
    private bool fired;
    private List<Target> prepared;          // 마녀가 나타난 뒤 미리 고른 대상
    private const int PrewarmPerFrame = 25; // 조각을 미리 만드는 속도

    private void OnEnable()
    {
        Current = this;
        WitchPresenter.Impacted += OnImpact;
    }

    private void OnDisable()
    {
        WitchPresenter.Impacted -= OnImpact;
        if (Current == this) Current = null;
    }

    private void OnImpact()
    {
        if (fired) return;
        fired = true;
        WitchPresenter witch = WitchPresenter.Current;
        if (witch == null) return;
        Vector3 impact = witch.ImpactPoint;
        GameAudio.Play(SoundId.WitchShatter);
        if (!WitchPath.TryGetGroundRect(gameObject.scene, out Rect ground)) return;
        float now = Time.time;
        foreach (Target t in prepared ?? new List<Target>(FindTargets(gameObject.scene, ground)))
        {
            if (t.Root == null) continue;
            Target scheduled = t;
            scheduled.Bounds = CurrentBounds(t); // 미리 고른 뒤 움직였을 수 있다(떠나는 탈것·문)
            scheduled.At = now + Flat(t.Bounds.center - impact).magnitude / WaveSpeed;
            pending.Add(scheduled);
        }
        pending.Sort((a, b) => a.At.CompareTo(b.At));
        Debug.Log($"[WitchShatter] {pending.Count} object(s) break from {impact}.");
    }

    private void Update()
    {
        Prepare();
        float now = Time.time;
        int done = 0;
        while (done < pending.Count && pending[done].At <= now) Break(pending[done++]);
        if (done > 0) pending.RemoveRange(0, done);
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            Piece p = pieces[i];
            float age = now - p.Born;
            if (age < PieceLifeSeconds - 0.6f) continue;
            float k = Mathf.Clamp01((PieceLifeSeconds - age) / 0.6f);
            p.T.localScale = p.Scale * k; // 끝에서 작아지며 사라진다
            if (k > 0f) continue;
            p.T.gameObject.SetActive(false);
            pool.Push(p);
            pieces.RemoveAt(i);
        }
    }

    // 마녀가 보이기 시작하면 대상을 한 번 고르고, 조각을 프레임마다 조금씩 만들어 풀에 넣는다.
    private void Prepare()
    {
        if (fired) return;
        WitchPresenter witch = WitchPresenter.Current;
        if (witch == null || !witch.IsShown) return;
        if (prepared == null)
        {
            prepared = WitchPath.TryGetGroundRect(gameObject.scene, out Rect ground)
                ? new List<Target>(FindTargets(gameObject.scene, ground))
                : new List<Target>();
            if (dust == null) EmitDust(transform.position, 0f, prewarmOnly: true);
        }
        for (int i = 0; i < PrewarmPerFrame && pool.Count + pieces.Count < MaxPieces; i++)
        {
            Piece p = NewPiece();
            p.T.gameObject.SetActive(false);
            pool.Push(p);
        }
    }

    private static Bounds CurrentBounds(Target t)
    {
        Bounds b = t.Bounds;
        bool any = false;
        foreach (Renderer r in t.Renderers)
        {
            if (r == null) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // ---------------- 대상 고르기(맵을 바꿔도 동작하는 일반 규칙) ----------------

    public static List<(Transform root, Renderer[] renderers, Bounds bounds)> FindTargetGroups(Scene scene, Rect ground)
    {
        var groups = new Dictionary<Transform, List<Renderer>>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled || !IsBreakable(r, ground)) continue;
                Transform key = GroupRoot(r.transform);
                if (!groups.TryGetValue(key, out List<Renderer> list)) groups[key] = list = new List<Renderer>();
                list.Add(r);
            }
        var result = new List<(Transform, Renderer[], Bounds)>();
        foreach (KeyValuePair<Transform, List<Renderer>> g in groups)
        {
            Bounds b = g.Value[0].bounds;
            foreach (Renderer r in g.Value) b.Encapsulate(r.bounds);
            result.Add((g.Key, g.Value.ToArray(), b));
        }
        return result;
    }

    private static IEnumerable<Target> FindTargets(Scene scene, Rect ground)
    {
        foreach (var g in FindTargetGroups(scene, ground))
            yield return new Target { Root = g.root, Renderers = g.renderers, Bounds = g.bounds };
    }

    public static bool IsBreakable(Renderer r, Rect ground)
    {
        Bounds b = r.bounds;
        if (!ground.Contains(new Vector2(b.center.x, b.center.z))) return false;
        float wide = Mathf.Max(b.size.x, b.size.z);
        if (Mathf.Max(wide, b.size.y) < MinBreakSize) return false;
        if (b.size.y < FlatHeight && wide > FlatWidth) return false;       // 물·바닥 무늬
        for (Transform t = r.transform; t != null; t = t.parent)
        {
            if (System.Array.IndexOf(KeepRoots, t.name) >= 0) return false;
            if (t.GetComponent<WitchPresenter>() != null || t.GetComponent<IGameCharacter>() != null) return false;
        }
        return true;
    }

    // 한 물체로 부술 단위: 씬 루트에서 셋째 단계(예: Map/MainStructures/House_01) 또는 그보다 얕은 곳.
    private static Transform GroupRoot(Transform t)
    {
        var chain = new List<Transform>();
        for (Transform c = t; c != null; c = c.parent) chain.Add(c);
        return chain[Mathf.Max(0, chain.Count - 3)];
    }

    // ---------------- 부수기 ----------------

    private void Break(Target t)
    {
        foreach (Renderer r in t.Renderers) if (r != null) r.enabled = false;
        if (t.Root != null) foreach (Collider c in t.Root.GetComponentsInChildren<Collider>()) c.enabled = false;
        Material mat = t.Renderers.Length > 0 && t.Renderers[0] != null ? t.Renderers[0].sharedMaterial : null;
        Burst(t.Bounds, mat, Color.white);
    }

    // 쿠키·괴물 몸: 몸 크기·색의 조각 몇 개와 먼지.
    public static void ShatterBody(Renderer[] renderers, Color color)
    {
        if (Current == null || renderers == null || renderers.Length == 0) return;
        Bounds b = default;
        bool any = false;
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (any) Current.Burst(b, null, color);
    }

    private void Burst(Bounds b, Material mat, Color color)
    {
        Vector3 size = b.size;
        float scale = Mathf.Max(size.x, size.y, size.z);
        EmitDust(b.center, scale);
        int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(Mathf.Max(1f, size.x * size.y * size.z), 1f / 3f) * 0.6f), 2, 8);
        count = Mathf.Min(count, budget);
        budget -= count;
        WitchPresenter witch = WitchPresenter.Current;
        Vector3 origin = witch != null ? witch.ImpactPoint : b.center - Vector3.up;
        for (int i = 0; i < count; i++)
        {
            Piece p = pool.Count > 0 ? pool.Pop() : NewPiece();
            Vector3 pieceScale = new Vector3(size.x, size.y, size.z) / Mathf.Pow(count, 1f / 3f) * Random.Range(0.45f, 0.85f);
            pieceScale = Vector3.Max(pieceScale, Vector3.one * 0.15f);
            p.Scale = pieceScale;
            p.T.localScale = pieceScale;
            p.T.SetPositionAndRotation(b.center + Vector3.Scale(Random.insideUnitSphere * 0.5f, size), Random.rotation);
            if (mat != null) p.Renderer.sharedMaterial = mat;
            else p.Renderer.sharedMaterial = TintMaterial(color);
            p.T.gameObject.SetActive(true);
            Vector3 away = Flat(b.center - origin);
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Random.insideUnitSphere;
            p.Body.linearVelocity = away * Random.Range(4f, 14f) + Vector3.up * Random.Range(6f, 16f);
            p.Body.angularVelocity = Random.insideUnitSphere * 6f;
            p.Born = Time.time;
            pieces.Add(p);
        }
    }

    private Piece NewPiece()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "WitchDebris";
        go.layer = 2; // Ignore Raycast: 바닥과는 부딪히고 레이캐스트 검사에는 걸리지 않는다
        go.transform.SetParent(transform, false);
        var renderer = go.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var body = go.AddComponent<Rigidbody>();
        body.mass = 5f;
        body.linearDamping = 0.2f;
        return new Piece { T = go.transform, Body = body, Renderer = renderer };
    }

    private readonly Dictionary<Color, Material> tints = new Dictionary<Color, Material>();

    private Material TintMaterial(Color c)
    {
        if (!tints.TryGetValue(c, out Material m)) tints[c] = m = EscapeVisuals.NewMaterial(c, 0f, fade: false);
        return m;
    }

    private void EmitDust(Vector3 at, float scale, bool prewarmOnly = false)
    {
        if (dust == null)
            dust = EscapeVisuals.CreateParticles("WitchShatterDust", transform, new EscapeVisuals.ParticleStyle
            {
                Lifetime = new Vector2(1.2f, 2.2f), Speed = new Vector2(2f, 7f), Size = new Vector2(2f, 5f),
                Color = new Color(0.62f, 0.55f, 0.5f, 0.55f), Gravity = -0.02f, EmissionOff = true, Manual = true,
                MaxParticles = 1500, Shape = ParticleSystemShapeType.Sphere, Radius = 1f,
                FadeFrom = Color.white, FadeTo = Color.white, AlphaFrom = 1f, GrowTo = 2f,
            });
        if (prewarmOnly) return;
        var emit = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
        dust.Emit(emit, Mathf.Clamp(Mathf.RoundToInt(scale * 1.5f), 4, 30));
    }

    private void OnDestroy()
    {
        foreach (Material m in tints.Values) if (m != null) Destroy(m);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
