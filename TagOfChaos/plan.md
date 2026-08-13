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

승인해주시면 이 계획대로 구현을 시작하겠습니다. 수정하고 싶은 부분(예: 폴더 분해 단위, `IsMovementLocked`
네이밍, 새 Input System 조기 도입 여부, `hide_or_seek_player` 아바타/클립 이름 규칙, `Run.fbx.meta` 처리
여부 등)이 있으면 알려주세요.
