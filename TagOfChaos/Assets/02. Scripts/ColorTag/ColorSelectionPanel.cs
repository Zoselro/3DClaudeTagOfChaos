using Photon.Pun;
using TMPro;
using UnityEngine;

// 색 슬롯 패널 UI 총괄: 등록된 색 슬롯 수 표시(최대 4개). 남은 시간은 화면 위 PhaseCountdownDisplay(Paint)만 보여 준다
// (예전에는 이 패널 아래에도 같은 시간이 한 번 더 보였고 째깍 소리도 두 번 났다 — AuditFixPlan.md F-12④, 결정 Q4). 구 ColorSelectionPanel
// (4라운드 색상 미니게임 전용)을 자유 색칠 방식으로 재작성했다(GameRule.md §3, ColorSlotPanel로
// 명칭 대체될 예정이지만 클래스 자체는 최소 변경 원칙으로 유지).
//
// 표시/숨김은 CanvasGroup으로만 한다 — 예전처럼 gameObject.SetActive(false)로 자기 자신을 끄면
// 첫 프레임(PaintPhaseEndTime이 아직 서버에서 도착하기 전)에 꺼진 뒤 Update()가 멈춰 다시 켜지지
// 못했고, 자식인 스와치·지우개·리셋 버튼까지 함께 사라졌다(Bug-fix-plan.md §23.2.1).
public class ColorSelectionPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI slotCountLabel;
    [SerializeField] private CanvasGroup canvasGroup; // 비어 있으면 Awake에서 같은 오브젝트에 확보

    private bool? lastVisible; // 상태가 바뀔 때만 CanvasGroup을 갱신
    private int lastSlotCount = -1;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = CanvasGroupVisibility.Ensure(gameObject);
    }

    private void Update()
    {
        bool isPaintPhaseActive = GamePhaseState.IsPaintActive; // 색칠 단계는 없앴다(Request1009Plan.md §25) — 늘 꺼짐
        SetVisible(isPaintPhaseActive);
        if (!isPaintPhaseActive) return;

        PlayerPaintCanvas localPaintCanvas = PlayerPaintCanvas.Local;
        if (localPaintCanvas == null || slotCountLabel == null) return;
        int count = localPaintCanvas.RegisteredColorSlots.Count;
        if (count == lastSlotCount) return; // 수가 바뀔 때만 문자열을 만든다
        lastSlotCount = count;
        slotCountLabel.text = $"{count} / {GameSettings.Current.MaxColorSlots}";
    }

    private void SetVisible(bool visible)
    {
        if (lastVisible == visible) return;
        lastVisible = visible;
        CanvasGroupVisibility.Set(canvasGroup, visible);
    }
}
