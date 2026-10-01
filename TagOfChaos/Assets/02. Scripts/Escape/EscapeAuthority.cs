using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 탈출 모드의 방장 전용 규칙(EscapePlan.md §5.2~§5.6). 모든 요청을 검증해 EscapeState를 바꾸고 Room Prop으로 쓴다.
// 방장이 바뀌면 새 방장이 Room Prop의 상태를 이어받는다(Adopt). EscapeManager가 소유하는 순수 C# 클래스다.
public sealed class EscapeAuthority
{
    public const int InventorySlots = 4;
    private const float PickUpReach = 3.5f;   // 바닥 아이템을 줍는 최대 거리(m, 요청 검증)
    private const float InteractReach = 6f;   // 장치·로켓·상자 요청 검증 거리(m, 여유 포함)
    private const float DropSpread = 0.6f;

    private readonly EscapeManager manager;
    private readonly EscapeCatalogSO catalog;
    private readonly EscapeRecipeSO recipe;
    private readonly System.Random rng = new System.Random();
    private readonly Dictionary<int, float> toolReadyAt = new Dictionary<int, float>();
    private readonly Dictionary<int, Vector3> lastPositions = new Dictionary<int, Vector3>();

    public EscapeState State { get; private set; }

    public EscapeAuthority(EscapeManager manager, EscapeCatalogSO catalog, EscapeRecipeSO recipe)
    {
        this.manager = manager;
        this.catalog = catalog;
        this.recipe = recipe;
    }

    // ---------------- setup ----------------

    // 판이 시작될 때 한 번(방장). 인원표로 쿠키 수 = 필요 재료 수를 정하고, 칸·상자·아이템을 배치한다.
    public void Initialize(int chestAnchorCount)
    {
        var s = new EscapeState();
        int players = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
        int monsters = Mathf.Max(0, RoomState.MonsterCount());
        int spies = RoomState.TryGetIntArray(NetKeys.SpyActorNumbers, out int[] spyActors) ? spyActors.Length : 0;
        int cookies = Mathf.Max(1, players - monsters - spies);
        s.RequiredCount = cookies;

        foreach (RecipePlanner.PlannedSlot p in RecipePlanner.Plan(recipe, cookies, rng))
            s.DeviceSlots.Add(new EscapeState.Slot { Label = p.Label, Accepts = p.Accepts, Prefilled = !p.Required, ItemIndex = -1 });

        foreach (EscapeRecipeSO.RocketSlot r in recipe.RocketSlots)
        {
            var accepts = new List<string>();
            if (r.accepts != null) foreach (ItemSO it in r.accepts) if (it != null) accepts.Add(it.ItemId);
            s.RocketSlots.Add(new EscapeState.Slot { Label = r.label, Accepts = accepts.ToArray(), ItemIndex = -1 });
        }

        // 상자: 일반 = 필요 재료 × 3, 스파이 = 스파이 1명당 2개(§1.4). 상자 자리 중에서 무작위.
        var anchors = new List<int>();
        for (int i = 0; i < chestAnchorCount; i++) anchors.Add(i);
        Shuffle(anchors);
        int spyChests = spies * 2;
        int normalChests = Mathf.Min(cookies * 3, Mathf.Max(0, anchors.Count - spyChests));
        for (int i = 0; i < normalChests; i++) s.Chests.Add(new EscapeState.Chest { Anchor = anchors[i], Spy = false });
        for (int i = 0; i < spyChests && normalChests + i < anchors.Count; i++)
            s.Chests.Add(new EscapeState.Chest { Anchor = anchors[normalChests + i], Spy = true });

        // 일반 상자에 넣을 것: 필요한 칸의 재료 + (공구상자가 탈출 재료가 아닌 맵) 스파이용 공구상자 + 뿅망치.
        var normalItems = new List<string>();
        foreach (EscapeState.Slot slot in s.DeviceSlots)
            if (!slot.Prefilled && slot.Accepts.Length > 0) normalItems.Add(PickSpawn(slot));
        if (recipe.SpyToolbox != null && !RecipeUses(recipe.SpyToolbox))
            for (int i = 0; i < spies * recipe.ExtraToolboxesPerSpy; i++) normalItems.Add(recipe.SpyToolbox.ItemId);
        if (catalog.Hammer != null)
            for (int i = 0; i < Mathf.CeilToInt(cookies * catalog.HammersPerCookie); i++) normalItems.Add(catalog.Hammer.ItemId);

        var normalIndices = new List<int>();
        for (int i = 0; i < s.Chests.Count; i++) if (!s.Chests[i].Spy) normalIndices.Add(i);
        Shuffle(normalIndices);
        for (int i = 0; i < normalItems.Count; i++)
        {
            ItemSO item = catalog.Find(normalItems[i]);
            if (item == null) continue;
            var entry = new EscapeState.Item { Id = item.ItemId, Charges = item.IsTool ? item.Tool.Charges : 0 };
            if (i < normalIndices.Count) { entry.Loc = ItemLocation.Chest; entry.A = normalIndices[i]; }
            else { entry.Loc = ItemLocation.Ground; entry.Pos = manager.DevicePosition + Random.insideUnitSphere.WithY(0f) * 3f; }
            s.Items.Add(entry);
        }

        // 스파이 상자: 스파이 1명당 스턴건 1 + 물풍선 1(D19).
        int toolIndex = 0;
        for (int i = 0; i < s.Chests.Count; i++)
        {
            if (!s.Chests[i].Spy || catalog.SpyChestTools.Count == 0) continue;
            ItemSO tool = catalog.SpyChestTools[toolIndex++ % catalog.SpyChestTools.Count];
            s.Items.Add(new EscapeState.Item { Id = tool.ItemId, Loc = ItemLocation.Chest, A = i, Charges = tool.IsTool ? tool.Tool.Charges : 0 });
        }

        State = s;
        Commit();
        Debug.Log($"[EscapeAuthority] Initialized {recipe.MapName}: cookies={cookies} spies={spies} deviceSlots={s.DeviceSlots.Count} " +
                  $"chests={s.Chests.Count} items={s.Items.Count}");
    }

    private string PickSpawn(EscapeState.Slot slot) => slot.Accepts[rng.Next(slot.Accepts.Length)];

    private bool RecipeUses(ItemSO item)
    {
        foreach (EscapeRecipeSO.SlotGroup g in recipe.Groups) if (g.item == item) return true;
        return false;
    }

    // 방장이 바뀌었을 때 Room Prop의 상태를 이어받는다.
    public void Adopt(EscapeState current) => State = current?.Clone();

    // 매 프레임(방장): 나간 사람의 아이템을 떨어뜨릴 위치를 기억해 둔다.
    public void TrackPositions()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c) || c.View == null || c.View.Owner == null) continue;
            lastPositions[c.View.Owner.ActorNumber] = c.gameObject.transform.position;
        }
    }

    // ---------------- requests ----------------

    public void Handle(EscapeRequest r)
    {
        if (State == null) return;
        if (!EscapeManager.ActionsAllowed) return; // 변장 시간에 온 요청(늦게 도착했거나 조작된 것 포함)은 받지 않는다
        bool changed;
        switch (r.Op)
        {
            case EscapeOp.OpenChest: changed = OpenChest(r.Sender, r.A); break;
            case EscapeOp.PickUp: changed = PickUp(r.Sender, r.A); break;
            case EscapeOp.Drop: changed = Drop(r.Sender, r.A, r.Pos); break;
            case EscapeOp.DropAll: changed = DropAll(r.Sender, r.Pos); break;
            case EscapeOp.Install: changed = Install(r.Sender, r.A); break;
            case EscapeOp.Steal: changed = Steal(r.Sender, r.A); break;
            case EscapeOp.RocketInsert: changed = RocketInsert(r.Sender, r.A); break;
            case EscapeOp.RocketBoard: changed = RocketBoard(r.Sender); break;
            case EscapeOp.Exit: changed = Exit(r.Sender); break;
            case EscapeOp.ToolUse: changed = ToolUse(r); break;
            default: changed = false; break;
        }
        if (changed) Commit();
    }

    private bool OpenChest(int actor, int chest)
    {
        if (!IsActiveEscaper(actor) || chest < 0 || chest >= State.Chests.Count) return false;
        EscapeState.Chest c = State.Chests[chest];
        if (c.Opened) return false;
        if (c.Spy && !RoomState.IsSpy(actor)) return false; // 쿠키는 스파이 상자를 열 수 없다(D7)
        if (!IsNear(actor, manager.ChestPosition(c.Anchor), InteractReach)) return false;

        c.Opened = true;
        State.Chests[chest] = c;
        int item = State.ItemInChest(chest);
        if (item >= 0) Give(actor, item, manager.ChestFront(c.Anchor));
        return true;
    }

    private bool PickUp(int actor, int item)
    {
        if (!IsActiveEscaper(actor) || item < 0 || item >= State.Items.Count) return false;
        EscapeState.Item it = State.Items[item];
        if (it.Loc != ItemLocation.Ground || !IsNear(actor, it.Pos, PickUpReach)) return false;
        return Give(actor, item, it.Pos);
    }

    private bool Drop(int actor, int slot, Vector3 pos)
    {
        int item = State.HeldItemAt(actor, slot);
        if (item < 0) return false;
        PutOnGround(item, ValidDropPosition(actor, pos));
        return true;
    }

    private bool DropAll(int actor, Vector3 pos)
    {
        bool any = false;
        int n = 0;
        Vector3 origin = ValidDropPosition(actor, pos);
        for (int i = 0; i < State.Items.Count; i++)
        {
            if (State.Items[i].Loc != ItemLocation.Held || State.Items[i].A != actor) continue;
            float angle = n++ * 90f;
            PutOnGround(i, origin + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * DropSpread);
            any = true;
        }
        return any;
    }

    private bool Install(int actor, int invSlot)
    {
        if (!IsActiveEscaper(actor) || !IsNearDevice(actor)) return false;
        int item = State.HeldItemAt(actor, invSlot);
        if (item < 0) return false;
        EscapeState.Item it = State.Items[item];
        for (int s = 0; s < State.DeviceSlots.Count; s++)
        {
            EscapeState.Slot slot = State.DeviceSlots[s];
            if (slot.Filled || !slot.Allows(it.Id)) continue;
            slot.ItemIndex = item;
            State.DeviceSlots[s] = slot;
            it.Loc = ItemLocation.Device;
            it.A = s;
            it.StolenFromDevice = false;
            State.Items[item] = it;
            if (State.DeviceComplete && State.CompletedAt <= 0)
            {
                State.CompletedAt = PhotonNetwork.Time; // 맵 연출(시동·문 열림·케이크 부서짐)의 기준 시각
                Notice(EscapeNoticeKind.DeviceComplete, null);
            }
            return true;
        }
        return false;
    }

    // 스파이가 장치에 끼워진 재료를 뺀다(D17). 자기 로켓에 아직 필요한 종류만(D27).
    private bool Steal(int actor, int deviceSlot)
    {
        if (!RoomState.IsSpy(actor) || !IsActiveEscaper(actor)) return false;
        if (deviceSlot < 0 || deviceSlot >= State.DeviceSlots.Count || !IsNearDevice(actor)) return false;
        EscapeState.Slot slot = State.DeviceSlots[deviceSlot];
        if (slot.Prefilled || slot.ItemIndex < 0) return false;
        if (State.CompletedAt > 0) return false; // 작동을 시작한 장치에서는 뺄 수 없다(탑승이 시작된 뒤 판이 꼬이지 않게)
        int item = slot.ItemIndex;
        if (!RocketNeeds(State.Items[item].Id) || !CanPickUp(actor, item)) return false;

        slot.ItemIndex = -1;
        State.DeviceSlots[deviceSlot] = slot;
        EscapeState.Item it = State.Items[item];
        it.StolenFromDevice = true;
        State.Items[item] = it;
        return Give(actor, item, manager.DevicePosition);
    }

    private bool RocketInsert(int actor, int invSlot)
    {
        if (!RoomState.IsSpy(actor) || !IsActiveEscaper(actor) || !IsNear(actor, manager.RocketPosition, InteractReach)) return false;
        int item = State.HeldItemAt(actor, invSlot);
        if (item < 0) return false;
        EscapeState.Item it = State.Items[item];
        int rocketSlot = FindRocketSlotFor(it.Id);
        if (rocketSlot < 0) return false;

        EscapeState.Slot slot = State.RocketSlots[rocketSlot];
        slot.RocketFilled = true;
        State.RocketSlots[rocketSlot] = slot;

        bool announce = it.StolenFromDevice; // 장치에서 훔친 재료만 알린다(D40)
        it.StolenFromDevice = false;
        State.Items[item] = it;
        RespawnInEmptyChest(item);
        if (announce) Notice(EscapeNoticeKind.StolenToRocket, it.Id);
        return true;
    }

    private bool RocketBoard(int actor)
    {
        if (!RoomState.IsSpy(actor) || !IsActiveEscaper(actor) || !State.RocketComplete) return false;
        if (!IsNear(actor, manager.RocketPosition, InteractReach) || HoldsTwoHanded(actor)) return false;
        DropAll(actor, manager.RocketPosition);
        State.Boarded.Add(actor);
        TryLaunchRocket();
        return true;
    }

    // 쿠키가 탈것에 타서 기다린다(EscapeVisualPlan.md §4.3). 살아 있는 쿠키(스파이 제외)가 모두 타면 한 번에 출발한다.
    private bool Exit(int actor)
    {
        if (RoomState.IsSpy(actor) || RoomState.IsMonster(actor) || !IsActiveEscaper(actor)) return false; // 탈출구는 쿠키만(D35)
        if (!State.DeviceComplete || State.DepartedAt > 0 || !BoardingOpen()) return false;
        if (!IsNear(actor, manager.BoardPosition, InteractReach)) return false;
        DropAll(actor, manager.BoardPosition);
        State.Waiting.Add(actor);
        TryDepart();
        return true;
    }

    // 맵 연출이 탈 수 있는 단계에 도착했는지(예: 오븐 문이 다 열림, 케이크 로켓이 다 솟아오름).
    private bool BoardingOpen() =>
        State.CompletedAt > 0 && PhotonNetwork.Time >= State.CompletedAt + manager.BoardReadySeconds;

    // 탑승·잡힘·나감 때마다 부른다. 밖에 남은 살아 있는 쿠키(스파이 제외)가 없고 탄 쿠키가 있으면 출발한다.
    // 출발한 순간 탄 쿠키 모두 탈출 성공이다(출발 연출이 끝나기 전에 시간이 끝나도 성공).
    public void TryDepart()
    {
        if (State == null || State.Waiting.Count == 0 || State.DepartedAt > 0) return;
        foreach (Player p in PhotonNetwork.PlayerList)
            if (!RoomState.IsSpy(p.ActorNumber) && IsActiveEscaper(p.ActorNumber)) return;
        State.DepartedAt = PhotonNetwork.Time;
        foreach (int actor in State.Waiting)
            if (!State.Escaped.Contains(actor)) State.Escaped.Add(actor);
        Debug.Log($"[EscapeAuthority] Vehicle departed with {State.Waiting.Count} cookie(s).");
    }

    private bool ToolUse(EscapeRequest r)
    {
        int item = State.HeldItemAt(r.Sender, r.A);
        if (item < 0 || !IsActiveEscaper(r.Sender)) return false;
        EscapeState.Item it = State.Items[item];
        ItemSO def = catalog.Find(it.Id);
        if (def == null || !def.IsTool || it.Charges <= 0) return false;
        if (toolReadyAt.TryGetValue(r.Sender, out float ready) && Time.time < ready) return false;
        toolReadyAt[r.Sender] = Time.time + def.Tool.CooldownSeconds;

        it.Charges--;
        if (it.Charges <= 0) it.Loc = ItemLocation.Gone;
        State.Items[item] = it;

        var payload = new object[] { it.Id, r.Sender, r.Targets ?? new int[0], r.Pos, r.B };
        PhotonNetwork.RaiseEvent(NetEventCodes.ToolHit, payload, new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
        return true;
    }

    // ---------------- room events ----------------

    public void OnPlayerLeft(Player player)
    {
        if (State == null || player == null) return;
        Vector3 pos = lastPositions.TryGetValue(player.ActorNumber, out Vector3 p) ? p : manager.DevicePosition;
        bool changed = DropAll(player.ActorNumber, pos); // 나간 쿠키·스파이의 모든 아이템을 그 자리에(D28)
        bool wasWaiting = State.DepartedAt <= 0;
        TryDepart(); // 나간 사람을 빼고 다시 센다
        if (changed || (wasWaiting && State.DepartedAt > 0)) Commit();
        TryLaunchRocket();
    }

    // 스파이가 잡히면 모두에게 알린다(D34). 잡힌 스파이의 아이템은 본인 클라이언트가 DropAll로 떨어뜨린다.
    public void OnPlayerBroken(Player player)
    {
        if (player != null && RoomState.IsSpy(player.ActorNumber)) Notice(EscapeNoticeKind.SpyCaught, null);
        if (State != null && State.DepartedAt <= 0)
        {
            TryDepart(); // 밖에 남은 마지막 쿠키가 잡히면 기다리던 쿠키들이 출발한다
            if (State.DepartedAt > 0) Commit();
        }
        TryLaunchRocket();
    }

    // 남아 있는 스파이가 모두 탔으면 로켓이 떠난다(D21). 쿠키가 먼저 모두 끝났으면 타임어택 없이 끝난다(D10 — 판정은 GameRuleController).
    public void TryLaunchRocket()
    {
        if (State == null || State.Boarded.Count == 0 || RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out _)) return;
        if (!RoomState.TryGetIntArray(NetKeys.SpyActorNumbers, out int[] spies)) return;
        foreach (int spy in spies)
        {
            Player p = FindPlayer(spy);
            if (p == null || RoomState.IsBroken(p)) continue; // 나갔거나 잡힌 스파이는 뺀다
            if (!State.Boarded.Contains(spy)) return;
        }
        double now = PhotonNetwork.Time;
        var props = new Hashtable { { NetKeys.SpyEscapedAt, now } }; // 로켓 발사(연출 기준) — 늘 기록한다
        // 쿠키가 모두 끝났으면 타임어택 없이 판이 끝난다(D10 — GameRuleController가 발사 연출 뒤에 판정). 타임어택이 없으면 마녀도 없다.
        bool timeAttack = AnyCookieStillPlaying();
        if (timeAttack) props[NetKeys.TimeAttackEndTime] = now + RoomState.TimeAttackSeconds();
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Debug.Log(timeAttack ? "[EscapeAuthority] Rocket launched. Time attack started." : "[EscapeAuthority] Rocket launched. No cookies left: no time attack.");
    }

    // ---------------- helpers ----------------

    private bool AnyCookieStillPlaying()
    {
        foreach (Player p in PhotonNetwork.PlayerList)
            if (!RoomState.IsSpy(p.ActorNumber) && IsActiveEscaper(p.ActorNumber)) return true;
        return false;
    }

    // 괴물이 아니고, 탈출·탑승하지 않았고, 잡히지 않은 참가자.
    private bool IsActiveEscaper(int actor)
    {
        if (RoomState.IsMonster(actor) || State.Escaped.Contains(actor) || State.Boarded.Contains(actor) || State.Waiting.Contains(actor)) return false;
        Player p = FindPlayer(actor);
        return p != null && !RoomState.IsBroken(p);
    }

    private bool Give(int actor, int item, Vector3 fallbackGround)
    {
        if (!CanPickUp(actor, item))
        {
            PutOnGround(item, fallbackGround);
            return true;
        }
        int slot = ChooseSlot(actor);
        EscapeState.Item it = State.Items[item];
        it.Loc = ItemLocation.Held;
        it.A = actor;
        it.B = slot;
        State.Items[item] = it;
        return true;
    }

    // 두 손 아이템을 든 동안은 줍지 못하고, 칸이 가득 차면 줍지 못한다(§1.9).
    public bool CanPickUp(int actor, int item)
    {
        if (State.HeldCount(actor) >= InventorySlots || HoldsTwoHanded(actor)) return false;
        ItemSO def = catalog.Find(State.Items[item].Id);
        return def != null;
    }

    private bool HoldsTwoHanded(int actor)
    {
        foreach (EscapeState.Item it in State.Items)
        {
            if (it.Loc != ItemLocation.Held || it.A != actor) continue;
            ItemSO def = catalog.Find(it.Id);
            if (def != null && def.LocksSlotSwitch) return true;
        }
        return false;
    }

    // 지금 고른 칸이 비었으면 그 칸, 아니면 첫 빈 칸.
    private int ChooseSlot(int actor)
    {
        Player p = FindPlayer(actor);
        int selected = p != null && RoomState.TryGetPlayerInt(p, NetKeys.SelectedSlot, out int sel) ? sel : 0;
        if (selected >= 0 && selected < InventorySlots && State.HeldItemAt(actor, selected) < 0) return selected;
        for (int i = 0; i < InventorySlots; i++) if (State.HeldItemAt(actor, i) < 0) return i;
        return 0;
    }

    private bool RocketNeeds(string itemId) => FindRocketSlotFor(itemId) >= 0;

    private int FindRocketSlotFor(string itemId)
    {
        ItemSO def = catalog.Find(itemId);
        for (int i = 0; i < State.RocketSlots.Count; i++)
        {
            EscapeState.Slot slot = State.RocketSlots[i];
            if (slot.Filled) continue;
            if (slot.Allows(itemId) || (def != null && def.FitsAnyRocketSlot)) return i;
        }
        return -1;
    }

    // 로켓에 끼운 재료는 빈 일반 상자에 닫힌 모습으로 다시 생긴다(§1.4).
    private void RespawnInEmptyChest(int item)
    {
        var candidates = new List<int>();
        for (int c = 0; c < State.Chests.Count; c++)
            if (!State.Chests[c].Spy && State.ItemInChest(c) < 0) candidates.Add(c);

        EscapeState.Item it = State.Items[item];
        if (candidates.Count == 0)
        {
            it.Loc = ItemLocation.Ground;
            it.Pos = manager.DevicePosition;
            State.Items[item] = it;
            return;
        }
        int chest = candidates[rng.Next(candidates.Count)];
        EscapeState.Chest ch = State.Chests[chest];
        ch.Opened = false;
        State.Chests[chest] = ch;
        it.Loc = ItemLocation.Chest;
        it.A = chest;
        State.Items[item] = it;
    }

    private void PutOnGround(int item, Vector3 pos)
    {
        EscapeState.Item it = State.Items[item];
        it.Loc = ItemLocation.Ground;
        it.Pos = manager.SnapToGround(pos);
        State.Items[item] = it;
    }

    private Vector3 ValidDropPosition(int actor, Vector3 requested)
    {
        // 요청 위치가 그 사람에게서 너무 멀면(조작) 그 사람 발밑으로.
        if (lastPositions.TryGetValue(actor, out Vector3 at) && Vector3.Distance(at, requested) > PickUpReach) return at;
        return requested;
    }

    // 장치 근처: 큰 장치(캔디숲 케이크)는 중심이 멀어 가장 가까운 칸 자리로 잰다.
    private bool IsNearDevice(int actor)
    {
        if (!lastPositions.TryGetValue(actor, out Vector3 at)) return true;
        return manager.DistanceToDevice(at) <= InteractReach;
    }

    private bool IsNear(int actor, Vector3 target, float reach)
    {
        if (!lastPositions.TryGetValue(actor, out Vector3 at)) return true; // 위치를 아직 모르면 막지 않는다(입장 직후)
        Vector3 d = at - target;
        d.y = 0f;
        return d.magnitude <= reach;
    }

    private static Player FindPlayer(int actor)
    {
        if (!RoomState.IsInRoom()) return null;
        return PhotonNetwork.CurrentRoom.GetPlayer(actor);
    }

    private void Notice(EscapeNoticeKind kind, string itemId)
    {
        PhotonNetwork.RaiseEvent(NetEventCodes.EscapeNotice, new object[] { (byte)kind, itemId ?? string.Empty },
            new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
    }

    public void Commit()
    {
        if (State == null || !RoomState.IsInRoom()) return;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.EscapeState, State.Encode() } });
        manager.ApplyLocalState(State.Clone()); // 방장 화면은 서버 응답을 기다리지 않고 바로 맞춘다
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

public static class EscapeVectorExtensions
{
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
}
