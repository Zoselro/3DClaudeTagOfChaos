# 조사 보고서: 프로젝트 전체 심층 분석 + 아키텍처 체크리스트 (2026-08-17)

> 이 문서는 이전 버전(2026-08-16, `ColorTag/` 폴더 15개 파일만 라인 단위로 분석하고 나머지 6개
> 도메인은 개요 수준으로 남겨뒀던 버전)을 대체한다. 이번 조사는 **`Assets/02. Scripts/` 아래
> 7개 도메인 32개 `.cs` 파일 전체**를 예외 없이 라인 단위로 읽고, 4개 씬 파일(`LobbyScene`,
> `GameLobbyScene`, `GameScene`, `PlayerTestScene`)의 실제 GameObject 구성과 `HideOrSeekPlayer.prefab`의
> 컴포넌트 구성을 직접 대조해, 사용자가 지정한 11개 아키텍처 관점을 **ColorTag뿐 아니라 프로젝트
> 전체 기준**으로 재검증한 결과다. `git log`/`git diff`로 2026-08-16 이후 커밋(`f33af46`, `e29e67a`)과
> 현재 미커밋 변경사항(캐릭터 애니메이션 리소스 교체 작업 중)까지 반영했다.

---

## 0. 프로젝트 개요

**TagOfChaos**는 Unity(Built-in RP) + **Photon PUN2** 기반 2~4인 실시간 멀티플레이어 게임이다.
로비에서 방을 만들고 → 대기방에서 인원을 채우고 → 게임 씬에서 **4라운드 동안 팀 전체가 색을
투표·페인팅으로 정하고** → 그중 1명이 **술래**로 뽑혀 미묘하게 다른 색 조합을 부여받는 숨은
식별 메커니즘의 술래잡기형 게임을 지향한다. 술래잡기 본게임(추격/승패 판정) 자체는 아직
구현되지 않았다 — 현재까지 구현된 것은 "로비 → 매칭 → 색상 결정 미니게임"까지다.

**씬 구성(흐름 순서)**:
```
LobbyScene (방 목록/생성/랜덤입장)
   └─ PhotonNetwork.LoadLevel(GameLobbyScene)  ← 방을 새로 만든 최초 1인만 호출
GameLobbyScene (대기방, 인원 표시, 방장의 "게임 시작" 버튼)
   └─ PhotonNetwork.LoadLevel(GameScene)  ← 정원(4명) 찼을 때 방장만 호출
GameScene (실제 플레이 — 채팅 + 캐릭터 스폰까지만 배선됨, ColorTag 미시작)
```
`PlayerTestScene`은 Build Settings에 포함되지 않는 개발용 씬으로, `ColorTagManagers`
(`ColorSelectionManager`+`RoomLifecycleWatcher`+`BrushCursorController`)와 `ColorSelectionPanel`
UI를 수동으로 배치해 ColorTag 미니게임을 **직접 실행해볼 수 있는 유일한 씬**이다.

**스크립트 도메인** (`Assets/02. Scripts/` 아래 7개, 총 32개 `.cs`):

| 도메인 | 파일 수 | 역할 |
|---|---|---|
| `Core/` | 1 | 씬 이름 상수 (도메인 공통 참조) |
| `Lobby/` | 4 | 방 목록·생성·입장, 대기방 인원 표시 및 게임 시작 버튼 |
| `GameManager/` | 5 | 채팅 중계(RPC), 캐릭터 스폰, 나가기 확인창, 낙사 방지 |
| `Unit/` | 6 | 이동·점프·회피·애니메이션·네트워크 동기화(전투/HP 없음, 책임별 5+1클래스 분리) |
| `Camera/` | 1 | 3인칭 추적 카메라(우클릭 드래그 회전만, 줌 없음) |
| `ColorTag/` | 15 | 4라운드 색상 투표→페인팅→술래 색 치환 미니게임 |
| `Dev/` | 1 | 오프라인 개발 부트스트랩 |

(`Assets/02. Scripts/Player.md`는 `.cs`가 아니라 **다른 프로젝트의 참고용 코드+분석 메모**이며
이 프로젝트의 실제 스크립트가 아니다 — §7에서 별도로 짚는다.)

---

## 1. 도메인별 상세 동작

### 1.1 `Core/` — 씬 이름 상수 (1파일)
`SceneNames.cs`: `Lobby`/`GameLobby`/`Game` 3개 문자열 상수. GameManager/ColorTag/Lobby 세 도메인이
공통 참조하므로 특정 도메인에 속하지 않고 별도 폴더에 둔 것. 프로젝트 전체에서 씬 이름을 직접
문자열로 다루는 코드는 이 파일 안에만 존재한다(§5.4에서 검증).

### 1.2 `Lobby/` — 매치메이킹 (4파일)
- **`LobbyController`**: `PhotonNetwork.ConnectUsingSettings()` → `OnConnectedToMaster()` →
  `JoinLobby()` → 방 생성/랜덤입장/이름으로 입장 3가지 진입점 제공. `OnRoomListUpdate()`가
  `RoomInfo` 딕셔너리를 캐싱하고 `RoomListItem` 프리팹을 diff 방식으로 생성/갱신/삭제(전체 재생성
  아님). `MaxPlayers=4` 고정, `GameVersion="1"`로 매치메이킹 격리. 방을 새로 만든 최초 1인(`PlayerCount==1`)
  만 `OnJoinedRoom()`에서 `GameLobbyScene`으로 로드하고 나머지는 `AutomaticallySyncScene=true`로
  자동 동행.
- **`GameLobbyController`**: `OnPlayerEnteredRoom`/`OnPlayerLeftRoom`/`OnMasterClientSwitched`
  콜백 + `Update()` 안전망(늦게 입장한 클라이언트가 콜백을 놓쳤을 경우 인원수/마스터 여부를
  매 프레임 비교해 스스로 보정) 이중 구조로 대기방 UI를 갱신. 방장에게만 "게임 시작" 버튼을
  노출하고 정원이 찼을 때만 `interactable`. `OnStartGameButtonClicked()`가 `Room.IsOpen=false` +
  `PhotonNetwork.LoadLevel(SceneNames.Game)`만 호출 — **`ColorSelectionManager`를 전혀 참조하지
  않음**(§4.1의 핵심 문제).
- **`RoomListItem`/`PlayerListItem`**: 순수 표시용 리스트 아이템. `RoomListItem`은 `IsOpen`이
  `false`(이미 게임 시작됨)면 입장 버튼을 잠근다.

### 1.3 `GameManager/` — 채팅·스폰·나가기·낙사 (5파일)
- **`GameManager`**: 원래 이름과 달리 지금은 **채팅 중계 전담**으로 축소된 클래스(스폰/나가기는
  분리됨, §1.3 하단 참고). `static Inst` 필드를 갖는 **프로젝트 유일의 싱글턴**이지만
  실제로 `GameManager.Inst`를 읽는 코드는 프로젝트 전체에서 0건(grep으로 재확인) — 사실상 죽은
  코드. `Start()`가 `PhotonNetwork.InRoom`이 `true`가 될 때까지 코루틴으로 대기한 뒤에야
  "Connected" 로그를 RPC로 브로드캐스트(Start() 시점에 InRoom이 보장되지 않는다는 실측 문제,
  `Bug-fix-plan.md §12`, 아래 `PlayerSpawner`와 동일 패턴). Enter 키로 채팅 입력창 토글 +
  `is_Conversating` 플래그를 `HideOrSeekPlayer.IsMovementLocked`에 연결해 채팅 중 이동을 잠근다
  (`SetLocalPlayerMovementLocked()` — `FindObjectsByType`로 로컬 플레이어를 1회 탐색 후 캐싱).
- **`PlayerSpawner`**: 캐릭터 스폰 전담(`GameManager`에서 분리됨). 동일하게 `InRoom==true`가 될
  때까지 코루틴 대기 후 `"PlayerSpawnPos"` 오브젝트를 `GameObject.Find()`로 찾아 반경 5 랜덤
  오프셋으로 `PhotonNetwork.Instantiate("HideOrSeekPlayer", ...)` 호출. 스폰 성공/실패를 `ViewID`,
  `IsMine`, `RoomPlayerCount` 등과 함께 로그로 남겨 네트워크 가시성 문제 진단을 돕는다.
- **`ConfirmDialog`**: 재사용 가능한 예/아니오 확인창. `Action onYesConfirmed` 콜백을 인자로 받아
  특정 기능에 종속되지 않음 — `RoomExitController`가 유일한 현재 사용처.
- **`RoomExitController`**: 뒤로가기 버튼(확인창 → `PhotonNetwork.LeaveRoom()` → `OnLeftRoom()`에서
  `LobbyScene` 이동) 전담. `confirmDialog`가 인스펙터에 연결 안 돼 있으면 확인창 없이 즉시 나가는
  폴백 포함. 마지막 1인이 나갈 때 `Room.CustomProperties.Clear()`.
- **`VoidKillZone`**: `[RequireComponent(typeof(Collider))]` 트리거. 맵 밖으로 떨어진 로컬 플레이어를
  `HideOrSeekPlayer.RespawnToSpawnPoint()`로 되돌림 — `HideOrSeekPlayer.FixedUpdate()`의
  `y < -100f` 최후 방어선과 이중 안전망.

### 1.4 `Unit/` — 캐릭터 이동·애니메이션 (6파일, 책임별 분리)
Rigidbody 기반 물리 이동 + 5개 협력 클래스로 구성. `HideOrSeekPlayer`(MonoBehaviour)가 조정자이고
나머지 4개는 순수 C# 클래스(Unity 생명주기 없음, 유닛 테스트 가능):

- **`PlayerMoveState`**: `Idle/Walk/SneakWalk/Jump/Dodge` 5개 상태 enum. `Animator` 트리거 이름과
  `enum.ToString()`이 직접 매칭되므로(`animator.SetTrigger(newState.ToString())`) Animator
  Controller의 파라미터 이름과 이 enum 값이 반드시 일치해야 하는 암묵적 계약이 있다.
- **`PlayerGroundDetector`**: 순수 접지 판정 클래스. `Physics.Raycast`로 매 `FixedUpdate` 접지
  여부만 질의 — 이벤트 기반(`OnCollisionEnter`) 판정이 아니라 폴링 방식이라 "걸어서 낭떠러지를
  넘어가도 낙하가 시작되지 않는" 사각지대가 구조적으로 없음(`Player.md` 분석 메모가 지적한
  타 프로젝트의 결함과 대조되는 설계, §7 참고).
- **`PlayerAnimationDriver`**: 애니메이션 상태 전이 전담. `ChangeState()`는 코드상 상태 라벨이
  이미 같으면 무시하는 가드가 있고, 점프만은 `ReplayJump()`로 `Animator.Play("Jump", 0, 0f)`를
  직접 호출해 Any State→Jump 트랜지션의 크로스페이드 블렌딩을 우회한다(연타 재점프 시 중간
  포즈에서 시작되는 버그의 최종 수정, `Bug-fix-plan.md §18`). `HandleJumpAnimationHold()`가
  점프 애니메이션이 착지 전에 끝까지 재생돼버리는 것을 막기 위해 정점 부근에서 `animator.speed=0`
  으로 멈춰둔다.
- **`PlayerNetworkSync`**: `IPunObservable` 스트림 Write/Read + `Interpolate()`(거리 기반
  스냅/보간 이원화, `snapDistance=10` 초과 시 순간이동, 그 이하는 `Lerp`/`Slerp`) 전담.
- **`PlayerBillBoard`**: 캐릭터 머리 위 닉네임 표시. `this.transform`을 직접 회전시키는 구조라
  "간접 참조 대상을 잘못 회전시키는" 버그가 애초에 발생할 수 없는 설계(`PlayerColorVoteIndicator`가
  겪었던 문제와 대조, §7 참고).
- **`HideOrSeekPlayer`** (MonoBehaviourPunCallbacks, IPunObservable): 위 4개를 소유하는 조정자.
  - `Awake()`에서 `networkSync`를 **`IsMine` 여부와 무관하게 무조건 먼저 생성** — Photon의
    `OnPhotonSerializeView` 디스패치가 이 오브젝트의 `Start()`보다 먼저 들어올 수 있는 경쟁을
    원천 차단(`Bug-fix-plan.md §12`).
  - `Start()`에서 로컬(`IsMine`) 캐릭터만 `rb.useGravity=true`+`ContinuousDynamic`+`Interpolate`+
    `FreezeRotation`을 설정, 원격 캐릭터는 `isKinematic=true`로 물리를 꺼서 `networkSync`의 보간과
    충돌하지 않게 함. 캐릭터 몸통 메시(`Ch36`, 콘케이브 콜라이더 회피용 별도 키네마틱 Rigidbody)와
    루트 `CapsuleCollider` 사이의 자기 충돌을 `Physics.IgnoreCollision()`으로 명시적으로 무시
    (`Bug-fix-plan.md §14` — 안 하면 위치는 고정된 채 속도만 불규칙하게 커지는 버그).
  - `Update()`: 로컬은 입력만 기록(좌표 이동은 안 함), 원격은 `networkSync.Interpolate()` +
    `animationDriver.ChangeState(RemoteState)`.
  - `FixedUpdate()`: 점프 여부와 무관하게 매 스텝 접지를 재확인(과거엔 점프 중에만 판정해 걸어서
    낙하가 시작되지 않는 버그가 있었음, `§18.2`). 의도한 점프든 걸어서 벗어난 낙하든 공중에서는
    항상 자유롭게 방향 전환 가능하도록 통일(`§24`). `Move()`는 `rb.MoveRotation()`으로 회전(→
    `transform.rotation` 직접 대입 시 Rigidbody 보간과 어긋나 걷기가 버벅이는 버그, `§13`),
    수평 속도만 매 스텝 덮어쓰고 수직 속도(중력/점프)는 물리 엔진이 채운 값을 보존.
  - `RespawnToSpawnPoint()`: `rb.position`과 `transform.position`을 **둘 다** 갱신 — non-kinematic
    Rigidbody는 `transform.position`만 바꾸면 다음 물리 스텝에 되돌아가버림(Play Mode 실측 확인).

### 1.5 `Camera/` — 3인칭 카메라 (1파일)
**`Camera_Ctrl`**: `InitCamera(player)`가 `Awake()`/`Start()` 호출 순서와 무관하게 항상 정확히
초기화되도록 `ResetToDefaultView()`를 공유(먼저 호출되든 나중에 호출되든 안전). 우클릭 드래그로만
회전(수직 -7°~80° 클램프), `Quaternion.Slerp`로 부드럽게 추적. 과거 있었던 마우스 휠 줌 기능은
완전히 제거되어 거리(`m_DefaultDist`)가 고정값이고, 휠 입력은 `PlayerPaintCanvas.HandleBrushSizeInput()`
이 붓 크기 조절 전용으로 가져감(도메인 간 입력 경합을 코드에서 명시적으로 분리).

### 1.6 `ColorTag/` — 색상 투표·페인팅 미니게임 (15파일, 4계층)
Photon **PUN2**만 사용(Fusion 아님). 4라운드 동안 팀 전체가 색을 투표→다수결 확정→각자 캔버스에
페인팅, 4라운드 후 무작위 술래 1명에게 살짝 다른 색 조합(변형 세트)을 부여하는 흐름.

**데이터/설정 계층(SO, 2)**: `BrushSettingsSO`(붓 반경 범위·휠 감도·커서 프리팹), `ColorPaletteSO`
(고정 10색, 인덱스 범위 검사 없음 — 호출부가 항상 유효 인덱스만 넘긴다는 암묵 전제).

**순수 로직 계층(정적 클래스, Unity API 미사용, 4)**: `NetKeys`(CustomProperties 키 7개),
`NetEventCodes`(`PaintStroke=1`), `ColorVoteTally.Resolve()`(다수결, 동점 시 랜덤, 무투표 시 남은
색 중 랜덤), `TaggerColorAssigner`(변형 세트 생성/비교), `RoomState`(Room CustomProperties 조회
헬퍼 — 4개 파일의 중복 조회 로직을 통합).

**라운드 진행 계층(마스터 권위, 2)**: `ColorSelectionManager`(`Update()`에서 마스터만
`RoundEndTime` 만료 폴링 → 다수결 확정 → 4라운드 완료 시 술래 지정, `StartColorSelection()`이
유일한 진입점), `RoomLifecycleWatcher`(술래 퇴장/인원 1명 이하 시 즉시 종료, 정상 종료는
`GameEndTime` 폴링 → 방 유지한 채 `GameLobbyScene` 복귀).

**클라이언트 표현 계층(플레이어별, 6)**: `PlayerPaintCanvas`(캐릭터당 512×512 `RenderTexture`
런타임 생성, 좌클릭 UV 스탬프 + `RaiseEvent(PaintStroke, Others)` 전파, 알파 채널을 잠금
마스크로 사용, `OnDestroy()`에서 `Release()`), `BrushCursorController`(3D 커서 1회 인스턴스화 후
재사용, `PlayerCapsule` 레이어 마스킹으로 자기 캡슐이 레이캐스트를 막지 않게 함),
`ColorSelectionPanel`/`ColorSwatchButton`(라운드/시간 UI, 확정색 잠금, 클릭 시 투표 위임),
`PlayerColorVoteIndicator`(머리 위 스프라이트, `LateUpdate()`에서 인디케이터 트랜스폼만 회전),
`PlayerColorDisplay`(`[RequireComponent(PlayerPaintCanvas)]`, 술래 본인만 4라운드 후 변형 슬롯을
찾아 캔버스 전역 색 치환).

**셰이더 3종**: `PaintStamp`(원형 스탬프, `_RespectLock` 분기), `PaintColorReplace`(색상 거리 기반
전역 치환, 술래 전용), `PlayerPaintedSkin`(베이스 스킨+페인트 텍스처 알파 마스크 합성).

전체 흐름: `투표(Player 프로퍼티) → 마스터 다수결 확정(Room 프로퍼티) → 각 클라이언트가 자기
캔버스를 확정색으로 재도색 → 4라운드 후 술래 지정 → 술래 캔버스만 전역 색 치환`. 이 도메인은
2026-08-16 이후 코드 변경이 0건(`git diff 4268d13..HEAD`로 확인, 머티리얼 파라미터 2건만 변경)
이라 이전 조사와 완전히 동일한 상태다.

### 1.7 `Dev/` — 오프라인 개발 부트스트랩 (1파일)
**`OfflineModeBootstrap`**: `Awake()`에서 `PhotonNetwork.OfflineMode=true`. 체크박스
(`autoStartColorSelection`)가 켜져 있으면 Play 버튼만 눌러도 방 생성 + `ColorSelectionManager.
StartColorSelection()`을 자동 호출 — **`StartColorSelection()`을 호출하는 코드는 프로젝트 전체에서
이 파일 단 한 곳뿐**이며 `PlayerTestScene`에만 존재한다(§4.1과 직결).

---

## 2. 씬 배선 실측 (4개 씬 GameObject 대조)

| 씬 | 주요 GameObject | ColorTag 배선 |
|---|---|---|
| `LobbyScene` | `LobbyUICanvas`, `Main`(카메라), `Directional`, `EventSystem` | 없음(해당 없음) |
| `GameLobbyScene` | `GameLobbyUICanvas`, `GameManager`, `PlayerSpawnPos`, `VoidKillZone`, 로비 환경 오브젝트(의자/테이블/파라솔/펜스 등) | **없음** |
| `GameScene` | `GameManager`, `PlayerSpawnPos`, `VoidKillZone`, `Ground`, 채팅 UI(`PanelLogMsg`/`InputFieldChat`) | **없음 — `ColorSelectionManager`/`ColorTagManagers`/`StartColorSelection` 문자열이 씬 파일에 전혀 없음(grep 재확인)** |
| `PlayerTestScene` | `ColorTagManagers`, `GameUICanvas`, `TestBootstrap`(`OfflineModeBootstrap`), `PlayerSpawnPos`, `VoidKillZone` | **유일하게 존재** |

`HideOrSeekPlayer.prefab`(`Assets/04. Prefabs/Resources/`, `PhotonNetwork.Instantiate`가 리소스
경로로 로드) 컴포넌트를 guid로 직접 대조한 결과: `HideOrSeekPlayer`(Unit) + `PlayerBillBoard`(Unit)
+ `PlayerPaintCanvas`/`PlayerColorVoteIndicator`/`PlayerColorDisplay`(ColorTag) + `PhotonView`/
`PhotonTransformView` 계열 2개가 **하나의 프리팹에 함께 부착**돼 있다 — 즉 **에셋(프리팹)은 이미
완성**돼 있고, 실제 매칭 플로우로 `GameScene`에 도달했을 때 이를 "실행 시작"시키는 씬 배선만
빠진 상태다.

---

## 3. 도메인 간 데이터/제어 흐름 (텍스트 다이어그램)

```
LobbyController (LobbyScene)
  │ PhotonNetwork.LoadLevel(GameLobby)  [방 생성자만]
  ▼
GameLobbyController (GameLobbyScene)
  │ PhotonNetwork.LoadLevel(Game)  [방장만, 정원 찼을 때]
  │ ※ ColorSelectionManager를 전혀 모른다 — §4.1
  ▼
GameScene
  ├─ GameManager (채팅 RPC 중계)
  ├─ PlayerSpawner → PhotonNetwork.Instantiate("HideOrSeekPlayer")
  │     └─ HideOrSeekPlayer(이동/점프) + PlayerPaintCanvas/VoteIndicator/ColorDisplay(잠재 기능, 미시작)
  ├─ RoomExitController → ConfirmDialog → LeaveRoom → LobbyScene
  └─ VoidKillZone → HideOrSeekPlayer.RespawnToSpawnPoint()

(PlayerTestScene 전용 경로)
OfflineModeBootstrap → ColorSelectionManager.StartColorSelection()
  → Room.CustomProperties{RoundIndex=0, RoundEndTime, Color0..3=-1}
  → [매 라운드] 각 클라이언트 ColorSwatchButton → SubmitVote → Player.CustomProperties{VoteColorIndex}
  → 마스터 ColorSelectionManager.Update() 폴링 → ResolveRound → ColorVoteTally → Room.Color{n} 확정
  → 각 클라이언트 PlayerPaintCanvas.DetectRoundChange() → FinalizeCurrentRoundStrokes(확정색)
  → 4라운드 완료 → AssignTagger → TaggerColorAssigner.BuildVariantSet → Room.TaggerActorNumber/VariantSet
  → 술래의 PlayerColorDisplay.TryApplyTaggerColor() → 캔버스 전역 치환
  → (본게임 미구현, GameEndTime 세팅 코드 없음) → RoomLifecycleWatcher는 대기만 함
```

---

## 4. 발견된 문제 (우선순위순, 프로젝트 전체 기준)

### 4.1 [최우선] `GameScene`에 ColorTag 시작 트리거가 없음
`GameLobbyController.OnStartGameButtonClicked()`는 씬 전환만 하고, `ColorSelectionManager.
StartColorSelection()`을 호출하는 코드는 프로젝트 전체에서 `Dev/OfflineModeBootstrap.cs`
(개발용, `PlayerTestScene` 전용) 단 한 곳뿐임을 grep으로 재확인했다(§2). 코드/에셋은 완성돼 있고
"`GameScene`에 `ColorTagManagers`+`ColorSelectionPanel` 배치 + 호출 한 줄"만 빠진 상태.

### 4.2 `ColorSelectionManager.ResetAllVotes()`가 마스터 자신의 투표만 리셋
```csharp
private void ResetAllVotes()
{
    if (PhotonNetwork.LocalPlayer != null)
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.VoteColorIndex, -1 } });
}
```
`PhotonNetwork.PlayerList`를 순회하지 않아 다른 플레이어의 이전 라운드 투표값이 새 라운드로
이월된다. "매 라운드 새로 골라야 한다"가 의도라면 결함.

### 4.3 `NetKeys.GameEndTime`을 세팅하는 코드가 없음
`RoomLifecycleWatcher.Update()`는 이 값의 경과를 감지해 정상 종료를 트리거하지만, 이 값을
쓰는(set) 코드는 프로젝트 전체에 없다. 본게임(추격/승패) 자체가 미구현이므로 현재는 이 경로가
트리거될 방법이 없음 — "게임 시작→진행→종료" 파이프라인의 끝쪽이 비어 있다.

### 4.4 `GameManager.Inst`가 죽은 코드
프로젝트 유일의 static 싱글턴이지만 이를 읽는 코드가 프로젝트 전체에서 0건(재확인, §5.5).
당장 위험하지는 않으나 존재 이유가 없는 코드.

### 4.5 `ColorPaletteSO.GetColor()`/`GetColorName()`에 범위 검사 없음
인덱스가 0~9를 벗어나면 `IndexOutOfRangeException`. 모든 호출부가 유효한 인덱스만 넘긴다는
암묵적 전제에 의존 — 지금까지 실제 문제는 없었으나 방어 코드는 없음.

### 4.6 버튼 계열 `AddListener`에 대응하는 해제 코드가 도메인 전체에 없음 (경미)
`ConfirmDialog`/`ColorSwatchButton`/`RoomExitController`/`RoomListItem` 4곳 모두 `Awake()`에서
`onClick.AddListener(...)`만 있고 `OnDestroy`/`RemoveListener`가 없다. `Button.onClick`은
GameObject 수명과 함께 소멸하므로 현재는 안전하지만, 프리팹 풀링/재부모화 시나리오가 생기면
문제가 될 수 있는 프로젝트 전반의 공통 패턴.

### 4.7 미완료(진행 중) 작업: 캐릭터 애니메이션 리소스 교체
현재 작업 트리(미커밋)에 `Assets/Animation/` 루트의 기존 `Dodge/Idle/Jumping/SneakWalking/
Walking.fbx`와 `PlayerAnimator.controller`가 전부 `Animation/Old/`로 옮겨졌고, `Animation/Cookie/`
아래 새 캐릭터 애니메이션 5종(`Cookie_Dodge/Idle/Jumping/Run/Walking.fbx`)이 추가됐다. 새 `Cookie`
세트를 사용하는 새 Animator Controller는 아직 커밋되지 않았고, `PlayerTestScene.unity`에만
관련 프리팹 참조 변경(58줄 diff)이 있다 — **`PlayerMoveState` enum의 트리거 이름(`Idle/Walk/
SneakWalk/Jump/Dodge`)과 새 Animator Controller의 파라미터 이름이 정확히 일치해야** 기존
`PlayerAnimationDriver` 로직이 그대로 작동한다는 점을 유의해야 한다.

---

## 5. 아키텍처 체크리스트 (사용자 지정 11개 관점, 프로젝트 전체 기준)

### 5.1 기존 책임 분리를 무시하는 코드가 있는가 — **경미한 위반 1건, 그 외 전반적으로 준수**
`PlayerColorDisplay.ApplyColorReplace()`가 `PlayerPaintCanvas.PaintCanvas`(공개 `RenderTexture`
프로퍼티)를 직접 `Graphics.Blit`한다 — `[RequireComponent]`로 결합 의도는 명확하지만 캡슐화
경계는 없음. 그 외 도메인들은 역할이 명확히 분리돼 있다: `GameManager`(채팅) / `PlayerSpawner`
(스폰) / `RoomExitController`(나가기)가 원래 하나의 `GameManager`였던 것을 책임별로 쪼갠 흔적이
뚜렷하고(주석에 명시), `Unit/`은 이동(`HideOrSeekPlayer`)·접지(`PlayerGroundDetector`)·애니메이션
(`PlayerAnimationDriver`)·네트워크(`PlayerNetworkSync`)·표시(`PlayerBillBoard`)로 5분할, `ColorTag/`는
SO/정적로직/라운드진행/클라이언트표현 4계층으로 일관되게 분리됨.

### 5.2 Manager 간 의존성이 과도하게 증가하는가 — **아니다, 오히려 결합 부재가 문제**
전체 32개 스크립트 중 다른 Manager를 `[SerializeField]`로 직접 참조하는 경우는 `ColorSwatchButton
.manager`(UI→매니저, 정상 방향), `RoomListItem.lobby`(UI→매니저), `OfflineModeBootstrap→
ColorSelectionManager`(개발용, `FindFirstObjectByType`) 뿐이다. 나머지는 전부 Photon
CustomProperties/RaiseEvent라는 공유 네트워크 상태로만 소통한다. 진짜 문제는 결합 과다가 아니라
**`GameLobbyController`가 `ColorSelectionManager`를 전혀 모른다는 결합 부재**(§4.1)다.

### 5.3 Prefab과 Script의 역할이 뒤섞이는가 — **경미, `HideOrSeekPlayer.prefab`의 도메인 융합**
코드 레벨에서 `Unit`과 `ColorTag`는 서로를 참조하지 않는 별개 도메인이지만, 프리팹 하나가
`HideOrSeekPlayer`/`PlayerBillBoard`(Unit) + `PlayerPaintCanvas`/`PlayerColorVoteIndicator`/
`PlayerColorDisplay`(ColorTag)를 물리적으로 함께 갖고 있다(§2에서 guid 대조로 재확인). 캐릭터라는
단일 오브젝트가 여러 기능을 갖는 것 자체는 자연스럽지만, 코드 분리와 에셋 결합이 다른 그림이라는
점은 향후 관전자용/AI용 변형 프리팹이 필요해지면 걸림돌 가능. `transform.Find("경로")` 류의 취약한
문자열 탐색은 `HideOrSeekPlayer.Start()`의 `transform.Find("Ch36")` 1건(Ch36 자기 충돌 무시용,
Unit 도메인)과 `PlayerSpawner`/`HideOrSeekPlayer.RespawnToSpawnPoint()`의 `GameObject.Find(
"PlayerSpawnPos")` 2건 — 씬에 해당 이름의 오브젝트가 반드시 있어야 하는 암묵적 계약(이름이
바뀌면 조용히 실패, `PlayerSpawner`는 `LogWarning`으로 방어, `RespawnToSpawnPoint`는 null이면
그냥 리턴).

### 5.4 Scene에 직접 의존하는 코드가 늘어나는가 — **깨끗함**
프로젝트 전체에서 씬 이름을 하드코딩 문자열로 다루는 코드는 0건 — `SceneManager.LoadScene`/
`PhotonNetwork.LoadLevel` 호출 5곳(`GameLobbyController`, `LobbyController` 2곳, `RoomExitController`,
`RoomLifecycleWatcher` 2곳) 전부 `SceneNames.*` 상수를 사용(§Grep으로 전수 확인, `Core/SceneNames.cs`
도입으로 매직 스트링 문제 해소).

### 5.5 Singleton이 남발되는가 — **아니다, 1개뿐이고 그마저 죽은 코드**
프로젝트 전체 32개 스크립트 중 `static Instance`류 패턴은 `GameManager.Inst` 1개뿐이며, 이를
읽는 코드가 0건(§4.4, grep 재확인). 나머지는 전부 씬에 배치된 일반 컴포넌트이거나
`FindObjectsByType`/`FindFirstObjectByType`으로 그때그때 탐색(`GameManager.
SetLocalPlayerMovementLocked`, `BrushCursorController.FindLocalPaintCanvas`,
`OfflineModeBootstrap`).

### 5.6 ScriptableObject의 책임이 잘못 사용되는가 — **문제 없음**
`ColorPaletteSO`/`BrushSettingsSO` 둘 다 읽기 전용 데이터 저장소로만 쓰이고, 런타임에 필드를
변경하는 코드는 프로젝트 전체에 없다. SO를 상태 저장소나 이벤트 버스로 쓰는 안티패턴 없음.
`ColorTag/` 밖에는 SO 자체가 없다(`Assets/03. SO/`는 `ColorTag/` 하위 2개 SO뿐).

### 5.7 Unity Lifecycle 순서 문제가 있는가 — **여러 건, 모두 방어적으로 처리되어 안전**
- `HideOrSeekPlayer.Awake()`가 `networkSync`를 `IsMine` 여부와 무관하게 최우선 생성 — Photon
  디스패치가 `Start()`보다 먼저 올 수 있는 경쟁을 원천 차단(§1.4).
- `Camera_Ctrl.InitCamera()`/`Start()` 둘 다 같은 `ResetToDefaultView()`를 호출해 호출 순서
  무관하게 안전(§1.5).
- `PlayerColorDisplay.Awake()`(`GetComponent<PlayerPaintCanvas>()`)와 `PlayerPaintCanvas.Start()`
  실행 순서는 Unity가 보장하지 않지만, `TryApplyTaggerColor()`가 캔버스가 아직 없으면 조용히
  리턴하고 매 `OnRoomPropertiesUpdate` 콜백마다 재시도.
- `ColorSelectionManager`(마스터 `Update()` 폴링)와 `PlayerPaintCanvas.DetectRoundChange()`(각
  클라이언트 `Update()` 폴링) 사이 순서도 Photon 서버 왕복 후 반영되므로 같은 프레임의 Script
  Execution Order에 의존하지 않음. **프로젝트에 커스텀 Script Execution Order 설정 자체가 없음**
  (`ProjectSettings/`에 관련 asset 부재 확인) — 즉 이런 방어 코드가 실제로 필요한 상황.
- `GameLobbyController.Update()` 안전망도 같은 패턴: 이벤트 콜백이 놓친 초기 상태를 다음 프레임에
  스스로 보정(§1.2).

### 5.8 Event 구독/해제가 제대로 되는가 — **Photon 콜백은 완전히 깨끗함, UI 버튼 리스너는 프로젝트 전체 공통 패턴으로 미해제**
- Photon 콜백: `MonoBehaviourPunCallbacks`가 `OnEnable`/`OnDisable`에서 자동으로
  `AddCallbackTarget`/`RemoveCallbackTarget`을 처리하며, **수동 이중 등록은 프로젝트 전체에서
  0건**(grep 재확인, §4.4의 죽은 코드와 별개로 이 부분은 완전히 정상). `PlayerPaintCanvas.
  IOnEventCallback.OnEvent()`도 별도 수동 등록 없이 기반 클래스가 처리.
- C# `+=`/`-=` 순수 이벤트 구독은 프로젝트 전체에 없음.
- UI `Button.onClick.AddListener()`는 `ConfirmDialog`/`ColorSwatchButton`/`RoomExitController`/
  `RoomListItem` 4개 도메인에 걸쳐 공통으로 나타나는 패턴이며 대응하는 `RemoveListener`가 어디에도
  없다(§4.6) — 지금까지 이 버튼들이 풀링되지 않으므로 실질 위험은 낮지만, ColorTag만의 문제가
  아니라 프로젝트 전반의 컨벤션임을 이번 조사로 확인.

### 5.9 Object Pool과 Instantiate/Destroy가 충돌하는가 — **풀 자체가 없음, 충돌 없음**
프로젝트 실제 코드(`Assets/02. Scripts/`)에 Object Pool 구현이 전혀 없다(`Player.md`에 등장하는
`ItemObjectPool`/`EnemyObjectPool` 등은 §7에서 설명하듯 **다른 프로젝트의 참고 코드**이며 이
게임과 무관). `Instantiate`/`Destroy` 호출은 `LobbyController`(방 목록 아이템, diff 갱신),
`GameLobbyController`(플레이어 목록 아이템, 매번 전체 재생성), `BrushCursorController`(붓 커서
1회 인스턴스화 후 `SetActive` 재사용 — 사실상 수동 풀링 1개체), `PlayerSpawner`(캐릭터,
`PhotonNetwork.Instantiate`) 정도로 전부 생명주기가 명확하고 서로 충돌하지 않는다.
`PlayerPaintCanvas`의 `RenderTexture`는 캐릭터당 1회 생성·`OnDestroy()`에서 해제로 관리됨.

### 5.10 Photon의 Ownership/RPC 구조를 무시하는가 — **잘 지켜짐, 프로젝트에서 가장 성숙한 영역**
- `pv.IsMine` 게이팅이 `HideOrSeekPlayer.Update()`/`FixedUpdate()`(자기 캐릭터만 물리·입력 처리),
  `PlayerPaintCanvas.Update()`(자기 캐릭터만 입력 처리) 등 10개 파일에 정확히 적용(grep 전수 확인).
- 마스터 전용 권위: `ColorSelectionManager.Update()`/`RoomLifecycleWatcher.Update()` 둘 다
  `PhotonNetwork.IsMasterClient` 가드로 라운드 판정·씬 전환 트리거를 제한 — "단일 권위자만 계산
  → CustomProperties로 전파 → 나머지는 신뢰" 패턴이 두 매니저에 일관되게 적용됨. `GameLobbyController.
  RefreshStartButton()`/`OnStartGameButtonClicked()`도 동일 패턴(방장만 시작 가능).
- 상태(투표, 라운드, 인원) vs 순간 이벤트(붓질, 채팅)를 CustomProperties/PlayerList vs
  `RaiseEvent`/`RPC`로 적절히 구분: `GameManager`/`RoomExitController`는 채팅·로그를
  `pv.RPC(..., RpcTarget.AllBuffered)`로, `ColorTag`는 상태를 CustomProperties로, 붓질처럼 잦고
  재생 가능한 이벤트만 `RaiseEvent`로 구분.
- `PlayerPaintCanvas.OnEvent()`/`HideOrSeekPlayer.OnPhotonSerializeView()` 수신부는 송신측이 이미
  계산한 값(force 플래그, position/rotation/state)을 그대로 재생만 하고 재해석하지 않음 — 클라이언트
  간 판단 불일치 가능성을 원천 차단.
- `pv` 필드는 필요한 클래스에만 있고(`GameManager`, `RoomExitController`, `HideOrSeekPlayer`,
  `PlayerBillBoard`, `PlayerPaintCanvas`, `PlayerColorDisplay`, `PlayerColorVoteIndicator`), Room
  단위 상태만 다루는 `ColorSelectionManager`/`RoomLifecycleWatcher`/`ColorSelectionPanel`/
  `LobbyController`/`GameLobbyController`에는 없음 — 불필요한 `PhotonView` 참조가 없다.
- `GameManager.Start()`/`PlayerSpawner.Start()` 둘 다 "Start()가 실행됐어도 `PhotonNetwork.InRoom`
  이 true라는 보장이 없다"는 동일한 실측 문제를 동일한 패턴(`while(!InRoom) yield return null`)으로
  대응 — 두 파일이 독립적으로 같은 결론에 도달한 일관된 방어.

### 5.11 중복 로직이 있는가 — **크게 개선된 상태, 경미한 잔여 중복만 존재**
`RoundIndex` 등 Room CustomProperties 조회 로직은 `RoomState` 정적 헬퍼로 통합돼 4개 파일
(`ColorSelectionManager`, `ColorSelectionPanel`, `PlayerPaintCanvas`, `PlayerColorDisplay`)이 이를
사용한다. `RoomState`가 다루지 않는 `int[]`/`object` 타입 프로퍼티(`TaggerVariantSet`, `Color0~3`
배열 형태 접근)는 `PlayerColorDisplay`/`ColorSelectionManager`가 각자 `PhotonNetwork.CurrentRoom.
CustomProperties`를 직접 읽는데, 이는 `RoomState`의 범용 헬퍼(`TryGetInt`/`TryGetDouble`) 범위를
의도적으로 벗어난 것으로 파일 자체 주석에 명시돼 있어 중복이라기보다는 설계상 경계.
도메인 간에는: `GameManager.Start()`와 `PlayerSpawner.Start()`가 "InRoom 대기" 코루틴 패턴을
각자 구현(완전히 동일한 로직이 2곳에 복붙돼 있음 — 공용 헬퍼로 뽑아낼 여지는 있으나 로직 자체가
3줄 내외로 짧아 심각하지는 않음), `PlayerColorVoteIndicator.LateUpdate()`와 `PlayerBillBoard.
LateUpdate()`가 거의 동일한 "카메라 forward로 정렬" 코드를 각자 구현(둘 다 3줄, 별도 도메인).

---

## 6. 종합 결론과 다음 단계 제안 (우선순위순)

1. **[최우선, 통합 공백]** `GameLobbyController.OnStartGameButtonClicked()` 또는 `GameScene` 진입
   시점에 `ColorSelectionManager.StartColorSelection()` 호출을 추가하고, `PlayerTestScene`의
   `ColorTagManagers`+`ColorSelectionPanel` UI를 `GameScene.unity`에 옮겨 배치한다(§4.1). 코드/에셋은
   이미 완성돼 있어 "씬 배치 + 호출 한 줄" 수준의 작업.
2. **`ColorSelectionManager.ResetAllVotes()`가 전원을 리셋해야 하는 의도인지 확인 후**
   `PhotonNetwork.PlayerList`를 순회하도록 수정(§4.2).
3. **본게임(태그/술래잡기) 승패 판정 로직 설계 + `GameEndTime` 기록 주체 결정** — `RoomLifecycleWatcher`
   의 정상 종료 경로가 실제로 동작하려면 선행 필요(§4.3).
4. **미완료 상태인 `Cookie` 애니메이션 세트 교체 작업을 마무리** — 새 Animator Controller를 만들고
   `PlayerMoveState` enum(`Idle/Walk/SneakWalk/Jump/Dodge`)과 트리거 이름을 정확히 맞춘 뒤
   `HideOrSeekPlayer.prefab`/관련 씬에 반영·커밋한다(§4.7).
5. (선택) `GameManager.Inst`가 죽은 코드라면 제거를 검토(§4.4).
6. (선택) `ColorPaletteSO`에 인덱스 범위 검사 추가 여부 검토(§4.5).
7. (선택) UI 버튼 4곳의 `AddListener`에 대응하는 해제 코드 추가 — 지금은 위험 낮음, 프리팹
   풀링/재부모화 계획이 생기면 우선순위 상향(§4.6).

**전체적으로**: 이 프로젝트는 도메인별 책임 분리(§5.1), Scene 의존성 관리(§5.4), Singleton 절제
(§5.5), Photon Ownership/RPC 구조(§5.10) 네 관점에서 뚜렷한 구조적 문제가 없다. 특히 여러 도메인이
독립적으로 "Start() 시점에 Photon InRoom을 보장할 수 없다"는 동일한 실측 교훈에 도달해 같은 패턴
(코루틴 폴링)으로 대응하고 있는 점, Rigidbody 도입 이후 `PlayerControllPlan.md`에 기록된 일련의
물리·애니메이션 버그(§1.4의 각 커밋 참조)가 근본 원인 단위로 추적·수정된 점은 이 프로젝트의 코드
품질이 이터레이션을 거치며 실제로 개선되고 있음을 보여준다. 남은 구조적 문제는 대부분 "도메인
자체의 결함"이 아니라 **완성된 도메인들을 게임 전체 플로우로 잇는 배선의 공백**(§4.1/§4.3)과
현재 진행 중인 애니메이션 리소스 교체 작업의 마무리(§4.7)로 좁혀져 있다.

---

## 7. 부록 — `Assets/02. Scripts/Player.md`의 정체

이 파일은 `.cs`가 아니라 `.md`이며, **이 프로젝트(`TagOfChaos`/`HideOrSeekPlayer`)의 스크립트가
아니라 다른(이전) 3인칭 슈팅 게임 프로젝트의 `Player.cs` 전문 + 그 코드에 대한 분석 메모**를 담고
있다. 무기 스왑/장전/투척, 상점, 적 스폰, 코인/탄약/체력 아이템, 대미지 처리 등 이 게임과 무관한
기능이 대부분이며, `EnemyObjectPool`/`ItemObjectPool` 등 §5.9에서 언급한 오브젝트 풀도 전부 이
참고 코드에서만 등장한다. 문서 하단의 분석 메모는 "들러붙는 버그"(캐릭터가 얇은 콜라이더에
걸리는 현상, `PlayerControllPlan.md §22`) 조사를 위해 이 참고 코드의 이동/접지 판정 방식을
검토한 기록으로, **결론은 "이 코드를 그대로 가져다 쓰지 말라"**다 — 접지 판정이 `OnCollisionEnter`
1회성 이벤트 기반이라 낭떠러지 낙하를 못 잡는 사각지대가 있고(우리 `PlayerGroundDetector`가 이미
해결한 문제, §1.4), `Walking()`의 `Time.deltaTime` 이중 적용은 그대로 옮기면 새 버그가 된다는 점을
지적한다. 이 프로젝트의 실제 `Unit/` 도메인 코드는 이 참고 코드와 무관하게 독립적으로 설계돼 있다.
