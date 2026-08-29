# 조사 보고서: TagOfChaos 프로젝트 전체 심층 분석 (2026-08-29, 커밋 `403f4b6` 기준)

> 이 문서는 커밋 `11f0a9d`(2026-08-21) 기준으로 작성된 이전 버전을 **전면 대체**한다. 이전 버전은
> "`Assets/02. Scripts` 아래 33개 `.cs`, 코드 아키텍처 변화 없음, ColorTag 미니게임은 씬 배선만
> 누락"이라고 결론지었으나, 그 직후 커밋 `d0fdf2a`→`403f4b6` 구간에서 **`GameRule.md` v3.7이
> 설계했던 새 게임 룰(쿠키 자유 색칠 + 괴물 술래잡기)이 실제 코드·프리팹·씬 배선까지 대부분
> 구현됐다.** `git diff --stat 11f0a9d..HEAD` 기준 125개 파일 변경(+13,445/−7,150), 그중:
>
> - **구 시스템 완전 삭제**: `ColorSelectionManager` / `ColorVoteTally` / `TaggerColorAssigner` /
>   `PlayerColorDisplay` / `PlayerColorVoteIndicator` / 구 `ColorTag/RoomLifecycleWatcher` (6개 `.cs`)
> - **신규 `Monster/` 도메인 17개 `.cs`**, `ColorTag/` 신규 4개, `Unit/` 신규 2개, `Dev/` 신규 1개
> - **신규 프리팹** `MonsterPlayer.prefab`(2,258줄), `PlayerResultRow.prefab`
> - **`GameScene.unity` +5,283줄, `GameLobbyScene.unity` 대규모 재구성** — ColorTag + Monster +
>   결과화면 시스템이 실제로 씬에 배선됨
>
> 이번 조사는 `Assets/02. Scripts/` 아래 **8개 도메인 51개 `.cs` 전부를 라인 단위로 정독**하고,
> 4개 씬(`LobbyScene`/`GameLobbyScene`/`GameScene`/`PlayerTestScene`)과 2개 캐릭터 프리팹, 2개
> Animator Controller를 GUID 단위로 대조했다. **이전 보고서의 §4.1(최우선 통합 공백)은 해소됐고**,
> 대신 새 구현에서 **미배선/미구현 항목이 여러 건 발견**됐다 — §6에 우선순위순으로 정리한다.

---

## 0. 프로젝트 개요

**TagOfChaos**는 Unity 6(Built-in RP) + **Photon PUN2** 기반 2~4인(사실상 4인 고정, §6.7)
실시간 멀티플레이어 술래잡기 게임이다. 현재 구현된 게임 루프:

```
LobbyScene       방 목록·생성·랜덤입장 (Photon 로비)
   │  최초 1인만 PhotonNetwork.LoadLevel(GameLobbyScene), 나머지는 AutomaticallySyncScene 자동 동행
   ▼
GameLobbyScene   대기실. 쿠키 스폰됨(돌아다닐 수 있음). 스킨 A/B/C 선택.
   │             가마솥에 자진 입장한 첫 쿠키 = 괴물(30초 무입장 시 마스터가 랜덤 배정).
   │             정원(4명) 찼을 때 방장이 "게임 시작" → PhotonNetwork.LoadLevel(GameScene)
   ▼
GameScene        전원 함께 입장. 괴물로 확정된 플레이어는 쿠키를 스폰하지 않음.
   │  0~60초    : 쿠키 자유 색칠(개인 캔버스, 최대 4색 슬롯 등록, 지우개/리셋).
   │             60초 만료 시 슬롯 0개 플레이어는 서로 겹치지 않는 색으로 전신 강제 도포.
   │  60초~     : 괴물이 GameScene에 합류(로컬 스폰). GameEndTime = 합류시각 + 10분.
   │             괴물: 촉수 돌진(15초 쿨, 20m) + 전방 자동 GrabKill(즉시 파괴).
   │             쿠키: 서로 그랩/캐리(E키). 파괴되면 생존 쿠키 시점 관전(Space).
   │  종료      : 쿠키 전원 파괴 → 괴물 승 / 10분 생존 → 쿠키 승 / 괴물 전원 이탈 → 5초 후 대기실 복귀
   ▼
결과 화면 → 12초 후 방장이 GameLobbyScene으로 복귀(같은 방 유지)
```

> **`GameRule.md`가 "의도적으로 단순화"라고 명시한 것**(v3.7 서두): ① "쿠키만 GameScene 이동,
> 괴물은 GameLobby 대기"는 미구현(PUN2 `AutomaticallySyncScene` 제약) — 전원 함께 이동 후
> `GamePhaseStarter`/`MonsterJoinController`가 순차 트리거. ② 가마솥/문 3D 모델은 원통형
> Primitive 플레이스홀더(`리소스/솥단지.glb` 미임포트). ③ VFX/SFX 전부 미구현(직렬화 훅만 존재).
> ④ 결과화면/슬롯 UI는 기능 위주 최소 배치. ⑤ 밸런스 값은 전부 가정값.

**스크립트 도메인** (`Assets/02. Scripts/` 아래 8개, 총 51개 `.cs`):

| 도메인 | 파일 수 | 역할 |
|---|---|---|
| `Core/` | 1 | 씬 이름 상수 |
| `Lobby/` | 5 | 방 목록·생성·입장, 대기방 인원 표시·게임 시작, 스킨 선택 |
| `GameManager/` | 5 | 채팅 중계(RPC), 쿠키 스폰, 나가기 확인창, 낙사 방지 |
| `Unit/` | 8 | 쿠키 캐릭터: 이동·점프·회피·애니메이션·네트워크 동기화·그랩·스킨·빌보드 |
| `Camera/` | 1 | 쿠키용 3인칭 추적 카메라(우클릭 드래그 회전) |
| `ColorTag/` | 12 | 개인 자유 색칠 미니게임(캔버스·슬롯·스탬프·강제도포) + SO 2개 |
| `Monster/` | 17 | 괴물 선정·리빌·1인칭 카메라·이동·돌진·GrabKill·합류·승패·관전·결과화면·이탈처리 |
| `Dev/` | 2 | 오프라인 부트스트랩, 몬스터 테스트 스폰(PlayerTestScene 전용) |

`Assets/02. Scripts/Player.md`는 `.cs`가 아니라 **다른(이전) 3인칭 슈팅 게임의 `Player.cs`
전문 + 분석 메모**다(무기 스왑/투척/상점/적 스폰/오브젝트 풀 등 이 게임과 무관). 이 프로젝트의
실제 코드가 아니다 — §9 참고.

---

## 1. 네트워크 아키텍처 (Photon PUN2)

이 프로젝트의 핵심 설계 원칙은 4개다:

1. **소유권 원칙**: 각 캐릭터(쿠키/괴물)의 상태는 그 캐릭터의 소유 클라이언트만 쓰고, 나머지는
   소유권과 무관하게 그 값을 읽어 표시만 한다.
2. **마스터 권위**: 방 전체에 영향을 주는 판정(괴물 확정, 페이즈 시작/종료, 강제 도포 배정,
   승패 판정)은 `IsMasterClient` 가드 후 마스터만 `Room.SetCustomProperties`로 확정한다.
3. **상태 vs 순간 이벤트 분리**: 지속 상태(투표/슬롯 수/라운드/괴물 목록/종료 시각)는
   CustomProperties, 순간 이벤트(붓 스트로크, 지우개, 채팅, 가마솥 신청)는 `RaiseEvent`/`RPC`.
4. **폴링 기반 만료 감지**: 타이머는 절대 시각(`PhotonNetwork.Time` 기준)을 프로퍼티에 기록하고,
   마스터가 매 `Update()`에서 경과를 폴링한다(코루틴 타이머 미사용 — 늦게 합류한 클라이언트도
   같은 절대 시각을 보고 동일하게 판정).

### 1.1 Room CustomProperties 키 (`NetKeys.cs`)

| 키 | 타입 | 쓰는 곳 | 읽는 곳 | 비고 |
|---|---|---|---|---|
| `MonsterActorNumbers` | `int[]` | `MonsterAssignmentAuthority.ConfirmMonster`, `RoomLifecycleWatcher.RemoveFromMonsterList` | `PlayerSpawner`, `MonsterRevealController`, `MonsterJoinController`, `GameRuleController`, `RoomLifecycleWatcher`, `ResultScreenController` | 처음부터 배열 — 다중 괴물 확장 대비 |
| `MonsterRevealTime` | `double` | `MonsterAssignmentAuthority.ConfirmMonster` | **없음** | 죽은 키(§6.8) |
| `PaintPhaseEndTime` | `double` | `GamePhaseStarter` | `PlayerPaintCanvas`, `BrushCursorController`, `ColorSelectionPanel`, `PaintPhaseController`, `MonsterJoinController` | 색칠 페이즈 만료 절대 시각 |
| `MonsterJoined` | `int(1)` | `MonsterJoinController.MasterTick` | `GameRuleController`, `MonsterJoinController.TryLocalSpawn` | 괴물 합류 완료 플래그 |
| `GameEndTime` | `double` | `MonsterJoinController.MasterTick` (합류 + 600초) | `GameRuleController` | 이전 보고서 §4.3(이 키를 쓰는 코드 없음)은 **해소됨** |
| `ForcedPaintActorNumbers` / `ForcedPaintColors` | `int[]` / `int[]` | `PaintPhaseController.ResolvePaintPhase` | `PlayerPaintCanvas.ApplyForcedColorIfAssignedToMe` | 인덱스로 1:1 대응 |
| `MonsterDepartedAt` | `double` / `null` | `RoomLifecycleWatcher` | `MonsterDepartureBanner` | 5초 경고 배너 트리거 |
| `GameResult` | `int` (`GameResult` enum) | `GameRuleController.Finish` | `ResultScreenController` | `0`=쿠키 승 / `1`=괴물 승 |
| `RoundIndex`,`RoundEndTime`,`ColorPrefix`,`TaggerActorNumber`,`TaggerVariantSet`,`VoteColorIndex`,`GameEndTime`(구),`CookiesDeparted` | — | **없음** | 구 4라운드 시스템 잔재(§6.8) |

### 1.2 Player CustomProperties 키

| 키 | 쓰는 곳 | 읽는 곳 |
|---|---|---|
| `SkinIndex` (int 0~2) | `PlayerSkinSelector.SelectSkin` | `PlayerSkinApplier.ApplySkin` |
| `RegisteredSlotCount` (int) | `PlayerPaintCanvas.ReportSlotCount` | `PaintPhaseController.ResolvePaintPhase` |
| `HitCount` (int, 0 또는 2) | `HideOrSeekPlayer.RequestGrabKill` | `GameRuleController.AllCookiesBroken`, `SpectatorController.RefreshAliveCookies`, `ResultScreenController` |

### 1.3 이벤트 코드 (`NetEventCodes.cs`)

| 코드 | 이름 | 송신 | 수신 |
|---|---|---|---|
| 1 | `PaintStroke` | `PlayerPaintCanvas.SendStrokeEvent` (`ReceiverGroup.Others`) | `PlayerPaintCanvas.OnEvent` — `viewId` 일치하는 캔버스만 재생. `object[]{viewId,uv.x,uv.y,radius,colorIndex,(byte)StrokeKind}` |
| 2 | `ClaimMonster` | `Cauldron.OnTriggerEnter` (`ReceiverGroup.MasterClient`) | `MonsterAssignmentAuthority.OnEvent` — content = 신청자 `ActorNumber` |
| 3 | `ClearColor` | `PlayerPaintCanvas.SendResetEvent` (`ReceiverGroup.Others`) | `PlayerPaintCanvas.OnEvent` — 해당 캔버스 `GL.Clear` |
| 4 | `FillAll` | **없음** | **없음** — 강제 도포는 코드 1(`StrokeKind.ForceFill`)을 재활용(§6.8) |

### 1.4 RPC

| RPC | 정의 | 호출 | 대상 |
|---|---|---|---|
| `LogMsg(string,bool,PhotonMessageInfo)` | `GameManager` | `GameManager.SendConnectedMessageWhenInRoom`/`BroadcastingChat`, `RoomExitController.OnClickBackBtn` | `AllBuffered` |
| `OnGrabbedByOwner(int)` / `OnReleased(bool)` | `HideOrSeekPlayer` | `PlayerGrabController.TryGrab`/`Release` | 대상 쿠키 소유자 |
| `RequestGrabKill()` | `HideOrSeekPlayer` | `MonsterGrabKillTrigger.OnTriggerEnter` | `RpcTarget.All`(본인만 `hitCount` 확정) |

### 1.5 IPunObservable (`OnPhotonSerializeView`)

- `HideOrSeekPlayer` → `PlayerNetworkSync.Write/Read`: position, rotation, `(int)PlayerMoveState`, `isJump`
- `MonsterController` → `MonsterNetworkSync.Write/Read`: position, rotation, `(int)MonsterMoveState`

두 `*NetworkSync`는 순수 C# 클래스로 구조가 완전히 동일하다(거리 기반 스냅/보간 이원화,
`isFirstUpdate`에 즉시 스냅). `Monster/`가 `Unit/`을 참조하지 않기 위해 **의도적으로 중복**시킨
것(코드 주석에 명시).

---

## 2. 도메인별 상세 동작

### 2.1 `Core/` (1파일)
`SceneNames.cs`: `Lobby`/`GameLobby`/`Game` 3개 상수. 프로젝트 전체에서 씬 이름 하드코딩은 이
파일 안에만 존재(재확인).

### 2.2 `Lobby/` (5파일)
- **`LobbyController`** (`LobbyScene`): `ConnectUsingSettings()` → `JoinLobby()`. 방 생성 /
  랜덤입장 / 이름입장 3진입점. `OnRoomListUpdate()`가 `RoomInfo` 캐시를 diff 방식으로
  `RoomListItem`에 반영. `MaxPlayers=4` 고정, `GameVersion="1"`. `AutomaticallySyncScene=true`.
  방을 새로 만든 최초 1인(`PlayerCount==1`)만 `GameLobby` 로드.
- **`GameLobbyController`** (`GameLobbyScene`): Photon 콜백
  (`OnPlayerEnteredRoom`/`OnPlayerLeftRoom`/`OnMasterClientSwitched`) + `Update()` 안전망(늦게
  입장한 클라가 놓친 초기 상태를 다음 프레임에 스스로 보정) 이중 구조. 방장에게만 "게임 시작"
  버튼 노출, **정원(4명)이 찼을 때만** `interactable`. `OnStartGameButtonClicked()`가
  `Room.IsOpen=false` + `LoadLevel(Game)`. 새 게임 룰의 색칠 시작은 `GamePhaseStarter`(GameScene)가
  담당하므로 이 클래스는 그것을 몰라도 된다 — **이전 보고서 §4.1의 통합 공백은 이렇게 해소됨.**
- **`PlayerListItem`** / **`RoomListItem`**: 순수 표시용. `RoomListItem`은 `IsOpen==false`면 입장
  버튼 잠금.
- **`PlayerSkinSelector`** (신규, `GameLobbyScene`의 `SkinSelectPanel`): A/B/C 버튼 →
  `LocalPlayer.SetCustomProperties({SkinIndex})`. 언제든 다시 눌러 변경 가능, 미선택 시 기본 A.

### 2.3 `GameManager/` (5파일)
- **`GameManager`**: 채팅 중계 전담. `static Inst`(유일 싱글턴, **읽는 코드 0건** — §6.8).
  `Start()`가 `InRoom==true` 대기 코루틴 후 "Connected" 로그를 `LogMsg` RPC 브로드캐스트.
  Enter 키로 채팅창 토글 + `is_Conversating` → `SetLocalPlayerMovementLocked()` 경유로
  로컬 `HideOrSeekPlayer.IsMovementLocked`에 연결(`FindObjectsByType`로 로컬 쿠키 탐색·캐시).
- **`PlayerSpawner`** (GameScene / GameLobbyScene / PlayerTestScene): `InRoom` 대기 후
  `IsAlreadyMonster()`면 스킵(괴물 확정자·`OfflineModeBootstrap.SpawnAsMonster`), 아니면
  `GameObject.Find("PlayerSpawnPos")` 반경 5 랜덤 오프셋으로 `PhotonNetwork.Instantiate("HideOrSeekPlayer")`.
  진단 로그(`ViewID`/`IsMine`/`IsRoomView`) 포함.
- **`ConfirmDialog`**: 재사용 예/아니오 확인창(`Action` 콜백).
- **`RoomExitController`**: 뒤로가기(확인창 → `LeaveRoom()` → `LobbyScene`). 마지막 1인 퇴장 시
  `Room.CustomProperties.Clear()` + 본인 `LocalPlayer.CustomProperties.Clear()`. `[SerializeField]
  PhotonView pv`로 `LogMsg` RPC — "GameManager와 같은 오브젝트의 PhotonView" 암묵 계약(§6.11).
- **`VoidKillZone`**: 트리거 콜라이더로 맵 밖 낙사 시 로컬 쿠키 `RespawnToSpawnPoint()`.

### 2.4 `Unit/` — 쿠키 캐릭터 (8파일)
`HideOrSeekPlayer`(`MonoBehaviourPunCallbacks`, `IPunObservable`)가 조정자. Rigidbody 물리 이동
+ 협력 클래스(순수 C#: `PlayerGroundDetector`, `PlayerAnimationDriver`, `PlayerNetworkSync`).

- **`PlayerMoveState`** enum: `Idle/Walk/Run/Jump/Dodge/Held/Broken`. `Animator.SetTrigger(state.ToString())`이
  Animator 파라미터 이름과 직접 매칭되는 암묵 계약. **`Held`/`Broken`은 `PlayerAnimator.controller`에
  트리거 파라미터로는 추가됐으나 대응 상태(state)·전이가 없다**(§6.3) — v3.7이 "파라미터가 아예
  없어 `SetTrigger`가 에러나던 버그"를 파라미터 추가로만 막은 것.
- **`PlayerGroundDetector`**: `Physics.Raycast`로 매 `FixedUpdate` 접지 폴링(이벤트 기반 아님 →
  낭떠러지 낙하 미시작 사각지대 없음).
- **`PlayerAnimationDriver`**: `ChangeState()`는 같은 라벨이면 무시. 점프만 `ReplayJump()`가
  `Animator.Play("Jump",0,0f)`로 트랜지션 그래프 우회(연타 재점프 블렌딩 버그 차단,
  `Bug-fix-plan.md §15/§18`). `HandleJumpAnimationHold()`가 정점 부근 `speed=0`, `suppressHoldCheckOnce`로
  한 프레임 유예(§16). `SetCarryLayerWeight()`는 "Carry" 레이어 없으면 조용히 무시.
- **`PlayerNetworkSync`** / **`PlayerBillBoard`**(머리 위 닉네임, 자기 transform 직접 회전).
- **`HideOrSeekPlayer`**:
  - `Awake()`: `networkSync`를 `IsMine` 무관하게 최우선 생성(Photon 디스패치가 `Start()`보다
    먼저 올 수 있는 경쟁 차단). `IsMine`이면 `Camera.main.GetComponent<Camera_Ctrl>().InitCamera(gameObject)`.
  - `Start()`: 로컬만 `useGravity`+`ContinuousDynamic`+`Interpolate`+`FreezeRotation`, 원격은
    `isKinematic=true`. `Mesh_0`(몸통 메시)와 루트 `CapsuleCollider`의 자기 충돌을
    `Physics.IgnoreCollision`으로 무시(`Bug-fix-plan.md §14`).
  - `FixedUpdate()`: 매 스텝 접지 재확인. `jumpRequested && grounded` → 점프, `!isJump && !grounded`
    → 낙하도 동일 처리. `Move()`는 `rb.MoveRotation()` 회전 + 수평 속도만 덮어쓰고 수직 속도 보존.
    Shift +30% 질주, LeftCtrl 회피(2배속 0.5초).
  - **그랩/캐리**: `carrierViewId`로 로컬 추적, `TryFollowCarrier()`가 매 `FixedUpdate`에서
    그랩버 `PlayerGrabController.CarrySocket` 위치로 이동(소유권 이전 없음). `OnGrabbedByOwner`/`OnReleased`
    RPC가 `IsMovementLocked` + `ChangeState(Held/Idle)`.
  - **`RequestGrabKill()`** [PunRPC, `RpcTarget.All`]: 본인만 `hitCount=2` +
    `SetCustomProperties({HitCount:2})` + `IsMovementLocked=true` + `ChangeState(Broken)` +
    `SpectatorController.EnterSpectatorMode()`.
  - `RespawnToSpawnPoint()`: `rb.position`과 `transform.position` 둘 다 갱신.

- **`PlayerGrabController`** (신규): E키 토글. `Physics.OverlapSphere(grabRange, cookieLayer)`로
  대상 쿠키 탐색 → `OnGrabbedByOwner` RPC + `SetCarryLayerWeight(1)`. 다시 E → `Release`.
- **`PlayerSkinApplier`** (신규): `Awake()`에서 `pv.Owner.CustomProperties[SkinIndex]` → `skins[]`
  배열에서 골라 `bodyRenderer.sharedMaterial` 교체. `OnPlayerPropertiesUpdate`로 늦게 도착한
  값 재적용. `PlayerPaintCanvas.InitPaintCanvas()`가 나중에 `sharedMaterial._MainTex`를 읽으므로
  스폰 프레임에 스킨이 먼저 반영돼야 함(Awake 사용 근거).

### 2.5 `Camera/` (1파일)
**`Camera_Ctrl`** (쿠키 전용 3인칭): `InitCamera(player)`/`Start()` 둘 다 `ResetToDefaultView()`
호출(순서 무관). 우클릭 드래그로만 회전(수직 -7°~80°), `Quaternion.Slerp` 추적, 고정 거리 3.2.
휠 줌 완전 제거(휠은 `PlayerPaintCanvas.HandleBrushSizeInput` 전용). **`m_Player`는
`HideOrSeekPlayer.Awake()`에서만 세팅** — 괴물 플레이어에게는 세팅되지 않아 카메라가 씬 기본
위치에 고정된다(§6.1).

### 2.6 `ColorTag/` — 개인 자유 색칠 (12파일)
- **SO 2개**: `BrushSettingsSO`(붓 반경/휠 감도/커서 프리팹/표면 오프셋), `ColorPaletteSO`(고정
  10색: Red/Orange/Yellow/Lime/Green/Teal/Blue/Navy/Purple/Magenta. `GetColor()`/`GetColorName()`
  범위 검사 없음 — §6.9). `DefaultColorPalette.asset`/`DefaultBrushSettings.asset`.
- **`NetKeys` / `NetEventCodes` / `RoomState`**(Room 프로퍼티 안전 조회 헬퍼: `TryGetInt/Double/IntArray`).
- **`PlayerPaintCanvas`** (`HideOrSeekPlayer.prefab`에 부착, `IOnEventCallback`): 핵심 클래스.
  - `Start()`: 캐릭터당 512×512 `RenderTexture` 런타임 생성, `paintedSkinShader`로 원본 스킨
    `_MainTex` + `_PaintTex` 합성 머티리얼을 `bodyRenderer.material`(인스턴스)에 적용.
    `paintRaycastMask = DefaultRaycastLayers & ~PlayerCapsule`(캡슐이 몸통을 가리는 문제 회피,
    `Bug-fix-plan.md §17`).
  - `Update()`(로컬 + 색칠 페이즈 중만): 3프레임 주기 `RefreshColliderMesh()` —
    `SkinnedMeshRenderer.BakeMesh()`로 현재 포즈를 구워 `MeshCollider`에 반영(`localScale`로
    나눠 정규화). 매 프레임 시 257→15fps라 3프레임에 1번(`Bug-fix-plan.md §20`).
  - 좌클릭: `ScreenPointToRay` → `hit.collider == paintableCollider` 확인 → `hit.textureCoord`에
    스탬프. `TryRegisterSlotAndStamp()`가 미등록 색은 누적 `minStrokesToRegister`(15)회 이상
    칠해야 슬롯 등록(최대 4). 등록 시 `ReportSlotCount()` → Player 프로퍼티. 4색 초과 시
    `OnSlotRejected`.
  - `ApplyStamp()`: `Graphics.Blit` 3회(temp 경유). `SendStrokeEvent()`로 `RaiseEvent`.
  - `OnEvent()`: 다른 클라 스트로크/지우개 재생(송신측 판단 그대로).
  - `SetEraseMode()` / `ResetCanvas()`(GL.Clear + 슬롯 초기화 + `ClearColor` 이벤트).
  - `OnRoomPropertiesUpdate` → `ApplyForcedColorIfAssignedToMe()`: `ForcedPaintActorNumbers`에
    본인 있으면 `finalizeStampMaterial`로 `float.MaxValue` 반경 전신 도포 + `ForceFill` 이벤트.
  - `OnDestroy()`: `RenderTexture.Release()` + `Destroy(bakedColliderMesh)`.
- **`BrushCursorController`** (`GameScene`의 `PaintManagers`): 3D 붓 커서 1회 인스턴스화 후 재사용.
  매 프레임 레이캐스트로 로컬 캔버스 표면 위면 커서 표시 + OS 커서 숨김, 아니면 반대. 색칠
  페이즈(`PaintPhaseEndTime`)로 활성 판정.
- **`ColorSwatchButton`**(×10, `Swatch0~9`, `colorIndex` 0~9) / **`PaintToolButton`**(Erase/Reset):
  런타임 스폰되는 캔버스라 인스펙터 연결 불가 → 클릭 시 `FindObjectsByType<PlayerPaintCanvas>`로
  로컬 캔버스 재탐색.
- **`ColorSelectionPanel`** (`GameScene`의 `ColorSlotPanel`): 남은 시간 + 슬롯 수(`N / 4`) 표시,
  페이즈 아닐 때 `SetActive(false)`.
- **`PaintPhaseController`** (`GameScene`의 `GameRuleManagers`, 마스터 전용): `PaintPhaseEndTime`
  만료 시 `RegisteredSlotCount==0` 플레이어 수집 → 팔레트 셔플 → 앞에서부터 겹치지 않게 배정 →
  `ForcedPaintActorNumbers`/`ForcedPaintColors` 세팅.
- **`PlayerCrackDisplay`** (`HideOrSeekPlayer.prefab`): `OnPlayerPropertiesUpdate`에서 `HitCount>=2`면
  `bodyRenderer.enabled=false` + `breakVfxPrefab` Instantiate(**VFX 미연결** — §6.12). 균열
  (hitCount==1) 개념은 v3.6에서 완전 폐기, 파괴 전용으로 단순화.

### 2.7 `Monster/` — 괴물 술래잡기 (17파일)

**선정 (GameLobbyScene)**
- **`Cauldron`** (`GameLobbyScene`, 트리거 콜라이더): 로컬 쿠키 진입 시 `ClaimMonster` 이벤트를
  마스터에게. 원통형 Primitive 플레이스홀더.
- **`MonsterAssignmentAuthority`** (`GameLobbyScene`의 `MonsterManagers`, 마스터 전용,
  `IOnEventCallback`): `ClaimMonster` 첫 수신자 확정, 또는 `Start` 이후 `monsterSelectTimeout`(30초)
  경과 시 `PlayerList` 중 랜덤 배정. `ConfirmMonster()` → `MonsterActorNumbers` + `MonsterRevealTime`.
- **`MonsterRevealController`** (`GameLobbyScene`의 `MonsterRevealBanner`): `MonsterActorNumbers`
  세팅 감지 시 "{닉네임}이(가) 괴물이 되었습니다!" 텍스트 배너. **3D 프리팹 교체·파티클 연출은
  미구현**(텍스트만).

**페이즈 진행 (GameScene, 마스터 전용 폴링)**
- **`GamePhaseStarter`**: `PaintPhaseEndTime` 미설정 시 `PhotonNetwork.Time + 60` 세팅.
- **`MonsterJoinController`**: `PaintPhaseEndTime` 경과 시 마스터가 `MonsterJoined=1` +
  `GameEndTime = Time + 600`. 그리고 괴물 소유 클라 각자가 `MonsterSpawnPos`에서
  `PhotonNetwork.Instantiate("MonsterPlayer")` 로컬 스폰.
- **`GameRuleController`** (마스터 전용): `MonsterJoined` 후 매 `Update()`. `AllCookiesBroken()`
  (괴물 제외 전원 `HitCount>=2`) → 괴물 승. `GameEndTime` 경과 → 쿠키 승. → `GameResult` 세팅.

**괴물 캐릭터 (`MonsterPlayer.prefab`)**
- **`MonsterMoveState`** enum: `Idle/Walk/TentacleDash/GrabKill`. `MonsterAnimator.controller`의
  트리거 파라미터와 일치해야 함. **Animator 상태명은 여전히 `GrapKill`(오타)** — 트리거
  파라미터만 `GrabKill`로 새로 추가됐고, `GrabKill`/`GrapKill` 두 전이가 모두 GrapKill 상태를
  가리킨다(GUID 대조 확인). `MonsterController.LateUpdate`가 `state.IsName("GrapKill")`에 의존.
- **`MonsterController`** (`IPunObservable`): HideOrSeekPlayer를 재사용하지 않음(1인칭은
  카메라=캐릭터 정면 전제라 이동 입력 처리가 근본적으로 다름). Rigidbody 물리(로컬만 시뮬레이션).
  `Update()`: 마우스로 yaw(transform.rotation) + pitch(eyeSocket.localRotation), WASD 이동 입력,
  LeftShift → `MonsterTentacleDash.TryStartDash`. `FixedUpdate()`: 대시 중엔 `rb.MovePosition`,
  아니면 `rb.linearVelocity`. `LateUpdate()`: GrabKill 애니메이션 `normalizedTime>=1` 감지 시
  `grabKillTrigger.ResetTrigger()` + `ChangeState(Idle)`.
  - **`Awake()`가 `Camera.main.GetComponent<MonsterFirstPersonCamera>()?.InitCamera(eyeSocket)`를
    호출하지만 그 컴포넌트가 어느 씬/프리팹에도 없다** → 괴물은 카메라 제어를 받지 못한다(§6.1).
- **`MonsterTentacleDash`** (순수 C#): 쿨 15초, 20m/0.25초(80m/s). `SphereCast(obstructionMask)`로
  시작 시 사거리 클램프. `TickDash()`가 스텝별 변위 반환. **프리팹 `obstructionMask`가 `Nothing`이라 벽 관통 확정**(§6.4).
- **`MonsterFirstPersonCamera`**: `InitCamera(eyeSocket)` 후 `LateUpdate`에서 카메라를 눈 위치·
  회전에 그대로 고정. **미배선**(§6.1).
- **`MonsterGrabKillTrigger`** (`MonsterPlayer.prefab`의 `SphereCollider` trigger): 괴물 소유
  클라만, 쿨다운 아니면, 전방 트리거에 쿠키 진입 시 `onCooldown=true` +
  `MonsterController.PlayGrabKill()` + 대상 쿠키에 `RequestGrabKill` RPC(`RpcTarget.All`).
  쿨다운은 GrabKill 애니메이션 종료까지(`MonsterController.LateUpdate`가 해제).
- **`MonsterNetworkSync`**: `PlayerNetworkSync`와 동일 구조(의도적 중복).

**종료/이탈/관전/결과**
- **`RoomLifecycleWatcher`** (`GameScene`의 `GameRuleManagers`, `Monster/` 재작성판): 마스터가
  `OnPlayerLeftRoom`에서 괴물 이탈 감지 → `RemoveFromMonsterList` → 남은 괴물 0이면
  `MonsterDepartedAt` 세팅, 5초 후 `Room.IsOpen=true` + `LoadLevel(GameLobby)`(방 유지). 쿠키
  이탈은 이 경로와 무관(파괴되면 관전만, 나가도 게임 계속).
- **`MonsterDepartureBanner`** (`GameScene`): `MonsterDepartedAt` 감지 시 "괴물이 나갔습니다.
  5초 뒤 게임이 종료됩니다." 배너.
- **`SpectatorController`** (`HideOrSeekPlayer.prefab`): `RequestGrabKill` 파괴 순간 진입.
  `Camera_Ctrl.enabled=false` 후 `LateUpdate`에서 생존 쿠키(`HitCount<2`) 뒤를 추적. Space로
  순환. 목록 갱신은 Space/진입 시점에만(`RefreshAliveCookies`).
- **`ResultScreenController`** (`GameScene`의 `ResultScreen`): `GameResult` 감지 → 승리 배너 +
  생존 수(`N / 4`) + 쿠키 아이콘 컬러/그레이 + `PlayerResultRow` 프리팹 목록 + 12초 자동
  카운트다운 → 방장이 `LoadLevel(GameLobby)`.
- **`PlayerResultRow`** (`PlayerResultRow.prefab`): 이름 + 상태("(괴물)"/"○ 생존"/"✕ 부숴짐").

### 2.8 `Dev/` (2파일)
- **`OfflineModeBootstrap`**: `Awake()`에서 `OfflineMode=true` + `SpawnAsMonster` static 노출.
  `autoCreateRoom` 체크 시 방 생성만(구 `StartColorSelection` 자동 호출은 v3.6 삭제 이후 미복구).
- **`MonsterTestSpawner`** (신규, PlayerTestScene 전용): `SpawnAsMonster`면 `MonsterSpawnPos`에서
  `MonsterPlayer` 스폰.

---

## 3. 씬 배선 실측 (GUID 대조)

| 씬 | Build Settings | 주요 GameObject → 스크립트 |
|---|---|---|
| **`LobbyScene`** | 포함 | `LobbyUICanvas`(`LobbyController`), Camera, Light, EventSystem |
| **`GameLobbyScene`** | 포함 | `GameManager`(+`PlayerSpawner`), `PlayerSpawnPos`, `Cauldron`(`Cauldron`), `MonsterManagers`(`PhotonView`+`MonsterRevealController`+`MonsterAssignmentAuthority`), `SkinSelectPanel`(`PlayerSkinSelector`), `MonsterRevealBanner`, `Main Camera`(`Camera_Ctrl`), 채팅 UI(`InputFieldChat`/`PanelLogMsg`), `ConfirmDialog`, `LobbyEnvironment` |
| **`GameScene`** | 포함 | `GameManager`(`GameManager`+`PhotonView`+`RoomExitController`+`PlayerSpawner`), `PlayerSpawnPos`, `MonsterSpawnPos`, `VoidKillZone`, `Ground`, `Main Camera`(**`Camera_Ctrl`만** — `m_Player: 0`), `GameRuleManagers`(`PhotonView`+`PaintPhaseController`+`RoomLifecycleWatcher`+`GameRuleController`+`MonsterJoinController`+`GamePhaseStarter`), `PaintManagers`(`BrushCursorController`), `ColorSlotPanel`(`ColorSelectionPanel`), `SwatchRow`(`Swatch0~9` = `ColorSwatchButton` ×10, `colorIndex` 0~9 전부), `ResetButton`/`EraseButton`(`PaintToolButton`), `ResultScreen`(`ResultScreenController` + banner들), `MonsterDepartureBanner`, 채팅 UI |
| **`PlayerTestScene`** | **미포함(개발용)** | `TestBootstrap`(`OfflineModeBootstrap`), `ColorTagManagers`, `GameUICanvas`, `PlayerSpawnPos`, `MonsterSpawnPos`, `VoidKillZone`, `Main Camera` |

**`MonsterFirstPersonCamera`(guid `a924b161…`)는 `Assets/` 전체에서 참조 0건** —
`grep -rn` 확인. 이것이 이번 조사의 최우선 발견이다(§6.1).

**프리팹 (`Assets/04. Prefabs/Resources/`, `PhotonNetwork.Instantiate` 대상)**:
- **`HideOrSeekPlayer.prefab`**: `HideOrSeekPlayer` + `PlayerGrabController` + `PlayerSkinApplier` +
  `PlayerBillBoard` + `PlayerPaintCanvas` + `PlayerCrackDisplay` + `SpectatorController` +
  `PhotonView` + `Animator`(`PlayerAnimator.controller`) + `Rigidbody` + `CapsuleCollider` +
  `Mesh_0`(layer 9 = Cookie, 자체 kinematic Rigidbody + `MeshCollider`). `Unit`과 `ColorTag`
  두 도메인 컴포넌트가 물리적으로 한 프리팹에 공존(§7.3).
- **`MonsterPlayer.prefab`**: `MonsterController` + `MonsterGrabKillTrigger` + `PhotonView` +
  `Animator`(`MonsterAnimator.controller`, avatar 포함) + `Rigidbody` + `CapsuleCollider`(non-trigger)
  + `SphereCollider`(trigger, GrabKill) + `SkinnedMeshRenderer`. `eyeSocket`/`grabKillTrigger`/
  `animator`/`monsterPv`/`monsterController` 직렬화 참조 전부 연결됨. **모든 오브젝트 layer 0**
  (Monster 레이어 10 미사용).
- **`Brush.fbx` / `BrushCursor.prefab`**: 3D 붓 커서.

**Animator Controller**:
- `PlayerAnimator.controller`: 파라미터 `Idle/Walk/Run/Jump/Dodge/Held/Broken`(7 트리거). 상태:
  Idle/Walk/Run/Jump/Dodge. **`Held`/`Broken` 상태 없음**(§6.3).
- `MonsterAnimator.controller`: 파라미터 `Idle/Walk/TentacleDash/GrapKill/GrabKill`(5 트리거).
  상태: Idle/Walk/TentacleDash/GrapKill. `GrabKill`·`GrapKill` 두 전이 모두 GrapKill 상태로.

---

## 4. 마이그레이션 완료 상태 (구 시스템 → 새 게임 룰)

`GameRule.md` v3.7이 "코드/씬 배선까지 완료"라고 주장한 항목을 실제 파일로 검증한 결과:

| 항목 | 상태 | 근거 |
|---|---|---|
| 구 4라운드 색상 시스템 6개 `.cs` 삭제 | ✅ | `git diff --stat`에 `ColorSelectionManager`/`ColorVoteTally`/`TaggerColorAssigner`/`PlayerColorDisplay`/`PlayerColorVoteIndicator`/구`RoomLifecycleWatcher` 전부 `.meta`까지 삭제 |
| §1.5 스킨 선택 | ✅ 코드+씬 | `PlayerSkinSelector`(GameLobbyScene) + `PlayerSkinApplier`(prefab) |
| §2 가마솥/괴물 선정 | ✅ 코드+씬 | `Cauldron` + `MonsterAssignmentAuthority` + `MonsterRevealController` |
| §3 자유 색칠·슬롯·지우개·리셋·강제도포 | ✅ 코드+씬 | `PlayerPaintCanvas` 대폭 개정 + `PaintPhaseController` + `GamePhaseStarter` |
| §4.1 그랩/캐리 | ✅ 코드 | `PlayerGrabController` + `HideOrSeekPlayer` RPC |
| §4.4 GrabKill 자동 처형 | ✅ 코드 | `MonsterGrabKillTrigger` + `RequestGrabKill` RPC |
| §4.3 TentacleDash | ✅ 코드 | `MonsterTentacleDash` + `MonsterController` |
| §6.2 1인칭 카메라 + MonsterController | ⚠️ **코드만, 씬 미배선** | `MonsterFirstPersonCamera` 어디에도 부착 안 됨(§6.1) |
| §6.3 관전 | ✅ 코드 | `SpectatorController` |
| §6.4 GameEndTime | ✅ 코드 | `MonsterJoinController.MasterTick` |
| §7.1 괴물 이탈 처리 | ✅ 코드+씬 | `Monster/RoomLifecycleWatcher` + `MonsterDepartureBanner` |
| §8 승리 판정 + 결과 화면 | ✅ 코드+씬 | `GameRuleController` + `ResultScreenController` + `PlayerResultRow` |
| §6.1 안개 | ⚠️ 부분 | `GameScene` RenderSettings에 `m_Fog: 1`, density 0.01 상시 켜짐. 단 "괴물 합류 시 쿠키 시야가 더 좁아진다"는 동적 축소는 스크립트 부재 |
| VFX/SFX | ❌ 전부 미구현 | `breakVfxPrefab` 등 직렬화 필드만, 에셋 없음 |
| 가마솥/문 3D 모델 | ❌ Primitive | `리소스/솥단지.glb` 미임포트 |

---

## 5. Unity 프로젝트 설정

- **레이어**: 8=`PlayerCapsule`, 9=`Cookie`, 10=`Monster`. 코드는 `LayerMask.GetMask("PlayerCapsule")`
  참조. **`MonsterPlayer.prefab`은 Monster 레이어(10)를 안 씀**(전부 0) — `PlayerGrabController.cookieLayer`
  마스크가 쿠키만 잡도록 돼 있으면 괴물이 그랩 대상에서 자연히 빠지지만, 레이어 규율은 느슨하다.
- **Script Execution Order 커스텀 설정 없음** — `Awake` 우선 생성 패턴들이 실제로 필요한 이유.
- **Photon**: `PhotonServerSettings.asset` 존재(AppId 등). PUN2. `AutomaticallySyncScene=true`.
- **3rd party**: Photon PUN2/Chat/Realtime, NatureStarterKit2(환경 에셋), ProBuilder(로비 환경
  메시 `pb_Mesh*`), TextMeshPro(+`NotoSansKR SDF` 한글 폰트 — 워킹트리에서 유일하게 수정됨).
- `TagOfChaosGame/` 폴더에 **빌드 산출물**이 커밋돼 있음(`level0~2`, `resources.assets` 등 — diff에
  포함됨).

---

## 6. 발견된 문제 (우선순위순)

### 6.1 [최우선] `MonsterFirstPersonCamera`가 어떤 씬에도 부착돼 있지 않음 — 괴물 플레이어에게 카메라가 없음
`MonsterController.Awake()`는 `Camera.main.GetComponent<MonsterFirstPersonCamera>()?.InitCamera(eyeSocket)`를
호출하지만, 이 컴포넌트의 GUID(`a924b161…`)는 `Assets/` 전체에서 참조가 0건이다(`GameScene`/
`GameLobbyScene`/`PlayerTestScene` Main Camera 모두 `Camera_Ctrl` 하나만 부착, `MonsterPlayer.prefab`에도
없음). 결과:
- 괴물 소유 클라이언트는 GameScene 입장 후 **카메라가 씬 기본 위치(`0,1,-10`)에 고정**된다.
  `Camera_Ctrl.m_Player`도 `HideOrSeekPlayer.Awake()`에서만 세팅되므로 괴물에겐 null → 아무것도
  따라가지 않음.
- 게다가 괴물은 색칠 60초 동안 캐릭터조차 없어(쿠키 스폰 스킵, MonsterPlayer는 60초 후 스폰)
  **첫 1분은 씬 기본 위치에 고정된 정지 화면**이다.
- 마우스 룩(`MonsterController.Update`의 `Input.GetAxis("Mouse X")`)은 `transform.rotation`을
  돌리지만 카메라가 그걸 안 따라가므로 화면상 아무 변화 없음.

**조치**: `GameScene`(그리고 `PlayerTestScene`)의 Main Camera에 `MonsterFirstPersonCamera`
컴포넌트를 추가하거나, `MonsterController.Awake`가 `AddComponent<MonsterFirstPersonCamera>()`로
런타임 부착하도록 변경. `Camera_Ctrl`/`MonsterFirstPersonCamera`가 같은 오브젝트에 공존하되
`SpectatorController`가 이미 `Camera_Ctrl.enabled=false`를 하듯, 괴물일 때 `Camera_Ctrl`을
비활성화하는 스위칭도 필요.

### 6.2 [높음] `Cursor.lockState`를 설정하는 코드가 전무
괴물 1인칭 마우스 룩과 쿠키 우클릭 드래그 회전 둘 다 커서 잠금이 없다. 괴물은 매 프레임 무조건
`Input.GetAxis("Mouse X/Y")`를 읽으므로 마우스가 게임 창을 벗어나면 시점이 멈추고, ColorTag의
좌클릭 색칠·스와치 클릭과도 입력이 섞인다. 최소한 괴물 진입 시 `Cursor.lockState = Locked`가
필요하다.

### 6.3 [중간] `PlayerAnimator`에 `Held`/`Broken` 상태·전이가 없음
v3.7이 "트리거 파라미터가 없어 `SetTrigger`가 에러나던 버그"를 파라미터 추가로만 막았다.
파라미터 `Held`/`Broken`은 있으나 대응 애니메이션 상태와 전이가 없어, 그랩당하거나 파괴돼도
캐릭터가 이전 포즈(Idle 등) 그대로다. 처형/피격 피드백이 시각적으로 없음.
(`MonsterAnimator`의 GrabKill은 `GrapKill` 상태 + 두 전이가 실제로 있어 동작 — 이쪽은 OK.)

### 6.4 [중간, 확정] `MonsterTentacleDash`의 `obstructionMask`가 프리팹에서 `Nothing`(0)
`MonsterPlayer.prefab`의 `MonsterController.obstructionMask.m_Bits: 0`을 직접 확인했다.
`Physics.SphereCast`가 아무것도 맞히지 못해 대시(`TryStartDash`)가 **항상 최대 20m 전진, 벽을
관통한다.** 인스펙터에서 지형 레이어를 지정해야 한다.

### 6.5 [높음] 재게임 시 Room/Player CustomProperties가 초기화되지 않음
정상 종료 경로(`ResultScreenController.OnLobbyButtonClicked` → `LoadLevel(GameLobby)`,
`RoomLifecycleWatcher.ReturnToGameLobby`)는 `MonsterActorNumbers` / `MonsterJoined` /
`PaintPhaseEndTime` / `GameEndTime` / `GameResult` / `ForcedPaint*`(Room)와
`HitCount` / `RegisteredSlotCount`(Player)를 전혀 비우지 않는다. `Room.CustomProperties.Clear()`는
`RoomExitController`(마지막 1인이 **방을 완전히 나갈 때만**)에만 있다. 다음 게임 시작 시:
- `GamePhaseStarter`가 `PaintPhaseEndTime` 잔존 값을 보고 `started=true`로 즉시 종료 → 색칠
  페이즈가 아예 시작 안 됨.
- `MonsterJoinController`가 이전 `MonsterJoined` 잔존 → 즉시 괴물 스폰 시도.
- `GameRuleController`가 이전 `HitCount>=2` 잔존 → `AllCookiesBroken`이 참 → 시작하자마자 괴물 승.
- `PlayerSpawner.IsAlreadyMonster()`가 이전 `MonsterActorNumbers` 잔존 → 엉뚱한 사람이 쿠키
  스폰 스킵.

**조치**: `LoadLevel(GameLobby)` 직전(마스터)에 게임 관련 Room 프로퍼티를 `null`로 세팅하고,
각 클라가 자기 `HitCount`/`RegisteredSlotCount`를 리셋하는 정리 루틴 필요.

### 6.6 [중간] `MonsterAssignmentAuthority` 30초 타임아웃이 정원 대기 시간보다 짧을 수 있음
마스터가 `GameLobbyScene`에 들어온 시점부터 30초가 지나면, 4명이 다 모이기 전이라도 랜덤으로
괴물이 확정된다. 자진 입장 기회가 사실상 30초로 제한됨. 타임아웃 기준을 "정원 도달 후"로
바꾸거나 값을 늘리는 게 자연스럽다.

### 6.7 [중간] 2~3인 플레이 불가 — `MaxPlayers=4` 고정 + "정원일 때만 시작"
`GameRule.md`/`UserPlan.md`는 "2~4인"을 표방하지만 `LobbyController.MaxPlayers=4`,
`GameLobbyController.RefreshStartButton`이 `PlayerCount >= MaxPlayers`, `OnStartGameButtonClicked`도
동일 재확인. 실제로는 정확히 4명이어야 시작 가능.

### 6.8 [경미] 죽은 코드 / 미사용 식별자
- `GameManager.Inst` — 읽는 코드 0건(유일한 static 싱글턴).
- `NetKeys`: `RoundIndex`, `RoundEndTime`, `ColorPrefix`, `TaggerActorNumber`, `TaggerVariantSet`,
  `VoteColorIndex`, `GameEndTime`(구, 별개), `CookiesDeparted`, `MonsterRevealTime`(쓰기만) — 구
  시스템 잔재 또는 미완.
- `NetEventCodes.FillAll`(4) — 강제 도포는 코드 1(`StrokeKind.ForceFill`) 재활용이라 미사용.
- `RoomState.GetRoundIndex()` — 호출부 없음.

### 6.9 [경미, 지속] `ColorPaletteSO.GetColor()`/`GetColorName()` 범위 검사 없음
인덱스가 0~9 벗어나면 `IndexOutOfRangeException`. 모든 호출부가 유효 인덱스만 넘긴다는 암묵
전제. `PlayerPaintCanvas.ApplyStamp`가 강제 도포 시 `radius = float.MaxValue`를 넘기는데 셰이더가
이를 처리한다는 전제도 동일 계열.

### 6.10 [경미, 지속] UI 버튼 `AddListener`에 대응하는 해제 코드 없음
`ConfirmDialog`/`ColorSwatchButton`/`PaintToolButton`/`PlayerSkinSelector`/`RoomListItem`/
`RoomExitController` 전부 `Awake`/`Start`의 `onClick.AddListener`만 있고 `OnDestroy`/`RemoveListener`
없음. GameObject 수명과 함께 소멸하므로 현재는 안전하나 프로젝트 전반의 공통 패턴.

### 6.11 [경미, 지속] `RoomExitController.pv` 씬 배선 암묵 계약
`LogMsg` RPC 수신자(`GameManager.LogMsg`)와 발신자가 같은 `ViewID`여야 하는데, 이 계약은 씬
인스펙터에서 "GameManager와 같은 오브젝트의 PhotonView"를 수동 연결하는 것에만 존재한다. 현재
두 씬 모두 올바르나 코드 차원 안전장치 없음.

### 6.12 [정보] v3.7이 인정한 의도적 단순화 (버그 아님, 미완성 항목)
파괴/타격/가마솥 VFX·SFX 전무(`breakVfxPrefab` 등 훅만), 가마솥·문 Primitive 플레이스홀더
(`솥단지.glb` 미임포트), 리빌 연출 = 텍스트 배너, 결과화면 = 단색 패널, 밸런스 값 전부 가정값
(`MinStrokesToRegister=15`, 가마솥 스케일 6배, GrabKill 트리거 3m 등).

### 6.13 [경미] `MonsterController.LateUpdate`가 오타 상태명 `"GrapKill"`에 문자열 의존
Animator 상태명을 `GrabKill`로 정정하면 이 감지가 조용히 깨진다(`manage_animation`에 상태 rename
액션이 없어 그대로 뒀다고 코드 주석에 명시). 트리거 파라미터와 상태명 표기가 갈린 상태.

### 6.14 [경미] `HideOrSeekPlayer.Mesh_0`만 Cookie 레이어(9), 루트는 Default(0)
`PlayerGrabController.cookieLayer` / `PlayerPaintCanvas.paintRaycastMask` 등이 레이어에
의존하는데 캐릭터 계층의 레이어가 혼재. 모델/프리팹 구조 변경 시 함께 갱신해야 하는 취약점.

---

## 7. 아키텍처 체크리스트 (11개 관점)

### 7.1 책임 분리를 무시하는 코드 — **대체로 준수**
`GameManager`(채팅)/`PlayerSpawner`(스폰)/`RoomExitController`(나가기) 3분할, `Unit/`은 이동·접지·
애니메이션·네트워크·표시·그랩·스킨으로 세분, `ColorTag/`는 SO/정적로직/마스터진행/클라표현,
`Monster/`는 선정/캐릭터/종료판정/관전/결과로 일관 분리. 경미한 위반: `SpectatorController`가
`Camera_Ctrl.enabled`를 직접 끄고, `PlayerCrackDisplay`가 `bodyRenderer.enabled`를 직접 끈다
(캡슐화 경계 없음, `[RequireComponent]` 없이 인스펙터 연결에 의존).

### 7.2 Manager 간 의존성 과다 — **아니다, 여전히 결합은 최소**
Manager가 다른 Manager를 `[SerializeField]`로 직접 참조하는 경우는 거의 없고, 대부분 Photon
CustomProperties/RaiseEvent로만 소통. `MonsterController→MonsterGrabKillTrigger`,
`MonsterGrabKillTrigger→MonsterController`는 같은 프리팹 내부 상호 참조(정상). 클라 표현
컴포넌트들이 `FindObjectsByType<PlayerPaintCanvas>`로 로컬 캔버스를 매번 재탐색하는 패턴이
5곳(`BrushCursorController`/`ColorSwatchButton`/`PaintToolButton`/`ColorSelectionPanel` + `GameManager`)
— 런타임 스폰 캐릭터라 인스펙터 연결 불가라는 정당한 이유.

### 7.3 Prefab과 Script 역할 혼재 — **경미**
`HideOrSeekPlayer.prefab`이 `Unit`(7)과 `ColorTag`(2) 두 도메인 컴포넌트를 물리적으로 함께
갖는다(코드 레벨에선 두 도메인이 서로 참조하지 않음). 관전자/변형 프리팹이 필요해지면 걸림돌
가능. `transform.Find("Mesh_0")`(HideOrSeekPlayer), `GameObject.Find("PlayerSpawnPos"/"MonsterSpawnPos")`
(`PlayerSpawner`/`MonsterJoinController`/`MonsterTestSpawner`/`HideOrSeekPlayer.RespawnToSpawnPoint`)
등 문자열 탐색이 씬 구조와 암묵 계약.

### 7.4 Scene 직접 의존 — **깨끗함**
씬 이름 하드코딩은 `SceneNames.*` 상수 경유만. `SceneManager.LoadScene`/`PhotonNetwork.LoadLevel`
전부 상수 사용. (§6.11의 인스펙터 배선 암묵 계약은 다른 성격.)

### 7.5 Singleton 남발 — **아니다, 1개(죽은 코드)**
`GameManager.Inst` 하나뿐이고 읽는 곳 0건. 나머지는 씬 배치 컴포넌트거나 `FindObjectsByType`
그때그때 탐색.

### 7.6 ScriptableObject 오용 — **없음**
`ColorPaletteSO`/`BrushSettingsSO` 둘 다 읽기 전용 데이터. 런타임 필드 변경/이벤트 버스 사용
없음. §6.9의 방어 코드 부재만.

### 7.7 Unity Lifecycle 순서 — **여러 건, 모두 방어 처리됨**
- `HideOrSeekPlayer.Awake()` — `networkSync` 최우선 생성.
- `PlayerSkinApplier.Awake()` — `PlayerPaintCanvas.Start()`보다 먼저 `sharedMaterial` 교체.
- `MonsterController.Awake()` — FPS 카메라 초기화(단, §6.1로 무효).
- `Camera_Ctrl.InitCamera()`/`Start()` — 동일 `ResetToDefaultView()` 양쪽 호출.
- 마스터 폴링 매니저들(`GamePhaseStarter`/`PaintPhaseController`/`MonsterJoinController`/
  `GameRuleController`/`MonsterAssignmentAuthority`)은 `Update()` 폴링 + `started`/`resolved` 가드
  + 프로퍼티 존재 여부 재확인으로 순서·중복 방어.
- 커스텀 Script Execution Order 없음 — 이 방어 코드들이 실제로 필요.

### 7.8 Event 구독/해제 — **Photon 콜백 깨끗, UI 리스너 미해제(공통)**
`MonoBehaviourPunCallbacks`가 `OnEnable`/`OnDisable` 자동 처리. `AddCallbackTarget`/`RemoveCallbackTarget`
수동 호출 0건(과거 지적된 이중 등록 문제 계속 해소 상태). `IOnEventCallback` 구현
(`PlayerPaintCanvas`/`MonsterAssignmentAuthority`)도 `MonoBehaviourPunCallbacks` 경유. C# `+=`/`-=`
순수 이벤트는 `PlayerPaintCanvas.OnSlotsChanged`/`OnSlotRejected` 정의만 있고 구독자는 씬 UI에서
연결(코드상 미검증). UI 버튼 리스너는 §6.10.

### 7.9 Object Pool vs Instantiate/Destroy — **풀 없음, 충돌 없음**
프로젝트 실제 코드에 오브젝트 풀 없음(`Player.md`의 `EnemyObjectPool` 등은 §9의 참고 코드).
`Instantiate`/`Destroy`: `LobbyController`/`GameLobbyController`(목록 diff/재생성),
`BrushCursorController`(커서 1회 후 `SetActive` 재사용), `PlayerSpawner`/`MonsterJoinController`/
`MonsterTestSpawner`(`PhotonNetwork.Instantiate`), `ResultScreenController`(결과 행),
`PlayerCrackDisplay`(VFX, 미연결). `PlayerPaintCanvas`의 `RenderTexture`+`bakedColliderMesh`는
캐릭터당 1회 생성·`OnDestroy` 확실히 해제.

### 7.10 Photon Ownership/RPC 구조 — **가장 성숙한 영역**
- `pv.IsMine` 게이팅 정확(`HideOrSeekPlayer`/`MonsterController` Update/FixedUpdate,
  `PlayerPaintCanvas.Update`, `MonsterGrabKillTrigger.OnTriggerEnter`, 관전/그랩 전부).
- 마스터 권위: 6개 매니저 전부 `IsMasterClient` 가드.
- 상태 vs 순간 이벤트 구분 일관(§1).
- 수신부는 송신측 계산값 그대로 재생.
- `RequestGrabKill`이 `RpcTarget.All`이지만 `hitCount` 확정은 `pv.IsMine`만 — 소유권 원칙 유지.
- 흠: §6.11 `RoomExitController.pv` 씬 배선 계약, §6.5 프로퍼티 미정리.

### 7.11 중복 로직 — **의도적 중복 + 경미한 잔여**
- `PlayerNetworkSync` ↔ `MonsterNetworkSync`: 완전 동일 구조, `Unit/`↔`Monster/` 도메인 격리
  위한 의도적 중복(주석 명시).
- "InRoom 대기 코루틴"이 `GameManager`/`PlayerSpawner`/`MonsterTestSpawner` 3곳(3줄 내외).
- `FindLocalPaintCanvas()`가 `BrushCursorController`/`ColorSwatchButton`/`PaintToolButton`/
  `ColorSelectionPanel` 4곳에 거의 동일 복붙(각 5줄) — `RoomState` 같은 헬퍼로 통합 여지.
- `PlayerBillBoard.LateUpdate`의 "카메라 forward 정렬"(3줄).
- `ChangeState()` 패턴이 `PlayerAnimationDriver`와 `MonsterController`에 각각 존재(타입만 다름).

---

## 8. 종합 결론 및 다음 단계 (우선순위순)

이전 4차례 조사(`16c662b`→`1280dac`→`d0fdf2a`→`11f0a9d`) 동안 "코드 아키텍처는 사실상 변화
없음, 남은 건 배선 공백"이었으나, **이번 구간(`11f0a9d`→`403f4b6`)에서 `GameRule.md`가 설계했던
새 게임 룰 전체가 실제 코드·프리팹·씬으로 옮겨졌다.** 이전 보고서의 최우선 항목(§4.1 ColorTag
시작 트리거 부재, §4.3 `GameEndTime` 미기록, §4.2 `ResetAllVotes`, §4.11 몬스터 미배선)은
전부 해소되거나 폐기된 시스템의 것이 됐다. 대신 새 구현에서 **"돌아가지만 아직 안 이어진"
지점들**이 드러났다.

1. **[최우선] 괴물 카메라 배선** (§6.1) — `MonsterFirstPersonCamera`를 `GameScene`·
   `PlayerTestScene` Main Camera에 부착 + 괴물일 때 `Camera_Ctrl` 비활성화 스위칭. 이게 없으면
   괴물 플레이가 성립하지 않는다. `Cursor.lockState`(§6.2)도 함께.

2. **[높음] 재게임 프로퍼티 정리 루틴** (§6.5) — `LoadLevel(GameLobby)` 직전에 게임 Room/Player
   프로퍼티를 비우는 마스터 루틴. 없으면 2번째 게임이 시작 즉시 "괴물 승"으로 끝나거나 색칠
   페이즈가 스킵된다.

3. **[중간] `Held`/`Broken` 애니메이션 상태·전이 추가** (§6.3), `MonsterTentacleDash.obstructionMask`
   실측/설정 (§6.4).

4. **[중간] 인원수 유연화** (§6.7) — 2~3인 플레이를 지원하려면 `MaxPlayers`와 시작 조건을
   재설계. 또는 "2~4인" 표방을 "4인 고정"으로 문서 정정.

5. **[중간] 괴물 선정 타임아웃 기준 재검토** (§6.6).

6. **[정리] 죽은 코드 제거** (§6.8) — 구 `NetKeys`/`NetEventCodes.FillAll`/`GameManager.Inst`/
   `RoomState.GetRoundIndex`. `MonsterAnimator` 상태명 `GrapKill`→`GrabKill` 정정 시 §6.13 동반 수정.

7. **[비주얼] 미완성 항목** (§6.12) — 가마솥/문 모델 임포트(`솥단지.glb`), VFX/SFX 연결,
   안개(§4 표), 리빌·결과화면 아트. `GameRule.md` §10/§14가 목록화해둠.

8. **[선택, 지속] `ColorPaletteSO` 범위 검사, UI 리스너 해제, `RoomExitController.pv` 코드화**
   (§6.9~6.11).

**전체적으로**: 프로젝트는 이제 "설계 문서"에서 "동작하는 수직 슬라이스"로 넘어왔다. 도메인
분리(7.1), Scene 의존 관리(7.4), Singleton 절제(7.5), Photon 콜백 구독(7.8), Ownership/RPC
구조(7.10) 다섯 관점은 새 코드 3만 줄이 추가된 뒤에도 구조적 문제가 없다. 남은 일은 대부분
**아키텍처 결함이 아니라 "마지막 배선 한두 개"(§6.1, §6.2)와 "세션 간 상태 정리"(§6.5)** 이고,
그다음이 비주얼·밸런스다. 다음 조사는 §6.1/§6.5가 해결된 뒤 실제 4인 Play Mode 세션을 1회
완주해보고 그 시점의 런타임 동작을 기준으로 §7 체크리스트를 재검증하는 것이 유효하다.

---

## 9. 부록 — `Assets/02. Scripts/Player.md`의 정체

`.cs`가 아니라 `.md`이며, 이 프로젝트가 아니라 **다른(이전) 3인칭 슈팅 게임의 `Player.cs` 전문 +
분석 메모**다(`using UnityEngine.InputSystem`, 무기 배열/수류탄/상점/코인·탄약·체력 아이템/적
스폰). `EnemyObjectPool`/`ItemObjectPool` 등 §7.9에서 언급한 오브젝트 풀도 전부 이 참고 코드에만
등장한다. 하단 메모의 결론은 "이 코드를 그대로 가져다 쓰지 말라" — 접지 판정이 `OnCollisionEnter`
1회성이라 낭떠러지 낙하 사각지대가 있고(우리 `PlayerGroundDetector`가 해결한 문제),
`Walking()`의 `Time.deltaTime` 이중 적용은 옮기면 새 버그가 된다는 지적. 이 프로젝트의 `Unit/`
도메인은 이 참고 코드와 독립적으로 설계됐다.

---

## 10. 부록 — Plan.md 폴더 문서 지도

| 문서 | 상태 | 내용 |
|---|---|---|
| `CLAUDE.md` | 규칙 | 폴더 규칙, "주석 외 한글 금지 / OOP / 계획 후 승인 / 최적화". (주요 시스템 표는 다른 프로젝트 잔재) |
| `UserPlan.md` | 원본 요구사항 | 구 4라운드 색상 투표 룰(현재 폐기됨) |
| `RoomItemPlan.md` | ✅ 완료 | 로비/대기방 시스템 |
| `PlayerControllPlan.md` | ✅ 완료 | `Hero_Ctrl`→`HideOrSeekPlayer` 이동 리팩토링 |
| `GameScenePlan.md` | ✅ 완료(구 시스템) | 구 색상 결정·술래 지정. 현재는 `GameRule.md`로 대체 |
| `GameManager.md` | ✅ 완료 | `GameManager.cs` 씬 재사용 배선 |
| `Bug-fix-plan.md` | ✅ 완료 | 버그 8건(미끄러짐/가시성/점프 애니메이션/붓 안보임/자기충돌 등) — 코드 주석이 `§NN`으로 참조 |
| `architecture-review.md` | 구버전 | 2026-08-15 스냅샷. 이 문서(research.md)가 대체 |
| `architecture-review-plan.md` | ✅ 완료 | 위 리뷰 5개 항목 수정(`SceneNames`/`RoomState`/`PlayerSpawner`/`RoomExitController` 신규) |
| `GameRule.md` | **v3.7, 부분 구현 완료** | 현재 게임 룰의 정본. §0~§14 + 개정 이력. 이 조사와 대조 시 §4 표 참고 |
| `research.md` | **이 문서** | 전체 동작 + 아키텍처 체크리스트 |
