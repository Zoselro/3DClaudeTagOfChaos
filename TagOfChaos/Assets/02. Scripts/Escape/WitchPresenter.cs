using Photon.Pun;
using UnityEngine;

// 마녀 타임어택 연출(EscapePlan.md §1.2, §5.7, D38). 맵 밖(WITCH_Anchor)에 아주 크게 선다.
// - 스파이가 떠난 순간(SpyEscapedAt)부터 5초 동안 알파 0 → 1(0 → 255)로 서서히 나타난다.
// - 타임어택 동안 뒷모습에서 앞모습으로 천천히 돌아선다. 진행률은 공통 시계(PhotonNetwork.Time) 기준이라 모든 화면이 같고,
//   늦게 들어온 사람도 바로 맞는 자세가 된다.
// - 타이머가 0이 되어 WitchStrike가 기록되면 손으로 맵을 내리친다(카메라 흔들림). 죽음 처리는 각 캐릭터가 한다.
// Blender 모델이 자식 "Model"로 있으면 그것을 쓰고, 없으면 도형으로 만든 임시 마녀를 쓴다.
public class WitchPresenter : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private const float RaisedArmAngle = -150f;

    [SerializeField, Min(0.1f)] private float fadeInSeconds = 5f;
    [SerializeField, Min(1f)] private float height = 90f;
    [SerializeField] private Transform model;
    [SerializeField] private Transform slamArm;

    private Renderer[] renderers = new Renderer[0];
    private MaterialPropertyBlock block;
    private bool fadeDone;
    private bool struck;
    private float strikeTime;
    private Quaternion faceMap;   // 앞모습(맵을 바라봄)
    private Quaternion faceAway;  // 뒷모습

    private void Awake()
    {
        if (model == null) model = transform.Find("Model");
        if (model == null) model = BuildPlaceholder();
        else UseFadeMaterials(model); // Blender 모델: 나타나는 동안만 Fade 머티리얼을 쓴다
        if (slamArm == null) slamArm = FindDeep(model, "SlamArm");
        if (slamArm != null) slamArm.localRotation = Quaternion.Euler(RaisedArmAngle, 0f, 0f); // 내리치기 전: 손을 머리 위로 든 자세
        renderers = model.GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();

        Vector3 toMap = -transform.position;
        toMap.y = 0f;
        faceMap = toMap.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toMap.normalized) : Quaternion.identity;
        faceAway = faceMap * Quaternion.Euler(0f, 180f, 0f);
        model.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 타임어택이 있을 때만 나타난다(남은 쿠키가 없으면 로켓만 떠나고 마녀는 없다, D10)
        if (!RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out double escapedAt) || !RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out _))
        {
            if (model.gameObject.activeSelf) model.gameObject.SetActive(false);
            return;
        }
        if (!model.gameObject.activeSelf)
        {
            model.gameObject.SetActive(true);
            fadeDone = false;
            if (fadeMaterials != null)
                for (int i = 0; i < renderers.Length && i < fadeMaterials.Length; i++) renderers[i].sharedMaterials = fadeMaterials[i];
            EscapeHud.Toast(EscapeTextsSO.Current.witchComing);
        }

        float elapsed = (float)(PhotonNetwork.Time - escapedAt);
        if (!fadeDone)
        {
            float alpha = Mathf.Clamp01(elapsed / fadeInSeconds); // 0 → 1 (= 0 → 255)
            SetAlpha(alpha);
            if (alpha >= 1f)
            {
                fadeDone = true;
                RestoreOpaqueMaterials(); // 다 나타나면 불투명으로 돌려 몸의 앞뒤가 올바르게 그려지게 한다
            }
        }

        float total = RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out double end) ? (float)(end - escapedAt) : 60f;
        float turn = Mathf.Clamp01(elapsed / Mathf.Max(1f, total));
        model.rotation = Quaternion.Slerp(faceAway, faceMap, Mathf.SmoothStep(0f, 1f, turn));

        if (!struck && RoomState.TryGetInt(NetKeys.WitchStrike, out _))
        {
            struck = true;
            strikeTime = Time.time;
        }
        if (struck) AnimateSlam(Time.time - strikeTime);
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

    // 손을 들어 맵 쪽으로 내리친다 + 카메라 흔들림.
    private void AnimateSlam(float t)
    {
        if (slamArm != null)
        {
            float k = t < 0.5f ? t / 0.5f : 1f;
            slamArm.localRotation = Quaternion.Euler(Mathf.Lerp(RaisedArmAngle, 20f, k), 0f, 0f);
        }
        Camera cam = Camera.main;
        if (cam != null && t < 1.2f)
        {
            float strength = (1.2f - t) * 0.6f;
            cam.transform.position += Random.insideUnitSphere * strength;
        }
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
        if (fadeMaterials == null) return;
        foreach (Material[] set in fadeMaterials)
            foreach (Material m in set) if (m != null) Destroy(m);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

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

    // Built-in Standard 셰이더를 Fade 모드로(알파로 서서히 나타나게).
    private static Material FadeMaterial(Color color, float emission, Material source = null)
    {
        var m = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
        m.color = color;
        m.SetFloat("_Mode", 2f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        if (emission > 0f && source == null)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * emission);
        }
        return m;
    }
}
