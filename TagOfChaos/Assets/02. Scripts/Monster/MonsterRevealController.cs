using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

using TMPro;
using UnityEngine;

// GameLobbyScene 배치 — 괴물이 확정되는 순간(MonsterActorNumbers) 배너로 알린다(GameRule.md §2.2).
// 실제 3D 프리팹 교체(보글보글→짜잔 파티클 연출 포함)는 파티클/모델 에셋이 아직 없어 이번
// 구현에서는 텍스트 배너로 단순화했다 — §10.6의 가마솥 파티클 확보 후 별도로 보강 필요.
public class MonsterRevealController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private TMP_Text bannerText;

    private void Awake()
    {
        if (bannerRoot != null) bannerRoot.SetActive(false);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.MonsterActorNumbers)) return;
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) || monsters.Length == 0) return;

        Player monsterPlayer = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == monsters[0]);
        if (bannerText != null)
            bannerText.text = monsterPlayer != null ? $"{monsterPlayer.NickName}이(가) 괴물이 되었습니다!" : "괴물이 정해졌습니다!";
        if (bannerRoot != null)
            bannerRoot.SetActive(true);
    }
}
