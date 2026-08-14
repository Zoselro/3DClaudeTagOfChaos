# 로비 & 대기방 시스템 설계 (RoomItemPlan.md)

> 상태: **구현 완료**. 아래 각 섹션에 구현 결과를 표시했다. 실제 코드/프리팹/씬 변경 사항은
> `Assets/02. Scripts/Lobby/*.cs`, `Assets/02. Scripts/ColorTag/RoomLifecycleWatcher.cs`,
> `Assets/Resources/UI/Scene/{LobbyPanel,RoomListItem,GameLobbyPanel,PlayerListItem}/*.prefab`,
> `Assets/Scenes/{LobbyScene,GameLobbyScene}.unity`를 참고.
>
> **알려진 후속 작업(이번 계획 범위 밖)**: 새로 만든 UI 텍스트(TMP)는 프로젝트 기본 폰트인
> `LiberationSans SDF`를 그대로 쓰는데, 이 폰트에는 한글 글리프가 없어 한국어 문자열이 화면에서
> `□`로 표시된다. 한글을 지원하는 TMP 폰트 에셋(예: Noto Sans KR SDF)을 프로젝트에 추가하고
> 각 TMP 컴포넌트의 Font Asset을 교체해야 실제로 읽을 수 있게 된다 — 이번 계획에 폰트 에셋이
> 포함되어 있지 않아 별도 작업으로 남겨둔다.

## 0. 조사 결과 요약

- `Assets/02. Scripts/`에는 로비/방 관련 스크립트가 **하나도 없다**. `LobbyScene.unity`,
  `GameLobbyScene.unity`, `GameScene.unity` 셋 다 316줄짜리 기본 빈 씬(카메라+라이트만 있는 템플릿)
  그대로였다 — 즉 이번이 이 세 씬을 실제로 채우는 첫 작업이다.
- 프로젝트에 아직 Photon 연결(`ConnectUsingSettings`)을 호출하는 코드가 전혀 없다. 지금까지 유일한
  진입점은 `Assets/02. Scripts/Dev/OfflineModeBootstrap.cs`인데, 이건 `PhotonNetwork.OfflineMode = true`
  로 로비 자체를 건너뛰는 **개발용 테스트 진입점**이라 이번 설계와는 무관하게 그대로 둔다.
- Photon PUN2 SDK에 포함된 데모(`PunBasics-Tutorial/Launcher.cs`, `DemoAsteroids/Lobby/*`)를 참고해서
  실제 동작하는 API 시그니처를 확인했다 — 아래 코드 스니펫은 전부 이 데모들에서 실제로 쓰이는 API를
  이 프로젝트 스타일(TMP, `[SerializeField]` + `Awake()`에서 `onClick.AddListener`, `NetKeys` 상수 패턴)
  에 맞춰 재구성한 것이다.
- **기존 코드(`RoomLifecycleWatcher.cs`)와 맞물리는 부분을 발견해 0.2에 정리했다** — 사용자 확인을
  거쳐 결론까지 났고(정상 종료 시 방을 나가지 않고 `GameLobbyScene`으로 복귀), 코드는 아직 고치지
  않았다.

## 0.1 해석/가정 정리

1. **"UserID"는 로그인 계정이 아니라 닉네임이다.** 이 프로젝트에는 인증/로그인 시스템이 없으므로,
   입력한 값은 `PhotonNetwork.NickName`(= `PhotonNetwork.LocalPlayer.NickName`)에 그대로 반영해서
   다른 사람에게 보이는 표시 이름으로만 쓴다. 별도 저장(로그인, 계정 DB)은 하지 않는다.
2. **방 최대 인원은 4명 고정이다.** `GameScenePlan.md` 0.1-5("4인 중 일부가 나가는 것은 정상 진행,
   1명만 남으면 방 폭파")가 이미 4인 기준으로 설계돼 있어서, 방 생성 시 `RoomOptions.MaxPlayers`를
   사용자가 고를 수 있게 하지 않고 **4로 고정**한다(Photon 데모들은 보통 이 값을 UI로 입력받게
   하지만, 이 프로젝트는 인원수 자체가 게임 룰에 고정돼 있어 그럴 필요가 없다고 판단).
3. **방 이름은 직접 입력한 값을 그대로 쓰고, 비어있으면 생성하지 않는다.** Photon 데모들은 이름이
   비어있으면 `"Room " + Random.Range(...)`로 자동 생성하는데, 이번 요구사항은 "내가 적는 이름이 방
   이름이 된다"이므로 자동 생성 fallback은 넣지 않고 빈 이름이면 안내 메시지만 띄운다.
4. **"랜덤한 방 들어가기"는 실패해도 방을 자동으로 만들지 않는다.** `PunBasics-Tutorial`의
   `Launcher.cs`는 `OnJoinRandomFailed`에서 바로 방을 새로 만드는데, 이번 요구사항은 "랜덤 입장"과
   "MakeRoom"이 사용자에게 분명히 분리된 두 버튼이라, 랜덤 입장 실패 시에는 "참가 가능한 방이
   없습니다" 안내만 하고 방을 만들진 않는다(원하시면 나중에 자동 생성으로 바꿀 수 있다).
5. **방 목록에는 꽉 찼거나(방장이 게임을 이미 시작해서) 닫힌 방도 계속 보여주되, 입장 버튼만 비활성화한다.**
   `DemoAsteroids`의 `LobbyMainPanel.UpdateCachedRoomList`는 `!info.IsOpen`이면 아예 목록에서
   지워버리는데, 이번 요구사항("시간초 잴 때는 방 목록은 보여도, 들어갈 수 없어")은 명시적으로 다르다
   — 그래서 이 프로젝트에서는 `RemovedFromList`(방이 완전히 사라진 경우)만 목록에서 제거하고,
   `IsOpen == false`인 방은 목록에는 남기되 입장 버튼(`interactable`)만 끈다.
6. **입장(생성/랜덤/직접) 즉시 `GameLobbyScene`으로 이동한다.** `LobbyScene`에서 대기하는 개념은
   없다 — 방에 들어가는 순간 바로 씬을 옮긴다(요구사항 원문: "입장을 하게 되면 GameLobbyScene에
   입장을 하게 돼").

## 0.2 게임 종료 → `GameLobbyScene` 복귀 (확정, `RoomLifecycleWatcher` 수정 필요) — ✅ 구현 완료

**사용자 확인 결과**: 정상 종료 후 목적지는 (제가 처음에 권장했던 `LobbyScene`이 아니라) 원래
설계대로 **`GameLobbyScene`이 맞다.** "게임 대기방은 어디까지나 대기할 수 있는 방"이라서, 게임이
끝나면 같은 방(같은 인원)이 그대로 대기실로 돌아가 다음 게임을 다시 기다리는 구조다 — 방을 나갔다가
새로 만들거나 다시 찾아 들어오는 게 아니다.

그런데 이 확인 때문에 `RoomLifecycleWatcher.cs`의 **동작 자체**를 손봐야 한다는 게 명확해졌다. 지금
코드는 정상 종료든 비정상 종료든 **똑같이 `PhotonNetwork.LeaveRoom()`을 호출해서 방을 완전히
나간 뒤** `OnLeftRoom()`에서 씬만 다르게 로드한다(`GameScenePlan.md` 810행: "7.2와 7.3은 둘 다 '방을
나간다'는 동작으로 귀결되므로 하나의 컴포넌트가 함께 관리"). 정상 종료 시에도 방을 나가버리면,
`GameLobbyScene`에 도착했을 때 `PhotonNetwork.CurrentRoom`이 `null`이라 인원 수 표시도, 3.3의
"방장만 보이는 시작 버튼"도 아무 의미가 없어진다.

**결론: 정상 종료(7.3)는 더 이상 방을 나가지 않고, 같은 방에 있는 채로 `PhotonNetwork.LoadLevel
("GameLobbyScene")`만 호출**하도록 바꿔야 한다(`AutomaticallySyncScene`으로 전원 동기화, 1장 참고).
비정상 종료(7.2, 술래 퇴장/인원 부족)는 지금 그대로 "방을 나가고 `LobbyScene`으로" 유지한다 — 이건
정말로 방이 폭파되는 경우라 다르다.

**변경 전/후 비교** (`Assets/02. Scripts/ColorTag/RoomLifecycleWatcher.cs`에 실제 반영 완료):

```csharp
// 변경 전 (현재 코드, GameScenePlan.md 7.3)
private void Update()
{
    if (leaveReason != LeaveReason.None) return;
    if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
    if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.GameEndTime, out object endObj)) return;

    double gameEndTime = (double)endObj;
    if (PhotonNetwork.Time < gameEndTime) return;

    leaveReason = LeaveReason.NormalGameEnd;
    PhotonNetwork.LeaveRoom(); // ← 방을 나가버림
}

public override void OnLeftRoom()
{
    string targetScene = leaveReason == LeaveReason.NormalGameEnd ? "GameLobbyScene" : "LobbyScene";
    SceneManager.LoadScene(targetScene);
}
```

```csharp
// 변경 후 (이번 계획에서 제안)
private void Update()
{
    if (leaveReason != LeaveReason.None) return; // 비정상 종료(7.2) 처리 중이면 건너뜀
    if (!PhotonNetwork.IsMasterClient) return; // 씬 전환은 마스터만 트리거 (ColorSelectionManager와 동일 패턴)
    if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
    if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.GameEndTime, out object endObj)) return;

    double gameEndTime = (double)endObj;
    if (PhotonNetwork.Time < gameEndTime) return;

    ReturnToGameLobby();
}

// 방은 나가지 않고, 같은 방을 그대로 유지한 채 대기실로 되돌아간다
private void ReturnToGameLobby()
{
    var props = new Hashtable
    {
        { NetKeys.GameEndTime, null }, // 다음 게임에서 같은 조건이 또 걸리지 않도록 제거
    };
    PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    PhotonNetwork.CurrentRoom.IsOpen = true; // 3.3에서 게임 시작 시 닫아뒀던 걸 다시 연다

    PhotonNetwork.LoadLevel("GameLobbyScene"); // AutomaticallySyncScene으로 전원 함께 이동
}

// OnLeftRoom은 이제 비정상 종료(7.2)에서만 호출된다
public override void OnLeftRoom()
{
    SceneManager.LoadScene("LobbyScene");
}
```

- `leaveReason` enum의 `NormalGameEnd` 값과 `OnLeftRoom()`의 분기 로직은 더 이상 필요 없어져서
  단순화된다(`Abnormal` 여부만 판단하는 `bool`로 줄일 수 있지만, 최소 변경을 원하시면 enum은 그대로
  두고 `NormalGameEnd` 값만 안 쓰는 형태로 남겨도 무방하다).
- `PhotonNetwork.CurrentRoom.IsOpen = true`를 여기서 명시적으로 다시 켜주는 이유: 3.3(방장 수동 시작
  버튼)에서 게임을 시작할 때 `IsOpen = false`로 닫아두는데, 게임이 끝나고 대기실로 돌아왔을 때는 다시
  누군가 들어올 수 있어야 하기 때문이다.

## 0.3 게임 시작 방식 변경: 자동 카운트다운 → 방장 전용 수동 시작 버튼 — ✅ 구현 완료 (3.3 참고)

**사용자 확인 결과**: "4인이 차면 자동으로 시작"이 아니라, **방장(MasterClient)에게만 "게임 시작"
버튼이 보이고**, 인원이 다 안 찼으면 버튼이 비활성/회색, 다 찼으면 활성/흰색으로 바뀌어서 **방장이
직접 눌러야** 게임이 시작된다. 최초 설계(0번째 요구사항 원문의 "10초 뒤 시작")를 대체하는 결정이라,
아래 3.3을 이 방식으로 다시 썼다 — 타이머(`GameStartTime` 같은 Room 프로퍼티)가 아예 필요 없어져서
설계가 오히려 단순해졌다.

이 패턴은 마침 Photon 공식 데모 `DemoAsteroids/Lobby/LobbyMainPanel.cs`의 `OnStartGameButtonClicked`
+ `CheckPlayersReady`가 정확히 같은 구조(방장만 시작 버튼 사용 가능, 조건 충족 시에만 활성화)라 그
패턴을 그대로 가져다 썼다(다만 그 데모는 "전원 준비 완료" 조건이고, 여기는 "정원 도달" 조건이라는
차이만 있다).

버튼의 회색/흰색 전환은 별도 코드가 필요 없다 — Unity `Button`(`Selectable`) 컴포넌트가 기본으로
`interactable` 값에 따라 `Normal Color`(흰색)/`Disabled Color`(회색)를 자동으로 전환해주므로,
스크립트에서는 `button.interactable`만 켜고 끄면 된다.

---

## 1. 전체 흐름

```
LobbyScene                         GameLobbyScene                    GameScene
─────────────                      ────────────────                  ──────────
[씬 진입]                                                              
  → ConnectUsingSettings()
  → OnConnectedToMaster()
  → JoinLobby()
  → 방 목록 실시간 갱신
                                                                        
[UserID 입력]                                                          
[랜덤 입장] ──┐                                                        
[RoomName 입력]│                                                       
[MakeRoom] ───┼──→ OnJoinedRoom() ──→ PhotonNetwork.LoadLevel          
[방 목록 항목  │      (PlayerCount==1인                                
 → 입장]     ─┘       클라이언트만 호출,                                
                       나머지는                                        
                       AutomaticallySyncScene으로                      
                       자동 동기화)                                    
                                    ↓                                  
                            [플레이어 목록 표시]                        
                            [방장에게만 "게임 시작" 버튼 노출]           
                              - 정원(4명) 미달: 버튼 비활성(회색)        
                              - 정원 도달: 버튼 활성(흰색)               
                            [방장이 버튼 클릭]                          
                              → Room.IsOpen=false (입장 차단)           
                              → MasterClient가 LoadLevel                 
                                                        ↓                
                                                  PhotonNetwork.LoadLevel
                                                  ("GameScene")로 전원 이동
                                                  (AutomaticallySyncScene)
                                                        │
                            ┌───────────────────────────┘
                            ↓  (본게임 종료 20초 후, 방을 나가지 않고
                            │   같은 방 그대로 복귀 — 0.2 참고)
                    [GameLobbyScene으로 재진입]
                    Room.IsOpen=true 로 복구, 다시 대기 → 위 순환 반복
```

`PhotonNetwork.AutomaticallySyncScene = true`를 켜두면, 같은 방에 있는 클라이언트 중 한 명(보통
MasterClient)이 `PhotonNetwork.LoadLevel(...)`을 호출할 때 **방 안의 모든 클라이언트가 자동으로 같은
씬을 로드**하고, 이후 새로 들어오는 플레이어도 방이 이미 로드해둔 씬으로 자동으로 맞춰진다(`PunBasics-
Tutorial/Launcher.cs`, `DemoAsteroids/LobbyMainPanel.cs` 둘 다 이 패턴). 그래서 씬 전환 코드에서
`SceneManager.LoadScene`이 아니라 `PhotonNetwork.LoadLevel`을 써야 하고, 각 클라이언트가 따로 로드
호출을 하지 않도록 조건을 걸어야 한다(`PlayerCount == 1`일 때만, 또는 `IsMasterClient`일 때만).

---

## 2. LobbyScene 설계 — ✅ 구현 완료

`LobbyController.cs`/`RoomListItem.cs` (`Assets/02. Scripts/Lobby/`)와 `LobbyPanel`/`RoomListItem`
프리팹(`Assets/Resources/UI/Scene/`)을 아래 설계대로 생성했고, `LobbyScene.unity`에
`LobbyUICanvas`(+ `EventSystem`) 하위에 `LobbyPanel` 인스턴스를 배치해 필드까지 연결했다.

### 2.1 연결 & 로비 진입

```csharp
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class LobbyController : MonoBehaviourPunCallbacks
{
    private const int MaxPlayers = 4; // GameScenePlan.md 0.1-5와 동일 (인원수는 게임 룰에 고정)
    private const string GameVersion = "1"; // 빌드가 바뀌면 올려서 이전 버전 클라이언트와 매치메이킹이 섞이지 않게 함

    [SerializeField] private TMP_InputField userIdInput;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private RoomListItem roomListItemPrefab;

    private readonly Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();
    private readonly Dictionary<string, RoomListItem> roomListItems = new Dictionary<string, RoomListItem>();

    private void Awake()
    {
        // #Critical: LoadLevel()이 방 전체에 씬 전환을 자동 동기화하게 함 (PunBasics-Tutorial 관례)
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = GameVersion;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Start()
    {
        userIdInput.text = "Player" + Random.Range(1000, 10000); // 기본값, 직접 수정 가능

        if (PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        cachedRoomList.Clear();
        ClearRoomListView();
    }
}
```

- `MonoBehaviourPunCallbacks`를 쓰는 다른 스크립트들(11장 검증에서 발견한 문제, `GameScenePlan.md`
  13장)과 마찬가지로 **`OnEnable`/`OnDisable`에서 반드시 `AddCallbackTarget`/`RemoveCallbackTarget`을
  호출**해야 콜백이 실제로 불린다. 이 문서의 모든 신규 스크립트에 동일하게 적용한다.

### 2.2 방 목록 갱신 (`OnRoomListUpdate`)

Photon 로비는 방 목록을 매번 전체로 다시 보내지 않고, **변경분(추가/갱신/삭제)만** 콜백으로 준다.
그래서 로컬에 캐시를 들고 있다가 델타를 반영하는 구조가 필요하다(`DemoAsteroids/LobbyMainPanel.cs`
참고, 0.1-5에서 정리한 대로 "닫힌 방도 목록엔 남긴다"는 부분만 다르게 가져간다):

```csharp
public override void OnRoomListUpdate(List<RoomInfo> roomList)
{
    foreach (RoomInfo info in roomList)
    {
        if (info.RemovedFromList)
        {
            cachedRoomList.Remove(info.Name);
            continue;
        }

        cachedRoomList[info.Name] = info; // IsOpen==false여도 목록에는 남겨둔다 (0.1-5)
    }

    RefreshRoomListView();
}

private void RefreshRoomListView()
{
    foreach (var kv in cachedRoomList)
    {
        if (!roomListItems.TryGetValue(kv.Key, out RoomListItem item))
        {
            item = Instantiate(roomListItemPrefab, roomListContent);
            roomListItems.Add(kv.Key, item);
        }
        item.Refresh(kv.Value, this); // 이름 / "N / 4" / 입장 버튼 interactable 갱신
    }

    // 목록에서 사라진 방의 UI 항목 정리
    var toRemove = new List<string>();
    foreach (var kv in roomListItems)
    {
        if (!cachedRoomList.ContainsKey(kv.Key)) toRemove.Add(kv.Key);
    }
    foreach (string name in toRemove)
    {
        Destroy(roomListItems[name].gameObject);
        roomListItems.Remove(name);
    }
}
```

`RoomListItem.cs`(항목 하나, `ColorSwatchButton.cs`와 같은 스타일 — 콜백은 매니저로 위임):

```csharp
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button joinButton;

    private string roomName;
    private LobbyController lobby;

    private void Awake()
    {
        joinButton.onClick.AddListener(() => lobby.JoinRoom(roomName));
    }

    public void Refresh(RoomInfo info, LobbyController owner)
    {
        lobby = owner;
        roomName = info.Name;
        roomNameText.text = info.Name;
        playerCountText.text = $"{info.PlayerCount} / {info.MaxPlayers}"; // 1/4, 2/4 ...
        joinButton.interactable = info.IsOpen; // 방장이 이미 게임을 시작했으면 비활성화 (0.1-5)
    }
}
```

### 2.3 방 생성 / 랜덤 입장 / 직접 입장

```csharp
public void OnMakeRoomButtonClicked()
{
    if (!TryApplyNickname()) return;

    string roomName = roomNameInput.text.Trim();
    if (string.IsNullOrEmpty(roomName))
    {
        feedbackText.text = "방 이름을 입력하세요.";
        return;
    }

    var options = new RoomOptions { MaxPlayers = MaxPlayers };
    PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
}

public void OnRandomJoinButtonClicked()
{
    if (!TryApplyNickname()) return;
    PhotonNetwork.JoinRandomRoom();
}

public void JoinRoom(string roomName) // RoomListItem에서 호출
{
    if (!TryApplyNickname()) return;
    PhotonNetwork.JoinRoom(roomName);
}

private bool TryApplyNickname()
{
    string nickname = userIdInput.text.Trim();
    if (string.IsNullOrEmpty(nickname))
    {
        feedbackText.text = "UserID를 입력하세요.";
        return false;
    }
    PhotonNetwork.NickName = nickname;
    return true;
}

public override void OnCreateRoomFailed(short returnCode, string message)
{
    feedbackText.text = "이미 존재하는 방 이름입니다."; // 대부분 이 케이스 (ErrorCode.GameIdAlreadyExists)
}

public override void OnJoinRandomFailed(short returnCode, string message)
{
    feedbackText.text = "참가 가능한 방이 없습니다.";
}

public override void OnJoinRoomFailed(short returnCode, string message)
{
    feedbackText.text = "입장할 수 없는 방입니다."; // 방금 꽉 찼거나 방장이 이미 게임을 시작한 경우 등
}

public override void OnJoinedRoom()
{
    if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
        PhotonNetwork.LoadLevel("GameLobbyScene"); // 방을 새로 만든 최초 1인만 로드, 나머지는 자동 동기화
}
```

`AutomaticallySyncScene`/`GameVersion`은 2.1의 `LobbyController.Awake()`에서 한 번만 설정해두면 이후
씬 전환/매치메이킹 전체에 적용된다.

### 2.4 UI 구성 (신규 프리팹)

CLAUDE.md 규칙(`Resources/UI/{Popup|Scene|Tab}/{클래스명}`)과 `ColorSelectionPanel` 전례를 따른다:

| 프리팹 | 경로 | 구성 |
|---|---|---|
| `LobbyPanel` | `Assets/Resources/UI/Scene/LobbyPanel/LobbyPanel.prefab` | `TMP_InputField`(UserID), `Button`(랜덤 입장), `TMP_InputField`(RoomName), `Button`(MakeRoom), `TMP_Text`(피드백), 방 목록용 `ScrollRect`+`Content` |
| `RoomListItem` | `Assets/Resources/UI/Scene/RoomListItem/RoomListItem.prefab` | 방 이름 텍스트, "N / 4" 텍스트, 입장 버튼 |

신규 스크립트 위치: `Assets/02. Scripts/Lobby/LobbyController.cs`, `Assets/02. Scripts/Lobby/RoomListItem.cs`
(CLAUDE.md `Assets/02. Scripts/{도메인}/` 규칙, 도메인 = `Lobby`).

---

## 3. GameLobbyScene 설계 — ✅ 구현 완료

`GameLobbyController.cs`/`PlayerListItem.cs` (`Assets/02. Scripts/Lobby/`)와 `GameLobbyPanel`/
`PlayerListItem` 프리팹(`Assets/Resources/UI/Scene/`)을 아래 설계대로 생성했고,
`GameLobbyScene.unity`에 `GameLobbyUICanvas`(+ `EventSystem`) 하위에 `GameLobbyPanel` 인스턴스를
배치해 필드까지 연결했다.

### 3.1 입장 시 상태

`AutomaticallySyncScene` 덕분에 방에 있는 모든 클라이언트가 이 씬을 함께 로드한 상태로 시작한다.
플레이어 목록과 게임 시작 버튼을 담당하는 `GameLobbyController`를 씬에 배치한다(패턴은 `ColorSelectionManager`
와 동일하게 씬에 고정 배치된 매니저 오브젝트 하나, 네트워크 프리팹으로 스폰하지 않음).

### 3.2 플레이어 목록 표시

`RefreshPlayerList()`는 인원이 바뀔 때마다 다시 그리는 헬퍼로, 실제 호출은 3.3의
`OnPlayerEnteredRoom`/`OnPlayerLeftRoom`(모든 인원 변화를 이미 감시하고 있음)에서 함께 한다 —
콜백 하나에 "목록 갱신 + 시작 버튼 갱신"을 같이 묶어서, 같은 콜백을 두 군데서 따로 오버라이드하는
충돌(C#은 같은 메서드를 한 클래스에 두 번 못 씀)을 피한다.

```csharp
private void RefreshPlayerList()
{
    foreach (Transform child in playerListContent) Destroy(child.gameObject);

    foreach (Player p in PhotonNetwork.PlayerList)
    {
        var item = Instantiate(playerListItemPrefab, playerListContent);
        item.SetNickname(p.NickName);
    }

    statusText.text = $"{PhotonNetwork.CurrentRoom.PlayerCount} / {PhotonNetwork.CurrentRoom.MaxPlayers} 대기 중";
}
```

### 3.3 방장 전용 "게임 시작" 버튼 (0.3 참고 — 자동 카운트다운에서 변경됨)

타이머가 없어지고 방장이 직접 눌러야 하므로, Room CustomProperties에 시각을 박아둘 필요 없이 **버튼의
`interactable` 상태만 정원 여부에 맞춰 갱신**하면 된다. `DemoAsteroids/LobbyMainPanel.cs`의
`OnStartGameButtonClicked`/`CheckPlayersReady`와 같은 구조(0.3 참고). 인원 변화 콜백은 3.2의
`RefreshPlayerList()`와 여기서 함께 호출한다:

```csharp
[SerializeField] private Button startGameButton;

private void Start()
{
    RefreshPlayerList();
    RefreshStartButton();
}

public override void OnPlayerEnteredRoom(Player newPlayer)
{
    RefreshPlayerList();
    RefreshStartButton();
}

public override void OnPlayerLeftRoom(Player otherPlayer)
{
    RefreshPlayerList();
    RefreshStartButton();
}

// 게임 도중 방장이 나가서 마스터가 바뀌는 경우, 새 마스터에게 버튼을 노출/숨김
public override void OnMasterClientSwitched(Player newMasterClient)
{
    RefreshStartButton();
}

// 방장에게만 버튼을 보여주고, 정원이 찼을 때만 눌리게 함 (회색/흰색 전환은 Button 컴포넌트가 자동 처리)
private void RefreshStartButton()
{
    bool isOwner = PhotonNetwork.IsMasterClient;
    startGameButton.gameObject.SetActive(isOwner);
    if (!isOwner) return;

    startGameButton.interactable =
        PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers;
}

// 방장이 "게임 시작" 버튼을 눌렀을 때 (UI Button.onClick에 연결)
public void OnStartGameButtonClicked()
{
    if (!PhotonNetwork.IsMasterClient) return;
    if (PhotonNetwork.CurrentRoom.PlayerCount < PhotonNetwork.CurrentRoom.MaxPlayers) return; // 방어적 재확인

    PhotonNetwork.CurrentRoom.IsOpen = false; // 씬 전환 도중 새로 입장하는 걸 방지
    PhotonNetwork.LoadLevel("GameScene"); // 방장만 호출, 나머지는 AutomaticallySyncScene으로 함께 이동
}
```

- **왜 방장만 시작할 수 있는가**: 요구사항 원문("방장만 게임시작 버튼이 보이고")을 그대로 반영. 다른
  플레이어 클라이언트에서는 `startGameButton` 자체가 `SetActive(false)`라 존재하지 않는다.
- **인원이 줄어들면 버튼이 다시 회색이 되는가**: `OnPlayerLeftRoom`에서 매번 `RefreshStartButton()`을
  다시 호출하므로, 정원 미달로 돌아가면 자동으로 `interactable = false`가 된다 — 타이머를 취소하는
  절차가 따로 필요 없다(0.3에서 설계가 단순해진 이유).
- **`IsOpen`을 여기서 닫는 이유**: `LoadLevel` 호출 후 씬 전환이 실제로 끝나기까지 약간의 지연이
  있는데, 그 사이 다른 사람이 `JoinRoom`을 시도하면 어색한 상태가 될 수 있어 미리 막아둔다 — 게임이
  끝나고 대기실로 돌아오면 0.2의 `ReturnToGameLobby()`가 다시 `IsOpen = true`로 복구한다.

### 3.4 신규 스크립트/프리팹

| 항목 | 경로 |
|---|---|
| `GameLobbyController.cs` | `Assets/02. Scripts/Lobby/GameLobbyController.cs` |
| `PlayerListItem.cs` | `Assets/02. Scripts/Lobby/PlayerListItem.cs` |
| `GameLobbyPanel` 프리팹 | `Assets/Resources/UI/Scene/GameLobbyPanel/GameLobbyPanel.prefab` |
| `PlayerListItem` 프리팹 | `Assets/Resources/UI/Scene/PlayerListItem/PlayerListItem.prefab` |

### 3.5 UserID(닉네임)가 씬을 넘나들며 유지되는지

**요구사항**: `LobbyScene`에서 정한 UserID가 `GameLobbyScene`/`GameScene`/(게임이 끝나고) 다시
`GameLobbyScene`으로 돌아왔을 때도 유지돼야 하고, 방을 나가거나 게임을 끄거나 강제 종료하면
초기화돼야 한다.

**결론: 추가 구현이 필요 없다.** 2.3에서 `PhotonNetwork.NickName`에 반영해두면, 이 값은
`PhotonNetwork.LocalPlayer.NickName`으로 **Photon 세션이 유지되는 동안 씬 전환과 무관하게 그대로
남아있는다** — `SceneManager.LoadScene`이든 `PhotonNetwork.LoadLevel`이든 씬을 아무리 옮겨도 값이
지워지지 않는다(애초에 `PlayerPrefs` 같은 별도 저장소를 쓰는 게 아니라 Photon 세션의 상태 그 자체다).
반대로 방을 나가거나(`LeaveRoom`) 앱을 끄면 그 세션 자체가 사라지므로 값도 자연히 초기화된다 — 요구
사항이 설명한 "유지/초기화" 조건과 정확히 일치한다. `PlayerPrefs` 등 영구 저장은 쓰지 않는다(재실행하면
다시 물어보는 게 맞는 동작).

각 씬에서 UserID를 화면에 보여줘야 할 때는 그냥 읽으면 된다: 자기 자신은
`PhotonNetwork.LocalPlayer.NickName`, 다른 플레이어는 목록을 돌 때 각 `Player.NickName`
(`RoomListItem`/`PlayerListItem`이 이미 이 값을 쓰고 있다, 2.2/3.2 참고).

---

## 4. `NetKeys.cs` 확장안

**신규 키 없음.** 처음 계획에서는 카운트다운 종료 시각을 담을 `GameStartTime` 키를 추가할 예정이었지만,
0.3에서 자동 카운트다운을 방장 수동 시작 버튼으로 바꾸면서 타이머 자체가 없어져 더 이상 필요 없다.
기존 `GameEndTime`(본게임 종료 후 대기실 복귀용, 0.2)은 그대로 재사용한다.

---

## 5. 남겨둘 것 (이번 계획 범위 밖)

- 실제 UI 프리팹 제작(레이아웃, 폰트, 배치)과 `LobbyScene`/`GameLobbyScene`의 `Canvas`/`EventSystem`
  구성은 계획에는 표만 정리했고 실제 제작은 구현 단계에서 진행한다.
- `GameScene`에서 실제 게임 로직(캐릭터 스폰 등)은 `PlayerControllPlan.md`/`GameScenePlan.md`의
  기존 범위이며 이 문서와는 무관하다 — 이 문서는 `GameScene` 진입 직전까지만 다룬다.
- `GameScene`의 승패 판정 로직이 게임이 끝나는 시점에 `GameEndTime = PhotonNetwork.Time + 20f`를
  Room CustomProperties에 기록해주는 부분은 이미 `GameScenePlan.md` 7.3에서 계약으로 정의돼 있고
  구현도 이미 있다고 가정한다 — 이 문서(0.2)는 그 이후(`RoomLifecycleWatcher`가 감지해서
  `GameLobbyScene`으로 되돌리는 부분)만 다룬다.

---

## 6. UI 후속 조정: Canvas Scaler 통일 & 로비 레이아웃 개선 — ✅ 구현 완료

### 6.1 배경

- 구현 단계(§2, §3)에서 `LobbyUICanvas`/`GameLobbyUICanvas`를 새로 만들면서 `CanvasScaler`는
  Unity 기본값(`Constant Pixel Size`)을 그대로 두었다. 기존 `PlayerTestScene`의 `GameUICanvas`도
  같은 기본값이었다. 씬마다 스케일 기준이 다르면 해상도가 다른 화면에서 UI 크기가 제각각으로
  보인다는 문제가 있어, 프로젝트의 모든 `Canvas`가 항상 같은 스케일 기준을 쓰도록 통일해달라는
  요청을 받았다.
- 로비 화면(`LobbyPanel`)에서 `MakeRoomButton`이 `RoomNameInput` 바로 옆(화면 상단, 방 목록 바로
  위)에 있어 방 목록과 시각적으로 뒤섞여 가시성이 떨어진다는 피드백을 받아, 버튼을 패널 우측 하단
  (별도 액션 버튼 위치)으로 옮기기로 했다.

### 6.2 계획

1. **`CanvasScaler` 값 통일**: 대상은 현재 프로젝트에 존재하는 모든 `Canvas`
   — `LobbyScene`의 `LobbyUICanvas`, `GameLobbyScene`의 `GameLobbyUICanvas`,
   `PlayerTestScene`의 `GameUICanvas`. 아래 값으로 맞춘다(이후 새로 만드는 `Canvas`도 동일하게
   맞춰야 한다):
   - `UI Scale Mode` = **Scale With Screen Size** (`CanvasScaler.ScaleMode.ScaleWithScreenSize`)
   - `Reference Resolution` = **1920 x 1080**
   - `Screen Match Mode` = **Match Width Or Height**
     (`CanvasScaler.ScreenMatchMode.MatchWidthOrHeight`)
2. **`LobbyPanel` 프리팹 레이아웃 조정** (`Assets/Resources/UI/Scene/LobbyPanel/LobbyPanel.prefab`):
   - `MakeRoomButton`을 2행(RoomName 입력 옆)에서 빼서 패널 **우측 하단**으로 이동
     (앵커 `(1,0)-(1,0)`, 크기 160x50, 우측/하단 여백 20px).
   - 버튼이 빠진 자리를 메우기 위해 `RoomNameInput`을 2행 전체 폭으로 넓힌다.
   - `RoomListScrollView` 하단 여백을 20px → 100px로 넓혀서 새로 옮긴 `MakeRoomButton`과
     겹치지 않게 한다.

### 6.3 구현 결과

- `LobbyUICanvas`(`LobbyScene`) / `GameLobbyUICanvas`(`GameLobbyScene`) / `GameUICanvas`
  (`PlayerTestScene`) 세 `Canvas`의 `CanvasScaler`를 모두 `Scale With Screen Size` /
  `1920x1080` / `Match Width Or Height`로 통일하고 각 씬을 저장했다.
- `LobbyPanel` 프리팹을 프리팹 스테이지에서 편집: `RoomNameInput`을 2행 전체 폭으로 넓히고,
  `MakeRoomButton`을 패널 우측 하단(앵커 `(1,0)-(1,0)`, 160x50, 여백 20px)으로 옮기고,
  `RoomListScrollView`의 하단 여백을 20px → 100px로 넓혀 버튼과 겹치지 않게 했다. 스크롤뷰
  오브젝트 이름이 `"2"`로 잘못 남아있던 것도 `RoomListScrollView`로 바로잡았다.
- Play 모드 스크린샷으로 확인: `RandomJoinButton`("랜덤 입장")·`FeedbackText` 등 한글이 정상
  렌더링되고, `MakeRoomButton`이 우측 하단에 배치되어 방 목록과 겹치지 않는다.

---

## 7. 버그: `GameLobbyScene`에서 카메라 화면 전체가 UI 패널에 뒤덮임 — ✅ 구현 완료

> 사용자 보고: "GameLobbyScene의 문제점은 지금 Camera가 GameLobbyUICanvas에 맞춰져 있어서 게임
> 전체가 안보여." 아래는 실제 `Assets/Scenes/GameLobbyScene.unity` 파일을 직접 열어 확인한
> 원인 분석과 수정 계획이다. **지시에 따라 이번에는 계획만 정리했고 실제 수정은 하지 않았다.**

### 7.1 원인 (씬 파일로 직접 확인)

`GameLobbyUICanvas`의 `Canvas` 컴포넌트가 **`m_RenderMode: 2` (World Space)** 로 되어 있다.
Screen Space - Overlay(`0`)여야 정상이다 — 실제로 같은 방식으로 만든 `LobbyScene`의
`LobbyUICanvas`는 `m_RenderMode: 0`으로 정상이고, 기존 `PlayerTestScene`의 `GameUICanvas`도
`0`으로 정상이다. `GameLobbyUICanvas` 하나만 잘못돼 있다.

**왜 이 Canvas만 다르게 만들어졌는가**: 두 Canvas는 생성 방식이 달랐다.
- `LobbyUICanvas`(§2)는 Unity 에디터 메뉴 `GameObject > UI > Canvas`(`execute_menu_item`)로
  만들었다. 이 메뉴는 Canvas를 추가한 직후 내부적으로(`UnityEditor.UI.MenuOptions.CreateNewUI()`)
  `renderMode`를 명시적으로 `ScreenSpaceOverlay`로 설정해준다.
- `GameLobbyUICanvas`(§3)는 이 메뉴 대신 `manage_gameobject`로 GameObject를 만들면서
  `UnityEngine.Canvas` 컴포넌트를 `AddComponent`로 직접 붙였다. 이 경로는 메뉴가 해주는 후처리
  (`renderMode` 명시적 설정)를 거치지 않기 때문에 Canvas가 기본값인 World Space로 남았다.

**왜 "카메라가 Canvas에 맞춰진 것처럼" 보이는가**: World Space Canvas에서는
`RectTransform`의 크기가 픽셀이 아니라 **월드 단위**로 해석된다. `GameLobbyPanel`은
700x800(픽셀 기준으로 설계)인데, World Space에서는 `referencePixelsPerUnit(100)` 기준으로
7 x 8 **유닛** 크기의 거대한 3D 평면이 되어 씬 원점 `(0,0,0)`에 그대로 놓인다. `Main Camera`는
`(0, 1, -10)`에서 FOV 60으로 원점 방향을 보고 있어서, 카메라로부터 10유닛 거리에 있는 이
8유닛 높이짜리 불투명 패널이 카메라 시야를 거의 다 채워버린다. 실제로는 카메라 값은 전혀
바뀌지 않았다 — Canvas 자체가 거대한 3D 오브젝트로 잘못 렌더링되면서 시야를 가리는 것이다.

부가로, §6에서 통일한 `CanvasScaler`의 `Scale With Screen Size` / `1920x1080` /
`Match Width Or Height` 설정도 World Space 모드에서는 애초에 적용되지 않는 옵션들이라
(World Space는 `Physical Unit` 기반 스케일링을 쓴다) 사실상 무시되고 있었다 — 즉 `GameLobbyUICanvas`는
§6 작업의 효과도 받지 못하고 있었다.

### 7.2 수정 계획 (미구현)

1. `GameLobbyScene.unity`의 `GameLobbyUICanvas` → `Canvas` 컴포넌트 `renderMode`를
   `WorldSpace(2)`에서 `ScreenSpaceOverlay(0)`로 변경한다 (`LobbyUICanvas`와 동일하게).
2. 전환 후 `RectTransform`의 앵커/크기가 Screen Space - Overlay 기준으로 정상 복귀했는지 확인한다
   (`GameLobbyPanel` 프리팹은 원래 픽셀 기준 700x800 anchored-center로 설계돼 있어, Canvas
   렌더 모드만 고치면 별도 프리팹 수정 없이 §3 레이아웃이 의도대로 나타날 것으로 예상).
3. 수정 후 검증 항목:
   - Play 모드 스크린샷으로 `GameLobbyPanel`이 화면 중앙에 정상 크기로 보이고(패널이 화면
     전체를 덮지 않고 배경도 함께 보이는 상태), §3의 `PlayerListScrollView`/`StatusText`/
     `StartGameButton`이 정상적으로 표시·클릭되는지 확인.
   - 콘솔에 `Canvas`/`CanvasScaler` 관련 에러·경고가 없는지 확인.
   - §6에서 적용한 `CanvasScaler`(Scale With Screen Size / 1920x1080 / Match Width Or Height)가
     Overlay 모드 전환 후 실제로 의도대로 동작하는지 함께 확인.
4. **재발 방지**: 앞으로 새 `Canvas`를 만들 때는 `manage_gameobject`로 `Canvas` 컴포넌트를
   직접 `AddComponent`하지 말고, `execute_menu_item("GameObject/UI/Canvas")`로 생성해서
   Unity가 `renderMode`를 올바르게 초기화하도록 한다. 부득이하게 컴포넌트를 직접 추가해야
   한다면, 추가 직후 반드시 `renderMode`를 `ScreenSpaceOverlay`로 명시적으로 설정한다.

### 7.3 구현 결과

- `GameLobbyScene.unity`의 `GameLobbyUICanvas` → `Canvas.renderMode`를 `WorldSpace(2)`에서
  `ScreenSpaceOverlay(0)`로 변경했다. 변경 즉시 `RectTransform`이 Unity에 의해 자동으로
  화면 전체(1920x1080, `drivenByObject`)로 재계산되는 것을 확인했다 — 프리팹 자체(`GameLobbyPanel`
  700x800 anchored-center 레이아웃)는 손대지 않았다.
- 같은 확인 과정에서 `CanvasScaler`(§6에서 설정한 Scale With Screen Size / 1920x1080 /
  Match Width Or Height)가 Overlay 전환 후 `renderingDisplaySize: 1920x1080`으로 실제 적용되고
  있음을 함께 확인했다 — World Space일 때는 무시되던 설정이 이제 정상 동작한다.
- Play 모드 스크린샷으로 확인: `GameLobbyPanel`이 화면 중앙에 정상 크기로 보이고 배경(하늘/바닥)도
  함께 보인다. `StatusText`("1 / 4"), `StartGameButton`("게임 시작") 등 §3 구성요소도 정상 표시된다.
- 프로젝트의 모든 씬(`LobbyScene`, `GameLobbyScene`, `GameScene`, `PlayerTestScene`, `SampleScene`)의
  `Canvas.m_RenderMode`를 전수 조사해, `GameLobbyUICanvas` 외에는 문제가 없음을 확인했다
  (`GameScene`/`SampleScene`에서 검색된 `m_RenderMode` 항목은 Canvas가 아니라 `Light` 컴포넌트의
  동명 필드였다).
- 컴파일/콘솔 에러 없음을 확인했다.
- (부수 발견) `GameLobbyScene`을 단독으로 Play해서 검증하는 과정에서 `GameLobbyController.Start()`가
  방에 들어오지 않은 상태로 실행되면 `PhotonNetwork.CurrentRoom`이 `null`이라
  `NullReferenceException`이 나는 것을 발견해 `Start()`에 다른 스크립트(`ColorSelectionManager`,
  `RoomLifecycleWatcher`)와 동일한 가드(`if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;`)를
  추가했다. 실제 플레이 흐름(§1)에서는 항상 방에 들어온 뒤에만 이 씬에 도달하므로 정상 흐름에는
  영향이 없다.
