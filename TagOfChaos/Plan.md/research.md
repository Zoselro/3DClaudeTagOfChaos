# 조사 보고서: 프로젝트 전체 심층 분석 + 아키텍처 체크리스트 (2026-08-17, 커밋 16c662b 기준)

> 이 문서는 같은 날 더 이른 시각(10:49)에 작성된 이전 버전을 대체한다. 이전 버전은 커밋
> `f33af46`/`e29e67a`(2026-08-16)까지만 반영했으나, 그 직후 `16c662b`("상체 색칠 안됨 및 붓
> 커서가 몸안에 파고드는 현상 수정 과 플레이어 메테리얼 A,B,C 구현", 2026-08-17 15:34)가 추가로
> 커밋되어 이전 문서가 이미 낡은 상태였다. 이번 조사는 `Assets/02. Scripts/` 아래 **7개 도메인
> 33개 `.cs` 파일 전체**를 예외 없이 라인 단위로 다시 읽고, `git diff 4268d13..16c662b`로 실제
> 코드 변경분을 대조하고, 4개 씬 파일과 `HideOrSeekPlayer.prefab`의 GameObject/컴포넌트/머티리얼
> 참조를 guid 단위로 직접 확인해 사용자가 지정한 11개 아키텍처 관점을 검증한 결과다. 현재 git
> 워킹 트리에는 폰트 에셋(`NotoSansKR SDF.asset`) 1건만 미커밋 상태이므로, 코드 기준으로는
> `16c662b`가 곧 현재 상태다.

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

## 1. `16c662b` 커밋에서 실제로 바뀐 것 (전수 diff 확인)

이전 조사 이후 코드가 "변경 없음"이 아니라 **7개 `.cs` 파일 + 프리팹 + 머티리얼 2개**가 바뀌었다.
이번 보고서의 존재 이유이므로 먼저 명확히 정리한다.

| 파일 | 변경 내용 |
|---|---|
| `Camera_Ctrl.cs` | 카메라 시선 높이 오프셋 `1.4f→1.5f` (Cookie 캐릭터가 옛 모델보다 약 7% 커짐에 맞춘 비례 조정, 2곳) |
| `BrushCursorController.cs` | 붓 커서를 표면 법선 방향으로 `CursorSurfaceOffset`만큼 띄움 (몸속으로 파고들어 보이는 현상 완화) |
| `BrushSettingsSO.cs` | `cursorSurfaceOffset` 필드/프로퍼티 추가 |
| `PlayerPaintCanvas.cs` | **핵심 변경**: `MeshCollider`가 스킨 애니메이션 포즈를 따라가지 않고 바인드 포즈에 고정돼 있던 문제(상체가 안 칠해지고 커서가 파고드는 근본 원인)를 고치기 위해, 로컬 플레이어가 색상 라운드 중일 때 3프레임에 1번 `SkinnedMeshRenderer.BakeMesh()`로 현재 포즈를 구워 `MeshCollider.sharedMesh`에 반영(`RefreshColliderMesh()`, 67줄 추가) |
| `HideOrSeekPlayer.cs` | ① `transform.Find("Ch36")` → `transform.Find("Mesh_0")`로 대상 오브젝트 이름 변경(모델 교체 반영) ② **`keepMovingAfterJump`/`jumpMoveDir` 완전 제거** — 의도한 점프도 이제 공중에서 자유롭게 방향 전환 가능(과거엔 점프 방향이 고정됐음) ③ Shift 이동 배율 `0.3배(감속/사복)` → `1.3배(가속/질주)`로 반전 |
| `PlayerAnimationDriver.cs` | (주석 정리만, `Animator.Play("Jump",0,0f)` 로직 자체는 이전 커밋에서 이미 도입됨) |
| `PlayerMoveState.cs` | `SneakWalk` → **`Run`**으로 열거값 이름 변경 (Shift 이동 의미가 "몰래 걷기"에서 "질주"로 바뀐 것과 정합) |
| `HideOrSeekPlayer.prefab` | 2078줄 변경 — 새 `Cookie` 모델/머티리얼/콜라이더 구조 반영 |
| `05. Materials/Character/Cookie_BaseSkin_{A,B,C}.mat` + 색상 텍스처 3장 | 신규 캐릭터 베이스 스킨 머티리얼 3종 추가 |
| `07. Expression/` | 표정 텍스처(무표정/스마일/웃음/화남) 4세트 × 2種 신규 추가 (아직 어떤 스크립트도 참조하지 않음, §4.6) |

이 중 **애니메이션 리소스 교체 마무리**(이전 보고서 §4.7의 최우선 미완료 항목)는 이번 커밋으로
**완전히 해결**됐음을 직접 확인했다: 새 `Assets/Animation/PlayerAnimator.controller`(guid
`54653bb9...`)의 상태/파라미터 이름이 정확히 `Idle`/`Walk`/`Run`/`Jump`/`Dodge`이고, 이는 새
`PlayerMoveState` enum과 1:1 일치하며, `HideOrSeekPlayer.prefab`의 `Animator.m_Controller`가
실제로 이 새 컨트롤러를 가리킨다(guid 대조로 확인, 옛 `Assets/Animation/Old/PlayerAnimator.controller`는
더 이상 참조되지 않음).

---

## 2. 도메인별 상세 동작

### 2.1 `Core/` — 씬 이름 상수 (1파일)
`SceneNames.cs`: `Lobby`/`GameLobby`/`Game` 3개 문자열 상수. 프로젝트 전체에서 씬 이름을 직접
문자열로 다루는 코드는 이 파일 안에만 존재한다(§6.4에서 재검증).

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
  유일의 싱글턴**이지만 `GameManager.Inst`를 읽는 코드는 프로젝트 전체에서 0건(재확인) — 죽은
  코드. `Start()`가 `InRoom==true`가 될 때까지 코루틴 대기 후 "Connected" 로그를 RPC 브로드캐스트.
  Enter로 채팅 입력창 토글 + `is_Conversating`을 `HideOrSeekPlayer.IsMovementLocked`에 연결.
- **`PlayerSpawner`**: 스폰 전담. `InRoom==true` 대기 후 `"PlayerSpawnPos"`를 `GameObject.Find()`로
  찾아 반경 5 랜덤 오프셋으로 `PhotonNetwork.Instantiate("HideOrSeekPlayer", ...)`. 진단 로그 포함.
- **`ConfirmDialog`**: 재사용 가능 예/아니오 확인창(`Action` 콜백 인자).
- **`RoomExitController`**: 뒤로가기(확인창→`LeaveRoom()`→`LobbyScene`) 전담. 마지막 1인 퇴장 시
  `Room.CustomProperties.Clear()`.
- **`VoidKillZone`**: 트리거 콜라이더로 맵 밖 낙사 시 `RespawnToSpawnPoint()` 호출.

### 2.4 `Unit/` — 캐릭터 이동·애니메이션 (6파일, 책임별 분리)
Rigidbody 기반 물리 이동 + 5개 협력 클래스. `HideOrSeekPlayer`(MonoBehaviour)가 조정자이고
나머지 4개는 순수 C# 클래스(Unity 생명주기 없음, 유닛 테스트 가능):

- **`PlayerMoveState`**: `Idle/Walk/Run/Jump/Dodge` 5개 상태 enum(이번 커밋에서 `SneakWalk→Run`
  개명, §1). `Animator.SetTrigger(newState.ToString())`이 enum 이름과 직접 매칭되므로 Animator
  Controller 파라미터 이름과 반드시 일치해야 하는 암묵적 계약 — 현재는 정확히 일치함(§1에서 확인).
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
    `isKinematic=true`. 몸통 메시(`Mesh_0`, 이번 커밋에서 `Ch36`→개명)와 루트 `CapsuleCollider`의
    자기 충돌을 `Physics.IgnoreCollision()`으로 무시.
  - `Update()`: 로컬은 입력만 기록, 원격은 `networkSync.Interpolate()`+`animationDriver.ChangeState`.
  - `FixedUpdate()`: 점프 여부와 무관하게 매 스텝 접지 재확인. **이번 커밋으로 의도한 점프와
    걸어서 벗어난 낙하가 완전히 동일하게 처리됨**(과거엔 의도한 점프만 방향이 고정됐음, §1).
    `Move()`는 `rb.MoveRotation()`으로 회전, 수평 속도만 매 스텝 덮어쓰고 수직 속도는 물리 엔진
    값 보존. Shift는 이제 **+30% 가속**(과거엔 -70% 감속, §1).
  - `RespawnToSpawnPoint()`: `rb.position`과 `transform.position` 둘 다 갱신.

### 2.5 `Camera/` — 3인칭 카메라 (1파일)
**`Camera_Ctrl`**: `InitCamera(player)`가 호출 순서와 무관하게 `ResetToDefaultView()`로 항상 정확히
초기화. 우클릭 드래그로만 회전(수직 -7°~80°), `Quaternion.Slerp` 추적. 휠 줌은 완전히 제거되어
`PlayerPaintCanvas.HandleBrushSizeInput()`이 붓 크기 조절 전용으로 사용(입력 경합을 코드로 명시 분리).

### 2.6 `ColorTag/` — 색상 투표·페인팅 미니게임 (15파일, 4계층)
Photon **PUN2**만 사용. 4라운드 동안 팀 전체가 색을 투표→다수결 확정→각자 캔버스에 페인팅,
4라운드 후 무작위 술래 1명에게 살짝 다른 색 조합을 부여.

**데이터/설정 계층(SO, 2)**: `BrushSettingsSO`(붓 반경·휠 감도·커서 프리팹·**신규
`cursorSurfaceOffset`**), `ColorPaletteSO`(고정 10색, 인덱스 범위 검사 없음).

**순수 로직 계층(정적 클래스, Unity API 미사용, 4)**: `NetKeys`, `NetEventCodes`,
`ColorVoteTally.Resolve()`(다수결, 동점 랜덤, 무투표 시 남은 색 중 랜덤), `TaggerColorAssigner`
(변형 세트 생성/비교), `RoomState`(Room CustomProperties 조회 헬퍼로 4개 파일의 중복 통합).

**라운드 진행 계층(마스터 권위, 2)**: `ColorSelectionManager`(`Update()`에서 마스터만 만료 폴링→
다수결 확정→4라운드 완료 시 술래 지정), `RoomLifecycleWatcher`(술래 퇴장/인원 부족 시 즉시 종료,
정상 종료는 `GameEndTime` 폴링→방 유지한 채 `GameLobbyScene` 복귀).

**클라이언트 표현 계층(플레이어별, 6)**: `PlayerPaintCanvas`(캐릭터당 512×512 `RenderTexture`
런타임 생성 + 좌클릭 UV 스탬프 + `RaiseEvent` 전파 + **이번 커밋에서 추가된 3프레임 주기
MeshCollider 실시간 포즈 반영**), `BrushCursorController`(3D 커서 1회 인스턴스화 후 재사용,
**이번 커밋에서 표면 오프셋 추가**), `ColorSelectionPanel`/`ColorSwatchButton`(라운드/시간 UI,
확정색 잠금), `PlayerColorVoteIndicator`(머리 위 스프라이트 빌보드), `PlayerColorDisplay`(술래
본인만 4라운드 후 변형 슬롯 색 치환).

전체 흐름: `투표(Player 프로퍼티) → 마스터 다수결 확정(Room 프로퍼티) → 각 클라이언트가 자기
캔버스를 확정색으로 재도색 → 4라운드 후 술래 지정 → 술래 캔버스만 전역 색 치환`.

### 2.7 `Dev/` — 오프라인 개발 부트스트랩 (1파일)
**`OfflineModeBootstrap`**: `Awake()`에서 `OfflineMode=true`. 체크박스가 켜져 있으면 방 생성 +
`ColorSelectionManager.StartColorSelection()` 자동 호출 — **`StartColorSelection()`을 호출하는
코드는 프로젝트 전체에서 이 파일 단 한 곳뿐**이며 `PlayerTestScene`에만 존재(§4.1과 직결, 재확인).

---

## 3. 씬 배선 실측 (4개 씬 GameObject + guid 직접 대조)

| 씬 | 주요 GameObject | ColorTag 배선 |
|---|---|---|
| `LobbyScene` | `LobbyUICanvas`, `Main`(카메라), `Directional`, `EventSystem` | 없음(해당 없음) |
| `GameLobbyScene` | `GameLobbyUICanvas`, `GameManager`, `PlayerSpawnPos`, `VoidKillZone`, 로비 환경 오브젝트 | **없음** |
| `GameScene` | `GameManager`, `PlayerSpawnPos`, `VoidKillZone`, `Ground`, `Directional Light`, `Main Camera`, 채팅 UI(`Canvas`/`PanelLogMsg`/`InputFieldChat`/`EventSystem`) | **없음 — `ColorSelectionManager`/`ColorTagManagers`/`StartColorSelection` 문자열이 씬 파일에 전혀 없음(재확인, GameObject 이름 전수 나열로 검증, §2.6)** |
| `PlayerTestScene` | `ColorTagManagers`, `GameUICanvas`, `TestBootstrap`(`OfflineModeBootstrap`), `PlayerSpawnPos`, `VoidKillZone` | **유일하게 존재** |

`HideOrSeekPlayer.prefab`(`Assets/04. Prefabs/Resources/`) 컴포넌트를 guid로 직접 대조: `HideOrSeekPlayer`
(Unit) + `PlayerBillBoard`(Unit) + `PlayerPaintCanvas`/`PlayerColorVoteIndicator`/`PlayerColorDisplay`
(ColorTag) + `PhotonView`/`PhotonTransformView` 계열 2개가 **하나의 프리팹에 함께 부착**돼 있다 —
에셋은 이미 완성돼 있고, `GameScene`에서 이를 "실행 시작"시키는 씬 배선만 빠진 상태(§4.1은 지난
조사와 동일하게 여전히 미해결).

**신규 확인 — Resources UI 프리팹 경로**: `Assets/Resources/UI/{Popup,Scene}/{클래스명}/` 아래
`ConfirmDialog`, `ColorSelectionPanel`, `GameLobbyPanel`, `LobbyPanel`, `PlayerListItem`,
`RoomListItem` 프리팹이 `CLAUDE.md`의 "UI 프리팹: `Resources/UI/{Popup|Scene|Tab}/{클래스명}`"
규칙대로 정리돼 있다. 다만 `ColorSelectionPanel.prefab`이 `Resources/`에 존재함에도 이를
`Resources.Load()`나 `Instantiate()`로 불러오는 코드는 프로젝트 전체에 없다 — 즉 이 프리팹도
`PlayerTestScene`에만 씬에 직접 배치된 상태이고, `GameScene`에서 동적으로 로드하는 경로는
없다(§4.1과 동일 원인).

**신규 확인 — 캐릭터 베이스 스킨 A/B/C 미배선**: 이번 커밋에서 추가된 `Cookie_BaseSkin_A/B/C.mat`
3종 중 `HideOrSeekPlayer.prefab`의 `SkinnedMeshRenderer.m_Materials`에는 **`Cookie_BaseSkin_A`
guid(`1268e918...`) 1개만** 참조돼 있음을 guid 전수 대조로 확인했다. `Cookie_BaseSkin_B`/`_C`
guid는 프로젝트 전체(프리팹/씬/스크립트) 어디에도 참조되지 않는다 — 커밋 메시지의 "메테리얼
A,B,C 구현"은 **에셋 3종 제작까지**를 의미하며, 플레이어별로 A/B/C 중 하나를 선택해 적용하는
로직(예: ActorNumber 기반 스킨 배정)은 아직 코드에 없다(§4.6 신규 항목).

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
이월된다(재확인, 이번 커밋에서 변경 없음).

### 4.3 [지속] `NetKeys.GameEndTime`을 세팅(write)하는 코드가 없음
`RoomLifecycleWatcher.Update()`는 이 값의 경과를 감지해 정상 종료를 트리거하지만, 이 값을 쓰는
코드는 프로젝트 전체에 없음을 재확인했다(§3의 grep 결과: `NetKeys.cs` 선언 + `RoomLifecycleWatcher`
읽기/삭제 2곳뿐). 본게임(추격/승패) 미구현이 원인.

### 4.4 [지속] `GameManager.Inst`가 죽은 코드
프로젝트 유일의 static 싱글턴이지만 이를 읽는 코드가 프로젝트 전체에서 0건(재확인).

### 4.5 [지속, 경미] `ColorPaletteSO.GetColor()`/`GetColorName()`에 범위 검사 없음
인덱스가 0~9를 벗어나면 `IndexOutOfRangeException`. 모든 호출부가 유효 인덱스만 넘긴다는 암묵
전제에 의존 — 지금까지 실제 문제는 없었으나 방어 코드는 없음.

### 4.6 [신규] `Cookie_BaseSkin_B`/`_C` 머티리얼과 `07. Expression/` 표정 텍스처가 에셋만 있고 미배선
§3에서 확인했듯, 이번 커밋의 "메테리얼 A,B,C 구현"은 A만 실제로 프리팹에 연결됐고 B/C는 데이터로만
존재한다. 같은 커밋에서 함께 추가된 `Assets/07. Expression/`의 표정 텍스처 4세트(무표정/스마일/
웃음/화남 × 2인분)도 이를 참조하는 스크립트나 프리팹이 프로젝트 전체에 없다(신규 폴더 자체가
`grep -rl "BaseSkin"` / 파일명 검색 모두 0건). 진행 중인 비주얼 작업의 중간 산출물로 보이며,
당장 버그는 아니지만 "왜 만들었는데 안 쓰이나"를 다음 세션에서 헷갈리지 않도록 명시해둔다.

### 4.7 [지속, 경미] 버튼 계열 `AddListener`에 대응하는 해제 코드가 도메인 전체에 없음
`ConfirmDialog`/`ColorSwatchButton`/`RoomExitController`/`RoomListItem` 4곳 모두 `Awake()`에서
`onClick.AddListener(...)`만 있고 `OnDestroy`/`RemoveListener`가 없다(재확인, 4개 파일 모두 이번
커밋에서 변경 없음). `Button.onClick`은 GameObject 수명과 함께 소멸하므로 현재는 안전하지만,
프리팹 풀링/재부모화 시나리오가 생기면 문제가 될 수 있는 프로젝트 전반의 공통 패턴.

### 4.8 [해결됨] 캐릭터 애니메이션 리소스 교체 작업
이전 보고서의 최우선 미완료 항목이었던 `Cookie` 애니메이션 세트 전환이 `16c662b`로 완료됐음을
확인했다(§1) — 새 `PlayerAnimator.controller`의 상태/파라미터 이름이 새 `PlayerMoveState` enum
(`Idle/Walk/Run/Jump/Dodge`)과 정확히 일치하고, 프리팹이 이를 참조한다. 더 이상 추적할 필요 없음.

---

## 5. 아키텍처 체크리스트 (사용자 지정 11개 관점, 프로젝트 전체 기준)

### 5.1 기존 책임 분리를 무시하는 코드가 있는가 — **경미한 위반 1건, 그 외 전반적으로 준수**
`PlayerColorDisplay.ApplyColorReplace()`가 `PlayerPaintCanvas.PaintCanvas`(공개 `RenderTexture`
프로퍼티)를 직접 `Graphics.Blit`한다 — `[RequireComponent]`로 결합 의도는 명확하지만 캡슐화
경계는 없음. 그 외: `GameManager`(채팅)/`PlayerSpawner`(스폰)/`RoomExitController`(나가기)가
원래 하나였던 `GameManager`를 책임별로 쪼갠 흔적이 뚜렷하고, `Unit/`은 이동·접지·애니메이션·
네트워크·표시로 5분할, `ColorTag/`는 SO/정적로직/라운드진행/클라이언트표현 4계층으로 일관
분리됨. 이번 커밋의 `PlayerPaintCanvas` 변경(콜라이더 베이킹)도 같은 클래스 내부 책임(캔버스
+콜라이더 갱신은 둘 다 "로컬 플레이어의 페인트 표면 관리")에 머물러 있어 새로운 위반은 아님.

### 5.2 Manager 간 의존성이 과도하게 증가하는가 — **아니다, 오히려 결합 부재가 문제**
전체 33개 스크립트 중 다른 Manager를 `[SerializeField]`로 직접 참조하는 경우는
`ColorSwatchButton.manager`(UI→매니저), `RoomListItem.lobby`(UI→매니저),
`OfflineModeBootstrap→ColorSelectionManager`(개발용, `FindFirstObjectByType`) 뿐. 나머지는 전부
Photon CustomProperties/RaiseEvent라는 공유 네트워크 상태로만 소통. 진짜 문제는 결합 과다가 아니라
**`GameLobbyController`가 `ColorSelectionManager`를 전혀 모른다는 결합 부재**(§4.1)다.

### 5.3 Prefab과 Script의 역할이 뒤섞이는가 — **경미, `HideOrSeekPlayer.prefab`의 도메인 융합 + 신규 머티리얼 미배선**
코드 레벨에서 `Unit`과 `ColorTag`는 서로를 참조하지 않는 별개 도메인이지만, 프리팹 하나가 양쪽
컴포넌트를 물리적으로 함께 갖고 있다(§3). 캐릭터라는 단일 오브젝트가 여러 기능을 갖는 것 자체는
자연스럽지만, 코드 분리와 에셋 결합이 다른 그림이라는 점은 향후 관전자용/AI용 변형 프리팹이
필요해지면 걸림돌 가능. 이번 조사에서 새로 확인된 것: `Cookie_BaseSkin_B/C` 머티리얼(§4.6)이
"에셋은 있는데 프리팹/코드 어디서도 선택되지 않는" 상태라는 점도 넓게 보면 같은 범주 —
에셋(머티리얼 3종)이 이를 활용할 스크립트 로직보다 먼저 만들어져 있다. `transform.Find("경로")`
류의 취약한 문자열 탐색은 `HideOrSeekPlayer.Start()`의 `transform.Find("Mesh_0")` 1건(이번
커밋에서 `"Ch36"`→`"Mesh_0"`로 이름이 바뀜, 모델 교체 시 함께 갱신해야 하는 암묵 계약이 실제로
한 번 발동한 사례)과 `PlayerSpawner`/`HideOrSeekPlayer.RespawnToSpawnPoint()`의
`GameObject.Find("PlayerSpawnPos")` 2건.

### 5.4 Scene에 직접 의존하는 코드가 늘어나는가 — **깨끗함**
프로젝트 전체에서 씬 이름을 하드코딩 문자열로 다루는 코드는 0건 — `SceneManager.LoadScene`/
`PhotonNetwork.LoadLevel` 호출 전부 `SceneNames.*` 상수 사용(재확인).

### 5.5 Singleton이 남발되는가 — **아니다, 1개뿐이고 그마저 죽은 코드**
프로젝트 전체 33개 스크립트 중 `static Instance`류 패턴은 `GameManager.Inst` 1개뿐이며, 이를
읽는 코드가 0건(§4.4, 재확인). 나머지는 씬 배치 컴포넌트이거나 `FindObjectsByType`/
`FindFirstObjectByType`으로 그때그때 탐색.

### 5.6 ScriptableObject의 책임이 잘못 사용되는가 — **문제 없음**
`ColorPaletteSO`/`BrushSettingsSO` 둘 다 읽기 전용 데이터 저장소로만 쓰이고, 런타임에 필드를
변경하는 코드는 없음. 이번 커밋에서 `BrushSettingsSO`에 필드(`cursorSurfaceOffset`)가 추가됐지만
동일하게 읽기 전용 패턴을 유지함. SO를 상태 저장소나 이벤트 버스로 쓰는 안티패턴 없음.

### 5.7 Unity Lifecycle 순서 문제가 있는가 — **여러 건, 모두 방어적으로 처리되어 안전**
- `HideOrSeekPlayer.Awake()`가 `networkSync`를 `IsMine` 여부와 무관하게 최우선 생성 — Photon
  디스패치가 `Start()`보다 먼저 올 수 있는 경쟁을 원천 차단.
- `Camera_Ctrl.InitCamera()`/`Start()` 둘 다 같은 `ResetToDefaultView()`를 호출해 호출 순서 무관.
- `PlayerColorDisplay.Awake()`와 `PlayerPaintCanvas.Start()` 실행 순서는 보장되지 않지만
  `TryApplyTaggerColor()`가 캔버스 없으면 조용히 리턴 후 재시도.
- **신규**: `PlayerPaintCanvas.Start()`에서 `paintableMeshCollider`/`skinnedBodyRenderer`/
  `bakedColliderMesh`를 초기화하는데, `Update()`의 `RefreshColliderMesh()`가 이보다 먼저 호출될
  수는 없음(같은 컴포넌트의 `Start()`→`Update()` 순서는 Unity가 보장) — 새로 추가된 코드도
  안전한 초기화 순서를 따름.
- 프로젝트에 커스텀 Script Execution Order 설정 자체가 없음(`ProjectSettings/`에 관련 asset
  부재 확인) — 즉 이런 방어 코드가 실제로 필요한 상황.

### 5.8 Event 구독/해제가 제대로 되는가 — **Photon 콜백은 완전히 깨끗함, UI 버튼 리스너는 프로젝트 전체 공통 패턴으로 미해제**
Photon 콜백은 `MonoBehaviourPunCallbacks`가 `OnEnable`/`OnDisable`에서 자동 처리, 수동 이중 등록
0건. C# `+=`/`-=` 순수 이벤트 구독은 프로젝트에 없음. UI `Button.onClick.AddListener()`는 4개
도메인에 걸쳐 공통 패턴이며 대응 `RemoveListener` 없음(§4.7) — ColorTag만의 문제가 아니라 프로젝트
전반의 컨벤션.

### 5.9 Object Pool과 Instantiate/Destroy가 충돌하는가 — **풀 자체가 없음, 충돌 없음**
프로젝트 실제 코드에 Object Pool 구현이 전혀 없다(`Player.md`에 등장하는 `ItemObjectPool` 등은
§7에서 설명하듯 다른 프로젝트의 참고 코드). `Instantiate`/`Destroy` 호출은 `LobbyController`(방
목록, diff 갱신), `GameLobbyController`(플레이어 목록, 매번 전체 재생성), `BrushCursorController`
(붓 커서 1회 인스턴스화 후 `SetActive` 재사용 — 사실상 수동 풀링 1개체), `PlayerSpawner`(캐릭터)
정도로 전부 생명주기가 명확. `PlayerPaintCanvas`의 `RenderTexture`+**신규 `bakedColliderMesh`**
둘 다 캐릭터당 1회 생성·`OnDestroy()`에서 해제(`Destroy(bakedColliderMesh)` 추가 확인)로 관리됨
— 새 리소스도 기존 해제 패턴을 그대로 따름.

### 5.10 Photon의 Ownership/RPC 구조를 무시하는가 — **잘 지켜짐, 프로젝트에서 가장 성숙한 영역**
- `pv.IsMine` 게이팅이 `HideOrSeekPlayer.Update()`/`FixedUpdate()`, `PlayerPaintCanvas.Update()`
  등에 정확히 적용. **신규 `RefreshColliderMesh()` 호출도 `pv.IsMine` 체크 이후 블록 안에서만
  실행**(원격 캐릭터는 콜라이더를 갱신하지 않음 — 로컬 붓칠 판정에만 필요하므로 올바른 설계).
- 마스터 전용 권위: `ColorSelectionManager`/`RoomLifecycleWatcher` 둘 다 `IsMasterClient` 가드.
- 상태(투표/라운드/인원) vs 순간 이벤트(붓질/채팅)를 CustomProperties/PlayerList vs
  `RaiseEvent`/`RPC`로 적절히 구분.
- 수신부(`OnEvent`/`OnPhotonSerializeView`)는 송신측이 이미 계산한 값을 그대로 재생만 함.
- `pv` 필드는 필요한 클래스에만 존재, Room 단위 상태만 다루는 매니저에는 없음.

### 5.11 중복 로직이 있는가 — **크게 개선된 상태, 경미한 잔여 중복만 존재**
`RoomState` 정적 헬퍼로 4개 파일의 CustomProperties 조회가 통합됨. `GameManager.Start()`와
`PlayerSpawner.Start()`가 "InRoom 대기" 코루틴 패턴을 각자 구현(동일 로직 2곳 복붙, 3줄 내외라
심각하지 않음), `PlayerColorVoteIndicator.LateUpdate()`와 `PlayerBillBoard.LateUpdate()`가 거의
동일한 "카메라 forward 정렬" 코드를 각자 구현(둘 다 3줄).

---

## 6. 종합 결론과 다음 단계 제안 (우선순위순)

1. **[최우선, 통합 공백, 지속]** `GameLobbyController.OnStartGameButtonClicked()` 또는 `GameScene`
   진입 시점에 `ColorSelectionManager.StartColorSelection()` 호출을 추가하고, `PlayerTestScene`의
   `ColorTagManagers`+`ColorSelectionPanel` UI를 `GameScene.unity`에 옮겨 배치한다(§4.1). 코드/에셋은
   이미 완성돼 있어 "씬 배치 + 호출 한 줄" 수준의 작업 — 이번 조사에서도 여전히 유일한 구조적 공백.
2. **`ColorSelectionManager.ResetAllVotes()`가 전원을 리셋해야 하는 의도인지 확인 후**
   `PhotonNetwork.PlayerList`를 순회하도록 수정(§4.2).
3. **본게임(태그/술래잡기) 승패 판정 로직 설계 + `GameEndTime` 기록 주체 결정**(§4.3).
4. **`Cookie_BaseSkin_B`/`_C`와 `07. Expression/` 표정 텍스처를 실제로 활용할 스크립트(플레이어별
   스킨 배정, 표정 전환 트리거 등)를 설계하거나, 당장 계획이 없다면 미사용 에셋임을 명시해둔다**(§4.6, 신규).
5. (선택) `GameManager.Inst`가 죽은 코드라면 제거를 검토(§4.4).
6. (선택) `ColorPaletteSO`에 인덱스 범위 검사 추가 여부 검토(§4.5).
7. (선택) UI 버튼 4곳의 `AddListener`에 대응하는 해제 코드 추가(§4.7).

**전체적으로**: 이 프로젝트는 도메인별 책임 분리(§5.1), Scene 의존성 관리(§5.4), Singleton 절제
(§5.5), Photon Ownership/RPC 구조(§5.10) 네 관점에서 뚜렷한 구조적 문제가 없다. 이번 조사로
확인한 가장 중요한 변화는 **애니메이션 리소스 교체 작업이 완료**됐다는 점(§4.8, 이전 최우선
미완료 항목의 해결)과, `PlayerPaintCanvas`에 추가된 실시간 MeshCollider 베이킹이 기존 아키텍처
경계(로컬 전용, 캐릭터당 리소스, `OnDestroy` 해제)를 정확히 따르며 새로운 구조적 위험을 만들지
않았다는 점이다. 반대로 새로 생긴 것은 "에셋은 만들어졌지만 아직 코드가 그것을 선택/사용하지
않는" 두 번째 사례(`Cookie_BaseSkin_B/C`, §4.6)로, `ColorTagManagers` 씬 배선 공백(§4.1)과 성격이
비슷한 "완성된 조각들 사이의 배선 공백" 패턴이 반복되고 있다. 남은 구조적 문제는 대부분 도메인
자체의 결함이 아니라 **완성된 도메인/에셋들을 게임 전체 플로우로 잇는 배선의 공백**으로 계속
좁혀지고 있다.

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
