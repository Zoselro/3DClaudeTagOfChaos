# 계획: research.md(2026-09-25, 커밋 `b134164`) 발견사항 조치 계획

> 상태: **제안됨 — 미구현**. `research.md` §6/§8이 정리한 발견사항을 실행 순서로 재구성한
> 계획이다. `CLAUDE.md`의 "계획부터 말하고 승인 받은 후에 작업 진행" 원칙에 따라, 아래 항목 중
> 실제 코드/씬/에셋 변경을 진행하기 전에 사용자 확인을 받는다. 항목 번호는 `research.md` §6의
> 발견 번호를 그대로 따른다.

---

## 0. 우선순위 요약

| 순위 | 항목 | 근거 | 종류 |
|---|---|---|---|
| 1 | 괴물 1인칭 카메라 배선 | §6.1 | 씬 배선(버그) |
| 2 | `Cursor.lockState` 설정 | §6.2 | 코드(버그) |
| 3 | 재게임 시 Room/Player 프로퍼티 정리 루틴 | §6.5 | 코드(버그) |
| 4 | `Held`/`Broken` 애니메이션 상태·전이 추가 | §6.3 | 씬/애니메이터(버그) |
| 5 | `MonsterTentacleDash.obstructionMask` 설정 | §6.4 | 씬 배선(버그) |
| 6 | `PlayerMonster.prefab` 처리 방침 확정 | §6.15 | 자산 정리(신규) |
| 7 | 2~4인 유연화 또는 "4인 고정" 문서 정정 | §6.7 | 설계 판단 |
| 8 | 괴물 선정 타임아웃 기준 재검토 | §6.6 | 밸런스 |
| 9 | 죽은 코드 정리 + `GrapKill`→`GrabKill` 상태명 정정 | §6.8, §6.13 | 정리 |
| 10 | 비주얼/VFX/SFX 미완성 항목 | §6.12 | 컨텐츠(범위 밖) |
| 11 | 지속 경미 항목(팔레트 범위 검사, UI 리스너 해제, `RoomExitController.pv` 코드화) | §6.9~6.11 | 정리 |

1~5는 "게임이 실제로 플레이 가능한가"에 직결되는 버그이므로 가장 먼저 처리를 권장한다. 6은
지금 당장 동작에 영향은 없지만 방치 기간이 길수록 위험이 커지는 자산 정리 항목이라 앞쪽에
둔다. 7~11은 설계 판단이 필요하거나 급하지 않은 정리 항목이다.

---

## 1. [최우선] 괴물 1인칭 카메라 배선 (`research.md` §6.1)

**증상**: `MonsterController.Awake()`가 `Camera.main.GetComponent<MonsterFirstPersonCamera>()`를
찾지만, 이 컴포넌트는 `GameScene`/`PlayerTestScene` 어느 Main Camera에도 부착돼 있지 않다
(`grep` 재확인, 참조 0건). 괴물 플레이어는 카메라가 씬 기본 위치에 고정된 채 시작한다.

**제안 조치**:
1. `GameScene.unity`와 `PlayerTestScene.unity`의 `Main Camera`에 `MonsterFirstPersonCamera`
   컴포넌트를 추가한다(현재 `Camera_Ctrl`과 같은 오브젝트에 공존).
2. 괴물/쿠키 전환 시점에 두 카메라 컴포넌트가 서로 충돌하지 않도록 스위칭 로직이 필요하다 —
   `SpectatorController`가 이미 `Camera_Ctrl.enabled = false`로 전환하는 패턴을 쓰고 있으므로,
   같은 방식으로 "괴물로 확정되면 `Camera_Ctrl.enabled = false`, `MonsterFirstPersonCamera`만
   활성" 처리를 어디에 둘지 결정해야 한다(`MonsterController.Awake()` 자체에서 처리하는 안,
   또는 `MonsterJoinController` 쪽에서 처리하는 안 — 후자가 "괴물 합류"라는 책임과 더 맞음).
3. 커서 잠금(§2)도 같은 시점에 함께 처리하는 것이 자연스럽다.

**확인 필요**: 카메라 스위칭 책임을 `MonsterController`와 `MonsterJoinController` 중 어디에
둘지 사용자 판단이 필요 — 진행 전 확인 요청.

---

## 2. [높음] `Cursor.lockState` 설정 추가 (`research.md` §6.2)

**증상**: 괴물 1인칭 마우스 룩과 쿠키 우클릭 드래그 회전 모두 커서 잠금이 없어, 마우스가 창
밖으로 나가면 시점이 멈추고 ColorTag 클릭 입력과 섞인다.

**제안 조치**: 괴물 캐릭터가 로컬로 스폰되는 시점(`MonsterController.Awake()`, `pv.IsMine`
분기 안)에 `Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;`를 설정하고,
`BrushCursorController.OnDisable()`이 이미 `Cursor.visible = true`로 되돌리는 것처럼 괴물이
관전 모드로 전환되거나 게임이 끝날 때 해제하는 지점도 함께 정해야 한다.

---

## 3. [높음] 재게임 시 Room/Player CustomProperties 정리 루틴 (`research.md` §6.5)

**증상**: `ResultScreenController`/`RoomLifecycleWatcher`가 `GameLobbyScene`으로 복귀할 때
`MonsterActorNumbers`/`MonsterJoined`/`PaintPhaseEndTime`/`GameEndTime`/`GameResult`/
`ForcedPaint*`(Room)와 `HitCount`/`RegisteredSlotCount`(Player)를 비우지 않는다. 두 번째 게임은
잔존값 때문에 시작하자마자 괴물 승리로 끝나거나 색칠 페이즈가 스킵된다.

**제안 조치**:
1. 마스터 전용 정리 메서드를 하나 만들어(예: `GameRuleController` 또는 신규 `Monster/
   GameSessionReset.cs`) `LoadLevel(GameLobby)` 직전에 위 Room 키들을 전부 `null`로
   `SetCustomProperties`.
2. 각 클라이언트가 자기 `HitCount`/`RegisteredSlotCount`를 리셋하는 것은 로컬 플레이어 프로퍼티라
   각자 처리해야 한다 — `HideOrSeekPlayer`/`PlayerPaintCanvas`가 씬 전환(`OnLeftRoom` 또는 다음
   씬 `OnEnable`) 시점에 자기 자신을 리셋하도록 추가.
3. 어느 클래스에 이 책임을 둘지(신규 클래스 vs 기존 `RoomLifecycleWatcher`/`GameRuleController`
   확장)는 §7.2(Manager 간 결합)의 "매니저는 직접 참조하지 않고 CustomProperties로만 소통"
   원칙을 지키는 선에서 결정 필요 — 진행 전 확인 요청.

---

## 4. [중간] `Held`/`Broken` 애니메이션 상태·전이 추가 (`research.md` §6.3)

**증상**: `PlayerAnimator.controller`에 `Held`/`Broken` 트리거 파라미터는 있지만 대응 상태와
전이가 없어, 그랩당하거나 파괴돼도 캐릭터가 이전 포즈 그대로 멈춰 있다.

**제안 조치**: Unity 애니메이터 에디터(또는 `manage_animation` MCP 도구)로 `PlayerAnimator.
controller`에 `Held`/`Broken` 상태를 추가하고, `Any State → Held`/`Any State → Broken` 전이를
`Held`/`Broken` 트리거 조건으로 연결한다. 애니메이션 클립이 아직 없다면(단순 정지 포즈만 필요한
경우) 기존 `Idle` 클립을 임시로 재사용하는 것도 가능 — 클립 유무를 먼저 확인 필요.

---

## 5. [중간, 확정] `MonsterTentacleDash.obstructionMask`를 지형 레이어로 설정 (`research.md` §6.4)

**증상**: `MonsterPlayer.prefab`의 `MonsterController.obstructionMask.m_Bits: 0`(Nothing)이라
`Physics.SphereCast`가 아무것도 맞히지 못해 돌진이 항상 최대 20m 전진, 벽을 관통한다.

**제안 조치**: Unity 인스펙터(또는 `manage_prefabs`)에서 `MonsterPlayer.prefab`의
`MonsterController.obstructionMask`를 지형/벽 레이어(예: `Default` + 맵 지오메트리가 속한
레이어)로 설정. 프로젝트 레이어 목록(8=`PlayerCapsule`, 9=`Cookie`, 10=`Monster`)과 맵 지오메트리
레이어를 먼저 확인 필요.

---

## 6. [신규, 자산 정리] `PlayerMonster.prefab` 처리 방침 확정 (`research.md` §6.15)

**증상**: 커밋 `b134164`가 새 리깅 모델 위에 게임플레이 스크립트를 얹은 `PlayerMonster.prefab`을
`Resources/`에 추가했으나, `pv`/`eyeSocket`/`Animator.controller`가 비어 있어 그대로
스폰하면 `NullReferenceException`이 난다. 코드가 아직 이 이름을 참조하지 않아 당장은 안전하지만
방치 시 "몬스터 프리팹이 2개, 이름만 반전" 상태가 지속된다.

**두 가지 선택지 — 사용자 확인 필요**:
- **(A) 이 프리팹이 `MonsterPlayer.prefab`을 새 모델로 교체하려는 진행 중 작업이다** → 배선을
  마저 채우고(`pv`/`eyeSocket`/`Animator.controller`/콜라이더 크기), `MonsterJoinController.
  MonsterPrefabName`/`MonsterTestSpawner.MonsterPrefabName` 상수를 이 프리팹 이름으로 교체한 뒤
  기존 `MonsterPlayer.prefab`을 삭제.
- **(B) 아직 착수 전인 실험용 산출물이다** → `Assets/04. Prefabs/Resources/` 밖(예:
  `Assets/04. Prefabs/WIP/`)으로 옮겨 `Resources.Load` 대상에서 제외, 완성되기 전까지 오배치
  위험을 없앤다.

어느 쪽인지는 코드로 판단할 수 없는 사용자의 작업 의도이므로, 진행 전 확인 요청.

---

## 7. [중간, 설계 판단] 2~4인 유연화 여부 (`research.md` §6.7)

`GameRule.md`/`UserPlan.md`는 "2~4인"을 표방하지만 실제로는 `MaxPlayers=4` + "정원일 때만 시작"
조건이라 정확히 4명이어야 시작 가능하다.

**선택지 — 사용자 확인 필요**:
- **(A)** `GameLobbyController.RefreshStartButton`/`OnStartGameButtonClicked`의 시작 조건을
  "정원 도달"에서 "최소 인원(예: 2명) 이상"으로 완화.
- **(B)** 설계 의도가 "4인 고정"이라면 `GameRule.md`/`UserPlan.md`의 "2~4인" 표기를 정정.

---

## 8. [중간] 괴물 선정 타임아웃 기준 재검토 (`research.md` §6.6)

**증상**: `MonsterAssignmentAuthority`의 30초 타임아웃이 "마스터가 `GameLobbyScene`에 들어온
시점"부터 시작돼, 4명이 다 모이기 전에도 랜덤으로 괴물이 확정될 수 있다.

**제안 조치**: 타임아웃 시작 기준을 "정원(4명) 도달 시점"으로 바꾸거나, 타임아웃 값을 늘리는 것
중 사용자 선호 확인 필요.

---

## 9. [정리] 죽은 코드 제거 + `GrapKill`→`GrabKill` 상태명 정정 (`research.md` §6.8, §6.13)

**대상**: `NetKeys`의 구 시스템 잔재 키(`RoundIndex`/`RoundEndTime`/`ColorPrefix`/
`TaggerActorNumber`/`TaggerVariantSet`/`VoteColorIndex`/구`GameEndTime`/`CookiesDeparted`),
`NetEventCodes.FillAll`, `GameManager.Inst`(읽는 곳 0건), `RoomState.GetRoundIndex()`(호출부
없음). `MonsterAnimator.controller`의 상태명 `GrapKill`을 `GrabKill`로 정정(트리거 파라미터와
표기 통일) — 정정 시 `MonsterController.LateUpdate()`의 `state.IsName("GrapKill")` 문자열도
함께 수정해야 한다(코드-애니메이터 동시 변경).

**주의**: 정리 범위(어디까지 지울지)는 "지금 당장 쓰이지 않지만 나중을 위해 남겨둔 것"인지
확인이 필요할 수 있음(예: `MonsterActorNumbers`가 배열인 것은 "다중 괴물 확장 대비"로 의도적).

---

## 10. [비주얼, 범위 밖] 미완성 항목 (`research.md` §6.12)

가마솥/문 3D 모델(`솥단지.glb` 미임포트), VFX/SFX 전무, 리빌/결과화면 아트는 `GameRule.md`
§10/§14가 이미 "의도적으로 나중으로 미룸"이라 명시한 항목이라 이번 계획에 포함하지 않는다.
착수 시점은 별도로 요청 필요.

---

## 11. [선택, 지속] 경미 항목 (`research.md` §6.9~6.11)

- `ColorPaletteSO.GetColor()`/`GetColorName()` 범위 검사 추가(인덱스 방어).
- UI 버튼 `AddListener`에 대응하는 해제 코드 — 현재는 오브젝트 수명과 함께 소멸해 안전하지만,
  프로젝트 공통 컨벤션으로 굳힐지 여부.
- `RoomExitController.pv` 씬 배선 암묵 계약을 코드 레벨 안전장치(예: `[RequireComponent
  (typeof(PhotonView))]` + `GetComponent` 자동 연결)로 바꿀지.

우선순위가 낮아 1~9 처리 후 여유가 있을 때 진행을 권장.

---

## 12. 진행 방식

`CLAUDE.md` 규칙에 따라, 위 항목 중 실제로 착수할 것을 사용자가 선택하면 그 항목만 별도로
상세 설계(파일별 변경 스니펫, 씬 작업 체크리스트, 검증 계획)를 다시 작성해 승인받은 뒤
진행한다 — `architecture-review-plan.md`가 5개 항목에 대해 했던 것과 동일한 형식.
