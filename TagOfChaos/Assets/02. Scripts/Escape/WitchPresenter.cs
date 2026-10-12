using Photon.Pun;
using UnityEngine;

// 마녀 타임어택 연출(EscapePlan.md §1.2, §5.7, D38). 맵 밖(WITCH_Anchor)에 아주 크게 선다.
// - 마녀가 나타나는 시각(WitchAppearAt — 로켓이 날아간 뒤, 또는 쿠키 탈것이 떠난 뒤, Request1009Plan.md §19)부터 3초 동안 알파 0 → 1로 서서히 나타난다.
// - 타임어택 동안 뒷모습에서 앞모습으로 천천히 돌아선다. 진행률은 공통 시계(PhotonNetwork.Time) 기준이라 모든 화면이 같고,
//   늦게 들어온 사람도 바로 맞는 자세가 된다.
// - 타이머가 0이 되어 WitchStrike가 기록되면 두 팔을 들어 주먹을 쥐고 두 주먹으로 맵을 내리친다(Strike 동작). 주먹이 닿는 순간
//   (StrikeImpactSeconds) 카메라가 흔들리고 충격음이 나며, 그때 남은 생명체가 죽는다(죽음 처리는 각 캐릭터, Request1009Plan.md §18.2).
// - 모델: 허수아비 마녀(Blender 리깅, 프리팹 WITCH_Scarecrow — 동작 Idle·Rise·Strike). 나타날 때 숙인 자세에서 일어선다(Rise).
// - 나타나는 동안 모든 화면의 카메라가 얼굴 정면으로 갔다 온다(WitchRevealCamera, §18.4) — 얼굴 위치·방향을 FacePoint·FaceForward로 준다.
// - 소리(SoundPlan.md S6, 모두 2D): 나타나기 시작 = 바람·웃음(WitchAppear), 다 나타남 = 스팅어(StWitchAppear — 타임어택 시작의
//   StSpyLaunch와 겹치지 않게), 내리침 = 충격(WitchSlam). 늦게 들어와 이미 지난 순간이면 내지 않는다.
// 자식 "Model"이 있으면 그것을 쓰고(애니메이터가 있으면 동작으로, 없으면 SlamArm 회전으로 내리침), 없으면 도형으로 만든 임시 마녀를 쓴다.
public class WitchPresenter : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private const float RaisedArmAngle = -150f;
    public const float DefaultFadeSeconds = 3f;      // 카메라가 얼굴에 닿는 3초에 다 나타나게(§18.8 Q6, 예전 5초)
    public const float StrikeImpactSeconds = 0.95f;  // Strike 동작에서 주먹이 맵에 닿는 시각(Blender anim.py IMPACT와 같음)
    public const float ImpactGraceSeconds = 1f;      // 이 화면의 마녀가 충격을 알리지 못할 때(없음·멈춤) 그만큼 늦게라도 죽음 처리
    private static readonly int RiseTrigger = Animator.StringToHash("Rise");
    private static readonly int StrikeTrigger = Animator.StringToHash("Strike");

    public static WitchPresenter Current { get; private set; }
    // 이 화면에서 주먹이 맵에 닿은 순간(늦게 들어온 화면은 내리침을 처음 본 순간). 남은 생명체는 이때 죽는다 — 화면의 충격과 죽음이 같은 프레임.
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
    private Quaternion faceMap;   // 앞모습(맵을 바라봄)
    private Quaternion faceAway;  // 뒷모습
    private Animator animator;
    private Transform head, hat;
    private bool impactDone;

    // 지금 보이는지(타임어택 중), 얼굴 가운데(월드)와 얼굴이 보는 방향 — 등장 카메라용.
    public bool IsShown => model != null && model.gameObject.activeSelf;
    public Vector3 FacePoint => head != null
        ? (hat != null ? Vector3.Lerp(head.position, hat.position, 0.3f) : head.position)
        : model.position + Vector3.up * height * 0.78f;
    public Vector3 FaceForward => model != null ? model.forward : transform.forward;
    public float Height => height;

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
        if (Camera.main != null && Camera.main.GetComponent<WitchRevealCamera>() == null) Camera.main.gameObject.AddComponent<WitchRevealCamera>();

        Vector3 toMap = -transform.position;
        toMap.y = 0f;
        faceMap = toMap.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toMap.normalized) : Quaternion.identity;
        faceAway = faceMap * Quaternion.Euler(0f, 180f, 0f);
        model.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 마녀가 오는 판(타임어택 또는 쿠키 탈출 뒤 처단)에서 나타날 시각이 되었을 때만(남은 쿠키가 없으면 로켓만 떠나고 마녀는 없다, D10)
        WitchMode mode = WitchTimeline.Mode;
        if (mode == WitchMode.None || !WitchTimeline.TryGetAppearAt(out double escapedAt) || PhotonNetwork.Time < escapedAt)
        {
            if (model.gameObject.activeSelf) model.gameObject.SetActive(false);
            activeBefore = false;
            return;
        }
        if (!model.gameObject.activeSelf)
        {
            model.gameObject.SetActive(true);
            fadeDone = false;
            if (fadeMaterials != null)
                for (int i = 0; i < renderers.Length && i < fadeMaterials.Length; i++) renderers[i].sharedMaterials = fadeMaterials[i];
            EscapeHud.Toast(EscapeTextsSO.Current.witchComing, alert: true);
            if (PhotonNetwork.Time - escapedAt < LateSoundSeconds) GameAudio.Play(SoundId.WitchAppear);
            if (animator != null && PhotonNetwork.Time - escapedAt < fadeInSeconds) animator.SetTrigger(RiseTrigger); // 숙인 자세에서 일어선다
        }

        float elapsed = (float)(PhotonNetwork.Time - escapedAt);
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

        // 타임어택: 남은 시간 동안 천천히 돌아선다 / 처단: 나타나는 동안 바로 맵을 향한다
        float total = mode == WitchMode.Purge ? fadeInSeconds
            : RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out double end) ? (float)(end - escapedAt) : 60f;
        float turn = Mathf.Clamp01(elapsed / Mathf.Max(1f, total));
        model.rotation = Quaternion.Slerp(faceAway, faceMap, Mathf.SmoothStep(0f, 1f, turn));

        if (!struck && RoomState.TryGetInt(NetKeys.WitchStrike, out _))
        {
            struck = true;
            strikeTime = Time.time;
            impactDone = !activeBefore; // 늦게 들어와 이미 내리친 상태면 흔들림·소리 없이
            if (animator != null) animator.SetTrigger(StrikeTrigger);
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

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (fadeMaterials == null) return;
        foreach (Material[] set in fadeMaterials)
            foreach (Material m in set) if (m != null) Destroy(m);
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
