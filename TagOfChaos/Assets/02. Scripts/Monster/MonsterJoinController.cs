using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

// 색칠 페이즈(PaintPhaseEndTime)가 끝나면 괴물이 GameScene에 합류한다(GameRule.md §6.4).
// 합류 시각을 기준으로 GameEndTime(GameSettings.SurvivalDuration 후)을 마스터가 세팅하고, 괴물 소유 클라이언트 각자가
// 자기 몫의 MonsterPlayer를 로컬로 스폰한다(PlayerSpawner.cs와 동일한 InRoom 대기 관례는
// 이미 색칠 페이즈 진입 시점에 보장돼 있으므로 별도 대기 불필요).
public class MonsterJoinController : MonoBehaviourPunCallbacks
{
    private const string MonsterSpawnPointName = SceneSpawnPoints.Monster;
    private const string MonsterPrefabName = "MonsterPlayer";

    // PUN 내부의 "현재 씬" Room Prop 키(PhotonNetworkPart.CurrentSceneProperty, internal이라 직접 참조 불가).
    // 괴물 클라이언트의 씬 자동 동기화를 되돌려도 되는 시점을 판단하는 데만 쓴다.
    private const string PunSceneSyncKey = "curScn";

    private bool hasSpawnedLocally;

    private void Update()
    {
        if (PhotonNetwork.IsMasterClient) MasterTick();
        if (!hasSpawnedLocally) TryLocalSpawn();
        RestoreSceneSyncWhenCaughtUp();
    }

    // 괴물은 대기실에서 AutomaticallySyncScene을 끄고 혼자 이 씬으로 왔다(MonsterLobbyWaitController).
    // 캐시의 curScn이 아직 이전 값(GameLobbyScene)인 상태에서 true로 되돌리면 PUN이 즉시 그 씬으로 다시
    // 끌고 가므로(setter가 LoadLevelIfSynced를 바로 호출), 쌓여 있던 Props 변경이 재생돼 현재 씬과
    // 일치하게 된 뒤에만 복구한다(Bug-fix-plan.md §23.3.4-⑤). 이후 방장의 LoadLevel(GameLobby)에 함께 따라간다.
    private void RestoreSceneSyncWhenCaughtUp()
    {
        if (PhotonNetwork.AutomaticallySyncScene) return;
        if (!RoomState.IsInRoom()) return;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PunSceneSyncKey, out object scene)) return;
        if (!Equals(scene, SceneManager.GetActiveScene().name)) return;

        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.KeepAliveInBackground = NetworkDefaults.KeepAliveInBackgroundSeconds;
    }

    // 처리 여부를 로컬 플래그가 아니라 Room Prop(MonsterJoined)으로 판단한다 — 방장이 바뀐 뒤 새 방장이 합류를
    // 다시 기록해 생존 종료 시각(GameEndTime)이 "지금 + 생존 시간"으로 늘어나던 문제를 막는다(Bug-fix-plan.md
    // §26.8.3 ③). 서버 응답 전 다음 프레임의 중복 요청은 joinRequested로 막는다.
    private bool joinRequested;

    private void MasterTick()
    {
        if (joinRequested || RoomState.TryGetInt(NetKeys.MonsterJoined, out _)) return;
        if (!RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double paintEnd)) return;
        if (PhotonNetwork.Time < paintEnd) return;

        joinRequested = true;
        double joinTime = PhotonNetwork.Time;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.MonsterJoined, 1 },
            { NetKeys.GameEndTime, joinTime + GameSettings.Current.SurvivalDuration },
        });
    }

    private void TryLocalSpawn()
    {
        if (!RoomState.TryGetInt(NetKeys.MonsterJoined, out _)) return;
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        if (PhotonNetwork.LocalPlayer == null || !monsters.Contains(PhotonNetwork.LocalPlayer.ActorNumber)) return;

        hasSpawnedLocally = true;

        GameObject spawnPointObj = GameObject.Find(MonsterSpawnPointName);
        if (spawnPointObj == null)
        {
            Debug.LogWarning($"[MonsterJoinController] Spawn point \"{MonsterSpawnPointName}\" not found in scene. Monster was not spawned.");
            return;
        }

        Vector3 spawnPos = SpawnPositionFinder.FindClearPosition(spawnPointObj.transform.position, GameSettings.Current.MonsterSpawnRange);
        PhotonNetwork.Instantiate(MonsterPrefabName, spawnPos, Quaternion.identity, 0);
    }
}
