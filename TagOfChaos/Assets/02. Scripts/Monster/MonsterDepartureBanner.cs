using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;

// GameScene에 1개, 전원 대상 — 괴물 전원 이탈 시 5초 경고 배너(GameRule.md §7.1).
//
// bannerRoot가 이 컴포넌트 자신의 GameObject이므로 SetActive(false)로 숨기면 Photon 콜백 등록이
// 풀려 MonsterDepartedAt을 받지 못했다(Bug-fix-plan.md §23.7) — CanvasGroup으로만 숨긴다.
public class MonsterDepartureBanner : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private TMP_Text bannerText;
    [SerializeField] private string departureMessage = "The monster left. Returning to the lobby in {0} seconds."; // {0}=GameSettings 복귀 지연(초). 인스펙터에서 입력(코드에 한글 금지)

    private CanvasGroup bannerGroup;

    private void Awake()
    {
        bannerGroup = CanvasGroupVisibility.Ensure(bannerRoot);
        CanvasGroupVisibility.Set(bannerGroup, false);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.MonsterDepartedAt)) return;
        if (changedProps[NetKeys.MonsterDepartedAt] == null) return; // 복귀 직전 키 삭제(null) 통지는 무시

        if (bannerText != null) bannerText.text = string.Format(departureMessage, Mathf.CeilToInt(GameSettings.Current.MonsterDepartureReturnDelay));
        CanvasGroupVisibility.Set(bannerGroup, true);
    }
}
