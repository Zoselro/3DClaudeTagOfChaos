using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerPaintCanvas : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Collider paintableCollider;
    [SerializeField] private Renderer bodyRenderer; // 캐릭터 스킨 렌더러 (SkinnedMeshRenderer)
    [SerializeField] private Shader paintedSkinShader; // ColorTag/PlayerPaintedSkin
    [SerializeField] private ColorPaletteSO palette;
    [SerializeField] private BrushSettingsSO brushSettings;
    [SerializeField] private Material brushStampMaterial;    // 일반 스탬프: 잠긴(알파=1) 픽셀은 건드리지 않음
    [SerializeField] private Material finalizeStampMaterial; // 라운드 확정 재도색 전용: 잠금 무시하고 항상 덮어씀
    [SerializeField] private int canvasSize = 512;

    // 인스턴스별로 새로 만드는 페인트 캔버스 (RenderTexture를 직렬화 필드로 공유하면 모든 캐릭터가 같은
    // 텍스처를 덮어써버리므로, 반드시 런타임에 개별 생성해야 함)
    public RenderTexture PaintCanvas { get; private set; }

    // BrushCursorController가 로컬 플레이어의 캐릭터를 찾아 붓 오브젝트를 표면에 맞춰 배치할 때 참조 (12장)
    public Collider PaintableCollider => paintableCollider;
    public float CurrentBrushRadius => currentBrushRadius;
    public bool IsMine => pv != null && pv.IsMine;

    private Camera localCamera;
    private float currentBrushRadius;
    private readonly List<Vector2> currentRoundStrokes = new List<Vector2>();
    private int trackedRoundIndex = -1;
    private int paintRaycastMask;

    // Mesh_0의 MeshCollider는 스킨 애니메이션을 따라가지 않고 임포트 시점 바인드 포즈에 고정돼
    // 있어, 실제 화면에 보이는 포즈와 레이캐스트 대상 표면이 크게 어긋나는 문제가 있었다 —
    // 상체가 안 칠해지고 붓 커서가 몸속으로 파고들어 보이던 원인(Bug-fix-plan.md §20). 로컬
    // 플레이어가 색상 라운드를 진행 중일 때만 매 프레임 현재 포즈를 구워 콜라이더에 반영한다.
    private MeshCollider paintableMeshCollider;
    private SkinnedMeshRenderer skinnedBodyRenderer;
    private Mesh bakedColliderMesh;

    // BakeMesh(정점 16만개대) + MeshCollider 재계산(cook)이 프레임당 약 6ms 이상 들어(Play Mode
    // 실측: 매 프레임 갱신 시 257fps -> 15fps로 급락) 매 프레임 수행하면 안 된다 — 3프레임에 1번만
    // 갱신해 비용을 1/3로 줄인다. 붓칠은 마우스를 천천히 움직이며 하는 조작이라 2프레임(약 0.03~0.05초)
    // 지연은 체감상 무시할 수준이다(Bug-fix-plan.md §20.6-4/§20.8-6).
    private const int ColliderRefreshInterval = 3;
    private int colliderRefreshCounter;

private void Start()
    {
        localCamera = Camera.main;
        currentBrushRadius = Mathf.Clamp(brushSettings.DefaultRadius, brushSettings.MinRadius, brushSettings.MaxRadius);
        // 캐릭터 자신의 물리용 CapsuleCollider가 붓칠 대상인 Ch36을 가려 레이캐스트가 항상 캡슐에
        // 먼저 맞는 문제를 막기 위해, 붓칠 레이캐스트에서는 PlayerCapsule 레이어를 제외한다
        // (Bug-fix-plan.md §17).
        paintRaycastMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("PlayerCapsule");
        InitPaintCanvas();

        paintableMeshCollider = paintableCollider as MeshCollider;
        skinnedBodyRenderer = bodyRenderer as SkinnedMeshRenderer;
        if (paintableMeshCollider != null && skinnedBodyRenderer != null)
        {
            bakedColliderMesh = new Mesh();
            bakedColliderMesh.name = $"BakedColliderMesh_{gameObject.name}_{pv.ViewID}";
        }
    }

    // 인스턴스 전용 RenderTexture를 만들고, 캐릭터 렌더러에 원본 스킨 + 페인트를 합성하는 머티리얼을 입힌다
    private void InitPaintCanvas()
    {
        PaintCanvas = new RenderTexture(canvasSize, canvasSize, 0, RenderTextureFormat.ARGB32);
        PaintCanvas.name = $"PaintCanvas_{gameObject.name}_{pv.ViewID}";
        PaintCanvas.Create();

        // 새로 생성된 RenderTexture의 초기 내용은 Unity가 문서상 보장하지 않으므로,
        // "알파=0이면 미도색"이라는 잠금 로직이 성립하도록 명시적으로 투명하게 지운다 (GameScenePlan.md 11.3)
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
        DetectRoundChange(); // 라운드가 넘어갔는지는 소유자 여부와 무관하게 항상 감지

        if (!pv.IsMine) return;
        if (!IsColorRoundActive()) return;

        // 붓칠 레이캐스트보다 먼저 — 이번 프레임(또는 최근 몇 프레임 내) 포즈를 콜라이더에 반영(§20)
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

        int voteColor = GetCurrentVoteColorIndex();
        if (voteColor < 0) return; // 아직 붓에 담긴 색이 없으면 칠하지 않음

        StampBrush(hit.textureCoord, voteColor);
    }

    // 현재 애니메이션 포즈를 구워 MeshCollider에 반영(Bug-fix-plan.md §20.6 A안).
    // 같은 Mesh 객체의 내용만 갱신해서 재대입하면 PhysX가 변경을 인식하지 못하는 경우가 있어,
    // sharedMesh를 매번 null로 비웠다가 다시 대입해 강제로 재계산(cook)시킨다 — Play Mode
    // 실측으로 이렇게 해야 실제로 반영됨을 확인했다(§20.6-5/§20.8-3).
    //
    // BakeMesh(mesh, useScale:false)의 결과 정점은 렌더러 자신의 Transform.localScale(Cookie
    // 캐릭터의 경우 100배 — 블렌더 재수출 과정에서 생긴 보정용 스케일, §25.1)이 적용되지 않은
    // "축소된" 좌표계다. MeshCollider는 자신이 속한 GameObject의 Transform(이 100배 스케일 포함)을
    // 그대로 다시 적용하므로, 아무 보정 없이 그대로 대입하면 스케일이 이중으로 적용돼 콜라이더가
    // 실제 몸보다 약 100배 커져버린다(Play Mode 실측으로 확인). 그래서 굽고 난 뒤 localScale의
    // 역수를 미리 곱해 상쇄해둔다 — GameObject Transform이 다시 곱해지면 정확히 1배로 돌아온다.
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

private bool IsColorRoundActive()
    {
        int roundIndex = RoomState.GetRoundIndex();
        return roundIndex >= 0 && roundIndex < 4;
    }



    private int GetCurrentVoteColorIndex()
    {
        return PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(NetKeys.VoteColorIndex, out object v)
            ? (int)v
            : -1;
    }

    // 로컬에 스탬프를 찍고(잠금 존중), 이번 라운드 기록에 남기고, 동일한 스트로크를 다른 클라이언트에도 전파
    private void StampBrush(Vector2 uv, int colorIndex)
    {
        ApplyStamp(brushStampMaterial, uv, currentBrushRadius, colorIndex);
        currentRoundStrokes.Add(uv); // 이 라운드가 확정되면 이 자리들을 확정색으로 재도색

        SendStrokeEvent(uv, currentBrushRadius, colorIndex, force: false);
    }

    // 라운드가 막 넘어갔는지 감지해서, 방금 끝난 라운드에 칠했던 자리를 확정색으로 재도색
private void DetectRoundChange()
    {
        int roundIndex = RoomState.GetRoundIndex();
        if (roundIndex == trackedRoundIndex) return;

        int justResolvedRound = trackedRoundIndex; // 재도색 대상은 "방금까지 진행 중이던" 라운드
        trackedRoundIndex = roundIndex;

        if (pv.IsMine && justResolvedRound >= 0 && justResolvedRound < 4 && currentRoundStrokes.Count > 0)
        {
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            if (props.TryGetValue(NetKeys.ColorPrefix + justResolvedRound, out object colorObj))
            {
                int confirmedColor = (int)colorObj;
                FinalizeCurrentRoundStrokes(confirmedColor);
            }
        }

        currentRoundStrokes.Clear();
    }

    // 이번 라운드에 칠했던 자리들을 확정색으로 강제 재도색 (잠금 무시) + 전파
    private void FinalizeCurrentRoundStrokes(int confirmedColorIndex)
    {
        foreach (Vector2 uv in currentRoundStrokes)
        {
            ApplyStamp(finalizeStampMaterial, uv, currentBrushRadius, confirmedColorIndex);
            SendStrokeEvent(uv, currentBrushRadius, confirmedColorIndex, force: true);
        }
    }

    private void SendStrokeEvent(Vector2 uv, float radius, int colorIndex, bool force)
    {
        if (!PhotonNetwork.InRoom) return;

        object[] content = { pv.ViewID, uv.x, uv.y, radius, colorIndex, force };
        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(NetEventCodes.PaintStroke, content, options, SendOptions.SendReliable);
    }

    // 다른 클라이언트가 보낸 스트로크(또는 라운드 확정 재도색)를 수신해 재생
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.PaintStroke) return;

        object[] data = (object[])photonEvent.CustomData;
        int viewId = (int)data[0];
        if (pv == null || viewId != pv.ViewID) return; // 내 캐릭터에 대한 스트로크가 아니면 무시

        Vector2 uv = new Vector2((float)data[1], (float)data[2]);
        float radius = (float)data[3];
        int colorIndex = (int)data[4];
        bool force = (bool)data[5];

        Material material = force ? finalizeStampMaterial : brushStampMaterial;
        ApplyStamp(material, uv, radius, colorIndex); // 송신 측이 이미 판단을 끝냈으므로 그대로 재생만 함
    }

    // uv 위치를 중심으로 radius 반경의 원형 스탬프를 paintCanvas에 그림 (로컬/원격 공용)
private void ApplyStamp(Material stampMaterial, Vector2 uv, float radius, int colorIndex)
    {
        stampMaterial.SetVector("_StampUV", uv);
        stampMaterial.SetFloat("_StampRadius", radius);
        stampMaterial.SetColor("_StampColor", palette.GetColor(colorIndex));

        RenderTexture temp = RenderTexture.GetTemporary(PaintCanvas.width, PaintCanvas.height, 0, PaintCanvas.format);
        Graphics.Blit(PaintCanvas, temp);                // 기존 캔버스(+알파 마스크)를 임시 버퍼로 복사
        Graphics.Blit(temp, PaintCanvas, stampMaterial);  // brushStampMaterial=잠금 존중, finalizeStampMaterial=항상 덮어씀
        RenderTexture.ReleaseTemporary(temp);
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
