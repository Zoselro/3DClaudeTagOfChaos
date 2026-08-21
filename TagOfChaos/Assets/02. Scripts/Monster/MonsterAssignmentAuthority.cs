using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

using UnityEngine;

// 마스터 전용 — 가마솥 선착순 신청 확정 + 30초 무입장 타임아웃 시 랜덤 배정(GameRule.md §2.1,
// 타임아웃 30초는 사용자 확인값). ColorSelectionManager/RoomLifecycleWatcher가 이미 쓰는
// "마스터만 Update()에서 만료 시각 폴링" 패턴 그대로다.
public class MonsterAssignmentAuthority : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private float monsterSelectTimeout = 30f;
    private double sceneEnterTime;

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient) sceneEnterTime = PhotonNetwork.Time;
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.ClaimMonster) return;
        if (!PhotonNetwork.IsMasterClient) return;
        if (HasMonsterAssigned()) return; // 이미 확정 — 이후 요청/타임아웃 전부 무시

        int claimantActorNumber = (int)photonEvent.CustomData;
        ConfirmMonster(new[] { claimantActorNumber });
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (HasMonsterAssigned()) return;
        if (PhotonNetwork.Time < sceneEnterTime + monsterSelectTimeout) return;

        var players = PhotonNetwork.PlayerList;
        if (players.Length == 0) return;

        int randomActorNumber = players[new System.Random().Next(players.Length)].ActorNumber;
        ConfirmMonster(new[] { randomActorNumber });
    }

    private bool HasMonsterAssigned() =>
        PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(NetKeys.MonsterActorNumbers);

    private void ConfirmMonster(int[] monsterActorNumbers)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.MonsterActorNumbers, monsterActorNumbers },
            { NetKeys.MonsterRevealTime, PhotonNetwork.Time },
        });
    }
}
