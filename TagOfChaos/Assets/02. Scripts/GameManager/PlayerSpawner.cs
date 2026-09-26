using System.Collections;
using Photon.Pun;
using UnityEngine;

// 캐릭터 스폰 전담. GameManager.cs에서 분리됨(architecture-review.md §1.1).
public class PlayerSpawner : MonoBehaviour
{
    private const string SpawnPointName = SceneSpawnPoints.Cookie;
    private const string PlayerPrefabName = "HideOrSeekPlayer";

    // GameScene=true(괴물은 쿠키 대신 MonsterJoinController가 MonsterPlayer를 스폰), GameLobbyScene=false.
    // 대기실에서는 괴물도 쿠키로 돌아다녀야 하는데, 판이 끝나 돌아온 직후에는 RoundStateResetter의 초기화가
    // 도착하기 전의 옛 MonsterActorNumbers를 보고 이전 판 괴물의 스폰을 건너뛰는 경쟁 조건이 있었다
    // (Bug-fix-plan.md §24.5).
    [SerializeField] private bool skipConfirmedMonster = true;

    private void Start()
    {
        // Start()가 실행됐다고 해서 Photon이 실제로 "방에 완전히 입장 완료"(InRoom) 상태가 됐다는
        // 보장은 없음이 실측으로 확인됐다(Bug-fix-plan.md §12) — IsMessageQueueRunning=true와
        // InRoom==true는 서로 다른 상태이며, follower 클라이언트의 Start() 시점에는 아직 InRoom이
        // false일 수 있다. 그 상태에서 PhotonNetwork.Instantiate를 호출하면 로컬에는 생성되지만
        // 네트워크 전파(RaiseEvent)가 조용히 실패해 다른 클라이언트에게는 영원히 보이지 않는다.
        // 그래서 고정된 생명주기 시점 하나를 추측하는 대신, InRoom이 실제로 true가 될 때까지
        // 매 프레임 확인하다가 그 순간에만 스폰한다.
        StartCoroutine(SpawnWhenInRoom());
    }

    private IEnumerator SpawnWhenInRoom()
    {
        while (!PhotonNetwork.InRoom)
            yield return null;

        if (skipConfirmedMonster && IsAlreadyMonster())
        {
            Debug.Log($"[PlayerSpawner] Skip cookie spawn: local actor {PhotonNetwork.LocalPlayer.ActorNumber} is the monster.");
            yield break; // 이미 괴물로 확정된 플레이어는 이 씬에서 쿠키를 스폰하지 않음
        }

        SpawnLocalPlayer();
    }

// GameRule.md §2.2/§6.4 — 이미 괴물로 확정된(MonsterActorNumbers에 ActorNumber가 포함된) 플레이어는
    // 이 씬에서 쿠키를 스폰하지 않는다 — MonsterJoinController가 따로 MonsterPlayer를 스폰한다(GameScene).
    // 아직 가마솥에 들어가기 전(GameLobbyScene 초기)에는 이 플래그가 없으므로 정상적으로 쿠키가 스폰된다.
    private bool IsAlreadyMonster()
    {
        if (OfflineModeBootstrap.SpawnAsMonster) return true; // PlayerTestScene 개발용 몬스터 플레이테스트 토글

        return RoomState.IsLocalMonster();
    }

    
    private void SpawnLocalPlayer()
    {
        GameObject spawnPointObj = GameObject.Find(SpawnPointName);
        if (spawnPointObj == null)
        {
            Debug.LogWarning($"[PlayerSpawner] Spawn point \"{SpawnPointName}\" not found in scene. Cookie was not spawned.");
            return;
        }

        // SpawnWhenInRoom()이 InRoom==true를 확인한 뒤에만 호출하므로 이 시점엔 이미 정상 상태이지만,
        // 혹시 모를 추후 다른 호출경로를 대비해 방어적으로 한 번 더 명시한다.
        PhotonNetwork.IsMessageQueueRunning = true;

        Vector3 spawnPos = SpawnPositionFinder.FindClearPosition(spawnPointObj.transform.position, GameSettings.Current.CookieSpawnRange);

        GameObject spawned = PhotonNetwork.Instantiate(PlayerPrefabName, spawnPos, Quaternion.identity, 0);

        // Bug-fix-plan.md §9.4 진단 로그 — 스폰이 실제로 네트워크상 정상 등록되었는지(ViewID != 0)를
        // 각 클라이언트 콘솔에서 바로 확인할 수 있게 남겨둔다 — 실제 다중 클라이언트 테스트에서
        // 가시성 문제가 다시 재현될 경우, 이 로그만으로 원인을 "로컬 생성 실패" vs
        // "네트워크 전파 실패"로 명확히 구분할 수 있다.
        if (spawned != null)
        {
            PhotonView spawnedView = spawned.GetComponent<PhotonView>();
            Debug.Log($"[PlayerSpawner] Spawned cookie: ViewID={spawnedView.ViewID}, IsMine={spawnedView.IsMine}, " +
                      $"IsRoomView={spawnedView.IsRoomView}, LocalActorNr={PhotonNetwork.LocalPlayer.ActorNumber}, " +
                      $"IsMasterClient={PhotonNetwork.IsMasterClient}, RoomPlayerCount={PhotonNetwork.CurrentRoom.PlayerCount}");
        }
        else
        {
            Debug.LogWarning("[PlayerSpawner] PhotonNetwork.Instantiate returned null. Local instantiation failed.");
        }
    }
}
