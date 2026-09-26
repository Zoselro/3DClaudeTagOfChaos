using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// GameScene 진입 시 마스터가 60초 자유 색칠 페이즈를 시작한다(GameRule.md §3). GameScene에 1개 배치.
// 정상 흐름에서는 GameLobbyController가 시작 버튼 시점에 PaintPhaseEndTime을 이미 기록하므로
// (괴물이 대기실에서 같은 종료 시각으로 카운트다운해야 하기 때문, Bug-fix-plan.md §23.3.4-②) 여기서는
// 아무 일도 하지 않는다 — 대기실을 거치지 않는 경로(테스트 씬 등)를 위한 대비책으로만 남긴다.
public class GamePhaseStarter : MonoBehaviourPunCallbacks
{
    private bool started;

    private void Update()
    {
        if (started) return;
        if (!PhotonNetwork.IsMasterClient) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;

        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(NetKeys.PaintPhaseEndTime))
        {
            started = true; // 이미 세팅됨(늦게 합류한 마스터 등) — 다시 쓰지 않음
            return;
        }

        started = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.PaintPhaseEndTime, PhotonNetwork.Time + GameSettings.Current.PaintPhaseDuration },
        });
    }
}
