using System.Collections.Generic;
using UnityEngine;

// 쿠키 탈출 장치(EscapePlan.md §1.6, §5.5): 재료 칸 표시, 설치, 스파이 훔치기, 완성 뒤 쿠키 탈출구.
// 칸 모양은 자식 "Slot_00".."Slot_nn"이 있으면 그 위치를 쓰고(Blender 모델), 없으면 장치 둘레에 만든다.
// 빈 칸 = 어두운 받침(Socket), 채워진 칸 = 재료 모양(Part). 완성되면 "ESC_Glow"를 켜고 맵별 연출을 한다.
public class EscapeDevice : MonoBehaviour, IInteractable, IInteractionLabel
{
    [SerializeField, Min(0.5f)] private float interactionRange = 3.8f; // 모델 본체(최대 6 m 길이) 끝에서도 닿게
    [SerializeField, Min(0.5f)] private float slotRingRadius = 1.6f;
    [SerializeField] private float slotHeight = 1.2f;

    private EscapeManager manager;
    private readonly List<GameObject> slotParts = new List<GameObject>();
    private readonly List<GameObject> slotSockets = new List<GameObject>();
    private readonly List<string> shownItems = new List<string>();
    private readonly List<bool> modelAnchors = new List<bool>(); // 칸 자리가 모델에 있으면 재료를 그 자리에 정확히 놓는다
    private bool registered;
    private bool complete;
    private Transform glow;
    private Renderer[] glowRenderers = new Renderer[0];

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => interactionRange;
    public bool IsComplete => complete;

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
        EnsureSlots(s.DeviceSlots.Count);
        for (int i = 0; i < s.DeviceSlots.Count; i++)
        {
            EscapeState.Slot slot = s.DeviceSlots[i];
            string itemId = slot.ItemIndex >= 0 ? s.Items[slot.ItemIndex].Id : (slot.Prefilled && slot.Accepts.Length > 0 ? slot.Accepts[0] : null);
            SetSlotVisual(i, itemId, slot.Filled);
        }

        bool nowComplete = s.DeviceComplete;
        if (nowComplete != complete)
        {
            complete = nowComplete;
            EscapeExitFx.SetComplete(transform, owner.Recipe.ExitKind, complete);
        }
    }

    private void Update()
    {
        if (complete) EscapeExitFx.Tick(transform, manager != null ? manager.Recipe.ExitKind : EscapeExitKind.CakeRocket);
    }

    private void EnsureSlots(int count)
    {
        while (slotSockets.Count < count)
        {
            int i = slotSockets.Count;
            Transform anchor = transform.Find($"Slot_{i:00}");
            modelAnchors.Add(anchor != null);
            if (anchor == null)
            {
                anchor = new GameObject($"Slot_{i:00}").transform;
                anchor.SetParent(transform, false);
                float angle = (count <= 1 ? 0f : i * 360f / count);
                anchor.localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * slotRingRadius + Vector3.up * slotHeight;
            }
            var socket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(socket.GetComponent<Collider>());
            socket.name = "Socket";
            socket.transform.SetParent(anchor, false);
            socket.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
            EscapeVisuals.Tint(socket, new Color(0.08f, 0.06f, 0.1f), 0f);
            slotSockets.Add(socket);
            slotParts.Add(null);
            shownItems.Add(null);
        }
        for (int i = 0; i < slotSockets.Count; i++) slotSockets[i].transform.parent.gameObject.SetActive(i < count);
    }

    private void SetSlotVisual(int i, string itemId, bool filled)
    {
        string wanted = filled ? itemId : null;
        if (shownItems[i] == wanted) return;
        if (slotParts[i] != null) Destroy(slotParts[i]);
        slotParts[i] = null;
        shownItems[i] = wanted;
        if (wanted == null) return;
        ItemSO def = manager.Catalog.Find(wanted);
        GameObject part = EscapeVisuals.CreateItemModel(def, slotSockets[i].transform.parent);
        part.transform.localPosition = modelAnchors[i] ? Vector3.zero : Vector3.up * 0.25f;
        slotParts[i] = part;
    }

    public bool CanInteract(IGameCharacter character)
    {
        if (manager == null || manager.State == null || character.Role != CharacterRole.Cookie) return false; // 괴물 X(D33)
        if (complete) return true; // 쿠키는 탈출, 스파이는 "들어갈 수 없음"(같은 아이콘 — 정체가 드러나지 않게, D35)
        PlayerInventory inv = PlayerInventory.Local;
        if (inv == null) return false;
        if (FindInstallSlot(inv.HeldItem) >= 0) return true;
        return RoomState.IsLocalSpy() && inv.HeldItem == null && FindStealSlot() >= 0;
    }

    public void Interact(IGameCharacter character)
    {
        PlayerInventory inv = PlayerInventory.Local;
        if (manager == null || inv == null) return;
        if (complete)
        {
            if (RoomState.IsLocalSpy()) EscapeHud.Toast(EscapeTextsSO.Current.cannotEnter);
            else manager.Request(EscapeOp.Exit);
            return;
        }
        if (FindInstallSlot(inv.HeldItem) >= 0)
        {
            manager.Request(EscapeOp.Install, inv.Selected);
            return;
        }
        int steal = RoomState.IsLocalSpy() ? FindStealSlot() : -1;
        if (steal >= 0) manager.Request(EscapeOp.Steal, steal);
    }

    public string GetLabel(IGameCharacter viewer) => complete ? EscapeTextsSO.Current.escapeDevice : null;

    private int FindInstallSlot(ItemSO held)
    {
        if (held == null || manager.State == null) return -1;
        List<EscapeState.Slot> slots = manager.State.DeviceSlots;
        for (int i = 0; i < slots.Count; i++) if (!slots[i].Filled && slots[i].Allows(held.ItemId)) return i;
        return -1;
    }

    // 스파이가 뺄 수 있는 칸: 쿠키가 끼운 재료 중 자기 로켓에 아직 필요한 종류(D27).
    private int FindStealSlot()
    {
        EscapeState s = manager.State;
        for (int i = 0; i < s.DeviceSlots.Count; i++)
        {
            EscapeState.Slot slot = s.DeviceSlots[i];
            if (slot.Prefilled || slot.ItemIndex < 0) continue;
            if (SpyRocket.RocketNeeds(s, manager.Catalog, s.Items[slot.ItemIndex].Id)) return i;
        }
        return -1;
    }
}
