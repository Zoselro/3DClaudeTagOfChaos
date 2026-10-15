using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 괴물 소음폭탄(Request1009Plan.md §25.5 K, §25.3.1 R7). 스킬 키(Q)로 발밑에 놓고(최대 3칸, 터지거나 치운 뒤 20초에 칸이 다시 참),
// 근처 건물에서 누가 수리를 시작하면 방장이 터뜨린다(기절·비상벨). 놓인 폭탄은 괴물에게만 보이고, 자기 폭탄은 E로 치울 수 있다.
public class NoiseBombPresenter : MonoBehaviour
{
    private EscapeManager manager;
    private readonly Dictionary<int, NoiseBombView> views = new Dictionary<int, NoiseBombView>();
    private readonly List<int> stale = new List<int>();

    public void Init(EscapeManager owner)
    {
        manager = owner;
        manager.StateChanged += Sync;
    }

    private void OnDestroy()
    {
        if (manager != null) manager.StateChanged -= Sync;
    }

    // 지금 내 괴물의 남은 칸(HUD).
    public static int LocalChargesLeft(EscapeManager m)
    {
        if (m == null || m.State == null || PhotonNetwork.LocalPlayer == null) return 0;
        return m.State.BombChargesLeft(PhotonNetwork.LocalPlayer.ActorNumber, EscapeAuthority.BombMaxCharges, PhotonNetwork.Time, EscapeAuthority.BombCooldown);
    }

    private void Update()
    {
        if (!PlayerInput.SkillPressed || !EscapeManager.ActionsAllowed || !RoomState.IsLocalMonster()) return;
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (c.Role != CharacterRole.Monster || c.View == null || !c.View.IsMine || !c.CanInteract) continue;
            if (LocalChargesLeft(manager) <= 0) { UiSoundCues.Error(); return; }
            Transform t = c.gameObject.transform;
            manager.Request(EscapeOp.PlaceBomb, 0, 0, t.position + t.forward * 1.2f);
            return;
        }
    }

    private void Sync()
    {
        EscapeState s = manager.State;
        bool monster = RoomState.IsLocalMonster();
        stale.Clear();
        foreach (var kv in views)
        {
            int i = s != null ? s.BombIndex(kv.Key) : -1;
            if (i < 0 || s.Bombs[i].Spent || !monster) stale.Add(kv.Key);
        }
        foreach (int id in stale)
        {
            if (views[id] != null) Destroy(views[id].gameObject);
            views.Remove(id);
        }
        if (s == null || !monster) return;
        foreach (EscapeState.Bomb b in s.Bombs)
        {
            if (b.Spent || views.ContainsKey(b.Id)) continue;
            views[b.Id] = NoiseBombView.Create(manager, b, transform);
        }
    }
}
