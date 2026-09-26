# 조사 보고서: TagOfChaos 전체 동작 분석 + 아키텍처 11개 관점 감사 (2026-09-26, 커밋 `7e7feba`)

> **추가 개정(2026-09-27)**: 커밋 `7e7feba` 이후 작업 트리에 새로 들어온 **마녀의 과자집(Witch Cookie House)**
> 에셋 일체(Blender 원본·스크립트, FBX·머티리얼·애니메이션, 프리팹, 에디터 도구 2종)와 로비 배경 이미지,
> `PlayerTestScene` 수정을 조사해 **§G**에 추가했다. 런타임 스크립트(`Assets/02. Scripts/`)는 바뀌지 않았으므로
> §A~§F의 분석은 그대로 유효하다. §B의 구조 표에는 새 폴더를 반영했다.
>
> **추가 개정 2(2026-09-27 새벽)**: §G 작성 이후 새로 들어온 **과자집 소품 56종 + 레이아웃 407개 + 환경 통합 프리팹**,
> 소품용 에디터 도구 2종, 과자집 임포터 리팩터링을 조사해 **§H**에 추가했다. 이번에도 런타임 스크립트는 바뀌지 않았다.
>
> **추가 개정 3(2026-09-27)**: GameLobbyScene의 임시 환경(`LobbyEnvironment`, NavMesh 포함)을 `Witch_Cookie_Environment`로 교체하고
> 밤 조명(달빛·Trilight 주변광·안개·Point Light 2개)을 적용했다. 지면 충돌 문제(§H H4-1)는 대기실에 한해 씬의 40 × 40 m BoxCollider로 해결했다.
> 상세 계획·결과는 `Plan.md/GameLobbyScene.md` §8. GameScene은 아직 옛 바닥 그대로다.

> **개정 안내**: 이 문서는 이전 판(커밋 `7e7feba`에 들어 있는 research.md, §0~§12)을 **전면 대체**한다.
> 코드 주석 곳곳에 남아 있는 `research.md §8.x`, `§12 E1~E12` 같은 참조는 **이전 판의 절 번호**다.
> 해당 원문은 `git show 7e7feba:TagOfChaos/Plan.md/research.md`로 그대로 볼 수 있다.
> 이번 판의 절 번호는 이전 판과 겹치지 않도록 **A~F 접두어**를 쓴다.
>
> **조사 방법**
> - `Assets/02. Scripts/` 아래 **8개 폴더, 74개 `.cs`, 5,446줄을 줄 단위로 모두 읽었다**(에디터 스크립트 3개는 별도로 읽음). 작업 트리의 스크립트는
>   커밋 `7e7feba`와 같다(git status에서 바뀐 것은 폰트 에셋과 빌드 산출물뿐이다).
> - 씬 5개와 프리팹 10개의 YAML을 스크립트로 파싱해 **스크립트 GUID → 붙어 있는 GameObject 경로 → 직렬화된
>   필드 값**까지 따라갔다(레이어, 콜라이더, PhotonView 설정, 인스펙터 문구, UI onClick 바인딩 포함).
> - Photon PUN2 SDK 내부(`DefaultPool.Instantiate`, `PhotonNetwork.LoadLevel`, `MonoBehaviourPunCallbacks.OnEnable`,
>   `PhotonView.OnDestroy`, `LoadBalancingClient` 연결 끊김 처리)를 직접 읽고 코드의 가정과 맞춰 봤다.
> - 파일 경로는 따로 적지 않으면 `Assets/02. Scripts/` 기준이다. 예: `Monster/MonsterController.cs:122`.

---

## 목차

- **A. 요약**: 결론, 11개 관점 판정표, 우선 조치 Top 10
- **B. 프로젝트 구조**: 엔진·패키지·폴더·asmdef·씬·프리팹·SO
- **C. 동작 방식 상세**: 게임 흐름, 네트워크 계약, 도메인별 클래스 동작
- **D. 씬·프리팹 배선 실측**
- **E. 아키텍처 11개 관점 감사**(각 관점마다 판정, 근거 file:line, 영향, 권장)
- **F. 그 밖의 발견 사항 및 권장 로드맵**
- **G. 2026-09-27 추가·수정 사항**: 마녀의 과자집 에셋 파이프라인(Blender → FBX → Unity 에디터 도구 → 프리팹), 게임 통합 시 문제, 기타 변경
- **H. 2026-09-27 추가 2**: 과자집 소품 56종·레이아웃 JSON·환경 프리팹, 소품 에디터 도구, 발견한 문제(충돌 없는 지면, 레이아웃 이름·구역 오류, 재현 불가 파이프라인 등)
- **부록**: 의존성 표, 전역 상태를 바꾸는 곳, 정적 상태 목록

---

## A. 요약

### A.1 결론

이전 판들이 지적한 치명 결함(결과 화면 미표시, 색칠 UI 자기 비활성화, 붓 레이캐스트 차단, GrabKill 1회 제한,
괴물 카메라 부재 등)은 **현재 코드에서 모두 고쳐져 있다.** 한 판을 처음부터 끝까지 진행하는 흐름
(로비 → 대기실 → 괴물 선정 → 색칠 60초 → 괴물 합류 → 생존 10분 → 결과 → 대기실 복귀)은 코드상 모두 연결돼 있다.

아키텍처도 이전보다 크게 정리됐다. `RoomState`(조회), `NetKeys`(키와 수명 표), `GamePhaseState`(단계 해석),
`CharacterRegistry`/`IGameCharacter`(캐릭터 공통 계약), `PlayerInput`/`InputBindingsSO`(입력), `GameSettingsSO`(수치)가
**공용 허브** 역할을 하며, 고전적인 싱글톤은 하나도 없다. Room Props는 방장만, Player Props는 본인만 쓰는
**Photon 소유권 원칙도 대체로 잘 지키고 있다.**

남은 문제는 "망가진 기능"보다 **구조적 틈**에 가깝다. 대표적인 것은 다음과 같다.

1. **입력 잠금 책임이 쿠키 한 종류에만 붙어 있다.** 채팅창을 열어도 괴물은 WASD로 움직이고, Shift로 돌진하고,
   관전자는 Space로 대상을 바꾸고, 쿠키도 마우스로 색칠된다(§E.1.1). **실제 플레이에서 드러나는 버그**다.
2. **네트워크 메시지를 받는 쪽이 보낸 사람을 확인하지 않는다.** 괴물 신청 이벤트는 보낸 사람 대신 본문에 적힌
   ActorNumber를 믿고, 처형 RPC·그랩 RPC·색칠 이벤트도 보낸 사람을 검증하지 않는다(§E.10.2).
3. **쿠키끼리 그랩은 "보내자마자 확정"이라 두 명이 동시에 같은 쿠키를 잡으면 상태가 어긋난다**(§E.10.3).
4. **전역 네트워크 설정**(`IsMessageQueueRunning`, `AutomaticallySyncScene`, `KeepAliveInBackground`, `Time.timeScale`)을
   **매니저 6곳이 각자 바꾼다.** 채팅 매니저(`GameManager`)가 메시지 큐와 타임스케일까지 건드린다(§E.1.2).
5. **방장 전용 진행 로직 7개 컴포넌트**가 같은 "Update에서 폴링 + 서버 응답 전 중복 요청 방지 플래그" 패턴을 각자
   다시 구현한다. Photon의 CAS(`expectedProperties`)는 쓰지 않는다(§E.11.1).

### A.2 11개 관점 판정표

| # | 관점 | 판정 | 핵심 근거 |
|---|---|---|---|
| 1 | 기존 책임 분리를 무시하는 코드 | **주의** | 입력 잠금이 `HideOrSeekPlayer` 전용(§E.1.1), 채팅 매니저가 전역 네트워크 상태 변경(§E.1.2), UI 패널이 시작 요청 이벤트 수신(§E.1.3), 운영 코드가 Dev 도구에 의존(§E.1.4) |
| 2 | Manager 간 의존성 과다 | **양호~주의** | 대부분 정적 허브(`RoomState` 등) 경유라 직접 결합은 적다. 예외: `RoomExitController`→`GameManager`의 RPC와 PhotonView, `MonsterLobbyWaitController`→`GameManager`(인스펙터로 끄기), `Unit`↔`Monster` 폴더 간 순환 참조(§E.2) |
| 3 | Prefab과 Script 역할 혼재 | **주의** | 쿠키 프리팹 하나에 이동·색칠·관전·그랩·생존 표시 7개 역할(§E.3.1), 옛 `ColorSelectionPanel.prefab`이 씬 UI와 따로 놀며 남아 있음(§E.3.2), 쓰지 않는 씬 PhotonView 2개(§E.3.3), 밸런스 수치가 프리팹·SO·코드 상수에 흩어짐(§E.3.4) |
| 4 | Scene 직접 의존 증가 | **양호** | 씬 이름·스폰 지점 이름은 상수로 모였다. 남은 것: `GameObject.Find` 4곳 중 3곳이 헬퍼를 우회(§E.4.1), PUN 내부 키 `curScn` 의존(§E.4.2), 컴포넌트 존재를 씬 표식으로 쓰는 정적 플래그(§E.4.3), `Camera.main` 의존 10곳(§E.4.4) |
| 5 | Singleton 남발 | **양호** | 고전적 싱글톤(`Instance`) **0개**. 정적 상태 9종은 전부 목적이 분명하다. 수명 관리가 필요한 것은 2종(§E.5) |
| 6 | ScriptableObject 책임 오용 | **양호** | SO 5종 모두 읽기 전용 데이터이며 런타임에 수정하는 코드가 없다. 사소한 점: 팔레트가 인스펙터 5곳에서 따로 연결됨, `ColorPaletteSO.Count` null 방어 누락(§E.6) |
| 7 | Unity Lifecycle 순서 문제 | **양호(방어됨)** | Awake/Start/OnPhotonSerializeView 순서 문제를 이미 의식적으로 막아 두었다. 잠재 위험: `ConfirmDialog`가 Awake에서 스스로 꺼짐(§E.7.2), `PlayerPaintCanvas.Local`이 Start에서 등록돼 1프레임 공백(§E.7.3) |
| 8 | Event 구독/해제 | **양호** | Photon 콜백은 전부 `MonoBehaviourPunCallbacks`의 자동 등록/해제, C# 정적 이벤트 1개도 짝이 맞다. 단, 콜백을 하나도 쓰지 않으면서 콜백 대상으로 등록되는 클래스가 6개 있다(§E.8.2) |
| 9 | Object Pool과 Instantiate/Destroy 충돌 | **충돌 없음** | 커스텀 풀이 없고 PUN `DefaultPool`만 쓴다. `PhotonNetwork.Destroy`는 한 번도 쓰지 않는다. 나중에 풀을 도입하면 충돌할 지점 2곳(§E.9) |
| 10 | Photon Ownership/RPC 구조 무시 | **대체로 준수, 틈 있음** | 소유자만 자기 상태를 확정, Room Props는 방장만 기록. 틈: 보낸 사람 미검증 4곳(§E.10.2), 낙관적 그랩 확정(§E.10.3), "파괴됨" 상태 표현 4벌(§E.10.4) |
| 11 | 중복 로직 | **주의** | 방장 폴링 패턴 7벌(§E.11.1), 스폰 로직 3벌(§E.11.2), 캐릭터 이동 골격 2벌(§E.11.3), 색칠 남은 시간 UI 2개가 동시에 표시(§E.11.4), `PaintPhaseEndTime` 기록 2곳(§E.11.5) 등 |

### A.3 우선 조치 Top 10

| 순위 | 항목 | 분류 | 규모 | 절 |
|---|---|---|---|---|
| 1 | 채팅 중 괴물·관전·색칠·카메라 입력 차단 — 잠금을 `PlayerInput` 한 곳으로 | 버그 | 소 | §E.1.1 |
| 2 | `ClaimMonster`는 `photonEvent.Sender`를 사용, RPC·이벤트에 보낸 사람 검증 추가 | 네트워크 | 소 | §E.10.2 |
| 3 | 그랩을 "대상 확인 후 확정"으로 바꾸고, 이미 들린 쿠키에 대한 재부착 거부 | 네트워크 | 중 | §E.10.3 |
| 4 | 색칠 남은 시간이 두 번 표시되는 문제 — `ColorSelectionPanel.timeLabel`과 `PaintCountdown` 중 하나로 정리 | UI 중복 | 소 | §E.11.4 |
| 5 | 전역 네트워크 설정 변경을 `NetworkSessionPolicy`(가칭) 한 곳으로 모으기 | 책임 | 중 | §E.1.2 |
| 6 | 스폰 3곳을 `SceneSpawnPoints.TryFindClearPosition` + 프리팹 이름 상수로 통일 | 중복 | 소 | §E.11.2 |
| 7 | `PlayerSpawner`가 `OfflineModeBootstrap`(Dev)을 직접 참조하지 않게 분리 | 의존 | 소 | §E.1.4 |
| 8 | 방장 진행 로직의 "요청 중 플래그" 패턴을 공용 헬퍼나 CAS로 통일 | 중복/네트워크 | 중 | §E.11.1 |
| 9 | 콜백을 쓰지 않는 6개 클래스를 `MonoBehaviour`로 되돌리기 | 이벤트 | 소 | §E.8.2 |
| 10 | 옛 `ColorSelectionPanel.prefab`과 쓰지 않는 씬 PhotonView 정리 | 프리팹 | 소 | §E.3.2, §E.3.3 |

---

## B. 프로젝트 구조

### B.1 기술 스택

| 항목 | 값 |
|---|---|
| 엔진 | Unity 6000.0.x, Built-in RP |
| 네트워크 | Photon PUN2 (`Assets/Photon/`), `GameVersion = "1"`(`Lobby/LobbyController.cs:9`) |
| 입력 | 레거시 Input Manager. 모든 읽기는 `Core/PlayerInput.cs` 한 곳을 거친다. Input System 패키지와 `InputSystem_Actions.inputactions`는 있지만 쓰지 않는다 |
| Enter Play Mode | `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 0` → 도메인·씬 리로드는 **켜져 있다**(정적 상태가 플레이마다 초기화됨) |
| asmdef | `TagOfChaos.Scripts`(런타임 전체 1개), `TagOfChaos.Editor`, `TagOfChaos.EditorTests` |
| 장르 | 비대칭 술래잡기. 쿠키(기본 3명)는 60초 동안 자기 몸을 칠해 위장한 뒤, 괴물(기본 1명)에게서 600초를 버틴다 |

### B.2 폴더와 규모

| 폴더 | 파일 | 줄 | 역할 |
|---|---|---|---|
| `Core/` | 19 | 833 | 공용 계약과 허브: `RoomState`, `NetKeys`, `NetEventCodes`, `GamePhaseState`, `CharacterRegistry`/`IGameCharacter`, `IRespawnable`, `PlayerInput`, `GameSettings(SO)`, `InputBindingsSO`, `NetworkTransformSync<T>`, `SceneNames`, `SceneSpawnPoints`, `SpawnPositionFinder`, `RoomSceneTransition`, `UiListBuilder`, `CanvasGroupVisibility`, `PhaseCountdownDisplay`, `NetworkDefaults` |
| `Unit/` | 10 | 846 | 쿠키 캐릭터: `HideOrSeekPlayer`(조정자), 협력 클래스(`PlayerAnimationDriver`, `PlayerGroundDetector`, `PlayerCarryFollower`, `PlayerNetworkSync`), `PlayerGrabController`, `PlayerSkinApplier`, `PlayerBillBoard`, `SkinCatalogSO` |
| `ColorTag/` | 11 | 1,114 | 색칠: `PlayerPaintCanvas`(552줄, 최대 파일), `PaintColliderUpdater`(Job 쿠킹), `PaintPhaseController`(강제 도포), `BrushCursorController`, UI 4종, `CookieLifeStatePresenter`, SO 2종 |
| `Monster/` | 19 | 1,533 | 괴물 선정·이동·처형·합류, 방장 정책, 승패, 결과 화면, 이탈 처리, 관전 |
| `GameManager/` | 6 | 376 | 채팅(`GameManager`), 스폰(`PlayerSpawner`), 퇴장(`RoomExitController`), `ConfirmDialog`, 낙하 복귀(`FallGuard`, `VoidKillZone`) |
| `Lobby/` | 6 | 536 | 로비(`LobbyController`), 대기실 패널(`GameLobbyController`), 시작 권한(`GameStartAuthority`), 스킨 선택, 목록 항목 |
| `Camera/` | 1 | 138 | `Camera_Ctrl` 3인칭 궤도 카메라(쿠키·괴물·관전 공용) |
| `Dev/` | 2 | 70 | `OfflineModeBootstrap`, `MonsterTestSpawner` |
| `Editor/` | 3 | 약 290 | `UILayoutValidator`, EditMode 테스트 12개(`RuleTests`) |
| `Editor/WitchCookieHouse/` (2026-09-27 추가) | 4 | 825 | 과자집 임포트 규칙·빌더(§G.3), 소품 임포트 규칙(`WitchCookiePropsImportPostprocessor`)·소품/환경 프리팹 빌더(`WitchCookiePropsBuilder`) — §H.3 |

### B.3 씬 (빌드 순서)

| 씬 | 빌드 | 역할 |
|---|---|---|
| `LobbyScene` | ✅ 0 | 접속, 방 목록, 방 만들기와 입장 |
| `GameLobbyScene` | ✅ 1 | 대기실. 쿠키로 돌아다니다 가마솥에 들어가 괴물 신청, 스킨 선택, 시작 버튼. **괴물은 색칠 페이즈 동안 여기 남는다** |
| `GameScene` | ✅ 2 | 색칠 페이즈 → 술래잡기 → 결과 |
| `PlayerTestScene` | ❌ | 오프라인 개발 테스트 |
| `SampleScene` | ❌ | 비어 있음(프로젝트 스크립트 없음) |

### B.4 프리팹과 SO

| 에셋 | 위치 | 쓰임 |
|---|---|---|
| `HideOrSeekPlayer.prefab` | `04. Prefabs/Resources/` | `PhotonNetwork.Instantiate("HideOrSeekPlayer")` — 쿠키 |
| `MonsterPlayer.prefab` | `04. Prefabs/Resources/` | `PhotonNetwork.Instantiate("MonsterPlayer")` — 괴물 |
| `BrushCursor.prefab` | `04. Prefabs/Resources/` | `BrushSettingsSO.cursorPrefab`으로 **직접 참조**(Resources 로드는 하지 않음) |
| `PlayerResultRow.prefab` | `Resources/UI/Scene/PlayerResultRow/` (2026-09-27 `04. Prefabs/`에서 이동, GUID 유지) | 결과 화면 행 |
| `Resources/UI/**` 6종 | `Resources/UI/{Popup,Scene}/` | 씬에 프리팹 인스턴스로 배치. **`Resources.Load`로 불러오는 곳은 없다** |
| `GameSettings.asset`, `InputBindings.asset` | `Resources/` | `Resources.Load`로 전역 접근 |
| `DefaultColorPalette`, `DefaultBrushSettings` | `03. SO/ColorTag/` | 인스펙터 연결 |
| `SkinCatalog` | `03. SO/Unit/` | 인스펙터 연결 |
| `Witch_Cookie_House.prefab` (2026-09-27 추가) | `04. Prefabs/Environment/` | FBX 모델 프리팹 변형. **아직 어떤 씬에도 배치되지 않음** — §G |
| 과자집 원본 에셋 (2026-09-27 추가) | `09. Environment/WitchCookieHouse/` | FBX 1, 머티리얼 22, 텍스처 2, 애니메이션 클립 9 + 컨트롤러 5, README |
| 과자집 소품 (2026-09-27 추가 2) | `09. Environment/WitchCookieProps/`, `04. Prefabs/Environment/WitchCookieProps/` | FBX 56 + 프리팹 56(5개 카테고리), 머티리얼 32, 텍스처 2, `Witch_Cookie_Layout.json`(407개 배치) — §H |
| `Witch_Cookie_Environment.prefab` (2026-09-27 추가 2) | `04. Prefabs/Environment/` | 과자집 + 소품 407개를 조립한 환경 프리팹(중첩 프리팹 408개, 약 128 × 21 × 128 m). **씬 배치 없음** — §H |
| `LobbySceneBackGround.png` (2026-09-27 추가) | `08. Resources/` | 1673x940 PNG. **참조하는 씬·프리팹·머티리얼이 아직 없음** — §G.6 |

---

## C. 동작 방식 상세

### C.1 한 판의 전체 흐름

```
[LobbyScene]
  LobbyController.Awake: AutomaticallySyncScene=true, GameVersion
  Start: ConnectUsingSettings (SerializationRate = GameSettings.CharacterSyncRate)
  OnConnectedToMaster → JoinLobby → OnRoomListUpdate(목록을 diff로 갱신)
  방 만들기(MaxPlayers = GameSettings.MaxPlayers) / 입장 → OnJoinedRoom
    PlayerCount==1(방 생성자)만 LoadLevel(GameLobby), 나머지는 자동 씬 동기화

[GameLobbyScene]
  PlayerSpawner: InRoom이 될 때까지 기다린 뒤 쿠키 스폰(skipConfirmedMonster=false라 괴물도 쿠키로 스폰)
  RoundStateResetter: 첫 프레임에 본인 Round 키 삭제, 방장은 이전 판 흔적이 있으면 Round Room 키 삭제 + 방 재개방
  MasterClientPolicy: 방장 = "괴물이 아닌 사람 중 ActorNumber 최소"로 계속 맞춤
  MonsterAssignmentAuthority(방장): 정원이 차면 MonsterSelectDeadline = now + 30초
     ├ Cauldron.OnTriggerEnter(내 쿠키, 정원 찼음) → RaiseEvent(ClaimMonster → 방장)
     │   → 선착순 확정: MonsterActorNumbers, MonsterRevealTime
     └ 마감 시각 경과 → 남은 자리를 무작위로 채움
     └ 확정 후 정원 미달이 되면 선정 초기화
  MonsterRevealController: 배너("X가 괴물" / "당신이 괴물")
  MonsterLobbyWaitController(괴물 본인): AutomaticallySyncScene=false
  GameLobbyController: 호스트(최소 ActorNumber, 괴물이어도 됨)에게만 시작 버튼
     └ 누르면 GameStartAuthority.RequestStart
         방장이면 TryStart, 아니면 RaiseEvent(StartGameRequest → 방장)
         방장 TryStart: 요청자==호스트, 정원·괴물 확정 확인
           → PaintPhaseEndTime = now + 3초(로딩 여유) + 60초
           → IsOpen=false → OpRemoveCompleteCache → LoadLevel(Game)
  괴물: PaintPhaseEndTime 변경을 받으면 EnterWaiting
     → 다른 쿠키 아바타를 로컬에서 Destroy, 대기 UI, KeepAlive 늘림
     → IsMessageQueueRunning=false(이후 이벤트는 모두 쌓아 둠)
     → 로컬 실시간 시계로 카운트다운 → 0이 되면 혼자 LoadLevel(Game)

[GameScene] (쿠키들 먼저 도착)
  PlayerSpawner(skipConfirmedMonster=true): 쿠키 스폰
  PaintPhaseController.Awake: IsPaintScene=true
  색칠: PlayerPaintCanvas(로컬) — 스와치 선택 → 마우스 드래그 → 자기 MeshCollider만 레이캐스트
        → UV 스탬프를 로컬 RT에 GL로 그림, 묶어서 RaiseEvent(PaintStroke, Others)
        → 같은 색 15스탬프 이상이면 슬롯 등록(최대 4개), RegisteredSlotCount(Player Prop) 보고
  PaintPhaseController(방장): 페이즈 종료 시 슬롯 0개인 쿠키에게 색 배정
        → ForcedPaintActorNumbers/Colors(Room Prop) → 해당 쿠키가 전신 강제 도포
  MonsterJoinController(방장): 페이즈 종료 시 MonsterJoined=1, GameEndTime=now+600
  (괴물 도착 → PUN이 큐 재개 → 쌓인 Instantiate/스트로크/Props 재생
     → TryLocalSpawn으로 MonsterPlayer 스폰 → curScn이 현재 씬과 같아지면 AutomaticallySyncScene 복구)
  술래잡기:
     MonsterGrabKillTrigger(괴물 소유자): 구형 트리거에 쿠키 진입 → PlayGrabKill → RPC(RequestGrabKill → 쿠키 주인)
     MonsterController: Shift 촉수 돌진(20m, 0.25초, 쿨다운 15초), 돌진 경로 SphereCast로 쿠키 포착
     쿠키 주인: HitCount=2(Player Prop), 키네마틱, SpectatorController.EnterSpectatorMode
     CookieLifeStatePresenter(전원): HitCount를 보고 렌더러·콜라이더 끄기, BrokenCookie 레이어로 이동
     PlayerGrabController(쿠키끼리): E키로 들기/내려놓기(소유권 이전 없이 소켓을 로컬 추적)
  GameRuleController(방장): 쿠키 전원 파괴 → MonsterWins / GameEndTime 경과 → CookiesWin
  ResultScreenController(전원): 결과 표시, 12초 뒤 방장이 LoadLevelForRoom(GameLobby)
  RoomLifecycleWatcher(방장): 괴물이 모두 나가면 MonsterDepartedAt 기록 → 5초 뒤 대기실 복귀

[GameLobbyScene로 복귀] → RoundStateResetter가 판 상태 초기화 → 다음 판
```

### C.2 네트워크 계약

#### C.2.1 Room CustomProperties (쓰는 쪽은 모두 방장)

| 키 | 타입 | 쓰는 곳 | 읽는 곳 | 수명 |
|---|---|---|---|---|
| `MonsterActorNumbers` | int[] | `MonsterAssignmentAuthority`, `RoomLifecycleWatcher` | `RoomState` 전반, 방장 정책, 스폰, 결과 | Round |
| `MonsterRevealTime` | double | `MonsterAssignmentAuthority` | (기록만 하고 읽는 곳 없음) | Round |
| `MonsterSelectDeadline` | double | `MonsterAssignmentAuthority` | 같은 곳, `GameLobbyController` 상태 문구 | Round |
| `PaintPhaseEndTime` | double | `GameStartAuthority`(정상 경로), `GamePhaseStarter`(대비 경로) | `GamePhaseState`, 괴물 대기, 강제 도포, 합류 | Round |
| `MonsterJoined` | int | `MonsterJoinController` | `GamePhaseState`, 괴물 스폰 | Round |
| `ForcedPaintActorNumbers`/`Colors` | int[] | `PaintPhaseController` | `PlayerPaintCanvas` | Round |
| `GameEndTime` | double | `MonsterJoinController` | `GameRuleController`, 생존 카운트다운 | Round |
| `MonsterDepartedAt` | double | `RoomLifecycleWatcher` | 이탈 배너, 방장 교체 시 이어받기 | Round |
| `GameResult` | int | `GameRuleController` | `GamePhaseState`, `ResultScreenController` | Round |
| `curScn`(PUN 내부) | string | PUN `LoadLevel` | `MonsterJoinController.RestoreSceneSyncWhenCaughtUp` | — |

`MonsterRevealTime`은 쓰기만 하고 아무도 읽지 않는 키다(정리 후보).

#### C.2.2 Player CustomProperties (쓰는 쪽은 모두 본인)

| 키 | 쓰는 곳 | 수명 |
|---|---|---|
| `HitCount` | `HideOrSeekPlayer.RequestGrabKill`(피해자 본인) | Round |
| `RegisteredSlotCount` | `PlayerPaintCanvas.ReportSlotCount` | Round |
| `SkinIndex` | `PlayerSkinSelector.SelectSkin` | Session |

Round 키 목록은 `NetKeys.Scopes`(`Core/NetKeys.cs:59-74`)에서 자동으로 만들어지고, EditMode 테스트가 빠진 키를 잡는다.

#### C.2.3 RaiseEvent

| 코드 | 이름 | 보내는 쪽 → 받는 쪽 | 본문 | 수신 처리 |
|---|---|---|---|---|
| 1 | `PaintStroke` | 색칠하는 본인 → Others, Reliable | `{viewId, float[] u, v, radius, int[] color, byte[] kind}` | 모든 쿠키의 `PlayerPaintCanvas.OnEvent`가 viewId로 거른다 |
| 2 | `ClaimMonster` | 가마솥에 들어간 본인 → MasterClient | `int actorNumber` | `MonsterAssignmentAuthority.OnEvent` |
| 3 | `ClearColor` | 리셋한 본인 → Others | `{viewId}` | `PlayerPaintCanvas.OnEvent` |
| 4 | (비워 둠) | — | — | 옛 빌드와 혼동 방지 |
| 5 | `StartGameRequest` | 호스트 → MasterClient | 없음(`photonEvent.Sender` 사용) | `GameLobbyController.OnEvent` → `GameStartAuthority.TryStart` |

#### C.2.4 RPC

| RPC | 소속 | 보내는 쪽 | 대상 | 비고 |
|---|---|---|---|---|
| `LogMsg(string, bool, info)` | `GameManager` (씬 뷰 ID 1) | `GameManager`, `RoomExitController` | `RpcTarget.All` | 채팅·입퇴장 로그. 버퍼 없음 |
| `OnGrabbedByOwner(int carrierViewId)` | `HideOrSeekPlayer` | 그랩하는 쿠키 | 들리는 쿠키의 **주인만** | |
| `OnReleased(bool)` | `HideOrSeekPlayer` | 그랩하던 쿠키 | 들린 쿠키의 **주인만** | |
| `RequestGrabKill()` | `HideOrSeekPlayer` | 괴물 소유자 | 피해자 **주인만** | 피해자가 자기 상태를 확정 |

RPC 이름은 모두 `nameof` 상수(`HideOrSeekPlayer.cs:44-46`, `GameManager.cs:77`)로 참조해 이름이 바뀌면 컴파일 단계에서 드러난다.

#### C.2.5 직렬화(OnPhotonSerializeView)

- 쿠키: `위치(Vector3) → 회전(Quaternion) → 상태(int) → 들고 있는지(bool)` (`Unit/PlayerNetworkSync.cs`)
- 괴물: `위치 → 회전 → 상태(int)` (`Core/NetworkTransformSync.cs`)
- 두 프리팹 모두 PhotonView `Synchronization: 3`(UnreliableOnChange), `OwnershipTransfer: 0`(Fixed), 관찰 대상은
  캐릭터 스크립트 하나다. 원격 복사본은 `isKinematic`이고 `Interpolate`(Lerp, 10m 넘게 벌어지면 순간이동)로 따라간다.

### C.3 도메인별 동작 요약

#### C.3.1 Core

- **`RoomState`**: Room/Player Props를 **예외 없이** 읽는 조회 헬퍼. `HostActor()`(시작 버튼 주인 = 최소 ActorNumber),
  `DesiredMasterActor()`(진행 권한 = 괴물이 아닌 최소 ActorNumber), `IsRoomFull()`(오프라인 방은 찬 것으로 봄),
  `CanSelectMonster()`, `IsBroken(player)`. 순수 계산 함수를 따로 둬 테스트 가능하게 만들었다.
- **`GamePhaseState`**: `GameResult > MonsterJoined > PaintPhaseEndTime` 순서로 `Lobby/Paint/AwaitingMonster/Hunt/Result`를 해석한다.
  색칠 UI, 붓 커서, 캔버스, 카운트다운, 승패 판정이 모두 여기서 판단한다.
- **`CharacterRegistry`**: 캐릭터가 `OnEnable`/`OnDisable`에서 스스로 등록/해제하는 정적 목록. `IsAlive`는 Unity의 null 비교를 쓴다.
- **`NetworkTransformSync<TState>`**: 쿠키·괴물 공용 동기화. enum을 `UnsafeUtility.As`로 int로 변환하고, 정적 생성자에서 enum의 기반 타입을 검증한다.
- **`SpawnPositionFinder`**: 스폰 지점 ±range 안에서 `OverlapCapsuleNonAlloc`(트리거 포함)로 빈 곳을 최대 16번 찾는다.
- **`RoomSceneTransition`**: 방 전체 씬 전환의 유일한 경로. `OpRemoveCompleteCache()`로 이벤트 캐시를 비운 뒤 `LoadLevel`한다.
- **`UiListBuilder`**: 컨테이너의 자식을 재사용하고, 모자라면 복제하고, 남으면 지운다(템플릿은 지우지 않고 숨김).

#### C.3.2 Unit (쿠키)

- **`HideOrSeekPlayer`**: 조정자. Awake에서 협력 객체를 **IsMine과 무관하게** 먼저 만들고(OnPhotonSerializeView가 Start보다
  먼저 올 수 있음), 내 캐릭터면 카메라를 넘긴다. Start에서 물리를 설정하고(원격은 kinematic), 루트 캡슐과 `Mesh_0`
  MeshCollider 사이 충돌을 무시한다. 입력은 Update, 물리는 FixedUpdate. `IsMovementLocked`는
  `외부 잠금 || 파괴됨 || 들림`을 합친 값이다.
- **`PlayerGrabController`**: E키로 `OverlapSphereNonAlloc(cookieLayer=Cookie)` → 대상 주인에게 `OnGrabbedByOwner` RPC → 곧바로
  `carriedPlayer = target`으로 확정. 내려놓을 때는 0.5초 뒤에 충돌 무시를 되돌린다.
- **`PlayerCarryFollower`**: 들린 쿠키 쪽. 소켓 위치로 `rb.position`을 매 FixedUpdate 대입한다. 드는 쪽이 사라지면 스스로 내려온다.
- **`PlayerAnimationDriver`**: enum 이름 = Animator 트리거 이름 계약. 점프는 `Play("Jump",0,0)`로 강제 재생하고, 정점에서 `animator.speed=0`으로 멈춘다.
- **`PlayerSkinApplier`**: Awake에서 스킨을 적용하고, 늦게 도착한 `SkinIndex`는 `PlayerPaintCanvas.TrySetBaseSkin`으로 기본 텍스처만 바꾼다.

#### C.3.3 ColorTag (색칠)

- **`PlayerPaintCanvas`**: Awake에서 512² RT와 스탬프 머티리얼 인스턴스를 만든다(괴물 측 큐 재생으로 Start 전에 스트로크가 올 수 있음).
  Start에서 `Local` 등록과 합성 머티리얼 적용. 로컬 입력: 화면 경로를 6px 간격으로 보간해 스탬프(프레임당 최대 32개),
  한 프레임치를 GL QUADS로 한 번에 그리고, 인원수에 맞춘 간격(4인 1/15초)으로 묶어 전송한다.
  잠금 규칙("이미 칠해진 곳은 덮지 않음")은 하드웨어 블렌딩(`OneMinusDstAlpha`/`DstAlpha`)으로 처리한다.
- **`PaintColliderUpdater`**: 칠하는 중이거나 커서가 몸 근처일 때만 0.2초 간격으로 `BakeMesh` → 워커 스레드
  `Physics.BakeMesh` Job → 완료되면 `sharedMesh`에 대입한다(더블 버퍼).
- **`PaintPhaseController`**: `IsPaintScene` 정적 플래그 겸 강제 도포 배정(방장). 처리 여부를 Room Prop 존재로 판단해 방장이 바뀌어도 다시 뽑지 않는다.
- **`CookieLifeStatePresenter`**: `HitCount` 콜백과 0.5초 주기 재확인으로 파괴 상태를 **멱등하게** 맞춘다. 모든 렌더러·콜라이더를 끄고 `BrokenCookie` 레이어로 옮긴다.

#### C.3.4 Monster

- **`MonsterController`**: 쿠키와 같은 카메라 기준 이동과 Rigidbody 방식. GrabKill과 촉수 돌진은 **클립 길이 타이머**로 상태를 유지한다.
  돌진 경로는 처형 구체 크기로 `SphereCastNonAlloc`(Cookie 레이어만)을 거리순으로 정렬해 검사한다.
- **`MonsterGrabKillTrigger`**: Enter/Stay 모두 검사, 쿨다운 동안은 즉시 반환, 파괴된 쿠키는 제외.
- **`MonsterLobbyWaitController` / `MonsterJoinController`**: "괴물만 대기실에 남기기"의 앞뒤 절반.
- **`MasterClientPolicy`**: 방장 교체, 퇴장, 입장, 괴물 목록 변경 때마다 `SetMasterClient`로 정책을 맞춘다.
- **`SpectatorController`**: 후보를 `CharacterRegistry` + `IGameCharacter.IsSpectatable`로만 찾고, 카메라는 `Camera_Ctrl.SetFollowTarget`에 맡긴다.
  정적 이벤트 `SpectateTargetChanged`로 씬 UI(`SpectatorLabel`)에 알린다.

#### C.3.5 GameManager / Lobby / Camera / Dev

- **`GameManager`**: 채팅만 담당(Enter로 입력창 토글, `LogMsg` RPC). 그런데 Start에서 `Time.timeScale=1`과 `IsMessageQueueRunning=true`도 설정한다(§E.1.2).
- **`PlayerSpawner`**: `InRoom`을 기다린 뒤 스폰. 스폰 지점은 `GameObject.Find`로 직접 찾는다.
- **`RoomExitController`**: 확인창 → 퇴장 로그 RPC → 로컬 Round Player 키 제거 → `LeaveRoom` → `OnLeftRoom`에서 네트워크 설정 복구 + `LoadScene(Lobby)`.
- **`GameStartAuthority`**(정적): 시작 버튼 주인(호스트)과 진행 권한(방장)을 분리한 뒤 둘을 잇는 곳.
- **`Camera_Ctrl`**: 우클릭 드래그 회전(누르는 동안만 커서 잠금), 대상별 높이·거리, `keepRotation` 옵션.

---

## D. 씬·프리팹 배선 실측

### D.1 GameLobbyScene

| GameObject | 컴포넌트 |
|---|---|
| `GameManager` (PhotonView sceneViewId 1) | `GameManager`, `PlayerSpawner`(skipConfirmedMonster=false), `RoomExitController` |
| `MonsterManagers` (PhotonView sceneViewId 2) | `MasterClientPolicy`, `MonsterAssignmentAuthority`, `MonsterLobbyWaitController`, `MonsterRevealController`, `RoundStateResetter` |
| `Cauldron` | `Cauldron`(→ `MonsterRevealController`) |
| `GameLobbyUICanvas/SkinSelectPanel` | `PlayerSkinSelector` |
| `Main Camera` | `Camera_Ctrl` |
| `VoidKillZone` | `VoidKillZone` |
| 프리팹 인스턴스 | `GameLobbyPanel`(`GameLobbyController`), `ConfirmDialog`, 나무·덤불 |

`MonsterLobbyWaitController.behavioursToDisableWhileWaiting = [GameManager]`, `buttonsToDisableWhileWaiting = [Back 버튼]`.

### D.2 GameScene

| GameObject | 컴포넌트 |
|---|---|
| `GameManager` (sceneViewId 1) | `GameManager`, `PlayerSpawner`(skipConfirmedMonster=true), `RoomExitController` |
| `GameRuleManagers` (sceneViewId 3) | `GamePhaseStarter`, `GameRuleController`, `MasterClientPolicy`, `MonsterJoinController`, `PaintPhaseController`, `RoomLifecycleWatcher` |
| `PaintManagers` | `BrushCursorController` |
| `Canvas/ColorSlotPanel` | `ColorSelectionPanel`(timeLabel, slotCountLabel), `SwatchRow/ColorSwatchGroup`, `Swatch0~9/ColorSwatchButton`, `EraseButton`/`ResetButton`/`PaintToolButton` |
| `Canvas/PaintCountdown` | `PhaseCountdownDisplay(phase=Paint)` |
| `Canvas/SurvivalTimer` | `PhaseCountdownDisplay(phase=Survival)` |
| `Canvas/ResultScreen` | `ResultScreenController`(root = 자기 자신) |
| `Canvas/MonsterDepartureBanner` | `MonsterDepartureBanner`(bannerRoot = 자기 자신) |
| `Canvas/SpectatorLabel` | `SpectatorLabel` |
| `Main Camera` / `VoidKillZone` | `Camera_Ctrl` / `VoidKillZone` |

GameScene의 버튼 13개는 모두 인스펙터 `onClick`이 비어 있고 코드(`Awake`/`Start`의 `AddListener`)로 연결된다.
`ResultScreenController.OnLobbyButtonClicked`는 public이지만 **연결된 버튼이 없어** 자동 복귀 코루틴에서만 호출된다.

### D.3 프리팹

| 프리팹 | 루트 레이어 | 컴포넌트 | 콜라이더/물리 |
|---|---|---|---|
| `HideOrSeekPlayer` | **Cookie(9)**, 자식은 모두 Default(0) | PhotonView(관찰 대상: HideOrSeekPlayer), `HideOrSeekPlayer`, `PlayerPaintCanvas`, `PlayerSkinApplier`, `PlayerGrabController`(cookieLayer=512=Cookie), `CookieLifeStatePresenter`(breakVfxPrefab 없음), `SpectatorController`, `FallGuard`, `Nameplate/PlayerBillBoard` | 루트 Capsule(r=0.46) + Rigidbody, `Mesh_0` MeshCollider(**non-convex**) + kinematic Rigidbody |
| `MonsterPlayer` | Monster(10) | PhotonView(관찰 대상: MonsterController), `MonsterController`(cameraDistance=12, obstructionMask=Default), `MonsterGrabKillTrigger`, `FallGuard` | Capsule(r=0.1) + Rigidbody, Sphere 트리거(r=0.5) |

`Mesh_0`의 non-convex MeshCollider는 "캐릭터 콜라이더는 convex" 원칙의 **정당한 예외**다. `RaycastHit.textureCoord`(UV)는
non-convex MeshCollider에서만 채워지고, kinematic Rigidbody에 붙어 있어 Unity 제약도 만족한다.

---

## E. 아키텍처 11개 관점 감사

### E.1 기존 책임 분리를 무시하는 코드 — **주의**

프로젝트는 "조정자 MonoBehaviour + 순수 C# 협력 클래스", "Core 허브", "방장 = 진행, 본인 = 자기 상태"라는 분리 원칙을
분명히 세웠고 대부분 지킨다. 아래는 그 원칙에서 벗어난 곳이다.

#### E.1.1 입력 잠금이 쿠키 한 종류에만 있다 (버그, 중간~높음)

- `PlayerInput`은 "게임 입력을 읽는 유일한 창구"(`Core/PlayerInput.cs:3-5`)인데 **잠금 개념이 없다.** 잠금은
  `HideOrSeekPlayer.IsMovementLocked`(`Unit/HideOrSeekPlayer.cs:52-56`)에만 있고, 채팅은 그 쿠키만 찾아 잠근다
  (`GameManager/GameManager.cs:126-135`, `CharacterRegistry.FindLocal<HideOrSeekPlayer>()`).
- 그 결과, 채팅 입력창이 열려 있어도 다음 입력이 그대로 게임에 들어간다.
  - **괴물**: WASD 이동, Shift 촉수 돌진(`Monster/MonsterController.cs:154-166`). 괴물에는 쿠키를 찾는 잠금이 닿지 않는다.
  - **관전 중인 쿠키**: Space를 누르면 관전 대상이 바뀐다(`Monster/SpectatorController.cs:59`).
  - **색칠 중인 쿠키**: 마우스 드래그로 칠해지고 휠로 붓 크기가 바뀐다(`ColorTag/PlayerPaintCanvas.cs:189-190`). 이동 잠금과 무관하다.
  - **카메라**: 우클릭 회전과 커서 잠금(`Camera/Camera_Ctrl.cs:96-102`).
- 재현: 괴물로 GameScene에 들어가 Enter → "wd" 입력 → 괴물이 앞·오른쪽으로 움직인다.
- **권장**: `PlayerInput`에 `static bool GameplayBlocked`(또는 잠금 사유 카운터)를 두고, 모든 게임 입력 프로퍼티가
  그 값을 보게 한다. 채팅은 `PlayerInput`만 잠그면 된다. `HideOrSeekPlayer.IsMovementLocked`의 외부 잠금 세터는
  사망·컷신 용도로 남겨 두되 채팅에서는 쓰지 않는다. 그러면 `GameManager`→`HideOrSeekPlayer` 의존도 사라진다.

#### E.1.2 전역 네트워크 상태를 매니저 여러 곳이 각자 바꾼다 (책임, 중간)

| 전역 설정 | 바꾸는 곳 |
|---|---|
| `PhotonNetwork.IsMessageQueueRunning` | `GameManager.Start`(`GameManager.cs:28`), `PlayerSpawner.SpawnLocalPlayer`(`PlayerSpawner.cs:65`), `MonsterLobbyWaitController.EnterWaiting`(`:78`), `RoomExitController.OnLeftRoom`(`:53`), 그리고 PUN 자체(`LoadLevel`/씬 로드 완료) |
| `PhotonNetwork.AutomaticallySyncScene` | `LobbyController.Awake`(`:30`), `MonsterLobbyWaitController.ApplySceneSyncPolicy`(`:56`), `MonsterJoinController.RestoreSceneSyncWhenCaughtUp`(`:40`) |
| `PhotonNetwork.KeepAliveInBackground` | `MonsterLobbyWaitController`(`:76`), `MonsterJoinController`(`:41`), `RoomExitController`(`:54`) |
| `Time.timeScale` | `GameManager.Start`(`:27`) |
| `PhotonNetwork.SerializationRate` | `LobbyController.Start`(`:45`, 연결되지 않았을 때만) |

- **채팅 전담이라고 선언된 `GameManager`**(`GameManager.cs:8`)가 타임스케일과 메시지 큐를 건드린다. 스포너도 큐를 켠다.
  지금은 괴물 대기(큐 정지)가 이 두 `Start`보다 항상 늦게 일어나서 문제가 없지만, 순서가 바뀌면(예: 대기실 씬을 다시
  로드하거나 스포너를 늦게 실행) **괴물 대기 중 큐가 다시 켜져** "쿠키 아바타가 대기실에 생성됐다가 사라지는" 옛 버그
  (Bug-fix-plan.md §23.3.2 F4)가 되살아난다.
- 괴물 대기 흐름의 "끄기"(`MonsterLobbyWaitController`)와 "되돌리기"(`MonsterJoinController`, `RoomExitController`)가 세 클래스에
  나뉘어 있어, 새 이탈 경로(예: 연결 끊김)를 추가할 때 복구를 빠뜨리기 쉽다.
- **권장**: `NetworkSessionPolicy`(가칭, Core) 한 곳에 `EnterMonsterWait(remaining)`, `ExitMonsterWait()`, `ResetToDefaults()`를 두고
  모든 호출부가 이를 쓰게 한다. `GameManager`와 `PlayerSpawner`의 `IsMessageQueueRunning = true`는 PUN이 씬 로드 후 스스로
  재개하므로(`PhotonNetworkPart.cs:1470-1474`) 지워도 된다.

#### E.1.3 UI 패널이 게임 시작 네트워크 이벤트를 받는다 (낮음~중간)

- `GameLobbyController`(UI 프리팹 `GameLobbyPanel`)가 `IOnEventCallback`을 구현하고 `StartGameRequest`를 받아
  `GameStartAuthority.TryStart`를 호출한다(`Lobby/GameLobbyController.cs:13, 98-104`). 권한 로직은 정적 클래스에 잘 분리했지만,
  **수신 창구가 UI의 수명에 묶여 있다.** 패널을 비활성화하거나 UI를 교체하면 방장이 시작 요청을 받지 못한다.
- **권장**: 수신을 대기실의 비UI 매니저(`MonsterManagers` 오브젝트에 `GameStartReceiver`를 두거나 `MonsterAssignmentAuthority`
  옆)로 옮긴다. UI는 `RequestStart()`만 호출한다.

#### E.1.4 운영 코드가 개발 도구에 의존한다 (낮음)

- `PlayerSpawner.IsAlreadyMonster()`가 `OfflineModeBootstrap.SpawnAsMonster`(Dev)를 직접 읽는다(`GameManager/PlayerSpawner.cs:48`).
  `Dev/` 폴더를 빌드에서 빼거나 별도 asmdef로 나누면 컴파일이 깨진다. 반대 방향(`MonsterTestSpawner` → 운영 코드)은 문제없다.
- **권장**: `PlayerSpawner`에 "이 씬에서 쿠키를 스폰할지" 판정 훅(예: 정적 `Func<bool> SkipCookieSpawnOverride`)을 두고,
  `OfflineModeBootstrap`이 그 훅을 등록하게 뒤집는다.

#### E.1.5 그 밖의 경계 흐림 (낮음)

- `HideOrSeekPlayer.RequestGrabKill`이 `GetComponent<SpectatorController>()?.EnterSpectatorMode()`로 관전 진입까지 직접 호출한다
  (`Unit/HideOrSeekPlayer.cs:126`). 파괴 표시는 `CookieLifeStatePresenter`가 속성 변경으로 알아서 반응하는데, 관전만 직접 호출이라
  비대칭이다. 관전도 로컬 `HitCount` 변화에 반응하게 하면 `Unit`→`Monster` 폴더 의존이 없어진다.
- `RoomExitController.OnClickBackBtn`이 `PhotonNetwork.LocalPlayer.CustomProperties`에서 키를 **로컬로만** 지운다(`:43-44`). 다음 방에
  들어갈 때 딸려 가지 않게 하려는 의도지만, PUN의 속성 캐시를 직접 고치는 것이라 `NetKeys`/`RoundStateResetter`의 초기화 책임과 겹친다.

### E.2 Manager 간 의존성 — **양호~주의**

#### E.2.1 전체 그림

대부분의 매니저는 서로를 직접 참조하지 않고 **정적 허브**(`RoomState`, `NetKeys`, `GamePhaseState`, `GameSettings`,
`CharacterRegistry`, `PlayerPaintCanvas.Local`)를 거쳐 느슨하게 연결된다. 방장 진행 컴포넌트 7개는 서로를 전혀 모르고
Room Props로만 소통한다. **이 부분은 잘 설계됐다.**

직접 참조가 남은 곳(부록 1에 전체 표):

| 의존 | 방식 | 평가 |
|---|---|---|
| `RoomExitController` → `GameManager` | `GameManager.RpcLogMsg` 상수 + **같은 PhotonView를 인스펙터로 공유**(`RoomExitController.cs:11, 39`) | 퇴장 로그를 보내려고 채팅 매니저의 RPC 채널을 빌려 쓴다. 채팅 매니저를 교체하면 퇴장이 깨진다 |
| `MonsterLobbyWaitController` → `GameManager` | 인스펙터 `Behaviour[]`로 `enabled=false` | 대기 흐름이 채팅 매니저의 존재와 동작 방식을 안다. 입력 잠금(§E.1.1)이 `PlayerInput`에 있으면 필요 없다 |
| `GameManager` → `HideOrSeekPlayer` | `CharacterRegistry.FindLocal<HideOrSeekPlayer>()` | §E.1.1 |
| `Cauldron` → `MonsterRevealController` | 인스펙터 | 같은 기능 묶음이라 허용 범위 |
| `MonsterController` ↔ `MonsterGrabKillTrigger` | **양방향** 인스펙터 참조 | 트리거가 컨트롤러의 `PlayGrabKill`을 부르고, 컨트롤러가 트리거의 `ResetTrigger`/`TryGrabKill`/`ReachCenter`를 부른다. 쿨다운 소유권이 둘로 나뉘어 있다 |
| `PlayerSkinApplier` → `PlayerPaintCanvas` | `GetComponent` | 스킨(Unit)이 색칠(ColorTag)을 안다 |

#### E.2.2 폴더(도메인) 사이 순환

런타임 asmdef가 하나라 도메인 경계를 컴파일러가 막지 않는다. 실제 참조를 세어 보면 다음 순환이 있다.

- `Unit/HideOrSeekPlayer` → `Monster/SpectatorController`, `Monster/*` → `Unit/HideOrSeekPlayer`(RPC 상수, `GetComponentInParent<HideOrSeekPlayer>`)
- `Unit/PlayerSkinApplier` → `ColorTag/PlayerPaintCanvas`, `ColorTag/*` → `Unit`(`HideOrSeekPlayer`는 아니지만 `PlayerPaintCanvas`가 쿠키 프리팹 구성을 전제로 함)
- `GameManager/PlayerSpawner` → `Dev/OfflineModeBootstrap`(§E.1.4)

**평가**: 지금 규모(5.4천 줄)에서는 과도하지 않다. 다만 `Monster` 폴더가 19개 파일로 가장 크고, 실제로는 "괴물"뿐 아니라
판 진행(`GameRuleController`, `RoundStateResetter`, `MasterClientPolicy`, `RoomLifecycleWatcher`, `ResultScreenController`,
`GamePhaseStarter`)과 관전(`SpectatorController`)까지 담고 있다. **폴더 이름과 내용이 어긋난 것이 결합을 늘리는 원인**이다.
`Round/`(판 진행)와 `Spectator/`로 나누고, 필요하면 asmdef를 `Core`(의존 없음) ← `Characters` ← `Round` 순의 단방향으로 쪼개는 것을 권장한다.

### E.3 Prefab과 Script의 역할 혼재 — **주의**

#### E.3.1 쿠키 프리팹 하나에 역할 7개

`HideOrSeekPlayer.prefab` 루트에 이동/물리, 색칠 캔버스, 스킨, 그랩, 생존 표시, 관전, 낙하 복귀가 모두 붙어 있고,
**대기실과 게임 씬에서 같은 프리팹을 쓴다.** 그 결과는 다음과 같다.

- 대기실의 쿠키도 모두 512² RenderTexture, 스탬프 머티리얼 3개, 합성 머티리얼을 만든다(`PlayerPaintCanvas.cs:101-109, 149-160`).
  4인 방이면 대기실에서만 RT 4개(약 4MB)와 머티리얼 16개가 쓰이지 않은 채 만들어진다.
- 색칠 코드가 "지금 색칠 씬인가"를 매번 물어야 한다(`PaintPhaseController.IsPaintScene` 검사 4곳, `PlayerPaintCanvas.cs:135, 177, 407, 498`).
  이전에 "대기실에 돌아온 쿠키가 다시 칠해지던" 버그(§26.2 ⑲)가 이 구조에서 나왔다.
- **권장**: 당장 프리팹을 나눌 필요는 없다. 다만 `PlayerPaintCanvas`의 무거운 초기화(RT·머티리얼)를 "색칠 씬일 때만"으로 늦추거나,
  색칠 관련 컴포넌트를 자식 오브젝트로 묶어 씬 표식에 따라 켜고 끄면 두 씬의 역할이 분명해진다.

#### E.3.2 옛 `ColorSelectionPanel.prefab`이 씬 UI와 따로 논다

- `Resources/UI/Scene/ColorSelectionPanel/ColorSelectionPanel.prefab`은 **`PlayerTestScene`에서만** 쓰인다. 스와치 10개가
  `SwatchGrid` 아래에 손으로 배치돼 있고 `ColorSwatchGroup`이 없다.
- 실제 게임(`GameScene`)은 프리팹이 아닌 **씬 안에 직접 만든** `Canvas/ColorSlotPanel`(`SwatchRow` + `ColorSwatchGroup`)을 쓴다.
- 같은 기능의 UI가 두 벌이고 구조도 다르다. 테스트 씬에서 본 UI와 실제 게임 UI가 다르게 동작할 수 있다.
- **권장**: 씬의 `ColorSlotPanel`을 프리팹으로 만들어 두 씬이 함께 쓰고, 옛 프리팹은 지운다(CLAUDE.md 규칙 `Resources/UI/{Scene}/{클래스명}`에도 맞다).

#### E.3.3 쓰지 않는 씬 PhotonView

- `GameLobbyScene/MonsterManagers`(sceneViewId 2)와 `GameScene/GameRuleManagers`(sceneViewId 3)에 PhotonView가 있지만,
  그 오브젝트의 어떤 스크립트도 RPC나 직렬화를 쓰지 않는다. 역할이 없는 뷰는 씬 뷰 ID만 차지하고, "여기서 RPC를 쓰나?"라는 오해를 낳는다.

#### E.3.4 설정값이 세 곳에 흩어져 있다

| 값 | 위치 |
|---|---|
| 쿠키 속도 5, 점프 6, 회피 0.5초 | 프리팹(`HideOrSeekPlayer`) |
| 괴물 속도 4, 카메라 거리 12(코드 기본값은 4.5) | 프리팹(`MonsterPlayer`) |
| 슬롯 등록 기준 15스탬프, 캔버스 512 | 프리팹(`PlayerPaintCanvas`) |
| 최대 슬롯 4, 색칠 60초, 돌진 20m 등 | `GameSettingsSO` |
| 질주 배율 1.3, 회피 배율 2 | 코드 상수(`HideOrSeekPlayer.cs:300, 341`) |
| 카메라 회전 속도·각도 제한 | 코드 필드(직렬화 안 됨, `Camera_Ctrl.cs:9-19`) |
| 결과 화면 자동 복귀 12초 | 씬(`ResultScreenController.autoReturnDelay`) |

`GameSettingsSO`를 "규칙 수치를 한 곳에 모은 전역 설정"(`GameSettingsSO.cs:3`)이라고 선언했지만 **밸런스 값의 절반은 여전히 프리팹과 코드에 있다.**
괴물 종류나 쿠키 종류를 늘릴 계획이면 `CookieStatsSO`/`MonsterStatsSO`처럼 캐릭터별 SO로 옮기고 프리팹은 그 SO만 참조하게 하는 것을 권장한다.

#### E.3.5 Resources 폴더 과사용 (낮음)

`Resources/UI/**` 프리팹 6종과 `BrushCursor.prefab`은 `Resources.Load`로 불러오지 않는데 `Resources` 폴더에 있다. Resources 폴더의
내용은 참조 여부와 관계없이 **모두 빌드에 포함**된다. `PhotonNetwork.Instantiate` 대상(쿠키·괴물)과 `GameSettings`/`InputBindings`만 남기고 옮겨도 된다.
(CLAUDE.md의 폴더 규칙이 UI를 `Resources/UI`에 두라고 정해 두었으므로, 옮길지는 규칙 자체를 바꿀지와 함께 판단할 문제다.)

### E.4 Scene에 직접 의존하는 코드 — **양호(늘지 않음)**

이전 판 대비 씬 이름(`SceneNames`), 스폰 지점 이름(`SceneSpawnPoints`)이 상수로 모였고, 이름 비교 대신 컴포넌트
존재로 씬 성격을 판단하게 바뀌었다. **씬 의존은 줄어드는 추세다.** 남은 지점은 다음과 같다.

#### E.4.1 `GameObject.Find` 4곳 중 3곳이 헬퍼를 우회

| 위치 | 방식 |
|---|---|
| `Core/SceneSpawnPoints.cs:13` | 헬퍼 본체(`TryFindClearPosition`) — 리스폰 2곳이 사용 |
| `GameManager/PlayerSpawner.cs:56` | 직접 `Find` + `FindClearPosition` |
| `Monster/MonsterJoinController.cs:72` | 직접 `Find` + `FindClearPosition` |
| `Dev/MonsterTestSpawner.cs:24` | 직접 `Find`, 빈 곳 찾기도 하지 않음 |

같은 일을 하는 헬퍼가 이미 있다(§E.11.2). 이름 기반 `Find`는 비활성 오브젝트를 못 찾고 이름이 겹치면 첫 번째를 고른다.
장기적으로는 스폰 지점에 `SpawnPoint` 컴포넌트(역할 enum)를 붙이고 그 컴포넌트가 스스로 등록하는 방식이 더 안전하다.

#### E.4.2 PUN 내부 구현에 대한 의존

- `MonsterJoinController`가 PUN 내부 Room Prop 키 `"curScn"`을 문자열로 읽고(`Monster/MonsterJoinController.cs:18, 37`),
  그 값을 `SceneManager.GetActiveScene().name`과 비교한다. PUN 업데이트로 키 이름이나 형식(이름 ↔ 빌드 인덱스)이 바뀌면
  **괴물의 씬 자동 동기화가 영원히 복구되지 않아** 다음 판에 대기실로 따라가지 못한다. 주석에 이유가 적혀 있고
  공개 API가 없어 불가피한 선택이지만, PUN을 업데이트할 때 반드시 확인해야 할 항목이다.

#### E.4.3 컴포넌트 존재를 씬 표식으로 쓰는 정적 플래그

- `PaintPhaseController.IsPaintScene`이 Awake에서 true, OnDestroy에서 false가 된다(`ColorTag/PaintPhaseController.cs:16-29`).
  씬 이름 비교보다 낫지만, "강제 도포 배정 매니저"가 "색칠 씬 표식"을 겸한다(책임 2개). 두 인스턴스가 동시에 있거나
  (추가 로드), 이전 씬의 OnDestroy가 새 씬의 Awake보다 늦게 오면 값이 틀어진다. 지금의 씬 전환(GameLobby ↔ Game, 단일 로드)에서는 안전하다.
- **권장**: 전용 `SceneRole` 컴포넌트(enum Lobby/GameLobby/Game)를 씬마다 하나 두고 `SceneRole.Current`로 묻는다.

#### E.4.4 `Camera.main` 의존 10곳(6개 파일)

`HideOrSeekPlayer`(Awake, 매 Update의 이동 방향), `MonsterController`(Awake, 매 Update), `PlayerBillBoard`(매 LateUpdate, 인스턴스마다),
`SpectatorController`, `BrushCursorController`, `PlayerPaintCanvas`(Start, 입력 시). Unity 6에서 `Camera.main`은 캐시돼 비용 문제는
없지만, 모두 **"MainCamera 태그 + `Camera_Ctrl` 컴포넌트가 씬에 있다"**는 암묵적 계약에 기대고 있다. `Camera_Ctrl`이 없으면
괴물·관전만 에러 로그를 남기고 쿠키는 조용히 카메라 없이 스폰된다(`HideOrSeekPlayer.cs:146-148`). 계약 위반을 한 곳에서 알리도록
`Camera_Ctrl.Main`(없으면 경고 1회) 같은 접근자를 두면 좋다.

### E.5 Singleton 남발 — **양호**

`static ... Instance` 형태의 고전적 싱글톤은 **0개**다(`MonsterController.RaycastHitDistanceComparer.Instance`는 비교자 캐시일 뿐이다).
이전의 `GameManager.Inst`도 이미 없어졌다. 대신 정적 상태가 9종 있고, 모두 이유가 분명하다(부록 3).

수명 관리에 주의할 것은 두 가지다.

1. **`PlayerPaintCanvas.Local`**(`ColorTag/PlayerPaintCanvas.cs:40, 121, 530`): Start에서 등록되고 OnDestroy에서 해제된다.
   UI 4종(스와치, 도구 버튼, 슬롯 패널, 붓 커서)이 이 값을 매 프레임 또는 클릭 때 읽는다. null 검사는 모두 있다.
   `CharacterRegistry.FindLocal`과 목적이 겹치므로, 캔버스도 레지스트리에서 찾게 하면 정적 필드 하나를 줄일 수 있다.
2. **`GameStartAuthority.lastStartTime`**(`Lobby/GameStartAuthority.cs:20`): 방을 나갔다 다시 만들어도 남는다. 중복 방지 창이
   3초라 실제 영향은 없다.

Enter Play Mode 설정에서 도메인 리로드가 켜져 있으므로(§B.1) 에디터에서 플레이할 때마다 정적 상태가 초기화된다. 나중에 빠른 플레이를 위해
도메인 리로드를 끄면 `CharacterRegistry`의 목록, `OfflineModeBootstrap.SpawnAsMonster`, `GameSettings`/`PlayerInput` 캐시가 이전 플레이의
값을 들고 시작한다. 그때는 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 초기화가 필요하다.

### E.6 ScriptableObject 책임 — **양호**

| SO | 성격 | 평가 |
|---|---|---|
| `GameSettingsSO` | 전역 규칙 수치 + 순수 계산 2개(`PaintStrokeSendIntervalFor`, `MonsterCountFor`) + `OnValidate` 제약 | 데이터에서 바로 나오는 파생 계산이라 SO에 두는 것이 맞다. 테스트도 있다 |
| `InputBindingsSO` | 키 매핑 | 적절 |
| `SkinCatalogSO` | 스킨 목록, 범위 밖 인덱스 보정 | 적절(네트워크 값 방어) |
| `ColorPaletteSO` | 색 목록 | `Count => colors.Length`(`ColorTag/ColorPaletteSO.cs:8`)만 null 검사가 없다. 같은 파일의 `IsValid`는 null을 검사한다. 빈 에셋을 새로 만들면 NRE |
| `BrushSettingsSO` | 붓 수치 + 커서 프리팹 참조 | 적절 |

- **런타임에 SO를 수정하는 코드는 없다.** 에셋 오염 위험이 없다.
- **접근 방식이 두 가지 섞여 있다**: `GameSettings`/`InputBindings`는 `Resources.Load` 서비스 로케이터, 나머지 3종은 인스펙터 주입.
  전역 하나뿐인 설정은 로케이터, 교체 가능한 데이터는 주입으로 역할이 나뉘어 있어 합리적이다.
- **팔레트 일관성이 인스펙터 5곳에 걸려 있다**: `PlayerPaintCanvas`(프리팹), `PaintPhaseController`, `BrushCursorController`,
  `ColorSwatchGroup`(씬), 옛 `ColorSelectionPanel.prefab`. 지금은 모두 같은 `DefaultColorPalette`(guid `9d4f892f…`)를 가리킨다.
  하나라도 다른 팔레트를 가리키면 **스와치 번호, 강제 도포 색, 원격 스탬프 색이 서로 어긋난다**(네트워크로는 인덱스만 보내므로).
  팔레트도 `GameSettingsSO`에서 참조하게 하거나, 적어도 EditMode 테스트로 "모든 참조가 같은 팔레트인지" 검사할 것을 권장한다.

### E.7 Unity Lifecycle 순서 — **양호(대부분 의식적으로 방어됨)**

#### E.7.1 이미 잘 막아 둔 것

- **PUN 인스턴스 생성 순서**: PUN `DefaultPool`은 프리팹을 **비활성 상태로** 복제한 뒤 ViewID·Owner를 설정하고 활성화한다
  (`Photon/PhotonUnityNetworking/Code/PunClasses.cs:908-918`). 그래서 Awake에서 `pv.IsMine`, `pv.Owner`, `pv.ViewID`를 써도 안전하다
  (`HideOrSeekPlayer.Awake`, `PlayerSkinApplier.Awake`, `PlayerPaintCanvas.CreatePaintCanvasTexture`).
- **OnPhotonSerializeView가 Start보다 먼저 오는 경우**: 쓰는 객체를 모두 Awake 또는 필드 초기화에서 만든다(`HideOrSeekPlayer.cs:136-142`, `MonsterController.cs:25-26`).
- **괴물 큐 재생으로 Start 전에 스트로크가 오는 경우**: RT를 Awake에서 만든다(`PlayerPaintCanvas.cs:96-109`).
- **`InRoom`이 씬 Start 시점에 아직 false인 경우**: 스폰·접속 메시지·초기화가 모두 `InRoom`을 기다리거나 첫 `InRoom` 프레임에 실행된다
  (`PlayerSpawner`, `GameManager`, `RoundStateResetter`, `MasterClientPolicy`).
- **자기 GameObject를 끄면 콜백이 끊기는 문제**: 표시/숨김은 `CanvasGroupVisibility`로만 한다(결과 화면, 이탈 배너, 슬롯 패널, 카운트다운, 관전 라벨).
- **Rigidbody 조작은 FixedUpdate에서만**, 순간이동은 `rb.position` + `transform.position`.

#### E.7.2 `ConfirmDialog`가 Awake에서 스스로 꺼진다 (잠재 위험)

- `ConfirmDialog.Awake`에서 리스너를 등록하고 `gameObject.SetActive(false)`를 한다(`GameManager/ConfirmDialog.cs:15-20`).
  지금은 두 씬 모두 활성 상태로 배치돼 있어(프리팹 오버라이드 `m_IsActive: 1`) 씬 로드 때 Awake가 실행되고 숨겨진다.
- 누군가 편집 중에 확인창을 보기 싫어 **씬에서 비활성으로 저장하면**, 첫 `Show()`에서 `SetActive(true)` → 그제야 Awake 실행 →
  **그 자리에서 다시 꺼진다.** 뒤로가기 확인창이 첫 번째에는 뜨지 않는다.
- **권장**: 숨김을 Awake가 아니라 `Start`에서 한 번만 하거나(첫 표시 이후에는 호출되지 않도록 플래그), 이 문서에 쓰인 `CanvasGroupVisibility` 방식으로 바꾼다.

#### E.7.3 `PlayerPaintCanvas.Local` 1프레임 공백 (낮음)

`Local`은 Start에서 설정된다(`:121`). 스폰 프레임에 UI가 `Local`을 읽으면 null이다. 모든 읽는 쪽에 null 검사가 있어 문제는 없지만,
Awake에서 설정하지 않는 이유(스킨 적용 순서)는 `Local` 등록과 관계가 없으므로 등록만 Awake로 옮겨도 된다.

#### E.7.4 같은 오브젝트의 Awake 순서에 기대는 곳

`PlayerSkinApplier.Awake`(스킨 교체) → `PlayerPaintCanvas.Start`(합성 머티리얼 생성) 순서는 "Awake가 모두 끝난 뒤 Start"라는
보장에 기대므로 안전하다. 반대로 스킨이 늦게 오면 `TrySetBaseSkin`으로 처리한다. 두 경로가 모두 있어 **순서와 무관하게 수렴**한다.

#### E.7.5 `UiListBuilder`의 템플릿 재활성화 누락 (낮음)

`UiListBuilder.Sync`는 개수가 줄면 템플릿을 `SetActive(false)`로 숨기지만(`Core/UiListBuilder.cs:26`), 다음에 개수가 늘어 템플릿을
재사용할 때 다시 켜지 않는다(`:22`). 지금은 `Awake`에서 한 번만 호출돼 드러나지 않는다. 팔레트나 스킨 목록을 런타임에 바꾸는
기능이 생기면 첫 항목이 사라진다.

### E.8 Event 구독/해제 — **양호**

#### E.8.1 구독 지점 전수

| 종류 | 위치 | 해제 | 평가 |
|---|---|---|---|
| Photon 콜백(`IMatchmakingCallbacks`, `IInRoomCallbacks` 등) | `MonoBehaviourPunCallbacks`를 상속한 24개 클래스 | `OnDisable`에서 자동 해제(`PunClasses.cs:109-117`) | ✅ `OnEnable`/`OnDisable`을 재정의한 4곳(`HideOrSeekPlayer`, `MonsterController`, `BrushCursorController`, `PlayerPaintCanvas`) 모두 `base`를 호출한다 |
| `IOnEventCallback` | `PlayerPaintCanvas`, `MonsterAssignmentAuthority`, `GameLobbyController` | 위와 같음(`AddCallbackTarget`이 인터페이스별로 한 번에 등록) | ✅ 이전의 "이중 등록" 문제는 없다 |
| C# 정적 이벤트 | `SpectatorController.SpectateTargetChanged` ← `SpectatorLabel` | `OnEnable` +=, `OnDisable` -= (`SpectatorLabel.cs:19-27`) | ✅ 관전 종료 시 null 알림도 보낸다 |
| `Button.onClick` | `ColorSwatchButton`, `PaintToolButton`, `ConfirmDialog`(Awake), `RoomExitController`(Start), `RoomListItem`(Awake), `PlayerSkinSelector`(Awake, `RemoveAllListeners` 후 등록) | 버튼과 수명이 같아 해제 불필요 | ✅ 인스펙터 바인딩과 코드 바인딩이 겹치는 버튼은 없다(인스펙터 바인딩은 `GameLobbyPanel`의 시작 버튼, `LobbyPanel`의 두 버튼뿐) |
| 콜백 인자 | `ConfirmDialog.Show(msg, onYes)` | 호출마다 덮어씀 | ✅ 누적 없음 |

#### E.8.2 콜백을 쓰지 않으면서 콜백 대상으로 등록되는 클래스 6개 (낮음)

`BrushCursorController`, `ColorSelectionPanel`, `GamePhaseStarter`, `GameRuleController`, `PaintPhaseController`, `GameManager`는
`MonoBehaviourPunCallbacks`를 상속하지만 **Photon 콜백을 하나도 재정의하지 않는다.** 이들은 방 속성이 바뀔 때마다 콜백 목록에서 순회된다(비용은 작다).
더 중요한 것은 **읽는 사람에게 "이 클래스는 네트워크 콜백에 반응한다"는 잘못된 신호**를 준다는 점이다. `MonoBehaviour`로 되돌릴 것을 권장한다.
(`GameManager`는 RPC만 쓰므로 `MonoBehaviour` + `[PunRPC]`로 충분하다.)

#### E.8.3 `enabled=false`로 끄는 매니저

`MonsterLobbyWaitController`가 `GameManager.enabled = false`로 채팅을 끈다. 이때 `MonoBehaviourPunCallbacks.OnDisable`이 콜백 등록을 해제하지만,
`GameManager`는 콜백을 쓰지 않으므로 부작용이 없다. 다만 이 방식은 "끄면 콜백도 끊긴다"는 함정(이 프로젝트가 여러 번 겪은 문제)을 그대로 안고 있어,
나중에 `GameManager`가 콜백을 쓰기 시작하면 괴물 대기 중 그 콜백이 사라진다. §E.1.1대로 입력 잠금을 `PlayerInput`으로 옮기면 이 끄기 자체가 필요 없다.

### E.9 Object Pool과 Instantiate/Destroy 충돌 — **충돌 없음**

- **커스텀 `IPunPrefabPool`이 없다.** `PhotonNetwork.PrefabPool`은 PUN 기본 `DefaultPool`이다. 이 풀은 이름과 달리 실제로 재사용하지 않고,
  `Resources.Load` 결과를 캐시한 뒤 매번 `Instantiate`/`Destroy`한다.
- **`PhotonNetwork.Destroy`를 한 번도 호출하지 않는다.** 네트워크 오브젝트는 (1) 씬 로드, (2) 방을 떠난 플레이어에 대한 PUN 자동 정리,
  (3) `MonsterLobbyWaitController.RemoveOtherPlayersAvatars`의 로컬 `Destroy`(`Monster/MonsterLobbyWaitController.cs:97-104`)로만 사라진다.
  (3)은 원래 주인이 이미 씬을 떠난 복사본이라 로컬 정리가 맞고, PUN도 `PhotonView.OnDestroy`에서 로컬 목록을 정리한다
  (`PhotonView.cs:398-409`, 로그 레벨 Informational일 때만 안내 로그).
- 이벤트 캐시는 씬 전환마다 `OpRemoveCompleteCache`로 비운다(`Core/RoomSceneTransition.cs:23`). 늦게 들어온 사람에게 이전 씬의 캐릭터가 생기지 않는다.
- 로컬 오브젝트:

| 위치 | 생성/파괴 | 평가 |
|---|---|---|
| `GameLobbyController.RefreshPlayerList` | 갱신할 때마다 전부 `Destroy` 후 `Instantiate`(`Lobby/GameLobbyController.cs:115-124`) | 입장 한 번에 **두 번** 재생성된다(콜백 + 다음 프레임 Update의 인원 변화 감지, `:57-63`, `:79-83`). `UiListBuilder`나 `LobbyController`의 diff 방식과 일관성이 없다 |
| `LobbyController.RefreshRoomListView` | 이름 기준 diff | ✅ |
| `ResultScreenController.ShowResult` | 행·아이콘을 한 번만 `Instantiate`(`isShown` 가드) | ✅ |
| `CookieLifeStatePresenter.ApplyBroken` | `breakVfxPrefab` `Instantiate`, 파괴 코드 없음(`ColorTag/CookieLifeStatePresenter.cs:112-113`) | 지금은 프리팹이 비어 있어 실행되지 않는다. VFX를 연결할 때 파티클 `Stop Action = Destroy` 설정이나 자동 파괴가 필요하다 |
| `BrushCursorController` | 커서 1개, `OnDestroy`에서 정리 | ✅ |
| `PlayerPaintCanvas`, `PaintColliderUpdater` | RT, 머티리얼, 메시 모두 `OnDestroy`/`Dispose`에서 해제 | ✅ |

- **나중에 풀을 도입하면 충돌할 곳**: (1) `MonsterLobbyWaitController`의 로컬 `Destroy`는 풀의 `Destroy`(비활성화 후 반납)를 우회한다.
  (2) 캐릭터 상태를 `Awake`/`Start`에서만 초기화한다(`hitCount`, `isJump`, `CookieLifeStatePresenter.appliedState`, `PlayerPaintCanvas`의 슬롯 등).
  풀에서 재사용하면 이전 판의 상태를 들고 나온다. 풀을 도입할 때는 `IPunInstantiateMagicCallback.OnPhotonInstantiate`에서 초기화하도록 옮겨야 한다.
- GC 관점: `PlayerPaintCanvas.FlushStrokes`가 전송마다 배열 5개를 새로 만든다(`:374-379`, 4인 기준 초당 최대 15회). 작지만 색칠 페이즈 내내
  꾸준히 쌓이는 할당이다. 크기별 재사용 버퍼나 `ByteArraySlice` 기반 직렬화로 줄일 수 있다.

### E.10 Photon Ownership/RPC 구조 — **대체로 준수, 틈 있음**

#### E.10.1 잘 지키고 있는 것

- **Room Props는 방장만 쓴다.** 8개 쓰기 지점 모두 `IsMasterClient` 확인 뒤에 쓴다(부록 2).
- **Player Props는 본인만 쓴다.** `HitCount`(피해자 본인), `RegisteredSlotCount`, `SkinIndex`. 다른 사람의 파괴를 괴물이 직접 쓰지 않고
  **피해자 주인에게 RPC로 요청**한다(`Monster/MonsterGrabKillTrigger.cs:73-74`). 소유권 원칙에 맞는 설계다.
- **RPC 대상이 정확하다.** 개인 상태 변경 RPC 3개는 모두 `targetPv.Owner`(한 사람)만 받는다. 브로드캐스트는 채팅 로그뿐이다.
- **버퍼 RPC(`AllBuffered`)를 쓰지 않는다.** 이벤트 캐시는 씬 전환마다 비운다.
- **소유권 이전을 쓰지 않는다**(`OwnershipTransfer: Fixed`). 그랩도 "들린 쿠키가 자기 뷰를 유지한 채 소켓을 로컬로 따라가는" 방식이다.
- **방장 교체에 강하다.** 강제 도포, 합류, 괴물 선정 마감 시각을 로컬 플래그가 아니라 Room Prop 존재로 판단해 새 방장이 이어받는다.

#### E.10.2 보낸 사람을 확인하지 않는 수신 지점 (중간)

| 수신 지점 | 현재 | 문제 | 권장 |
|---|---|---|---|
| `MonsterAssignmentAuthority.OnEvent`(ClaimMonster) | 본문의 `int claimantActorNumber`를 그대로 신뢰(`Monster/MonsterAssignmentAuthority.cs:33`) | 한 클라이언트가 **다른 사람을 괴물로 신청**할 수 있다. 정상 클라이언트도 본문과 보낸 사람이 다를 이유가 없다 | `photonEvent.Sender`를 쓰고 본문은 버린다(`StartGameRequest`는 이미 이렇게 한다) |
| `HideOrSeekPlayer.RequestGrabKill` RPC | `PhotonMessageInfo` 없음(`Unit/HideOrSeekPlayer.cs:107-113`) | 괴물이 아닌 누구든 쿠키를 파괴할 수 있다 | `info.Sender`가 `RoomState.IsMonster`인지, 지금이 Hunt 단계인지 확인 |
| `HideOrSeekPlayer.OnGrabbedByOwner` RPC | 보낸 사람과 `newCarrierViewId`의 주인이 같은지 확인하지 않음(`:76-81`) | 아무 뷰에나 붙게 할 수 있다(괴물 뷰 포함) | `PhotonView.Find(id).OwnerActorNr == info.Sender.ActorNumber`, 대상이 쿠키인지, 거리 확인 |
| `PlayerPaintCanvas.OnEvent`(PaintStroke/ClearColor) | 본문의 `viewId`만 확인(`ColorTag/PlayerPaintCanvas.cs:405-406`) | 다른 사람의 몸을 칠하거나 지울 수 있다 | `photonEvent.Sender == pv.OwnerActorNr` |

치트 방지가 목표가 아니더라도, 이 검사들은 **버그가 났을 때 피해를 한 클라이언트 안에 가두는** 역할을 한다. 네 곳 모두 한두 줄이면 된다.

#### E.10.3 그랩의 낙관적 확정 (중간)

- 그랩하는 쪽은 RPC를 보내자마자 `carriedPlayer = target`으로 확정하고 캐리 자세·충돌 무시를 켠다(`Unit/PlayerGrabController.cs:54-58`).
  들리는 쪽은 `TryAttach`에서 **이미 다른 사람에게 들려 있는지 확인하지 않고** 새 운반자로 덮어쓴다(`Unit/PlayerCarryFollower.cs:29-40`).
- 문제 시나리오:
  1. **동시 그랩**: A와 B가 거의 동시에 C를 잡으면 C는 나중 RPC의 주인을 따라간다. 먼저 잡은 쪽도 자기가 들고 있다고 믿고 캐리 자세와
     충돌 무시를 유지한다. 그 쪽이 E를 누르면 C에게 `OnReleased`가 가서 **실제 운반자와 무관하게 C가 내려온다.**
  2. **연쇄 그랩**: A가 B를 든 상태에서 C가 A를 잡으면, A는 들린 상태(`IsMovementLocked`)라 E키가 막혀(`:36`) **B를 내려놓을 수 없다.**
     B는 A의 소켓을, A는 C의 소켓을 따라간다.
  3. **들린 쪽의 거절 경로가 없다**: 대상이 파괴됐거나(`IsBroken`) 뷰를 찾지 못하면 RPC를 조용히 무시하는데, 그랩하는 쪽은 계속 들고 있다고 믿는다.
     `Update`에서 파괴 여부로 정리하는 방어(`:32-33`)가 일부만 막는다.
- 들린 쪽이 **"운반자의 원격 복사본"**을 따라가고 그 결과를 다시 동기화하므로, 제3자 화면에서는 들린 쿠키가 운반자보다 **지연 2단계**만큼 늦게 따라온다.
  소유권 이전 없이 구현한 대가이며, 설계로서는 수긍할 수 있는 트레이드오프다.
- **권장**: 그랩을 요청-응답으로 바꾼다. 들린 쪽이 부착에 성공하면 운반자에게 `OnGrabConfirmed` RPC를 보내고, 운반자는 그때 확정한다.
  들린 쪽은 이미 운반자가 있거나, 자기가 무언가를 들고 있으면 거절한다. 원격 화면에서 "들림" 상태를 알 수 있도록 `PlayerMoveState.Held`는
  이미 동기화되고 있으므로 그랩하는 쪽의 사전 검사에도 쓸 수 있다.

#### E.10.4 "파괴됨" 상태 표현이 4벌 (낮음~중간)

| 표현 | 위치 | 누가 믿나 |
|---|---|---|
| `HideOrSeekPlayer.hitCount`(로컬 필드) | `Unit/HideOrSeekPlayer.cs:70-72` | 소유자 본인의 이동 잠금·그랩 |
| `HitCount` Player Prop | `RoomState.IsBroken` | 관전 후보, 처형 대상, 승패, 결과 화면 |
| `PlayerGrabController.IsOwnerBroken` | `Unit/PlayerGrabController.cs:87-93` | 그랩 대상 거르기(`RoomState.IsBroken`과 같은 로직을 다시 씀) |
| `CookieLifeStatePresenter.appliedState` | `ColorTag/CookieLifeStatePresenter.cs:27, 36` | 표시(공개 `IsBroken`은 아무도 읽지 않음) |

원천은 Player Prop 하나이고 나머지는 파생값이어야 한다. `IsOwnerBroken`은 `RoomState.IsBroken(targetPv.Owner)` 한 줄로 바꾸고,
`hitCount` 로컬 필드는 "Prop 반영 전 즉시 잠금"용이라는 목적을 주석에 밝히거나 `RoomState.IsBroken(pv.Owner) || localBrokenPending`으로 합치는 것을 권장한다.

#### E.10.5 연결 끊김 처리

`OnDisconnected`를 재정의한 곳이 없다. 방 안에서 연결이 끊기면 PUN이 `OnLeftRoom`을 불러 주므로(`LoadBalancingClient.cs:3229-3235`)
`RoomExitController.OnLeftRoom`이 로비로 보내고, `LobbyController.Start`가 다시 접속한다. **대부분의 경우 복구된다.**
예외는 **메시지 큐를 멈추고 대기 중인 괴물**이다. 큐가 멈춰 있으면 끊김 콜백도 대기 종료(최대 약 63초)까지 처리되지 않는다.
그 사이 괴물 화면은 카운트다운을 계속 보여 준다. 대기 중에는 `PhotonNetwork.NetworkClientState`를 Update에서 직접 확인해 즉시 로비로 보내는 방어를 권장한다.

### E.11 중복 로직 — **주의**

#### E.11.1 방장 진행 로직의 "폴링 + 요청 중 플래그" 패턴 7벌 (중간)

| 컴포넌트 | Update 폴링 조건 | 중복 요청 방지 수단 |
|---|---|---|
| `MonsterAssignmentAuthority` | 정원·마감 시각 | `deadlineRequested`, `confirmRequested`, `resetRequested` |
| `GamePhaseStarter` | `PaintPhaseEndTime` 없음 | `started` |
| `PaintPhaseController` | 색칠 종료 | Room Prop 존재 |
| `MonsterJoinController` | 색칠 종료 | `joinRequested` + Room Prop |
| `GameRuleController` | Hunt 단계 | 없음 — 응답 전까지 **매 프레임 `SetCustomProperties(GameResult)`를 다시 보낸다** |
| `RoomLifecycleWatcher` | 복귀 시각 | `monstersGoneAt = null` |
| `MasterClientPolicy` | 첫 InRoom 프레임 | `requestedActor` |

- 모두 "방장만 → 조건 확인 → 서버 응답 전 중복 방지 → Room Prop 기록"이라는 같은 뼈대를 각자 다시 구현한다.
  그중 **`GameRuleController`만 방지 수단이 없어** 승패가 정해진 뒤 서버 응답(보통 RTT 50~150ms)이 올 때까지 매 프레임 같은 속성 변경을 보낸다.
  속성 이벤트가 여러 번 전파되고, `ResultScreenController`는 `isShown`으로 막지만 다른 수신자는 여러 번 처리한다.
- Photon은 이런 경우를 위해 **CAS**(`SetCustomProperties(props, expectedProperties)`)를 제공한다. 예를 들어 `GameResult`를 "`GameResult`가 아직 없을 때만"
  조건으로 쓰면, 방장이 바뀌는 순간 두 클라이언트가 동시에 쓰는 경쟁도 서버가 막아 준다. 현재 코드는 CAS를 한 곳도 쓰지 않는다.
- **권장**: `MasterRoomWriter.TrySetOnce(key, value)` 같은 공용 헬퍼(로컬 대기 플래그 + `expectedProperties = {key: null}`)를 Core에 두고 7곳이 이를 쓰게 한다.

#### E.11.2 스폰 로직 3벌 + 프리팹 이름 문자열 3곳 (낮음~중간)

- `PlayerSpawner.SpawnLocalPlayer`(`:54-86`), `MonsterJoinController.TryLocalSpawn`(`:64-81`), `MonsterTestSpawner.SpawnWhenInRoom`(`:17-32`)이
  모두 "`GameObject.Find(이름)` → 없으면 경고 → (빈 곳 찾기) → `PhotonNetwork.Instantiate(문자열)`"을 되풀이한다.
  `SceneSpawnPoints.TryFindClearPosition`이 이미 앞의 절반을 한다. 테스트 스포너는 빈 곳 찾기를 빼먹었다.
- 프리팹 이름: `"HideOrSeekPlayer"`(`PlayerSpawner.cs:9`), `"MonsterPlayer"`(`MonsterJoinController.cs:14`, `MonsterTestSpawner.cs:10`).
  RPC 이름과 씬 이름은 상수로 모았는데 프리팹 이름만 흩어져 있다.
- `MonsterJoinController`의 `MonsterSpawnPointName`, `MonsterTestSpawner`의 `SpawnPointName`, `PlayerSpawner`의 `SpawnPointName`은 `SceneSpawnPoints` 상수를 다시 이름만 바꾼 별칭이다.
- **권장**: `CharacterSpawner.Spawn(CharacterRole role, float range)` 하나로 합치고 `NetPrefabs.Cookie/Monster` 상수를 둔다.

#### E.11.3 캐릭터 이동 골격 2벌 (낮음~중간)

`HideOrSeekPlayer`와 `MonsterController`에 다음이 거의 같게 반복된다.

| 중복 | 쿠키 | 괴물 |
|---|---|---|
| Awake에서 내 캐릭터면 `Camera_Ctrl.InitCamera` | `:144-148` | `:76-91` |
| Start에서 Rigidbody 설정(원격 kinematic, 로컬 중력·연속 충돌·보간·회전 고정) | `:161-168` | `:95-103` |
| `RespawnToSpawnPoint`(스폰 지점 → 속도 0 → `rb.position` + `transform.position`) | `:359-373` | `:239-252` |
| `OnEnable`/`OnDisable`에서 `CharacterRegistry` 등록 | `:186-196` | `:64-74` |
| 원격이면 Update에서 보간 + 상태 반영 | `:210-215` | `:124-129` |
| 트리거 기반 애니메이션 상태 전환(`ResetTrigger(prev)` → `SetTrigger(new)`) | `PlayerAnimationDriver.ChangeState` | `MonsterController.ChangeState` `:264-272` |
| 카메라 기준 이동 방향 | `PlayerInput.CameraRelativeMove` 직접 호출 | `ReadCameraRelativeMoveInput` 래퍼 |

`IGameCharacter`/`IRespawnable`/`NetworkTransformSync<T>`로 **계약과 동기화는 이미 공통화**했다. 남은 것은 "구현 골격"이다.
`NetworkCharacterBase<TState>`(추상 `MonoBehaviourPunCallbacks`)에 위 6개를 올리고 쿠키·괴물은 입력과 고유 동작만 구현하게 하면 캐릭터 종류를 늘리기 쉬워진다.
`AnimatorTriggerStateDriver<TState>`로 애니메이션 전환도 공유할 수 있다.

#### E.11.4 색칠 남은 시간이 화면에 두 번 표시된다 (UI 중복, 낮음~중간)

- `GameScene/Canvas/ColorSlotPanel`의 `ColorSelectionPanel.timeLabel`이 남은 초를 표시한다(`ColorTag/ColorSelectionPanel.cs:31-35`).
- 같은 캔버스의 `Canvas/PaintCountdown`(`PhaseCountdownDisplay`, phase=Paint, 형식 "변장 시간 mm:ss")도 **같은 값을 동시에** 표시한다.
- 두 컴포넌트가 같은 `GamePhaseState.TryGetActiveEndTime(Paint)` 계산을 각자 한다. `CanvasGroup` 표시/숨김 + `lastVisible` 캐시 코드도 두 벌이다.
- **권장**: 디자인상 하나만 남긴다. 슬롯 패널의 `timeLabel` 연결을 비우고 `PhaseCountdownDisplay`로 통일하면 코드 수정이 거의 없다.

#### E.11.5 `PaintPhaseEndTime`을 쓰는 곳이 2곳이고 계산식이 다르다 (낮음)

- `GameStartAuthority.TryStart`: `now + SceneLoadGrace + PaintPhaseDuration`(`Lobby/GameStartAuthority.cs:61-64`)
- `GamePhaseStarter.Update`: `now + PaintPhaseDuration`(`Monster/GamePhaseStarter.cs:26-29`) — "대기실을 거치지 않는 경로(테스트 씬 등)를 위한 대비책"

정상 흐름에서 `GamePhaseStarter`는 아무것도 하지 않는다. 그런데 이 컴포넌트는 `GameScene`에 있고 `PlayerTestScene`에는 **없다**(§D). 즉 주석의
의도와 달리 대비책이 필요한 씬에는 없고, 필요 없는 씬에만 있다. 대기실을 우회하는 개발 경로가 정말 필요하면 `GameStartAuthority`의 기록 함수를 공개해
그쪽을 부르게 하고, 필요 없으면 컴포넌트를 지운다.

#### E.11.6 그 밖의 작은 중복

| 중복 | 위치 |
|---|---|
| `IsPaintPhaseActive() => GamePhaseState.IsPaintActive` 래퍼 | `BrushCursorController.cs:92`, `PlayerPaintCanvas.cs:346` |
| `IsOwnerBroken`이 `RoomState.IsBroken`을 다시 구현 | `PlayerGrabController.cs:87-93` |
| 표시 이름("닉네임 없으면 #번호") | `GameLobbyController.DisplayName`(`:181-185`), `SpectatorController.GetDisplayName`(`:132-137`), `MonsterRevealController.BuildText`(`:74`) |
| 괴물 닉네임 목록 만들기 | `GameLobbyController.BuildMonsterNames`(`:187-199`), `MonsterRevealController.BuildText`(`:71-75`) |
| `IsRoomFull()` 래퍼 | `GameLobbyController.cs:136`(`RoomState.IsRoomFull` 그대로 호출) |
| PlayerList → ActorNumber 배열 변환 | `RoomState.HostActor`(`:87-88`), `RoomState.DesiredMasterActor`(`:104-105`) |
| "파괴 여부 → 이동/그랩 잠금" 판단 | `HideOrSeekPlayer.IsMovementLocked`, `PlayerGrabController.Update`(`:36`)가 각자 조합 |

`Player` 표시 이름은 `RoomState.DisplayName(Player)` 하나로 모으는 것이 가장 효과가 크다(3곳).

---

## F. 그 밖의 발견 사항 및 권장 로드맵

### F.1 그 밖의 발견 사항 (11개 관점에 들지 않지만 확인한 것)

| # | 내용 | 근거 | 영향 |
|---|---|---|---|
| F1 | **괴물이 모두 나간 5초 동안 생존 시간이 끝나면** `GameRuleController`가 `CookiesWin`을 기록하고, 결과 화면(12초 뒤 복귀)과 이탈 배너(5초 뒤 복귀)가 함께 뜬다. 복귀도 두 경로에서 각각 시도한다 | `GameRuleController.cs:16`, `RoomLifecycleWatcher.cs:43-51` | 낮음. 괴물이 없으면(`MonsterActorNumbers`가 빈 배열) 승패 판정을 멈추게 하면 된다 |
| F2 | 결과 화면에서 방장이 아닌 사람이 `OnLobbyButtonClicked`를 부르면 **자기 자동 복귀 코루틴만 멈추고** 아무 일도 하지 않는다. 그 뒤 방장이 나가 이 사람이 방장이 되면 복귀가 영영 일어나지 않는다. 지금은 연결된 버튼이 없어 드러나지 않는다 | `ResultScreenController.cs:104-109` | 낮음(잠재) |
| F3 | 강제 도포 판정 시점에 **막 등록한 슬롯의 `RegisteredSlotCount`가 아직 방장에게 도착하지 않았으면** 그 쿠키도 강제 도포 대상이 되어 칠한 내용이 덮인다 | `PaintPhaseController.cs:44-52` | 낮음(마지막 RTT 안에 첫 슬롯을 등록한 경우만) |
| F4 | 회피 속도를 `speed *= 2` / `speed *= 0.5`로 **필드를 직접 바꿔** 처리한다. 회피 도중 들리거나 파괴돼 Update가 조기 반환하면 `dodgeTimer`가 멈춘 채 `isDodge`가 남고, 내려온 뒤 남은 회피가 이어진다 | `HideOrSeekPlayer.cs:296-324` | 낮음. `baseSpeed * dodgeMultiplier`로 계산만 하게 바꾸면 된다 |
| F5 | `MonsterRevealTime` Room Prop은 쓰기만 하고 읽는 곳이 없다 | `MonsterAssignmentAuthority.cs:86, 160` | 정리 후보 |
| F6 | `CookieLifeStatePresenter.IsBroken`, `MonsterLobbyWaitController.IsWaiting`은 public인데 읽는 곳이 없다 | — | 정리 후보 |
| F7 | 모든 쿠키의 `PlayerPaintCanvas`가 모든 `PaintStroke` 이벤트를 받아 viewId로 거른다(4인 기준 이벤트 하나당 4번 확인) | `PlayerPaintCanvas.cs:402-406` | 무시 가능. 8인 이상이면 viewId → 캔버스 사전으로 한 번에 보내는 편이 낫다 |
| F8 | 괴물 `CapsuleCollider` 반지름이 0.1이다(쿠키 0.46). 프리팹 스케일을 확인하지 않았다면 벽 통과·끼임 원인이 될 수 있다 | `MonsterPlayer.prefab` | 확인 필요 |
| F9 | `GameLobbyController.RefreshPlayerList`가 입장 한 번에 두 번 실행된다(§E.9) | `:57-63`, `:79-83` | 무시 가능 |

### F.2 권장 로드맵

**1단계 — 버그와 네트워크 틈 (반나절)**
1. `PlayerInput`에 게임 입력 잠금 추가, 채팅은 `PlayerInput`만 잠금 → `GameManager`의 `HideOrSeekPlayer` 의존, `MonsterLobbyWaitController`의 `GameManager` 끄기 제거 (§E.1.1, §E.8.3)
2. 수신 4곳에 보낸 사람 검증 추가, `ClaimMonster`는 `photonEvent.Sender` 사용 (§E.10.2)
3. `GameRuleController`에 중복 기록 방지(또는 CAS) (§E.11.1)
4. 색칠 남은 시간 UI 하나로 정리 (§E.11.4)

**2단계 — 책임과 중복 정리 (1~2일)**
5. 그랩 요청-응답화, 이중·연쇄 그랩 거절 (§E.10.3)
6. `NetworkSessionPolicy`로 전역 네트워크 설정 변경 일원화 (§E.1.2)
7. 스폰 통합(`CharacterSpawner` + `NetPrefabs`), `PlayerSpawner`의 Dev 의존 제거 (§E.11.2, §E.1.4)
8. `StartGameRequest` 수신을 UI 밖으로 이동 (§E.1.3)
9. 콜백을 쓰지 않는 6개 클래스를 `MonoBehaviour`로, 쓰지 않는 씬 PhotonView 2개 제거, 옛 `ColorSelectionPanel.prefab` 정리 (§E.8.2, §E.3.2, §E.3.3)

**3단계 — 확장 대비 (필요할 때)**
10. `NetworkCharacterBase<TState>`로 캐릭터 골격 공통화 (§E.11.3)
11. `Monster/` 폴더를 `Round/`·`Spectator/`로 나누고 asmdef를 단방향으로 분리 (§E.2.2)
12. 캐릭터별 스탯 SO, 팔레트 참조 단일화 (§E.3.4, §E.6)
13. 방장 전용 Room Prop 기록을 `MasterRoomWriter`(CAS)로 통일 (§E.11.1)

---

## G. 2026-09-27 추가·수정 사항

### G.1 변경 목록 (커밋 `7e7feba` 대비 작업 트리)

`git status`로 찾은 변경 가운데 빌드 산출물(`TagOfChaosGame/`)과 폰트 아틀라스 재생성을 뺀 전부다.

| 구분 | 경로 | 내용 |
|---|---|---|
| 신규 | `리소스/WitchCookieHouse/` (Unity 밖, 20MB) | Blender 원본 `.blend`(+`.blend1` 백업, gitignore 대상), `BlenderScripts/` 3개(475줄), 검수 렌더 15장, 텍스처 원본 2장, README |
| 신규 | `Assets/09. Environment/WitchCookieHouse/` (3.6MB) | `Witch_Cookie_House.fbx`, 머티리얼 22개, 텍스처 2장(512px), 문 클립 8개 + 연기 루프 클립 1개, Animator Controller 5개, README |
| 신규 | `Assets/04. Prefabs/Environment/Witch_Cookie_House.prefab` | FBX 모델의 프리팹 변형(Animator 5, kinematic Rigidbody 4, 스크립트 없음) |
| 신규 | `Assets/Editor/WitchCookieHouse/` | `WitchCookieHouseImportPostprocessor.cs`(84줄), `WitchCookieHouseBuilder.cs`(387줄) |
| 신규 | `Assets/08. Resources/LobbySceneBackGround.png` | 로비 배경 이미지(참조처 없음) |
| 수정 | `Assets/Scenes/PlayerTestScene.unity` | 씬에 놓인 `MonsterPlayer` 인스턴스를 **비활성화**하고 위치를 (−13.2, 19.9, …)로 옮김(오버라이드 3줄) |
| 삭제 | `Plan.md/PalletEx.png`, `Plan.md/몬스터 컨트롤러.png` | 기획 참고 이미지 삭제 |
| 수정 | `Plan.md/research.md` | 이 문서 |

**`Assets/02. Scripts/`는 한 줄도 바뀌지 않았다.** 과자집은 아직 게임 코드와 연결되지 않은 순수 에셋 추가다.

### G.2 과자집 사양 요약 (README와 파일 실측)

| 항목 | 값 |
|---|---|
| 단위 / 원점 | 1 unit = 1 m, 원점 = 건물 바닥 중앙(바닥 윗면 y = 0.03, 지면과 z-fighting 방지) |
| 방향 | Unity +Z = 정면(Front), −Z = 후면, +X = 좌, −X = 우 |
| 전체 크기 | 약 18.7 × 21.0(연기 포함) × 18.7 m, 벽 몸체 15 × 15 m, 처마 7.5 m, 용마루 15.85 m |
| 내부 | 14 × 14 m, 천장은 지붕 안쪽까지 트임 |
| 문 | 4면에 하나씩, 개구부 1.5 × 3.0 m(쿠키 캡슐 폭 0.92 m의 약 1.5배), 모두 **안쪽으로 −90° 열림**, 피벗 = 경첩 |
| 폴리곤 / 렌더러 | 약 10만 삼각형, 렌더러 149개, 머티리얼 22종(모두 Opaque, Built-in Standard) |
| 충돌체 | `Collision/COL_*` 42개, 전부 **convex MeshCollider**, 렌더러 없음. 문짝 충돌체 `COL_Door_{Side}`는 문 자식이라 함께 회전 |
| 애니메이션 | 문마다 Animator(Bool `IsOpen`, Closed → Open ↔ Close, 18프레임 = 0.6초), 굴뚝 연기 6덩어리 18프레임 루프(투명 재질 없이 크기로 소멸 표현) |
| 레이어 | 전부 Default(0) — 쿠키 접지(`groundLayer = Default`)와 괴물 돌진 장애물(`obstructionMask = Default`)에 그대로 걸린다 |
| Static | 문(`Doors`)과 연기를 뺀 나머지 전부 ContributeGI·BatchingStatic·Occluder/Occludee·ReflectionProbe Static |

충돌체 구성은 프로젝트 원칙(캐릭터·소품 콜라이더는 convex)에 맞다. 개구부가 있는 벽을 `COL_Wall_{Side}_L/_R/_Top`처럼
**볼록 조각으로 나눠** 비볼록 MeshCollider 없이 문 구멍을 통과할 수 있게 만든 점이 좋다.

### G.3 에디터 도구 분석

#### G.3.1 `WitchCookieHouseImportPostprocessor` (`Assets/Editor/WitchCookieHouse/WitchCookieHouseImportPostprocessor.cs`)

- 대상 경로 한 곳(`ModelPath`)에만 적용되는 `AssetPostprocessor`. `GetVersion() => 2`로 규칙 변경 시 재임포트를 강제한다(`:16`).
- `OnPreprocessModel`(`:18-36`): 스케일 1, 축 변환 굽기 끔(Blender에서 이미 처리), 애니메이션·카메라·라이트·블렌드셰이프 끔,
  노멀 임포트 + MikkTSpace 탄젠트, 라이트맵 UV 생성, 머티리얼을 `Materials/` 폴더의 `.mat`으로 이름 기준 리맵.
- `OnPreprocessTexture`(`:38-43`): 노멀맵 한 장만 `NormalMap` 타입으로.
- `OnPostprocessModel`(`:45-64`): `COL_` 접두 오브젝트에 convex `MeshCollider`를 붙이고 렌더러·필터를 지운다.
  **이름 규칙 하나로 "보이는 메시 / 충돌 메시"를 나누는 것은 흔히 쓰는 안정적인 방식이다.**
- 리맵 결과는 `.fbx.meta`의 `externalObjects`에 22개 모두 저장돼 있어, 새로 클론해 Library 없이 열어도 머티리얼이 제대로 연결된다(확인함).

#### G.3.2 `WitchCookieHouseBuilder` (`Assets/Editor/WitchCookieHouse/WitchCookieHouseBuilder.cs`)

- `[InitializeOnLoad]` + `delayCall`로 **에디터가 켜질 때 프리팹이 없으면 한 번 자동 빌드**한다(`:36-51`). 메뉴 `Tools/TagOfChaos/Build Witch Cookie House`로 다시 실행할 수 있다.
- 빌드(`:54-115`): 프리뷰 씬에 모델을 놓고 → 문마다 열기·닫기 클립(열기 ease-out, 닫기 ease-in-out), 컨트롤러, Animator(`AlwaysAnimate`),
  kinematic Rigidbody를 붙이고 → 연기 루프 클립과 컨트롤러(`CullCompletely`) → Static 지정 → 프리팹 저장 → `Verify()`.
- 검증(`:294-382`, 메뉴 `Verify Witch Cookie House`): 문이 경첩에 고정된 채 **안쪽으로** 열리는지, 충돌체가 따라 도는지, 연기 루프 이음새 오차,
  머티리얼 슬롯이 전부 `Materials/`를 가리키는지, 빈 메시, 충돌체에 렌더러가 남았는지를 검사해 Console에 남긴다.
  README의 검증 로그는 `VERIFY ALL OK`다. **에셋 생성물을 스스로 검증하는 도구까지 갖춘 점은 이 프로젝트의 다른 에디터 도구(`UILayoutValidator`)와 같은 좋은 습관이다.**

주의할 점:

| # | 내용 | 근거 | 영향 / 권장 |
|---|---|---|---|
| G3-1 | 컨트롤러는 빌드할 때마다 **지우고 새로 만든다**(`RecreateController`, `:247-252`). 클립은 기존 에셋을 재사용한다(`LoadOrCreateClip`, `:203-211`). 컨트롤러의 GUID가 빌드마다 바뀐다 | `:247-252` | 프리팹은 같은 빌드에서 다시 저장되니 괜찮다. 하지만 씬에 배치한 인스턴스가 컨트롤러를 오버라이드하거나 다른 에셋이 컨트롤러를 참조하면 **재빌드 한 번에 끊어진다.** 클립처럼 기존 컨트롤러를 비우고 재사용하는 방식으로 통일을 권장 |
| G3-2 | 에디터를 열 때 자동으로 에셋을 쓰는 부수 효과가 있다 | `:36-51` | 프리팹이 커밋돼 있어 평소에는 아무 일도 하지 않는다. 누군가 프리팹을 지우거나 이동하면 다음 에디터 실행 때 조용히 다시 생긴다. 의도된 동작이면 README에 한 줄 적어 두는 정도로 충분하다 |
| G3-3 | 문 Animator의 Update Mode가 **Normal**(`m_UpdateMode: 0`)이다 | 프리팹 실측 | 애니메이션으로 kinematic Rigidbody를 움직일 때는 `AnimatePhysics`가 권장이다. Normal이면 문 충돌체가 물리 스텝과 어긋나 움직여, 문이 여닫힐 때 옆의 쿠키를 밀지 못하고 뚫고 들어가거나 튕길 수 있다. 빌더에서 `animator.updateMode = AnimatorUpdateMode.Fixed`(Unity 6 이름)로 설정 권장 |
| G3-4 | 연기 클립 생성은 연기 덩어리가 2개 이상이라고 가정한다(`slotPos[n - 2]`, `:158`) | `:150-160` | 모델을 수정해 덩어리가 1개 이하가 되면 `IndexOutOfRangeException`. 지금 모델(6개)에서는 문제없다 |
| G3-5 | 편집기 전용 asmdef(`TagOfChaos.Editor`, platforms = Editor)에 포함된다 | `Assets/Editor/TagOfChaos.Editor.asmdef` | 빌드에 들어가지 않으므로 런타임 영향은 없다. 런타임에서 `IsOpen` 상수(`DoorIsOpenParam`)를 쓰고 싶어도 이 클래스에서는 참조할 수 없다 — 런타임 문 스크립트를 만들면 상수를 그쪽(Core 또는 Environment)으로 옮겨야 한다 |

### G.4 Blender 파이프라인 (`리소스/WitchCookieHouse/BlenderScripts/`)

| 파일 | 역할 |
|---|---|
| `wch_lib.py`(357줄) | 공용 헬퍼. 치수 상수(벽 반폭 7.5, 두께 0.5, 문 개구부 1.5×3.0 등), 컬렉션 관리, bmesh 기본형(박스·구·원기둥·토러스·튜브·프리즘·보석·꼬인 사탕 기둥), 모디파이어 굽기(베벨·불리언), 프로토타입 메시 링크 복제, Smart UV. 첫 줄 주석대로 **"Blender MCP 호출마다 먼저 exec"**하는 전제로 만들어졌다 |
| `wch_export.py`(77줄) | Unity용 FBX 내보내기. `-Z forward, Y up`, `bake_space_transform=True`, 단위·스케일 적용. Blender 5.2 FBX 익스포터가 **계층이 깊은 오브젝트에 X 90° 회전을 잘못 붙이는 버그**를 내보내는 동안만 몽키패치(`fbx_object_matrix`를 `G @ M @ G⁻¹`로 대체)로 우회한다. 내보낸 FBX를 다시 파싱해 트랜스폼을 검증하는 `fbx_models()`도 있다 |
| `wch_render.py`(41줄) | 충돌체를 숨기고 지정 시점에서 EEVEE로 검수 렌더를 찍는다 |

주의할 점:

- **절대 경로가 코드에 박혀 있다**(`wch_export.py:8`의 `F:\3DClaudeTagOfChaos\…`, `wch_render.py:3`). 다른 PC나 다른 드라이브에서는 인자로 경로를 넘겨야 한다.
- `wch_export.py`와 `wch_render.py`는 `bpy`, `COLL_NAME`, `coll()`, `Vector`를 **자체적으로 import하지 않는다.** `wch_lib.py`를 먼저 exec해야만 동작한다
  (README에도 적혀 있다). 셸에서 `blender.exe --background Witch_Cookie_House.blend --python wch_export.py`처럼 단독 실행하면 `NameError`가 난다.
  단독 실행용 진입 스크립트(`wch_lib` exec → `export_fbx()` 호출)를 하나 두면 MCP 없이도 재내보내기를 자동화할 수 있다.
- 몽키패치는 Blender 5.2 FBX 익스포터 내부(`io_scene_fbx.fbx_utils.ObjectWrapper.fbx_object_matrix`)에 기대므로, **Blender를 업그레이드하면 먼저 다시 확인**해야 한다.
  `try/finally`로 원래 함수를 반드시 되돌리는 점은 안전하다.
- 모델의 "원본(소스 오브 트루스)"은 `.blend`이고 모델 생성 스크립트 전체는 남아 있지 않다(헬퍼와 내보내기만 있음). 모델을 수정할 때는 `.blend`를 직접 편집해야 한다.

### G.5 게임 통합 관점의 문제 (11개 관점 적용)

과자집은 아직 **어느 씬에도 배치되지 않았다**(프리팹 GUID `7c51f80f…`를 참조하는 씬·프리팹 0개). 아래는 배치할 때 부딪힐 문제다.

| # | 관점 | 내용 | 권장 |
|---|---|---|---|
| G5-1 | Photon Ownership/RPC | **문을 여닫는 런타임 코드가 없고, 네트워크 동기화도 없다**(README도 "현재 동기화 코드는 없음"이라고 적음). 지금 배치하면 문 4개가 닫힌 채 충돌체로 막혀 **집 안에 들어갈 수 없다** | 이 프로젝트의 원칙대로 "상태는 Room Props, 변경은 방장"으로 만든다. 예: 문 4개 상태를 Room Prop `DoorStates`(비트마스크)로 두고, 플레이어는 상호작용 시 방장에게 RaiseEvent로 요청, 모든 클라이언트가 Props 변경을 보고 `SetBool("IsOpen")`. Room Props는 늦게 도착한 괴물(메시지 큐 재생)과 방장 교체에도 일관된다. RPC만 쓰면 괴물 대기 흐름에서 상태가 어긋날 수 있다. 판 초기화가 필요하면 `NetKeys.Scopes`에 Round 수명으로 등록 |
| G5-2 | Scene 의존 | 크기가 **GameScene 바닥(24×24 m)에 비해 매우 크다.** 원점에 두면 벽 바깥 여유가 사방 약 4.5 m(처마 기준 3.2 m, 장식 포함 전체 바운딩 기준 약 2.7 m)뿐이다. 또 `PlayerSpawnPos`(0,0,0)과 `MonsterSpawnPos`(5,0,5)가 **모두 집 안쪽 바닥(±7 m)에 들어간다** | 맵(바닥)을 키우거나 집 위치를 옮기고, 스폰 지점을 다시 배치한다. `SpawnPositionFinder`는 16번 모두 막히면 스폰 지점 자체를 돌려주므로(`Core/SpawnPositionFinder.cs:23`), 스폰 지점이 벽과 겹치면 벽 속에 스폰될 수 있다 |
| G5-3 | Scene 의존 / 카메라 | `Camera_Ctrl`에는 **카메라 충돌 처리가 없다.** 쿠키 카메라 거리 3.2 m, 괴물은 12 m다. 집 안(14×14 m)이나 벽 근처에서는 카메라가 벽 밖으로 나가 벽이 시야를 가린다. 괴물은 특히 심하다 | `Camera_Ctrl.LateUpdate`에 대상→카메라 방향 SphereCast(Default 레이어)로 거리를 줄이는 처리를 추가 |
| G5-4 | 레이어 | 과자집 전체가 Default 레이어다. 쿠키 접지, 괴물 돌진 장애물, 스폰 겹침 검사에 자동으로 걸리므로 **지금 규칙과 맞다.** 다만 괴물 처형 트리거는 레이어 필터가 없어 영향이 없고, 색칠 레이캐스트는 자기 몸 콜라이더만 검사하므로 역시 영향이 없다 | 그대로 두어도 된다. 벽 전용 레이어가 필요해지면(예: 카메라 충돌만 벽에 적용) `Environment` 레이어를 만든다 |
| G5-5 | Prefab/Script 역할 | 프리팹에는 Animator와 Rigidbody만 있고 **스크립트가 없다.** 문 제어 스크립트를 추가할 때 빌더가 프리팹을 **덮어써서 다시 만든다**(`SaveAsPrefabAsset`)는 점에 주의해야 한다. 프리팹에 직접 붙인 컴포넌트는 다음 `Build` 때 사라진다 | 문 제어 컴포넌트는 (1) 빌더가 붙이게 하거나, (2) 과자집 프리팹을 감싸는 **별도 프리팹 변형**(예: `Witch_Cookie_House_Networked.prefab`)에 붙인다. 문 제어를 씬 PhotonView 하나에 모으면 §E.3.3의 "쓰지 않는 씬 PhotonView" 문제도 자연스럽게 해소된다 |
| G5-6 | 성능 | 렌더러 149개, 머티리얼 22개, 약 10만 삼각형. 정적 부분은 BatchingStatic이라 씬에 **배치된 상태로 빌드**하면 정적 배칭이 적용된다. 런타임 `Instantiate`로 생성하면 정적 배칭이 적용되지 않는다 | 씬에 직접 배치한다. 문 4개는 `AlwaysAnimate`라 화면 밖에서도 매 프레임 평가되는데, 닫힌 상태에서는 기본 상태에 모션이 없어 비용이 작다 |
| G5-7 | 중복 | 텍스처 원본(`리소스/…/Textures`)과 Unity 쪽(`09. Environment/…/Textures`)에 같은 이름의 PNG가 두 벌 있다 | 의도된 원본·사본 관계라면 문제없다. 어느 쪽이 원본인지 README에 한 줄 적어 두면 좋다 |
| G5-8 | 기획 | 괴물 눈 창문(`Window_{Side}_Eye`)과 발광 머티리얼(`M_Magic_EyeGlow`)이 있다. 창문 유리는 불투명이라 **안팎이 서로 보이지 않는다** | 숨바꼭질 구조상 "창문으로 안을 들여다보기"를 허용할지 기획 결정이 필요하다 |

11개 관점 중 **Singleton, SO 오용, Event 구독/해제, Object Pool 충돌**은 과자집 추가로 새로 생긴 문제가 없다(런타임 코드가 없으므로).

### G.6 그 밖의 변경

- **`08. Resources/LobbySceneBackGround.png`**: 1673×940 PNG, 텍스처 타입 Default(Sprite 아님), 최대 크기 2048. **아직 아무 씬·UI도 참조하지 않는다.**
  UI `Image`에 쓰려면 텍스처 타입을 `Sprite (2D and UI)`로 바꿔야 한다(`RawImage`면 지금 그대로 가능). 또 폴더 이름이 `08. Resources`라 Unity의 특수 폴더
  `Resources`와 헷갈리기 쉽다. 실제로는 특수 폴더가 **아니다**(이름이 정확히 `Resources`여야 함). CLAUDE.md 폴더 규칙에는 이 폴더가 없으므로, 규칙에 추가하거나
  `05. Materials`처럼 용도가 드러나는 이름(예: `08. Textures/UI`)을 권장한다. `09. Environment`도 마찬가지로 CLAUDE.md 폴더 규칙 표에 추가해야 한다.
- **`PlayerTestScene`**: 씬에 직접 놓여 있던 `MonsterPlayer` 인스턴스를 비활성화하고 멀리 옮겼다. 테스트 씬의 괴물은 `MonsterTestSpawner`가 `PhotonNetwork.Instantiate`로
  만드는 것이 정상 경로이므로, 씬에 놓인 비활성 사본은 PhotonView 없이 떠 있는 잔재다. 다시 쓸 계획이 없으면 삭제하는 편이 깔끔하다.
- **기획 이미지 2장 삭제**(`Plan.md/PalletEx.png`, `Plan.md/몬스터 컨트롤러.png`): 삭제가 커밋되지 않은 상태다. 다른 문서가 이 이미지를 링크하는지는 확인하지 않았다.

### G.7 과자집 관련 권장 조치 (우선순위순)

1. **문 네트워크 제어 컴포넌트**를 만든다 — Room Prop 기반, 방장 확정(§G5-1). 이게 없으면 배치해도 집이 막힌 상자다.
2. **맵 크기와 스폰 지점 재배치**(§G5-2), **카메라 벽 충돌**(§G5-3).
3. 빌더에서 문 Animator를 **AnimatePhysics**로, 컨트롤러는 **재생성 대신 재사용**(§G3-1, §G3-3).
4. Blender 스크립트의 **절대 경로 제거와 단독 실행 진입점** 추가(§G.4). `blender.exe --background`로 재내보내기를 자동화할 수 있다.
5. CLAUDE.md 폴더 규칙에 `08.`, `09.` 폴더와 `Assets/Editor/{기능}/` 하위 구조를 추가(§G.6).

---

## H. 2026-09-27 추가 2 — 과자집 소품·환경 프리팹

§G를 쓴 뒤 작업 트리에 새로 들어온 파일을 `research.md`보다 수정 시각이 늦은 파일로 전수 조사했다. 스크립트 4개는 모두 읽었고,
FBX 56개는 바이너리에서 `COL_` 노드를 세었으며, 레이아웃 JSON은 스크립트로 전수 분석했다. 렌더(`L01_Top`, `L06_Interior_Overview`)도 직접 확인했다.

### H.1 변경 목록

| 구분 | 경로 | 내용 |
|---|---|---|
| 신규 | `Assets/09. Environment/WitchCookieProps/{Ground,Interior,Exterior,Decoration,Effects}/*.fbx` | 소품 FBX **56개**(Ground 10, Interior 10, Exterior 16, Decoration 18, Effects 2), 개당 16~193KB |
| 신규 | `…/WitchCookieProps/Materials/*.mat` | 새 머티리얼 **32개**(Built-in Standard). 나머지는 과자집 머티리얼 22개를 이름으로 재사용 |
| 신규 | `…/WitchCookieProps/Textures/` | `T_Soil_BaseColor`(4 m 반복), `T_Mushroom_Gradient` |
| 신규 | `…/WitchCookieProps/Witch_Cookie_Layout.json` | Blender 배치를 Unity 좌표로 내보낸 **407개** 항목(이름·구역·에셋·위치·쿼터니언·균일 스케일) |
| 신규 | `…/WitchCookieProps/README.md` | 소품 사양서(`리소스/WitchCookieHouse/README_Props.md`와 내용 동일) |
| 신규 | `Assets/04. Prefabs/Environment/WitchCookieProps/{카테고리}/*.prefab` | 소품 프리팹 **56개**(FBX 모델 프리팹 변형, 컴포넌트 추가 없음, Static 지정) |
| 신규 | `Assets/04. Prefabs/Environment/Witch_Cookie_Environment.prefab` | 과자집 1 + 소품 407개 **중첩 프리팹 408개**, 파일 1.2MB, GameObject 3개(루트, `Interior`, `Exterior`) |
| 신규 | `Assets/Editor/WitchCookieHouse/WitchCookiePropsImportPostprocessor.cs`(48줄) | 소품 FBX 임포트 규칙 |
| 신규 | `Assets/Editor/WitchCookieHouse/WitchCookiePropsBuilder.cs`(295줄) | 소품 프리팹·환경 프리팹 빌더와 검증 |
| 수정 | `Assets/Editor/WitchCookieHouse/WitchCookieHouseImportPostprocessor.cs`(84 → 95줄) | 충돌체 변환을 `ConvertCollisionObjects`로, 머티리얼 리맵을 **여러 폴더 우선순위** 지원으로 공용화(소품 임포터가 재사용) |
| 신규 | `리소스/WitchCookieHouse/Renders/{Blockout,Layout,Props}/` | 검수 렌더 34장(렌더 폴더 전체 60MB) |
| 수정 | `리소스/WitchCookieHouse/Witch_Cookie_House.blend` | 소품과 배치가 추가됨(3.6MB). 에셋은 `A_<이름>` 컬렉션, 배치는 `WCH_Layout` 컬렉션 |

**런타임 스크립트, 빌드 대상 씬 3개는 바뀌지 않았다.** 새 프리팹 두 개(`Witch_Cookie_House`, `Witch_Cookie_Environment`)를 참조하는 씬은 여전히 **0개**다.
README는 치수를 "로비 씬 임시 배치에 맞췄다"고 적었지만 실제 씬 배치는 아직 없다.

### H.2 무엇을 만들었나 — 레이아웃 실측

- **범위**: 배치 좌표 x −33.9 ~ 33.1 m, z −30.4 ~ 28.1 m, y 0 ~ 9.75 m. 검증 로그상 환경 전체 바운딩은 **128 × 21.1 × 128 m**(원거리 지면 `Ground_Exterior_Far` 128 m 포함).
- **지면**: 과자집 주변에 8 m 모듈 `Ground_Exterior` 25장을 5 × 5로 깔아 **40 × 40 m 마당**을 만들고, 그 밖을 128 m 원거리 지면이 채운다.
  마당 가장자리는 사탕 울타리(직선 45, 모서리 4, 부서진 3)로 둘렀고, 과자집 네 문에서 쿠키 조각 길(`Ground_Cookie_Path` 20장)이 뻗는다.
- **구역별 개수**: `Exterior` 319개, `Interior` 88개. 가장 많은 것은 낙엽 64, 울타리 45, 지면 모듈 25, 쿠키 길 20, 랜턴·꽃 각 16, 물약병 44(4종 합계).
- **실내**(렌더 `L06`): 체크무늬 바닥 + 마법진, 물약 선반 3, 마녀 테이블 2, 의자 3, 책, 촛불, 해골, 거미줄. 실내 소품의 대부분(물약병 44개, 촛불, 사탕 병)은 충돌체가 없는 작은 장식이다.
- **충돌체**: 소품 56종 중 **24종에만 `COL_` 볼록 충돌체**(합계 32개, README와 일치)가 있다. 나무 3종·거대 버섯 3종·울타리 3종·테이블·의자·선반·파라솔 등 큰 것에는 있고,
  **지면 10종 전부와 낙엽·물약병·책·거미줄·이펙트에는 없다**(바이너리 실측).
- **재질**: 투명은 `M_Glass_Clear` 하나뿐(Standard Transparent, 큐 3000). 안개(`M_FX_Mist`)·반딧불(`M_FX_Firefly`)·거미줄(`M_Web`)은 **불투명 + Emission**이다.

### H.3 에디터 도구 분석

#### H.3.1 `WitchCookiePropsImportPostprocessor`

- 경로가 `…/WitchCookieProps/`로 시작하고 `.fbx`로 끝나면 적용(`IsPropModel`, `:21`). 임포트 설정은 과자집 임포터와 **같은 13줄을 복사**했다(`:27-39` ↔ 과자집 `:22-34`).
- 머티리얼은 **소품 폴더 → 과자집 폴더** 순으로 같은 이름을 찾아 리맵하고(`:13-17, :40`), 충돌체 변환은 과자집 임포터의 `ConvertCollisionObjects`를 그대로 부른다(`:46`).
- 리맵 결과는 각 `.fbx.meta`에 저장돼 있다. 다만 `RemapMaterials`가 **슬롯에 실제로 쓰이는지와 무관하게 두 폴더의 머티리얼 54개를 전부 등록**하므로, 56개 `.meta`에 리맵이 **3,024개**(= 56 × 54) 들어 있다.
  동작에는 문제가 없지만 `.meta` diff가 커지고, 머티리얼을 하나 추가할 때마다 56개 `.meta`가 모두 바뀐다.

#### H.3.2 `WitchCookieHouseImportPostprocessor` 리팩터링

- `ConvertCollisionObjects(GameObject)`를 public static으로 분리하고(`:52-69`), `RemapMaterials(importer, params string[] folders)`로 폴더 우선순위(앞 폴더 우선, 같은 이름은 처음 것만)를 지원한다(`:72-94`).
  과자집 자신은 인자 없이 호출해 기존 동작과 같다. **공용화 방향은 좋다.**
- 과자집 임포터의 `GetVersion()`은 2 그대로다. 과자집의 결과물은 바뀌지 않으므로 올리지 않은 것이 맞다.
- 이제 **과자집 임포터 클래스가 소품 임포터의 공용 라이브러리 역할도 겸한다.** 이름(`WitchCookieHouse…`)과 역할이 어긋나므로, 공용 부분은 `EnvironmentImportUtility` 같은 정적 클래스로 옮기고
  임포트 설정 13줄도 거기서 한 번만 정의하는 편이 낫다(§H.5 H5-7).

#### H.3.3 `WitchCookiePropsBuilder`

- `[InitializeOnLoad]`로 **환경 프리팹이 없으면 에디터 로드 때 자동 빌드**(`:43-59`). 메뉴 `Build Witch Cookie Props` / `Verify Witch Cookie Props`.
- 빌드(`:62-101`): 소품 FBX 전부 리맵 확인 → 카테고리 폴더에 소품 프리팹 저장(`Spell_Book_Closed` 외 전부 Static, `:24, :84`) → `BuildEnvironment`.
- `BuildEnvironment`(`:103-147`): JSON을 `JsonUtility`로 읽고, 루트 아래 과자집 프리팹 인스턴스 + `zone`별 그룹에 소품 인스턴스를 위치·회전·**균일 스케일**로 배치해 저장. 없는 에셋은 개수만 세어 에러 로그.
- 검증(`:152-216`): 프리팹 수 = 모델 수, 잘못된 머티리얼 슬롯, 빈 메시, 충돌체에 렌더러 잔존, 볼록 충돌체 수 > 0, 대표 소품 6종 실측 크기 범위, 배치 수 = JSON 항목 수.
  README의 결과는 `prefabs=56/56`, `convexColliders=32`, `placed=407/407`, **ALL OK**.

### H.4 발견한 문제

| # | 심각도 | 내용 | 근거 | 권장 |
|---|---|---|---|---|
| H4-1 | **높음(배치 시)** | **지면 에셋 10종에 충돌체가 하나도 없다.** 40 × 40 m 마당(`Ground_Exterior` 25장)과 128 m 원거리 지면, 쿠키 길, 실내 바닥 타일 모두 보이기만 한다. 걸을 수 있는 면은 과자집의 `COL_Floor`(실내 14 × 14 m)뿐이다. 지금 GameScene의 `Ground` 큐브는 24 × 24 m라서, 환경 프리팹을 그대로 올리면 **24 m 밖의 마당으로 걸어 나가는 순간 떨어져** `VoidKillZone`(y −15)에서 리스폰된다 | FBX 바이너리의 `COL_` 노드 수, `GameScene` Ground 스케일 | 마당 크기의 평평한 `BoxCollider` 하나(40 × 1 × 40 m, 윗면 y = 0)를 환경 프리팹 루트에 두는 것이 가장 싸다. 원거리 지면은 보이지 않는 경계벽으로 막는다. 프로젝트 원칙(소품은 반드시 충돌체) 관점에서도 지면에는 충돌체가 필요하다 |
| H4-2 | 중간 | **레이아웃 항목 4개의 이름 첫 글자가 잘리고 구역이 틀렸다**: `round_Floor`(Ground_Floor), `round_Floor_Debris`, `round_Magic_Circle`, `itch_Table`(Witch_Table). 이 넷은 실내에 있는데 `zone = "Exterior"`로 분류돼 환경 프리팹의 `Exterior` 그룹 아래에 들어간다 | `Witch_Cookie_Layout.json` 전수 분석 | Blender 쪽 이름 처리(접두어 제거)에서 한 글자를 더 자른 것으로 보인다. 내보내기 스크립트를 고쳐 JSON을 다시 만든다. 구역으로 실내·실외를 켜고 끄거나 라이트 설정을 나누면 이 넷이 잘못 처리된다 |
| H4-3 | 중간 | **소품 FBX와 레이아웃 JSON을 만드는 스크립트가 저장소에 없다.** `BlenderScripts/`에는 과자집 때의 3개(`wch_lib`, `wch_export`, `wch_render`)뿐이다. `.blend`에서 소품 56개를 카테고리 폴더로 내보내는 과정과 `WCH_Layout` → JSON 변환은 **재현할 수 없다** | `BlenderScripts/` 목록, 코드 검색 | Blender MCP로 실행한 코드를 `wch_export_props.py`, `wch_export_layout.py`로 저장한다. H4-2를 고치려면 어차피 필요하다 |
| H4-4 | 중간 | 안개·반딧불·거미줄이 **불투명 메시**이고, 빌더가 모든 소품에 **OccluderStatic**을 준다(`MarkStatic`, `:280-286`). 오클루전 컬링을 굽는 순간 얇거나 반투명해야 할 오브젝트(안개, 거미줄, 낙엽, 반딧불)가 가림막으로 계산돼 **뒤쪽 물체가 잘못 컬링될 수 있다** | 머티리얼 `_Mode: 0`, `WitchCookiePropsBuilder.cs:282-284` | Effects·거미줄·낙엽·작은 장식은 `OccluderStatic`을 빼고 `OccludeeStatic`만 준다(과자집 빌더의 `MarkStatic`도 같은 문제가 있다) |
| H4-5 | 중간 | **숨을 곳 관점**: 실내 소품의 대부분(물약병 44, 촛불 8, 사탕 병 5 등)과 실외 낙엽 64·작은 버섯·나무뿌리에 충돌체가 없다. 쿠키가 통과해 겹칠 수 있다. 색칠 위장 게임에서는 **"소품 안에 몸을 묻어 숨기"**가 허용되느냐는 기획 문제가 된다. 괴물의 돌진 경로 검사(`obstructionMask`)도 이 소품들을 무시한다 | FBX `COL_` 실측 | 기획이 정할 일이다. 통과를 막으려면 작은 소품은 개별 충돌체 대신 선반·테이블 단위의 박스 충돌체로 묶는다 |
| H4-6 | 낮음~중간 | **성능 예산**: 과자집 렌더러 149 + 소품 407개 인스턴스(다수가 여러 렌더러), 머티리얼 54종, 발광 재질 다수. Built-in RP 정적 배칭은 씬에 **직접 놓인** Static 오브젝트에만 적용되고, 중첩 프리팹이어도 씬에 배치되면 적용된다. 런타임 `Instantiate`로 만들면 적용되지 않는다. 낙엽 64·울타리 45처럼 반복 개수가 많은 것은 GPU 인스턴싱이 더 유리하다(일부 머티리얼만 `m_EnableInstancingVariants: 1`) | 레이아웃 통계, 머티리얼 | 씬에 직접 배치하고, 반복 소품 머티리얼의 **Enable GPU Instancing**을 켠다. 실제 프레임은 배치 후 프로파일러로 확인해야 한다(이번 조사는 정적 분석만 함) |
| H4-7 | 낮음 | **자동 빌드 순서 경쟁**: 두 빌더 모두 `[InitializeOnLoad]` + `delayCall`이다. 과자집 프리팹과 환경 프리팹이 **둘 다 없을 때**(예: 새 클론에서 프리팹을 지운 경우) 소품 빌더가 먼저 돌면 과자집 프리팹이 없어 조용히 그냥 끝나고(`:57`), **다시 시도하지 않는다** | `WitchCookiePropsBuilder.cs:48-59` | 과자집이 없으면 `delayCall`에 다시 등록하거나, 소품 빌더가 과자집 빌더를 먼저 호출한다. 프리팹이 커밋돼 있는 평소에는 드러나지 않는다 |
| H4-8 | 낮음 | 검증은 JSON 항목 수와 `Interior`+`Exterior` 그룹의 자식 수를 비교한다(`:205-209`). JSON에 새 구역 이름(예: `Ground`)을 쓰면 **배치는 정상이어도 검증이 FAIL**한다. 반대로 그룹 이름을 바꾸면 검증이 틀린다 | `:205-206` | 그룹 이름을 하드코딩하지 말고 과자집 인스턴스를 뺀 모든 자식 그룹을 센다 |
| H4-9 | 낮음 | `LayoutItem.scale`이 **균일 스케일 하나**다. Blender에서 비균일 스케일을 쓴 배치는 조용히 균일로 바뀐다. 지금 JSON은 0.35 ~ 1.6 범위의 균일 값만 있어 문제없다 | `:34, :141` | 내보내기 쪽에서 비균일이면 경고하게 한다 |
| H4-10 | 낮음 | 스케일이 1이 아닌 인스턴스(울타리·나무 등 다수)에 붙은 **convex MeshCollider**는 Unity가 스케일을 반영해 처리하므로 괜찮다. 다만 음수 스케일(반전)은 convex 메시에서 문제가 되는데, JSON에 음수 스케일은 없다(확인) | JSON 분석 | 유지 |
| H4-11 | 낮음 | 에디터 코드 중복: 두 빌더의 `MarkStatic`·`EnsureFolder`·자동 빌드 패턴·검증 로그 패턴, 두 임포터의 임포트 설정 13줄이 거의 같다. 소품 빌더의 `EnsureFolder`는 과자집 빌더 것과 한 줄 다르게 다시 썼다 | 4개 파일 비교 | `EnvironmentBuildUtility`로 묶는다(§H.3.2와 함께) |
| H4-12 | 낮음 | `Spell_Book_Closed`만 Static에서 빼 "표지 여닫기 연출"을 남겼지만, 그 연출을 하는 런타임 코드는 없다. 과자집 문과 같은 상황이다(§G5-1) | `:24` | 연출이 필요하면 문과 같은 네트워크 상태(Room Prop)로 함께 설계한다 |

### H.5 게임 통합 관점 재평가 (§G.5 갱신)

- **맵 크기 문제는 더 커졌다(§G5-2).** 환경의 플레이 가능 마당이 40 × 40 m인데 GameScene 바닥은 24 × 24 m다. 환경 프리팹을 쓰려면 **GameScene의 `Ground` 큐브를 없애고 H4-1의 마당 충돌체로 바꾸는 것**이 자연스럽다.
  이때 스폰 지점(`PlayerSpawnPos` (0,0,0), `MonsterSpawnPos` (5,0,5))은 둘 다 **과자집 실내**에 떨어진다. 실내는 소품이 많아 `SpawnPositionFinder`(반경 5 m, 16회 시도)가 빈 곳을 못 찾을 수 있다 — 마당 쪽으로 옮길 것을 권장한다.
- **대기실(GameLobbyScene)에도 쓸지 결정이 필요하다.** README는 "로비 씬 임시 배치"라고 한다. 대기실에는 가마솥 트리거와 스폰 지점이 있어, 환경을 놓으면 가마솥 위치와 겹치지 않게 다시 배치해야 한다(가마솥 트리거와 스폰이 겹치면 즉시 괴물 신청이 들어가던 옛 버그 §25 P10 참고).
- **카메라 충돌(§G5-3)** 은 실내가 소품으로 채워지면서 더 중요해졌다. 선반·테이블 사이에서 카메라가 벽과 소품을 뚫는다.
- **괴물 돌진(`obstructionMask = Default`)** 은 과자집 벽·큰 소품 충돌체에 걸리므로 벽을 뚫지 않는다. 충돌체가 없는 작은 소품은 통과한다.
- 네트워크 관점에서는 **모든 소품이 정적이라 동기화할 것이 없다.** 문(§G5-1)과 책 표지(H4-12)만 동기화 대상이다.

### H.6 권장 조치 (우선순위순)

1. **마당 충돌체 추가**(H4-1) — 환경을 씬에 놓기 전에 반드시 필요하다.
2. **Blender 내보내기 스크립트 저장 + 레이아웃 이름·구역 오류 수정**(H4-3, H4-2).
3. 이펙트·거미줄·낙엽·작은 장식의 **OccluderStatic 제거**(H4-4), 반복 소품 GPU 인스턴싱(H4-6).
4. 과자집 문 네트워크 제어(§G5-1)와 함께 **GameScene 레이아웃 재구성**: Ground 큐브 교체, 스폰 지점을 마당으로, 카메라 벽 충돌(§H.5).
5. 에디터 도구 공용화(`EnvironmentImportUtility`/`EnvironmentBuildUtility`), 리맵을 실제 사용 슬롯으로 한정(H4-11, §H.3.1), 자동 빌드 순서 보완(H4-7), 검증의 구역 이름 하드코딩 제거(H4-8).

---

## 부록 1. 클래스 간 직접 의존 (정적 허브 경유는 제외)

| 클래스 | 직접 참조하는 대상 |
|---|---|
| `HideOrSeekPlayer` | `Camera_Ctrl`, `PlayerGrabController`, `SpectatorController`(GetComponent), 협력 클래스 4종 |
| `PlayerGrabController` | `HideOrSeekPlayer`(자기·대상, RPC 상수) |
| `PlayerCarryFollower` | `PlayerGrabController`(소켓, `SetCollisionIgnored`) |
| `PlayerSkinApplier` | `PlayerPaintCanvas` |
| `PlayerPaintCanvas` | `PaintPhaseController.IsPaintScene`, `PaintColliderUpdater` |
| `BrushCursorController`, `ColorSelectionPanel`, `ColorSwatchButton`, `PaintToolButton` | `PlayerPaintCanvas.Local` |
| `MonsterController` | `MonsterGrabKillTrigger`, `MonsterTentacleDash`, `Camera_Ctrl`, `HideOrSeekPlayer` |
| `MonsterGrabKillTrigger` | `MonsterController`, `HideOrSeekPlayer` |
| `Cauldron` | `HideOrSeekPlayer`, `MonsterRevealController` |
| `SpectatorController` | `Camera_Ctrl` |
| `SpectatorLabel` | `SpectatorController`(정적 이벤트) |
| `GameManager` | `HideOrSeekPlayer` |
| `RoomExitController` | `GameManager`(RPC 상수 + 공유 PhotonView), `ConfirmDialog` |
| `MonsterLobbyWaitController` | `GameManager`(인스펙터 `Behaviour[]`) |
| `PlayerSpawner` | `OfflineModeBootstrap` |
| `GameLobbyController` | `GameStartAuthority`, `PlayerListItem` |
| `LobbyController` ↔ `RoomListItem` | 양방향(항목이 `JoinRoom` 호출) |
| `VoidKillZone` | `FallGuard` |

## 부록 2. 전역·공유 상태를 쓰는 곳

| 상태 | 쓰는 곳 |
|---|---|
| Room Props | `GameStartAuthority`, `GamePhaseStarter`, `MonsterAssignmentAuthority`, `PaintPhaseController`, `MonsterJoinController`, `GameRuleController`, `RoomLifecycleWatcher`, `RoundStateResetter` — **전부 방장** |
| Player Props | `HideOrSeekPlayer`(HitCount), `PlayerPaintCanvas`(RegisteredSlotCount), `PlayerSkinSelector`(SkinIndex), `RoundStateResetter`(Round 키 삭제), `RoomExitController`(로컬 캐시에서 삭제) — **전부 본인** |
| `Room.IsOpen`/`IsVisible` | `GameStartAuthority`(닫기), `RoundStateResetter`(열기) |
| `SetMasterClient` | `MasterClientPolicy` |
| PUN 전역 설정 | §E.1.2 표 |
| `Cursor.visible` | `BrushCursorController` |
| `Cursor.lockState` | `Camera_Ctrl` |

## 부록 3. 정적 상태 목록

| 정적 상태 | 위치 | 수명/초기화 |
|---|---|---|
| `CharacterRegistry.characters` | Core | 캐릭터가 OnEnable/OnDisable로 관리 |
| `PlayerPaintCanvas.Local` | ColorTag | Start 등록, OnDestroy 해제 |
| `PaintPhaseController.IsPaintScene` | ColorTag | Awake true, OnDestroy false |
| `SpectatorController.SpectateTargetChanged` | Monster | 구독자가 OnEnable/OnDisable로 관리 |
| `GameSettings.cached`, `PlayerInput.bindings` | Core | 첫 접근 때 로드, 앱 수명 |
| `GameStartAuthority.lastStartTime` | Lobby | 앱 수명(3초 창) |
| `OfflineModeBootstrap.SpawnAsMonster` | Dev | Awake 설정, OnDestroy 해제 |
| `SpawnPositionFinder.OverlapBuffer` | Core | 메인 스레드 전용 버퍼 |
| `NetKeys.RoundRoomKeys/RoundPlayerKeys` | Core | 정적 초기화(읽기 전용) |
