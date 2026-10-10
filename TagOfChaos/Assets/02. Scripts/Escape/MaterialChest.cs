using UnityEngine;

// 상자 하나(EscapePlan.md §1.4). 씬의 CHEST_Slot 자리에 붙어 있고, 판마다 EscapeState가 일반/스파이 상자와 열림 여부를 정한다.
// 겉모습은 두 상자가 같다. 이름 표시: 일반 = 재료 상자, 스파이 상자 = 모두에게 스파이 상자(쿠키에게는 잠겨 있음도).
// 한 번 열면 닫히지 않고, 재료가 다시 생긴 상자만 닫힌 모습으로 돌아간다. 괴물은 쓸 수 없다(D33).
// 여는 데 E를 holdSeconds(2초) 누르고 있어야 한다(Request1009Plan.md §1). 쿠키에게 잠긴 스파이 상자는 누르기가 시작되지 않는다.
public class MaterialChest : MonoBehaviour, IHoldInteractable, IInteractionLabel
{
    private const float OpenLidAngle = -110f;

    [SerializeField] private Transform lid;
    [SerializeField, Min(0f)] private float holdSeconds = 2f;
    [SerializeField, Min(0.5f)] private float interactionRange = 2.2f;

    private EscapeManager manager;
    private int anchorIndex;
    private int chestIndex = -1;
    private EscapeState.Chest data;
    private bool registered;

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => interactionRange;

    public void Bind(EscapeManager owner, int anchor)
    {
        manager = owner;
        anchorIndex = anchor;
        if (lid == null) lid = transform.Find("Lid");
        Hide();
    }

    public void Show(int index, EscapeState.Chest chest)
    {
        chestIndex = index;
        data = chest;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (lid != null) lid.localRotation = Quaternion.Euler(chest.Opened ? OpenLidAngle : 0f, 0f, 0f);
        SetRegistered(!chest.Opened);
    }

    public void Hide()
    {
        chestIndex = -1;
        SetRegistered(false);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDisable() => SetRegistered(false);

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
