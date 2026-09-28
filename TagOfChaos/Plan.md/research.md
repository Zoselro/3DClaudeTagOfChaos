# 조사 보고서: TagOfChaos 런타임 스크립트 전수 분석 + 아키텍처 11개 관점 감사 (2026-09-28 4차, 커밋 `7f8cd38` + 미커밋 작업 트리)

> **개정 안내 (4차)**: 이 문서는 같은 날 작성된 3차 판(커밋 `7f8cd38` 기준)을 **갱신**한다. 3차 판 원문은 덮어쓰기 전에
> `Plan.md/research.prev.md`로 복사해 두었다(3차 판의 후속 조치 절은 커밋되지 않은 상태였으므로 `git show`로는 볼 수 없다. 확인 후 지워도 된다).
> 코드 주석이 `research.md R4.10-2`처럼 이 문서의 절 번호를 참조하므로 **R 번호 체계(R1~R6, 부록 A~D)와 기존 항목 번호를 그대로 유지**했다.
> 상태 표기: **✅ 해결됨** / **🔁 부분 해결** / **⏸ 유지(미해결)** / **🆕 신규**. 새 항목은 같은 절 안에서 다음 번호를 받는다.
> (`§8.x`, `§12 E1~E12`, `G5-1` 같은 참조는 그보다 더 이전 판들의 번호다.)
>
> **3차 판 이후 들어온 변경(미커밋 작업 트리, Bug-fix-plan.md §41)**
> - 채팅 입력 억제를 `PlayerInput.IsGameplaySuppressed` 한 곳으로 옮김, 채팅 입력창 포커스 해제 처리(`onDeselect`).
> - 게임 규칙·HUD 매니저 세트를 **`Assets/04. Prefabs/Scene/GameSceneCore.prefab` 하나**로 묶고, 맵 씬 5개와 PlayerTestScene은 그 인스턴스만 둔다.
>   `MapSceneBuilder`의 복제 원본이 `GameScene` → `PlayerTestScene`으로 바뀌었고, `GameScene.unity`는 삭제됐다.
> - `SceneNames.Game` 제거, `GameSettingsSO.PickGameMap`이 목록이 비면 `null`을 돌려주고 시작을 막는다.
> - 방장 쓰기 중복 방지 플래그 추가(`GameRuleController.resultRequested`, `PaintPhaseController.resolveRequested`, `MonsterAssignmentAuthority.deadlineClearRequested`).
> - `PlayerGrabController`의 Unity null 판정 수정(`HasCarryReference`), 빈 팔레트 방어, 파괴 판정을 `RoomState.IsBroken` 하나로.
> - `PlayerGroundDetector` → **`Core/CharacterGroundDetector`**(괴물도 사용, 벽 접촉 성분 제거). 촉수 돌진을 속도 기반 물리 이동으로 재작성(경사 추종·지면 붙이기·막힘 판정).
> - EditMode 테스트 13개 → **16개**(맵 목록 ↔ 빌드 씬 일치, 게임 씬의 GameSceneCore 단일 사용, 돌진 경사 계산, UI 레이아웃 화면 밖 검사 등).
>
> **조사 방법**
> - Unity MCP로 에디터에 직접 접속해 `Assets/02. Scripts/` 아래 **10개 폴더, 79개 `.cs`, 6,614줄을 모두 읽었다.**
> - 빌드 씬 8개(비활성 PlayerTestScene 포함)의 YAML에서 **스크립트 GUID → 컴포넌트 수**, 프리팹 인스턴스(`m_SourcePrefab`), `sceneViewId`·`m_IsActive` 오버라이드를 집계했다(R2.4).
> - `GameSceneCore.prefab`을 `PrefabUtility.LoadPrefabContents`로 열어 자식 구성·직렬화 값(문구·`autoReturnDelay`·`bannerRoot`·PhotonView)을 확인했다.
> - `GameSettings.asset`, `ProjectSettings.asset`(`activeInputHandler`), `EditorSettings.asset`(Enter Play Mode), `Packages/manifest.json`, `TagManager`(레이어·충돌 행렬), `.git/HEAD`를 확인했다.
> - 3차 판의 모든 지적 항목을 현재 코드와 다시 대조했다. **이번 판은 정적 분석이다** — Play Mode 재현을 하지 않은 항목은 본문에 “코드 추론”이라고 적었다.
> - 파일 경로는 따로 적지 않으면 `Assets/02. Scripts/` 기준이다. 줄 번호는 현재 작업 트리 기준이다. 예: `Unit/HideOrSeekPlayer.cs:109`.

---

## 목차

- **R1. 요약** — 결론, 11개 관점 판정표, 우선 조치 Top 10, 이전 판 대비 변화
- **R2. 프로젝트 구조** — 엔진·패키지·폴더·씬·프리팹·SO·배선표
- **R3. 동작 방식 상세** — 한 판의 흐름, 네트워크 계약, 도메인별 동작
- **R4. 11개 관점 감사** — 관점마다 판정, 근거(file:line), 영향, 권장
- **R5. 그 밖의 발견 사항**
- **R6. 권장 로드맵**
- **부록** — 교차 도메인 의존성, 전역(static) 상태, 전역 네트워크·엔진 설정을 바꾸는 곳, 마스터 전용 Update 폴링

---

## R1. 요약

### R1.1 결론

뼈대는 좋고, 3차 판 이후 **가장 큰 구조 문제였던 “씬마다 매니저 세트 복제”가 해결됐다.** 게임 규칙·HUD 20종이 `GameSceneCore.prefab` 하나에 모였고,
EditMode 테스트(`GameScenes_UseSingleSceneCorePrefab`, `GameMaps_MatchEnabledBuildMapScenes`)가 회귀를 막는다. 채팅 입력 억제, Unity null 판정,
방장 쓰기 중복, 빈 팔레트, 빌드에 없는 대체 씬도 모두 고쳐졌다(**3차 판 지적 중 11건 해결**, R1.4).

역할 분리의 기본 틀도 그대로 지켜진다. `RoomState`(조회)·`NetKeys`(키·수명 표)·`GamePhaseState`(단계 해석)·`CharacterRegistry`/`IGameCharacter`(캐릭터 계약)·
`PlayerInput`/`InputBindingsSO`(입력)·`GameSettingsSO`(수치)·`RoomSceneTransition`(씬 전환)이 각자 한 책임을 맡고, **Core는 Photon·Unity 외에 어떤 도메인도 참조하지 않는다.**
`Instance` 싱글톤은 없고, Room Props는 방장만·Player Props는 본인만 쓰는 **Photon 소유권 원칙**도 지켜진다. 오브젝트 풀은 없으므로 풀과 Instantiate/Destroy의 충돌도 없다.

남은 문제는 세 갈래다.

1. **쿠키 그랩(들기)에 수락 절차·발신자 검증이 여전히 없다(⏸ R4.10-1, 실제 결함).** 두 명이 같은 쿠키를 잡거나 연쇄로 들면 캐리 상태가 꼬인다.
   처형 RPC는 발신자를 검증하지만 그랩/놓기 RPC와 RaiseEvent 3종(색칠·가마솥·문)은 검증하지 않는다(🆕 R4.10-7).
2. **처형 한 건에 5개 클래스가 양방향으로 얽힌 구조가 그대로다(⏸ R4.1-8, R4.2).** 그 사이 `MonsterController`는 촉수 돌진 물리가 더해져 319줄 → **404줄**이 됐다(⏸ R4.1-7 악화).
3. **판 단계 전이가 마스터 전용 폴링 8곳에 흩어져 있다(⏸ R4.2).** 이번에 “응답 대기 플래그”가 모두 갖춰졌지만, 그 결과 **같은 패턴이 9벌**이 됐고(🆕 R4.11-19),
   게임 씬의 `GamePhaseStarter`가 첫 프레임에 **방장 자신의 시작 기록이 아직 캐시에 없는 순간**을 보고 색칠 종료 시각을 다시 쓸 수 있는 경합이 코드상 존재한다(🆕 R4.7-7, 코드 추론).

현재 코드 기준으로 재현 가능한(또는 코드상 성립하는) 결함은 다음과 같다.

| # | 결함 | 상태 | 절 |
|---|---|---|---|
| 1 | 쿠키 그랩에 수락 절차·발신자 확인이 없어 두 명이 같은 쿠키를 잡으면 캐리 상태가 꼬인다(연쇄 캐리 가능) | ⏸ | R4.10-1 |
| 2 | 방장의 게임 씬 첫 프레임에 `GamePhaseStarter`가 `PaintPhaseEndTime`을 (여유 3초 없이) 다시 쓸 수 있다 — 대기실 괴물의 카운트다운과 어긋남 | 🆕 코드 추론 | R4.7-7 |
| 3 | 회피 도중 들리면 회피 상태가 얼어 있다가, 내려진 뒤 남은 시간만큼 옛 회피 방향으로 2배 속도로 밀려난다 | 🆕 코드 추론 | R5-14 |
| 4 | PlayerTestScene에 네트워크 캐릭터 프리팹(`HideOrSeekPlayer`)이 **활성 씬 오브젝트**로 배치돼 있다 — 오프라인에서 룸 뷰가 `IsMine`이 되면 스폰된 캐릭터와 입력을 함께 받는다 | 🆕 코드 추론 | R4.3-11 |
| 5 | 처형 중인 원격 쿠키의 Transform을 보간(Update)과 연출(LateUpdate)이 매 프레임 함께 쓴다(결과는 맞지만 주인이 실행 순서에만 달려 있음) | ⏸ | R4.7-6 |

### R1.2 11개 관점 판정표

| # | 관점 | 판정 | 한 줄 요약 | 3차 판 대비 |
|---|---|---|---|---|
| 1 | 기존 책임 분리를 무시하는 코드 | ⚠ 일부 있음 | ✅ 채팅 입력 억제가 `PlayerInput`으로. ⏸ 채팅이 `timeScale`·메시지 큐를 바꾸고, UI 패널이 시작 요청을 처리하고, 처형 연출 값이 괴물 컨트롤러에 있다 | 1건 해결 |
| 2 | Manager 간 의존성 과도 증가 | ⚠ 주의 | 씬 매니저끼리의 직접 참조는 5곳뿐. 처형 5클래스 양방향 결합과 마스터 폴링 8개의 암묵 결합은 그대로 | 동일 |
| 3 | Prefab과 Script 역할 뒤섞임 | ⚠ 일부 있음 | ✅ **매니저 세트 씬 복제 해결(GameSceneCore)**. ⏸ 자식 이름·트리거 이름·클립 키워드 의존. 🆕 테스트 씬에 네트워크 캐릭터 배치 + 그 씬이 맵 빌더 원본 | 크게 개선 |
| 4 | Scene에 직접 의존하는 코드 증가 | ⚠ 주의 | ✅ 빌드에 없는 대체 씬 제거. ⏸ `GameObject.Find` 4곳·`Camera.main` 10회. 🆕 맵 빌더가 테스트 씬 내용에 의존 | 1건 해결, 1건 신규 |
| 5 | Singleton 남발 | ✅ 양호(가변 정적 상태 14곳) | `Instance` 싱글톤 0개, 자동 생성 DDOL 1개. `PlayerInput.IsGameplaySuppressed` 1곳 추가(소유자가 해제를 보장) | 거의 동일 |
| 6 | ScriptableObject 책임 오용 | ✅ 양호 | 5개 SO 모두 읽기 전용, 런타임 상태 쓰기 없음. ✅ 팔레트 null 방어. 🆕 `GameSettings.asset` 직렬화가 코드보다 오래됨(옛 필드 1개, 새 필드 2개 미저장) | 동일 |
| 7 | Unity Lifecycle 순서 문제 | ⚠ 일부 있음 | ✅ Unity null 판정 결함 해결. ⏸ 정적 `IsPaintScene`, 원격 첫 프레임 물리, Transform 두 작성자. 🆕 `GamePhaseStarter` 첫 프레임 경합(코드 추론) | 1건 해결, 1건 신규 |
| 8 | Event 구독/해제 | ✅ 양호 | 정적 이벤트 1개 짝 맞음. `MonoBehaviourPunCallbacks` 25개 중 OnEnable/OnDisable 재정의 6개 모두 `base` 호출. 🆕 콜백을 전혀 쓰지 않는 상속 4개(비용만 발생) | 동일 |
| 9 | Object Pool ↔ Instantiate/Destroy 충돌 | ✅ 충돌 없음 | 풀이 없다(PUN `DefaultPool`). 반복 생성·파괴는 대기실 참가자 목록 한 곳(⏸) | 동일 |
| 10 | Photon Ownership/RPC 구조 무시 | ⚠ 일부 있음 | ✅ 방장 쓰기 응답 대기. ⏸ 그랩/놓기 RPC 검증 없음, 정적 문 캐시. 🆕 RaiseEvent 발신자 미검증 3종 | 1건 해결, 1건 신규 |
| 11 | 중복 로직 | ⚠ 다수 | ✅ 씬 복제·파괴 판정 중복 해결. ⏸ 스폰 3벌·표시 이름 3벌 등 12건. 🆕 응답 대기 플래그 패턴 9벌, 호출자 0인 `FindLocal<T>` | 개선 |

### R1.3 우선 조치 Top 10

| 순위 | 항목 | 근거 | 난이도 |
|---|---|---|---|
| 1 | 그랩 수락 절차 + `OnGrabbedByOwner`·`OnReleased` 발신자 검증 + 이미 들렸거나 들고 있는 쿠키 잡기 금지 | R4.10-1 | 중 |
| 2 | `GamePhaseStarter`를 “대기실을 거치지 않은 경우”에만 동작하게 제한(예: `GameMapScene` 키도 없고 몇 초 기다린 뒤, 또는 PlayerTestScene 전용 플래그) | R4.7-7 | 하 |
| 3 | RaiseEvent 발신자 검증: 색칠·리셋은 `photonEvent.Sender == pv.OwnerActorNr`, 괴물 신청은 `CustomData` 대신 `photonEvent.Sender` 사용 | R4.10-7 | 하 |
| 4 | 들림·파괴 시 회피 상태 정리(`DodgeOut()` 호출), 회피 속도를 `speed` 변경 대신 `baseSpeed * 2`로 계산 | R5-14 | 하 |
| 5 | 처형 연출 튜닝값(5개)을 `GrabKillPresentationSO`로 분리하고 쿠키 쪽은 `IGrabKillSource` 계약만 알게 하기 | R4.1-8, R4.2 | 중 |
| 6 | 원격 쿠키 보간을 처형 연출 동안 멈추기(`IsMovementLocked`에 `IsBeingGrabKilled` 포함) | R4.7-6 | 하 |
| 7 | PlayerTestScene의 활성 `HideOrSeekPlayer` 인스턴스를 입력 없는 더미로 바꾸거나 비활성화, 맵 빌더 원본을 전용 템플릿 씬으로 분리 | R4.3-11, R4.4-8 | 하 |
| 8 | 방장 쓰기 패턴을 `MasterPropertyWriter`(요청 플래그 + `expectedProperties` CAS) 하나로 | R4.11-19, R4.10-6 | 중 |
| 9 | `GameManager.Start`의 `Time.timeScale`·`IsMessageQueueRunning` 대입 제거, 시작 요청 수신을 UI 패널 밖으로 | R4.1-2, R4.1-3 | 하 |
| 10 | 스폰 3벌을 `SceneSpawnPoints.TryFindClearPosition` + 프리팹 이름 상수 한 곳으로 통일 | R4.11-1 | 하 |

### R1.4 이전 판 대비 변화

| 3차 판 항목 | 현재 상태 | 근거 |
|---|---|---|
| R4.1-1 채팅 이동 잠금이 쿠키에만 | ✅ 해결 | `Core/PlayerInput.cs:31` `IsGameplaySuppressed` — 게임플레이 키 프로퍼티가 모두 먼저 확인. `GameManager/GameManager.cs:74`에서 켜고 `CloseChat`·`OnDisable`·`OnDestroy`에서 끈다 |
| R5-1 채팅 포커스 | ✅ 해결 | `GameManager.cs:27` `InputFdChat.onDeselect` → `CloseChat(send: false)` |
| R4.7-5 Unity null 판정으로 캐리 정리 불가 | ✅ 해결 | `Unit/PlayerGrabController.cs:29` `HasCarryReference => !ReferenceEquals(carriedPlayer, null)` |
| R4.7-3 `self?.` | ✅ 해결 | `PlayerGrabController`는 `self != null`로 바뀜(단, `GetComponent<T>()?.` 2곳이 남음 — R4.7-3 본문) |
| R4.10-6 방장 쓰기 응답 대기 없음 | ✅ 해결 | `GameRuleController.cs:55`, `PaintPhaseController.cs:59,62`, `MonsterAssignmentAuthority.cs:55` |
| R5-3 빈 팔레트 0 나누기 | ✅ 해결 | `PaintPhaseController` 팔레트 검사 + `ColorPaletteSO.Count`·`GetColor` null/범위 방어 |
| R4.4-7 빌드에 없는 GameScene 대체값 | ✅ 해결 | `SceneNames.Game` 제거, `PickGameMap`이 `null` 반환 → `GameStartAuthority`가 시작 거부, 괴물 대기는 큐를 되살린다(`MonsterLobbyWaitController.cs:92` 이후) |
| R4.3-9·R4.11-15 매니저 세트 씬 복제 | ✅ 해결 | `GameSceneCore.prefab`(R2.4). 맵 씬 5개·PlayerTestScene 모두 인스턴스 1개, 테스트로 보증 |
| R4.11-16 ColorSlotPanel 7벌 | 🔁 부분 | 사용 중인 정의는 GameSceneCore 1벌. 옛 `Resources/UI/Scene/ColorSelectionPanel.prefab`은 **참조 0건으로 남음**(R5-15) |
| R4.11-6 파괴 판정 중복 | ✅ 해결 | `IsOwnerBroken` 제거, `RoomState.IsBroken` 사용 |
| R5-2 `GamePhaseStarter` 쓰임 없음 | ✅ 목적 회복 | PlayerTestScene도 GameSceneCore를 쓰므로 대기실 없는 경로에서 동작. 단 게임 씬 경합은 🆕 R4.7-7 |
| R5-12 결과 화면 타이밍 값이 씬마다 | ✅ 해결 | `autoReturnDelay=12`가 GameSceneCore 한 곳 |
| R4.10-1, R4.1-2~8, R4.3-1~8, R4.7-1·2·4·6, R4.11-1~5·7~14·17·18, R5-4~6·8·11 | ⏸ 유지 | 각 절에서 현재 줄 번호로 다시 적었다 |

---

## R2. 프로젝트 구조

### R2.1 엔진·패키지·설정

- Unity **6000.0.58f2**, Photon **PUN 2**(Realtime·Chat 포함), Post Processing **3.5.4**(`PostProcessing` 레이어), ProBuilder 6.1.2, uGUI 2.0.0.
- **입력**: 게임 코드는 레거시 Input Manager만 읽는다(`Core/PlayerInput.cs`가 유일한 창구). 그런데 **Input System 1.14.2 패키지가 설치돼 있고
  `activeInputHandler: 2`(Both)**이며, `GameSceneCore`의 EventSystem은 `InputSystemUIInputModule`, LobbyScene·GameLobbyScene은 `StandaloneInputModule`을 쓴다(🆕 R5-13).
- asmdef: 런타임 **`TagOfChaos.Scripts`** 하나(PhotonUnityNetworking, PhotonRealtime, Unity.TextMeshPro, Unity.ugui 참조), 에디터 `TagOfChaos.Editor`,
  테스트 `TagOfChaos.EditorTests`(`Assets/Editor/Tests/RuleTests.cs`, **16개**).
- Enter Play Mode Options: 켜짐, 옵션값 0(도메인·씬 리로드 모두 수행) — 정적 상태가 플레이 세션 사이에 남지 않는다.
- **빌드 씬**: `PlayerTestScene`(비활성) · `LobbyScene` · `GameLobbyScene` · `Game_CandyForest` · `Game_GingerbreadVillage` · `Game_ChocolateFactory` ·
  `Game_CursedCandyCarnival` · `Game_HauntedBakery`. `GameScene.unity`는 **삭제됐다**.
- **레이어**: 8 PlayerCapsule, 9 Cookie, 10 Monster, 11 BrokenCookie(충돌 행렬에서 모든 레이어와 충돌 해제 — 확인함), 12 PostProcessing.
- **Git**: 저장소 루트는 `F:\3DClaudeTagOfChaos`, `main` = `7f8cd38`. 3차 판 이후 변경은 모두 **미커밋** 작업 트리다.

### R2.2 폴더와 줄 수

| 폴더 | 파일 | 줄 | 역할 |
|---|---|---|---|
| Core | 20 | 976 | 공용 허브(조회·키·단계·입력·설정·씬 전환·레지스트리·스폰 위치·UI 헬퍼·**캐릭터 지면 판정**) |
| Unit | 9 | 847 | 쿠키 캐릭터(조정자 + 순수 C# 협력 객체 3개), 스킨 |
| Monster | 19 | 1,742 | 괴물 캐릭터 + **판 진행 전반**(괴물 선정·합류·승패·결과·관전·방장 정책·판 초기화) |
| ColorTag | 11 | 1,341 | 색칠(캔버스·붓·팔레트 UI·강제 도포) + 쿠키 생존·처형 연출 |
| GameManager | 6 | 387 | 채팅, 스폰, 퇴장, 낙하 복귀, 확인창 |
| Lobby | 6 | 563 | 로비(방 목록), 대기실 패널, 판 시작 권한·맵 선택, 스킨 선택 |
| Environment | 2 | 352 | 상호작용 문, 가마솥 풍덩 연출 |
| Interaction | 3 | 198 | 상호작용 키(E) 탐지·안내 UI·사물 계약 |
| Camera | 1 | 138 | 3인칭 궤도 카메라 |
| Dev | 2 | 70 | 오프라인 테스트 부트스트랩, 괴물 테스트 스포너 |
| **합계** | **79** | **6,614** | (3차 판 6,349줄 → +265줄: 돌진 물리·지면 판정 공용화·입력 억제·요청 플래그) |

250줄이 넘는 파일: `ColorTag/PlayerPaintCanvas.cs` 616, `Unit/HideOrSeekPlayer.cs` 416, `Monster/MonsterController.cs` **404**(3차 319), `Environment/InteractableDoor.cs` 276,
`ColorTag/CookieLifeStatePresenter.cs` 274.

### R2.3 에셋

- **SO**
  - `Resources/GameSettings` — 정원 4, 괴물 1, 선정 대기 30초, 색칠 60초 + 로딩 여유 3초, 슬롯 4, 콜라이더 재굽기 0.2초, 생존 600초, 괴물 이탈 복귀 5초,
    돌진 20m/0.25초/쿨다운 15초, 동기화 10Hz, 스탬프 15Hz(4인 기준, 최소 5Hz), 스폰 범위 쿠키 5m·괴물 4m, 낙하 한계 -100, **맵 5개 모두 등록**.
    직렬화 파일에는 **삭제된 필드 `tentacleDashRadius: 0.4`가 남아 있고 새 필드 `tentacleDashMaxSlope`·`tentacleDashGroundSnap`은 없다**(코드 기본값 45°·0.6m로 동작 — 🆕 R5-16).
  - `Resources/InputBindings`, `03. SO/ColorTag/`(팔레트 10색·붓 설정), `03. SO/Unit/`(스킨 목록).
- **네트워크 프리팹**(PUN `DefaultPool`이 `Resources`에서 로드): `04. Prefabs/Resources/HideOrSeekPlayer.prefab`, `MonsterPlayer.prefab`, (비네트워크) `BrushCursor.prefab`.
  - 둘 다 PhotonView `Synchronization: 3`(UnreliableOnChange), `OwnershipTransfer: 0`(Fixed). 루트 Rigidbody 기본값은 비키네마틱(원격은 `Start()`에서 전환 — R4.7-4).
  - 쿠키 `CookieLifeStatePresenter.breakVfxPrefab` = `CookieCrumbBurst`(`stopAction: Destroy`), `PlayerPaintCanvas.surfaceSampleUVs`(에디터에서 미리 뽑은 몸 표면 샘플).
- **씬 공용 프리팹**: `04. Prefabs/Scene/GameSceneCore.prefab`(R2.4).
- **UI 프리팹**: `Resources/UI/Scene/{LobbyPanel, GameLobbyPanel, ColorSelectionPanel, InteractionPromptUI, PlayerListItem, PlayerResultRow, RoomListItem}`,
  `Resources/UI/Popup/ConfirmDialog`. 런타임 `Resources.Load`는 InteractionPromptUI 하나뿐이고, **`ColorSelectionPanel` 프리팹은 어느 씬·프리팹에서도 참조되지 않는다**(🆕 R5-15).
- **환경 프리팹**: `Environment/Witch_Cookie_House`(InteractableDoor ×4), `Environment/Cauldron`(CauldronSplash), `Witch_Cookie_Environment`(소품 중첩 프리팹 수백 개).
- **맵**: `Assets/Maps/{CandyForest, ChocolateFactory, CursedCandyCarnival, GingerbreadVillage, HauntedBakery, Common}` — FBX·조명 JSON·문 JSON·PostFX.
  `Editor/Maps/MapSceneBuilder.cs`가 **PlayerTestScene을 복제**한 뒤 테스트 전용 루트를 지우고 GameSceneCore 인스턴스·맵 환경·조명·문을 배치한다(R4.4-8).

### R2.4 씬 배선(스크립트 → 컴포넌트 수, YAML 집계)

| 씬 | 씬에 직접 직렬화된 게임 스크립트 | 프리팹 인스턴스 |
|---|---|---|
| LobbyScene | (없음) | LobbyPanel(LobbyController) |
| GameLobbyScene | Camera_Ctrl, Cauldron, ConfirmDialog, GameManager, MasterClientPolicy, MonsterAssignmentAuthority, MonsterLobbyWaitController, MonsterRevealController, PlayerSkinSelector, PlayerSpawner(`skipConfirmedMonster=false`), RoomExitController, RoundStateResetter, VoidKillZone, PhotonView ×2(`sceneViewId` 1·2) | GameLobbyPanel(GameLobbyController), Witch_Cookie_Environment(과자집 문 ×4), Cauldron(CauldronSplash), ConfirmDialog, 소품 |
| Game_CandyForest / Game_GingerbreadVillage | Camera_Ctrl, VoidKillZone | **GameSceneCore**(`sceneViewId` 1·3 오버라이드) |
| Game_ChocolateFactory | 위 + InteractableDoor ×26 | GameSceneCore |
| Game_CursedCandyCarnival | 위 + InteractableDoor ×3 | GameSceneCore |
| Game_HauntedBakery | 위 + InteractableDoor ×22 | GameSceneCore |
| PlayerTestScene(비활성) | Camera_Ctrl, MonsterTestSpawner, OfflineModeBootstrap, VoidKillZone | GameSceneCore, **HideOrSeekPlayer(활성, `sceneViewId` 2)**, **MonsterPlayer(비활성, `sceneViewId` 4)** — R4.3-11 |
| (런타임 생성) | CharacterInteractor(DDOL, 모든 씬) + InteractionPromptUI(Resources) | |

**GameSceneCore.prefab 구성**

| 자식 | 컴포넌트 |
|---|---|
| GameManager | PhotonView, GameManager(채팅), PlayerSpawner, RoomExitController — 두 컴포넌트가 같은 PhotonView로 `LogMsg` RPC를 보낸다 |
| GameRuleManagers | GamePhaseStarter, MonsterJoinController, GameRuleController, RoomLifecycleWatcher, PaintPhaseController, MasterClientPolicy, **PhotonView**(RPC를 쓰는 컴포넌트가 없다 — R4.3-12) |
| PaintManagers | BrushCursorController |
| Canvas | ColorSlotPanel(ColorSelectionPanel + ColorSwatchGroup + ColorSwatchButton ×10 + PaintToolButton ×2), PaintCountdown·SurvivalTimer(PhaseCountdownDisplay ×2), MonsterDepartureBanner, SpectatorLabel, ResultScreen(ResultScreenController, `autoReturnDelay=12`), ConfirmDialog(중첩 프리팹) |
| EventSystem | EventSystem + `InputSystemUIInputModule` |

`MonsterDepartureBanner.bannerRoot`와 `ResultScreenController.root`는 **자기 자신의 GameObject**지만 둘 다 `CanvasGroup`으로만 숨기므로 Photon 콜백이 풀리지 않는다(확인함).
GameLobbyScene의 `MonsterRevealController.bannerRoot`(MonsterRevealBanner)와 `MonsterLobbyWaitController.waitPanelRoot`(MonsterWaitPanel)는 컴포넌트와 **다른** 오브젝트다(확인함 — 자기 자신을 끄는 함정 없음).
인스펙터 문구는 한글로 입력돼 있다(코드의 한글 금지 규칙과 충돌하지 않는다).

프리팹 `HideOrSeekPlayer`: HideOrSeekPlayer, PlayerGrabController, PlayerPaintCanvas, PlayerSkinApplier, CookieLifeStatePresenter, SpectatorController, FallGuard, PhotonView,
자식 Nameplate(PlayerBillBoard). 프리팹 `MonsterPlayer`: MonsterController, MonsterGrabKillTrigger, FallGuard, PhotonView.

---

## R3. 동작 방식 상세

### R3.1 한 판의 흐름

```
LobbyScene
  LobbyController.Awake: AutomaticallySyncScene=true, GameVersion=1
  Start: 미연결이면 SerializationRate=GameSettings.CharacterSyncRate → ConnectUsingSettings → OnConnectedToMaster → JoinLobby → 방 목록
  방 만들기(MaxPlayers=GameSettings.MaxPlayers) / 무작위 입장 / 목록 입장(IsOpen=false면 버튼 비활성)
  OnJoinedRoom: 방을 만든 1인만 LoadLevel(GameLobbyScene), 나머지는 AutomaticallySyncScene으로 따라옴
        │
GameLobbyScene (대기실)
  PlayerSpawner: InRoom이 될 때까지 코루틴 대기 → 쿠키 스폰(대기실에선 괴물도 쿠키, skipConfirmedMonster=false)
  RoundStateResetter: 첫 프레임에 자기 Round Player Props 삭제 / 방장은 이전 판 흔적이 있으면 Round Room Props 삭제 + IsOpen·IsVisible=true
  MasterClientPolicy: 방장 = “괴물이 아닌 사람 중 최소 ActorNumber”로 수렴(SetMasterClient)
  CharacterInteractor(DDOL): 0.1초마다 가장 가까운 IInteractable(과자집 문) → “Press the 'E'” → InteractableDoor가 방장에게 DoorStateRequest
  정원이 차면 MonsterAssignmentAuthority(방장)가 MonsterSelectDeadline = 지금 + 30초
    ├ 쿠키가 가마솥 트리거 진입 → Cauldron이 ClaimMonster 이벤트를 방장에게 → 선착순 확정
    ├ 기한 경과 → 남은 자리 무작위 확정
    └ 정원 미달이 되면 선정 초기화 / 대기실에서 괴물이 나가면 그 사람만 빼고 다시 뽑음
  MonsterRevealController: 배너 / MonsterLobbyWaitController: 괴물이면 AutomaticallySyncScene=false
  호스트(최소 ActorNumber, 괴물이어도 됨)가 시작 버튼 → GameStartAuthority.RequestStart
    ├ 내가 “의도된 방장”이면 바로 TryStart, 아니면 StartGameRequest 이벤트로 방장에게(GameLobbyController.OnEvent가 수신)
    └ TryStart(방장): 요청자=호스트, 미시작, 3초 중복 가드, 정원·괴물 확정 확인
                      map = PickGameMap(직전 맵 제외 무작위, 목록이 비면 null → 시작 거부)
                      Room Props { PaintPhaseEndTime = 지금+3+60초, GameMapScene = map } 한 번에 기록
                      IsOpen=false → OpRemoveCompleteCache → LoadLevel(map)
  괴물 클라이언트: PaintPhaseEndTime 변경 수신 → 다른 쿠키 아바타를 로컬 Destroy, KeepAliveInBackground 연장,
                  메시지 큐 정지, Back 버튼·채팅(GameManager) 비활성, 로컬 실시간 시계로 카운트다운(그동안 문은 로컬로만 여닫힘)
        │
Game_<맵> (쿠키만 먼저 도착 — 모든 맵 씬 = 맵 환경 + GameSceneCore)
  PlayerSpawner: 쿠키 스폰(괴물은 건너뜀) / PaintPhaseController.Awake: IsPaintScene=true
  GamePhaseStarter(방장): PaintPhaseEndTime이 캐시에 없으면 새로 기록(대기실 없는 테스트 경로용 — R4.7-7)
  색칠 60초: PlayerPaintCanvas → 내 몸 콜라이더 하나만 레이캐스트 → 스탬프를 로컬 RT(512²)에 GL 쿼드로 그림
             → 1/15초마다 묶어서 PaintStroke 이벤트(Others). 같은 색 15스탬프 이상이면 슬롯 등록(최대 4)
             → RegisteredSlotCount를 자기 Player Props에 보고
             PaintColliderUpdater: 칠하는 중·커서가 몸 근처일 때만 0.2초 간격으로 포즈를 굽고, 물리 쿠킹은 Job 워커 스레드
             ColorSlotPanel·PaintCountdown·BrushCursorController가 GamePhaseState로 표시 여부 결정
  색칠 종료(방장, 같은 프레임에 각자):
    ├ PaintPhaseController: 슬롯 0개 쿠키에게 색 배정(한 번만) → ForcedPaintActorNumbers/Colors → 대상 본인이 전신 강제 도포(ForceFill 스탬프)
    └ MonsterJoinController: MonsterJoined=1, GameEndTime = 지금 + 600초(한 번만)
  괴물: 카운트다운 종료 → 혼자 LoadLevel(GameStartAuthority.CurrentMapScene()) → PUN이 큐 재개 → 쌓인 이벤트(쿠키 Instantiate·스트로크·Props) 재생
        → MonsterJoined를 보고 MonsterPlayer 스폰 → curScn이 현재 씬과 같아지면 AutomaticallySyncScene=true, KeepAlive 복구
  생존 600초:
    괴물 이동: 카메라 기준 이동 + 이동 방향으로 회전(Rigidbody), 공중에서는 벽 방향 속도 성분 제거(CharacterGroundDetector)
    촉수 돌진(Shift): 매 물리 스텝 지면 법선을 따라 꺾은 속도(80m/s × 0.25초), 지면 붙이기, 2스텝 연속 막히면 종료, 경로 SphereCast로 쿠키 포착
    처형: 근접 구 트리거(Enter/Stay) 또는 돌진 경로 → MonsterGrabKillTrigger.TryGrabKill
      → MonsterController.PlayGrabKill(상태 동기화로 전원에 GrabKill 애니메이션)
      → RequestGrabKill RPC(RpcTarget.All, 괴물 ViewID)
         모든 클라이언트: 발신자 = 괴물 주인 확인 → CookieLifeStatePresenter.BeginGrabKill(충돌 끄기, Grab_Socket 추종, 가루 색 비동기 샘플)
         피해자 본인만: HitCount=2 기록, 그랩 해제, 키네마틱, Held 애니메이션
      → 각 클라이언트가 자기 화면의 괴물 Animator 진행도로 눌림 → 가루 → 축소 연출, 부서지는 순간 숨김, 본인은 관전 모드
    쿠키끼리: F로 가까운 쿠키를 들기(OnGrabbedByOwner RPC) — 들린 쪽이 캐리어 소켓을 로컬로 따라감(소유권 이전 없음)
  GameRuleController(방장): 쿠키 전원 파괴 → MonsterWins / GameEndTime 경과 → CookiesWin → GameResult 기록(한 번만)
  괴물 전원 이탈: RoomLifecycleWatcher → MonsterDepartedAt → 5초 배너 → 대기실 복귀
  ResultScreenController: (괴물 승이면 처형 연출이 끝날 때까지 최대 5초 대기) 결과 표시 → 12초 뒤 방장이 LoadLevelForRoom(GameLobbyScene)
        │
GameLobbyScene 복귀 → RoundStateResetter가 이전 판 흔적 삭제(GameMapScene 포함) → 다음 판(직전 맵은 방장 로컬 static으로 기억)
```

**채팅**(GameLobbyScene·게임 씬): Enter 떼기로 열고/보내고, 열려 있는 동안 `PlayerInput.IsGameplaySuppressed=true` → 이동·점프·회피·그랩·상호작용·돌진·관전 전환 키가
모두 중립값. 마우스(시점 회전·색칠)는 막지 않는다. 입력창 밖을 클릭하면 닫힌다.

### R3.2 네트워크 계약

**Room Props**(모두 방장만 쓴다. `NetKeys.Scopes`에 대상·수명 선언, Round 키는 대기실 복귀 때 삭제 — EditMode 테스트 2개가 선언 누락을 잡는다)

| 키 | 타입 | 쓰는 곳 | 읽는 곳 |
|---|---|---|---|
| MonsterActorNumbers | int[] | MonsterAssignmentAuthority, RoomLifecycleWatcher | RoomState 전반, 배너, 스폰, 방장 정책, 결과, 승패 |
| MonsterRevealTime | double | MonsterAssignmentAuthority | (쓰기만 함 — R5-5) |
| MonsterSelectDeadline | double | MonsterAssignmentAuthority | 대기실 상태 문구 |
| PaintPhaseEndTime | double | GameStartAuthority, (예비) GamePhaseStarter | GamePhaseState, 괴물 대기, 강제 도포, 합류, 판 흔적 판정 |
| GameMapScene | string | GameStartAuthority | 괴물 대기(`CurrentMapScene`) |
| MonsterJoined | int | MonsterJoinController | GamePhaseState, 괴물 스폰, 판 흔적 판정 |
| ForcedPaintActorNumbers / ForcedPaintColors | int[] | PaintPhaseController | PlayerPaintCanvas(본인) |
| GameEndTime | double | MonsterJoinController | 승패 판정, 생존 카운트다운 |
| MonsterDepartedAt | double | RoomLifecycleWatcher | 이탈 배너, 방장 교체 시 인계, 판 흔적 판정 |
| GameResult | int | GameRuleController | 결과 화면, 단계 판정 |
| DoorStates | Hashtable{문ID:byte} | InteractableDoor(방장) | InteractableDoor(전원) |
| curScn(PUN 내부) | string | PUN `LoadLevel` | MonsterJoinController(R5-4) |

**Player Props**(본인만 쓴다): `HitCount`(Round), `RegisteredSlotCount`(Round), `SkinIndex`(Session).

**RaiseEvent**

| 코드 | 이벤트 | 대상 | 발신자 검증 |
|---|---|---|---|
| 1 | PaintStroke(스탬프 묶음) | Others, 캐시 없음 | ❌ viewId만 비교(🆕 R4.10-7) |
| 2 | ClaimMonster(가마솥) | 방장 | ❌ 신청자 번호를 `CustomData`에서 읽음(🆕 R4.10-7) |
| 3 | ClearColor(리셋) | Others | ❌ viewId만 비교 |
| 4 | (예약, 비움) | | |
| 5 | StartGameRequest | 방장 | ✅ `photonEvent.Sender`를 호스트와 비교 |
| 6 | DoorStateRequest | 방장 | △ 문 ID·값 범위만 검사, 거리 검사 없음 |

**RPC**

| RPC | 대상 | 발신자 검증 |
|---|---|---|
| `HideOrSeekPlayer.OnGrabbedByOwner(int carrierViewId)` | 들릴 쿠키 주인 | ❌ 없음, 이미 들렸는지도 확인 안 함(R4.10-1) |
| `HideOrSeekPlayer.OnReleased(bool)` | 들린 쿠키 주인 | ❌ 없음(R4.10-1) |
| `HideOrSeekPlayer.RequestGrabKill(int monsterViewId, info)` | 전원 | ✅ 괴물 PhotonView 주인 == 발신자(`Unit/HideOrSeekPlayer.cs:113-114`) |
| `GameManager.LogMsg(string, bool, info)` | 전원(씬 PhotonView, GameManager·RoomExitController 공용) | 채팅이라 불필요 |

**PhotonView 직렬화**: 공용 `NetworkTransformSync<TState>`(위치·회전·상태 int). 쿠키는 캐리 여부 bool을 더 붙인다(`PlayerNetworkSync`). 원격 복사본은 키네마틱 +
보간(10m 이상이면 스냅). 괴물 GrabKill 진행도는 **동기화하지 않고** 각 클라이언트 Animator에서 읽는다. 씬 PhotonView ID는 `MapSceneBuilder`가 맵 씬마다 채운다(1·3).

### R3.3 도메인별 동작 요약

**Core** — 모두 정적 클래스·순수 데이터·순수 C# 협력 클래스. `GamePhaseState.Evaluate`·`RoomState.DesiredMasterActor`·`GameSettingsSO.PickGameMap`(그리고 Monster 도메인의
`MonsterTentacleDash.DirectionOnGround`) 등은 네트워크와 분리된 순수 함수라 테스트된다. `RoomSceneTransition`은 `OpRemoveCompleteCache` → `LoadLevel` 순서로 이전 씬 Instantiate 캐시를 지운다.
`CharacterGroundDetector`(🆕 Core로 이동)는 접지 레이캐스트, 지면 SphereCast, `OnCollisionStay` 접촉에서 벽 법선을 모아 공중 수평 속도의 벽 방향 성분을 뺀다.

**Unit(쿠키)** — `HideOrSeekPlayer`(416줄)가 조정자(입력 Update / 물리 FixedUpdate). 협력 객체: `PlayerAnimationDriver`(트리거 전환·점프 정점 정지·Carry 레이어),
`PlayerCarryFollower`(들린 쪽이 캐리어 소켓을 로컬로 따라감), `PlayerNetworkSync`, Core의 `CharacterGroundDetector`. 이동 잠금 = 파괴 ∨ 들림(채팅은 `PlayerInput`이 담당).
처형 RPC 수신 시 Monster 도메인의 `MonsterController`를 직접 조회한다(R4.1-8).

**Monster** — `MonsterController`(404줄)는 입력·물리·애니메이션·촉수 돌진(속도 기반 물리 스텝·지면 탐침·막힘 판정)·돌진 경로 처형 판정·리스폰·
처형 연출 진행도 값 7개를 맡는다. `MonsterTentacleDash`(순수 C#)가 쿨다운·시간·방향·막힘 판정. `MonsterGrabKillTrigger`는 처형 판정 단일 진입점. 나머지 9개 클래스는 판 진행·관전 담당이다.

**ColorTag** — `PlayerPaintCanvas`(616줄)가 512² RenderTexture에 GL 쿼드로 스탬프를 그린다(잠금은 하드웨어 블렌딩). `SampleSurfaceColors`가 캔버스·스킨을 64²로 줄여
`AsyncGPUReadback`으로 읽고 샘플 UV마다 표면 색을 만든다. `CookieLifeStatePresenter`(274줄)는 생존/파괴 표시(0.5초 주기 재조정 + 콜백)와 처형 연출(붙잡힘 → 눌림 → 가루 → 축소)을 함께 맡는다.

**Interaction / Environment** — `CharacterInteractor`(DDOL)가 0.1초마다 로컬 캐릭터 주변 가장 가까운 `IInteractable`을 찾는다. `InteractableDoor`는 방장 요청 → `DoorStates` 방식,
큐가 멈춘 클라이언트는 로컬 적용. `CauldronSplash`는 트리거로 Animator Splash 레이어를 잠깐 켜는 연출 전용(네트워크 없음).

**GameManager / Lobby** — 채팅(`GameManager`), 스폰(`PlayerSpawner`), 퇴장(`RoomExitController`), 낙하 복귀(`FallGuard`·`VoidKillZone` — `IRespawnable` 계약), 확인창.
Lobby는 방 목록(`LobbyController`), 대기실 패널(`GameLobbyController` — 목록·상태 문구·호스트 시작 버튼 + 시작 요청 수신), 시작 권한(`GameStartAuthority`, 정적), 스킨 선택.

---

## R4. 11개 관점 감사

### R4.1 기존 책임 분리를 무시하는 코드 — ⚠ 일부 있음

**R4.1-1 ✅ 채팅 이동 잠금이 쿠키 타입에 묶여 있던 문제(해결).**
`Core/PlayerInput.cs:31` `IsGameplaySuppressed`를 모든 게임플레이 키 프로퍼티가 먼저 확인한다. `GameManager`는 이 값만 켜고 끄며(`GameManager.cs:69-86`),
캐릭터 종류를 알지 않는다. 괴물 이동·돌진, 관전 전환, 상호작용, 그랩이 모두 함께 멈춘다. 교차 도메인 참조 `GameManager → HideOrSeekPlayer`도 사라졌다(부록 A).

**R4.1-2 ⏸ 채팅 매니저가 전역 설정을 바꾼다.** `GameManager/GameManager.cs:32-33` — `Time.timeScale = 1`, `IsMessageQueueRunning = true`. 같은 큐 재개가
`PlayerSpawner.cs:65`, `RoomExitController.cs:53`에도 있다(부록 C). 큐 정지를 설계의 일부로 쓰는 프로젝트(괴물 대기실 대기)에서 채팅 컴포넌트가 큐를 여는 것은 의도를 흐린다.
지금은 괴물 대기 중 GameManager를 끄므로(`behavioursToDisableWhileWaiting`) 충돌하지 않지만, 순서가 바뀌면 괴물이 큐를 연 채 대기실 이벤트를 받게 된다.
- 권장: 큐 재개는 PUN `LoadLevel`의 자동 재개와 `RoomExitController.OnLeftRoom`(복구) 두 곳으로 한정하고, `timeScale` 대입은 지운다(아무도 0으로 만들지 않는다).

**R4.1-3 ⏸ UI 패널이 네트워크 권한 처리를 맡는다.** `Lobby/GameLobbyController.cs:98-103` — 대기실 **UI 프리팹**이 `IOnEventCallback`으로
`StartGameRequest`를 받아 `GameStartAuthority.TryStart`를 실행한다. `LobbyController.cs:30-31, 45`는 Photon 전역 초기화(`AutomaticallySyncScene`·`GameVersion`·`SerializationRate`)를 한다.
- 권장: 시작 요청 수신은 `GameStartAuthority`와 짝을 이루는 씬 컴포넌트(예: `GameStartRequestReceiver`, MonsterManagers에 배치)로, Photon 초기화는 `NetworkBootstrap`으로.

**R4.1-4 ⏸ 캐릭터 조정자가 다른 도메인을 직접 호출한다.**
- 관전 진입: `Unit/HideOrSeekPlayer.cs:143`(연출이 없을 때), `ColorTag/CookieLifeStatePresenter.cs:176`(연출이 끝날 때) — 두 곳(R4.11-17).
- 카메라: `HideOrSeekPlayer.cs:166`, `Monster/MonsterController.cs:141` — `Camera.main`의 `Camera_Ctrl`에 자신을 넘긴다.
- 운영 코드 → **Dev**: `GameManager/PlayerSpawner.cs:48` `OfflineModeBootstrap.SpawnAsMonster`.
- `Unit/PlayerCarryFollower.cs:38, 55` → `PlayerGrabController.SetCollisionIgnored`(정적 유틸이 컴포넌트 클래스에 있음).
- 권장: 로컬 캐릭터 등장을 `CharacterRegistry`의 정적 이벤트(`LocalCharacterChanged`)로 알리고 카메라가 구독, 개발용 플래그는 `SpawnPolicy` 같은 Core 인터페이스 뒤로.

**R4.1-5 ⏸ 퇴장 컨트롤러가 채팅 RPC를 빌려 쓴다.** `RoomExitController.cs:39`가 GameManager의 PhotonView와 `GameManager.RpcLogMsg`로 보낸다
(같은 GameObject 배치 제약 — GameSceneCore·GameLobbyScene 모두 지켜짐).

**R4.1-6 ⏸ 폴더와 실제 책임이 어긋난다.** `Monster/` 19개(1,742줄) 중 9개(GameRuleController, ResultScreenController, PlayerResultRow, RoundStateResetter, MasterClientPolicy,
RoomLifecycleWatcher, GamePhaseStarter, SpectatorController, SpectatorLabel)는 판 진행·관전이다. `ColorTag/CookieLifeStatePresenter`는 색칠과 무관한 처형 연출을 맡고,
`GameManager/`의 FallGuard·VoidKillZone·ConfirmDialog는 채팅과 무관하다.
- 권장: `Round/`, `Spectator/`, `World/`, `Cookie/` 폴더로 재배치(파일 이동은 `.meta` GUID를 유지하므로 씬·프리팹 참조가 깨지지 않는다).

**R4.1-7 ⏸ MonsterController의 책임이 계속 늘어난다(악화).** 282 → 319 → **404줄**. 이번에 돌진 물리 스텝(`FixedUpdateDash` `:256`), 지면 탐침(`TryProbeGround`),
돌진 경로 처형 판정(`TryCatchCookieOnDashPath` `:321`), 거리 비교기 클래스가 들어왔다. 쿠키는 같은 종류의 책임을 협력 객체로 나눴다.
- 권장: `MonsterDashMotor`(FixedUpdateDash·TryProbeGround·ReportProgress 연동), `MonsterGrabKillPresenter`(진행도 공개 값) 두 협력 객체로 분리. `MonsterTentacleDash`와 짝을 이룬다.

**R4.1-8 ⏸ 처형 연출 데이터가 괴물 컨트롤러에 있고, 쿠키 표시 컴포넌트가 그것을 직접 읽는다.**
- `Monster/MonsterController.cs:31-43, 80`: 블렌더 프레임에서 뽑은 쿠키 분쇄 진행도 5개(눌림 시작·끝, 축소 시작, 가루, 소멸) + `GrabSocket`·`GrabKillDuration`·`GrabKillProgress`.
- `ColorTag/CookieLifeStatePresenter.cs:53, 99-118`이 `MonsterController` 필드를 들고 위 값을 매 프레임 읽는다. `Unit/HideOrSeekPlayer.cs:113`도 `GetComponent<MonsterController>()`.
- 영향: 괴물 종류가 늘거나 처형 모션이 바뀌면 세 클래스를 함께 고쳐야 하고, 쿠키 연출 튜닝이 괴물 프리팹에 직렬화된다.
- 권장: `GrabKillPresentationSO`(진행도 5개 + 크기 배율 2개) + 괴물 쪽 `IGrabKillSource { Transform GrabSocket; float Progress; float Duration; GrabKillPresentationSO Presentation; }`.

**R4.1-9 🆕 입력 억제가 단일 bool이라 억제 원인이 둘 이상이 되면 서로 덮어쓴다(낮음).** `Core/PlayerInput.cs:31`은 `set`이 공개된 bool이고 지금은 채팅 하나만 쓴다.
확인창·일시정지 메뉴·결과 화면이 같은 억제를 원하게 되면 한쪽이 끄는 순간 다른 쪽의 억제도 풀린다.
- 권장: `PlayerInput.Suppress(object owner)` / `Release(object owner)`(집합 또는 카운트)로 바꾸고, `IsGameplaySuppressed`는 읽기 전용으로.

### R4.2 Manager 간 의존성 과도 증가 — ⚠ 주의(동일)

씬 매니저끼리의 직접 참조는 여전히 5곳뿐이다.

| 참조 | 방식 |
|---|---|
| Cauldron → MonsterRevealController | 인스펙터 참조(`ShowAgain`) |
| MonsterLobbyWaitController → GameManager, Back 버튼 | 인스펙터 `Behaviour[]`, `Button[]`(GameLobbyScene 배선 확인) |
| RoomExitController → GameManager | RPC 이름 상수 + 같은 PhotonView |
| GameLobbyController → GameStartAuthority | 정적 호출(`IsReady`, `RequestStart`, `TryStart`) |
| MonsterLobbyWaitController → GameStartAuthority | 정적 호출(`CurrentMapScene`, `:92`) — 괴물 대기가 Lobby 도메인의 시작 권한 클래스를 안다 |

**처형 한 건을 처리하는 캐릭터 컴포넌트 결합(⏸ 그대로)**

```
MonsterGrabKillTrigger ──PlayGrabKill()──▶ MonsterController
MonsterController ──ResetTrigger()/TryGrabKill()/IsOnCooldown/ReachCenter──▶ MonsterGrabKillTrigger        (양방향)
MonsterGrabKillTrigger ──RPC──▶ HideOrSeekPlayer.RequestGrabKill ──GetComponent──▶ MonsterController
HideOrSeekPlayer ──BeginGrabKill()──▶ CookieLifeStatePresenter ──읽기(진행도 7개)──▶ MonsterController
CookieLifeStatePresenter ──SampleSurfaceColors()──▶ PlayerPaintCanvas
CookieLifeStatePresenter / HideOrSeekPlayer ──EnterSpectatorMode()──▶ SpectatorController
ResultScreenController ──GetComponent──▶ CookieLifeStatePresenter.IsBeingGrabKilled
```

- 쿨다운 소유자가 둘이다: 트리거의 `onCooldown` 플래그를 컨트롤러의 `grabKillRemaining` 타이머가 푼다(`MonsterController` Update → `grabKillTrigger.ResetTrigger()`).
- 결과 화면(판 진행)이 쿠키 프리팹의 연출 컴포넌트 상태를 폴링한다(`Monster/ResultScreenController.cs:71`).
- 권장: 처형을 `GrabKillSession`(순수 C#) 하나로 모델링해 괴물 측 쿨다운·타이머를 한곳에 두고, 쿠키·결과 화면은 `CharacterRegistry`와 정적 이벤트(`GrabKillStarted/Finished`)로만 관찰.

**암묵 결합(⏸ 그대로)**: 마스터 전용 Room Props 폴링 Update가 **8개**다(부록 D). `PaintPhaseEndTime` 경과 순간 `PaintPhaseController`(강제 도포)와
`MonsterJoinController`(합류)가 같은 프레임에 각자 쓴다. `GamePhaseState`가 **해석**은 모았지만 **전이**는 흩어져 있다. 이번에 응답 대기 플래그가 모두 갖춰져 중복 전송은 사라졌지만,
대신 같은 “요청 플래그 + 캐시 확인 + 방장 교체 시 초기화” 코드가 9벌이 됐다(R4.11-19).
- 권장(규모가 커질 때): 마스터 전용 `RoundDirector` 하나가 단계 전이를 순서대로 수행하고, 방장 쓰기는 `MasterPropertyWriter`(CAS) 하나로.

### R4.3 Prefab과 Script의 역할 뒤섞임 — ⚠ 일부 있음(크게 개선)

| # | 내용 | 근거 | 상태 |
|---|---|---|---|
| 1 | 코드가 프리팹 자식 이름을 안다 | `Unit/HideOrSeekPlayer.cs:202` `Mesh_0` | ⏸ |
| 2 | 애니메이터 트리거·상태 이름 = enum `ToString()` / 문자열 | `PlayerAnimationDriver.cs:28-29, 46`(`Jump`), `:54`(`Carry`), `MonsterController.cs:391-392`, `CauldronSplash`의 상수 3개 | ⏸ |
| 3 | 클립 길이를 이름 키워드로 찾음 | `MonsterController.cs:169`(`GrabKill`·`TentacleDash`), `InteractableDoor.cs:256`(`Close`) | ⏸ |
| 4 | 같은 렌더러를 두 컴포넌트에 따로 배선 | `PlayerSkinApplier.bodyRenderer` ↔ `PlayerPaintCanvas.bodyRenderer` | ⏸ |
| 5 | 같은 UI의 프리팹판과 씬판 | 사용 중인 ColorSlotPanel은 GameSceneCore 1벌. `Resources/UI/Scene/ColorSelectionPanel.prefab`은 참조 0건 | 🔁 R5-15 |
| 6 | 빌더가 만든 프리팹 직렬화가 코드보다 오래됨 | `Witch_Cookie_House.prefab`에 옛 필드 `zoneWidth` 4개 | ⏸ |
| 7 | 런타임에 UI를 코드로 생성 | `CharacterInteractor` + `InteractionPromptUI.Create` | ⏸ |
| 8 | 스폰 겹침 검사 크기가 쿠키 캡슐 상수 | `Core/SpawnPositionFinder.cs:10-12`(괴물 스폰에도 같은 크기) | ⏸ |
| 9 | 게임 매니저 세트를 씬마다 복제 | `GameSceneCore.prefab` + 테스트 `GameScenes_UseSingleSceneCorePrefab` | ✅ |
| 10 | 괴물 프리팹에 쿠키 연출 튜닝값 직렬화 | R4.1-8 | ⏸ |
| **11** | **테스트 씬에 네트워크 캐릭터 프리팹을 씬 오브젝트로 배치** | 아래 | 🆕 |
| **12** | **GameSceneCore에 쓰이지 않는 PhotonView** | 아래 | 🆕(낮음) |

**R4.3-11 🆕 PlayerTestScene에 `HideOrSeekPlayer`(활성)·`MonsterPlayer`(비활성) 프리팹 인스턴스가 씬 오브젝트로 들어 있다.**
- YAML: `HideOrSeekPlayer` 인스턴스 `m_IsActive=1`, `sceneViewId=2` / `MonsterPlayer` `m_IsActive=0`, `sceneViewId=4`. 이 프리팹들은 원래 `PhotonNetwork.Instantiate`로만
  만들어지는 **플레이어 소유** 네트워크 객체인데, 씬에 두면 **룸 뷰**(방장 소유)가 된다.
- 같은 씬의 GameSceneCore에 `PlayerSpawner`가 있으므로 방에 들어가면 쿠키가 하나 더 스폰된다. 오프라인에서는 방장 = 나라서 룸 뷰의 `IsMine`이 true가 될 수 있고,
  그러면 **씬에 놓인 쿠키도 `PlayerInput`을 읽어 함께 움직이고**, `Camera_Ctrl`·`PlayerPaintCanvas.Local`을 마지막 `Awake/Start`가 가져간다(코드 추론, 재현 안 함).
  과녁(더미)으로 둔 것이라면 의도와 다르게 동작할 수 있다.
- 이 씬은 맵 빌더의 복제 원본이기도 하다(R4.4-8). 빌더가 테스트 전용 루트를 지우므로 현재 맵 씬 5개에는 들어가지 않았다(확인함).
- 권장: 과녁이 필요하면 입력을 읽지 않는 `DummyCookie` 프리팹(네트워크 없음, `IGameCharacter`만 구현)을 쓰고, 네트워크 캐릭터 프리팹은 씬에 두지 않는다.

**R4.3-12 🆕 `GameSceneCore/GameRuleManagers`의 PhotonView는 쓰는 곳이 없다(낮음).** 이 오브젝트의 컴포넌트 6개(GamePhaseStarter, MonsterJoinController, GameRuleController,
RoomLifecycleWatcher, PaintPhaseController, MasterClientPolicy)는 RPC를 보내거나 받지 않는다(`MonoBehaviourPunCallbacks`는 PhotonView가 필요 없다). 맵 빌더가 씬마다 ViewID(3)를
배정하므로 해가 되지는 않지만, “이 오브젝트에 RPC가 있다”는 오해를 준다. 지우거나 용도를 주석으로 남긴다.

**GameSceneCore 자체에 대한 메모** — 3차 판은 매니저(`GameSystems`)와 HUD(`GameHUD`) 두 프리팹을 권했지만 하나로 합쳤다. 한 판 단위로 함께 쓰이는 묶음이라
지금 규모에서는 합리적이다. 맵마다 HUD를 바꾸고 싶어지면 그때 나눈다.

UI 프리팹을 `Resources/`에 두는 폴더 규칙(CLAUDE.md)을 따르지만 런타임 로드는 InteractionPromptUI뿐이라, 나머지 7개는 빌드에 무조건 포함될 이유가 없다(⏸).

### R4.4 Scene에 직접 의존하는 코드 — ⚠ 주의

| # | 의존 | 위치 | 상태 |
|---|---|---|---|
| 1 | `GameObject.Find(이름)` | `Core/SceneSpawnPoints.cs:13`, `GameManager/PlayerSpawner.cs:56`, `Monster/MonsterJoinController.cs:72`, `Dev/MonsterTestSpawner.cs:24` — 모두 스폰 지점 | ⏸ |
| 2 | `Camera.main` + `Camera_Ctrl` 전제 | HideOrSeekPlayer:164·277, MonsterController:134·355, SpectatorController:45, PlayerPaintCanvas:127·233, BrushCursorController:66, PlayerBillBoard:21·22 — 10회 | ⏸ |
| 3 | 컴포넌트 존재로 씬 판별 | `PaintPhaseController.IsPaintScene`(`:16, 27, 32`) — GameSceneCore에 있으므로 맵 5개 + PlayerTestScene이 색칠 씬 | ⏸ |
| 4 | PUN 내부 키 문자열 | `MonsterJoinController.cs:18` `curScn` | ⏸ |
| 5 | 문 ID 기본값 = GameObject 이름 | `InteractableDoor.DoorId` | ✅ 맵 씬은 빌더가 고유 ID를 채움 |
| 6 | 씬 이름 상수 | `Core/SceneNames.cs`(Lobby, GameLobby 두 개만) | ⏸ 한 곳에 모여 있음 |
| 7 | 빌드에 없는 씬을 대체값으로 사용 | `SceneNames.Game` 제거, `PickGameMap`이 `null` 반환 + 테스트 `GameMaps_MatchEnabledBuildMapScenes` | ✅ |
| **8** | **맵 빌더가 테스트 씬 내용에 의존** | 아래 | 🆕 |

**R4.4-8 🆕 `MapSceneBuilder`의 복제 원본이 PlayerTestScene이다.** `Editor/Maps/MapSceneBuilder.cs`의 `TemplateScene = "Assets/Scenes/PlayerTestScene.unity"`.
카메라·스폰 지점·낙하 영역을 이 씬에서 복제하고 “테스트 전용 루트와 시험용 환경”을 이름으로 지운다. 테스트 씬은 개발자가 자유롭게 바꾸는 곳인데, 그 내용이 **다음 맵 빌드 결과**를 결정한다.
테스트용 오브젝트를 새로 넣고 빌더의 삭제 목록에 넣지 않으면 맵 씬에 그대로 복제된다(R4.3-11의 네트워크 캐릭터가 대표적 후보).
- 권장: 복제 원본을 전용 `Assets/Scenes/Templates/GameMapTemplate.unity`(카메라·스폰 지점·낙하 영역·GameSceneCore만)로 분리하고, PlayerTestScene은 이 템플릿 + 테스트 도구로 만든다.

새 코드(입력 억제·돌진 물리)는 씬 탐색을 늘리지 않았다. `FindObjectsByType`/`FindObjectOfType` 호출은 **0건**이다.

---

### R4.5 Singleton 남발 — ✅ 양호(가변 정적 상태 14곳)

`public static X Instance` 형태는 **없다**. 가변 정적 상태는 14곳(부록 B, 3차 13곳 + `PlayerInput.IsGameplaySuppressed`). 불변 정적 값(셰이더 ID·해시·비교기 인스턴스·설정 캐시 경로)은 세지 않았다.

- **숨은 DDOL 싱글톤**(⏸): `Interaction/CharacterInteractor.cs:15, 22` — `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`로 모든 씬(LobbyScene 포함)에 자동 생성·유지.
  쓸 캐릭터가 없는 로비에서도 0.1초마다 레지스트리를 훑는다(비용은 작다).
- **정적 “현재 로컬” 참조**(⏸, 적절): `ColorTag/PlayerPaintCanvas.cs:45, 126` `Local` — `Start`에서 설정, `OnDestroy`에서 해제. 스와치·도구 버튼·붓 커서·슬롯 패널이 씬 탐색 없이 쓴다.
- **방을 옮겨도 남는 정적 상태**: `InteractableDoor.masterPendingWrites`(⏸ R4.10-3), `GameStartAuthority.lastStartTime`(무해), `GameStartAuthority.lastMap`(⏸ `Lobby/GameStartAuthority.cs:83`).
  `lastMap`은 판을 시작한 방장 클라이언트에만 있어 방장이 바뀌면 직전 맵을 모르고, 방을 옮겨도 남는다. 영향은 작다.
  - 권장: 직전 맵을 `Session` 수명 Room Prop(`LastGameMap`)으로(`NetKeys.Scopes`에 한 줄).
- **🆕 `PlayerInput.IsGameplaySuppressed`**(`Core/PlayerInput.cs:31`): 소유자(GameManager)가 `CloseChat`·`OnDisable`·`OnDestroy`에서 반드시 되돌리므로 씬을 넘어 새지 않는다.
  Enter Play Mode 옵션이 도메인 리로드를 하므로 에디터 세션 사이에도 남지 않는다. 다만 원인이 여럿이 되면 R4.1-9.

### R4.6 ScriptableObject의 책임 — ✅ 양호

| SO | 역할 | 평가 |
|---|---|---|
| GameSettingsSO | 규칙 수치 + 순수 계산 4개(`PaintStrokeSendIntervalFor`, `MonsterCountFor`, `PickGameMap`, `FirstGameMap`) | 적절. `OnValidate`가 맵 목록이 비면 경고. 🆕 에셋 직렬화가 코드보다 오래됨(R5-16) |
| InputBindingsSO | 키 설정 | 읽기 전용. 런타임 키 변경 시 복제본 필요(주석에 언급) |
| ColorPaletteSO | 색 목록 | ✅ `Count`·`GetColor`·`GetColorName`이 null·범위 밖을 방어. ⏸ 주석 “10개 고정”(`:6`)은 사실이 아님(스와치는 `ColorSwatchGroup`이 개수에 맞춰 생성) |
| BrushSettingsSO | 붓 크기·커서 | ⏸ `CreateAssetMenu` 경로가 `ColorTag/…`로 다른 SO(`TagOfChaos/…`)와 다름 |
| SkinCatalogSO | 스킨 목록 | `ClampIndex`로 네트워크 인덱스 방어. 적용(`PlayerSkinApplier`)과 선택 UI(`PlayerSkinSelector`)가 같은 목록을 씀 |

런타임 상태를 SO에 쓰는 곳은 **없다**(모든 SO 필드가 `private` + 읽기 전용 프로퍼티). 설정이 없으면 `GameSettings`·`PlayerInput`이 기본값 인스턴스를 만들고 경고한다.
SO로 옮길 후보는 그대로다: 처형 연출 진행도(R4.1-8), `ResultScreenController.MaxGrabKillWait`(`:48`, 코드 상수), `CookieLifeStatePresenter`의 `SquashHeightScale`/`SquashWidthScale`,
`HideOrSeekPlayer`의 질주 배율 1.3·회피 배율 2(코드 상수).

### R4.7 Unity Lifecycle 순서 — ⚠ 일부 있음

이미 해결된 것(현재 코드로 재확인): 협력 객체·캔버스·스탬프 머티리얼을 `Awake`에서 생성(`HideOrSeekPlayer.Awake`, `PlayerPaintCanvas.cs:108`), 스킨(`Awake`) → 합성 머티리얼(`Start`) 순서,
씬 로드 직후 `InRoom=false` 대응(코루틴 3곳·첫 프레임 Update 2곳·`OnJoinedRoom` 보강), CanvasGroup 숨김 통일, 카메라 `InitCamera`/`Start` 호출 순서 무관화,
처형 연출의 `LateUpdate` 소켓 추종(Animator가 본을 움직인 뒤).

**R4.7-1 ⏸ `PaintPhaseController.IsPaintScene` 정적 bool.** `Awake` true / `OnDestroy` false(`:16, 27, 32`). 비동기 씬 로드에서 새 씬 `Awake`와 이전 씬 `OnDestroy`의 순서에 기대는 구조다.
맵 → 맵 직행 전환이 없어(항상 대기실 경유) 드러나지 않지만, “다음 판 바로 시작”을 만들면 새 맵에서 false로 덮일 수 있다.
- 권장: 카운터(`Awake`에서 ++, `OnDestroy`에서 --) 또는 “현재 인스턴스” 참조.

**R4.7-2 ⏸ `ConfirmDialog`가 `Awake`에서 자기 자신을 끈다**(`GameManager/ConfirmDialog.cs:19`). 비활성으로 배치하면 첫 `Show()`가 `Awake`를 부르며 곧바로 다시 꺼진다.
GameSceneCore·GameLobbyScene 모두 활성 배치라 지금은 동작한다.

**R4.7-3 ✅ UnityEngine.Object에 `?.`(해결, 잔여 2곳).** `PlayerGrabController`는 `self != null`로 바뀌었다. 남은 곳: `Unit/HideOrSeekPlayer.cs:143`,
`ColorTag/CookieLifeStatePresenter.cs:176`의 `GetComponent<SpectatorController>()?.EnterSpectatorMode()`. 에디터에서는 `GetComponent`가 “가짜 null” 객체를 돌려줄 수 있어
컴포넌트가 없으면 `?.`를 통과해 `MissingComponentException`이 난다. 두 프리팹 모두 컴포넌트가 있어 지금은 안전하다. `TryGetComponent`로 바꾸면 된다.

**R4.7-4 ⏸ 원격 복사본의 첫 프레임 물리.** 두 캐릭터 루트 Rigidbody 기본값이 비키네마틱이고, 원격은 `Start()`에서야 키네마틱이 된다
(`HideOrSeekPlayer.cs:179`, `MonsterController.cs:147`). 생성 프레임의 물리 스텝에서 원격 쿠키가 중력을 받을 수 있다. 프리팹 기본값을 키네마틱으로 두고 로컬만 `Awake`에서 푸는 편이 안전하다.

**R4.7-5 ✅ 들고 있던 쿠키가 사라지면 캐리 정리가 실행되지 않던 문제(해결).** `Unit/PlayerGrabController.cs:29` `HasCarryReference`(ReferenceEquals)로 참조 존재와 생존을 구분한다.

**R4.7-6 ⏸ 처형 중인 원격 쿠키의 Transform을 두 컴포넌트가 매 프레임 쓴다.** 원격 클라이언트에서 처형 중인 쿠키는 `HideOrSeekPlayer.Update`가 보간하고(`:230` —
원격의 `IsMovementLocked`(`:51`)는 로컬 `hitCount`가 0이라 false), 같은 프레임 `CookieLifeStatePresenter.LateUpdate`(`:99`)가 소켓 위치·크기로 덮어쓴다.
결과는 맞지만 누가 Transform의 주인인지가 실행 순서에만 달려 있다.
- 권장: `IsMovementLocked`(또는 별도 `IsPresentationDriven`)에 `lifePresenter.IsBeingGrabKilled`를 포함.

**R4.7-7 🆕 게임 씬 첫 프레임에 `GamePhaseStarter`가 방장 자신의 시작 기록을 아직 못 본 채 `PaintPhaseEndTime`을 다시 쓸 수 있다(코드 추론, 미재현).**
- 흐름: 방장이 대기실에서 `GameStartAuthority.TryStart`로 `{PaintPhaseEndTime = T0+3+60, GameMapScene}`을 보내고 곧바로 `LoadLevel(map)`을 호출한다.
  PUN 기본(`BroadcastPropsChangeToAll=true`)에서는 **서버 응답 전까지 로컬 캐시에 반영되지 않는다**(이 프로젝트의 여러 주석이 전제하는 동작, `GameStartAuthority.cs` `DuplicateGuardSeconds` 주석 등).
  `LoadLevel`은 로드 동안 메시지 큐를 멈추고, 씬이 로드된 뒤 큐를 되살린다. PUN은 기본 설정에서 들어온 메시지를 `FixedUpdate`에서 처리한다.
- 게임 씬 `GameSceneCore/GameRuleManagers`의 `GamePhaseStarter.Update`(`Monster/GamePhaseStarter.cs`)는 방장이고 방에 있고 **캐시에 `PaintPhaseEndTime`이 없으면**
  `PaintPhaseEndTime = 지금 + 60`을 새로 쓴다. 로드 직후 프레임에 물리 스텝이 한 번도 돌지 않으면 응답 처리보다 `Update`가 먼저 실행돼 이 조건이 성립한다.
- 영향: 쿠키 쪽 색칠 종료 시각이 “대기실 기록(여유 3초 포함)”에서 “게임 씬 도착 + 60초”로 바뀐다. 대기실 괴물은 이미 첫 값을 받고 큐를 멈춰 **옛 시각**으로 카운트다운하므로,
  로딩이 3초보다 길면 괴물이 색칠 종료 **전에** 도착하고(합류 기록까지 스폰 대기), 짧으면 늦게 도착한다. 판이 깨지지는 않지만 설계한 동기화가 어긋난다.
- 3차 판은 “정상 흐름에서는 아무 일도 하지 않는다”로 봤고, 이번 판에서 이 컴포넌트가 GameSceneCore를 통해 **맵 씬 5개 모두**에 들어가 있으므로 다시 따졌다.
- 근거(PUN 소스): `Assets/Photon/PhotonUnityNetworking/Code/PhotonHandler.cs:154-177`(기본값에서는 `FixedUpdate`에서만 `Dispatch`), `PhotonNetwork.cs:801`(`MinimalTimeScaleToDispatchInFixedUpdate = -1`),
  `PhotonNetwork.cs:3069-3071`(`LoadLevel`이 큐를 멈추고 비동기 로드).
- 확인 방법: 방장 클라이언트에서 게임 씬 도착 직후 `PaintPhaseEndTime`을 로그로 남겨 대기실에서 쓴 값과 비교한다.
- 권장: 대기실을 거친 판에서는 동작하지 않도록 조건을 좁힌다(예: `PhotonNetwork.OfflineMode`일 때만, 또는 PlayerTestScene에만 두는 직렬화 플래그 `onlyWithoutLobby`).
  또는 쓰기를 `expectedProperties: { PaintPhaseEndTime: null }` CAS로 보내 이미 값이 있으면 서버가 거절하게 한다.

### R4.8 Event 구독/해제 — ✅ 양호

| 이벤트 | 구독 | 해제 | 평가 |
|---|---|---|---|
| `SpectatorController.SpectateTargetChanged`(static) | `SpectatorLabel.OnEnable` | `OnDisable` | 짝 맞음. 발행자 파괴 시 null 통지(`SpectatorController.OnDestroy`) |
| Photon 콜백·`IOnEventCallback` | `MonoBehaviourPunCallbacks.OnEnable` | `OnDisable` | 상속 25개 중 OnEnable/OnDisable 재정의 6개(HideOrSeekPlayer, MonsterController, InteractableDoor, BrushCursorController, PlayerPaintCanvas, GameManager) 모두 `base` 호출 |
| `Button.onClick.AddListener`, `TMP_InputField.onDeselect` | 각 `Awake`/`Start` | 없음 | 버튼·입력창과 수명이 같아 누수 없음. `PlayerSkinSelector`는 재사용 버튼에 `RemoveAllListeners` 후 등록 |
| 레지스트리(Character/Interactable) | `OnEnable` | `OnDisable` | 짝 맞음. 순회 중 Destroy 시 먼저 복사(`MonsterLobbyWaitController.RemoveOtherPlayersAvatars`) |
| `AsyncGPUReadback.Request` 콜백 | `PlayerPaintCanvas.SampleSurfaceColors` | 1회성 | 콜백 안에서 임시 RT를 `ReleaseTemporary`. 쿠키가 먼저 파괴돼도 관리 객체 필드에만 쓰므로 예외 없음 |
| Job(`PaintColliderUpdater`) | `Tick`에서 Schedule | `Dispose`에서 `Complete` | `PlayerPaintCanvas.OnDestroy`가 `Dispose` 호출 — 누수 없음 |

주의(⏸): `MonsterLobbyWaitController`가 대기 중 `GameManager.enabled = false`로 끄면 GameManager의 Photon 콜백 등록도 풀린다(재정의한 콜백이 없어 무해, 채팅은 `OnDisable`에서 닫힘).

주의(⏸): 문 26개(ChocolateFactory)·22개(HauntedBakery)가 **각자** `MonoBehaviourPunCallbacks` + `IOnEventCallback`이라 `DoorStates` 변경·`DoorStateRequest`·방장 교체마다
26번 호출되고 그중 하나만 자기 ID를 처리한다. 사물이 늘면 `DoorStateRouter` 하나가 받아 ID로 분배하는 편이 낫다(정적 `masterPendingWrites`도 그 인스턴스 필드로 — R4.10-3과 함께).

🆕 주의(낮음): Photon 콜백을 **하나도 쓰지 않는데** `MonoBehaviourPunCallbacks`를 상속한 클래스가 4개다 — `BrushCursorController`(`OnDisable`만 재정의),
`ColorSelectionPanel`, `GamePhaseStarter`, `GameManager`(`OnDisable`만 재정의, RPC는 `PhotonView`로 보냄). 모두 `PhotonNetwork.AddCallbackTarget`에 등록돼 콜백마다 호출 대상이 되고,
읽는 사람은 “어떤 콜백을 쓰나” 찾게 된다. `MonoBehaviour`로 바꾸면 된다(`OnDisable`의 `override`/`base` 호출만 정리).

---

### R4.9 Object Pool과 Instantiate/Destroy 충돌 — ✅ 충돌 없음

`IPunPrefabPool`·커스텀 풀은 **없다**(검색 결과 0). 네트워크 객체는 PUN `DefaultPool`(Resources + Instantiate/Destroy)이고 씬 전환으로 파괴된다.
“풀에서 꺼낸 객체를 Destroy” 같은 충돌은 구조적으로 없다. 풀을 도입한다면 아래 로컬 `Destroy`(⏸ R4.9-1)를 함께 바꿔야 한다.

| 생성/파괴 지점 | 빈도 | 평가 |
|---|---|---|
| `PhotonNetwork.Instantiate` — `PlayerSpawner.cs:69`, `MonsterJoinController.cs:80`, `MonsterTestSpawner.cs:31` | 씬당 1회 | 적절 |
| `CookieLifeStatePresenter.cs:153` 가루 `Instantiate(breakVfxPrefab)` | 처형 1회당 1개 | 파티클 `stopAction: Destroy`로 자동 정리. 한 판 최대 (쿠키 수)회라 풀 불필요 |
| `BrushCursorController.cs:40` 커서 1개 | 씬당 1회 | `OnDestroy`에서 정리(`:32`) |
| `MonsterLobbyWaitController.cs:111` 다른 쿠키 로컬 `Destroy` | 판당 1회 | ⏸ R4.9-1 — 원 주인이 이미 씬 전환으로 파괴한 네트워크 객체를 로컬로 치움(의도). 풀 도입 시 `PhotonNetwork.Destroy` 경로와 충돌할 수 있으니 함께 바꿀 것 |
| `GameLobbyController.cs:117, 121` 참가자 목록 전부 파괴 후 재생성 | 인원·호스트 변경마다 | ⏸ R4.9-2 — 같은 프레임에 두 번 불리면(예: 입장 콜백 + Update 안전망) 첫 호출이 만든 행을 곧바로 파괴·재생성하는 낭비. `UiListBuilder.Sync` 재사용 권장. `Update`의 `RoomState.HostActor()`(`:55`)가 **매 프레임 int 배열 할당** |
| `LobbyController.cs:83, 97, 105` 방 목록 항목 | 방 목록 변경마다 | 방 이름 키 사전으로 재사용 — 적절 |
| `ResultScreenController.cs:94, 114` 결과 행·아이콘 | 판당 1회 | `isShown` 가드로 중복 없음 |
| `UiListBuilder.Sync`(스와치 10개·스킨 버튼) | 씬당 1회 | 기존 자식 재사용, 모자라면 복제, 남으면 파괴(템플릿은 숨김) — 적절 |
| `PlayerPaintCanvas` RenderTexture·머티리얼 4개, `PaintColliderUpdater` 메시 2개 | 쿠키 생성마다 | `OnDestroy`에서 모두 해제. 단, **대기실 쿠키에도 512² RT를 만든다**(색칠이 없는 씬, ⏸) — 인원이 늘면 `IsPaintScene`일 때만 지연 생성 |
| `InteractionPromptUI.cs:26` | 앱당 1회(DDOL) | 적절 |

### R4.10 Photon Ownership/RPC 구조 — ⚠ 일부 있음

잘 지켜지는 것: Room Props는 방장만(12개 키), Player Props는 본인만(3개 키), **파괴 확정은 피해자 본인만**, 문 상태는 방장 요청 방식,
괴물 스폰은 괴물 본인이 `PhotonNetwork.Instantiate`, 씬 전환은 방장만(`RoomSceneTransition`), 맵 이름은 시작 신호와 **한 요청으로 원자적 기록**,
방장 쓰기는 모두 서버 응답 전 재전송을 막는다(✅ R4.10-6), 방장 권한 정책(`MasterClientPolicy`)이 괴물에게 진행 권한을 주지 않는다.

**R4.10-1 ⏸ 그랩에 수락 절차가 없다(실제 결함).**
- 드는 쪽은 RPC를 보내자마자 `carriedPlayer = target`으로 확정한다(`Unit/PlayerGrabController.cs:46-61`). 대상이 이미 들려 있는지·무언가를 들고 있는지 확인하지 않는다.
- 들리는 쪽 `OnGrabbedByOwner`(`Unit/HideOrSeekPlayer.cs:76`)는 `IsMine`·`IsBroken`만 보고, 이미 들려 있어도 캐리어를 덮어쓴다(`PlayerCarryFollower.TryAttach`).
- `OnReleased`(`:83`)는 보낸 사람이 현재 캐리어인지 확인하지 않는다. 두 RPC 모두 `PhotonMessageInfo`를 받지 않는다.
- 재현(코드 경로): A가 C를 든다 → B가 C를 든다(C는 B를 따라감) → A가 놓는다 → C가 B에게서 떨어진다 → B는 빈손으로 캐리 자세·충돌 무시 상태에 갇힌다.
  또 B가 C를 든 채 A가 B를 들면 C → B → A **연쇄 캐리**가 된다.
- 권장: 들리는 쪽이 `IsCarried`이거나 자신이 무언가를 들고 있으면 거절하고 결과를 회신(`OnGrabResult(bool)` RPC — 드는 쪽은 회신을 받은 뒤에 `carriedPlayer` 확정),
  `OnReleased`·`OnGrabbedByOwner`는 `PhotonMessageInfo`를 받아 `info.Sender`가 캐리어 PhotonView의 주인일 때만 처리.

**R4.10-2 ✅ 처형 RPC 발신자 검증(유지).** `Unit/HideOrSeekPlayer.cs:109-118` — 괴물 ViewID로 PhotonView를 찾고 `monsterView.Owner != info.Sender`면 무시.
설계 메모(유지): 파괴 확정이 **피해자 클라이언트 권한**이라 수정된 클라이언트는 처형 RPC를 무시해 무적이 될 수 있다. 방장 판정과의 트레이드오프이며, 현재 선택(소유권 원칙)을 문서로 명시해 둘 가치가 있다.

**R4.10-3 ⏸ 문 상태의 방장 대기 캐시가 정적이다.** `Environment/InteractableDoor.cs:31` `static masterPendingWrites`는 `DoorStates` 삭제(`:170`)·방장 교체(`:178`) 때만 비워진다.
응답 전에 방을 나가 다른 방을 만들면 이전 방의 값이 새 방의 첫 쓰기에 섞인다(과자집 문 ID는 대기실 공통이라 대기실 문에서 드러난다).
- 권장: `OnLeftRoom`에서도 비우기(또는 R4.8의 `DoorStateRouter` 인스턴스 필드로).

**R4.10-4 ⏸ 퇴장 시 Player Props를 API 없이 지운다.** `RoomExitController.cs:44`가 `LocalPlayer.CustomProperties.Remove(key)`로 로컬 사본만 지운다(방을 떠난 뒤 다음 방으로 가져가지 않게 하려는 의도 — 주석에 있음).
다음 방에 입장할 때 옛 `HitCount`가 함께 전송돼 잠시 파괴 상태로 보이는 것을 막는 역할이 있으므로 지우면 안 된다. 다만 PUN 내부 캐시를 직접 고치는 방식이라 PUN 업데이트 때 동작을 다시 확인해야 한다(주석에 명시 권장).

**R4.10-5 ⏸ PaintStroke 이벤트는 캐시되지 않는다.** 시작 때 방을 닫으므로 정상 흐름에선 문제없다. 재접속 기능을 넣으면 캔버스 스냅샷 전송이 필요하다.

**R4.10-6 ✅ 방장 쓰기에 응답 대기 가드가 없던 곳(해결).** `GameRuleController.resultRequested`(`:55`), `PaintPhaseController.resolveRequested`(`:59, 62`),
`MonsterAssignmentAuthority.deadlineClearRequested`(`:55`). 방장 교체 시 플래그를 풀고 새 방장이 캐시로 다시 판단한다. CAS(`expectedProperties`)는 아직 쓰지 않는다(R4.11-19).

**R4.10-7 🆕 RaiseEvent 3종이 발신자를 검증하지 않는다.**
- **PaintStroke·ClearColor**: `ColorTag/PlayerPaintCanvas.cs:466-470`은 `CustomData[0]`의 viewId가 자기 것인지만 본다. `photonEvent.Sender`가 그 캐릭터의 주인인지 확인하지 않으므로
  아무 클라이언트나 남의 쿠키 몸에 칠하거나 지울 수 있다. 변장(색칠)이 승패에 직결되는 게임이라 영향이 작지 않다.
- **ClaimMonster**: `Monster/Cauldron.cs:31`이 자기 ActorNumber를 **내용**으로 보내고, `Monster/MonsterAssignmentAuthority.cs:23-35`가 `CustomData`의 번호를 괴물로 확정한다.
  `photonEvent.Sender`를 쓰면 “다른 사람을 괴물로 지목”이 불가능해지고 내용도 필요 없다.
- **DoorStateRequest**: 문 ID·값 범위만 검사하고 요청자와 문 사이 거리는 보지 않는다(원격으로 문을 여닫을 수 있음 — 영향 작음).
- 대조: `StartGameRequest`는 이미 `photonEvent.Sender`를 호스트와 비교한다(`Lobby/GameLobbyController.cs:102`) — 같은 방식을 나머지에도 적용하면 된다.
- 권장: 색칠은 `photonEvent.Sender == pv.OwnerActorNr`, 가마솥은 `photonEvent.Sender`로 신청자 결정, 문은 방장이 요청자 캐릭터와 문 사이 거리를 `interactionRange` + 여유로 확인.

### R4.11 중복 로직 — ⚠ 다수(개선)

| # | 중복 | 위치 | 상태 | 권장 |
|---|---|---|---|---|
| 1 | 스폰(Find → 겹침 회피 → Instantiate) 3벌, `MonsterPlayer` 이름 2벌 | `PlayerSpawner.cs:56-69`, `MonsterJoinController.cs:14, 72-80`, `MonsterTestSpawner.cs:10, 24-31` | ⏸ | `SceneSpawnPoints.TryFindClearPosition`(리스폰은 이미 사용) + `NetworkPrefabs` 상수 |
| 2 | InRoom 대기 코루틴 3벌 + 첫 프레임 Update 2벌 | GameManager, PlayerSpawner, MonsterTestSpawner / MasterClientPolicy, RoundStateResetter | ⏸ | `RoomState.WaitUntilInRoom()` |
| 3 | 클립 이름으로 길이 찾기 | `MonsterController.cs:169`, `InteractableDoor.cs:256` | ⏸ | `AnimatorUtil.FindClipLength` |
| 4 | 트리거 기반 상태 전환 | `PlayerAnimationDriver.ChangeState`, `MonsterController.ChangeState`(`:387`) | ⏸ | 제네릭 `TriggerStateAnimator<TState>` |
| 5 | 로컬 리지드바디 설정 / 낙하 리스폰 / 카메라 초기화 | HideOrSeekPlayer ↔ MonsterController | 🔁 지면·벽 판정은 `CharacterGroundDetector`로 공용화, 나머지 3개는 그대로 | 공용 `CharacterBody` |
| 6 | 파괴 여부 판정 | — | ✅ `RoomState.IsBroken` 하나 | — |
| 7 | 표시 이름(닉네임 없으면 `#번호`) 3벌 + 1곳 미적용 | `GameLobbyController.cs:181`, `MonsterRevealController.cs:74`, `SpectatorController.cs:132` / `ResultScreenController.cs:95`(빈 닉네임 그대로) | ⏸ | `RoomState.DisplayName(Player)` |
| 8 | 색칠 남은 시간 표시 2곳 | `ColorSelectionPanel.cs:34`(timeLabel) + `PhaseCountdownDisplay`(Paint) — 이제 GameSceneCore 한 곳에 둘 다 | ⏸ | 하나로 |
| 9 | 로컬 캐릭터 찾기 | `CharacterInteractor.cs:64` ↔ `CharacterRegistry.FindLocal<T>()`(`GameCharacter.cs:57`) — 🆕 **`FindLocal<T>`는 호출자가 0이 됐다**(채팅이 쓰던 것) | ⏸ | `CharacterInteractor`가 `FindLocal<IGameCharacter>()`를 쓰거나 `FindLocal` 삭제 |
| 10 | 레지스트리 클래스 2벌 | CharacterRegistry ↔ InteractableRegistry | ⏸ | 제네릭 `Registry<T>` |
| 11 | “바뀔 때만 CanvasGroup 갱신” | PhaseCountdownDisplay, ColorSelectionPanel, InteractionPromptUI | ⏸ | `CanvasGroupVisibility` 캐시 버전 |
| 12 | `IsPaintPhaseActive()` 한 줄 래퍼 | `BrushCursorController.cs:92`, `PlayerPaintCanvas.cs:410` | ⏸ | `GamePhaseState.IsPaintActive` 직접 사용 |
| 13 | 괴물 이탈 시 목록에서 빼기 | `MonsterAssignmentAuthority.cs:81` ↔ `RoomLifecycleWatcher.cs:13-23` | ⏸ | 배열 제거만 `RoomState.WithoutMonster` |
| 14 | `IsRoomFull()` 한 줄 래퍼 | `GameLobbyController.cs:136` | ⏸ | 직접 사용 |
| 15 | 게임 매니저·HUD 세트 씬 복제 | — | ✅ GameSceneCore | — |
| 16 | ColorSlotPanel 정의 여러 벌 | GameSceneCore 1 + 참조 없는 Resources 프리팹 1 | 🔁 | 옛 프리팹 삭제(R5-15) |
| 17 | 관전 진입 호출 2곳 | `HideOrSeekPlayer.cs:143`, `CookieLifeStatePresenter.cs:176` | ⏸ | 파괴 표시가 확정되는 한 곳(`CookieLifeStatePresenter`가 Broken으로 바뀔 때)으로 |
| 18 | 클립 진행도로 끝 판정 | `CookieLifeStatePresenter.LateUpdate`(GrabKill), `CauldronSplash.Update`(Splash), `PlayerAnimationDriver.HandleJumpAnimationHold`(Jump) | ⏸ | `AnimatorUtil.TryGetProgress(layer, hash)` |
| **19** | **방장 쓰기 “요청 플래그” 패턴 9벌** | 아래 | 🆕 | `MasterPropertyWriter` |

**R4.11-19 🆕 방장 쓰기 응답 대기 패턴이 9벌이다.** `MonsterAssignmentAuthority`(`deadlineRequested`·`confirmRequested`·`resetRequested`·`deadlineClearRequested`),
`MonsterJoinController.joinRequested`, `GameRuleController.resultRequested`, `PaintPhaseController.resolveRequested`, `MasterClientPolicy.requestedActor`,
`InteractableDoor.requestExpireTime`(+정적 `masterPendingWrites`). 모두 “보냈다 → 서버 응답(캐시 반영)이나 방장 교체까지 다시 보내지 않는다”는 같은 규칙을 손으로 구현했고,
언제 플래그를 푸는지(응답 도착, 방장 교체, 조건 해제)가 클래스마다 조금씩 다르다. 3차 판의 R4.10-6 결함도 이 패턴을 두 곳에서 빠뜨린 것이었다.
- 권장: `MasterPropertyWriter.TrySet(key, value, expectedPrevious)` — 내부에서 키별 “대기 중” 상태를 관리하고 `OnRoomPropertiesUpdate`·`OnMasterClientSwitched`로 풀며,
  가능하면 `expectedProperties`로 **서버 CAS**를 건다(방장 교체 경합까지 서버가 막는다). 새 방장 전용 쓰기를 추가할 때 빠뜨릴 여지가 없어진다.

---

## R5. 그 밖의 발견 사항

1. **R5-1 ✅ 채팅 토글이 키 떼기만 보던 문제(해결).** `GameManager.cs:27` 입력창 선택 해제 시 닫기.
2. **R5-2 ✅ `GamePhaseStarter` 쓰임(목적 회복).** PlayerTestScene도 GameSceneCore를 쓰므로 대기실 없는 경로에서 동작한다. 단, 맵 씬에서의 경합은 🆕 R4.7-7.
3. **R5-3 ✅ 빈 팔레트 예외(해결).** `PaintPhaseController`가 빈 팔레트를 한 번만 오류로 남기고 멈추며, `ColorPaletteSO`가 null·범위 밖을 방어한다.
4. **R5-4 ⏸ PUN 내부 키 의존.** `MonsterJoinController.cs:18` `curScn`. PUN 업데이트로 이름이 바뀌면 괴물의 씬 자동 동기화가 영원히 꺼진 채 남는다(다음 판에 방장을 따라가지 못함).
   PUN 업데이트 체크리스트에 올리거나, `SceneManager.sceneLoaded` 이후 일정 시간·조건으로 복구하는 대체 경로를 둔다.
5. **R5-5 ⏸ 쓰기만 하는 키.** `MonsterRevealTime` — 읽는 곳이 없다. 공지 연출 타이밍용으로 남긴 것이 아니라면 `NetKeys`에서 제거.
6. **R5-6 ⏸ 문 상태가 판마다 초기화된다.** `DoorStates`가 Round 수명. 맵 씬 문은 판마다 새 씬이라 자연스럽고, 대기실 과자집 문만 판이 끝날 때 닫힌다(의도라면 유지).
7. **R5-7 ✅ 방향 변경(유지)** — 큐가 멈춘 동안 문은 로컬에서만 여닫는다.
8. **R5-8 ⏸ 끝나지 않는 폴링.** `GameLobbyController.Update`의 `HostActor()` 배열 할당(매 프레임). `MonsterJoinController.Update`(`:22`)는 괴물이 아닌 클라이언트에서도
   **판 내내 매 프레임** `TryLocalSpawn` 조건(Props 조회 2번 + 배열 `Contains`)을 검사한다(`hasSpawnedLocally`가 괴물에게만 세팅됨).
   “괴물이 아님”이 확정되면(`MonsterJoined` 이후) 플래그를 세우면 된다.
9. **R5-9 테스트 범위.** `RuleTests.cs` **16개**: 방장·호스트 정책 3, 괴물 수 1, 스탬프 전송 간격 1, NetKeys 선언·수명 2, 맵 선택 1, **맵 목록 ↔ 빌드 씬 1**, **GameSceneCore 단일 사용 1**,
   **돌진 경사 방향 1**, 단계 해석 1, 스킨 인덱스 1, 동기화 왕복 2, **UI 레이아웃 화면 밖 검사 1**. 여전히 없는 것: 그랩 RPC 순서(R4.10-1), 이벤트 발신자 검증(R4.10-7),
   `GamePhaseStarter` 경합(R4.7-7), 테스트 씬에 네트워크 캐릭터 금지(R4.3-11 — `GameScenes_UseSingleSceneCorePrefab`와 같은 방식으로 YAML을 검사하면 된다).
10. **R5-10 (정정 유지)** — 3차 판의 “빌드에 있지만 선택되지 않는 맵 2개”는 조사 오류였다. `gameMapScenes` 5개와 빌드 맵 씬 5개가 일치하며, 이제 테스트가 이를 보증한다.
11. **R5-11 ⏸ 결과 화면 버튼은 방장이 아니면 카운트다운만 멈춘다.** `Monster/ResultScreenController.cs:132-136` — 비방장이 누르면 `StopAllCoroutines()`로 자기 카운트다운 표시가
    멈추고 아무 일도 일어나지 않는다(방장 쪽 12초가 끝나야 이동). 비방장에게는 버튼을 숨기거나 “방장 대기” 문구를 보여 주는 편이 낫다.
12. **R5-12 ✅ 결과 화면 타이밍 값이 씬마다 달라질 수 있던 문제(해결).** `autoReturnDelay=12`가 GameSceneCore 한 곳. `MaxGrabKillWait=5`는 코드 상수로 남음(R4.6).
13. **R5-13 🆕 입력 백엔드가 섞여 있다.** 게임 코드는 레거시 `Input`만 읽는데(`PlayerInput`), Input System 1.14.2가 설치돼 있고 `activeInputHandler: 2`(Both)이며,
    UI 입력 모듈은 GameSceneCore가 `InputSystemUIInputModule`, LobbyScene·GameLobbyScene이 `StandaloneInputModule`이다. 지금은 Both라 모두 동작하지만,
    - 씬에 따라 UI 클릭·포커스(채팅 입력창 포함)를 처리하는 시스템이 다르다.
    - “입력 API를 바꿀 때는 `PlayerInput.cs`만 고치면 된다”는 설계 전제가 UI 쪽에서는 이미 성립하지 않는다.
    - 권장: 하나로 정한다. 레거시 유지라면 GameSceneCore의 모듈을 `StandaloneInputModule`로, Input System 전환이라면 `PlayerInput` 내부를 Input Actions로 바꾸고 모든 씬 모듈을 통일.
14. **R5-14 🆕 회피 도중 들리면 회피가 얼었다가 내려진 뒤 이어진다(코드 추론).** `Unit/HideOrSeekPlayer.cs:318` 회피 시작 시 `speed *= 2f`, `:338` `DodgeOut`에서 `speed *= 0.5f`.
    회피 타이머는 `Update`의 `CheckDodgeInput`에서만 줄어드는데, 들리면 `IsMovementLocked`(`:51`)로 `Update`가 일찍 끝나 **타이머가 멈추고 `isDodge`가 true로 남는다**.
    내려진 뒤 `FixedUpdate.Move`가 `isDodge && keepMovingAfterDodge` 분기로 **옛 회피 방향으로 2배 속도**를 남은 시간만큼 적용한다.
    - 권장: 들림·파괴가 시작될 때 `DodgeOut()`을 호출하고, 회피 속도는 `speed`를 바꾸지 말고 `baseSpeed * DodgeSpeedMultiplier`로 계산한다(누적 오차도 사라진다).
15. **R5-15 🆕 참조 없는 UI 프리팹이 Resources에 남아 있다.** `Resources/UI/Scene/ColorSelectionPanel/ColorSelectionPanel.prefab`(ColorSelectionPanel + 스와치 10개) — 씬·프리팹 참조 0건,
    코드의 `Resources.Load` 대상도 아니다. `Resources` 폴더라 빌드에 무조건 포함된다. 삭제하거나 GameSceneCore의 ColorSlotPanel을 이 프리팹의 인스턴스로 바꿔 하나로 합친다.
16. **R5-16 🆕 `GameSettings.asset` 직렬화가 코드보다 오래됐다.** 삭제된 필드 `tentacleDashRadius: 0.4`가 남아 있고, 새 필드 `tentacleDashMaxSlope`·`tentacleDashGroundSnap`은 파일에 없다.
    Unity는 없는 필드에 코드 초기값(45°, 0.6m)을 쓰므로 동작은 같지만, 에셋 diff만 보고는 실제 값을 알 수 없고 옛 필드가 혼동을 준다. 인스펙터에서 한 번 저장(또는 `EditorUtility.SetDirty` + `SaveAssets`)하면 정리된다.
17. **R5-17 🆕 호출자가 없는 공개 API.** `CharacterRegistry.FindLocal<T>()`(`Core/GameCharacter.cs:57`) — 채팅이 `PlayerInput` 억제로 바뀌면서 마지막 호출자가 사라졌다(R4.11-9).

---

## R6. 권장 로드맵

**1단계 — 결함 수정(각 30분 이내)**
- 그랩 수락·놓기 발신자 검증·이미 들렸거나 들고 있는 쿠키 잡기 금지(R4.10-1)
- `GamePhaseStarter` 조건 좁히기 또는 CAS(R4.7-7) — 먼저 로그로 실제 덮어쓰기가 일어나는지 확인
- RaiseEvent 발신자 검증(색칠·리셋·가마솥, R4.10-7)
- 들림·파괴 시 `DodgeOut()`, 회피 속도 계산 방식 변경(R5-14)
- `InteractableDoor.masterPendingWrites`를 `OnLeftRoom`에서도 비우기(R4.10-3)
- `GetComponent<T>()?.` 2곳을 `TryGetComponent`로(R4.7-3)

**2단계 — 자산·설정 정리(1시간 이내)**
- PlayerTestScene의 네트워크 캐릭터 인스턴스 정리 + 맵 빌더 전용 템플릿 씬 분리(R4.3-11, R4.4-8), 이를 막는 EditMode 테스트 추가
- 참조 없는 `ColorSelectionPanel.prefab` 삭제(R5-15), `GameSettings.asset` 재저장(R5-16), 쓰이지 않는 PhotonView 정리(R4.3-12)
- 입력 백엔드 하나로 통일(R5-13)
- 콜백을 쓰지 않는 `MonoBehaviourPunCallbacks` 4개를 `MonoBehaviour`로(R4.8)

**3단계 — 책임 정리(각 1시간 이내)**
- 처형 연출 데이터를 `GrabKillPresentationSO` + `IGrabKillSource`로(R4.1-8, R4.2), 관전 진입 한 곳으로(R4.11-17), 원격 보간 정지(R4.7-6)
- `MonsterController`에서 돌진 이동·처형 연출 값을 협력 객체로 분리(R4.1-7)
- GameManager 전역 설정 대입 제거, 시작 요청 수신을 비 UI 컴포넌트로(R4.1-2, R4.1-3)
- 스폰·InRoom 대기·표시 이름·클립 길이·애니메이션 진행도 공용화(R4.11-1·2·3·7·18), `FindLocal` 정리(R4.11-9, R5-17)
- 대기실 목록 `UiListBuilder` 재사용 + `HostActor` 할당 제거(R4.9-2), 비괴물 클라이언트의 합류 폴링 종료(R5-8)
- 직전 맵을 Session Room Prop으로(R4.5), 입력 억제를 소유자 집합으로(R4.1-9)

**4단계 — 구조(판 규모가 커질 때)**
- `MasterPropertyWriter`(요청 플래그 + CAS) 하나로 방장 쓰기 통일(R4.11-19) → 이어서 마스터 전용 단계 전이를 `RoundDirector` 하나로(R4.2)
- 폴더 재배치 `Round/`·`Spectator/`·`World/`·`Cookie/`(R4.1-6), 도메인별 asmdef 분리(부록 A의 순환을 먼저 끊어야 함)
- 공용 `CharacterBody`(R4.11-5), 문 이벤트를 `DoorStateRouter` 하나로(R4.8), `IsPaintScene` 정적 bool을 카운터/참조로(R4.7-1)

---

## 부록

### A. 교차 도메인 의존성(주석 제외, Core 제외)

| 파일 | 참조하는 다른 도메인 클래스 |
|---|---|
| GameManager/GameManager.cs | (없음 — ✅ 3차 판의 HideOrSeekPlayer 참조 제거) |
| GameManager/PlayerSpawner.cs | **OfflineModeBootstrap(Dev)** |
| Lobby/PlayerSkinSelector.cs | SkinCatalogSO(Unit) |
| Monster/Cauldron.cs | HideOrSeekPlayer(Unit) |
| Monster/MonsterController.cs | Camera_Ctrl(Camera), HideOrSeekPlayer(Unit) |
| Monster/MonsterGrabKillTrigger.cs | HideOrSeekPlayer(Unit) |
| Monster/MonsterLobbyWaitController.cs | GameStartAuthority(Lobby) |
| Monster/ResultScreenController.cs | CookieLifeStatePresenter(ColorTag) |
| Monster/SpectatorController.cs | Camera_Ctrl(Camera) |
| Unit/HideOrSeekPlayer.cs | Camera_Ctrl(Camera), **SpectatorController(Monster)**, **MonsterController(Monster)**, CookieLifeStatePresenter(ColorTag) |
| Unit/PlayerSkinApplier.cs | **PlayerPaintCanvas(ColorTag)** |
| ColorTag/CookieLifeStatePresenter.cs | **MonsterController(Monster)**, SpectatorController(Monster) |
| ColorTag/ColorSwatchButton.cs, PaintToolButton.cs, BrushCursorController.cs, ColorSelectionPanel.cs | (ColorTag 내부 `PlayerPaintCanvas.Local`) |
| Environment/InteractableDoor.cs | IInteractable, InteractableRegistry(Interaction) |
| Interaction/CharacterInteractor.cs | (Core만) |

Core는 Photon·Unity 외에 어떤 도메인도 참조하지 않는다(단방향 의존 유지). Unit ↔ Monster ↔ ColorTag 사이의 **순환**은 그대로다
(Unit → Monster → Unit, ColorTag → Monster → ColorTag). 한 asmdef 안이라 컴파일은 되지만, 도메인별 asmdef로 나누려면 R4.1-8의 계약 분리가 먼저 필요하다.

### B. 가변 전역(static) 상태 목록

| 상태 | 위치 | 수명 | 비고 |
|---|---|---|---|
| `CharacterRegistry.characters` | Core/GameCharacter.cs:39 | 앱 | OnEnable/OnDisable |
| `InteractableRegistry.interactables` | Interaction/IInteractable.cs:24 | 앱 | 같음 |
| `CharacterInteractor.instance` | Interaction/CharacterInteractor.cs:15 | 앱(DDOL) | 자동 생성 |
| `PlayerPaintCanvas.Local` | ColorTag/PlayerPaintCanvas.cs:45 | 로컬 쿠키 수명 | Start 설정, OnDestroy 해제 |
| `PaintPhaseController.IsPaintScene` | ColorTag/PaintPhaseController.cs:16 | 씬 | R4.7-1 |
| `InteractableDoor.masterPendingWrites` | Environment/InteractableDoor.cs:31 | 앱 | **방을 옮겨도 남음**, R4.10-3 |
| `GameStartAuthority.lastStartTime` | Lobby/GameStartAuthority.cs:20 | 앱 | 3초 가드 |
| `GameStartAuthority.lastMap` | Lobby/GameStartAuthority.cs:83 | 앱 | 방장 교체 시 유실, 방을 옮겨도 남음(R4.5) |
| `SpectatorController.SpectateTargetChanged` | Monster/SpectatorController.cs:26 | 앱 | 이벤트 |
| `OfflineModeBootstrap.SpawnAsMonster` | Dev/OfflineModeBootstrap.cs:14 | 씬 | 개발용, OnDestroy에서 해제 |
| `GameSettings.cached` | Core/GameSettings.cs:9 | 앱 | SO 캐시 |
| `PlayerInput.bindings` | Core/PlayerInput.cs:9 | 앱 | SO 캐시 |
| `PlayerInput.IsGameplaySuppressed` 🆕 | Core/PlayerInput.cs:31 | 채팅이 열린 동안 | GameManager가 CloseChat·OnDisable·OnDestroy에서 해제(R4.1-9) |
| `SpawnPositionFinder.OverlapBuffer` | Core/SpawnPositionFinder.cs:14 | 앱 | 메인 스레드 전용 버퍼 |

(불변: `NetKeys.Scopes`/`RoundRoomKeys`/`RoundPlayerKeys`, 셰이더 프로퍼티 ID, Animator 해시, `RaycastHitDistanceComparer.Instance`, `NetworkTransformSync<T>` 정적 생성자 검사)

### C. 전역 네트워크·엔진 설정을 바꾸는 곳

| 설정 | 쓰는 곳 |
|---|---|
| `IsMessageQueueRunning` | false: MonsterLobbyWaitController:78 / true: GameManager:33, PlayerSpawner:65, RoomExitController:53, MonsterLobbyWaitController:97(맵 없음 복구) (+PUN LoadLevel 자동 정지·재개) |
| `AutomaticallySyncScene` | LobbyController:30(true), MonsterLobbyWaitController:52~(괴물=false), MonsterJoinController(복구) |
| `KeepAliveInBackground` | MonsterLobbyWaitController(연장), MonsterJoinController, RoomExitController:54(복구) |
| `SerializationRate`, `GameVersion` | LobbyController:45, :31 |
| `OfflineMode` | OfflineModeBootstrap |
| `Time.timeScale` | GameManager:32 |
| `Cursor.visible` / `Cursor.lockState` | BrushCursorController, Camera_Ctrl |
| `PlayerInput.IsGameplaySuppressed` 🆕 | GameManager |

### D. 마스터 전용 Update 폴링

| 컴포넌트 | 씬 | 조건 → 동작 | 응답 대기 가드 |
|---|---|---|---|
| MonsterAssignmentAuthority | GameLobby | 정원·기한 → 괴물 확정/초기화/기한 삭제 | ✅ 4개 플래그 |
| MasterClientPolicy | GameLobby + GameSceneCore | 입장 첫 프레임 → 방장 정책 | ✅ `requestedActor` |
| RoundStateResetter | GameLobby | 입장 첫 프레임 → 판 초기화 | ✅ 1회 |
| GamePhaseStarter | GameSceneCore(맵 5 + PlayerTestScene) | 캐시에 PaintPhaseEndTime 없음 → 기록 | ✅ `started` — 단, 첫 프레임에 **방장 자신의 대기실 기록이 캐시에 아직 없을 수 있음**(R4.7-7) |
| PaintPhaseController | GameSceneCore | 색칠 종료 → 강제 도포 배정 | ✅ `resolveRequested` + Room Prop 확인 |
| MonsterJoinController | GameSceneCore | 색칠 종료 → 합류·생존 종료 시각 | ✅ `joinRequested` + Room Prop 확인 |
| GameRuleController | GameSceneCore | Hunt 중 → 승패 판정 | ✅ `resultRequested` |
| RoomLifecycleWatcher | GameSceneCore | 괴물 전원 이탈 후 지연 → 대기실 복귀 | ✅ 1회 |
