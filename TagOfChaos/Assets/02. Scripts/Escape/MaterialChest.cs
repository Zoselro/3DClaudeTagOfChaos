using UnityEngine;

// 상자 하나(EscapePlan.md §1.4). 씬의 CHEST_Slot 자리에 붙어 있고, 판마다 EscapeState가 일반/스파이 상자와 열림 여부를 정한다.
// 겉모습은 두 상자가 같다. 이름 표시: 일반 = 재료 상자, 스파이 상자 = 모두에게 스파이 상자(쿠키에게는 잠겨 있음도).
// 한 번 열면 닫히지 않고, 재료가 다시 생긴 상자만 닫힌 모습으로 돌아간다. 괴물은 쓸 수 없다(D33).
// 여는 데 E를 holdSeconds(2초) 누르고 있어야 한다(Request1009Plan.md §1). 쿠키에게 잠긴 스파이 상자는 누르기가 시작되지 않는다.
// 변장 시간에는 숨어 있다가 끝나면 살짝 튀어나오며 나타난다(Request1009Plan.md §16.1 — 소리 없음).
public class MaterialChest : MonoBehaviour, IHoldInteractable, IInteractionLabel
{
    private const float OpenLidAngle = -110f;
    private const float AppearSeconds = 0.35f;
    private const float AppearOvershoot = 1.08f;
    private const int AppearDust = 14;

    [SerializeField] private Transform lid;
    [SerializeField, Min(0f)] private float holdSeconds = 2f;
    [SerializeField, Min(0.5f)] private float interactionRange = 2.2f;

    private EscapeManager manager;
    private int anchorIndex;
    private int chestIndex = -1;
    private EscapeState.Chest data;
    private bool registered;
    private Vector3 baseScale = Vector3.one;
    private float appearStart = -1f;
    private ParticleSystem dust;

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => interactionRange;

    public void Bind(EscapeManager owner, int anchor)
    {
        manager = owner;
        anchorIndex = anchor;
        if (lid == null) lid = transform.Find("Lid");
        baseScale = transform.localScale;
        Hide();
    }

    // appear: 숨어 있던 상자가 변장 시간이 끝나 처음 나타나는 순간이면 튀어나오는 연출을 한다.
    public void Show(int index, EscapeState.Chest chest, bool appear = false)
    {
        chestIndex = index;
        data = chest;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            if (appear) BeginAppear();
        }
        if (lid != null) lid.localRotation = Quaternion.Euler(chest.Opened ? OpenLidAngle : 0f, 0f, 0f);
        SetRegistered(!chest.Opened);
    }

    public void Hide()
    {
        chestIndex = -1;
        SetRegistered(false);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    public bool IsAppearing => appearStart >= 0f;

    private void OnDisable()
    {
        SetRegistered(false);
        EndAppear();
    }

    private void BeginAppear()
    {
        appearStart = Time.time;
        transform.localScale = baseScale * AppearScale(0f);
        if (dust == null) dust = CreateDust();
        dust.Emit(AppearDust);
    }

    private void EndAppear()
    {
        if (appearStart < 0f) return;
        appearStart = -1f;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        if (appearStart < 0f) return;
        float t = (Time.time - appearStart) / AppearSeconds;
        if (t >= 1f) EndAppear();
        else transform.localScale = baseScale * AppearScale(t);
    }

    // 0→1.08(70%)→1로 커지는 크기(순수 계산, 시험용).
    public static float AppearScale(float t)
    {
        t = Mathf.Clamp01(t);
        const float peakAt = 0.7f;
        if (t < peakAt) return Mathf.SmoothStep(0f, AppearOvershoot, t / peakAt);
        return Mathf.Lerp(AppearOvershoot, 1f, Mathf.SmoothStep(0f, 1f, (t - peakAt) / (1f - peakAt)));
    }

    // 바닥에서 퍼지는 작은 먼지(상자 자식이 아니라 같은 자리에 둬서 크기 연출의 영향을 받지 않는다).
    private ParticleSystem CreateDust()
    {
        var go = new GameObject("ChestAppearDust");
        go.transform.SetParent(transform.parent, false);
        go.transform.position = transform.position + Vector3.up * 0.1f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startColor = new Color(0.55f, 0.5f, 0.48f, 0.45f);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.6f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = g;
        go.GetComponent<ParticleSystemRenderer>().material = EscapeVisuals.SoftParticleMaterial();
        return ps;
    }

    private void SetRegistered(bool value)
    {
        if (registered == value) return;
        registered = value;
        if (value) InteractableRegistry.Register(this);
        else InteractableRegistry.Unregister(this);
    }

    public bool CanInteract(IGameCharacter character) =>
        EscapeManager.ActionsAllowed && chestIndex >= 0 && !data.Opened && character.Role == CharacterRole.Cookie;

    public float HoldSecondsFor(IGameCharacter character) => data.Spy && !RoomState.IsLocalSpy() ? 0f : holdSeconds;

    public void Interact(IGameCharacter character)
    {
        if (chestIndex < 0 || manager == null) return;
        if (data.Spy && !RoomState.IsLocalSpy()) return; // 쿠키에게는 잠겨 있음(아이콘 아래 표시)
        manager.Request(EscapeOp.OpenChest, chestIndex);
    }

    public string GetLabel(IGameCharacter viewer)
    {
        EscapeTextsSO t = EscapeTextsSO.Current;
        if (!data.Spy) return t.materialChest;
        return RoomState.IsLocalSpy() ? t.spyChest : t.spyChest + "\n" + t.locked;
    }
}
