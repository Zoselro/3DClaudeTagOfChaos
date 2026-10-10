using TMPro;
using UnityEngine;

// GameScene 배치 — 관전 중인 대상의 닉네임을 표시한다(SpectatorController.SpectateTargetChanged 구독).
// 자기가 탄 탈것을 보는 동안은 vehicleText(Space로 다른 사람을 볼 수 있다는 안내, Request1009Plan.md §9)를 띄운다.
// 관전 대상이 없으면 CanvasGroup으로 숨긴다(자기 GameObject를 끄지 않음, §23.2.1과 같은 이유).
public class SpectatorLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private string format = "Spectating: {0} (Space: next)"; // {0}=닉네임 — 인스펙터에서 입력(코드에 한글 금지)
    [SerializeField] private string vehicleText = "Watching your ride (Space: watch others)";

    private CanvasGroup canvasGroup;
    private string targetName;
    private bool watchingVehicle;

    private void Awake()
    {
        canvasGroup = CanvasGroupVisibility.Ensure(gameObject);
        CanvasGroupVisibility.Set(canvasGroup, false);
    }

    private void OnEnable()
    {
        SpectatorController.SpectateTargetChanged += HandleTargetChanged;
        SpectatorController.SpectatingVehicleChanged += HandleVehicleChanged;
    }

    private void OnDisable()
    {
        SpectatorController.SpectateTargetChanged -= HandleTargetChanged;
        SpectatorController.SpectatingVehicleChanged -= HandleVehicleChanged;
    }

    private void HandleTargetChanged(string name)
    {
        targetName = name;
        Refresh();
    }

    private void HandleVehicleChanged(bool value)
    {
        watchingVehicle = value;
        Refresh();
    }

    private void Refresh()
    {
        bool hasTarget = !string.IsNullOrEmpty(targetName);
        bool visible = watchingVehicle || hasTarget;
        CanvasGroupVisibility.Set(canvasGroup, visible);
        if (!visible || labelText == null) return;
        labelText.text = watchingVehicle ? vehicleText : string.Format(format, targetName);
    }
}
