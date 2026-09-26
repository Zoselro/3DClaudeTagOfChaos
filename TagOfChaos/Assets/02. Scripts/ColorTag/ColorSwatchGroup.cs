using UnityEngine;

// 색 스와치 줄(SwatchRow)에 부착 — 팔레트(ColorPaletteSO)의 색 수만큼 스와치를 맞추고 각 스와치의 색 번호·아이콘 색을
// 팔레트에서 설정한다(research.md §12 E4). 예전에는 스와치 10개의 번호와 색을 씬에 손으로 입력해, 팔레트 색을
// 바꾸거나 늘리면 UI가 따라가지 않았다. 이제 팔레트 에셋만 바꾸면 된다.
public class ColorSwatchGroup : MonoBehaviour
{
    [SerializeField] private ColorPaletteSO palette;
    [SerializeField] private ColorSwatchButton template; // 복제 원본(보통 첫 스와치)

    private void Awake()
    {
        if (palette == null || template == null)
        {
            Debug.LogWarning($"[ColorSwatchGroup] Palette or template is not assigned on {name}.");
            return;
        }

        var swatches = UiListBuilder.Sync(transform, template, palette.Count);
        for (int i = 0; i < swatches.Count; i++)
        {
            swatches[i].Setup(i, palette.GetColor(i));
            swatches[i].name = $"Swatch{i}";
        }
    }
}
