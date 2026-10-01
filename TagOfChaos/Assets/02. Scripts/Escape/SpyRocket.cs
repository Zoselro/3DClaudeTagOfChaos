using Photon.Pun;
using UnityEngine;

// 스파이 로켓(EscapePlan.md §1.5, §5.6). 맵마다 모양이 다르고 재료 2개가 필요하다. 스파이만 보고 쓸 수 있다(쿠키·괴물에게는
// 아이콘이 뜨지 않는다). 재료나 공구상자를 들고 누르면 끼우고, 2개가 차면 빈손으로 눌러 탄다. 남은 스파이가 모두 타면 떠난다.
// 모델(EscapeVisualPlan.md §3.4): 칸마다 Slot_nn_Empty / Slot_nn_Filled 중 하나를 보여주고(이름이 Spin으로 시작하는 부품은 돈다),
// 스파이가 타면 Hatch가 열렸다 닫힌다. 출발 시각(SpyEscapedAt)부터 바닥 Flame이 서서히 커지고, 떨리다가 가속하며 떠오른다.
// 모든 화면이 공통 시계(PhotonNetwork.Time)로 같은 장면을 본다.
public class SpyRocket : MonoBehaviour, IInteractable, IInteractionLabel
{
    [SerializeField, Min(0.5f)] private float interactionRange = 3f;

    private EscapeManager manager;
    private bool registered;
    private readonly GameObject[] slotParts = new GameObject[4];

    private const float IgniteSeconds = 2f;      // 불꽃이 다 커지기까지
    private const float RiseAcceleration = 5f;   // m/s² (4초 뒤 40 m)
    private const float HideAfterSeconds = 7f;   // 이만큼 지나면 하늘로 사라진 것으로 보고 숨긴다
    private const float HatchOpenSeconds = 1.4f;
    private const float HatchOpenAngle = 100f;

    private Transform flame;
    private Transform hatch;
    private Vector3 basePosition;
    private bool baseSaved;
    private int seenBoarded;
    private float hatchOpenUntil = -1f;
    private float hatchAngle;
    private ParticleSystem smoke;
    private Renderer[] renderers;
    private Collider[] colliders;
    private bool launchedVisible = true;

    private void Awake()
    {
        flame = transform.Find("Flame");
        hatch = transform.Find("Hatch");
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        if (flame != null) flame.localScale = Vector3.zero;
    }

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => interactionRange;

    private void OnEnable() => SetRegistered(true);
    private void OnDisable() => SetRegistered(false);

    private void SetRegistered(bool value)
    {
        if (registered == value) return;
        registered = value;
        if (value) InteractableRegistry.Register(this);
        else InteractableRegistry.Unregister(this);
    }

    public void Refresh(EscapeManager owner)
    {
        manager = owner;
        EscapeState s = owner.State;
        if (s == null) return;
        if (s.Boarded.Count > seenBoarded) hatchOpenUntil = Time.time + HatchOpenSeconds; // 누군가 탔다 — 해치 열림
        seenBoarded = s.Boarded.Count;
        for (int i = 0; i < s.RocketSlots.Count && i < slotParts.Length; i++)
        {
            bool filled = s.RocketSlots[i].Filled;
            Transform emptyLook = transform.Find($"Slot_{i:00}_Empty");
            Transform filledLook = transform.Find($"Slot_{i:00}_Filled");
            if (emptyLook != null || filledLook != null)
            {
                // 모델에 칸 모양이 있으면 빈/찬 모양을 바꿔 끼운다(공구상자로 채워도 같은 찬 모양 = 수리된 모습)
                if (emptyLook != null && emptyLook.gameObject.activeSelf == filled) emptyLook.gameObject.SetActive(!filled);
                if (filledLook != null && filledLook.gameObject.activeSelf != filled) filledLook.gameObject.SetActive(filled);
                continue;
            }
            Transform anchor = transform.Find($"Slot_{i:00}");
            if (anchor == null) continue;
            if (filled && slotParts[i] == null)
            {
                ItemSO def = s.RocketSlots[i].Accepts.Length > 0 ? owner.Catalog.Find(s.RocketSlots[i].Accepts[0]) : null;
                slotParts[i] = EscapeVisuals.CreateItemModel(def, anchor);
            }
            else if (!filled && slotParts[i] != null)
            {
                Destroy(slotParts[i]);
                slotParts[i] = null;
            }
        }
    }

    private void Update()
    {
        // 끼운 기어처럼 이름이 Spin으로 시작하는 부품은 돈다
        for (int i = 0; i < 2; i++)
        {
            Transform filledLook = transform.Find($"Slot_{i:00}_Filled");
            if (filledLook == null || !filledLook.gameObject.activeInHierarchy) continue;
            foreach (Transform t in filledLook) if (t.name.StartsWith("Spin")) t.Rotate(180f * Time.deltaTime, 0f, 0f, Space.Self);
        }

        // 해치: 탈 때 잠깐 열린다
        if (hatch != null)
        {
            float target = Time.time < hatchOpenUntil ? HatchOpenAngle : 0f;
            hatchAngle = Mathf.MoveTowards(hatchAngle, target, 220f * Time.deltaTime);
            hatch.localRotation = Quaternion.Euler(0f, hatchAngle, 0f);
        }
        AnimateLaunch();
    }

    // 출발 시각부터: 불꽃이 서서히 커지며 떨림·연기(2초) → 가속하며 떠오름 → 하늘에서 사라짐.
    private void AnimateLaunch()
    {
        if (!RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out double escapedAt))
        {
            if (!launchedVisible) SetVisible(true);
            if (baseSaved) transform.position = basePosition;
            return;
        }
        if (!baseSaved) { basePosition = transform.position; baseSaved = true; }
        float t = (float)(PhotonNetwork.Time - escapedAt);
        if (t < 0f) return;

        foreach (Collider c in colliders) if (c != null && c.enabled) c.enabled = false; // 떠오르는 로켓에 걸리지 않게
        if (flame != null)
        {
            float grow = Mathf.Clamp01(t / IgniteSeconds);
            float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 40f);
            flame.localScale = new Vector3(grow, grow * flicker, grow);
        }
        if (smoke == null) smoke = CreateSmoke();

        float rise = Mathf.Max(0f, t - IgniteSeconds);
        Vector3 shake = t < IgniteSeconds + 0.5f ? Random.insideUnitSphere * 0.06f * Mathf.Clamp01(t) : Vector3.zero;
        shake.y = 0f;
        transform.position = basePosition + Vector3.up * (0.5f * RiseAcceleration * rise * rise) + shake;
        if (t > HideAfterSeconds && launchedVisible) SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        launchedVisible = visible;
        foreach (Renderer r in renderers) if (r != null) r.enabled = visible;
        if (!visible && smoke != null) smoke.Stop();
    }

    // 점화 연기: 바닥에서 옆으로 퍼지는 회색 퍼프(코드로 만든 파티클, 텍스처 없음).
    private ParticleSystem CreateSmoke()
    {
        var go = new GameObject("LaunchSmoke");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.2f;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.6f;
        main.startSpeed = 3f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startColor = new Color(0.75f, 0.72f, 0.8f, 0.6f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var emission = ps.emission;
        emission.rateOverTime = 40f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 70f;
        shape.radius = 0.6f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = grad;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = EscapeVisuals.SoftParticleMaterial(); // 둥근 부드러운 퍼프(텍스처 없으면 네모로 보인다)
        ps.Play();
        return ps;
    }

    // 이 재료가 로켓의 빈 칸에 맞는지(공구상자는 아무 칸이나, D18).
    public static bool RocketNeeds(EscapeState s, EscapeCatalogSO catalog, string itemId)
    {
        ItemSO def = catalog.Find(itemId);
        foreach (EscapeState.Slot slot in s.RocketSlots)
            if (!slot.Filled && (slot.Allows(itemId) || (def != null && def.FitsAnyRocketSlot))) return true;
        return false;
    }

    public bool CanInteract(IGameCharacter character)
    {
        if (!EscapeManager.ActionsAllowed) return false; // 변장 시간(§1.3)
        if (manager == null || manager.State == null || character.Role != CharacterRole.Cookie || !RoomState.IsLocalSpy()) return false;
        PlayerInventory inv = PlayerInventory.Local;
        if (inv == null) return false;
        if (inv.HeldItem != null && RocketNeeds(manager.State, manager.Catalog, inv.HeldItem.ItemId)) return true;
        return manager.State.RocketComplete && !inv.HandsLocked; // 두 손 아이템을 든 채로는 탈 수 없다(§1.5)
    }

    public void Interact(IGameCharacter character)
    {
        PlayerInventory inv = PlayerInventory.Local;
        if (inv == null || manager == null) return;
        if (inv.HeldItem != null && RocketNeeds(manager.State, manager.Catalog, inv.HeldItem.ItemId))
            manager.Request(EscapeOp.RocketInsert, inv.Selected);
        else if (manager.State.RocketComplete)
            manager.Request(EscapeOp.RocketBoard);
    }

    // 로켓에 (n/2)처럼 진행 상황을 보여준다(스파이에게만 보이는 사물이라 그대로 띄운다).
    public string GetLabel(IGameCharacter viewer)
    {
        if (manager == null || manager.State == null) return null;
        int filled = 0;
        foreach (EscapeState.Slot s in manager.State.RocketSlots) if (s.Filled) filled++;
        return $"{EscapeTextsSO.Current.spyRocket} ({filled}/{manager.State.RocketSlots.Count})";
    }
}
