using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 맵 진입 시 마스터가 준비 시간(Request1009Plan.md §25.5 C — 예전 60초 색칠 자리)을 시작한다. 맵 씬에 1개 배치.
// 정상 흐름에서는 GameLobbyController가 시작 버튼 시점에 PrepareEndTime을 이미 기록하므로
// (괴물이 대기실에서 같은 종료 시각으로 카운트다운해야 하기 때문, Bug-fix-plan.md §23.3.4-②) 여기서는
// 아무 일도 하지 않는다 — 대기실을 거치지 않는 경로(테스트 씬 등)를 위한 대비책으로만 남긴다.
public class GamePhaseStarter : MonoBehaviour
{
    private bool started;

    private void Update()
    {
        if (started) return;
        if (!PhotonNetwork.IsMasterClient) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;

        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(NetKeys.PrepareEndTime))
        {
            started = true; // 이미 세팅됨(늦게 합류한 마스터 등) — 다시 쓰지 않음
            return;
        }

        // 방장의 씬이 먼저 열려도 시작 버튼이 쓴 키가 아직 도착하지 않았을 수 있다(F-09). 씬에 들어온 뒤 SceneLoadGrace 동안
        // 기다렸는데도 없을 때만 쓴다 — 정상 흐름(대기실 경유)에서는 쓰지 않게 된다.
        if (Time.timeSinceLevelLoad < GameSettings.Current.SceneLoadGrace) return;

        started = true;
        AuthorityProps.SetRoom(new Hashtable
        {
            { NetKeys.PrepareEndTime, PhotonNetwork.Time + GameSettings.Current.PrepareSeconds },
        });
    }
}
