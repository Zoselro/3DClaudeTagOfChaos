using UnityEngine;
using UnityEngine.UI;

// 연출용 검은 화면 덮개(Request1009Plan.md §24.10 — 마녀 도착 테이크 1 시작·밟는 순간). 화면 맨 위 캔버스에 검은 이미지 하나,
// 알파만 바꾼다. 처음 쓸 때 만들고 씬과 함께 사라진다(다음 씬에서 다시 만든다). 클릭을 막지 않는다.
public static class CutsceneFade
{
    private const int SortingOrder = 32000;
    private static Image image;

    public static void Set(float alpha)
    {
        if (alpha <= 0f)
        {
            if (image != null) image.enabled = false;
            return;
        }
        if (image == null) Create();
        image.enabled = true;
        image.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }

    private static void Create()
    {
        var canvasGo = new GameObject("CutsceneFade", typeof(Canvas));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var imageGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
        var rect = imageGo.GetComponent<RectTransform>();
        rect.SetParent(canvasGo.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        image = imageGo.GetComponent<Image>();
        image.raycastTarget = false;
    }
}
