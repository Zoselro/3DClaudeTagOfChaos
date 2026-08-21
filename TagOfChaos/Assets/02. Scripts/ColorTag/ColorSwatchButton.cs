using UnityEngine;
using UnityEngine.UI;

// 색 스와치 버튼 — 클릭 시 로컬 플레이어의 브러시 색을 바꾼다(GameRule.md §3.3).
// paintCanvas는 씬 배치 시점엔 아직 존재하지 않는 런타임 인스턴스(PhotonNetwork.Instantiate로
// 늦게 생성됨)라 Inspector로 미리 연결할 수 없다 — BrushCursorController와 동일한 방식으로
// 클릭 시점에 매번 로컬 플레이어를 재탐색한다.
public class ColorSwatchButton : MonoBehaviour
{
    [SerializeField] private int colorIndex;
    [SerializeField] private Image icon;
    [SerializeField] private Button button;

    private void Awake()
    {
        button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        var canvas = FindLocalPaintCanvas();
        canvas?.SetBrushColor(colorIndex);
    }

    private PlayerPaintCanvas FindLocalPaintCanvas()
    {
        var all = FindObjectsByType<PlayerPaintCanvas>(FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c.IsMine) return c;
        }
        return null;
    }

    public void SetLocked(bool locked)
    {
        button.interactable = !locked;
    }
}
