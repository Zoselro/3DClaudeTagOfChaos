using UnityEngine;

[CreateAssetMenu(menuName = "ColorTag/ColorPalette")]
public class ColorPaletteSO : ScriptableObject
{
    [SerializeField] private ColorEntry[] colors; // 10개 고정

    public int Count => colors.Length;
    public Color GetColor(int index) => colors[index].color;
    public string GetColorName(int index) => colors[index].colorName;
}

[System.Serializable]
public struct ColorEntry
{
    public string colorName;
    public Color color;
}
