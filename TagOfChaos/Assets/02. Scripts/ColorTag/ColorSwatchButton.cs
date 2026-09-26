using UnityEngine;
using UnityEngine.UI;

// 색 스와치 버튼 — 클릭 시 로컬 플레이어의 브러시 색을 바꾼다(GameRule.md §3.3).
// paintCanvas는 씬 배치 시점엔 아직 존재하지 않는 런타임 인스턴스(PhotonNetwork.Instantiate로
// 늦게 생성됨)라 Inspector로 미리 연결할 수 없다 — 클릭 시점에 PlayerPaintCanvas.Local을 참조한다.
// 색 번호와 아이콘 색은 ColorSwatchGroup이 팔레트에서 설정한다(research.md §12 E4).
public class ColorSwatchButton : MonoBehaviour
{
    [SerializeField] private int colorIndex;
    [SerializeField] private Image icon;
    [SerializeField] private Button button;

    private void Awake()
    {
        button.onClick.AddListener(OnClicked);
    }

    public void Setup(int index, Color color)
    {
        colorIndex = index;
        if (icon != null) icon.color = color;
    }

    private void OnClicked()
    {
        PlayerPaintCanvas canvas = PlayerPaintCanvas.Local;
        if (canvas != null) canvas.SetBrushColor(colorIndex);
    }
}
