# 계획: Hero_Ctrl → HideOrSeekPlayer 이동 로직 리팩토링

상태: **구현 완료** (2026-08-13) — 아래 전체 계획이 승인을 받아 구현됐다. 각 섹션 제목 옆에 완료 표시를
남겼고, 구현 중 실제로 발생한 이슈와 계획 대비 변경/추가 사항은 맨 끝의 §12에 정리했다.

사용자 확인 사항(사전 질문으로 확정):
- Photon 네트워킹(소유권 체크 + 위치/회전 동기화)은 그대로 포함한다.
- 입력 처리는 기존 legacy Input Manager(`Input.GetAxisRaw`/`Input.GetKeyDown`)를 그대로 사용한다.
  (새 Input System으로의 마이그레이션은 이번 범위에서 제외, §10 참고)
- 이동 관련 애니메이션 상태 전환(idle/walk/sneakwalk/jump/dodge)은 포함하되, 전투 관련(attack/skill)은 제외한다.
- `Assets/Animation/Idle.fbx`의 캐릭터를 `hide_or_seek_player`로 명명하고, 기존 `mixamo_com` 플레이스홀더
  애니메이션 대신 `Idle`/`Walking`/`SneakWalking`/`Jumping`/`Dodge` 5종의 새 mocap 클립을 사용한다
  (§8 참고).
- 기존 계획의 오케스트레이터 클래스 `PlayerController`를 `HideOrSeekPlayer`로 개명한다. 클래스/파일
  이름은 사용자가 요청한 `hide_or_seek_player`(에셋 이름 표기 그대로)가 아니라, Unity가 요구하는
  "MonoBehaviour 파일명 = 클래스명" 제약과 `CLAUDE.md`의 C#/OOP 관례를 따라 PascalCase
  `HideOrSeekPlayer.cs` / `class HideOrSeekPlayer`로 표기한다(§3-1 참고, 사전 질문으로 확정).
- `AnimState`는 이번 계획과 별도로 새로 정의될 예정이며(사용자 확인), `Camera_Ctrl.cs`는 이미
  `Assets/Scripts/`에 추가되어 더 이상 미정의 타입이 아니다. 다만 이 두 가지 모두 `HideOrSeekPlayer.cs`의
  범위에는 영향을 주지 않는다 — `AnimState`는 전투 상태(attack/skill)를 포함하는 `Hero_Ctrl` 전용 타입이라
  이동 전용 클래스에는 여전히 `PlayerMoveState`(§7)를 쓰고, `Camera_Ctrl`은 이제 존재하더라도 카메라
  추적은 여전히 별도 시스템의 책임이라는 원래 설계 이유로 범위에서 제외한다(§2.2 참고).

---

## 1. 목적 ✅

`Hero_Ctrl.cs`(`Assets/Scripts/Hero_Ctrl.cs`)에서 **이동 관련 로직만** 분리하여, `CLAUDE.md`의
폴더/설계 원칙을 따르는 새 클래스 `HideOrSeekPlayer`로 다시 만든다. 몬스터와 상호작용하는 코드
(공격 판정, 데미지, HP, 사망 처리 등)는 포함하지 않는다. `Hero_Ctrl.cs` 자체는 이번 작업에서
수정/삭제하지 않는다(별도 파일로 새로 만드는 것이며, 기존 파일과의 교체 여부는 별도 논의 대상).

## 2. 범위 ✅

### 2.1 포함 (이동 관련)
- 걷기/달리기 이동(`Move`), 카메라 기준 방향 입력 처리
- 살금살금 걷기(`SneakWalk`) — 기존 Hero_Ctrl에서 Shift 홀드 시 30% 속도로만 처리되던 것을, 전용
  애니메이션 상태로 승격(§8 참고)
- 점프(`Jump`) — 커스텀 중력, 레이캐스트 기반 착지 판정, 공중 궤적 고정
- 회피(`Dodge`) — 방향 고정, 이동 배속, 종료 타이머
- 이동 관련 애니메이션 상태 전환(Idle/Walk/SneakWalk/Jump/Dodge, 5종) — `hide_or_seek_player` 캐릭터의
  새 mocap 클립으로 교체(§8 참고)
- Photon 동기화(위치, 회전, 이동 상태, 점프 여부) — 리모트 아바타 보간 포함
- NavMeshAgent와의 연동(점프 중 비활성화 → 착지 후 `Warp` 재동기화)

### 2.2 제외 (몬스터 상호작용 / 전투 / UI)
- 공격 판정 및 히트 처리(`Event_AttHit`, `AttackOrder`, `IsAttack`)
- HP, 데미지, 사망 처리(`TakeDamage`, `Die`, `Remote_TakeDamage`, `CurHp`, `MaxHp`)
- HP 바 UI(`ImgHpbar`), 닉네임 라벨(`id`)
- 카메라 초기화(`Camera_Ctrl.InitCamera`) — `Camera_Ctrl.cs`가 이제 `Assets/Scripts/`에 존재하지만
  (더 이상 미정의 타입이 아님, research.md 갱신 예정), 카메라 추적은 이동 로직이 아니라 별도 시스템의
  책임이라는 원래 설계 이유는 그대로 유효하므로 계속 제외한다. 필요해지면 `HideOrSeekPlayer`를 소유한
  상위 스폰 로직(예: `GameManager.CreateHero()`)이나 별도 카메라 리그 컴포넌트에서 `Camera_Ctrl.InitCamera(...)`를
  호출하는 편이 책임 분리에 맞다.
- `PlayerPrefs.SetInt("MaxScore", ...)` — `research.md` §8.5에서 지적한 디버그 잔재 코드, 이식하지 않음

## 3. 배치 위치 ✅

`CLAUDE.md` 폴더 규칙(`Scripts` → `Assets/02. Scripts/{도메인}/`)과 주요 시스템 표(`유닛/파티` →
`Scripts/Unit/`)에 따라, 플레이어 캐릭터는 `Unit` 도메인으로 분류한다.

```
Assets/02. Scripts/Unit/
├── HideOrSeekPlayer.cs        # MonoBehaviour, 조립(오케스트레이션) 담당
├── PlayerMoveState.cs         # enum (Idle, Walk, SneakWalk, Jump, Dodge)
├── PlayerGroundDetector.cs    # 순수 C# 클래스: 커스텀 중력 + 착지 판정
├── PlayerAnimationDriver.cs   # 순수 C# 클래스: Animator 트리거 전환
└── PlayerNetworkSync.cs       # 순수 C# 클래스: Photon 직렬화/역직렬화 + 리모트 보간
```

현재 프로젝트에는 `Assets/02. Scripts/` 자체가 아직 없다(기존 `Assets/Scripts/Hero_Ctrl.cs`는 구
구조). 이번 작업에서 새 폴더 구조를 처음 만들게 된다.

> 참고: `CLAUDE.md`는 `유닛/파티` 시스템 문서를 `Docs/Systems/Unit.md`에 두도록 명시하고 있으나,
> `Docs/` 폴더 자체가 아직 존재하지 않는다. 이번 계획에서는 문서 작성을 필수 범위로 잡지 않고
> §10(후속 작업)에 남겨둔다 — 필요하면 알려주면 같이 작성한다.

### 3-1. 파일/클래스 명명 — `hide_or_seek_player` vs `HideOrSeekPlayer`

사전 질문으로 확정된 사항: Unity는 MonoBehaviour를 GameObject에 붙이려면 **`.cs` 파일 이름과 그 안의
public 클래스 이름이 정확히 일치**해야 한다(대소문자까지 일치해야 컴포넌트로 인식됨). 사용자가 언급한
`hide_or_seek_player`(소문자+언더스코어)는 `Assets/Animation/Idle.fbx`의 아바타/모델 이름(§8.2)이지,
C# 클래스 명명 규칙은 아니다. 두 가지 선택지가 있었다:

1. 에셋 이름 그대로: `hide_or_seek_player.cs` / `class hide_or_seek_player` — 캐릭터 에셋과 완전히
   동일한 표기지만 `CLAUDE.md`의 "OOP 기반 설계" 원칙이 암묵적으로 전제하는 C# 관례(PascalCase)와 어긋남.
2. **PascalCase로 변환**: `HideOrSeekPlayer.cs` / `class HideOrSeekPlayer` — 에셋 이름과 표기만 다를 뿐
   같은 이름을 가리키며, 프로젝트의 다른 클래스(`Hero_Ctrl`, `Camera_Ctrl`, `GameManager`)와 명명
   스타일이 맞고 C# 관례를 따름.

**2번으로 확정.** 이번 계획 전체에서 `PlayerController`로 불리던 오케스트레이터 클래스를
`HideOrSeekPlayer`로 개명한다(§4~§11 전체 반영 완료). `PlayerMoveState`/`PlayerGroundDetector`/
`PlayerAnimationDriver`/`PlayerNetworkSync` 같은 보조 순수 C# 클래스들은 Unity의 파일명 일치 제약을
받지 않고(MonoBehaviour가 아님), §4에서 설명하듯 몬스터 등 다른 유닛에도 재사용할 목적으로 이미
범용적인 `Player` 접두사를 쓰고 있었으므로 그대로 유지한다 — `HideOrSeekPlayer` 접두사로 바꾸면 오히려
재사용 의도와 모순되기 때문이다.

## 4. 아키텍처 설계 (OOP 분해) ✅

`Hero_Ctrl`은 이동/애니메이션/네트워크/전투/UI를 한 클래스가 전부 처리하는 모놀리식 구조였다.
`CLAUDE.md`의 "OOP 기반 설계" 원칙에 따라, 이번 리팩토링에서는 책임을 4개의 협력 객체로 나눈다.
Unity 라이프사이클(Update, PhotonView 등)에 묶여야 하는 부분만 `MonoBehaviour`로 남기고, 나머지는
테스트 가능한 순수 C# 클래스로 분리한다.

```
HideOrSeekPlayer (MonoBehaviourPunCallbacks, IPunObservable)
 ├─ 입력 읽기 + 상태 플래그 관리 (isJump, isDodge, rotation 등)
 ├─ PlayerGroundDetector   ── 중력 적분 + 레이캐스트 착지 판정, 착지 콜백 제공
 ├─ PlayerAnimationDriver  ── Animator 참조를 감싸 PlayerMoveState 트리거 전환만 담당
 └─ PlayerNetworkSync      ── OnPhotonSerializeView의 읽기/쓰기, 리모트 위치·회전 보간 계산
```

**책임 분리 이유**
- `PlayerGroundDetector`는 Hero_Ctrl의 `ApplyGravity()` 로직을 그대로 옮기되, 순수 C# 클래스로
  분리해 두면 추후 몬스터 등 다른 유닛의 낙하/착지 판정에도 재사용할 수 있다.
- `PlayerAnimationDriver`는 `ChangeAnimState()`를 대체하며, `PlayerMoveState` 5종만 다룬다. 전투
  애니메이션(attack/skill)이 추가될 미래 시점에도 이 클래스는 건드릴 필요가 없다.
- `PlayerNetworkSync`는 스트림 read/write 순서와 리모트 보간 수식을 한 곳에 모아, `HideOrSeekPlayer`
  가 "무엇을 얼마나 자주 보내는지"에 관심을 두지 않아도 되게 한다.
- `HideOrSeekPlayer` 자체는 Photon 콜백과 Unity 생명주기를 받아야 하므로 MonoBehaviour로 남지만,
  각 프레임의 실제 계산은 위 세 클래스에 위임하는 얇은 조립자(orchestrator) 역할만 한다.

## 5. Hero_Ctrl → HideOrSeekPlayer 필드 매핑 ✅

| Hero_Ctrl 필드 | 처리 | 이동 위치 / 비고 |
|---|---|---|
| `speed`, `jumpPower`, `groundLayer`, `groundCheckOffset`, `jumpFreezeNormalizedTime` | 유지 | `HideOrSeekPlayer` (Options), 일부는 `PlayerGroundDetector` 생성자로 전달 |
| `MaxHp`, `CurHp` | 제외 | HP는 전투 도메인 |
| `pv` (PhotonView) | 유지 | `HideOrSeekPlayer` |
| `agent` (NavMeshAgent) | 유지 | `HideOrSeekPlayer` |
| `id` (Text), `ImgHpbar` (Image) | 제외 | UI, 이동과 무관 |
| `velocity` | 수정 | 클래스 필드 → `Move()` 내 지역 변수로 축소 (research.md §8.6 스멜 수정) |
| `baseSpeed` | 유지 | `HideOrSeekPlayer` |
| `m_MvDelay` | 제외 | 어디서도 0이 아닌 값이 대입되지 않는 죽은 코드(research.md §8.6) — 이식하지 않음 |
| `h`, `v`, `rotation`, `rotation_value` | 유지 | `HideOrSeekPlayer` |
| `m_Animator`, `m_PreState`, `m_CurState` | 이동 | `PlayerAnimationDriver`로 캡슐화, 타입은 `AnimState` 대신 `PlayerMoveState` |
| `isJump`, `isDodge`, `keepMovingAfterJump` | 유지 | `HideOrSeekPlayer` |
| `keepMovingAfterDodge` | 수정 | research.md §8.1 버그 수정: `isDodge`와 동시에 세팅되도록 순서 변경 |
| `isDead`, `isChat` | 대체 | 둘 다 "이동을 막는다"는 동일한 목적의 서로 다른 플래그였음 → `bool IsMovementLocked` 단일 공개
  프로퍼티로 통합. 사망/대화/컷신 등 상위 시스템이 이 프로퍼티만 세팅하면 됨(§8 참고) |
| `dodgeRotation`, `dodgeMoveDir`, `jumpMoveDir` | 유지 | `HideOrSeekPlayer` |
| `yVelocity`, `gravity` | 이동 | `PlayerGroundDetector` |
| `isFirstUpdate`, `CurPos`, `CurRot`, `m_IsJump` | 이동 | `PlayerNetworkSync` |
| `m_Id`, `NetHp` | 제외 | 닉네임/HP, 이동과 무관 |
| `m_EnemyList`, `m_CacTgVec`, `m_AttackDist` | 제외 | 전투 전용 |

## 6. Hero_Ctrl → HideOrSeekPlayer 메서드 매핑 ✅

| Hero_Ctrl 메서드 | 처리 | 비고 |
|---|---|---|
| `Awake()` | 축소 | `PlayerPrefs.SetInt` 잔재 코드 삭제, `Camera_Ctrl`/닉네임 초기화 삭제(카메라·UI는 별도 시스템) |
| `Start()` | 축소 | `CurHp = MaxHp` 삭제, `baseSpeed`/`Animator` 캐싱만 유지 |
| `Update()` | 재구성 | `AttackOrder()`, `Remote_TakeDamage()` 호출 제거. research.md §8.8에서 지적한 "입력이 이동보다
  한 프레임 늦게 반영되는" 순서 버그를 `CheckMovementInput()`을 `Move()`보다 먼저 호출하도록 고쳐서
  같이 수정 |
| `ApplyGravity()` | 이동 | `PlayerGroundDetector.Tick(...)`로 이식, 착지 시 `HideOrSeekPlayer`에 콜백 |
| `HandleJumpAnimationHold()` | 이동 | `PlayerAnimationDriver`로 이식 |
| `CheckMovementInput()` | 수정 | `GameManager.Inst.Is_Conversating` 참조 제거 → `IsMovementLocked` 프로퍼티로 대체(§5) |
| `CheckJumpInput()`, `CheckDodgeInput()`, `DodgeOut()` | 유지(버그 수정) | 회피 로직은 §5의
  `keepMovingAfterDodge` 순서 수정 반영. `Invoke("DodgeOut", 0.5f)` 리플렉션 예약 대신 `Update()`에서
  검사하는 float 타이머로 교체(최적화, research.md §8.6에서 지적한 매직 문자열 Invoke 제거) |
| `Move()` | 축소 | `IsAttack()` 게이팅 분기 삭제(전투 없음) → 이동 모드가 3개에서 2개(회피 관성 / 점프 관성 /
  일반)로 단순화. §5 버그 수정 덕분에 회피 관성 분기가 실제로 동작하게 됨 |
| `Event_AttHit()`, `Event_AttFinish()`, `AttackOrder()`, `IsAttack()` | 제외 | 전투 전용 |
| `IsDodge()` | 유지 | 외부에서 회피 상태를 조회할 수 있는 공개 API로 유지 |
| `ChangeAnimState()` | 이동 | `PlayerAnimationDriver.ChangeState(PlayerMoveState)`로 대체, attack/skill 케이스 없이 5개
  상태(Idle/Walk/SneakWalk/Jump/Dodge)만 처리 |
| `TakeDamage()`, `Die()`, `Remote_TakeDamage()` | 제외 | 전투/HP 전용 |
| `OnPhotonSerializeView()` | 축소 | 스트림에서 `id.text`, `CurHp` 제거. `position, rotation, (int)state, isJump`만 송수신 |

## 7. 새 타입: `PlayerMoveState` ✅

```csharp
public enum PlayerMoveState
{
    Idle,
    Walk,
    SneakWalk,
    Jump,
    Dodge
}
```

기존 `Hero_Ctrl`의 `AnimState`는 소문자 멤버(`idle`, `move`, ...)를 사용했지만, `PlayerAnimator.controller`가
현재 완전히 비어 있는 상태(research.md §3.2)라 기존 파라미터와 충돌할 여지가 없다. C# 명명 규칙에 맞춰
PascalCase로 새로 정의하고, Animator 트리거 파라미터도 동일하게 `Idle`/`Walk`/`SneakWalk`/`Jump`/`Dodge`로
만든다.

`Move`를 `Walk`로 개명하고 `SneakWalk`를 새로 추가한 이유: 기존 Hero_Ctrl의 `Move()`는 `Input.GetKey
(KeyCode.LeftShift)` 홀드 여부로 속도만 30%로 낮췄을 뿐, 애니메이션은 항상 `move` 트리거 하나였다.
이번에 실제 `SneakWalking.fbx` 클립이 생겼으므로, Shift 홀드 상태를 속도뿐 아니라 애니메이션 상태
전환에도 반영해야 한다(§8.3 참고). 점프/회피 중에는 기존과 동일하게 `Idle`/`Walk`/`SneakWalk`
상태 전환이 억제된다.

## 8. 애니메이션 에셋 교체 — `hide_or_seek_player` ✅ (§12 참고: Avatar 이름 관련 제약 있음)

### 8.1 현재 상태 확인
`Assets/Animation/`에는 다음 mocap 파일이 있다(전부 스켈레탈 메시가 포함된 원본 Mixamo-계열 fbx로
추정, 파일당 약 35MB):

| 파일 | `.meta` 존재 | 비고 |
|---|---|---|
| `Idle.fbx` | 있음 | Animation Type: Humanoid(2), Avatar Definition: Create From This Model(0). 단 `.meta`의
  `humanDescription.human`/`skeleton`이 비어 있어, 실제 본 매핑은 아직 Unity 에디터가 이 파일을
  임포트/구성한 적이 없거나 재확인이 필요한 상태로 보인다 |
| `Walking.fbx` | 있음 | 위와 동일하게 확인 필요 |
| `SneakWalking.fbx` | 있음 | 위와 동일하게 확인 필요 |
| `Jumping.fbx` | 있음 | 위와 동일하게 확인 필요 |
| `Dodge.fbx` | **없음** | 아직 한 번도 Unity에 임포트되지 않음 — 에디터가 이 파일을 인식하는 시점에
  `.meta`가 새로 생성되며, 그 뒤에 아래 아바타 설정을 적용해야 함 |

추가로 `Run.fbx.meta`만 남아있고 실제 `Run.fbx`는 존재하지 않는다(고아 meta로 추정). 이번 계획의
필수 범위는 아니며, 삭제 여부는 별도로 확인받은 뒤 처리한다(임의로 지우지 않음).

기존 `PlayerAnimator.controller`(research.md §3.2에서 지적된 상태)는 `mixamo_com`이라는 단일 상태만
가지고 있고 파라미터가 0개다 — 이번 작업으로 완전히 재구성된다(§8.4).

### 8.2 아바타(Rig) 전략 — 기준 캐릭터 하나만 사용
5개 fbx 전부가 각자 메시+스켈레톤을 포함한 통짜 원본이므로, 그대로 두면 파일마다 별도 Avatar가
생겨 리타겟이 어긋나거나(휴머노이드 리타게팅은 본 계층/이름이 동일해도 Avatar가 다르면 미묘한
차이가 생길 수 있음), 5벌의 중복 메시가 프로젝트에 남는다. Mixamo 계열 fbx의 표준 처리 방식을 따라:

1. `Idle.fbx`를 기준 캐릭터로 지정한다. 임포트 설정에서 Animation Type: **Humanoid**, Avatar
   Definition: **Create From This Model**로 두고, 생성된 Avatar 및 모델 루트 오브젝트 이름을
   `hide_or_seek_player`로 변경한다. 실제 씬에 배치되는 `HideOrSeekPlayer` 프리팹의 메시/Animator는
   이 캐릭터를 사용한다.
2. `Walking.fbx`, `SneakWalking.fbx`, `Jumping.fbx`, `Dodge.fbx`는 애니메이션 **클립 추출 용도로만**
   사용한다. 각각의 임포트 설정에서 Animation Type: Humanoid, Avatar Definition: **Copy From Other
   Avatar** → `hide_or_seek_player`(1의 Avatar)를 지정하여, 4개 파일 모두 동일한 리타겟 기준을 공유하게
   한다. 이 4개 fbx 안의 메시는 실제로 씬에 사용하지 않는다(클립만 꺼내 쓴다).
3. 각 fbx를 임포트한 뒤 Animation 탭에서 실제 클립 이름을 확인한다(Mixamo 원본은 보통 파일명이 아니라
   `mixamo.com` 등의 Take 이름을 쓰는 경우가 많다) — 필요하면 클립 이름을 파일명과 동일하게
   (`Idle`, `Walking`, `SneakWalking`, `Jumping`, `Dodge`) 정리해서 `PlayerAnimator.controller`에서
   참조하기 쉽게 만든다.

### 8.3 `PlayerAnimator.controller` 재구성
기존 `mixamo_com` 단일 상태를 제거하고, `PlayerMoveState`(§7) 5종에 대응하는 State 5개를 새로 만든다.

| AnimatorState | Motion(클립) | Trigger 파라미터 |
|---|---|---|
| `Idle` | `Idle.fbx`의 클립 | `Idle` |
| `Walk` | `Walking.fbx`의 클립 | `Walk` |
| `SneakWalk` | `SneakWalking.fbx`의 클립 | `SneakWalk` |
| `Jump` | `Jumping.fbx`의 클립 (상태 이름은 문자 그대로 `Jump`여야 `HandleJumpAnimationHold`의
  `state.IsName("Jump")` 검사가 동작함, §6 참고) | `Jump` |
| `Dodge` | `Dodge.fbx`의 클립 | `Dodge` |

각 상태는 Any State → 해당 상태로의 전이(해당 Trigger 조건, `Has Exit Time` 꺼짐)를 갖는다. 기본
상태(Default State)는 `Idle`로 지정한다. `PlayerAnimationDriver.ChangeState(PlayerMoveState)`(§4, §6)는
이 5개 트리거만 다루면 된다.

`HideOrSeekPlayer.Move()`/`CheckMovementInput()`에서 Shift 홀드 + 이동 입력이 있을 때는
`PlayerMoveState.SneakWalk`로, Shift를 떼면 `PlayerMoveState.Walk`로 전환한다(속도 배율 자체는 기존
Hero_Ctrl 로직 그대로 30%/100% 유지, §6 `Move()` 항목 참고).

### 8.4 검증
`Idle`/`Walk`/`SneakWalk`/`Jump`/`Dodge` 전환 시 Unity 콘솔에 "parameter does not exist" 경고가 뜨지
않는지, 그리고 실제로 각 상태에서 지정한 mocap 클립이 재생되는지 Play Mode에서 확인한다(§11 검증
계획에 반영, 기존 §10.3 항목을 이 절로 대체).

## 9. `IsMovementLocked` — GameManager 의존성 제거 방안 ✅

`Hero_Ctrl.CheckMovementInput()`은 `GameManager.Inst.Is_Conversating`을 직접 참조한다. 최초 조사
시점(research.md §2)에는 이 `GameManager` 타입이 프로젝트 어디에도 없어 컴파일이 아예 불가능했지만,
이후 `Assets/Scripts/GameManager.cs`가 추가되어 정확히 같은 시그니처(`static Inst`,
`bool Is_Conversating`)로 실제 존재하게 되었다(research.md §14). 즉 지금은 "타입이 없어서" 대체하는
것이 아니라, **`HideOrSeekPlayer`(이동 전용 컴포넌트)가 채팅/UI 시스템에 하드 의존하지 않는 느슨한
결합을 유지하기 위해** 여전히 다음과 같이 대체한다:

```csharp
public bool IsMovementLocked { get; set; }
```

`Update()` 최상단에서 `if (IsMovementLocked) return;` 형태로 사용하며, `isDead`가 하던 역할과
`Is_Conversating`이 하던 역할을 하나의 플래그로 통합한다. 이후 대화 시스템이나 전투 시스템(사망 처리)이
만들어지면, 각자 이 프로퍼티에 값을 세팅하기만 하면 되고 `HideOrSeekPlayer`는 그 출처를 알 필요가 없다
(느슨한 결합).

## 10. 이번 범위에서 제외하는 것 (후속 작업 후보) ✅ (계획대로 제외 유지)

- **새 Input System 마이그레이션**: 프로젝트에 `InputSystem_Actions.inputactions`가 이미 존재하지만,
  이번 리팩토링은 legacy Input Manager를 그대로 유지하기로 확정했다. 추후 별도 작업으로 분리 가능.
- **`Docs/Systems/Unit.md` 작성**: `CLAUDE.md`가 요구하는 시스템 문서. 이번 계획에는 포함하지 않았고,
  필요하면 별도로 요청해달라.
- **`Hero_Ctrl.cs` 삭제/교체**: 이번 작업은 새 파일을 만드는 것으로 끝나며, 기존 `Hero_Ctrl.cs`를
  지우거나 프리팹을 교체하는 작업은 포함하지 않는다. 두 클래스가 당분간 공존한다.
- **카메라 추적, 닉네임 UI, HP UI**: 각각 별도 컴포넌트로 나중에 다뤄야 함(§2.2).
- **`Run.fbx.meta` 고아 파일 정리**: 실체 없는 `.meta`를 지울지 여부는 별도로 확인받은 뒤 처리(§8.1).

## 11. 검증 계획 ✅ (§12 참고: 일부 항목은 정적 검증으로 대체)

1. Unity MCP `read_console`로 컴파일 에러 0건 확인 (기존 `Hero_Ctrl.cs`의 컴파일 에러는 그대로 두되,
   새 `HideOrSeekPlayer.cs`는 독립적으로 컴파일 가능해야 함 — 미정의 타입 의존성이 없으므로 가능함).
2. 임시 테스트용 GameObject에 `HideOrSeekPlayer` + `PhotonView` + `NavMeshAgent` + `Animator`를 붙여서
   실제 씬에서 이동/점프/회피가 동작하는지 수동 확인(Play Mode).
3. `hide_or_seek_player`(`Idle.fbx`)를 기준으로 `Walking`/`SneakWalking`/`Jumping`/`Dodge`의 Avatar가
   모두 "Copy From Other Avatar"로 올바르게 연결됐는지, 리타겟 경고가 없는지 확인(§8.2).
4. `PlayerAnimator.controller`에 `Idle`/`Walk`/`SneakWalk`/`Jump`/`Dodge` 트리거와 `Jump`라는 이름의
   상태가 추가되어, 트리거 호출 시 콘솔에 "parameter does not exist" 경고가 뜨지 않는지 확인(§8.3, §8.4).
5. Shift 홀드 여부에 따라 `Walk` ↔ `SneakWalk` 애니메이션이 올바르게 전환되는지 확인.
6. 회피 관성 이동(§6 버그 수정) 분기가 실제로 실행되는지, 회피 중 방향을 유지한 채 이동하는지 확인.

---

## 12. 구현 완료 보고 (2026-08-13)

### 12.1 생성된 파일
- `Assets/02. Scripts/Unit/PlayerMoveState.cs`
- `Assets/02. Scripts/Unit/PlayerGroundDetector.cs`
- `Assets/02. Scripts/Unit/PlayerAnimationDriver.cs`
- `Assets/02. Scripts/Unit/PlayerNetworkSync.cs`
- `Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`
- `Assets/Animation/PlayerAnimator.controller` — `Idle`/`Walk`/`SneakWalk`/`Jump`/`Dodge` 5개 State +
  5개 Trigger 파라미터 + Any State 전이로 재구성 완료 (기존 `mixamo_com` 상태는 제거)

### 12.2 계획 대비 변경/추가 사항
계획서 자체는 그대로 구현했고, 아래는 계획에 없었지만 **구현·검증 과정에서 막혀서 추가로 필요했던**
항목들이다. 전부 사용자에게 확인 후 진행했다(§9의 `Monster_Ctrl` 처리 제외 전 재확인).

1. **`Assets/02. Scripts/TagOfChaos.Scripts.asmdef` 신규 추가.** 프로젝트에 `Assembly-CSharp`을 분리하는
   asmdef가 전혀 없어서, `Hero_Ctrl.cs`의 기존 컴파일 에러가 `HideOrSeekPlayer.cs`를 포함한 전체
   `Assembly-CSharp` 어셈블리 빌드를 막고 있었다(개별 파일 단위로는 에러가 없어도 Unity는 어셈블리
   단위로 빌드 성공/실패를 가른다). `Assets/02. Scripts/` 전체를 별도 어셈블리로 분리해 `Hero_Ctrl.cs`를
   건드리지 않고도 새 스크립트들이 독립적으로 컴파일·로드되게 했다.
2. **`Assets/Scripts/AnimState.cs`, `Assets/Scripts/Monster_Ctrl.cs` 최소 스텁 추가 (사용자 확인 후 진행).**
   Unity는 프로젝트 내 *어떤* 어셈블리에든 컴파일 에러가 있으면 Play Mode 진입 자체를 거부한다. §11
   검증(Play Mode 수동 확인)을 실제로 수행하려면 `Hero_Ctrl.cs`가 요구하던 미정의 타입 2개가 있어야 했다.
   - `AnimState`: `idle`/`move`/`jump`/`dodge`/`attack`/`skill` 6개 멤버를 가진 enum만 추가(로직 없음).
   - `Monster_Ctrl`: `TakeDamage(GameObject, float)` 시그니처만 가진 빈 `MonoBehaviour`만 추가(로직 없음).
   둘 다 `Hero_Ctrl.cs` 자체는 수정하지 않았고, `HideOrSeekPlayer`는 이 타입들을 전혀 참조하지 않는다
   (이동 전용 클래스이므로 §2.2 범위 밖).

### 12.3 검증 결과 (§11 대응)
1. **컴파일 에러 0건** — `read_console`로 최종 확인. `HideOrSeekPlayer`/`PlayerMoveState`/
   `PlayerGroundDetector`/`PlayerAnimationDriver`/`PlayerNetworkSync` 5개 파일 모두 별도 어셈블리에서
   독립적으로 컴파일 성공.
2. **Avatar 리타겟 확인** — `Idle.fbx`를 Humanoid + Create From This Model로 설정(Avatar `isHuman=True`,
   `isValid=True` 확인). `Walking`/`SneakWalking`/`Jumping`/`Dodge.fbx`는 Humanoid + Copy From Other
   Avatar로 `Idle.fbx`의 Avatar를 참조하도록 설정. 콘솔에 리타겟 경고 없음.
   - **제약 확인**: `Idle.fbx`의 Avatar 서브 에셋 이름을 `hide_or_seek_player`로 변경 시도했으나, Unity
     ModelImporter는 재임포트할 때마다 자동 생성된 Avatar 이름(`IdleAvatar`)으로 되돌린다 — 코드/UI
     어느 쪽으로도 영구히 리네임할 수 없는 것으로 확인됨(Unity 자체 제약). 대신 §12.4에서 만든 테스트
     캐릭터의 실제 GameObject 이름을 `hide_or_seek_player`로 지정해 명명 의도를 반영했다.
3. **`PlayerAnimator.controller` 구성 확인** — 코드로 직접 조회해 `Idle`(기본 상태)/`Walk`/`SneakWalk`/
   `Jump`/`Dodge` 5개 State가 각각 올바른 클립을 참조하고, 5개 Trigger 파라미터와 Any State 전이
   (`hasExitTime=false`)가 정확히 연결됐음을 확인. "parameter does not exist" 경고 없음.
4. **Play Mode 동작 확인** — `SampleScene`에 임시로 `hide_or_seek_player` GameObject를 만들어 `Animator`
   + `PhotonView` + `NavMeshAgent` + `HideOrSeekPlayer`를 부착하고 Play Mode 진입. `PhotonNetwork.OfflineMode`
   활성화 후 `pv.IsMine == true` 확인, `Update()`가 예외 없이 정상 실행됨을 확인(필드를 올바르게 연결한
   뒤에는 NullReferenceException 없음 — 초기 NRE는 `pv`를 Inspector로 연결하지 않은 임시 테스트 설정
   문제였고 스크립트 결함이 아니었음). 검증 후 이 임시 오브젝트들은 저장하지 않고 `LobbyScene`으로 다시
   전환해 정리했다(프로젝트에 남지 않음).
5. **(해결됨, 2026-08-13 후속 세션) 키보드 입력 검증**: 사용자 요청으로 `Assets/Scenes/PlayerTestScene.unity`에
   상시 테스트 환경(Ground+NavMesh, Camera, `hide_or_seek_player` 캐릭터, `OfflineModeBootstrap`)을
   구축했다. `Player Settings > Active Input Handling`도 `Both`(`activeInputHandler: 2`)로 변경했는데,
   실제로는 에디터 재시작 없이 즉시 반영되어(도메인 리로드만으로 충분했음) legacy `Input.GetAxisRaw`
   호출이 예외 없이 동작함을 Play Mode에서 직접 확인했다. §12.3-5의 "미검증" 상태는 해소됨.

### 12.4 남은 후속 확인 사항
- `Assets/Animation/Run.fbx.meta` 고아 파일은 계획대로 이번 작업에서 삭제하지 않고 그대로 두었다(§8.1).
- `Hero_Ctrl.cs` 삭제/교체, `Docs/Systems/Unit.md` 작성, 새 Input System 마이그레이션은 계획대로 이번
  범위에서 제외했다(§10).

### 12.5 상시 테스트 환경 (2026-08-13 후속 세션)
사용자 요청으로 `hide_or_seek_player` 테스트 환경을 씬으로 만들어 프로젝트에 남겨뒀다.
- `Assets/Scenes/PlayerTestScene.unity` — `Ground`(NavMesh 베이크 완료) + `Directional Light` +
  `Main Camera`(캐릭터를 비추도록 배치) + `hide_or_seek_player`(`Idle.fbx` 모델 + `Animator`
  (`PlayerAnimator.controller`) + `PhotonView` + `NavMeshAgent` + `HideOrSeekPlayer`, 필드 연결 완료) +
  `TestBootstrap`(`OfflineModeBootstrap` 부착, Play 시 자동으로 `PhotonNetwork.OfflineMode = true` 설정).
- `Assets/02. Scripts/Dev/OfflineModeBootstrap.cs` 신규 추가 — Photon 룸 없이도 `pv.IsMine`이 바로
  동작하게 하는 순수 테스트용 스크립트. `CLAUDE.md`의 "개발 도구 → Scripts/Dev/" 규칙을 따름.
- Play 버튼만 누르면 WASD/Shift/Space/Ctrl로 바로 조작 테스트가 가능하다.

### 12.6 버그 수정 — Walk/SneakWalk 모션이 재생 중 멈추는 현상 (2026-08-13)
**증상**: 사용자가 Play Mode에서 Walking 또는 SneakWalking 상태로 이동하다가 어느 순간 모션(애니메이션)이
멈추는 현상을 보고함.

**원인**: `Idle.fbx`/`Walking.fbx`/`SneakWalking.fbx`의 `AnimationClip`이 전부 `Loop Time`이 꺼진 채로
임포트되어 있었다(`ModelImporterClipAnimation.loopTime = false`, `clip.isLooping = false`). Mecanim은
루프가 꺼진 클립이 재생을 한 번 끝내면 그 상태에 계속 머물러 있더라도 마지막 프레임에서 애니메이션
재생을 멈춘다 — `Walking.fbx`(1초 분량)는 약 1초 뒤에, `SneakWalking.fbx`(4.58초 분량)는 약 4.58초 뒤에
정확히 이 현상이 나타난다. `HideOrSeekPlayer`/`PlayerAnimationDriver` 코드 자체의 결함이 아니라
애니메이션 클립 임포트 설정 누락이었다.

**수정**: `Idle.fbx`/`Walking.fbx`/`SneakWalking.fbx` 3개의 `ModelImporter.clipAnimations`에서
`loopTime = true`, `loopPose = true`로 설정 후 재임포트. `Jumping.fbx`/`Dodge.fbx`는 원샷(one-shot)
동작이 맞으므로 `loopTime = false`를 그대로 유지했다(점프는 `HandleJumpAnimationHold`가 별도로 정점
포즈를 붙잡아두는 방식이라 애초에 루프가 필요 없고, 회피는 `dodgeDuration` 동안만 재생되는 짧은 동작).

**검증**: `PlayerTestScene`에서 Play Mode 진입 후 `Animator.SetTrigger("Walk")`로 강제 전환하고
`AnimatorStateInfo.normalizedTime`을 두 차례 측정 — 17.25 → 22.55로 계속 증가함을 확인(1초짜리 클립이
22바퀴 넘게 정상적으로 반복 재생 중이며 멈추지 않음). `read_console`로 컴파일 에러 0건도 함께 재확인.

### 12.7 버그 수정 — Dodge 중 위치/회전 드리프트 (Root Motion 겹침) (2026-08-13)
사용자 요청으로 Jump/Dodge 모션을 Play Mode에서 정밀 검증하던 중 발견.

**Jump**: `PlayerGroundDetector`/`HandleJumpAnimationHold`가 설계대로 정확히 동작함을 확인. 상승 중
정상 재생 → `normalizedTime >= jumpFreezeNormalizedTime(0.5)`에서 `animator.speed = 0`으로 정확히
고정(공중에서 포즈 유지) → 착지 시 `speed = 1`로 즉시 재개, `isJump`/`keepMovingAfterJump` 초기화,
`NavMeshAgent.Warp` 재동기화까지 전부 이상 없음. 슬로우모션(`Time.timeScale = 0.05`)으로 재생해
`normalizedTime`/`y`좌표를 프레임 단위로 추적해 확인했다.

**Dodge에서 발견된 버그**: 회피 중 `dodgeMoveDir`이 `(0,0,1)`(정면 고정)인데도 캐릭터가 X축으로도
서서히 밀리고(`-0.3`~`-0.47` 관측), Y축 이론과 무관하게 흔들리며(`Y`가 `0.08`→`-0.13`까지 내려감),
회전도 의도치 않게 `8~9도` 정도 틀어지는 현상을 발견했다. `NavMeshAgent.updatePosition`을 꺼도
동일하게 재현되어 NavMeshAgent 문제는 아님을 확인했고, `Animator.applyRootMotion`이 `true`(Unity
기본값)로 켜져 있는 것이 원인으로 확인됐다 — `HideOrSeekPlayer.Move()`는 매 프레임
`transform.position +=`로 100% 수동으로 이동을 구동하는 설계인데(원래 `Hero_Ctrl`도 동일), 여기에
Mixamo 클립에 내장된 루트 본 모션이 `applyRootMotion=true` 때문에 추가로 겹쳐 적용되어 미세한
드리프트가 프레임마다 누적된 것이었다. 애니메이션 클립 자체나 `HideOrSeekPlayer`의 이동 로직 결함이
아니라, Animator 컴포넌트의 Root Motion 설정 누락이 원인.

**수정**: `HideOrSeekPlayer.Start()`에서 `animator.applyRootMotion = false`를 명시적으로 설정하도록
추가(`Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`). Inspector 설정에 의존하지 않고 코드에서 항상
강제하므로, 앞으로 이 컴포넌트를 어떤 프리팹/씬에 붙이더라도 Root Motion 겹침 버그가 재발하지 않는다.
`PlayerTestScene`의 `hide_or_seek_player` Animator도 Inspector 값 자체를 `Off`로 맞춰뒀다.

**검증**: 수정 후 동일한 방식(슬로우모션)으로 Dodge/Jump/Walk를 재현 — Dodge 중 `X`좌표가 정확히
`0.00`으로 고정되고 회전도 `(0,0,0)`을 그대로 유지함을 확인(드리프트 완전히 사라짐), 회피 종료 후
`speed`가 정확히 원래값(5)으로 복원되고 `Idle` 상태로 정상 전환됨도 재확인. Jump/Walk도 회귀 없이
동일하게 정상 동작함을 재확인. `read_console`로 컴파일 에러 0건 재확인.

---

## 13. Camera_Ctrl을 Main Camera에 부착 + 마우스 휠 줌 제거 ✅ (구현 완료)

상태: **구현 완료** (2026-08-14) — 13.10의 구현 순서 1~8번이 전부 완료됐다(9번은 `GameLobbyScene`이
아직 없어 대기 상태로 남음, 13.6 참고). §1~§12(`HideOrSeekPlayer` 리팩토링)과 별개의 작업으로, 맨 아래
남겨주신 주석("Camera_Ctrl을 Main Camera에 부착할 것이고, 그때 마우스 휠 줌인/아웃 기능은 빼서
`StampBrush()`의 붓 크기 조절에 쓰겠다")에서 시작해 여러 차례 주석을 거쳐 확정된 설계를 그대로
구현했다. 실제 작업 중 발생한 이슈와 세부 결과는 13.11 참고.

### 13.1 요청 사항 요약 ✅

- `Camera_Ctrl`(`Assets/Scripts/Camera_Ctrl.cs`)을 Main Camera에 부착한다.
- 부착과 동시에 `Camera_Ctrl`에서 **마우스 휠 줌인/아웃 기능을 제거**한다. 마우스 휠은 앞으로
  `GameScenePlan.md`(옛 `NetWorkPlan.md`) 5.2절의 `PlayerPaintCanvas.StampBrush()` 붓 크기 조절
  (`HandleBrushSizeInput()`, `Input.mouseScrollDelta.y` 사용)이 전담하므로, 카메라 줌과 입력이 겹치면
  안 된다는 취지로 이해했다.
- (13.3에서 추가 확정) `AnimState.cs`/`Monster_Ctrl.cs`도 `Hero_Ctrl.cs`와 함께 삭제한다.
- (13.3에서 추가 확정) `GameLobbyScene`에도 `GameScene`과 동일한 방법으로 `Camera_Ctrl`을 `Main Camera`에
  부착해야 한다.

### 13.2 조사 결과 — Camera_Ctrl의 현재 상태 ✅

- `Camera_Ctrl.cs`는 이미 존재하며, **`Assets/Scenes/PlayerTestScene.unity`의 `Main Camera`
  GameObject에 이미 컴포넌트로 부착되어 있다.** `m_Player` 필드도 인스펙터에서 `hide_or_seek_player`
  프리팹 인스턴스로 이미 직접(정적으로) 연결되어 있다(오프라인 단독 테스트용, §12.5 환경).
- 반면 `Assets/Scenes/SampleScene.unity`의 `Main Camera`에는 `Camera_Ctrl`이 없다(Camera +
  AudioListener만 존재). `GameManager`/`HeroSpawnPos`도 이 씬에서 찾지 못했다 — 옛 `Hero_Ctrl` +
  `GameManager.CreateHero()` 흐름이 실제로 동작하는 씬은 현재 저장소에 없는 것으로 보인다.
- 기존 `Hero_Ctrl.Awake()`(라인 67~80)는 `pv.IsMine`일 때 `Camera.main.GetComponent<Camera_Ctrl>()`을
  찾아 **런타임에 `InitCamera(this.gameObject)`를 호출**하는 동적 연결 방식이었다 — 여러 클라이언트가
  동시에 접속해도 각자 자기 카메라를 자기 캐릭터에 붙일 수 있는 멀티플레이 대응 패턴이다.
- 즉 현재 `PlayerTestScene`의 연결(인스펙터 고정값)과 원래 `Hero_Ctrl`의 연결(런타임 동적 호출)은
  **서로 다른 방식**이다. 고정값 방식은 1인 오프라인 테스트에만 맞고, 여러 명이 접속하는 실제 멀티플레이
  씬에서는 전원이 같은 카메라/같은 고정 인스턴스를 바라보게 되어 재사용할 수 없다.
- **추가 조사 (이번 주석 반영):** `PlayerTestScene`의 `hide_or_seek_player`는 사실 **정식 Prefab 에셋이
  아니다.** 프로젝트 전체를 뒤져봐도 `Assets/04. Prefabs/`는 물론 어디에도 `hide_or_seek_player`라는
  이름의 `.prefab` 파일이 없다 — 씬 YAML을 직접 확인해보니 이 오브젝트는 `Idle.fbx` 모델(Unity가 fbx를
  임포트하면 자동으로 생기는 "모델 프리팹")의 인스턴스에 `HideOrSeekPlayer`/`PhotonView`/
  `NavMeshAgent`/`Animator`를 얹어놓은 **씬 인스턴스**일 뿐이다. 이게 바로 "프리팹이라 정적 연결이 안
  된다"는 지적이 기술적으로 정확한 이유다 — 프리팹 에셋(모델 프리팹이든 나중에 만들 정식 프리팹이든)은
  **씬에만 존재하는 오브젝트(Main Camera)를 필드로 들고 있을 수 없다.** 게다가 멀티플레이에서는
  `PhotonNetwork.Instantiate`로 클라이언트마다 별도 인스턴스가 스폰되므로, 애초에 "이 카메라 하나"로
  고정할 방법 자체가 없다 — 각 클라이언트가 실행 시점에 **자기 자신의 `Camera.main`**을 찾아 연결하는
  수밖에 없다. → 13.5의 `HideOrSeekPlayer.Awake()` 내 런타임 조회 방식이 유일하게 맞는 해법임을 재확인.
- **`Hero_Ctrl` 삭제 영향 조사:** `Hero_Ctrl` 클래스를 참조하는 다른 파일은 없다(자기 자신 뿐).
  `GameManager.CreateHero()`는 `Hero_Ctrl` 타입을 직접 참조하지 않고 `PhotonNetwork.Instantiate
  ("HeroPrefab", ...)`처럼 **문자열로 리소스를 찾는 방식**인데, 정작 `HeroPrefab`이라는 에셋 파일 자체가
  프로젝트 어디에도 없다(찾아보니 애초에 깨진 참조였다) — 즉 `GameManager`의 스폰 로직은 `Hero_Ctrl`
  삭제와 무관하게 이미 동작하지 않는 상태였다. 다만 `AnimState.cs`/`Monster_Ctrl.cs`는 §12.2에서
  **오직 `Hero_Ctrl.cs`의 컴파일을 통과시키기 위한 최소 스텁**으로 추가된 것이라, 참조하는 파일이
  `Hero_Ctrl.cs` 하나뿐이다 — `Hero_Ctrl.cs`를 지우면 이 두 스텁은 더 이상 어디서도 쓰이지 않는
  고아 파일이 된다(13.6 참고).

### 13.3 결정 — B안 확정 + Hero_Ctrl 계열 제거 + GameLobbyScene까지 동일 구조 적용 ✅

주석으로 아래 5가지가 확정됐다:

1. **B안 확정.** `HideOrSeekPlayer`가 쓰이는 어떤 씬에서도 자기 소유 인스턴스가 실행 시점에
   `Camera.main`을 찾아 스스로 연결하는 동적 방식(§13.2의 기술적 이유로도 이 방식이 사실상 유일한
   선택지였음이 확인됨) — 13.5의 `HideOrSeekPlayer.Awake()`에 구현한다(구체적인 형태는 13.8-1에서
   `PlayerCameraBinder` 분리안을 포기하고 최종 확정됨).
2. **`Hero_Ctrl`은 더 이상 쓰지 않으므로 삭제한다.** §1/§10에서 "당분간 공존"으로 남겨뒀던 결정을
   뒤집는 것이다 — `HideOrSeekPlayer`로의 이전이 끝났고 카메라 연결까지 `HideOrSeekPlayer` 쪽에 붙는
   지금 시점부터는 `Hero_Ctrl`을 남겨둘 이유가 없다는 취지로 이해했다.
3. **(이번 주석으로 확정) `AnimState.cs`, `Monster_Ctrl.cs`도 `Hero_Ctrl.cs`와 함께 삭제한다.** 13.2
   마지막 항목에서 "확인 필요"로 남겨뒀던 항목 — §12.2에서 오직 `Hero_Ctrl.cs`의 컴파일을 통과시키기
   위한 최소 스텁으로 추가됐던 것이었으므로, `Hero_Ctrl.cs`가 없어지면 존재 이유도 함께 없어진다는
   판단으로 이해했다. 세부 삭제 범위는 13.7 참고.
4. **(이번 주석으로 신규 확정) `GameLobbyScene`에도 동일한 방식으로 `Camera_Ctrl`을 `Main Camera`에
   부착한다.** `GameScenePlan.md`(옛 `NetWorkPlan.md`) 8장에서 "게임 정상 종료 20초 후 전원이
   `GameLobbyScene`으로 이동한다"고 정의했던 그 씬이다 — 즉 색상 선택/술래잡기가 진행되는 실제 게임
   씬("GameScene", 지금은 `PlayerTestScene`이 그 프로토타입 역할을 겸하고 있음)에 붙였던 것과 **완전히
   동일한 두 요소(Main Camera의 `Camera_Ctrl` + 캐릭터 쪽 `HideOrSeekPlayer.Awake()`의 카메라 연결
   로직)를 그대로 재사용**하면 된다는 의미로 이해했다. 자세한 내용은 13.6 참고.
5. **(추가 주석으로 확정) `hide_or_seek_player`를 정식 Prefab 에셋으로 승격한다.** 13.2에서 발견한 대로
   지금은 씬 인스턴스일 뿐이었는데, `GameScene`/`GameLobbyScene` 두 곳에서 같은 캐릭터를 스폰해야 하므로
   프리팹 하나로 승격해 공유하기로 확정됐다. 세부 설계는 13.9 참고.

### 13.4 마우스 휠 줌 제거 (`Camera_Ctrl.cs`, 확정) ✅ 구현 완료

`LateUpdate()`가 매 프레임 `Input.GetAxis("Mouse ScrollWheel")`을 읽어 `m_TargetDistance`를
`minDist`(3.0)~`maxDist`(50.0) 사이로 바꾸고 `SmoothDamp`로 부드럽게 좁혀가는 방식이었다(현재 코드
100~112번 줄). 이 기능 전체와, 이 기능에만 쓰이던 필드(`zoomSpeed`, `minDist`, `maxDist`,
`m_TargetDistance`, `zoomSmoothTime`, `zoomVelocity`, `m_CurDistance`)를 제거하고, 카메라 거리는
`m_DefaultDist` 고정값 하나만 쓰도록 단순화한다. (더 이상 거리가 프레임마다 변하지 않으므로
`SmoothDamp` 보간 자체가 불필요해진다 — CLAUDE.md의 "최적화를 고려한 코드 작성" 원칙에도 부합)

**제거 대상 필드**

| 필드 | 비고 |
|---|---|
| `zoomSpeed` | 휠 감도 계수, 더 이상 사용 안 함 |
| `minDist`, `maxDist` | 줌 거리 제한, 더 이상 사용 안 함 |
| `m_TargetDistance` | 줌의 목표 거리, 더 이상 사용 안 함 |
| `m_CurDistance` | 줌의 현재(보간) 거리 — `m_DefaultDist`를 직접 쓰므로 별도 필드 불필요 |
| `zoomSmoothTime`, `zoomVelocity` | `SmoothDamp` 보간용 — 줌이 없어지므로 불필요 |

**유지 필드**: `m_DefaultDist`(5.2) — 이제 "초기값"이 아니라 **유일하고 변하지 않는 카메라 거리
고정값**이 된다.

**변경 후 예상 코드 (설계 스니펫, 아직 실제 파일에는 반영 안 함)**

```csharp
using UnityEngine;

public class Camera_Ctrl : MonoBehaviour
{
    [SerializeField] GameObject m_Player;
    Vector3 m_TargetPos = Vector3.zero;

    // --- 카메라 회전 관련 설정 ---
    float m_RotH = 0.0f;
    float m_RotV = 0.0f;
    float hSpeed = 5.0f;
    float vSpeed = 2.4f;
    float vMinLimit = -7.0f;
    float vMaxLimit = 80.0f;
    // --- 카메라 회전 관련 설정 ---
    // (줌 관련 필드 zoomSpeed/minDist/maxDist 삭제 — 마우스 휠은 이제 붓 크기 조절 전용)

    float m_DefaultRotH = 0.0f;
    float m_DefaultRotV = 25.0f;
    float m_DefaultDist = 5.2f; // 이제 유일한 카메라 거리 고정값 (더 이상 휠로 바뀌지 않음)

    Quaternion m_CurrentRotation;
    Quaternion m_TargetRotation;
    Vector3 m_BasicPos = Vector3.zero;
    Vector3 m_BuffPos = Vector3.zero;
    float rotationSmoothTime = 0.08f;
    // (줌 스무딩 필드 m_CurDistance/m_TargetDistance/zoomSmoothTime/zoomVelocity 삭제)

    public void InitCamera(GameObject player)
    {
        m_Player = player;
    }

    void Start()
    {
        if (m_Player == null) return;

        m_TargetPos = m_Player.transform.position;
        m_TargetPos.y += 1.4f;

        m_RotH = m_DefaultRotH;
        m_RotV = m_DefaultRotV;

        m_CurrentRotation = Quaternion.Euler(m_RotV, m_RotH, 0.0f);
        m_BasicPos = new Vector3(0f, 0f, -m_DefaultDist);

        m_BuffPos = m_TargetPos + (m_CurrentRotation * m_BasicPos);
        transform.position = m_BuffPos;
        transform.LookAt(m_TargetPos);
    }

    void LateUpdate()
    {
        if (m_Player == null) return;

        m_TargetPos = m_Player.transform.position;
        m_TargetPos.y += 1.4f;

        if (Input.GetMouseButton(1)) // 우클릭 드래그로 시점 회전은 그대로 유지
        {
            m_RotH += Input.GetAxis("Mouse X") * hSpeed;
            m_RotV -= Input.GetAxis("Mouse Y") * vSpeed;
            m_RotV = ClampAngle(m_RotV, vMinLimit, vMaxLimit);
        }

        m_TargetRotation = Quaternion.Euler(m_RotV, m_RotH, 0.0f);
        m_CurrentRotation = Quaternion.Slerp(m_CurrentRotation, m_TargetRotation,
                             Mathf.Clamp(Time.deltaTime / rotationSmoothTime, 0.0f, 1.0f));

        // 마우스 휠 줌 입력 처리 블록 전체 삭제
        // (Input.GetAxis("Mouse ScrollWheel") 읽기 + m_TargetDistance 갱신 + SmoothDamp 보간)
        // → 휠 입력은 색상 라운드 중 PlayerPaintCanvas.HandleBrushSizeInput()이 전담 (GameScenePlan.md 5.2)

        m_BasicPos.z = -m_DefaultDist; // 고정 거리, 더 이상 보간 불필요

        m_BuffPos = m_TargetPos + (m_CurrentRotation * m_BasicPos);
        transform.position = m_BuffPos;
        transform.LookAt(m_TargetPos);
    }

    float ClampAngle(float angle, float min, float max)
    {
        angle = Mathf.DeltaAngle(0.0f, angle);
        return Mathf.Clamp(angle, min, max);
    }
}
```

> 참고: `Input.mouseScrollDelta`(`PlayerPaintCanvas`가 씀)와 `Input.GetAxis("Mouse ScrollWheel")`
> (`Camera_Ctrl`이 쓰던 방식)은 같은 물리적 휠 입력을 읽는 서로 다른 API라 코드 레벨에서 자동으로
> 충돌하지는 않는다. 다만 두 기능이 동시에 살아있으면 "휠을 굴렸는데 카메라도 줌되고 붓 크기도
> 바뀌는" **사용자 경험상의 충돌**이 생기므로, 요청하신 대로 카메라 쪽 줌 자체를 완전히 제거하는
> 방향으로 설계했다(라운드 중에만 조건부로 끄는 방식은 채택하지 않음 — "제거할거야"라는 표현을
> 영구 삭제로 해석).

### 13.5 멀티플레이 대응 동적 연결 — `HideOrSeekPlayer.Awake()`에 직접 구현 (13.8-1에서 확정, 설계 변경) ✅ 구현 완료

**(이번 주석으로 확정) `PlayerCameraBinder` 별도 컴포넌트 안을 포기하고, `HideOrSeekPlayer.Awake()`
안에 카메라 연결 로직을 직접 넣는 방식으로 확정한다.** §2.2가 "별도 카메라 리그 컴포넌트가 책임
분리에 맞다"고 미리 적어둔 원칙을 뒤집는 결정이지만, 실제로 손으로 `HideOrSeekPlayer.cs`에 `Awake()`를
직접 추가해보신 방향(13.8에서 발견한 미완성 스텁)을 그대로 완성하는 쪽이 좋겠다는 판단으로 이해했다.
`Hero_Ctrl.Awake()`가 원래 하던 일(라인 67~80)과 동일한 자리에 동일한 방식으로 다시 넣는 셈이다.

```
Assets/02. Scripts/Unit/
└── HideOrSeekPlayer.cs   # 기존 파일의 Awake()를 완성 (새 파일 추가 없음)
```

**변경 후 예상 코드 (설계 스니펫, 아직 실제 파일에는 반영 안 함 — 13.8에서 발견된 미완성 스텁을
완성한 모습)**

```csharp
private void Awake()
{
    if (!pv.IsMine) return;

    Camera_Ctrl camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
    if (camCtrl != null)
        camCtrl.InitCamera(gameObject);
}
```

- 지금 코드에 있는 `if (pv.IsMine) { Camera_Ctrl a_CamCtrl = Camera.main.GetComponent<Camera_Ctrl>(); }`
  스텁에서 빠져 있던 `camCtrl.InitCamera(gameObject);` 호출만 채워 넣으면 된다(13.8-1 참고). `Camera.main`이
  아직 없는 극단적인 타이밍을 대비해 null 체크도 추가했다.
- `Hero_Ctrl.Awake()`가 하던 일 중 `id.text = ...`(닉네임 UI) 부분은 가져오지 않는다 — §2.2에서 이미
  UI는 이동/카메라 로직과 무관하다고 결론 낸 것과 동일한 이유(이 부분은 변경 없음).
- 이 방식을 쓰는 씬에서는 `Main Camera`에 `Camera_Ctrl`만 부착돼 있으면 되고, `m_Player`를 인스펙터에서
  미리 연결해 둘 필요가 없다(런타임에 자동 연결). **`Camera.main`을 실행 시점에 찾는 방식이라 씬을
  가리지 않는다** — 별도 컴포넌트가 아니라 `HideOrSeekPlayer` 안에 있어도 이 특성은 그대로라, 13.6에서
  다루는 `GameLobbyScene` 재사용 요구사항도 추가 코드 없이 그대로 만족된다.
- `PlayerTestScene`의 `Camera_Ctrl.m_Player`에는 아직 `hide_or_seek_player`가 정적으로 연결되어
  있음을 재확인했다(`m_Player: {fileID: 877421276}`) — `HideOrSeekPlayer.Awake()`가 런타임에 채워줄
  것이므로, 구현 시 이 인스펙터 값을 **비워서(None) 정적 연결을 제거**해야 한다(13.10의 구현 순서 참고).
- §4의 "책임 분리(협력 객체로 분리)" 원칙과는 다소 어긋나는 결정이지만, `Hero_Ctrl.Awake()`도 원래
  같은 방식(오케스트레이터 안에서 직접 처리)이었고 카메라 연결은 한 줄짜리 호출이라 별도 클래스로
  쪼갤 만큼의 복잡도가 아니라는 점에서 실용적인 선택으로 이해했다.

### 13.6 여러 씬 재사용 — `GameLobbyScene` 포함 (신규, 13.3-4 반영) ⏳ 설계 원칙만 확정, `GameLobbyScene` 생성 시점에 적용 대기

`GameScenePlan.md` 8장이 정의한 `GameLobbyScene`(게임 정상 종료 20초 후 전원이 모이는 씬)에도 실제
게임 씬과 동일한 카메라 추적이 필요하다는 요구사항이다.

- **왜 코드 추가 없이 재사용되는가**: 13.5의 `HideOrSeekPlayer.Awake()`는 특정 씬을 하드코딩해서 참조하지
  않고, `Awake()` 시점에 그 씬의 `Camera.main`을 찾는다. `Camera_Ctrl` 역시 `m_Player`를 인스펙터
  고정값이 아니라 `InitCamera(...)` 런타임 호출로만 받는다(13.4에서 줌 제거 후에도 이 구조는 그대로
  유지). 즉 "그 씬의 Main Camera에 `Camera_Ctrl`을 붙여두기만 하면, 그 씬에 스폰되는 `hide_or_seek_player`가
  자기 `Awake()`에서 알아서 찾아 연결하는" 설계라, 씬이 하나 늘어나도 `Camera_Ctrl.cs`/`HideOrSeekPlayer.cs`
  어느 쪽도 손댈 필요가 없다(13.5가 별도 컴포넌트에서 `HideOrSeekPlayer.Awake()` 내장 방식으로 바뀐
  뒤에도 이 재사용성 자체는 그대로 유지된다).
- **씬별로 필요한 작업(코드가 아니라 씬 구성)**:
  1. `GameLobbyScene`의 `Main Camera`에 `Camera_Ctrl` 컴포넌트를 부착 (`m_Player`는 비워둠 — 자동 연결)
  2. `GameLobbyScene`에 스폰되는 캐릭터에도 `HideOrSeekPlayer` + `PhotonView`가 붙어 있으면 충분함(13.5가
     `HideOrSeekPlayer.Awake()`에 통합됐으므로 별도 컴포넌트를 추가로 붙일 필요가 없어졌음)
- **현재 상태**: `GameLobbyScene`은 `GameScenePlan.md` 8장에서 이미 "추후 별도 구현" 범위 밖으로
  분류된 씬이라, 저장소에 아직 씬 파일 자체가 없다(`Assets/Scenes/`에는 `LobbyScene`/`SampleScene`/
  `PlayerTestScene`만 존재, 옛 `LobbyScene`과는 별개의 씬임에 주의). 따라서 이 절의 작업은 **`GameLobbyScene`이
  실제로 만들어지는 시점에** 위 1~2번을 적용하면 된다는 "설계 원칙 확정"이며, 지금 당장 손댈 파일은 없다.
- 두 씬(`GameScene`, `GameLobbyScene`) 모두 같은 캐릭터를 스폰해야 하는 것이 바로 13.3-5에서
  `hide_or_seek_player`를 정식 프리팹으로 승격하기로 확정한 이유이기도 하다 — 프리팹 하나만 만들어두면
  두 씬 모두 그 프리팹을 인스턴스화하는 것만으로 `HideOrSeekPlayer` + `PhotonView` 구성을 통째로
  재사용할 수 있다. 승격 설계는 13.9 참고.

### 13.7 `Hero_Ctrl` 및 연쇄 파일 제거 계획 ✅ 구현 완료

13.3에서 확정된 `Hero_Ctrl`/`AnimState`/`Monster_Ctrl` 삭제를 13.2의 조사 결과에 따라 구체화한다.

| 대상 | 처리 | 근거 |
|---|---|---|
| `Assets/Scripts/Hero_Ctrl.cs` | 삭제 | `HideOrSeekPlayer`로 이전 완료, 더 이상 쓰지 않음(13.3-2) |
| `Assets/Scripts/AnimState.cs` | **삭제 (13.3-3에서 확정)** | `Hero_Ctrl.cs`만을 위한 §12.2 스텁이었고, 참조하는 파일이 `Hero_Ctrl.cs` 하나뿐이라 함께 삭제해도 다른 곳이 깨지지 않음 |
| `Assets/Scripts/Monster_Ctrl.cs` | **삭제 (13.3-3에서 확정)** | 위와 동일한 이유(§12.2 스텁) |
| `Assets/Scripts/GameManager.cs`의 `CreateHero()` | 변경 안 함(이번 범위 밖) | `"HeroPrefab"` 리소스 자체가 이미 존재하지 않아 `Hero_Ctrl` 삭제와 무관하게 원래도 동작하지 않던 코드다. `HideOrSeekPlayer`를 스폰하도록 갈아끼우는 작업은 별도 후속 작업으로 남긴다(§10 연장선) |
| `Assets/02. Scripts/TagOfChaos.Scripts.asmdef`(§12.2에서 추가) | 변경 안 함(그대로 유지 권장) | 원래 `Hero_Ctrl.cs`의 컴파일 에러로부터 `Assets/02. Scripts/`를 격리하려고 추가한 것이라 그 이유는 사라지지만, `Assets/02. Scripts/`를 별도 어셈블리로 두는 것 자체는 여전히 좋은 관례라 유지해도 무방하다고 판단 |

### 13.8 작업 중 발견 및 확정 — 이미 손으로 반영된 변경사항 (계획과 대조) ✅

이 절이 아직 "승인 대기" 상태인데도, 실제 작업 폴더(`git status`)를 확인해보니 코드에 몇 가지가 이미
직접 반영되어 있었다. 무엇이 바뀌었는지 조사해서 정리했고, 그 조사 결과에 달린 주석으로 3가지가 모두
확정됐다. **아래는 조사 + 확정된 결정일 뿐이며, 이 내용을 이유로 추가 구현은 하지 않았다.**

| 파일 | 실제로 이미 바뀐 내용 | 계획(13.x) 대비 상태 |
|---|---|---|
| `Camera_Ctrl.cs`(+`.meta`) | `Assets/Scripts/` → `Assets/02. Scripts/Unit/`로 이동됨 | 계획에 없던 이동이었고, **임의로 옮긴 것**이었음이 이번 주석으로 확인됨 → 13.8-2에서 최종 위치를 다시 확정 |
| `HideOrSeekPlayer.cs` | `Awake()`가 새로 추가됨: `if (pv.IsMine) { Camera_Ctrl a_CamCtrl = Camera.main.GetComponent<Camera_Ctrl>(); }` — 참조만 가져오고 아무 것도 하지 않음(`InitCamera` 호출 없음) | 13.5에서 설계한 별도 컴포넌트 `PlayerCameraBinder` 대신 `HideOrSeekPlayer` 안에 직접 로직을 넣는 방향 → 13.8-1에서 확정, 13.5에 반영 완료 |
| `Hero_Ctrl.cs` | `Awake()`에서 `a_CamCtrl.InitCamera(this.gameObject);` 호출 줄만 삭제, 나머지는 그대로(파일 자체는 삭제되지 않음) | 13.3-2/13.7의 "삭제" 결정을 향해 가는 중간 상태였음 → 13.8-3에서 삭제 결정 재확인됨, 이 중간 편집 상태는 어차피 파일 전체가 삭제되므로 더 손볼 필요 없음 |
| `GameManager.cs` | `CreateHero()` → `CreatePlayer()`로 메서드 이름만 변경, 내부 로직은 그대로 | 13.7에서 "이번 범위 밖"으로 남겨뒀던 항목을 미리 건드리기 시작한 것으로 보임. 이번 주석에서 별도 언급이 없어 13.7의 결정(변경 안 함, 범위 밖)을 그대로 유지 |
| `Camera_Ctrl.cs`의 줌 로직 | 변경 없음(원본 그대로) | 13.4는 아직 반영 전 — 계획대로 대기 중 |

**이번 주석으로 확정된 결정 3가지:**

1. **`PlayerCameraBinder` 포기, `HideOrSeekPlayer.Awake()`에 직접 구현.** "후자(별도 컴포넌트)를 포기하고
   전자(`HideOrSeekPlayer.Awake()` 직접 구현)로 가는 게 좋겠다"는 확인을 받았다. 13.5를 이 방향으로
   다시 작성했다 — 새 파일을 추가하지 않고, 이미 손으로 추가된 `Awake()` 스텁에 빠진
   `InitCamera(gameObject)` 호출 한 줄만 채우면 된다.
2. **`Camera_Ctrl.cs`의 `Assets/02. Scripts/Unit/` 이동은 임의였고, 정식 도메인 폴더로 분리해야 한다.**
   "내가 임의로 옮긴 것이다. 별도 컴포넌트로 분리가 필요하다"는 확인을 그대로 반영해, `Unit` 도메인에
   묶어두지 않고 카메라 전용의 새 도메인 폴더로 옮기기로 했다:
   ```
   Assets/02. Scripts/Camera/
   └── Camera_Ctrl.cs   # (+ .meta) Assets/02. Scripts/Unit/에서 이곳으로 재배치
   ```
   `CLAUDE.md`의 `Scripts → Assets/02. Scripts/{도메인}/` 규칙을 따른 것이다. `Camera_Ctrl`은 특정
   유닛(플레이어) 전용 로직이 아니라 "카메라를 어떻게 움직일지"라는 독립된 관심사라 `Unit`과 나란히 두는
   `Camera` 도메인이 맞다고 판단했다. 이 재배치에 맞춰 13.4의 코드 스니펫과 13.5의 설명이 가리키는
   경로도 `Assets/02. Scripts/Camera/Camera_Ctrl.cs`로 갱신됐다(13.4/13.5 본문 참고, 스니펫 내용 자체는
   위치와 무관하므로 코드는 그대로).
3. **`Hero_Ctrl.cs`, `AnimState.cs`, `Monster_Ctrl.cs` 삭제 재확정.** "삭제를 요청한다"는 확인으로,
   13.3-2/13.3-3/13.7에서 이미 확정했던 삭제 결정에 이견이 없음을 재확인했다 — 13.7의 표는 변경 없이
   그대로 유효하다.

13.3에서 열려있던 "`hide_or_seek_player`를 정식 프리팹으로 승격할지" 질문은 이번 절 이후 별도 주석으로
확정됐다 — 13.9 참고.

### 13.9 `hide_or_seek_player` 정식 프리팹 승격 (신규, 확정) ✅ 구현 완료

**(추가 주석으로 확정) `hide_or_seek_player`를 정식 Prefab 에셋으로 승격한다.** 13.3-5/13.6에서
근거를 남겨둔 대로, `GameScene`(지금은 `PlayerTestScene`이 프로토타입 역할)과 `GameLobbyScene` 두
곳에서 같은 캐릭터를 스폰해야 하므로, 씬마다 손으로 컴포넌트를 얹는 대신 프리팹 하나를 공유하는 쪽으로
확정됐다.

**저장 경로 (확정):**

```
Assets/04. Prefabs/Resources/
└── HideOrSeekPlayer.prefab
```

`CLAUDE.md`의 `Prefabs → Assets/04. Prefabs/` 규칙과, Photon의 `PhotonNetwork.Instantiate(...)`가
**반드시 어떤 `Resources` 폴더 밑에 있는 프리팹만 이름으로 찾을 수 있다**는 기술적 제약(3.2/13.7에서
확인했듯 `GameManager`가 결국 이 프리팹을 이런 방식으로 스폰하게 될 것이므로)을 절충한 경로다.
`Assets/04. Prefabs/` 밑에 두면서 그 안에 `Resources/` 서브폴더를 하나 둬서, "프리팹은 여기"라는
CLAUDE.md 규칙과 "Photon이 찾을 수 있어야 한다"는 제약을 동시에 만족시켰다. **이 경로가 맞다고
확인받았다.**

**승격 절차 (Unity 에디터 작업, 아직 실행하지 않음):**

1. `PlayerTestScene`의 `hide_or_seek_player` GameObject(이미 `HideOrSeekPlayer` + `PhotonView` +
   `NavMeshAgent` + `Animator`(`PlayerAnimator.controller`)가 전부 연결되어 있음, §12.5)를 그대로
   `Assets/04. Prefabs/Resources/` 폴더로 드래그해 프리팹 에셋을 새로 만든다. Unity가 자동으로 씬
   인스턴스를 이 프리팹의 인스턴스로 전환해준다(컴포넌트 설정을 다시 손으로 옮길 필요 없음).
2. 프리팹에 13.5에서 완성한 `HideOrSeekPlayer.Awake()`의 카메라 연결 로직이 그대로 포함되므로, 이
   프리팹을 인스턴스화하는 어떤 씬이든 자기 소유 캐릭터가 자동으로 그 씬의 `Main Camera`를 찾아 연결한다
   (13.5/13.6에서 설명한 "씬을 가리지 않는" 특성이 프리팹 승격 이후에도 그대로 유지됨).
3. `GameLobbyScene`이 실제로 만들어지면(13.6), 이 프리팹을 그대로 인스턴스화하기만 하면 된다 — 새로
   컴포넌트를 조립할 필요가 없다.

**범위 밖으로 남겨두는 것:** `GameManager.CreateHero()`(현재 `CreatePlayer()`로 이름만 바뀐 상태,
13.8 표)를 이 프리팹을 스폰하도록 실제로 갈아끼우는 작업은 13.7에서 이미 "범위 밖"으로 분류해뒀다 —
이 프리팹을 만들어두는 것은 그 후속 작업의 전제 조건을 미리 준비해두는 것일 뿐, `GameManager` 자체는
이번에도 손대지 않는다.

### 13.10 구현 순서 제안 (승인 후 진행, 13.8/13.9 반영 최신화) ✅ 1~8번 완료, 9번은 `GameLobbyScene` 생성 대기

1. `Camera_Ctrl.cs`(+`.meta`)를 `Assets/02. Scripts/Unit/`에서 `Assets/02. Scripts/Camera/`로 재배치 (13.8-2)
2. `Camera_Ctrl.cs`에서 줌 관련 필드/로직 제거 (13.4)
3. `Assets/Scripts/Hero_Ctrl.cs`, `AnimState.cs`, `Monster_Ctrl.cs` 삭제 (13.7, 13.8-3)
4. `HideOrSeekPlayer.cs`의 기존 `Awake()` 스텁에 `camCtrl.InitCamera(gameObject);` 호출을 채워 완성 (13.5,
   새 파일 추가 없음)
5. `PlayerTestScene`의 `Camera_Ctrl` 인스펙터에서 `m_Player` 정적 참조를 None으로 비움 (13.5)
6. `PlayerTestScene`의 `hide_or_seek_player`를 `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab`로
   승격 (13.9)
7. `PlayerTestScene`에서 Play Mode로 확인: `HideOrSeekPlayer.Awake()`가 자동으로 카메라를 연결하는지,
   우클릭 드래그 회전은 기존과 동일하게 동작하는지, 마우스 휠을 굴려도 카메라 거리가 더 이상 변하지
   않는지 확인
8. `read_console`로 컴파일 에러 0건 확인 (특히 `Hero_Ctrl`/`AnimState`/`Monster_Ctrl` 삭제, `Camera_Ctrl`
   재배치, 프리팹 승격 후 다른 곳에서 깨지는 참조가 없는지)
9. `GameLobbyScene`이 실제로 만들어지는 시점에 13.6의 1~2번을 적용 — `Main Camera`에 `Camera_Ctrl`
   부착, 캐릭터는 13.9의 `HideOrSeekPlayer.prefab`을 그대로 인스턴스화 — 지금은 씬이 없어 대기 상태로
   남겨둠

---

### 13.11 구현 완료 보고 (2026-08-14)

Unity MCP로 직접 실행했다. `manage_asset(action="move")`로 `Camera_Ctrl.cs`의 GUID를 보존하며
재배치해서, 씬에 이미 붙어있던 `Camera_Ctrl` 컴포넌트 참조가 깨지지 않았다.

**변경된 파일**
- `Assets/02. Scripts/Camera/Camera_Ctrl.cs` — `Assets/02. Scripts/Unit/`에서 이동(GUID 보존) + 줌 관련
  필드/로직(`zoomSpeed`, `minDist`, `maxDist`, `m_TargetDistance`, `m_CurDistance`, `zoomSmoothTime`,
  `zoomVelocity`, 휠 입력 처리 블록) 전부 제거, `m_DefaultDist` 고정 거리만 사용하도록 단순화(13.4).
- `Assets/02. Scripts/Unit/HideOrSeekPlayer.cs` — `Awake()`를 13.5 설계대로 완성(`Camera.main` null 체크
  + `InitCamera` 호출).
- `Assets/Scripts/Hero_Ctrl.cs`, `Assets/Scripts/AnimState.cs`, `Assets/Scripts/Monster_Ctrl.cs` — 삭제
  (13.7). 삭제 전 두 스텁 파일의 GUID를 씬/프리팹 전체에서 grep해 다른 곳에서 참조하지 않음을 재확인한
  뒤 진행했다.
- `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab` — 신규 생성(13.9). `PlayerTestScene`의
  `hide_or_seek_player`(원래 `Idle.fbx` 모델 프리팹의 인스턴스였음)를 `unlink_if_instance`로 모델
  프리팹 연결을 끊고 정식 프리팹으로 승격했다. 루트 컴포넌트: `Transform`/`Animator`/`PhotonView`/
  `NavMeshAgent`/`HideOrSeekPlayer`.
- `Assets/Scenes/PlayerTestScene.unity` — `Main Camera`의 `Camera_Ctrl.m_Player` 정적 참조를 None으로
  비움(13.5), `hide_or_seek_player` 인스턴스가 새 프리팹에 연결된 상태로 저장.

**검증 결과**
- 각 변경 직후 `read_console`로 컴파일 에러 0건을 반복 확인(스크립트 이동, Hero_Ctrl 계열 삭제,
  Awake 수정, 프리팹 승격 이후 각각 별도 확인).
- Play Mode 진입 후 `Camera_Ctrl.m_Player`가 런타임에 `hide_or_seek_player`로 자동 채워지는 것을
  컴포넌트 리소스로 확인 — `HideOrSeekPlayer.Awake()`의 동적 연결이 실제로 동작함을 검증.
- `Main Camera` 기준 스크린샷으로 카메라가 캐릭터를 정상적으로 프레이밍하는 것을 육안 확인(검증
  스크린샷은 확인 후 삭제, 프로젝트에 남기지 않음).
- 마우스 휠 조작에 의한 줌 코드 자체가 파일에서 완전히 제거됐으므로, 휠을 굴려도 카메라 거리가
  바뀌지 않는다(코드 레벨로 보장됨 — 로직이 없으므로 별도 조작 테스트 불필요).

**남겨둔 것 (계획대로)**
- `GameLobbyScene`은 아직 만들어지지 않아 13.6/13.10-9는 계속 대기 상태.
- `GameManager.CreatePlayer()`를 이 프리팹으로 스폰하도록 갈아끼우는 작업은 13.7에서 이미 범위 밖으로
  분류했으므로 손대지 않았다.

---

## 14. 버그: Dodge 모션이 한 번 눌렀을 때 끝까지 재생되지 않음 — 🔎 원인 분석 완료, 구현 대기

> 사용자 보고: "지금 Dodge 모션이 한번 눌렀을 때 끝까지 모션이 실행이 안된다." 아래는
> `HideOrSeekPlayer.cs`/`PlayerAnimationDriver.cs`와 `PlayerAnimator.controller`를 직접 조사해서
> 확인한 원인과 수정 계획이다. **지시에 따라 이번에는 계획만 정리했고 실제 수정은 하지 않았다.**

### 14.1 증상

회피(Dodge, `LeftControl`) 입력 시 캐릭터가 순간적으로 튀어나가긴 하지만, `Dodge.fbx` 모션이
끝까지 재생되지 못하고 도중에 잘려서 `Idle`/`Walk` 자세로 갑자기 바뀐다.

### 14.2 원인 (코드/에셋 실측)

`Assets/Animation/PlayerAnimator.controller`를 코드로 직접 조회해 확인한 수치:

| 항목 | 값 |
|---|---|
| `Dodge` 애니메이터 상태의 모션(`Dodge.fbx`의 `Dodge` 클립) 실제 길이 | **1.633초** (`isLooping=False`, 원샷 — §12.6에서 의도한 대로) |
| `HideOrSeekPlayer.dodgeDuration`(회피 로직 타이머, `Assets/02. Scripts/Unit/HideOrSeekPlayer.cs:13`) | **0.5초** |
| Any State → `Idle`/`Walk`/`SneakWalk` 전이 설정 | `hasExitTime=False`, `duration=0.1`(즉시 반응이 설계 의도, §8.3) |

`CheckDodgeInput()`(`HideOrSeekPlayer.cs:169~191`)의 흐름:

```csharp
// LeftControl을 누른 프레임
dodgeMoveDir = rotation;
isDodge = true;
keepMovingAfterDodge = true;
dodgeTimer = dodgeDuration; // 0.5초
animationDriver.ChangeState(PlayerMoveState.Dodge); // Dodge 트리거 발동, 클립 재생 시작(총 1.633초)

// 이후 매 프레임
dodgeTimer -= Time.deltaTime;
if (dodgeTimer <= 0f) DodgeOut(); // 0.5초 뒤 isDodge = false
```

`DodgeOut()`이 `isDodge`를 `false`로 내리면, **바로 다음 프레임**의 `CheckMovementInput()`이
`!isJump && !isDodge` 조건을 통과해 `animationDriver.ChangeState(Walk 또는 Idle)`을 호출한다.
`PlayerAnimator.controller`의 Any State 전이는 `hasExitTime=False`(위 표)라서 트리거가 켜지는 즉시
(0.1초 블렌드로) 지금 재생 중이던 `Dodge` 클립을 밀어낸다.

**결론**: 회피 버튼을 누르면 1.633초짜리 `Dodge` 클립이 재생을 시작하지만, 정확히 **0.5초 지점
(전체 재생의 약 30.6%)**에서 `dodgeTimer`가 만료되어 강제로 `Idle`/`Walk`로 전환된다 — 나머지
약 69%(회피 동작 후반부, 복귀 자세 등)는 한 번도 재생되지 못한다. 이것이 "한 번 눌렀을 때 끝까지
실행이 안 된다"는 증상의 정확한 원인이다.

**참고**: `Jump`도 구조적으로 동일한 위험(원샷 클립 + Any State 즉시 전이)을 갖고 있지만,
`PlayerAnimationDriver.HandleJumpAnimationHold()`(§6, §12.7)가 "착지 전까지는 정점 포즈에서
재생을 멈춰 붙잡아두는" 별도 안전장치를 이미 갖고 있어서 문제가 드러나지 않았다. **Dodge에는
이 Jump가 가진 것과 동등한 안전장치가 없다는 것이 이 버그의 근본 설계 결함이다.**

또한 `dodgeDuration`(0.5초)은 애초에 "회피 중 캐릭터를 강제로 미는 이동(대시 슬라이드) 지속시간"을
튜닝하기 위한 값으로 보인다 — `Dodge.fbx`의 실제 길이를 반영해서 정해진 값이 아니다. 즉 **서로
다른 두 관심사(① 얼마나 오래 강제로 밀어붙일지 ② 애니메이션이 얼마나 재생돼야 하는지)가 우연히
하나의 타이머(`dodgeTimer`)에 묶여 있던 것**이 근본 원인이다.

### 14.3 수정 방안 검토

**방안 A — `dodgeDuration`을 클립 길이(1.633초)에 맞춰 늘린다.**
- 장점: 필드 기본값 하나만 바꾸면 되는 최소 변경.
- 단점: 회피 중 "강제 이동(대시 슬라이드)"도 똑같이 1.6초 넘게 지속되어, 원래 순간적인 대시
  느낌(0.5초)이 사라지고 캐릭터가 너무 오래 미끄러지는 것처럼 느껴질 수 있다 — 이동감(게임
  디자인)과 애니메이션 길이(에셋)를 여전히 한 값에 묶어두는 것이라, 나중에 `Dodge.fbx`가 다른
  클립으로 교체되면 똑같은 버그가 재발할 수 있는 매직 넘버 문제도 그대로 남는다.

**방안 B (권장) — "이동 지속시간"과 "애니메이션 재생 완료"를 서로 다른 조건으로 분리한다.**
- `dodgeDuration`(0.5초)·`keepMovingAfterDodge`는 그대로 두어 **강제 슬라이드 이동**만 원래
  튜닝값대로 0.5초에 끝낸다(대시 이동감 변경 없음).
- **애니메이션 상태 전환**은 Jump의 `HandleJumpAnimationHold()`와 대칭되는 새 메서드로,
  "Dodge 애니메이터 상태의 `normalizedTime`이 1.0 이상이 될 때까지" `Idle`/`Walk`로의 전환을
  보류시킨다. Jump에 이미 있는 `state.IsName(...) + normalizedTime` 패턴을 그대로 재사용하므로
  코드 스타일도 일관된다.
- 장점: "빠른 대시감(0.5초)"과 "회피 모션 완주(1.633초)"를 둘 다 원래 의도대로 만족. `normalizedTime`
  기반이라 `Dodge.fbx`가 나중에 교체돼도(클립 길이가 바뀌어도) 다시 어긋나지 않는다.
- 단점: 방안 A보다 변경 범위가 조금 더 넓다(새 필드 1개, 새 메서드 1개).

**결론: 방안 B로 진행한다.** Jump에 이미 검증된 것과 동일한 패턴을 재사용해 일관성을 유지하면서,
이동감과 애니메이션 완주를 모두 만족하는 유일한 방법이기 때문이다.

### 14.4 상세 구현 계획 (미구현 — 설계 스니펫만)

**`PlayerAnimationDriver.cs`에 Jump와 대칭되는 메서드 추가:**

```csharp
// Dodge 애니메이션이 끝까지(정지 포즈 전까지) 재생됐는지 여부.
// HandleJumpAnimationHold()와 대칭되는 역할이지만, Dodge는 "붙잡아두기"가 아니라
// "다른 상태로 못 넘어가게 막는 조건 조회"만 하면 되므로 반환값 있는 조회 메서드로 둔다.
public bool IsDodgeAnimationFinished()
{
    if (animator == null || currentState != PlayerMoveState.Dodge)
        return true; // Dodge 상태가 아니면 막을 이유가 없음

    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
    return state.IsName("Dodge") && state.normalizedTime >= 1f;
}
```

**`HideOrSeekPlayer.cs` 변경:**

1. 새 상태 플래그 추가: `private bool isDodgeAnimationPlaying;`
2. `CheckDodgeInput()`의 회피 시작 블록에서 `isDodgeAnimationPlaying = true;`도 함께 세팅
   (`isDodge = true;` 옆).
3. `DodgeOut()`은 이동 관련 필드만 원복하고(`speed`, `isDodge`, `keepMovingAfterDodge`, `rotation`),
   `isDodgeAnimationPlaying`은 건드리지 않는다(애니메이션은 아직 안 끝났을 수 있으므로).
4. `CheckDodgeInput()`의 재입력 가드도 `!isJump && !isDodge`에 `&& !isDodgeAnimationPlaying`을
   추가한다 — 회피 애니메이션이 실제로 끝나기 전에 회피를 다시 발동해 클립이 또 잘리는 것을
   방지(§14.2에서 지적한 문제가 연타로 재발하지 않도록).
5. `CheckMovementInput()`의 두 `if (!isJump && !isDodge)` 조건(이동 시/정지 시 각각 1곳, 총 2곳)에
   `&& !isDodgeAnimationPlaying`을 추가해, 회피 모션이 재생 중일 때는 `Idle`/`Walk`/`SneakWalk`로
   강제 전환되지 않게 한다.
6. `Update()`에서 `animationDriver.HandleJumpAnimationHold();` 옆에 아래를 추가:
   ```csharp
   if (isDodgeAnimationPlaying && animationDriver.IsDodgeAnimationFinished())
       isDodgeAnimationPlaying = false;
   ```
   이 프레임 이후부터는 `CheckMovementInput()`이 다시 `Idle`/`Walk`로 자연스럽게 전환할 수 있다.

**영향받지 않는 것**: `PlayerAnimator.controller`의 트리거/전이 설정(§8.3)은 변경하지 않는다 —
문제는 애니메이터 쪽이 아니라 C# 쪽에서 너무 일찍 트리거를 바꿔버리는 것이었으므로, 트리거를
"언제 호출하느냐"만 늦추면 충분하다. `Jump` 관련 로직, `PlayerNetworkSync`, `PlayerGroundDetector`도
변경 없음.

### 14.5 검증 계획 (미실행)

1. `read_console`로 컴파일 에러 0건 확인.
2. `PlayerTestScene`에서 Play Mode 진입 후 슬로우모션(`Time.timeScale`을 낮춰서, §12.7에서 쓴
   방식과 동일)으로 Dodge를 발동해 `AnimatorStateInfo.normalizedTime`을 프레임 단위로 추적 —
   0 → 1.0까지 끊기지 않고 도달하는지(즉 `Dodge` 클립이 실제로 끝까지 재생되는지) 확인.
3. 회피 중 강제 이동(대시 슬라이드)이 여전히 기존과 동일하게 약 0.5초만 지속되는지 확인(방안 B가
   이동감을 바꾸지 않는다는 것을 검증 — §14.3의 장점 확인).
4. 회피 애니메이션이 채 끝나기 전에 `LeftControl`을 연타해도 클립이 다시 끊기지 않는지(§14.4-4의
   재입력 가드) 확인.
5. Walk/SneakWalk/Jump가 기존과 동일하게 회귀 없이 동작하는지 재확인(§12.6/§12.7에서 이미 검증된
   부분이 이번 변경으로 깨지지 않았는지).

### 14.6 상태

**원인 분석 완료(수치로 확인: Dodge 클립 1.633초 vs `dodgeDuration` 0.5초). 구현 대기 중** —
사용자 지시에 따라 이번에는 계획만 정리했고, 실제 수정(`PlayerAnimationDriver`/`HideOrSeekPlayer`
변경 및 검증)은 진행하지 않았다. **→ B안 적용 시의 부작용은 §15에서 추가로 조사했다.**

---

## 15. B안(§14.3) 적용 시 예상되는 문제점 — 🔎 조사 완료, 미구현

> 사용자 요청: "B안으로 했을 경우 문제가 발생할 수 있는게 있는지에 대해 상세히 파악해줘." 코드를
> 다시 정독하고 `PlayerAnimator.controller`의 State/Transition 구조까지 재확인해서 찾아낸 문제들이다.
> **역시 지시에 따라 계획만 정리했고 실제 수정은 하지 않았다.**

먼저 구조적으로 확인한 사실: `PlayerAnimator.controller`의 5개 State(`Idle`/`Walk`/`SneakWalk`/
`Jump`/`Dodge`)는 전부 **자체 Outgoing Transition이 0개**다(직접 조회로 재확인). 즉 상태 전환은
Any State 전이(§14.2 표) 외에는 발생하지 않고, Any State 전이는 오직 C# 코드가
`Animator.SetTrigger(...)`를 호출할 때만 열린다 — 다시 말해 **Dodge 애니메이션을 끊을 수 있는
경로는 100% `PlayerAnimationDriver.ChangeState()` 호출 지점뿐**이다. 아래 문제들은 전부 이
전제 위에서, §14.4의 계획이 `ChangeState()` 호출 지점을 빠짐없이 막고 있는지를 다시 추적한
결과다.

### 15.1 [가장 심각] 애니메이션은 잠겨 있는데 이동/회전은 그대로 자유로워짐

`HideOrSeekPlayer.CheckMovementInput()`의 실제 코드를 다시 보면:

```csharp
if (moveDir != Vector3.zero)
{
    rotation = moveDir;        // ← isDodge/isDodgeAnimationPlaying와 무관하게 무조건 갱신됨
    rotation_value = rotation;

    if (isDodge)
        rotation = dodgeRotation;

    if (!isJump && !isDodge)   // §14.4 계획은 이 줄에만 && !isDodgeAnimationPlaying을 추가함
    {
        ...
        animationDriver.ChangeState(...);
    }
}
```

§14.4의 계획은 `animationDriver.ChangeState(...)` 호출을 감싸는 `if` 조건에만
`!isDodgeAnimationPlaying`을 추가하는 것이었다 — 그런데 **`rotation = moveDir;` 대입 자체는
그 바깥에서 무조건 실행된다.** `dodgeTimer`가 만료되는 0.5초 시점에 `isDodge`는 이미 `false`가
되므로(§14.2), `Move()`의 분기 선택(`isDodge && keepMovingAfterDodge` → 거짓)도 즉시 "일반 이동"
분기로 넘어가 버린다. 즉:

- **0~0.5초**: 강제 슬라이드 이동 + `Dodge` 애니메이션 재생 (의도대로 일치)
- **0.5~1.633초 (§14.4 적용 후 새로 생기는 구간)**: `isDodgeAnimationPlaying`만 `true`라서
  애니메이션 **트리거만** `Dodge`에 묶여 있을 뿐, 캐릭터는 `Move()`의 일반 이동 분기(`else`)를 타고
  **완전히 자유로운 속도/방향으로 움직이고 `transform.LookAt()`으로 회전까지 자유롭게 바뀐다.**
  거기에 `applyRootMotion = false`(§12.7)라 클립 내장 이동은 애초에 무시되므로, 화면에는 "구르는
  자세로 고정된 채 마음대로 걸어 다니고 방향을 트는" 것처럼 보이는 새로운 시각적 버그가 생긴다.

**결론**: §14.4의 계획은 "언제 애니메이션 트리거가 바뀌는지"만 막았을 뿐, "언제 이동/회전 입력이
다시 자유로워지는지"는 막지 않았다 — 이 둘을 같이 잠그지 않으면, 원래 버그(모션이 잘림)는
없어지지만 대신 "모션과 실제 움직임이 따로 노는" 다른 버그로 바뀔 뿐이다. B안을 실제로 완성하려면
`rotation`/`rotation_value` 갱신(및 `Move()`의 분기 선택)도 `isDodgeAnimationPlaying`이 풀리기
전까지 함께 억제해야 한다 — 그런데 이렇게 하면 사실상 "조작 잠금" 자체가 0.5초가 아니라 1.633초까지
늘어나는 셈이라, §14.3에서 B안의 장점으로 내세웠던 "대시 이동감(0.5초)은 그대로 유지"라는 전제가
**이동만이 아니라 회전까지 포함하면 이미 지켜지지 않고 있었다**는 뜻이기도 하다(§15.6에서 절충안
검토).

### 15.2 `CheckJumpInput()`에는 새 가드가 반영되지 않음 — 점프로 우회하면 버그가 그대로 재발

`CheckJumpInput()`의 현재 가드는 `!isJump && !isDodge`뿐이다:

```csharp
if (Input.GetKeyDown(KeyCode.Space) && !isJump && !isDodge)
{
    ...
    animationDriver.ChangeState(PlayerMoveState.Jump);
}
```

`isDodge`는 0.5초 시점에 이미 `false`가 되므로, `isDodgeAnimationPlaying`이 아직 `true`인
0.5~1.633초 구간에 `Space`를 누르면 이 조건을 그대로 통과해 `ChangeState(Jump)`가 호출된다.
Any State → `Jump` 전이도 `hasExitTime=False`(§14.2 표)라 즉시 발동하므로, **Dodge 클립이 이번엔
`dodgeTimer`가 아니라 Jump 입력 때문에 도중에 잘린다** — §14.2에서 고치려던 것과 정확히 같은
증상이 다른 입력으로 재발하는 것이다. §14.4 계획의 1~6번 항목 중 어디에도 `CheckJumpInput()`을
수정하는 항목이 없다 — **이 항목이 계획에서 빠진 것 자체가 이번 조사로 발견된 결함**이다. B안을
실제로 구현할 때는 `CheckJumpInput()`의 조건도 `!isJump && !isDodge && !isDodgeAnimationPlaying`으로
확장해야 한다(또는, 점프 입력이 회피 애니메이션을 의도적으로 취소시켜도 된다는 디자인 결정을
따로 내려야 한다 — 지금은 둘 중 어느 쪽도 계획에 명시돼 있지 않다).

### 15.3 회피 재발동 쿨다운이 0.5초 → 최대 1.633초로 조용히 늘어남 (의도치 않은 밸런스 변화 가능성)

§14.4-4는 회피 재입력 가드에 `!isDodgeAnimationPlaying`을 추가한다 — 클립이 채 끝나기 전에
`LeftControl`을 다시 눌러도 씹히게 하려는 의도였다(§14.2에서 지적한 "재입력으로 다시 잘리는"
문제 예방). 그런데 이 변경의 부작용으로, **회피를 다시 쓸 수 있을 때까지의 실제 대기시간이
`dodgeDuration`(0.5초)이 아니라 클립 전체 길이(1.633초)로 늘어난다** — 지금 코드(버그가 있는
상태)에서는 오히려 0.5초마다 계속 회피를 연달아 쓸 수 있었다(모션이 매번 끊기긴 했지만 재발동
자체는 빨랐다). B안 적용 후에는 "모션은 끝까지 재생되지만 그만큼 다음 회피까지 3배 이상 기다려야
하는" 트레이드오프가 생긴다. 버그 수정치고는 게임플레이 체감에 영향이 큰 변화라, 별도로 의도한
것인지 확인이 필요하다(§15.6).

### 15.4 `IsDodge()` 공개 API의 지속 시간도 함께 늘어남 — 향후 무적판정 등에 영향

```csharp
public bool IsDodge() { return animationDriver.CurrentState == PlayerMoveState.Dodge; }
```

전체 프로젝트에서 `IsDodge()`를 호출하는 곳은 현재 없다(grep으로 확인, `HideOrSeekPlayer.cs`
자기 자신의 정의 외 참조 0건) — 지금 당장 깨지는 기존 기능은 없다. 다만 `IsDodge()`는
`animationDriver.CurrentState == Dodge` 여부로 판단하는데, B안 적용 후에는 이 조건이 참으로
유지되는 시간이 (현재 버그 상태 기준) 약 0.5초에서 1.633초로 3배 이상 늘어난다. 나중에 "회피 중
무적(태그 판정 무시)" 같은 기능을 `IsDodge()` 위에 만들 경우, 무적 지속시간도 의도치 않게 함께
늘어나게 된다는 점을 기억해둬야 한다 — 지금 당장 손볼 곳은 없지만, 이 API를 소비하는 코드를 나중에
추가할 때 반드시 참고해야 하는 사이드이펙트다.

### 15.5 [경미, 자가 치유됨] `IsMovementLocked`(대화/사망 등)와 겹칠 때 1프레임 지연

§14.4-6에서 제안한 "애니메이션 종료 감지" 체크는 `Update()`의 맨 끝(`HandleJumpAnimationHold()`
옆)에 놓인다. 그런데 `Update()`는 최상단에서 `if (IsMovementLocked) return;`으로 전체를 건너뛴다
(예: 향후 대화/사망 시스템이 이 프로퍼티를 세팅하는 경우). `Animator` 자체는 스크립트 로직과
무관하게(=`IsMovementLocked`와 무관하게) 매 프레임 계속 재생되므로(회피는 Jump와 달리
`animator.speed`를 0으로 멈추는 로직이 없음), 이동이 잠긴 동안에도 `normalizedTime`은 실제로는
계속 증가해 결국 1.0을 넘어선다. 다만 `isDodgeAnimationPlaying` 플래그를 실제로 내리는 코드는
`Update()` 안에 있으므로 `IsMovementLocked`가 풀리기 전까지는 갱신되지 않고 `true`로 멈춰있는다 —
잠금이 풀린 첫 프레임에도 `CheckMovementInput()`(순서상 먼저 실행)이 아직 갱신 전의 낡은 값을
한 번 더 참조하지만, 같은 프레임 뒤쪽의 종료 체크가 즉시 플래그를 내려주므로 **다음 프레임부터는
정상화된다.** 실질적으로 눈에 띄는 문제는 아니고, 최악의 경우 이동 잠금이 풀리는 그 순간 1프레임
동안 회피 후 동작(Idle/Walk 전환)이 살짝 늦게 반영되는 정도다. 완전성을 위해 기록만 해둔다.

### 15.6 종합 — B안은 §14.4 형태 그대로는 "절반만" 고친다

§15.1과 §15.2를 종합하면, §14.4에 적힌 변경만으로는 다음 두 가지가 보장되지 않는다:
1. 회피 애니메이션이 재생되는 동안 이동/회전이 그 애니메이션과 시각적으로 어긋나지 않을 것(§15.1)
2. 회피 애니메이션이 다른 입력(점프)에 의해 조기 종료되지 않을 것(§15.2)

이를 실제로 다 해결하려면 최소 다음 두 가지가 §14.4에 추가로 필요하다:
- `CheckJumpInput()` 가드에 `!isDodgeAnimationPlaying` 추가(§15.2 해결).
- `CheckMovementInput()`의 `rotation = moveDir;` 대입과 `Move()`의 분기 선택 자체도
  `isDodgeAnimationPlaying`이 풀리기 전까지 억제(§15.1 해결) — 이 경우 사실상 "조작이 자유로워지는
  시점"이 0.5초가 아니라 1.633초가 되므로, §14.3에서 방안 A 대비 B안의 장점으로 들었던 "대시
  이동감은 그대로 유지"라는 이점이 이동에는 해당돼도 **회전/조작 잠금 시간 관점에서는 사실상
  방안 A와 큰 차이가 없어진다.** (다만 "캐릭터가 실제로 미끄러지는 거리/속도"는 여전히 0.5초
  분량으로 짧게 유지되므로, 완전히 A안과 동일해지는 것은 아니다 — 밀려나가는 건 짧고, 조작만
  묶여있는 형태가 된다.)

즉, B안을 §15.1/§15.2까지 반영해서 제대로 완성하면 "모션 완주"는 확실히 보장되지만, §14.3에서
기대했던 것보다 조작감에 미치는 영향이 크고(§15.1, §15.3), 향후 다른 시스템과의 연동 시 유의할
사이드이펙트(§15.4)도 있다는 것이 이번 조사의 결론이다. 어떤 절충(예: 회전만 잠그고 이동은
자유롭게 둔다 / 클립 후반부 도달 시점부터는 조작을 되돌려준다 등)으로 갈지는 구현 전에 별도
확인이 필요하다.

### 15.7 상태

**B안(§14.3)의 부작용 조사 완료. 구현 대기 중** — §15.1(이동/회전-애니메이션 불일치),
§15.2(`CheckJumpInput()` 가드 누락), §15.3(재발동 쿨다운 3배 증가), §15.4(`IsDodge()` API 지속시간
증가) 4가지를 실제 코드/Animator 구조 조사로 확인했다. 사용자 지시에 따라 이번에도 계획만
정리했고 실제 수정은 진행하지 않았다.

**→ 최종적으로 코드 방안(B안)이 아니라 애니메이션 클립 자체를 재타이밍하는 방향으로 결정됐다.
§16 참고.**

---

## 16. 최종 결정: Dodge 애니메이션 클립을 0.5초 언저리로 재타이밍 — ✅ 구현 완료

### 16.1 결정 배경

§14(코드로 `dodgeDuration`을 클립 길이에 맞춰 늘리는 방안 A)와 §15(코드로 애니메이션/이동을
분리하는 방안 B, 부작용 다수 발견)를 검토한 뒤, **B안은 §15의 부작용(특히 §15.1의 이동-애니메이션
시각적 불일치, §15.2의 Jump 우회 구멍)이 얻는 것에 비해 너무 많다는 결론**을 내렸다. 대신
"게임플레이가 원하는 회피 길이(0.5초)"와 "모션캡처 클립 길이(1.633초)"가 애초에 서로 다르게
만들어진 것이 문제의 본질이므로, **코드를 건드리는 대신 애니메이션 에셋 쪽의 타이밍을 게임플레이
의도(0.5초)에 맞게 재조정**하기로 확정했다.

이 방향의 핵심 장점: `HideOrSeekPlayer.dodgeDuration` 필드가 이미 `0.5f`로 설정돼 있으므로,
**클립 길이만 그와 비슷하게(0.5초 언저리) 줄이면 기존 코드(`CheckDodgeInput()`/`DodgeOut()`/
`PlayerAnimationDriver`)를 단 한 줄도 고치지 않고 버그가 사라진다.** §15에서 지적된 모든 부작용
(새 플래그, `CheckJumpInput()` 가드 추가 필요성, 재발동 쿨다운 변화, `IsDodge()` API 지속시간
변화)은 애초에 코드를 바꾸지 않으므로 전부 발생하지 않는다.

### 16.2 재타이밍 방식 — 원본 보존 + 별도 재타이밍 클립

`Assets/Animation/Dodge.fbx`를 직접 조사해 확인한 원본 클립 실측치:

| 항목 | 값 |
|---|---|
| 클립 이름 | `Dodge` (FBX 내장) |
| 길이 | 1.6333초 |
| 프레임레이트 | 30fps |
| `isLooping` / `loopTime` | `False` / `False` (원샷, §12.6 결정 유지) |
| 커브 바인딩 수 | 130개 (`mixamorig1:Hips` 이하 전신 본 Transform 커브) |

**작업 방식 (사용자 요청: 원본을 복사해두고 재타이밍):**

1. **원본 백업**: `Dodge.fbx`에 내장된 `Dodge` 클립을 그대로(시간 스케일 변경 없이) 복제해
   `Assets/Animation/Dodge_Original.anim`으로 저장한다. `Dodge.fbx` 자체는 전혀 손대지 않으므로
   원본은 그 안에도 그대로 남아있지만, FBX 서브 에셋은 재임포트 시 손상될 위험이 있으므로 독립된
   `.anim` 파일로 한 번 더 안전하게 남겨둔다.
2. **재타이밍 클립 생성**: `Dodge` 클립을 한 번 더 복제해 `Assets/Animation/Dodge_Retimed.anim`으로
   저장하고, 이 복제본의 **130개 커브 전부**에 대해 각 키프레임의 `time`을 스케일 팩터
   `0.5 / 1.6333 ≈ 0.3062`만큼 균일하게 압축한다. 모션의 형태(포즈 변화 순서)는 그대로 유지한 채
   재생 시간만 약 1.633초 → 약 0.5초로 줄이는 것이다(내용을 자르는 게 아니라 전체를 압축 재생).
   - 키프레임 `time`뿐 아니라 `inTangent`/`outTangent`(접선, 기울기=값변화/시간변화)도
     `1/스케일팩터`(≈3.266배)만큼 같이 조정해야 압축 후에도 커브 모양이 원본과 동일하게 유지된다
     (안 하면 튀거나 처지는 부자연스러운 움직임이 생길 수 있음).
   - `AnimationClipSettings.loopTime`은 원본과 동일하게 `False`로 명시 설정한다(원샷 유지).
3. **`PlayerAnimator.controller`의 `Dodge` 상태 Motion을 `Dodge_Retimed.anim`으로 교체**한다
   (기존 FBX 내장 `Dodge` 클립 참조 대신). 트리거/전이 설정(§8.3, Any State → Dodge,
   `hasExitTime=False`)은 그대로 유지 — 클립만 바뀌는 것이라 전이 구조를 바꿀 필요가 없다.
4. **코드 변경 없음** — `HideOrSeekPlayer.cs`/`PlayerAnimationDriver.cs`/`PlayerMoveState.cs`
   전부 그대로 둔다. `dodgeDuration = 0.5f`가 이미 새 클립 길이(≈0.5초)와 맞아떨어지므로, 기존
   `dodgeTimer` 로직이 클립이 끝나는 시점과 거의 동시에 상태를 전환하게 된다.

### 16.3 검증 계획

1. `read_console`로 에셋 작업 도중 에러/경고 0건 확인(스크립트 변경이 없으므로 컴파일 자체는
   영향 없지만, 클립 임포트/커브 조작 과정에서 에디터 경고가 뜨는지는 계속 확인).
2. `PlayerAnimator.controller`의 `Dodge` 상태 Motion이 `Dodge_Retimed.anim`(길이 ≈0.5초)을
   정확히 가리키는지 코드로 재조회해 확인.
3. `PlayerTestScene`에서 Play Mode 진입 후 Dodge를 발동해 `AnimatorStateInfo.normalizedTime`을
   프레임 단위로 추적 — `dodgeTimer`가 만료되는 시점(0.5초)에 `normalizedTime`이 1.0에 근접했는지
   확인(§12.6/§12.7에서 쓴 것과 동일한 슬로우모션 추적 방식).
4. 회피 동작이 시각적으로 끊기지 않고 자연스럽게 끝까지 재생되는지, 이동/회전이 기존과 동일하게
   0.5초에 정확히 자유로워지는지(§15.1에서 지적했던 문제가 애초에 발생하지 않는지) 육안 확인.
5. Walk/SneakWalk/Jump/Idle이 기존과 동일하게 회귀 없이 동작하는지 재확인(클립 하나만 바뀌었으므로
   영향이 없어야 정상).

### 16.4 상태

**구현 완료.** 아래 §16.5에 실제 작업 결과와 검증 결과를 정리했다.

### 16.5 구현 결과 (Unity MCP `execute_code`로 직접 수행)

**생성된 파일**
- `Assets/Animation/Dodge_Original.anim` — `Dodge.fbx` 내장 `Dodge` 클립을 스케일 변경 없이 그대로
  복제한 백업. `length=1.6333`, `loopTime=False` — 원본과 완전히 동일하게 확인.
- `Assets/Animation/Dodge_Retimed.anim` — 위 백업을 다시 복제해 130개 커브 바인딩 전부의 키프레임
  `time`을 `0.5 / 1.6333 ≈ 0.30612`배로 압축하고, 각 키프레임의 `inTangent`/`outTangent`도
  `1/스케일` 배(≈3.266배)로 같이 조정해 커브 모양을 유지했다. 결과 `length=0.5000`(정확히
  0.5초), `loopTime=False` 명시 설정 확인.

**변경된 파일**
- `Assets/Animation/PlayerAnimator.controller` — `Dodge` 상태의 Motion을 기존 FBX 내장 `Dodge`
  클립에서 `Dodge_Retimed.anim`으로 교체. 트리거/전이 구조(§8.3, Any State → Dodge,
  `hasExitTime=False`)는 변경하지 않음. 재조회로 `Dodge` 상태의 Motion이 `Dodge_Retimed`
  (`length=0.5000`)를 정확히 가리키는 것을 확인.
- `Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`, `PlayerAnimationDriver.cs`, `Dodge.fbx` — **변경
  없음**(계획대로 코드 무변경, `Dodge.fbx` 원본도 전혀 건드리지 않음).

**검증 결과**
- 매 단계(백업 생성 → 재타이밍 클립 생성 → 컨트롤러 교체) 직후 `read_console`로 에러/경고 0건을
  반복 확인. 리컴파일 로그에 뜬 "Disconnecting PUN due to recompile. Exit PlayMode."는 스크립트를
  전혀 건드리지 않았는데도 도메인 리로드가 한 번 발생하며 뜬 정상적인 Photon 안내 로그로,
  에러가 아님을 확인.
- `PlayerTestScene`에서 Play Mode 진입 후 `Time.timeScale = 0.05`(§12.6/§12.7과 동일한 슬로우모션
  기법)로 낮추고 `hide_or_seek_player`의 `Animator.SetTrigger("Dodge")`를 직접 호출해 재생 추적:
  `normalizedTime`이 `0.1528`(전환 직후, 아직 이전 상태) → `0.6828`(Dodge 상태 진입 확인,
  `IsName("Dodge")=True`) → `1.5664`까지 **끊기지 않고 단조 증가**하는 것을 확인 — 새 클립이
  중간에 잘리지 않고 자연스럽게 끝(1.0)을 지나 원샷 정지 포즈까지 도달함을 검증했다.
  (이 테스트는 §16.1의 전제, 즉 "코드를 바꾸지 않아도 `dodgeDuration`(0.5초)과 클립 길이(0.5초)가
  이제 일치해 버그가 사라진다"는 것을 애니메이터/클립 레벨에서 직접 검증한 것이다 — `HideOrSeekPlayer`
  쪽 로직 자체는 §12.6/§12.7에서 이미 충분히 검증됐고 이번에 전혀 수정하지 않았으므로 별도
  재검증하지 않았다.)
- 검증 후 `Time.timeScale`을 1로 복원하고 Play Mode를 종료했다.

**남겨둔 것**
- §14(방안 A)/§15(방안 B, 부작용 조사)는 채택되지 않았지만, 향후 비슷한 애니메이션 타이밍 이슈가
  생겼을 때 참고할 수 있도록 문서에 그대로 남겨둔다(삭제하지 않음).
- `Dodge_Original.anim`은 향후 다른 타이밍으로 재조정하고 싶을 때를 대비한 백업이므로 계속
  프로젝트에 남겨둔다.

---

## 17. 플레이어 머리 위 닉네임 빌보드 — ✅ 구현·검증 완료 (2026-08-15)

### 17.1 요청 사항

캐릭터 머리 위에 항상 카메라를 향하는 빌보드를 붙이고, 그 빌보드에는 플레이어의 닉네임을
표시한다. `HideOrSeekPlayer.prefab`(`Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab`)을
사용하는 모든 인스턴스(로컬/원격 구분 없이) 전부에 적용한다.

### 17.2 기존 자산 조사 — 이미 같은 문제를 풀어본 전례가 있다

프리팹을 직접 열어보니, 이미 정확히 같은 종류의 "머리 위 빌보드"가 하나 존재한다:
`VoteIndicator`라는 자식 오브젝트(로컬 위치 `(0, 2.2, 0)`, `SpriteRenderer` 부착)로, 색상 투표
라운드 중 자신이 고른 색을 표시하는 스프라이트다. 이걸 담당하는
`Assets/02. Scripts/ColorTag/PlayerColorVoteIndicator.cs`의 빌보드 처리 방식:

```csharp
private void LateUpdate()
{
    if (Camera.main != null && indicator != null)
        indicator.transform.forward = Camera.main.transform.forward;
}
```

`transform.forward = Camera.main.transform.forward`(카메라 쪽을 바라보도록 `LookAt`하는 대신,
카메라와 같은 forward 벡터로 맞추는 방식)를 쓰면 카메라가 위/아래로 기울어도 빌보드 평면이
항상 뷰 평면과 평행하게 유지되어 세로로 찌그러지지 않는다 — 검증된 방식이므로 닉네임
빌보드에도 그대로 재사용한다.

**중요한 반면교사**: `PlayerColorVoteIndicator`는 스크립트 자체는 캐릭터 **루트**
GameObject에 붙어 있고, `[SerializeField] indicator` 필드로 자식 스프라이트를 가리키는
간접 참조 구조다. 이 구조 때문에 실제로 버그가 났었다(`Bug-fix-plan.md` §10) —
`LateUpdate()`가 실수로 `this.transform`(캐릭터 루트)을 돌려버려서 캐릭터 전체가 카메라를
따라 돌아가며 "항상 뒷모습만 보이는" 치명적 버그로 이어졌다. 이번 닉네임 빌보드는 **같은 종류의
실수가 애초에 불가능하도록**, 스크립트를 루트가 아니라 빌보드 자식 오브젝트 자신에게 직접
부착해 `this.transform`을 조작하는 구조로 설계한다(17.4 참고) — 간접 참조 필드 자체를 없애
"어느 transform을 돌려야 하는지 헷갈릴 여지"를 구조적으로 제거한다.

### 17.3 텍스트 표시 방식 — World Space Canvas 대신 3D TextMeshPro

프로젝트에는 이미 TextMeshPro(TMP)가 임포트되어 있다(`GameManager.cs`, `GameLobbyController.cs`가
`TMPro.TMP_Text`/`TextMeshProUGUI` 사용 중 — `research.md`/기존 코드로 확인). 닉네임 하나만
띄우는 용도로는 `Canvas`(World Space) + `TextMeshProUGUI` + `EventSystem` 의존성을 새로 얹을
필요가 없으므로, **UI가 아닌 3D `TextMeshPro` 컴포넌트**(메시 기반, Canvas 불필요)를 자식
오브젝트에 직접 붙이는 방식을 쓴다. 플레이어 수만큼(최대 4명) 인스턴스가 생기므로, 매 인스턴스마다
별도 Canvas/EventSystem을 두는 것보다 가볍다.

### 17.4 새 컴포넌트 설계 — `PlayerBillBoard.cs`

`Assets/02. Scripts/Unit/PlayerBillBoard.cs` (신규). `HideOrSeekPlayer`와 같은 `Unit` 도메인.

```csharp
using Photon.Pun;
using TMPro;
using UnityEngine;

// 캐릭터 머리 위 닉네임 빌보드. 이 스크립트 자신이 빌보드 자식 오브젝트에 직접 붙어
// this.transform을 다루므로, PlayerColorVoteIndicator가 겪었던 "간접 참조 대상을 잘못 회전시키는"
// 버그(Bug-fix-plan.md §10)가 구조적으로 재발할 수 없다.
public class PlayerBillBoard : MonoBehaviour
{
    [SerializeField] private PhotonView pv; // 부모(캐릭터 루트)의 PhotonView
    [SerializeField] private TextMeshPro nameText;

    private void Start()
    {
        if (pv != null && pv.Owner != null)
            nameText.text = pv.Owner.NickName;
    }

    private void LateUpdate()
    {
        if (Camera.main != null)
            transform.forward = Camera.main.transform.forward;
    }
}
```

- `IsMine` 체크를 두지 않는다 — `VoteIndicator`와 마찬가지로 로컬/원격 구분 없이 **모든 클라이언트가
  자신의 화면에서 모든 캐릭터의 닉네임을 봐야 하는** 기능이므로, 소유권과 무관하게 항상 동작해야
  맞다(17.2에서 확인한 기존 패턴과 동일한 이유).
- `pv.Owner.NickName`은 `Bug-fix-plan.md` §12에서 다뤘던 "InRoom이 아직 안 됐을 때 전송이
  실패하는" 문제와는 다른 영역이다 — 이건 로컬에서 이미 갖고 있는 `PhotonView`의 소유자 정보를
  **읽기만** 하는 것이고, `PhotonNetwork.Instantiate`가 로컬에서 성공하는 시점에는 그 View의
  `Owner`도 이미 채워져 있다(닉네임은 룸 입장 전에 `PhotonNetwork.NickName`으로 미리 설정되고
  Photon이 `Player` 객체 생성 시점에 함께 동기화하는 값이므로, 네트워크 전송 성공 여부와 무관).
  따라서 §12처럼 별도 대기 로직 없이 `Start()`에서 한 번만 읽어도 안전하다.
- 닉네임은 게임 중 바뀌지 않으므로(로비 입장 전에만 설정 가능, `LobbyController.TryApplyNickname`)
  실시간 갱신 콜백은 두지 않는다.

### 17.5 프리팹 변경 — `HideOrSeekPlayer.prefab`

캐릭터 루트(`fileID 4508182218260622245`) 아래에 새 자식 오브젝트 `Nameplate`를 추가한다.

| 항목 | 값 |
|---|---|
| 이름 | `Nameplate` |
| 부모 | `HideOrSeekPlayer`(루트) |
| Local Position | `(0, 2.5, 0)` — 기존 `VoteIndicator`(`y=2.2`)보다 약간 위에 둬서 투표 라운드 중에도 두 빌보드가 겹치지 않게 함(정확한 값은 Play Mode에서 육안으로 조정, 17.6 참고) |
| 컴포넌트 | `TextMeshPro`(3D) + `PlayerBillBoard`(신규 스크립트) |
| `TextMeshPro` 설정 | 정렬 Center/Middle, 폰트 크기·색상은 구현 시 Play Mode로 가독성 확인 후 조정(기본값: 흰색 텍스트 + 검은 Outline로 어떤 배경에서도 잘 보이게) |
| `PlayerBillBoard.pv` | 루트의 `PhotonView`(`fileID 8262779534870907500`, 기존 `VoteIndicator`가 참조하는 것과 동일한 컴포넌트) 연결 |

`VoteIndicator`와 동일하게 프리팹의 정식 자식으로 등록하므로, `PhotonNetwork.Instantiate`로
스폰되는 모든 캐릭터 인스턴스(로컬/원격 전부)에 자동으로 포함된다 — 별도의 스폰/초기화 코드
변경이 필요 없다.

### 17.6 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. `PlayerTestScene`(1인 오프라인 테스트 환경, §12.5)에서 Play Mode 진입 후 닉네임 텍스트가
   머리 위에 정상 표시되는지, 카메라를 우클릭 드래그로 회전시켜도(§10에서 검증한 방식과 동일)
   빌보드가 항상 카메라를 향해 평평하게 유지되는지 확인.
3. 캐릭터가 걷기/점프/회피로 움직이거나 회전해도 닉네임 위치가 머리 위를 정확히 따라가고
   텍스트 자체는 캐릭터 회전과 무관하게(빌보드이므로) 항상 카메라를 향하는지 확인.
4. `VoteIndicator`(투표색 스프라이트)와 겹치지 않고 위아래로 적절히 구분되어 보이는지 확인 —
   겹치면 17.5의 Y 오프셋 값을 조정.
5. 가능하면 Unity 에디터를 실제 멀티 클라이언트 세션에 참가시켜(`Bug-fix-plan.md` §12에서 쓴
   방식과 동일) 다른 플레이어의 닉네임도 정확히 보이는지 실사용 조건에서 확인.

### 17.7 상태

**✅ 구현·검증 완료.** 사용자 승인을 받아 그대로 구현했다(요청에 따라 클래스/파일명만
`PlayerNameplate` 대신 `PlayerBillBoard`로 확정). 상세 결과는 §17.8 참고.

### 17.8 구현 결과

**생성된 파일**
- `Assets/02. Scripts/Unit/PlayerBillBoard.cs` — 계획(§17.4)과 동일하게 구현.

**변경된 파일**
- `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab` — 루트 아래에 `Nameplate` 자식 추가
  (`RectTransform`/`MeshRenderer`/`MeshFilter`는 `TextMeshPro` 컴포넌트가 자동으로 요구하는
  구성요소, `TMPro.TextMeshPro` + `PlayerBillBoard` 부착). `PlayerBillBoard.pv`는 루트의
  `PhotonView`, `nameText`는 같은 오브젝트의 `TextMeshPro`를 정확히 참조하도록 연결됨(직접
  `SerializedObject`로 설정 후 재조회로 확인).

**계획 대비 조정한 값**
- `TextMeshPro.fontSize`: 계획(§17.5)에서 "Play Mode로 가독성 확인 후 조정"이라고 명시했던
  대로, 최초 적용값 `4`는 실제로 캐릭터보다 훨씬 큰 글자(화면 폭을 거의 다 채움, §17.8 첫 번째
  스크린샷)로 렌더링됨을 확인해 `0.5`로 낮췄다 — `Nameplate`의 `localScale`이 `(1,1,1)`인 상태에서
  `TextMeshPro`(3D)를 코드로 새로 추가하면(Unity 메뉴의 "3D Text" 생성 시 자동으로 딸려오는
  `localScale ≈ 0.1` 스케일링이 없으므로) 같은 `fontSize` 값이라도 훨씬 크게 나온다는 것이
  실측으로 확인된 원인이다. 재조정 후 스크린샷(§17.8 두 번째)에서 머리 위에 적당한 크기로
  표시됨을 확인했다.
- `Nameplate` Local Position `(0, 2.5, 0)`은 계획대로 유지 — `VoteIndicator`(`y=2.2`)와 겹치지
  않고 위쪽에 자연스럽게 위치함을 Play Mode에서 확인했다.

**검증 결과 (§17.6 대응)**
1. `read_console`로 컴파일 에러/경고 0건 확인(최종 재확인 포함).
2. `PlayerTestScene`(오프라인 1인 테스트 환경)에서 Play Mode 진입 후 닉네임 텍스트가 머리 위에
   표시됨을 확인. 이 씬은 `PhotonNetwork.OfflineMode`라 `pv.Owner`가 `null`인데, `PlayerBillBoard.
   Start()`의 `if (pv != null && pv.Owner != null)` 널 가드 덕분에 예외 없이 기본 텍스트("Player")를
   유지함을 확인(오프라인 테스트 환경에 대한 방어 로직이 실제로 의도대로 동작).
3. `Camera_Ctrl.m_RotH`를 리플렉션으로 `0→180`으로 바꿔 카메라를 궤도 회전시킨 뒤 직접 조회 —
   `nameplate.forward`가 `camera.transform.forward`와 **정확히 일치**(`(0.00, -0.42, -0.91)`로
   양쪽 동일)함을 확인했고, 동시에 캐릭터 루트의 `eulerAngles`는 `(0,0,0)`으로 전혀 흔들리지
   않음을 확인 — §17.2에서 우려했던 "루트를 잘못 돌리는" 버그(Bug-fix-plan.md §10)와 같은 실수가
   이 구현에는 없음을 실측으로 검증했다.
4. **실제 Photon 룸으로 닉네임 표시까지 검증**: Unity 에디터를 `PhotonNetwork.NickName =
   "BillboardTester"`로 설정해 새 룸을 만들고 `GameLobbyScene`까지 정상 진입 → 스폰된
   `HideOrSeekPlayer`의 `pv.Owner.NickName`이 `"BillboardTester"`였고, `Nameplate`의 `TextMeshPro.
   text`도 정확히 `"BillboardTester"`로 채워짐을 코드 조회와 스크린샷 양쪽으로 확인했다 —
   §17.4에서 "읽기만 하는 것이라 §12의 InRoom 경쟁과 무관하게 안전하다"고 판단한 근거가 실제
   룸 입장 조건에서도 맞았음을 확인.
5. `VoteIndicator`(투표색 스프라이트)와의 겹침 여부는 이번 세션에서 실제 색상 선택 라운드까지
   진행하지는 않아 육안으로 직접 확인하지 못했다 — `Nameplate`가 `y=2.2`인 `VoteIndicator`보다
   위(`y=2.5`)에 있으므로 겹치지 않을 것으로 예상되지만, 다음에 색상 투표 UI가 실제로 뜨는
   상황에서 한 번 더 확인하는 것을 권장한다(경미한 후속 확인 사항).

**부수적으로 발견한, 이번 작업과 무관한 기존 이슈**
- `PlayerTestScene`의 `Main Camera`에 스크립트 참조가 끊긴(Missing) 컴포넌트가 하나 더 있음을
  콘솔 경고(`The referenced script (Unknown) on this Behaviour is missing!`)로 발견했다. 직접
  조회해보니 `Camera_Ctrl`은 정상적으로 존재하고 있었고(별도 컴포넌트로 살아있음), 그 옆에 완전히
  분리된 빈 Missing 슬롯이 하나 더 있는 것이었다 — `Nameplate`/`PlayerBillBoard`와는 무관한 그
  씬의 기존 상태였고, 이번 작업 범위 밖이라 손대지 않았다. 추후 정리가 필요하면 알려달라.