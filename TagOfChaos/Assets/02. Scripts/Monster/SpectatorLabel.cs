using TMPro;
using UnityEngine;

// GameScene 배치 — 관전 중인 대상의 닉네임을 표시한다(SpectatorController.SpectateTargetChanged 구독).
// 관전 대상이 없으면 CanvasGroup으로 숨긴다(자기 GameObject를 끄지 않음, §23.2.1과 같은 이유).
public class SpectatorLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private string format = "Spectating: {0} (Space: next)"; // {0}=닉네임 — 인스펙터에서 입력(코드에 한글 금지)

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = CanvasGroupVisibility.Ensure(gameObject);
        CanvasGroupVisibility.Set(canvasGroup, false);
    }

    private void OnEnable()
    {
        SpectatorController.SpectateTargetChanged += HandleTargetChanged;
    }

    private void OnDisable()
    {
        SpectatorController.SpectateTargetChanged -= HandleTargetChanged;
    }

    private void HandleTargetChanged(string targetName)
    {
        bool visible = !string.IsNullOrEmpty(targetName);
        CanvasGroupVisibility.Set(canvasGroup, visible);
        if (visible && labelText != null) labelText.text = string.Format(format, targetName);
    }
}
