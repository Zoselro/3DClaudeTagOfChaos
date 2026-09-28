# 조사 보고서: TagOfChaos 런타임 스크립트 전수 분석 + 아키텍처 11개 관점 감사 (2026-09-28, 커밋 `7f8cd38`)

> **개정 안내 (3차)**: 이 문서는 커밋 `0a52d28` 기준 판(2026-09-27)을 **갱신**한다. 이전 판 원문은
> `git show 7f8cd38:TagOfChaos/Plan.md/research.md`로 볼 수 있다.
> 코드 주석이 `research.md R4.10-2`처럼 이 문서의 절 번호를 참조하므로 **R 번호 체계(R1~R6, 부록 A~D)와 기존 항목 번호를 그대로 유지**했다.
> 각 항목에 상태를 붙였다: **✅ 해결됨** / **⏸ 유지(미해결)** / **🆕 신규**. 새 항목은 같은 절 안에서 다음 번호를 받는다.
> (`§8.x`, `§12 E1~E12`, `G5-1` 같은 참조는 그보다 더 이전 판들의 번호다.)
>
> 이전 판 이후 들어온 변경(커밋 `3e25de8`, `7f8cd38`): **맵 5종 + 판마다 맵 무작위 선택**(GameSettingsSO.gameMapScenes, NetKeys.GameMapScene,
> Editor/Maps/MapSceneBuilder), **괴물 처형 연출**(CookieLifeStatePresenter 붙잡힘 단계·쿠키 가루, MonsterController GrabKill 진행도 값,
> PlayerPaintCanvas.SampleSurfaceColors), **처형 RPC 전원 전송 + 발신자 검증**, 가마솥 풍덩 연출(CauldronSplash), 문 상호작용 큐 정지 중 로컬 처리.
>
> **조사 방법**
> - `Assets/02. Scripts/` 아래 **10개 폴더, 79개 `.cs`, 6,349줄을 모두 읽었다.** (`Player.md`는 이 프로젝트와 무관한 다른 게임의 참고 코드라 제외.)
> - 빌드 씬 7개 + GameScene + PlayerTestScene, 네트워크·UI 프리팹의 YAML에서 **스크립트 GUID → 붙은 컴포넌트**를 셸 스크립트로 집계했다(R2.4).
> - `EditorBuildSettings.asset`, `GameSettings.asset`, `TagManager.asset`, 프리팹의 PhotonView·Rigidbody 직렬화 값을 직접 확인했다.
> - 이전 판의 모든 지적 항목을 현재 코드와 다시 대조했다.
> - 파일 경로는 따로 적지 않으면 `Assets/02. Scripts/` 기준이다. 예: `Unit/HideOrSeekPlayer.cs:115`.

> **후속 조치(2026-09-28, Bug-fix-plan.md §41)** — 다음 항목을 수정·검증했다: R4.1-1(채팅 이동 잠금 → `PlayerInput` 억제), R5-1(채팅 포커스), R4.7-3·R4.7-5(Unity null), R4.10-6(방장 쓰기 중복), R5-3(빈 팔레트), R4.4-7(GameScene 대체 씬), R4.3-9·R4.11-15·16(씬 복제 → `GameSceneCore.prefab`), R4.11-6(파괴 판정 중복), R5-2(`GamePhaseStarter` — 새 PlayerTestScene에서 원래 목적대로 동작). **R5-10은 조사 오류로 정정했다.**

---

## 목차

- **R1. 요약** — 결론, 11개 관점 판정표, 우선 조치 Top 10, 이전 판 대비 변화
- **R2. 프로젝트 구조** — 엔진·패키지·폴더·씬·프리팹·SO·배선표
- **R3. 동작 방식 상세** — 한 판의 흐름, 네트워크 계약, 도메인별 동작
- **R4. 11개 관점 감사** — 관점마다 판정, 근거(file:line), 영향, 권장
- **R5. 그 밖의 발견 사항**
- **R6. 권장 로드맵**
- **부록** — 교차 도메인 의존성, 전역(static) 상태, 전역 네트워크 설정을 바꾸는 곳, 마스터 전용 Update 폴링

---

## R1. 요약

### R1.1 결론

뼈대는 여전히 좋다. `RoomState`(조회)·`NetKeys`(키·수명 표)·`GamePhaseState`(단계 해석)·`CharacterRegistry`/`IGameCharacter`(캐릭터 계약)·
`PlayerInput`/`InputBindingsSO`(입력)·`GameSettingsSO`(수치)·`RoomSceneTransition`(씬 전환)이 각자 한 책임을 맡고,
**Core는 Photon·Unity 외에 어떤 도메인도 참조하지 않는다.** `Instance` 싱글톤은 없고, Room Props는 방장만·Player Props는 본인만 쓰는
**Photon 소유권 원칙**도 지켜진다. 이번 변경에서 처형 RPC에 **발신자 검증이 들어가** 이전 판의 R4.10-2가 해결됐다.

이번 판에서 새로 드러난 가장 큰 문제는 **맵 추가 방식**이다.

1. **게임 규칙·UI 매니저 세트가 씬 6개에 복제돼 있다(🆕 R4.3-9, R4.11-15).** `MapSceneBuilder`가 GameScene을 **처음 한 번만** 복사해 맵 씬을 만들고
   (`Editor/Maps/MapSceneBuilder.cs:90`), 이후 GameScene을 고쳐도 맵 씬에 반영되지 않는다. 컴포넌트 21종(ColorSlotPanel 포함)이 GameScene + 맵 5개에
   각자 따로 직렬화돼 있어, 인스펙터 값 하나를 바꾸려면 6번 고쳐야 한다.
2. **빌드에서 빠진 GameScene을 코드가 아직 기본값으로 쓴다(🆕 R4.4-7).** `SceneNames.Game = "GameScene"`이 맵 목록이 비었을 때·맵 키가 없을 때의
   대체 씬인데(`Lobby/GameStartAuthority.cs:62, 85`), GameScene은 빌드 목록에서 제거됐다. 지금 설정으로는 도달하지 않지만, 도달하면 빌드에서 씬 로드가 실패한다.

실제로 재현 가능한 결함은 다음과 같다(이전 판에서 이어진 것 포함).

| # | 결함 | 상태 | 절 |
|---|---|---|---|
| 1 | 쿠키 그랩에 수락 절차·발신자 확인이 없어 두 명이 같은 쿠키를 잡으면 캐리 상태가 꼬인다 | ⏸ | R4.10-1 |
| 2 | 채팅 중 이동 잠금이 쿠키에게만 걸린다(괴물은 채팅하며 이동·돌진) | ⏸ | R4.1-1 |
| 3 | 들고 있던 쿠키가 방을 나가면 드는 쪽 상체가 캐리 자세로 굳는다(Unity null 판정 오류로 정리 코드가 죽어 있음) | 🆕 | R4.7-5 |
| 4 | 승패 판정·강제 도포 배정이 서버 응답 전까지 매 프레임 다시 전송된다(강제 도포는 매번 다른 무작위 색) | 🆕 | R4.10-6 |
| 5 | 팔레트가 비면 강제 도포가 0으로 나누기 예외(현재 에셋 10색이라 잠재) | ⏸ | R5-3 |

### R1.2 11개 관점 판정표

| # | 관점 | 판정 | 한 줄 요약 | 이전 판 대비 |
|---|---|---|---|---|
| 1 | 기존 책임 분리를 무시하는 코드 | ⚠ 일부 있음 | 채팅이 전역 네트워크 설정·timeScale을 바꾸고, UI 패널이 방장 권한 처리를 맡는다. 처형 연출 튜닝값이 괴물 컨트롤러에 들어갔다 | 신규 1건 |
| 2 | Manager 간 의존성 과도 증가 | ⚠ 주의 | 매니저 간 직접 참조는 적지만, 처형 한 건에 **5개 클래스가 양방향으로** 얽혔다. 마스터 전용 폴링 8개의 암묵 결합은 그대로 | 악화 |
| 3 | Prefab과 Script 역할 뒤섞임 | ⚠ 일부 있음 | 자식 이름·트리거 이름·클립 키워드 의존은 그대로. **씬 6개에 매니저 세트를 복제**하는 방식이 새로 생겼다 | 악화 |
| 4 | Scene에 직접 의존하는 코드 증가 | ⚠ 주의 | `GameObject.Find` 4곳·`Camera.main` 10회는 그대로. **빌드에 없는 GameScene을 대체 씬으로 참조** | 신규 1건 |
| 5 | Singleton 남발 | ✅ 양호(정적 상태 13곳) | `Instance` 싱글톤 0개, 자동 생성 DDOL 1개. 정적 상태 1곳(`GameStartAuthority.lastMap`) 추가 | 거의 동일 |
| 6 | ScriptableObject 책임 오용 | ✅ 양호 | 5개 SO 모두 읽기 전용. 런타임 상태를 SO에 쓰는 곳 없음. 처형 연출 값은 SO로 옮길 후보 | 동일 |
| 7 | Unity Lifecycle 순서 문제 | ⚠ 일부 있음 | 과거 결함은 정리됨. **Unity null 판정 오류 1건(실제 결함)**, Update/LateUpdate 두 작성자 1건 신규 | 신규 2건 |
| 8 | Event 구독/해제 | ✅ 양호 | 정적 이벤트 1개 짝 맞음. `MonoBehaviourPunCallbacks` 26개 중 OnEnable/OnDisable 재정의 5개 모두 `base` 호출. 비동기 GPU 읽기 콜백도 안전 | 동일 |
| 9 | Object Pool ↔ Instantiate/Destroy 충돌 | ✅ 충돌 없음 | 풀이 없다(PUN `DefaultPool`). 새 가루 연출은 `stopAction=Destroy`로 자동 정리 | 동일 |
| 10 | Photon Ownership/RPC 구조 무시 | ⚠ 일부 있음 | ✅ 처형 RPC 발신자 검증 추가. ⏸ 그랩/놓기 RPC 검증 없음, 정적 문 캐시. 🆕 응답 대기 없는 방장 쓰기 | 1건 해결, 1건 신규 |
| 11 | 중복 로직 | ⚠ 다수 | 이전 14건 모두 유지 + **씬 단위 복제**(매니저 세트·ColorSlotPanel 7벌) | 악화 |

### R1.3 우선 조치 Top 10

| 순위 | 항목 | 근거 | 난이도 |
|---|---|---|---|
| 1 | 게임 매니저 세트를 프리팹 하나(`GameSystems.prefab`)로 묶고 맵 씬은 그 인스턴스만 두기. `MapSceneBuilder`가 GameScene 복사 대신 이 프리팹을 배치 | R4.3-9, R4.11-15 | 중 |
| 2 | 그랩 수락 절차 + `OnReleased`·`OnGrabbedByOwner` 발신자 검증 + 캐리 중인 쿠키 잡기 금지 | R4.10-1 | 중 |
| 3 | `PlayerGrabController`의 파괴 감지를 `ReferenceEquals`로 고쳐 캐리 자세 고착 해결 | R4.7-5 | 하 |
| 4 | 채팅 입력 억제를 `PlayerInput` 한 곳으로(괴물·관전·상호작용 공통) | R4.1-1 | 하 |
| 5 | `GameRuleController`·`PaintPhaseController`에 요청 플래그(또는 `expectedProperties` CAS) 추가 | R4.10-6 | 하 |
| 6 | 대체 씬 `SceneNames.Game` 정리: 빌드에 있는 첫 맵으로 바꾸거나, 맵 목록이 비면 시작을 막고 오류 로그 | R4.4-7 | 하 |
| 7 | 처형 연출 튜닝값(7개)을 `GrabKillPresentationSO`로 분리해 MonsterController ↔ CookieLifeStatePresenter 결합 끊기 | R4.1-8, R4.2 | 중 |
| 8 | `GameManager.Start`의 `Time.timeScale`·`IsMessageQueueRunning` 대입 제거, 시작 요청 수신을 UI 패널 밖으로 | R4.1-2, R4.1-3 | 하 |
| 9 | 스폰 3벌을 `SceneSpawnPoints.TryFindClearPosition`으로 통일, 프리팹 이름 상수 한 곳으로 | R4.11-1 | 하 |
| 10 | `InteractableDoor.masterPendingWrites`를 방 퇴장·씬 전환 때 비우기 | R4.10-3 | 하 |

### R1.4 이전 판 대비 변화

| 이전 판 항목 | 현재 상태 | 근거 |
|---|---|---|
| R4.10-2 처형 RPC 발신자 미검증 | ✅ 해결 | `Unit/HideOrSeekPlayer.cs:115-124` — 괴물 ViewID를 받아 그 PhotonView의 주인이 `info.Sender`인지 확인 |
| R4.9-2 가루 연출 누수 우려(`breakVfxPrefab` 비어 있음) | ✅ 해결 | 프리팹에 `CookieCrumbBurst` 연결, 파티클 `stopAction: 2`(Destroy) 확인 |
| R5-7 큐 정지 중 문 상호작용 차단이 사물 쪽에 있음 | ✅ 방향 변경 | 이제 큐가 멈추면 **로컬에서만 문을 여닫는다**(`Environment/InteractableDoor.cs:126`). 판이 끝나면 `DoorStates` 초기화로 다시 맞춰져 어긋남이 남지 않는다 |
| R4.10-1, R4.1-1~7, R4.3-1~8, R4.7-1~4, R4.11-1~14, R5-1~9 | ⏸ 유지 | 각 절에서 현재 줄 번호로 다시 적었다 |

---

## R2. 프로젝트 구조

### R2.1 엔진·패키지

- Unity **6000.0.58f2**, 레거시 Input Manager(`PlayerInput`이 유일한 창구), Photon **PUN 2**(Realtime·Chat 포함), 후처리는 Post Processing v2(`PostProcessing` 레이어 신설).
- 런타임 asmdef **`TagOfChaos.Scripts`** 하나(참조: PhotonUnityNetworking, PhotonRealtime, Unity.TextMeshPro, Unity.ugui).
  에디터 도구 `TagOfChaos.Editor`, 테스트 `TagOfChaos.EditorTests`(`Assets/Editor/Tests/RuleTests.cs`, **13개** — 맵 선택 테스트 추가).
- Enter Play Mode Options: 켜짐, 옵션값 0(도메인·씬 리로드 모두 수행) — 정적 상태가 플레이 세션 사이에 남지 않는다.
- **빌드 씬(현재)**: `PlayerTestScene`(비활성) · `LobbyScene` · `GameLobbyScene` · `Game_CandyForest` · `Game_GingerbreadVillage` · `Game_ChocolateFactory` ·
  `Game_CursedCandyCarnival` · `Game_HauntedBakery`. **`GameScene`은 빌드 목록에서 빠졌다**(맵 씬의 복제 원본으로만 남음).
- `GameSettings.gameMapScenes` = 맵 5개 모두(`Game_CandyForest, Game_GingerbreadVillage, Game_ChocolateFactory, Game_CursedCandyCarnival, Game_HauntedBakery`).
  > **정정(2026-09-28)**: 처음 판에는 "3개뿐, 2개는 선택되지 않는다"(R5-10)고 적었으나 **조사 오류**였다 — 에셋의 목록 앞 3줄만 읽었다. 실제로는 5개 모두 등록돼 있다.

### R2.2 폴더와 줄 수

| 폴더 | 파일 | 줄 | 역할 |
|---|---|---|---|
| Core | 19 | 869 | 공용 허브(조회·키·단계·입력·설정·씬 전환·레지스트리·스폰 위치·UI 헬퍼) |
| Unit | 10 | 870 | 쿠키 캐릭터(조정자 + 순수 C# 협력 객체 4개), 스킨 |
| Monster | 19 | 1,600 | 괴물 캐릭터 + **판 진행 전반**(괴물 선정·합류·승패·결과·관전·방장 정책·판 초기화) |
| ColorTag | 11 | 1,324 | 색칠(캔버스·붓·팔레트 UI·강제 도포) + 쿠키 생존·처형 연출 |
| GameManager | 6 | 376 | 채팅, 스폰, 퇴장, 낙하 복귀, 확인창 |
| Lobby | 6 | 553 | 로비(방 목록), 대기실 패널, 판 시작 권한·맵 선택, 스킨 선택 |
| Environment | 2 | 352 | 상호작용 문, 가마솥 풍덩 연출 |
| Interaction | 3 | 197 | 상호작용 키(E) 탐지·안내 UI·사물 계약 |
| Camera | 1 | 138 | 3인칭 궤도 카메라 |
| Dev | 2 | 70 | 오프라인 테스트 부트스트랩, 괴물 테스트 스포너 |
| **합계** | **79** | **6,349** | (이전 판 5,969줄 → +380줄: 처형 연출·맵 선택) |

### R2.3 에셋

- **SO**: `Resources/GameSettings`(정원 4, 괴물 1, 색칠 60초, 생존 600초, 맵 5개), `Resources/InputBindings`, `03. SO/ColorTag/`(팔레트 10색·붓 설정), `03. SO/Unit/`(스킨 목록).
- **네트워크 프리팹**(PUN `DefaultPool`이 `Resources`에서 로드): `04. Prefabs/Resources/HideOrSeekPlayer.prefab`, `MonsterPlayer.prefab`.
  - 둘 다 PhotonView `Synchronization: 3`(UnreliableOnChange), `OwnershipTransfer: 0`(Fixed).
  - 두 루트 Rigidbody 모두 프리팹 기본값 `isKinematic=0, useGravity=1`(원격은 `Start()`에서 키네마틱 전환 — R4.7-4). 쿠키 `Mesh_0`은 별도 키네마틱 Rigidbody.
  - 쿠키 `CookieLifeStatePresenter.breakVfxPrefab` = `04. Prefabs/Effects/CookieCrumb/CookieCrumbBurst.prefab`(`stopAction: Destroy`), `crumbCount: 12`.
  - 쿠키 `PlayerPaintCanvas.surfaceSampleUVs` — 에디터에서 미리 뽑은 몸 표면 샘플 UV(가루 색 계산용).
- **UI 프리팹**: `Resources/UI/Scene/{LobbyPanel, GameLobbyPanel, ColorSelectionPanel, InteractionPromptUI, PlayerListItem, PlayerResultRow, RoomListItem}`,
  `Resources/UI/Popup/ConfirmDialog`. 런타임 `Resources.Load`는 InteractionPromptUI 하나뿐(R4.3 끝 참고).
- **맵**: `Assets/Maps/{CandyForest, ChocolateFactory, CursedCandyCarnival, GingerbreadVillage, HauntedBakery, Common}` — FBX·조명 JSON·문 JSON·PostFX.
  `Editor/Maps/MapSceneBuilder.cs`(1,221줄)가 맵 씬을 조립한다.

### R2.4 씬 배선(스크립트 → 컴포넌트 수, YAML 집계)

| 씬 | 씬에 직접 직렬화된 스크립트 컴포넌트 |
|---|---|
| LobbyScene | (없음 — `LobbyPanel` 프리팹 인스턴스의 LobbyController, RoomListItem 프리팹) |
| GameLobbyScene | Camera_Ctrl, Cauldron, ConfirmDialog, GameManager, MasterClientPolicy, MonsterAssignmentAuthority, MonsterLobbyWaitController, MonsterRevealController, PlayerSkinSelector, PlayerSpawner(`skipConfirmedMonster=false`), RoomExitController, RoundStateResetter, VoidKillZone + GameLobbyPanel 프리팹(GameLobbyController) + 과자집 프리팹(InteractableDoor ×4, CauldronSplash) |
| GameScene (빌드 제외, 복제 원본) | BrushCursorController, Camera_Ctrl, ColorSelectionPanel, ColorSwatchButton ×10, ColorSwatchGroup, ConfirmDialog, GameManager, GamePhaseStarter, GameRuleController, MasterClientPolicy, MonsterDepartureBanner, MonsterJoinController, PaintPhaseController, PaintToolButton ×2, PhaseCountdownDisplay ×2, PlayerSpawner, ResultScreenController, RoomExitController, RoomLifecycleWatcher, SpectatorLabel, VoidKillZone |
| Game_CandyForest / Game_GingerbreadVillage | **GameScene과 같은 21종 세트** |
| Game_ChocolateFactory | 같은 세트 + InteractableDoor ×26 |
| Game_CursedCandyCarnival | 같은 세트 + InteractableDoor ×3 |
| Game_HauntedBakery | 같은 세트 + InteractableDoor ×22 |
| PlayerTestScene | BrushCursorController, Camera_Ctrl, MonsterTestSpawner, OfflineModeBootstrap, VoidKillZone + ColorSelectionPanel 프리팹 |
| (런타임 생성) | CharacterInteractor(DDOL, 모든 씬) + InteractionPromptUI(Resources) |

프리팹 `HideOrSeekPlayer`: HideOrSeekPlayer, PlayerGrabController, PlayerPaintCanvas, PlayerSkinApplier, CookieLifeStatePresenter, SpectatorController, FallGuard,
자식 Nameplate(PlayerBillBoard). 프리팹 `MonsterPlayer`: MonsterController, MonsterGrabKillTrigger, FallGuard.

맵 씬의 문 ID는 빌더가 문 JSON의 id로 채워 **씬 안에서 중복이 없다**(YAML 확인 — R4.4-5의 "이름 충돌" 위험은 맵 씬에서는 해소).

---

## R3. 동작 방식 상세

### R3.1 한 판의 흐름

```
LobbyScene
  LobbyController.Awake: AutomaticallySyncScene=true, GameVersion="1"
  Start: 미연결이면 SerializationRate=GameSettings.CharacterSyncRate → ConnectUsingSettings → JoinLobby → 방 목록
  방 만들기(MaxPlayers=GameSettings.MaxPlayers) / 무작위 입장 / 목록 입장(IsOpen=false면 버튼 비활성)
  OnJoinedRoom: 방을 만든 1인만 LoadLevel(GameLobby), 나머지는 AutomaticallySyncScene으로 따라옴
        │
GameLobbyScene (대기실)
  PlayerSpawner: InRoom이 될 때까지 코루틴 대기 → 쿠키 스폰(대기실에선 괴물도 쿠키)
  RoundStateResetter: 첫 프레임에 자기 Round Player Props 삭제 / 방장은 이전 판 흔적이 있으면 Round Room Props 삭제 + IsOpen·IsVisible=true
  MasterClientPolicy: 방장 = "괴물이 아닌 사람 중 최소 ActorNumber"로 수렴
  정원이 차면 MonsterAssignmentAuthority(방장)가 MonsterSelectDeadline = 지금 + 30초
    ├ 쿠키가 가마솥 트리거 진입 → Cauldron이 ClaimMonster 이벤트를 방장에게 → 선착순 확정
    └ 기한 경과 → 남은 자리 무작위 확정          (정원 미달이 되면 선정 초기화)
  MonsterRevealController: 배너 / MonsterLobbyWaitController: 괴물이면 AutomaticallySyncScene=false
  호스트(최소 ActorNumber, 괴물이어도 됨)가 시작 버튼 → GameStartAuthority
    ├ 내가 "의도된 방장"이면 바로 TryStart, 아니면 StartGameRequest 이벤트로 방장에게 요청(GameLobbyController.OnEvent가 수신)
    └ TryStart(방장): map = PickGameMap(직전 맵 제외 무작위)
                      Room Props { PaintPhaseEndTime = 지금+3초+60초, GameMapScene = map } 한 번에 기록
                      IsOpen=false → OpRemoveCompleteCache → LoadLevel(map)
  괴물: PaintPhaseEndTime 변경 수신 → 다른 쿠키 아바타 로컬 Destroy, KeepAliveInBackground 연장, 메시지 큐 정지,
        로컬 실시간 시계로 카운트다운 (그동안 문은 로컬로만 여닫힘)
        │
Game_<맵> (쿠키만 먼저 도착)
  PlayerSpawner: 쿠키 스폰(괴물은 건너뜀) / PaintPhaseController.Awake: IsPaintScene=true
  색칠 60초: PlayerPaintCanvas → 몸 콜라이더 레이캐스트 → 스탬프를 로컬 RT에 GL로 그리고, 묶어서 PaintStroke 이벤트(Others)
             같은 색 15스탬프 이상이면 슬롯 등록(최대 4) → RegisteredSlotCount를 자기 Player Props에 보고
             PaintColliderUpdater: 칠하는 중·커서가 몸 근처일 때만 포즈를 굽고, 물리 쿠킹은 Job 워커 스레드
  색칠 종료(방장, 같은 프레임에 각자):
    ├ PaintPhaseController: 슬롯 0개 쿠키에게 색 배정 → ForcedPaintActorNumbers/Colors → 대상 본인이 전신 강제 도포
    └ MonsterJoinController: MonsterJoined=1, GameEndTime = 지금 + 600초
  괴물: 카운트다운 종료 → 혼자 LoadLevel(GameStartAuthority.CurrentMapScene()) → PUN이 큐 재개 → 쌓인 이벤트 재생
        → MonsterJoined를 보고 MonsterPlayer 스폰 → curScn이 현재 씬과 같아지면 AutomaticallySyncScene=true, KeepAlive 복구
  생존 600초:
    괴물 근접(구 트리거 Enter/Stay) 또는 촉수 돌진 경로 SphereCast → MonsterGrabKillTrigger.TryGrabKill
      → MonsterController.PlayGrabKill(상태 동기화로 전원에 GrabKill 애니메이션)
      → RequestGrabKill RPC(RpcTarget.All, 괴물 ViewID)
         모든 클라이언트: 발신자 = 괴물 주인 확인 → CookieLifeStatePresenter.BeginGrabKill(충돌 끄기, Grab_Socket 추종, 가루 색 비동기 샘플)
         피해자 본인만: HitCount=2 기록, 그랩 해제, 키네마틱, Held 애니메이션
      → 각 클라이언트가 자기 화면의 괴물 애니메이션 진행도로 눌림→가루→축소 연출, 부서지는 순간 숨김, 본인은 관전 모드
  GameRuleController(방장, 매 프레임): 쿠키 전원 파괴 → MonsterWins / GameEndTime 경과 → CookiesWin → GameResult 기록
  괴물 전원 이탈: RoomLifecycleWatcher → MonsterDepartedAt → 5초 배너 → 대기실 복귀
  ResultScreenController: (괴물 승이면 처형 연출이 끝날 때까지 최대 5초 대기) 결과 표시 → 12초 뒤 방장이 LoadLevelForRoom(GameLobby)
        │
GameLobbyScene 복귀 → RoundStateResetter가 이전 판 흔적 삭제(GameMapScene 포함) → 다음 판(직전 맵은 방장 로컬 static으로 기억)
```

### R3.2 네트워크 계약

**Room Props**(모두 방장만 쓴다. `NetKeys.Scopes`에 대상·수명 선언, Round 키는 대기실 복귀 때 삭제)

| 키 | 타입 | 쓰는 곳 | 읽는 곳 |
|---|---|---|---|
| MonsterActorNumbers | int[] | MonsterAssignmentAuthority, RoomLifecycleWatcher | RoomState 전반, 배너, 스폰, 방장 정책, 결과 |
| MonsterRevealTime | double | MonsterAssignmentAuthority | (쓰기만 함 — R5-5) |
| MonsterSelectDeadline | double | MonsterAssignmentAuthority | 대기실 상태 문구 |
| PaintPhaseEndTime | double | GameStartAuthority, (예비) GamePhaseStarter | GamePhaseState, 괴물 대기, 강제 도포, 합류 |
| **GameMapScene** 🆕 | string | GameStartAuthority | 괴물 대기(MonsterLobbyWaitController) |
| MonsterJoined | int | MonsterJoinController | GamePhaseState, 괴물 스폰, 판 흔적 판정 |
| ForcedPaintActorNumbers / ForcedPaintColors | int[] | PaintPhaseController | PlayerPaintCanvas(본인) |
| GameEndTime | double | MonsterJoinController | 승패 판정, 생존 카운트다운 |
| MonsterDepartedAt | double | RoomLifecycleWatcher | 이탈 배너, 방장 교체 시 인계 |
| GameResult | int | GameRuleController | 결과 화면, 단계 판정 |
| DoorStates | Hashtable{문ID:byte} | InteractableDoor(방장) | InteractableDoor(전원) |

**Player Props**(본인만 쓴다): `HitCount`(Round), `RegisteredSlotCount`(Round), `SkinIndex`(Session).

**RaiseEvent**: 1 PaintStroke(Others, 캐시 없음), 2 ClaimMonster(→방장), 3 ClearColor(Others), 5 StartGameRequest(→방장), 6 DoorStateRequest(→방장). 4는 예약 비움.

**RPC**

| RPC | 대상 | 발신자 검증 |
|---|---|---|
| `HideOrSeekPlayer.OnGrabbedByOwner(int carrierViewId)` | 들릴 쿠키 주인 | ❌ 없음 |
| `HideOrSeekPlayer.OnReleased(bool)` | 들린 쿠키 주인 | ❌ 없음 |
| `HideOrSeekPlayer.RequestGrabKill(int monsterViewId, info)` | **전원**(🆕 이전: 주인만) | ✅ 괴물 PhotonView 주인 == 발신자 |
| `GameManager.LogMsg(string, bool, info)` | 전원(씬 PhotonView, GameManager·RoomExitController 공용) | 채팅이라 불필요 |

**PhotonView 직렬화**: 공용 `NetworkTransformSync<TState>`(위치·회전·상태 int). 쿠키는 캐리 여부 bool을 더 붙인다(`PlayerNetworkSync`).
원격 복사본은 키네마틱 + 보간(10m 이상이면 스냅). 괴물 GrabKill 진행도는 **동기화하지 않고** 각 클라이언트 Animator에서 읽는다.

### R3.3 도메인별 동작 요약

**Core** — 모두 정적 클래스 또는 순수 데이터. `GamePhaseState.Evaluate`·`RoomState.DesiredMasterActor`·`GameSettingsSO.PickGameMap` 등은 네트워크와 분리된 순수 함수라 테스트된다.
`RoomSceneTransition`은 `OpRemoveCompleteCache` → `LoadLevel` 순서로 이전 씬 Instantiate 캐시를 지운다.

**Unit(쿠키)** — `HideOrSeekPlayer`가 조정자(입력 Update / 물리 FixedUpdate). 협력 객체: `PlayerGroundDetector`, `PlayerAnimationDriver`(트리거 전환·점프 정점 정지·Carry 레이어),
`PlayerCarryFollower`(들린 쪽이 캐리어 소켓을 로컬로 따라감 — 소유권 이전 없음), `PlayerNetworkSync`. 이동 잠금 = 외부 잠금 ∨ 파괴 ∨ 들림.
처형 RPC 수신 시 **Monster 도메인의 `MonsterController`를 직접 조회**한다(🆕 R4.1-8).

**Monster** — `MonsterController`(319줄)는 입력·물리·애니메이션·촉수 돌진·돌진 경로 처형 판정·리스폰에 더해 **처형 연출 진행도 값 7개**까지 맡는다.
`MonsterGrabKillTrigger`는 처형 판정 단일 진입점. 나머지 9개 클래스는 괴물이 아니라 판 진행·관전 담당이다.

**ColorTag** — `PlayerPaintCanvas`(616줄)가 512² RenderTexture에 GL 쿼드로 스탬프를 그린다(잠금은 하드웨어 블렌딩). 새로 `SampleSurfaceColors`가
캔버스·스킨을 64²로 줄여 `AsyncGPUReadback`으로 읽고 샘플 UV마다 표면 색을 만든다. `CookieLifeStatePresenter`(274줄)는 생존/파괴 표시(0.5초 주기 재조정 + 콜백)와
**처형 연출(붙잡힘 → 눌림 → 가루 → 축소)**을 함께 맡는다.

**Interaction / Environment** — `CharacterInteractor`(DDOL)가 0.1초마다 로컬 캐릭터 주변 가장 가까운 `IInteractable`을 찾는다. `InteractableDoor`는 방장 요청 → `DoorStates` 방식,
큐가 멈춘 클라이언트는 로컬 적용. `CauldronSplash`는 트리거로 Animator Splash 레이어를 잠깐 켜는 연출 전용(네트워크 없음).

---

## R4. 11개 관점 감사

### R4.1 기존 책임 분리를 무시하는 코드 — ⚠ 일부 있음

**R4.1-1 ⏸ 채팅 이동 잠금이 쿠키 타입에 묶여 있다(실제 결함).**
`GameManager/GameManager.cs:130`이 `CharacterRegistry.FindLocal<HideOrSeekPlayer>()`로 쿠키만 찾아 잠근다. 괴물은 채팅을 치는 동안
레거시 `Input`(포커스 무관)을 그대로 읽어 WASD 이동·Shift 돌진을 한다(`Monster/MonsterController.cs` Update). 관전 중인 쿠키는 채팅에 Space를 치면
관전 대상이 바뀐다(`Monster/SpectatorController.cs:59`). `CharacterInteractor.IsTypingInUi`가 상호작용만 따로 막고 있다.
- 권장: `PlayerInput`에 `IsSuppressed` 플래그를 두고 모든 프로퍼티가 그것을 먼저 확인하게 한다. 채팅은 그 플래그만 켠다.

**R4.1-2 ⏸ 채팅 매니저가 전역 설정을 바꾼다.** `GameManager.cs:27-28` — `Time.timeScale = 1`, `IsMessageQueueRunning = true`. 같은 대입이
`PlayerSpawner.cs:65`, `RoomExitController.cs:53`에도 있다(부록 C). 큐 정지 설계를 바꿀 때 어느 대입이 의도인지 알기 어렵다.

**R4.1-3 ⏸ UI 패널이 네트워크 권한 처리를 맡는다.** `Lobby/GameLobbyController.cs:13, 98-104` — 대기실 **UI 프리팹**이 `IOnEventCallback`으로
`StartGameRequest`를 받아 `GameStartAuthority.TryStart`를 실행한다. `LobbyController.cs:27-32, 45`는 Photon 초기화(`AutomaticallySyncScene`·`GameVersion`·`SerializationRate`)를 한다.

**R4.1-4 ⏸ 캐릭터 조정자가 다른 도메인을 직접 호출한다.** `HideOrSeekPlayer.cs:149`(관전 진입), `:170`·`MonsterController.cs` Awake(`Camera.main`의 `Camera_Ctrl`에 자신을 넘김),
`PlayerSpawner.cs:48`(운영 코드 → **Dev** `OfflineModeBootstrap.SpawnAsMonster`), `PlayerCarryFollower`(→ `PlayerGrabController.SetCollisionIgnored`).
관전 진입은 이제 **두 곳**에서 한다: `HideOrSeekPlayer.cs:149`(연출이 없을 때)와 `ColorTag/CookieLifeStatePresenter.cs:176`(연출이 끝날 때).

**R4.1-5 ⏸ 퇴장 컨트롤러가 채팅 RPC를 빌려 쓴다.** `RoomExitController`가 GameManager의 PhotonView와 `GameManager.RpcLogMsg`로 보낸다(같은 GameObject 배치 제약).

**R4.1-6 ⏸ 폴더와 실제 책임이 어긋난다.** `Monster/` 19개 중 9개(GameRuleController, ResultScreenController, PlayerResultRow, RoundStateResetter, MasterClientPolicy,
RoomLifecycleWatcher, GamePhaseStarter, SpectatorController, SpectatorLabel)는 판 진행·관전이다. `ColorTag/CookieLifeStatePresenter`는 이제 색칠과 무관한
**처형 연출**까지 맡아 더 어긋났다. `GameManager/`의 FallGuard·VoidKillZone·ConfirmDialog도 채팅과 무관하다.
- 권장: `Round/`, `Spectator/`, `World/`, `Cookie/` 폴더로 재배치(파일 이동은 `.meta` GUID를 유지하므로 씬 참조가 깨지지 않는다).

**R4.1-7 ⏸ MonsterController가 쿠키보다 책임이 많다.** 282줄 → **319줄**. 쿠키는 협력 객체 4개로 나눈 책임을 한 파일이 맡는다.

**R4.1-8 🆕 처형 연출 데이터가 괴물 컨트롤러에 있고, 쿠키 표시 컴포넌트가 그것을 직접 읽는다.**
- `Monster/MonsterController.cs:27-37, 57-77`: 블렌더 프레임에서 뽑은 **쿠키 분쇄 연출 진행도**(눌림 시작·끝, 축소 시작, 가루, 소멸) 5개와 `GrabSocket`,
  `GrabKillDuration`, `GrabKillProgress`를 공개한다. 이 값들은 괴물의 이동·판정과 무관한 **쿠키 연출 데이터**다.
- `ColorTag/CookieLifeStatePresenter.cs:77-178`이 `MonsterController` 타입을 필드로 들고 위 값을 매 프레임 읽는다.
- `Unit/HideOrSeekPlayer.cs:115-124`도 `MonsterController`를 `GetComponent`로 찾는다.
- 영향: 괴물 종류가 늘거나(다른 처형 모션) 쿠키 외 대상이 생기면 세 클래스를 함께 고쳐야 한다. 또 튜닝값이 괴물 **프리팹**에 직렬화되므로
  연출 담당자가 쿠키 연출을 바꾸려면 괴물 프리팹을 열어야 한다.
- 권장: `GrabKillPresentationSO`(진행도 5개 + 크기 배율)로 옮기고, 괴물 쪽은 `IGrabKillSource { Transform GrabSocket; float Progress; float Duration; GrabKillPresentationSO Presentation; }`
  계약만 노출한다. 쿠키 쪽은 이 인터페이스만 안다.

### R4.2 Manager 간 의존성 과도 증가 — ⚠ 주의(악화)

씬 매니저끼리의 직접 참조는 여전히 적다.

| 참조 | 방식 |
|---|---|
| Cauldron → MonsterRevealController | 인스펙터 참조 |
| MonsterLobbyWaitController → GameManager, Back 버튼 | 인스펙터 `Behaviour[]`, `Button[]` |
| RoomExitController → GameManager | RPC 이름 상수 + 같은 PhotonView |
| GameLobbyController → GameStartAuthority | 정적 호출 |
| MonsterLobbyWaitController → GameStartAuthority 🆕 | 정적 호출(`CurrentMapScene`) — 괴물 대기가 Lobby 도메인의 시작 권한 클래스를 안다 |

하지만 **처형 한 건을 처리하는 캐릭터 컴포넌트들의 결합이 크게 늘었다.**

```
MonsterController ──PlayGrabKill()──▶ (자기)          MonsterGrabKillTrigger ──PlayGrabKill()──▶ MonsterController
MonsterController ──ResetTrigger()/TryGrabKill()──▶ MonsterGrabKillTrigger        (양방향)
MonsterGrabKillTrigger ──RPC──▶ HideOrSeekPlayer.RequestGrabKill ──GetComponent──▶ MonsterController
HideOrSeekPlayer ──BeginGrabKill()──▶ CookieLifeStatePresenter ──읽기(진행도 7개)──▶ MonsterController
CookieLifeStatePresenter ──SampleSurfaceColors()──▶ PlayerPaintCanvas
CookieLifeStatePresenter / HideOrSeekPlayer ──EnterSpectatorMode()──▶ SpectatorController
ResultScreenController ──GetComponent──▶ CookieLifeStatePresenter.IsBeingGrabKilled
```

- `MonsterController` ↔ `MonsterGrabKillTrigger`는 **양방향**(트리거가 컨트롤러의 `PlayGrabKill`, 컨트롤러가 트리거의 `ResetTrigger`·`TryGrabKill`·`IsOnCooldown`·`ReachCenter`).
  쿨다운 소유자가 둘로 나뉘어 있다(트리거의 `onCooldown` 플래그를 컨트롤러의 타이머가 푼다).
- 결과 화면(판 진행)이 쿠키 프리팹의 연출 컴포넌트 상태를 폴링한다(`Monster/ResultScreenController.cs` `IsAnyCookieBeingGrabKilled`).
- 권장: 처형을 `GrabKillSession`(순수 C#) 하나로 모델링해 괴물 측 쿨다운·타이머를 한곳에 두고, 쿠키·결과 화면은 `CharacterRegistry`와 정적 이벤트
  (`GrabKillStarted/Finished`)로만 관찰하게 한다.

**암묵 결합(그대로)**: 마스터 전용 Room Props 폴링 Update가 **8개**다(부록 D). `PaintPhaseEndTime` 경과 순간 `PaintPhaseController`(강제 도포)와
`MonsterJoinController`(합류)가 같은 프레임에 각자 쓴다. `GamePhaseState`가 **해석**은 모았지만 **전이**는 흩어져 있다.
- 권장(규모가 커질 때): 마스터 전용 `RoundDirector` 하나가 단계 전이를 순서대로 수행.

### R4.3 Prefab과 Script의 역할 뒤섞임 — ⚠ 일부 있음(악화)

| # | 내용 | 근거 | 상태 |
|---|---|---|---|
| 1 | 코드가 프리팹 자식 이름을 안다 | `HideOrSeekPlayer.cs:208` `"Mesh_0"` | ⏸ |
| 2 | 애니메이터 트리거·상태 이름 = enum `ToString()` / 문자열 | `PlayerAnimationDriver`, `MonsterController.ChangeState`, `"Jump"`, `"Carry"`, `CauldronSplash`의 `"Splash"` 상수 3개 | ⏸(+1) |
| 3 | 클립 길이를 이름 키워드로 찾음 | `MonsterController.cs:149`(`GrabKill`·`TentacleDash`), `InteractableDoor`(`Close`) | ⏸ |
| 4 | 같은 렌더러를 두 컴포넌트에 따로 배선 | `PlayerSkinApplier.bodyRenderer` ↔ `PlayerPaintCanvas.bodyRenderer` | ⏸ |
| 5 | 같은 UI의 프리팹판과 씬판이 따로 존재 | `Resources/UI/Scene/ColorSelectionPanel.prefab`(PlayerTestScene만 사용) ↔ **씬 6개의 비프리팹 ColorSlotPanel** | ⏸ 악화(2벌 → 7벌) |
| 6 | 빌더가 만든 프리팹 직렬화가 코드보다 오래됨 | 과자집 프리팹의 옛 필드 `zoneWidth` | ⏸ |
| 7 | 런타임에 UI를 코드로 생성 | `CharacterInteractor` + `InteractionPromptUI.Create` | ⏸ |
| 8 | 스폰 겹침 검사 크기가 쿠키 캡슐 상수 | `Core/SpawnPositionFinder.cs:10-12` | ⏸ |
| **9** | **게임 매니저 세트를 씬마다 복제** | 아래 설명 | 🆕 |
| **10** | **괴물 프리팹에 쿠키 연출 튜닝값 직렬화** | R4.1-8 | 🆕 |

**R4.3-9 🆕 게임 매니저 세트가 씬 6개에 복제돼 있다.**
`Editor/Maps/MapSceneBuilder.cs:20, 90`: `TemplateScene = "Assets/Scenes/GameScene.unity"`를 **맵 씬 파일이 없을 때만** `AssetDatabase.CopyAsset`으로 복제한다.
다시 실행하면 "맵 부분만 새로 만든다"(`:13-15`). 그 결과:
- GameScene + 맵 5개에 GameManager·GameRuleController·MonsterJoinController·PaintPhaseController·ResultScreenController·ColorSlotPanel(스와치 10개) 등
  **21종 컴포넌트가 각각 독립적으로 직렬화**돼 있다(R2.4). 이들은 프리팹 인스턴스가 아니다.
- GameScene의 매니저·UI를 고쳐도(예: 결과 화면 문구, `autoReturnDelay`, PhaseCountdownDisplay 포맷, 새 매니저 추가) **맵 씬에는 반영되지 않는다**.
  GameScene은 빌드에서도 빠져 있어 수정 결과를 게임에서 확인할 수도 없다.
- 새 매니저를 넣으려면 씬 6개를 모두 열어 같은 작업을 반복해야 하고, 한 곳을 빠뜨리면 **특정 맵에서만** 재현되는 버그가 된다.
- 권장: 씬 공통 부분을 `GameSystems.prefab`(매니저) + `GameHUD.prefab`(ColorSlotPanel·카운트다운·결과·배너·관전 라벨)으로 묶는다.
  맵 씬은 이 두 프리팹 인스턴스 + 맵 환경 + 스폰 지점만 갖는다. `MapSceneBuilder`는 GameScene 복사 대신 빈 씬에 두 프리팹을 배치한다.
  (대안: 공용 부분을 Additive 씬 `GameCore`로 두고 맵 씬을 추가 로드 — PUN `LoadLevel`이 Single 전용이라 전환 로직이 커지므로 프리팹 방식이 싸다.)

`UI 프리팹`을 `Resources/`에 두는 폴더 규칙(CLAUDE.md)을 따르지만 런타임 로드는 InteractionPromptUI뿐이라 나머지 7개는 빌드에 무조건 포함될 이유가 없다(⏸).

### R4.4 Scene에 직접 의존하는 코드 — ⚠ 주의

| # | 의존 | 위치 | 상태 |
|---|---|---|---|
| 1 | `GameObject.Find(이름)` | `Core/SceneSpawnPoints.cs:13`, `GameManager/PlayerSpawner.cs:56`, `Monster/MonsterJoinController.cs:72`, `Dev/MonsterTestSpawner.cs:24` — 모두 스폰 지점 | ⏸ |
| 2 | `Camera.main` + `Camera_Ctrl` 전제 | HideOrSeekPlayer(2), MonsterController(2), SpectatorController(1), PlayerPaintCanvas(2), BrushCursorController(1), PlayerBillBoard(2) — 10회 | ⏸ |
| 3 | 컴포넌트 존재로 씬 판별 | `PaintPhaseController.IsPaintScene`(`:23, 28`) — 이제 맵 씬 5개가 모두 이 컴포넌트를 가진다 | ⏸ |
| 4 | PUN 내부 키 문자열 | `MonsterJoinController.cs:18` `"curScn"` | ⏸ |
| 5 | 문 ID 기본값 = GameObject 이름 | `InteractableDoor.DoorId` | ✅ 맵 씬은 빌더가 고유 ID를 채움(중복 0) |
| 6 | 씬 이름 상수 | `SceneNames` | ⏸ 한 곳에 모여 있음 |
| **7** | **빌드에 없는 씬을 대체값으로 사용** | 아래 설명 | 🆕 |

**R4.4-7 🆕 `SceneNames.Game`("GameScene")이 빌드에서 빠졌는데 대체 씬으로 남아 있다.**
- `Lobby/GameStartAuthority.cs:62`: `settings.PickGameMap(lastMap, SceneNames.Game)` — 맵 목록이 비면 "GameScene"을 로드한다.
- `Lobby/GameStartAuthority.cs:85`: `CurrentMapScene()`이 `GameMapScene` 키가 없으면 "GameScene"을 돌려준다. 대기실 괴물은 이 값으로 `LoadLevel`한다.
- `EditorBuildSettings.asset`에서 GameScene이 제거됐다(커밋 `7f8cd38`). **에디터에서는** 빌드 목록에 없는 씬도 경고와 함께 로드될 수 있어 테스트에서 드러나지 않고,
  **빌드에서만** 로드 실패로 나타난다.
- 현재 `gameMapScenes`가 5개이고 두 키를 한 요청으로 기록하므로 정상 흐름에서는 도달하지 않는다. 하지만 설정 에셋을 비우거나, 방장 교체 경합으로
  `GameMapScene`만 늦게 도착하는 경우의 안전망이 **실패하는 안전망**이다.
- 권장: 대체값을 "빌드 목록의 첫 맵"으로 바꾸거나, `GameSettingsSO.OnValidate`에서 목록이 비면 오류, 그리고 EditMode 테스트로
  "`gameMapScenes`의 모든 이름이 빌드 씬에 있다"를 검사한다(`RuleTests`에 1개 추가).

새 상호작용·처형 코드는 레지스트리(`CharacterRegistry`, `InteractableRegistry`)를 써서 씬 탐색을 늘리지 않았다.

### R4.5 Singleton 남발 — ✅ 양호(정적 상태 13곳)

`public static X Instance` 형태는 **없다**. 정적 상태 13곳(부록 B). 주의할 것:

- **숨은 DDOL 싱글톤**(⏸): `Interaction/CharacterInteractor.cs:21` — `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`로 모든 씬에 자동 생성·유지.
- **방을 옮겨도 남는 정적 상태**: `InteractableDoor.masterPendingWrites`(⏸ R4.10-3), `GameStartAuthority.lastStartTime`(무해),
  **`GameStartAuthority.lastMap`**(🆕 `Lobby/GameStartAuthority.cs:77`).
  - `lastMap`은 **판을 시작한 방장 클라이언트에만** 있다. 방장이 바뀌면 새 방장은 직전 맵을 모르므로 같은 맵이 연속될 수 있고,
    방을 나가 다른 방을 만들면 이전 방의 마지막 맵이 새 방 첫 판에서 제외된다. 영향은 작다.
  - 권장: 직전 맵을 `Session` 수명 Room Prop(`LastGameMap`)으로 두면 방장 교체에도 이어지고 방을 옮기면 자연히 사라진다(`NetKeys.Scopes`에 한 줄).

### R4.6 ScriptableObject의 책임 — ✅ 양호

| SO | 역할 | 평가 |
|---|---|---|
| GameSettingsSO | 규칙 수치 + 순수 계산 3개(`PaintStrokeSendIntervalFor`, `MonsterCountFor`, 🆕`PickGameMap`) | 적절. `PickGameMap`은 `UnityEngine.Random`을 써서 결정적이지 않지만 테스트는 "직전 맵 제외"만 검사하므로 무방. 맵 이름의 **빌드 포함 여부 검증이 없다**(R4.4-7) |
| InputBindingsSO | 키 설정 | 읽기 전용. 런타임 키 변경 시 복제본 필요(주석에 언급) |
| ColorPaletteSO | 색 목록 | ⏸ `Count`가 `colors` null이면 NRE(`ColorPaletteSO.cs:8`). 주석 "10개 고정"은 사실이 아님 |
| BrushSettingsSO | 붓 크기·커서 | ⏸ `CreateAssetMenu` 경로가 `ColorTag/…`로 다른 SO(`TagOfChaos/…`)와 다름 |
| SkinCatalogSO | 스킨 목록 | `ClampIndex`로 네트워크 인덱스 방어 |

런타임 상태를 SO에 쓰는 곳은 **없다.** 새로 SO로 옮길 후보: 처형 연출 진행도(R4.1-8), `ResultScreenController.autoReturnDelay`·`MaxGrabKillWait`,
`CookieLifeStatePresenter`의 `SquashHeightScale`/`SquashWidthScale` 상수 — 지금은 씬 6개·프리팹·코드 상수에 흩어져 있다.

### R4.7 Unity Lifecycle 순서 — ⚠ 일부 있음

이미 해결된 것(현재 코드로 재확인): 협력 객체·캔버스·스탬프 머티리얼을 `Awake`에서 생성, 스킨(`Awake`) → 합성 머티리얼(`Start`) 순서,
씬 로드 직후 `InRoom=false` 대응(코루틴 3곳·첫 프레임 Update 2곳·`OnJoinedRoom` 보강), CanvasGroup 숨김 통일, PUN `DefaultPool`의 비활성 복제 후 Owner 설정.
처형 연출은 `LateUpdate`에서 소켓을 따라가 Animator가 본을 움직인 **뒤**에 위치를 맞춘다 — 올바른 선택이다.

**R4.7-1 ⏸ `PaintPhaseController.IsPaintScene` 정적 bool.** `Awake` true / `OnDestroy` false(`:23, 28`). 비동기 씬 로드에서 새 씬 `Awake`와
이전 씬 `OnDestroy`의 순서에 기대는 구조다. 지금은 맵 → 맵 직행 전환이 없어(항상 대기실 경유) 드러나지 않지만, "다음 판 바로 시작"을 만들면 새 맵에서 false로 덮일 수 있다.
- 권장: 카운터 또는 "현재 인스턴스" 참조.

**R4.7-2 ⏸ `ConfirmDialog`가 `Awake`에서 자기 자신을 끈다**(`GameManager/ConfirmDialog.cs:19`). 비활성으로 배치하면 첫 `Show()`가 곧바로 다시 꺼진다. 씬 6개 모두 활성 배치라 지금은 동작.

**R4.7-3 ⏸ UnityEngine.Object에 `?.`** — `PlayerGrabController.cs:58, 77` `self?.SetCarryLayerWeight`.

**R4.7-4 ⏸ 원격 복사본의 첫 프레임 물리.** 두 캐릭터 루트 Rigidbody 기본값이 비키네마틱이고 원격은 `Start()`에서야 키네마틱이 된다.

**R4.7-5 🆕 들고 있던 쿠키가 사라지면 캐리 정리가 실행되지 않는다(실제 결함 — Unity null 판정).**
`Unit/PlayerGrabController.cs:32`

```csharp
if (carriedPlayer != null && (!carriedPlayer || IsOwnerBroken(carriedPlayer)))
    Release();
```

`carriedPlayer`는 `HideOrSeekPlayer`(UnityEngine.Object)라 `!= null`이 Unity의 오버로드된 비교다. 오브젝트가 **파괴되면 `carriedPlayer != null`이 false**가 되어
뒤의 `!carriedPlayer`(파괴 감지) 분기에 **절대 도달하지 않는다**. `Release()`도 첫 줄 `if (carriedPlayer == null) return;`(`:66`)에서 같은 이유로 빠져나간다.
- 재현: A가 B를 든다 → B가 방을 나간다(B의 쿠키 오브젝트 파괴) → A는 `Release()`가 한 번도 불리지 않아 **로컬 Animator의 Carry 레이어 가중치가 1로 남는다**
  (상체가 계속 무언가를 든 자세). `IsCarrying`은 false로 바뀌므로 원격 화면에서는 정상 자세라, 본인만 보는 어긋남이다. 다음에 F로 다른 쿠키를 들고 놓아야 풀린다.
- 코드 주석("방을 나가 사라졌으면 캐리 상태를 정리한다", "방을 나가 파괴된 경우에는 RPC/충돌 복구를 건너뛴다")이 의도한 경로가 죽어 있다.
- 권장: 참조 존재 여부는 `!ReferenceEquals(carriedPlayer, null)`, 살아 있는지는 `carriedPlayer`(Unity bool)로 구분한다. 같은 패턴이 `CharacterRegistry.IsAlive`에 이미 있다.

**R4.7-6 🆕 처형 중인 원격 쿠키의 Transform을 두 컴포넌트가 매 프레임 쓴다.**
원격 클라이언트에서 처형 중인 쿠키는 `HideOrSeekPlayer.Update`가 `networkSync.Interpolate(transform)`로 위치·회전을 보간하고(`Unit/HideOrSeekPlayer.cs:236` — 원격의 `IsMovementLocked`는
로컬 `hitCount`가 0이라 false), 같은 프레임 `CookieLifeStatePresenter.LateUpdate`가 소켓 위치·크기로 **덮어쓴다**(`ColorTag/CookieLifeStatePresenter.cs:99, 133`).
Update → LateUpdate 순서 덕분에 최종 결과는 맞지만, 보간이 10m 스냅 판정을 하고 회전 Slerp를 매 프레임 버리는 낭비이며, 누가 Transform의 주인인지가 실행 순서에만 달려 있다.
- 권장: `IsMovementLocked`(또는 별도 `IsPresentationDriven`)에 `lifePresenter.IsBeingGrabKilled`를 포함해 원격 보간을 멈춘다.

### R4.8 Event 구독/해제 — ✅ 양호

| 이벤트 | 구독 | 해제 | 평가 |
|---|---|---|---|
| `SpectatorController.SpectateTargetChanged`(static) | `SpectatorLabel.OnEnable` | `OnDisable` | 짝 맞음. 발행자 파괴 시 null 통지(`SpectatorController.OnDestroy`) |
| Photon 콜백·`IOnEventCallback` | `MonoBehaviourPunCallbacks.OnEnable` | `OnDisable` | 상속 26개 중 OnEnable/OnDisable 재정의 5개(HideOrSeekPlayer, MonsterController, InteractableDoor, BrushCursorController, PlayerPaintCanvas) 모두 `base` 호출 |
| `Button.onClick.AddListener` | 각 `Awake`/`Start` | 없음 | 버튼과 수명이 같아 누수 없음 |
| 레지스트리(Character/Interactable) | `OnEnable` | `OnDisable` | 짝 맞음. 순회 중 Destroy 시 먼저 복사 |
| 🆕 `AsyncGPUReadback.Request` 콜백 | `PlayerPaintCanvas.SampleSurfaceColors`(`:380, 386`) | 1회성 | 콜백 안에서 임시 RT를 `ReleaseTemporary`. 쿠키가 먼저 파괴돼도 관리 객체 필드에만 쓰므로 예외 없음 |

주의(⏸): `MonsterLobbyWaitController`가 대기 중 `GameManager.enabled = false`로 끄면 GameManager의 Photon 콜백 등록도 풀린다(지금은 재정의한 콜백이 없어 무해).

주의(🆕): 문 26개(ChocolateFactory)·22개(HauntedBakery)가 **각자** `MonoBehaviourPunCallbacks`라 `DoorStates` 변경·`DoorStateRequest`·방장 교체마다 26번 호출되고,
그중 하나만 자기 ID를 처리한다. 지금 규모에선 비용이 작지만 사물이 늘면 `DoorStateRouter` 하나가 받아 ID로 분배하는 편이 낫다(정적 `masterPendingWrites`도 그 인스턴스 필드로 — R4.10-3과 함께 해결).

### R4.9 Object Pool과 Instantiate/Destroy 충돌 — ✅ 충돌 없음

`IPunPrefabPool`·커스텀 풀은 **없다**(검색 결과 0). 네트워크 객체는 PUN `DefaultPool`(Resources + Instantiate/Destroy)이고 씬 전환으로 파괴된다.
"풀에서 꺼낸 객체를 Destroy" 같은 충돌은 구조적으로 없다.

| 생성/파괴 지점 | 빈도 | 평가 |
|---|---|---|
| `PhotonNetwork.Instantiate` — PlayerSpawner, MonsterJoinController, MonsterTestSpawner | 씬당 1회 | 적절 |
| 🆕 `CookieLifeStatePresenter.cs:153` 가루 `Instantiate(breakVfxPrefab)` | 처형 1회당 1개 | 파티클 `stopAction: Destroy`로 자동 정리. 한 판 최대 (쿠키 수)회라 풀 불필요 |
| `BrushCursorController` 커서 1개 | 씬당 1회 | `OnDestroy`에서 정리 |
| `MonsterLobbyWaitController` 다른 쿠키 로컬 `Destroy` | 판당 1회 | ⏸ R4.9-1 — 원 주인이 이미 파괴한 네트워크 객체를 로컬로 치움(의도). 커스텀 풀 도입 시 함께 바꿔야 함 |
| `GameLobbyController.cs:117-121` 참가자 목록 전부 파괴 후 재생성 | 인원·호스트 변경마다 | ⏸ R4.9-2 — 같은 프레임 두 번 호출 시 한 프레임 동안 행이 두 배. `UiListBuilder.Sync` 재사용 권장. `Update`의 `RoomState.HostActor()`가 **매 프레임 int 배열 할당** |
| `ResultScreenController` 결과 행·아이콘 | 판당 1회 | `isShown` 가드로 중복 없음 |
| `PlayerPaintCanvas` RenderTexture·머티리얼 4개 | 쿠키 생성마다 | `OnDestroy`에서 모두 해제. 단, **대기실 쿠키에도 512² RT를 만든다**(색칠이 없는 씬) — 인원이 늘면 지연 생성 고려 |

### R4.10 Photon Ownership/RPC 구조 — ⚠ 일부 있음

잘 지켜지는 것: Room Props는 방장만(11개 키), Player Props는 본인만(3개 키), **파괴 확정은 피해자 본인만**, 문 상태는 방장 요청 방식,
괴물 스폰은 괴물 본인이 `PhotonNetwork.Instantiate`, 색칠 이벤트는 viewId로 대상 필터링, 씬 전환은 방장만(`RoomSceneTransition`), 맵 이름은 시작 신호와 **한 요청으로 원자적 기록**.

**R4.10-1 ⏸ 그랩에 수락 절차가 없다(실제 결함).**
- 드는 쪽은 RPC를 보내자마자 `carriedPlayer = target`으로 확정(`PlayerGrabController.cs` TryGrab).
- 들리는 쪽 `OnGrabbedByOwner`(`HideOrSeekPlayer.cs:82-86`)는 이미 들려 있는지 확인하지 않고 캐리어를 덮어쓴다(`PlayerCarryFollower.TryAttach`).
- `OnReleased`(`:89`)는 보낸 사람이 현재 캐리어인지 확인하지 않는다. 두 RPC 모두 `PhotonMessageInfo`를 받지 않는다.
- 재현: A가 C를 든다 → B가 C를 든다(C는 B를 따라감) → A가 놓는다 → C가 B에게서 떨어진다 → B는 빈손으로 캐리 자세·충돌 무시 상태에 갇힌다.
  또 B가 C를 든 채 A가 B를 들면 C→B→A **연쇄 캐리**가 된다.
- 권장: 들리는 쪽이 `IsCarried`/`IsCarrying`이면 거절하고 결과를 회신(`OnGrabResult(bool)`), `OnReleased`는 `info.Sender == 현재 캐리어 주인`일 때만 처리.

**R4.10-2 ✅ 처형 RPC 발신자 검증(해결).** `HideOrSeekPlayer.cs:115-124` — 괴물 ViewID로 PhotonView를 찾고 `monsterView.Owner != info.Sender`면 무시.
남은 점: `DoorStateRequest`(`InteractableDoor.OnEvent`)는 여전히 거리 검증이 없다(문을 원격으로 여닫을 수 있음 — 영향 작음).
설계 메모: 파괴 확정이 **피해자 클라이언트 권한**이라 수정된 클라이언트는 처형 RPC를 무시해 무적이 될 수 있다. 방장이 `HitCount`를 대신 기록하는
방식(방장 판정)과의 트레이드오프이며, 현재 선택(소유권 원칙)을 유지한다면 문서에 명시해 둘 가치가 있다.

**R4.10-3 ⏸ 문 상태의 방장 대기 캐시가 정적이다.** `Environment/InteractableDoor.cs:31` `static masterPendingWrites`는 `DoorStates` 삭제(`:170`)·방장 교체(`:178`) 때만 비워진다.
응답 전에 방을 나가 다른 방을 만들면 이전 방의 값이 새 방의 첫 `WriteState`(`:158`)에 섞인다. 맵 씬마다 문 ID가 다르고(`CHO_…`, `HAU_…`) 과자집 문 ID는 대기실 공통이라,
섞이면 **대기실 문**에서 드러난다.
- 권장: `OnLeftRoom`과 씬 전환 때 비우기(또는 R4.8의 `DoorStateRouter` 인스턴스 필드).

**R4.10-4 ⏸ 퇴장 시 Player Props를 API 없이 지운다.** `RoomExitController`가 `LocalPlayer.CustomProperties.Remove(key)`로 로컬 사본만 지운다(의도는 주석에 있음).

**R4.10-5 ⏸ PaintStroke 이벤트는 캐시되지 않는다.** 시작 때 방을 닫으므로 정상 흐름에선 문제없다. 재접속 기능을 넣으면 필요하다.

**R4.10-6 🆕 방장 쓰기에 "응답 대기" 가드가 없는 곳이 있다.**
PUN 기본(`BroadcastPropsChangeToAll=true`)에서는 `SetCustomProperties`가 서버 응답 전까지 로컬 캐시에 반영되지 않는다. 다른 방장 컴포넌트는
`joinRequested`·`confirmRequested`·`deadlineRequested` 같은 플래그로 막는데 두 곳은 빠졌다.
- `Monster/GameRuleController.cs:20, 26, 44`: 승패 조건이 성립하면 **왕복 시간(RTT) 동안 매 프레임 `GameResult`를 다시 보낸다**(60fps·RTT 100ms면 약 6번).
  결과 값은 같아 화면은 정상이지만 방 전체에 Props 변경 이벤트가 그만큼 브로드캐스트되고, `ResultScreenController`·`MasterClientPolicy` 등 모든 `OnRoomPropertiesUpdate`가 반복 호출된다.
  드물게 "마지막 쿠키 파괴"와 "시간 종료"가 한 RTT 안에 겹치면 두 다른 결과가 연달아 전송돼 **나중 값이 이긴다**(화면은 `isShown` 가드로 첫 값만 표시 → 표시와 Room Prop이 어긋날 수 있음).
- `ColorTag/PaintPhaseController.cs` Update: 처리 여부를 `ContainsKey(ForcedPaintActorNumbers)`로만 판단하므로 응답 전 매 프레임 `ResolvePaintPhase()`가 다시 돌고,
  **매번 `System.Random`으로 다른 색을 배정해** 보낸다. 대상 쿠키는 통지마다 전신 강제 도포를 다시 하므로 최종 색은 마지막 값으로 맞춰지지만, 짧게 색이 바뀌어 보이고
  `RegisteredSlotCount` 보고와 PaintStroke(ForceFill) 이벤트가 그 횟수만큼 나간다.
- `Monster/MonsterAssignmentAuthority.cs:52`: 정원 미달 시 `MonsterSelectDeadline = null`을 응답 전까지 매 프레임 보낸다(영향 작음).
- 권장: 요청 플래그 추가. 더 확실하게는 `SetCustomProperties(props, expectedProperties: { GameResult: null })`로 **CAS**를 걸어 첫 기록만 성공하게 한다.
  같은 방식이 `MonsterJoined`·`ForcedPaintActorNumbers`에도 방장 교체 경합 방지로 유효하다(현재는 로컬 플래그 + 캐시 확인).

### R4.11 중복 로직 — ⚠ 다수(악화)

| # | 중복 | 위치 | 상태 | 권장 |
|---|---|---|---|---|
| 1 | 스폰(Find → 겹침 회피 → Instantiate) 3벌, `"MonsterPlayer"` 2벌 | `PlayerSpawner.cs:54-69`, `MonsterJoinController.cs:64-80`, `MonsterTestSpawner.cs:24-31` | ⏸ | `SceneSpawnPoints.TryFindClearPosition` + `NetworkPrefabs` 상수 |
| 2 | InRoom 대기 코루틴 3벌 + 첫 프레임 Update 2벌 | GameManager, PlayerSpawner, MonsterTestSpawner / MasterClientPolicy, RoundStateResetter | ⏸ | `RoomState.WaitUntilInRoom()` |
| 3 | 클립 이름으로 길이 찾기 | `MonsterController.cs:149`, `InteractableDoor` | ⏸ | `AnimatorUtil.FindClipLength` |
| 4 | 트리거 기반 상태 전환 | `PlayerAnimationDriver.ChangeState`, `MonsterController.ChangeState` | ⏸ | 제네릭 `TriggerStateAnimator<TState>` |
| 5 | 로컬 리지드바디 설정 / 낙하 리스폰 / 카메라 초기화 | HideOrSeekPlayer ↔ MonsterController | ⏸ | 공용 `CharacterBody` |
| 6 | 파괴 여부 판정 | `PlayerGrabController.IsOwnerBroken` ↔ `RoomState.IsBroken` | ⏸ | `RoomState.IsBroken` |
| 7 | 표시 이름(닉네임 없으면 `#번호`) 3벌 + 1곳 미적용 | GameLobbyController, MonsterRevealController, SpectatorController / ResultScreenController | ⏸ | `RoomState.DisplayName(Player)` |
| 8 | 색칠 남은 시간 표시 2곳 | `ColorSelectionPanel.cs:31-34`(timeLabel) + PhaseCountdownDisplay(Paint) — **씬 6개 각각** | ⏸ | 하나로 |
| 9 | 로컬 캐릭터 찾기 | `CharacterInteractor.cs:63` ↔ `CharacterRegistry.FindLocal<T>()` | ⏸ | `FindLocal<IGameCharacter>()` |
| 10 | 레지스트리 클래스 2벌 | CharacterRegistry ↔ InteractableRegistry | ⏸ | 제네릭 `Registry<T>` |
| 11 | "바뀔 때만 CanvasGroup 갱신" | PhaseCountdownDisplay, ColorSelectionPanel, InteractionPromptUI | ⏸ | `CanvasGroupVisibility` 캐시 버전 |
| 12 | `IsPaintPhaseActive()` 한 줄 래퍼 | BrushCursorController, PlayerPaintCanvas | ⏸ | 직접 사용 |
| 13 | 괴물 이탈 시 목록에서 빼기 | MonsterAssignmentAuthority ↔ RoomLifecycleWatcher | ⏸ | 배열 제거만 `RoomState.WithoutMonster` |
| 14 | `IsRoomFull()` 한 줄 래퍼 | GameLobbyController | ⏸ | 직접 사용 |
| **15** | **게임 매니저·HUD 세트 씬 복제** | GameScene + 맵 5개(R4.3-9) | 🆕 | 프리팹 2개로 |
| **16** | **ColorSlotPanel 정의 7벌** | Resources 프리팹 1 + 씬 6(비프리팹) | 🆕(R4.3-5 확대) | 프리팹 하나로 |
| **17** | **관전 진입 호출 2곳** | `HideOrSeekPlayer.cs:149`, `CookieLifeStatePresenter.cs:176` | 🆕 | 파괴 확정 시점 하나(예: `CookieLifeStatePresenter`가 `Broken`으로 바뀔 때)로 |
| **18** | **"트리거·코루틴 없이 클립 진행도로 끝 판정"** | `CookieLifeStatePresenter.LateUpdate`(GrabKill 진행도), `CauldronSplash.Update`(Splash 상태), `PlayerAnimationDriver.HandleJumpAnimationHold`(Jump 진행도) | 🆕 | 3곳 모두 문자열/해시 상태 이름 + normalizedTime 조합. `AnimatorUtil.TryGetProgress(layer, hash)` 하나로 |

---

## R5. 그 밖의 발견 사항

1. **R5-1 ⏸ 채팅 토글이 키 떼기만 본다.** 입력창 밖 클릭으로 포커스를 잃어도 `bEnter`는 true로 남아 다음 Enter가 "닫기"로 처리되고 그동안 이동 잠금이 유지된다.
2. **R5-2 ⏸ `GamePhaseStarter`는 정상 흐름에서 아무 일도 하지 않는다.** 대기실을 거치지 않는 테스트 경로용인데 PlayerTestScene에는 없고 **게임 씬 6개에만** 있다.
3. **R5-3 ⏸ 빈 팔레트 예외.** `ColorTag/PaintPhaseController.cs:64` `i % shuffledColors.Length` — 팔레트가 비면 0으로 나누기, 강제 도포 기록 실패로 방장이 매 프레임 재시도.
   `ColorPaletteSO.Count`도 null 배열이면 NRE(`:8`).
4. **R5-4 ⏸ PUN 내부 키 의존.** `MonsterJoinController.cs:18` `"curScn"`.
5. **R5-5 ⏸ 쓰기만 하는 키.** `MonsterRevealTime`.
6. **R5-6 ⏸ 문 상태가 판마다 초기화된다.** `DoorStates`가 Round 수명. 맵 씬 문은 판마다 새 씬이라 자연스럽고, 대기실 과자집 문만 영향.
7. **R5-7 ✅ 방향 변경** — R1.4 참고.
8. **R5-8 ⏸ 매 프레임 할당**: `GameLobbyController.Update`의 `HostActor()` 배열. 🆕 `MonsterJoinController.Update`는 괴물이 아닌 클라이언트에서도 **판 내내 매 프레임**
   `TryLocalSpawn` 조건(Props 조회 2번 + 배열 `Contains`)을 검사한다 — 할당은 없지만 끝나지 않는 폴링이다(`hasSpawnedLocally`가 괴물에게만 세팅됨).
9. **R5-9 테스트 범위.** `RuleTests.cs` **13개**(맵 선택 추가). RPC 순서 문제(R4.10-1)·Unity null 판정(R4.7-5)·빌드 씬 포함 여부(R4.4-7)는 테스트가 없다.
10. **R5-10 ~~🆕 빌드에 있지만 선택되지 않는 맵 2개~~ — 정정: 조사 오류, 문제 없음.** 에셋의 목록 앞 3줄만 읽고 판단했다. 실제 `gameMapScenes`에는 5개가 모두 있다. 아래 원문은 기록으로 남긴다.<br> `Game_CursedCandyCarnival`·`Game_HauntedBakery`는 빌드 목록에 있지만 `gameMapScenes`에 없다.
    개발 중이라 뺀 것이라면 빌드 목록에서도 빼 용량을 줄이고, 완성됐다면 목록에 넣는다. 어느 쪽이든 두 설정이 **서로 다른 진실**을 말하고 있다.
11. **R5-11 🆕 결과 화면 버튼은 방장이 아니면 카운트다운만 멈춘다.** `Monster/ResultScreenController.cs:132-136` — 비방장이 누르면 `StopAllCoroutines()`로 자기 카운트다운 표시가
    멈추고 아무 일도 일어나지 않는다(방장 쪽 카운트다운이 끝나야 이동). 비방장에게는 버튼을 숨기거나 "방장 대기" 문구를 보여 주는 편이 낫다.
12. **R5-12 🆕 결과 화면 타이밍 값이 흩어져 있다.** 괴물 승리 결과는 처형 연출이 끝날 때까지 최대 5초 기다리는데(`MaxGrabKillWait`), 이 값과 자동 복귀 12초가 코드·씬 인스펙터에 나뉘어 있어
    씬 6개마다 `autoReturnDelay`가 달라질 수 있다(R4.3-9의 직접적 결과).

---

## R6. 권장 로드맵

**1단계 — 결함 수정(각 30분 이내)**
- `PlayerGrabController` 파괴 감지 `ReferenceEquals` 수정(R4.7-5)
- `GameRuleController`·`PaintPhaseController` 요청 플래그/CAS(R4.10-6)
- 그랩 수락·놓기 발신자 검증·캐리 중 잡기 금지(R4.10-1)
- 채팅 입력 억제를 `PlayerInput` 한 곳으로(R4.1-1, R5-1)
- 빈 팔레트 방어(R5-3), 문 대기 캐시 초기화(R4.10-3)
- 대체 씬 정리 + 맵 목록·빌드 씬 일치 테스트(R4.4-7; R5-10은 조사 오류로 정정, 회귀 방지 테스트만 유지)

**2단계 — 씬 구조(반나절)**
- `GameSystems.prefab` + `GameHUD.prefab` 추출, 씬 6개를 프리팹 인스턴스로 교체, `MapSceneBuilder`가 복사 대신 배치(R4.3-9, R4.11-15·16)
  — 이 작업 전에 씬 6개의 인스펙터 값이 서로 다른지(예: `autoReturnDelay`, 문구) 먼저 대조해 하나로 정한다.

**3단계 — 책임 정리(각 1시간 이내)**
- 처형 연출 데이터를 `GrabKillPresentationSO` + `IGrabKillSource`로(R4.1-8, R4.2), 관전 진입 한 곳으로(R4.11-17)
- GameManager 전역 설정 대입 제거, 시작 요청 수신을 비 UI 컴포넌트로(R4.1-2, R4.1-3)
- 스폰·InRoom 대기·표시 이름·클립 길이·애니메이션 진행도 공용화(R4.11-1·2·3·7·18)
- 대기실 목록 `UiListBuilder` 재사용 + `HostActor` 할당 제거(R4.9-2)
- 직전 맵을 Session Room Prop으로(R4.5)

**4단계 — 구조(판 규모가 커질 때)**
- 폴더 재배치 `Round/`·`Spectator/`·`World/`·`Cookie/`(R4.1-6)
- MonsterController 협력 객체 분리 + 공용 `CharacterBody`(R4.1-7, R4.11-5)
- 마스터 전용 단계 전이를 `RoundDirector` 하나로(R4.2), 문 이벤트를 `DoorStateRouter` 하나로(R4.8)
- `IsPaintScene` 정적 bool을 카운터/참조로(R4.7-1)

---

## 부록

### A. 교차 도메인 의존성(주석 제외, Core 제외)

| 파일 | 참조하는 다른 도메인 클래스 |
|---|---|
| GameManager/GameManager.cs | HideOrSeekPlayer(Unit) |
| GameManager/PlayerSpawner.cs | **OfflineModeBootstrap(Dev)** |
| Lobby/PlayerSkinSelector.cs | SkinCatalogSO(Unit) |
| Monster/Cauldron.cs | HideOrSeekPlayer(Unit) |
| Monster/MonsterController.cs | Camera_Ctrl(Camera), HideOrSeekPlayer(Unit) |
| Monster/MonsterGrabKillTrigger.cs | HideOrSeekPlayer(Unit) |
| Monster/MonsterLobbyWaitController.cs 🆕 | GameStartAuthority(Lobby) |
| Monster/ResultScreenController.cs 🆕 | CookieLifeStatePresenter(ColorTag) |
| Monster/SpectatorController.cs | Camera_Ctrl(Camera) |
| Unit/HideOrSeekPlayer.cs | Camera_Ctrl(Camera), **SpectatorController(Monster)**, 🆕 **MonsterController(Monster)**, CookieLifeStatePresenter(ColorTag) |
| Unit/PlayerSkinApplier.cs | **PlayerPaintCanvas(ColorTag)** |
| ColorTag/CookieLifeStatePresenter.cs 🆕 | **MonsterController(Monster)**, SpectatorController(Monster) |
| Environment/InteractableDoor.cs | IInteractable, InteractableRegistry(Interaction) |

Core는 Photon·Unity 외에 어떤 도메인도 참조하지 않는다(단방향 의존 유지). Unit ↔ Monster ↔ ColorTag 사이에는 **순환**이 생겼다
(Unit → Monster → Unit, ColorTag → Monster → ColorTag). 한 asmdef 안이라 컴파일은 되지만, 도메인별 asmdef로 나누려면 R4.1-8의 계약 분리가 먼저 필요하다.

### B. 전역(static) 상태 목록

| 상태 | 위치 | 수명 | 비고 |
|---|---|---|---|
| `CharacterRegistry.characters` | Core/GameCharacter.cs | 앱 | OnEnable/OnDisable |
| `InteractableRegistry.interactables` | Interaction/IInteractable.cs | 앱 | 같음 |
| `CharacterInteractor.instance` | Interaction/CharacterInteractor.cs | 앱(DDOL) | 자동 생성 |
| `PlayerPaintCanvas.Local` | ColorTag/PlayerPaintCanvas.cs:126 | 로컬 쿠키 수명 | Start 설정, OnDestroy 해제 |
| `PaintPhaseController.IsPaintScene` | ColorTag/PaintPhaseController.cs:23 | 씬 | R4.7-1 |
| `InteractableDoor.masterPendingWrites` | Environment/InteractableDoor.cs:31 | 앱 | **방을 옮겨도 남음**, R4.10-3 |
| `GameStartAuthority.lastStartTime` | Lobby/GameStartAuthority.cs | 앱 | 3초 가드 |
| `GameStartAuthority.lastMap` 🆕 | Lobby/GameStartAuthority.cs:77 | 앱 | 방장 교체 시 유실, 방을 옮겨도 남음(R4.5) |
| `SpectatorController.SpectateTargetChanged` | Monster/SpectatorController.cs | 앱 | 이벤트 |
| `OfflineModeBootstrap.SpawnAsMonster` | Dev/OfflineModeBootstrap.cs | 씬 | 개발용 |
| `GameSettings.cached`, `PlayerInput.bindings` | Core | 앱 | SO 캐시 |
| `SpawnPositionFinder.OverlapBuffer` | Core/SpawnPositionFinder.cs | 앱 | 메인 스레드 전용 |
| `NetworkTransformSync<T>` 정적 생성자 | Core/NetworkTransformSync.cs | 앱 | enum 크기 검사만 |

### C. 전역 네트워크·엔진 설정을 바꾸는 곳

| 설정 | 쓰는 곳 |
|---|---|
| `IsMessageQueueRunning` | false: MonsterLobbyWaitController / true: GameManager:28, PlayerSpawner:65, RoomExitController:53 (+PUN LoadLevel 자동 재개) |
| `AutomaticallySyncScene` | LobbyController(true), MonsterLobbyWaitController(괴물=false), MonsterJoinController(복구) |
| `KeepAliveInBackground` | MonsterLobbyWaitController(연장), MonsterJoinController, RoomExitController(복구) |
| `SerializationRate`, `GameVersion` | LobbyController |
| `OfflineMode` | OfflineModeBootstrap |
| `Time.timeScale` | GameManager:27 |
| `Cursor.visible` / `Cursor.lockState` | BrushCursorController, Camera_Ctrl |

### D. 마스터 전용 Update 폴링

| 컴포넌트 | 씬 | 조건 → 동작 | 응답 대기 가드 |
|---|---|---|---|
| MonsterAssignmentAuthority | GameLobby | 정원·기한 → 괴물 확정/초기화 | 확정·기한·초기화 ✅ / 기한 삭제 ❌(`:52`) |
| MasterClientPolicy | GameLobby + 게임 6 | 입장 첫 프레임 → 방장 정책 | `requestedActor` ✅ |
| RoundStateResetter | GameLobby | 입장 첫 프레임 → 판 초기화 | 1회 ✅ |
| GamePhaseStarter | 게임 6 | PaintPhaseEndTime 없음 → 기록(예비) | `started` ✅ |
| PaintPhaseController | 게임 6 | 색칠 종료 → 강제 도포 배정 | ❌ 캐시 확인만(응답 전 매 프레임 재배정 가능 — 무작위라 **다른 색**이 연달아 전송될 수 있음, 마지막 값이 이김) |
| MonsterJoinController | 게임 6 | 색칠 종료 → 합류·생존 종료 시각 | `joinRequested` ✅ |
| GameRuleController | 게임 6 | Hunt 중 → 승패 판정 | ❌ R4.10-6 |
| RoomLifecycleWatcher | 게임 6 | 괴물 전원 이탈 후 지연 → 대기실 복귀 | 1회 ✅ |

> 표의 PaintPhaseController 행: `ContainsKey(ForcedPaintActorNumbers)`는 서버 응답 후에야 true가 되므로, 응답 전 프레임마다 `ResolvePaintPhase()`가 다시 실행되고
> 매번 `System.Random`으로 **다른 색 배정**을 보낸다. 대상 쿠키는 `OnRoomPropertiesUpdate`마다 강제 도포를 다시 하므로 최종 색은 마지막 값으로 맞춰지지만,
> 짧은 순간 색이 바뀌어 보이고 슬롯 보고가 여러 번 나간다. R4.10-6과 같은 방식(요청 플래그 또는 CAS)으로 함께 고친다.
