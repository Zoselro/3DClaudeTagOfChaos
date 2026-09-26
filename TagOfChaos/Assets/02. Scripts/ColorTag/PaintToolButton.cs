using UnityEngine;
using UnityEngine.UI;

// 지우개/리셋 버튼 공용 — 로컬 플레이어의 PlayerPaintCanvas(PlayerPaintCanvas.Local)를 호출한다
// (ColorSwatchButton과 동일한 이유, GameRule.md §3.4/§3.5).
public class PaintToolButton : MonoBehaviour
{
    private enum ToolAction { Erase, Reset }

    [SerializeField] private ToolAction action;
    [SerializeField] private Button button;

    private void Awake()
    {
        button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        PlayerPaintCanvas canvas = PlayerPaintCanvas.Local;
        if (canvas == null) return;

        if (action == ToolAction.Erase) canvas.SetEraseMode();
        else canvas.ResetCanvas();
    }
}
