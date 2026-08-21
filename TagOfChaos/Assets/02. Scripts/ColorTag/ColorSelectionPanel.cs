using Photon.Pun;
using TMPro;
using UnityEngine;

// 색 슬롯 패널 UI 총괄: 남은 시간 표시 + 등록된 색 슬롯 수 표시(최대 4개). 구 ColorSelectionPanel
// (4라운드 색상 미니게임 전용)을 자유 색칠 방식으로 재작성했다(GameRule.md §3, ColorSlotPanel로
// 명칭 대체될 예정이지만 클래스 자체는 최소 변경 원칙으로 유지).
public class ColorSelectionPanel : MonoBehaviourPunCallbacks
{
    [SerializeField] private TextMeshProUGUI timeLabel;
    [SerializeField] private TextMeshProUGUI slotCountLabel;

    private PlayerPaintCanvas localPaintCanvas;

    private void Update()
    {
        bool isPaintPhaseActive = RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double endTime) && PhotonNetwork.Time < endTime;
        gameObject.SetActive(isPaintPhaseActive);
        if (!isPaintPhaseActive) return;

        if (timeLabel != null)
        {
            double remaining = System.Math.Max(0, endTime - PhotonNetwork.Time);
            timeLabel.text = Mathf.CeilToInt((float)remaining).ToString();
        }

        if (localPaintCanvas == null || !localPaintCanvas.IsMine)
            localPaintCanvas = FindLocalPaintCanvas();

        if (localPaintCanvas != null && slotCountLabel != null)
            slotCountLabel.text = $"{localPaintCanvas.RegisteredColorSlots.Count} / 4";
    }

    private PlayerPaintCanvas FindLocalPaintCanvas()
    {
        var all = FindObjectsByType<PlayerPaintCanvas>(FindObjectsSortMode.None);
        foreach (var canvas in all)
        {
            if (canvas.IsMine) return canvas;
        }
        return null;
    }
}
