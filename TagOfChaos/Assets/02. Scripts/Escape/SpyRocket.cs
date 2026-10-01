using UnityEngine;

// 스파이 로켓(EscapePlan.md §1.5, §5.6). 맵마다 모양이 다르고 재료 2개가 필요하다. 스파이만 보고 쓸 수 있다(쿠키·괴물에게는
// 아이콘이 뜨지 않는다). 재료나 공구상자를 들고 누르면 끼우고, 2개가 차면 빈손으로 눌러 탄다. 남은 스파이가 모두 타면 떠난다.
public class SpyRocket : MonoBehaviour, IInteractable, IInteractionLabel
{
    [SerializeField, Min(0.5f)] private float interactionRange = 3f;

    private EscapeManager manager;
    private bool registered;
    private readonly GameObject[] slotParts = new GameObject[4];

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
        for (int i = 0; i < s.RocketSlots.Count && i < slotParts.Length; i++)
        {
            bool filled = s.RocketSlots[i].Filled;
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
