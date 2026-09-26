using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameScene에 1개, 전원 대상 — 결과 화면 표시/자동 복귀(GameRule.md §8.2).
//
// root가 이 컴포넌트 자신의 GameObject이므로 SetActive(false)로 숨기면 OnDisable에서 Photon 콜백
// 등록이 풀려 GameResult를 영영 받지 못했다(Bug-fix-plan.md §23.7) — CanvasGroup으로만 숨긴다.
public class ResultScreenController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject monsterWinBanner;
    [SerializeField] private GameObject cookieWinBanner;
    [SerializeField] private TMP_Text remainingCountText;
    // 쿠키 생존 아이콘: 쿠키 수만큼 템플릿을 복제해 컨테이너에 채운다(생존=흰색/파괴=회색). 예전에는 4칸 고정
    // 배열이라 인원이 늘면 아이콘이 모자랐다(Bug-fix-plan.md §27 확장성). 템플릿은 비활성 상태로 둔다.
    [SerializeField] private Transform cookieIconContainer;
    [SerializeField] private Image cookieIconTemplate;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerResultRow playerRowPrefab;
    [SerializeField] private TMP_Text lobbyButtonCountdownText;
    [SerializeField] private float autoReturnDelay = 12f;
    [SerializeField] private string lobbyCountdownFormat = "Back to lobby ({0})"; // 인스펙터에서 입력(코드에 한글 금지)

    private CanvasGroup rootGroup;
    private bool isShown;

    private void Awake()
    {
        rootGroup = CanvasGroupVisibility.Ensure(root);
        CanvasGroupVisibility.Set(rootGroup, false);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.GameResult)) return;
        if (!RoomState.TryGetInt(NetKeys.GameResult, out int result)) return;
        ShowResult((GameResult)result);
    }

    private void ShowResult(GameResult result)
    {
        if (isShown) return; // 결과 행/코루틴이 중복 생성되지 않도록 한 번만 표시
        isShown = true;

        CanvasGroupVisibility.Set(rootGroup, true);
        if (monsterWinBanner != null) monsterWinBanner.SetActive(result == GameResult.MonsterWins);
        if (cookieWinBanner != null) cookieWinBanner.SetActive(result == GameResult.CookiesWin);

        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters);
        int aliveCount = 0;
        int cookieCount = 0;

        foreach (Player p in PhotonNetwork.PlayerList.OrderBy(pl => pl.ActorNumber))
        {
            bool isMonster = monsters != null && monsters.Contains(p.ActorNumber);
            bool isBroken = RoomState.IsBroken(p);

            if (playerRowPrefab != null && playerListContent != null)
            {
                var row = Instantiate(playerRowPrefab, playerListContent);
                if (isMonster) row.SetMonster(p.NickName);
                else row.SetCookie(p.NickName, alive: !isBroken);
            }

            if (isMonster) continue;

            AddCookieIcon(isBroken);
            cookieCount++;
            if (!isBroken) aliveCount++;
        }

        // 분모는 실제 쿠키 수(최대 3명) — 예전에는 4로 하드코딩돼 있었다(research.md §8.23).
        if (remainingCountText != null) remainingCountText.text = $"{aliveCount} / {cookieCount}";
        StartCoroutine(AutoReturnCountdown());
    }

    private void AddCookieIcon(bool isBroken)
    {
        if (cookieIconContainer == null || cookieIconTemplate == null) return;
        Image icon = Instantiate(cookieIconTemplate, cookieIconContainer);
        icon.gameObject.SetActive(true);
        icon.color = isBroken ? Color.gray : Color.white;
    }

    private IEnumerator AutoReturnCountdown()
    {
        float remaining = autoReturnDelay;
        while (remaining > 0f)
        {
            if (lobbyButtonCountdownText != null)
                lobbyButtonCountdownText.text = string.Format(lobbyCountdownFormat, Mathf.CeilToInt(remaining));
            remaining -= Time.deltaTime;
            yield return null;
        }
        OnLobbyButtonClicked();
    }

    public void OnLobbyButtonClicked()
    {
        StopAllCoroutines();
        if (PhotonNetwork.IsMasterClient)
            RoomSceneTransition.LoadLevelForRoom(SceneNames.GameLobby); // 방 재개방은 대기실의 RoundStateResetter가 한다(§26.3)
    }
}
