using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

// GameLobbyScene 배치 — 괴물이 확정되면 배너로 알린다(GameRule.md §2.2).
// 실제 3D 프리팹 교체(보글보글→짜잔 파티클 연출 포함)는 파티클/모델 에셋이 아직 없어 텍스트 배너로
// 단순화했다 — §10.6의 가마솥 파티클 확보 후 별도로 보강 필요.
//
// 예전에는 OnRoomPropertiesUpdate(= 값이 바뀔 때)에만 표시해, 괴물이 정해진 뒤 입장한 사람은 누가 괴물인지
// 알 수 없었다. 그래서 가마솥에 들어간 사람을 괴물로 착각해 "술래가 GameScene으로 넘어가 색칠한다"로 보였다
// (Bug-fix-plan.md §24.3 ⑰-B). 입장 시점에도 표시하고, 괴물 본인에게는 별도 문구를 보여준다.
public class MonsterRevealController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private TMP_Text bannerText;

    // 표시 문구는 인스펙터에서 입력한다(코드에 한글을 넣지 않는 프로젝트 규칙). {0} = 괴물 닉네임.
    [SerializeField] private string revealFormat = "{0} is the monster!";
    [SerializeField] private string localMonsterText = "You are the monster!";
    [SerializeField] private string unknownMonsterText = "The monster has been chosen!";

    private void Awake()
    {
        if (bannerRoot != null) bannerRoot.SetActive(false);
    }

    private void Start()
    {
        Refresh(); // 이미 괴물이 정해진 방에 늦게 들어온 경우
    }

    public override void OnJoinedRoom()
    {
        Refresh();
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (changedProps.ContainsKey(NetKeys.MonsterActorNumbers)) Refresh();
    }

    // 가마솥에 들어갔는데 이미 괴물이 정해져 있을 때 Cauldron이 호출한다 — 누가 괴물인지 다시 보여준다.
    public void ShowAgain()
    {
        Refresh();
    }

    private void Refresh()
    {
        // 판 초기화(RoundStateResetter)나 괴물 이탈로 목록이 비면 배너를 숨긴다.
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) || monsters.Length == 0)
        {
            if (bannerRoot != null) bannerRoot.SetActive(false);
            return;
        }

        if (bannerText != null)
            bannerText.text = BuildText(monsters);
        if (bannerRoot != null)
            bannerRoot.SetActive(true);
    }

    // 괴물이 여럿이면(GameSettings.MonsterCount) 닉네임을 모두 나열한다.
    private string BuildText(int[] monsters)
    {
        if (RoomState.IsLocalMonster()) return localMonsterText;

        string names = string.Join(", ", monsters
            .Select(a => PhotonNetwork.CurrentRoom.GetPlayer(a))
            .Where(p => p != null)
            .Select(p => string.IsNullOrEmpty(p.NickName) ? "#" + p.ActorNumber : p.NickName));
        return names.Length > 0 ? string.Format(revealFormat, names) : unknownMonsterText;
    }
}
