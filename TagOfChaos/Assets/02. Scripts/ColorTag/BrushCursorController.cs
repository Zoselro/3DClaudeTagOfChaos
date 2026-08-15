using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 색상 라운드 중 로컬 플레이어의 마우스 위치를 3D 붓 모델(Brush.fbx 기반 프리팹)로 표시한다.
// OS 하드웨어 커서는 캐릭터 표면 위에 있을 때만 숨기고, 스와치 UI 등을 조작할 때는 그대로 보여준다
// (GameScenePlan.md 12장 — 원래는 2D 커서 텍스처 교체 방식이었으나 3D 모델로 대체됨).
public class BrushCursorController : MonoBehaviourPunCallbacks
{
    [SerializeField] private BrushSettingsSO brushSettings;
    [SerializeField] private ColorPaletteSO palette;

    private GameObject cursorInstance;
    private Renderer cursorRenderer;
    private MaterialPropertyBlock propertyBlock;
    private PlayerPaintCanvas localPaintCanvas;
    private Camera targetCamera;
    private bool isColorRoundActive;
    private int paintRaycastMask;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        Cursor.visible = true;
        // 캐릭터 자신의 물리용 CapsuleCollider가 붓칠 대상인 Ch36을 가려 레이캐스트가 항상 캡슐에
        // 먼저 맞는 문제를 막기 위해, 붓 관련 레이캐스트에서는 PlayerCapsule 레이어를 제외한다
        // (Bug-fix-plan.md §17).
        paintRaycastMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("PlayerCapsule");
    }



public override void OnDisable()
    {
        base.OnDisable();
        Cursor.visible = true;
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.RoundIndex)) return;
        ApplyCursorForRound((int)changedProps[NetKeys.RoundIndex]);
    }

    private void ApplyCursorForRound(int roundIndex)
    {
        isColorRoundActive = roundIndex >= 0 && roundIndex < 4;

        if (isColorRoundActive)
        {
            EnsureCursorInstance();
        }
        else
        {
            if (cursorInstance != null) cursorInstance.SetActive(false);
            Cursor.visible = true;
        }
    }

    private void EnsureCursorInstance()
    {
        if (cursorInstance != null) return;
        if (brushSettings == null || brushSettings.CursorPrefab == null) return;

        cursorInstance = Instantiate(brushSettings.CursorPrefab);
        cursorInstance.name = "BrushCursor(Runtime)";
        cursorRenderer = cursorInstance.GetComponentInChildren<Renderer>();
        cursorInstance.SetActive(false); // 실제 표시 여부는 매 프레임 레이캐스트 결과로 결정
    }

    private void Update()
    {
        if (!isColorRoundActive || cursorInstance == null) return;

        if (localPaintCanvas == null || !localPaintCanvas.IsMine)
            localPaintCanvas = FindLocalPaintCanvas();

        if (localPaintCanvas == null)
        {
            cursorInstance.SetActive(false);
            Cursor.visible = true;
            return;
        }

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        bool hitSurface = Physics.Raycast(ray, out RaycastHit hit) && hit.collider == localPaintCanvas.PaintableCollider;

        cursorInstance.SetActive(hitSurface);
        Cursor.visible = !hitSurface; // 캐릭터 표면 위가 아니면(스와치 클릭 등) OS 커서를 그대로 둔다

        if (!hitSurface) return;

        cursorInstance.transform.SetPositionAndRotation(hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));

        float scale = localPaintCanvas.CurrentBrushRadius / brushSettings.DefaultRadius * brushSettings.CursorWorldScale;
        cursorInstance.transform.localScale = Vector3.one * scale;

        UpdateColor();
    }

    // 씬에 하나뿐인 매니저이므로, 로컬 플레이어가 스폰된 후에도 계속 참조를 찾을 수 있도록 매번 재탐색
    private PlayerPaintCanvas FindLocalPaintCanvas()
    {
        var all = FindObjectsByType<PlayerPaintCanvas>(FindObjectsSortMode.None);
        foreach (var canvas in all)
        {
            if (canvas.IsMine) return canvas;
        }
        return null;
    }

    private void UpdateColor()
    {
        if (cursorRenderer == null || palette == null) return;
        if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(NetKeys.VoteColorIndex, out object v)) return;

        int voteColor = (int)v;
        if (voteColor < 0) return;

        cursorRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", palette.GetColor(voteColor));
        cursorRenderer.SetPropertyBlock(propertyBlock);
    }
}
