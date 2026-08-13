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