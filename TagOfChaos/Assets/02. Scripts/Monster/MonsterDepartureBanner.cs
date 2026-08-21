using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;

// GameScene에 1개, 전원 대상 — 괴물 전원 이탈 시 5초 경고 배너(GameRule.md §7.1).
public class MonsterDepartureBanner : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private TMP_Text bannerText;

    private void Awake()
    {
        if (bannerRoot != null) bannerRoot.SetActive(false);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.MonsterDepartedAt)) return;
        if (bannerText != null) bannerText.text = "괴물이 나갔습니다. 5초 뒤 게임이 종료됩니다.";
        if (bannerRoot != null) bannerRoot.SetActive(true);
    }
}
