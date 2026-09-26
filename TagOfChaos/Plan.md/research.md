# 조사 보고서: TagOfChaos 프로젝트 전체 심층 분석 (2026-09-26, 커밋 `12415ee` 기준)

> **추가 개정(2026-09-26 19시)**: 사용자 주석(파괴된 쿠키 이름표 잔존, 괴물 맵 밖 낙하 시 미복귀)의 원인 분석을 §11에, §23~§29 반영 후 **전체 코드 확장성 검토**를 §12에 추가했다. §0~§10은 이전 판 내용 그대로다.
>
> **개정 이력**: 이 문서는 2026-09-25판(커밋 `b134164` 기준)을 **전면 대체**한다. 이전 판은 git
> 히스토리(`git show 12415ee:TagOfChaos/Plan.md/research.md`)에 그대로 남아 있다.
>
> **이번 판의 조사 방법**
> - `Assets/02. Scripts/` 아래 **8개 도메인, 51개 `.cs`(총 3,145줄)를 라인 단위로 전부 정독**했다.
> - 씬 4개(`LobbyScene`/`GameLobbyScene`/`GameScene`/`PlayerTestScene`)를 YAML 단위로 파싱해
>   **스크립트 GUID → 부착된 GameObject 경로**까지 역추적했다.
> - 프리팹 2개(`HideOrSeekPlayer`, `MonsterPlayer`), Animator Controller 2개(파라미터·상태·전이
>   조건까지), 셰이더 4개, 스탬프 머티리얼, ScriptableObject 2개, `TagManager`/`DynamicsManager`/
>   `EditorBuildSettings`를 직접 확인했다.
> - 레이어 변경 같은 회귀는 `git log`로 도입 커밋까지 확인했다.
>
> **이전 판과 비교한 핵심 차이** (상세는 §9)
> 1. **새로 확인한 치명 결함 5건**: 결과 화면·괴물 이탈 배너가 **절대 표시되지 않음**(§8.1),
>    색칠 UI 패널이 **첫 프레임에 스스로 꺼져 다시 켜지지 않음**(§8.2), 붓 레이캐스트가 **루트
>    캡슐에 막혀 색칠 불가**(§8.3, 커밋 `403f4b6` 회귀), 괴물 GrabKill이 **게임당 1회만 동작**
>    (§8.4), 쿠키 그랩의 `cookieLayer`가 `Nothing`이라 **그랩 불가**(§8.6).
> 2. 이전 판 §6.14("루트는 Default(0), `Mesh_0`만 Cookie(9)")는 **사실과 반대**였다. 실제로는
>    **루트가 Cookie(9), `Mesh_0`이 Default(0)** 이다. 이 사실이 §8.3 결함의 원인이다.
> 3. 이전 판 §6.15의 `PlayerMonster.prefab`은 **이미 삭제됐다**(현재 `Resources/`에 없음).
>
> 결론부터 말하면, **현재 빌드로는 게임 한 판을 처음부터 끝까지 정상적으로 진행할 수 없다.**
> 대기실 → 괴물 선정 → 게임 씬 진입까지는 동작하지만, 색칠 페이즈 UI가 뜨지 않고(§8.2), 색칠
> 레이캐스트도 막혀 있으며(§8.3), 괴물은 1인칭 카메라가 없고(§8.5), 처치는 한 번만 되며(§8.4),
> 승패가 판정돼도 결과 화면이 뜨지 않아 로비로 돌아가지 않는다(§8.1).

---

## 목차

- §0 프로젝트 개요
- §1 전체 게임 흐름 (씬 전환 + 타임라인)
- §2 네트워크 계약 (Room/Player Props, 이벤트, RPC, 직렬화)
- §3 도메인별 상세 동작 (51개 `.cs`)
- §4 씬 배선 실측 (GameObject 경로)
- §5 프리팹 · 애니메이터 · 셰이더 · SO 실측
- §6 핵심 메커니즘 심층 분석
- §7 아키텍처 평가
- §8 발견된 문제 (우선순위순)
- §9 이전 research.md 대비 정정 사항
- §10 권장 다음 단계

---

## 0. 프로젝트 개요

| 항목 | 값 |
|---|---|
| 엔진 | Unity **6000.0.58f2** (Unity MCP 인스턴스 `TagOfChaos@d988267a`로 확인) |
| 렌더 파이프라인 | Built-in RP (커스텀 셰이더가 `CGPROGRAM` + `LightMode=ForwardBase` 사용) |
| 네트워크 | **Photon PUN2** (`Assets/Photon/`), `AutomaticallySyncScene = true`, `GameVersion = "1"` |
| 입력 | 레거시 `UnityEngine.Input`만 사용 (`com.unity.inputsystem` 1.14.2가 설치돼 있고 `InputSystem_Actions.inputactions`도 있지만 **코드에서 쓰지 않음**) |
| 주요 패키지 | unity-mcp(CoplayDev), ai.navigation 2.0.14, probuilder 6.1.2, timeline 1.8.9, ugui 2.0.0, visualscripting 1.9.7 |
| 인원 | `MaxPlayers = 4` 고정, 정원이 차야 시작 가능 |
| 장르 | 쿠키(3인칭, 최대 3명)가 괴물(1인칭, 1명)에게서 10분 동안 살아남는 비대칭 술래잡기. 시작 전 60초 동안 자기 몸을 색칠해 위장한다. |

### 0.1 폴더 구조 (게임 코드와 에셋만)

```
TagOfChaos/
├─ Assets/
│  ├─ 02. Scripts/           ← 게임 코드 전부 (asmdef: TagOfChaos.Scripts)
│  │  ├─ Core/        (1)  SceneNames
│  │  ├─ Lobby/       (5)  LobbyController, GameLobbyController, RoomListItem, PlayerListItem, PlayerSkinSelector
│  │  ├─ GameManager/ (5)  GameManager(채팅), PlayerSpawner, RoomExitController, ConfirmDialog, VoidKillZone
│  │  ├─ Unit/        (8)  HideOrSeekPlayer(쿠키) + 협력 클래스 5개 + PlayerGrabController, PlayerBillBoard, PlayerSkinApplier
│  │  ├─ Camera/      (1)  Camera_Ctrl(쿠키 3인칭)
│  │  ├─ ColorTag/   (12)  색칠 시스템 + NetKeys/NetEventCodes/RoomState + Shaders/(셰이더 4, 머티리얼 4)
│  │  ├─ Monster/    (17)  괴물·게임 룰·결과·관전
│  │  └─ Dev/         (2)  OfflineModeBootstrap, MonsterTestSpawner
│  ├─ 03. SO/ColorTag/       DefaultBrushSettings, DefaultColorPalette
│  ├─ 04. Prefabs/           PlayerResultRow.prefab
│  │  └─ Resources/          HideOrSeekPlayer.prefab, MonsterPlayer.prefab, BrushCursor.prefab, Brush.fbx
│  ├─ 05. Materials/         Character(스킨 A/B/C), Environment, TestGround
│  ├─ 06. Physics/           PlayerNoFriction(.physicMaterial) + _tmp 사본
│  ├─ 07. Expression/        얼굴 표정 PNG 20장 (코드 참조 없음, 미사용)
│  ├─ Animation/             Cookie/*.fbx, Monster/{NewAnimation,OldAnimation,textures}, 두 Animator Controller
│  ├─ Resources/UI/          LobbyPanel, GameLobbyPanel, ColorSelectionPanel, RoomListItem, PlayerListItem, ConfirmDialog
│  ├─ Scenes/                LobbyScene, GameLobbyScene, GameScene, PlayerTestScene, SampleScene
│  └─ NatureStarterKit2/, Photon/, TextMesh Pro/, Fonts/(NotoSansKR SDF)
├─ Plan.md/                  설계·계획 문서 (이 파일 포함)
└─ ../TagOfChaosGame/        빌드 결과물 (git에 커밋돼 있음)
```

### 0.2 빌드 씬 (`EditorBuildSettings.asset`)

| 인덱스 | 씬 | 활성 |
|---|---|---|
| — | `PlayerTestScene` | **비활성** (개발 전용) |
| 0 | `LobbyScene` | ✅ |
| 1 | `GameLobbyScene` | ✅ |
| 2 | `GameScene` | ✅ |

`SampleScene`은 빌드에 없고 프로젝트 스크립트도 없는 빈 씬이다.

### 0.3 레이어 (`TagManager.asset`)

| 인덱스 | 이름 | 실제 사용처 |
|---|---|---|
| 0 | Default | 지형, `HideOrSeekPlayer/Mesh_0`(붓칠 대상 MeshCollider) |
| 8 | PlayerCapsule | 코드에서 붓 레이캐스트 제외용으로 참조하지만 **현재 이 레이어에 있는 오브젝트가 없음** (§8.3) |
| 9 | Cookie | `HideOrSeekPlayer` 루트(CapsuleCollider + Rigidbody) |
| 10 | Monster | `MonsterPlayer` 루트 |

충돌 매트릭스는 전부 `ff…`(모든 레이어가 서로 충돌)이고, `queriesHitBackfaces = 0`이다. 태그는 쓰지
않는다(`tags: []`).

---

## 1. 전체 게임 흐름

### 1.1 씬 전환 다이어그램

```
[LobbyScene] ── LobbyPanel(LobbyController)
   │ ConnectUsingSettings → JoinLobby → 방 목록 표시
   │ 방 만들기 / 랜덤 입장 / 목록에서 입장
   │ OnJoinedRoom: PlayerCount==1(방을 만든 사람)만 LoadLevel(GameLobby)
   ▼   나머지는 AutomaticallySyncScene으로 자동 이동
[GameLobbyScene] ── 대기실 + 가마솥
   │ PlayerSpawner → 쿠키(HideOrSeekPlayer) 스폰, 자유 이동
   │ PlayerSkinSelector → SkinIndex(A/B/C) 선택
   │ Cauldron 트리거 → ClaimMonster 이벤트 → 마스터가 선착순으로 확정
   │   (씬 진입 후 30초가 지나도 아무도 안 들어가면 무작위 배정)
   │ MonsterRevealController → "OO이(가) 괴물이 되었습니다!" 배너
   │ 방장 + 정원(4/4) → "게임 시작" → IsOpen=false, LoadLevel(Game)
   ▼
[GameScene]
   │ t=0     GamePhaseStarter(마스터): PaintPhaseEndTime = Now+60
   │         PlayerSpawner: 괴물이 아닌 사람만 쿠키 스폰 (괴물은 60초 동안 아바타 없음)
   │ 0~60초  쿠키: 자기 몸 색칠(PlayerPaintCanvas), 붓 커서(BrushCursorController)
   │ t=60    PaintPhaseController(마스터): 등록 슬롯 0개인 플레이어에게 강제 도포 색 배정
   │         MonsterJoinController(마스터): MonsterJoined=1, GameEndTime = Now+600
   │         MonsterJoinController(괴물 본인): MonsterPlayer를 MonsterSpawnPos에 스폰
   │ 60~660초 술래잡기: 괴물이 전방 약 3m 트리거로 쿠키를 자동 처형(GrabKill)
   │         파괴된 쿠키 → 관전 모드(Space로 대상 순환)
   │ 판정    GameRuleController(마스터): 쿠키 전멸 → MonsterWins / 시간 종료 → CookiesWin
   │ 결과    ResultScreenController: 결과 표시, 12초 후 마스터가 LoadLevel(GameLobby)  ← §8.1 때문에 실제로는 표시 안 됨
   │ 이탈    RoomLifecycleWatcher: 괴물이 전원 나가면 5초 경고 후 GameLobby로 복귀
   ▼
[GameLobbyScene] (같은 방 유지) ← Room Props가 초기화되지 않아 두 번째 판이 진행되지 않음(§8.7)

어느 씬에서든: Back 버튼 → ConfirmDialog → LeaveRoom → LobbyScene
```

### 1.2 GameScene 시간 흐름에서 각 컴포넌트의 책임

| 시점 | 마스터만 | 모든 클라이언트 | 소유자만 |
|---|---|---|---|
| 씬 로드 | `GamePhaseStarter`가 `PaintPhaseEndTime`을 설정 | `PlayerSpawner`가 InRoom이 될 때까지 대기 | — |
| 0~60초 | — | `ColorSelectionPanel`이 남은 시간과 슬롯 수 표시(§8.2), `BrushCursorController` | `PlayerPaintCanvas`의 입력·스탬프·이벤트 송신 |
| 60초 | `PaintPhaseController.ResolvePaintPhase()`, `MonsterJoinController.MasterTick()` | — | `PlayerPaintCanvas.ApplyForcedColorIfAssignedToMe()`, 괴물 스폰 |
| 60~660초 | `GameRuleController` 폴링 | — | `MonsterController`, `MonsterGrabKillTrigger`, `HideOrSeekPlayer`, `SpectatorController` |
| 괴물 이탈 | `RoomLifecycleWatcher` | `MonsterDepartureBanner`(§8.1) | — |
| 종료 | `ResultScreenController.OnLobbyButtonClicked` → `LoadLevel` | `ResultScreenController`(§8.1) | — |

**설계 패턴**: 모든 게임 진행은 "마스터가 Room CustomProperties에 **절대 시각(`PhotonNetwork.Time`
기준 double)** 을 기록하고, 모든 클라이언트가 `Update()`에서 폴링하거나 `OnRoomPropertiesUpdate`로
반응하는" 구조로 통일돼 있다. 타이머를 따로 동기화하지 않고 서버 시각만으로 판정하므로, 마스터가
바뀌어도 새 마스터가 같은 값을 읽어 이어서 진행할 수 있다.

---

## 2. 네트워크 계약

### 2.1 Room CustomProperties (`ColorTag/NetKeys.cs`)

| 키 | 타입 | 쓰는 쪽 | 읽는 쪽 | 상태 |
|---|---|---|---|---|
| `MonsterActorNumbers` | `int[]` | `MonsterAssignmentAuthority.ConfirmMonster`, `RoomLifecycleWatcher.RemoveFromMonsterList` | `MonsterRevealController`, `PlayerSpawner`, `MonsterJoinController`, `GameRuleController`, `ResultScreenController`, `RoomLifecycleWatcher` | 사용 중 |
| `MonsterRevealTime` | `double` | `MonsterAssignmentAuthority` | **읽는 곳 없음** | 죽은 키 |
| `PaintPhaseEndTime` | `double` | `GamePhaseStarter` | `PlayerPaintCanvas`, `BrushCursorController`, `ColorSelectionPanel`, `PaintPhaseController`, `MonsterJoinController` | 사용 중 |
| `ForcedPaintActorNumbers` | `int[]` | `PaintPhaseController` | `PlayerPaintCanvas` | 사용 중 |
| `ForcedPaintColors` | `int[]` (위와 인덱스 1:1) | `PaintPhaseController` | `PlayerPaintCanvas` | 사용 중 |
| `MonsterJoined` | `int`(1) | `MonsterJoinController` | `MonsterJoinController`, `GameRuleController` | 사용 중 |
| `GameEndTime` | `double` | `MonsterJoinController` | `GameRuleController` | 사용 중 (화면 표시는 없음) |
| `GameResult` | `int`(`GameResult` enum) | `GameRuleController` | `GameRuleController`(중복 방지), `ResultScreenController` | 사용 중 |
| `MonsterDepartedAt` | `double` / `null` | `RoomLifecycleWatcher` | `MonsterDepartureBanner` | 사용 중 |
| `RoundIndex`, `RoundEndTime`, `Color0~3`(`ColorPrefix`), `TaggerActorNumber`, `TaggerVariantSet`, `VoteColorIndex`, `CookiesDeparted` | — | **없음** | `RoomState.GetRoundIndex()`만 참조(호출하는 곳 없음) | 구 4라운드 미니게임 잔재, 죽은 키 |

### 2.2 Player CustomProperties

| 키 | 타입 | 쓰는 쪽 | 읽는 쪽 |
|---|---|---|---|
| `SkinIndex` | `int`(0~2) | `PlayerSkinSelector` | `PlayerSkinApplier` |
| `RegisteredSlotCount` | `int`(0~4) | `PlayerPaintCanvas.ReportSlotCount` | `PaintPhaseController` |
| `HitCount` | `int`(0 또는 2) | `HideOrSeekPlayer.RequestGrabKill` | `PlayerCrackDisplay`, `GameRuleController`, `ResultScreenController`, `SpectatorController` |

### 2.3 RaiseEvent 코드 (`ColorTag/NetEventCodes.cs`)

| 코드 | 이름 | 송신 | 수신 | 페이로드 | 옵션 |
|---|---|---|---|---|---|
| 1 | `PaintStroke` | `PlayerPaintCanvas.SendStrokeEvent` | `PlayerPaintCanvas.OnEvent` (ViewID로 필터) | `object[]{viewId, u, v, radius, colorIndex, (byte)StrokeKind}` | Others, Reliable, **버퍼링 안 함** |
| 2 | `ClaimMonster` | `Cauldron.OnTriggerEnter` | `MonsterAssignmentAuthority.OnEvent` | `int actorNumber` | MasterClient, Reliable |
| 3 | `ClearColor` | `PlayerPaintCanvas.SendResetEvent` | `PlayerPaintCanvas.OnEvent` | `object[]{viewId}` | Others, Reliable |
| 4 | `FillAll` | **없음** | **없음** | — | 죽은 코드 (강제 도포는 `PaintStroke` + `StrokeKind.ForceFill`로 대체됨) |

`StrokeKind`: `Normal`(0) → `BrushStampMaterial`(잠금 존중), `Erase`(1) → `EraseStampMaterial`,
`ForceFill`(2) → `FinalizeStampMaterial`(잠금 무시).

### 2.4 RPC

| 메서드 | 위치 | 호출자 | 대상 | 비고 |
|---|---|---|---|---|
| `LogMsg(string, bool)` | `GameManager` | `GameManager`(입장·채팅), `RoomExitController`(퇴장) | `AllBuffered` | 채팅. 버퍼가 계속 쌓임 |
| `OnGrabbedByOwner(int carrierViewId)` | `HideOrSeekPlayer` | `PlayerGrabController.TryGrab` | 대상 쿠키 소유자 | 소유권 이전 없이 "들린 쪽"이 로컬에서 따라감 |
| `OnReleased(bool)` | `HideOrSeekPlayer` | `PlayerGrabController.Release` | 대상 쿠키 소유자 | `withThrow`는 받기만 하고 쓰지 않음 |
| `RequestGrabKill()` | `HideOrSeekPlayer` | `MonsterGrabKillTrigger.OnTriggerEnter` | `All` (소유자만 처리) | 파괴 확정은 피해자 본인 클라이언트가 함 |

### 2.5 IPunObservable (`OnPhotonSerializeView`)

| 컴포넌트 | 송신 내용 | 수신 측 처리 |
|---|---|---|
| `HideOrSeekPlayer` → `PlayerNetworkSync` | position, rotation, `(int)PlayerMoveState`, `isJump` | `Update()`에서 Lerp(10/s)·Slerp, 10m 넘게 벌어지면 순간이동, 상태는 `ChangeState`로 애니메이터에 반영 |
| `MonsterController` → `MonsterNetworkSync` | position, rotation, `(int)MonsterMoveState` | 위와 같음 |

`RemoteIsJump`는 수신만 하고 쓰지 않는다. 원격 인스턴스는 `rb.isKinematic = true`로 물리를 끄고
transform을 직접 보간한다.

### 2.6 소유권 원칙

- 모든 캐릭터는 `PhotonNetwork.Instantiate`로 **본인이 소유**하고, 소유권 이전(`TransferOwnership`)은
  한 번도 쓰지 않는다.
- 남의 상태를 바꿀 때는 항상 "소유자에게 RPC로 요청 → 소유자가 자기 Player Props를 기록"한다.
  (`RequestGrabKill`, `OnGrabbedByOwner`)
- 게임 진행 판정은 전부 마스터 전용 `Update()` 폴링으로 처리한다.

---

## 3. 도메인별 상세 동작

### 3.1 `Core/`

- **`SceneNames`** (`Core/SceneNames.cs:4`): `Lobby="LobbyScene"`, `GameLobby="GameLobbyScene"`,
  `Game="GameScene"` 상수.

### 3.2 `Lobby/`

- **`LobbyController`** (`LobbyPanel` 프리팹에 부착, `LobbyScene`에 인스턴스로 배치)
  - `Awake`: `AutomaticallySyncScene = true`, `GameVersion = "1"`.
  - `Start`: 닉네임 기본값 `"Player"+1000~9999`. 연결 안 됨 → `ConnectUsingSettings`, 연결됨 →
    `JoinLobby`.
  - `OnRoomListUpdate`: `cachedRoomList` 딕셔너리를 증분 갱신하고, `RoomListItem`을 재사용·생성·삭제한다.
    닫힌 방(`IsOpen=false`)도 목록에 남기고 버튼만 비활성화한다.
  - 방 만들기: `RoomOptions{MaxPlayers=4}`. 랜덤 입장: `JoinRandomRoom`. 실패 콜백은 전부 한국어
    피드백 텍스트로 처리한다.
  - `OnJoinedRoom`: `PlayerCount == 1`일 때만 `LoadLevel(GameLobby)`를 호출하고, 나머지는 자동
    동기화로 따라간다.
- **`GameLobbyController`** (`GameLobbyPanel` 프리팹)
  - 플레이어 목록을 전부 지우고 다시 만드는 방식으로 갱신한다(`RefreshPlayerList`).
  - 시작 버튼은 방장에게만 보이고, `PlayerCount >= MaxPlayers`일 때만 누를 수 있다.
  - 늦게 입장한 클라이언트의 초기 상태 불일치를 막기 위해 `Update()`에서 인원수·방장 여부 변화를
    감지하는 안전망이 있다.
  - `OnStartGameButtonClicked`: `IsOpen=false` → `LoadLevel(Game)`. **UI Button의 onClick에 인스펙터로 연결된다.**
- **`RoomListItem`**: `Refresh(RoomInfo, LobbyController)`. 입장 버튼은 `lobby.JoinRoom(roomName)`을 호출한다.
- **`PlayerListItem`**: 닉네임 텍스트만 설정한다.
- **`PlayerSkinSelector`** (`GameLobbyScene/GameLobbyUICanvas/SkinSelectPanel`): A/B/C 버튼을 누르면
  `SkinIndex`(0/1/2)를 `LocalPlayer`에 기록한다.

### 3.3 `GameManager/`

- **`GameManager`** — 사실상 **채팅 전용**이다. `static Inst` 싱글톤이지만 외부에서 참조하는 곳은 없다.
  - Enter(`GetKeyUp(Return)`)로 입력창을 토글한다. 열 때 로컬 쿠키의 `IsMovementLocked = true`.
  - 메시지는 `LogMsg` RPC를 `AllBuffered`로 보내고, 최근 50개만 유지한다. 본인이 보낸 채팅은 흰색에서
    노란색으로 치환한다.
  - `Start`: `Time.timeScale = 1`, `IsMessageQueueRunning = true`. `InRoom`이 될 때까지 기다렸다가
    "Connected" 메시지를 보낸다.
  - 괴물 캐릭터는 `HideOrSeekPlayer`가 아니므로, 괴물은 채팅 중에도 이동이 잠기지 않는다.
- **`PlayerSpawner`** — `InRoom`이 될 때까지 코루틴으로 기다린 뒤, `MonsterActorNumbers`에 본인이
  없으면 `"PlayerSpawnPos"` 주변 ±5m에 `HideOrSeekPlayer`를 `PhotonNetwork.Instantiate`한다.
  `OfflineModeBootstrap.SpawnAsMonster`가 켜져 있으면 스폰하지 않는다. ViewID 진단 로그를 남긴다.
- **`RoomExitController`** — Back 버튼 → `ConfirmDialog.Show` → `OnClickBackBtn`: 퇴장 메시지 RPC,
  `CustomProperties.Clear()`(**로컬 사본만 지워짐**, §8.12), `LeaveRoom` → `OnLeftRoom`에서
  `LoadScene(Lobby)`.
- **`ConfirmDialog`** — 콜백을 인자로 받는 재사용 가능한 예/아니오 창. `Awake`에서 스스로 비활성화한다
  (일반 `MonoBehaviour`라서 문제없음. §8.1과 비교).
- **`VoidKillZone`** — 트리거에 들어온 로컬 쿠키에게 `RespawnToSpawnPoint()`를 호출한다.

### 3.4 `Unit/` — 쿠키 캐릭터

**`HideOrSeekPlayer`** (조정자 MonoBehaviour, 364줄)

협력 클래스(순수 C#)를 소유한다: `PlayerGroundDetector`, `PlayerAnimationDriver`, `PlayerNetworkSync`.

- **생명주기**
  - `Awake`: `networkSync`를 **IsMine 여부와 관계없이 가장 먼저 생성**한다(Start보다 먼저
    `OnPhotonSerializeView`가 호출되는 경쟁 조건 방지). 로컬이면 `Camera_Ctrl.InitCamera(gameObject)`.
  - `Start`: `applyRootMotion = false`. 로컬이면 Rigidbody를 `useGravity`, `ContinuousDynamic`,
    `Interpolate`, `FreezeRotation`으로 설정하고, 원격이면 `isKinematic`. 루트 캡슐과 `Mesh_0`의
    MeshCollider 사이 충돌은 `Physics.IgnoreCollision`으로 무시한다.
- **입력 (Update, 로컬)**: WASD는 카메라 기준 수평 방향으로 변환한다. LeftShift = 달리기(×1.3),
  Space = 점프 요청(FixedUpdate에서 적용), LeftCtrl = 회피(0.5초, 속도 ×2, 시작 방향으로 고정 이동).
  `IsMovementLocked`이면 Update 전체를 건너뛴다.
- **물리 (FixedUpdate, 로컬)**
  1. `TryFollowCarrier()`: 그랩당한 상태면 그랩한 쪽의 `CarrySocket` 위치로 `rb.position`을 이동하고 끝낸다.
  2. 접지 판정은 `PlayerGroundDetector.IsGrounded`(발밑 0.1m 위에서 아래로 0.4m 레이, `groundLayer=Default`).
  3. 착지: `isJump && grounded && vy <= 0`이면 `isJump=false`, 애니메이터 재생 속도를 복구한다.
  4. 점프: `vy = jumpPower(6)`, `ReplayJump()`. 발판에서 떨어져도 Jump 상태로 들어간다.
  5. `Move()`: 수평 속도만 `rb.linearVelocity`에 쓰고 수직 속도는 유지한다. 회전은 `rb.MoveRotation`으로 한다.
  6. `y < -100`이면 리스폰한다(최후 방어선).
- **RPC**: `OnGrabbedByOwner`(이동 잠금, `Held`), `OnReleased`(잠금 해제, `Idle`),
  `RequestGrabKill`(`HitCount=2` 기록, 이동 잠금, `Broken`, 관전 모드 진입).
- **리스폰**: `GameObject.Find("PlayerSpawnPos")` ±5m 위치로 이동. `rb.position`과 `transform.position`을 둘 다 갱신한다.
- 프리팹 값: `speed 5`, `jumpPower 6`, `groundCheckOffset 0.3`, `jumpFreezeNormalizedTime 0.5`,
  `dodgeDuration 0.5`.

**`PlayerAnimationDriver`**

- `ChangeState(state)`: 이전 상태와 같으면 무시한다. 다르면 `ResetTrigger(이전)` → `SetTrigger(새 상태)`.
  **enum 이름이 곧 Animator 트리거 이름**이라는 암묵적 계약이다.
- `ReplayJump()`: 트리거 대신 `animator.Play("Jump", 0, 0f)`로 즉시 전환하고, 다음 한 번은 정지
  판정을 건너뛴다(`suppressHoldCheckOnce`).
- `HandleJumpAnimationHold()`: Jump의 normalizedTime이 0.5 이상이면 `animator.speed = 0`으로 공중
  자세를 고정하고, 착지하면 `ResumePlayback()`으로 1로 되돌린다.
- `SetCarryLayerWeight()`: "Carry" 레이어를 찾는다. **이 레이어는 존재하지 않아 항상 아무 동작도 하지 않는다.**

**그 밖의 Unit 클래스**

| 클래스 | 역할 |
|---|---|
| `PlayerMoveState` | `Idle, Walk, Run, Jump, Dodge, Held, Broken` |
| `PlayerNetworkSync` | §2.5 참고 |
| `PlayerGroundDetector` | 레이캐스트 접지 판정만 담당 |
| `PlayerGrabController` | E키: 반경 1.5m `OverlapSphere(cookieLayer)` → 대상 소유자에게 `OnGrabbedByOwner` RPC. 한 번 더 누르면 `OnReleased`. **`cookieLayer = 0(Nothing)`** (§8.6) |
| `PlayerBillBoard` | 머리 위 닉네임(`TextMeshPro`), `LateUpdate`에서 카메라 forward에 맞춤 |
| `PlayerSkinApplier` | `Awake`에서 소유자의 `SkinIndex`로 `bodyRenderer.sharedMaterial`을 교체한다. 늦게 도착하면 `OnPlayerPropertiesUpdate`에서 다시 적용 |

### 3.5 `Camera/`

- **`Camera_Ctrl`** (Main Camera): 쿠키 3인칭 궤도 카메라. 대상은 플레이어 위치 + 1.5m, 거리 3.2m 고정
  (휠은 붓 크기 조절 전용). 우클릭 드래그로 회전(수직 −7°~80°), 회전은 Slerp로 부드럽게 한다.
  `InitCamera`와 `Start`의 호출 순서와 관계없이 `ResetToDefaultView`로 초기화된다.

### 3.6 `ColorTag/` — 개인 자유 색칠

**`PlayerPaintCanvas`** (354줄, 가장 복잡한 클래스)

- **초기화 (`Start`)**
  - 캐릭터마다 512×512 ARGB32 `RenderTexture`를 만들고 투명으로 초기화한다.
  - `bodyRenderer.sharedMaterial`(이미 `PlayerSkinApplier`가 스킨을 바꿔둔 상태)의 `_MainTex`를
    가져와서, `PlayerPaintedSkin` 셰이더로 새 머티리얼(`_MainTex` + `_PaintTex`)을 만들어 `.material`에 적용한다.
  - 붓 레이캐스트 마스크 = `DefaultRaycastLayers & ~PlayerCapsule`.
  - `Mesh_0`의 `MeshCollider`와 `SkinnedMeshRenderer`가 있으면 베이크용 `Mesh`를 준비한다.
- **입력 (`Update`, 로컬, 색칠 페이즈 중에만)**
  - **3프레임마다** `SkinnedMeshRenderer.BakeMesh` → 정점을 `localScale`로 나눔 → `MeshCollider.sharedMesh`
    재할당(콜라이더가 현재 애니메이션 포즈를 따라가게 함). 매 프레임 하면 257fps가 15fps로 떨어진다는 실측 주석이 있다.
  - 휠로 붓 반경 조절: 0.005~0.1 UV, 한 틱에 0.002.
  - 좌클릭을 누르고 있으면 매 프레임 레이캐스트 → `hit.collider == paintableCollider`일 때만 `hit.textureCoord`에 스탬프를 찍는다.
  - 지우개 모드: `EraseStampMaterial` 스탬프 + `PaintStroke(Erase)` 이벤트.
  - 일반: `TryRegisterSlotAndStamp`.
- **슬롯 등록 규칙 (`TryRegisterSlotAndStamp`)**
  - 이미 등록된 색이면 바로 칠한다.
  - 등록되지 않은 색: 이미 슬롯이 4개면 `OnSlotRejected`(구독자 없음)를 호출하고 **칠하지 않는다**.
    아니면 스탬프 **횟수(=누른 프레임 수)** 를 누적하고, 15회가 되면 등록 + `RegisteredSlotCount` 보고.
    등록 전에도 화면에는 칠해진다.
- **도구**: `SetBrushColor(i)`(지우개 해제), `SetEraseMode()`, `ResetCanvas()`(캔버스·슬롯·대기 카운트
  초기화 + `ClearColor` 이벤트).
- **원격 재생 (`OnEvent`)**: ViewID가 일치하면 송신 측이 이미 판단한 결과를 그대로 재생한다.
- **강제 도포**: `OnRoomPropertiesUpdate(ForcedPaintActorNumbers)` 또는 `Start` 시점에 자기 ActorNumber가
  목록에 있으면 `FinalizeStamp(uv=0, radius=float.MaxValue)`로 전신을 칠하고 `ForceFill` 이벤트를 보낸다.
  슬롯을 그 색 하나로 교체한다(**`RegisteredSlotCount`는 다시 보고하지 않음**).
- **정리 (`OnDestroy`)**: RenderTexture를 `Release`하고, 베이크 메시를 `Destroy`한다. (`.material`로 만든
  머티리얼 인스턴스는 파괴하지 않음, §8.19)

**스탬프 방식**: `ApplyStamp`가 머티리얼에 `_StampUV/_StampRadius/_StampColor`를 설정한 뒤, 임시 RT로
`Blit(canvas→temp)` → `Blit(temp→canvas, stampMat)`. 스탬프 한 번마다 512² 전체 화면 패스를 2번 수행한다.

**셰이더 (`ColorTag/Shaders/`)**

| 셰이더 | 동작 |
|---|---|
| `PaintStamp` | UV 거리 ≤ 반경인 픽셀에 `(색, a=1)`을 쓴다. `_RespectLock=1`이면 **이미 a≥0.999인 픽셀은 건드리지 않는다.** 즉, **한 번 칠한 곳은 다른 색으로 덧칠할 수 없고** 지우개로 지워야만 다시 칠할 수 있다. |
| `PaintErase` | 반경 안을 `(0,0,0,0)`으로 만든다. |
| `PaintColorReplace` | 특정 색을 다른 색으로 치환한다. **사용처 없음(죽은 셰이더)** |
| `PlayerPaintedSkin` | `lerp(base, paint.rgb, paint.a)` × (램버트 N·L × 메인 라이트 + `unity_AmbientSky`). ForwardBase 한 패스만 있어 **추가 라이트를 받지 않는다.** 그림자는 `Fallback "Standard"`로 처리된다. |

| 머티리얼 | 셰이더 / 값 |
|---|---|
| `BrushStampMaterial` | PaintStamp, `_RespectLock=1` |
| `FinalizeStampMaterial` | PaintStamp, `_RespectLock=0` |
| `EraseStampMaterial` | PaintErase |
| `BrushCursorMaterial` | 붓 커서용 (`_Color`를 PropertyBlock으로 설정) |

**나머지 ColorTag 클래스**

| 클래스 | 위치 | 역할 |
|---|---|---|
| `BrushCursorController` | GameScene/PaintManagers, PlayerTestScene | 색칠 페이즈 중 마우스가 로컬 캔버스 콜라이더 위에 있으면 3D 붓 커서를 표면 법선 방향으로 0.01 띄워 표시하고 OS 커서를 숨긴다. 크기 = 반경/기본 반경 × 0.05. 색은 현재 붓 색. 로컬 캔버스가 없으면 **매 프레임 `FindObjectsByType`** 을 호출한다. |
| `ColorSelectionPanel` | GameScene/Canvas/ColorSlotPanel | 남은 시간과 "N / 4"를 표시한다. **자기 GameObject를 `SetActive(false)`로 끔** (§8.2) |
| `ColorSwatchButton` ×10 | ColorSlotPanel/SwatchRow/Swatch0~9 | 클릭 시 로컬 캔버스를 찾아 `SetBrushColor(colorIndex)`. `SetLocked`는 호출하는 곳이 없다. |
| `PaintToolButton` ×2 | ColorSlotPanel/EraseButton, ResetButton | 지우개 / 리셋 |
| `PaintPhaseController` | GameScene/GameRuleManagers | 마스터 전용. 페이즈가 끝나면 `RegisteredSlotCount==0`인 플레이어(**괴물 포함**)에게 팔레트 10색을 섞어 겹치지 않게 배정한다. |
| `PlayerCrackDisplay` | HideOrSeekPlayer 프리팹 | `HitCount>=2`가 되면 `bodyRenderer.enabled=false` + `breakVfxPrefab`(**null**) 생성. 콜라이더는 그대로 남는다. |
| `RoomState` | static | Room Props 조회 헬퍼(`TryGetInt/Double/IntArray`). **직접 캐스트**를 해서 타입이 다르면 예외가 발생한다. |
| `ColorPaletteSO` | `DefaultColorPalette` | Red, Orange, Yellow, Lime, Green, Teal, Blue, Navy, Purple, Magenta (10색) |
| `BrushSettingsSO` | `DefaultBrushSettings` | 커서 프리팹 `BrushCursor`, 스케일 0.05, 반경 0.005/0.1/0.02, 휠 0.002, 표면 오프셋 0.01(에셋에 직렬화되지 않아 기본값 사용) |

### 3.7 `Monster/` — 괴물과 게임 룰

**괴물 선정 (GameLobbyScene)**

- **`Cauldron`** (GameLobbyScene/Cauldron): 로컬 쿠키가 트리거에 들어오면 `ClaimMonster`를 마스터에게
  보낸다. 중복 전송은 마스터가 걸러낸다.
- **`MonsterAssignmentAuthority`** (GameLobbyScene/MonsterManagers): 마스터 전용.
  - 첫 `ClaimMonster`로 `MonsterActorNumbers=[신청자]`와 `MonsterRevealTime`을 기록한다.
  - `Start` 시점의 `PhotonNetwork.Time + 30초`까지 아무도 신청하지 않으면 **현재 방에 있는 인원 중**
    무작위로 배정한다(정원이 차지 않았어도 배정됨, §8.13).
- **`MonsterRevealController`** (GameLobbyScene/MonsterManagers, 배너는 별도 `MonsterRevealBanner`
  오브젝트): 확정되면 닉네임 배너를 켠다. 괴물로 확정돼도 대기실에서는 **여전히 쿠키 모습으로 돌아다닌다.**

**게임 진행 (GameScene/GameRuleManagers에 모여 있음)**

- **`GamePhaseStarter`**: 마스터가 `PaintPhaseEndTime`이 없으면 `Now+60`으로 설정한다. 이미 있으면
  아무것도 하지 않는다(§8.7에서 두 번째 판이 막히는 원인).
- **`MonsterJoinController`**: 마스터는 페인트 페이즈가 끝나면 `MonsterJoined=1`과 `GameEndTime=Now+600`을
  기록한다. 괴물 본인은 두 값이 모두 있으면 `"MonsterSpawnPos"`에 `MonsterPlayer`를 스폰한다.
- **`GameRuleController`**: 마스터가 매 프레임 판정한다. 괴물이 아닌 모든 플레이어가 `HitCount>=2`이면
  `MonsterWins`, 아니면 `GameEndTime`이 지났을 때 `CookiesWin`. 한 번만 기록한다.
- **`RoomLifecycleWatcher`**: 마스터가 `OnPlayerLeftRoom`에서 나간 사람이 괴물이면 목록에서 제거한다.
  남은 괴물이 0명이면 `MonsterDepartedAt`을 기록하고, 5초 뒤 `MonsterDepartedAt=null`, `IsOpen=true`,
  `LoadLevel(GameLobby)`. `OnLeftRoom`이면 `LoadScene(Lobby)`.

**괴물 캐릭터 (`MonsterPlayer` 프리팹)**

- **`MonsterController`**
  - 로컬: 마우스로 yaw(몸통)와 pitch(`eyeSocket`, ±80°)를 조절한다. WASD는 몸 기준 이동(속도 4).
    LeftShift는 촉수 돌진. Rigidbody 설정은 쿠키와 같다.
  - `Awake`에서 `Camera.main`의 `MonsterFirstPersonCamera`에 `eyeSocket`을 넘기려 하지만, **그 컴포넌트가
    어디에도 없다**(§8.5).
  - 상태: `Idle / Walk / TentacleDash / GrabKill`. 트리거 이름은 enum 이름과 같다.
  - `LateUpdate`: 상태가 GrabKill이고 Animator 상태 `"GrapKill"`(오타 그대로)의 normalizedTime이 1 이상이면
    처형 트리거 쿨다운을 풀고 Idle로 돌아간다. **실제로는 여기까지 도달하지 못한다**(§8.4).
- **`MonsterTentacleDash`** (순수 C#): 사거리 20m, 0.25초(80m/s), 쿨타임 15초, 반경 0.4 SphereCast로
  장애물 앞까지 사거리를 줄인다. **`obstructionMask = 0`이라 줄이는 동작이 일어나지 않는다**(§8.10).
  이동은 `rb.MovePosition`으로 한다. `CooldownRemaining01`은 UI에서 쓰지 않는다.
- **`MonsterGrabKillTrigger`**: 루트의 SphereCollider(트리거, 반지름 0.5, 중심 z+0.1) × 루트 스케일 5.42 =
  **반지름 약 2.7m, 전방으로 약 0.54m 치우친 구**. 뒤쪽도 약 2.2m까지 닿는다. 괴물 소유자 쪽에서 쿨다운이
  아니고 쿠키가 들어오면 `PlayGrabKill()` + 쿠키에게 `RequestGrabKill`(All) RPC. **이미 파괴된 쿠키인지는 확인하지 않는다.**
- **`MonsterNetworkSync`**: `PlayerNetworkSync`와 같은 코드다(도메인끼리 참조하지 않으려고 일부러 중복).
- **`MonsterFirstPersonCamera`**: `eyeSocket`의 위치와 회전을 그대로 복사한다. **부착된 곳이 없다.**

**결과와 관전**

- **`ResultScreenController`** (GameScene/Canvas/ResultScreen): `GameResult`를 받으면 승리 배너, 생존 수
  "N / 4"(분모 하드코딩), 쿠키 아이콘 회색 처리, `PlayerResultRow` 목록, 12초 카운트다운 후 마스터만
  `LoadLevel(GameLobby)`. 마스터가 아닌 사람이 버튼을 누르면 코루틴만 멈추고 아무 일도 일어나지 않는다.
  **Awake에서 자기 자신을 꺼서 콜백을 받지 못한다**(§8.1).
- **`MonsterDepartureBanner`** (GameScene/Canvas/MonsterDepartureBanner): 위와 같은 결함이 있다.
- **`PlayerResultRow`**: 이름과 "(괴물)" / "○ 생존" / "✕ 부숴짐".
- **`SpectatorController`** (쿠키 프리팹): 파괴되면 `Camera_Ctrl`을 끄고, 살아 있는 다른 쿠키 뒤 4m,
  위 2m에서 따라간다. Space로 대상을 바꾼다. 대상이 파괴돼도 목록은 Space를 누를 때만 갱신된다.
  살아 있는 쿠키가 0명이면 카메라가 멈춘다.

### 3.8 `Dev/`

- **`OfflineModeBootstrap`** (PlayerTestScene): `Awake`에서 `PhotonNetwork.OfflineMode = true`(**static 전역
  상태, 다른 씬으로 넘어가도 유지됨**). `SpawnAsMonster`는 static 플래그다. `autoCreateRoom`이면
  테스트 방을 만든다.
- **`MonsterTestSpawner`** (PlayerTestScene): `SpawnAsMonster`이면 `MonsterPlayer`를 스폰한다.
- 참고: PlayerTestScene에는 `PlayerSpawner`가 **없어서**, `SpawnAsMonster=false`이면 아무것도 스폰되지 않는다.

---

## 4. 씬 배선 실측 (컴포넌트 → GameObject 경로)

### 4.1 LobbyScene
- `LobbyPanel`(프리팹 인스턴스) → `LobbyController`. 씬 자체에 부착된 프로젝트 스크립트는 없다.

### 4.2 GameLobbyScene
| 컴포넌트 | 경로 |
|---|---|
| `GameManager`, `RoomExitController`, `PlayerSpawner` | `GameManager` |
| `Camera_Ctrl` | `Main Camera` |
| `Cauldron` | `Cauldron` |
| `MonsterAssignmentAuthority`, `MonsterRevealController` | `MonsterManagers` (배너 → `MonsterRevealBanner`, 별도 오브젝트이므로 정상) |
| `PlayerSkinSelector` | `GameLobbyUICanvas/SkinSelectPanel` |
| `VoidKillZone` | `VoidKillZone` |
| `ConfirmDialog` | 프리팹 인스턴스 |
| `GameLobbyController` | `GameLobbyPanel` 프리팹 인스턴스 |
| 그 밖에 | NatureStarterKit2 나무·덤불 프리팹 |

### 4.3 GameScene
| 컴포넌트 | 경로 |
|---|---|
| `GameManager`, `RoomExitController`, `PlayerSpawner` | `GameManager` |
| `GamePhaseStarter`, `PaintPhaseController`, `MonsterJoinController`, `GameRuleController`, `RoomLifecycleWatcher` | `GameRuleManagers` |
| `BrushCursorController` | `PaintManagers` |
| `Camera_Ctrl` | `Main Camera` (**`MonsterFirstPersonCamera` 없음**) |
| `ColorSelectionPanel` | `Canvas/ColorSlotPanel` |
| `ColorSwatchButton` ×10 | `Canvas/ColorSlotPanel/SwatchRow/Swatch0~9` (**패널의 자식**) |
| `PaintToolButton` ×2 | `Canvas/ColorSlotPanel/EraseButton`, `ResetButton` (**패널의 자식**) |
| `ResultScreenController` | `Canvas/ResultScreen` (`root` 필드 = **자기 자신**) |
| `MonsterDepartureBanner` | `Canvas/MonsterDepartureBanner` (`bannerRoot` 필드 = **자기 자신**) |
| `VoidKillZone`, `ConfirmDialog` | 각각 |

`MonsterAssignmentAuthority`와 `Cauldron`은 GameScene에 없다. 괴물 선정은 대기실에서만 일어난다.

### 4.4 PlayerTestScene (빌드 제외)
`OfflineModeBootstrap`, `MonsterTestSpawner`, `Camera_Ctrl`, `BrushCursorController`, `VoidKillZone`.
(`PlayerSpawner`, `MonsterFirstPersonCamera` 없음)

---

## 5. 프리팹 · 애니메이터 · 에셋 실측

### 5.1 `HideOrSeekPlayer.prefab` (Resources, 79개 오브젝트)

- **루트** (레이어 **9 Cookie**): `PhotonView`, `Rigidbody`(mass 1), `CapsuleCollider`(r 0.46, h 2, 중심 y 1),
  `Animator`(PlayerAnimator), `HideOrSeekPlayer`, `PlayerPaintCanvas`, `PlayerCrackDisplay`, `PlayerSkinApplier`,
  `PlayerGrabController`, `SpectatorController`.
- **`Mesh_0`** (레이어 **0 Default**): `SkinnedMeshRenderer`, `MeshCollider`(convex 0, 붓칠 대상), 키네마틱 `Rigidbody`.
- 자식: `Armature`(mixamorig 본), `CarrySocket`, `Nameplate`(`PlayerBillBoard`), `VoteIndicator`(구 시스템 잔재 오브젝트).
- 스킨 머티리얼 3개(A/B/C), `breakVfxPrefab = null`, `cookieLayer = 0`.

### 5.2 `MonsterPlayer.prefab` (Resources)

- 루트 (레이어 10 Monster, **스케일 5.42**): `PhotonView`, `Rigidbody`, `CapsuleCollider`(r 0.1, h 0.35 →
  월드 기준 r 약 0.54, h 약 1.9), `SphereCollider`(트리거, r 0.5 → 약 2.7m), `MonsterController`, `MonsterGrabKillTrigger`.
- `Animator`가 2개 있다(루트와 모델 쪽). `MonsterController.animator`는 인스펙터로 연결된 하나만 제어한다.
- `obstructionMask = 0`, `speed 4`, `mouseSensitivity 2`, `eyeSocket` 연결됨.
- 모델은 `Monster_Rigged_kihong.fbx`(Monster-Rerig-Plan.md §10에서 교체 완료).

### 5.3 `PlayerAnimator.controller`

- 파라미터(트리거): `Idle, Walk, Run, Jump, Dodge, Held, Broken`
- 레이어: **Base Layer 하나뿐** (Carry 레이어 없음)
- 상태: `Idle, Walk, Run, Jump(Cookie_Jumping, 루프 없음), Dodge(Cookie_Dodge_Retimed)`
- Any State 전이: `Walk/Run/Jump/Idle/Dodge` (0.1초 블렌드), **`Held → Idle`, `Broken → Idle`** (0초, 임시 대체)
- 사용하지 않는 FBX: `Cookie_Carrying`, `Cookie_Hanging_Idle`, `Cookie_StandUp`

### 5.4 `MonsterAnimator.controller`

- 파라미터: `Idle, Walk, TentacleDash, GrapKill(오타), GrabKill`
- 상태: `Idle, Walk, TentacleDash, GrapKill` (NewAnimation의 `Monster_Manual_*.fbx`, GrabKill·TentacleDash는 루프 없음)
- Any State 전이가 **중복**돼 있다: `Idle`×2, `Walk`×2, `TentacleDash`×2, `GrabKill→GrapKill`, `GrapKill→GrapKill`. 모두 즉시 전이(0초)이다.
- `GrapKill` 상태에서 나가는 전이는 없다(마지막 프레임에 멈춰 있음. 코드가 Idle 트리거로 빼내는 구조).

### 5.5 기타 에셋

- `07. Expression/` 얼굴 PNG 20장: 코드와 프리팹에서 참조하지 않는다.
- `06. Physics/PlayerNoFriction_tmp.physicMaterial`: 임시 사본.
- `Cookie_Dodge_Retimed_Backup_PreRootFix.anim`, `Monster/OldAnimation/`: 백업 에셋.
- 빌드 결과물 `TagOfChaosGame/`이 git에 커밋돼 있다(바이너리 약 40MB가 매번 diff에 포함됨).

---

## 6. 핵심 메커니즘 심층 분석

### 6.1 색칠 파이프라인 (한 번의 클릭이 화면에 반영되기까지)

```
[로컬] Update (색칠 페이즈 && IsMine)
  ├─ 3프레임마다 BakeMesh → MeshCollider 갱신 (현재 포즈)
  ├─ Camera.main.ScreenPointToRay(mouse)
  ├─ Physics.Raycast(mask = Default|… & ~PlayerCapsule)
  │     └─ 루트 캡슐(Cookie 레이어)이 마스크에 포함 → 캡슐에 먼저 맞음 → hit.collider != Mesh_0 → 종료 (§8.3)
  ├─ hit.textureCoord (MeshCollider에서만 유효한 UV)
  ├─ 슬롯 판정 → ApplyStamp: Blit 2회 (RT 512²)
  └─ RaiseEvent(PaintStroke, Others, Reliable)   ← 매 프레임 1회, 60fps면 초당 60개
[원격] OnEvent → ViewID 필터 → 같은 ApplyStamp 재생
[렌더] PlayerPaintedSkin: base와 paint를 paint.a로 섞음
```

- **대역폭**: 좌클릭을 누르고 있는 동안 프레임마다 Reliable 이벤트가 1개씩 나간다. 쿠키 3명이 동시에
  칠하면 초당 약 180개다. Photon 무료 플랜의 권장 메시지 한도(방당 초당 500개)의 상당 부분을 차지한다.
  보간 없이 프레임 단위 점을 찍기 때문에, 마우스를 빨리 움직이면 **점선처럼 끊긴다.**
- **잠금 규칙**: 일반 붓은 이미 칠한 픽셀을 덮지 못한다. 색을 바꾸려면 지우개로 지우거나 리셋해야 한다.
  GameRule.md의 "슬롯 악용 방지"와 맞물린 의도된 설계로 보이지만, 사용자 입장에서는 "칠이 안 된다"로
  느껴질 수 있다.
- **슬롯 카운트는 면적이 아니라 프레임 수**로 센다. 60fps에서 15프레임은 0.25초다. 잠긴 픽셀 위에서
  클릭해 실제로 아무것도 칠해지지 않아도 카운트는 올라간다.
- **정합성**: 이벤트는 버퍼링되지 않는다. 색칠 중에 재접속하거나, 원격 인스턴스의 `Start`보다 이벤트가
  먼저 도착하면(그때 `PaintCanvas == null`) 색칠 결과가 어긋나거나 예외가 날 수 있다.

### 6.2 쿠키 이동 / 물리

- 입력은 Update에서 읽고, 물리 적용은 FixedUpdate에서 한다(Unity 관례를 지킴).
- 수평 속도는 매 스텝 **직접 덮어쓰기** 때문에 외부 힘(넉백 등)이 남지 않는다. 공중에서도 자유롭게 조작할 수 있다.
- 회피는 `speed *= 2` → 끝나면 `*= 0.5`로 되돌린다. 실제 이동은 `Move()`가 회피 중에만 `speed`를 쓰고,
  평소에는 `baseSpeed`를 쓰므로 상태가 누적되지는 않는다.
- 접지 레이 길이 0.4m, `groundLayer = Default`. 쿠키끼리 밟고 올라서는 경우는 접지로 인정되지 않는다(Cookie 레이어).
- 원격 쿠키는 키네마틱으로 transform을 보간한다. 로컬 쿠키가 원격 쿠키를 밀면 키네마틱 쪽이 이겨서 밀리지 않는다.

### 6.3 괴물 GrabKill 한 번의 흐름 (현재 코드 기준)

```
Physics step: SphereTrigger.OnTriggerEnter(쿠키 콜라이더)
  → onCooldown = true
  → MonsterController.PlayGrabKill(): ResetTrigger(Idle/Walk) + SetTrigger(GrabKill), state=GrabKill
  → 쿠키 PhotonView.RPC("RequestGrabKill", All)
같은 프레임 Update():
  → moveInput이 있으면 ChangeState(Walk), 없으면 ChangeState(Idle)   ← previousState(GrabKill) != 새 상태
      → ResetTrigger("GrabKill")  ← 방금 건 트리거를 Animator가 처리하기 전에 취소
      → SetTrigger(Idle|Walk), state=Idle|Walk
LateUpdate(): currentState != GrabKill → 아무것도 안 함
결과: 처형 애니메이션이 재생되지 않고, grabKillTrigger.ResetTrigger()가 영원히 호출되지 않음
      → onCooldown == true 고정 → 이후 모든 처형 불가 (§8.4)
```

피해자 쪽 처리는 정상이다(`HitCount=2`, 이동 잠금, 관전 전환). 원격 클라이언트에는 상태 값 GrabKill이
직렬화 틱 사이에 사라지므로 역시 애니메이션이 보이지 않는다.

### 6.4 Photon 콜백과 GameObject 활성 상태의 관계

`MonoBehaviourPunCallbacks`는 **`OnEnable`에서 `PhotonNetwork.AddCallbackTarget(this)`, `OnDisable`에서
`RemoveCallbackTarget(this)`** 을 호출한다. 따라서 **자기 GameObject를 끄는 순간 모든 Photon 콜백
(`OnRoomPropertiesUpdate` 등)과 `IOnEventCallback`이 끊긴다.** 이 규칙을 어기는 컴포넌트가 3개 있다
(§8.1, §8.2). 반대로 `MonsterRevealController`는 배너를 별도 오브젝트로 분리해 올바르게 구현했다.

---

## 7. 아키텍처 평가

| 관점 | 평가 |
|---|---|
| 책임 분리 | **좋음.** 조정자 MonoBehaviour + 순수 C# 협력 클래스(`PlayerGroundDetector`, `PlayerAnimationDriver`, `PlayerNetworkSync`, `MonsterTentacleDash`). `GameManager`에서 스폰·퇴장을 분리한 이력도 있다. |
| 도메인 간 의존 | 대체로 한 방향이다. 다만 `NetKeys`/`NetEventCodes`/`RoomState`가 **`ColorTag/`에 있으면서** Monster, Unit, Lobby, GameManager가 모두 참조한다. `RoomState` 주석("ColorTag 도메인 밖에서는 쓰지 않는다")과 실제 사용이 다르다. `Core/`로 옮기는 것이 맞다. |
| 싱글톤 | `GameManager.Inst` 하나뿐이고 참조하는 곳이 없다(죽은 코드). |
| 씬 문자열 의존 | `GameObject.Find("PlayerSpawnPos"/"MonsterSpawnPos")`, `transform.Find("Mesh_0")`, 트리거 이름 = enum 이름, Animator 상태 `"GrapKill"`, `"Jump"`, 레이어 `"PlayerCapsule"`. 이런 **문자열 계약이 깨지면 조용히 실패**하고, 실제로 §8.3과 §8.4가 이런 식으로 생겼다. |
| 늦은 참조 탐색 | `FindObjectsByType<PlayerPaintCanvas>`가 4곳에 중복돼 있다(`BrushCursorController`, `ColorSelectionPanel`, `ColorSwatchButton`, `PaintToolButton`). 로컬 캔버스가 없는 괴물 클라이언트에서는 **매 프레임 전체 탐색**을 한다. 로컬 플레이어 레지스트리(static 이벤트)를 두면 해결된다. |
| 이벤트 구독 해제 | Photon 콜백은 베이스 클래스가 관리한다. UI `AddListener`는 해제하지 않지만, 씬과 함께 파괴되므로 실질적인 문제는 없다. |
| 풀링 | 없다. `PlayerListItem`은 매번 전부 Destroy/Instantiate하지만 최대 4개라 무시할 수 있다. |
| 소유권 / RPC | 가장 성숙한 영역이다. "소유자만 자기 상태를 확정한다"는 원칙이 일관된다. |
| 입력 | 레거시 `Input`이 11개 클래스에 흩어져 있다. 키 설정 UI는 없다. |
| 프로젝트 규칙(CLAUDE.md) | "주석 외 한글 금지" **위반**: UI 문자열(`LobbyController` 피드백, `MonsterRevealController`, `MonsterDepartureBanner`, `PlayerResultRow`, `ResultScreenController`, `RoomExitController`)과 `Debug.Log` 메시지에 한글이 들어 있다. 현지화 테이블로 분리해야 한다. |

---

## 8. 발견된 문제 (우선순위순)

> **수정 현황 (2026-09-26)**: 아래 §8의 23개 항목은 `Bug-fix-plan.md` §23~§25에서 순차적으로 수정했다. 각 항목 제목 끝에 상태를 표시했다. 에디터 Play Mode 검증 결과는 `Bug-fix-plan.md` §25.1, 빌드 멀티 테스트가 필요한 항목은 §25.3에 있다.

> 표기: **[치명]** 게임 진행 불가 / **[높음]** 핵심 기능 불능 / **[중간]** 잘못된 동작 또는 악용 가능 /
> **[경미]** 품질·정리. "근거"는 직접 확인한 파일과 줄 번호이다. 모든 항목은 코드와 에셋 정적 분석으로
> 확인했으며, **Play Mode 재현은 하지 않았다.**

### 8.1 [치명·신규] 결과 화면과 괴물 이탈 배너가 절대 표시되지 않음 — ✅ 수정 완료 (Bug-fix-plan §23, CanvasGroup 숨김 + 결과 화면 위치 수정)

- **근거**: `ResultScreenController.cs:23-26`(`root.SetActive(false)`)과 `MonsterDepartureBanner.cs:12-15`
  (`bannerRoot.SetActive(false)`). GameScene에서 `root`/`bannerRoot`가 **컴포넌트 자신의 GameObject**를
  가리키는 것을 fileID로 확인했다(`Canvas/ResultScreen` 1399079600, `Canvas/MonsterDepartureBanner` 1505476079).
- **메커니즘**: 자기 자신을 끄면 `MonoBehaviourPunCallbacks.OnDisable`이 콜백 등록을 해제한다(§6.4).
  그래서 `OnRoomPropertiesUpdate(GameResult / MonsterDepartedAt)`을 받지 못한다.
- **영향**: 승패가 판정돼도 **결과 화면이 뜨지 않고**, 12초 자동 복귀 코루틴도 시작되지 않는다. 결국
  **게임이 끝난 뒤 모두 GameScene에 갇힌다.** (괴물이 나갔을 때 5초 후 복귀는 `RoomLifecycleWatcher`가
  따로 처리하므로 동작하지만, 경고 배너는 보이지 않는다.)
- **해결 방향**: `MonsterRevealController`처럼 표시용 자식 오브젝트를 분리해 그것만 켜고 끄거나, 콜백
  수신 컴포넌트를 항상 활성인 매니저 오브젝트로 옮긴다.

### 8.2 [치명·신규] 색칠 UI 패널이 첫 프레임에 스스로 꺼지고 다시 켜지지 않음 → 색을 고를 수 없음 — ✅ 수정 완료 (Bug-fix-plan §23)

- **근거**: `ColorSelectionPanel.cs:193-195`. `Update()` 안에서 `gameObject.SetActive(isPaintPhaseActive)`.
  이 컴포넌트는 `Canvas/ColorSlotPanel`에 있고, **스와치 10개와 지우개·리셋 버튼이 모두 이 오브젝트의 자식**이다.
- **메커니즘**: GameScene이 로드된 첫 프레임에는 아직 `PaintPhaseEndTime`이 없다. 마스터의
  `GamePhaseStarter.Update`가 설정하더라도, PUN2의 `SetCustomProperties`는 서버 응답이 와야 로컬
  캐시에 반영된다. 그래서 `false`가 되어 패널이 꺼진다. 꺼진 GameObject에서는 `Update()`가 더 이상
  호출되지 않으므로 **다시 켜질 방법이 없다.**
- **영향**: 스와치를 누를 수 없으므로 `currentBrushColorIndex`가 계속 −1이고 **아무도 칠할 수 없다.** 60초
  후 전원이 등록 슬롯 0개로 강제 도포된다. 사실상 색칠 페이즈 전체가 무력화된다.
- **해결 방향**: 표시 대상을 자식 컨테이너로 분리하고, 컴포넌트는 항상 활성 상태로 둔다.

### 8.3 [치명·신규 회귀] 붓 레이캐스트가 루트 캡슐에 막혀 색칠·붓 커서가 동작하지 않음 — ✅ 수정 완료 (Bug-fix-plan §23 `paintableCollider.Raycast` + §25 P8 빌드 정점 압축 TexCoord0 해제)

- **근거**: `PlayerPaintCanvas.cs:491`, `BrushCursorController.cs:36`. 마스크 = `DefaultRaycastLayers & ~PlayerCapsule(8)`.
  그런데 `HideOrSeekPlayer.prefab` 루트(CapsuleCollider r 0.46 × h 2)의 레이어가 **9 Cookie**이다
  (프리팹 880줄). `git log`로 확인한 결과, 루트 레이어는 `16c662b`(상체 색칠 버그 수정)에서 **8
  PlayerCapsule**로 바뀌었다가 **`403f4b6`("몬스터 구현진행중")에서 9 Cookie로 다시 바뀌었다.**
- **메커니즘**: 카메라에서 쏜 광선이 몸을 감싼 캡슐에 먼저 맞으므로 `hit.collider != paintableCollider`가
  되어 조기 종료된다. 커서도 `hitSurface=false`가 되어 표시되지 않는다. 16c662b에서 고쳤던 버그가 그대로 재발했다.
- **해결 방향**: 마스크에서 `Cookie` 레이어도 제외한다(권장. 레이어는 Cookie로 유지해 그랩과 몬스터
  판정에 쓰기 위함). 또는 `Physics.Raycast` 대신 `paintableCollider.Raycast(ray, …)`로 **대상 콜라이더
  하나만** 검사한다. 두 번째 방법이 레이어 변경에 영향을 받지 않아 더 견고하다.
- 참고: 이 문제는 §8.2와 별개다. §8.2를 고쳐도 이 문제 때문에 여전히 칠할 수 없다.

### 8.4 [치명·신규] 괴물 GrabKill이 게임당 1회만 동작함 (애니메이션도 재생되지 않음) — ✅ 수정 완료 (Bug-fix-plan §25 P2 — Play Mode 검증 V6)

- **근거**: `MonsterGrabKillTrigger.cs:25-28`, `MonsterController.cs:74-79`(Update의 무조건 `ChangeState(Walk|Idle)`),
  `MonsterController.cs:106-114`(`ResetTrigger(previousState)`), `MonsterController.cs:119-129`(LateUpdate 조건).
- **메커니즘**: §6.3 참고. 같은 프레임의 Update가 GrabKill 트리거를 취소하고 상태를 Idle/Walk로 덮어쓴다.
  그래서 쿨다운 해제 조건(`currentState == GrabKill`)에 영원히 도달하지 못한다.
- **영향**: 첫 처형은 성립한다(피해자 쪽 RPC는 정상). 하지만 이후 `onCooldown == true`로 고정되어 괴물은
  **남은 쿠키를 잡을 수 없다.** 쿠키 3명 중 1명만 잡히므로 괴물이 이기는 것이 사실상 불가능하다.
- **해결 방향**: GrabKill 중에는 Update의 이동 상태 전환을 막는다(`if (currentState == GrabKill) return;`
  + 처형 중 이동 정지). 쿨다운 해제는 Animator 상태 이름 문자열 대신 타이머나 애니메이션 이벤트로
  처리하는 것이 견고하다.

### 8.5 [치명·지속] `MonsterFirstPersonCamera`가 어떤 씬에도 부착되지 않음 — ✅ 해결 (Bug-fix-plan §23 — 사용자 결정으로 3인칭 A안, `MonsterFirstPersonCamera` 삭제)

- **근거**: 스크립트 GUID `a924b161…`를 모든 `.unity`와 `.prefab`에서 검색한 결과 0건.
  `MonsterController.cs:35-36`은 null 조건 연산자(`?.`)로 조용히 넘어간다.
- **영향**: 괴물 클라이언트의 Main Camera에는 `Camera_Ctrl`만 있고, `m_Player`가 null이라 카메라가
  **씬의 초기 위치에 고정**된다. 괴물은 자기 시점 없이 플레이하게 된다.
- **해결 방향**: GameScene과 PlayerTestScene의 `Main Camera`에 `MonsterFirstPersonCamera`를 추가한다.
  괴물일 때는 `Camera_Ctrl`을 끄는 처리도 필요하다(지금은 `m_Player == null`이라 우연히 충돌하지 않음).

### 8.6 [높음·신규] 쿠키 그랩(E키) 불가: `cookieLayer = Nothing` — ✅ 수정 완료 (§25 P3 — 검증 V4, 해제 시 튕김 추가 수정 V5)

- **근거**: `HideOrSeekPlayer.prefab`의 `PlayerGrabController.cookieLayer.m_Bits: 0`.
  `PlayerGrabController.cs:31`은 `OverlapSphere(…, cookieLayer)`.
- **영향**: OverlapSphere가 항상 빈 배열을 돌려주므로 그랩이나 캐리가 **전혀 동작하지 않는다.**
- **해결 방향**: `cookieLayer = Cookie(9)`로 설정한다. 그 뒤에는 §8.9의 애니메이션 공백도 드러난다.

### 8.7 [높음·지속] 두 번째 판이 진행되지 않음: Room/Player Props를 초기화하지 않음 — ✅ 수정 완료 (§25 P4 `RoundStateResetter` — 검증 V10)

- **근거**: 게임을 마치고 GameLobby로 돌아가는 두 경로(`ResultScreenController.OnLobbyButtonClicked`,
  `RoomLifecycleWatcher.ReturnToGameLobby`) 모두 `MonsterDepartedAt` 외에는 아무 키도 지우지 않는다.
- **연쇄 효과**
  - `MonsterAssignmentAuthority.HasMonsterAssigned()`가 true → 가마솥과 타임아웃이 모두 무시되고 **이전 괴물이 그대로 유지된다.**
  - `GamePhaseStarter`: `PaintPhaseEndTime`이 이미 있으므로 **색칠 페이즈가 시작되지 않는다.** 이전 값이 과거
    시각이라 곧바로 "페이즈 종료" 상태가 된다.
  - `MonsterJoinController`는 곧바로 조인 처리를 하고 `GameEndTime`을 다시 쓰지만, `GameRuleController`는
    `GameResult`가 이미 있어서 **판정을 하지 않는다.**
  - `PaintPhaseController`는 씬마다 `resolved=false`로 시작하므로 다시 실행되지만, 이전 판의 `RegisteredSlotCount`가 남아 있다.
  - Player Props의 `HitCount=2`가 남아 있어, 이전에 파괴된 쿠키는 결과 화면과 판정에서 **처음부터 죽은 것으로 취급된다.**
- **해결 방향**: GameLobbyScene 진입 시 마스터가 게임 관련 Room 키를 전부 `null`로 설정하고, 각 클라이언트가
  자기 `HitCount`와 `RegisteredSlotCount`를 `null`로 설정하는 초기화 루틴을 둔다(`SkinIndex`는 유지).

### 8.8 [높음·지속] `Cursor.lockState`를 설정하는 코드가 없음 — ✅ 수정 완료 (§25 P5 — 우클릭 회전 중 커서 잠금)

- 괴물은 마우스 이동량(`Mouse X/Y`)으로 시점을 돌리는데, 커서가 잠기지 않아 화면 가장자리에서 멈추고
  창 밖을 클릭하게 된다. 반대로 쿠키는 색칠 페이즈에 커서가 필요하다. 역할과 페이즈별로 커서 정책을
  관리하는 주체가 필요하다.

### 8.9 [중간·지속] `Held`/`Broken` 애니메이션이 없음, Carry 레이어도 없음 — ✅ 수정 완료 (§25 P3 — Held 상태 + Carry 레이어, 원격 동기화)

- Animator에서 `Held → Idle`, `Broken → Idle`로 임시 연결돼 있다. 들린 쿠키와 파괴된 쿠키가 모두 Idle
  자세로 보인다. `SetCarryLayerWeight`는 레이어가 없어서 아무 동작도 하지 않는다.
  `Cookie_Carrying`/`Cookie_Hanging_Idle` FBX는 이미 있지만 연결되지 않았다.

### 8.10 [중간·지속] 촉수 돌진이 벽을 무시함: `obstructionMask = Nothing` — ✅ 수정 완료 (§25 P5 — `obstructionMask`=Default)

- `MonsterTentacleDash.cs:30`의 SphereCast가 아무것도 맞히지 않으므로 항상 20m를 이동한다.
  `rb.MovePosition`은 비키네마틱 바디에서 충돌 해소가 제한적이라 얇은 벽을 **통과할 수 있다.**
  `obstructionMask`에 Default(지형)를 설정해야 한다.

### 8.11 [중간·신규] 파괴된 쿠키도 처형 트리거에 반응함 — ✅ 수정 완료 (§25 P2 — 검증 V7)

- `PlayerCrackDisplay`는 렌더러만 끄고, 콜라이더와 Rigidbody는 그대로 남는다. `MonsterGrabKillTrigger`는
  `HitCount`를 확인하지 않고 쿨다운을 소모한다. §8.4를 고친 뒤에도 **보이지 않는 시체가 괴물의 처형을
  빼앗는** 문제가 남는다. 트리거에서 `HitCount>=2`를 건너뛰거나, 파괴될 때 콜라이더를 끄고 레이어를 바꿔야 한다.
  (`breakVfxPrefab`이 null이라 파괴 연출도 없다.)

### 8.12 [중간·지속] 방을 나갈 때 `CustomProperties.Clear()`가 로컬 사본만 지움 — ✅ 수정 완료 (§25 P5)

- `RoomExitController.cs:41,50`. `Hashtable.Clear()`는 서버에 전달되지 않는다. 마지막 사람이 나가면 방이
  사라지므로 Room 쪽은 실제 문제가 없다. 하지만 코드의 의도(초기화)와 동작이 다르므로, §8.7을 고칠 때
  잘못 참고하지 않도록 주의해야 한다. 퇴장 메시지는 `"]" + 닉네임 + "]"`로 **여는 괄호 오타**가 있다(34줄).

### 8.13 [중간·지속] 괴물 선정 타임아웃의 기준이 잘못됨 — ✅ 수정 완료 (§25 P6 — 정원 충족 기준 deadline, 검증 V11~V13)

- `MonsterAssignmentAuthority.cs:15-18`. 기준 시각이 "GameLobbyScene 진입"이라, 정원이 차지 않은 상태에서도
  30초가 지나면 **현재 방에 있는 사람 중 무작위로** 배정된다. 이후 입장한 사람은 후보가 될 수 없다.
- 마스터가 바뀌면 새 마스터의 `sceneEnterTime`이 0이라서 **즉시** 무작위 배정이 일어난다.
- 괴물로 확정된 사람이 GameLobby에서 나가면 처리하는 로직이 없다(`RoomLifecycleWatcher`는 GameScene에만 있음).
  그러면 정원이 다시 차도 괴물 없이 시작될 수 있다.

### 8.14 [중간·신규] 괴물은 색칠 페이즈 60초 동안 아바타와 카메라가 없음 — ✅ 수정 완료 (Bug-fix-plan §23 괴물 대기실 대기 + §25 P7 강제 도포 제외)

- 설계상(괴물은 60초 뒤 합류) 의도된 것이지만, 괴물 클라이언트의 화면은 초기 카메라 위치에 고정된다.
  색칠 패널도 보이고(§8.2를 고치면), 강제 도포 대상 목록에도 포함된다(`PaintPhaseController`가 괴물을
  제외하지 않음). 대기 화면과 카운트다운 UI가 필요하다.

### 8.15 [중간] `GameRuleController`의 괴물 승리 판정이 공허하게 참이 될 수 있음 — ✅ 수정 완료 (§25 P7 생존 타이머 UI — 검증 V8, 쿠키 0명 시 괴물 승은 의도된 규칙으로 명시)

- `AllCookiesBroken`은 방에 남은 쿠키가 0명이어도 true를 돌려준다. 쿠키가 모두 나가면 괴물 승리로
  처리되는데, 의도와 맞는지 확인이 필요하다.
- 생존 남은 시간(`GameEndTime`)을 화면에 표시하는 UI가 없다.

### 8.16 [중간] 색칠 네트워크 부하와 끊김 — ✅ 수정 완료 (§25 P7 보간 스탬프 + 15Hz 묶음 전송 — 검증 V2/V3)

- §6.1 참고. 프레임마다 Reliable 이벤트를 보낸다. 누른 프레임마다 점 하나를 찍으므로 빠르게 그리면
  점선이 된다. 해결책: 일정 거리마다 보간 스탬프를 찍고, 스트로크를 묶어서 10~20Hz로 전송한다.

### 8.17 [중간] 슬롯 등록이 면적이 아니라 프레임 수 기준 — ✅ 수정 완료 (§25 P7 — 스탬프 수가 이동 거리에 비례)

- 프레임레이트에 따라 달라진다(144fps에서는 약 0.1초만에 등록). 잠긴 픽셀 위에서 클릭만 해도 카운트가 올라간다.

### 8.18 [경미] 생명주기 경계 조건 — ✅ 수정 완료 (§25 P7 — Awake 생성, 강제 도포 후 보고)

- `HideOrSeekPlayer.OnPhotonSerializeView`의 쓰기 쪽이 `animationDriver`(Start에서 생성)를 참조한다. Start
  이전에 직렬화가 호출되면 NRE가 발생한다(networkSync만 Awake로 옮긴 상태).
- `PlayerPaintCanvas.OnEvent`: `PaintCanvas`가 Start에서 생성되므로, 그 전에 이벤트가 도착하면 `Blit(null)`이 된다.
- `PlayerPaintCanvas.ApplyForcedColorIfAssignedToMe`가 `RegisteredSlotCount`를 다시 보고하지 않는다(판정에는 영향 없음).

### 8.19 [경미] 리소스 누수 — ✅ 수정 완료 (§25 P8)

- `PlayerPaintCanvas`: `bodyRenderer.material`로 만든 머티리얼 인스턴스를 파괴하지 않는다. 원격
  `PlayerPaintCanvas`(IsMine 아님)에서도 `bakedColliderMesh`를 만들지만 쓰지 않는다.
- `BrushCursorController`: `cursorInstance`를 파괴하지 않는다(씬과 함께 정리되므로 영향은 작음).
- `LogMsg`를 `AllBuffered`로 보내 채팅과 입퇴장 메시지가 방 버퍼에 무한히 쌓인다. 늦게 입장한 사람이
  전부 재생하고, 방 이벤트 캐시도 커진다.

### 8.20 [경미] `OnLeftRoom`에서 씬을 두 번 로드함 (GameScene) — ✅ 수정 완료 (§25 P8)

- `RoomExitController.OnLeftRoom`과 `RoomLifecycleWatcher.OnLeftRoom`이 둘 다 `LoadScene(Lobby)`를 호출한다.

### 8.21 [경미] 죽은 코드와 에셋 — ✅ 코드 정리 완료 (§25 P9 — 사용자 에셋(표정 PNG·백업 애니메이션·_tmp 물리 머티리얼)은 삭제하지 않음)

- 키: `RoundIndex`, `RoundEndTime`, `ColorPrefix`, `TaggerActorNumber`, `TaggerVariantSet`, `VoteColorIndex`,
  `CookiesDeparted`, `MonsterRevealTime`. 이벤트: `FillAll`.
- 메서드와 멤버: `RoomState.GetRoundIndex`, `ColorSwatchButton.SetLocked`, `PlayerPaintCanvas.OnSlotsChanged/OnSlotRejected`(구독자 없음),
  `MonsterTentacleDash.CooldownRemaining01`, `HideOrSeekPlayer.IsDodge()`, `PlayerNetworkSync.RemoteIsJump`, `GameManager.Inst`, `Is_Conversating`.
- 셰이더: `PaintColorReplace`. 프리팹 오브젝트: `VoteIndicator`. 에셋: 표정 PNG 20장, `_tmp`/`Backup` 파일.
- Animator: `GrapKill` 파라미터(오타)와 중복된 Any State 전이 5개.

### 8.22 [경미] 문자열 계약 — ✅ 수정 완료 (§25 P9)

- `"GrapKill"` 상태 이름, `"Jump"` 상태 이름, `"Mesh_0"`, `"PlayerSpawnPos"`, `"MonsterSpawnPos"`, `"Carry"`,
  레이어 `"PlayerCapsule"`. 모두 깨져도 조용히 실패한다. 최소한 `Awake`에서 한 번 검증하고 `Debug.LogError`를 남기는 것이 좋다.
- `RoomState`와 콜백들은 `(int)raw` 같은 직접 캐스트를 쓴다. 타입이 다른 값이 들어오면 `InvalidCastException`이 발생한다.

### 8.23 [경미] 기타 — ✅ 대부분 완료 (§25 P9 — 빌드 폴더 git 추적 해제는 저장소 구조 변경이라 보류)

- `ResultScreenController`의 분모 `"/ 4"`와 아이콘 4개가 하드코딩돼 있다. 실제 쿠키 수는 최대 3명이다.
- `OfflineModeBootstrap`이 static `PhotonNetwork.OfflineMode`를 켠 뒤 끄지 않는다. 에디터에서 PlayerTestScene
  다음에 다른 씬을 테스트하면 오프라인 상태가 남아 있을 수 있다.
- CLAUDE.md "주석 외 한글 금지" 규칙 위반(§7).
- 빌드 결과물 `TagOfChaosGame/`이 git에 커밋돼 있다. `.gitignore` 대상으로 권장한다.

---

## 9. 이전 research.md(2026-09-25판) 대비 정정 사항

| 이전 판 | 이번 판 |
|---|---|
| §6.14 "`Mesh_0`만 Cookie(9), 루트는 Default(0)" | **반대다.** 루트가 Cookie(9), `Mesh_0`이 Default(0). `403f4b6`에서 루트가 8 → 9로 바뀌면서 붓칠 회귀(§8.3)가 생겼다. |
| §6.15 `PlayerMonster.prefab` 방치 | **해소됨.** 현재 `Resources/`에 없고, 재리깅 결과는 `MonsterPlayer.prefab`에 반영됐다(Monster-Rerig-Plan.md §10). |
| §6.1 괴물 카메라 미배선 | 그대로 재현된다(§8.5). |
| §6.2, §6.3, §6.4, §6.5, §6.6 | 그대로 재현된다(§8.8, §8.9, §8.10, §8.7, §8.13). |
| (없음) | **신규**: §8.1 결과·배너 콜백 해제, §8.2 색칠 패널 자기 비활성화, §8.3 레이캐스트 회귀, §8.4 GrabKill 1회 제한, §8.6 그랩 레이어, §8.11 시체 트리거, §8.14~§8.17 |
| §7.4 "Scene 직접 의존 — 깨끗함" | 정정: `GameObject.Find` 문자열 의존이 3곳 있다(§7, §8.22). |

Plan.md(09-25판 조치 계획) 1~11항은 이번 판의 §8.5/§8.8/§8.7/§8.9/§8.10/§8.13/§8.21과 대응하며 여전히
유효하다. 다만 **§8.1~§8.4와 §8.6이 그보다 먼저 처리돼야 한다.**

---

## 10. 권장 다음 단계 (우선순위와 작업 규모)

| 순서 | 항목 | 규모 | 참조 |
|---|---|---|---|
| 1 | 결과 화면과 이탈 배너의 표시 대상을 자식 오브젝트로 분리 | 씬 수정 + 몇 줄 | §8.1 |
| 2 | `ColorSelectionPanel`의 표시 대상을 자식 컨테이너로 분리 | 씬 수정 + 몇 줄 | §8.2 |
| 3 | 붓 레이캐스트를 `paintableCollider.Raycast`로 교체(커서도 같이) | 2곳, 몇 줄 | §8.3 |
| 4 | GrabKill 중 상태 전환 차단 + 쿨다운 해제 방식 교체 + 파괴된 쿠키 제외 | `MonsterController`, `MonsterGrabKillTrigger` | §8.4, §8.11 |
| 5 | `MonsterFirstPersonCamera` 부착 + 역할별 카메라와 커서 정책 | 씬 + 새 컴포넌트 1개 | §8.5, §8.8 |
| 6 | 프리팹 값 수정: `cookieLayer=Cookie`, `obstructionMask=Default` | 인스펙터 | §8.6, §8.10 |
| 7 | GameLobby 진입 시 Room/Player Props 초기화 루틴 | 새 컴포넌트 1개 | §8.7 |
| 8 | 괴물 선정 타임아웃 기준을 "정원 충족 시점"으로 변경 + 마스터 교체 대응 | `MonsterAssignmentAuthority` | §8.13 |
| 9 | `Held`/`Broken`/Carry 애니메이션 연결 | Animator | §8.9 |
| 10 | 색칠 스트로크 보간과 전송 묶음, 슬롯 등록을 면적 기준으로 | `PlayerPaintCanvas` | §8.16, §8.17 |
| 11 | `NetKeys`/`NetEventCodes`/`RoomState`를 `Core/`로 이동, 죽은 코드 정리, UI 문자열 현지화 분리 | 정리 | §7, §8.21 |

**검증 권장**: 1~4번을 고친 뒤 ParrelSync나 빌드 + 에디터 2~4개 클라이언트로 "대기실 → 가마솥 → 시작 →
색칠 → 괴물 합류 → 처형 3회 → 결과 → 대기실 → 두 번째 판"을 끝까지 한 번 돌려봐야 한다. 이번 보고서의
모든 항목은 정적 분석 결과이므로, 특히 §8.2(서버 응답 전 첫 프레임 타이밍)와 §8.3(캡슐이 몸 메시를
완전히 감싸는지)은 Play Mode에서 한 번 확인하는 것이 좋다.

---

## 11. 사용자 주석 확인 (2026-09-26 19시, 커밋 전 작업 트리 기준)

`Bug-fix-plan.md` 맨 끝에 사용자가 남긴 주석:

> **추가적으로 괴물이 쿠키를 파괴했을 때 파괴된 쿠키의 이름표가 사라지지 않는 버그 및 쿠키처럼 몬스터가 맵(Ground) 밖으로 벗어났을 때 스폰 되지 않는 현상을 발견**

두 증상 모두 현재 코드에서 원인을 확인했다(정적 분석 — Play Mode 재현은 구현 단계에서 수행).

### 11.1 ㉕ 파괴된 쿠키의 이름표가 사라지지 않음 — 원인 확정 → ✅ 수정 완료(Bug-fix-plan.md §31)

- 쿠키 머리 위 이름표는 `HideOrSeekPlayer.prefab`의 자식 `Nameplate`(TextMeshPro, `PlayerBillBoard`)이며, 자체 `MeshRenderer`로 그려진다.
- 파괴 표시 담당 `CookieLifeStatePresenter.ApplyBroken()`(`ColorTag/CookieLifeStatePresenter.cs:82-83`)은
  **인스펙터로 연결된 `bodyRenderer`(몸통 `SkinnedMeshRenderer`) 하나만** 끈다. 콜라이더(모든 자식)와 레이어(모든 자식)는
  전체를 순회하도록 §29에서 바꿨지만, **렌더러는 "몸통 하나" 계약이 그대로 남아** 이름표·기타 표시물이 공중에 남는다.
- 같은 뿌리의 잠재 문제: 향후 쿠키에 모자·그림자 블롭·이펙트 같은 표시물을 추가해도 파괴 시 숨겨지지 않는다.

**권장 수정(확장성)**: "파괴 시 숨길 것"을 "몸통 렌더러"가 아니라 **캐릭터의 모든 표시 요소**로 정의한다.
- 모든 `Renderer`(SkinnedMeshRenderer·MeshRenderer — TextMeshPro 포함)를 순회해 원래 enabled 상태를 저장하고 끈다(콜라이더·레이어와 같은 방식, 복원 가능).
- 예외(파괴 후에도 보여야 하는 것 — 예: 향후 "파편" 이펙트)는 `[SerializeField] Renderer[] keepVisibleWhenBroken` 목록으로 제외한다. 기본은 전부 숨김.
- `PlayerBillBoard`는 표시 담당이 아니므로 수정하지 않는다(렌더러만 꺼지면 충분).

### 11.2 ㉖ 괴물이 맵 밖으로 떨어져도 리스폰되지 않음 — 원인 확정 → ✅ 수정 완료(Bug-fix-plan.md §31)

- 맵 밖 낙하 처리는 두 곳뿐이며 **둘 다 쿠키 전용**이다.
  - `GameManager/VoidKillZone.cs:10-12` — 트리거에 들어온 콜라이더에서 `GetComponentInParent<HideOrSeekPlayer>()`만 찾는다. 괴물(`MonsterController`)은 무시된다.
  - `Unit/HideOrSeekPlayer.cs:272-273` — `y < -100` 최후 방어선도 `HideOrSeekPlayer.FixedUpdate` 안에만 있다.
- `MonsterController`에는 리스폰 메서드도, 낙하 방어선도 없다. 괴물은 Rigidbody 물리(중력)로 움직이므로 지형 밖으로 나가면 **무한 낙하**하고,
  괴물 소유 클라이언트는 게임을 계속할 수 없다(쿠키만 남아 생존 시간 종료까지 진행).
- 구조적 원인: 쿠키와 괴물이 "조작 가능한 캐릭터"라는 공통 추상화를 공유하지 않아, 쿠키에 추가한 공통 기능(낙하 복귀)이 괴물에 전파되지 않았다(§12.2 E1).

**권장 수정(확장성)**
- 공통 인터페이스 `IRespawnable { bool IsLocallyControlled { get; } void RespawnToSpawnPoint(); }`를 `Core/`에 두고
  `HideOrSeekPlayer`·`MonsterController`가 구현한다. `VoidKillZone`은 `GetComponentInParent<IRespawnable>()`로 **캐릭터 종류와 무관하게** 처리한다.
- 낙하 방어선(`y < 설정값`)은 캐릭터마다 복붙하지 말고 공용 컴포넌트 `FallGuard`(`IRespawnable`을 호출)로 분리해 두 프리팹에 부착한다.
  임계 높이는 `GameSettingsSO`로 옮긴다.
- 괴물 리스폰 위치는 `MonsterSpawnPos` + `SpawnPositionFinder`(이미 괴물 스폰에 사용 중)로 쿠키와 같은 방식. 리스폰 시 `rb.linearVelocity=0`,
  `rb.position` 대입(쿠키에서 확인된 "비키네마틱 Rigidbody는 transform만 바꾸면 되돌아감" 문제 동일 적용), 진행 중이던 돌진은 취소.

---

## 12. 전체 코드 확장성 검토 (2026-09-26, §23~§29 반영 후 작업 트리 기준) — 반영 현황은 §12.6

### 12.0 검토 범위와 방법

- 대상: `Assets/02. Scripts/` 8개 도메인 **60개 `.cs`, 4,507줄** + `Assets/Editor/UILayoutValidator.cs`(92줄), `Resources/GameSettings.asset`.
  - 도메인별: Core 10개(373줄) · Lobby 5(438) · GameManager 5(337) · Unit 8(801) · Camera 1(137) · ColorTag 9(932) · Monster 20(1,419) · Dev 2(70).
- 관점: "인원·괴물 수·페이즈·캐릭터 종류·UI 요소·입력이 늘어날 때 **몇 군데를 고쳐야 하는가**", 문자열·수치 하드코딩, 중복, 결합도, 테스트 가능성.
- 근거는 전부 grep/소스 직접 확인 결과이며, 수치(개수·줄 번호)는 이 시점 기준이다.

### 12.1 한눈에 보기

| 영역 | 평가 | 요약 |
|---|---|---|
| 인원·괴물 수 | 🟢 양호 | `GameSettingsSO`(최대 인원·괴물 수·시간·슬롯) 한 곳, 괴물 목록은 처음부터 배열, 결과 아이콘 동적 생성 |
| 네트워크 상태 키·조회 | 🟢 양호 | `NetKeys`/`NetEventCodes`/`RoomState`(Core) 공용화, 안전 캐스트, 판 초기화 키 목록 `RoundRoomKeys` |
| 방장·씬 전환 | 🟢 양호 | 방장 정책 단일 컴포넌트 + 순수 함수, 방 전체 씬 전환 단일 경로(`RoomSceneTransition`) |
| 방장 전용 작업의 재실행 안전성 | 🟢 양호 | 처리 여부를 Room Prop으로 판정(방장 교체에 안전) |
| UI 표시 패턴 | 🟢 양호 | `CanvasGroupVisibility`, `PhaseCountdownDisplay`(페이즈 enum), 레이아웃 검사 도구 |
| **캐릭터 공통 추상화** | 🔴 부족 | 쿠키/괴물 간 공통 인터페이스 없음 → 기능 누락(㉖)·타입 분기·코드 중복 |
| **게임 진행 단계(페이즈)** | 🟠 보통 | 단계가 Room Prop 조합으로 암묵적 표현, 6개 방장 컴포넌트가 각자 폴링 → 새 단계 추가 비용 큼 |
| 씬·애니메이터·RPC 문자열 계약 | 🟠 보통 | `GameObject.Find` 스폰 지점 4곳(리터럴 중복 1), RPC 이름 문자열 6곳, 트리거명=enum명 |
| UI 요소 개수 | 🟠 보통 | 색 스와치 10개·스킨 버튼 3개가 씬에 수동 배치(데이터와 개수 불일치 가능) |
| 입력 | 🔴 부족 | 레거시 `Input` 7개 클래스 18곳, 키 하드코딩(관전 키만 직렬화), 설치된 Input System 미사용 |
| 클래스 크기·책임 | 🟠 보통 | `PlayerPaintCanvas` 489줄, `HideOrSeekPlayer` 421줄에 여러 책임 혼재 |
| 튜닝 수치 | 🟠 보통 | 전역 규칙 수치는 설정화됐으나 캐릭터 능력치(돌진·그랩·스폰 범위 등)는 상수로 분산 |
| 네트워크 부하의 인원 확장 | 🟠 주의 | 메시지 수가 인원에 대해 O(n²) — 8인 이상에서 무료 플랜 권장치 초과 가능(§12.4) |
| 자동 테스트 | 🔴 없음 | Test Framework 패키지는 의존성으로 설치돼 있으나 테스트 0개 |
| 지역화 | 🟠 보통 | 코드 내 한글 0(규칙 준수), 문구는 씬·프리팹 인스펙터에 분산(문자열 테이블 없음) |

### 12.2 확장성 약점 (우선순위순)

**E1 🔴 쿠키/괴물 공통 추상화 부재** — 가장 큰 구조적 약점, 실제 결함(㉖)으로 이어졌다.
- `HideOrSeekPlayer`(쿠키)와 `MonsterController`(괴물)가 공통 인터페이스·기반 클래스 없이 각자 구현.
  - 낙하 복귀: 쿠키만 있음(§11.2).
  - 관전 카메라 설정: `SpectatorController.GetCameraSettings()`가 `MonsterController`인지 타입으로 분기하고, 쿠키 값은 `Camera_Ctrl.CookieTargetHeight` 상수에서 가져온다 — 캐릭터 종류가 늘 때마다 분기 추가 필요.
  - 네트워크 동기화: `PlayerNetworkSync` vs `MonsterNetworkSync`는 상태 enum 타입과 캐리 필드 하나만 다른 거의 같은 코드(diff 확인).
  - 이동 입력(카메라 기준 WASD)·Rigidbody 초기화·회전 적용도 두 클래스에 각각 구현.
- **권장**: `Core/`에 작은 인터페이스들로 계약만 공유(도메인 간 참조 방향 유지).
  - `IRespawnable`(§11.2), `ICameraFollowTarget { float CameraTargetHeight; float CameraDistance; }`(관전·조작 카메라 공용), `ISpectatable`.
  - `NetworkTransformSync<TState> where TState : Enum` 제네릭 하나로 두 Sync 통합, 추가 필드는 확장 콜백.
  - 새 캐릭터 종류(예: 두 번째 괴물 타입)가 생겨도 인터페이스만 구현하면 스폰·관전·낙하·동기화가 동작.

**E2 🟠 게임 진행 단계가 암묵적**
- 현재 단계는 `PaintPhaseEndTime`/`MonsterJoined`/`GameEndTime`/`GameResult`/`MonsterDepartedAt` 조합으로만 표현되고,
  방장 전용 6개 컴포넌트(`GamePhaseStarter`·`PaintPhaseController`·`MonsterJoinController`·`GameRuleController`·`MonsterAssignmentAuthority`·`RoomLifecycleWatcher`)가 각자 `Update`에서 조건을 폴링한다.
  표시 쪽(`PhaseCountdownDisplay`·`ColorSelectionPanel`·`PlayerPaintCanvas`·`RoundStateResetter.HasFinishedRoundTrace`)도 같은 조합을 각자 해석한다.
- 새 단계(예: "색칠 후 숨기 준비 10초")를 넣으려면 **최소 6~8개 파일의 조건을 함께** 고쳐야 하고, 해석이 어긋나면 이번처럼 단계 경계 버그가 생긴다.
- **권장**: Room Prop `GamePhase`(enum: Lobby/Paint/Hunt/Result) + 단계 종료 시각 하나로 명시하고, 방장 전용 단일 `GameFlowController`가 전이를 담당. 각 컴포넌트는 "현재 단계"만 구독. 기존 키는 호환 기간 동안 유지.

**E3 🟠 문자열 계약**
- 스폰 지점: `GameObject.Find("PlayerSpawnPos"/"MonsterSpawnPos")` 4곳 — `HideOrSeekPlayer.RespawnToSpawnPoint()`는 `PlayerSpawner`의 상수를 쓰지 않고 **리터럴을 중복**. 스폰 지점을 여러 개로 늘리기도 어렵다.
  → `SpawnPoint` 컴포넌트(역할 enum: Cookie/Monster, 여러 개 허용) + 레지스트리. 인원이 늘면 스폰 지점 여러 곳에 분산.
- RPC 이름 문자열 6곳(`"LogMsg"`·`"RequestGrabKill"`·`"OnGrabbedByOwner"`·`"OnReleased"`) → `nameof(HideOrSeekPlayer.RequestGrabKill)` 등으로 컴파일 타임 검증.
- 애니메이터: 트리거명=`enum.ToString()`, 상태명 `"Jump"`, 레이어명 `"Carry"`(누락 시 경고는 있음). `"curScn"`(PUN 내부 키, 1곳 격리됨), `"Mesh_0"`(1곳, 누락 시 오류 로그).

**E4 🟠 UI 요소 개수가 데이터와 분리**
- 색 스와치: 씬에 `Swatch0~9` 10개를 수동 배치하고 각 버튼에 `colorIndex`를 직접 입력 — `ColorPaletteSO` 색 수를 바꾸면 UI는 따라가지 않는다.
- 스킨: `PlayerSkinSelector`에 A/B/C 버튼 3개 필드가 하드코딩, `PlayerSkinApplier.skins` 배열과 개수 계약이 암묵적.
- **권장**: 팔레트·스킨 배열에서 버튼을 템플릿으로 생성(결과 화면 쿠키 아이콘과 같은 방식, §27 Q7).

**E5 🔴 입력 처리 분산**
- 레거시 `Input.GetKey/GetAxis/GetMouseButton`이 7개 클래스 18곳(`Camera_Ctrl`·`PlayerPaintCanvas`·`GameManager`·`MonsterController`·`SpectatorController`·`HideOrSeekPlayer`·`PlayerGrabController`).
  키(Shift·Ctrl·Space·E·Enter·우클릭)가 하드코딩(관전 키만 직렬화) — 키 설정 UI·게임패드 지원 시 전부 수정 필요.
- 프로젝트에 `com.unity.inputsystem` 1.14.2와 `InputSystem_Actions.inputactions`가 이미 있으나 미사용.
- **권장**: 입력을 한 곳(`PlayerInputReader` 또는 Input Actions 래퍼)에서 읽고, 각 컴포넌트는 "의도(이동 벡터·점프·대시·그랩·시점 회전)"만 받는다.

**E6 🟠 큰 클래스의 책임 혼재**
- `PlayerPaintCanvas`(489줄): 캔버스 RT·스킨 합성·입력·보간·슬롯 규칙·네트워크 묶음 전송·수신 재생·콜라이더 베이크·강제 도포.
- `HideOrSeekPlayer`(421줄): 입력·물리 이동·점프/회피·캐리 추종·파괴 처리·리스폰·동기화.
- **권장**: 규칙(슬롯 등록)·전송(StrokeBatcher)·입력·표면 베이크를 협력 클래스로 분리(기존 `PlayerGroundDetector`/`PlayerAnimationDriver`처럼 "조정자 + 순수 C# 협력 클래스" 패턴을 이미 쓰고 있으므로 같은 스타일로).

**E7 🟠 튜닝 수치 분산**
- 전역 규칙 수치는 `GameSettingsSO`로 모았으나, 캐릭터 능력치는 상수로 흩어져 있다:
  `MonsterTentacleDash`(사거리 20·시간 0.25·쿨 15·반경 0.4), `PlayerGrabController.grabRange`, `PlayerSpawner.SpawnRange 5`, `MonsterJoinController.MonsterSpawnRange 4`,
  `SpawnPositionFinder` 캡슐 치수, `DefaultKeepAliveSeconds 60`(**2곳 중복**), 낙하 임계 `-100`.
- **권장**: 캐릭터별 `CookieStatsSO`·`MonsterStatsSO`(괴물 타입이 늘면 SO만 추가), 네트워크 상수는 `Core/NetworkDefaults` 한 곳.

**E8 🟠 판 초기화 키 목록의 수동 관리**
- `NetKeys.RoundRoomKeys`/`RoundPlayerKeys`에 새 키를 **직접 추가해야** 초기화된다. 잊으면 두 번째 판에서만 드러나는 버그가 된다(§8.7 유형).
- **권장**: 키 정의 시 수명(Round/Room/Session)을 함께 선언하는 레지스트리, 또는 EditMode 테스트로 "Round 접두/분류 키가 목록에 모두 있는지" 검사.

**E9 🟠 표시/상태 분리가 부분적** — §11.1의 원인. 콜라이더·레이어는 "전체 순회", 렌더러는 "지정 1개"로 규칙이 섞여 있다. 표시 요소 규칙을 한 방식으로 통일해야 새 표시물 추가 시 누락이 없다.

**E10 🔴 자동 테스트 없음**
- 테스트 폴더 없음(※ 정정: 게임 코드 asmdef `TagOfChaos.Scripts`는 있었다 — §12.6). 이번까지 검증은 전부 에디터 Play Mode 수동·반자동이었고, **오프라인의 한계**(단일 소유자, 즉시 반영되는 Props)로 온라인 전용 결함(⑰-A·⑱·㉓)을 놓쳤다.
- 이미 테스트하기 좋은 순수 함수가 있다: `RoomState.DesiredMasterActor(int[], int[])`, `GameSettingsSO.MonsterCountFor`, `SpawnPositionFinder`, `PhaseCountdownDisplay` 조건, `UILayoutValidator`.
- **권장**: EditMode 테스트 asmdef 추가(순수 규칙) + `UILayoutValidator`를 테스트로 감싸 CI 대용. 온라인 결함은 "Props 반영 지연" 시뮬레이션 헬퍼로 일부 재현.

**E11 🟠 지역화** — 코드 내 한글 문자열 0(CLAUDE.md 준수). 다만 UI 문구가 씬·프리팹 인스펙터 필드 30여 곳에 분산돼, 언어 추가·문구 일괄 수정 시 씬마다 찾아야 한다. → `LocalizedTextSO`(키→문구) 또는 Unity Localization 패키지.

**E12 🟢~🟠 정적 상태** — `PaintPhaseController.IsPaintScene`, `PlayerPaintCanvas.Local`, `GameSettings` 캐시, `OfflineModeBootstrap.SpawnAsMonster`, `SpectatorController.SpectateTargetChanged`.
단일 씬·클라이언트당 로컬 쿠키 1개 전제에서는 문제없고 탐색 비용을 없앴지만, **가산(Additive) 씬 구성**이나 "한 클라이언트가 여러 캐릭터 조작"으로 확장할 때는 전제가 깨진다. 전제를 주석으로 명시해 두었다.

**E13 🟠 도메인 경계 명칭** — `ColorTag/`에 파괴 표시(`CookieLifeStatePresenter`)가, `Monster/`에 게임 진행·대기실·결과·관전·방장 정책까지 모여 있다(20개 파일). 기능 추가 위치가 직관적이지 않다.
→ `GameFlow/`(페이즈·판정·결과·초기화·방장), `Spectate/`, `Character/`(공통 인터페이스) 등으로 재배치 권장(파일 이동은 `.meta` 유지로 참조 보존).

### 12.3 인원 확장 시나리오 점검 — "8인, 괴물 2명"으로 설정을 바꾼다면

| 영역 | 설정만으로 동작? | 근거 / 필요한 추가 작업 |
|---|---|---|
| 방 생성·목록·대기실 목록 | ✅ | `RoomOptions.MaxPlayers = GameSettings.MaxPlayers`, 목록 "N / 정원" |
| 시작 조건(정원 충족 + 괴물 확정) | ✅ | 방의 `MaxPlayers`·`MonsterCountFor(인원)` 기준 |
| 괴물 선정(가마솥 선착순 + 타임아웃 무작위) | ✅ | 자리 수만큼 채움, 대기실 이탈 시 해당 자리만 재선정 |
| 방장 정책 | ✅ | 괴물 전원 제외 후 최소 ActorNumber(순수 함수 검증 완료) |
| 괴물 대기실 대기·합류 | ✅ | 각 괴물 클라이언트가 독립적으로 대기·스폰(스폰 위치 분산) |
| 쿠키 스폰 | 🟠 | 스폰 지점 1곳 ±5m + 겹침 검사 16회 — 8인은 가능, 그 이상은 **스폰 지점 여러 개**(E3) 권장 |
| 색칠·강제 도포 | 🟠 | 색 10개 초과 인원은 순환 배정(색 중복 허용). 스와치 UI는 팔레트 개수와 연동 안 됨(E4) |
| 판정·결과 화면 | ✅ | 괴물 배열 기준 판정, 결과 행·쿠키 아이콘 동적 생성 |
| 관전 | ✅ | 후보 목록 동적, 입장 순 정렬 |
| **네트워크 부하** | ⚠️ | 아래 §12.4 |

### 12.4 네트워크 부하의 인원 확장성 (주의)

- Photon 메시지는 **보낸 사람 수 × 받는 사람 수**로 계산된다. 현재 주요 송신원:
  - 캐릭터 위치 동기화: PhotonView 기본 10회/초 × 캐릭터 수 × (인원-1)
  - 색칠 스탬프 묶음: 최대 15회/초 × 동시에 칠하는 쿠키 수 × (인원-1)
- 4인(쿠키 3 동시 색칠): 위치 약 4×10×3=120 + 색칠 3×15×3=135 ≈ **255 msg/s**
- 8인(쿠키 6 동시 색칠): 위치 약 8×10×7=560 + 색칠 6×15×7=630 ≈ **1,190 msg/s** — Photon 무료 플랜 권장치(방당 약 500 msg/s)를 크게 넘는다.
- **권장**: 인원 확대 전 ① 색칠 묶음 주기를 `GameSettingsSO`로 빼고 인원에 따라 낮춤(예: 8인 5Hz), ② 색칠 중에는 위치 동기화 빈도 하향(`PhotonNetwork.SerializationRate`),
  ③ 색칠 결과를 "스탬프 스트림" 대신 페이즈 종료 시 압축 텍스처 1회 전송하는 방식 검토, ④ 인원별 메시지 수를 Photon 대시보드로 실측.

### 12.5 권장 로드맵 (확장성 관점)

| 순서 | 작업 | 효과 | 관련 |
|---|---|---|---|
| 1 | §11 두 결함 수정: 파괴 시 모든 렌더러 숨김(`keepVisibleWhenBroken` 예외), `IRespawnable` + `FallGuard`로 괴물 낙하 복귀 | 사용자 제보 해결 + E1 첫 단계 | ㉕ ㉖ |
| 2 | 캐릭터 공통 인터페이스(`IRespawnable`·`ICameraFollowTarget`) + Sync 제네릭 통합 | 새 캐릭터 종류 추가 비용↓, 타입 분기 제거 | E1 |
| 3 | EditMode 테스트 asmdef + 순수 규칙 테스트 + 레이아웃 검사 테스트 | 회귀 방지 | E10 |
| 4 | `SpawnPoint` 컴포넌트·레지스트리, RPC `nameof` | 문자열 계약 제거, 다중 스폰 지점 | E3 |
| 5 | 입력 단일화(Input System Actions 래퍼) | 키 설정·패드 대비 | E5 |
| 6 | 명시적 `GamePhase` + 단일 흐름 컨트롤러 | 새 단계 추가 비용↓ | E2 |
| 7 | 인원 확대 전 네트워크 부하 조정 | 8인 이상 대비 | §12.4 |
| 8 | 스와치·스킨 버튼 데이터 기반 생성, 캐릭터 능력치 SO, 도메인 재배치, 문자열 테이블 | 콘텐츠 확장 편의 | E4 E6 E7 E11 E13 |

> 결론: §23~§29로 **"인원·괴물 수·방장·씬 전환·판 초기화"** 축의 확장성은 설정 한 곳(`GameSettings.asset`)에서 조절 가능한 수준까지 올라왔다.
> 반면 **"캐릭터 종류"(E1)·"입력"(E5)·"테스트"(E10)** 축은 아직 확장에 취약하며, 사용자가 발견한 괴물 낙하 미복귀(㉖)가 바로 E1의 증상이다.
> 인원을 8명 이상으로 늘리기 전에는 §12.4의 네트워크 부하 조정이 선행돼야 한다.


### 12.6 반영 현황 (2026-09-26 갱신 — Bug-fix-plan.md §31·§32 구현 후)

§12.0~§12.5는 검토 당시 기록으로 그대로 두고, 이후 수정 결과를 여기에 정리한다.
검증: 컴파일 오류 0·게임 코드 경고 0, **EditMode 테스트 11/11 통과**, 오프라인 Play Mode 실동작 확인(Bug-fix-plan.md §31.1, §32.1).

> **정정**: §12.2 E10에 "테스트 폴더·asmdef 없음"이라고 적었으나, `Assets/02. Scripts/TagOfChaos.Scripts.asmdef`는 **이미 있었다**(없던 것은 테스트 어셈블리와 테스트). 이 asmdef 덕분에 테스트 어셈블리가 게임 코드를 바로 참조할 수 있었다.

#### 12.6.1 약점별 상태

| # | 약점 | 상태 | 반영 내용 | 남은 것 |
|---|---|---|---|---|
| E1 | 쿠키/괴물 공통 추상화 부재 | 🟢 해소 | `IRespawnable`(낙하 복귀), `ICameraFollowTarget`·`IGameCharacter`(역할·관전 가능 여부·카메라 높이/거리), `CharacterRegistry`(활성 캐릭터 목록). `SpectatorController`의 타입 분기·`FindObjectsByType` 제거. `PlayerNetworkSync`/`MonsterNetworkSync` → 공용 제네릭 `NetworkTransformSync<TState>`(직렬화 순서 동일, 테스트로 고정). 공용 `FallGuard` | 이동 물리(쿠키·괴물 각자)는 규칙이 달라 유지 |
| E2 | 게임 진행 단계가 암묵적 | 🟡 부분 | `GamePhase`(Lobby/Paint/AwaitingMonster/Hunt/Result) + `GamePhaseState` — Props 조합 해석을 한 곳으로. 색칠 캔버스·붓 커서·색칠 패널·카운트다운·승패 판정이 이 한 곳을 쓴다. 네트워크 프로토콜 불변 | 단계 **전이**(방장 전용 컴포넌트 여러 개가 각자 Props 기록)는 그대로 — 단일 `GameFlowController`는 빌드 멀티 검증과 함께 별도 진행 권장 |
| E3 | 문자열 계약 | 🟢 해소 | 스폰 지점 이름·조회 `SceneSpawnPoints` 한 곳, RPC 이름 6곳 → `nameof` 기반 상수(`HideOrSeekPlayer.Rpc*`, `GameManager.RpcLogMsg`) | 애니메이터 상태·레이어 이름은 `PlayerAnimationDriver` 안 상수로 이미 한 곳(누락 시 경고) |
| E4 | UI 요소 개수가 데이터와 분리 | 🟢 해소 | `ColorSwatchGroup`: 팔레트 색 수·색으로 스와치 생성(12색·5색으로 바꿔 생성 확인). `SkinCatalogSO` + `PlayerSkinSelector`: 스킨 목록 수만큼 버튼 생성, `PlayerSkinApplier`도 같은 목록 사용. 공용 `UiListBuilder` | 옛 `ColorSelectionPanel.prefab`(PlayerTestScene 전용)은 수동 배치 유지 |
| E5 | 입력 처리 분산 | 🟢 해소 | `InputBindingsSO`(Resources/InputBindings) + `PlayerInput` 정적 파사드. 레거시 `Input` 직접 호출 **18곳 → 0곳**(파사드 제외). 관전 키 직렬화 필드도 설정으로 통합 | Input System 패키지 전환은 이 파일만 바꾸면 됨(키 설정 UI와 함께) |
| E6 | 큰 클래스 | 🟡 부분 | `HideOrSeekPlayer` 421→389줄(캐리 추종 → `PlayerCarryFollower`, 입력 → `PlayerInput`), `PlayerPaintCanvas`에서 콜라이더 갱신 → `PaintColliderUpdater` | `PlayerPaintCanvas`는 ㉘의 스탬프 일괄 그리기가 더해져 552줄 — 스탬프 전송/그리기를 협력 클래스로 더 나눌 여지 |
| E7 | 튜닝 수치 분산 | 🟢 해소 | 괴물 돌진 4개 수치, 쿠키/괴물 스폰 범위, 낙하 높이, 색칠 콜라이더 갱신 간격, 네트워크 전송 빈도 → `GameSettingsSO`. KeepAlive 기본값 중복 → `NetworkDefaults` | 채팅 최대 줄 수 등 UI 상수는 유지(규칙 수치 아님) |
| E8 | 판 초기화 키 수동 관리 | 🟢 해소 | `NetKeys.Scopes` 표에 키마다 대상(Room/Player)·수명(Round/Session) 선언, 초기화 목록은 표에서 자동 생성. 표에 없는 키는 EditMode 테스트가 실패로 알림 | — |
| E9 | 표시/상태 분리 부분적 | 🟢 해소 | 파괴 시 렌더러도 콜라이더·레이어와 같은 "전체 순회 + 저장·복원" 규칙(㉕) | — |
| E10 | 자동 테스트 없음 | 🟢 해소 | `Assets/Editor/Tests`(asmdef `TagOfChaos.EditorTests`) EditMode 11개: 방장 정책, 괴물 수, 색칠 전송 간격, 키 수명 분류 누락, 판 초기화 목록, 게임 단계 해석, 스킨 인덱스 보정, 쿠키/괴물 동기화 직렬화 왕복, **UI 화면 밖 배치 0건**(`UILayoutValidator`를 `TagOfChaos.Editor` asmdef로 분리해 테스트에서 호출) | 온라인 전용 결함(Props 반영 지연)을 흉내 내는 PlayMode 테스트는 미작성 |
| E11 | 지역화 | ⏸ 보류 | — | 씬·프리팹 문구 30여 곳을 문자열 테이블로 — 콘텐츠 작업이라 별도 진행 |
| E12 | 정적 상태 | 🟢 개선 | 로컬 쿠키·관전 후보 탐색이 `CharacterRegistry`로 바뀌어 게임 코드의 `FindObjectsByType` **6곳 → 0곳** | `PlayerPaintCanvas.Local` 등 "클라이언트당 로컬 캐릭터 1개" 전제는 유지(주석으로 명시) |
| E13 | 도메인 폴더 명칭 | ⏸ 보류 | — | 커밋되지 않은 변경이 많아, 커밋 후 `.meta` 유지 이동으로 별도 진행 권장 |
| §12.4 | 인원 증가 시 네트워크 부하 | 🟢 대비 | 색칠 전송 간격을 인원에 맞게 자동 조정(4명 이하 1/15초 그대로, 8명 약 1/6.4초, 하한 5Hz), 캐릭터 동기화 빈도 `SerializationRate`를 설정값으로. 8인 추정 색칠 메시지 630 → 약 270 msg/s | 실측(Photon 대시보드)은 인원 확대 시 |

#### 12.6.2 갱신된 한눈에 보기(§12.1 대비)

| 영역 | 이전 | 현재 |
|---|---|---|
| 캐릭터 공통 추상화 | 🔴 부족 | 🟢 양호 — 새 캐릭터 종류는 `IGameCharacter`·`IRespawnable` 구현 + 등록으로 관전·카메라·낙하 복귀·동기화가 동작 |
| 게임 진행 단계 | 🟠 보통 | 🟡 해석은 한 곳, 전이는 분산 |
| 문자열 계약 | 🟠 보통 | 🟢 양호 |
| UI 요소 개수 | 🟠 보통 | 🟢 양호 — 팔레트·스킨 에셋만 바꾸면 UI가 따라감 |
| 입력 | 🔴 부족 | 🟢 양호 — 파사드 1개 + 설정 에셋 |
| 클래스 크기·책임 | 🟠 보통 | 🟡 일부 개선 |
| 튜닝 수치 | 🟠 보통 | 🟢 양호 — 규칙·능력치·네트워크 빈도 모두 `GameSettings.asset` |
| 네트워크 부하의 인원 확장 | 🟠 주의 | 🟢 자동 조정 |
| 자동 테스트 | 🔴 없음 | 🟢 EditMode 11개(규칙·직렬화·UI 배치) |
| 지역화 | 🟠 보통 | 🟠 보통(보류) |

#### 12.6.3 규모 변화
- `Assets/02. Scripts`: 60개 4,507줄 → **73개 5,250줄**. 공용 계약·설정·협력 클래스 14개가 새로 생겼다(Core 9개: IRespawnable·SceneSpawnPoints·NetworkTransformSync·GameCharacter·InputBindingsSO·PlayerInput·NetworkDefaults·UiListBuilder·GamePhaseState / 그 외 FallGuard·PaintColliderUpdater·PlayerCarryFollower·ColorSwatchGroup·SkinCatalogSO). 대신 중복 클래스 1개(`MonsterNetworkSync`)가 삭제됐고, 같은 조건·수치·문자열이 여러 파일에 반복되던 곳이 한 곳으로 모였다.
- 새 에셋: `Assets/Resources/InputBindings.asset`, `Assets/03. SO/Unit/SkinCatalog.asset`. 씬: GameScene `SwatchRow`에 `ColorSwatchGroup`, GameLobbyScene `SkinSelectPanel`의 `PlayerSkinSelector` 연결 변경. 프리팹: 쿠키 `PlayerSkinApplier.catalog`, 쿠키·괴물 `FallGuard`.

> 갱신된 결론: 인원·괴물 수 축에 더해 **캐릭터 종류·입력·UI 데이터·튜닝 수치·네트워크 부하** 축도 코드 수정 없이 에셋·설정으로 늘릴 수 있게 됐다.
> 규칙 회귀는 EditMode 테스트가 막는다. 남은 큰 과제는 ① 단계 전이 단일화(E2) ② 지역화(E11) ③ 폴더 재배치(E13) ④ 온라인 지연을 재현하는 PlayMode 테스트다.
> 모두 동작 변경 위험이 있거나 콘텐츠 작업이라, 이번 변경의 **빌드 멀티 확인 후** 진행하기를 권장한다.
