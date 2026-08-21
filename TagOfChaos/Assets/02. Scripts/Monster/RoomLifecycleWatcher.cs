using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

// 다중 괴물 이탈 감지 + 5초 경고(GameRule.md §7.1). 남은 괴물이 0명일 때만 5초 경고 후
// GameLobbyScene으로 복귀한다. ColorTag/RoomLifecycleWatcher.cs(4라운드 색상 미니게임 전용)를
// 대체하는 Monster/ 도메인 재작성판이다.
public class RoomLifecycleWatcher : MonoBehaviourPunCallbacks
{
    private double? monstersGoneAt;

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (monstersGoneAt.HasValue) return;
        if (!PhotonNetwork.IsMasterClient) return;
        if (!IsMonster(otherPlayer)) return; // 쿠키가 나간 건 이 경로와 무관(쿠키는 파괴되면 관전만 함, 나가도 게임 계속)

        RemoveFromMonsterList(otherPlayer.ActorNumber);

        if (RemainingMonsterCount() > 0) return;

        monstersGoneAt = PhotonNetwork.Time;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.MonsterDepartedAt, monstersGoneAt.Value },
        });
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!monstersGoneAt.HasValue) return;
        if (PhotonNetwork.Time < monstersGoneAt.Value + 5.0) return;

        monstersGoneAt = null;
        ReturnToGameLobby();
    }

    // 방은 나가지 않고, 같은 방을 그대로 유지한 채 대기실로 되돌아간다.
    private void ReturnToGameLobby()
    {
        var props = new Hashtable
        {
            { NetKeys.MonsterDepartedAt, null },
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        PhotonNetwork.CurrentRoom.IsOpen = true; // 게임 시작 시 닫아뒀던 걸 다시 연다

        PhotonNetwork.LoadLevel(SceneNames.GameLobby); // AutomaticallySyncScene으로 전원 함께 이동
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene(SceneNames.Lobby);
    }

    private bool IsMonster(Player player) =>
        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) && monsters.Contains(player.ActorNumber);

    private int RemainingMonsterCount() =>
        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) ? monsters.Length : 0;

    private void RemoveFromMonsterList(int actorNumber)
    {
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        int[] updated = monsters.Where(a => a != actorNumber).ToArray();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.MonsterActorNumbers, updated } });
    }
}
