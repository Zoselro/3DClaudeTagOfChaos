using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 쿠키가 손에 든 아이템을 모든 클라이언트 화면에 보여준다(EscapePlan.md §1.9). 소유자의 고른 칸(SelectedSlot)과
// EscapeState로 무엇을 드는지 정하고, 두 손 아이템은 몸 앞 가운데, 한 손 아이템은 오른쪽에 든다.
// 손에 든 아이템이 겉으로 보이므로 누가 재료를 들고 있는지 모두가 볼 수 있다(스파이 추리 단서).
public class HeldItemPresenter : MonoBehaviourPunCallbacks
{
    private static readonly Vector3 TwoHandedOffset = new Vector3(0f, 1.0f, 0.45f);
    private static readonly Vector3 OneHandedOffset = new Vector3(0.38f, 0.95f, 0.25f);

    private EscapeManager manager;
    private PhotonView view;
    private Transform socket;
    private GameObject model;
    private string shownId;

    public void Init(EscapeManager escape, PhotonView ownerView)
    {
        manager = escape;
        view = ownerView;
        socket = new GameObject("CarriedItemSocket").transform;
        socket.SetParent(transform, false);
        manager.StateChanged += Refresh;
        Refresh();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (manager != null) manager.StateChanged -= Refresh;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        if (manager != null)
        {
            manager.StateChanged -= Refresh;
            manager.StateChanged += Refresh;
        }
    }

    public override void OnPlayerPropertiesUpdate(Player target, ExitGames.Client.Photon.Hashtable changed)
    {
        if (view != null && target == view.Owner && changed.ContainsKey(NetKeys.SelectedSlot)) Refresh();
    }

    private void Refresh()
    {
        if (manager == null || manager.State == null || view == null || view.Owner == null) return;
        int actor = view.Owner.ActorNumber;
        int slot = RoomState.TryGetPlayerInt(view.Owner, NetKeys.SelectedSlot, out int s) ? s : 0;
        int item = manager.State.HeldItemAt(actor, slot);
        ItemSO def = manager.ItemDef(item);
        string id = def != null ? def.ItemId : null;
        if (id == shownId) return;

        shownId = id;
        if (model != null) Destroy(model);
        model = null;
        if (def == null) return;
        socket.localPosition = def.LocksSlotSwitch ? TwoHandedOffset : OneHandedOffset;
        model = EscapeVisuals.CreateItemModel(def, socket);
    }
}
