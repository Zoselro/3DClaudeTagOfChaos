using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameScene에 1개, 전원 대상 — 결과 화면 표시/자동 복귀(GameRule.md §8.2).
public class ResultScreenController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject monsterWinBanner;
    [SerializeField] private GameObject cookieWinBanner;
    [SerializeField] private TMP_Text remainingCountText;
    [SerializeField] private Image[] cookieIcons; // 4개, 생존=컬러/파괴=그레이스케일
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerResultRow playerRowPrefab;
    [SerializeField] private TMP_Text lobbyButtonCountdownText;
    [SerializeField] private float autoReturnDelay = 12f;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.GameResult)) return;
        if (!RoomState.TryGetInt(NetKeys.GameResult, out int result)) return;
        ShowResult((GameResult)result);
    }

    private void ShowResult(GameResult result)
    {
        if (root != null) root.SetActive(true);
        if (monsterWinBanner != null) monsterWinBanner.SetActive(result == GameResult.MonsterWins);
        if (cookieWinBanner != null) cookieWinBanner.SetActive(result == GameResult.CookiesWin);

        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters);
        int aliveCount = 0;
        int cookieIndex = 0;

        foreach (Player p in PhotonNetwork.PlayerList.OrderBy(pl => pl.ActorNumber))
        {
            bool isMonster = monsters != null && monsters.Contains(p.ActorNumber);
            int hitCount = p.CustomProperties.TryGetValue(NetKeys.HitCount, out object v) ? (int)v : 0;
            bool isBroken = hitCount >= 2;

            if (playerRowPrefab != null && playerListContent != null)
            {
                var row = Instantiate(playerRowPrefab, playerListContent);
                if (isMonster) row.SetMonster(p.NickName);
                else row.SetCookie(p.NickName, alive: !isBroken);
            }

            if (isMonster) continue;

            if (cookieIndex < (cookieIcons?.Length ?? 0))
                cookieIcons[cookieIndex].color = isBroken ? Color.gray : Color.white;
            cookieIndex++;
            if (!isBroken) aliveCount++;
        }

        if (remainingCountText != null) remainingCountText.text = $"{aliveCount} / 4";
        StartCoroutine(AutoReturnCountdown());
    }

    private IEnumerator AutoReturnCountdown()
    {
        float remaining = autoReturnDelay;
        while (remaining > 0f)
        {
            if (lobbyButtonCountdownText != null)
                lobbyButtonCountdownText.text = $"로비로 이동 ({Mathf.CeilToInt(remaining)})";
            remaining -= Time.deltaTime;
            yield return null;
        }
        OnLobbyButtonClicked();
    }

    public void OnLobbyButtonClicked()
    {
        StopAllCoroutines();
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel(SceneNames.GameLobby);
    }
}
