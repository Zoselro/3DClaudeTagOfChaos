# 조사 보고서: Assets/Scripts

범위: 현재 `Assets/Scripts/` 아래에 있는 모든 스크립트. 이 보고서 작성 시점 기준으로 해당 디렉터리에는
**단 하나의 파일**만 존재한다: `Hero_Ctrl.cs` (그리고 그 `.meta`). 이 문서는 그 파일에 대한 라인 단위의
깊이 있는 분석이다: 무엇을 하는지, 각 서브시스템이 어떻게 상호작용하는지, 런타임 의존성은 무엇인지,
그리고 리뷰 중 발견한 구체적인 결함들(정적 독해뿐 아니라 Unity 에디터 콘솔과 reflection으로 실제로도
확인된 사항 포함)까지 다룬다.

---

## 1. 파일 인벤토리

| 파일 | 타입 | 라인 수 | 역할 |
|---|---|---|---|
| `Assets/Scripts/Hero_Ctrl.cs` | `MonoBehaviour` (`MonoBehaviourPunCallbacks`, `IPunObservable`) | 491 | 플레이어 캐릭터 컨트롤러: 입력, 이동, 점프, 회피, 근접 공격, HP/사망, Photon PUN2 네트워크 동기화, 그리고 작은 애니메이션 상태 레이어까지 담당. |

`Assets/Scripts/` 아래에는 다른 `.cs` 파일도, `.asmdef` 파일도 존재하지 않는다. 따라서 이 스크립트는
프로젝트의 다른 모든 스크립트(`Assets/Photon/...` 아래의 Photon PUN2 데모 스크립트 포함)와 함께
기본 `Assembly-CSharp` 어셈블리로 컴파일된다.

---

## 2. 컴파일 상태 — 심각(CRITICAL)

**`Hero_Ctrl.cs`는 현재 컴파일되지 않는다.** Unity 콘솔 실시간 출력(`read_console`)에서 다음이
확인된다:

```
Assets\Scripts\Hero_Ctrl.cs(30,5): error CS0246: The type or namespace name 'AnimState' could not be found
Assets\Scripts\Hero_Ctrl.cs(31,5): error CS0246: The type or namespace name 'AnimState' could not be found
Assets\Scripts\Hero_Ctrl.cs(383,33): error CS0246: The type or namespace name 'AnimState' could not be found
```

`AnimState` 외에도, 정적 검색(레포 전체 텍스트 `grep`과 Unity의 실시간 reflection 타입 검색,
scope=`project` 모두)으로 **프로젝트 어디에도 존재하지 않는 3개의 타입이 추가로 참조되고 있음**을
확인했다:

| 참조되는 심볼 | 사용 위치 | 프로젝트에 존재하는가? |
|---|---|---|
| `enum AnimState` (`idle`, `move`, `jump`, `dodge`, `attack`, `skill` 멤버) | 30, 31, 383, 389, 392, 397, 405, 406, 373, 379, 185, 213, 221, 238, 252, 365행 | **없음** — reflection 매치 0건 |
| `GameManager` (정적 `Inst`와 `bool Is_Conversating`을 기대) | 176행 | **없음** — reflection 매치 0건. `Assets/Photon/PhotonUnityNetworking/Demos/PunBasics-Tutorial/Scripts/GameManager.cs`에 *이름은 같지만 무관한* `GameManager`가 존재하지만, 이건 `Photon.Pun.Demo.PunBasics` 네임스페이스에 속해 있고 여기서 `using`으로 임포트되지도 않으며, `Inst`가 아닌 `Instance`를 노출하고, `Is_Conversating` 멤버도 없다. 이 호출부와는 무관하다. |
| `Camera_Ctrl` (`InitCamera(GameObject)`를 기대) | 73행 | **없음** — reflection 매치 0건 |
| `Monster_Ctrl` (`TakeDamage(GameObject, float)`를 기대) | 331행 | **없음** — reflection 매치 0건 |

현재 콘솔에는 `AnimState` 관련 에러만 표면화되어 있다(30~31행의 필드 레벨 `AnimState` 에러가 발생한
시점 이후로 컴파일러가 나머지 파일을 완전히 재검증하지 않는 것으로 보인다). **이 스크립트가 빌드되기
전에 4개 타입 모두를 작성해야 한다.** 이것이 현재 `Assets/Scripts`의 상태에 대한 가장 중요한 사실이다.

---

## 3. 클래스 개요

```csharp
public class Hero_Ctrl : MonoBehaviourPunCallbacks, IPunObservable
```

- Photon PUN2의 `MonoBehaviourPunCallbacks`를 상속(콜백 접근권을 얻지만, 이 스크립트는 상속받은
  헬퍼 대신 직접 명시한 `[SerializeField] PhotonView pv`를 사용한다).
- `IPunObservable`을 구현하여 `PhotonView`의 **Observed Components** 목록에 참여, 프레임 단위 상태
  스트리밍(`OnPhotonSerializeView`)을 가능하게 한다.
- 플레이어 아바타 1개당 인스턴스 1개가 기대된다(로컬 플레이어 자신의 인스턴스 + 접속한 다른 각
  플레이어당 리모트 프록시 인스턴스 하나씩, 모두 같은 프리팹/스크립트를 공유).

### 3.1 필요한 동반 에셋
- `Animator`(`Start()`에서 `GetComponent<Animator>()`로 가져옴), 그리고 이를 구동하는
  `AnimatorController`는 반드시 정확히 `idle`, `move`, `jump`, `dodge`, `attack`, `skill`이라는 이름의
  **트리거 파라미터**와, 문자 그대로 `Jump`라는 이름의 상태(state)를 정의하고 있어야 한다.
- `PhotonView` 컴포넌트(`pv` 필드로 수동 연결, 자동으로 가져오지 않음).
- `NavMeshAgent`(`agent`로 연결), 보조적인 역할로만 사용됨(§5.6 참고).
- UI `Text`(`id`, 닉네임 라벨)와 UI `Image`(`ImgHpbar`, `fillAmount`를 쓰는 HP 바).

### 3.2 Animator Controller의 공백
`Assets/Animation/PlayerAnimator.controller`는 현재 **단 하나의 상태**(`mixamo_com`, 고정된 모캡
클립 재생)만 정의하고 있으며, **파라미터도, 전환(transition)도 0개**다. `ChangeAnimState`가 필요로
하는 6개의 트리거 중 어느 것도, `Jump`라는 이름의 상태도 아직 존재하지 않는다. 구체적으로 이는 다음을
의미한다:
- `m_Animator.SetTrigger(newState.ToString())`(397행)은 항상 존재하지 않는 파라미터를 대상으로 하게
  되며 — Unity는 런타임 경고만 띄우고 아무 일도 하지 않는다.
- `HandleJumpAnimationHold()`의 `state.IsName("Jump")` 검사(164행)는 절대 참이 되지 않는다.
- 스크립트가 컴파일되더라도, 어떤 시각적 애니메이션 전환도 절대 일어나지 않는다.

---

## 4. 필드 목록 (`[Header]` 별 그룹핑)

### "Options" (조정 가능한 직렬화 필드)
| 필드 | 타입 | 용도 |
|---|---|---|
| `speed` | `float` | 현재 수평 이동 속도; 회피(dodge) 시 런타임에 ×2 / ×0.5로 변경됨 (§7 참고). |
| `jumpPower` | `float` | 점프 시 부여되는 초기 상승 속도(`yVelocity`). |
| `MaxHp` | `float` | 최대 HP; `CurHp`가 이 값을 기준으로 클램프/파생됨. |
| `groundLayer` | `LayerMask` (기본값 `1`, 즉 `Default` 레이어) | 착지 레이캐스트가 사용하는 레이어 마스크. |
| `groundCheckOffset` | `float` (0.3) | 레이캐스트 시작 높이와 최소 탐지 거리. |
| `jumpFreezeNormalizedTime` | `float` (0.5) | `Jump` 클립에서 재생을 공중 중간에 멈추는 normalized time 지점. |

### "Components" (직렬화된 참조)
| 필드 | 타입 | 용도 |
|---|---|---|
| `pv` | `PhotonView` | 소유권 체크(`pv.IsMine`)가 로컬 전용 분기 대부분을 게이팅함. |
| `agent` | `NavMeshAgent` | 동기화는 되지만(`Warp`, `updatePosition`) 실제 이동을 직접 구동하지는 않음 (§7.6 참고). |
| `id` | `UnityEngine.UI.Text` | 캐릭터 위 닉네임 라벨; 소유자는 `PhotonNetwork.LocalPlayer.NickName`에서, 리모트는 네트워크로 스트리밍된 `m_Id`에서 값을 받음. |
| `ImgHpbar` | `UnityEngine.UI.Image` | HP 바; `fillAmount`로 구동됨. |

### 이동/물리 런타임 상태 (별도 표기 없으면 직렬화 안 됨)
`velocity`, `baseSpeed`, `m_MvDelay`, `h`, `v`, `rotation`, `rotation_value`,
`dodgeRotation`, `dodgeMoveDir`, `jumpMoveDir`, `yVelocity`, `gravity`(`-9.81`, `Physics.gravity`와
무관한 커스텀 값).

### 애니메이션 상태 레이어
`m_Animator`, `m_PreState`, `m_CurState` (둘 다 타입이 `AnimState`이며, 현재 정의되어 있지 않음).

### "States" (직렬화된 bool, 대부분 디버그용으로 인스펙터에 노출됨)
`isJump`, `isDodge`, `keepMovingAfterDodge`, `keepMovingAfterJump`, `isDead`, `isChat`
(선언만 되어 있고, **파일 내 다른 어디에서도 읽거나 쓰이지 않음** — 죽은 필드).

### HP
`CurHp` — `[Header("States")]` 블록 밖(26행, `h, v` 바로 다음)에 다소 어색하게 위치해 있지만, 사실상
캐릭터의 실시간 HP 값이다.

### 네트워킹 / 리모트 프록시 미러 상태
`isFirstUpdate`, `CurPos`, `CurRot`, `m_IsJump`, `m_Id`, `NetHp` — 이 필드들은 순전히 `OnPhotonSerializeView`를
통해 수신한, 소유하지 않은(non-owned) 인스턴스를 위한 최신 값을 보관하는 용도로만 존재한다.

### 전투
`m_EnemyList`, `m_CacTgVec`, `m_AttackDist` (`1.9f`).

---

## 5. 라이프사이클

### `Awake()`
```csharp
PlayerPrefs.SetInt("MaxScore", 112500);
if (pv.IsMine) {
    Camera_Ctrl a_CamCtrl = Camera.main.GetComponent<Camera_Ctrl>();
    if (a_CamCtrl != null) {
        a_CamCtrl.InitCamera(this.gameObject);
        id.text = PhotonNetwork.LocalPlayer.NickName;
    }
}
```
- **`Hero_Ctrl` 인스턴스가 awake될 때마다(리모트 프록시 포함) 매번** `PlayerPrefs["MaxScore"] = 112500`을
  무조건 기록한다 — 의도된 게임 로직이라기보다는 디버그/테스트용으로 남겨진 코드로 보인다. 이 스크립트가
  포함된 씬이 로드될 때마다, 그 룸에 있는 모든 플레이어에 대해 실제 최고 점수 값이 조용히 덮어써지게 된다.
- 로컬로 소유된 아바타에 한해서만: `Camera.main`에서 `Camera_Ctrl`을 가져와 이 GameObject를 따라가게
  하고, 닉네임 라벨을 설정한다. `Camera.main`에 `Camera_Ctrl` 컴포넌트가 없으면 블록 전체가 조용히
  스킵된다(`a_CamCtrl != null` 가드) — 다만 `id.text`는 그 `if` 안에서만 설정되므로, `Camera_Ctrl`이
  없으면 로컬 플레이어 자신의 네임플레이트조차 초기화되지 않는다(부수 효과가 결합되어 있음).

### `Start()`
```csharp
CurHp = MaxHp;
baseSpeed = speed;
m_Animator = this.GetComponent<Animator>();
```
단순한 초기화: HP를 풀로 채우고, 인스펙터에서 설정한 speed를 수정되지 않은 이동에 쓰일 "기본" 속도로
스냅샷하고, Animator를 캐싱한다.

### `Update()`
소유권에 따라 크게 분기된다:

```
if (isDead) return;

if (pv.IsMine)
    ApplyGravity() → Move() → AttackOrder() → CheckMovementInput()
    → CheckJumpInput() → CheckDodgeInput() → HandleJumpAnimationHold()
else
    // 리모트 프록시: 위치 보간, 애니메이션 상태 재생, 회전 슬러프, HP 동기화
```

**로컬 분기의 호출 순서**에 주목할 필요가 있다: `Move()`는 `CheckMovementInput()`이 이번 프레임의
`h`, `v`, `rotation`을 갱신하기 **전에** 실행된다. 즉 모든 프레임의 이동은 *이전* 프레임의 입력 상태를
기준으로 계산된다는 뜻이다. `AttackOrder()`(이동 중이면 공격을 억제하기 위해 `h`/`v`를 읽는 메서드,
§7.4 참고) 역시 이번 프레임의 `CheckMovementInput()`보다 먼저 실행되므로, 이것도 오래된(stale) `h`/`v`
값을 기준으로 판단한다. 이는 (크래시를 일으키는 버그는 아니고) 일관성 있게 동작하긴 하지만, 알아둘
가치는 있다: 입력에 대한 반응이 항상 한 프레임 늦다.

**리모트 분기**의 경우: 위치는 마지막으로 알려진 `CurPos`에서 `10`유닛보다 멀리 떨어져 있으면
스냅(순간이동/디싱크 복구용 휴리스틱)되고, 그렇지 않으면 `10×Time.deltaTime` 고정 비율로 부드럽게
`Lerp`된다; 회전은 같은 비율로 `Slerp`된다; 애니메이션 상태는 네트워크로 수신한 마지막 값을 이용해
`ChangeAnimState(m_CurState)`로 재생된다; HP는 매 프레임 `Remote_TakeDamage()`로 동기화된다(§8.4의
버그 참고).

---

## 6. 이동 & 물리 (§ 소유자 전용)

### 6.1 커스텀 중력 (`ApplyGravity`)
이 스크립트는 낙하 처리에 Unity 내장 `Rigidbody`/`CharacterController` 물리를 **사용하지 않는다**.
대신:
- `isJump`가 참일 때만 동작한다.
- `yVelocity += gravity * Time.deltaTime`(커스텀 `gravity = -9.81`)로 누적한다.
- 상승 중일 때는 착지 판정을 아예 건너뛴다(`yVelocity >= 0`).
- 하강 중일 때는 `transform.position + up * groundCheckOffset`에서 수직 아래로 레이캐스트를 쏘며,
  레이 길이는 `groundCheckOffset + |yVelocity| * Time.deltaTime`이다(즉, 캐릭터가 이번 프레임에 도달할
  거리만큼만 정확히 검사하는 "스윕(sweep)" 방식의 착지 판정으로, 빠른 낙하 속도에서 얇은 바닥을 뚫고
  지나가는 것을 방지한다).
- 히트 시: Y를 고정된 0이 아니라 히트 지점의 정확한 높이로 스냅하고, `isJump` / `keepMovingAfterJump` /
  `yVelocity`를 초기화하며, `m_Animator.speed = 1f`을 복구하고(`HandleJumpAnimationHold`가 걸어둔 공중
  정지 취소), `NavMeshAgent`를 다시 활성화 + `Warp`시켜 캐릭터 위치로 되돌려놓는다.

### 6.2 점프 애니메이션 홀드 (`HandleJumpAnimationHold`)
순전히 연출용 타이밍 보정이다. `Jump` 애니메이터 상태의 `normalizedTime`이
`jumpFreezeNormalizedTime`(기본 0.5, 즉 클립의 정점 포즈)을 넘어서면, `m_Animator.speed = 0`으로
설정해 아직 공중에 있는데도 착지 포즈까지 미리 재생되어 버리는 것을 막고 그 정점 포즈로 고정시킨다.
착지 시 다시 풀린다(§6.1). (현재는 비활성 상태다 — `Jump`라는 이름의 상태가 아직 존재하지 않기 때문,
§3.2 참고.)

### 6.3 입력 캡처 (`CheckMovementInput`)
- 먼저 `GameManager.Inst.Is_Conversating`(미정의 의존성, §2 참고)을 확인한다 — 참이면 모든 이동
  입력을 0으로 만들고 (점프/회피/공격 중이 아니라면) `idle`을 강제한다. "대화/채팅 중에는 이동을
  멈춘다"는 게이트 역할을 하는 것으로 보인다.
- 그렇지 않으면 raw 축값(`Input.GetAxisRaw`)을 읽어 카메라 기준 이동 방향으로 변환하고
  (`Camera.main.transform.forward/right`을 Y축 평탄화 후 정규화), `rotation`/`rotation_value`와
  `move`/`idle` 애니메이션 상태를 갱신한다(점프/회피 중이 아닐 때만).

### 6.4 `Move()` — 서로 배타적인 3가지 이동 모드
1. **공격 중** (`IsAttack() && !isJump && !isDodge`): 수평 이동이 완전히 정지됨; 수직 이동
   (`yVelocity`, 즉 여전히 진행 중인 낙하)만 적용된다.
2. **회피 후 관성 이동** (`isDodge && keepMovingAfterDodge`): 고정된 `dodgeMoveDir` 방향으로 전체
   `speed`로 강제 이동시킨다. **이 분기는 도달 불가능한 것으로 보인다 — §8.1의 버그 참고.**
3. **점프 후 관성 이동** (`isJump && keepMovingAfterJump`): 점프 시작 시점에 캡처된 방향
   (`jumpMoveDir`)으로 `baseSpeed`로 강제 이동시켜, 공중 방향 입력 변화를 무시하는 고정된 포물선
   궤적을 만들어낸다. 이 분기는 실제로 도달 가능하며 올바르게 게이팅되어 있다(#2와 대조됨).
4. **일반**: `Shift` 홀드 시 → `baseSpeed`의 `30%`, 아니면 전체 `baseSpeed`; `rotation` 방향을
   바라보며 이동한다.

모든 경우에 `moveVector.y = yVelocity`로 커스텀 중력의 수직 성분을 선택된 수평 모드와 합치고, 결과는
(`NavMeshAgent`를 거치지 않고) `transform.position +=`로 직접 적용된다. 305행에 주석 처리된
`agent.Move` 호출이 남아있는데, 이는 명백히 시도했다가 포기한 경로다.

`m_MvDelay` 스로틀(값이 `>0`이면 감소시키고 조기 리턴)이 존재하지만, 이 파일 안 어디에도
`m_MvDelay`에 0이 아닌 값을 대입하는 곳이 없다 — 미래의 스턴/넉백 효과를 위해 준비된 것으로 추정되는
죽은 게이팅 로직이다.

### 6.5 점프 입력 (`CheckJumpInput`)
`Space`, 현재 점프/회피/공격 중이 아닐 때 → `yVelocity = jumpPower`로 설정하고, `isJump`/
`keepMovingAfterJump` 플래그를 세우고, `jumpMoveDir`을 현재 `rotation`으로 고정하고,
`agent.updatePosition`을 비활성화하며(공중에서 NavMeshAgent가 수동 이동과 충돌하지 않도록),
`jump` 애니메이션 상태를 트리거한다.

### 6.6 회피 입력 (`CheckDodgeInput` / `DodgeOut`)
이동 중이고 점프/회피/공격 중이 아닐 때 `Left Ctrl` → `dodgeMoveDir`/`dodgeRotation`을 현재
`rotation`으로 고정하고, **`speed`를 2배로 늘리고**, `isDodge`를 세우고, `Invoke("DodgeOut", 0.5f)`를
예약하며, `dodge` 상태를 재생한다. 0.5초 후 `DodgeOut()`이 **`speed`를 절반으로 되돌린다**(결과적으로
×2 다음 ×0.5는 정확히 서로 역연산이므로 speed는 원래 값으로 돌아온다), `isDodge`를 해제하고,
`rotation`을 `rotation_value`에서 복원하며, `keepMovingAfterDodge = true`를 설정한다. 이 마지막
플래그가 실제로는 아무 효과가 없는 이유는 §8.1 참고.

### 6.7 NavMeshAgent의 실제 역할
`agent`는 이동을 직접 구동하는 `SetDestination`/`.Move` 호출을 전혀 받지 않는다. 유일한 용도는:
`agent.updatePosition = false/true`(점프 전후로 토글, §6.1/6.5)와 `agent.Warp(transform.position)`
(착지 후 재동기화)뿐이다. 즉 이 에이전트는 순전히 (이 파일에는 없는) *다른* 시스템들이 이 캐릭터에
대해 유효한 NavMeshAgent 상태를 조회할 수 있도록(예: AI 타겟팅, off-mesh 쿼리) 함께 끌고 다니는
것일 뿐 — 실제 이동은 100% 수동 `transform.position` 대입으로 이루어진다.

---

## 7. 전투

### 7.1 공격 트리거 (`AttackOrder`)
소유자 전용(`pv.IsMine` 가드). 좌클릭, 이미 공격/스킬 사용 중이 아님, 그리고 **현재 이동 입력을 누르고
있지 않음**(`h == 0 && v == 0`) — 이 마지막 조건은 마우스를 빠르게 연타할 때 달리기 애니메이션에 공격
블렌드가 잠깐 끼어들어 끊기는 것을 막기 위해 존재한다(인라인 주석 참고). 조건이 모두 충족되면 `attack`
애니메이션 상태로 전환한다.

### 7.2 히트 판정 (`Event_AttHit`) — Animation Event 콜백으로 추정됨
`Event_` 접두사 네이밍 컨벤션(Unity Animation Event가 정확한 메서드 이름으로 호출하는 방식과 일치)과,
이 파일 내에 이 메서드를 직접 호출하는 곳이 없다는 점을 볼 때, 이 메서드는 (아직 작성되지 않은) 공격
클립 안의 특정 키프레임에서 호출되도록 연결될 예정인 것으로 거의 확실하다.

로직: `Enemy` 태그가 붙은 **모든** GameObject를 찾아서, 각각에 대해 `m_AttackDist + 0.1`(`2.0f`)
이내이고 전방 원뿔각 안(`Vector3.Dot(transform.forward, dirToEnemy.normalized) >= 0.45`)에 있으면
`enemy.GetComponent<Monster_Ctrl>().TakeDamage(this.gameObject, 20f)`를 호출한다. 데미지 낙폭이나
크리티컬 로직 없이 고정 `20` 데미지를 가하는, 조건을 만족하는 모든 적을 동시에 타격하는 평면적인
범위(AoE) 근접 공격이다. **참고**: 코드 내 주석은 "45도" 원뿔이라 주장하지만, dot 임계값 `0.45`는
실제로는 `arccos(0.45) ≈ 63.3°`의 반각(half-angle)에 해당한다 — 주석과 실제 구현된 각도가 서로
다르다(사소하지만 실재하는 문제).

**이 메서드에는 `pv.IsMine` 가드가 없다** — §8.2, 이 파일에서 가장 중요한 네트워킹 정합성 우려
사항이다.

### 7.3 공격 종료 (`Event_AttFinish`)
이것도 Animation Event 콜백으로 추정된다. 소유자 전용(`pv.IsMine` 가드, 이번엔 올바르게 존재함)이며,
`m_CurState`가 여전히 `attack`일 때만 `idle`로 되돌린다(상태가 이미 바뀐 후 오래된/중복 호출로부터
방어하는 역할).

### 7.4 `IsAttack()` / `IsDodge()`
`m_CurState`에 대한 단순 술어(predicate)로, 이 파일 전반에서 재진입/상태 가드(예: "공격 중에는
점프하지 않기")로 사용된다.

### 7.5 애니메이션 상태 전환 (`ChangeAnimState`)
```csharp
if (m_PreState == newState) return;       // 이미 그 상태면 아무 것도 안 함
m_Animator.ResetTrigger(m_PreState.ToString());
if (crossTime > 0) m_Animator.SetTrigger(newState.ToString());
else m_Animator.Play(animName, -1, 0);
m_PreState = newState;
m_CurState = newState;
```
이 파일의 모든 호출부는 기본값인 `crossTime = 0.1f`를 사용하므로, `else` 분기(명시적 클립 이름으로
바로 `Play`하는 경로)는 실질적으로 죽은 코드다 — 항상 트리거 경로를 탄다. `m_PreState`와
`m_CurState`는 함께 등장하는 모든 곳에서 동일하게 설정된다. 즉 이 파일 안에서는 실제로 절대 서로
갈라지지 않는다 — 두 개의 다른 이름을 가진 하나의 값처럼 동작한다(향후 기능에서 갈라질 것을 대비한
것일 수도 있다, 예: "돌아갈 상태").

### 7.6 데미지 / HP (`TakeDamage`, `Die`, `Remote_TakeDamage`)
- `TakeDamage(float Damage)`: 이미 HP가 `<=0`이면 조기 리턴; 데미지를 차감; 0으로 클램프하고 치명타면
  `Die()`를 호출; HP 바 `fillAmount`를 갱신한다. `cacPos`(캐릭터 위치 + Y축 2.65)를 계산하지만
  **한 번도 사용되지 않는다** — 죽은 코드로, 거의 확실히 연결되지 않은 데미지 숫자 팝업 기능을 위한
  것으로 보인다.
  - **현재 프로젝트 어디에도 이 메서드를 호출하는 곳이 없다**(공개 API로, 아직 존재하지 않는
    `Monster_Ctrl`이 호출할 것으로 추정됨).
- `Die()`: 소유자 전용; 로그를 남기고 `isDead = true`로 설정(이후 92행에 의해 그 인스턴스의
  `Update()` 전체가 조기 리턴됨).
- `Remote_TakeDamage()`: 소유하지 않은 인스턴스에서 매 `Update()`마다 실행된다. **도달 불가능한 죽은
  코드가 포함되어 있음** — §8.4 참고.

### 7.7 네트워크 동기화 (`OnPhotonSerializeView`)
`PhotonNetwork.InRoom`으로 가드됨. write 시(소유자): `position, rotation, (int)m_CurState, isJump,
id.text, CurHp`를 정확히 이 순서로 스트리밍한다 — 순서가 중요하며, 리더는 정확히 같은 순서로
역직렬화해야 하고 실제로도 그렇게 되어 있다. read 시(리모트 프록시): §4에서 설명한 미러 필드에 모든
값을 저장하고, 네임플레이트 텍스트를 즉시 갱신하며, 리모트에서 점프 중이면 `agent.updatePosition`을
강제로 false로 만들고, 처음 수신한 패킷에 한해서는 아바타가 월드 원점에서 눈에 띄게 미끄러져 오는
것을 방지하기 위해 위치와 회전을 (보간이 아니라) **스냅**시킨다.

---

## 8. 확인되었거나 유력한 버그 & 코드 스멜

### 8.1 `keepMovingAfterDodge` 분기가 도달 불가능함 (Move(), 281~286행)
`DodgeOut()`에서 `isDodge = false`(258행)가 `keepMovingAfterDodge = true`(260행)**보다 먼저**
실행된다. `keepMovingAfterDodge`를 다시 `true`로 만드는 곳은 오직 여기뿐인데, 그 시점이 바로
`isDodge`가 `false`가 되는 순간이므로, `Move()`의 가드 `if (isDodge && keepMovingAfterDodge)`는 두
값이 동시에 `true`인 상황을 절대 관찰할 수 없다 — 이 이동 모드 전체가 죽은 코드다. 구조적으로
대칭되는 점프 케이스와 비교해보면, `keepMovingAfterJump`와 `isJump`는 함께 `true`로 세워지고
(`CheckJumpInput`, 230~231행) 함께 초기화된다(`ApplyGravity`, 141~142행) — 그쪽은 실제로 도달
가능하다. 이는 회피 경로에서 발생한 복사/붙여넣기 기반의 로직 버그로 보인다. 부수 효과로:
`keepMovingAfterDodge`는 `true`로 설정된 후 **어디서도 다시 `false`로 초기화되지 않는다**, 즉
플레이어가 처음 회피한 이후로는 영원히 `true`로 남는다(가드의 나머지 절반인 `isDodge`도 절대
`true`가 되지 않으므로 무해할 뿐이다).

### 8.2 `Event_AttHit`에 형제 메서드가 갖고 있는 소유권 가드가 빠져 있음
`Event_AttFinish()`는 리플레이된 애니메이션 이벤트로부터 비소유자 클라이언트가 공유 상태를 변경하는
것을 막기 위해 `!pv.IsMine`이면 명시적으로 조기 리턴한다(339행). `Event_AttHit()`에는 그런 가드가
없다. 만약 — 이름 규칙과 형제 메서드에 가드가 존재한다는 사실이 강하게 시사하듯 — 두 메서드 모두
공격 클립에서 발동되는 Animation Event 콜백이라면, **다른 플레이어의 공격 애니메이션을 시각적으로
재생하는 모든 클라이언트(즉, `Update()`의 `else` 분기에서 `ChangeAnimState(m_CurState)`를 통해
재생하는 모든 관찰자)가 각자의 머신에서 독립적으로 히트 스캔을 실행하고 `Monster_Ctrl.TakeDamage`를
호출하게 된다.** `Monster_Ctrl`의 HP 권한(authority)이 최종적으로 어떻게 구현되는지에 따라 다르겠지만,
이는 클라이언트 간 데미지가 중복/배가되어 적용될 위험이 있다 — `Monster_Ctrl`을 이 위에 구축하기 전에
반드시 해결해야 할 사항이다(`Event_AttFinish`와 맞춰 `pv.IsMine` 가드를 추가하거나, RPC/마스터클라이언트
권위 경로 뒤로 호출을 옮기는 방법이 있다).

### 8.3 각도 판정 주석이 실제 수식과 맞지 않음 (`Event_AttHit`, 324행)
주석은 "45도 정도 범위"라고 되어 있지만, `Vector3.Dot(...) < 0.45f`가 실제로 구현하는 것은 대략
**63°** 반각 원뿔이다(`arccos(0.45) ≈ 63.3°`). 크래시는 아니지만, 주석이 실제 히트 원뿔의 폭을
잘못 설명하고 있어 이후 밸런싱 작업 시 혼동을 줄 수 있다.

### 8.4 `Remote_TakeDamage`에 도달 불가능한 죽은 분기가 있음 (437~451행)
```csharp
if (CurHp >= 0) { CurHp = NetHp; ImgHpbar.fillAmount = CurHp / MaxHp; }
else if (CurHp <= 0.0f) { CurHp = 0.0f; /* Die(); */ }
```
`CurHp >= 0`은 정확히 0을 **포함한** 모든 음이 아닌 값에 대해 참이므로, `else if`는 `CurHp < 0`일
때만 도달 가능한데, 이 파일 어디에서도 `CurHp`가 음수 값을 갖게 만드는 코드가 없다(`CurHp`는 항상
음이 아닌 값만 대입받는다). 사망 호출이 (주석 처리된 채로) 위치했을 `else if` 분기는 죽은 코드다.
결과적으로: **리모트 프록시는 `CurHp`가 네트워크 동기화를 통해 정확히 `0`에 도달하더라도, "사망"
상태/시각으로는 절대 전환되지 않는다.** `Die()`가 소유자 전용인 `TakeDamage()` 경로에서만 호출된다는
점과 결합하면, 비소유자 클라이언트에는 현재 리모트 아바타를 `isDead`로 표시하는 코드 경로가 전혀
존재하지 않는다.

### 8.5 `Awake()`가 PlayerPrefs 값을 무조건 초기화함
`PlayerPrefs.SetInt("MaxScore", 112500)`는 awake되는 **모든** `Hero_Ctrl` 인스턴스에서 실행된다 —
로컬 플레이어와 모든 리모트 프록시를 가리지 않고, 소유권 가드도 없고, 클래스의 나머지 부분과의
명백한 연관성도 없다. 디버그/테스트용으로 남겨진 코드로 읽힌다; 작성된 그대로라면 `Hero_Ctrl`이
포함된 씬이 로드될 때마다, 그 룸에 있는 모든 플레이어에 대해 실제 `"MaxScore"` 값을 조용히 덮어쓰게
된다.

### 8.6 미사용 / 사실상 죽은 필드
- `isChat` — 선언만 되어 있고, 파일 내 다른 어디에서도 다시 참조되지 않음.
- `m_MvDelay` — `Move()`에서 감소/체크되지만, 0이 아닌 값을 대입하는 곳이 없어 이 스로틀은 절대
  작동할 수 없음.
- `TakeDamage()`의 `cacPos` — 계산만 되고 사용되지 않음.
- `velocity` — 클래스 필드인데, `Move()`의 같은 분기 안에서 대입 즉시 읽히기만 하는, 지역 변수처럼
  동작하는 필드.

### 8.7 취약한 직렬화 의존성, null 가드 없음
`pv`, `agent`, `id`, `ImgHpbar`는 모두 `[SerializeField]`이지만, `[RequireComponent]`도, 자동
`GetComponent` 폴백도, 최초 사용 전 null 체크도 없다(`pv.IsMine`은 라이프사이클의 가장 첫 메서드인
`Awake()`에서 무조건적으로 역참조된다). 프리팹이 인스펙터에서 올바르게 연결되지 않으면, 명확한 설정
오류 메시지 대신 즉시 `NullReferenceException`으로 실패한다.

### 8.8 입력이 이동보다 한 프레임 늦게 반영됨
`Update()`의 소유자 분기에서, `Move()`는 같은 프레임의 `CheckMovementInput()`**보다 먼저** 호출된다
(§5의 `Update()` 참고). 즉 모든 프레임의 이동 처리는 이전 프레임의 `h`/`v`/`rotation`을 기준으로
이루어진다. `AttackOrder()`의 이동 억제 판정도 마찬가지다. 일반적인 프레임 레이트에서는 체감되지
않지만, 입력 반응성을 조사하게 될 경우 알아둘 가치가 있다.

---

## 9. 파일 간 의존성 맵

```
Hero_Ctrl.cs
├── enum  AnimState 필요             → 없음 (프로젝트 전체)
├── class GameManager 필요           → 없음 (같은 이름이지만 무관한 Photon 데모 클래스가
│                                       다른 네임스페이스에 존재)
├── class Camera_Ctrl 필요           → 없음
├── class Monster_Ctrl 필요          → 없음
├── 필요한 Animator 상태/파라미터:    idle, move, jump, dodge, attack, skill (트리거)
│                                     + "Jump"라는 이름의 상태
│                                     → PlayerAnimator.controller에는 현재 이 중 어느 것도 없음
│                                       (관련 없는 "mixamo_com" 상태 하나만 존재)
├── 공개 TakeDamage(float)를 호출할 외부 주체 필요 → 아직 존재하지 않음(의도된 호출자는
│                                       아마도 Monster_Ctrl로 추정)
└── Photon.Pun에 의존 (MonoBehaviourPunCallbacks, PhotonView, IPunObservable,
    PhotonNetwork) → 존재하며 해석 가능함 (Photon PUN2 패키지가 Assets/Photon/ 아래에
    설치되어 있음)
```

`Hero_Ctrl.cs`가 필요로 하는 4개의 프로젝트 전용 타입 중 어느 것도 레포지토리 어디에도 아직
존재하지 않는다(텍스트 검색과 현재 로드된 프로젝트 어셈블리에 대한 Unity의 실시간 reflection API
양쪽 모두로 확인됨). 이 4개 타입과, 제대로 작성된 `PlayerAnimator.controller`가 만들어지기 전까지는
이 스크립트는 컴파일될 수 없으며, 컴파일이 된다 하더라도 컨트롤러가 채워지기 전까지는 어떤
애니메이션도 눈에 보이게 재생되지 않는다.

---

## 10. 다음 단계 제안

1. 누락된 `AnimState` enum을 작성한다(최소한 `idle, move, jump, dodge, attack, skill`을 포함해야
   하며, 모든 `ChangeAnimState(AnimState.X)` 호출부와 일치해야 함).
2. 정적 `Inst`와 `bool Is_Conversating`을 갖는 `GameManager`를 작성한다(또는 176행이 실제로 만들어질
   대화/채팅 게이팅 시스템이 무엇이든 그것을 가리키도록 다시 연결한다).
3. `Camera_Ctrl.InitCamera(GameObject)`와 `Monster_Ctrl.TakeDamage(GameObject, float)`를 작성한다.
4. `PlayerAnimator.controller`를 구축한다: 6개의 트리거 파라미터와 문자 그대로 `Jump`라는 이름의
   상태를 추가하고, `Event_AttHit`/`Event_AttFinish`를 공격 클립의 적절한 프레임에 Animation Event로
   연결한다.
5. 실제 `Monster_Ctrl`에 데미지를 연결하기 전에, `Event_AttHit`와 `Event_AttFinish` 사이의 소유권
   비대칭 문제(§8.2)를 어떻게 처리할지 결정하고 수정한다.
6. 죽어있는 `keepMovingAfterDodge` 분기(§8.1)와 죽어있는 `Remote_TakeDamage` 분기(§8.4)를 수정한다
   — 둘 다 한 줄짜리 순서/조건 수정으로 해결 가능하다.
7. `Awake()`의 무조건적인 `PlayerPrefs.SetInt("MaxScore", 112500)`을 제거하거나 그 의도를 명확히
   설명한다(§8.5).

---

# 조사 보고서: Assets/Scripts/GameManager.cs

(2026-08-13 추가 — `Hero_Ctrl.cs` 이후 `Assets/Scripts/`에 새로 추가된 두 번째 파일. 위 보고서의
연속편이며, 아래 섹션 번호는 위 보고서(§1~§10)에 이어서 매긴다.)

## 11. 파일 인벤토리 갱신

| 파일 | 타입 | 라인 수 | 역할 |
|---|---|---|---|
| `Assets/Scripts/Hero_Ctrl.cs` | `MonoBehaviourPunCallbacks`, `IPunObservable` | 491 | (§1~§10 참고) |
| `Assets/Scripts/GameManager.cs` | `MonoBehaviourPunCallbacks` | 185 | 룸 단위 싱글턴: 채팅 로그 RPC 중계, 방 나가기 처리, 씬 진입 시 히어로 스폰. **CP949로 저장되어 있음(§12 참고)** |

## 12. 인코딩 결함 — 심각(CRITICAL)

`GameManager.cs`는 UTF-8이 아니다. 세 가지 방법으로 교차 확인했다:

1. `file` 유틸리티 결과 — `Hero_Ctrl.cs`는 `UTF-8 (with BOM) text`로 정확히 판별되는 반면,
   `GameManager.cs`는 `ISO-8859 text`로 판별된다(= 유효한 UTF-8이 아니라는 libmagic의 폴백 판정).
2. `iconv -f UTF-8 -t UTF-8`로 strict 디코딩을 시도하면 **12행 33열에서 즉시 실패**한다
   (`cannot convert`) — 즉 파일 안에 유효하지 않은 UTF-8 바이트 시퀀스가 실제로 존재한다.
3. `iconv -f CP949 -t UTF-8`로 디코딩하면 처음부터 끝까지 깨짐 없이 정상적인 한글 주석/문자열이
   복원된다(예: 12행 `// ä�� �ִ� ����` → `// 채팅 최대 갯수`). 즉 이 파일은 Windows 한글 코드페이지
   (CP949/EUC-KR 계열)로 저장된 뒤 UTF-8로 재저장되지 않은 것으로 보인다.

**왜 심각한가**: C# 컴파일러(Roslyn)는 BOM이 없는 소스 파일을 기본적으로 UTF-8로 해석한다. 유효하지
않은 UTF-8 바이트를 만나면 대체 문자(U+FFFD)로 치환하거나 손상된 상태로 컴파일에 들어갈 수 있다.
이 파일에서 한글이 등장하는 곳은 단순 주석뿐 아니라, **실제로 플레이어에게 노출되는 문자열
리터럴**도 포함한다 — `"] 방 나감</color>"`(81행), 로그 조립에 쓰이는 한글 라벨 등. 즉 최악의 경우
컴파일은 되지만 채팅 로그나 "방 나감" 알림이 깨진 문자로 모든 클라이언트에 표시될 수 있다(RPC로
`AllBuffered` 브로드캐스트되므로 한 명이라도 깨진 문자열을 보내면 방 전체에 영구히 남는다, §17 참고).
같은 폴더의 `Hero_Ctrl.cs`는 정상적인 UTF-8 BOM 파일이라는 점과 대조하면, 이 파일만 다른 도구/설정으로
저장되었을 가능성이 높다. **UTF-8(BOM)으로 재저장하는 것을 최우선으로 권장한다.**

## 13. 클래스 개요

```csharp
public class GameManager : MonoBehaviourPunCallbacks
```

- `static public GameManager Inst`로 클래스 싱글턴을 노출한다(스레드 세이프 가드 없음, `Awake()`에서
  무조건 자신을 대입 — 씬에 두 개 이상 존재하면 나중에 `Awake`되는 쪽이 조용히 덮어씀).
- `[SerializeField] PhotonView pv`를 통해 `LogMsg` RPC를 송수신하는 룸 단위 컨트롤러로, 채팅 로그
  중계, 방 나가기(`OnClickBackBtn`/`OnLeftRoom`), 씬 진입 시 플레이어 아바타 스폰(`CreateHero`)까지
  세 가지 서로 다른 책임을 한 클래스가 담당한다.

## 14. `Hero_Ctrl.cs`와의 의존성 교차 확인 — 부분 해소

위 보고서 §2/§9에서 `Hero_Ctrl.CheckMovementInput()`(176행)이 참조하는 `GameManager.Inst.Is_Conversating`이
프로젝트 어디에도 존재하지 않는 미정의 의존성이라고 지적했다. 이번에 추가된 `GameManager.cs`는:

```csharp
static public GameManager Inst;
...
private bool is_Conversating;
public bool Is_Conversating => is_Conversating;
```

`Hero_Ctrl.cs`가 기대하던 정확한 시그니처(`static Inst`, `bool Is_Conversating` 프로퍼티)와 **정확히
일치한다.** 즉 `Hero_Ctrl.cs`의 컴파일을 막던 4개 미정의 타입(§2) 중 `GameManager` 하나는 이번 추가로
해소되었다.

다만 나머지 3개(`AnimState`, `Camera_Ctrl`, `Monster_Ctrl`)는 여전히 프로젝트 어디에도 존재하지 않으므로,
**`Hero_Ctrl.cs`는 여전히 컴파일되지 않는다.** 또한 `plan.md` §9의 `IsMovementLocked` 도입 근거("존재하지
않는 전역 싱글턴에 하드 의존하지 않기 위함")는 이제 "타입이 없어서"가 아니라 "느슨한 결합을 유지하기
위함"으로 성격이 바뀐다 — `GameManager`가 실제로 존재하게 됐으니 `PlayerController`가 직접 참조하는
것도 기술적으로는 가능해졌지만, 여전히 `PlayerController`를 채팅/UI 시스템에 하드 의존시키지 않는 편이
낫다고 판단된다면 `IsMovementLocked` 설계를 그대로 유지할 수 있다(구현 전 재확인 필요 시 알려달라).

## 15. 필드 목록

| 필드 | 타입 | 용도 |
|---|---|---|
| `Inst` | `static GameManager` | 전역 접근용 싱글턴 참조 |
| `MAX_CHAT` | `const int` (50) | 채팅 로그에 유지할 최대 메시지 수 |
| `pv` | `[SerializeField] PhotonView` | `LogMsg` RPC 송수신용. null 가드 없음(§20-10, Hero_Ctrl과 동일 패턴) |
| `m_BackBtn` | `[SerializeField] Button` | 방 나가기 버튼, `OnClickBackBtn` 리스너 연결 |
| `InputFdChat` | `[SerializeField] TMP_InputField` | 채팅 입력창 |
| `txtLogMsg` | `[SerializeField] TextMeshProUGUI` | 채팅 로그 출력 텍스트 |
| `m_MsgList` | `List<string>` | 수신된 로그 메시지 누적 목록(최대 `MAX_CHAT`개 유지) |
| `bEnter` | `bool` | 채팅창 열림/닫힘 토글 상태 |
| `is_Conversating` / `Is_Conversating` | `bool` / 공개 읽기전용 프로퍼티 | 채팅 중 여부. `Hero_Ctrl`이 이동 억제 조건으로 참조(§14) |

## 16. 라이프사이클

**`Awake()`**: `Inst = this`로 싱글턴 등록 직후 `CreateHero()`를 무조건 호출한다(§19에서 문제점 설명).

**`Start()`**: `Time.timeScale = 1.0f`로 일시정지 해제, `PhotonNetwork.IsMessageQueueRunning = true`로
RPC 수신 큐 활성화, 뒤나가기 버튼 리스너 연결, 그리고 `"[닉네임] Connected"` 메시지를 `LogMsg` RPC로
`AllBuffered` 브로드캐스트한다.

**`Update()`**: 매 프레임 `Return` 키의 `KeyUp`만 감지한다(§17).

## 17. 채팅 시스템

- `Return` 키를 뗄 때마다 `bEnter`를 토글한다. 열릴 때(`bEnter == true`): `is_Conversating = true`,
  입력창 활성화 + 포커스. 닫힐 때: 입력창 비활성화, `is_Conversating = false`, 입력값이 비어있지
  않으면 `BroadcastingChat()` 호출.
- **채팅창을 닫는 유일한 경로가 "Enter 키를 다시 누르는 것"뿐이다.** 만약 플레이어가 입력창을 클릭해서
  포커스를 잃거나(`InputField`가 자체적으로 포커스 아웃되는 경우), 다른 UI를 클릭하거나, `Esc` 등으로
  닫으려 하면 `is_Conversating`이 `true`로 영구히 남아 `Hero_Ctrl.CheckMovementInput()`(§14로 연결됨,
  §6.3 참고)이 이동 입력을 계속 0으로 만들어버릴 수 있다 — 즉 "채팅창이 열린 것처럼 보이지 않는데
  캐릭터가 움직이지 않는" 형태의 소프트락 가능성이 있다.
- `BroadcastingChat()`은 `"[닉네임] 입력내용"`을 흰색(`#ffffff`)으로 감싸 `LogMsg` RPC를
  `RpcTarget.AllBuffered`로 보낸다.
- `LogMsg`(`[PunRPC]`)는 `info.Sender.IsLocal && isChatMsg`일 때만 `msg.Replace("#ffffff", "#ffff00")`로
  흰색을 노란색으로 바꾼다. **주석과 실제 코드가 서로 다른 값을 이야기한다**: 주석은 "방장이 말을 하면
  `#00ffff`(하늘색)로 들어오니 방장 자신에게도 하늘색으로 보일 것"이라고 설명하지만, 실제 치환 대상은
  `#ffffff`(흰색)이고 결과는 `#ffff00`(노란색)이며, `#00ffff`는 코드 어디에도 등장하지 않는다. 또한
  `BroadcastingChat()`은 방장 여부(`PhotonNetwork.IsMasterClient`)를 전혀 확인하지 않으므로, 주석이
  설명하는 "방장 메시지는 다른 색으로 온다"는 동작 자체가 애초에 구현되어 있지 않다. 실제로 일어나는
  일은 "자신이 보낸 메시지만 로컬에서 노란색으로 하이라이트된다"이며, 이는 나쁘지 않은 UX이지만
  주석의 설명과는 무관하다(Hero_Ctrl.cs §8.3의 "45도" 주석-코드 불일치와 같은 종류의 문제).
- `m_MsgList`는 `MAX_CHAT`(50)을 넘으면 가장 오래된 항목을 제거하지만, `txtLogMsg.text`는 매 메시지
  수신마다 `""`로 초기화한 뒤 리스트 전체를 처음부터 다시 이어붙인다(144~148행). 새로 추가된 메시지
  하나만 텍스트에 append하면 될 것을 매번 최대 50개 문자열을 반복 연결(각 `+=`가 새 문자열을 생성)
  — `CLAUDE.md`의 "최적화를 고려한 코드 작성" 원칙과 부딪히는 지점이다.
- 구조적 확장성 문제: `RpcTarget.AllBuffered`로 보낸 RPC는 Photon 서버에 룸이 살아있는 동안 계속
  쌓인다(명시적으로 `PhotonNetwork.RemoveRPCs`를 호출하지 않는 한 자동 정리되지 않음). 방이 오래
  유지될수록 새로 입장하는 클라이언트가 재생해야 하는 버퍼링된 RPC 수가 계속 늘어난다 — 클라이언트
  쪽에서는 `m_MsgList`가 50개로 잘리지만, 서버가 보내는 버퍼 RPC 자체의 양은 줄어들지 않는 구조적
  비효율이다.

## 18. 방 나가기 처리 (`OnClickBackBtn` / `OnLeftRoom`)

- `OnClickBackBtn()`: 버튼을 즉시 `interactable = false`로 만들어 중복 클릭을 막는다(Hero_Ctrl에는
  없던 방어 코드). `"] 방 나감</color>"` 메시지를 `LogMsg` RPC로 브로드캐스트한 뒤, 자신이 방의
  마지막 인원이면(`PlayerList.Length <= 1`) 룸의 `CustomProperties`를 초기화하고, 이어서 자신의
  `CustomProperties`도 초기화한 다음 `PhotonNetwork.LeaveRoom()`을 호출한다.
- `OnLeftRoom()`(Photon 콜백, `LeaveRoom()` 완료 후 호출됨): `SceneManager.LoadScene("PhotonLobby")`를
  호출한다. **이 호출은 현재 프로젝트 상태에서 반드시 실패한다.** 두 가지를 확인했다:
  1. `ProjectSettings/EditorBuildSettings.asset`의 `m_Scenes: []` — **Build Settings에 씬이 단 하나도
     등록되어 있지 않다.** `SceneManager.LoadScene(string)`은 Build Settings에 등록된 씬만 이름으로
     찾을 수 있으므로, 등록된 씬이 0개인 현재 상태에서는 어떤 이름을 넣어도 실패한다
     (`"Scene '...' couldn't be loaded because it has not been added to the build settings"`).
  2. 설사 Build Settings를 채우더라도, 프로젝트에 `"PhotonLobby"`라는 이름의 씬 자체가 존재하지 않는다
     (`Assets/Scenes/`에는 `SampleScene.unity`와 `LobbyScene.unity`만 있음, 프로젝트 전체 텍스트 검색으로도
     `"PhotonLobby"` 문자열은 이 스크립트 자체 말고는 없음). 아마 새로 추가된 `LobbyScene.unity`를
     가리키려 한 것으로 보인다 — 이름 불일치로 추정된다.

## 19. `CreateHero()` 스폰 로직

```csharp
GameObject hPosObj = GameObject.Find("HeroSpawnPos");
if (hPosObj != null) {
    addPos.x = Random.Range(-5.0f, 5.0f);
    addPos.z = Random.Range(-5.0f, 5.0f);
    hPos = hPosObj.transform.position + addPos;
    PhotonNetwork.Instantiate("HeroPrefab", hPos, Quaternion.identity, 0);
}
```

- `"HeroSpawnPos"`라는 이름의 오브젝트를 `GameObject.Find`로 찾는다. **프로젝트의 어떤 씬 파일에도
  이 이름의 오브젝트가 존재하지 않는다**(전체 텍스트 검색으로 스크립트 자기 자신 외에는 매치 없음).
  즉 `hPosObj`는 항상 `null`이고, `if` 블록 전체가 조용히 스킵된다 — 에러 로그조차 없이 히어로가
  스폰되지 않는다.
- 설사 스폰 위치를 찾더라도, `"HeroPrefab"`이라는 이름의 프리팹이 **어떤 `Resources` 폴더 아래에도
  존재하지 않는다**(프로젝트 전체 `Resources/` 하위 에셋을 나열해 확인 — Photon 데모용 프리팹들만
  있고 `HeroPrefab`은 없음). `PhotonNetwork.Instantiate`는 내부적으로 `Resources.Load`를 사용하므로,
  이 이름의 프리팹이 없으면 인스턴스화가 실패한다(Photon이 콘솔에 에러를 남김).
- `CreateHero()`가 `Awake()`에서 **룸 조인 여부 확인 없이** 무조건 호출된다. `PhotonNetwork.Instantiate`는
  로컬 클라이언트가 룸에 실제로 조인한 뒤에만 유효한 호출인데, `Awake()`는 씬이 로드되는 시점에 실행되며
  네트워크 연결/룸 조인은 비동기이므로, 아직 룸에 들어가기 전에 이 코드가 먼저 실행되는 레이스 컨디션이
  있을 수 있다. 통상적인 Photon 패턴은 `OnJoinedRoom()` 콜백(이 클래스는 이미 `MonoBehaviourPunCallbacks`를
  상속하고 있어 오버라이드하기 쉬움) 안에서 스폰하는 것이다.

## 20. 확인된 버그 / 코드 스멜 정리 (총괄)

1. **CP949 인코딩** — 심각, 플레이어에게 노출되는 문자열이 손상될 위험(§12).
2. **Build Settings에 씬이 0개 등록** + **존재하지 않는 씬 이름 `"PhotonLobby"` 로드 시도** — 방 나가기
   흐름이 항상 실패한다(§18).
3. **`"HeroSpawnPos"` 오브젝트가 어떤 씬에도 없음** — 스폰이 조용히 스킵됨(§19).
4. **`"HeroPrefab"` 프리팹이 `Resources`에 없음** — 스폰 위치를 찾더라도 인스턴스화 실패(§19).
5. **`CreateHero()`가 `Awake()`에서 룸 조인 레이스 컨디션 없이 호출됨**(§19).
6. **`LogMsg`의 색상 치환 로직이 주석과 불일치**(`#00ffff`/방장 언급 vs 실제 `#ffffff→#ffff00`
   자기 메시지 하이라이트)(§17).
7. **`is_Conversating`이 Enter 키로만 해제됨** — 다른 방식으로 채팅 UI가 닫히면 이동이 영구히 잠길
   가능성(§17). `Hero_Ctrl`과 연결되는 지점이라 특히 중요(§14).
8. **`AllBuffered` 채팅 RPC가 방 수명 동안 서버에 무한정 누적** — 오래 유지되는 방에서 신규 입장자의
   버퍼 RPC 재생량이 계속 늘어나는 구조적 비효율(§17).
9. **`txtLogMsg.text` 매 메시지마다 전체 재구성** — 새 메시지 하나만 append하면 되는데 최대 50개
   문자열을 매번 반복 연결(§17, `CLAUDE.md` 최적화 원칙과 상충).
10. **`pv`/`m_BackBtn`/`InputFdChat`/`txtLogMsg`에 null 가드 없음** — `Hero_Ctrl.cs` §8.7과 동일한
    패턴의 취약한 직렬화 의존성.
11. **`Inst` 싱글턴에 중복 인스턴스 가드가 없음** — 씬에 `GameManager`가 두 개 이상 존재하면 나중에
    `Awake`되는 쪽이 아무 경고 없이 `Inst`를 덮어쓴다.

## 21. 파일 간 의존성 맵 갱신

```
GameManager.cs
├── GameObject "HeroSpawnPos" 필요        → 없음 (모든 씬)
├── Resources 프리팹 "HeroPrefab" 필요    → 없음 (모든 Resources 폴더)
├── 씬 "PhotonLobby" 필요                 → 없음. Build Settings 자체가 비어 있음(§18)
├── TextMeshPro(TMPro) 패키지 의존        → Package Manager 매니페스트 확인 필요(미검증)
└── Photon.Pun 의존 (MonoBehaviourPunCallbacks, PhotonView, PunRPC, PhotonNetwork)
                                          → 존재하며 해석 가능함

Hero_Ctrl.cs
└── class GameManager 필요 (static Inst, bool Is_Conversating)
                                          → 이번에 해소됨(§14). 나머지 3개 미정의 타입은 여전히 없음
                                            (AnimState, Camera_Ctrl, Monster_Ctrl — 위 §9 참고)
```

## 22. 다음 단계 제안

1. `GameManager.cs`를 UTF-8(BOM)로 재저장한다 — 데이터 손상 위험이 있으므로 가장 시급(§12).
2. Build Settings(`File > Build Settings`)에 실제 씬(`SampleScene`, `LobbyScene` 등)을 등록하고,
   `OnLeftRoom()`의 `"PhotonLobby"`를 실제 씬 이름(추정: `"LobbyScene"`)으로 맞춘다(§18).
3. 스폰 지점 오브젝트를 `"HeroSpawnPos"`라는 이름으로 실제 씬에 배치한다(§19).
4. `"HeroPrefab"`을 `Resources` 폴더 하위에 만든다 — 단, `Hero_Ctrl.cs`가 아직 컴파일되지 않으므로
   (§2의 남은 3개 미정의 타입) 이 프리팹을 완성하려면 그 타입들도 함께 해결해야 한다.
5. `CreateHero()` 호출 시점을 `Awake()`에서 `OnJoinedRoom()` 콜백으로 옮겨 레이스 컨디션을 제거하는
   것을 검토한다(§19).
6. `LogMsg`의 색상 치환 로직을 실제 의도(방장 메시지 강조인지, 자기 메시지 강조인지)에 맞게 주석과
   코드를 함께 정리한다(§17).
7. `is_Conversating`을 닫는 경로를 Enter 키 하나에만 의존하지 않도록 보강하는 것을 검토한다(예:
   `InputField.onDeselect`, `Esc` 키 등) — `Hero_Ctrl`의 이동 잠금과 직결되므로 우선순위가 있다(§17).

---

# 조사 보고서: Assets/02. Scripts/

(2026-08-14 추가. `RoomItemPlan.md`/`PlayerControllPlan.md` 작업 과정에서 이미 부분적으로 다룬
파일들도 있지만, 이 보고서는 그 파일들을 포함해 `Assets/02. Scripts/` 전체를 도메인 횡단으로 다시
훑어 하나의 문서로 정리한 것이다. 위 §1~§22(`Assets/Scripts/`)와는 별개의 트리이며, 섹션 번호는
이어서 매긴다.)

## 23. 폴더/어셈블리 구조

```
Assets/02. Scripts/
├── TagOfChaos.Scripts.asmdef   # 이 폴더 전체가 공유하는 단일 어셈블리
├── Camera/
│   └── Camera_Ctrl.cs
├── ColorTag/                    # 10개 파일 — 색상 투표/페인팅/술래 판정 미니게임
│   ├── NetKeys.cs, NetEventCodes.cs, ColorPaletteSO.cs, BrushSettingsSO.cs
│   ├── ColorVoteTally.cs, TaggerColorAssigner.cs
│   ├── ColorSelectionManager.cs, ColorSelectionPanel.cs, ColorSwatchButton.cs
│   ├── PlayerPaintCanvas.cs, BrushCursorController.cs
│   ├── PlayerColorVoteIndicator.cs, PlayerColorDisplay.cs
│   └── RoomLifecycleWatcher.cs
├── Unit/                        # 5개 파일 — 이동 전용 캐릭터 컨트롤러 (PlayerControllPlan.md에서 이미 상세 조사)
│   ├── HideOrSeekPlayer.cs, PlayerMoveState.cs, PlayerGroundDetector.cs
│   ├── PlayerAnimationDriver.cs, PlayerNetworkSync.cs
├── Lobby/                       # 4개 파일 — 로비/대기방 UI (RoomItemPlan.md에서 이미 상세 조사)
│   ├── LobbyController.cs, RoomListItem.cs
│   ├── GameLobbyController.cs, PlayerListItem.cs
└── Dev/
    └── OfflineModeBootstrap.cs
```

총 25개 `.cs` 파일. `TagOfChaos.Scripts.asmdef`(`Assets/02. Scripts.asmdef`) 하나가 이 폴더 아래
**전체**(5개 하위 도메인 전부)를 하나의 어셈블리로 묶는다 — 도메인별로 별도 asmdef가 나뉘어 있지
않으므로, 예를 들어 `ColorTag/` 안의 스크립트 하나에 컴파일 에러가 나면 `Lobby/`나 `Unit/`도 함께
빌드가 막힌다(§12.2/§13.7에서 `Hero_Ctrl.cs`의 에러가 `Assembly-CSharp` 전체를 막았던 것과 같은
종류의 위험이, 이번엔 이 asmdef 하나의 스코프 안에서 여전히 유효하다). 참조 어셈블리는
`PhotonUnityNetworking`, `PhotonRealtime`, `Unity.TextMeshPro`, `Unity.ugui` 4개이며, 실제 코드가
쓰는 `UnityEngine.AI`(`NavMeshAgent`)는 별도 참조 없이도 기본 엔진 모듈로 해석된다
(`noEngineReferences: false`). 현재 이 어셈블리는 **컴파일 에러 0건**이다(이번 조사 중 `read_console`로
재확인).

`Assets/Scripts/`(§1~§22, 이제 `GameManager.cs` 하나만 남음)와는 별도의 어셈블리 트리다 — 즉
`Assets/02. Scripts/`의 어떤 스크립트도 `Assets/Scripts/GameManager.cs`를 직접 참조하지 않고
(§30에서 확인), 그 반대도 마찬가지다. 두 트리는 지금 완전히 분리되어 있다.

## 24. 도메인 개요 (한 줄 요약)

| 도메인 | 파일 수 | 역할 |
|---|---|---|
| `Camera/` | 1 | 3인칭 추적 카메라(줌 없음, 우클릭 드래그 회전만) |
| `ColorTag/` | 10 | 4라운드 색상 투표 → 캐릭터 페인팅 → 술래 컬러 치환까지 이어지는 이 게임의 핵심 미니게임 |
| `Unit/` | 5 | 이동/점프/회피 전용 캐릭터 컨트롤러(전투/HP 없음, `PlayerControllPlan.md` §1~§16에서 이미 상세 조사·리팩토링·버그 수정 완료) |
| `Lobby/` | 4 | 로비 방 목록/생성/입장 + 대기방 인원/시작 버튼 UI(`RoomItemPlan.md` §1~§7에서 이미 상세 조사·구현 완료) |
| `Dev/` | 1 | 오프라인 단독 테스트용 부트스트랩(프로덕션 코드 아님) |

`Unit/`과 `Lobby/`는 각각 별도 계획 문서(`PlayerControllPlan.md`, `RoomItemPlan.md`)에서 이미
라인 단위로 조사됐으므로, 아래 §26/§28에서는 핵심 요약과 "그 문서 이후 바뀐 것이 있는지"만 다시
확인하고, 새로 조사하는 `Camera/`(§27)와 `ColorTag/`(§25)를 중심으로 깊이 다룬다.

## 25. `ColorTag/` 도메인 상세 — 4라운드 색상 투표 → 페인팅 → 술래 치환

### 25.1 전체 데이터 흐름 (10개 파일을 하나의 시퀀스로 재구성)

```
① ColorSelectionManager.StartColorSelection()  [마스터 전용, §25.2]
   → Room.CustomProperties: RoundIndex=0, RoundEndTime=now+20s,
     Color0..3=-1, TaggerActorNumber=-1 / Room.IsOpen=false

② (매 프레임, 전 클라이언트) ColorSelectionPanel.Update()
   → RoundIndex/RoundEndTime을 읽어 "N/4"·남은시간 표시, 이미 확정된 색 스와치 잠금

③ (플레이어 조작) ColorSwatchButton.OnClick → ColorSelectionManager.SubmitVote(colorIndex)
   → LocalPlayer.CustomProperties: VoteColorIndex = colorIndex

④ (플레이어 조작, 자기 캐릭터 표면 클릭+드래그) PlayerPaintCanvas.Update()
   → 좌클릭 레이캐스트가 자기 paintableCollider에 맞으면, 현재 VoteColorIndex로 StampBrush()
   → 로컬 PaintCanvas(RenderTexture)에 즉시 스탬프 + PaintStroke 이벤트로 다른 클라이언트에 전파
   → BrushCursorController가 마우스 위치에 3D 붓 커서를 투표색으로 표시(§25.7)
   → PlayerColorVoteIndicator가 캐릭터 머리 위에 현재 투표색 스프라이트를 빌보드로 표시(§25.8)

⑤ (매 프레임, 마스터 전용) ColorSelectionManager.Update()
   → RoundEndTime 경과 감지 → ResolveRound(roundIndex)
     - ColorVoteTally.Resolve(): 다수결(동점이면 랜덤) 또는 무투표 시 남은 색 중 랜덤
     - ColorN = 확정색 세팅
     - roundIndex+1 < 4  → RoundIndex/RoundEndTime 갱신, 다음 라운드로
     - roundIndex+1 == 4 → AssignTagger() [무작위 1인 + TaggerColorAssigner.BuildVariantSet로
       확정 4색 중 한 슬롯을 다른 색으로 치환] 도 같은 트랜잭션에 포함, RoundIndex=5("완료")
   → 매 라운드 끝에 전원 투표(VoteColorIndex) 초기화(LocalPlayer만, §25.2 버그 참고)

⑥ (매 프레임, 전 클라이언트) PlayerPaintCanvas.DetectRoundChange()
   → RoundIndex가 바뀐 것을 감지하면, 방금 끝난 라운드에 자신이 칠했던 좌표들을
     "확정색"으로 강제 재도색(FinalizeCurrentRoundStrokes, 잠금 무시) + 전파
     → 투표에서 졌거나 다른 색으로 칠했더라도, 라운드가 끝나면 시각적으로 그 라운드의
       확정색으로 맞춰진다

⑦ RoundIndex == 5("완료")가 되면, PlayerColorDisplay.OnRoomPropertiesUpdate()
   → 자신이 TaggerActorNumber와 같은 플레이어라면(=술래):
     baseSet(확정 4색) vs TaggerVariantSet(치환된 4색)에서 다른 슬롯 1개를 찾아,
     자기 캔버스 전체에서 그 옛 색으로 칠해진 모든 픽셀을 새 색으로 전역 치환
     (ApplyColorReplace, 브러시 스탬프가 아니라 캔버스 전체 검색-치환)
```

`NetKeys`(7개 키)와 `NetEventCodes`(`PaintStroke=1` 1개)가 이 전체 흐름에서 쓰이는 Room/Player
CustomProperties 키와 Photon RaiseEvent 코드를 한곳에 모아둔 상수 클래스다.

### 25.2 `ColorSelectionManager.cs` — 라운드 진행의 유일한 권위자

- **마스터 클라이언트만** `Update()`에서 라운드 만료를 감지하고 `ResolveRound()`를 실행한다
  (`if (!PhotonNetwork.IsMasterClient) return;`). 결과는 `Room.SetCustomProperties`로 전원에게
  동기화되므로, 클라이언트마다 다른 `System.Random rng`(시드 미지정, 인스턴스별로 다름)를 갖고
  있어도 **문제가 되지 않는다** — 오직 마스터의 굴림만 실제로 결과를 결정하고 그 결과만 전파되기
  때문이다(다수의 리뷰어가 "멀티플레이에서 시드 없는 RNG는 위험하다"고 지레짐작하기 쉬운
  패턴이지만, 이 경우엔 단일 권위자 구조라 안전하다는 것을 직접 코드 추적으로 확인했다).
- **사소한 버그 후보**: `ResetAllVotes()`는 `PhotonNetwork.LocalPlayer.SetCustomProperties(...)`만
  호출한다 — 즉 **이 코드를 실행하는 마스터 클라이언트 자기 자신의 투표만** `-1`로 리셋되고,
  다른 플레이어들의 `VoteColorIndex`는 리셋되지 않는다. 매 라운드가 시작될 때 이전 라운드에
  투표했던 값이 남아있는 상태로 새 라운드가 시작되는 것이다. 실제로 문제가 되는지는 UI 쪽
  동작에 달려 있다: `ColorSwatchButton.SetLocked()`가 이미 확정된 색의 스와치를 잠그므로 "이전
  라운드에 골랐던 색"이 이번 라운드에서 이미 잠겨있다면 새로 골라야 하지만, 아직 잠기지
  않은(=아직 확정 안 된) 색이라면 플레이어가 아무 것도 다시 누르지 않아도 예전 선택이 그대로
  이번 라운드의 투표로 계속 집계된다 — "매 라운드 새로 골라야 한다"는 의도라면 결함이고,
  "굳이 다시 안 골라도 마지막 선택이 이어진다"는 의도라면 정상이다. 의도 확인이 필요하다.
- `StartColorSelection()`을 실제로 호출하는 곳은 프로젝트 전체에서
  **`Assets/02. Scripts/Dev/OfflineModeBootstrap.cs` 단 한 곳뿐**이며, 그마저도
  `autoStartColorSelection` 체크박스가 켜져 있을 때만 실행된다(현재 `PlayerTestScene.unity`에서는
  켜져 있음, §25.9 참고). **실제 로비 흐름(`LobbyController` → `GameLobbyController` →
  `GameScene`) 어디에도 `StartColorSelection()`을 호출하는 코드가 없다** — `GameLobbyController.
  OnStartGameButtonClicked()`는 `PhotonNetwork.LoadLevel("GameScene")`만 호출할 뿐이다. 즉 지금
  상태로 실제 멀티플레이 매칭을 끝까지 진행해도, `GameScene`에 도착한 뒤 색상 선택 미니게임이
  저절로 시작되지 않는다 — 이 미니게임은 현재 오직 `PlayerTestScene`의 개발용 부트스트랩
  경로로만 실행 가능하다. **이것이 이 도메인 전체에서 가장 중요한 통합 공백이다.**

### 25.3 `ColorSelectionPanel.cs` — 순수 표시 전용

라운드 진행에 어떤 영향도 주지 않는 읽기 전용 UI: `Update()`마다 Room 프로퍼티를 폴링해
라운드/남은시간 표시와 스와치 잠금만 갱신한다. `PhotonNetwork.InRoom`이 아니면 아무 것도 하지
않으므로 로비 등 방 밖 화면에서 실수로 동작할 위험은 없다.

### 25.4 `ColorSwatchButton.cs` — 얇은 입력 위임자

클릭 시 `manager.SubmitVote(colorIndex)`만 호출하는 얇은 래퍼. `manager` 필드가 `[SerializeField]`로
씬/프리팹에서 수동 연결되어야 하며 null 가드가 없다 — `Assets/Scripts/Hero_Ctrl.cs` 시절부터
이 프로젝트 전반에 걸쳐 반복되는 패턴(§8.7)이 여기도 그대로 있다.

### 25.5 `ColorVoteTally.cs` / `TaggerColorAssigner.cs` — 순수 함수, 부작용 없음

둘 다 `static class`의 순수 함수라 유닛 테스트가 쉬운 형태로 잘 분리되어 있다. `ColorVoteTally.Resolve`는
무투표/전원-제외색 상황까지 방어적으로 처리한다(§25.1 코드 스니펫 참고). `TaggerColorAssigner.
FindSwappedSlot`은 정확히 한 슬롯만 다르다고 가정하는데, 이 가정은 `BuildVariantSet`이 항상 정확히
한 슬롯만 바꾸도록 보장하므로 실제로 깨지지 않는다(교차 확인 완료).

### 25.6 `PlayerPaintCanvas.cs` — 이 도메인에서 가장 복잡한 파일 (213줄)

- 캐릭터 1개당 런타임에 개별 `RenderTexture`(기본 512×512, ARGB32)를 새로 만들어 스킨 머티리얼에
  합성한다 — 직렬화 필드로 텍스처를 공유하면 모든 캐릭터가 같은 캔버스를 덮어쓰게 된다는 점을
  주석으로 명시하고 실제로 `InitPaintCanvas()`에서 매번 `new RenderTexture(...)`로 새로 만든다
  (올바른 패턴).
- 알파 채널을 "잠금 마스크"로 쓴다: `brushStampMaterial`은 이미 칠해진(알파=1) 픽셀을 다시
  건드리지 않고, `finalizeStampMaterial`만 잠금을 무시하고 항상 덮어쓴다 — 그래서 라운드 진행
  중에는 한 번 칠한 자리를 다른 색으로 덮어 칠할 수 없고(§25.1-④), 라운드가 끝나야만
  확정색으로 강제 재도색된다(§25.1-⑥). 게임 디자인 의도로 보이며 버그는 아니다.
  - **다만 이 잠금은 최종 사용자에게 설명되지 않는 한 혼란을 줄 수 있다**: 라운드 중 자기가
    고른 색으로 한 번 칠한 자리는, 그 라운드 안에서는 다른 색으로 다시 칠할 방법이 없다(브러시
    잠금 때문에). 의도된 "실수해도 못 고친다"는 긴장감인지, UX상 재고가 필요한 제약인지는
    확인이 필요하다.
- `ApplyStamp()`가 `[SerializeField] Material` 2개(`brushStampMaterial`/`finalizeStampMaterial`)의
  셰이더 프로퍼티(`_StampUV`/`_StampRadius`/`_StampColor`)를 매 스탬프마다 직접 변경한다 — 이
  머티리얼들이 인스턴스가 아니라 **에셋을 그대로 공유**하는 것이라면, 여러 캐릭터가 동시에
  칠할 때 서로의 프로퍼티 값을 덮어쓸 위험이 있어 보이지만, 실제로는 C#이 싱글스레드로 실행되고
  `ApplyStamp()` 안에서 프로퍼티 설정 → `Graphics.Blit` → 임시 텍스처 해제까지 한 번에 끝나므로
  (한 캐릭터의 스탬프 처리가 다음 캐릭터의 스탬프 처리 도중에 끼어들 수 없음) 실질적인 경합은
  없다 — 겉보기엔 위험해 보이지만 실행 순서상 안전한 패턴으로 확인됐다.
- 네트워크 전파(`SendStrokeEvent`)는 `ReceiverGroup.Others`로만 보낸다 — 보낸 사람은 자기 스트로크를
  로컬에서 이미 찍었으므로 자기 자신에게 다시 받아 이중 스탬프를 찍는 것을 원천적으로 피한다
  (올바른 설계).
- `DetectRoundChange()`는 소유 여부와 무관하게 **모든 클라이언트에서** 매 프레임 실행되지만,
  실제 재도색(`FinalizeCurrentRoundStrokes`)은 `pv.IsMine`일 때만 실행된다 — 즉 각자 자신의
  캐릭터에 대해서만 재도색 책임을 진다(자기 캐릭터니까 당연하지만, 관찰만 하는 리모트 인스턴스도
  이 메서드 자체는 매 프레임 호출된다는 점은 사소한 낭비다 — `roundIndex == trackedRoundIndex`
  얼리 리턴이 있어 실질 비용은 낮다).
- null 가드 없음: `palette`, `bodyRenderer`, `paintedSkinShader` 등 다수의 `[SerializeField]`가
  인스펙터 미연결 시 `NullReferenceException`으로 즉시 실패한다(§8.7과 동일 패턴). 다만
  `bodyRenderer == null || paintedSkinShader == null`인 경우 스킨 합성만 건너뛰는 부분적 가드는
  있다(44행대).

### 25.7 `BrushCursorController.cs` — 3D 붓 커서

`brushSettings.CursorPrefab`을 라운드 시작 시 1회 인스턴스화해 계속 재사용한다(매 프레임 새로
만들지 않음 — 최적화 관점에서 적절). 자신의 `PlayerPaintCanvas`를 찾을 때
`FindObjectsByType<PlayerPaintCanvas>`로 **씬 전체를 매 프레임 순회**한다(`localPaintCanvas`가
아직 없거나 소유자가 아닐 때만) — 플레이어 수가 적어 지금은 문제가 안 되지만, `CLAUDE.md`의
"최적화를 고려한 코드 작성" 원칙에 비춰보면 씬에 하나뿐인 로컬 캔버스를 찾는 데 매번 전체 탐색을
쓰는 것은 개선 여지가 있다(예: 찾은 뒤에는 캐싱하고 씬 전환/재접속 시에만 다시 탐색하는 식).
OS 커서와 3D 커서 전환 로직(캐릭터 표면 위=3D 커서/OS 커서 숨김, 그 외=OS 커서)은 꼼꼼하게
`OnDisable()`에서도 `Cursor.visible = true`로 복구해 씬 전환 시 커서가 숨겨진 채로 남는 사고를
막고 있다.

### 25.8 `PlayerColorVoteIndicator.cs` — 투표색 빌보드

캐릭터 머리 위에 자신이 현재 투표 중인 색을 스프라이트로 표시. `LateUpdate()`에서
`transform.forward = Camera.main.transform.forward`로 빌보드 처리하는데, 이는 "카메라를 향하게"가
아니라 **"카메라가 보는 방향과 같은 방향을 보게"**(카메라와 평행)로, 일반적인 빌보드
(`LookAt(Camera.main.transform)`, 즉 카메라를 정면으로 마주보게)와는 미묘하게 다르다.
원근 카메라를 정면으로 오래 볼 때는 거의 차이가 없지만, 카메라가 스프라이트를 비스듬히
내려다보는 각도(이 프로젝트의 3인칭 추적 카메라, §27의 `m_DefaultRotV=25°`)에서는 스프라이트
평면이 카메라 시선과 정확히 수직이 되지 않아 살짝 찌그러져 보일 수 있다 — 크래시는 아니지만
시각적으로 완벽한 빌보드는 아니다.

### 25.9 `RoomLifecycleWatcher.cs`

이번 세션 앞부분(`RoomItemPlan.md` §0.2/§7)에서 이미 상세히 다루고 직접 수정한 파일이라 여기서는
요약만 한다: 술래 퇴장/인원 부족 시 방을 나가 `LobbyScene`으로(비정상 종료), 정상 종료(20초 타이머,
`GameEndTime` 프로퍼티) 시에는 방을 유지한 채 `GameLobbyScene`으로 돌아간다(마스터만 트리거). 현재
버전은 이 세션에서 수정한 대로 정상 동작한다.

### 25.10 이 도메인의 실제 배선(wiring) 현황

`ColorTag/`의 10개 스크립트가 실제로 GameObject에 붙어 동작하는 곳은 현재 **`PlayerTestScene`
뿐이다**(이번 세션 초반 씬 계층 조회로 확인: `hide_or_seek_player`에 `PlayerPaintCanvas`/
`PlayerColorVoteIndicator`/`PlayerColorDisplay`가, `ColorTagManagers`에 `ColorSelectionManager`/
`RoomLifecycleWatcher`/`BrushCursorController`가 붙어 있음). 정작 실제 매치가 도달하는
`GameScene.unity`는 이번 세션에서 직접 열어 확인한 대로 카메라+라이트만 있는 빈 템플릿이다
(`RoomItemPlan.md`의 범위 밖으로 이미 명시됨). 즉 이 도메인은 **코드/에셋 레벨로는 완성도가
높지만, 실제 매칭 플로우에 아직 배선되지 않았다**는 것이 §25.2의 통합 공백과 함께 이 도메인의
가장 중요한 현재 상태 요약이다.

## 26. `Unit/` 도메인 요약 (상세는 `PlayerControllPlan.md` §1~§16)

이미 별도 계획 문서에서 라인 단위로 조사·리팩토링·버그 2건 수정(Walk/SneakWalk 루프 설정 누락,
Dodge Root Motion 겹침)·Dodge 애니메이션 재타이밍까지 끝난 상태라 이번 조사에서 새로 발견한 것은
없다. 핵심만 재확인:

- `HideOrSeekPlayer`(오케스트레이터, `MonoBehaviourPunCallbacks`+`IPunObservable`) +
  `PlayerGroundDetector`/`PlayerAnimationDriver`/`PlayerNetworkSync`(순수 C#) +
  `PlayerMoveState`(enum) 5개 파일로 책임이 분리되어 있다.
- 전투/HP 없음, 이동·점프·회피·애니메이션·네트워크 동기화만 담당(§2.2 범위 확정).
- `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab`로 이미 정식 프리팹 승격 완료
  (`PlayerControllPlan.md` §13.9/§13.11) — `Resources` 하위에 있어 `PhotonNetwork.Instantiate("Hide
  OrSeekPlayer", ...)`로 이름으로 스폰 가능한 상태다. **그런데 이 이름으로 스폰을 시도하는 코드가
  프로젝트 어디에도 없다**(§30에서 재확인) — `Assets/Scripts/GameManager.cs`는 여전히 존재하지 않는
  `"HeroPrefab"`을 스폰하려 한다(§33 이하 재조사 참고). §25.2의 "미니게임 시작 트리거 없음"과 같은
  종류의, "만들어는 놨는데 아직 아무도 실제로 부르지 않는" 통합 공백이다.
- `Awake()`가 `Camera.main`에서 `Camera_Ctrl`을 찾아 스스로 연결하는 방식이라(§27과 연동)
  씬을 가리지 않고 재사용 가능(`GameLobbyScene`/`GameScene` 어디든 `Main Camera`에 `Camera_Ctrl`만
  있으면 자동 연결).

## 27. `Camera/Camera_Ctrl.cs` 상세 (신규 조사)

`PlayerControllPlan.md` §13.4/§13.5가 "설계 스니펫"으로 제시했던 코드와 실제 파일을 줄 단위로
대조했다.

- 구조는 계획대로 정확히 구현되어 있다: 마우스 휠 줌 관련 필드/로직(`zoomSpeed`, `minDist`,
  `maxDist`, `m_TargetDistance`, `m_CurDistance`, `zoomSmoothTime`, `zoomVelocity`)이 전부 제거됐고,
  `m_DefaultDist` 하나만 남아 카메라 거리를 고정한다. `InitCamera(GameObject)`는 `HideOrSeekPlayer.
  Awake()`가 호출하는 유일한 진입점이라 씬에 미리 `m_Player`를 연결해둘 필요가 없다(§26 참고).
- **수치 불일치 발견**: 계획 문서(§13.4)의 설계 스니펫은 `m_DefaultDist = 5.2f`라고 명시했지만,
  실제 구현된 파일은 `[SerializeField] float m_DefaultDist = 3.2f;`다 — 값이 다르고, 계획에는 없던
  `[SerializeField]`도 추가되어 있다(인스펙터에서 개별 조정 가능하게 한 것으로 보임, 합리적인
  개선). 3.2가 실제로 플레이한 뒤 더 나은 카메라 거리로 판단해 의도적으로 바꾼 것인지, 단순
  오기인지는 코드만으로는 알 수 없다 — 계획 문서와 실제 값이 다르다는 사실만 기록해둔다.
- `Update()`가 아니라 `LateUpdate()`에서 카메라를 갱신한다(플레이어의 `Move()`가 `Update()`에서
  먼저 실행된 뒤 카메라가 뒤따르므로 한 프레임 지연/떨림이 없는 올바른 순서).
- 회전은 우클릭 드래그(`Input.GetMouseButton(1)`)에서만 마우스 X/Y를 반영하고,
  `Quaternion.Slerp`로 `rotationSmoothTime`(0.08초) 기준 프레임 독립적으로 보간한다. 수직 각도는
  `ClampAngle`로 `-7°~80°`로 제한된다.
- `m_Player`가 `null`이면 `Start()`/`LateUpdate()` 둘 다 조용히 아무 것도 하지 않는다 — 방어적이라
  안전하지만, `InitCamera()`가 한 번도 호출되지 않은 채(예: `HideOrSeekPlayer`가 소유자가 아니거나
  `Camera.main`을 못 찾은 경우) 카메라가 원점에 가만히 있는 상태가 되어도 에러/경고가 전혀 없다 —
  디버깅 시 "왜 카메라가 안 움직이지"를 원인 로그 없이 조사해야 할 수 있다.

## 28. `Lobby/` 도메인 요약 (상세는 `RoomItemPlan.md` §1~§7)

이번 세션에서 직접 설계·구현·검증까지 마친 도메인이라 재조사에서 새로 발견한 것은 없다. 핵심만
재확인: `LobbyController`(로비 접속/방 목록/생성/입장) + `RoomListItem`(방 목록 항목) +
`GameLobbyController`(대기방 인원/방장 전용 시작 버튼) + `PlayerListItem`(대기방 플레이어 목록
항목) 4개 파일. 전부 `MonoBehaviourPunCallbacks`의 `OnEnable`/`OnDisable`에서
`AddCallbackTarget`/`RemoveCallbackTarget`을 빠짐없이 호출하는 이 프로젝트의 표준 패턴을 따른다.
`GameLobbyController.Start()`에는 이번 세션에서 발견해 고친 방어 코드(방에 들어오지 않은 채 씬이
단독 실행되는 경우의 `NullReferenceException` 가드)가 반영되어 있다(`RoomItemPlan.md` §7.3 참고).

**`ColorTag`/`Unit`과의 연결 공백**: `GameLobbyController.OnStartGameButtonClicked()`는
`PhotonNetwork.LoadLevel("GameScene")`만 호출하고, 그 씬에서 캐릭터를 스폰하거나
색상 선택을 시작하는 어떤 코드도 호출하지 않는다 — §25.2/§26에서 지적한 두 공백(미니게임
시작 트리거 없음, `HideOrSeekPlayer` 스폰 코드 없음)이 정확히 이 지점에서 이어져야 할 다음
작업이다.

## 29. `Dev/OfflineModeBootstrap.cs`

프로덕션 플로우와 무관한 개발용 진입점. `Awake()`에서 무조건 `PhotonNetwork.OfflineMode = true`로
설정하고, `autoStartColorSelection`이 켜져 있으면 `Start()`에서 오프라인 룸을 만들고
`ColorSelectionManager.StartColorSelection()`을 직접 호출한다(§25.2에서 확인했듯, 프로젝트 전체에서
이 메서드를 호출하는 유일한 지점). `CLAUDE.md`의 "개발 도구 → `Scripts/Dev/`" 규칙을 정확히 따르고
있다. 씬에 `ColorSelectionManager`가 없으면 `Debug.LogWarning`으로 안전하게 알리고 종료한다(이
파일 안에서는 드물게 존재하는 null 가드/로깅 사례).

## 30. 크로스 도메인 의존성 맵

```
Lobby/*            (독립적 — ColorTag/Unit/Camera 어느 것도 참조하지 않음)

Unit/HideOrSeekPlayer.cs
└── Camera/Camera_Ctrl.cs 참조 (Awake()에서 Camera.main.GetComponent<Camera_Ctrl>())

ColorTag/PlayerColorDisplay.cs
└── [RequireComponent] ColorTag/PlayerPaintCanvas.cs (같은 도메인 내부 결합)

ColorTag/BrushCursorController.cs
└── ColorTag/PlayerPaintCanvas.cs, ColorPaletteSO, BrushSettingsSO 참조 (같은 도메인)

Dev/OfflineModeBootstrap.cs
└── ColorTag/ColorSelectionManager.cs 참조 (StartColorSelection 호출)

(모든 도메인) → Assets/Scripts/GameManager.cs 참조 없음 [양방향]
(모든 도메인) → Assets/02. Scripts/Unit/HideOrSeekPlayer.prefab을 실제로 스폰하는 코드 없음
(모든 도메인) → Assets/02. Scripts/ColorTag/ColorSelectionManager.StartColorSelection()을
                실제 매칭 흐름(Lobby)에서 호출하는 코드 없음
```

`Assets/Scripts/`(구 트리, GameManager.cs만 남음)와 `Assets/02. Scripts/`(신규 트리) 사이에는
**코드 레벨의 참조가 전혀 없다** — 두 트리는 지금 완전히 분리된 섬처럼 존재한다. 이론적으로
연결되어야 할 지점(로비에서 방을 나갈 때의 씬 이름, 캐릭터 스폰)은 모두 `GameManager.cs` 쪽이
낡은 값(`"PhotonLobby"`, `"HeroPrefab"`, `"HeroSpawnPos"`)을 참조하고 있어 실제로는 연결되지 않은
상태다(§33 이하에서 상세 재조사).

## 31. 확인된 버그·스멜 종합 (`Assets/02. Scripts/`)

1. **[통합 공백, 가장 중요] 실제 매칭 흐름에 색상 선택 시작 트리거가 없음** — `StartColorSelection()`을
   호출하는 코드가 `OfflineModeBootstrap`(개발용) 하나뿐(§25.2).
2. **[통합 공백] 실제 매칭 흐름에 `HideOrSeekPlayer` 스폰 코드가 없음** — 프리팹은 완성돼 있지만
   아무도 `PhotonNetwork.Instantiate`로 부르지 않음(§26, §30).
3. **[통합 공백] `GameScene.unity`가 아직 빈 템플릿** — `ColorTag/` 10개 스크립트가 실제로 동작하는
   곳은 `PlayerTestScene` 하나뿐(§25.10).
4. `ColorSelectionManager.ResetAllVotes()`가 마스터 자신의 투표만 리셋하고 다른 플레이어의
   투표는 리셋하지 않음 — 의도 확인 필요(§25.2).
5. `Camera_Ctrl.m_DefaultDist`가 계획 문서(5.2f)와 실제 구현(3.2f)이 서로 다름 — 의도적 튜닝인지
   오기인지 확인 필요(§27).
6. `PlayerColorVoteIndicator`의 빌보드가 `LookAt` 방식이 아니라 `forward` 정렬 방식이라, 카메라가
   비스듬히 내려다보는 각도에서 스프라이트가 살짝 찌그러질 수 있음(§25.8).
7. `BrushCursorController`가 로컬 캔버스를 찾을 때 매 프레임 `FindObjectsByType`로 씬 전체를
   순회함 — 결과를 캐싱하지 않는 최적화 여지(§25.7, `CLAUDE.md` 최적화 원칙 관련).
8. 이 폴더 전반에 걸쳐 반복되는 패턴: `[SerializeField]` 참조 다수가 null 가드/`[RequireComponent]`
   없이 즉시 역참조됨(`ColorSwatchButton.manager`, `PlayerPaintCanvas`의 다수 필드 등) —
   `Assets/Scripts/` 시절부터(§8.7) 이어지는 프로젝트 전반의 습관.

## 32. 다음 단계 제안

1. `GameLobbyController.OnStartGameButtonClicked()`(또는 `GameScene` 진입 시점의 별도 매니저)에서
   `HideOrSeekPlayer.prefab`을 스폰하고 `ColorSelectionManager.StartColorSelection()`을 호출하는
   실제 연결 코드를 작성한다 — §31-1/2/3을 한 번에 해소하는 핵심 작업.
2. `GameScene.unity`에 `PlayerTestScene`에 이미 검증된 구성(`ColorTagManagers`, `GameUICanvas` 등)을
   옮겨 채운다.
3. `ColorSelectionManager.ResetAllVotes()`의 의도를 확인하고, 전원 리셋이 맞다면
   `PhotonNetwork.PlayerList`를 순회하도록 고친다(§31-4).
4. `Camera_Ctrl.m_DefaultDist` 값(3.2 vs 5.2)의 의도를 확인해 계획 문서 쪽을 최신값으로
   맞추거나, 실제로 오기였다면 되돌린다(§31-5).

---

# 조사 보고서 재조사: Assets/Scripts/GameManager.cs (2026-08-14)

(위 §11~§22가 2026-08-13에 작성된 최초 조사다. 그 사이 `Hero_Ctrl.cs`/`AnimState.cs`/
`Monster_Ctrl.cs`가 삭제되고, `Assets/02. Scripts/` 전체가 새로 만들어지는 등 프로젝트가 크게
바뀌었으므로, 사용자 요청에 따라 `GameManager.cs`를 처음부터 다시 정독하고 재조사했다. 파일 자체는
§11~§22 이후 **한 글자도 바뀌지 않았다**(git 이력·바이트 단위 대조 없이도, 385~13번 줄까지 §11의
서술과 정확히 일치함을 이번에 재확인) — 달라진 것은 파일이 아니라 **파일을 둘러싼 프로젝트의
나머지 부분**이다. 이 섹션은 "무엇이 이제는 맞고, 무엇이 여전히 틀렸는지"를 다시 정리한다.)

## 33. 재조사 결론 요약

| §20의 기존 지적 | 2026-08-13 상태 | **2026-08-14 재확인 결과** |
|---|---|---|
| ① CP949 인코딩 | 심각 | **그대로 심각.** 이번에 파일을 다시 읽어도 `ä��` 류의 깨진 바이트가 동일하게 나타난다(예: 12행 `MAX_CHAT` 주석, 81행 "방 나감" 메시지 리터럴). 손대지 않았으므로 당연하지만, 여전히 최우선 수정 대상이다. |
| ② Build Settings 씬 0개 | 심각 | **"완전히 비어있다"는 문제 자체는 해소됨.** 다만 정확히 어떤 씬이 몇 번에 등록돼 있는지는 이 보고서 작성 도중에도 실시간으로 바뀌는 것을 직접 목격했다 — §34에서 스냅샷과 함께 신뢰도 문제를 별도로 기록. |
| ③ `"PhotonLobby"` 씬 없음 | 버그 | **여전히 버그, 그러나 원인이 바뀜.** 이제 Build Settings는 채워져 있지만 등록된 4개 씬 중 `"PhotonLobby"`라는 이름은 없다(가장 가까운 후보는 `"LobbyScene"`) — 즉 이제는 "Build Settings가 비어서" 실패하는 게 아니라 "이름이 틀려서" 실패한다. 고치는 방법은 여전히 한 줄(`"PhotonLobby"` → `"LobbyScene"`)이다. |
| ④ `"HeroSpawnPos"` 없음 | 버그 | **여전히 버그.** 프로젝트 전체 재검색(§35)으로도 이 이름의 오브젝트는 `GameManager.cs` 자기 자신 말고 어디에도 없다. |
| ⑤ `"HeroPrefab"` 없음 | 버그 | **여전히 버그, 그리고 이제 "정답"이 따로 존재한다.** `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab`이 이번 세션 이전에 이미 정식 프리팹으로 승격되어 있다(`PlayerControllPlan.md` §13.9/§13.11) — `CreatePlayer()`가 스폰해야 할 프리팹은 사실상 이것인데, 코드는 여전히 존재하지 않는 `"HeroPrefab"`이라는 이름을 참조한다. |
| ⑥ `CreatePlayer()`가 `Awake()`에서 레이스 컨디션 없이 호출됨 | 버그 | **여전히 버그.** 변경 없음. |
| ⑦ `is_Conversating`이 Enter 키로만 해제됨 | UX 버그 | **성격이 바뀜 — 더 심각한 문제로 대체됨.** §36 참고: 이 프로퍼티를 **읽는 코드 자체가 이제 프로젝트에 없다.** |

## 34. Build Settings 재확인 (§18-1 갱신) — ⚠️ 조사 중 실시간으로 상태가 바뀜, 아래는 스냅샷일 뿐

이 섹션을 작성하는 도중 Unity 에디터에서 Build Settings를 다시 조회했더니, 앞서 확인했던 값과
**다른 결과**가 나왔다:

```
0: Assets/Scenes/PhotonLobby.unity       (enabled, guid 5cdf08a4...)
1: Assets/Scenes/ColorSelectScene.unity  (enabled, guid ae225822...)
2: Assets/Scenes/GameScene.unity         (enabled, guid 9fc0d401...)
3: Assets/Scenes/SampleScene.unity       (enabled, guid b551b0be...)
```

그런데 이 프로젝트의 `Assets/Scenes/` 폴더를 직접 나열해보면 여전히 `SampleScene`/`PlayerTestScene`/
`LobbyScene`/`GameLobbyScene`/`GameScene` 5개 `.unity` 파일만 존재한다 — **`PhotonLobby.unity`나
`ColorSelectScene.unity`라는 파일 자체가 디스크에 없다.** 게다가 에디터가 현재 열어 둔
"활성 씬"은 `Assets/Scenes/GameScene.unity`(guid `11206140...`)인데, 이는 방금 위 Build Settings
목록에 나온 `GameScene.unity`의 guid(`9fc0d401...`)와도 **서로 다르다.** 같은 세션 중 `git status`로도
`Assets/Scenes/GameScene.unity` 자체와 `Assets/02. Scripts/ColorTag/Shaders/`의 머티리얼 2개가
이 보고서 작성 도중 새로 수정됨(M)으로 표시됐고, 이 세션에서 만든 적 없는 `Chatting.png` 파일도
새로 나타났다.

**해석**: 이 보고서를 작성하는 동안 **사용자가 Unity 에디터에서 직접 `GameScene`을 살아있는
상태로 편집하고 있었던 것으로 보인다**(새 씬 생성/이름변경, 셰이더 머티리얼 수정, 채팅 UI용
이미지 추가 등). 즉 위 4줄짜리 Build Settings 스냅샷은 "지금 이 순간" 하나를 찍은 것일 뿐이며,
이 문서가 읽히는 시점에는 이미 또 달라져 있을 가능성이 크다 — **이 보고서의 다른 어떤 섹션보다도
신뢰도가 낮으니, 실제 작업 시에는 이 표를 참고하지 말고 그때그때 `File > Build Settings`를 직접
열어 확인할 것을 권장한다.** §18-1에서 지적했던 "`m_Scenes: []`"(완전히 비어있음) 문제 자체는
이미 여러 세션 전에 해소됐다는 사실만은 안정적으로 유지되고 있다. `SceneManager.LoadScene
("PhotonLobby")`(115~120행) 호출은 — 흥미롭게도 지금 이 순간의 스냅샷 기준으로는 `PhotonLobby.unity`가
Build Settings 0번에 **등록되어 있는 것처럼 보이지만**, 그 경로에 실제 파일이 없으므로 여전히
런타임에는 실패할 것으로 추정된다(직접 실행해 재현하지는 않았다 — 사용자가 실시간으로 작업
중인 상태를 건드리고 싶지 않아 보류함).

## 35. 스폰 의존성 재확인 (§19 갱신)

`GameObject.Find("HeroSpawnPos")`와 `PhotonNetwork.Instantiate("HeroPrefab", ...)`가 요구하는 두
이름을 프로젝트 전체(씬 파일 + `Resources` 하위 에셋)에서 다시 검색했다 — **정확히 1건, 즉
`GameManager.cs` 자기 자신의 코드에서만 등장**하고 그 외에는 어디에도 없다. §19의 결론(스폰이
조용히 스킵됨)은 그대로 유효하다.

**새로 확인된 사실**: 이제 이 프로젝트에는 스폰 가능한 "진짜" 캐릭터 프리팹이 존재한다 —
`Assets/04. Prefabs/Resources/HideOrSeekPitle...`가 아니라 정확히
`Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab`이며, `Resources` 폴더 아래 있으므로
`PhotonNetwork.Instantiate("HideOrSeekPlayer", ...)`처럼 이름으로 스폰 가능한 상태다
(`PlayerControllPlan.md` §13.9). 즉 `CreatePlayer()`를 고치는 작업은 이제 "프리팹을 새로 만드는"
작업이 아니라 "이미 있는 프리팹의 정확한 이름으로 문자열만 바꾸고, 스폰 위치를 실제 씬 오브젝트에
맞추는" 작업으로 범위가 줄어들어 있다 — 다만 이 작업은 `RoomItemPlan.md`/`PlayerControllPlan.md`
양쪽 모두에서 명시적으로 "범위 밖"으로 분류해 두었으므로(§26의 `Lobby`↔`Unit` 통합 공백과 동일),
이번 조사에서도 손대지 않았다.

## 36. `Is_Conversating` — 이제는 아무도 읽지 않는 완전한 고아 프로퍼티

§17/§20-7에서는 "Enter 키로만 닫혀서 소프트락 위험이 있다"는 **UX 버그**로 지적했다. 이번에
`grep`으로 프로젝트 전체에서 `Is_Conversating`/`is_Conversating`을 다시 찾아본 결과, **선언된
바로 그 줄(`GameManager.cs:23`) 외에는 어디에도 등장하지 않는다.**

이유는 명확하다: 원래 이 프로퍼티를 읽던 유일한 소비자는 `Hero_Ctrl.CheckMovementInput()`(§14에서
교차 확인했던 그 지점)이었는데, `Hero_Ctrl.cs`가 `PlayerControllPlan.md` §13.3-2/§13.7에서 완전히
삭제됐다. 그리고 그 자리를 대신하도록 설계됐던 `HideOrSeekPlayer.IsMovementLocked`(`PlayerControllPlan.md`
§9, "사망/대화/컷신 등 상위 시스템이 이 프로퍼티만 세팅하면 이동이 잠긴다")도, 이번에 다시
검색해보니 **`get; set;`로 선언되고 `Update()` 최상단에서 읽히기만 할 뿐, 어디서도
`true`로 세팅하는 코드가 없다**(`HideOrSeekPlayer.cs:41,66` 외 참조 0건).

**결론**: 지금 이 프로젝트에는
- 채팅 중임을 나타내는 값을 들고 있는 `GameManager.is_Conversating`이 있지만 **아무도 읽지 않고**,
- 이동을 잠글 수 있는 `HideOrSeekPlayer.IsMovementLocked`가 있지만 **아무도 쓰지(set) 않는다.**

두 절반이 서로 다른 클래스에 따로 존재하고, 그 사이를 이어주는 코드가 없다 — "채팅 중에는
캐릭터가 못 움직인다"는 원래 `Hero_Ctrl` 시절의 기능이 리팩토링 과정에서 **완전히 끊어진
채로 방치되어 있다.** 이전 조사(§17)가 지적했던 "닫는 방법이 Enter 하나뿐이라 소프트락 위험"이라는
문제보다 근본적으로 더 심각한 상태로 바뀐 셈이다(소프트락은 "기능이 있는데 가끔 오작동"하는
문제였지만, 지금은 "기능 자체가 아예 작동하지 않는" 상태). `GameManager`(채팅 UI)와
`HideOrSeekPlayer`(이동)를 다시 연결하는 코드 — 예를 들어 `GameManager.Update()`의 `bEnter` 토글
지점에서 로컬 `HideOrSeekPlayer` 인스턴스를 찾아 `IsMovementLocked`를 세팅해주는 한 줄 — 가
없으면, 이 프로젝트에는 현재 "채팅 중 이동 잠금" 기능이 사실상 존재하지 않는다.

## 37. 인코딩 재확인 (§12 갱신)

`iconv -f UTF-8 -t UTF-8` strict 디코딩을 다시 시도한 결과 이번에도 동일하게 실패했고(12행 부근),
`iconv -f CP949 -t UTF-8`로는 처음부터 끝까지 정상 복원된다 — §12의 진단이 여전히 정확하다. 파일
자체가 바뀌지 않았으므로 당연한 결과지만, "혹시 프로젝트의 다른 부분이 바뀌면서 이 파일도 같이
재저장됐을까"라는 가능성을 배제하기 위해 다시 직접 확인했다.

## 38. 재조사 총괄 — 남은 작업 우선순위

이번 재조사로 갱신된 우선순위(중복 제거, §20 대비 변경분 반영):

1. **[신규 최우선] `GameManager`의 채팅-이동잠금 연결 복구**(§36) — 기능이 아예 끊어져 있다는 게
   이번에 새로 확인된 사실이라, CP949 인코딩 다음으로 우선순위가 높다고 판단된다.
2. CP949 → UTF-8(BOM) 재저장(§37, 변경 없음, 여전히 최우선급).
3. `OnLeftRoom()`의 `"PhotonLobby"` → `"LobbyScene"`(§34에서 확인한 대로 이제 Build Settings 문제는
   해소됐으니 이름만 고치면 됨).
4. `CreatePlayer()`를 `"HeroPrefab"`/`"HeroSpawnPos"` 대신 실제로 존재하는
   `"HideOrSeekPlayer"`(§35) 프리팹과 실제 씬의 스폰 포인트를 쓰도록 갱신 — 다만 이 작업은
   `RoomItemPlan.md §31-1/2`와 정확히 같은 통합 공백의 반대쪽 절반이므로, 두 문서의 "다음 단계"를
   함께 보고 한 번에 처리하는 것이 합리적이다.
5. `CreatePlayer()`를 `Awake()`가 아니라 `OnJoinedRoom()`으로 옮겨 레이스 컨디션 제거(§20-5, 변경
   없음).
6. `LogMsg` 색상 치환 로직과 주석 정리(§20-6, 변경 없음).
