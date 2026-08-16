# 조사 보고서: ColorTag 폴더 심층 분석 + 아키텍처 체크리스트 (2026-08-16)

> 이 문서는 이전 버전(2026-08-15 작성, `Plan.md/research.md` + `Plan.md/architecture-review.md`
> 두 문서로 분리돼 있었음)을 대체한다. 이번 조사는 **`Assets/02. Scripts/ColorTag/` 폴더 15개
> `.cs` 파일 + 셰이더 3종 + 머티리얼 4종 전체를 라인 단위로 다시 읽고**, 프로젝트 전체(`Assets/02.
> Scripts/` 나머지 6개 도메인, `Assets/Scenes/*.unity`, `Plan.md/Bug-fix-plan.md`)와 대조해 지금
> 이 순간의 실제 동작 방식과 사용자가 지정한 11개 아키텍처 관점을 함께 검증한 결과다. 2026-08-15
> 이후 커밋(`495e9fa`~`4268d13`, "PhotonNetwork.Instantiate 안되는 문제", "카메라 뒷모습만 보임",
> "페인트 붓이 안되는 버그", "점프 Time 버그", "붓 오브젝트 안나오는 오류")에서 다수의 구조적 개선이
> 있었으며, 이번 문서는 그 변화를 반영한 최신 스냅샷이다.

---

## 프로젝트 개요 (다음 세션이 이 문서만 읽어도 되도록)

**TagOfChaos**는 Unity(Built-in RP) + **Photon PUN2** 기반 2~4인 실시간 멀티플레이어 게임이다.
로비에서 방을 만들고 → 대기방에서 인원을 채우고 → 게임 씬에서 **4라운드 동안 팀 전체가 색을
투표·페인팅으로 정하고** → 그중 1명이 **술래**로 뽑혀 미묘하게 다른 색 조합을 부여받는 숨은
식별 메커니즘의 술래잡기형 게임을 지향한다.

**씬 구성(Build Settings)**: `LobbyScene`(방 목록/생성) → `GameLobbyScene`(대기방, 방장이 "게임
시작" 클릭) → `GameScene`(실제 플레이). `PlayerTestScene`은 Build Settings에 비활성화된 개발용
씬으로, ColorTag 미니게임을 수동으로 부트스트랩해 확인할 수 있는 **유일한** 씬이다(§3.1 참고).

**스크립트 도메인**(`Assets/02. Scripts/` 아래 6개, 총 27개 `.cs`):
| 도메인 | 역할 |
|---|---|
| `Lobby/` | 방 목록·생성·입장, 대기방 인원 표시 및 게임 시작 버튼 |
| `GameManager/` | 채팅 중계(RPC), 캐릭터 스폰, 나가기 확인창 |
| `Unit/` | 이동·점프·회피·애니메이션·네트워크 동기화(전투/HP 없음, 5파일로 책임 분리) |
| `Camera/` | 3인칭 추적 카메라(우클릭 드래그 회전만, 줌 없음) |
| `ColorTag/` | **이 문서의 핵심 대상** — 4라운드 색상 투표→페인팅→술래 색 치환 미니게임(§1) |
| `Dev/` | 오프라인 개발 부트스트랩(`OfflineModeBootstrap`) |

이 문서는 `ColorTag/` 도메인을 라인 단위로 깊이 분석하고(§1), 사용자가 지정한 11가지 아키텍처
관점을 프로젝트 전체 기준으로 검증한다(§4). 다른 도메인의 세부 구현까지 필요하다면 코드를 직접
읽거나 `Plan.md/`의 개별 설계 문서(`RoomItemPlan.md`=Lobby, `GameManager.md`=GameManager,
`PlayerControllPlan.md`=Unit, `GameScenePlan.md`=ColorTag 설계 원본)를 참고할 것.

**같은 폴더의 다른 문서와의 관계**: `Plan.md/architecture-review.md`(2026-08-15)는 이 문서가
작성되기 전 버전이며, 이 문서(§2/§4)가 그 내용을 최신 상태로 갱신·대체했다. 최신 정보는 항상 이
문서를 우선한다.

---

## 0. 핵심 요약

**좋은 소식**: 2026-08-15 조사에서 지적됐던 구조적 문제 중 **가장 심각했던 두 가지가 실제로
고쳐졌다** — ① Photon 콜백 이중 등록(7개 파일, `AddCallbackTarget` 중복 호출)이 프로젝트 전체에서
0건으로 제거됐고, ② `PlayerPaintCanvas`의 `RenderTexture`가 이제 `OnDestroy()`에서 정상
`Release()`된다. 또한 이전에 "도입을 권장"했던 `RoomState` 공용 헬퍼가 실제로 `ColorTag/RoomState.cs`
로 구현되어 4개 파일의 중복 조회 로직을 흡수했다.

**여전히 남아있는 것**: `GameScene.unity`에 ColorTag 미니게임을 시작시키는 배선이 없다는 점(가장
치명적인 통합 공백), `ColorSelectionManager.ResetAllVotes()`가 마스터 자신의 투표만 리셋하는 버그,
`NetKeys.GameEndTime`을 세팅하는 코드가 프로젝트 어디에도 없다는 점은 그대로다.

**새로 확인된 것**: `Bug-fix-plan.md`는 8개 버그(Back 버튼, 채팅 중 이동, 랜덤 입장, 가시성, 카메라,
미끄러짐, 점프 애니메이션, 붓 커서 미표시)를 전부 "✅ 구현·검증 완료"로 표기하고 있으나, 가장 최근
커밋(`4268d13`, 2026-08-16)의 메시지는 "점프애니메이션은 아직 해결 못함"이라고 명시한다 — 문서상
"완료"와 실제 커밋 메시지가 상충하므로 §5에서 별도로 짚는다.

---

## 1. `ColorTag/` 폴더가 실제로 어떻게 동작하는가

Photon **PUN2** 기반(Fusion 아님 — `MonoBehaviourPunCallbacks`, `PhotonNetwork.RaiseEvent`,
`[PunRPC]` 없음, 오직 CustomProperties + 1개의 `RaiseEvent` 코드만 사용). 15개 스크립트는 역할별로
4계층으로 나뉜다.

### 1.1 데이터/설정 계층 (ScriptableObject, 2파일)
- **`BrushSettingsSO`**: 붓 반경 범위(min/max/default)·휠 감도·커서 프리팹. 런타임 변경 없음.
- **`ColorPaletteSO`**: 고정 10색 팔레트(`ColorEntry[]`). 인덱스 범위 검사 없음(호출부가 항상 유효한
  인덱스만 넘긴다고 가정).

### 1.2 순수 로직 계층 (정적 클래스, 4파일, Unity API 미사용 — 유닛 테스트 가능)
- **`NetKeys`**: Room/Player CustomProperties 키 7개(`RoundIndex`, `RoundEndTime`, `Color0~3`,
  `TaggerActorNumber`, `TaggerVariantSet`, `VoteColorIndex`, `GameEndTime`).
- **`NetEventCodes`**: RaiseEvent 코드 1개(`PaintStroke = 1`).
- **`ColorVoteTally.Resolve()`**: 다수결(동점 시 랜덤) + 무투표/전원 제외색일 때 랜덤 폴백.
- **`TaggerColorAssigner`**: `BuildVariantSet()`(확정 4색 중 무작위 1슬롯을 미사용 색으로 치환),
  `FindSwappedSlot()`(두 세트 중 다른 슬롯 탐색).
- **`RoomState`**(신규 확인, §3.1): `IsInRoom()`/`TryGetInt()`/`TryGetDouble()`/`GetRoundIndex()`.
  자체 주석으로 "ColorTag 도메인 전용" 명시, 실제로 도메인 밖에서 쓰이지 않음(재확인).

### 1.3 라운드 진행 계층 (마스터 클라이언트 권위, 2파일)
- **`ColorSelectionManager`**: `Update()`에서 마스터만 `RoundEndTime` 만료를 폴링 → `ResolveRound()`
  → 다수결 확정 → 4라운드 완료 시 `AssignTagger()`(무작위 술래 선정 + 변형 색셋 생성) → 결과를
  Room CustomProperties에 기록. `StartColorSelection()`(공개, 마스터 전용)이 라운드 시스템의
  유일한 진입점.
- **`RoomLifecycleWatcher`**: 술래 퇴장/인원 1명 이하 감지 시 즉시 `LeaveRoom()`(비정상 종료),
  마스터가 `GameEndTime` 경과를 폴링해 정상 종료 시 방을 유지한 채 `GameLobbyScene`으로 복귀.

### 1.4 클라이언트 표현 계층 (플레이어별, 6파일)
- **`PlayerPaintCanvas`**: 캐릭터 1개당 512×512 `RenderTexture`를 런타임 생성, 좌클릭 레이캐스트로
  UV 스탬프 → 로컬 즉시 반영 + `RaiseEvent(PaintStroke, ReceiverGroup.Others)`로 전파. 알파 채널을
  "잠금 마스크"로 사용(`_RespectLock` 셰이더 프로퍼티) — 일반 붓은 이미 확정된 픽셀을 보호하고,
  라운드 종료 시 `FinalizeCurrentRoundStrokes()`가 확정색으로 강제 재도색(`_RespectLock=0`)한다.
- **`BrushCursorController`**: 3D 붓 커서를 1회 인스턴스화해 재사용, 캐릭터 자신의 캡슐 콜라이더를
  레이캐스트에서 제외(`PlayerCapsule` 레이어 마스킹 — `Bug-fix-plan.md` §17에서 확정한 수정).
- **`ColorSelectionPanel`/`ColorSwatchButton`**: 라운드/시간 표시, 이미 확정된 색 잠금, 클릭 시
  `manager.SubmitVote()` 위임.
- **`PlayerColorVoteIndicator`**: 머리 위 스프라이트로 현재 투표색 표시, `LateUpdate()`에서 인디케이터
  트랜스폼만 카메라 방향으로 정렬(`Bug-fix-plan.md` §10/§11에서 "카메라 뒷모습만 보임" 버그를 고친
  방식 — 캐릭터 전체가 아니라 인디케이터만 회전).
- **`PlayerColorDisplay`**: `[RequireComponent(PlayerPaintCanvas)]`. 술래 본인일 때만, 4라운드
  완료 후 자신의 변형 슬롯을 찾아 캔버스 전체에서 옛 색 픽셀을 새 색으로 전역 치환.

### 1.5 셰이더 3종
- **`PaintStamp.shader`**: 원형 스탬프, `_RespectLock`으로 덮어쓰기 여부 분기.
- **`PaintColorReplace.shader`**: 색상 거리 기반 전역 치환(술래 전용), 알파 보존.
- **`PlayerPaintedSkin.shader`**: 베이스 스킨 위에 페인트 텍스처를 알파 마스크로 `lerp` 합성.

전체 데이터 흐름은 `투표(Player 프로퍼티) → 마스터가 다수결 확정(Room 프로퍼티) → 각 클라이언트가
자기 캔버스를 확정색으로 재도색 → 4라운드 후 술래 지정 → 술래 캔버스만 전역 색 치환` 순서로,
설계 문서(`GameScenePlan.md`)와 실제 코드가 정확히 일치함을 재확인했다.

---

## 2. 2026-08-15 대비 변경 사항 (실제로 고쳐진 것)

| 항목 | 2026-08-15 상태 | 2026-08-16 상태 (이번 조사로 확인) |
|---|---|---|
| Photon 콜백 이중 등록 | 7개 파일이 `base.OnEnable()` 뒤에 `PhotonNetwork.AddCallbackTarget(this)`를 추가로 호출해 모든 콜백이 2배 실행 | 프로젝트 전체에 `AddCallbackTarget`/`RemoveCallbackTarget` 호출이 **0건**. 완전히 제거됨 |
| `PlayerPaintCanvas`의 `RenderTexture` 해제 | `OnDestroy()` 오버라이드가 프로젝트 전체 0건 — 씬 전환마다 GPU 메모리 누적 위험 | `PlayerPaintCanvas.cs:206`에 `OnDestroy()`가 있고 `PaintCanvas.Release()` 호출 확인 |
| `RoundIndex` 등 CustomProperties 조회 중복 | 5개 파일이 각자 재구현 | `RoomState` 정적 클래스로 통합, 4개 파일이 이를 사용(`ColorSelectionManager`, `ColorSelectionPanel`, `PlayerPaintCanvas`, `PlayerColorDisplay`) |
| 붓 커서/캔버스 레이캐스트가 자기 캡슐에 막힘 | 미확인(당시 미발견) | `PlayerCapsule` 전용 레이어 분리로 해결(`Bug-fix-plan.md` §17, 49/49 → 0/49 차단으로 개선) |
| 카메라가 캐릭터 뒷모습만 보임 | 버그로 기록 | `PlayerColorVoteIndicator.LateUpdate()`가 인디케이터 트랜스폼만 회전하도록 수정, 실측 검증 완료 |
| `GameLobbyScene`에서 미끄러짐/버벅거림 | 미확인 | `Ch36` 전용 Rigidbody의 self-collision이 원인으로 확정, `Physics.IgnoreCollision()`으로 해결(§14) |

---

## 3. 여전히 남아있는 문제 (재확인)

### 3.1 [최우선] `GameScene.unity`에 ColorTag 시작 트리거가 없음

`GameLobbyController.OnStartGameButtonClicked()`(`Lobby/GameLobbyController.cs:91-98`)는
`PhotonNetwork.LoadLevel(SceneNames.Game)`만 호출한다. `ColorSelectionManager.StartColorSelection()`
을 호출하는 코드는 프로젝트 전체에서 **`Dev/OfflineModeBootstrap.cs`(개발용, `PlayerTestScene`
전용) 단 한 곳뿐**임을 grep으로 재확인했다. `GameScene.unity`를 직접 검색해도 `ColorSelectionManager`
/`ColorTagManagers`/`StartColorSelection` 문자열이 전혀 없다 — 실제 매칭 플로우로 `GameScene`에
도달해도 색상 선택 라운드가 시작되지 않는다. `HideOrSeekPlayer.prefab`에는 `PlayerPaintCanvas` 등
ColorTag 컴포넌트가 이미 정상 부착돼 있으므로(§1.4), **에셋/코드는 완성돼 있고 "씬 배치 + 호출 한
줄"만 빠진 상태**라는 이전 결론이 여전히 유효하다.

### 3.2 `ColorSelectionManager.ResetAllVotes()`가 마스터 자신의 투표만 리셋

```csharp
private void ResetAllVotes()
{
    if (PhotonNetwork.LocalPlayer != null)
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.VoteColorIndex, -1 } });
}
```
`ResolveRound()` 끝에서 호출되지만 `PhotonNetwork.LocalPlayer`(= 이걸 실행하는 마스터 자신)만
리셋한다. 다른 플레이어의 `VoteColorIndex`는 이전 라운드 값이 그대로 남고, `ColorSwatchButton.
SetLocked()`는 이미 확정된 색만 잠그므로 새 라운드에서 아무것도 다시 누르지 않은 플레이어의 이전
선택이 자동으로 이월된다 — "매 라운드 새로 골라야 한다"가 의도라면 결함.

### 3.3 `NetKeys.GameEndTime`을 세팅하는 코드가 없음

`RoomLifecycleWatcher.Update()`는 마스터가 `GameEndTime`(Room 프로퍼티) 경과를 감지해 정상
종료(→`GameLobbyScene` 복귀)를 트리거하지만, 이 값을 쓰는(set) 코드는 프로젝트 전체에서 발견되지
않았다. 태그/술래잡기 본게임 자체가 아직 구현돼 있지 않으므로(`GameScenePlan.md`가 범위 밖으로
명시), 이 경로는 현재 트리거될 방법이 없다 — §3.1과 함께 "게임 시작→진행→종료" 전체 파이프라인의
양 끝이 비어 있는 상태.

### 3.4 `ColorPaletteSO.GetColor()`/`GetColorName()`에 범위 검사 없음

인덱스가 유효 범위(0~9)를 벗어나면 `IndexOutOfRangeException`을 던진다. 모든 호출부가 항상 유효한
`RoundIndex`/`VoteColorIndex`/팔레트 인덱스만 넘긴다는 암묵적 전제에 의존 — 지금까지는 이 전제가
깨지지 않았지만 방어 코드는 없다.

### 3.5 `ColorSwatchButton`의 `AddListener`에 대응하는 해제 코드 없음 (경미)

`Awake()`에서 `button.onClick.AddListener(...)`만 있고 `OnDestroy`/`OnDisable`이 없다. `Button.
onClick`이 GameObject 수명과 함께 소멸하므로 현재는 안전하지만, 이 버튼이 풀링되거나 재부모화되는
시나리오가 생기면 문제가 될 수 있다.

### 3.6 [해소됨] 점프 애니메이션 문서-커밋 불일치 — 실제로 세 번째 원인이 있었음 (2026-08-16 해결)

이 절이 처음 작성됐을 때는 `Bug-fix-plan.md`의 §16("1차+2차 원인 모두 구현·검증 완료")과 커밋
`4268d13`("점프애니메이션은 아직 해결 못함")이 상충하는 것으로 보였다. 이후 사용자가 직접 재현한
증상("착지 직후 바로 재점프하면 Jump 애니메이션이 중간 Time에서 시작되는 것처럼 보인다")을
조사한 결과, **①의 해석이 맞았다** — §15/§16이 고친 것과는 **별개의 세 번째 원인**이 실제로
남아있었다. `PlayerAnimator.controller`의 `Any State → Jump` 트랜지션이 0.1초 크로스페이드
(`m_TransitionDuration: 0.1`) + `m_CanTransitionToSelf: 1`로 설정돼 있어, 착지로 얼려뒀던 Jump
포즈가 풀리자마자 재점프하면 `SetTrigger`가 즉시 컷되지 않고 옛 포즈와 새 포즈를 블렌딩해버리는
것이 원인이었다. `ReplayJump()`를 `Animator.Play("Jump", 0, 0f)` 기반으로 교체해 구현·Play Mode
실측 검증까지 완료했다(`Bug-fix-plan.md` §18). `Unit/` 도메인 소관 이슈이며 `ColorTag/` 자체의
문제는 아니다.

---

## 4. 아키텍처 체크리스트 (사용자 지정 11개 관점, 현재 코드 기준 재검증)

### 4.1 기존 책임 분리를 무시하는 코드가 있는가 — **경미한 위반 1건**
`PlayerColorDisplay.ApplyColorReplace()`가 `paintCanvas.PaintCanvas`(공개 `RenderTexture` 프로퍼티)를
직접 `Graphics.Blit`한다. `[RequireComponent(PlayerPaintCanvas)]`로 결합 의도는 명확하지만
캡슐화 경계는 없음 — 지금 규모에서는 문제없음. 그 외 `ColorTag/` 내부는 SO(설정) / 정적 로직(순수
함수) / 라운드 진행(마스터 권위) / 클라이언트 표현(플레이어별)의 4계층 분리가 일관되게 지켜진다.

### 4.2 Manager 간 의존성이 과도하게 증가하는가 — **아니다, 오히려 결합 부재가 문제**
`ColorSelectionManager`/`RoomLifecycleWatcher`/`ColorSelectionPanel`/`PlayerPaintCanvas`/
`BrushCursorController`/`PlayerColorVoteIndicator`/`PlayerColorDisplay` 7개는 서로를 `[SerializeField]`
로 직접 참조하지 않고 전부 Photon CustomProperties라는 공유 네트워크 상태를 통해 소통한다. 유일한
직접 매니저 참조는 `ColorSwatchButton.manager`(UI→매니저, 정상 방향)와 `OfflineModeBootstrap→
ColorSelectionManager`(개발용)뿐. 진짜 문제는 결합 과다가 아니라 **`GameLobbyController`가
`ColorSelectionManager`를 전혀 모른다는 결합 부재**(§3.1)다.

### 4.3 Prefab과 Script의 역할이 뒤섞이는가 — **경미, `HideOrSeekPlayer.prefab`의 도메인 융합**
코드 레벨에서 `Unit`과 `ColorTag`는 서로를 참조하지 않는 별개 도메인이지만, `HideOrSeekPlayer.prefab`
하나가 `HideOrSeekPlayer`(Unit) + `PlayerPaintCanvas`/`PlayerColorVoteIndicator`/`PlayerColorDisplay`
(ColorTag)를 물리적으로 함께 갖고 있다. 캐릭터라는 단일 오브젝트가 여러 기능을 갖는 것 자체는
자연스럽지만, 코드 분리와 에셋 결합이 서로 다른 그림이라는 점은 인지해둘 필요가 있다(향후 관전자용/
AI용 변형 프리팹이 필요해지면 걸림돌 가능). `BrushCursorController`가 프리팹 내부를
`GetComponentInChildren<Renderer>()`로 탐색하고 `transform.Find("경로")` 류의 취약한 문자열 탐색은
`ColorTag/` 전체에서 발견되지 않았다 — 양호.

### 4.4 Scene에 직접 의존하는 코드가 늘어나는가 — **`ColorTag/` 자체는 깨끗함**
`RoomLifecycleWatcher`는 `SceneNames.Lobby`/`SceneNames.GameLobby` 상수를 사용(하드코딩 문자열
아님) — 2026-08-15 당시 지적됐던 씬 이름 매직 스트링 문제가 `Core/SceneNames.cs` 도입으로 해소된
것을 재확인했다(`GameLobbyController.cs:97`의 `SceneNames.Game`도 동일). `ColorTag/` 폴더 안에서
씬 이름을 직접 문자열로 다루는 코드는 없다.

### 4.5 Singleton이 남발되는가 — **`ColorTag/` 안에는 없음**
`ColorTag/` 10개 MonoBehaviour 중 `static Instance` 패턴을 쓰는 클래스는 0개 — 전부 씬에 배치된
일반 컴포넌트이며 Photon 상태 공유(§4.2)로만 소통한다. 프로젝트 전체에서 유일한 static 싱글턴은
`GameManager.Inst`(ColorTag 밖)이며, 이를 읽는 코드가 0건이라 사실상 죽은 코드다.

### 4.6 ScriptableObject의 책임이 잘못 사용되는가 — **문제 없음**
`ColorPaletteSO`/`BrushSettingsSO` 둘 다 읽기 전용 데이터 저장소로만 쓰이고, 런타임에 필드를
변경하는 코드는 없다(재확인). SO를 상태 저장소나 이벤트 버스로 쓰는 안티패턴 없음.

### 4.7 Unity Lifecycle 순서 문제가 있는가 — **1건, 방어적으로 처리되어 안전**
`PlayerColorDisplay`(`Awake`에서 `GetComponent<PlayerPaintCanvas>()`)와 `PlayerPaintCanvas`의
`Start()` 실행 순서는 Unity가 보장하지 않지만, `TryApplyTaggerColor()`가 `paintCanvas.PaintCanvas
== null`이면 조용히 리턴하고 `OnRoomPropertiesUpdate` 콜백마다 재시도하는 구조라 순서 무관하게
안전하다. `ColorSelectionManager`(마스터, `Update()` 폴링)와 `PlayerPaintCanvas.DetectRoundChange()`
(각 클라이언트 `Update()` 폴링) 사이의 순서도, 결과가 Photon 서버 왕복 후 반영되므로 같은 프레임의
Script Execution Order에 의존하지 않아 안전하다.

### 4.8 Event 구독/해제가 제대로 되는가 — **개선됨, 경미한 항목 1건만 남음**
- Photon 콜백: `MonoBehaviourPunCallbacks`가 `OnEnable`/`OnDisable`에서 자동으로 `AddCallbackTarget`/
  `RemoveCallbackTarget`을 처리하며, **수동 이중 등록은 프로젝트 전체에서 0건**(§2에서 확인한 개선
  사항). `PlayerPaintCanvas.IOnEventCallback.OnEvent()`도 별도 수동 등록 없이 기반 클래스가 처리.
- C# `+=`/`-=` 순수 이벤트 구독은 `ColorTag/` 안에 전혀 없음.
- `ColorSwatchButton.AddListener()`만 대응 `RemoveListener`가 없음(§3.5, 경미, 실질 위험 낮음).

### 4.9 Object Pool과 Instantiate/Destroy가 충돌하는가 — **풀 자체가 없음, 충돌 없음**
`ColorTag/`에 Object Pool 구현 없음. `BrushCursorController`는 커서를 1회 `Instantiate` 후
`SetActive`로 재사용(사실상 수동 풀링 1개체, 적절). `PlayerPaintCanvas`의 `RenderTexture`는
캐릭터당 1회 생성·해제(§2에서 `OnDestroy()` 추가 확인)로 생명주기가 명확하다. `ApplyStamp()`/
`ApplyColorReplace()`가 스탬프 1회마다 `RenderTexture.GetTemporary`/`Blit`/`Blit`/`ReleaseTemporary`
왕복을 수행하는 것은 페인팅이 잦은 게임 특성상 성능 관점에서 눈여겨볼 지점(치명적이지는 않음).

### 4.10 Photon의 Ownership/RPC 구조를 무시하는가 — **잘 지켜짐, 이 프로젝트에서 가장 성숙한 영역**
- `pv.IsMine` 게이팅이 `PlayerPaintCanvas.Update()`(자기 캐릭터만 입력 처리)에 정확히 적용됨.
- 마스터 전용 권위: `ColorSelectionManager.Update()`/`RoomLifecycleWatcher.Update()` 둘 다
  `PhotonNetwork.IsMasterClient` 가드로 라운드 판정·씬 전환 트리거를 제한 — 클라이언트마다 다른
  `System.Random` 시드를 가져도 안전한 이유.
  ```
  단일 권위자만 계산 → 결과를 CustomProperties로 전파 → 나머지는 그 값을 그대로 신뢰
  ```
- 상태(투표, 라운드) vs 순간 이벤트(붓질)를 CustomProperties vs `RaiseEvent`로 적절히 구분해서 사용.
- `PlayerPaintCanvas.OnEvent()`는 수신 데이터를 재해석하지 않고 송신측이 계산한 `force` 플래그를
  그대로 재생 — 클라이언트 간 판단 불일치 가능성을 원천 차단.
- `pv` 필드는 필요한 클래스(`PlayerPaintCanvas`, `PlayerColorDisplay`, `PlayerColorVoteIndicator`)
  에만 있고, Room 단위 상태만 다루는 클래스(`ColorSelectionManager`, `RoomLifecycleWatcher`,
  `ColorSelectionPanel`)에는 없음 — 불필요한 `PhotonView` 참조 없음.

### 4.11 중복 로직이 있는가 — **크게 개선됨**
2026-08-15 당시 5개 파일에 흩어져 있던 `RoundIndex` 등 CustomProperties 조회 로직이 `RoomState`
정적 헬퍼로 통합됐다(§2). 다만 `RoomState`가 다루지 않는 `int[]`/`object` 타입 프로퍼티
(`TaggerVariantSet`, `Color0~3` 배열 형태 접근)는 여전히 `PlayerColorDisplay`가 `PhotonNetwork.
CurrentRoom.CustomProperties`를 직접 읽는다 — 이는 `RoomState`의 범용 헬퍼(`TryGetInt`/
`TryGetDouble`) 범위를 의도적으로 벗어난 것으로 파일 자체 주석에 명시돼 있어 중복이라기보다는
설계상 경계로 보는 것이 맞다.

---

## 5. 종합 결론과 다음 단계 제안 (우선순위순)

1. **[최우선, 통합 공백]** `GameLobbyController.OnStartGameButtonClicked()` 또는 `GameScene` 진입
   시점에 `ColorSelectionManager.StartColorSelection()` 호출을 추가하고, `PlayerTestScene`의
   `ColorTagManagers`(`ColorSelectionManager`+`RoomLifecycleWatcher`+`BrushCursorController`)와
   `ColorSelectionPanel` UI를 `GameScene.unity`에 옮겨 배치한다(§3.1). 코드/에셋은 이미 완성돼 있어
   "씬 배치 + 호출 한 줄" 수준의 작업.
2. **`ColorSelectionManager.ResetAllVotes()`가 전원을 리셋해야 하는 의도인지 확인 후**
   `PhotonNetwork.PlayerList`를 순회하도록 수정(§3.2).
3. **본게임(태그/술래잡기) 승패 판정 로직 설계 + `GameEndTime` 기록 주체 결정** — `RoomLifecycleWatcher`
   의 정상 종료 경로가 실제로 동작하려면 선행 필요(§3.3).
4. **점프 애니메이션 관련 `Bug-fix-plan.md` 상태와 최근 커밋 메시지의 불일치를 사용자가 직접
   재확인** — 어떤 증상이 실제로 남아있는지 명확히 한 뒤 문서 상태를 갱신(§3.6, `Unit/` 도메인).
5. (선택) `ColorPaletteSO`에 인덱스 범위 검사 추가 여부 검토(§3.4, 지금까지 실제 문제는 없었음).
6. (선택) `ColorSwatchButton`에 `OnDestroy`/`RemoveListener` 추가 — 버튼이 풀링/재부모화될
   계획이 없다면 우선순위 낮음(§3.5).

**전체적으로**: `ColorTag/` 도메인은 이 프로젝트에서 가장 성숙한 부분이다 — Photon Ownership/RPC
구조, Singleton 사용, ScriptableObject 책임, Manager 간 결합도 네 관점 모두 뚜렷한 구조적 문제가
없고, 2026-08-15에 지적됐던 이중 콜백 등록·RenderTexture 누수·중복 조회 로직이 실제로 고쳐졌다.
남은 문제는 전부 "이 도메인 자체의 결함"이 아니라 **이 도메인을 게임 전체 플로우에 연결하는
배선(§3.1/§3.3)**과 한 가지 명확한 버그(§3.2)로 좁혀져 있다.
