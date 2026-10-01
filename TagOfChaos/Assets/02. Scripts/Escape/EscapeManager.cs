using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// 맵 씬마다 하나(EscapeSetup 루트, EscapeMapSetup 에디터 도구가 배치). 탈출 상태의 사본을 들고 화면(상자·바닥 아이템·
// 장치·로켓·HUD)을 맞추고, 로컬 요청을 방장에게 보낸다. 방장이면 EscapeAuthority로 요청을 처리한다(EscapePlan.md §5.2).
// 이 컴포넌트가 없는 씬(GameScene 등)은 예전 규칙(생존)으로 돈다.
public class EscapeManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static EscapeManager Instance { get; private set; }

    [SerializeField] private Transform[] chestAnchors = new Transform[0];
    [SerializeField] private EscapeDevice device;
    [SerializeField] private SpyRocket rocket;
    [SerializeField] private WitchPresenter witch;
    [Tooltip("비우면 씬 이름(Game_<Map>)으로 카탈로그에서 찾는다.")]
    [SerializeField] private EscapeRecipeSO recipeOverride;

    private EscapeCatalogSO catalog;
    private EscapeRecipeSO recipe;
    private EscapeAuthority authority;
    private MaterialChest[] chests = new MaterialChest[0];
    private readonly List<GroundItemView> groundPool = new List<GroundItemView>();
    private readonly HashSet<int> brokenReported = new HashSet<int>();
    private bool initRequested;

    public EscapeState State { get; private set; }
    public EscapeCatalogSO Catalog => catalog;
    public EscapeRecipeSO Recipe => recipe;
    public EscapeDevice Device => device;
    public SpyRocket Rocket => rocket;
    public WitchPresenter Witch => witch;
    public event System.Action StateChanged;

    public Vector3 DevicePosition => device != null ? device.transform.position : transform.position;

    // 장치까지의 수평 거리: 장치 중심과 칸 자리(Slot_nn) 중 가장 가까운 곳(큰 장치는 둘레에 칸이 있다).
    public float DistanceToDevice(Vector3 p) => device != null ? device.DistanceTo(p) : Flat(p - transform.position);

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    // 맵별 탈출 연출(EscapeVisualPlan.md §4). 연출이 없으면(임시 도형) 완성 즉시 탈 수 있고 출발 연출도 없다.
    public EscapeSequence Sequence => device != null ? device.Sequence : null;
    public Vector3 BoardPosition => Sequence != null ? Sequence.BoardPoint.position : DevicePosition;
    public float BoardReadySeconds => Sequence != null ? Sequence.BoardReadySeconds : 0f;
    public float DepartureSeconds => Sequence != null ? Sequence.DepartureSeconds : 0f;

    // 탈것에 타서 출발을 기다리는 쿠키인지(몸 숨김·이동 잠금·관전 대상에서 빼기).
    // 탈것에 탔거나(기다림·출발) 탈출했거나 스파이 로켓에 탄 쿠키인지 — 맵에서 몸이 사라진 상태.
    public static bool HasLeftMap(int actor) => Instance != null && Instance.State != null
        && (Instance.State.Waiting.Contains(actor) || Instance.State.Escaped.Contains(actor) || Instance.State.Boarded.Contains(actor));

    public static bool IsWaiting(int actor) => Instance != null && Instance.State != null && Instance.State.Waiting.Contains(actor) && !Instance.State.Escaped.Contains(actor);

    // 출발 연출이 아직 진행 중인지(게임 끝 판정을 미룬다).
    public bool IsDeparting(double now) =>
        (State != null && State.DepartedAt > 0 && now < State.DepartedAt + DepartureSeconds) || IsRocketLaunchingWithoutTimeAttack(now);

    // 쿠키가 모두 끝나 타임어택 없이 스파이 로켓만 떠나는 경우(D10): 발사 연출이 보이도록 잠시 끝 판정을 미룬다.
    public const float RocketLaunchShowSeconds = 6f;
    private static bool IsRocketLaunchingWithoutTimeAttack(double now) =>
        RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out double at) && !RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out _)
        && now < at + RocketLaunchShowSeconds;
    public Vector3 RocketPosition => rocket != null ? rocket.transform.position : transform.position;
    public Vector3 ChestPosition(int anchor) => anchor >= 0 && anchor < chestAnchors.Length ? chestAnchors[anchor].position : transform.position;
    public Vector3 ChestFront(int anchor) =>
        anchor >= 0 && anchor < chestAnchors.Length ? chestAnchors[anchor].position + chestAnchors[anchor].forward * 1.2f : transform.position;

    public static bool IsActive => Instance != null && Instance.isActiveAndEnabled;

    // 변장(색칠) 시간에는 상자 열기·줍기·도구·설치·로켓·탈출을 모두 막는다(EscapeVisualPlan.md §1.3).
    public static bool ActionsAllowed => GamePhaseState.Current != GamePhase.Paint;

    // ---------------- lifecycle ----------------

    private void Awake()
    {
        Instance = this;
        catalog = EscapeCatalogSO.Current;
        string map = SceneManager.GetActiveScene().name.Replace("Game_", string.Empty);
        recipe = recipeOverride != null ? recipeOverride : catalog != null ? catalog.RecipeFor(map) : null;
        if (catalog == null || recipe == null)
        {
            Debug.LogError($"[EscapeManager] Catalog or recipe for '{map}' not found. Escape mode disabled.");
            enabled = false;
            return;
        }

        chests = new MaterialChest[chestAnchors.Length];
        for (int i = 0; i < chestAnchors.Length; i++)
        {
            if (chestAnchors[i] == null) continue;
            chests[i] = chestAnchors[i].GetComponent<MaterialChest>();
            if (chests[i] == null) chests[i] = chestAnchors[i].gameObject.AddComponent<MaterialChest>();
            chests[i].Bind(this, i);
        }
        authority = new EscapeAuthority(this, catalog, recipe);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        EscapeHud.Create(this);
        ReadStateFromRoom();
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || !RoomState.IsInRoom()) return;
        authority.TrackPositions();

        if (State == null && !initRequested && CanInitialize())
        {
            initRequested = true;
            authority.Initialize(chestAnchors.Length);
        }
        ReportBrokenPlayers();
    }

    // 판 시작 신호(PaintPhaseEndTime)가 있고 괴물이 정해졌으면 시작한다. 오프라인 개발 방은 바로 시작한다.
    private static bool CanInitialize() =>
        PhotonNetwork.OfflineMode || (RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out _) && RoomState.HasMonster());

    // 방장: 잡힌 사람을 한 번씩 확인해 스파이 잡힘 알림과 로켓 출발 조건을 다시 본다.
    private void ReportBrokenPlayers()
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (!RoomState.IsBroken(p) || brokenReported.Contains(p.ActorNumber)) continue;
            brokenReported.Add(p.ActorNumber);
            authority.OnPlayerBroken(p);
        }
    }

    // ---------------- state sync ----------------

    private void ReadStateFromRoom()
    {
        if (!RoomState.IsInRoom()) return;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.EscapeState, out object raw) && raw is byte[] data)
            ApplyLocalState(EscapeState.Decode(data));
    }

    public override void OnJoinedRoom() => ReadStateFromRoom();

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (changed.ContainsKey(NetKeys.EscapeState))
        {
            if (changed[NetKeys.EscapeState] is byte[] data) ApplyLocalState(EscapeState.Decode(data));
            else { State = null; initRequested = false; StateChanged?.Invoke(); } // 판 초기화로 지워짐
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonNetwork.IsMasterClient && State != null) authority.Adopt(State); // 새 방장이 이어받는다
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (PhotonNetwork.IsMasterClient) authority.OnPlayerLeft(otherPlayer);
    }

    public void ApplyLocalState(EscapeState state)
    {
        if (state == null) return;
        State = state;
        if (PhotonNetwork.IsMasterClient && authority.State == null) authority.Adopt(state);
        RefreshViews();
        StateChanged?.Invoke();
    }

    private void RefreshViews()
    {
        var used = new bool[chests.Length];
        for (int c = 0; c < State.Chests.Count; c++)
        {
            int anchor = State.Chests[c].Anchor;
            if (anchor < 0 || anchor >= chests.Length || chests[anchor] == null) continue;
            used[anchor] = true;
            chests[anchor].Show(c, State.Chests[c]);
        }
        for (int a = 0; a < chests.Length; a++)
            if (!used[a] && chests[a] != null) chests[a].Hide();

        int g = 0;
        for (int i = 0; i < State.Items.Count; i++)
        {
            if (State.Items[i].Loc != ItemLocation.Ground) continue;
            ItemSO def = catalog.Find(State.Items[i].Id);
            if (def == null) continue;
            if (g >= groundPool.Count) groundPool.Add(GroundItemView.Create(this, transform));
            groundPool[g++].Show(i, def, State.Items[i].Pos);
        }
        for (; g < groundPool.Count; g++) groundPool[g].Hide();

        if (device != null) device.Refresh(this);
        if (rocket != null) rocket.Refresh(this);
    }

    // ---------------- requests ----------------

    public void Request(EscapeOp op, int a = 0, int b = 0, Vector3 pos = default, int[] targets = null)
    {
        if (!RoomState.IsInRoom() || PhotonNetwork.LocalPlayer == null) return;
        var r = new EscapeRequest { Op = op, Sender = PhotonNetwork.LocalPlayer.ActorNumber, A = a, B = b, Pos = pos, Targets = targets };
        if (PhotonNetwork.IsMasterClient)
        {
            authority.Handle(r);
            return;
        }
        PhotonNetwork.RaiseEvent(NetEventCodes.EscapeRequest, r.ToPayload(),
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData e)
    {
        switch (e.Code)
        {
            case NetEventCodes.EscapeRequest:
                if (PhotonNetwork.IsMasterClient && EscapeRequest.TryParse(e.CustomData, e.Sender, out EscapeRequest r)) authority.Handle(r);
                break;
            case NetEventCodes.EscapeNotice:
                if (e.CustomData is object[] n && n.Length >= 2 && n[0] is byte kind) EscapeHud.Notice((EscapeNoticeKind)kind, n[1] as string);
                break;
            case NetEventCodes.ToolHit:
                ToolHitReceiver.Handle(e.CustomData);
                break;
        }
    }

    // ---------------- helpers ----------------

    public ItemSO ItemDef(int index) =>
        State != null && index >= 0 && index < State.Items.Count ? catalog.Find(State.Items[index].Id) : null;

    public Vector3 SnapToGround(Vector3 pos)
    {
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(pos + Vector3.up * 3f, Vector3.down, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<IGameCharacter>() != null) continue;
            if (h.point.y > best && h.point.y <= pos.y + 1.5f) best = h.point.y;
        }
        return float.IsNegativeInfinity(best) ? pos : new Vector3(pos.x, best, pos.z);
    }

#if UNITY_EDITOR
    public void EditorBind(Transform[] anchors, EscapeDevice escapeDevice, SpyRocket spyRocket, WitchPresenter witchPresenter)
    {
        chestAnchors = anchors;
        device = escapeDevice;
        rocket = spyRocket;
        witch = witchPresenter;
    }
#endif
}
