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

private void Start()
    {
        localCamera = Camera.main;
        currentBrushRadius = Mathf.Clamp(brushSettings.DefaultRadius, brushSettings.MinRadius, brushSettings.MaxRadius);
        // 캐릭터 자신의 물리용 CapsuleCollider가 붓칠 대상인 Ch36을 가려 레이캐스트가 항상 캡슐에
        // 먼저 맞는 문제를 막기 위해, 붓칠 레이캐스트에서는 PlayerCapsule 레이어를 제외한다
        // (Bug-fix-plan.md §17).
        paintRaycastMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("PlayerCapsule");
        InitPaintCanvas();
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
    }
}
