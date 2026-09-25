# 계획: 몬스터 재리깅 전환 — `Monster_Rigged_kihong` + `NewAnimation` 세트로 `MonsterPlayer` 교체

> 상태: **✅ 구현 완료 (2026-09-25, A안)**. Unity MCP(`execute_code`/`manage_editor`/
> `manage_components`)로 §1(A안)~§7(검증)까지 전부 수행했다. 실제 적용된 수치·결과는 **§10 구현
> 완료 보고**에 정리했다 — 계획 단계의 추정치(스케일 6, EyeSocket (0,0.2,0.05) 등)와 실제 적용값이
> 다른 항목이 있으니 최종 값은 반드시 §10을 기준으로 볼 것.
>
> 원래 아래는 착수 전 작성한 계획이었다. `Assets/02. Scripts/Monster/`의 실제 소스 코드와
> `Assets/04. Prefabs/Resources/MonsterPlayer.prefab`(현재 실사용 프리팹) YAML을 직접 읽고,
> Unity MCP로 새 리깅 에셋(`Assets/Animation/Monster/NewAnimation/`)의 임포트 설정을 확인해
> 작성한 계획이다. `CLAUDE.md`의 "계획부터 말하고 승인 받은 후 진행" 원칙에 따라 실제 작업 착수
> 전 사용자 확인이 필요한 지점을 §1에 명시했다. 관련 기존 발견: `research.md` §6.15(미완성
> `PlayerMonster.prefab`), §6.4(`obstructionMask` 버그), §6.13(`GrapKill` 오타).

---

## 0. 현재 자산 상태 (직접 확인한 사실)

### 0.1 기존(현역) 구성 — `MonsterPlayer.prefab`

`Assets/04. Prefabs/Resources/MonsterPlayer.prefab`(코드가 `PhotonNetwork.Instantiate`로 실제
스폰하는 프리팹, `MonsterJoinController.cs:14`/`MonsterTestSpawner.cs:10`의
`MonsterPrefabName = "MonsterPlayer"`)의 루트 GameObject 구조를 YAML에서 직접 확인:

| 항목 | 현재 값 |
|---|---|
| 루트 이름/레이어 | `MonsterPlayer`, Layer 10(Monster) |
| 루트 `m_LocalScale` | `{6, 6, 6}` |
| 자식 계층 | `MonsterArmature`(스케일 100×, 커스텀 크리처 본 — `Spine_Root`/`Tentacle.0N.{L,R}`/`UpperArm`/`Forearm`/`Hand`/`Finger`/`FootIK` 등, Humanoid 아님) + `Mesh_0`(SkinnedMeshRenderer) + `EyeSocket`(로컬 `{0, 0.2, 0.05}`, 본이 아닌 순수 빈 자식 오브젝트) |
| `Animator` | Avatar = `Monster_Rigged.fbx` 내장 아바타, Controller = `MonsterAnimator.controller` |
| `Rigidbody` | mass 1, useGravity 1, 나머지 기본값 |
| `CapsuleCollider`(루트) | radius 0.1 / height 0.35 / center (0, 0.15, 0) / direction 1 — **루트 스케일 6배가 곱해져 실제 월드 단위로는 반경 0.6, 높이 2.1**(작아 보이지만 스케일 고려하면 정상 범위) |
| `PhotonView` | `ObservedComponents = [MonsterController]` |
| `MonsterController` | `pv`=자기 PhotonView, `eyeSocket`=`EyeSocket` 자식, `obstructionMask`=**Nothing(버그, `research.md` §6.4)**, `grabKillTrigger`=자기 `MonsterGrabKillTrigger`, `animator`=자기 Animator |
| `MonsterGrabKillTrigger` | `monsterPv`=자기 PhotonView, `monsterController`=자기 MonsterController |
| `SphereCollider`(루트) | trigger, GrabKill 판정용 |

`MonsterAnimator.controller` 파라미터 5개(전부 Trigger): `Idle`/`Walk`/`TentacleDash`/`GrapKill`/
`GrabKill`. 상태는 4개뿐(`Idle`/`Walk`/`TentacleDash`/`GrapKill`—오타 그대로), 각 상태의 클립은:

| 상태 | 클립 소스 (guid로 직접 대조) |
|---|---|
| `Idle` | `Assets/Animation/Monster/Monster_Rigged_Idle.fbx` |
| `Walk` | `Assets/Animation/Monster/Monster_Rigged_Walk.fbx` |
| `TentacleDash` | `Assets/Animation/Monster/Monster_Rigged_TentacleDash.fbx` |
| `GrapKill`(상태명 오타) | `Assets/Animation/Monster/Monster_Rigged_GrabKill.fbx` |

`MonsterController.cs`의 `ChangeState()`는 `animator.SetTrigger(newState.ToString())`으로
`MonsterMoveState` enum(`Idle/Walk/TentacleDash/GrabKill`) 값을 그대로 트리거 이름으로 쓴다 —
**파라미터 이름과 이 enum이 정확히 일치해야 하는 암묵 계약**(`research.md` §2.4와 동일 패턴).

### 0.2 신규 리깅·애니메이션 세트 — `Assets/Animation/Monster/NewAnimation/`

커밋 `b134164`가 추가한 5개 `.fbx`:

| 파일 | 용도(추정, 이름 기준) | Rig 임포트 설정(직접 확인) |
|---|---|---|
| `Monster_Rigged_kihong.fbx` | 새 모델 본체(메시+본) | `animationType: 2`(Generic) — 맞음. **`avatarSetup: 0`(No Avatar) — 미설정**. `materialImportMode: 2`, `materialLocation: 1`(외부/레거시 참조 방식 — 머티리얼이 아직 정식 추출·연결 안 됐을 가능성) |
| `Monster_Manual_Idle.fbx` | Idle 애니메이션 | `animationType: 2`, **`avatarSetup: 0`** |
| `Monster_Manual_Walk.fbx` | Walk 애니메이션 | 〃 |
| `Monster_Manual_TentacleDash.fbx` | TentacleDash 애니메이션 | 〃 |
| `Monster_Manual_GrabKill.fbx` | GrabKill 애니메이션 | 〃 |

**핵심 발견**: 구 세트(`Monster_Rigged.fbx`)는 `avatarSetup: 1`("Create From This Model")로
설정돼 있어 자체 Avatar를 갖고 있었다. 반면 **새 세트 5개는 전부 `avatarSetup: 0`(No Avatar) —
즉 지금 상태로는 이 모델에 애니메이션을 재생시킬 Avatar 자체가 없다.** 실제로 기존
`PlayerMonster.prefab`(`research.md` §6.15, `b134164`가 함께 추가한 미완성 프리팹)의
`Animator.m_Controller`/`m_Avatar`가 모두 `{fileID: 0}`(미할당)인 것과 정확히 맞아떨어진다 —
**이 작업이 중단된 지점이 정확히 여기(Avatar 미생성)로 보인다.**

같은 폴더에 `Monster_Manual.blend`(원본 Blender 파일, 저장소 루트 `괴물FBX/`에 위치)가 함께 있어
5개 `.fbx`가 한 블렌더 파일에서 각 액션별로 분리 내보내기된 것으로 보인다 — 이는 "본체 1개
Avatar 생성 + 애니메이션 파일들은 그 Avatar를 Copy" 하는 표준 멀티-FBX 파이프라인과 정확히
일치하는 구조다.

### 0.3 이미 존재하는 미완성 시도 — `PlayerMonster.prefab`

`Assets/04. Prefabs/Resources/PlayerMonster.prefab`(`research.md` §3/§6.15에서 이미 지적)이
`Monster_Rigged_kihong.fbx` 위에 `Animator`/`Rigidbody`/`CapsuleCollider`/`PhotonView`/
`MonsterController`/`MonsterGrabKillTrigger`/`SphereCollider`를 얹어놓은 상태다. 즉 **이번
작업과 정확히 같은 목적의 시도가 이미 절반쯤 진행돼 있다.** 다만:
- `Animator.m_Controller`/`m_Avatar` 미할당(§0.2의 원인 그대로).
- `MonsterController.pv`/`eyeSocket` 미배선(`{fileID: 0}`) → 지금 스폰하면 `Awake()`의
  `if (!pv.IsMine) return;`에서 즉시 NRE.
- `CapsuleCollider`(radius 0.1 / height 0.35)가 `MonsterPlayer.prefab`과 완전히 같은 수치로
  복사돼 있는데, **루트 `m_LocalScale`을 6으로 맞추는 수정이 없다**(prefab modification에
  `m_LocalPosition`/`m_LocalRotation`만 0으로 오버라이드하고 `m_LocalScale`은 원본 FBX의 기본값
  그대로) — 새 모델의 실제 크기를 모르는 채로 수치만 복붙된 상태로 보인다.
- 코드 어디에서도 `"PlayerMonster"` 문자열을 참조하지 않아(재확인) 지금은 스폰되지 않는다.

---

## 1. 결정 필요 사항 — 최종 프리팹을 어느 파일로 할 것인가 — ✅ A안으로 확정(사용자 확인)

**추천안(A)을 기본값으로 제안하되, 실제 착수 전 확인 요청.**

- **(A, 추천)** 기존 `MonsterPlayer.prefab`을 새 리깅·애니메이션으로 **내용만 교체**한다.
  프리팹 자산 경로/이름이 그대로 유지되므로 `MonsterJoinController.cs`/`MonsterTestSpawner.cs`의
  프리팹 이름 상수(`"MonsterPlayer"`)를 **전혀 건드릴 필요가 없다** — 씬 배선(`PhotonView`
  ObservedComponents 등)도 프리팹 내부 구조만 다시 짜면 되므로 리스크가 가장 낮다. `PlayerMonster
  .prefab`은 시행착오 산출물로 보고 작업 완료 후 삭제.
- **(B)** `PlayerMonster.prefab`을 완성해서 이것을 새 정본으로 삼는다. 이 경우
  `MonsterJoinController.MonsterPrefabName`/`MonsterTestSpawner.MonsterPrefabName` 두 상수를
  `"PlayerMonster"`로 바꾸는 코드 수정이 추가로 필요하고, 기존 `MonsterPlayer.prefab`을 삭제해야
  "이름이 뒤집힌 프리팹 2개" 상태가 해소된다.

두 안 모두 최종적으로 프리팹 내부 구조(§5)는 동일하게 다시 짜야 한다 — 차이는 **어느 파일 이름을
정본으로 남길지, 코드를 건드릴지 여부**뿐이다. 아래 §2~§9는 (A) 기준으로 서술하되, (B)를 선택하면
§7에서 코드 변경 2줄이 추가된다.

---

## 2. Unity 임포트 설정 수정 (Rig 탭) — 선행 작업, 반드시 먼저 — ✅ 완료

Avatar가 없는 한(§0.2) Animator/Controller 작업 자체가 불가능하므로 가장 먼저 처리한다.

1. `Monster_Rigged_kihong.fbx` 선택 → Inspector → **Rig** 탭 → Animation Type: Generic(이미
   맞음) → **Avatar Definition: "Create From This Model"**로 변경 → Apply. 이 시점에 Unity가
   본 계층으로부터 Avatar 서브에셋을 생성한다(구 `Monster_Rigged.fbx`가 `avatarSetup: 1`이었던
   것과 동일한 상태로 맞추는 것).
2. `Monster_Manual_Idle.fbx`/`Monster_Manual_Walk.fbx`/`Monster_Manual_TentacleDash.fbx`/
   `Monster_Manual_GrabKill.fbx` 4개 각각 선택 → Rig 탭 → **Avatar Definition: "Copy From Other
   Avatar"** → Source 필드에 1번에서 생성한 `Monster_Rigged_kihong` Avatar를 지정 → Apply.
   (본 이름·계층이 5개 파일 사이에서 정확히 일치해야 클립이 제대로 재생된다 — 한 블렌더 파일에서
   액션별로 내보낸 것이라면 보통 일치하지만, Apply 후 Animation 탭 프리뷰에서 실제로 모델이
   올바르게 움직이는지 눈으로 확인 필요.)
3. **Animation** 탭에서 각 클립의 Loop Time(Idle/Walk는 루프 필요, TentacleDash/GrabKill은
   1회 재생이 자연스러움 — 구 세트의 `Monster_Rigged_Walk.fbx`는 `loopTime: 0`으로 임포트돼
   있었는데 이것이 의도적이었는지 재확인 후 새 클립에도 동일 정책 적용) 확인·설정.
4. `read_console`로 임포트 에러/경고 0건 확인.

---

## 3. 머티리얼 확인 — ✅ 완료(문제 없음 확인)

`Monster_Rigged_kihong.fbx`의 `materialLocation: 1`(외부/레거시 참조)이 실제로 텍스처가 제대로
붙은 상태인지 Unity 에디터에서 모델을 직접 눈으로 확인 필요 — 핑크색(머티리얼 누락) 표시가
나오면 구 `Monster_Rigged_textures` 폴더처럼 머티리얼을 별도 폴더로 추출(`Extract Materials`)하고
텍스처를 재연결해야 한다. 이 항목은 코드와 무관한 순수 아트 파이프라인 작업.

---

## 4. `MonsterAnimator.controller` 갱신 — ✅ 완료(클립 4개 교체, 오타 정정은 미포함)

`MonsterMoveState` enum(`Idle/Walk/TentacleDash/GrabKill`)과 `MonsterController.ChangeState()`가
**트리거 파라미터 이름 문자열**로만 연결되므로, 파라미터 이름 자체(`Idle`/`Walk`/`TentacleDash`/
`GrabKill`)는 그대로 두고 **각 상태의 재생 클립만 새 파일로 교체**하면 C# 코드는 한 줄도 바꿀
필요가 없다.

1. **기존 컨트롤러를 그대로 재사용**할지, 새로 만들지 결정 — **재사용을 추천**한다(상태 4개·
   파라미터 5개 구조를 그대로 옮기고 클립만 갈아끼우는 것이 리스크가 가장 낮음).
2. `Idle` 상태의 Motion을 `Monster_Manual_Idle.fbx` 안의 클립으로 교체.
3. `Walk` 상태 → `Monster_Manual_Walk.fbx`.
4. `TentacleDash` 상태 → `Monster_Manual_TentacleDash.fbx`.
5. `GrapKill`(오타) 상태 → `Monster_Manual_GrabKill.fbx`.
6. **[선택, 이번 기회에 함께 처리 권장]** `research.md` §6.13이 지적한 상태명 오타
   (`GrapKill`→`GrabKill`)를 이번에 함께 정정할지 결정 필요 — 정정 시
   `MonsterController.cs:124`의 `state.IsName("GrapKill")` 문자열도 **반드시 동시에** `"GrabKill"`로
   수정해야 한다(코드-애니메이터 동기화, 둘 중 하나만 바꾸면 GrabKill 쿨다운 해제가 영구히
   깨짐). 이번 재작업 범위에 포함할지는 사용자 확인 필요 — 포함하지 않으면 상태명은 오타 그대로
   두고 클립만 교체.

---

## 5. 프리팹 재구성 (§1의 결정에 따라 `MonsterPlayer.prefab` 또는 `PlayerMonster.prefab`) — ✅ 완료

`Monster_Rigged_kihong.fbx`를 씬/프리팹에 배치한 뒤, §0.1에 정리한 기존 `MonsterPlayer.prefab`
구성을 그대로 재현한다:

1. **루트 GameObject**: 이름을 `MonsterPlayer`로, **Layer 10(Monster)** 지정.
2. **`m_LocalScale`**: 새 모델의 실제 크기를 Scene 뷰에서 확인해가며 조정 — 구 모델의 "6배"라는
   수치 자체는 구 모델 고유의 단위 스케일에 맞춘 값이라 새 모델에 그대로 쓸 근거가 없다.
   **에디터에서 시각적으로 맞추는 작업이 필요**(사람 크기 쿠키 캐릭터와 비교해 적절한 위협감을
   주는 크기로).
3. **`Animator`**: Avatar = §2-1에서 생성한 `Monster_Rigged_kihong` Avatar, Controller =
   §4에서 갱신한 `MonsterAnimator.controller`. `Apply Root Motion`은 기존과 동일하게 **꺼둔다**
   (`MonsterController`가 `Rigidbody` 이동을 직접 제어하므로 루트 모션과 충돌하면 안 됨).
4. **`Rigidbody`**: 기존과 동일 설정(mass 1, useGravity on) — `MonsterController.Start()`가
   `rb.useGravity`/`collisionDetectionMode`/`interpolation`/`constraints`를 코드로 다시
   설정하므로 인스펙터 기본값은 크게 중요하지 않음.
5. **`CapsuleCollider`(루트)**: radius/height/center를 **새 모델·새 스케일 기준으로 재설정**
   (구 수치 0.1/0.35/(0,0.15,0)를 그대로 복사하지 않는다 — §0.3에서 지적한 `PlayerMonster.prefab`의
   실수를 반복하지 않기 위함). Scene 뷰 Gizmo로 캡슐이 실제 모델 몸통을 덮는지 확인.
6. **`EyeSocket`**: 새 모델의 머리 위치에 맞는 빈 자식 GameObject를 새로 만들어 배치(본에
   종속시키지 않는 것은 구 프리팹과 동일한 패턴 — 머리 본을 못 찾아도 동작하도록). 1인칭 시점이
   자연스러운 높이/전방 오프셋으로 배치.
7. **`PhotonView`**: 추가 후 `ObservedComponents`에 `MonsterController`(아래 8번)를 등록.
8. **`MonsterController`**: `pv`=같은 오브젝트의 `PhotonView`, `eyeSocket`=6번의 `EyeSocket`,
   `grabKillTrigger`=9번의 `MonsterGrabKillTrigger`, `animator`=3번의 `Animator`,
   **`obstructionMask`=지형/벽 레이어로 실제 설정**(`research.md` §6.4의 기존 버그를 이번
   재구성 기회에 함께 수정 — 새로 만드는 김에 방치할 이유가 없음, 단 이것도 이번 범위에 포함할지
   사용자 확인 필요).
9. **`MonsterGrabKillTrigger`**: `monsterPv`=7번, `monsterController`=8번.
10. **`SphereCollider`(루트, trigger)**: GrabKill 판정 범위 — 새 모델의 팔/촉수 리치에 맞춰
    반경·위치 재조정.

---

## 6. 코드 변경 범위 — ✅ 확인됨(계획대로 `.cs` 변경 0건)

- **(A) 안 선택 시**: `Assets/02. Scripts/Monster/` 어떤 `.cs`도 수정 불필요(프리팹 이름이
  그대로 `"MonsterPlayer"`이므로 `MonsterJoinController.cs:14`/`MonsterTestSpawner.cs:10` 그대로
  유효). 단, §4-6에서 `GrapKill`→`GrabKill` 상태명 정정을 함께 하기로 하면
  `MonsterController.cs:124`의 문자열 리터럴 1곳만 수정.
- **(B) 안 선택 시**: 위 내용 + `MonsterJoinController.cs:14`, `MonsterTestSpawner.cs:10`의
  `MonsterPrefabName` 상수값을 `"PlayerMonster"`로 변경(2개 파일, 각 1줄).

---

## 7. 검증 계획 — ✅ 완료(결과는 §10)

1. `read_console`로 임포트/컴파일 에러 0건 확인(§2, §5 각 단계 직후).
2. **`PlayerTestScene`을 활용한 단독 검증** — 이미 이 목적을 위한 개발 도구가 갖춰져 있다
   (`Dev/OfflineModeBootstrap.cs` + `Dev/MonsterTestSpawner.cs`, `research.md` §2.8). `Offline
   ModeBootstrap.autoCreateRoom` + `SpawnAsMonster`를 켜고 Play Mode 진입 → `MonsterTestSpawner`가
   `MonsterSpawnPos`에서 새 프리팹을 스폰하는지 확인.
3. Play Mode에서 직접 확인할 목록:
   - Idle/Walk/TentacleDash/GrabKill 4개 애니메이션이 실제로 새 모델·새 클립으로 재생되는지
     (`MonsterController.ChangeState()`가 트리거를 걸 때마다 눈으로 전환 확인).
   - `MonsterTentacleDash`(좌Shift)가 실제로 동작하고, 새 캡슐 콜라이더 크기 기준으로 이동 거리가
     부자연스럽지 않은지.
   - `MonsterGrabKillTrigger`의 SphereCollider 범위가 실제 팔/촉수 리치와 맞는지(쿠키를 근접시켜
     확인).
   - `EyeSocket` 위치가 자연스러운 1인칭 시점을 주는지 — 단, **이 검증에는 `research.md` §6.1의
     별도 배선 공백(`MonsterFirstPersonCamera`가 어떤 Main Camera에도 부착돼 있지 않음)이 여전히
     막고 있다.** 이번 재리깅 작업과는 별개 이슈이므로, 카메라가 안 움직이는 것은 이번 작업의
     실패가 아니라 §6.1이 아직 해결되지 않았기 때문임을 구분해서 판단할 것 — 필요하면 이번
     작업에 카메라 배선도 함께 포함할지 사용자에게 재확인.
   - `PhotonView`/`MonsterController`의 `pv.IsMine` 분기가 정상 동작하는지(원격 클라이언트
     시점에서 캐릭터가 보간되며 따라오는지 — 2개 이상의 에디터/빌드 인스턴스 필요, 여의치 않으면
     최소한 로컬 스폰만이라도 NRE 없이 되는지 확인).

---

## 8. 정리 대상 (완료 후)

- (A) 선택 시: `Assets/04. Prefabs/Resources/PlayerMonster.prefab`(§0.3의 미완성 시도) 삭제.
  → **✅ 완료**(`git rm`으로 `.prefab`/`.meta` 제거, 아직 커밋은 안 함).
- 구 `Assets/Animation/Monster/Monster_Rigged*.fbx`(본체+4개 애니메이션) 및
  `Monster_Rigged_textures/` — 더는 어떤 프리팹도 참조하지 않게 되면 삭제 여부 결정(즉시 삭제
  대신 한동안 보관 후 정리도 가능, 사용자 판단 필요). → **보류(미삭제)** — 되돌릴 필요가 생길
  경우를 대비해 이번 작업 범위에서는 남겨뒀다. 삭제는 별도 확인 후 진행.
- `Assets/02. Scripts/QuarterViewActionGame.zip`(관련 없는 다른 프로젝트 잔재, git status에도
  삭제 대기 중으로 표시돼 있음) — 이번 작업과 무관하지만 발견된 김에 언급.

---

## 9. 범위 밖 (이번 재리깅 작업과 분리해서 다룰 것)

- `research.md` §6.1(괴물 1인칭 카메라 미배선) — §7에서 언급했듯 이번 작업의 `EyeSocket`은
  준비하지만, 카메라 스위칭 자체는 별도 작업(포함 여부 재확인 가능).
- `research.md` §6.2(`Cursor.lockState` 미설정), §6.5(재게임 시 프로퍼티 미정리) — 이번 재리깅과
  무관한 별도 버그.
- §4-6에서 언급한 `GrapKill`→`GrabKill` 오타 정정과 §5-8의 `obstructionMask` 수정은 "이번 김에
  포함할 수 있는 선택 항목"으로만 표시했다 — 기본 범위(모델·애니메이션 교체)에는 필수가 아니므로
  포함 여부를 명시적으로 확인 후 진행. **→ 이번 구현에서는 포함하지 않았다**(§10 참고, 여전히
  `research.md` §6.4/§6.13 미해결 상태로 남아 있음).

---

## 10. 구현 완료 보고 (2026-09-25)

Unity MCP `execute_code`로 Unity 6000.0.58f2 에디터(인스턴스 `TagOfChaos@ca592fd6`)에 직접 접속해
아래를 순서대로 수행했다. 모든 단계 사이사이 `read_console`로 에러/경고 0건을 확인했다.

### 10.1 §2 — Rig 임포트 설정

- `Monster_Rigged_kihong.fbx`: `ModelImporter.animationType = Generic`,
  `avatarSetup = CreateFromThisModel` → 재임포트 → **`Monster_Rigged_kihongAvatar` 생성 확인**
  (`avatar.isValid = true`, `isHuman = false`, 예상대로 Generic).
  - 임포트된 계층에서 본 이름을 실제로 열거해본 결과, 구 리그와 달리 **`Head`/`Chest` 본이
    명시적으로 존재**했다(구 리그는 `Spine_Root`부터 시작, 별도 `Head` 본 없음) — 이 덕분에
    EyeSocket 위치를 "임의 추정" 대신 **실제 Head 본 좌표로 배치**할 수 있었다(§10.3).
  - 나머지 본 이름은 구 리그와 명명 규칙만 다름(`.` 대신 `_`, 예: `Hip.R`→`Hip_R`), 촉수·손가락
    체계는 동일 계열.
- `Monster_Manual_Idle/Walk/TentacleDash/GrabKill.fbx` 4개: `avatarSetup = CopyFromOther` +
  `sourceAvatar = Monster_Rigged_kihongAvatar` → 재임포트 → **4개 전부 비어있지 않은(`empty=false`)
  `Scene` 클립 추출 확인**. 클립 길이: Idle 4.00s, Walk 2.00s, TentacleDash 2.04s, GrabKill 3.71s.
  (참고: Unity `ModelImporterAvatarSetup` enum의 실제 멤버명은 `NoAvatar`/`CreateFromThisModel`/
  `CopyFromOther`이다 — 계획 초안의 "CopyFromOtherAvatar" 표기는 실제 API명과 다름, 실행 시
  발견해 정정.)

### 10.2 §3 — 머티리얼

`Monster_Rigged_kihong`의 `SkinnedMeshRenderer.sharedMaterial`(`Material_0`, Standard 셰이더)이
`Image_0_BaseColor` 텍스처를 정상 참조 — 핑크(누락) 아님, 추가 조치 불필요로 확인.

### 10.3 §5 — 프리팹 재구성 실제 값

`Assets/04. Prefabs/Resources/MonsterPlayer.prefab`을 `PrefabUtility.LoadPrefabContents`로 직접
편집(씬을 거치지 않는 헤드리스 방식):

1. 기존 `Mesh_0`/`MonsterArmature`(구 리그) 자식 삭제.
2. `Monster_Rigged_kihong.fbx`를 `PrefabUtility.InstantiatePrefab`으로 인스턴스화 →
   **`PrefabUtility.UnpackPrefabInstance(..., Completely, ...)`로 언팩**(이 단계 없이 바로
   `SetParent`를 시도했다가 "Setting the parent of a transform which resides in a Prefab instance
   is not possible" 경고와 함께 **자식이 통째로 유실되는 실패를 1회 겪었다** — 아래 §10.5 참고,
   즉시 감지하고 재작업으로 복구함) → `Mesh_0`/`MonsterArmature`를 프리팹 루트의 직계 자식으로
   재배치, 남은 `CINEMA_4D_Editor`(익스포트 잔재 카메라 오브젝트) 삭제.
3. **루트 스케일**: 계획 초안은 구 리그의 "6배"를 그대로 언급했으나, 실행 시 **양쪽 모델을 각각
   임시 인스턴스화해 `SkinnedMeshRenderer.bounds`를 직접 측정**해 비교했다:
   - 구 `MonsterPlayer.prefab`(스케일 6) 실측 월드 바운즈: `(13.04, 12.55, 9.14)`
   - 신 모델(스케일 1) 실측 월드 바운즈: `(2.39, 2.32, 1.92)`
   - 높이(Y) 기준 배율 `13.04/2.32 ≈ 5.42` 산출 → **루트 `localScale = (5.42, 5.42, 5.42)`로 확정**
     (6이 아님). 최종 재구성 후 실측 바운즈 `(12.93, 12.56, 10.40)` — 구 모델과 거의 동일한
     크기로 재현됨.
4. **자식 레이어**: `Mesh_0`/`MonsterArmature` 하위 59개 오브젝트 전부 레이어 0으로 정규화(루트만
   레이어 10 유지, 구 프리팹과 동일 컨벤션).
5. **`Animator`**: `avatar = Monster_Rigged_kihongAvatar`로 교체, `controller`는 **같은
   `MonsterAnimator.controller` 애셋을 그대로 재사용**(§4에서 모션만 갈아끼웠으므로 참조 변경
   불필요).
6. **`EyeSocket`**: 계획 초안의 "임의 배치, 추후 튜닝"을 실행 단계에서 개선 — 실제 `Head` 본의
   월드 좌표를 `root.transform.InverseTransformPoint()`로 루트 로컬 좌표로 변환하고 전방으로
   0.03 미세 오프셋을 더해 배치. 결과: **로컬 `(0, 0.25, 0.03)`**(계획 초안이 가정했던 구 프리팹
   값 `(0, 0.2, 0.05)`와 근접하되, 실측 기반이라 더 신뢰도 높음).
7. `CapsuleCollider`(반경 0.1/높이 0.35/중심 (0,0.15,0))·`SphereCollider`(반경 0.5/중심
   (0,0.15,0.1))·`PhotonView`·`MonsterController`·`MonsterGrabKillTrigger`는 **로컬 수치를 그대로
   유지**했다 — 스케일이 6→5.42로 10%만 달라져 월드 단위 차이가 미미하고(캡슐 월드 반경
   0.6→0.542, 높이 2.1→1.897), §9에서 범위 밖으로 명시한 `obstructionMask`(여전히 Nothing)도
   이번 작업에서 건드리지 않았다.

### 10.4 §7 — 검증 결과 (`PlayerTestScene`, Play Mode)

`OfflineModeBootstrap.autoCreateRoom`/`spawnAsMonster`를 테스트 동안만 `true`로 설정(검증 후
`false`로 원복, 씬 파일은 저장하지 않아 디스크상 변경 없음) → Play 진입:

- `MonsterTestSpawner`가 `MonsterSpawnPos`에서 `MonsterPlayer(Clone)`을 정상 스폰. **콘솔 에러/
  예외 0건**(계획에서 우려했던 "`pv`/`eyeSocket` 미배선 시 `Awake()` NRE" 시나리오는 재구성된
  프리팹에서는 재현되지 않음 — 필드가 전부 정상 배선돼 있었기 때문).
- `PhotonView.IsMine = true`, `ViewID` 정상 발급.
- `Animator.avatar.isValid = true`, 기본 상태 `Idle`이 루프 재생 중(`normalizedTime`이 계속
  누적).
- `Animator.SetTrigger()`로 `Walk`/`TentacleDash`/`GrabKill`/`Idle` 4개 상태를 순서대로 강제
  진입시켜 전부 새 클립(`Scene`)으로 정상 전환되는 것을 `GetCurrentAnimatorStateInfo().IsName()`로
  확인. (`GrabKill` 트리거는 기존과 동일하게 `GrapKill`이라는 오타 상태로 들어간다 — §9에서
  범위 밖으로 명시한 그대로, 이번 작업이 새로 만든 문제가 아님.)
- `Rigidbody`(useGravity, non-kinematic), `SkinnedMeshRenderer`(bounds 정상) 모두 예상대로 동작.
- 테스트 종료 후 Play Mode 정지, `OfflineModeBootstrap` 플래그 원복.

### 10.5 계획에 없었던 실행 중 발견 사항

- **`PrefabUtility.InstantiatePrefab`으로 모델(.fbx) 애셋을 인스턴스화하면 "모델 프리팹 인스턴스"로
  취급되어, 언팩(`UnpackPrefabInstance`) 없이는 그 자식(`Mesh_0`/`MonsterArmature`)을 다른
  부모로 옮길 수 없다** — 시도 시 Unity가 경고를 내며 조용히 무시하고, 그 상태에서 임시
  래퍼(`temp`) 오브젝트를 `DestroyImmediate`하면 **자식까지 함께 삭제되어 프리팹이 시각적으로
  텅 비어버리는 사고가 1회 발생**했다. `read_console`로 즉시 감지(`Setting the parent of a
  transform which resides in a Prefab instance is not possible` 경고 3건) → `UnpackPrefabInstance`
  단계를 추가해 재작업, 최종 결과물에는 영향 없음. 향후 유사 작업(모델 애셋을 프리팹 내부로
  합성) 시 이 언팩 단계를 먼저 넣을 것.
- `ModelImporterAvatarSetup` enum의 실제 값은 `CopyFromOther`이지 `CopyFromOtherAvatar`가 아니다
  (계획 문서 §2의 표기를 실제 API 확인 후 정정).
- `git status`상 `Assets/Fonts/NotoSansKR SDF.asset`이 일시적으로 수정된 것으로 표시됐으나
  `git diff`상 실제 내용 차이는 없었다(개행 방식 등 무해한 차이로 추정, 이번 작업이 만든 실질적
  변경 아님).

### 10.6 최종 변경 파일 목록 (`git status`)

- `Assets/04. Prefabs/Resources/MonsterPlayer.prefab` — 수정(신규 리그로 내부 재구성).
- `Assets/04. Prefabs/Resources/PlayerMonster.prefab`(+`.meta`) — 삭제(§8, `git rm`).
- `Assets/Animation/Monster/NewAnimation/Monster_Rigged_kihong.fbx.meta`,
  `Monster_Manual_{Idle,Walk,TentacleDash,GrabKill}.fbx.meta` — 수정(Rig 임포트 설정).
- `Assets/Animation/MonsterAnimator.controller` — 수정(4개 상태 모션 교체).
- `Assets/02. Scripts/**/*.cs` — **변경 없음**(계획대로 0건).

### 10.7 남은 후속 항목 (이번 범위 밖, `research.md`에 이미 기록됨)

- §6.1 괴물 1인칭 카메라 미배선, §6.2 `Cursor.lockState`, §6.4 `obstructionMask`, §6.5 재게임
  프로퍼티 정리, §6.13 `GrapKill` 오타 — 전부 이번 재리깅과 무관하게 그대로 남아 있음.
- 스케일(5.42)·EyeSocket 위치·콜라이더 크기는 "구 모델과 비슷한 크기로 재현"을 기준으로 정한
  값이라 실제 게임 내(카메라 시점, 그랩킬 판정 리치)에서 위화감이 있으면 인스펙터에서 미세 조정
  권장.
