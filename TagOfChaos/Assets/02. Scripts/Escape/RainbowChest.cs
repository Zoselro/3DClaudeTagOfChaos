using UnityEngine;

// 무지개 상자(Request1009Plan.md §25.5 H). 마녀 타이머가 흐르기 시작하면 방장이 후보 자리(RainbowSpawn_nn) 중 하나에 놓는다.
// 쿠키·스파이는 E 2초로 열어 헬리콥터 모자를 얻고, 괴물은 E 3초로 부순다(다시 생기지 않음, §25.3 Q12).
// 모양은 사용자 3D가 오기 전까지 코드로 만든 빛나는 상자(무지개 띠) — 모델이 생기면 이 오브젝트 아래에 넣고 Lid 이름만 맞추면 된다.
public class RainbowChest : MonoBehaviour, IHoldInteractable, IInteractionLabel
{
    [SerializeField, Min(0f)] private float openSeconds = 2f;
    [SerializeField, Min(0f)] private float breakSeconds = 3f;
    [SerializeField, Min(0.5f)] private float interactionRange = 2.6f;

    private static readonly Color[] Rainbow =
    {
        new Color(0.95f, 0.2f, 0.2f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.9f, 0.2f),
        new Color(0.3f, 0.85f, 0.35f), new Color(0.25f, 0.55f, 1f), new Color(0.6f, 0.3f, 0.9f),
    };

    private EscapeManager manager;
    private bool registered;
    private Transform lid;
    private Light glow;
    private GameObject visual;
    private RainbowState shown = (RainbowState)255;

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => interactionRange;

    public void Bind(EscapeManager owner)
    {
        manager = owner;
        if (transform.childCount == 0) BuildPlaceholder();
        visual = transform.childCount > 0 ? transform.GetChild(0).gameObject : null;
        lid = EscapeCue.FindDeep(transform, "Lid");
        glow = GetComponentInChildren<Light>(true);
        SetShown(false);
    }

    private void BuildPlaceholder()
    {
        var root = new GameObject("Visual").transform;
        root.SetParent(transform, false);
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root, false);
        body.transform.localScale = new Vector3(1.4f, 0.8f, 0.9f);
        body.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        EscapeVisuals.Tint(body, new Color(0.95f, 0.95f, 1f), 0.6f);
        for (int i = 0; i < Rainbow.Length; i++)
        {
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(stripe.GetComponent<Collider>());
            stripe.name = "Stripe" + i;
            stripe.transform.SetParent(root, false);
            stripe.transform.localScale = new Vector3(1.42f / Rainbow.Length, 0.82f, 0.92f);
            stripe.transform.localPosition = new Vector3(-0.7f + (i + 0.5f) * 1.4f / Rainbow.Length, 0.4f, 0f);
            EscapeVisuals.Tint(stripe, Rainbow[i], 1.2f);
        }
        var lidT = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(lidT.GetComponent<Collider>());
        lidT.name = "Lid";
        lidT.transform.SetParent(root, false);
        lidT.transform.localScale = new Vector3(1.5f, 0.2f, 1f);
        lidT.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        EscapeVisuals.Tint(lidT, new Color(1f, 0.85f, 0.4f), 1f);
        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.localPosition = Vector3.up * 1.5f;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 8f;
        light.intensity = 2.5f;
        light.color = new Color(1f, 0.85f, 0.6f);
    }

    public void Refresh()
    {
        EscapeState s = manager != null ? manager.State : null;
        RainbowState state = s != null && s.RainbowAnchor >= 0 ? s.Rainbow : RainbowState.None;
        if (state != RainbowState.None) transform.position = manager.RainbowPosition(s.RainbowAnchor);
        if (state == shown) return;
        shown = state;
        SetShown(state == RainbowState.Closed || state == RainbowState.Opened);
        if (lid != null) lid.localRotation = state == RainbowState.Opened ? Quaternion.Euler(-100f, 0f, 0f) : Quaternion.identity;
        if (glow != null) glow.enabled = state == RainbowState.Closed;
        bool register = state == RainbowState.Closed;
        if (registered != register)
        {
            registered = register;
            if (register) InteractableRegistry.Register(this);
            else InteractableRegistry.Unregister(this);
        }
    }

    private void SetShown(bool value)
    {
        if (visual != null && visual.activeSelf != value) visual.SetActive(value);
    }

    private void Update()
    {
        if (shown == RainbowState.Closed && visual != null) visual.transform.localPosition = Vector3.up * (0.08f * Mathf.Sin(Time.time * 2f));
    }

    private void OnDisable()
    {
        if (!registered) return;
        registered = false;
        InteractableRegistry.Unregister(this);
    }

    public bool CanInteract(IGameCharacter character) =>
        EscapeManager.ActionsAllowed && manager != null && manager.State != null && manager.State.Rainbow == RainbowState.Closed;

    public float HoldSecondsFor(IGameCharacter character) => character.Role == CharacterRole.Monster ? breakSeconds : openSeconds;

    public void Interact(IGameCharacter character) =>
        manager.Request(character.Role == CharacterRole.Monster ? EscapeOp.RainbowBreak : EscapeOp.RainbowOpen);

    public string GetLabel(IGameCharacter viewer) =>
        viewer != null && viewer.Role == CharacterRole.Monster ? EscapeTextsSO.Current.breakChest : EscapeTextsSO.Current.rainbowChest;
}
