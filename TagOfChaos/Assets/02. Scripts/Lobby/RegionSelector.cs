using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Photon 지역 코드(EscapeVisualPlan.md §1.1). 방은 한 지역 서버 안에만 있으므로, 같은 지역에 접속한 사람끼리만 방이 보인다.
public static class PhotonRegions
{
    public static readonly string[] Codes = { "kr", "jp", "asia", "us", "eu" };
    public const string Default = "kr";
    private const string PrefsKey = "TOC_Region";

    // PhotonNetwork.CloudRegion은 "kr/*"처럼 오므로 앞부분만 쓴다.
    public static string Normalize(string cloudRegion)
    {
        if (string.IsNullOrEmpty(cloudRegion)) return null;
        int slash = cloudRegion.IndexOf('/');
        return (slash >= 0 ? cloudRegion.Substring(0, slash) : cloudRegion).ToLowerInvariant();
    }

    public static int IndexOf(string code)
    {
        for (int i = 0; i < Codes.Length; i++) if (Codes[i] == code) return i;
        return -1;
    }

    public static string Saved
    {
        get
        {
            string code = PlayerPrefs.GetString(PrefsKey, Default);
            return IndexOf(code) >= 0 ? code : Default;
        }
        set
        {
            if (IndexOf(value) < 0) return;
            PlayerPrefs.SetString(PrefsKey, value);
            PlayerPrefs.Save();
        }
    }
}

// 지역 고르기 한 줄: ◀ 지역 이름 ▶ (로비의 "볼 지역", 방 만들기의 "방 지역"). 이름은 인스펙터에서 입력한다(코드에 한글 금지).
public class RegionSelector : MonoBehaviour
{
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text valueText;
    [Tooltip("PhotonRegions.Codes와 같은 순서(kr, jp, asia, us, eu)")]
    [SerializeField] private string[] displayNames = { "Korea", "Japan", "Asia", "USA East", "Europe" };

    private int index = -1;

    public event Action<string> Changed;

    public string Code => PhotonRegions.Codes[Mathf.Max(0, index)];

    private void Awake()
    {
        if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(+1));
        if (index < 0) SetCode(PhotonRegions.Default);
    }

    public void SetCode(string code)
    {
        int i = PhotonRegions.IndexOf(code);
        index = i >= 0 ? i : 0;
        Refresh();
    }

    public void Step(int delta)
    {
        int n = PhotonRegions.Codes.Length;
        index = ((Mathf.Max(0, index) + delta) % n + n) % n;
        Refresh();
        Changed?.Invoke(Code);
    }

    public void SetInteractable(bool value)
    {
        if (previousButton != null && previousButton.interactable != value) previousButton.interactable = value;
        if (nextButton != null && nextButton.interactable != value) nextButton.interactable = value;
    }

    private void Refresh()
    {
        if (valueText == null) return;
        int i = Mathf.Max(0, index);
        valueText.text = displayNames != null && i < displayNames.Length ? displayNames[i] : PhotonRegions.Codes[i];
    }
}
