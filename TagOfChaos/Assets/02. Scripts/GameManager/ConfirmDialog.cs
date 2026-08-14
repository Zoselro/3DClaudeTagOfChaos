using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 재사용 가능한 예/아니오 확인창. 특정 기능(나가기 등)에 종속되지 않도록 콜백을 인자로 받는다.
public class ConfirmDialog : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private Action onYesConfirmed;

    private void Awake()
    {
        yesButton.onClick.AddListener(OnYesClicked);
        noButton.onClick.AddListener(Hide);
        gameObject.SetActive(false); // 평소에는 숨겨둠
    }

    public void Show(string message, Action onYes)
    {
        messageText.text = message;
        onYesConfirmed = onYes;
        gameObject.SetActive(true);
    }

    private void OnYesClicked()
    {
        Hide();
        onYesConfirmed?.Invoke();
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}
