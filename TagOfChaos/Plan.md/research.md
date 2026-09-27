# 조사 보고서: TagOfChaos 런타임 스크립트 전수 분석 + 아키텍처 11개 관점 감사 (2026-09-27, 커밋 `0a52d28`)

> **개정 안내**: 이 문서는 이전 판(커밋 `0a52d28`에 들어 있는 research.md, §A~§H)을 **전면 대체**한다.
> 이전 판 원문은 `git show 0a52d28:TagOfChaos/Plan.md/research.md`로 볼 수 있다. 코드 주석에 남은
> `research.md §8.x`, `§12 E1~E12`, `G5-1` 같은 참조는 이전 판들의 절 번호다. 이번 판은 겹치지 않도록 **R 접두어**(R1~R9)를 쓴다.
>
> 이전 판(커밋 `7e7feba` 기준 감사) 이후 런타임에 새로 들어온 **Interaction/(CharacterInteractor·IInteractable·
> InteractionPromptUI)**, **Environment/(InteractableDoor·CauldronSplash)**, 그리고 Core의 소규모 변경(InteractKey,
> DoorStateRequest, DoorStates, IGameCharacter.CanInteract)을 이번 감사 범위에 포함했다.
>
> **조사 방법**
> - `Assets/02. Scripts/` 아래 **10개 폴더, 79개 `.cs`, 5,969줄을 줄 단위로 모두 읽었다.** (`Player.md`는 이 프로젝트와 무관한
>   다른 게임의 참고 코드라 제외.)
> - 씬 4개(빌드 포함 3개 + PlayerTestScene)와 프리팹의 YAML을 스크립트로 파싱해 **스크립트 GUID → 붙은 GameObject**를 따라갔다(R2.4).
> - 연결된 Unity 에디터(MCP)에서 프리팹을 직접 로드해 PhotonView 관찰 대상, Rigidbody 기본값, 문 ID, GameSettings 값 등을 실측했다.
> - 파일 경로는 따로 적지 않으면 `Assets/02. Scripts/` 기준이다. 예: `Unit/HideOrSeekPlayer.cs:127`.

---

## 목차

- **R1. 요약** — 결론, 11개 관점 판정표, 우선 조치 Top 10
- **R2. 프로젝트 구조** — 엔진·패키지·폴더·asmdef·씬·프리팹·SO·배선표
- **R3. 동작 방식 상세** — 한 판의 흐름, 네트워크 계약(Room/Player Props·이벤트·RPC), 도메인별 클래스 동작
- **R4. 11개 관점 감사** — 관점마다 판정, 근거(file:line), 영향, 권장
- **R5. 그 밖의 발견 사항**
- **R6. 권장 로드맵**
- **부록** — 교차 도메인 의존성 표, 전역(static) 상태 목록, 전역 네트워크 설정을 바꾸는 곳, 마스터 전용 Update 폴링 목록

---

## R1. 요약

### R1.1 결론

코드베이스는 **"공용 허브 + 도메인 컴포넌트"** 구조로 잘 정리돼 있다. `RoomState`(조회), `NetKeys`(키·수명 표),
`GamePhaseState`(단계 해석), `CharacterRegistry`/`IGameCharacter`(캐릭터 계약), `PlayerInput`/`InputBindingsSO`(입력),
`GameSettingsSO`(수치), `RoomSceneTransition`(씬 전환)이 한 곳씩만 책임지고, **Core는 Photon 외에 어떤 도메인도 참조하지 않는다.**
고전적인 `Instance` 싱글톤은 없고, Room Props는 방장만·Player Props는 본인만 쓰는 **Photon 소유권 원칙도 대체로 지켜진다.**
새로 들어온 상호작용 시스템(Interaction/·Environment/)도 같은 레지스트리 패턴과 "방장 요청 → Room Prop" 패턴을 그대로 따랐다.

남은 문제는 "기능이 망가짐"보다 **책임이 엉뚱한 곳에 붙은 것**과 **검증 없는 RPC 수신**에 가깝다. 실제로 재현 가능한 결함은 다음 3개다.

1. **쿠키 그랩에 확인 절차가 없다.** 두 쿠키가 같은 쿠키를 연달아 잡으면 먼저 잡은 쪽의 "놓기"가 나중에 잡은 쪽의 캐리를 끊고,
   나중에 잡은 쪽은 빈 손으로 캐리 자세·충돌 무시 상태에 갇힌다(R4.10-1).
2. **채팅 중 이동 잠금이 쿠키에게만 걸린다.** 괴물은 채팅을 치는 동안 WASD로 움직이고 Shift로 촉수 돌진을 쓴다(R4.1-1).
3. **팔레트 색이 0개면 강제 도포가 0으로 나누기 예외를 낸다**(`PaintPhaseController.cs:64`, R5-3). 현재 에셋은 10색이라 잠재 결함이다.

### R1.2 11개 관점 판정표

| # | 관점 | 판정 | 한 줄 요약 |
|---|---|---|---|
| 1 | 기존 책임 분리를 무시하는 코드 | ⚠ 일부 있음 | 채팅(GameManager)이 전역 네트워크 설정·timeScale을 바꾸고, UI 패널(GameLobbyController·LobbyController)이 네트워크 권한 처리·Photon 초기화를 맡는다 |
| 2 | Manager 간 의존성 과도 증가 | ✅ 양호(암묵 결합 주의) | 매니저끼리 직접 참조는 3곳뿐. 대신 8개 컴포넌트가 같은 Room Props를 매 프레임 폴링하는 **암묵 결합**이 있다 |
| 3 | Prefab과 Script 역할 뒤섞임 | ⚠ 일부 있음 | 자식 이름 `"Mesh_0"`, 애니메이터 트리거=enum 이름, 클립 이름 키워드 검색, 렌더러 이중 배선, 같은 UI의 프리팹판·씬판 이중 정의 |
| 4 | Scene에 직접 의존하는 코드 증가 | ✅ 양호 | `GameObject.Find` 4곳(스폰 지점, 모두 이전부터), `Camera.main` 10곳. 새 코드(상호작용)는 레지스트리를 써서 씬 탐색을 늘리지 않았다 |
| 5 | Singleton 남발 | ✅ 양호(정적 상태 12곳) | `Instance` 싱글톤 0개. 자동 생성 DDOL 1개(CharacterInteractor), 정적 레지스트리·플래그·캐시 12곳 — 일부는 방을 옮겨도 남는다 |
| 6 | ScriptableObject 책임 오용 | ✅ 양호 | 5개 SO 모두 읽기 전용 설정/데이터. 런타임 상태를 SO에 쓰는 곳 없음. 사소한 방어 누락·메뉴 경로 불일치만 있음 |
| 7 | Unity Lifecycle 순서 문제 | ✅ 대부분 해결됨 | 과거 결함(Awake/Start 순서, InRoom 대기, CanvasGroup 숨김)은 모두 정리됨. 잠재 위험 3건(정적 플래그 Awake/OnDestroy, ConfirmDialog 자기 비활성, `?.`와 Unity null) |
| 8 | Event 구독/해제 | ✅ 양호 | 정적 이벤트 1개는 OnEnable/OnDisable로 짝이 맞고, `MonoBehaviourPunCallbacks`를 상속한 25개 클래스 중 OnEnable/OnDisable을 재정의한 5개 모두 `base`를 호출한다 |
| 9 | Object Pool ↔ Instantiate/Destroy 충돌 | ✅ 충돌 없음 | 풀이 없다(PUN `DefaultPool`만 사용). 대신 로비 목록을 매번 전부 부수고 다시 만드는 비용, 네트워크 객체의 로컬 `Destroy`가 있다 |
| 10 | Photon Ownership/RPC 구조 무시 | ⚠ 일부 있음 | 그랩/놓기/처형 RPC가 **보낸 사람을 검증하지 않는다**. 문 상태 방장 캐시가 정적이라 방을 옮겨도 남는다. 퇴장 시 Player Props를 API 없이 로컬에서 지운다 |
| 11 | 중복 로직 | ⚠ 다수 | 스폰 3벌, InRoom 대기 3벌+첫 프레임 대기 2벌, 클립 길이 찾기 2벌, 트리거 전환 2벌, 리지드바디 설정·리스폰·카메라 초기화 2벌, 표시 이름 3벌, 레지스트리 2벌, 색칠 남은 시간 표시 2곳 |

### R1.3 우선 조치 Top 10

| 순위 | 항목 | 근거 | 난이도 |
|---|---|---|---|
| 1 | 그랩 확인 절차: 들린 쪽이 수락/거절을 알리고, `OnReleased`는 현재 캐리어가 보낸 것만 받기 | R4.10-1 | 중 |
| 2 | 채팅 이동 잠금을 캐릭터 공통 계약으로 옮기기(괴물 포함) | R4.1-1 | 하 |
| 3 | `RequestGrabKill`·`OnGrabbedByOwner`·`OnReleased`에서 `PhotonMessageInfo.Sender` 검증 | R4.10-2 | 하 |
| 4 | `GameManager.Start`의 `Time.timeScale`·`IsMessageQueueRunning` 대입 제거(또는 전담 부트스트랩으로 이동) | R4.1-2 | 하 |
| 5 | 시작 요청 이벤트 처리를 UI 패널(GameLobbyController)에서 비 UI 컴포넌트로 이동 | R4.1-3 | 하 |
| 6 | `InteractableDoor.masterPendingWrites`를 방 퇴장·씬 전환 시 비우기 | R4.10-3 | 하 |
| 7 | 스폰 3벌을 `SceneSpawnPoints.TryFindClearPosition` 하나로 통일, 프리팹 이름 상수 한 곳으로 | R4.11-1 | 하 |
| 8 | `PaintPhaseController`·`ColorPaletteSO.Count`의 빈 팔레트 방어 | R5-3 | 하 |
| 9 | 로비 참가자 목록을 부수고 다시 만들지 말고 `UiListBuilder.Sync`로 재사용 | R4.9-2 | 하 |
| 10 | 색칠 남은 시간 이중 표시(ColorSelectionPanel.timeLabel + PaintCountdown) 정리 | R4.11-8 | 하 |

---

## R2. 프로젝트 구조

### R2.1 엔진·패키지

- Unity **6000.0.58f2**, 레거시 Input Manager(`PlayerInput`이 유일한 창구), Photon **PUN 2**(Realtime·Chat 포함).
- 런타임 스크립트는 asmdef **`TagOfChaos.Scripts`** 하나(참조: PhotonUnityNetworking, PhotonRealtime, Unity.TextMeshPro, Unity.ugui).
  에디터 도구는 `TagOfChaos.Editor`, 테스트는 `TagOfChaos.EditorTests`(`Assets/Editor/Tests/RuleTests.cs`, 12개 테스트).
- Enter Play Mode Options: 켜짐, 옵션값 0(도메인·씬 리로드 모두 수행) — 정적 상태가 플레이 세션 사이에 남지 않는다.
- 빌드 씬: `LobbyScene`(0) → `GameLobbyScene`(1) → `GameScene`(2). `PlayerTestScene`은 목록에 있으나 비활성.

### R2.2 폴더와 줄 수

| 폴더 | 파일 | 줄 | 역할 |
|---|---|---|---|
| Core | 18 | 약 780 | 공용 허브(조회·키·단계·입력·설정·씬 전환·레지스트리·스폰 위치·UI 헬퍼) |
| Unit | 10 | 약 850 | 쿠키 캐릭터(조정자 + 순수 C# 협력 객체 4개), 스킨 |
| Monster | 19 | 약 1,540 | 괴물 캐릭터 + **게임 진행 전반**(괴물 선정·합류·승패·결과·관전·방장 정책·판 초기화) |
| ColorTag | 13 | 약 1,110 | 색칠(캔버스·붓·팔레트 UI·강제 도포) + 쿠키 생존 표시 |
| GameManager | 6 | 약 380 | 채팅, 스폰, 퇴장, 낙하 복귀, 확인창 |
| Lobby | 6 | 약 540 | 로비(방 목록), 대기실 패널, 판 시작 권한, 스킨 선택 |
| Interaction | 3 | 약 200 | 상호작용 키(E) 탐지·안내 UI·사물 계약 |
| Environment | 2 | 약 310 | 상호작용 문, 가마솥 풍덩 연출 |
| Camera | 1 | 138 | 3인칭 궤도 카메라 |
| Dev | 2 | 70 | 오프라인 테스트 부트스트랩, 괴물 테스트 스포너 |

### R2.3 에셋

- **SO**: `Resources/GameSettings`(정원 4, 괴물 1, 색칠 60초, 생존 600초 — MCP 실측), `Resources/InputBindings`,
  `03. SO/ColorTag/`(팔레트·붓 설정), `03. SO/Unit/`(스킨 목록).
- **네트워크 프리팹**(PUN `DefaultPool`이 `Resources`에서 로드): `04. Prefabs/Resources/HideOrSeekPlayer.prefab`, `MonsterPlayer.prefab`.
  - 둘 다 PhotonView `UnreliableOnChange`, 관찰 대상은 각 조정자 한 개(HideOrSeekPlayer / MonsterController).
  - 둘 다 프리팹 기본값이 `isKinematic=false, useGravity=true`이고 `Start()`에서 원격 복사본만 키네마틱으로 바꾼다.
  - 레이어: 쿠키 `Cookie`, 괴물 `Monster`. 쿠키 캡슐 r=0.46 h=2, 괴물 캡슐 r=0.1 h=0.35 × 스케일 3.
- **UI 프리팹**: `Resources/UI/Scene/{LobbyPanel, GameLobbyPanel, ColorSelectionPanel, InteractionPromptUI, PlayerListItem, PlayerResultRow, RoomListItem}`,
  `Resources/UI/Popup/ConfirmDialog`. 런타임에 `Resources.Load`로 읽는 것은 **InteractionPromptUI 하나뿐**이고 나머지는 씬에 인스턴스로 배치돼 있다.
- **환경**: `04. Prefabs/Environment/{Cauldron, Witch_Cookie_House, Witch_Cookie_Environment}` — 과자집 문 4개
  (`Door_Front/Back/Left/Right`, 각자 전용 AnimatorController, 상호작용 범위 2m)는 GameLobbyScene에만 있다.

### R2.4 씬 배선(스크립트 → GameObject, YAML 파싱 결과)

| 씬 | GameObject | 컴포넌트 |
|---|---|---|
| LobbyScene | LobbyPanel(프리팹) | LobbyController, RoomListItem(프리팹) |
| GameLobbyScene | GameManager | GameManager, PlayerSpawner(`skipConfirmedMonster=false`), RoomExitController |
| | MonsterManagers | MasterClientPolicy, MonsterAssignmentAuthority, MonsterLobbyWaitController, MonsterRevealController, RoundStateResetter |
| | GameLobbyPanel(프리팹) | GameLobbyController, PlayerListItem(프리팹) |
| | Cauldron / SkinSelectPanel / VoidKillZone / ConfirmDialog(프리팹) / Main Camera | Cauldron / PlayerSkinSelector / VoidKillZone / ConfirmDialog / Camera_Ctrl |
| | Witch_Cookie_Environment(프리팹) | InteractableDoor ×4, Cauldron.prefab의 CauldronSplash |
| GameScene | GameManager | GameManager, PlayerSpawner, RoomExitController |
| | GameRuleManagers | GamePhaseStarter, GameRuleController, MasterClientPolicy, MonsterJoinController, PaintPhaseController, RoomLifecycleWatcher |
| | PaintManagers | BrushCursorController |
| | ColorSlotPanel(**프리팹 아님**) | ColorSelectionPanel, SwatchRow(ColorSwatchGroup), Swatch0~9, Erase/ResetButton(PaintToolButton) |
| | PaintCountdown / SurvivalTimer | PhaseCountdownDisplay(Paint / Survival) |
| | ResultScreen / MonsterDepartureBanner / SpectatorLabel / VoidKillZone / ConfirmDialog / Main Camera | 각 이름과 같은 컴포넌트, Camera_Ctrl |
| PlayerTestScene | TestBootstrap / ColorTagManagers / ColorSelectionPanel(프리팹) | OfflineModeBootstrap, MonsterTestSpawner / BrushCursorController / … |
| (런타임 생성) | CharacterInteractor(DDOL) | CharacterInteractor + InteractionPromptUI(Resources) |

프리팹 `HideOrSeekPlayer`: HideOrSeekPlayer, PlayerGrabController, PlayerPaintCanvas, PlayerSkinApplier, CookieLifeStatePresenter,
SpectatorController, FallGuard, 자식 Nameplate(PlayerBillBoard). 프리팹 `MonsterPlayer`: MonsterController, MonsterGrabKillTrigger, FallGuard.

---

## R3. 동작 방식 상세

### R3.1 한 판의 흐름

```
LobbyScene
  LobbyController: ConnectUsingSettings → JoinLobby → 방 목록 표시
  방 만들기(MaxPlayers = GameSettings.MaxPlayers) / 무작위 입장 / 목록 입장
  OnJoinedRoom: 방을 만든 1인만 LoadLevel(GameLobby), 나머지는 AutomaticallySyncScene으로 따라옴
        │
GameLobbyScene (대기실)
  PlayerSpawner: InRoom이 될 때까지 기다린 뒤 쿠키 스폰(괴물이어도 대기실에선 쿠키)
  RoundStateResetter: 입장 첫 프레임에 자기 Round Player Props 삭제, 방장은 이전 판 흔적이 있으면 Round Room Props 삭제 + 방 재개방
  MasterClientPolicy: 방장 = "괴물이 아닌 사람 중 최소 ActorNumber"로 수렴
  정원이 차면 MonsterAssignmentAuthority(방장)가 MonsterSelectDeadline = 지금 + 30초 기록
    ├ 쿠키가 가마솥 트리거 진입 → Cauldron이 ClaimMonster 이벤트를 방장에게 → 선착순 확정
    └ 기한 경과 → 남은 자리 무작위 확정
  MonsterRevealController: 배너 표시 / MonsterLobbyWaitController: 괴물이면 AutomaticallySyncScene=false
  호스트(최소 ActorNumber, 괴물이어도 됨)가 시작 버튼 → GameStartAuthority
    ├ 내가 방장이면 바로 TryStart, 아니면 StartGameRequest 이벤트로 방장에게 요청
    └ TryStart(방장): PaintPhaseEndTime = 지금 + 3초(로딩 여유) + 60초 기록 → IsOpen=false → 캐시 비우고 LoadLevel(Game)
  괴물: PaintPhaseEndTime 변경을 받는 순간 다른 쿠키 아바타 로컬 삭제, 메시지 큐 정지, 로컬 실시간 시계로 카운트다운
        │
GameScene (쿠키만 먼저 도착)
  PlayerSpawner: 쿠키 스폰(괴물은 건너뜀)
  색칠 60초: PlayerPaintCanvas가 몸 콜라이더에 레이캐스트 → 스탬프를 로컬 RT에 그리고 묶어서 PaintStroke 이벤트로 전송
             같은 색 15스탬프 이상이면 슬롯 등록(최대 4) → RegisteredSlotCount를 자기 Player Props에 보고
  색칠 종료(PaintPhaseEndTime 경과, 방장):
    ├ PaintPhaseController: 슬롯 0개 쿠키에게 색 배정 → ForcedPaintActorNumbers/Colors 기록 → 대상 본인이 전신 강제 도포
    └ MonsterJoinController: MonsterJoined=1, GameEndTime = 지금 + 600초 기록
  괴물: 대기실 카운트다운 종료 → 혼자 LoadLevel(Game) → 큐 재개로 쌓인 이벤트 재생 → MonsterJoined를 보고 MonsterPlayer 스폰
        → curScn이 현재 씬과 같아지면 AutomaticallySyncScene=true 복구
  생존 600초:
    괴물 근접(구 트리거) 또는 촉수 돌진 경로 스윕 → RequestGrabKill RPC(피해자 소유자에게) → 피해자가 HitCount=2 기록 → 관전 모드
    CookieLifeStatePresenter: 모든 클라이언트에서 HitCount를 보고 렌더러·콜라이더 끄고 BrokenCookie 레이어로
  GameRuleController(방장): 쿠키 전원 파괴 → MonsterWins / GameEndTime 경과 → CookiesWin → GameResult 기록
  괴물 전원 이탈: RoomLifecycleWatcher → MonsterDepartedAt 기록 → 5초 배너 → 대기실 복귀
  ResultScreenController: 결과 표시 → 12초 뒤(또는 버튼) 방장이 LoadLevelForRoom(GameLobby)
        │
GameLobbyScene으로 복귀 → RoundStateResetter가 이전 판 흔적 삭제 → 다음 판
```

### R3.2 네트워크 계약

**Room Props**(모두 방장만 쓴다. `NetKeys.Scopes` 표에 대상·수명 선언, 수명 Round 키는 대기실 복귀 때 삭제)

| 키 | 타입 | 쓰는 곳 | 읽는 곳 |
|---|---|---|---|
| MonsterActorNumbers | int[] | MonsterAssignmentAuthority, RoomLifecycleWatcher | RoomState 전반, 배너, 스폰, 방장 정책, 결과 |
| MonsterRevealTime | double | MonsterAssignmentAuthority | (쓰기만 함 — R5-5) |
| MonsterSelectDeadline | double | MonsterAssignmentAuthority | 대기실 상태 문구 |
| PaintPhaseEndTime | double | GameStartAuthority, (예비) GamePhaseStarter | GamePhaseState, 괴물 대기, 강제 도포, 합류 |
| MonsterJoined | int | MonsterJoinController | GamePhaseState, 괴물 스폰, 판 흔적 판정 |
| ForcedPaintActorNumbers / ForcedPaintColors | int[] | PaintPhaseController | PlayerPaintCanvas(본인) |
| GameEndTime | double | MonsterJoinController | 승패 판정, 생존 카운트다운 |
| MonsterDepartedAt | double | RoomLifecycleWatcher | 이탈 배너, 방장 교체 시 인계 |
| GameResult | int | GameRuleController | 결과 화면, 단계 판정 |
| DoorStates | Hashtable{문ID:byte} | InteractableDoor(방장) | InteractableDoor(전원) |

**Player Props**(본인만 쓴다): `HitCount`(Round), `RegisteredSlotCount`(Round), `SkinIndex`(Session).

**RaiseEvent**: 1 PaintStroke(Others, 캐시 없음), 2 ClaimMonster(→방장), 3 ClearColor(Others), 5 StartGameRequest(→방장), 6 DoorStateRequest(→방장). 4는 예약 비움.

**RPC**: `HideOrSeekPlayer.OnGrabbedByOwner / OnReleased / RequestGrabKill`(모두 대상 쿠키의 소유자에게만),
`GameManager.LogMsg`(씬 PhotonView, All — GameManager와 RoomExitController가 같은 PhotonView로 보냄).

**PhotonView 직렬화**: 공용 `NetworkTransformSync<TState>`(위치·회전·상태 int). 쿠키는 캐리 여부 bool을 하나 더 붙인다(`PlayerNetworkSync`).
원격 복사본은 키네마틱 + 보간(10m 이상 벌어지면 스냅).

### R3.3 도메인별 동작 요약

**Core** — 모두 정적 클래스 또는 순수 데이터. `GamePhaseState.Evaluate`는 네트워크와 분리된 순수 함수라 테스트된다.
`RoomState.HostActor`(시작 버튼 주인 = 최소 ActorNumber)와 `DesiredMasterActor`(진행 권한 = 괴물이 아닌 최소 ActorNumber)를 구분한다.
`RoomSceneTransition`은 `OpRemoveCompleteCache` → `LoadLevel` 순서로 이전 씬 Instantiate 캐시를 지운다.
`SpawnPositionFinder`는 캡슐 겹침 검사로 트리거(가마솥 등) 위 스폰을 피한다.

**Unit(쿠키)** — `HideOrSeekPlayer`가 조정자. 입력은 Update, 물리는 FixedUpdate. 협력 객체: `PlayerGroundDetector`(레이캐스트 접지),
`PlayerAnimationDriver`(트리거 전환, 점프 정점 정지, Carry 레이어), `PlayerCarryFollower`(들린 쪽이 캐리어 소켓을 로컬로 따라감 — 소유권 이전 없음),
`PlayerNetworkSync`. `PlayerGrabController`는 드는 쪽. 이동 잠금 = 외부 잠금 ∨ 파괴 ∨ 들림.

**Monster** — `MonsterController`는 입력·물리·애니메이션·촉수 돌진·돌진 경로 처형 판정을 혼자 맡는다(쿠키와 달리 협력 객체는 `MonsterTentacleDash` 하나).
`MonsterGrabKillTrigger`는 처형 판정의 단일 진입점(트리거 Enter/Stay + 돌진 경로), 쿨다운은 GrabKill 클립 길이.
나머지 9개 클래스는 괴물이 아니라 **판 진행** 담당이다(선정·합류·승패·결과·이탈·방장 정책·판 초기화·관전·배너).

**ColorTag** — `PlayerPaintCanvas`가 512² RenderTexture에 GL 쿼드로 스탬프를 그린다(잠금 규칙은 하드웨어 블렌딩).
로컬은 한 프레임치 스탬프를 모아 한 번에 그리고, 전송은 인원에 따라 1/15초~1/5초 간격으로 묶어 보낸다.
`PaintColliderUpdater`는 필요할 때만(칠하는 중이거나 커서가 몸 경계 안) 스킨 메시를 굽고, 물리 쿠킹은 Job으로 워커 스레드에서 한다.

**Interaction / Environment** — `CharacterInteractor`가 0.1초마다 로컬 캐릭터와 가장 가까운 `IInteractable`을 찾아 안내 문구를 띄우고 E로 전달한다.
`InteractableDoor`는 누른 쪽 반대 방향으로 열고, 상태는 Room Prop(`DoorStates`) 하나의 해시테이블에 문 ID별로 둔다.
방장은 서버 응답 전 값을 정적 `masterPendingWrites`에 겹쳐 두어 연속 변경이 서로 덮어쓰지 않게 한다.
메시지 큐가 멈춘 클라이언트(대기실 괴물)는 상호작용 대상에서 뺀다.

---

## R4. 11개 관점 감사

### R4.1 기존 책임 분리를 무시하는 코드 — ⚠ 일부 있음

**R4.1-1 채팅 이동 잠금이 쿠키 타입에 묶여 있다(실제 결함).**
`GameManager/GameManager.cs:126-135`는 `CharacterRegistry.FindLocal<HideOrSeekPlayer>()`로 **쿠키만** 찾아 잠근다.
`IGameCharacter`라는 공통 계약이 있는데도 채팅이 구체 타입을 안다. 결과적으로 괴물은 채팅창에 글자를 치는 동안
`PlayerInput`(레거시 `Input.GetKey`, 포커스와 무관)을 그대로 읽어 WASD로 움직이고 Shift로 돌진한다
(`Monster/MonsterController.cs:155-167`). `Interaction/CharacterInteractor.cs:95`의 주석("괴물은 채팅 이동 잠금 대상이 아니므로 여기서 거른다")이
이 틈을 이미 알고 상호작용만 따로 막고 있다. 관전 중인 쿠키도 채팅에 Space를 치면 관전 대상이 바뀐다(`SpectatorController.cs:59`).
- 권장: `IGameCharacter`에 `bool InputLocked { set; }`(또는 전역 `PlayerInput.IsSuppressed`)를 두고 채팅이 그것만 쓴다.
  입력 억제를 `PlayerInput` 한 곳에서 하면 괴물·관전·상호작용·색칠이 모두 한 번에 막힌다.

**R4.1-2 채팅 매니저가 전역 네트워크 설정을 바꾼다.**
`GameManager.cs:27-28`이 `Time.timeScale = 1`과 `PhotonNetwork.IsMessageQueueRunning = true`를 대입한다. 채팅과 무관한 전역 상태이고,
같은 대입이 `PlayerSpawner.cs:65`, `RoomExitController.cs:53`에도 있다(부록 C). 큐를 멈추는 쪽(`MonsterLobbyWaitController.cs:78`)과
되돌리는 쪽이 셋으로 흩어져 있어, 큐 정지 설계를 바꿀 때 어느 대입이 의도된 것인지 알기 어렵다.
- 권장: 채팅에서 제거. 큐 재개는 PUN `LoadLevel`이 자동으로 하므로 `RoomExitController.OnLeftRoom`의 복구 한 곳이면 충분하다.

**R4.1-3 UI 패널이 네트워크 권한 처리를 맡는다.**
- `Lobby/GameLobbyController.cs:98-104`: 대기실 **UI 패널 프리팹**이 `IOnEventCallback`으로 `StartGameRequest`를 받아 방장 권한 로직(`GameStartAuthority.TryStart`)을 실행한다.
  UI 프리팹을 바꾸거나 패널을 끄면 판 시작이 안 된다. 같은 성격의 방장 로직은 모두 `MonsterManagers`에 있다.
- `Lobby/LobbyController.cs:27-32, 45`: 로비 UI 패널이 `AutomaticallySyncScene`, `GameVersion`, `SerializationRate`를 설정한다.
  `SerializationRate`는 **연결되지 않았을 때만** 설정되므로 게임에서 로비로 돌아온 뒤에는 다시 적용되지 않는다(정적이라 값은 유지됨).
- 권장: 시작 요청 수신은 `MonsterManagers`의 비 UI 컴포넌트로, Photon 초기화는 전용 부트스트랩(또는 `NetworkDefaults` 옆 정적 초기화)으로.

**R4.1-4 캐릭터 조정자가 다른 도메인을 직접 호출한다.**
- `Unit/HideOrSeekPlayer.cs:127`: 파괴되면 `GetComponent<SpectatorController>()?.EnterSpectatorMode()` — 쿠키가 관전(Monster 도메인)을 안다.
  `CookieLifeStatePresenter`처럼 HitCount를 보고 스스로 반응하게 하면 의존이 사라진다.
- `Unit/HideOrSeekPlayer.cs:147-149`, `Monster/MonsterController.cs:84-91`: 캐릭터가 `Camera.main`에서 `Camera_Ctrl`을 찾아 자신을 넘긴다.
- `GameManager/PlayerSpawner.cs:48`: 운영 코드가 **Dev 도메인**(`OfflineModeBootstrap.SpawnAsMonster`)을 참조한다.
- `Unit/PlayerCarryFollower.cs:38, 55`: 들린 쪽 협력 객체가 드는 쪽 컴포넌트의 정적 유틸(`PlayerGrabController.SetCollisionIgnored`)을 쓴다.

**R4.1-5 퇴장 컨트롤러가 채팅 RPC를 빌려 쓴다.**
`RoomExitController.cs:11, 39`는 GameManager의 PhotonView와 `GameManager.RpcLogMsg`로 퇴장 메시지를 보낸다. 같은 GameObject에 있어야 한다는 배치 제약이 생긴다.
- 권장: 채팅에 `public void PostSystemMessage(string)`를 두고 퇴장 컨트롤러는 그것만 호출.

**R4.1-6 폴더(도메인)와 실제 책임이 어긋난다.**
`Monster/`의 19개 중 9개(GameRuleController, ResultScreenController, PlayerResultRow, RoundStateResetter, MasterClientPolicy, RoomLifecycleWatcher,
GamePhaseStarter, SpectatorController, SpectatorLabel)는 괴물이 아니라 판 진행·관전이다. `ColorTag/CookieLifeStatePresenter`는 색칠과 무관한 쿠키 생존 표시,
`GameManager/`의 FallGuard·VoidKillZone·ConfirmDialog는 채팅 매니저와 무관하다. 새 기능을 어디에 둘지 판단 기준이 흐려진다.
- 권장: `Round/`(판 진행), `Spectator/`, `World/`(낙하·킬존) 폴더로 재배치. `.meta` GUID는 파일 이동으로 유지되므로 씬 참조는 깨지지 않는다.

**R4.1-7 MonsterController가 쿠키보다 책임이 많다.** 282줄에 입력·물리·애니메이션 전환·클립 길이 조회·돌진 경로 처형 판정·리스폰이 모여 있다.
쿠키는 같은 책임을 협력 객체 4개로 나눴다. 괴물 종류를 늘리면 이 파일이 먼저 커진다.

### R4.2 Manager 간 의존성 과도 증가 — ✅ 양호(암묵 결합 주의)

직접 참조는 적다(부록 A).

| 참조 | 방식 |
|---|---|
| Cauldron → MonsterRevealController | 인스펙터 참조(`Cauldron.cs:11`) |
| MonsterLobbyWaitController → GameManager, Back 버튼 | 인스펙터 `Behaviour[]`, `Button[]` |
| RoomExitController → GameManager | RPC 이름 상수 + 같은 PhotonView |
| GameLobbyController → GameStartAuthority | 정적 호출 |

대신 **Room Props를 매 프레임 폴링하는 마스터 전용 Update가 8개**다(부록 D). 이들은 서로 모르지만 같은 키의 조합에 동시에 반응한다.
예: `PaintPhaseEndTime` 경과 순간 `PaintPhaseController`(강제 도포)와 `MonsterJoinController`(합류)가 같은 프레임에 각자 쓰기를 보낸다.
지금은 서로 다른 키라 안전하지만, "강제 도포가 끝난 뒤 합류" 같은 순서가 필요해지면 폴링 구조로는 표현할 수 없다.
`GamePhaseState`가 **해석**은 한 곳으로 모았지만 **전이**(누가 언제 다음 단계로 넘기는가)는 여전히 흩어져 있다.
- 권장(규모가 커질 때): 마스터 전용 `RoundDirector` 하나가 단계 전이를 순서대로 수행하고, 나머지는 결과만 읽게 한다.

### R4.3 Prefab과 Script의 역할 뒤섞임 — ⚠ 일부 있음

| # | 내용 | 근거 | 위험 |
|---|---|---|---|
| 1 | 코드가 프리팹 자식 이름을 안다 | `HideOrSeekPlayer.cs:178, 185` `"Mesh_0"` | 모델 교체·개명 시 자기 충돌 무시가 조용히 빠짐(에러 로그만) |
| 2 | 애니메이터 트리거 이름 = enum `ToString()` | `PlayerAnimationDriver.cs:28-29`, `MonsterController.cs:269-270`, `"Jump"` 상태명(`:54`), `"Carry"` 레이어 | 컨트롤러 파라미터 개명 시 조용히 무반응 |
| 3 | 클립 길이를 이름 키워드로 찾음 | `MonsterController.cs:112-121`(`GrabKill`, `TentacleDash`), `InteractableDoor.cs:255-261`(`Close`) | 클립 이름에 키워드가 두 번 들어가면 엉뚱한 길이 |
| 4 | 같은 렌더러를 두 컴포넌트에 따로 배선 | `PlayerSkinApplier.cs:6-7, 18` ↔ `PlayerPaintCanvas.bodyRenderer` | 한쪽만 바꾸면 스킨·페인트가 다른 메시에 적용 |
| 5 | 같은 UI의 프리팹판과 씬판이 따로 존재 | `Resources/UI/Scene/ColorSelectionPanel.prefab`(스와치 10개 고정, ColorSwatchGroup 없음, PlayerTestScene만 사용) ↔ GameScene의 비프리팹 `ColorSlotPanel`(ColorSwatchGroup 사용) | 한쪽 수정이 다른 쪽에 반영되지 않음 |
| 6 | 에디터 빌더가 만든 프리팹의 직렬화가 코드보다 오래됨 | `Witch_Cookie_House.prefab:945-951` — 없는 필드 `zoneWidth`가 남고 `doorId`·`interactionRange`는 직렬화돼 있지 않음(기본값으로 동작, MCP 실측: ID=오브젝트 이름, 범위 2m) | 지금은 무해. 빌더를 다시 돌리지 않으면 인스펙터 값이 저장되지 않은 상태로 남음 |
| 7 | 런타임에 UI를 코드로 생성 | `CharacterInteractor.cs:21-33` + `InteractionPromptUI.Create` | 씬에서 안내 UI를 볼 수 없고, 다른 Canvas와의 정렬 순서를 씬에서 조정할 수 없음 |
| 8 | 스폰 겹침 검사 크기가 쿠키 캡슐 상수 | `SpawnPositionFinder.cs:10-12` | 괴물 스폰에도 쿠키 크기로 검사. 괴물 콜라이더(r 0.3)가 작아 지금은 문제없음 |

`UI 프리팹`을 `Resources/`에 두는 폴더 규칙(CLAUDE.md)을 따르고 있지만, 런타임 로드는 InteractionPromptUI뿐이라
나머지 7개는 `Resources`에 있을 이유가 없다(빌드에 무조건 포함됨). 규칙 자체를 "Resources.Load로 쓰는 것만"으로 좁히는 편이 낫다.

### R4.4 Scene에 직접 의존하는 코드 — ✅ 양호

| 의존 | 위치 | 비고 |
|---|---|---|
| `GameObject.Find(이름)` | `SceneSpawnPoints.cs:13`, `PlayerSpawner.cs:56`, `MonsterJoinController.cs:72`, `MonsterTestSpawner.cs:24` | 모두 스폰 지점. 뒤의 3곳은 이미 있는 `SceneSpawnPoints.TryFindClearPosition`을 쓰지 않고 직접 찾는다(R4.11-1) |
| `Camera.main` + `Camera_Ctrl` 전제 | HideOrSeekPlayer, MonsterController, SpectatorController, PlayerPaintCanvas, BrushCursorController, PlayerBillBoard (10회) | Main Camera에 `Camera_Ctrl`이 있어야 함(GameLobbyScene·GameScene·PlayerTestScene 모두 있음) |
| 컴포넌트 존재로 씬 판별 | `PaintPhaseController.IsPaintScene`(`:16, 21-29`) | 씬 이름 비교보다 낫지만 정적 플래그(R4.7-1) |
| PUN 내부 키 문자열 | `MonsterJoinController.cs:18` `"curScn"` | PUN 업데이트로 키가 바뀌면 괴물의 씬 동기화가 영영 복구되지 않음 |
| 문 ID = GameObject 이름 | `InteractableDoor.cs:59` | 한 씬에 같은 이름의 문이 둘이면 서로 연동됨. 현재는 과자집 1채·문 4개로 이름이 모두 다름(MCP 실측) |
| 씬 이름 상수 | `SceneNames` | 한 곳에 모여 있음 |

새로 들어온 상호작용 시스템은 `InteractableRegistry`를 써서 씬 탐색·물리 쿼리 없이 사물을 찾는다 — 의존이 늘지 않았다.

### R4.5 Singleton 남발 — ✅ 양호(정적 상태 12곳)

`public static X Instance` 형태는 **없다**. 대신 정적 상태가 12곳 있다(부록 B). 대부분 의도된 전역 캐시·레지스트리지만 두 가지는 주의가 필요하다.

- **숨은 DDOL 싱글톤**: `CharacterInteractor.cs:14, 21-28` — `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`로 **모든 씬**(LobbyScene 포함)에 자동 생성되고
  씬 전환에도 남는다. 씬 목록에서 보이지 않으므로 "상호작용 문구가 왜 로비에서도 떠 있나" 같은 문제를 씬만 보고는 찾을 수 없다.
  현재 LobbyScene에는 캐릭터가 없어 무해하다.
- **방을 옮겨도 남는 정적 상태**: `InteractableDoor.masterPendingWrites`(R4.10-3), `GameStartAuthority.lastStartTime`(3초 가드 — 무해),
  `CharacterRegistry`/`InteractableRegistry`(OnDisable로 해제되므로 무해).

### R4.6 ScriptableObject의 책임 — ✅ 양호

| SO | 역할 | 평가 |
|---|---|---|
| GameSettingsSO | 규칙 수치 + 파생 계산 2개(`PaintStrokeSendIntervalFor`, `MonsterCountFor`) | 순수 함수라 적절. `OnValidate`에서 괴물 수를 정원-1로 제한 |
| InputBindingsSO | 키 설정 | 읽기 전용. 런타임 키 변경 UI를 만들면 에디터에서 **에셋 자체가 바뀌므로** 복제본을 써야 함(주석에 이미 언급) |
| ColorPaletteSO | 색 목록 | `GetColor`는 범위 방어가 있지만 **`Count`는 `colors`가 null이면 NRE**(`ColorPaletteSO.cs:8`). 주석 "10개 고정"은 이제 사실이 아님(ColorSwatchGroup이 가변 처리) |
| BrushSettingsSO | 붓 크기·커서 | 적절. `CreateAssetMenu` 경로가 `ColorTag/…`로 다른 SO(`TagOfChaos/…`)와 다름 |
| SkinCatalogSO | 스킨 목록 | `ClampIndex`로 네트워크 인덱스 방어. 적절 |

런타임 상태(점수·현재 단계 등)를 SO에 쓰는 곳은 **없다.** `GameSettings.Current`/`PlayerInput.Bindings`는 에셋이 없으면
`CreateInstance`로 기본값을 만들고 경고한다 — 설정 누락이 조용히 지나가지 않는다.

### R4.7 Unity Lifecycle 순서 — ✅ 대부분 해결됨

이미 해결된 것(코드 주석과 실제 구현이 일치함을 확인):
- 협력 객체·캔버스를 `Awake`에서 생성해 `OnPhotonSerializeView`/이벤트가 `Start` 전에 와도 안전(`HideOrSeekPlayer.cs:130-150`, `PlayerPaintCanvas.cs:101-109`).
- 스킨(`PlayerSkinApplier.Awake`) → 합성 머티리얼(`PlayerPaintCanvas.Start`) 순서 보장.
- 씬 로드 직후 `InRoom=false` 대응(코루틴 대기 3곳, 첫 프레임 Update 2곳, `OnJoinedRoom` 보강).
- 자기 GameObject를 끄면 Photon 콜백이 풀리는 문제 → `CanvasGroupVisibility`로 통일(5개 UI).
- PUN `DefaultPool`은 비활성 상태로 복제한 뒤 ViewID·Owner를 넣고 활성화하므로 `Awake`에서 `pv.Owner`를 읽는 것은 안전.

남은 잠재 위험:

**R4.7-1 `PaintPhaseController.IsPaintScene` 정적 플래그.** `Awake`에서 true, `OnDestroy`에서 false(`:21-29`). `PhotonNetwork.LoadLevel`은
비동기 로드라 새 씬의 `Awake`가 이전 씬의 `OnDestroy`보다 먼저 실행될 수 있다. 지금은 "색칠 씬 → 색칠 씬" 전환이 없어 드러나지 않지만,
GameScene을 재시작하거나 색칠 씬을 둘로 나누면 새 씬에서 플래그가 false로 덮인다.
- 권장: bool 대신 카운터(Awake에서 +1, OnDestroy에서 -1)나 "현재 인스턴스" 참조로.

**R4.7-2 `ConfirmDialog`가 `Awake`에서 자기 자신을 끈다**(`ConfirmDialog.cs:19`). 씬에 **비활성 상태로** 배치하면 첫 `Show()` 때
`SetActive(true)` → `Awake` → 곧바로 `SetActive(false)`가 돼 대화상자가 절대 뜨지 않는다. 현재 두 씬 모두 활성 상태로 배치돼 있어 동작한다(YAML 확인).
- 권장: 숨김은 CanvasGroup으로, 또는 `Show()`에서 `Awake` 여부와 무관하게 동작하도록.

**R4.7-3 UnityEngine.Object에 `?.` 사용.** `PlayerGrabController.cs:58, 77`의 `self?.SetCarryLayerWeight` — `self`는 직렬화 필드라
에디터에서 비어 있으면 Unity의 "가짜 null"이 들어가 `?.`가 통과하고 예외가 난다. 현재는 연결돼 있어 무해.

**R4.7-4 원격 복사본의 첫 프레임 물리.** 두 캐릭터 프리팹 모두 기본값이 `isKinematic=false, useGravity=true`이고 원격 복사본은 `Start()`에서야 키네마틱이 된다
(`HideOrSeekPlayer.cs:162`, `MonsterController.cs:97`). Unity는 `Start`를 첫 `FixedUpdate` 전에 부르므로 실제로 떨어지지는 않지만,
프리팹 기본값을 키네마틱으로 두고 로컬만 풀면 이 가정에 기대지 않아도 된다.

### R4.8 Event 구독/해제 — ✅ 양호

| 이벤트 | 구독 | 해제 | 평가 |
|---|---|---|---|
| `SpectatorController.SpectateTargetChanged`(static) | `SpectatorLabel.OnEnable` | `OnDisable` | 짝 맞음. 발행자가 파괴될 때 null을 한 번 보내 라벨을 숨김(`SpectatorController.cs:70-73`) |
| Photon 콜백·`IOnEventCallback` | `MonoBehaviourPunCallbacks.OnEnable` | `OnDisable` | OnEnable/OnDisable을 재정의한 5개 클래스(HideOrSeekPlayer, MonsterController, InteractableDoor, BrushCursorController, PlayerPaintCanvas) 모두 `base` 호출 |
| `Button.onClick.AddListener` | 각 `Awake`/`Start` | 없음 | 버튼과 수명이 같아 누수 없음. `PlayerSkinSelector`는 복제 버튼에 대비해 `RemoveAllListeners` 후 등록 |
| 레지스트리(Character/Interactable) | `OnEnable` | `OnDisable` | 짝 맞음. 목록을 돌며 Destroy할 때는 먼저 복사(`MonsterLobbyWaitController.cs:99-103`) |

주의할 점 하나: `MonsterLobbyWaitController`가 대기 중 `GameManager.enabled = false`로 끄면 GameManager의 Photon 콜백 등록도 풀린다.
GameManager는 Photon 콜백을 재정의하지 않아 지금은 영향이 없지만, 나중에 채팅에 `OnPlayerEnteredRoom` 등을 넣으면 괴물 대기 중에는 받지 못한다.

### R4.9 Object Pool과 Instantiate/Destroy 충돌 — ✅ 충돌 없음

`IPunPrefabPool`·커스텀 풀은 **없다**. 네트워크 객체는 PUN `DefaultPool`(Resources 로드 + Instantiate/Destroy)이고 씬 전환으로 파괴된다.
그래서 "풀에서 꺼낸 객체를 Destroy" 같은 충돌은 구조적으로 없다. 대신:

**R4.9-1 네트워크 객체를 로컬 `Destroy`로 지운다.** `MonsterLobbyWaitController.cs:97-104`가 대기실에 굳은 다른 쿠키 아바타를
`PhotonNetwork.Destroy`가 아닌 `Object.Destroy`로 치운다. 원 주인이 이미 씬 전환으로 파괴했으므로 의도된 처리이고,
PUN은 "엔진이 파괴함" 로그만 남긴다. 나중에 커스텀 풀을 도입하면 이 경로가 풀을 우회하므로 함께 바꿔야 한다.

**R4.9-2 대기실 참가자 목록을 매번 전부 부수고 다시 만든다.** `GameLobbyController.cs:115-124`는 인원·호스트가 바뀔 때마다(그리고 Update 안전망에서도)
자식 전부를 `Destroy`하고 `Instantiate`한다. 같은 프레임에 두 번 호출되면(OnPlayerEnteredRoom + Update 안전망) `Destroy`가 프레임 끝에 처리되므로
**한 프레임 동안 행이 두 배로** 쌓인다(보이는 시간이 짧아 눈에 띄지 않을 뿐). 이미 있는 `UiListBuilder.Sync`로 재사용하면 해결된다.
또한 `Update`가 매 프레임 `RoomState.HostActor()`를 호출해 **매 프레임 int 배열을 할당**한다(`RoomState.cs:87`).

기타 UI 목록(`UiListBuilder`, `LobbyController` 방 목록, 결과 행)은 필요한 만큼만 만들고 지운다 — 문제없음.
`CookieLifeStatePresenter.breakVfxPrefab`은 현재 비어 있어(MCP 실측) 파편 연출 누수 걱정은 없다. 넣을 때는 자동 파괴(`stopAction=Destroy`)가 필요하다.

### R4.10 Photon Ownership/RPC 구조 — ⚠ 일부 있음

잘 지켜지는 것: Room Props는 방장만(10개 키 모두), Player Props는 본인만(3개 키), 파괴 확정은 피해자 본인만, 문 상태는 방장 요청 방식,
괴물 스폰은 괴물 본인이 `PhotonNetwork.Instantiate`, 색칠 이벤트는 viewId로 대상 필터링, 씬 전환은 방장만(`RoomSceneTransition`).

**R4.10-1 그랩에 수락 절차가 없다(실제 결함).**
- 드는 쪽은 RPC를 보내자마자 `carriedPlayer = target`으로 확정한다(`PlayerGrabController.cs:54-58`).
- 들리는 쪽 `OnGrabbedByOwner`는 이미 다른 사람에게 들려 있는지 확인하지 않고 캐리어를 **덮어쓴다**(`HideOrSeekPlayer.cs:77-82` → `PlayerCarryFollower.cs:29-40`).
- `OnReleased`는 보낸 사람이 현재 캐리어인지 확인하지 않는다(`HideOrSeekPlayer.cs:84-89`).

재현: A가 C를 든다 → B가 C를 든다(C는 B를 따라감) → A가 F로 놓는다 → C에게 `OnReleased` 도착 → **C가 B에게서 떨어진다.**
B는 여전히 `carriedPlayer = C`, 상체 캐리 자세, C와의 충돌 무시 상태로 남는다. B가 다시 F를 눌러야 풀리는데, 그때 보내는
`OnReleased`는 C가 들려 있지 않아 무시된다. 또 다른 경우: B가 C를 들고 있는데 A가 B를 들면 C→B→A로 **연쇄 캐리**가 된다(막는 조건 없음).
- 권장: 들리는 쪽이 `IsCarried`면 거절하고, 결과를 드는 쪽에 회신(`OnGrabResult(bool)`)해 그때 `carriedPlayer`를 확정. `OnReleased`는
  `info.Sender`가 현재 캐리어 소유자일 때만 처리. 캐리 중인 쿠키는 잡을 수 없게 `TryGrab`에서 거른다.

**R4.10-2 RPC 수신 측이 보낸 사람을 검증하지 않는다.** `RequestGrabKill`(`HideOrSeekPlayer.cs:108-128`)은 누가 보냈든 자신을 파괴한다.
정상 클라이언트는 괴물만 보내지만, 수정된 클라이언트 하나가 모든 쿠키를 즉시 파괴할 수 있다. `PhotonMessageInfo`를 받아
`RoomState.IsMonster(info.Sender.ActorNumber)`를 확인하는 한 줄로 막힌다. 같은 이유로 `DoorStateRequest`(`InteractableDoor.cs:142-149`)도
거리 검증이 없다(문을 원격으로 여닫을 수 있음 — 영향 작음).

**R4.10-3 문 상태의 방장 대기 캐시가 정적이다.** `InteractableDoor.cs:31`의 `masterPendingWrites`는 `static`이고 방장 교체(`:177`)와
판 초기화로 전체 삭제될 때(`:169`)만 비워진다. 방장이던 사람이 응답을 받기 전에 방을 나가 다른 방을 만들면, 이전 방의 대기 값이
새 방의 첫 `WriteState`에 합쳐져 **다른 방의 문 상태가 섞인다**(같은 문 이름을 쓰므로). 드물지만 원인 찾기가 매우 어렵다.
- 권장: `OnLeftRoom`과 씬 전환 때 비우기(또는 인스턴스 필드 + 문 관리자 한 개로).

**R4.10-4 퇴장 시 Player Props를 API 없이 지운다.** `RoomExitController.cs:43-44`는 `LocalPlayer.CustomProperties.Remove(key)`로 로컬 사본만 지운다.
다음 방 입장 때 로컬 사본이 전송되므로 목적(다음 방에 HitCount를 가져가지 않기)은 달성되지만, 떠나기 전까지 이 클라이언트와 서버 값이 어긋난다.
의도를 주석으로 남겨 두었으므로 유지해도 되지만, `OnLeftRoom`에서 지우면 어긋나는 구간이 없다.

**R4.10-5 PaintStroke 이벤트는 캐시되지 않는다.** 늦게 들어온 사람은 칠해진 색을 볼 수 없다. 게임 시작 때 방을 닫으므로(`GameStartAuthority.cs:66`)
정상 흐름에선 문제없고, 괴물은 큐를 멈춰 이벤트를 모아 받는다. 재접속 기능을 넣으면 필요해진다.

### R4.11 중복 로직 — ⚠ 다수

| # | 중복 | 위치 | 권장 |
|---|---|---|---|
| 1 | 스폰(Find → 겹침 회피 → Instantiate) 3벌, 프리팹 이름 `"MonsterPlayer"` 2벌 | `PlayerSpawner.cs:54-69`, `MonsterJoinController.cs:72-80`, `MonsterTestSpawner.cs:24-31`(겹침 회피도 안 함) | `SceneSpawnPoints.TryFindClearPosition` + 프리팹 이름 상수(`NetworkPrefabs`) |
| 2 | "InRoom이 될 때까지" 코루틴 3벌 + "방에 들어온 첫 프레임" Update 2벌 | `GameManager.cs:36-39`, `PlayerSpawner.cs:29-32`, `MonsterTestSpawner.cs:17-20` / `MasterClientPolicy.cs:19-25`, `RoundStateResetter.cs:14-22` | `RoomState.WaitUntilInRoom()`(IEnumerator) 하나 |
| 3 | 클립 이름으로 길이 찾기 | `MonsterController.cs:112-121`, `InteractableDoor.cs:255-261` | `AnimatorUtil.FindClipLength` |
| 4 | 트리거 기반 상태 전환(`ResetTrigger(prev)`, `SetTrigger(new)`) | `PlayerAnimationDriver.cs:20-33`, `MonsterController.cs:265-273` | 제네릭 `TriggerStateAnimator<TState>` — 동기화처럼 공용화 |
| 5 | 로컬 리지드바디 설정 / 낙하 리스폰 / 카메라 초기화 | `HideOrSeekPlayer.cs:162-169, 360-374, 147-149` ↔ `MonsterController.cs:96-104, 240-253, 84-91` | 공용 `CharacterBody` 협력 객체 |
| 6 | 파괴 여부 판정 | `PlayerGrabController.cs:87-93` ↔ `RoomState.IsBroken`(`RoomState.cs:54`) | `RoomState.IsBroken` 사용 |
| 7 | 표시 이름(닉네임 없으면 `#번호`) 3벌, 1곳은 미적용 | `GameLobbyController.cs:181-185`, `MonsterRevealController.cs:74`, `SpectatorController.cs:132-137` / `ResultScreenController.cs:67-68`은 원본 NickName | `RoomState.DisplayName(Player)` |
| 8 | 색칠 남은 시간 표시 2곳 | `ColorSelectionPanel.cs:31-35`(timeLabel, GameScene에 연결됨) + GameScene `PaintCountdown`(PhaseCountdownDisplay Paint, "변장 시간 mm:ss") | 하나로 정리 |
| 9 | 로컬 캐릭터 찾기 | `CharacterInteractor.cs:63-68` ↔ `CharacterRegistry.FindLocal<T>()`(`GameCharacter.cs:57-62`) | `FindLocal<IGameCharacter>()` |
| 10 | 레지스트리 클래스 2벌(코드 동일) | `CharacterRegistry`(`GameCharacter.cs:37-63`) ↔ `InteractableRegistry`(`IInteractable.cs:22-40`) | 제네릭 `Registry<T>` |
| 11 | "상태가 바뀔 때만 CanvasGroup 갱신" | `PhaseCountdownDisplay.cs:55-60`, `ColorSelectionPanel.cs:42-47`, `InteractionPromptUI.cs:49-54` | `CanvasGroupVisibility`에 캐시 버전 추가 |
| 12 | `IsPaintPhaseActive()` 한 줄 래퍼 | `BrushCursorController.cs:92`, `PlayerPaintCanvas.cs:346` | `GamePhaseState.IsPaintActive` 직접 사용 |
| 13 | 괴물 이탈 시 목록에서 빼기 | 대기실 `MonsterAssignmentAuthority.cs:75-89` ↔ 게임 `RoomLifecycleWatcher.cs:13-34` | 씬별 후속 처리가 달라 분리는 타당. 배열 제거만 `RoomState.WithoutMonster(actor)`로 |
| 14 | `IsRoomFull()` 한 줄 래퍼 | `GameLobbyController.cs:136` | `RoomState.IsRoomFull` 직접 사용 |

---

## R5. 그 밖의 발견 사항

1. **R5-1 채팅 토글이 키 떼기만 본다.** `GameManager.cs:54-72` — 입력창 밖을 클릭해 포커스를 잃어도 `bEnter`는 true로 남아
   다음 Enter가 "닫기"로 처리되고, 그동안 쿠키 이동 잠금이 유지된다.
2. **R5-2 `GamePhaseStarter`는 정상 흐름에서 아무 일도 하지 않는다**(`GamePhaseStarter.cs:5-8`). 대기실을 거치지 않는 테스트 경로용 대비책인데,
   PlayerTestScene에는 배치돼 있지 않고 GameScene에만 있다. 목적과 배치가 반대다.
3. **R5-3 빈 팔레트 예외.** `PaintPhaseController.cs:60-64` — 팔레트가 비어 있으면 `shuffledColors.Length == 0`이라 `i % 0`에서 예외, 강제 도포 기록이
   안 돼 방장이 매 프레임 재시도한다. `ColorPaletteSO.Count`도 null 배열이면 NRE(`ColorPaletteSO.cs:8`). 현재 에셋은 10색이라 잠재 결함.
4. **R5-4 PUN 내부 키 의존.** `MonsterJoinController.cs:18`의 `"curScn"` — PUN 업데이트 시 가장 먼저 확인할 곳.
5. **R5-5 쓰기만 하는 키.** `MonsterRevealTime`은 기록만 되고 읽는 곳이 없다(`NetKeys.cs:13`). 연출 예정이면 두고, 아니면 제거.
6. **R5-6 문 상태가 판마다 초기화된다.** `DoorStates`가 Round 수명이라(`NetKeys.cs:74`) 판이 끝나 대기실로 돌아오면 모든 문이 닫힌다. 의도라면 문서화, 아니면 Session으로.
7. **R5-7 괴물의 큐 정지 중 상호작용 차단**은 잘 처리됐다(`InteractableDoor.cs:105`) — 다만 판정이 사물 쪽 `CanInteract`에 있어,
   새 사물을 만들 때마다 같은 조건을 다시 넣어야 한다. `CharacterInteractor`에서 한 번 거르는 편이 안전하다.
8. **R5-8 매 프레임 할당**: `GameLobbyController.Update`의 `HostActor()`(배열), `MonsterAssignmentAuthority`의 LINQ(이탈 시에만 — 무해),
   `ResultScreenController`의 LINQ(1회 — 무해). 실질적인 것은 첫 번째뿐이다.
9. **R5-9 테스트 범위.** `RuleTests.cs` 12개가 순수 함수(방장·호스트 계산, 괴물 수, 전송 간격, 키 표, 단계 해석, 스킨 인덱스, 동기화 왕복, UI 레이아웃)를 덮는다.
   R4.10-1 같은 RPC 순서 문제는 테스트가 없다 — 그랩 수락 로직을 순수 클래스로 빼면 EditMode 테스트가 가능하다.

---

## R6. 권장 로드맵

**1단계 — 결함 수정(각 30분 이내)**
- 그랩 수락·놓기 발신자 검증·캐리 중 잡기 금지(R4.10-1)
- 처형 RPC 발신자 검증(R4.10-2)
- 채팅 입력 억제를 `PlayerInput` 한 곳으로(R4.1-1, R5-1)
- 빈 팔레트 방어(R5-3), 문 대기 캐시 초기화(R4.10-3)

**2단계 — 책임 정리(각 1시간 이내)**
- GameManager의 전역 설정 대입 제거(R4.1-2), 시작 요청 수신을 비 UI 컴포넌트로(R4.1-3)
- 스폰·InRoom 대기·표시 이름·클립 길이 공용화(R4.11-1·2·3·7)
- 대기실 목록 `UiListBuilder` 재사용 + `HostActor` 할당 제거(R4.9-2)
- 색칠 시간 이중 표시 정리(R4.11-8), ColorSelectionPanel 프리팹/씬 정의 하나로(R4.3-5)

**3단계 — 구조(판 규모가 커질 때)**
- 폴더 재배치: `Round/`, `Spectator/`, `World/`(R4.1-6)
- MonsterController 협력 객체 분리 + 캐릭터 공용 `CharacterBody`(R4.1-7, R4.11-5)
- 마스터 전용 단계 전이를 `RoundDirector` 하나로(R4.2)
- 정적 플래그 `IsPaintScene`을 카운터/참조로(R4.7-1)

---

## 부록

### A. 교차 도메인 의존성(주석 제외, Core 제외)

| 파일 | 참조하는 다른 도메인 클래스 |
|---|---|
| GameManager/GameManager.cs | HideOrSeekPlayer(Unit) |
| GameManager/PlayerSpawner.cs | HideOrSeekPlayer(Unit, 주석 수준), **OfflineModeBootstrap(Dev)** |
| Lobby/PlayerSkinSelector.cs | SkinCatalogSO(Unit) |
| Monster/Cauldron.cs | HideOrSeekPlayer(Unit) |
| Monster/MonsterController.cs | Camera_Ctrl(Camera), HideOrSeekPlayer(Unit) |
| Monster/MonsterGrabKillTrigger.cs | HideOrSeekPlayer(Unit) |
| Monster/SpectatorController.cs | Camera_Ctrl(Camera) |
| Unit/HideOrSeekPlayer.cs | Camera_Ctrl(Camera), **SpectatorController(Monster)** |
| Unit/PlayerSkinApplier.cs | **PlayerPaintCanvas(ColorTag)** |
| Environment/InteractableDoor.cs | IInteractable, InteractableRegistry(Interaction) |

Core는 Photon·Unity 외에 어떤 도메인도 참조하지 않는다(단방향 의존 유지).

### B. 전역(static) 상태 목록

| 상태 | 위치 | 수명 | 비고 |
|---|---|---|---|
| `CharacterRegistry.characters` | Core/GameCharacter.cs:39 | 앱 | OnEnable/OnDisable 등록·해제 |
| `InteractableRegistry.interactables` | Interaction/IInteractable.cs:24 | 앱 | 같음 |
| `CharacterInteractor.instance` | Interaction/CharacterInteractor.cs:14 | 앱(DDOL) | 자동 생성 |
| `PlayerPaintCanvas.Local` | ColorTag/PlayerPaintCanvas.cs:40 | 로컬 쿠키 수명 | Start 설정, OnDestroy 해제 |
| `PaintPhaseController.IsPaintScene` | ColorTag/PaintPhaseController.cs:16 | 씬 | R4.7-1 |
| `InteractableDoor.masterPendingWrites` | Environment/InteractableDoor.cs:31 | 앱 | **방을 옮겨도 남음**, R4.10-3 |
| `GameStartAuthority.lastStartTime` | Lobby/GameStartAuthority.cs:20 | 앱 | 3초 가드 |
| `SpectatorController.SpectateTargetChanged` | Monster/SpectatorController.cs:26 | 앱 | 이벤트 |
| `OfflineModeBootstrap.SpawnAsMonster` | Dev/OfflineModeBootstrap.cs:14 | 씬(OnDestroy 해제) | 개발용 |
| `GameSettings.cached`, `PlayerInput.bindings` | Core | 앱 | SO 캐시 |
| `SpawnPositionFinder.OverlapBuffer` | Core/SpawnPositionFinder.cs:14 | 앱 | 메인 스레드 전용 버퍼 |

### C. 전역 네트워크·엔진 설정을 바꾸는 곳

| 설정 | 쓰는 곳 |
|---|---|
| `IsMessageQueueRunning` | false: MonsterLobbyWaitController:78 / true: GameManager:28, PlayerSpawner:65, RoomExitController:53 (+PUN LoadLevel 자동 재개) |
| `AutomaticallySyncScene` | LobbyController:30(true), MonsterLobbyWaitController:57(괴물=false), MonsterJoinController:40(복구) |
| `KeepAliveInBackground` | MonsterLobbyWaitController:76(연장), MonsterJoinController:41, RoomExitController:54(복구) |
| `SerializationRate`, `GameVersion` | LobbyController:31, 45 |
| `OfflineMode` | OfflineModeBootstrap:18, 27 |
| `Time.timeScale` | GameManager:27 |
| `Cursor.visible` / `Cursor.lockState` | BrushCursorController, Camera_Ctrl |

### D. 마스터 전용 Update 폴링

| 컴포넌트 | 씬 | 조건 → 동작 |
|---|---|---|
| MonsterAssignmentAuthority | GameLobby | 정원·기한 → 괴물 확정/초기화 |
| MasterClientPolicy | 둘 다 | 입장 첫 프레임 → 방장 정책 적용 |
| RoundStateResetter | GameLobby | 입장 첫 프레임 → 판 초기화 |
| GamePhaseStarter | Game | PaintPhaseEndTime 없음 → 기록(예비) |
| PaintPhaseController | Game | 색칠 종료 → 강제 도포 배정 |
| MonsterJoinController | Game | 색칠 종료 → 합류·생존 종료 시각 |
| GameRuleController | Game | Hunt 중 → 승패 판정 |
| RoomLifecycleWatcher | Game | 괴물 전원 이탈 후 지연 → 대기실 복귀 |
