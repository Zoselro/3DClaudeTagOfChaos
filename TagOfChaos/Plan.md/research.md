# 조사 보고서: 프로젝트 전체 심층 분석 + 아키텍처 체크리스트 (2026-08-18, 커밋 1280dac 기준)

> 이 문서는 2026-08-17 15:34 커밋(`16c662b`) 기준으로 작성된 이전 버전을 대체한다. 그 사이 추가된
> `1280dac`("게임 룰에 대해 정리 검토중")는 `git diff --stat 16c662b..1280dac`로 직접 대조한 결과
> **`.cs` 스크립트를 단 한 줄도 건드리지 않았다** — 변경분은 `Plan.md/GameRule.md` 신규 작성(1033줄),
> 이전 `research.md` 자체 갱신, 신규 애니메이션 FBX 3종(`Cookie_Carrying`/`Cookie_Hanging_Idle`/
> `Cookie_StandUp`, 아직 어떤 스크립트·Animator Controller도 참조하지 않음), 스크린샷 3장,
> 폰트 애셋 재직렬화뿐이다. 즉 **코드 아키텍처 관점에서는 이전 조사 시점과 현재가 완전히 동일한
> 상태**이며, 이번 조사는 그 사실을 재확인하는 데 그치지 않고 `Assets/02. Scripts/` 아래 **7개
> 도메인 33개 `.cs` 파일 전체를 예외 없이 처음부터 다시 라인 단위로 정독**하고, 4개 씬 파일의
> GameObject 이름을 `grep`으로 전수 대조하며, 사용자가 지정한 11개 아키텍처 관점을 스스로 독립
> 검증했다. 결론은 이전 보고서와 대부분 일치하지만, 이전 보고서가 짚지 않은 **신규 발견 2건**
> (§4.9, §4.10)이 있다.

---

## 0. 프로젝트 개요

**TagOfChaos**는 Unity(Built-in RP) + **Photon PUN2** 기반 2~4인 실시간 멀티플레이어 게임이다.
로비에서 방을 만들고 → 대기방에서 인원을 채우고 → 게임 씬에서 **4라운드 동안 팀 전체가 색을
투표·페인팅으로 정하고** → 그중 1명이 **술래**로 뽑혀 미묘하게 다른 색 조합을 부여받는 숨은
식별 메커니즘의 술래잡기형 게임을 지향한다. 술래잡기 본게임(추격/승패 판정) 자체는 여전히
구현되지 않았다 — 현재까지 완성된 것은 "로비 → 매칭 → 색상 결정 미니게임(코드/에셋 완성, 씬
배선만 누락)"까지다.

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

**스크립트 도메인** (`Assets/02. Scripts/` 아래 7개, 총 33개 `.cs`):

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

## 1. 이전 조사(`16c662b`) 이후 실제로 바뀐 것

`git log --oneline 16c662b..1280dac` 결과 커밋은 `1280dac` 단 하나이고, `git diff --stat`로 확인한
변경 파일은 다음과 같다 — **스크립트(`.cs`)는 0건**:

| 파일 | 성격 |
|---|---|
| `Plan.md/GameRule.md` (신규, 1033줄) | 게임 룰 기획 문서. 코드에는 아직 반영되지 않은 설계 단계 내용 포함 |
| `Plan.md/research.md` | 이전 조사 보고서 자체 갱신(이번에 다시 대체됨) |
| `Assets/Animation/Cookie/Cookie_Carrying.fbx` 외 2종 | 신규 애니메이션 클립. `PlayerAnimator.controller`나 `PlayerMoveState` enum 어디에도 아직 연결되지 않음(§4.9, 신규) |
| `Assets/Screenshots/*.png` 3장 | 스크린샷(개발 기록용, 실행에 무관) |
| `Assets/Fonts/NotoSansKR SDF.asset` | 재직렬화(TMP 폰트 아틀라스 갱신 추정) |

따라서 이번 조사에서 §2(도메인별 동작)·§3(씬 배선)·§5(아키텍처 체크리스트)는 이전 보고서와
동일한 코드를 대상으로 하지만, **모든 서술은 이번 조사에서 직접 라인 단위로 재확인한 결과**이며
이전 보고서를 그대로 옮긴 것이 아니다.

---

## 2. 도메인별 상세 동작

### 2.1 `Core/` — 씬 이름 상수 (1파일)
`SceneNames.cs`: `Lobby`/`GameLobby`/`Game` 3개 문자열 상수. 프로젝트 전체에서 씬 이름을 직접
문자열로 다루는 코드는 이 파일 안에만 존재한다(§5.4에서 재검증).

### 2.2 `Lobby/` — 매치메이킹 (4파일)
- **`LobbyController`**: `ConnectUsingSettings()`→`JoinLobby()`→방 생성/랜덤입장/이름입장 3진입점.
  `OnRoomListUpdate()`가 `RoomInfo` 캐시를 diff 방식으로 `RoomListItem` 프리팹에 반영. `MaxPlayers=4`
  고정, `GameVersion="1"`. 방을 새로 만든 최초 1인만 `GameLobbyScene`으로 로드, 나머지는
  `AutomaticallySyncScene`으로 자동 동행.
- **`GameLobbyController`**: 콜백(`OnPlayerEnteredRoom`/`OnPlayerLeftRoom`/`OnMasterClientSwitched`)
  + `Update()` 안전망(늦게 입장한 클라이언트가 콜백을 놓친 초기 상태를 다음 프레임에 스스로 보정)
  이중 구조. 방장에게만 "게임 시작" 버튼 노출, 정원 찼을 때만 활성. `OnStartGameButtonClicked()`가
  `Room.IsOpen=false` + `LoadLevel(Game)`만 호출 — **`ColorSelectionManager`를 전혀 참조하지
  않음**(§4.1, 여전히 미해결).
- **`RoomListItem`/`PlayerListItem`**: 순수 표시용. `RoomListItem`은 `IsOpen==false`면 입장 버튼 잠금.

### 2.3 `GameManager/` — 채팅·스폰·나가기·낙사 (5파일)
- **`GameManager`**: 채팅 중계 전담으로 축소된 클래스. `static Inst` 필드를 가진 **프로젝트
  유일의 싱글턴**이지만 `GameManager.Inst`를 읽는 코드는 프로젝트 전체에서 0건(재확인, `grep`으로
  직접 검증) — 죽은 코드. `Start()`가 `InRoom==true`가 될 때까지 코루틴 대기 후 "Connected" 로그를
  RPC 브로드캐스트. Enter로 채팅 입력창 토글 + `is_Conversating`을 `HideOrSeekPlayer.IsMovementLocked`에 연결.
- **`PlayerSpawner`**: 스폰 전담. `InRoom==true` 대기 후 `"PlayerSpawnPos"`를 `GameObject.Find()`로
  찾아 반경 5 랜덤 오프셋으로 `PhotonNetwork.Instantiate("HideOrSeekPlayer", ...)`. 진단 로그 포함.
- **`ConfirmDialog`**: 재사용 가능 예/아니오 확인창(`Action` 콜백 인자).
- **`RoomExitController`**: 뒤로가기(확인창→`LeaveRoom()`→`LobbyScene`) 전담. 마지막 1인 퇴장 시
  `Room.CustomProperties.Clear()`. `pv.RPC("LogMsg", ...)`를 호출하는데, 이 `pv`는
  `[SerializeField]`로 씬에서 수동 연결해야 하는 값이고 주석상 "`GameManager`와 같은 오브젝트의
  `PhotonView`"라는 암묵 전제에 의존한다(§4.10, 신규).
- **`VoidKillZone`**: 트리거 콜라이더로 맵 밖 낙사 시 `RespawnToSpawnPoint()` 호출.

### 2.4 `Unit/` — 캐릭터 이동·애니메이션 (6파일, 책임별 분리)
Rigidbody 기반 물리 이동 + 5개 협력 클래스. `HideOrSeekPlayer`(MonoBehaviour)가 조정자이고
나머지 4개는 순수 C# 클래스(Unity 생명주기 없음, 유닛 테스트 가능):

- **`PlayerMoveState`**: `Idle/Walk/Run/Jump/Dodge` 5개 상태 enum. `Animator.SetTrigger(newState.ToString())`이
  enum 이름과 직접 매칭되므로 Animator Controller 파라미터 이름과 반드시 일치해야 하는 암묵적
  계약 — 현재는 정확히 일치함(직접 재확인).
- **`PlayerGroundDetector`**: 순수 접지 판정 클래스. `Physics.Raycast`로 매 `FixedUpdate` 폴링 —
  이벤트 기반이 아니라 사각지대(낭떠러지에서 낙하 미시작)가 구조적으로 없음.
- **`PlayerAnimationDriver`**: `ChangeState()`는 상태 라벨이 같으면 무시하는 가드가 있고, 점프만은
  `ReplayJump()`가 `Animator.Play("Jump",0,0f)`로 트랜지션 그래프를 우회해 연타 재점프 시 중간
  포즈 블렌딩 버그를 원천 차단. `HandleJumpAnimationHold()`가 착지 전 정점 부근에서 `speed=0`.
- **`PlayerNetworkSync`**: `IPunObservable` Write/Read + `Interpolate()`(거리 기반 스냅/보간 이원화).
- **`PlayerBillBoard`**: 머리 위 닉네임, `this.transform`을 직접 회전(간접 참조 오류 구조적 불가능).
- **`HideOrSeekPlayer`**(MonoBehaviourPunCallbacks, IPunObservable): 위 4개를 소유하는 조정자.
  - `Awake()`에서 `networkSync`를 `IsMine` 여부와 무관하게 최우선 생성 — Photon 디스패치가
    `Start()`보다 먼저 올 수 있는 경쟁을 원천 차단.
  - `Start()`: 로컬만 `useGravity`+`ContinuousDynamic`+`Interpolate`+`FreezeRotation`, 원격은
    `isKinematic=true`. 몸통 메시(`Mesh_0`)와 루트 `CapsuleCollider`의 자기 충돌을
    `Physics.IgnoreCollision()`으로 무시.
  - `Update()`: 로컬은 입력만 기록, 원격은 `networkSync.Interpolate()`+`animationDriver.ChangeState`.
  - `FixedUpdate()`: 점프 여부와 무관하게 매 스텝 접지 재확인. 의도한 점프와 걸어서 벗어난 낙하가
    동일하게 처리됨. `Move()`는 `rb.MoveRotation()`으로 회전, 수평 속도만 매 스텝 덮어쓰고 수직
    속도는 물리 엔진 값 보존. Shift는 +30% 가속.
  - `RespawnToSpawnPoint()`: `rb.position`과 `transform.position` 둘 다 갱신.

### 2.5 `Camera/` — 3인칭 카메라 (1파일)
**`Camera_Ctrl`**: `InitCamera(player)`가 호출 순서와 무관하게 `ResetToDefaultView()`로 항상 정확히
초기화. 우클릭 드래그로만 회전(수직 -7°~80°), `Quaternion.Slerp` 추적. 휠 줌은 완전히 제거되어
`PlayerPaintCanvas.HandleBrushSizeInput()`이 붓 크기 조절 전용으로 사용(입력 경합을 코드로 명시 분리).

### 2.6 `ColorTag/` — 색상 투표·페인팅 미니게임 (15파일, 4계층)
Photon **PUN2**만 사용. 4라운드 동안 팀 전체가 색을 투표→다수결 확정→각자 캔버스에 페인팅,
4라운드 후 무작위 술래 1명에게 살짝 다른 색 조합을 부여.

**데이터/설정 계층(SO, 2)**: `BrushSettingsSO`(붓 반경·휠 감도·커서 프리팹·표면 오프셋),
`ColorPaletteSO`(고정 10색, `GetColor()`/`GetColorName()` 인덱스 범위 검사 없음 — 모든 호출부가
유효 인덱스만 넘긴다는 암묵 전제에 의존, §4.5).

**순수 로직 계층(정적 클래스, Unity API 미사용, 4)**: `NetKeys`, `NetEventCodes`,
`ColorVoteTally.Resolve()`(다수결, 동점 랜덤, 무투표 시 남은 색 중 랜덤), `TaggerColorAssigner`
(변형 세트 생성/비교 — `BuildVariantSet()`이 `baseSet`에 없는 팔레트 색 후보 리스트를 만드는데,
`paletteSize <= baseSet.Length`가 되면 후보가 0개가 되어 `IndexOutOfRangeException`이 발생한다.
현재 팔레트가 10색·`baseSet`이 4개라 실제로는 안전하지만 방어 코드가 없다는 점은
`ColorPaletteSO`와 동일한 성격의 잠재 위험이다, §4.9), `RoomState`(Room CustomProperties 조회
헬퍼로 4개 파일의 중복 통합).

**라운드 진행 계층(마스터 권위, 2)**: `ColorSelectionManager`(`Update()`에서 마스터만 만료 폴링→
다수결 확정→4라운드 완료 시 술래 지정), `RoomLifecycleWatcher`(술래 퇴장/인원 부족 시 즉시 종료,
정상 종료는 `GameEndTime` 폴링→방 유지한 채 `GameLobbyScene` 복귀).

**클라이언트 표현 계층(플레이어별, 6)**: `PlayerPaintCanvas`(캐릭터당 512×512 `RenderTexture`
런타임 생성 + 좌클릭 UV 스탬프 + `RaiseEvent` 전파 + 3프레임 주기 `SkinnedMeshRenderer.BakeMesh()`로
`MeshCollider`를 실시간 포즈에 맞춰 재계산), `BrushCursorController`(3D 커서 1회 인스턴스화 후
재사용), `ColorSelectionPanel`/`ColorSwatchButton`(라운드/시간 UI, 확정색 잠금),
`PlayerColorVoteIndicator`(머리 위 스프라이트 빌보드), `PlayerColorDisplay`(술래 본인만 4라운드 후
변형 슬롯 색 치환).

전체 흐름: `투표(Player 프로퍼티) → 마스터 다수결 확정(Room 프로퍼티) → 각 클라이언트가 자기
캔버스를 확정색으로 재도색 → 4라운드 후 술래 지정 → 술래 캔버스만 전역 색 치환`.

### 2.7 `Dev/` — 오프라인 개발 부트스트랩 (1파일)
**`OfflineModeBootstrap`**: `Awake()`에서 `OfflineMode=true`. 체크박스가 켜져 있으면 방 생성 +
`ColorSelectionManager.StartColorSelection()` 자동 호출 — **`StartColorSelection()`을 호출하는
코드는 프로젝트 전체에서 이 파일 단 한 곳뿐**이며 `PlayerTestScene`에만 존재(§4.1과 직결, 재확인).

---

## 3. 씬 배선 실측 (4개 씬 GameObject 이름 grep 전수 대조)

| 씬 | 주요 GameObject | ColorTag 배선 |
|---|---|---|
| `LobbyScene` | `LobbyUICanvas`, `Main`(카메라), `Directional`, `EventSystem` | 없음(해당 없음) |
| `GameLobbyScene` | `GameLobbyUICanvas`, `GameManager`, `PlayerSpawnPos`, `VoidKillZone`, 로비 환경 오브젝트(`Table_*`/`Chair*`/`Fence_*`/`Crate_*`/`Parasol_*` 등) | **없음** |
| `GameScene` | `GameManager`, `PlayerSpawnPos`, `VoidKillZone`, `Ground`, `Directional Light`, `Main Camera`, 채팅 UI(`Canvas`/`PanelLogMsg`/`InputFieldChat`/`EventSystem`) | **없음 — `ColorSelectionManager`/`ColorTagManagers`/`StartColorSelection` 문자열이 씬 파일에 전혀 없음(이번 조사에서 grep으로 재확인)** |
| `PlayerTestScene` | `ColorTagManagers`, `GameUICanvas`, `TestBootstrap`(`OfflineModeBootstrap`, `autoStartColorSelection: 1`), `PlayerSpawnPos`, `VoidKillZone` | **유일하게 존재** |

`HideOrSeekPlayer.prefab`(`Assets/04. Prefabs/Resources/`)은 7개의 커스텀 스크립트 컴포넌트를
guid로 대조 확인했다(`HideOrSeekPlayer`/`PlayerBillBoard` — Unit 도메인, `PlayerPaintCanvas`/
`PlayerColorVoteIndicator`/`PlayerColorDisplay` — ColorTag 도메인, 그 외 Photon 표준 컴포넌트) —
에셋은 이미 완성돼 있고, `GameScene`에서 이를 "실행 시작"시키는 씬 배선(§4.1)만 빠진 상태다.

`Assets/Resources/UI/{Popup,Scene}/{클래스명}/` 아래 `ConfirmDialog`, `ColorSelectionPanel`,
`GameLobbyPanel`, `LobbyPanel`, `PlayerListItem`, `RoomListItem` 프리팹이 존재한다. 다만
`ColorSelectionPanel.prefab`이 `Resources/`에 있음에도 이를 `Resources.Load()`나 `Instantiate()`로
불러오는 코드는 프로젝트 전체에 없다 — `PlayerTestScene`에만 씬에 직접 배치된 상태이고,
`GameScene`에서 동적으로 로드하는 경로는 없다(§4.1과 동일 원인).

---

## 4. 발견된 문제 (우선순위순, 프로젝트 전체 기준)

### 4.1 [최우선, 지속] `GameScene`에 ColorTag 시작 트리거가 없음
`GameLobbyController.OnStartGameButtonClicked()`는 씬 전환만 하고, `StartColorSelection()`을
호출하는 코드는 `Dev/OfflineModeBootstrap.cs`(개발용, `PlayerTestScene` 전용) 단 한 곳뿐임을
재확인했다(§2.7, §3). 코드/에셋은 완성돼 있고 "`GameScene`에 `ColorTagManagers`+
`ColorSelectionPanel` 배치 + 호출 한 줄"만 빠진 상태 — 이전 조사 이후 변화 없음.

### 4.2 [지속] `ColorSelectionManager.ResetAllVotes()`가 마스터 자신의 투표만 리셋
```csharp
private void ResetAllVotes()
{
    if (PhotonNetwork.LocalPlayer != null)
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.VoteColorIndex, -1 } });
}
```
`PhotonNetwork.PlayerList`를 순회하지 않아 다른 플레이어의 이전 라운드 투표값이 새 라운드로
이월된다(재확인, 변경 없음). `ResolveRound()`는 마스터 클라이언트에서만 실행되므로
`PhotonNetwork.LocalPlayer`는 항상 "마스터 자신"이고, 나머지 플레이어의 `VoteColorIndex`는 다음
라운드가 시작돼도 이전 값 그대로 남는다 — 다음 라운드 `ResolveRound()`가 그 값을 그대로 투표로
재집계한다.

### 4.3 [지속] `NetKeys.GameEndTime`을 세팅(write)하는 코드가 없음
`RoomLifecycleWatcher.Update()`는 이 값의 경과를 감지해 정상 종료를 트리거하지만, 이 값을 쓰는
코드는 프로젝트 전체에 없음을 재확인했다(`NetKeys.cs` 선언 + `RoomLifecycleWatcher` 읽기/삭제
2곳뿐). 본게임(추격/승패) 미구현이 원인.

### 4.4 [지속] `GameManager.Inst`가 죽은 코드
프로젝트 유일의 static 싱글턴이지만 이를 읽는 코드가 프로젝트 전체에서 0건(재확인).

### 4.5 [지속, 경미] `ColorPaletteSO.GetColor()`/`GetColorName()`에 범위 검사 없음
인덱스가 0~9를 벗어나면 `IndexOutOfRangeException`. 모든 호출부가 유효 인덱스만 넘긴다는 암묵
전제에 의존 — 지금까지 실제 문제는 없었으나 방어 코드는 없음.

### 4.6 [지속] `Cookie_BaseSkin_B`/`_C` 머티리얼과 `07. Expression/` 표정 텍스처가 에셋만 있고 미배선
`HideOrSeekPlayer.prefab`의 `SkinnedMeshRenderer.m_Materials`에는 `Cookie_BaseSkin_A` 1개만
연결돼 있다. B/C 머티리얼과 표정 텍스처 4세트를 참조하는 스크립트·프리팹이 프로젝트 전체에
없다 — 진행 중인 비주얼 작업의 중간 산출물.

### 4.7 [지속, 경미] 버튼 계열 `AddListener`에 대응하는 해제 코드가 도메인 전체에 없음
`ConfirmDialog`/`ColorSwatchButton`/`RoomExitController`/`RoomListItem` 4곳 모두 `Awake()`에서
`onClick.AddListener(...)`만 있고 `OnDestroy`/`RemoveListener`가 없다(재확인, `grep`으로 4개
파일 전수 대조). `Button.onClick`은 GameObject 수명과 함께 소멸하므로 현재는 안전하지만,
프리팹 풀링/재부모화 시나리오가 생기면 문제가 될 수 있는 프로젝트 전반의 공통 패턴.

### 4.8 [해결됨, 유지] 캐릭터 애니메이션 리소스 교체 작업
`Cookie` 애니메이션 세트 전환이 `16c662b`로 완료된 상태가 이번 조사에서도 그대로 유지됨을
확인했다 — `PlayerAnimator.controller`의 상태/파라미터 이름이 `PlayerMoveState` enum과 정확히
일치하고, 프리팹이 이를 참조한다.

### 4.9 [신규] `TaggerColorAssigner.BuildVariantSet()`에 방어 코드 없이 팔레트 크기 의존
```csharp
var available = Enumerable.Range(0, paletteSize).Where(i => !baseSet.Contains(i)).ToList();
variant[slot] = available[rng.Next(available.Count)]; // available이 비면 예외
```
`paletteSize`(현재 10)가 `baseSet.Length`(4, 확정된 4라운드 색) 이하로 줄어들면 `available`이
빈 리스트가 되어 `IndexOutOfRangeException`이 발생한다. 현재 팔레트 구성으로는 절대 발생하지
않지만, `ColorPaletteSO.GetColor()`(§4.5)와 같은 계열의 "호출부의 암묵 전제에 의존하는 인덱스
접근" 패턴이 `ColorTag/` 순수 로직 계층에 최소 2곳 존재한다는 것을 이번 조사에서 새로 확인했다.

### 4.10 [신규, 경미] `RoomExitController`/`GameManager`의 `PhotonView` 배선이 씬 구성에 암묵 의존
`RoomExitController.OnClickBackBtn()`은 `[SerializeField] PhotonView pv`로 `LogMsg` RPC를 보내는데,
클래스 주석에 "GameManager와 같은 오브젝트의 PhotonView를 연결"이라고 명시돼 있다. 즉 두 컴포넌트가
코드로는 서로를 참조하지 않지만, **씬 인스펙터에서 같은 `PhotonView`를 수동으로 연결해야만
`LogMsg` RPC 수신자(`GameManager.LogMsg`)와 발신자(`RoomExitController`)가 같은 `ViewID`로
맞아떨어지는 암묵 계약**이 존재한다. 코드 리뷰만으로는 드러나지 않고 씬 파일을 직접 열어야 확인
가능한 종류의 결합이라, 새 씬(`GameScene`류)을 셋업할 때 다른 `PhotonView`를 잘못 연결하면 RPC가
조용히 다른 대상에게 전달되거나 실패할 수 있다. 두 씬(`GameLobbyScene`/`GameScene`) 모두에서
현재는 올바르게 연결돼 있음을 GameObject 계층 구조로 확인했으나, 코드 차원의 안전장치(예: RPC
자체를 `GameManager`에만 두고 `RoomExitController`가 `GameManager` 인스턴스를 통해 호출)는 없다.

---

## 5. 아키텍처 체크리스트 (사용자 지정 11개 관점, 프로젝트 전체 기준)

### 5.1 기존 책임 분리를 무시하는 코드가 있는가 — **경미한 위반 1건, 그 외 전반적으로 준수**
`PlayerColorDisplay.ApplyColorReplace()`가 `PlayerPaintCanvas.PaintCanvas`(공개 `RenderTexture`
프로퍼티)를 직접 `Graphics.Blit`한다 — `[RequireComponent]`로 결합 의도는 명확하지만 캡슐화
경계는 없음(`PlayerPaintCanvas`가 "이 텍스처를 어떻게 수정해도 되는지"를 스스로 통제하지 못하고
호출자의 선의에 의존). 그 외: `GameManager`(채팅)/`PlayerSpawner`(스폰)/`RoomExitController`
(나가기)가 원래 하나였던 `GameManager`를 책임별로 쪼갠 흔적이 뚜렷하고, `Unit/`은 이동·접지·
애니메이션·네트워크·표시로 5분할, `ColorTag/`는 SO/정적로직/라운드진행/클라이언트표현 4계층으로
일관 분리됨.

### 5.2 Manager 간 의존성이 과도하게 증가하는가 — **아니다, 오히려 결합 부재가 문제**
전체 33개 스크립트 중 다른 Manager를 `[SerializeField]`로 직접 참조하는 경우는
`ColorSwatchButton.manager`(UI→매니저), `RoomListItem.lobby`(UI→매니저),
`OfflineModeBootstrap→ColorSelectionManager`(개발용, `FindFirstObjectByType`) 뿐이다. 나머지는
전부 Photon CustomProperties/RaiseEvent라는 공유 네트워크 상태로만 소통한다. 진짜 문제는 결합
과다가 아니라 **`GameLobbyController`가 `ColorSelectionManager`를 전혀 모른다는 결합 부재**(§4.1)다.

### 5.3 Prefab과 Script의 역할이 뒤섞이는가 — **경미, `HideOrSeekPlayer.prefab`의 도메인 융합 + 신규 애셋 미배선**
코드 레벨에서 `Unit`과 `ColorTag`는 서로를 참조하지 않는 별개 도메인이지만, 프리팹 하나가 양쪽
컴포넌트를 물리적으로 함께 갖고 있다(§3). 캐릭터라는 단일 오브젝트가 여러 기능을 갖는 것 자체는
자연스럽지만, 코드 분리와 에셋 결합이 다른 그림이라는 점은 향후 관전자용/AI용 변형 프리팹이
필요해지면 걸림돌 가능. `Cookie_BaseSkin_B/C` 머티리얼(§4.6)과 이번 커밋에서 추가된 미사용
애니메이션 클립(§1) 모두 "에셋은 있는데 프리팹/코드 어디서도 선택·참조되지 않는" 동일한 패턴이다.
`transform.Find("경로")` 류의 취약한 문자열 탐색은 `HideOrSeekPlayer.Start()`의
`transform.Find("Mesh_0")` 1건과 `PlayerSpawner`/`HideOrSeekPlayer.RespawnToSpawnPoint()`의
`GameObject.Find("PlayerSpawnPos")` 2건으로, 모델/씬 구조가 바뀌면 함께 갱신해야 하는 암묵 계약이
여전히 존재한다.

### 5.4 Scene에 직접 의존하는 코드가 늘어나는가 — **깨끗함**
프로젝트 전체에서 씬 이름을 하드코딩 문자열로 다루는 코드는 0건 — `SceneManager.LoadScene`/
`PhotonNetwork.LoadLevel` 호출 전부 `SceneNames.*` 상수 사용(재확인). 다만 §4.10에서 새로 짚은
"씬 인스펙터 배선에 대한 암묵 계약"은 코드가 아닌 씬 구성 차원의 의존성으로, 이 항목이 다루는
"씬 이름 하드코딩"과는 다른 성격의 취약점이다.

### 5.5 Singleton이 남발되는가 — **아니다, 1개뿐이고 그마저 죽은 코드**
프로젝트 전체 33개 스크립트 중 `static Instance`류 패턴은 `GameManager.Inst` 1개뿐이며, 이를
읽는 코드가 0건(§4.4, 재확인). 나머지는 씬 배치 컴포넌트이거나 `FindObjectsByType`/
`FindFirstObjectByType`으로 그때그때 탐색(`OfflineModeBootstrap`, `GameManager.SetLocalPlayerMovementLocked`,
`BrushCursorController.FindLocalPaintCanvas` 3곳).

### 5.6 ScriptableObject의 책임이 잘못 사용되는가 — **문제 없음**
`ColorPaletteSO`/`BrushSettingsSO` 둘 다 읽기 전용 데이터 저장소로만 쓰이고, 런타임에 필드를
변경하는 코드는 없다. SO를 상태 저장소나 이벤트 버스로 쓰는 안티패턴 없음. 다만 두 SO의 인덱스
접근 메서드(`GetColor`/`GetColorName`)에 방어 코드가 없다는 점(§4.5)과, 이와 같은 패턴이
`TaggerColorAssigner`(순수 로직 계층, SO는 아니지만 같은 도메인)에도 반복된다는 점(§4.9)은 "SO
오용"은 아니지만 이 도메인 전반의 방어적 프로그래밍 부재로 기록해둔다.

### 5.7 Unity Lifecycle 순서 문제가 있는가 — **여러 건, 모두 방어적으로 처리되어 안전**
- `HideOrSeekPlayer.Awake()`가 `networkSync`를 `IsMine` 여부와 무관하게 최우선 생성 — Photon
  디스패치가 `Start()`보다 먼저 올 수 있는 경쟁을 원천 차단.
- `Camera_Ctrl.InitCamera()`/`Start()` 둘 다 같은 `ResetToDefaultView()`를 호출해 호출 순서 무관.
- `PlayerColorDisplay.Awake()`와 `PlayerPaintCanvas.Start()` 실행 순서는 보장되지 않지만
  `TryApplyTaggerColor()`가 캔버스 없으면 조용히 리턴 후 재시도(`OnRoomPropertiesUpdate`에서).
- `PlayerPaintCanvas.Start()`에서 `paintableMeshCollider`/`skinnedBodyRenderer`/
  `bakedColliderMesh`를 초기화하는데, `Update()`의 `RefreshColliderMesh()`가 이보다 먼저 호출될
  수는 없음(같은 컴포넌트의 `Start()`→`Update()` 순서는 Unity가 보장).
- 프로젝트에 커스텀 Script Execution Order 설정 자체가 없음(`ProjectSettings/`에 관련 asset
  부재 확인) — 즉 이런 방어 코드가 실제로 필요한 상황이며, 현재는 모두 대응돼 있다.

### 5.8 Event 구독/해제가 제대로 되는가 — **Photon 콜백은 완전히 깨끗함, UI 버튼 리스너는 프로젝트 전체 공통 패턴으로 미해제**
Photon 콜백은 `MonoBehaviourPunCallbacks`가 `OnEnable`/`OnDisable`에서 자동 처리, 수동 이중 등록
0건. C# `+=`/`-=` 순수 이벤트 구독은 프로젝트에 없음. UI `Button.onClick.AddListener()`는 4개
도메인에 걸쳐 공통 패턴이며 대응 `RemoveListener` 없음(§4.7) — ColorTag만의 문제가 아니라 프로젝트
전반의 컨벤션. `PlayerPaintCanvas`는 유일하게 `OnDestroy()`를 구현하지만 이는 `RenderTexture`/
`Mesh` 네이티브 리소스 해제 목적이지 이벤트 구독 해제와는 무관하다.

### 5.9 Object Pool과 Instantiate/Destroy가 충돌하는가 — **풀 자체가 없음, 충돌 없음**
프로젝트 실제 코드에 Object Pool 구현이 전혀 없다(`Player.md`에 등장하는 `ItemObjectPool` 등은
§7에서 설명하듯 다른 프로젝트의 참고 코드). `Instantiate`/`Destroy` 호출은 `LobbyController`(방
목록, diff 갱신), `GameLobbyController`(플레이어 목록, 매번 전체 재생성), `BrushCursorController`
(붓 커서 1회 인스턴스화 후 `SetActive` 재사용 — 사실상 수동 풀링 1개체), `PlayerSpawner`(캐릭터)
정도로 전부 생명주기가 명확하다. `PlayerPaintCanvas`의 `RenderTexture`+`bakedColliderMesh` 둘 다
캐릭터당 1회 생성·`OnDestroy()`에서 해제 확인.

### 5.10 Photon의 Ownership/RPC 구조를 무시하는가 — **잘 지켜짐, 프로젝트에서 가장 성숙한 영역**
- `pv.IsMine` 게이팅이 `HideOrSeekPlayer.Update()`/`FixedUpdate()`, `PlayerPaintCanvas.Update()`
  등에 정확히 적용됨. `RefreshColliderMesh()` 호출도 `pv.IsMine` 체크 이후 블록 안에서만 실행됨
  (원격 캐릭터는 콜라이더를 갱신하지 않음 — 로컬 붓칠 판정에만 필요하므로 올바른 설계).
- 마스터 전용 권위: `ColorSelectionManager`/`RoomLifecycleWatcher` 둘 다 `IsMasterClient` 가드.
- 상태(투표/라운드/인원) vs 순간 이벤트(붓질/채팅)를 CustomProperties/PlayerList vs
  `RaiseEvent`/`RPC`로 적절히 구분.
- 수신부(`OnEvent`/`OnPhotonSerializeView`)는 송신측이 이미 계산한 값을 그대로 재생만 함.
- `pv` 필드는 필요한 클래스에만 존재, Room 단위 상태만 다루는 매니저에는 없음.
- 유일한 흠은 §4.10에서 새로 짚은 것처럼, `RoomExitController`의 `pv`가 "다른 컴포넌트와 같은
  `PhotonView`여야 한다"는 계약이 코드가 아니라 씬 배선에만 존재한다는 점 — 실행 결과는 현재
  올바르지만 강제할 장치가 없다.

### 5.11 중복 로직이 있는가 — **크게 개선된 상태, 경미한 잔여 중복만 존재**
`RoomState` 정적 헬퍼로 4개 파일의 CustomProperties 조회가 통합됨. `GameManager.Start()`와
`PlayerSpawner.Start()`가 "InRoom 대기" 코루틴 패턴을 각자 구현(동일 로직 2곳 복붙, 3줄 내외라
심각하지 않음). `PlayerColorVoteIndicator.LateUpdate()`와 `PlayerBillBoard.LateUpdate()`가 거의
동일한 "카메라 forward 정렬" 코드를 각자 구현(둘 다 3줄). `ColorPaletteSO.GetColor()`의 방어
부재(§4.5)와 `TaggerColorAssigner.BuildVariantSet()`의 방어 부재(§4.9)는 로직 자체의 중복은
아니지만, "인덱스 유효성을 호출부가 보장한다는 암묵 전제"라는 동일한 설계 패턴이 반복된다는
점에서 넓은 의미의 중복으로 볼 수 있다.

---

## 6. 종합 결론과 다음 단계 제안 (우선순위순)

1. **[최우선, 통합 공백, 지속]** `GameLobbyController.OnStartGameButtonClicked()` 또는 `GameScene`
   진입 시점에 `ColorSelectionManager.StartColorSelection()` 호출을 추가하고, `PlayerTestScene`의
   `ColorTagManagers`+`ColorSelectionPanel` UI를 `GameScene.unity`에 옮겨 배치한다(§4.1). 코드/에셋은
   이미 완성돼 있어 "씬 배치 + 호출 한 줄" 수준의 작업 — 이번 조사에서도 여전히 유일한 구조적 공백.
2. **`ColorSelectionManager.ResetAllVotes()`가 전원을 리셋해야 하는 의도인지 확인 후**
   `PhotonNetwork.PlayerList`를 순회하도록 수정(§4.2).
3. **본게임(태그/술래잡기) 승패 판정 로직 설계 + `GameEndTime` 기록 주체 결정**(§4.3) — 신규
   `Plan.md/GameRule.md`(1033줄, 이번 커밋에서 추가됨)가 이 설계의 출발점이 될 수 있으니 다음
   세션에서 게임 룰 문서와 미구현 항목을 대조하는 것을 권장한다.
4. **`Cookie_BaseSkin_B`/`_C`, `07. Expression/` 표정 텍스처, 신규 애니메이션 클립 3종(§1)을 실제로
   활용할 스크립트를 설계하거나, 당장 계획이 없다면 미사용 에셋임을 명시해둔다**(§4.6).
5. (선택) `ColorPaletteSO.GetColor()`/`TaggerColorAssigner.BuildVariantSet()`에 인덱스·후보 개수
   방어 코드 추가 검토(§4.5, §4.9 — 둘 다 현재는 안전하지만 팔레트 크기를 줄이는 변경이 생기면
   즉시 예외로 이어짐).
6. (선택) `GameManager.Inst`가 죽은 코드라면 제거를 검토(§4.4).
7. (선택) UI 버튼 4곳의 `AddListener`에 대응하는 해제 코드 추가(§4.7).
8. (선택) `RoomExitController`의 `PhotonView` 씬 배선 암묵 계약을 코드 차원으로 옮기는 것을 검토
   — 예를 들어 `RoomExitController`가 자체 `pv`를 갖지 않고 `GameManager.Inst.BroadcastLogMsg(msg)`
   같은 명시적 API를 호출하도록 바꾸면, §4.4의 죽은 싱글턴을 유용하게 되살리는 부수 효과도 있다(§4.10).

**전체적으로**: 이 프로젝트는 도메인별 책임 분리(§5.1), Scene 의존성 관리(§5.4), Singleton 절제
(§5.5), Photon Ownership/RPC 구조(§5.10) 네 관점에서 뚜렷한 구조적 문제가 없다. 이번 조사로 새로
확인한 것은 두 가지다: 첫째, `1280dac` 커밋은 코드에 아무 영향을 주지 않았으므로 아키텍처 상태
자체는 전날 조사와 완전히 동일하다는 점(§1). 둘째, `ColorTag/` 순수 로직 계층에 "호출부가 인덱스
유효성을 암묵적으로 보장한다"는 동일 패턴의 방어 코드 부재가 `ColorPaletteSO` 외에
`TaggerColorAssigner`에도 있다는 것(§4.9), 그리고 `RoomExitController`의 RPC 배선이 씬 구성에만
존재하는 암묵 계약이라는 것(§4.10)이다. 두 신규 항목 모두 지금 당장 버그를 일으키지는 않지만,
남은 구조적 문제는 대부분 도메인 자체의 결함이 아니라 **완성된 도메인/에셋들을 게임 전체 플로우로
잇는 배선의 공백**(§4.1, §4.6)과 **코드로 강제되지 않는 암묵 계약**(§4.9, §4.10) 두 축으로
계속 좁혀지고 있다.

---

## 7. 부록 — `Assets/02. Scripts/Player.md`의 정체

이 파일은 `.cs`가 아니라 `.md`이며, **이 프로젝트(`TagOfChaos`/`HideOrSeekPlayer`)의 스크립트가
아니라 다른(이전) 3인칭 슈팅 게임 프로젝트의 `Player.cs` 전문 + 그 코드에 대한 분석 메모**를 담고
있다. 무기 스왑/장전/투척, 상점, 적 스폰, 코인/탄약/체력 아이템, 대미지 처리 등 이 게임과 무관한
기능이 대부분이며, `EnemyObjectPool`/`ItemObjectPool` 등 §5.9에서 언급한 오브젝트 풀도 전부 이
참고 코드에서만 등장한다. 문서 하단의 분석 메모는 "들러붙는 버그" 조사를 위해 이 참고 코드의
이동/접지 판정 방식을 검토한 기록으로, 결론은 "이 코드를 그대로 가져다 쓰지 말라"다 — 접지 판정이
`OnCollisionEnter` 1회성 이벤트 기반이라 낭떠러지 낙하를 못 잡는 사각지대가 있고(우리
`PlayerGroundDetector`가 이미 해결한 문제, §2.4), `Walking()`의 `Time.deltaTime` 이중 적용은
그대로 옮기면 새 버그가 된다는 점을 지적한다. 이 프로젝트의 실제 `Unit/` 도메인 코드는 이 참고
코드와 무관하게 독립적으로 설계돼 있다.
