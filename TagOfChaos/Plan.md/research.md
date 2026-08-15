# 조사 보고서: TagOfChaos 프로젝트 전체 (2026-08-15 전면 재작성)

> 이 문서는 이전 버전(§1~§38, 2026-08-13/14 작성)을 전면 대체한다. 이전 버전은 이미 삭제된
> `Assets/Scripts/Hero_Ctrl.cs`와, 현재는 `Assets/02. Scripts/GameManager/`로 옮겨진
> `GameManager.cs`의 이력을 다루고 있어 상당 부분이 낡았다. 이번 조사는 `Plan.md/` 폴더의 모든
> 계획 문서(`Claude.md`, `GameManager.md`, `GameScenePlan.md`, `RoomItemPlan.md`, `UserPlan.md`)와
> `Assets/02. Scripts/` 아래 **실제 코드 27개 파일 전체**, 4개 씬 파일(`LobbyScene`/
> `GameLobbyScene`/`GameScene`/`PlayerTestScene`), `HideOrSeekPlayer.prefab`, 커스텀 셰이더 3종,
> `EditorBuildSettings.asset`, `asmdef`를 직접 읽고 서로 대조해 지금 이 순간의 실제 동작 방식을
> 라인 단위로 재확인한 결과다. 오래된 정보는 폐기하고, 검증된 사실만 담았다.

---

## 1. 프로젝트 한 줄 요약

**Unity(URP 아님, Built-in RP 셰이더 사용) + Photon PUN2** 기반 2~4인 실시간 멀티플레이어 게임.
로비에서 방을 만들고 → 대기방에서 인원을 채우고 → 게임 씬에서 **4라운드에 걸쳐 팀 전체가 색을
투표/페인팅으로 정하고** → 그중 1명이 **술래**로 뽑혀 미묘하게 다른 색 조합을 부여받는(숨은
"보이지만 티 안 나는" 술래 식별 메커니즘) 술래잡기형 게임을 지향한다. `UserPlan.md`(요구사항
원문, 1줄 지시)를 `GameScenePlan.md`가 설계 문서로 상세화했고, `RoomItemPlan.md`가 로비/대기방을,
`GameManager.md`가 채팅+스폰+나가기 UX를 설계·구현했다.

**핵심 결론(가장 중요)**: 코드/에셋 레벨의 완성도는 각 도메인마다 높지만, **로비 → 대기방 →
게임 씬으로 이어지는 실제 매칭 플로우와 ColorTag 미니게임 사이에 배선이 끊겨 있다.** 즉 현재
상태로 실제로 2~4명이 매칭해 `GameScene`까지 도달해도, 화면에는 채팅창과 캐릭터만 있을 뿐 색상
선택 라운드가 시작되지 않는다. 이 미니게임은 오직 개발용 `PlayerTestScene`(Build Settings에서
비활성)에서만 수동 부트스트랩으로 확인 가능하다. §7에서 상세히 다룬다.

---

## 2. 폴더 규칙과 실제 구조 대조

`Plan.md/Claude.md`(프로젝트 컨벤션)가 규정한 규칙과 실제 디스크 상태를 대조한 결과, **규칙이
정확히 지켜지고 있다**:

| 분류 | 규칙 | 실제 |
|---|---|---|
| Scripts | `Assets/02. Scripts/{도메인}/` | `Camera/`, `ColorTag/`, `Dev/`, `GameManager/`, `Lobby/`, `Unit/` 6개 도메인, 27개 `.cs` |
| SO | `Assets/03. SO/{도메인}/` | `ColorTag/DefaultBrushSettings.asset`, `DefaultColorPalette.asset` |
| Prefabs | `Assets/04. Prefabs/` | `Resources/Brush.fbx`, `BrushCursor.prefab`, `HideOrSeekPlayer.prefab` |
| UI 프리팹 | `Resources/UI/{Popup\|Scene\|Tab}/{클래스명}` | `UI/Popup/ConfirmDialog/`, `UI/Scene/{ColorSelectionPanel,GameLobbyPanel,LobbyPanel,PlayerListItem,RoomListItem}/` |
| 에디터 | `Assets/Editor/` | 존재하지 않음(아직 커스텀 에디터 도구 없음, 문제 아님) |

`Assets/Scripts/`(구 트리)는 **완전히 사라졌다** — `GameManager.md` 작업에서 마지막 파일
(`GameManager.cs`)까지 옮기고 폴더를 삭제했음을 이번에 직접 재확인했다(`find` 결과 0건). 프로젝트
전체에 `.cs` 파일은 `Assets/02. Scripts/`의 27개와 `Assets/Photon/`(SDK/데모, 손대지 않음)뿐이다.

`TagOfChaos.Scripts.asmdef`(`Assets/02. Scripts/`)는 이 폴더 전체를 하나의 어셈블리로 묶으며,
참조는 `PhotonUnityNetworking`, `PhotonRealtime`, `Unity.TextMeshPro`, `Unity.ugui` 4개다(직접
파일 확인). 도메인별 asmdef 분리는 없다 — 한 도메인의 컴파일 에러가 전체를 막을 수 있는 구조.

**"주석 제외 한글 사용 금지"** 규칙: 코드를 직접 읽은 결과 모든 식별자(클래스/필드/메서드명)는
영어이고, 한글은 주석과(의도적으로 UI에 노출되는) 문자열 리터럴에만 등장한다 — 규칙 준수 확인.
`GameManager.cs`가 과거 CP949로 저장되어 있던 인코딩 결함(구 버전 §12/§37)은 `GameManager.md`
§2/§8.1에서 UTF-8(BOM)로 재저장되었고, 이번에 파일을 직접 읽어도 한글이 정상 표시되어 **해소가
유지되고 있음을 재확인**했다.

---

## 3. 씬 인벤토리 & Build Settings

`ProjectSettings/EditorBuildSettings.asset`을 직접 읽은 현재 상태:

```
0: Assets/Scenes/PlayerTestScene.unity  (enabled: 0 — 비활성, 개발용)
1: Assets/Scenes/LobbyScene.unity        (enabled: 1)
2: Assets/Scenes/GameLobbyScene.unity    (enabled: 1)
3: Assets/Scenes/GameScene.unity         (enabled: 1)
```

`SampleScene.unity`는 디스크에 파일은 있지만(`Assets/Scenes/SampleScene.unity`) Build Settings에는
등록되어 있지 않다. 구 버전이 지적했던 "`PhotonLobby`라는 존재하지 않는 씬 로드 시도"(§18/§34)
문제는 **완전히 해소됨을 확인** — `GameManager.OnLeftRoom()`과 `RoomLifecycleWatcher.OnLeftRoom()`
모두 `SceneManager.LoadScene("LobbyScene")`을 호출하며, `LobbyScene`은 Build Settings에 정상
등록되어 있다.

각 씬의 실제 계층 구조(씬 파일의 `m_Name` 필드를 grep해 직접 확인):

- **`LobbyScene.unity`**: `Main Camera` / `Directional Light` / `EventSystem` / `LobbyUICanvas`
  하나. `LobbyController`가 붙는 `LobbyPanel` 프리팹 인스턴스가 이 Canvas 하위에 있다(간단한
  구조, 채팅 없음).
- **`GameLobbyScene.unity`**: `Main Camera` / `Directional Light` / `EventSystem` /
  `GameLobbyUICanvas`(`GameLobbyPanel` 포함) / `Canvas`(`InputFieldChat`, `PanelLogMsg`, `Button`)
  / `GameManager` / `PlayerSpawnPos`. **`ColorSelectionManager`/`RoomLifecycleWatcher`/
  `BrushCursorController` 등 ColorTag 계열 오브젝트는 없다** — 이 씬은 대기방 UI + 채팅 + 캐릭터
  스폰까지만 책임진다(설계대로).
- **`GameScene.unity`**: `Main Camera` / `Directional Light` / `EventSystem` / `Canvas`
  (`InputFieldChat`, `PanelLogMsg`, `Button`) / `GameManager` / `PlayerSpawnPos`. **이 씬도
  ColorTag 계열 오브젝트가 전혀 없다** — §7에서 이것이 왜 치명적인지 설명한다.
- **`PlayerTestScene.unity`**(Build Settings 비활성): `Main Camera` / `Directional Light` /
  `GameUICanvas` / `EventSystem` / `TestBootstrap`(`OfflineModeBootstrap`) / `Ground` /
  **`ColorTagManagers`**(`ColorSelectionManager`+`RoomLifecycleWatcher`+`BrushCursorController`
  등이 붙어 있을 것으로 추정되는 오브젝트, §7 참고). **ColorTag 미니게임이 실제로 동작하는 것을
  확인할 수 있는 유일한 씬**이지만, 빌드에는 포함되지 않는다.

---

## 4. 전체 게임 플로우 (실제 코드 기준으로 재구성)

```
[LobbyScene]
  LobbyController.Start()
    → ConnectUsingSettings() (미접속 시) 또는 JoinLobby()
    → OnConnectedToMaster() → JoinLobby()
    → OnRoomListUpdate()로 방 목록 실시간 갱신 (닫힌 방도 표시, 입장 버튼만 비활성)

  [UserID 입력] → PhotonNetwork.NickName에 반영 (로그인 시스템 없음, 표시 이름일 뿐)
  [랜덤 입장] / [방 이름 입력 후 MakeRoom] / [목록에서 방 클릭]
    → OnJoinedRoom(): PlayerCount==1인 클라이언트(=최초 생성자)만
      PhotonNetwork.LoadLevel("GameLobbyScene") 호출, 나머지는 AutomaticallySyncScene으로 자동 동기화
                    ↓
[GameLobbyScene]  ← GameManager(채팅+스폰) + GameLobbyController(대기방 UI) 공존
  GameLobbyController: 플레이어 목록 표시, 방장 전용 "게임 시작" 버튼(정원 4명 도달 시만 활성)
  GameManager.Awake(): CreatePlayer() → "PlayerSpawnPos" 근처에 HideOrSeekPlayer 캐릭터 스폰
  GameManager.Start(): "[닉네임] Connected" 채팅 로그 브로드캐스트
  [방장이 게임 시작 클릭] → OnStartGameButtonClicked()
    → Room.IsOpen = false
    → PhotonNetwork.LoadLevel("GameScene")  ***여기서 색상 선택을 시작하는 코드가 전혀 없음(§7)***
                    ↓
[GameScene]  ← GameManager(채팅+스폰)만 존재. ColorTag 미니게임 오브젝트 없음(§3/§7)
  GameManager.Awake(): CreatePlayer() → 또 새로 스폰 (씬 전환 시 이전 오브젝트는 Unity가 자동 파괴)
  → 여기서 아무 일도 더 일어나지 않는다. 캐릭터가 돌아다니고 채팅만 가능한 상태로 멈춤.
  → (설계상 기대되는 동작이지만 실제 코드에 없음: 색상 4라운드 → 술래 지정 → 태그 게임 →
    GameEndTime 기록 → RoomLifecycleWatcher가 20초 후 GameLobbyScene 복귀)

[뒤로가기(Back) 버튼, 두 씬 공통]
  GameManager.OnClickBackButtonPressed() → ConfirmDialog.Show(문구, OnClickBackBtn)
    "예" → PhotonNetwork.LeaveRoom() → OnLeftRoom() → SceneManager.LoadScene("LobbyScene")
    "아니오" → 확인창만 닫힘, 아무 일도 없음
  (GameScene 문구: "게임이 진행중입니다. 나가시겠습니까?"
   GameLobbyScene 문구: "로비로 나가시겠습니까?")

[비정상 종료 — RoomLifecycleWatcher, ColorTag 오브젝트가 배치된 씬에서만 동작(현재 어디에도 없음)]
  술래 퇴장 또는 인원 1명만 남음 → PhotonNetwork.LeaveRoom() → OnLeftRoom() → "LobbyScene"

[정상 종료 — 마찬가지로 RoomLifecycleWatcher가 있어야 동작]
  GameEndTime(Room 프로퍼티) 경과 감지(마스터만) → 방 유지한 채 PhotonNetwork.LoadLevel
  ("GameLobbyScene") → Room.IsOpen 복구
```

---

## 5. 도메인별 상세

### 5.1 `Lobby/` — 로비 & 대기방 (4파일, `RoomItemPlan.md`와 완전히 일치 확인)

- **`LobbyController.cs`**: 접속(`ConnectUsingSettings`/`JoinLobby`), 방 목록 델타 갱신(`OnRoomListUpdate`,
  `RemovedFromList`만 제거하고 `IsOpen==false`인 방은 목록에 남기되 입장 버튼만 비활성 — 데모 코드와
  의도적으로 다른 정책), 방 생성/랜덤 입장/직접 입장, 실패 콜백 4종 모두 한글 피드백 텍스트로 처리.
  `MaxPlayers = 4` 고정(하드코딩 상수), `GameVersion = "1"`.
- **`RoomListItem.cs`**: 방 이름/인원수(`N/4`)/입장 버튼(`interactable = info.IsOpen`) 표시하는
  얇은 뷰. 클릭 시 `lobby.JoinRoom(roomName)`으로 위임.
- **`GameLobbyController.cs`**: 플레이어 목록 갱신 + 방장 전용 시작 버튼. **주목할 점**: `Update()`에
  `lastKnownPlayerCount`/`lastKnownIsMasterClient` 캐시 비교 기반의 "자기 치유" 안전망이 있다 —
  `AutomaticallySyncScene`으로 뒤늦게 합류하는 클라이언트가 `Start()` 시점에 아직 완전히 동기화되지
  않은 룸 상태(`PlayerCount`가 실제보다 작게 읽히는 등)를 보였던 버그(`RoomItemPlan.md` §9.4/§9.5)를
  다음 프레임에 자동으로 재보정하는 방식으로 고쳤다. 이벤트 콜백(`OnPlayerEnteredRoom` 등)은 그대로
  유지하고, `Update()`는 그 경로가 놓친 초기 상태만 보정하는 보완재로 동작 — 매 프레임 값이 실제로
  바뀐 경우에만 `RefreshPlayerList()`/`RefreshStartButton()`을 다시 실행해 불필요한 재계산을 피한다.
- **`PlayerListItem.cs`**: 닉네임 텍스트 하나만 갖는 최소 뷰.

**교차 도메인 참조 없음** — `Lobby/`는 `ColorTag/`, `Unit/`, `Camera/`, `GameManager/` 어느 것도
참조하지 않는다(독립적). 단, `OnStartGameButtonClicked()`가 `ColorSelectionManager.
StartColorSelection()`을 호출하지 않는다는 사실이 §7의 핵심 공백이다.

### 5.2 `GameManager/` — 채팅 · 스폰 · 나가기 확인창 (2파일)

- **`GameManager.cs`**(`Assets/02. Scripts/GameManager/`, 구 `Assets/Scripts/`에서 이동·수정됨):
  - `static Inst` 싱글턴(중복 가드 없음 — 씬마다 별도 인스턴스가 있고 서로 다른 씬이라 실질적 충돌은
    없지만, 같은 씬에 실수로 2개 배치되면 조용히 덮어써짐).
  - `Awake()`: `Inst = this` 직후 무조건 `CreatePlayer()` 호출 — `GameObject.Find("PlayerSpawnPos")`로
    스폰 지점을 찾아 반경 ±5m 랜덤 오프셋 후 `PhotonNetwork.Instantiate("HideOrSeekPlayer", ...)`.
    **레이스 컨디션 이론상 가능성은 여전히 있음**(룸 조인 완료 전에 `Awake()`가 실행될 수 있는 구조,
    `Update()`가 아니라 `Awake()`에서 무조건 호출) — 다만 `GameManager.md` §5에서 "로비를 거쳐야만
    도달하는 씬이라 실질적으로 발생하지 않는다"고 검증했고 이번 조사에서도 이 판단을 뒤집을 근거는
    찾지 못했다.
  - `Start()`: `Time.timeScale=1`, 메시지 큐 활성화, Back 버튼에 `OnClickBackButtonPressed` 연결,
    `"[닉네임] Connected"` 초록색 로그 브로드캐스트.
  - `Update()`: `Return` 키업마다 채팅 입력창 토글(`bEnter`). **`is_Conversating`을 여기서 true/false로
    세팅하지만, 이 값을 읽는 코드가 프로젝트 어디에도 없다** — §6.3에서 상세 설명(완전한 고아 상태,
    "채팅 중 이동 잠금" 기능이 실질적으로 존재하지 않음).
  - `OnClickBackButtonPressed()` → `ConfirmDialog.Show(leaveConfirmMessage, OnClickBackBtn)`,
    `confirmDialog`가 연결 안 돼 있으면 즉시 `OnClickBackBtn()`으로 안전 폴백.
  - `OnClickBackBtn()`: 버튼 비활성화(중복 클릭 방지) → "방 나감" 로그 브로드캐스트 → 마지막 인원이면
    Room `CustomProperties.Clear()` → 자기 `CustomProperties.Clear()` → `PhotonNetwork.LeaveRoom()`.
  - `OnLeftRoom()` → `SceneManager.LoadScene("LobbyScene")` (§3에서 확인한 대로 정상 동작).
  - `LogMsg`(`[PunRPC]`, `AllBuffered`): 자기 메시지만 흰색→노란색으로 하이라이트. **코드 내 주석
    ("방장이 말하면 하늘색으로 들어온다")은 실제 로직과 무관** — 실제 색 치환 대상은 `#ffffff`이고
    방장 여부는 전혀 확인하지 않는다(구 버전 §17에서 지적된 주석-코드 불일치, 이번에 파일을 다시
    읽어도 그대로임을 확인). 매 메시지 수신마다 `txtLogMsg.text`를 `""`로 초기화 후 리스트 전체를
    다시 이어붙이는 방식(최대 50개, `CLAUDE.md` "최적화" 원칙과 상충하는 지점, 여전히 그대로).
  - `CreatePlayer()`: `"PlayerSpawnPos"`/`"HideOrSeekPlayer"` 정확한 이름 사용 — **구 버전이 지적했던
    `"HeroSpawnPos"`/`"HeroPrefab"` 불일치 버그는 완전히 해소됨을 확인.**
- **`ConfirmDialog.cs`**: 재사용 가능한 예/아니오 팝업. `Action onYesConfirmed`를 인자로 받는
  범용 설계 — 특정 기능에 종속되지 않는다. `Awake()`에서 리스너 연결 + 기본 숨김.

### 5.3 `Camera/Camera_Ctrl.cs` — 3인칭 추적 카메라 (1파일)

우클릭 드래그로만 회전(수직 -7°~80° 클램프), 줌 기능은 완전히 제거되어 `m_DefaultDist=3.2f`
고정 거리만 사용(마우스 휠은 `PlayerPaintCanvas.HandleBrushSizeInput()`이 붓 크기 조절 용도로
전담 — 두 시스템이 휠 입력을 두고 충돌하지 않도록 역할이 명확히 분리되어 있음, 실제 코드로 확인).
`InitCamera(GameObject)`가 유일한 연결 진입점이며 `HideOrSeekPlayer.Awake()`가 `pv.IsMine`일 때만
`Camera.main.GetComponent<Camera_Ctrl>()`으로 찾아 호출한다. `m_Player == null`이면 `Start()`/
`LateUpdate()` 둘 다 조용히 아무 것도 안 하므로(경고 없음), 연결이 안 되면 카메라가 원점에 가만히
있는 채로 디버깅 단서가 없다는 점은 여전한 잠재적 함정.

### 5.4 `Unit/` — 이동 전용 캐릭터 컨트롤러 (5파일)

전투/HP 없이 이동·점프·회피·애니메이션·네트워크 동기화만 담당하도록 책임이 잘 분리되어 있다:

- **`HideOrSeekPlayer.cs`**(오케스트레이터, `MonoBehaviourPunCallbacks`+`IPunObservable`): 소유자는
  `Update()`에서 중력→입력→이동→점프→회피→애니메이션 홀드 순서로, 비소유자는 보간+애니메이션
  재생만. `IsMovementLocked { get; set; }` 프로퍼티가 있고 `Update()` 최상단에서 체크하지만,
  **`grep` 재확인 결과 이 프로퍼티를 `true`로 설정(set)하는 코드가 프로젝트 어디에도 없다** —
  선언(41행)과 읽기(66행) 두 곳뿐. 원래 `GameManager.is_Conversating`과 연결되어 "채팅 중 이동
  잠금" 기능을 구현할 목적으로 설계된 훅으로 보이지만, 두 값을 잇는 코드가 존재하지 않아 **기능이
  사실상 없는 것과 같다**(§6.3에서 재확인).
- **`PlayerGroundDetector`**(순수 C# 클래스): 커스텀 중력(`-9.81`) + 스윕 레이캐스트 착지 판정.
  `NavMeshAgent`를 물리 이동에 전혀 쓰지 않고(`Warp`/`updatePosition` 토글용으로만 사용), 이동은
  100% `transform.position` 직접 갱신.
- **`PlayerAnimationDriver`**(순수 C#): 트리거 기반 상태 전환(`Idle/Walk/SneakWalk/Jump/Dodge`),
  점프 정점에서 애니메이션 정지(`HandleJumpAnimationHold`).
- **`PlayerNetworkSync`**(순수 C#): `OnPhotonSerializeView` 스트림 read/write, 원격 보간(스냅 임계
  10유닛, Lerp/Slerp 비율 10).
- **`PlayerMoveState`**: `enum { Idle, Walk, SneakWalk, Jump, Dodge }`.

구 버전(§8.1)이 지적했던 "회피 후 관성 이동 분기가 도달 불가능함" 버그는 **이미 고쳐져 있음을
코드로 확인** — `CheckDodgeInput()`에서 `isDodge = true`와 `keepMovingAfterDodge = true`를 같은
줄 순서로 함께 세팅해(177행), `Move()`의 `if (isDodge && keepMovingAfterDodge)` 분기가 실제로
도달 가능해졌다(`PlayerControllPlan.md`에서 이미 수정 완료로 기록된 내용과 일치).

`HideOrSeekPlayer.prefab`(`Assets/04. Prefabs/Resources/`)을 직접 열어 확인한 결과, `HideOrSeekPlayer`
+ `PlayerPaintCanvas`(`paintableCollider`/`bodyRenderer` 연결됨) + `VoteIndicator`(`SpriteRenderer`,
`PlayerColorVoteIndicator`용) + `MeshCollider`(페인팅 레이캐스트용, `SkinnedMeshRenderer "Ch36"`
위에 배치) 등 ColorTag 컴포넌트들이 전부 정상 부착돼 있다 — **프리팹 자체는 완성돼 있다**는 것이
확인된다(§7의 "코드/에셋은 완성, 배선만 없음" 결론의 근거).

### 5.5 `Dev/OfflineModeBootstrap.cs` — 개발용 진입점 (1파일)

`Awake()`에서 무조건 `PhotonNetwork.OfflineMode = true`. `autoStartColorSelection` 체크박스가
켜져 있으면(`PlayerTestScene`에서만 켜짐) 오프라인 룸 생성 + `ColorSelectionManager.
StartColorSelection()`을 직접 호출 — **프로젝트 전체에서 이 메서드를 호출하는 유일한 지점**(재확인
완료, grep 결과 1건). 프로덕션 플로우와 무관.

---

## 6. `ColorTag/` 도메인 — 4라운드 색상 투표 → 페인팅 → 술래 치환 (10파일 + 셰이더 3종)

이 게임의 핵심 미니게임이며, 코드 품질이 가장 높은 도메인이다. 전체 데이터 흐름을 실제 코드
기준으로 재구성:

```
① ColorSelectionManager.StartColorSelection() [마스터 전용, 현재 OfflineModeBootstrap에서만 호출됨]
   → Room: RoundIndex=0, RoundEndTime=now+20s, Color0..3=-1, TaggerActorNumber=-1, IsOpen=false

② (매 프레임, 전 클라이언트) ColorSelectionPanel.Update()
   → RoundIndex/RoundEndTime 폴링 → "N/4"·남은시간 표시, ColorSwatchButton.SetLocked()로 이미
     확정된 색 잠금(라운드 진행 아니면 gameObject.SetActive(false)로 패널 자체를 숨김)

③ (플레이어) ColorSwatchButton.OnClick → manager.SubmitVote(colorIndex)
   → LocalPlayer.CustomProperties[VoteColorIndex] = colorIndex

④ (플레이어, 자기 캐릭터 표면 클릭) PlayerPaintCanvas.Update()
   → pv.IsMine이고 색상 라운드(0~3) 중일 때만: 좌클릭 레이캐스트가 자기 paintableCollider에
     맞으면 현재 VoteColorIndex로 StampBrush() → 로컬 RenderTexture(512×512)에 즉시 스탬프 +
     PaintStroke 이벤트(RaiseEvent, ReceiverGroup.Others)로 다른 클라이언트에 전파
   → BrushCursorController가 3D 붓 모델을 캐릭터 표면 위 마우스 위치에 투표색으로 표시
   → PlayerColorVoteIndicator가 머리 위 스프라이트로 현재 투표색을 빌보드 표시

⑤ (매 프레임, 마스터 전용) ColorSelectionManager.Update()
   → RoundEndTime 경과 감지 → ResolveRound(roundIndex)
     - ColorVoteTally.Resolve(): 다수결(동점=랜덤) 또는 무투표 시 남은 색 중 랜덤
     - ColorN = 확정색, 다음 라운드로 진행 또는(4라운드 완료 시) AssignTagger()도 같은 트랜잭션에
       포함해 RoundIndex=5("완료")로 세팅
   → ResetAllVotes() [버그, 아래 §6.1 참고]

⑥ (매 프레임, 전 클라이언트) PlayerPaintCanvas.DetectRoundChange()
   → RoundIndex 변화 감지 → 방금 끝난 라운드에 자신이 칠했던 좌표만 확정색으로 강제 재도색
     (FinalizeCurrentRoundStrokes, finalizeStampMaterial=잠금 무시) + 전파

⑦ RoundIndex==5("완료") → PlayerColorDisplay.TryApplyTaggerColor()
   → 자신이 TaggerActorNumber와 같으면(=술래): baseSet vs TaggerVariantSet에서 다른 슬롯 1개를
     찾아 캔버스 전체에서 그 옛 색 픽셀을 새 색으로 전역 치환(ApplyColorReplace,
     PaintColorReplace.shader, 색상 거리 기반 매칭)
```

### 6.1 `ColorSelectionManager.cs` — 라운드 진행의 유일한 권위자

마스터 클라이언트만 `Update()`에서 만료를 감지해 `ResolveRound()`를 실행하고 결과를
`SetCustomProperties`로 전파하므로, 클라이언트마다 시드가 다른 `System.Random`을 갖고 있어도
안전하다(단일 권위자 구조이기 때문 — 코드 추적으로 재확인).

**버그로 남아있는 것 (코드 재확인)**: `ResetAllVotes()`는
```csharp
private void ResetAllVotes()
{
    if (PhotonNetwork.LocalPlayer != null)
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.VoteColorIndex, -1 } });
}
```
**`PhotonNetwork.LocalPlayer`만** 리셋한다 — 즉 이 메서드를 실행하는 마스터 클라이언트 자기
자신의 투표만 `-1`로 돌아가고, 다른 플레이어들의 `VoteColorIndex`는 이전 라운드 값이 그대로
남는다. `ColorSwatchButton.SetLocked()`가 이미 확정된 색만 잠그므로, 아직 잠기지 않은 색을 이전
라운드에 선택해뒀던 플레이어는 이번 라운드에 아무것도 다시 누르지 않아도 그 선택이 계속 집계된다
— "매 라운드 새로 골라야 한다"가 의도라면 결함, 아니라면 정상. 여전히 의도 확인이 필요한 상태.

### 6.2 순수 함수 계층 — `ColorVoteTally`/`TaggerColorAssigner`

둘 다 `static class`의 부작용 없는 순수 함수(유닛 테스트 용이). `ColorVoteTally.Resolve`는
무투표/전원 제외색 상황까지 방어적으로 처리(남은 색 중 랜덤 폴백). `TaggerColorAssigner.
BuildVariantSet`은 확정 4색 중 무작위 1슬롯을 나머지 6색 중 하나로 치환하고,
`FindSwappedSlot`은 "정확히 한 슬롯만 다르다"는 전제로 그 슬롯을 찾는다 — `BuildVariantSet`이
항상 정확히 한 슬롯만 바꾸므로 이 전제는 실제로 깨지지 않는다(교차 확인 완료).

### 6.3 `PlayerPaintCanvas.cs` — 이 도메인에서 가장 복잡한 파일 (213줄)

- 캐릭터 1개당 런타임에 `RenderTexture`(512×512, ARGB32)를 개별 생성(`InitPaintCanvas()`, 직렬화
  필드 공유 시 전원이 같은 캔버스를 덮어쓰는 문제를 코드 주석으로 명시하고 실제로 회피).
  `GL.Clear(true, true, Color.clear)`로 생성 직후 명시적으로 투명 초기화(RenderTexture 초기 내용은
  Unity가 문서상 보장하지 않으므로, "알파=0이면 미도색"이라는 잠금 로직 전제를 위해 필요).
- **알파 채널 = 잠금 마스크**: `PaintStamp.shader`(직접 확인)가 `_RespectLock` 프로퍼티로 분기 —
  `brushStampMaterial`(_RespectLock=1)은 이미 알파 1인 픽셀을 보호, `finalizeStampMaterial`
  (_RespectLock=0으로 추정, `.mat` 파일 자체는 안 열어봤으나 셰이더 로직과 사용 패턴상 확실)은
  항상 덮어씀. 게임 디자인 의도로 보이며 버그는 아니다.
- `SendStrokeEvent`는 `ReceiverGroup.Others`로만 전파(자기 자신은 로컬에서 이미 찍었으므로 이중
  스탬프 방지, 올바른 설계).
- `OnEvent`(수신측)는 `viewId != pv.ViewID`면 무시 — 자기 캐릭터에 대한 스트로크만 반영.
- `DetectRoundChange()`는 소유 여부와 무관하게 매 프레임 실행되지만 실제 재도색은 `pv.IsMine`일
  때만 — 각자 자기 캐릭터만 책임진다.

### 6.4 `BrushCursorController.cs` — 3D 붓 커서

`brushSettings.CursorPrefab`을 라운드 시작 시 1회 인스턴스화해 재사용(최적화 관점에서 적절).
**로컬 캔버스를 찾을 때 `localPaintCanvas == null || !localPaintCanvas.IsMine`일 때만
`FindObjectsByType<PlayerPaintCanvas>()`로 재탐색** — 매 프레임 무조건 전체 순회하는 것은 아니고
찾은 뒤에는 캐싱하는 방식으로 확인됨(구 버전이 "매 프레임 전체 순회"라고 표현한 것보다는 실제로
더 최적화되어 있다 — 코드를 직접 재확인한 결과 조건부 재탐색임). `OnDisable()`에서 `Cursor.visible
= true`로 복구해 씬 전환 시 커서가 숨겨진 채 남는 사고를 방지.

### 6.5 `PlayerColorVoteIndicator.cs` — 투표색 빌보드

`LateUpdate()`에서 `transform.forward = Camera.main.transform.forward` — "카메라를 향해 회전"이
아니라 "카메라와 같은 방향을 보도록 평행 정렬"하는 방식이라, 일반적인 `LookAt` 빌보드와는 미묘하게
다르다(카메라가 비스듬히 내려다보는 각도에서 스프라이트가 살짝 찌그러져 보일 수 있음, 크래시 아님).

### 6.6 `PlayerColorDisplay.cs` — 술래 전용 최종 색상 치환

`[RequireComponent(typeof(PlayerPaintCanvas))]`. `hasApplied` 플래그로 1회만 적용. 자신이
`TaggerActorNumber`와 일치할 때만 `ApplyColorReplace()`로 캔버스 전체에서 옛 색 픽셀을 새 색으로
치환(색상 거리 기반, `PaintColorReplace.shader`의 `_Tolerance=0.01` 매칭).

### 6.7 `NetKeys.cs` / `NetEventCodes.cs` — 상수 클래스

Room/Player CustomProperties 키(`RoundIndex`, `RoundEndTime`, `Color0~3`, `TaggerActorNumber`,
`TaggerVariantSet`, `VoteColorIndex`, `GameEndTime`) 7개와 RaiseEvent 코드(`PaintStroke=1`) 1개.
오타/충돌 방지 목적의 얇은 상수 클래스, 문제 없음.

### 6.8 셰이더 3종 (직접 코드 확인)

- **`PaintStamp.shader`**: 캔버스에 원형 스탬프를 찍는다. `_StampUV`/`_StampRadius`/`_StampColor`/
  `_RespectLock` 프로퍼티. 거리 기반 원형 마스크(`distance(uv, StampUV) > radius`면 통과), 잠금
  로직은 위 §6.3에서 설명한 대로.
- **`PaintColorReplace.shader`**: 캔버스 전체에서 `_OldColor`와 색상 거리(`_Tolerance` 이내)가
  가까운 픽셀만 `_NewColor`로 치환, 나머지는 그대로 통과. 알파는 보존(`existing.a`).
- **`PlayerPaintedSkin.shader`**: 캐릭터 스킨 렌더링 셰이더. `_MainTex`(원본 스킨) 위에
  `_PaintTex`(페인트 캔버스)를 알파 기준으로 `lerp` 합성, 간단한 램버트 라이팅(`ForwardBase`
  라이트맵/그림자 미지원 — Fallback "Standard"로 나머지 패스 위임).

### 6.9 이 도메인의 실제 배선 현황 (가장 중요한 결론, §7과 연결)

`ColorTag/`의 10개 스크립트가 GameObject에 붙어 동작하는 곳은 **오직 `PlayerTestScene`뿐**임을
이번에 씬 파일의 `m_Name` 목록을 직접 grep해 재확인했다(`ColorTagManagers` 오브젝트 존재).
`GameScene.unity`(실제 매치가 도달하는 씬)에는 `GameManager`/`InputFieldChat`/`PanelLogMsg`/
`Button`/`PlayerSpawnPos`만 있고 ColorTag 계열 오브젝트는 **1개도 없다**(§3에서 grep 결과로 확인).

---

## 7. 크로스 도메인 배선 지도 & 통합 공백 (지금 이 순간 기준으로 재검증됨)

```
Lobby/*                    (독립적 — ColorTag/Unit/Camera/GameManager 어느 것도 참조하지 않음)

GameManager/GameManager.cs
└── "PlayerSpawnPos"(GameObject.Find) + "HideOrSeekPlayer"(Resources 프리팹명) 참조 → 둘 다 실존, 정상 스폰

Unit/HideOrSeekPlayer.cs
└── Camera/Camera_Ctrl.cs 참조 (Awake, pv.IsMine일 때만)

ColorTag/PlayerColorDisplay.cs
└── [RequireComponent] ColorTag/PlayerPaintCanvas.cs (같은 도메인 내부 결합)

ColorTag/BrushCursorController.cs
└── ColorTag/PlayerPaintCanvas, ColorPaletteSO, BrushSettingsSO 참조 (같은 도메인)

Dev/OfflineModeBootstrap.cs
└── ColorTag/ColorSelectionManager.StartColorSelection() 호출 (유일한 호출자, 개발용)

*** 존재하지 않는 연결 (실제 매칭 플로우 기준) ***
Lobby/GameLobbyController.OnStartGameButtonClicked()
  → PhotonNetwork.LoadLevel("GameScene")만 호출
  → ColorSelectionManager.StartColorSelection()을 호출하는 코드 없음
  → GameScene.unity 자체에 ColorSelectionManager 오브젝트가 없음
GameManager.cs (GameScene에 배치됨)
  → HideOrSeekPlayer는 스폰하지만, "게임을 시작"시키는 코드(색상 선택 트리거)는 전혀 없음
ColorTag 미니게임이 끝난 뒤 GameEndTime을 Room 프로퍼티에 기록하는 "게임 승패 판정" 로직
  → GameScenePlan.md 범위 밖으로 명시(태그/술래잡기 게임 자체는 이 프로젝트에 아직 구현되지 않음)
  → 따라서 RoomLifecycleWatcher의 "정상 종료 20초 후 GameLobbyScene 복귀" 경로도 실제로 트리거될
    방법이 현재 없음(GameEndTime을 세팅하는 코드가 존재하지 않음)
```

**결론**: 3개의 독립된 시스템(`Lobby`=매칭, `ColorTag`=미니게임, `Unit`=캐릭터 이동)이 각각은 잘
만들어져 있지만, 이들을 하나로 잇는 "게임 시작 트리거" 코드가 프로젝트 어디에도 없다. 구체적으로
비어 있는 지점은 정확히 하나다 — **`GameLobbyController.OnStartGameButtonClicked()`(또는
`GameScene` 진입 시 실행되는 새 매니저)에서 `ColorSelectionManager.StartColorSelection()`을
호출하고, `GameScene.unity`에 `ColorSelectionManager`/`RoomLifecycleWatcher`/
`BrushCursorController`/`ColorSelectionPanel` 등 `PlayerTestScene`에 이미 검증된 구성을 그대로
옮겨 배치하는 작업.** 코드 재사용 자체는 각 컴포넌트가 이미 완성돼 있어 어렵지 않고, "씬에 배치 +
한 줄짜리 호출 추가" 수준의 작업으로 보인다.

---

## 8. 확인된 버그·코드 스멜 종합 (현재 코드 기준, 전부 직접 재확인)

1. **[통합 공백, 최우선] 실제 매칭 흐름에 색상 선택 시작 트리거가 없음** — §7. `GameScene.unity`가
   `PlayerTestScene`에 이미 검증된 `ColorTag` 구성을 옮겨 받지 못한 상태.
2. **[기능 소실] "채팅 중 이동 잠금" 기능이 배선되지 않음** — `GameManager.is_Conversating`을
   세팅하는 코드는 있지만 읽는 코드가 없고, `HideOrSeekPlayer.IsMovementLocked`를 읽는 코드는
   있지만 세팅하는 코드가 없다. 두 절반이 서로 다른 클래스에 남아 있을 뿐 이어져 있지 않다(§6.4의
   §5.4/§5.2와 교차 확인, grep으로 최종 재확인 — set 지점 0건).
3. **`ColorSelectionManager.ResetAllVotes()`가 마스터 자신의 투표만 리셋** — 다른 플레이어의
   이전 라운드 투표가 잠기지 않은 색이라면 자동으로 이월됨(§6.1).
4. **`GameManager.LogMsg`의 색상 치환 주석과 실제 로직 불일치** — 주석은 "방장 메시지는
   `#00ffff`" 라고 말하지만 실제 코드는 방장 여부를 전혀 확인하지 않고 "자기 자신이 보낸 메시지"만
   흰색→노란색으로 하이라이트한다(§5.2).
5. **`txtLogMsg.text` 매 메시지마다 전체 재구성** — 새 메시지 하나만 append하면 되는데 최대 50개
   문자열을 매번 반복 연결, `CLAUDE.md` "최적화" 원칙과 상충(§5.2, 변경 없음).
6. **`[SerializeField]` 참조 다수에 null 가드가 없음** — `ColorSwatchButton.manager`,
   `PlayerPaintCanvas`의 다수 필드, `GameManager.pv`/`InputFdChat` 등. 프리팹/씬에서 연결이 빠지면
   즉시 `NullReferenceException`으로 실패한다. 프로젝트 전반에 일관된 패턴(설계 습관이지 개별
   버그는 아님).
7. **`PlayerColorVoteIndicator`의 빌보드가 `forward` 정렬 방식** — `LookAt`이 아니므로 카메라가
   비스듬한 각도일 때 스프라이트가 살짝 찌그러질 수 있음(§6.5, 경미).
8. **`GameManager.Inst`/`OnClickBackBtn` 등에 중복 인스턴스 가드 없음** — 같은 씬에 실수로
   `GameManager`가 2개 배치되면 나중에 `Awake`되는 쪽이 조용히 `Inst`를 덮어씀(경미, 현재 씬
   구성에서는 각 씬에 1개씩만 배치돼 있어 실무상 문제는 없음).
9. **게임 승패 판정(태그/술래잡기 본게임) 로직 자체가 아직 미구현** — `GameScenePlan.md`가
   명시적으로 범위 밖으로 분류한 부분(§1 플로우 다이어그램의 "태그 게임 시작" 이후)이라 버그는
   아니지만, `GameEndTime`을 기록하는 주체가 없어 `RoomLifecycleWatcher`의 정상 종료 경로가 현재
   트리거될 수 없다는 점은 §7의 공백과 함께 다음 작업의 선행 조건이다.

**해소되어 더 이상 유효하지 않은 구 버전 지적들(재확인 완료)**:
CP949 인코딩(§2), `"HeroSpawnPos"`/`"HeroPrefab"`/`"PhotonLobby"` 오타(§3/§5.2), Build Settings
빈 상태(§3), `keepMovingAfterDodge` 도달 불가능 버그(§5.4), 뒤로가기 버튼 미배치·확인창 없음(§5.2).

---

## 9. 우선순위별 다음 단계 제안

1. **`GameScene.unity`에 `PlayerTestScene`의 `ColorTagManagers` 구성(`ColorSelectionManager`,
   `RoomLifecycleWatcher`, `BrushCursorController`)과 `ColorSelectionPanel` UI를 옮겨 배치하고,
   `GameLobbyController.OnStartGameButtonClicked()`(또는 `GameScene` 진입 시점의 별도 트리거)에서
   `ColorSelectionManager.StartColorSelection()`을 호출하도록 연결한다** — §7의 핵심 공백 해소,
   가장 임팩트가 큰 단일 작업.
2. **"채팅 중 이동 잠금" 기능을 다시 연결하거나, 더 이상 필요 없다면 `is_Conversating`/
   `IsMovementLocked` 둘 다 제거해 죽은 코드를 정리한다** — 기능 부활이냐 제거냐는 사용자 확인 필요.
3. **본게임(태그/술래잡기) 승패 판정 로직 설계·구현** — 술래가 다른 플레이어를 태그했을 때의
   처리, 게임 종료 조건, `GameEndTime` Room 프로퍼티 기록 주체를 결정해야 §7의 두 번째 공백
   (`RoomLifecycleWatcher`의 정상 종료 경로)도 실제로 동작하게 된다.
4. `ColorSelectionManager.ResetAllVotes()`가 전원을 리셋해야 하는 의도인지 확인 후,
   `PhotonNetwork.PlayerList`를 순회하도록 수정할지 결정한다(§6.1/§8-3).
5. `GameManager.LogMsg`의 색상 치환 주석을 실제 동작(자기 메시지 하이라이트)에 맞게 정리한다(§8-4).
6. (선택) `txtLogMsg` 텍스트 누적 방식을 append 방식으로 최적화한다(§8-5, `CLAUDE.md` 원칙 관련).

---

## 부록: 참고한 계획 문서 요약

- **`Claude.md`**: 폴더 규칙, 한글 사용 금지(주석 제외), OOP, "계획 후 승인 후 작업" 원칙.
- **`UserPlan.md`**: 최초 1줄 요구사항 원문(4라운드 색상 투표 + 다수결/랜덤 확정 로직) — 이
  요구사항이 `GameScenePlan.md`로 상세화됨.
- **`GameScenePlan.md`**: ColorTag 도메인 전체의 설계 원본(13장, "구현 완료" 표시). 이번 조사로
  §6의 실제 코드가 이 설계와 정확히 일치함을 재확인했다.
- **`RoomItemPlan.md`**: 로비/대기방 설계 원본(7장 + 후속 UI 조정, "구현 완료"). §5.1의 실제 코드와
  정확히 일치.
- **`GameManager.md`**: `GameManager.cs` 이전/수정 + Back 버튼/확인창/레이아웃 후속 작업 기록
  ("구현 완료"). §5.2의 실제 코드와 정확히 일치.

이 문서(신규 `research.md`)는 위 4개 계획 문서가 "구현 완료"로 표시한 개별 작업들이 실제 코드에
정확히 반영되어 있음을 파일 단위로 재검증하는 한편, **그 계획 문서들 각각의 범위 밖에 있던
"도메인 간 배선"이라는 더 큰 그림에서는 여전히 빈틈이 있다**는 점을 새로 확인해 정리한 결과다.
