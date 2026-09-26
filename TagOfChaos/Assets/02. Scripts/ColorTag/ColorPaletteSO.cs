using UnityEngine;

[CreateAssetMenu(menuName = "ColorTag/ColorPalette")]
public class ColorPaletteSO : ScriptableObject
{
    [SerializeField] private ColorEntry[] colors; // 10개 고정

    public int Count => colors.Length;
    // 네트워크로 받은 인덱스가 범위를 벗어나도 예외 대신 안전한 값을 돌려준다(research.md §8.22).
    public Color GetColor(int index) => IsValid(index) ? colors[index].color : Color.clear;
    public string GetColorName(int index) => IsValid(index) ? colors[index].colorName : string.Empty;

    private bool IsValid(int index) => colors != null && index >= 0 && index < colors.Length;
}

[System.Serializable]
public struct ColorEntry
{
    public string colorName;
    public Color color;
}
