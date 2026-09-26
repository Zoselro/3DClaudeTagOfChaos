using Photon.Pun;
using TMPro;
using UnityEngine;

// 색 슬롯 패널 UI 총괄: 남은 시간 표시 + 등록된 색 슬롯 수 표시(최대 4개). 구 ColorSelectionPanel
// (4라운드 색상 미니게임 전용)을 자유 색칠 방식으로 재작성했다(GameRule.md §3, ColorSlotPanel로
// 명칭 대체될 예정이지만 클래스 자체는 최소 변경 원칙으로 유지).
//
// 표시/숨김은 CanvasGroup으로만 한다 — 예전처럼 gameObject.SetActive(false)로 자기 자신을 끄면
// 첫 프레임(PaintPhaseEndTime이 아직 서버에서 도착하기 전)에 꺼진 뒤 Update()가 멈춰 다시 켜지지
// 못했고, 자식인 스와치·지우개·리셋 버튼까지 함께 사라졌다(Bug-fix-plan.md §23.2.1).
public class ColorSelectionPanel : MonoBehaviourPunCallbacks
{
    [SerializeField] private TextMeshProUGUI timeLabel;
    [SerializeField] private TextMeshProUGUI slotCountLabel;
    [SerializeField] private CanvasGroup canvasGroup; // 비어 있으면 Awake에서 같은 오브젝트에 확보

    private bool? lastVisible; // 상태가 바뀔 때만 CanvasGroup을 갱신

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = CanvasGroupVisibility.Ensure(gameObject);
    }

    private void Update()
    {
        bool isPaintPhaseActive = GamePhaseState.TryGetActiveEndTime(GamePhase.Paint, out double endTime) && PhotonNetwork.Time < endTime;
        SetVisible(isPaintPhaseActive);
        if (!isPaintPhaseActive) return;

        if (timeLabel != null)
        {
            double remaining = System.Math.Max(0, endTime - PhotonNetwork.Time);
            timeLabel.text = Mathf.CeilToInt((float)remaining).ToString();
        }

        PlayerPaintCanvas localPaintCanvas = PlayerPaintCanvas.Local;
        if (localPaintCanvas != null && slotCountLabel != null)
            slotCountLabel.text = $"{localPaintCanvas.RegisteredColorSlots.Count} / {GameSettings.Current.MaxColorSlots}";
    }

    private void SetVisible(bool visible)
    {
        if (lastVisible == visible) return;
        lastVisible = visible;
        CanvasGroupVisibility.Set(canvasGroup, visible);
    }
}
