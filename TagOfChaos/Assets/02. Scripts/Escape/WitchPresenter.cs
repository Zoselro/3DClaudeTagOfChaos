using Photon.Pun;
using UnityEngine;

// 마녀 연출(EscapePlan.md §1.2, §5.7, D38 → Request1009Plan.md §23). 맵 밖 멀리서 나타나 걸어와 맵을 내리친다. 시각은 모두 WitchTimeline.
// - 나타나는 시각(WitchAppearAt)부터 3초 동안 알파 0 → 1로 서서히 나타나며 숙인 자세에서 일어선다(Rise).
//   나타나는 곳 = 맵 중심에서 WITCH_Anchor 방향으로 450 m(WitchPath). 그동안 맵 안개를 옅게 해 멀리서도 보이게 한다(§23.4 Q5).
// - 타임어택: 맵을 등진 채 나타나 2초 동안 뒤돌아선다(TurnBack, §23.4 Q1). 처단: 처음부터 맵을 본다.
// - 걷기: 타임어택은 시작 연출이 끝난 뒤 타임어택 끝까지, 처단은 10초. 내리칠 자리는 발끝이 맵 "Ground" 사각형에 닿는 곳(Q6).
//   걷는 속도는 따로 없다 — 걷는 시간이 바뀌면 속도가 바뀌고, Walk 동작 재생 속도와 발걸음(소리·흔들림)도 그에 맞춘다.
// - 위치·방향·동작은 공통 시계로 계산하므로 모든 화면이 같고, 늦게 들어온 사람도 바로 맞는 자리에 있다.
// - WitchStrike가 기록되면 두 주먹으로 맵을 내리친다(Strike). 주먹이 닿는 순간(StrikeImpactSeconds) 흔들림·충격음과 함께
//   Impacted를 알린다 — 맵 박살(WitchShatter)이 그 자리(ImpactPoint)에서 시작된다. 죽음 확정은 방장(GameRuleController).
// - 소리(SoundPlan.md S6): 나타나기 시작 = WitchAppear, 다 나타남 = 스팅어 StWitchAppear, 발걸음 = WitchStep, 내리침 = WitchSlam.
// 자식 "Model"이 있으면 그것을 쓰고(애니메이터가 있으면 동작으로, 없으면 SlamArm 회전으로 내리침), 없으면 도형으로 만든 임시 마녀를 쓴다.
public class WitchPresenter : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private const float RaisedArmAngle = -150f;
    public const float DefaultFadeSeconds = WitchTimeline.RevealSeconds; // 카메라가 마녀에게 닿는 3초에 다 나타나게
    public const float StrikeImpactSeconds = 0.95f;  // Strike 동작에서 주먹이 맵에 닿는 시각(Blender anim.py IMPACT와 같음)
    public const float ImpactGraceSeconds = 1f;      // 이 화면의 마녀가 충격을 알리지 못할 때(없음·멈춤) 그만큼 늦게라도 죽음 처리
    public const float FistReachRatio = 0.5f;        // 주먹이 떨어지는 곳 = 발에서 앞으로 키 × 비율
    // Walk 동작(anim.py WALK_CYCLE): 2.4초에 두 걸음, 발이 닿는 순간 = 주기의 0.25·0.75
    public const float WalkCycleSeconds = 2.4f;
    public const float StepLengthRatio = 0.25f;      // 한 걸음 길이 = 키 × 비율(90 m → 22.5 m)
    // 뒤돌아서는 방향: Blender TurnBack(TURN_SIGN = +1, 위에서 반시계)과 같은 방향 — Unity 요(Y) 회전으로는 −
    public const float TurnYawSign = -1f;
    public const float WitchFogDensity = 0.003f;     // 마녀가 나타난 뒤의 안개(§23.4 Q5)
    private static readonly int RiseTrigger = Animator.StringToHash("Rise");
    private static readonly int StrikeTrigger = Animator.StringToHash("Strike");
    private static readonly int TurnTrigger = Animator.StringToHash("TurnBack");
    private static readonly int WalkingBool = Animator.StringToHash("Walking");

    public static WitchPresenter Current { get; private set; }
    // 이 화면에서 주먹이 맵에 닿은 순간(늦게 들어온 화면은 내리침을 처음 본 순간). 화면의 충격·박살과 죽음이 같은 프레임.
    public static event System.Action Impacted;

    [SerializeField, Min(0.1f)] private float fadeInSeconds = DefaultFadeSeconds;
    [SerializeField, Min(1f)] private float height = 90f;
    [SerializeField] private Transform model;
    [SerializeField] private Transform slamArm;

    private Renderer[] renderers = new Renderer[0];
    private MaterialPropertyBlock block;
    private const float LateSoundSeconds = EscapeSequence.CueLateTolerance; // 맵 연출과 같은 기준

    private bool fadeDone;
    private bool activeBefore; // 지난 프레임에도 보였는지(처음 보인 프레임에 이미 내리친 상태면 충격음을 내지 않는다)
    private bool struck;
    private float strikeTime;
    private Animator animator;
    private Transform head, hat;
    private bool impactDone;
    private double turnTriggeredFor = double.NaN;
    private int lastStep = int.MinValue;

    // 길(씬이 열릴 때 한 번 — 모든 화면이 같은 씬이라 같은 값)
    private Vector3 mapCenter, outward, startPoint, strikePoint, groundContact;
    private Quaternion faceMap, faceAway;
    private float originalFog;
    private bool fogChanged;

    // 지금 보이는지, 얼굴 가운데(월드)와 얼굴이 보는 방향 — 등장 카메라용.
    public bool IsShown => model != null && model.gameObject.activeSelf;
    public Vector3 FacePoint => head != null
        ? (hat != null ? Vector3.Lerp(head.position, hat.position, 0.3f) : head.position)
        : model.position + Vector3.up * height * 0.78f;
    public Vector3 FaceForward => model != null ? model.forward : transform.forward;
    public float Height => height;
    public Vector3 FeetPosition => model != null ? model.position : transform.position;
    public Vector3 TowardMap => -outward;                          // 맵 쪽(수평)
    public Vector3 GroundContact => groundContact;                 // 발끝이 닿을 Ground 경계 위의 점
    public Vector3 MapCenter => mapCenter;
    public Vector3 StrikePoint => strikePoint;                     // 내리칠 자리(발)
    public Vector3 StartPoint => startPoint;                       // 나타나는 자리(발)
    public Vector3 ImpactPoint => strikePoint + TowardMap * (height * FistReachRatio); // 주먹이 떨어지는 곳(땅)

    private void Awake()
    {
        if (model == null) model = transform.Find("Model");
        if (model == null) model = BuildPlaceholder();
        else UseFadeMaterials(model); // Blender 모델: 나타나는 동안만 Fade 머티리얼을 쓴다
        if (slamArm == null) slamArm = FindDeep(model, "SlamArm");
        if (slamArm != null) slamArm.localRotation = Quaternion.Euler(RaisedArmAngle, 0f, 0f); // 내리치기 전: 손을 머리 위로 든 자세
        renderers = model.GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        animator = model.GetComponentInChildren<Animator>(true);
        head = FindDeep(model, "Head");
        hat = FindDeep(model, "Hat1");
        Current = this;
        if (GetComponent<WitchShatter>() == null) gameObject.AddComponent<WitchShatter>(); // 내리칠 때 맵 박살(§23.5 X7)
        if (Camera.main != null && Camera.main.GetComponent<WitchRevealCamera>() == null) Camera.main.gameObject.AddComponent<WitchRevealCamera>();
        BuildPath();
        model.gameObject.SetActive(false);
    }

    // 걷는 길: 맵 중심(Ground 사각형 가운데) → 앵커 방향으로 450 m에서 나타나 Ground 경계까지(§23.4 Q6).
    private void BuildPath()
    {
        Vector3 anchor = transform.position;
        bool hasGround = WitchPath.TryGetGroundRect(gameObject.scene, out Rect ground);
        if (!hasGround) Debug.LogWarning($"[WitchPresenter] No '{WitchPath.GroundName}' in {gameObject.scene.name} — the witch strikes from {WitchPath.FallbackStrikeDistance} m.");
        mapCenter = hasGround ? new Vector3(ground.center.x, anchor.y, ground.center.y) : new Vector3(0f, anchor.y, 0f);
        outward = WitchPath.OutwardDirection(mapCenter, anchor);
        startPoint = mapCenter + outward * WitchPath.StartDistance;
        float foot = height * WitchPath.FootReachRatio;
        if (hasGround) strikePoint = WitchPath.StrikePoint(ground, startPoint, outward, foot, out groundContact);
        else
        {
            strikePoint = mapCenter + outward * WitchPath.FallbackStrikeDistance;
            groundContact = strikePoint - outward * foot;
        }
        faceMap = Quaternion.LookRotation(-outward);
        faceAway = Quaternion.LookRotation(outward);
    }

    private void OnDestroy()
    {
        RestoreFog();
        if (Current == this) Current = null;
        if (fadeMaterials == null) return;
        foreach (Material[] set in fadeMaterials)
            foreach (Material m in set) if (m != null) Destroy(m);
    }

    private void Update()
    {
        // 마녀가 오는 판(타임어택 또는 쿠키 탈출 뒤 처단)에서 나타날 시각이 되었을 때만(남은 쿠키가 없으면 로켓만 떠나고 마녀는 없다, D10)
        WitchMode mode = WitchTimeline.Mode;
        double now = PhotonNetwork.Time;
        if (mode == WitchMode.None || !WitchTimeline.TryGetAppearAt(out double appearAt) || now < appearAt)
        {
            if (model.gameObject.activeSelf) model.gameObject.SetActive(false);
            activeBefore = false;
            RestoreFog();
            return;
        }
        float elapsed = (float)(now - appearAt);
        if (!model.gameObject.activeSelf)
        {
            model.gameObject.SetActive(true);
            fadeDone = false;
            if (fadeMaterials != null)
                for (int i = 0; i < renderers.Length && i < fadeMaterials.Length; i++) renderers[i].sharedMaterials = fadeMaterials[i];
            EscapeHud.Toast(EscapeTextsSO.Current.witchComing, alert: true);
            if (elapsed < LateSoundSeconds) GameAudio.Play(SoundId.WitchAppear);
            if (animator != null && elapsed < fadeInSeconds) animator.SetTrigger(RiseTrigger); // 숙인 자세에서 일어선다
        }

        if (!fadeDone)
        {
            float alpha = Mathf.Clamp01(elapsed / fadeInSeconds); // 0 → 1 (= 0 → 255)
            SetAlpha(alpha);
            if (alpha >= 1f)
            {
                fadeDone = true;
                if (elapsed < fadeInSeconds + LateSoundSeconds) GameAudio.Stinger(MusicId.StWitchAppear);
                RestoreOpaqueMaterials(); // 다 나타나면 불투명으로 돌려 몸의 앞뒤가 올바르게 그려지게 한다
            }
        }
        ApplyFog(Mathf.Clamp01(elapsed / fadeInSeconds));

        // 위치(걷기)·방향(돌아섬)·동작 — 모두 공통 시계로
        double walkStart = WitchTimeline.WalkStart(mode, appearAt);
        double walkEnd = WitchTimeline.WalkEnd(mode, appearAt);
        float progress = WitchTimeline.WalkProgress(now, walkStart, walkEnd);
        model.position = Vector3.Lerp(startPoint, strikePoint, progress);
        model.rotation = Facing(mode, now, appearAt);
        UpdateTurn(mode, now, appearAt);
        UpdateWalk(now, walkStart, walkEnd, progress);

        if (!struck && RoomState.TryGetInt(NetKeys.WitchStrike, out _))
        {
            struck = true;
            strikeTime = Time.time;
            impactDone = !activeBefore; // 늦게 들어와 이미 내리친 상태면 흔들림·소리 없이
            if (animator != null) { animator.speed = 1f; animator.SetBool(WalkingBool, false); animator.SetTrigger(StrikeTrigger); }
            if (impactDone) Impacted?.Invoke();
        }
        activeBefore = true;
        if (!struck) return;
        float since = Time.time - strikeTime;
        if (animator == null) AnimateSlam(since);
        if (!impactDone && since >= (animator != null ? StrikeImpactSeconds : 0f))
        {
            impactDone = true; // 주먹이 맵에 닿는 순간
            CameraShake.Add(SlamShakeStrength, SlamShakeSeconds); // 카메라가 LateUpdate에서 더한다(research.md R4.7-9)
            GameAudio.Play(SoundId.WitchSlam);
            Impacted?.Invoke();
        }
    }

    // 타임어택: 돌아서기 전에는 맵을 등지고, 2초 동안 돌아선 뒤 맵을 본다. 처단: 늘 맵을 본다.
    private Quaternion Facing(WitchMode mode, double now, double appearAt)
    {
        if (mode == WitchMode.Purge) return faceMap;
        float k = TurnAmount(now, WitchTimeline.TurnStart(appearAt));
        return faceAway * Quaternion.Euler(0f, TurnYawSign * 180f * k, 0f);
    }

    // 돌아선 정도(0~1, 순수 계산). 동작 TurnBack의 고개·가슴 비틀림이 이 몸 회전을 앞서 이끈다.
    public static float TurnAmount(double now, double turnStart)
    {
        float t = (float)((now - turnStart) / WitchTimeline.TurnSeconds);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }

    private void UpdateTurn(WitchMode mode, double now, double appearAt)
    {
        if (mode != WitchMode.TimeAttack || animator == null) return;
        double turnStart = WitchTimeline.TurnStart(appearAt);
        if (now < turnStart || now >= turnStart + WitchTimeline.TurnSeconds || turnTriggeredFor == turnStart) return;
        turnTriggeredFor = turnStart;
        animator.SetTrigger(TurnTrigger);
    }

    // 걷기: 동작 재생 속도를 걷는 속도에 맞추고(발이 미끄러지지 않게), 한 걸음마다 쿵 소리·흔들림.
    private void UpdateWalk(double now, double walkStart, double walkEnd, float progress)
    {
        float stepLength = height * StepLengthRatio;
        float distance = Vector3.Distance(startPoint, strikePoint);
        bool walking = progress > 0f && progress < 1f && !struck;
        if (animator != null && !struck)
        {
            animator.SetBool(WalkingBool, walking);
            animator.speed = walking ? WalkAnimatorSpeed(distance, (float)(walkEnd - walkStart), stepLength) : 1f;
        }
        if (!walking) { lastStep = int.MinValue; return; }
        int step = Mathf.FloorToInt(distance * progress / stepLength + 0.5f); // 발이 닿는 순간 = 반 걸음째부터 한 걸음마다
        if (lastStep != int.MinValue && step != lastStep)
        {
            float near = 1f - Mathf.Clamp01(Vector3.Distance(model.position, strikePoint) / Mathf.Max(1f, distance));
            GameAudio.Play(SoundId.WitchStep);
            CameraShake.Add(Mathf.Lerp(StepShakeFar, StepShakeNear, near), StepShakeSeconds);
        }
        lastStep = step;
    }

    // Walk 동작 재생 속도(순수 계산): 한 주기(2.4초) = 두 걸음. 걷는 속도 ÷ (두 걸음 길이) = 초당 주기 수.
    public static float WalkAnimatorSpeed(float distance, float seconds, float stepLength)
    {
        if (seconds <= 0f || stepLength <= 0f) return 1f;
        return distance / seconds / (2f * stepLength) * WalkCycleSeconds;
    }

    private const float StepShakeFar = 0.04f, StepShakeNear = 0.3f, StepShakeSeconds = 0.5f;

    private void ApplyFog(float k)
    {
        if (!RenderSettings.fog) return;
        if (!fogChanged) { originalFog = RenderSettings.fogDensity; fogChanged = true; }
        RenderSettings.fogDensity = Mathf.Lerp(originalFog, Mathf.Min(originalFog, WitchFogDensity), k);
    }

    private void RestoreFog()
    {
        if (!fogChanged) return;
        fogChanged = false;
        RenderSettings.fogDensity = originalFog;
    }

    public static float FadeAlpha(double escapedAt, double now, float fadeSeconds) =>
        Mathf.Clamp01((float)(now - escapedAt) / Mathf.Max(0.01f, fadeSeconds));

    private void SetAlpha(float alpha)
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            Color c = r.sharedMaterial != null && r.sharedMaterial.HasProperty(ColorId) ? r.sharedMaterial.color : Color.white;
            c.a = alpha;
            block.SetColor(ColorId, c);
            r.SetPropertyBlock(block);
        }
    }

    private const float SlamShakeStrength = 0.72f; // 내리친 순간의 흔들림(m), SlamShakeSeconds 동안 0으로 줄어든다
    private const float SlamShakeSeconds = 1.2f;

    // 손을 들어 맵 쪽으로 내리친다(카메라 흔들림은 내리친 순간 CameraShake에 한 번 등록).
    private void AnimateSlam(float t)
    {
        if (slamArm == null) return;
        float k = t < 0.5f ? t / 0.5f : 1f;
        slamArm.localRotation = Quaternion.Euler(Mathf.Lerp(RaisedArmAngle, 20f, k), 0f, 0f);
    }

    // ---------------- Blender model ----------------

    private Material[][] opaqueMaterials;
    private Material[][] fadeMaterials;

    private void UseFadeMaterials(Transform root)
    {
        Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
        opaqueMaterials = new Material[all.Length][];
        fadeMaterials = new Material[all.Length][];
        for (int i = 0; i < all.Length; i++)
        {
            opaqueMaterials[i] = all[i].sharedMaterials;
            fadeMaterials[i] = new Material[opaqueMaterials[i].Length];
            for (int k = 0; k < opaqueMaterials[i].Length; k++)
            {
                Material src = opaqueMaterials[i][k];
                bool glows = src != null && src.IsKeywordEnabled("_EMISSION");
                fadeMaterials[i][k] = FadeMaterial(src != null ? src.color : Color.white, glows ? 1f : 0f, src);
                fadeMaterials[i][k].SetInt("_ZWrite", 1); // 반투명한 동안에도 뒤쪽(빛나는 눈 등)이 몸을 뚫고 보이지 않게
            }
            all[i].sharedMaterials = fadeMaterials[i];
            all[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private void RestoreOpaqueMaterials()
    {
        if (opaqueMaterials == null) return;
        for (int i = 0; i < renderers.Length && i < opaqueMaterials.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].SetPropertyBlock(null);
            renderers[i].sharedMaterials = opaqueMaterials[i];
        }
    }

    private static Transform FindDeep(Transform root, string name) => EscapeSequence.FindDeep(root, name); // 깊은 자식 찾기는 한 곳(F-14)

    // ---------------- placeholder ----------------

    // 임시 마녀: 긴 치마(원뿔 대신 원기둥), 머리, 뾰족 모자, 빛나는 두 눈(앞모습에서만 보임), 내리치는 팔.
    private Transform BuildPlaceholder()
    {
        var root = new GameObject("Model").transform;
        root.SetParent(transform, false);
        float h = height;
        Color robe = new Color(0.18f, 0.08f, 0.25f);
        Color skin = new Color(0.55f, 0.75f, 0.45f);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, h * 0.3f, 0f), new Vector3(h * 0.35f, h * 0.3f, h * 0.35f), robe, 0f);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, h * 0.62f, 0f), new Vector3(h * 0.22f, h * 0.06f, h * 0.22f), robe, 0f);
        Part(root, PrimitiveType.Sphere, new Vector3(0f, h * 0.76f, 0f), Vector3.one * h * 0.2f, skin, 0f);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, h * 0.86f, 0f), new Vector3(h * 0.34f, h * 0.01f, h * 0.34f), robe, 0f);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, h * 0.93f, 0f), new Vector3(h * 0.12f, h * 0.08f, h * 0.12f), robe, 0f);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, h * 1.02f, 0f), new Vector3(h * 0.05f, h * 0.05f, h * 0.05f), robe, 0f);
        Part(root, PrimitiveType.Sphere, new Vector3(-h * 0.04f, h * 0.78f, h * 0.09f), Vector3.one * h * 0.03f, new Color(1f, 0.9f, 0.2f), 3f);
        Part(root, PrimitiveType.Sphere, new Vector3(h * 0.04f, h * 0.78f, h * 0.09f), Vector3.one * h * 0.03f, new Color(1f, 0.9f, 0.2f), 3f);

        slamArm = new GameObject("SlamArm").transform;
        slamArm.SetParent(root, false);
        slamArm.localPosition = new Vector3(h * 0.2f, h * 0.6f, 0f);
        slamArm.localRotation = Quaternion.Euler(RaisedArmAngle, 0f, 0f);
        Transform arm = Part(slamArm, PrimitiveType.Cylinder, new Vector3(0f, 0f, h * 0.22f), new Vector3(h * 0.06f, h * 0.22f, h * 0.06f), robe, 0f);
        arm.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Part(slamArm, PrimitiveType.Sphere, new Vector3(0f, 0f, h * 0.46f), Vector3.one * h * 0.1f, skin, 0f);
        return root;
    }

    private static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, float emission)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = FadeMaterial(color, emission);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    // Fade 모드 재질(알파로 서서히 나타나게). 원본 재질이 있으면 텍스처 등을 유지하려 복제하고, 키워드 조합은
    // Resources의 Fade 틀(EscapeVisuals.FadeTemplate·FadeGlowTemplate)과 같아 빌드에 셰이더 변형이 들어간다(research.md R5-21).
    private static Material FadeMaterial(Color color, float emission, Material source = null)
    {
        if (source == null) return EscapeVisuals.NewMaterial(color, emission, fade: true);
        var m = new Material(source) { color = color };
        EscapeVisuals.ApplyFade(m);
        return m;
    }
}
