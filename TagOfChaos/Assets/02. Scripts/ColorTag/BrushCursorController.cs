using Photon.Pun;
using UnityEngine;

// 색칠 페이즈 중 로컬 플레이어의 마우스 위치를 3D 붓 모델로 표시한다. OS 하드웨어 커서는
// 캐릭터 표면 위에 있을 때만 숨기고, 스와치 UI 등을 조작할 때는 그대로 보여준다.
// (GameRule.md §3 자유 색칠 재작성 — 활성 여부를 PaintPhaseEndTime 기반으로 매 프레임 판정한다.)
public class BrushCursorController : MonoBehaviourPunCallbacks
{
    [SerializeField] private BrushSettingsSO brushSettings;
    [SerializeField] private ColorPaletteSO palette;

    private GameObject cursorInstance;
    private Renderer cursorRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Camera targetCamera;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        Cursor.visible = true;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Cursor.visible = true;
    }

    // 런타임에 만든 붓 커서 인스턴스를 함께 정리한다(research.md §8.19).
    private void OnDestroy()
    {
        if (cursorInstance != null) Destroy(cursorInstance);
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
        if (!IsPaintPhaseActive())
        {
            if (cursorInstance != null) cursorInstance.SetActive(false);
            Cursor.visible = true;
            return;
        }

        EnsureCursorInstance();
        if (cursorInstance == null) return;

        PlayerPaintCanvas localPaintCanvas = PlayerPaintCanvas.Local;
        if (localPaintCanvas == null)
        {
            cursorInstance.SetActive(false);
            Cursor.visible = true;
            return;
        }

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        Ray ray = targetCamera.ScreenPointToRay(PlayerInput.PointerPosition);
        // PlayerPaintCanvas.Update()와 동일하게 내 몸 콜라이더 하나만 검사 — 루트 캡슐 등 다른
        // 콜라이더가 앞을 가려도 커서가 사라지지 않는다(Bug-fix-plan.md §23.2.2).
        Collider paintable = localPaintCanvas.PaintableCollider;
        RaycastHit hit = default;
        bool hitSurface = paintable != null && paintable.Raycast(ray, out hit, PlayerPaintCanvas.MaxPaintRayDistance);

        cursorInstance.SetActive(hitSurface);
        Cursor.visible = !hitSurface; // 캐릭터 표면 위가 아니면(스와치 클릭 등) OS 커서를 그대로 둔다

        if (!hitSurface) return;

        // 콜라이더가 실시간 포즈를 반영하지만, 모델 두께에 따라 표면에 딱 붙어 파고든 것처럼
        // 보일 수 있어 법선 방향으로 살짝 띄운다.
        Vector3 cursorPos = hit.point + hit.normal * brushSettings.CursorSurfaceOffset;
        cursorInstance.transform.SetPositionAndRotation(cursorPos, Quaternion.FromToRotation(Vector3.up, hit.normal));

        float scale = localPaintCanvas.CurrentBrushRadius / brushSettings.DefaultRadius * brushSettings.CursorWorldScale;
        cursorInstance.transform.localScale = Vector3.one * scale;

        UpdateColor(localPaintCanvas);
    }

    private static bool IsPaintPhaseActive() => GamePhaseState.IsPaintActive;

    private void UpdateColor(PlayerPaintCanvas localPaintCanvas)
    {
        if (cursorRenderer == null || palette == null) return;

        int brushColor = localPaintCanvas.CurrentBrushColorIndex;
        if (brushColor < 0) return;

        cursorRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", palette.GetColor(brushColor));
        cursorRenderer.SetPropertyBlock(propertyBlock);
    }
}
