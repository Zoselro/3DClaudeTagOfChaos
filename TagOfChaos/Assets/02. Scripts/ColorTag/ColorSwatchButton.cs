using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class ColorSwatchButton : MonoBehaviour
{
    [SerializeField] private int colorIndex;
    [SerializeField] private Image icon;
    [SerializeField] private Button button;
    [SerializeField] private ColorSelectionManager manager;

    private void Awake()
    {
        button.onClick.AddListener(() => manager.SubmitVote(colorIndex));
    }

    // 라운드 시작 시 매니저가 호출: 이미 확정된 색이면 버튼을 잠금
    public void SetLocked(bool locked)
    {
        button.interactable = !locked;
    }
}
