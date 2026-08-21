using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// GameScene 진입 시 마스터가 60초 자유 색칠 페이즈를 시작한다(GameRule.md §3) — 이전 세대
// 시스템(research.md §4.1)이 갖고 있던 "GameScene에 시작 트리거가 없다"는 구조적 공백을
// 정확히 메우는 역할. GameScene에 1개 배치.
public class GamePhaseStarter : MonoBehaviourPunCallbacks
{
    private const float PaintPhaseDuration = 60f;
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
            { NetKeys.PaintPhaseEndTime, PhotonNetwork.Time + PaintPhaseDuration },
        });
    }
}
