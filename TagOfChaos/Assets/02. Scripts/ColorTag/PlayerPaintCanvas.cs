using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 개인 자유 색칠(GameRule.md §3). 60초 동안 각자 원하는 색으로 자유롭게 칠하다가, 한 색을
// 일정량(minStrokesToRegister) 이상 칠하면 그 색이 슬롯에 등록된다(최대 4개). 등록되지 않은
// 색은 화면엔 보이지만 슬롯 카운트에는 잡히지 않는다 — "1픽셀만 칠하고 넘어가는" 악용 방지.
public class PlayerPaintCanvas : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Collider paintableCollider;
    [SerializeField] private Renderer bodyRenderer; // 캐릭터 스킨 렌더러 (SkinnedMeshRenderer)
    [SerializeField] private Shader paintedSkinShader; // ColorTag/PlayerPaintedSkin
    [SerializeField] private ColorPaletteSO palette;
    [SerializeField] private BrushSettingsSO brushSettings;
    [SerializeField] private Material brushStampMaterial;    // 일반 스탬프: 잠긴(알파=1) 픽셀은 건드리지 않음
    [SerializeField] private Material finalizeStampMaterial; // 강제 도포 전용: 잠금 무시하고 항상 덮어씀
    [SerializeField] private Material eraseStampMaterial;    // 지우개: 알파를 0으로 되돌림
    [SerializeField] private int canvasSize = 512;
    [SerializeField] private int minStrokesToRegister = 15; // 밸런스 값, 플레이테스트로 조정 필요(GameRule.md §12)

    // 마우스가 이동한 화면 경로를 이 간격(px)마다 보간해 스탬프를 찍는다. 예전에는 누른 프레임마다 점 하나만
    // 찍어 빠르게 그리면 점선이 됐고, 슬롯 등록 기준도 "누른 프레임 수"라 프레임레이트에 따라 달랐다
    // (research.md §8.16, §8.17). 이제 스탬프 수가 이동 거리에 비례하므로 등록 기준도 프레임레이트와 무관하다.
    [SerializeField] private float stampSpacingPixels = 6f;
    [SerializeField] private int maxStampsPerFrame = 32;

    // 스탬프를 매 프레임 개별 이벤트로 보내지 않고 묶어 한 번에 보낸다(research.md §8.16). 전송 주기는 인원에 따라
    // GameSettingsSO.PaintStrokeSendIntervalFor가 정한다(research.md §12.4 — 4명 이하 1/15초).
    [SerializeField] private int maxStampsPerEvent = 64;

    // 붓 레이캐스트 최대 거리. 씬 전체가 아니라 paintableCollider 하나만 검사하므로(아래 Update 참고)
    // 무한대 대신 카메라-캐릭터 거리보다 넉넉한 유한값을 쓴다.
    public const float MaxPaintRayDistance = 100f;

    // 이 클라이언트가 조종하는 캐릭터의 캔버스. 붓 커서·색 슬롯 패널·스와치·도구 버튼이 각자
    // FindObjectsByType로 매번(일부는 매 프레임) 전체 탐색하던 것을 대체한다(research.md §7).
    public static PlayerPaintCanvas Local { get; private set; }

    public RenderTexture PaintCanvas { get; private set; }
    public Collider PaintableCollider => paintableCollider;
    public float CurrentBrushRadius => currentBrushRadius;
    public bool IsMine => pv != null && pv.IsMine;
    public int CurrentBrushColorIndex => isErasing ? -1 : currentBrushColorIndex;
    public IReadOnlyList<int> RegisteredColorSlots => registeredColorSlots;

    private Camera localCamera;
    private float currentBrushRadius;
    private int currentBrushColorIndex = -1; // 로컬 전용 선택값(네트워크 동기화 불필요 — 이 클라이언트만 참조)
    private bool isErasing;

    private readonly Dictionary<int, int> pendingStrokeCounts = new Dictionary<int, int>(); // 미등록 색 → 누적 스탬프 수
    private readonly List<int> registeredColorSlots = new List<int>();

    // 인스턴스별 스탬프 머티리얼 — 공유 머티리얼 에셋에 _StampUV 등을 쓰면 에디터에서 에셋 파일 자체가
    // 계속 수정됐다(§23.9 테스트 부수 효과). 원본은 인스펙터 연결용으로만 쓰고 복제본에 그린다.
    private Material brushStampInstance;
    private Material finalizeStampInstance;
    private Material eraseStampInstance;
    private Material paintedSkinInstance; // bodyRenderer.material로 만든 합성 머티리얼 — OnDestroy에서 해제(research.md §8.19)

    // 마우스 드래그 보간 상태
    private bool isStroking;
    private Vector2 lastStampScreenPos;

    // 송신 대기 중인 스탬프 묶음
    private enum StrokeKind : byte { Normal, Erase, ForceFill }
    private struct PendingStamp { public float U, V, Radius; public int Color; public StrokeKind Kind; }
    private readonly List<PendingStamp> pendingStamps = new List<PendingStamp>(64);
    private float nextSendTime;

    // 캔버스에 그릴 스탬프 묶음(로컬: 한 프레임 동안 모아 Update 끝에서 한 번에, 수신: 이벤트 하나를 한 번에).
    // 예전에는 스탬프 1개마다 캔버스 전체(512x512)를 임시 RT로 복사한 뒤 다시 그리는 Blit 2회를 수행했다(§30.5 F5).
    private readonly List<PendingStamp> localDrawStamps = new List<PendingStamp>(64);
    private readonly List<PendingStamp> remoteDrawStamps = new List<PendingStamp>(64);

    // 반경이 이 값 이상이면 캔버스 전체를 덮는 강제 도포로 본다(ApplyForcedColorIfAssignedToMe가 float.MaxValue 사용).
    private const float FullCanvasRadius = 1f;

    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int RespectLockId = Shader.PropertyToID("_RespectLock");

    // Mesh_0의 MeshCollider는 스킨 애니메이션을 따라가지 않고 임포트 시점 바인드 포즈에 고정돼
    // 있어, 실제 화면에 보이는 포즈와 레이캐스트 대상 표면이 크게 어긋나는 문제가 있었다 —
    // 상체가 안 칠해지고 붓 커서가 몸속으로 파고들어 보이던 원인(Bug-fix-plan.md §20). 로컬
    // 플레이어가 색칠 페이즈 중일 때만 매 프레임 현재 포즈를 구워 콜라이더에 반영한다.
    // 갱신은 PaintColliderUpdater가 맡는다 — 예전에는 여기서 3프레임마다 동기로 굽고 쿠킹해(정점 16.7만 개) 색칠
    // 페이즈 내내 프레임이 끊겼다(Bug-fix-plan.md §30.5). 이제 필요할 때만, 쿠킹은 워커 스레드에서 한다.
    private MeshCollider paintableMeshCollider;
    private SkinnedMeshRenderer skinnedBodyRenderer;
    private PaintColliderUpdater colliderUpdater;

    // RenderTexture와 스탬프 머티리얼은 Awake에서 만든다. 괴물이 대기실에서 기다리다 GameScene에 도착하면,
    // 쌓여 있던 "이 캐릭터의 Instantiate → 그 캐릭터의 스트로크 이벤트 수십~수천 개"가 같은 Dispatch 루프에서
    // 연달아 처리된다 — Start()는 다음 프레임에야 실행되므로, 캔버스를 Start에서 만들면 그 사이 도착한
    // 스트로크가 null 캔버스에 그려지다 예외가 났다(Bug-fix-plan.md §23.3.4-④). 이벤트 수신 등록은
    // MonoBehaviourPunCallbacks.OnEnable(Awake 직후)이 하므로 Awake면 충분히 이르다.
    private void Awake()
    {
        CreatePaintCanvasTexture();
        brushStampInstance = new Material(brushStampMaterial);
        finalizeStampInstance = new Material(finalizeStampMaterial);
        eraseStampInstance = new Material(eraseStampMaterial);
        ConfigureStampBlend(brushStampInstance);
        ConfigureStampBlend(finalizeStampInstance);
    }

    // 잠금 규칙을 캔버스 읽기 대신 하드웨어 블렌딩으로 처리한다(PaintStamp.shader 참고). 머티리얼의 _RespectLock 값을 따른다.
    private static void ConfigureStampBlend(Material material)
    {
        bool respectLock = !material.HasProperty(RespectLockId) || material.GetFloat(RespectLockId) > 0.5f;
        material.SetFloat(SrcBlendId, (float)(respectLock ? UnityEngine.Rendering.BlendMode.OneMinusDstAlpha : UnityEngine.Rendering.BlendMode.One));
        material.SetFloat(DstBlendId, (float)(respectLock ? UnityEngine.Rendering.BlendMode.DstAlpha : UnityEngine.Rendering.BlendMode.Zero));
    }

    private void Start()
    {
        if (pv.IsMine) Local = this;
        localCamera = Camera.main;
        currentBrushRadius = Mathf.Clamp(brushSettings.DefaultRadius, brushSettings.MinRadius, brushSettings.MaxRadius);
        ApplyPaintedSkinMaterial();

        // 콜라이더 베이크는 로컬 소유자가 색칠할 때만 필요하므로 원격 인스턴스에는 메시를 만들지 않는다(research.md §8.19).
        paintableMeshCollider = paintableCollider as MeshCollider;
        skinnedBodyRenderer = bodyRenderer as SkinnedMeshRenderer;
        if (pv.IsMine && paintableMeshCollider != null && skinnedBodyRenderer != null)
            colliderUpdater = new PaintColliderUpdater(skinnedBodyRenderer, paintableMeshCollider, $"{gameObject.name}_{pv.ViewID}");

        // 이 클라이언트가 강제 도포 대상으로 이미 확정돼 있는 상태에서 늦게 Start()가 실행되는
        // 경우(재접속 등)를 대비해 한 번 확인한다. 색칠 씬이 아니면(대기실) 하지 않는다 — 대기실로 돌아온 쿠키가
        // 아직 지워지지 않은 이전 판 강제 도포 정보를 읽어 다시 칠해지던 문제(Bug-fix-plan.md §26.2 ⑲).
        if (pv.IsMine && PaintPhaseController.IsPaintScene) ApplyForcedColorIfAssignedToMe();
    }

    // 인스턴스 전용 RenderTexture를 만들어 투명(미도색)으로 초기화한다 — 스킨과 무관한 부분이라 Awake에서 수행
    private void CreatePaintCanvasTexture()
    {
        PaintCanvas = new RenderTexture(canvasSize, canvasSize, 0, RenderTextureFormat.ARGB32);
        PaintCanvas.name = $"PaintCanvas_{gameObject.name}_{pv.ViewID}";
        PaintCanvas.Create();
        ClearCanvas();
    }

    // 캐릭터 렌더러에 원본 스킨 + 페인트를 합성하는 머티리얼을 입힌다. PlayerSkinApplier.Awake()가
    // 스킨(sharedMaterial)을 바꾼 뒤여야 하므로 Start에서 수행한다(같은 오브젝트의 Awake 간 순서는 보장되지 않음).
    private void ApplyPaintedSkinMaterial()
    {
        if (bodyRenderer == null || paintedSkinShader == null) return;

        Material original = bodyRenderer.sharedMaterial;
        paintedSkinInstance = new Material(paintedSkinShader);
        if (original != null && original.HasProperty("_MainTex"))
            paintedSkinInstance.SetTexture("_MainTex", original.mainTexture);
        paintedSkinInstance.SetTexture("_PaintTex", PaintCanvas);

        bodyRenderer.sharedMaterial = paintedSkinInstance; // 인스턴스를 직접 만들었으므로 .material의 자동 복제는 쓰지 않는다
    }

    // 스킨(SkinIndex)이 합성 머티리얼을 만든 뒤에 늦게 도착한 경우 PlayerSkinApplier가 호출한다. 합성
    // 머티리얼을 스킨 원본으로 통째로 바꾸면 칠한 색이 사라지므로 기본 텍스처만 교체한다.
    public bool TrySetBaseSkin(Material skin)
    {
        if (paintedSkinInstance == null || skin == null) return false;
        if (skin.HasProperty("_MainTex")) paintedSkinInstance.SetTexture("_MainTex", skin.mainTexture);
        return true;
    }

    private void Update()
    {
        if (!pv.IsMine) return;

        // 대기실 등 색칠 씬이 아닌 곳에서는 입력·콜라이더 베이크를 하지 않는다. 괴물이 대기실에서 기다리는 동안
        // PaintPhaseEndTime이 유효해 대기실 쿠키가 3프레임마다 베이크(프레임당 약 6ms)하던 비용도 없앤다.
        if (!PaintPhaseController.IsPaintScene || !IsPaintPhaseActive())
        {
            isStroking = false;
            colliderUpdater?.Tick(false, 0f); // 진행 중인 쿠킹 Job의 완료 처리만
            FlushStrokes(); // 페이즈가 끝나는 순간 남은 스탬프를 보낸다
            return;
        }

        // 콜라이더는 칠하는 중이거나 커서가 몸 근처에 있을 때만 현재 포즈로 갱신한다 — 스와치 UI를 고르거나
        // 가만히 서 있는 동안에는 비용이 없다(§30.5 F1).
        colliderUpdater?.Tick(IsColliderRefreshWanted(), GameSettings.Current.PaintColliderRefreshInterval);

        HandleBrushSizeInput();
        HandlePaintInput();
        DrawStamps(localDrawStamps);

        if (Time.unscaledTime >= nextSendTime) FlushStrokes();
    }

    private bool IsColliderRefreshWanted()
    {
        if (PlayerInput.PaintHeld) return true;
        if (localCamera == null || bodyRenderer == null) return false;
        return IsPointerOverBounds(localCamera, bodyRenderer.bounds, PlayerInput.PointerPosition);
    }

    // 몸 렌더러의 월드 경계 상자를 화면에 투영한 사각형 안에 마우스가 있는지(정밀 판정은 레이캐스트가 한다).
    private static bool IsPointerOverBounds(Camera cam, Bounds bounds, Vector2 pointer)
    {
        Vector3 min = bounds.min, max = bounds.max;
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            Vector3 sp = cam.WorldToScreenPoint(corner);
            if (sp.z <= 0f) return true; // 카메라가 상자 안/뒤에 걸침 — 보수적으로 갱신
            xMin = Mathf.Min(xMin, sp.x); xMax = Mathf.Max(xMax, sp.x);
            yMin = Mathf.Min(yMin, sp.y); yMax = Mathf.Max(yMax, sp.y);
        }
        return pointer.x >= xMin && pointer.x <= xMax && pointer.y >= yMin && pointer.y <= yMax;
    }

    private void HandlePaintInput()
    {
        if (!PlayerInput.PaintHeld)
        {
            if (isStroking) FlushStrokes(); // 한 획이 끝나면 지연 없이 보낸다
            isStroking = false;
            return;
        }

        if (localCamera == null) localCamera = Camera.main;
        if (localCamera == null) return;
        if (!isErasing && currentBrushColorIndex < 0) return; // 아직 붓에 담긴 색이 없으면 칠하지 않음

        Vector2 mousePos = PlayerInput.PointerPosition;
        if (!isStroking)
        {
            isStroking = true;
            lastStampScreenPos = mousePos;
            StampAtScreenPoint(mousePos);
            return;
        }

        // 지난 스탬프 위치에서 현재 마우스 위치까지 일정 간격으로 보간한다. 마우스가 멈춰 있으면 새로
        // 찍지 않으므로 누르고만 있어도 슬롯 카운트가 오르지 않는다.
        float distance = Vector2.Distance(lastStampScreenPos, mousePos);
        if (distance < stampSpacingPixels) return;

        int steps = Mathf.Min(Mathf.FloorToInt(distance / stampSpacingPixels), maxStampsPerFrame);
        Vector2 from = lastStampScreenPos;
        for (int i = 1; i <= steps; i++)
            StampAtScreenPoint(Vector2.Lerp(from, mousePos, (float)i / steps));
        lastStampScreenPos = mousePos;
    }

    // 씬 전체(Physics.Raycast)가 아니라 내 몸 콜라이더 하나만 검사한다. 예전에는 레이어 마스크로
    // 루트 캡슐(PlayerCapsule)을 제외했는데, 루트 레이어가 Cookie로 바뀌면서 캡슐이 다시 몸 메시를
    // 가려 색칠이 막혔다(Bug-fix-plan.md §17 회귀, §23.2.2). 이 방식은 레이어 설정과 무관하다.
    private void StampAtScreenPoint(Vector2 screenPos)
    {
        Ray ray = localCamera.ScreenPointToRay(screenPos);
        if (!paintableCollider.Raycast(ray, out RaycastHit hit, MaxPaintRayDistance)) return;

        if (isErasing)
        {
            QueueLocalDraw(hit.textureCoord, currentBrushRadius, 0, StrokeKind.Erase);
            QueueStamp(hit.textureCoord, currentBrushRadius, 0, StrokeKind.Erase);
            return;
        }

        TryRegisterSlotAndStamp(hit.textureCoord);
    }

    // 현재 색이 이미 등록된 슬롯이면 곧바로 칠하고, 아직 미등록이면 임계량(minStrokesToRegister)에
    // 도달했는지 누적 카운트로 판정한다(GameRule.md §3.2).
    private void TryRegisterSlotAndStamp(Vector2 uv)
    {
        int brushColor = currentBrushColorIndex;

        if (!registeredColorSlots.Contains(brushColor))
        {
            if (registeredColorSlots.Count >= GameSettings.Current.MaxColorSlots) return; // 슬롯을 모두 사용함 — 새 색은 칠하지 않는다

            pendingStrokeCounts.TryGetValue(brushColor, out int count);
            count++;
            if (count >= minStrokesToRegister)
            {
                pendingStrokeCounts.Remove(brushColor);
                registeredColorSlots.Add(brushColor);
                ReportSlotCount();
            }
            else
            {
                pendingStrokeCounts[brushColor] = count;
            }
        }

        QueueLocalDraw(uv, currentBrushRadius, brushColor, StrokeKind.Normal);
        QueueStamp(uv, currentBrushRadius, brushColor, StrokeKind.Normal);
    }

    // 등록 슬롯 수를 자기 자신의 Player CustomProperties에 계속 보고(GameRule.md §3.6이
    // 이 값을 읽어 등록 슬롯 0개 플레이어를 판별한다)
    private void ReportSlotCount()
    {
        if (!pv.IsMine) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable { { NetKeys.RegisteredSlotCount, registeredColorSlots.Count } });
    }

    // 색 슬롯 UI에서 스와치를 클릭했을 때 호출 — 로컬 전용 선택값이라 네트워크 동기화 불필요
    // (다른 클라이언트는 이 캐릭터가 실제로 칠한 결과만 보면 되지, "지금 뭘 들고 있는지"는
    // 볼 필요가 없다).
    public void SetBrushColor(int colorIndex)
    {
        currentBrushColorIndex = colorIndex;
        isErasing = false;
    }

    public void SetEraseMode()
    {
        isErasing = true;
    }

    // Reset — 전체 캔버스를 미도색 상태로 되돌리고 등록 슬롯도 초기화한다(GameRule.md §3.4).
    public void ResetCanvas()
    {
        if (!pv.IsMine) return;

        FlushStrokes(); // 리셋 이전 스탬프가 리셋 이후에 도착해 다시 칠해지지 않도록 먼저 보낸다
        localDrawStamps.Clear();
        ClearCanvas();

        registeredColorSlots.Clear();
        pendingStrokeCounts.Clear();
        ReportSlotCount();

        SendResetEvent();
    }

    private void ClearCanvas()
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = PaintCanvas;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }

    private static bool IsPaintPhaseActive() => GamePhaseState.IsPaintActive;

    // 마우스 휠로 붓 크기를 min~max 범위 내에서 조절
    private void HandleBrushSizeInput()
    {
        float scroll = PlayerInput.BrushSizeDelta;
        if (Mathf.Approximately(scroll, 0f)) return;

        currentBrushRadius = Mathf.Clamp(
            currentBrushRadius + scroll * brushSettings.WheelStep,
            brushSettings.MinRadius,
            brushSettings.MaxRadius);
    }

    private void QueueStamp(Vector2 uv, float radius, int colorIndex, StrokeKind kind)
    {
        pendingStamps.Add(new PendingStamp { U = uv.x, V = uv.y, Radius = radius, Color = colorIndex, Kind = kind });
        if (pendingStamps.Count >= maxStampsPerEvent) FlushStrokes();
    }

    // 쌓인 스탬프를 한 이벤트로 보낸다. 페이로드: { viewId, float[] u, float[] v, float[] radius, int[] color, byte[] kind }
    private void FlushStrokes()
    {
        int playerCount = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
        nextSendTime = Time.unscaledTime + GameSettings.Current.PaintStrokeSendIntervalFor(playerCount);
        if (pendingStamps.Count == 0) return;
        if (!PhotonNetwork.InRoom) { pendingStamps.Clear(); return; }

        int n = pendingStamps.Count;
        var us = new float[n];
        var vs = new float[n];
        var radii = new float[n];
        var colors = new int[n];
        var kinds = new byte[n];
        for (int i = 0; i < n; i++)
        {
            PendingStamp s = pendingStamps[i];
            us[i] = s.U; vs[i] = s.V; radii[i] = s.Radius; colors[i] = s.Color; kinds[i] = (byte)s.Kind;
        }
        pendingStamps.Clear();

        object[] content = { pv.ViewID, us, vs, radii, colors, kinds };
        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(NetEventCodes.PaintStroke, content, options, SendOptions.SendReliable);
    }

    private void SendResetEvent()
    {
        if (!PhotonNetwork.InRoom) return;

        object[] content = { pv.ViewID };
        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(NetEventCodes.ClearColor, content, options, SendOptions.SendReliable);
    }

    // 다른 클라이언트가 보낸 스탬프 묶음(또는 리셋)을 수신해 재생
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.PaintStroke && photonEvent.Code != NetEventCodes.ClearColor) return;
        if (!(photonEvent.CustomData is object[] data) || data.Length == 0 || !(data[0] is int viewId)) return;
        if (pv == null || viewId != pv.ViewID) return;
        if (!PaintPhaseController.IsPaintScene) return; // 씬 전환 중 늦게 도착한 스트로크가 대기실 캐릭터에 그려지지 않게

        if (photonEvent.Code == NetEventCodes.ClearColor)
        {
            ClearCanvas();
            return;
        }

        if (data.Length < 6 || !(data[1] is float[] us) || !(data[2] is float[] vs) || !(data[3] is float[] radii)
            || !(data[4] is int[] colors) || !(data[5] is byte[] kinds)) return;

        // 송신 측이 이미 판단을 끝냈으므로 그대로 재생만 한다 — 이벤트 하나(최대 maxStampsPerEvent개)를 한 번에 그린다.
        int count = Mathf.Min(us.Length, Mathf.Min(vs.Length, Mathf.Min(radii.Length, Mathf.Min(colors.Length, kinds.Length))));
        remoteDrawStamps.Clear();
        for (int i = 0; i < count; i++)
            remoteDrawStamps.Add(new PendingStamp { U = us[i], V = vs[i], Radius = radii[i], Color = colors[i], Kind = (StrokeKind)kinds[i] });
        DrawStamps(remoteDrawStamps);
    }

    private void QueueLocalDraw(Vector2 uv, float radius, int colorIndex, StrokeKind kind)
    {
        localDrawStamps.Add(new PendingStamp { U = uv.x, V = uv.y, Radius = radius, Color = colorIndex, Kind = kind });
    }

    // 스탬프 묶음을 캔버스에 그린다(로컬/원격 공용) — uv 중심, radius 반경의 원. 스탬프 영역만 덮는 사각형을 GL로
    // 그리고 셰이더가 원 밖을 버린다. 같은 종류가 이어지는 구간은 SetPass 한 번에 모두 그리고, 순서(지우개→붓 등)는
    // 보낸 순서 그대로 유지한다. 그린 뒤 목록을 비운다.
    private void DrawStamps(List<PendingStamp> stamps)
    {
        if (stamps.Count == 0 || PaintCanvas == null) return;

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = PaintCanvas;
        GL.PushMatrix();
        GL.LoadOrtho();

        int i = 0;
        while (i < stamps.Count)
        {
            StrokeKind kind = stamps[i].Kind;
            MaterialFor(kind).SetPass(0);
            GL.Begin(GL.QUADS);
            for (; i < stamps.Count && stamps[i].Kind == kind; i++)
                EmitStampQuad(stamps[i]);
            GL.End();
        }

        GL.PopMatrix();
        RenderTexture.active = prev;
        stamps.Clear();
    }

    private Material MaterialFor(StrokeKind kind)
    {
        switch (kind)
        {
            case StrokeKind.Erase: return eraseStampInstance;
            case StrokeKind.ForceFill: return finalizeStampInstance;
            default: return brushStampInstance;
        }
    }

    private void EmitStampQuad(PendingStamp s)
    {
        // 정점 색은 머티리얼 SetColor와 달리 색 공간 변환이 없으므로, Linear 프로젝트에서는 직접 선형으로 바꿔 넘긴다
        // (예전 _StampColor 경로와 같은 결과 — 캔버스에 팔레트 색 그대로 저장됨).
        Color color = s.Kind == StrokeKind.Erase ? Color.clear : palette.GetColor(s.Color);
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;
        GL.Color(color);

        if (s.Radius >= FullCanvasRadius)
        {
            // 캔버스 전체(강제 도포) — 원 판정 좌표를 0으로 두어 전부 칠한다.
            GL.TexCoord2(0f, 0f); GL.Vertex3(0f, 0f, 0f);
            GL.TexCoord2(0f, 0f); GL.Vertex3(0f, 1f, 0f);
            GL.TexCoord2(0f, 0f); GL.Vertex3(1f, 1f, 0f);
            GL.TexCoord2(0f, 0f); GL.Vertex3(1f, 0f, 0f);
            return;
        }

        float r = s.Radius;
        GL.TexCoord2(-1f, -1f); GL.Vertex3(s.U - r, s.V - r, 0f);
        GL.TexCoord2(-1f, 1f); GL.Vertex3(s.U - r, s.V + r, 0f);
        GL.TexCoord2(1f, 1f); GL.Vertex3(s.U + r, s.V + r, 0f);
        GL.TexCoord2(1f, -1f); GL.Vertex3(s.U + r, s.V - r, 0f);
    }

    // 강제 도포(§3.6) — 마스터가 PaintPhaseController로 배정한 색을 전신에 덮어씌운다.
    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.ForcedPaintActorNumbers)) return;
        if (!pv.IsMine || !PaintPhaseController.IsPaintScene) return;
        ApplyForcedColorIfAssignedToMe();
    }

    private void ApplyForcedColorIfAssignedToMe()
    {
        if (!RoomState.TryGetIntArray(NetKeys.ForcedPaintActorNumbers, out int[] actorNumbers)) return;
        if (!RoomState.TryGetIntArray(NetKeys.ForcedPaintColors, out int[] colors)) return;
        if (PhotonNetwork.LocalPlayer == null) return;

        int myIndex = System.Array.IndexOf(actorNumbers, PhotonNetwork.LocalPlayer.ActorNumber);
        if (myIndex < 0 || myIndex >= colors.Length) return;

        localDrawStamps.Clear(); // 아직 그리지 않은 이번 프레임 스탬프가 강제 도포 위에 덧칠되지 않도록
        QueueLocalDraw(Vector2.zero, float.MaxValue, colors[myIndex], StrokeKind.ForceFill);
        DrawStamps(localDrawStamps);
        QueueStamp(Vector2.zero, float.MaxValue, colors[myIndex], StrokeKind.ForceFill);
        FlushStrokes();

        registeredColorSlots.Clear();
        registeredColorSlots.Add(colors[myIndex]);
        ReportSlotCount(); // 강제 도포 후 슬롯 수를 다시 보고한다(research.md §8.18)
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (pv != null && pv.IsMine) FlushStrokes();
    }

    private void OnDestroy()
    {
        if (Local == this) Local = null;

        if (PaintCanvas != null)
        {
            PaintCanvas.Release();
            Destroy(PaintCanvas);
            PaintCanvas = null;
        }

        colliderUpdater?.Dispose();
        colliderUpdater = null;

        DestroyIfCreated(paintedSkinInstance);
        DestroyIfCreated(brushStampInstance);
        DestroyIfCreated(finalizeStampInstance);
        DestroyIfCreated(eraseStampInstance);
    }

    private static void DestroyIfCreated(Object obj)
    {
        if (obj != null) Destroy(obj);
    }
}
