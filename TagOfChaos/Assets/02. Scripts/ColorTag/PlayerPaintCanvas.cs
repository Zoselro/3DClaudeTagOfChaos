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

    public RenderTexture PaintCanvas { get; private set; }
    public Collider PaintableCollider => paintableCollider;
    public float CurrentBrushRadius => currentBrushRadius;
    public bool IsMine => pv != null && pv.IsMine;
    public int CurrentBrushColorIndex => isErasing ? -1 : currentBrushColorIndex;
    public IReadOnlyList<int> RegisteredColorSlots => registeredColorSlots;

    public event System.Action<IReadOnlyList<int>> OnSlotsChanged;
    public event System.Action OnSlotRejected; // "이미 4가지 색상을 모두 사용했습니다"

    private Camera localCamera;
    private float currentBrushRadius;
    private int paintRaycastMask;
    private int currentBrushColorIndex = -1; // 로컬 전용 선택값(네트워크 동기화 불필요 — 이 클라이언트만 참조)
    private bool isErasing;

    private readonly Dictionary<int, int> pendingStrokeCounts = new Dictionary<int, int>(); // 미등록 색 → 누적 스탬프 수
    private readonly List<int> registeredColorSlots = new List<int>(4);

    // Mesh_0의 MeshCollider는 스킨 애니메이션을 따라가지 않고 임포트 시점 바인드 포즈에 고정돼
    // 있어, 실제 화면에 보이는 포즈와 레이캐스트 대상 표면이 크게 어긋나는 문제가 있었다 —
    // 상체가 안 칠해지고 붓 커서가 몸속으로 파고들어 보이던 원인(Bug-fix-plan.md §20). 로컬
    // 플레이어가 색칠 페이즈 중일 때만 매 프레임 현재 포즈를 구워 콜라이더에 반영한다.
    private MeshCollider paintableMeshCollider;
    private SkinnedMeshRenderer skinnedBodyRenderer;
    private Mesh bakedColliderMesh;

    // BakeMesh(정점 16만개대) + MeshCollider 재계산(cook)이 프레임당 약 6ms 이상 들어(Play Mode
    // 실측: 매 프레임 갱신 시 257fps -> 15fps로 급락) 매 프레임 수행하면 안 된다 — 3프레임에 1번만
    // 갱신해 비용을 1/3로 줄인다.
    private const int ColliderRefreshInterval = 3;
    private int colliderRefreshCounter;

    private void Start()
    {
        localCamera = Camera.main;
        currentBrushRadius = Mathf.Clamp(brushSettings.DefaultRadius, brushSettings.MinRadius, brushSettings.MaxRadius);
        // 캐릭터 자신의 물리용 CapsuleCollider가 붓칠 대상인 몸통 메시를 가려 레이캐스트가 항상
        // 캡슐에 먼저 맞는 문제를 막기 위해, 붓칠 레이캐스트에서는 PlayerCapsule 레이어를 제외한다.
        paintRaycastMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("PlayerCapsule");
        InitPaintCanvas();

        paintableMeshCollider = paintableCollider as MeshCollider;
        skinnedBodyRenderer = bodyRenderer as SkinnedMeshRenderer;
        if (paintableMeshCollider != null && skinnedBodyRenderer != null)
        {
            bakedColliderMesh = new Mesh();
            bakedColliderMesh.name = $"BakedColliderMesh_{gameObject.name}_{pv.ViewID}";
        }

        // 이 클라이언트가 강제 도포 대상으로 이미 확정돼 있는 상태에서 늦게 Start()가 실행되는
        // 경우(재접속 등)를 대비해 한 번 확인한다.
        if (pv.IsMine) ApplyForcedColorIfAssignedToMe();
    }

    // 인스턴스 전용 RenderTexture를 만들고, 캐릭터 렌더러에 원본 스킨 + 페인트를 합성하는 머티리얼을 입힌다
    private void InitPaintCanvas()
    {
        PaintCanvas = new RenderTexture(canvasSize, canvasSize, 0, RenderTextureFormat.ARGB32);
        PaintCanvas.name = $"PaintCanvas_{gameObject.name}_{pv.ViewID}";
        PaintCanvas.Create();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = PaintCanvas;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;

        if (bodyRenderer == null || paintedSkinShader == null) return;

        Material original = bodyRenderer.sharedMaterial;
        Material painted = new Material(paintedSkinShader);
        if (original != null && original.HasProperty("_MainTex"))
            painted.SetTexture("_MainTex", original.mainTexture);
        painted.SetTexture("_PaintTex", PaintCanvas);

        bodyRenderer.material = painted; // .material은 자동으로 인스턴스를 만들어 다른 캐릭터와 공유되지 않음
    }

    private void Update()
    {
        if (!pv.IsMine) return;
        if (!IsPaintPhaseActive()) return;

        colliderRefreshCounter++;
        if (colliderRefreshCounter >= ColliderRefreshInterval)
        {
            colliderRefreshCounter = 0;
            RefreshColliderMesh();
        }

        HandleBrushSizeInput();

        if (!Input.GetMouseButton(0)) return;
        if (localCamera == null) localCamera = Camera.main;
        if (localCamera == null) return;

        Ray ray = localCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, paintRaycastMask)) return;
        if (hit.collider != paintableCollider) return; // 자신의 오브젝트가 아니면 무시

        if (isErasing)
        {
            ApplyStamp(eraseStampMaterial, hit.textureCoord, currentBrushRadius, 0);
            SendStrokeEvent(hit.textureCoord, currentBrushRadius, 0, StrokeKind.Erase);
            return;
        }

        if (currentBrushColorIndex < 0) return; // 아직 붓에 담긴 색이 없으면 칠하지 않음

        TryRegisterSlotAndStamp(hit.textureCoord);
    }

    // 현재 색이 이미 등록된 슬롯이면 곧바로 칠하고, 아직 미등록이면 임계량(minStrokesToRegister)에
    // 도달했는지 누적 카운트로 판정한다(GameRule.md §3.2).
    private void TryRegisterSlotAndStamp(Vector2 uv)
    {
        int brushColor = currentBrushColorIndex;

        if (!registeredColorSlots.Contains(brushColor))
        {
            if (registeredColorSlots.Count >= 4)
            {
                OnSlotRejected?.Invoke();
                return;
            }

            pendingStrokeCounts.TryGetValue(brushColor, out int count);
            count++;
            if (count >= minStrokesToRegister)
            {
                pendingStrokeCounts.Remove(brushColor);
                registeredColorSlots.Add(brushColor);
                OnSlotsChanged?.Invoke(registeredColorSlots);
                ReportSlotCount();
            }
            else
            {
                pendingStrokeCounts[brushColor] = count;
            }
        }

        ApplyStamp(brushStampMaterial, uv, currentBrushRadius, brushColor);
        SendStrokeEvent(uv, currentBrushRadius, brushColor, StrokeKind.Normal);
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

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = PaintCanvas;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;

        registeredColorSlots.Clear();
        pendingStrokeCounts.Clear();
        OnSlotsChanged?.Invoke(registeredColorSlots);
        ReportSlotCount();

        SendResetEvent();
    }

    private bool IsPaintPhaseActive()
    {
        return RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double endTime) && PhotonNetwork.Time < endTime;
    }

    // 현재 애니메이션 포즈를 구워 MeshCollider에 반영(Bug-fix-plan.md §20.6 A안).
    private void RefreshColliderMesh()
    {
        if (paintableMeshCollider == null || skinnedBodyRenderer == null) return;

        skinnedBodyRenderer.BakeMesh(bakedColliderMesh, false);

        Vector3 localScale = skinnedBodyRenderer.transform.localScale;
        Vector3[] verts = bakedColliderMesh.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = new Vector3(verts[i].x / localScale.x, verts[i].y / localScale.y, verts[i].z / localScale.z);
        }
        bakedColliderMesh.vertices = verts;
        bakedColliderMesh.RecalculateBounds();

        paintableMeshCollider.sharedMesh = null;
        paintableMeshCollider.sharedMesh = bakedColliderMesh;
    }

    // 마우스 휠로 붓 크기를 min~max 범위 내에서 조절
    private void HandleBrushSizeInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f)) return;

        currentBrushRadius = Mathf.Clamp(
            currentBrushRadius + scroll * brushSettings.WheelStep,
            brushSettings.MinRadius,
            brushSettings.MaxRadius);
    }

    private enum StrokeKind : byte { Normal, Erase, ForceFill }

    private void SendStrokeEvent(Vector2 uv, float radius, int colorIndex, StrokeKind kind)
    {
        if (!PhotonNetwork.InRoom) return;

        object[] content = { pv.ViewID, uv.x, uv.y, radius, colorIndex, (byte)kind };
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

    // 다른 클라이언트가 보낸 스트로크(또는 지우개/리셋)를 수신해 재생
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == NetEventCodes.PaintStroke)
        {
            object[] data = (object[])photonEvent.CustomData;
            int viewId = (int)data[0];
            if (pv == null || viewId != pv.ViewID) return;

            Vector2 uv = new Vector2((float)data[1], (float)data[2]);
            float radius = (float)data[3];
            int colorIndex = (int)data[4];
            var kind = (StrokeKind)(byte)data[5];

            Material material = kind switch
            {
                StrokeKind.Erase => eraseStampMaterial,
                StrokeKind.ForceFill => finalizeStampMaterial,
                _ => brushStampMaterial,
            };
            ApplyStamp(material, uv, radius, colorIndex); // 송신 측이 이미 판단을 끝냈으므로 그대로 재생만 함
        }
        else if (photonEvent.Code == NetEventCodes.ClearColor)
        {
            object[] data = (object[])photonEvent.CustomData;
            int viewId = (int)data[0];
            if (pv == null || viewId != pv.ViewID) return;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = PaintCanvas;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;
        }
    }

    // uv 위치를 중심으로 radius 반경의 원형 스탬프를 paintCanvas에 그림 (로컬/원격 공용)
    private void ApplyStamp(Material stampMaterial, Vector2 uv, float radius, int colorIndex)
    {
        stampMaterial.SetVector("_StampUV", uv);
        stampMaterial.SetFloat("_StampRadius", radius);
        stampMaterial.SetColor("_StampColor", palette.GetColor(colorIndex));

        RenderTexture temp = RenderTexture.GetTemporary(PaintCanvas.width, PaintCanvas.height, 0, PaintCanvas.format);
        Graphics.Blit(PaintCanvas, temp);
        Graphics.Blit(temp, PaintCanvas, stampMaterial);
        RenderTexture.ReleaseTemporary(temp);
    }

    // 강제 도포(§3.6) — 마스터가 PaintPhaseController로 배정한 색을 전신에 덮어씌운다.
    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.ForcedPaintActorNumbers)) return;
        if (!pv.IsMine) return;
        ApplyForcedColorIfAssignedToMe();
    }

    private void ApplyForcedColorIfAssignedToMe()
    {
        if (!RoomState.TryGetIntArray(NetKeys.ForcedPaintActorNumbers, out int[] actorNumbers)) return;
        if (!RoomState.TryGetIntArray(NetKeys.ForcedPaintColors, out int[] colors)) return;
        if (PhotonNetwork.LocalPlayer == null) return;

        int myIndex = System.Array.IndexOf(actorNumbers, PhotonNetwork.LocalPlayer.ActorNumber);
        if (myIndex < 0) return;

        ApplyStamp(finalizeStampMaterial, Vector2.zero, float.MaxValue, colors[myIndex]);
        SendStrokeEvent(Vector2.zero, float.MaxValue, colors[myIndex], StrokeKind.ForceFill);

        registeredColorSlots.Clear();
        registeredColorSlots.Add(colors[myIndex]);
        OnSlotsChanged?.Invoke(registeredColorSlots);
    }

    private void OnDestroy()
    {
        if (PaintCanvas != null)
        {
            PaintCanvas.Release();
            PaintCanvas = null;
        }

        if (bakedColliderMesh != null)
        {
            Destroy(bakedColliderMesh);
            bakedColliderMesh = null;
        }
    }
}
