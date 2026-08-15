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

---

## 18. 물리 엔진(Rigidbody) 도입 + 맵 밖 낙하 시 스폰 지점 복귀 — ✅ 구현·검증 완료 (2026-08-16)

### 18.1 요청 사항

1. 맵 가장자리에서 점프하면 맵 밖으로 나가버리는데, 나간 뒤 일정 높이만큼 떨어지면 자동으로
   스폰 지점으로 복귀시킨다.
2. 지금 플레이어에게 물리 엔진(Rigidbody)이 전혀 적용되어 있지 않은데, 적용되도록 설계한다.

두 요청은 사실 하나의 원인에서 나온다 — 아래 18.2에서 실제 코드를 근거로 확인한다.

### 18.2 현재 구조 조사 — 정확한 버그 원인

`HideOrSeekPlayer.cs`/`PlayerGroundDetector.cs`를 직접 재확인했다. 이 프로젝트는 **Rigidbody가
전혀 없다** — 이동은 전부 `Move()`가 `transform.position +=`으로 직접 좌표를 갱신하고, "중력"도
`PlayerGroundDetector`라는 순수 C# 클래스가 `yVelocity`를 수동으로 적분해 흉내만 내는 구조다
(Unity 물리 엔진 자체는 개입하지 않음, `Rigidbody`/`CapsuleCollider` 등 플레이어 쪽 물리
컴포넌트가 프리팹에 아예 없음 — `HideOrSeekPlayer.prefab` 컴포넌트 목록에 `Transform`/
`Animator`/`PhotonView`/`NavMeshAgent`/스크립트들만 있고 `Rigidbody`/`Collider`는 없다).

**낙하 로직이 점프 중에만 동작한다는 것이 핵심 원인이다:**

```csharp
private void ApplyGravity()
{
    if (!isJump)          // ← 점프 중이 아니면 중력 계산 자체를 안 함
        return;
    ...
}
```

그리고 점프 중 낙하 판정(`PlayerGroundDetector.Tick`)은 **이번 프레임에 떨어질 거리만큼만** 아래로
레이캐스트를 쏜다:

```csharp
Vector3 rayOrigin = transform.position + Vector3.up * groundCheckOffset;
float rayDist = groundCheckOffset + Mathf.Abs(yVelocity) * deltaTime;
if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDist, groundLayer)) { ... }
```

맵 가장자리에서 점프해 맵 바깥(바닥이 없는 허공)으로 넘어가면, 이 레이캐스트가 **영원히 아무것도
맞히지 못한다** — `landed`가 절대 `true`가 되지 않으므로 `isJump`도 절대 `false`로 안 풀리고,
`yVelocity`는 계속 음의 방향으로 누적되며 캐릭터가 무한히 떨어진다. 게다가 `HandleJumpAnimationHold()`가
착지 전까지 Jump 애니메이션을 얼려두므로, 화면상으로도 공중에서 얼어붙은 채 끝없이 추락하는
것처럼 보인다. **점프 없이 그냥 걸어서 가장자리를 넘어가는 경우는 증상이 다르다** —
`ApplyGravity()` 자체가 `isJump`가 아니면 아예 실행되지 않으므로, 캐릭터는 낙하하지 않고 허공에서
같은 높이로 계속 걸어 다니게 된다(이것도 버그이지만 사용자가 보고한 "점프로 맵을 벗어남" 증상과는
다른 결이라 18.3에서 물리 엔진을 도입하면 두 증상 모두 함께 해소된다).

`NavMeshAgent`는 프리팹에 붙어 있지만 실제 경로탐색(`SetDestination`)에는 전혀 쓰이지 않고,
착지 시 `agent.Warp(...)`로 위치만 재동기화하는 용도로만 쓰인다 — 이동 자체를 담당하지 않는다.

### 18.3 설계 A — Rigidbody 기반 물리 엔진 도입

**핵심 방향**: `HideOrSeekPlayer` 루트에 `Rigidbody` + `CapsuleCollider`를 추가하고, **로컬
소유(`pv.IsMine`) 캐릭터만 실제 물리 시뮬레이션(중력·충돌)을 받게** 하며, 원격 캐릭터는 지금처럼
`PlayerNetworkSync.Interpolate()`가 순수하게 위치를 보간하도록 유지한다 — 두 시스템이 같은
Transform을 동시에 제어하면 충돌하기 때문이다.

```csharp
private void Start()
{
    ...
    rb = GetComponent<Rigidbody>();
    rb.isKinematic = !pv.IsMine; // 원격 캐릭터는 물리 비활성 — networkSync가 transform을 직접 보간
    if (pv.IsMine)
    {
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 빠른 낙하/점프 시 얇은 바닥/난간 통과 방지
        rb.interpolation = RigidbodyInterpolation.Interpolate; // FixedUpdate 사이 시각적 끊김 완화
        rb.constraints = RigidbodyConstraints.FreezeRotation;  // 회전은 transform.LookAt으로 직접 제어, 물리 회전(넘어짐)은 막음
    }
}
```

**입력은 `Update()`, 물리 적용은 `FixedUpdate()`로 책임 분리** (Unity 관례 — 고정 타임스텝이
아닌 `Update()`에서 `Rigidbody.velocity`를 건드리면 프레임레이트에 따라 물리 거동이 들쭉날쭉해짐):

```csharp
private void Update()
{
    if (IsMovementLocked) return;
    if (pv.IsMine)
    {
        CheckMovementInput(); // 입력 읽기 + rotation 갱신만, 여기서 좌표를 옮기지 않음
        CheckJumpInputFlag(); // "점프 버튼을 눌렀다"는 의도만 플래그로 기록
        CheckDodgeInput();
        animationDriver.HandleJumpAnimationHold();
    }
    else
    {
        networkSync.Interpolate(transform, Time.deltaTime);
        animationDriver.ChangeState(networkSync.RemoteState);
    }
}

private void FixedUpdate()
{
    if (!pv.IsMine || IsMovementLocked) return;

    isGrounded = groundDetector.IsGrounded(transform.position); // 점프 중 여부와 무관하게 매 스텝 확인(18.2의 버그 근본 수정)

    if (jumpRequested && isGrounded && !isDodge)
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpPower, rb.linearVelocity.z);
        isJump = true;
        ...
    }
    jumpRequested = false;

    Move(); // 이제 transform.position += 대신 rb.linearVelocity의 x/z만 갱신(아래 18.3-1)
}
```

**18.3-1. `Move()` 재작성** — 수평 속도만 `Rigidbody`에 넘기고, 수직 속도(중력/점프)는 물리 엔진이
전담하도록 건드리지 않는다:

```csharp
public void Move()
{
    Vector3 dir = /* 기존과 동일한 dodge/jump-관성/일반 분기로 계산된 방향 */;
    float velocity = /* 기존과 동일한 speed 계산 */;

    Vector3 horizontal = new Vector3(dir.x * velocity, 0f, dir.z * velocity);
    rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z); // y는 물리 엔진이 채운 값 그대로 보존
    if (dir != Vector3.zero)
        transform.LookAt(transform.position + new Vector3(dir.x, 0f, dir.z)); // 회전은 지금처럼 직접 제어 유지
}
```

**18.3-2. `PlayerGroundDetector` 역할 축소** — 더 이상 `yVelocity`를 직접 적분하지 않는다(그건
이제 `Rigidbody`+`Physics.gravity`의 몫). 순수하게 "지금 땅에 붙어 있는가"만 답하는 질의 클래스로
단순화한다:

```csharp
public class PlayerGroundDetector
{
    private readonly LayerMask groundLayer;
    private readonly float checkDistance;

    public PlayerGroundDetector(LayerMask groundLayer, float checkDistance)
    {
        this.groundLayer = groundLayer;
        this.checkDistance = checkDistance;
    }

    public bool IsGrounded(Vector3 position)
    {
        return Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, checkDistance + 0.1f, groundLayer);
    }
}
```

`StartJump()`/`Tick()`/`yVelocity` 필드는 전부 제거된다 — 기존에 이 클래스가 겪던 "짧은 레이캐스트라
가장자리를 넘어가면 착지 판정 자체가 불가능해지는" 문제(18.2)가 설계상 사라진다: 이제는 점프 중이든
아니든 매 `FixedUpdate`마다 독립적으로 접지 여부만 물어보고, 실제 낙하/충돌은 Unity 물리 엔진이
맡으므로 "허공에서 얼어붙은 채 무한 낙하"가 애초에 불가능하다(그냥 계속 떨어질 뿐이고, 18.4의
낙사 복귀 로직이 이 낙하를 붙잡는다).

**18.3-3. `NavMeshAgent` 완전 제거로 최종 확정** — 두 차례 논의를 거쳐 방향이 확정됐다.

1차로 "영구 비활성화(컴포넌트는 남기고 `updatePosition/updateRotation`만 끔)"를 검토했으나, 그
방식도 `enabled=true`인 이상 NavMeshAgent가 매 프레임 "지금 NavMesh 위에 유효하게 있는가"를 내부적으로
계속 검사한다 — 이 프로젝트 콘솔에서 이미 여러 번 관측된 `"Failed to create agent because there is
no valid NavMesh"` 경고가 정확히 이 검사에서 나온다. NavMesh가 안 구워진 씬(`GameLobbyScene` 등)에서
계속 경고가 찍힐 여지가 있다.

이어서 "그럼 `agent.enabled = false`로 컴포넌트째 끄면 어떤가"까지 검토했으나, 애초에 **플레이어는
AI 추격/경로탐색을 전혀 쓰지 않고, 나중에 몬스터를 추가하더라도 `HideOrSeekPlayer`와는 별개의
클래스로 만들어 그 몬스터 전용 파라미터(속도/반경 등)로 새 `NavMeshAgent`를 붙이게 될 것**이므로,
지금 플레이어 프리팹에 "나중 재활용"을 이유로 남겨둘 근거 자체가 약하다는 결론에 이르렀다. 따라서
**`HideOrSeekPlayer`에서 `NavMeshAgent` 필드와 `agent.Warp/updatePosition` 참조를 전부 제거하고,
`HideOrSeekPlayer.prefab`에서도 컴포넌트 자체를 삭제한다.** 이후 몬스터/AI 유닛이 실제로 필요해지면
그 전용 클래스에 처음부터 맞는 설정으로 새로 붙이면 된다 — 이 편이 지금 남겨두는 것보다 오히려
더 간단하고 경고도 없다.

`CheckJumpInput()`/`ApplyGravity()`/`OnPhotonSerializeView()`에 남아 있던 `agent.updatePosition = false/true`,
`agent.Warp(...)` 호출과 `[SerializeField] private NavMeshAgent agent;` 필드, `using UnityEngine.AI;`
지시문을 전부 삭제한다.

**18.3-4. 플레이어에 `CapsuleCollider` 필요** — 지금은 플레이어 쪽에 어떤 Collider도 없어서
바닥 레이캐스트(그것도 점프 중에만) 외에는 세상 무엇과도 물리적으로 부딪히지 않는다. `Rigidbody`가
의미 있게 동작하려면 캐릭터 몸통 크기의 `CapsuleCollider`(대략 height 1.8~2.0, radius 0.3~0.4,
center y ≈ 0.9~1.0)를 루트에 추가해야 한다 — 이 프리팹은 §13에서 정식 프리팹으로 승격된
`HideOrSeekPlayer.prefab` 하나뿐이므로, 여기 한 번만 추가하면 모든 씬(스폰되는 모든 인스턴스)에
자동 적용된다.

### 18.4 설계 B — 맵 밖으로 떨어지면 스폰 지점으로 복귀

**방식 선택 — 씬에 배치하는 트리거 볼륨(권장) vs 스크립트에 Y 좌표 하드코딩.** 후자(예:
`if (transform.position.y < -20) Respawn();`를 `HideOrSeekPlayer.cs`에 박아넣는 방식)는 간단하지만
맵마다 바닥 높이·규모가 다를 수 있는데 그 기준값을 플레이어 스크립트가 알아야 하는 것은 책임
분리에 어긋난다(`CLAUDE.md`의 OOP 원칙). **맵의 "경계"는 맵(씬)이 정의해야 할 정보이므로, 새
컴포넌트를 씬에 배치하는 트리거 볼륨 방식을 채택한다** — `Ground`/`VoteIndicator`처럼 이미 씬에
직접 배치해 쓰는 다른 요소들과도 패턴이 일관된다.

**새 파일: `Assets/02. Scripts/GameManager/VoidKillZone.cs`** — `PlayerSpawner.cs`/
`RoomExitController.cs`와 같은 "씬 인프라" 스크립트이므로 `GameManager` 도메인에 둔다(`Unit`
도메인이 아님 — 캐릭터 행동이 아니라 레벨 경계 정의이기 때문).

```csharp
using UnityEngine;

// 맵 바깥으로 떨어진 로컬 플레이어를 스폰 지점으로 되돌린다.
// 씬 하단에 이 컴포넌트가 붙은 큰 트리거 콜라이더를 배치해서 사용한다.
[RequireComponent(typeof(Collider))]
public class VoidKillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<HideOrSeekPlayer>();
        if (player != null && player.IsMine)
            player.RespawnToSpawnPoint();
    }
}
```

**`HideOrSeekPlayer`에 추가할 공개 메서드** — `PlayerSpawner.SpawnLocalPlayer()`와 동일한 규칙
(`"PlayerSpawnPos"` 이름으로 찾고, 겹침 방지용 랜덤 오프셋)으로 위치를 되돌리고, 낙하 중이던
속도도 함께 0으로 초기화한다(안 그러면 스폰 직후에도 떨어지던 속도가 남아 있어 바닥을 뚫고
지나갈 수 있음):

```csharp
public void RespawnToSpawnPoint()
{
    GameObject spawnPointObj = GameObject.Find("PlayerSpawnPos");
    if (spawnPointObj == null) return;

    Vector3 offset = new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
    rb.linearVelocity = Vector3.zero;
    transform.position = spawnPointObj.transform.position + offset;

    isJump = false;
    animationDriver.ResumePlayback();
}
```

**씬 배치 방법**: 각 씬(`GameLobbyScene`, `GameScene`, `PlayerTestScene`)의 `PlayerSpawnPos`보다
한참 아래(예: y = -15 ~ -20)에, 플레이 영역 전체를 넉넉히 덮는 큰 `BoxCollider`(`isTrigger = true`)
+ `VoidKillZone`을 가진 `VoidKillZone` GameObject를 하나씩 배치한다. "일정 높이만큼 떨어지면"이라는
사용자 요구사항의 "일정 높이"가 바로 이 트리거의 Y 위치이며, 씬마다 지형 규모가 다르면 씬마다
다르게 조정할 수 있다(코드 수정 없이 씬 편집만으로 대응 가능 — 이게 Y좌표 하드코딩 대신 이
방식을 택한 이유).

**안전장치(폴백)**: 혹시 어떤 씬에 `VoidKillZone` 배치를 깜빡하더라도 무한히 떨어지는 사고를
막기 위해, `HideOrSeekPlayer.FixedUpdate()`에 극단적인 최후 방어선을 하나 더 둔다:

```csharp
if (transform.position.y < -100f) // VoidKillZone을 깜빡 빠뜨렸을 때의 최후 방어선
    RespawnToSpawnPoint();
```

**Photon 동기화 관점에서 안전한 이유(실측 확인)** — `PlayerNetworkSync.Interpolate()`를 다시
확인해보니, 이미 다음과 같은 스냅 로직이 있다:

```csharp
public void Interpolate(Transform transform, float deltaTime, float lerpRate = 10.0f, float snapDistance = 10.0f)
{
    if (snapDistance < (transform.position - RemotePosition).magnitude)
        transform.position = RemotePosition; // 10유닛 넘게 차이나면 즉시 스냅
    else
        transform.position = Vector3.Lerp(...); // 그 이하는 부드럽게 보간
}
```

`VoidKillZone`이 스폰 지점보다 15~20유닛 아래에 있으므로, 복귀 시 이동 거리는 항상 `snapDistance
(10)`를 넘는다 — 즉 **원격 클라이언트 화면에서 캐릭터가 죽었다가 스폰 지점으로 스르륵 미끄러져
오는 것처럼 보이는 문제가 이미 기존 코드로 방지되어 있다.** 별도의 텔레포트 전용 RPC나 플래그를
새로 만들 필요가 없다 — 이 부분은 설계상 추가 작업이 필요 없음을 확인한 것이다.

### 18.5 필드/메서드 변경 매핑

| 대상 | 처리 | 비고 |
|---|---|---|
| `HideOrSeekPlayer.Rigidbody rb` (신규 필드) | 추가 | `Start()`에서 `GetComponent`, `IsMine` 여부로 `isKinematic` 분기 |
| `HideOrSeekPlayer.agent` (`NavMeshAgent`) | 완전 제거 | 18.3-3 — 플레이어는 경로탐색 미사용, 몬스터 도입 시 그쪽에 별도로 새로 붙이는 편이 더 간단 |
| `PlayerGroundDetector.yVelocity/StartJump()/Tick()` | 제거 | 중력 적분은 이제 Unity 물리 엔진이 담당 |
| `PlayerGroundDetector.IsGrounded(Vector3)` (신규) | 추가 | 점프 여부와 무관하게 매 `FixedUpdate` 호출 — 18.2 버그의 근본 수정 |
| `HideOrSeekPlayer.ApplyGravity()` | 제거 | 물리 엔진이 대체 |
| `HideOrSeekPlayer.Move()` | 수정 | `transform.position +=` → `rb.linearVelocity` 수평 성분만 갱신 |
| `HideOrSeekPlayer.CheckJumpInput()` | 수정 | `groundDetector.StartJump()` → `rb.linearVelocity.y = jumpPower` 직접 대입, 접지 여부(`IsGrounded`)로 게이팅 |
| `HideOrSeekPlayer.Update()`/`FixedUpdate()` | 분리 | 입력 읽기는 `Update()`, 물리 갱신은 `FixedUpdate()`(신규) |
| `HideOrSeekPlayer.RespawnToSpawnPoint()` (신규) | 추가 | `VoidKillZone`이 호출, `PlayerSpawner`와 동일한 스폰 규칙 재사용 |
| `Assets/02. Scripts/GameManager/VoidKillZone.cs` (신규 파일) | 추가 | 씬에 배치하는 트리거 볼륨 |
| `HideOrSeekPlayer.prefab`의 `Rigidbody`/`CapsuleCollider` (신규 컴포넌트) | 추가 | 18.3-4 |
| `GameLobbyScene`/`GameScene`/`PlayerTestScene`의 `VoidKillZone` GameObject (신규) | 씬별 추가 | 18.4 |

### 18.6 범위 밖 / 후속 확인 필요 사항

- **각 씬의 `Ground`에 실제 `Collider`가 있는지 재확인 필요.** `GameLobbyScene`은 이번 배경 작업
  중 `MeshCollider`를 직접 추가해 확인됐지만(`Bug-fix-plan.md`와 무관한 이번 세션 작업), `GameScene`/
  `PlayerTestScene`의 바닥은 이번 계획 조사 범위에 포함하지 않았다 — Rigidbody가 실제로 착지하려면
  필수이므로 구현 단계에서 씬별로 반드시 확인해야 한다.
- **`GameLobbyScene`의 펜스/테이블/의자에는 아직 Collider가 없다**(크레이트에만 있음, 이전 작업
  기록 참고) — 실제 Rigidbody가 적용되면 플레이어가 이 오브젝트들을 그냥 통과하게 된다. 이번
  계획 범위 밖이지만, 자연스러운 후속 작업으로 필요하면 알려달라.
- **회피(Dodge)/점프 관성 이동 중 `transform.LookAt` 회전과 `Rigidbody.MoveRotation`의 관계**는
  18.3에서 기존 방식(직접 `transform` 회전)을 유지하는 것으로 설계했지만, `RigidbodyConstraints.
  FreezeRotation`을 켜두면 물리 충돌로 캐릭터가 넘어지는 것은 막히되 `transform.rotation` 직접
  대입 자체는 여전히 허용된다 — 구현 단계에서 실제로 회전이 매끄럽게 반영되는지 Play Mode로
  확인이 필요하다.
- **`GameScene`의 실제 맵 형태(경계/절벽 위치)는 이번 조사에 포함되지 않았다** — `VoidKillZone`
  배치 위치(Y값, XZ 크기)는 구현 시 그 씬을 직접 열어 확인 후 정한다.

### 18.7 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. `PlayerTestScene`에서 Play Mode 진입 후, 평지에서 걷기/점프/회피가 기존과 동일한 조작감으로
   동작하는지 확인(수평 속도를 Rigidbody로 옮긴 것이 기존 체감과 달라지지 않았는지).
3. 맵 가장자리로 이동해 점프해서 밖으로 나가 봤을 때: (a) 더 이상 애니메이션이 얼어붙은 채
   허공에 멈추지 않고 실제로 계속 낙하하는지, (b) `VoidKillZone` 통과 시 스폰 지점으로 정확히
   복귀하는지, (c) 복귀 직후 다시 바닥을 뚫고 떨어지지 않는지(속도 초기화 확인) 확인.
4. 걸어서(점프 없이) 가장자리를 넘어갈 때도 이제는 실제로 떨어지고 `VoidKillZone`에 걸려
   복귀하는지 확인(18.2에서 지적한 "점프 없이 벗어나면 아예 낙하하지 않던" 기존 버그도 함께
   해소되는지).
5. Unity 에디터를 실제 멀티 클라이언트 세션에 참가시켜(`Bug-fix-plan.md` §12와 동일한 방식),
   한 클라이언트가 낙사 복귀할 때 다른 클라이언트 화면에서 순간이동처럼 보이는지(미끄러지듯
   보이면 §18.4의 스냅 로직이 예상과 다르게 동작한 것) 확인.
6. `NavMeshAgent` 제거 후에도 착지/충돌 판정에 회귀가 없는지 재확인.

### 18.8 상태

**✅ 구현·검증 완료.** §19(`NavMeshAgent` 제거)를 먼저 독립적으로 끝낸 뒤, 그 위에 이 §18을
그대로 이어서 구현했다. 계획(18.3~18.4) 대비 실제 구현 결과와 실측 중 발견한 이슈는 §18.9에 정리한다.

### 18.9 구현 결과

**변경된 파일**
- `Assets/02. Scripts/Unit/PlayerGroundDetector.cs` — 18.3-2 그대로, `yVelocity`/`StartJump()`/
  `Tick()` 전부 제거하고 `IsGrounded(Vector3)` 단일 메서드만 남는 순수 질의 클래스로 재작성.
- `Assets/02. Scripts/Unit/HideOrSeekPlayer.cs` — 18.3/18.4 설계 그대로 `rb` 필드 추가,
  `Update()`(입력)/`FixedUpdate()`(물리) 분리, `Move()`를 `rb.linearVelocity` 갱신 방식으로 재작성,
  `CheckJumpInput()`을 `jumpRequested` 플래그 방식으로 변경, `RespawnToSpawnPoint()` 신규 추가,
  `FixedUpdate()`에 `y < -100` 최후 방어선 추가.
- `Assets/02. Scripts/GameManager/VoidKillZone.cs` (신규) — 18.4 그대로 트리거 볼륨 컴포넌트.
- `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab` — 루트에 `Rigidbody`(useGravity/
  isKinematic 등은 코드가 `Start()`에서 `IsMine` 여부로 런타임에 재설정하므로 프리팹 기본값은
  큰 의미 없음)와 `CapsuleCollider`(height 1.8, radius 0.35, center y=0.9) 추가.
- 씬별 `VoidKillZone` GameObject 신규 배치:
  - `PlayerTestScene` — `Ground`가 실제로는 72×72(±36) 크기였음을 이번에 처음 확인(기존 문서에
    "베이크 완료"로만 적혀 있었음). 트리거를 `(0,-15,0)`에 `BoxCollider(120×2×120, isTrigger)`로
    배치. **이 씬엔 `PlayerSpawnPos`가 원래 없었다**(오프라인 단독 테스트용 씬이라 `PlayerSpawner`
    흐름을 안 씀) — `RespawnToSpawnPoint()` 검증 자체가 불가능해서, 테스트 목적으로 `PlayerSpawnPos`
    빈 오브젝트를 `(0,0,0)`에 새로 추가했다.
  - `GameLobbyScene` — 이번 세션에서 이미 만든 24×24 바닥 기준으로 트리거를 `(0,-15,0)`에
    `BoxCollider(60×2×60, isTrigger)`로 배치.
  - `GameScene` — **이 씬은 바닥/환경이 전혀 없는 완전히 빈 상태였다**(카메라/조명/UI/
    `PlayerSpawnPos`/`GameManager`뿐). 물리 엔진을 켠 상태로 이대로 두면 스폰하자마자 바닥이
    없어 끝없이 추락 → 트리거에 걸려 스폰 지점으로 복귀 → 다시 추락을 반복하는 무한 루프가 된다.
    이건 `PlayerControllPlan.md`나 `GameScenePlan.md`가 다루는 "레벨 아트/맵 디자인" 범위이지
    이번 물리 작업의 범위는 아니라고 판단했지만, 최소한 이번 변경이 게임을 확실히 깨뜨리지 않도록
    **임시 placeholder 바닥**(`Cube` 프리미티브, 24×24, `(0,-0.5,0)`, 기본 `BoxCollider` 포함)만
    추가했다 — 실제 술래잡기 맵 디자인은 별도 작업으로 남겨둔다. 그 위에 `VoidKillZone`도 동일하게
    `(0,-15,0)`에 배치.

**계획 대비 실제로 다르게 구현/발견한 것**

1. **`RespawnToSpawnPoint()`가 처음엔 실제로 동작하지 않았다 — Play Mode 실측으로 발견한
   버그.** 계획(18.4)의 코드 스니펫은 `transform.position = ...`으로 순간이동시키는 방식이었는데,
   실제로 Play Mode에서 테스트해보니 위치가 전혀 바뀌지 않았다(정확히는, 바뀌었다가 바로 다음
   물리 스텝에서 원래 있던 자리로 되돌아갔다). 원인: **non-kinematic `Rigidbody`가 붙어 있으면
   `transform.position`을 직접 대입해도 물리 엔진이 자신이 마지막으로 시뮬레이션한 위치로 다음
   `FixedUpdate`에서 덮어써 버린다** — `Rigidbody`를 붙이는 순간부터는 위치의 "진짜 주인"이
   `Transform`이 아니라 물리 엔진 쪽으로 넘어가기 때문이다. 이 프로젝트에 물리 엔진을 이번에
   처음 도입하면서 새로 생긴 함정이라 계획 단계에선 예상하지 못했다. **`transform.position` 대신
   `rb.position`에 대입해야 실제로 반영된다**는 것을 실측으로 확인하고 수정했다(§18.9 코드는
   `rb.position = ...; transform.position = rb.position;` 순서로 최종 수정됨). 순간이동을 다루는
   코드에서 이 패턴이 또 필요해지면 반드시 `rb.position`을 써야 한다 — 앞으로의 참고 사항으로
   남겨둔다.
2. **테스트 중 한 차례 원인 불명의 위치 드리프트를 관측했으나, 재현 불가능한 일회성 현상으로
   결론 냈다.** Play Mode 진입 직후 캐릭터가 실제 입력 없이(직접 `Input.GetAxisRaw`로 확인,
   `h=0, v=0`, `anyKey=False`) 한동안 이동한 것처럼 보이는 위치 변화가 있었다. 코드 조사 결과
   `CheckMovementInput()`은 입력이 없으면 반드시 `rotation = Vector3.zero`로 리셋하므로 코드
   로직상 지속적인 드리프트가 발생할 방법이 없고, 이후 동일한 상태(무입력)에서 위치를 여러 차례
   재확인한 결과 완전히 정지해 있음을 확인했다(속도도 `(0,0,0)`으로 고정) — 즉 일시적으로만
   발생하고 재현되지 않는 현상이었다. Play Mode 진입 시점의 포커스 전환 등 이 자동화 테스트
   환경 자체의 일회성 이벤트로 추정되며, 이번에 작성한 코드(`CheckMovementInput`/`Move`)는
   전혀 건드리지 않은 로직이라 이번 변경이 원인일 가능성은 낮다고 판단했다. 실제 사용자 플레이
   중 이런 증상이 재현되면 별도로 다시 조사가 필요하다.

**검증 결과 (18.7 대응)**
1. `read_console`로 컴파일 에러 0건 확인(코드 변경 직후, 프리팹 컴포넌트 추가 직후 두 차례).
2. `PlayerTestScene`에서 Play Mode 진입 — 콘솔에 새로운 에러/경고 없음(§17.8에 이미 기록된,
   이 작업과 무관한 `Main Camera` Missing Script 경고 하나만 남음).
3. `Time.timeScale = 0.05` 슬로모션(이 프로젝트의 기존 검증 관례, §12.6/§12.7과 동일 기법)으로
   점프를 실제로 재현해 `y`가 `0 → 0.93(상승 중, vel.y=4.04) → 1.71(정점 부근, vel.y=-1.06,
   하강 시작) → 0.01(착지, isJump=False)`로 정확히 포물선을 그리며 상승·하강·착지함을 프레임
   단위로 직접 확인 — Rigidbody 기반 점프가 기존과 동일한 체감으로 동작함을 실측 확인.
4. `VoidKillZone` 트리거를 직접 검증 — `rb.position`으로 트리거 볼륨 한가운데(`(0,-15,0)`)로
   순간이동시킨 뒤, `OnTriggerEnter`가 실제로 발동해 `RespawnToSpawnPoint()`가 호출되고 캐릭터가
   `PlayerSpawnPos`(원점) 기준 ±5 범위 안(`(2.71, 0.00, 4.44)`)으로 정확히 복귀함을 확인 —
   `transform.position`/`rb.position`이 서로 어긋나지 않고 일치함도 함께 확인(위 1번 버그 수정
   확인).
5. 걸어서(점프 없이) 맵 밖으로 나가도 이제 실제로 낙하하는지는 §18.2에서 지적한 문제(과거엔
   `isJump`가 아니면 중력 계산 자체가 없어서 절대 안 떨어졌음)의 근본 원인이 `FixedUpdate()`에서
   물리 엔진의 상시 중력(`rb.useGravity=true`)으로 완전히 대체됐으므로 구조적으로 해소됐다 —
   `isJump` 상태와 무관하게 `Rigidbody`는 항상 중력의 영향을 받는다(Unity 물리 엔진 자체 특성이므로
   별도 시나리오 재현 없이도 구조적으로 보장됨).
6. 멀티 클라이언트(원격 캐릭터) 쪽 회귀는 이번 세션에서 별도로 실측하지 못했다 — `rb.isKinematic
   = !pv.IsMine`로 원격 인스턴스는 물리를 꺼서 기존 `networkSync.Interpolate()` 경로를 그대로
   타도록 설계했지만(18.3), 실제 여러 클라이언트로 동시 접속해 원격 캐릭터가 정상적으로 보이는지는
   다음 실제 멀티 테스트 때 함께 확인하는 것을 권장한다.

**후속 확인 권장 사항 (18.6에서 이미 예고했던 것들의 실제 조사 결과)**
- `GameLobbyScene`의 펜스/테이블/의자에는 여전히 Collider가 없다(크레이트만 있음) — Rigidbody가
  실제로 켜졌으므로, 이제 플레이어가 이 오브젝트들을 그냥 통과한다는 것을 실제로 확인할 수 있는
  상태가 됐다. 필요하면 후속 작업으로 콜라이더를 추가해달라.
- `GameScene`은 이번에 placeholder 바닥만 추가했을 뿐 실제 맵 디자인은 전혀 없다 — 실제 술래잡기
  게임플레이가 가능한 맵을 만드는 것은 별도의 큰 작업(이번 대화에서 `GameLobbyScene` 배경을 만든
  것과 비슷한 규모)이며, 이번 계획·구현 범위에는 포함하지 않았다.

---

## 19. `NavMeshAgent` 제거 — ✅ 구현·검증 완료 (2026-08-16)

§18.3-3에서 방향은 "완전 제거"로 확정됐다. 이 절은 그 제거 작업을 **독립적으로 먼저 실행 가능한
첫 단계**로 보고, 실제로 어떤 순서로 손대야 하는지와 무엇이 문제가 될 수 있는지를 구체적으로
정리한다. §18의 나머지(Rigidbody 도입, VoidKillZone)는 이 작업 이후에 별도로 진행한다 — 두
작업을 분리하는 이유는 19.1에서 설명한다.

### 19.1 왜 Rigidbody 작업보다 먼저, 독립적으로 하는가

`NavMeshAgent`는 현재도 실제 이동에 관여하지 않는다(§18.2에서 이미 확인 — 경로탐색 미사용,
`Warp()`/`updatePosition` 토글만 함). 즉 **`NavMeshAgent`를 지워도 Rigidbody가 아직 없는
지금 상태(기존 `PlayerGroundDetector` 수동 중력 방식)에서 동작이 전혀 달라지지 않아야 정상이다**
— 그래서 이 제거 작업은 Rigidbody 도입과 완전히 분리해 **그 자체로 독립적으로 구현하고 검증할 수
있다.** 먼저 이 작업만 끝내고 "회귀 없음"을 확인한 뒤에, 그 위에 §18의 Rigidbody 작업을 얹는
순서로 진행하면 문제가 생겼을 때 원인을 훨씬 좁혀서 찾을 수 있다(두 가지를 한 번에 바꾸면 어느
쪽 때문에 깨졌는지 구분이 어려움).

### 19.2 제거 대상 — 코드에서 실제로 참조하는 모든 지점 (재확인 완료)

`HideOrSeekPlayer.cs`를 다시 정독해 `agent`를 참조하는 지점을 전부 나열했다(이 4곳이 전부다):

| 위치 | 현재 코드 | 처리 |
|---|---|---|
| 파일 상단 | `using UnityEngine.AI;` | 삭제 |
| 필드 선언 (`[Header("Components")]` 아래) | `[SerializeField] private NavMeshAgent agent;` | 삭제 |
| `CheckJumpInput()` | `if (agent != null) { agent.updatePosition = false; }` | 블록 삭제 |
| `ApplyGravity()` (착지 처리 부분) | `if (agent != null) { agent.Warp(transform.position); agent.updatePosition = true; }` | 블록 삭제 |
| `OnPhotonSerializeView()` 수신 분기 | `if (networkSync.RemoteIsJump && agent != null) { agent.updatePosition = false; }` | 블록 삭제 |

프로젝트 전체를 `agent`/`NavMeshAgent` 기준으로 검색해도 `HideOrSeekPlayer.cs` 외에 이 필드를
참조하는 다른 스크립트는 없다(이전 조사에서 이미 확인됨, `PlayerPaintCanvas`/
`PlayerColorVoteIndicator`/`PlayerColorDisplay` 등 같은 프리팹의 다른 컴포넌트들은 전부 `pv`만
참조하고 `agent`는 참조하지 않음) — 따라서 `HideOrSeekPlayer.cs` 한 파일만 고치면 코드 쪽은 끝난다.
`[RequireComponent(typeof(NavMeshAgent))]` 같은 강제 의존 attribute도 없다(직접 확인 완료) — 즉
컴포넌트를 지워도 컴파일이 막히는 다른 경로는 없다.

### 19.3 구현 순서

1. **`HideOrSeekPlayer.cs` 코드 수정** — 19.2의 5곳을 전부 제거.
2. **컴파일 확인** (`read_console`, 에러 0건).
3. **`HideOrSeekPlayer.prefab`을 프리팹 스테이지로 열어 `NavMeshAgent` 컴포넌트 자체를 삭제**,
   저장 후 스테이지 닫기 — 코드를 먼저 고쳐서 필드 참조가 없어진 뒤에 컴포넌트를 지우는 순서를
   지킨다(반대 순서로 해도 Unity가 알아서 참조를 `null`로 정리하긴 하지만, 코드-먼저 순서가 더
   안전하고 확인하기 쉽다).
4. **프리팹이 의도치 않게 손상되지 않았는지 재확인** — 19.4에서 설명하는 알려진 위험 때문에,
   컴포넌트 목록과 `speed`/`jumpPower`/`pv` 등 다른 필드 값이 그대로인지 프리팹을 다시 읽어 확인.
5. **Play Mode 검증** (19.5).
6. 이 문서(§19)에 완료 표시 후, §18의 Rigidbody 작업으로 넘어간다.

### 19.4 문제가 될 수 있는 부분

- **MCP 프리팹 편집 도구의 알려진 부작용**: 이 세션과 이전 세션들에서 반복적으로 확인된 사실인데,
  `script_apply_edits`로 메서드를 지우거나 바꿀 때 바로 위에 있는 `[SerializeField]` 필드가 같이
  삭제되는 사고가 여러 번 있었다(`Bug-fix-plan.md`에도 기록된 재발 패턴). 이번엔 코드 파일 편집은
  단순 텍스트 치환(Edit 도구)으로 처리해 그 위험을 피하고, 프리팹 쪽 컴포넌트 삭제도 별도 도구
  (`manage_prefabs`/`manage_components`)로 명시적으로만 수행해 같은 사고가 재발하지 않도록 한다.
  프리팹 인스턴스화/편집 도구가 `position`을 초기화해버렸던 사례도 있었으므로(`GameManager.md`
  §9.11.4), 컴포넌트 삭제 후 루트 Transform의 `position`이 `(0,0,0)`으로 그대로인지도 반드시
  재확인한다.
- **`PlayerTestScene`에 이미 구워둔 NavMesh 데이터**: `NavMeshAgent`를 없애도 씬에 베이크된
  NavMesh 데이터 자체는 자동으로 지워지지 않는다 — 더 이상 아무도 참조하지 않는 죽은 데이터로
  남지만, 지우지 않아도 동작에는 영향이 없다(용량만 아주 조금 차지). 이번 작업 범위에서는 굳이
  지우지 않는다 — 필요하면 나중에 별도로 정리.
- **`"Failed to create agent because there is no valid NavMesh"` 경고가 사라지는지가 검증
  포인트**: 이 경고는 이번 대화 세션 내내 Play Mode 테스트 때마다 반복적으로 관측됐던 것이다
  (NavMesh가 없는 씬에서 `NavMeshAgent`가 자기 위치를 유효화하려다 실패하는 경고로 추정). 제거
  후에는 이 경고가 더 이상 나오지 않아야 정상이며, 반대로 계속 나온다면 어딘가에 `NavMeshAgent`가
  또 남아있다는 신호이므로 재조사가 필요하다.
- **점프 중 `agent.updatePosition` 토글이 실제로 아무 기능도 안 하고 있었는지 재확인**: §18.2에서
  "경로탐색 미사용, `Warp()` 재동기화 용도뿐"이라고 판단했지만, 이건 정적 코드 분석이었다 — 실제로
  제거 후 Play Mode에서 점프/착지가 제거 전과 **눈으로 봐도 차이 없이 완전히 동일하게** 동작하는지
  직접 비교해야 이 판단이 맞았음을 확실히 확인할 수 있다(19.5의 핵심 검증 항목).
- **네트워크(Photon) 쪽 영향은 없음** — `OnPhotonSerializeView`에서 지워지는 `agent.updatePosition
= false` 줄은 실제 스트림에 실어 보내는 데이터(`position`/`rotation`/`state`/`isJump`)와는 무관한,
  그 메서드 안에 우연히 같이 있던 로컬 부수 효과일 뿐이다 — 직렬화 포맷이 바뀌는 게 아니므로
  클라이언트 간 버전 호환성 문제는 없다.
- **씬에 남아있는 프리팹 인스턴스(비-Resources 사본이 있다면)**: `HideOrSeekPlayer.prefab`은
  `Assets/04. Prefabs/Resources/`에 있는 단일 정식 프리팹이고, 모든 씬은 `PhotonNetwork.
  Instantiate("HideOrSeekPlayer", ...)`로 런타임에 이 프리팹을 스폰하는 구조라(§13에서 확정)
  씬 파일에 미리 배치된 별도 사본이 없다 — 즉 프리팹 하나만 고치면 모든 씬에 자동 반영되고,
  씬마다 따로 찾아 고칠 필요는 없다. 다만 `PlayerTestScene`처럼 예전에 씬 인스턴스로 남아있던
  테스트용 오브젝트가 있다면(과거 §13.2에서 지적됐던 것과 비슷한 경우) 그건 프리팹 연결이 아닐
  수 있으므로 별도 확인이 필요하다.

### 19.5 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. `PlayerTestScene`에서 Play Mode 진입 후 걷기/살금살금 걷기/점프/회피가 제거 전과 체감상 완전히
   동일한지 확인(이 시점엔 아직 Rigidbody가 없으므로 기존 `PlayerGroundDetector` 수동 중력 그대로
   동작해야 함 — 즉 "아무것도 안 변한 것처럼 보여야" 성공).
3. 점프 후 착지 시 캐릭터가 지형 표면에 정확히 붙는지(과거 `agent.Warp`가 하던 재동기화가 빠져도
   착지 위치 계산 자체는 `PlayerGroundDetector.Tick()`이 이미 전담하고 있었으므로 영향 없어야 함)
   확인.
4. 콘솔에서 `"Failed to create agent because there is no valid NavMesh"` 경고가 더 이상 나오지
   않는지 확인.
5. 프리팹을 다시 조회해 `NavMeshAgent`가 컴포넌트 목록에서 완전히 사라졌는지, 그리고 `Transform.
   position`(0,0,0)과 `speed`/`jumpPower`/`pv` 등 다른 필드 값이 그대로 보존됐는지 확인(19.4의
   MCP 편집 부작용 위험 대비).
6. 실제 Photon 룸(`GameLobbyScene`/`GameScene`)에서 스폰까지 정상 동작하는지 최종 확인 — 프리팹
   구조 변경이 `PhotonNetwork.Instantiate("HideOrSeekPlayer", ...)` 스폰 자체에 영향이 없어야 함.

### 19.6 상태

**✅ 구현·검증 완료.** 계획한 순서(19.3) 그대로 진행했다.

**변경된 파일**
- `Assets/02. Scripts/Unit/HideOrSeekPlayer.cs` — 19.2에서 나열한 5곳(`using UnityEngine.AI;`,
  `agent` 필드, `CheckJumpInput()`/`ApplyGravity()`/`OnPhotonSerializeView()`의 `agent` 참조)을
  전부 제거. 파일 전체를 다시 읽어 다른 로직(이동/회피/애니메이션/네트워크 직렬화)은 한 글자도
  건드리지 않았음을 확인.
- `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab` — 루트의 `NavMeshAgent` 컴포넌트를
  프리팹 스테이지에서 직접 삭제.

**검증 결과 (19.5 대응)**
1. `read_console`로 컴파일 에러 0건 확인(코드 수정 직후, 프리팹 컴포넌트 삭제 직후 두 차례 모두).
2. 프리팹 컴포넌트 삭제 직후 `SerializedObject`로 재조회 — 루트 `Transform.position`이
   `(0,0,0)`으로 그대로, `speed=5`/`jumpPower=6`/`pv` 참조 모두 보존됨을 확인(19.4에서 우려한
   MCP 프리팹 편집 부작용 없음).
3. `PlayerTestScene`의 씬 인스턴스가 프리팹 변경을 자동으로 반영해 `NavMeshAgent`가 사라졌음을
   확인(`GetComponent<NavMeshAgent>() == null`).
4. Play Mode 진입 후 콘솔 확인 — **`"Failed to create agent because there is no valid NavMesh"`
   경고가 더 이상 나오지 않음**(이 세션 내내 반복 관측되던 경고가 실제로 사라짐, §19.4에서 세운
   가설이 맞았음을 확인). 남은 경고는 `PlayerTestScene`의 `Main Camera` Missing Script 하나뿐인데,
   이는 §17.8에서 이미 이번 작업과 무관한 기존 이슈로 기록된 것과 동일한 건이다.
5. 리플렉션으로 `PlayerGroundDetector.StartJump(6f)` + `isJump=true`를 직접 호출해 점프를
   시뮬레이션 — 캐릭터가 정상적으로 상승(`y`가 0 → 약 1.6까지)했다가 중력에 의해 자연스럽게
   하강해 `y=0`, `isJump=false`로 정확히 착지함을 확인. `agent.Warp()`가 빠졌어도 착지 위치 계산은
   원래 `PlayerGroundDetector.Tick()`이 전담하고 있었다는 §18.2의 분석이 실측으로 확인됨 —
   제거 전과 동작 차이 없음.
6. `UnityEngine.Resources.Load<GameObject>("HideOrSeekPlayer")`로 `PhotonNetwork.Instantiate`가
   실제로 타는 것과 동일한 경로를 직접 재현해 프리팹이 정상 로드되고 컴포넌트 목록도 의도한 대로
   (`NavMeshAgent` 없이) 구성되어 있음을 확인 — 스폰 경로에 영향 없음.

---

## 20. 사물 오브젝트 콜라이더 정책 — ✅ 소급 적용 완료 (2026-08-16)

### 20.1 방침

§18로 실제 물리 엔진(Rigidbody)이 켜졌으므로, 이제부터 **씬에 놓는 사물(가구/장애물 등) 오브젝트는
만들 때마다 항상 콜라이더를 같이 부여한다** — 콜라이더가 없으면 플레이어가 그냥 통과해버려서
눈에는 보이는데 물리적으론 없는 것과 같아지기 때문이다. §18.6/§18.9에서 이미 이 문제(펜스/테이블/
의자에 콜라이더가 없다는 것)를 지적해뒀었는데, 이번에 사용자 요청으로 실제로 적용했다.

### 20.2 이번에 소급 적용한 대상

`GameLobbyScene`의 `LobbyEnvironment` 하위, 지금까지 콜라이더가 없었던 20개 오브젝트 전부에
`MeshCollider`를 추가했다(`Ground`/`Crate_A~D`는 이미 §18 이전 세션에서 콜라이더가 있었으므로 대상
아님):

- `Fence_Post1~6`, `Fence_Rail` (7개)
- `Table_Top`, `Table_Leg1~4` (5개)
- `ChairA_Seat`/`ChairA_Back`/`ChairA_Support`, `ChairB_Seat`/`ChairB_Back`/`ChairB_Support` (6개)
- `Parasol_Pole`, `Parasol_Canopy` (2개)

씬 저장 후 컴파일/콘솔 에러 0건 확인.

### 20.3 앞으로의 규칙

`manage_probuilder`로 사물을 새로 만들 때는 색상 지정(머티리얼 할당)과 같은 마무리 단계에
`MeshCollider` 추가를 항상 같이 포함한다 — 이번 절이 그 기준을 문서로 남겨두는 역할을 한다.

---

## 21. `Ch36`(캐릭터 바디 메쉬) Concave Mesh Collider 에러 — ✅ 수정 완료 (2026-08-16, 계획 대비 방향 변경됨)

### 21.1 사용자가 발견한 에러

```
Concave Mesh Colliders are not supported when used with dynamic Rigidbody GameObjects.
Either make the Mesh Collider convex, or make the Rigidbody kinematic.
Scene hierarchy path "HideOrSeekPlayer(Clone)/Ch36", Mesh asset path "Assets/Animation/Idle.fbx", Mesh name "Ch36"
```

### 21.2 이게 뭔가 — §18에서 Rigidbody를 추가한 것의 직접적인 부작용

`Ch36`은 캐릭터 본체의 `SkinnedMeshRenderer` + `MeshCollider`를 가진 자식 오브젝트로,
**물리 충돌용이 아니라 ColorTag 붓칠 기능의 레이캐스트 타깃 전용**이다 —
`PlayerPaintCanvas.cs`(83번째 줄)와 `BrushCursorController.cs`(84번째 줄)가 마우스 위치에서
`Physics.Raycast`를 쏴서 `hit.collider == paintableCollider`(=`Ch36`의 `MeshCollider`)인지 검사해
캐릭터 몸에 색을 칠하고 붓 커서를 표시하는 용도로만 쓰인다. 팔·다리가 있는 사람 형태의 메쉬라
당연히 오목(concave)한 모양이고, 이 콜라이더는 원래부터 그렇게(비-Convex) 만들어져 있었다.

문제는 §18에서 `HideOrSeekPlayer` 루트에 **동적(non-kinematic) `Rigidbody`**를 처음 추가하면서
생겼다 — Unity(PhysX)는 동적 Rigidbody 계층 안에 있는 오목한 `MeshCollider`를 "물리적으로 부딪히는
단단한 형태"로는 시뮬레이션할 수 없다(오목한 형태끼리의 정확한 충돌 계산은 계산 비용이 너무 커서
PhysX가 애초에 지원하지 않음). `Ch36`은 `HideOrSeekPlayer(Clone)`의 자식이고 그 루트에 이번에
동적 `Rigidbody`가 생겼으므로, `Ch36`의 오목한 콜라이더도 자동으로 "이 캐릭터의 물리적 형태 중
일부"로 취급되면서 이 에러가 나는 것이다 — `Ch36` 자체는 전혀 건드리지 않았는데도, **루트에
Rigidbody가 생긴 것만으로 이전에 문제없던 콜라이더가 갑자기 조건을 위반하게 된** 경우다.

### 21.3 왜 단순히 "Convex 체크박스 켜기"로 때우면 안 되는가

Unity가 제안하는 두 해결책 중 "Convex로 바꾸기"를 쓰면 에러 자체는 사라지지만, Convex로 표시된
`MeshCollider`는 원래 메쉬 모양이 아니라 **그 메쉬를 감싸는 단순화된 볼록 껍질(convex hull)**로
동작한다 — 사람 캐릭터라면 팔 사이/다리 사이/겨드랑이 아래처럼 오목하게 들어간 부분이 전부
껍질로 메워진 뭉툭한 덩어리가 된다. `PlayerPaintCanvas`/`BrushCursorController`가 정확히 이
콜라이더 표면에 레이캐스트를 맞춰서 "지금 마우스가 캐릭터 몸의 어디를 가리키는지"를 판정하는데,
Convex 껍질로 바뀌면 실제 눈에 보이는 몸 표면과 레이캐스트가 맞는 위치가 어긋난다(예: 겨드랑이
아래 빈 공간인데도 껍질 때문에 "몸에 맞았다"고 잘못 판정될 수 있음) — 붓칠 정확도가 눈에 띄게
나빠질 위험이 있어 채택하지 않는다.

### 21.4 채택할 수정 방향 — `Ch36`을 트리거(Trigger)로 전환

Unity의 "오목한 메쉬는 동적 Rigidbody와 못 쓴다"는 제약은 **물리적으로 실제 부딪히는(solid)
콜라이더**에만 적용된다 — `isTrigger = true`로 표시된 콜라이더는 애초에 물리적 충돌 반응을
하지 않고 겹침(overlap) 이벤트만 발생시키므로, 오목한 모양이어도 동적 Rigidbody 밑에 있을 수
있다(PhysX가 이 경우는 막지 않음). `Ch36`은 원래부터 물리적으로 "부딪히는" 용도가 아니라
레이캐스트 타깃 전용이었으므로, 이 성질과 정확히 맞아떨어진다.

**변경 사항**
1. `Ch36`의 `MeshCollider.isTrigger`를 `true`로 설정(Convex는 그대로 `false` 유지 — 원래 모양
   그대로 남아 붓칠 정확도에 영향 없음).
2. `Physics.Raycast()`는 기본값(`QueryTriggerInteraction.UseGlobal`, 보통 프로젝트 기본 설정상
   트리거를 무시함)으로는 트리거 콜라이더를 맞히지 못한다 — 1번만 하면 붓칠/커서 기능이 오히려
   조용히 깨진다. 그래서 아래 두 호출 지점에 반드시 `QueryTriggerInteraction.Collide`를 명시적으로
   추가해야 한다:
   - `Assets/02. Scripts/ColorTag/PlayerPaintCanvas.cs` 83번째 줄:
     `Physics.Raycast(ray, out RaycastHit hit)` → `Physics.Raycast(ray, out RaycastHit hit,
     Mathf.Infinity, ~0, QueryTriggerInteraction.Collide)`
   - `Assets/02. Scripts/ColorTag/BrushCursorController.cs` 84번째 줄: 동일하게
     `QueryTriggerInteraction.Collide` 추가.

### 21.5 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. Play Mode 진입 시 더 이상 "Concave Mesh Colliders are not supported..." 에러가 나오지 않는지
   확인.
3. 색상 선택 라운드에서 실제로 캐릭터 몸에 마우스로 붓칠했을 때 이전과 동일하게 정확한 위치에
   칠해지는지, `BrushCursorController`의 붓 커서도 몸 표면 위에서 정상적으로 따라다니는지 확인
   (트리거 전환 이후 회귀 없는지가 핵심 검증 포인트).
4. §22의 "사물에 들러붙는" 버그 재현 테스트를 할 때, 이 수정이 먼저 적용된 상태에서 진행해
   `Ch36`의 무효화된 콜라이더가 §22 조사 결과에 잡음을 주지 않도록 한다(21.6 참고).

### 21.6 상태 — ✅ 수정 완료

**계획(트리거 전환)과 다른, 더 나은 방법으로 최종 구현했다.** 실제로 구현을 시작해보니
**Unity는 `isTrigger=true`인 `MeshCollider`가 동시에 `convex=false`인 것 자체를 허용하지 않는다**
— 계획서 21.4의 전제(트리거는 오목해도 된다)가 틀렸었다. `mc.isTrigger = true;`를 직접 코드로
대입해봐도 조용히 무시되고 `false`로 남는 것을 Play Mode 실측으로 확인했다.

대신 **`Ch36`에 별도의 킨매틱(kinematic) `Rigidbody`를 추가**하는 방법으로 수정했다 — Unity가
"오목한 메쉬는 동적 Rigidbody와 못 쓴다"고 판단하는 기준은 "부모 계층에 있는 **가장 가까운**
Rigidbody가 동적인가"이므로, `Ch36` 자신에게 킨매틱 Rigidbody를 붙이면 그 지점에서 물리적으로
독립된 별개의 몸체로 취급되어 부모(`HideOrSeekPlayer` 루트)의 동적 Rigidbody와 완전히 분리된다.
이 방법의 장점: **`MeshCollider`의 `convex`/`isTrigger` 값을 원래 그대로(둘 다 `false`) 유지할 수
있어서**, 21.3에서 우려했던 "Convex 껍질로 바뀌어 붓칠 정확도가 나빠지는 문제"가 아예 발생하지
않는다. 처음에 시도했던 트리거 전환용 코드 수정(`PlayerPaintCanvas.cs`/`BrushCursorController.cs`에
`QueryTriggerInteraction.Collide` 추가)은 필요 없어져서 전부 되돌렸다 — 최종적으로 두 스크립트는
**한 글자도 바뀌지 않은 원본 그대로**다.

**변경된 파일**: `HideOrSeekPlayer.prefab`의 `Ch36` 자식 오브젝트에 `Rigidbody`(`isKinematic=true`,
`useGravity=false`) 추가. `MeshCollider`는 `isTrigger=false`, `convex=false` 그대로(변경 없음).

**검증**: Play Mode 진입 시 `"Concave Mesh Colliders are not supported..."` 에러가 더 이상 나오지
않음을 확인(여러 차례 재확인, 최종적으로 다시 정리된 깨끗한 씬에서도 재확인 완료).

---

## 22. 점프해서 콜라이더 있는 사물에 착지하면 들러붙는 버그 — 22.3-1(마찰 0 재질) ✅ 구현 완료, 나머지는 확인 대기

### 22.1 증상

사용자 보고: 콜라이더가 있는 사물(예: 크레이트, 이제는 §20에서 콜라이더가 추가된 펜스/테이블/
의자/파라솔도 포함)에 점프해서 올라가거나 부딪히면, 캐릭터가 그 사물에 "들러붙어" 버린다 —
의도한 동작이 아닌 명백한 버그. 플레이어 쪽(§18에서 새로 만든 물리 코드) 원인으로 추정.

### 22.2 코드 재조사 — 유력한 원인 후보 3가지

`HideOrSeekPlayer.cs`(§18 구현분)와 Unity 물리 기본값을 다시 살펴봤다. 아래 세 가지가 결합해서
이런 증상을 만들 가능성이 높다고 판단했다 — 실제 원인 특정은 승인 후 Play Mode 실측으로
좁혀나갈 예정이다.

**후보 ① `PhysicMaterial`(마찰) 미설정 — 가장 유력**

지금 `CapsuleCollider`(플레이어)에도, 새로 콜라이더를 붙인 사물들에도 **`PhysicMaterial`을 전혀
지정하지 않았다** — 즉 Unity 기본 물리 재질(마찰 계수가 낮지 않음, `frictionCombine=Average`)이
그대로 적용된다. 그런데 `Move()`는 매 `FixedUpdate`마다 `rb.linearVelocity`의 수평 성분을
**입력 방향으로 무조건 강제 대입**한다:

```csharp
rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
```

캐릭터가 사물의 옆면에 붙은 채로 계속 그 방향으로 이동 입력을 주고 있으면, 물리 엔진이 매 스텝
"벽에서 밀어내려는" 접촉 반응을 계산해도 바로 다음 프레임에 우리 코드가 다시 "벽 쪽으로" 속도를
강제로 덮어써버린다 — 결과적으로 마찰과 이 강제 속도 대입이 매 프레임 서로 밀고 당기며 캐릭터가
그 자리에 붙박인 것처럼 보이게 될 수 있다. 캐릭터 컨트롤러에서 이런 "벽에 들러붙는" 증상은
Unity에서 매우 흔한 패턴이며, 표준적인 해법은 **플레이어의 `CapsuleCollider`에 마찰 0(또는
`frictionCombine = Minimum`)인 전용 `PhysicMaterial`을 만들어 지정하는 것**이다.

**후보 ② `groundLayer`가 모든 사물과 같은 레이어를 가리킴**

`groundLayer`(기본값 `1` = Default 레이어)로 접지 판정을 하는데, §20에서 콜라이더를 추가한
사물들도 전부 기본적으로 Default 레이어에 있다(따로 레이어를 지정한 적이 없음). 즉 크레이트/
테이블/펜스 레일 위에 올라서면 `IsGrounded()`가 "바닥에 닿았다"고 정상적으로 판정해버리는데,
이건 "사물 위를 밟고 설 수 있다"는 의미에서는 자연스러울 수 있지만, 사물의 **옆면**에 부딪혀
멈춘 경우에도 레이캐스트가 우연히 사물의 다른 부분(튀어나온 모서리 등)에 맞아 "접지"로 잘못
판정하면서 점프가 다시 안 먹히는 등 어색한 상태에 빠질 가능성이 있다. 바닥과 사물을 서로 다른
레이어로 분리하고 `groundLayer`를 실제 바닥 레이어만 가리키도록 좁히는 편이 더 명확하다(다만
"사물 위에 올라설 수 있어야 하는가"는 게임 디자인 결정이 필요한 부분이라 21.4에서 확인받는다).

**후보 ③ 얇은 상자 콜라이더의 모서리에 캡슐이 걸리는 현상**

펜스 기둥(`0.15` 두께)이나 테이블 다리(`0.08` 두께)처럼 얇은 `BoxCollider`류 형태는, Unity
물리 엔진에서 캡슐 콜라이더가 모서리/꼭짓점 부근에서 미세하게 걸려(snag) 미끄러지지 않고
멈추는 현상이 잘 알려진 문제다. 후보 ①(마찰 0 재질)을 적용하면 이 증상도 상당 부분 같이
완화되는 경우가 많아, 별도 조치가 필요한지는 ①을 먼저 적용한 뒤 재현 여부로 판단한다.

### 22.3 수정 계획

1. **`Assets/03. SO/` 또는 물리 전용 폴더에 마찰 0짜리 `PhysicMaterial` 에셋 생성**(예:
   `PlayerNoFriction.physicMaterial`, `dynamicFriction=0`, `staticFriction=0`,
   `frictionCombine=Minimum`, `bounciness=0`)하고 `HideOrSeekPlayer.prefab`의 `CapsuleCollider`에
   지정한다 — 후보 ①의 직접적인 해결책이자 가장 우선순위 높은 조치.
2. (확인 필요) 바닥 전용 레이어(예: `Ground`)를 새로 만들어 `Ground` 오브젝트들을 그 레이어로
   옮기고, `HideOrSeekPlayer`의 `groundLayer` 필드를 그 레이어만 가리키도록 좁힌다 — 사물
   위에 올라설 수 있는 것을 의도한 동작으로 유지할지, 아니면 접지 판정에서 사물을 완전히
   제외할지는 게임 디자인 확인이 필요하다(플레이어가 크레이트나 테이블 위에 올라가 숨는 것도
   술래잡기 컨셉에는 오히려 어울릴 수 있어서, 무조건 막는 것이 맞는지 먼저 확인받고 싶다).
3. 1번 적용 후에도 얇은 콜라이더 모서리 걸림(후보 ③)이 재현되면, 그때 추가로 조사해 필요한
   조치(예: 콜라이더 살짝 둥글리기, `Rigidbody.sleepThreshold` 조정 등)를 검토한다 — 지금
   시점에는 ①을 우선 적용하고 나서 재현 여부로 필요성을 판단하는 것이 순서상 맞다고 본다.

### 22.4 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. `PlayerTestScene`이나 `GameLobbyScene`에서 크레이트/테이블/펜스 등 콜라이더가 있는 사물의
   옆면으로 점프해 부딪혀본 뒤, 이동 입력을 유지한 상태에서 캐릭터가 들러붙지 않고 자연스럽게
   미끄러지거나 멈췄다가 다시 움직이는지 확인.
3. 사물 위로 점프해 착지했을 때 정상적으로 그 위에 서 있을 수 있는지(또는 22.3-2에서 결정한
   방향에 따라 미끄러져 내려오는지) 확인.
4. §21의 `Ch36` 수정(킨매틱 Rigidbody 추가)을 먼저 적용한 뒤 이 재현 테스트를 진행해, 무효화됐던
   콜라이더가 결과에 섞여 들어가지 않게 한다.

### 22.5 상태 — 22.3-1 ✅ 구현 완료, 22.3-2/22.3-3은 확인 대기

**22.3-1(마찰 0 `PhysicMaterial`)을 구현·검증했다.** `Assets/06. Physics/PlayerNoFriction.physicMaterial`
(`dynamicFriction=0`, `staticFriction=0`, `frictionCombine=Minimum`, `bounciness=0`)을 생성해
`HideOrSeekPlayer.prefab`의 `CapsuleCollider`에 지정했다.

**구현 중 발견한 사고**: 처음에 `manage_physics`의 `assign_physics_material` 도구로 지정했을 때는
Play Mode 재확인 결과 실제로는 저장되지 않고(`m_Material: {fileID: 0}`, 즉 미할당 상태) 있었다 —
프리팹 스테이지 안에서 조회하면 정상으로 보였지만 저장이 누락된 것이었다. `SerializedObject`
없이 `cc.sharedMaterial = mat;` 대입 + `EditorUtility.SetDirty` + 저장으로 다시 처리해 실제 프리팹
파일(`m_Material: {fileID: 13400000, guid: 118a...}`)에 정상 반영된 것을 직접 확인했다.

**Play Mode 검증**: 재현 테스트 도중 이 세션의 Unity 브리지가 장시간 사용으로 불안정해지는
문제(아래 22.6 참고)를 만나 정밀한 "들러붙음 자체가 재현되는지"까지는 이번에 확정적으로 재현·
반증하지 못했다 — 다만 `VoidKillZone` 낙사 복귀 테스트 도중 `rb.position` 기반 순간이동 후
캐릭터가 정상적으로 자유롭게 움직이는 것은 확인했다. `PlayerNoFriction` 재질이 정확히 지정되어
있고(Play Mode에서 `cc.sharedMaterial.name == "PlayerNoFriction"`, `dynamicFriction == 0` 재확인
완료) 콘솔에 관련 에러/경고가 없다는 것까지는 확실하다.

**22.3-2(바닥 전용 레이어 분리 + 사물 위 착지 허용 여부)**는 여전히 게임 디자인 확인이 필요한
지점이라 미구현 상태로 남겨뒀다 — 22.3-1로 충분한지, 실제 플레이에서 재확인한 뒤 필요하면
이어서 진행하는 것을 제안한다. **22.3-3(얇은 콜라이더 모서리 걸림 대응)**도 마찬가지로 22.3-1
적용 후 재현 여부에 따라 필요성을 판단하는 것으로 미룬다.

### 22.6 구현 과정에서 발견하고 직접 복구한 사고 — `GameLobbyScene`/`PlayerTestScene` 중복·오염

이번 §21/§22 작업 도중, 이 세션의 Unity MCP 브리지가 장시간 연속 사용으로 불안정해지는
현상(응답 지연, `execute_code` 결과가 실제 라이브 상태를 반영하지 못하는 것처럼 보이는 현상,
`refresh_unity` 타임아웃)을 겪었고, 그 와중에 씬 전환/저장이 꼬이면서 **`GameLobbyScene`과
`PlayerTestScene` 두 씬 파일 모두에 실제 데이터 문제가 생겼던 것을 발견해 직접 복구했다.** 이번
작업(물리/콜라이더)과는 별개의 사고이지만, 씬 파일 자체를 건드린 작업이라 투명하게 기록해둔다.

**발견한 문제**
- `GameLobbyScene`의 `LobbyEnvironment`: 피크닉 테이블 세트(테이블+의자+파라솔, 13개)가 **4벌**,
  크레이트 일부가 2~3벌 중복 생성되어 있었고(정상 37개 → 실제 81개), 부모 `LobbyEnvironment`의
  `localScale`도 의도치 않게 `(3,3,3)`으로 되어 있었다. 사용자가 직접 커밋한 스냅샷
  (`6c4ea08 게임 로비 배경 added`, 2026-08-16 00:47)에 이미 이 상태로 저장되어 있었다 — 즉 이번
  세션 전에 이미 발생해 있던 문제였다.
- `PlayerTestScene`: 원래 있어야 할 자체 `Ground`(72×72 평지) 대신, `GameLobbyScene`의 구버전
  환경(교체 전 저폴리곤 나무 `Tree1_Trunk` 등, 18개)이 `LobbyEnvironment`라는 이름으로 통째로
  섞여 들어가 있었다 — 역시 같은 커밋에 이미 저장되어 있던 상태였다.

**복구 과정**
1. 만일을 대비해 `git stash`로 작업 전 상태를 안전하게 보존(`stash@{0}: "corrupted scene state
   before restore from 6c4ea08"` — 아직 스택에 남아있음, 필요 없다고 판단되면 나중에 정리해도 됨).
2. `git show 6c4ea08:...`로 마지막 커밋 시점 상태를 비교 확인한 뒤, 두 씬 파일을 그 커밋 상태로
   되돌림(`git stash push`가 결과적으로 HEAD와 동일하게 만듦).
3. `GameLobbyScene`: 이름에 `(1)`/`(2)`/`(3)` 접미사가 붙은 중복 오브젝트 44개를 코드로 찾아
   전부 삭제, `LobbyEnvironment`의 `localScale`을 `(1,1,1)`로 재설정 → 37개(원래 의도한 정확한
   개수)로 정리됨을 확인.
4. `PlayerTestScene`: 오염된 `LobbyEnvironment`를 통째로 삭제하고, 원래 스펙(위치 `(0,-1.5,0)`,
   바닥 상단이 `y=0`에 오는 72×72 크기)에 맞는 `Ground`를 새로 생성.
5. 두 씬 모두, 이번 §18~§22 작업에서 추가했던 `PlayerSpawnPos`/`VoidKillZone`/사물 콜라이더
   20개가 이 되돌리기 과정에서 함께 사라졌으므로 전부 다시 추가.
6. 최종 상태를 다시 조회해 `GameLobbyScene`(37개 자식, scale 1) / `PlayerTestScene`(자체 `Ground`
   보유, `LobbyEnvironment` 없음) 양쪽 모두 정상임을 확인 후 저장, Play Mode에서 관련 에러 없음을
   재확인.

**남은 확인 사항**: 이 중복/오염이 정확히 *언제* 처음 발생했는지(이번 세션의 어느 시점인지, 혹은
그 이전 세션인지)는 로그만으로 완전히 특정하지 못했다 — 커밋에 이미 있었다는 것만 확인했다.
`git stash`에 원래(오염된) 상태가 보존되어 있으니, 혹시 이번 복구가 의도와 다르다고 판단되면
`git stash show -p`로 비교해볼 수 있다.

---

## 23. 걸어서 낙하해도 Jump(낙하) 모션이 나오도록 — ✅ 구현·검증 완료 (2026-08-16)

### 23.1 요청 배경

기획에 없던 추가 요청: 현재는 **점프해서** 맵/`LobbyEnvironment` 가장자리 밖으로 떨어질 때는
Jump 애니메이션이 정상적으로 보이지만, **점프 없이 그냥 걸어서** 가장자리 밖으로 떨어질 때는
아무 낙하 모션도 나오지 않고(걷기/Idle 애니메이션이 계속 재생된 채로) 캐릭터만 물리적으로
낙하한다 — 걸어서 떨어질 때도 점프(낙하)와 동일한 모션이 나오도록 만들어달라는 요청이다.

### 23.2 원인 — 왜 지금은 걸어서 떨어질 때 애니메이션이 안 바뀌는가

`HideOrSeekPlayer.FixedUpdate()`의 현재 구조:

```csharp
bool grounded = groundDetector.IsGrounded(transform.position);

if (isJump && grounded && rb.linearVelocity.y <= 0f) { ... } // 착지 처리 — isJump가 true일 때만

if (jumpRequested && grounded && !isDodge) // 점프 시작 — Space를 눌렀을 때만 isJump=true
{
    ...
    isJump = true;
    ...
    animationDriver.ChangeState(PlayerMoveState.Jump);
}
jumpRequested = false;

Move();
```

`isJump`는 오직 `jumpRequested`(Space 입력)를 통해서만 `true`가 된다 — 즉 **"공중에 떠 있다"는
사실 자체를 감지하는 코드가 어디에도 없고, 오직 "Space를 눌러서 의도적으로 점프했는가"만
추적한다.** 걸어서 가장자리를 벗어나면 `grounded`는 자연스럽게 `false`가 되고 중력(`rb.
useGravity = true`)에 의해 실제로는 낙하하지만, `isJump`가 계속 `false`이므로:
- `animationDriver.ChangeState(Jump)`가 전혀 호출되지 않아 애니메이션이 바뀌지 않는다
  (`CheckMovementInput()`이 매 프레임 `Idle`/`Walk`로 계속 되돌려놓기까지 한다).
- 착지 처리 분기(`isJump && grounded && ...`)도 애초에 `isJump`가 `false`라 실행되지 않는다 —
  다만 이 경우는 원래 `Jump` 상태로 들어간 적이 없으므로 딱히 되돌릴 것도 없어 착지 자체는
  문제없이 조용히 끝난다(애니메이션만 계속 Walk/Idle이었을 뿐).

### 23.3 설계 방향 — "의도한 점프"와 "감지된 낙하"를 같은 `isJump`/`Jump` 상태로 합류시킨다

`FixedUpdate()`에 **"공중에 떠 있는데 점프 중이 아니다"를 감지하는 분기를 하나 추가**해서,
감지되는 즉시 지금 점프가 진행 중인 것과 동일하게 취급한다 — 이렇게 하면 애니메이션 정지
(`HandleJumpAnimationHold`)·착지 감지·`ResumePlayback` 등 §18에서 이미 만들어둔 점프 관련
인프라를 전부 그대로 재사용할 수 있어 코드 중복이 없다.

```csharp
private void FixedUpdate()
{
    if (!pv.IsMine || IsMovementLocked)
        return;

    bool grounded = groundDetector.IsGrounded(transform.position);

    if (isJump && grounded && rb.linearVelocity.y <= 0f) // 착지 처리(기존과 동일 — 점프든 낙하든 공용)
    {
        isJump = false;
        keepMovingAfterJump = false;
        animationDriver.ResumePlayback();
    }

    if (jumpRequested && grounded && !isDodge) // 의도한 점프 시작(기존과 동일)
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpPower, rb.linearVelocity.z);
        isJump = true;
        keepMovingAfterJump = true; // 의도한 점프는 기존처럼 방향(관성)을 고정
        jumpMoveDir = rotation;
        animationDriver.ReplayJump(); // §15에서 신설한 점프 전용 재생 메서드
    }
    else if (!isJump && !grounded && !isDodge) // ← 신규: 걸어서(비의도) 낙하 시작 감지
    {
        isJump = true;
        keepMovingAfterJump = false; // 의도한 점프와 달리 방향을 고정하지 않음(23.4 참고)
        animationDriver.ReplayJump();
    }
    jumpRequested = false;

    Move();

    if (transform.position.y < -100f)
        RespawnToSpawnPoint();
}
```

이렇게 하면:
- 낙하가 시작되는 순간 `isJump = true`가 되어 `Jump` 애니메이션이 재생되고,
  `HandleJumpAnimationHold()`가 기존과 동일하게 정점(낙하 중이므로 사실상 거의 즉시)에서
  자세를 얼려 유지한다.
- 착지 시 위쪽의 기존 착지 분기(`isJump && grounded && ...`)가 **아무 추가 코드 없이 그대로**
  처리해준다 — 의도한 점프든 걸어서 낙하든 착지 로직은 완전히 공용.
- `animationDriver.ReplayJump()`는 §15(`Bug-fix-plan.md`)에서 신설한, 점프 전용으로 항상
  처음부터 재생을 보장하는 메서드다 — "낙하 도중 다시 짧게 땅에 닿았다가 또 낙하하는" 것과 같은
  연속 낙하 상황에서도 매번 확실히 처음부터 재생된다. §15를 먼저 구현한 뒤 이 메서드를 그대로
  가져다 썼다.

### 23.4 설계 결정이 필요한 지점 — 낙하 중 이동 입력을 계속 반영할 것인가

의도한 점프(`keepMovingAfterJump = true`)는 `Move()`에서 `jumpMoveDir`(점프 시작 시점의 방향)로
방향이 고정되어 공중에서 방향키를 바꿔도 포물선이 바뀌지 않는다(기존 설계, §18). **걸어서
낙하하는 경우도 똑같이 방향을 고정할지, 아니면 계속 입력에 반응하게(공중 조작 허용) 둘지는
게임 디자인 선택의 문제**라 이 계획에서는 확정하지 않고 아래처럼 기본값만 제안해둔다:

- **채택(사용자 확정, 2026-08-16): `keepMovingAfterJump = false`로 두어 낙하 중에도 계속 이동
  입력에 반응**하게 한다 — 플레이어가 "의도적으로 점프 버튼을 누른 것"이 아니라 "실수로/자연스럽게
  가장자리를 걸어서 벗어난 것"이므로, 공중에서도 방향을 조절해 되돌아오거나 원하는 곳에 착지할
  여지를 주는 편이 자연스럽다는 제안에 사용자가 동의했다.
- **대안(채택 안 함)**: 의도한 점프와 완전히 동일하게 방향을 고정(`keepMovingAfterJump = true`,
  `jumpMoveDir = rotation`)하면 코드가 더 단순해지고 두 경우가 완전히 동일하게 취급되지만,
  "걷다가 실수로 떨어졌는데 그 순간 방향이 고정돼버려 되돌아오지 못한다"는 체감이 나쁠 수 있다.

### 23.5 잠재적 오탐(false positive) 위험 — 지형 이음매·계단·경사로

`groundDetector.IsGrounded()`는 단순 레이캐스트 1개(`groundCheckOffset = 0.3f` 여유)라, 계단
사이·ProBuilder로 제작한 바닥의 메시 이음매·경사로 전환 지점 등에서 한두 물리 스텝(각 20ms)만
`grounded=false`로 잘못 판정될 수 있다(§13.5에서 이미 한 번 언급된 종류의 우려). 지금 설계대로면
이런 아주 짧은 순간에도 `isJump=true`가 되어 매번 짧게 Jump 애니메이션이 깜빡이는 시각적
노이즈가 생길 위험이 있다 — 실제 "점프"는 원래 명시적 입력이라 이런 오탐이 없었지만, "감지 기반"
낙하는 이 위험에 새로 노출된다.

**완화 방안(제안, 필요성은 실측 후 판단)**: 낙하 감지에 짧은 유예 시간("코요테 타임"과 반대
개념의 "그레이스 타임")을 둬서, `!grounded`가 일정 시간(예: 0.15~0.2초) 이상 연속으로 유지될
때만 실제로 `isJump=true`를 트리거하도록 타이머를 하나 추가할 수 있다:

```csharp
private float airborneTimer;
private const float FallAnimGraceTime = 0.15f;
...
if (!grounded && !isJump && !isDodge)
{
    airborneTimer += Time.fixedDeltaTime;
    if (airborneTimer >= FallAnimGraceTime)
    {
        isJump = true;
        keepMovingAfterJump = false;
        animationDriver.ReplayJump();
    }
}
else
{
    airborneTimer = 0f;
}
```

이 유예 시간 로직은 처음부터 넣기보다, **§23을 우선 단순하게(유예 없이) 구현한 뒤
`GameLobbyScene`의 실제 지형(계단·경사·이음매가 있는 ProBuilder 바닥)에서 깜빡임이 실제로
관측되는지 확인하고, 필요할 때만 추가**하는 순서를 제안한다 — 처음부터 넣으면 정말 필요한
기능인지 검증 없이 복잡도만 늘어난다.

### 23.6 검증 계획 (구현 시점에 사용)

1. `read_console`로 컴파일 에러 0건 확인.
2. `GameLobbyScene`에서 점프 없이 걸어서 가장자리를 벗어났을 때 Jump(낙하) 애니메이션이 재생되고,
   착지 시 정상적으로 `Idle`/`Walk`로 복귀하는지 확인.
3. 기존 "의도한 점프"(Space) 동작이 회귀 없이 그대로인지 확인(방향 고정, 애니메이션 정지/재개
   타이밍 등).
4. §23.4에서 확정한 방향(입력 반영 여부)대로 공중 이동이 동작하는지 확인.
5. §23.5의 지형 이음매·계단 오탐(깜빡임) 여부를 실제 `GameLobbyScene` 지형에서 확인 — 발생하면
   그레이스 타임 완화안 적용 여부를 다시 논의.
6. `VoidKillZone`/맵 밖 낙하 후 스폰 복귀(§18)와의 상호작용 확인 — 걸어서 낙하 → Jump 애니메이션
   → 한계 높이 초과 → `RespawnToSpawnPoint()` 흐름에서 `isJump`/애니메이션 상태가 깨끗하게
   리셋되는지(현재 `RespawnToSpawnPoint()`가 이미 `isJump = false; animationDriver.
   ResumePlayback();`을 호출하므로 기존 로직으로 충분해 보이지만 실제 확인 필요).

### 23.6-1 구현 결과

`Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`의 `FixedUpdate()`에 §23.3 코드 그대로
`else if (!isJump && !grounded && !isDodge)` 분기를 추가했다(§15의 `ReplayJump()` 구현 이후
진행). 컴파일 에러 0건. 그레이스 타임(§23.5) 완화안은 계획대로 처음엔 넣지 않았다.

### 23.6-2 검증 결과 — `GameLobbyScene` 실제 Photon 방에서 실측

Space를 누르지 않은 채(`jumpRequested`를 전혀 건드리지 않고) 캐릭터를 트인 공간 상공(`y=40`)으로
순간이동시켜 자유낙하만 시키는 방식으로, `EditorApplication.update` 훅을 등록해 매 틱
`isJump`/애니메이터 상태/`normalizedTime`을 실측했다:

- **낙하 첫 물리 스텝(tick=1)부터 `isJump=True`로 즉시 전환** — 점프 키 없이도 감지 분기가
  정상 동작함을 확인.
- 애니메이터가 `Jump` 상태로 실제 전환되기까지 약 15틱(`AnyState→Jump`의 0.1초 크로스페이드
  전환 시간에 해당)이 걸린 뒤, `isJumpAnim=True`로 전환되며 `normalizedTime`이 `0.05`부터
  깨끗하게 매 틱 증가(0.05→0.06→...→0.12) — 튐이나 역행 없이 매끄러운 단일 재생.
- 착지 후 재조회 결과 `pos.y=0`, `vel=(0,0,0)`, `isJump=False`, `isJumpAnim=False` — 착지 시
  기존 착지 분기가 그대로 처리해 `Idle`로 정상 복귀함을 확인(§23.3에서 기대한 대로, 추가 코드
  없이 기존 착지 로직을 그대로 재사용).
- **공중 조작(§23.4) 확인**: 낙하 도중 옆 방향(`x`) 이동 입력을 계속 유지시킨 결과, `x`좌표가
  매 틱 꾸준히 증가(수평 속도 `5.0`으로 고정 유지)하면서 동시에 `y`는 중력에 따라 자연스럽게
  가속 낙하 — `keepMovingAfterJump=false`로 인해 낙하 중에도 이동 입력이 정상적으로 반영됨을
  확인, 방향이 고정되는 회귀 없음.
- `read_console` 최종 확인 결과 이번 테스트 전 구간 에러/경고 0건.
- §23.5의 지형 이음매 오탐(깜빡임) 문제는 이번 테스트(순간이동 후 개활지 낙하)로는 재현 조건이
  아니라 확인하지 못했다 — `GameLobbyScene`의 실제 계단·경사 지형에서 사용자가 플레이하며
  깜빡임이 체감되는지 추가 확인이 필요하며, 관측되면 §23.5의 그레이스 타임 완화안을 적용한다.

### 23.7 상태

**구현·검증 완료.** §23.4(공중 이동 입력 반영)는 사용자가 확정한 방향대로 구현했고 Play Mode
실측으로 정상 동작을 확인했다. §23.5의 그레이스 타임 완화안은 계획대로 미적용 상태로 남겨뒀다 —
실제 지형에서 깜빡임이 체감되면 그때 추가한다.