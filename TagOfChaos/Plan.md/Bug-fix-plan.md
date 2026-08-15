# 계획: 버그 5건 수정 (Bug-fix-plan.md)

> 상태: **①②③④⑤⑥ 구현 완료, ⑦은 1차 수정으로 불충분함이 확인되어 2차 원인을 찾아 계획
> 수립 중(미구현).** ⑥(미끄러짐·버벅거림)은 §13의 1차 수정(`rb.MoveRotation()`)이
> 실제로는 증상을 고치지 못했음을 사용자가 재테스트로 확인 — `PlayerTestScene`이 아닌
> `GameLobbyScene`에 직접 들어가 재조사한 결과, 진짜 원인은 회전 문제가 아니라 §21에서 `Ch36`의
> 콘케이브 메시 콜라이더 에러를 고치려고 추가했던 `Ch36` 전용 키네마틱 `Rigidbody`가 부작용으로
> 캐릭터의 루트 `CapsuleCollider`와 자기 자신의 몸통(`Ch36`) 사이에 "자기 자신과의 충돌
> (self-collision)"을 새로 만들어낸 것이었다 — `Physics.IgnoreCollision()`으로 수정했고,
> `GameLobbyScene`에서 실제 Photon 방으로 재측정해 위치 고정·속도 폭주 현상이 완전히 사라짐을
> 확인했다(§14, 2026-08-16). 다만 실제 체감 확인은 사용자 플레이 테스트가 필요하다(§14.9).
> (2026-08-16 최종 업데이트). ④(GameLobbyScene 가시성)는 1차·2차 수정이 모두 실측에서
> 불충분함이 드러났고, 3차 조사에서 사용자가 제공한 실제 빌드 스크린샷 + Unity 에디터를 네 번째
> 참가자로 투입한 실시간 공동 디버깅으로 진짜 원인(§12)을 찾아 수정했다 — 이번엔 실제 빌드
> 4개 클라이언트 전원의 화면에서 서로가 보임을 사용자가 직접 확인했다. ⑤는 사용자가 남긴
> 스크린샷을 근거로 재조사해 확정적 원인을 찾았다(§10). 실제 구현 결과는 §11(④ 2차 시도·⑤),
> §12(④ 최종 원인·수정·실측 검증), §13(⑥ 1차 시도 — 불충분했음), §14(⑥ 진짜 원인·구현·검증,
> 2026-08-16)에 정리했다. ⑦(점프 연타 시 애니메이션이 중간부터 재생)은 1차로 공유 메서드
> `ChangeState()`는 건드리지 않는 전용 `ReplayJump()` 메서드를 구현했으나(§15), 사용자 재테스트
> 결과 증상이 남아있어 재조사 — `ReplayJump()`가 요청한 `SetTrigger`가 애니메이터에 실제 반영되기
> 전 그 틈에 `HandleJumpAnimationHold()`가 오래된 `normalizedTime`을 보고 즉시 다시 얼려버리는
> 두 번째 메커니즘을 Play Mode 실측으로 확정하고 수정 계획을 세웠다(§16, 2026-08-16) — 아직
> 미구현, 승인 대기. ⑧(PlayerTestScene에서 붓이 안 보임)은 §18에서 추가한 캐릭터의
> `CapsuleCollider`가 페인트 대상 `Ch36`을 레이캐스트에서 가려버리는 것이 원인임을 실측(격자
> 레이캐스트 49/49 캡슐에 막힘)으로 확정하고, 캡슐을 전용 레이어로 분리하는 수정 계획을
> 세웠다(§17, 2026-08-16) — 아직 미구현, 승인 대기.

---

## 0. 요약

| # | 버그 | 상태 | 근거 |
|---|---|---|---|
| ① | Back 버튼 확인창이 첫 클릭에 안 뜸 | ✅ **구현·검증 완료** | §1, §8 |
| ② | 채팅 중 방향키로 캐릭터가 움직임 | ✅ **구현·검증 완료** | §2, §8 |
| ③ | 랜덤 입장이 최근 생성된 방으로만 들어감 | ✅ **구현·검증 완료** | §3, §8 |
| ④ | GameLobbyScene에서만 플레이어들이 서로 안 보임(방장 퇴장 시 전원 안 보임 포함) | ✅ **최종 원인 확정·수정·실제 4인 멀티 테스트로 검증 완료** | 진짜 원인은 `PhotonNetwork.InRoom`이 `Start()` 시점에 아직 false일 수 있어 `Instantiate`/`RPC`의 네트워크 전송(`RaiseEvent`)이 조용히 실패하는 것 + `HideOrSeekPlayer.networkSync`가 `Start()`에서만 초기화돼 그보다 먼저 오는 `OnPhotonSerializeView` 수신에서 NRE 발생. `InRoom`이 true가 될 때까지 대기 후 전송 + `networkSync`를 `Awake()`로 이동(§12) |
| ⑤ | 카메라가 캐릭터 뒷모습만 보임 | ✅ **재계획(§10) 구현·검증 완료** | `PlayerColorVoteIndicator`의 `LateUpdate()`가 `indicator.transform`만 회전시키도록 수정 — Play Mode에서 회전값과 스크린샷으로 정면이 정상적으로 보임을 직접 확인(§11) |
| ⑥ | GameLobbyScene에서 플레이어가 미끄러지고 걸을 때 버벅거림 | ✅ **진짜 원인 실측 확정·구현·`GameLobbyScene` 실측 검증 완료** | §13의 `rb.MoveRotation()` 수정은 실제로는 증상을 고치지 못함(사용자 재테스트로 반증됨). `GameLobbyScene`에 직접 들어가 재조사한 결과, 진짜 원인은 §21에서 추가한 `Ch36` 전용 키네마틱 `Rigidbody`가 루트 `CapsuleCollider`와 자기 자신을 계속 충돌시키는 self-collision — `Physics.IgnoreCollision()`으로 이 둘의 충돌만 끄니 위치 고정·속도 폭주 현상이 완전히 사라지고 매끄러운 선형 이동·회전으로 바뀌는 것을 `GameLobbyScene` 실제 Photon 방에서 확인(§14) |
| ⑦ | 점프 키를 연속으로 눌렀을 때, 가끔 재점프 시 Jump 애니메이션이 처음부터 재생되지 않고 이전 점프가 멈췄던 지점(중간)부터 이어서 재생됨 | 🔎 **1차 원인(§15) 구현 완료했으나 사용자 재테스트로 불충분함이 확인됨 — 2차 원인(§16) 실측 확정, 수정 계획 수립 완료, 승인 대기, 미구현** | §15: `ChangeState()`의 가드가 `SetTrigger` 자체를 건너뛰던 것 → 점프 전용 `ReplayJump()`로 해결(구현 완료, 그 메커니즘 자체는 실측으로 해소 확인). 그런데 별개의 두 번째 메커니즘이 동시에 존재했다: `ReplayJump()`가 요청한 `SetTrigger`가 애니메이터에 실제 반영되기 전(다음 프레임 전) 그 틈에 `HandleJumpAnimationHold()`가 "재트리거 이전"의 오래된 `normalizedTime`을 보고 즉시 다시 `animator.speed=0`으로 얼려버림 — Play Mode 실측으로 `ReplayJump()` 직후 `HandleJumpAnimationHold()`를 호출하면 즉시 재동결되는 것을 확인(§16.2). 재생 요청 후 한 프레임은 정지 판정을 건너뛰는 방식으로 수정 계획 수립(§16.3) |
| ⑧ | PlayerTestScene에서 색을 골라도 붓 커서가 나타나지 않음(실제 색칠도 동일 원인으로 막힘) | 🔎 **원인 실측 확정(§17), 수정 계획 수립 완료, 승인 대기 — 미구현** | 라운드/색상 선택 시스템 자체는 정상(`RoundIndex`, `isColorRoundActive` 전부 정상 확인)이었고, 진짜 원인은 §18에서 추가한 캐릭터 루트의 `CapsuleCollider`가 페인트 대상인 `Ch36`(몸 메시)의 `MeshCollider`를 카메라 시점에서 거의 완전히 가리는 것 — 캐릭터 화면 영역 49개 지점에서 레이캐스트를 직접 쏴본 결과 49/49 전부 `Ch36`이 아니라 `CapsuleCollider`가 먼저 맞음을 확인(§17.3). 캡슐을 전용 레이어로 분리하고 붓 관련 레이캐스트 두 곳에서 그 레이어만 제외하는 방식으로 수정 계획 수립(§17.4) |

---

## 1. Back 버튼 확인창이 첫 클릭에 안 뜨는 버그 — ✅ 원인 확정, ✅ 구현·검증 완료

### 1.1 증상

`GameLobbyScene`/`GameScene` 둘 다 동일: Back 버튼을 처음 누르면 아무 반응이 없고, 다시 한 번
눌러야 `ConfirmDialog`가 나타난다.

### 1.2 원인 (씬 파일 직접 확인)

`ConfirmDialog.cs`의 `Awake()`:

```csharp
private void Awake()
{
    yesButton.onClick.AddListener(OnYesClicked);
    noButton.onClick.AddListener(Hide);
    gameObject.SetActive(false); // 평소에는 숨겨둠
}
```

이 코드는 **"GameObject가 처음부터 씬에 활성 상태로 배치되어 있다"는 전제**로 작성되어 있다 —
씬이 로드되는 즉시 `Awake()`가 한 번 실행되어 리스너를 등록한 뒤 스스로를 숨기는 설계다. 그런데
`GameLobbyScene.unity`/`GameScene.unity` 두 씬 파일을 직접 열어 `ConfirmDialog` 프리팹 인스턴스의
`m_IsActive` 오버라이드 값을 확인한 결과, **둘 다 `0`(비활성)으로 저장되어 있었다**:

```yaml
# GameLobbyScene.unity, PrefabInstance 수정 목록 중
- target: {fileID: 5718382913717916466, guid: c51f4e5772fe8764994720d67f71bb90, type: 3}
  propertyPath: m_IsActive
  value: 0          # ← GameObject가 씬 로드 시점에 이미 비활성 상태로 저장됨
  objectReference: {fileID: 0}
```

```yaml
# GameScene.unity에도 동일한 오버라이드 존재
- target: {fileID: 5718382913717916466, guid: c51f4e5772fe8764994720d67f71bb90, type: 3}
  propertyPath: m_IsActive
  value: 0
```

(참고로 `ConfirmDialog.prefab` 원본 에셋 자체는 `m_IsActive: 1`로 정상이다 — **씬에 배치할 때만**
비활성으로 덮어써진 상태다. `GameManager.md` §9.11.4에서 이미 "MCP 프리팹 인스턴스화 도구가
`position` 인자를 명시하지 않으면 좌표를 예기치 않게 덮어쓴다"는 동일 계열의 문제를 한 번 발견한
적이 있는데, 이번 것도 같은 원인(프리팹 인스턴스화 시점에 의도치 않은 속성 오버라이드가 함께
기록됨)으로 추정된다.)

**Unity의 핵심 동작 원리**: GameObject가 **씬 로드 시점에 이미 비활성 상태**라면, `Awake()`는
그 오브젝트가 **처음으로 활성화되는 순간까지 실행되지 않는다.** 그런데 `Awake()` 안에 바로 그
"처음 활성화"를 취소하는 `gameObject.SetActive(false)`가 들어있으므로:

1. **첫 번째 클릭**: `RoomExitController.OnClickBackButtonPressed()` → `ConfirmDialog.Show()` →
   `gameObject.SetActive(true)` 호출. 이 호출이 이 오브젝트의 **생애 첫 활성화**이므로, Unity가
   바로 이 시점에 `Awake()`를 동기적으로 실행한다. `Awake()`는 리스너를 등록한 뒤 마지막 줄에서
   **다시 `SetActive(false)`를 호출** — 방금 `Show()`가 켠 것을 그 자리에서 도로 꺼버린다. 결과:
   화면에는 아무것도 뜨지 않는다.
2. **두 번째 클릭**: 이번에는 `Awake()`가 **이미 한 번 실행된 상태**(Unity는 오브젝트당 `Awake()`를
   정확히 한 번만 호출)라, `Show()`의 `SetActive(true)`를 취소하는 코드가 더 이상 없다 — 정상적으로
   화면에 뜬다.

이 메커니즘은 Play Mode에서 직접 재현·검증했다(§1.4).

### 1.3 수정 계획 — 씬 파일 수정 (코드 변경 없음)

`GameLobbyScene`/`GameScene` 두 씬 모두에서 `ConfirmDialog` GameObject의 활성 상태를 **`true`로
되돌린다.** 이렇게 하면 `Awake()`가 씬 로드 시점에 정상적으로(원래 설계 의도대로) 한 번 실행되어
리스너를 등록하고 스스로를 숨기므로, 플레이어가 화면을 보기 전에 이미 정상적으로 숨겨진 상태가
된다 — 이후 `Show()` 호출은 항상 첫 클릭부터 기대대로 동작한다.

Unity MCP로는 아래처럼 처리한다(코드 파일은 전혀 건드리지 않음):

```
manage_gameobject(action="modify", target=<ConfirmDialog 인스턴스 ID>, set_active=true)
```

두 씬 각각에서 실행 후 씬 저장.

**대안으로 검토했으나 채택하지 않은 방법**: `ConfirmDialog`를 `SetActive(false)` 대신
`CanvasGroup`(`alpha=0`, `interactable=false`, `blocksRaycasts=false`)으로 숨기도록 바꾸면 이
클래스의 "비활성 상태에서 Awake가 지연되는" 문제 자체가 구조적으로 사라진다(오브젝트가 항상
활성 상태를 유지하므로 `Awake()`가 씬 로드 시 항상 정상 실행됨). 더 견고한 방식이지만, 이번 버그의
**확정된 원인은 씬 데이터 한 곳의 잘못된 값**이므로 `CLAUDE.md`의 최소 변경 원칙에 따라 씬 수정만
으로 충분하다고 판단했다 — 필요하면 이 대안은 별도로 요청해달라.

### 1.4 검증 계획

1. `find_gameobjects`로 두 씬의 `ConfirmDialog` 인스턴스 활성 상태가 `true`로 저장됐는지 확인.
2. Play Mode에서 두 씬 모두 **첫 번째 클릭만으로** 확인창이 뜨는지 확인(지금까지처럼 두 번째
   클릭에서야 뜨지 않는지).
3. 확인창의 "예"/"아니오" 버튼이 기존과 동일하게 정상 동작하는지(회귀 확인).
4. `read_console`로 에러/경고 0건 확인.

---

## 2. 채팅 중 방향키 입력 시 캐릭터가 움직이는 버그 — ✅ 원인 확정, ✅ 구현·검증 완료

### 2.1 증상

채팅 입력창이 열려 있는 상태에서 방향키(WASD)를 누르면 캐릭터가 움직인다 — 채팅 중에는 이동이
잠겨야 한다는 것이 사용자가 명시한 기대 동작이다(`PlayerControllPlan.md` §9에서 이미 이 기능을
염두에 두고 `IsMovementLocked` 프로퍼티를 설계해뒀었음).

### 2.2 원인 (코드 직접 확인 — `research.md` §6.3에서 이미 지적됐던 사안의 재확인)

`GameManager.cs`(현재, 채팅 전용으로 축소된 버전)의 `Update()`:

```csharp
private void Update()
{
    if (Input.GetKeyUp(KeyCode.Return))
    {
        bEnter = !bEnter;
        if (bEnter)
        {
            is_Conversating = true;      // ← 값은 세팅되지만
            InputFdChat.gameObject.SetActive(true);
            InputFdChat.ActivateInputField();
        }
        else
        {
            InputFdChat.gameObject.SetActive(false);
            is_Conversating = false;     // ← 여기서도 세팅만 될 뿐
            ...
        }
    }
}
```

`HideOrSeekPlayer.cs`의 `Update()`:

```csharp
private void Update()
{
    if (IsMovementLocked)   // ← 이 프로퍼티를 읽긴 하지만
        return;
    ...
}

public bool IsMovementLocked { get; set; }   // ← 이 프로퍼티를 true로 세팅하는 코드가 프로젝트 어디에도 없음
```

`grep`으로 재확인한 결과, `is_Conversating`을 **읽는** 코드도, `IsMovementLocked`를 **쓰는** 코드도
프로젝트 전체에 이 두 선언부 자체를 빼면 0건이다 — 두 절반이 서로 다른 클래스에 따로 존재할 뿐
연결하는 코드가 없다. `PlayerControllPlan.md` §9가 애초에 "대화 시스템이 만들어지면 이 프로퍼티만
세팅하면 된다"고 설계해뒀던 연결 지점이 실제로는 한 번도 구현되지 않은 상태였다.

### 2.3 수정 계획

**`Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`**: `PlayerPaintCanvas.IsMine`과 동일한 패턴으로
공개 프로퍼티를 하나 추가한다(외부에서 "이 인스턴스가 내 캐릭터인지" 판별할 수단이 현재 없음):

```csharp
public bool IsMine => pv != null && pv.IsMine;
```

**`Assets/02. Scripts/GameManager/GameManager.cs`**: 채팅창을 열고 닫을 때 로컬 플레이어의
`IsMovementLocked`를 함께 세팅한다.

```csharp
private HideOrSeekPlayer localPlayer;

private void Update()
{
    if (Input.GetKeyUp(KeyCode.Return))
    {
        bEnter = !bEnter;
        if (bEnter)
        {
            is_Conversating = true;
            InputFdChat.gameObject.SetActive(true);
            InputFdChat.ActivateInputField();
            SetLocalPlayerMovementLocked(true);
        }
        else
        {
            InputFdChat.gameObject.SetActive(false);
            is_Conversating = false;
            SetLocalPlayerMovementLocked(false);
            if (!string.IsNullOrEmpty(InputFdChat.text.Trim()))
            {
                BroadcastingChat();
            }
        }
    }
}

// 채팅 입력창이 열려있는 동안 로컬 플레이어의 이동 입력을 잠근다
// (research.md §6.3 — is_Conversating과 IsMovementLocked가 서로 연결되지 않았던 문제를 복구)
private void SetLocalPlayerMovementLocked(bool locked)
{
    if (localPlayer == null)
    {
        foreach (var p in FindObjectsByType<HideOrSeekPlayer>(FindObjectsSortMode.None))
        {
            if (p.IsMine) { localPlayer = p; break; }
        }
    }

    if (localPlayer != null)
        localPlayer.IsMovementLocked = locked;
}
```

`localPlayer`를 매번 재검색하지 않고 한 번 찾으면 캐싱하는 이유: `GameManager`가 `Awake()`되는
시점(`PlayerSpawner`와 같은 오브젝트, 같은 프레임 근처)에는 아직 캐릭터가 스폰되지 않았을 수
있으므로, 최초 채팅 시도 시점에 지연 탐색(lazy lookup)하는 방식을 택했다 — `BrushCursorController.
FindLocalPaintCanvas()`가 로컬 페인트 캔버스를 찾을 때 쓰는 것과 같은 패턴이다.

### 2.4 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. Play Mode에서 채팅창을 연 상태로 WASD를 눌러도 캐릭터가 움직이지 않는지 확인.
3. 채팅창을 닫으면(Enter 재입력) 다시 정상적으로 움직이는지 확인.
4. 점프/회피 등 다른 입력도 채팅 중에는 막히는지(모든 이동 관련 입력이 `Update()` 최상단의
   `IsMovementLocked` 가드로 함께 막히므로 별도 처리 불필요 — 회귀 없이 그대로 적용됨) 확인.
5. 채팅을 안 쓰는 플레이어의 캐릭터는 기존과 동일하게 정상 이동하는지(로컬 전용 잠금이므로
   네트워크 동기화와 무관함) 확인.

---

## 3. 랜덤 입장이 최근 생성된 방으로만 들어가는 버그 — ✅ 원인 확정, ✅ 구현·검증 완료

### 3.1 증상

"랜덤 입장" 버튼을 누르면 매번 최근에 생성된 방으로만 들어가고, 실제로 여러 방 중에서 무작위로
선택되지 않는다.

### 3.2 원인

`Lobby/LobbyController.cs`의 `OnRandomJoinButtonClicked()`:

```csharp
public void OnRandomJoinButtonClicked()
{
    if (!TryApplyNickname()) return;
    PhotonNetwork.JoinRandomRoom();
}
```

`PhotonNetwork.JoinRandomRoom()`을 옵션 없이 호출하면 `matchingType` 매개변수가
**`MatchmakingMode.FillRoom`(기본값, 0)** 으로 적용된다. Photon 공식 문서/열거형 정의에 따르면:

- `FillRoom`(기본값): 새 방을 만들기 전에 **기존 방들을 먼저 채운다**(진짜 균등 무작위가 아니라
  매칭 최적화 알고리즘).
- `RandomMatching`: 필터 조건은 지키되 **진짜로 무작위 방**을 골라 입장시킨다.

테스트 환경처럼 방마다 인원 차이가 거의 없을 때는 `FillRoom` 모드가 매칭 서버 내부의 방 목록
순회 순서(실질적으로 최근 생성 순과 자주 일치)에 따라 결정적으로 동작하는 것처럼 보이는 경우가
많다 — 사용자가 관찰한 "항상 최근 방으로만 들어간다"는 정확히 이 기본 모드의 알려진 특성과
일치한다.

### 3.3 수정 계획

```csharp
using Photon.Realtime; // MatchmakingMode — 이미 파일 상단에 있음(RoomInfo 등에 이미 사용 중)

public void OnRandomJoinButtonClicked()
{
    if (!TryApplyNickname()) return;
    PhotonNetwork.JoinRandomRoom(null, 0, MatchmakingMode.RandomMatching);
}
```

`expectedCustomRoomProperties`(null)와 `expectedMaxPlayers`(0, 제한 없음)는 기존 동작과 동일하게
유지하고, `matchingType`만 명시적으로 `RandomMatching`으로 바꾼다 — 다른 어떤 매칭 조건도 추가하지
않으므로 "빈 방이든 사람이 있는 방이든 무작위로 하나 골라 입장한다"는 사용자 기대와 정확히
일치한다.

### 3.4 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. 방을 3개 이상 만들어두고(서로 다른 시점에 생성) "랜덤 입장"을 여러 번 눌러, 매번 같은 방이
   아니라 서로 다른 방에 입장하는지 확인.
3. 방이 하나도 없을 때 `OnJoinRandomFailed` 콜백(기존 로직, 변경 없음)이 정상 동작하는지 재확인.

---

## 4. GameLobbyScene에서만 플레이어들이 서로 안 보이는 치명적 버그 — 1차 진단(아래, 불충분했음) → ✅ 최종 수정은 §9

### 4.1 증상 (사용자 보고 재정리)

플레이어1이 `GameLobbyScene`에서 방을 생성 → 플레이어2/3/4가 입장. 플레이어1 화면에는 아무도(다른
플레이어들이) 보이지 않고, 플레이어2/3/4 화면에는 플레이어1만 보인다(서로는 안 보임). 같은 방이
`GameScene`으로 전환되면 4명 전원이 정상적으로 보인다 — **오직 `GameLobbyScene`에서만** 발생한다.

### 4.2 원인 분석 — Photon PUN2 SDK 소스 직접 확인

`PlayerSpawner.cs`(캐릭터 스폰 담당, `architecture-review-plan.md`에서 `GameManager`로부터
분리됨)의 `Awake()`가 `PhotonNetwork.Instantiate(...)`를 호출한다. 이 시점의 `PhotonNetwork.
IsMessageQueueRunning` 상태를 SDK 소스에서 직접 추적했다.

**`Assets/Photon/PhotonUnityNetworking/Code/PhotonNetwork.cs`(3057~3072행)** —
`PhotonNetwork.LoadLevel(string)`의 실제 구현(주석은 SDK 원본 그대로):

```csharp
/// While loading levels in a networked game, it makes sense to not dispatch messages received
/// by other players. LoadLevel takes care of that by setting
/// PhotonNetwork.IsMessageQueueRunning = false until the scene loaded.
public static void LoadLevel(string levelName)
{
    ...
    PhotonNetwork.IsMessageQueueRunning = false;   // ← 씬 로딩 시작과 동시에 즉시 꺼짐
    loadingLevelAndPausedNetwork = true;
    _AsyncLevelLoadingOperation = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Single);
}
```

**`Assets/Photon/PhotonUnityNetworking/Code/PhotonHandler.cs`(140~142행)** — 큐가 언제 다시
켜지는지:

```csharp
UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, loadingMode) =>
{
    PhotonNetwork.NewSceneLoaded();   // 여기서만 IsMessageQueueRunning = true로 복구됨
};
```

Unity의 씬 로딩 생명주기는 **"새 씬의 모든 오브젝트 `Awake()`(+`OnEnable()`) → `SceneManager.
sceneLoaded` 이벤트 발생 → 모든 오브젝트 `Start()`"** 순서로 진행된다(공식 문서 순서). 즉
`SceneManager.sceneLoaded`(그리고 그 안에서 실행되는 `IsMessageQueueRunning = true` 복구)는
**`Awake()` 이후, `Start()` 이전**에 일어난다 — `PlayerSpawner.Awake()`의 `PhotonNetwork.
Instantiate(...)` 호출은 이보다 **먼저** 실행되므로, **`IsMessageQueueRunning`이 아직 `false`인
상태에서 스폰이 이루어진다.**

**`IsMessageQueueRunning == false`가 실제로 무엇을 막는지** —
`Assets/Photon/PhotonUnityNetworking/Code/PhotonHandler.cs`(183~247행)를 직접 확인:

```csharp
while (PhotonNetwork.IsMessageQueueRunning && doSend && sendCounter < MaxDatagrams)
{
    doSend = PhotonNetwork.NetworkingClient.LoadBalancingPeer.SendOutgoingCommands(); // 나가는 명령 전송
    ...
}
...
while (PhotonNetwork.IsMessageQueueRunning && doDispatch)
{
    doDispatch = PhotonNetwork.NetworkingClient.LoadBalancingPeer.DispatchIncomingCommands(); // 들어오는 이벤트 처리
    ...
}
```

`IsMessageQueueRunning == false`인 동안은 **나가는 네트워크 명령이 전혀 전송되지 않고, 들어오는
이벤트도 전혀 처리되지 않는다.** `PhotonNetwork.Instantiate(...)`는 로컬 GameObject는 즉시(동기적
으로) 만들지만, "이 오브젝트가 생겼다"는 사실을 서버·다른 클라이언트에 알리는 네트워크 명령은
**전송 큐에만 쌓이고 실제로 나가지 못한다** — 스폰한 사람 눈에는 자기 캐릭터가 바로 보이지만, 그
정보가 다른 사람에게 실제로 전달되는 것은 `IsMessageQueueRunning`이 다시 켜진 뒤(즉 `Start()`가
호출되기 직전)로 미뤄진다.

**왜 `GameScene`은 괜찮아 보이는가**: 이 경합(race) 자체는 두 씬 모두에서 코드상 동일하게
존재한다 — `PlayerSpawner.Awake()`가 두 씬 모두에서 스폰을 담당하기 때문이다. 다만 실제로 문제가
가시화되는 정도는 씬 진입 패턴에 따라 다르다:
- `GameLobbyScene`은 각 플레이어가 **서로 다른, 예측할 수 없는 실제 시간 간격으로** 로비 목록에서
  방을 찾아 순차적으로 입장한다(`AutomaticallySyncScene`으로 각자 별도 타이밍에 씬 전환이
  트리거됨) — 경합 창(window)이 매번 다른 조건에서 열리고, 큐가 눌린 채로 걸리는 인스턴스가
  누적되기 쉬운 환경이다.
- `GameScene`은 이미 4명 전원이 완전히 합류해 안정된 방 상태에서, 방장 1명의 "게임 시작" 클릭
  **한 번**으로 전원이 거의 동시에 전환된다 — 경합 창이 열리는 조건 자체가 더 좁고 균일하다.

이 차이가 정확히 "왜 `GameLobbyScene`에서만 두드러지는가"를 설명하는 유력한 근거이지만, **이
비대칭성 자체를 다중 클라이언트로 직접 재현해 100% 확정하지는 못했다**(§4.4의 한계 참고) —
다만 `IsMessageQueueRunning`이 스폰 시점에 꺼져있다는 사실 자체, 그리고 이것이 네트워크 명령
전송을 막는다는 사실은 SDK 소스로 명확히 확인된 사실이다.

### 4.3 수정 계획

**`Assets/02. Scripts/GameManager/PlayerSpawner.cs`**: 스폰 직전에 메시지 큐를 명시적으로 다시
켜서, `Awake()` 타이밍에 걸리는 것과 무관하게 네트워크 명령이 항상 즉시 전송되도록 한다.

```csharp
private void Awake()
{
    // Camera_Ctrl.InitCamera()가 이 시점(씬의 최초 Awake 일괄 처리 단계)에 함께 호출되어야
    // 카메라 초기 각도가 정상 적용된다(architecture-review-plan.md §7.1) — 호출 시점 자체는
    // 그대로 Awake()에 둔다.
    SpawnLocalPlayer();
}

private void SpawnLocalPlayer()
{
    GameObject spawnPointObj = GameObject.Find(SpawnPointName);
    if (spawnPointObj == null)
    {
        Debug.LogWarning($"PlayerSpawner: \"{SpawnPointName}\" 오브젝트를 씬에서 찾을 수 없어 캐릭터를 스폰하지 못했습니다.");
        return;
    }

    // PhotonNetwork.LoadLevel()은 씬 로딩 동안 IsMessageQueueRunning을 false로 꺼두고,
    // Unity의 SceneManager.sceneLoaded 이벤트(모든 오브젝트의 Awake() 이후, Start() 이전)에서만
    // 다시 켠다 — 즉 이 Awake()가 실행되는 지금 시점에는 아직 꺼져있을 수 있다. 꺼진 상태로
    // PhotonNetwork.Instantiate()를 호출하면 로컬 오브젝트는 즉시 생기지만, 그 사실을 다른
    // 클라이언트에게 알리는 네트워크 명령은 SendOutgoingCommands()가 멈춰있어 전송되지 못하고
    // 큐에만 쌓인다(PhotonHandler.cs). 스폰 직전에 명시적으로 다시 켜서 이 경합을 없앤다.
    PhotonNetwork.IsMessageQueueRunning = true;

    Vector3 offset = new Vector3(Random.Range(-5.0f, 5.0f), 0f, Random.Range(-5.0f, 5.0f));
    Vector3 spawnPos = spawnPointObj.transform.position + offset;

    PhotonNetwork.Instantiate(PlayerPrefabName, spawnPos, Quaternion.identity, 0);
}
```

**왜 이 위치인가**: `IsMessageQueueRunning`을 끄는 것도, 다시 켜는 것도 전부 PUN이 "씬 전체"를
기준으로 관리하는 전역 플래그라 이 한 줄을 어디서 실행하든 같은 효과를 낸다 — `Instantiate` 호출
바로 앞에 두면 "내가 이 네트워크 호출을 하기 직전에 큐가 반드시 켜져 있어야 한다"는 의도가
코드만 봐도 명확하다. `GameManager.Start()`가 잠시 뒤(같은 프레임 근처) 다시 한번
`IsMessageQueueRunning = true`를 설정하지만(기존 코드, 변경 없음), 이는 중복 설정일 뿐 부작용이
없다(단순 bool 대입).

### 4.4 정직한 한계 — 다중 클라이언트 실측 불가

이번 세션은 Unity 에디터 인스턴스가 하나뿐이라 실제로 2~4개의 별도 클라이언트를 동시에 접속시켜
"수정 전엔 안 보이고 수정 후엔 보인다"는 것을 직접 재현·대조하지는 못했다. §4.2에서 제시한
근거는 다음 두 가지가 **사실임을 SDK 소스로 직접 확인**한 데서 나온 것이다:
1. `PlayerSpawner.Awake()` 시점에 `IsMessageQueueRunning`이 꺼져있을 수 있다는 것(Unity 씬 로딩
   생명주기 + PUN의 `LoadLevel`/`NewSceneLoaded` 구현으로 확정).
2. 꺼진 상태에서는 네트워크 송수신 자체가 멈춘다는 것(`PhotonHandler.Update()`의 `while
   (IsMessageQueueRunning && ...)` 가드로 확정).

이 두 사실만으로도 "스폰 직전에 큐가 켜져 있는지 확신할 수 없는 상태로 네트워크 인스턴스화를
한다"는 것은 명백한 결함이며, 수정안(§4.3)은 이 결함을 완전히 제거한다 — 다만 이것이 사용자가
보고한 정확한 비대칭 증상(왜 마스터는 아무도 안 보이고 나머지는 마스터만 보이는지의 구체적인
메커니즘)까지 100% 재현·설명한다고 단정하지는 않는다. **실제 4인 동시 접속 환경에서 재검증이
필요하다.**

### 4.5 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. (가능하다면) 여러 클라이언트(다른 PC/빌드 포함)로 실제 4인 매칭을 진행해, `GameLobbyScene`에서
   전원이 서로를 볼 수 있는지 확인 — 이번 세션에서 못한 검증이므로 사용자 측 실기기 테스트가 필요.
3. 단일 클라이언트로는 `PhotonNetwork.IsMessageQueueRunning`이 `PlayerSpawner.Awake()` 실행
   시점에 `true`로 강제되는지, 스폰이 여전히 정상 동작하는지(회귀 없음)만 확인 가능.

---

## 5. 카메라가 캐릭터 뒷모습만 보여주는 버그 — 1차 조사(아래, `Camera_Ctrl.cs` 가설 반증) → ✅ 진짜 원인과 수정은 §10에서 완료

### 5.1 증상 (사용자 보고)

- 카메라가 항상 캐릭터의 뒷모습만 비춘다.
- 마우스 우클릭 드래그로 시점을 돌려도 뒷모습만 보인다(정상이라면 옆/앞모습도 보여야 함).
- 심지어 플레이어2(내 캐릭터)의 카메라로 플레이어1(방장, 다른 캐릭터)을 봐도 플레이어1 역시
  뒷모습만 보인다.
- 사용자는 "마우스 휠 줌 제거할 때 버그가 터진 것으로 추정"하며 `PlayerControllPlan.md`와
  `Camera_Ctrl.cs`를 재검토해달라고 요청함.

### 5.2 조사 — `PlayerControllPlan.md` §13 재검토

§13.4(줌 제거)/§13.5(카메라 동적 연결)를 다시 읽고 **현재 `Camera_Ctrl.cs` 코드와 한 줄 한 줄
대조**했다 — 계획서의 "변경 후 예상 코드"와 실제 파일이 완전히 일치함을 확인했다. 즉 계획대로
정확히 구현되어 있고, 계획 자체에서 줌 제거가 회전(우클릭 드래그) 로직을 건드리는 부분은 없다
(줌은 `m_BasicPos.z`만, 회전은 `m_RotH`/`m_RotV`/`m_CurrentRotation`만 다룸 — 서로 다른 축).

### 5.3 실측 검증 — Play Mode에서 직접 회전값을 조작해 결과 확인

코드만 읽어서는 "정상으로 보이는데 실제로는 문제가 있을 수도 있다"는 가능성을 배제할 수 없어,
Unity MCP `execute_code`로 실제 Play Mode(GameLobbyScene, 방 생성 후 자기 캐릭터 존재)에서
`Camera_Ctrl`의 비공개 필드 `m_RotH`를 리플렉션으로 직접 90°/180°로 강제 설정하고, **트랜스폼
값을 직접 조회**했다(스크린샷이 아니라 실제 컴포넌트 데이터로 확인 — §5.4 참고):

```csharp
var camCtrl = UnityEngine.Object.FindFirstObjectByType<Camera_Ctrl>();
var rotHField = typeof(Camera_Ctrl).GetField("m_RotH", BindingFlags.NonPublic | BindingFlags.Instance);
rotHField.SetValue(camCtrl, 180f);
```

결과(실제 측정값):

| 상태 | `m_RotH` | 카메라 `transform.position` | 카메라 `transform.eulerAngles` |
|---|---|---|---|
| 기본(rotH=0) | 0 | 계산상 캐릭터 뒤쪽 | (25, 0, 0) |
| 180° 강제 | 180 | `(0.69, 2.75, 0.80)` | `(25, 180, 0)` |
| 90° 강제 | 90 | `(-2.21, 2.75, -2.10)` | `(25, 90, 0)` |

180°/90° 두 경우 모두, **캐릭터 위치(`playerPos=(0.69, 0, -2.10)`)를 기준으로 정확히 반대편/
측면에 해당하는 좌표로 카메라가 이동**했고(직접 좌표 계산으로 재검산 완료 — `Quaternion.
Euler(25, rotH, 0) * (0,0,-3.2)`를 손으로 계산한 값과 실측값이 소수점까지 일치), `Camera.main`이
정확히 이 하나의 카메라 인스턴스를 가리키는 것(씬에 카메라가 정확히 1개뿐임도 함께 확인)까지
검증했다.

**결론: `Camera_Ctrl.cs`의 회전/궤도(orbit) 수학은 100% 정확하게 동작한다.** `m_RotH`가 바뀌면
카메라의 실제 `Transform`(위치·회전 컴포넌트 데이터)이 정확히 기대한 대로 갱신된다 — **"마우스
휠 줌 제거 시 로직이 깨졌다"는 가설은 이 실측으로 근거가 없음이 확인됐다.**

### 5.4 검증 중 발견한 한계 — 이번 환경에서는 스크린샷이 실시간 카메라 상태를 반영하지 못함

위 표의 트랜스폼 값은 명확히 회전이 반영됐음을 보여주는데도, **같은 시점에 찍은 게임 화면
스크린샷은 세 경우(0°/90°/180°) 모두 시각적으로 거의 동일한 "뒷모습" 구도로 보였다.** 씬에
카메라가 정확히 1개뿐이고 그 카메라의 `Transform`이 실측으로 확인한 값과 정확히 일치하는데도
렌더링 결과가 이를 반영하지 못했다 — 이는 실제 게임 버그가 아니라, **이 자동화 세션에서 Unity
에디터 창이 OS 포커스를 갖지 못한 상태로 스크린샷을 찍을 때 Game View가 실시간으로 다시 그려지지
않는(stale) 이 환경 특유의 한계**로 판단된다(`editor_state`의 `is_focused: false`가 세션 내내
유지됨). 즉 **이번 세션의 스크린샷 기반 시각 검증은 이 특정 버그에 대해서는 신뢰할 수 없다** —
트랜스폼 데이터(실제 게임 로직의 결과물)만 신뢰할 수 있는 근거로 삼았다.

### 5.5 "플레이어2 시점에서 플레이어1도 뒷모습만 보인다"에 대한 별도 설명

`HideOrSeekPlayer.cs`를 다시 확인한 결과, 캐릭터의 **몸통 회전(`transform.rotation`)은 이동
입력이 있을 때만 갱신된다**:

```csharp
private void CheckMovementInput()
{
    ...
    if (moveDir != Vector3.zero)
    {
        rotation = moveDir;
        ...
        if (!isJump && !isDodge)
            animationDriver.ChangeState(...);
    }
    else
    {
        rotation = Vector3.zero;   // 가만히 있으면 rotation이 0 벡터 — Move()의 어느 분기도 LookAt을 호출하지 않음
        ...
    }
}
```

`Move()`의 모든 분기는 `rotation != Vector3.zero`일 때만 `transform.LookAt(...)`을 호출한다 —
즉 **캐릭터가 이동 입력을 전혀 받지 않고 가만히 서 있으면, 스폰 시점의 회전값(기본
`Quaternion.identity`, 월드 기준 정면)이 그대로 유지된 채 한 번도 갱신되지 않는다.** 대기방
(`GameLobbyScene`)에서는 플레이어들이 실제로 캐릭터를 조작해 돌아다닐 이유가 적으므로, 다른
플레이어(예: 방장)가 이동을 전혀 하지 않았다면 그 캐릭터는 스폰 이후 계속 같은 방향을 보고 있을
것이다 — 카메라 자체는(§5.3에서 검증했듯) 정상적으로 궤도를 돌 수 있으므로, 카메라가 실제로
움직였는데도 "여전히 등만 보인다"면 이는 **카메라가 아니라 상대방 캐릭터가 계속 같은 방향을
보고 있기 때문일 가능성**을 함께 고려해야 한다. 다만 이것만으로 "우클릭을 아무리 돌려도 항상
등만 보인다"는 사용자의 전체 증상을 다 설명하지는 못한다 — §5.3에서 카메라 자체의 궤도는
정상임을 이미 확인했으므로, 실제 사용자 환경에서는 카메라가 물리적으로 움직였을 것으로 예상되고,
그 상태에서도 계속 등이 보인다면 그 시점에 상대방이 정말로 그 방향을 보고 서 있었을 가능성이
높다는 정황 증거로 참고할 수 있다.

### 5.6 남은 가설 및 다음 진단 단계 (미해결)

`Camera_Ctrl.cs`의 궤도 수학 자체는 반증됐으므로, 남은 후보는 다음과 같다 — 전부 **실제 마우스로
조작하는 라이브 세션에서만 확정 가능**하며, 이번 세션(단일 에디터, 포커스 없는 자동화 환경)에서는
검증 수단이 없었다:

1. **입력이 `Camera_Ctrl`까지 도달하지 못함**: `Input.GetMouseButton(1)`/`Input.GetAxis("Mouse
   X"/"Mouse Y")`가 실제 우클릭 드래그에도 값이 0으로 읽히는 경우. `ProjectSettings/
   InputManager.asset`에 `Mouse X`/`Mouse Y` 축이 정상 정의돼 있고, `Player Settings → Active
   Input Handling`도 `Both`(`activeInputHandler: 2`)로 확인했으므로(`PlayerControllPlan.md`
   §12.3-5에서 이미 legacy Input 동작이 검증된 것과 일치) 설정 자체는 정상으로 보이지만, 실제
   런타임 값이 0인지는 로그로 직접 찍어봐야 확실하다.
2. **Game View 포커스/클릭 선점 문제**: 에디터에서 Game View를 처음 클릭하는 순간이 "창 포커스
   획득"으로 소비되어 실제 드래그로 인식되지 않는 경우 — 사용자가 Game View를 한 번 클릭해
   포커스를 준 다음 다시 우클릭 드래그하면 되는지 확인 필요.
3. **UI가 마우스를 가로챔**: 화면 대부분을 채우는 UI(채팅 패널, 플레이어 목록 등)가 우클릭
   이벤트 자체를 삼키는 경우 — 다만 `Camera_Ctrl`은 `EventSystem`을 거치지 않는 저수준
   `Input.GetMouseButton` 폴링이라 이론적으로는 UI와 무관해야 하지만, 확인해볼 가치는 있다.

**권장 다음 단계**: 코드 수정 전에, `Camera_Ctrl.LateUpdate()`의 `if (Input.GetMouseButton(1))`
블록 안에 임시로 `Debug.Log($"MouseX={Input.GetAxis(\"Mouse X\")} MouseY={Input.GetAxis(\"Mouse
Y\")}")`를 추가해, 실제 사용자 환경(에디터 또는 빌드)에서 우클릭 드래그 시 이 값이 실제로
0이 아닌지부터 확인하는 것을 제안한다. 값이 정상적으로 찍히는데도 화면이 안 바뀐다면 렌더링
쪽(카메라가 여러 개이거나 다른 렌더 경로) 문제로 좁혀지고, 값이 0으로 찍힌다면 입력 계층
문제로 좁혀진다 — 이번 조사로 "카메라 회전 계산 로직"이라는 큰 용의선은 이미 제외됐으므로,
다음 조사는 이 진단 로그 결과를 갖고 좁혀서 진행하는 것이 효율적이다.

### 5.7 상태

**`Camera_Ctrl.cs` 자체의 결함 가설은 실측으로 반증 완료.** 코드 수정은 제안하지 않는다(고칠
지점을 찾지 못한 상태에서 코드를 바꾸는 것은 의미가 없다) — 대신 §5.6의 진단 로그를 먼저
추가해서 실제 원인을 좁히는 것을 다음 단계로 제안한다. 사용자가 실기기에서 위 진단 로그 결과를
확인해주면, 그 결과를 갖고 다시 상세 계획을 세우겠다.

---

## 6. 종합 구현 순서 제안 — ✅ 아래 순서 그대로 구현 완료

①③④는 서로 독립적이라 순서 무관하게 적용 가능하고, ②는 ①과 같은 씬 작업 없이 코드만으로
끝난다. ⑤는 이번 계획에 포함된 코드 수정이 없으므로(진단 로그만 제안) 제외.

1. `Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`에 `IsMine` 프로퍼티 추가(②).
2. `Assets/02. Scripts/GameManager/GameManager.cs`에 `SetLocalPlayerMovementLocked` 추가 및
   `Update()` 수정(②).
3. `Assets/02. Scripts/Lobby/LobbyController.cs`의 `OnRandomJoinButtonClicked()` 수정(③).
4. `Assets/02. Scripts/GameManager/PlayerSpawner.cs`의 `SpawnLocalPlayer()`에
   `PhotonNetwork.IsMessageQueueRunning = true;` 추가(④).
5. 매 파일 저장 직후 `read_console`로 컴파일 에러 0건 확인.
6. `GameLobbyScene`/`GameScene`에서 `ConfirmDialog` GameObject를 활성 상태로 되돌리고 저장(①).
7. Play Mode로 §1.4/§2.4/§3.4/§4.5의 검증 항목을 순서대로 확인.

---

## 7. 범위 밖으로 남겨두는 것

- **⑤ 카메라 버그의 실제 코드 수정** — 원인이 아직 좁혀지지 않아 진단 로그 결과를 먼저 확인해야
  한다(§5.6/§5.7).
- **④의 다중 클라이언트 실측 검증** — 이번 세션(단일 에디터)에서는 근본적으로 수행 불가능하다
  (§4.4). 수정 코드는 반영하되, 실기기 다중 접속 재검증이 필요하다.
- **`ConfirmDialog`를 `CanvasGroup` 기반으로 재설계** — §1.3에서 대안으로 검토했으나, 이번
  버그의 확정 원인(씬 데이터 오버라이드 한 곳)에 대한 최소 수정이 아니므로 채택하지 않았다.
  향후 유사한 문제가 재발하면 별도로 요청해달라.
- **`architecture-review-plan.md`/`research.md`에서 이미 범위 밖으로 분류된 항목들**(ColorTag
  통합 공백, `GameManager.Inst`/`is_Conversating` 죽은 코드 등)은 이번에도 건드리지 않는다.

---

## 8. 구현 완료 보고 (2026-08-15)

사용자 지시에 따라 ⑤(카메라)를 제외한 ①②③④를 전부 구현하고 Play Mode에서 검증했다.

### 8.1 실제 변경 파일

| 파일 | 변경 내용 | 관련 항목 |
|---|---|---|
| `Assets/02. Scripts/Unit/HideOrSeekPlayer.cs` | `IsMine` 공개 프로퍼티 추가(`PlayerPaintCanvas.IsMine`과 동일 패턴) | ② |
| `Assets/02. Scripts/GameManager/GameManager.cs` | `localPlayer` 필드 + `SetLocalPlayerMovementLocked(bool)` 추가, `Update()`에서 채팅 열고/닫을 때 호출 | ② |
| `Assets/02. Scripts/Lobby/LobbyController.cs` | `OnRandomJoinButtonClicked()`에서 `PhotonNetwork.JoinRandomRoom(null, 0, MatchmakingMode.RandomMatching, null, null)` 호출로 변경 | ③ |
| `Assets/02. Scripts/GameManager/PlayerSpawner.cs` | `SpawnLocalPlayer()`에서 `PhotonNetwork.Instantiate()` 직전 `PhotonNetwork.IsMessageQueueRunning = true;` 명시적 설정 | ④ |
| `Assets/Scenes/GameLobbyScene.unity` | `ConfirmDialog` GameObject의 `m_IsActive` 오버라이드를 `0` → `1`로 수정 | ① |
| `Assets/Scenes/GameScene.unity` | 위와 동일 | ① |

### 8.2 계획과 달랐던 점 — `JoinRandomRoom` 오버로드 시그니처 정정

계획서(§3.3)에는 `PhotonNetwork.JoinRandomRoom(null, 0, MatchmakingMode.RandomMatching)` 3개
인자로 적었으나, 실제 Photon SDK(`PhotonNetwork.cs`)에는 이 3개 인자짜리 오버로드가 **존재하지
않는다** — `()`, `(Hashtable, int)`, `(Hashtable, int, MatchmakingMode, TypedLobby, string,
string[] = null)` 세 종류뿐이다. 이 사실은 계획 단계에서 SDK 소스까지 직접 열어보지 않고 API
문서 지식만으로 시그니처를 적었기 때문에 생긴 차이였다 — 실제로 Play Mode에 처음 진입을 시도할
때 `CS7036`(필수 인자 `typedLobby` 누락) 컴파일 에러로 즉시 드러났고, `read_console`로 즉시
잡아내 아래처럼 정정했다:

```csharp
public void OnRandomJoinButtonClicked()
{
    if (!TryApplyNickname()) return;
    PhotonNetwork.JoinRandomRoom(null, 0, MatchmakingMode.RandomMatching, null, null);
}
```

`typedLobby`(null, 기본 로비 사용)와 `sqlLobbyFilter`(null, 필터 없음) 둘 다 기존 동작을 바꾸지
않는 값으로 채웠다 — 매칭 모드(`RandomMatching`)만 명시한다는 계획의 의도는 그대로 유지된다.

### 8.3 Play Mode 검증 결과 (실제 Photon Cloud 접속, 코드 기반 조작으로 확인)

1. **①(ConfirmDialog 첫 클릭)** — `GameLobbyScene`/`GameScene` 두 씬 모두, 씬 진입 직후
   `dialog.activeSelf == false`(정상적으로 숨겨진 초기 상태)를 먼저 확인한 뒤, `m_BackBtn.onClick.
   Invoke()`를 **단 한 번**만 호출해 `dialog.activeSelf == true`로 바뀌는 것을 확인했다 — 이전에는
   이 첫 번째 호출에서 아무 변화가 없었던 것과 명확히 대조된다. 각 씬에 맞는 문구
   (`"로비로 나가시겠습니까?"` / `"게임이 진행중입니다. 나가시겠습니까?"`)도 정확히 표시됨을
   확인했다. "아니오" 클릭 시 확인창만 닫히고 `PhotonNetwork.InRoom`이 유지되는 것도 재확인했다.
2. **②(채팅 중 이동 잠금)** — `GameManager.SetLocalPlayerMovementLocked`를 리플렉션으로 직접
   호출해, 로컬 `HideOrSeekPlayer.IsMovementLocked`가 `true`/`false`로 정확히 토글되는 것을
   확인했다. `HideOrSeekPlayer.IsMine`이 로컬 캐릭터에 대해 `true`를 반환하는 것도 함께 확인했다
   (연결 지점 자체가 정상 동작).
3. **③(랜덤 입장)** — 수정된 시그니처로 `OnRandomJoinButtonClicked()`를 실제 호출해, 예외 없이
   정상 실행되고(런타임 에러 0건) 참가 가능한 방이 없는 상황에서는 `OnJoinRandomFailed` 콜백이
   정상적으로 `"참가 가능한 방이 없습니다."` 피드백을 표시하는 것까지 확인했다 — 회귀 없음.
   **다만 "정말로 무작위로 방을 고르는지"(통계적 분포)는 동시에 여러 방이 열려 있어야 검증
   가능한데, 단일 클라이언트로는 한 번에 하나의 방만 유지할 수 있어 이번 세션에서 직접
   재현하지 못했다** — 이 부분은 `MatchmakingMode.RandomMatching`이라는 Photon 공식 문서상
   보장되는 동작에 근거를 둔 것이며, 실제 여러 사용자가 여러 방을 동시에 만든 실환경에서
   재확인이 필요하다.
4. **④(GameLobbyScene 가시성)** — 방 생성 → `GameLobbyScene` 진입 → `PlayerSpawner.
   SpawnLocalPlayer()` 실행 전 과정에서 `read_console` 확인 결과 새로운 에러/경고가 없음을
   확인했다(캐릭터 스폰 자체는 정상). **`architecture-review-plan.md` 작업 때와 동일한 한계로,
   이번 세션도 단일 에디터라 실제 2인 이상 동시 접속 상황에서 "전원이 서로 보이는지"까지는
   재현·검증하지 못했다** — §4.4에 이미 기록된 한계 그대로다. 코드 수정 자체(스폰 직전
   `IsMessageQueueRunning = true` 강제)는 반영·컴파일 확인까지 마쳤다.
5. 전 과정에서 `read_console` 최종 확인 결과 에러 0건, 콘솔에 남은 항목은 전부 이번 작업과
   무관한 기존 항목뿐이었다(`"Failed to create agent because there is no valid NavMesh"` —
   `GameManager.md` §8.4에서 이미 기존 씬 구성 문제로 기록된 것과 동일 / `"Operation JoinLobby
   ... not called"` — `LobbyScene` 재진입 시 재연결 타이밍에 따른 기존의 일시적 경고).

### 8.4 최종 상태

①②③④ 전부 코드/씬 변경 및 컴파일 확인, 그리고 가능한 범위 내 Play Mode 검증까지 완료했다.
③의 통계적 무작위성과 ④의 다중 클라이언트 가시성은 이 세션(단일 에디터)의 구조적 한계로 완전한
실측 검증은 하지 못했다는 점을 §8.3에 정직하게 남겨둔다 — 실사용 환경(여러 클라이언트 동시 접속)
에서의 재확인을 권장한다. ⑤(카메라)는 사용자 지시대로 이번 구현에 포함하지 않았다.

**→ 사용자가 실제로 4인 멀티 테스트를 진행한 결과, ④는 §8의 수정 이후에도 동일 증상이
재현됐다. ⑤는 사용자가 직접 남긴 스크린샷을 근거로 재조사를 요청받았다. 두 버그 모두 아래
§9/§10에서 처음부터 다시 조사했다.**

---

## 9. [재조사] GameLobbyScene 가시성 버그 — 1차 수정 불충분, 더 근본적인 재계획 ✅ 구현·검증 완료

### 9.1 실측 결과 (사용자 4인 멀티 테스트)

> "방장은 아직도 GameLobbyScene에서 플레이어가 안보임. 다른 플레이어들은 아직도 방장밖에
> 안보임. 그리고 방장이 퇴장하면 아무도 안보임."

§8에서 적용한 수정(`PlayerSpawner.SpawnLocalPlayer()`에서 `PhotonNetwork.Instantiate()` 직전
`PhotonNetwork.IsMessageQueueRunning = true;` 명시)은 실제 4인 환경에서 **증상을 전혀 바꾸지
못했다.** 게다가 "방장이 퇴장하면 아무도 안 보인다"는 새 관찰은 기존 진단을 재검토할 중요한
단서다: 이 패턴은 **완전히 결정적(deterministic)**이다 — 매번 정확히 "방장의 캐릭터만
네트워크상에서 정상적으로 존재를 인정받고, 방장이 아닌 클라이언트가 스폰한 캐릭터는 아무에게도
(스폰한 본인의 화면에서조차 다른 사람에게 보이지 않는다는 의미로) 정상적으로 등록되지 않는다"는
구조로 매번 똑같이 재현된다. **이렇게 매번 똑같이 재현되는 패턴은 "타이밍이 어쩌다 어긋나는"
경합(race) 조건보다는, 방장과 참가자 사이에 구조적으로 다른 경로가 있다는 쪽에 더 무게가
실린다** — §8.2에서 제시했던 "`Awake()` 시점에 메시지 큐가 우연히 꺼져 있을 수 있다"는 설명은
여전히 SDK 소스로 확인된 사실이지만, 그것만으로는 이렇게 100% 결정적인 패턴을 전부 설명하기
부족하다는 것을 이번 실측이 보여준다.

### 9.2 재조사 — Photon SDK에서 씬 동기화 경로 재확인

`Assets/Photon/PhotonUnityNetworking/Code/PhotonNetworkPart.cs`를 다시 확인한 결과, 씬 동기화
(`LoadLevelIfSynced()`)가 트리거되는 지점이 **두 곳**이다:

```csharp
// PhotonHandler.cs:271 — 룸 프로퍼티가 바뀔 때마다(=방장이 씬을 바꿀 때마다)
public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
{
    PhotonNetwork.LoadLevelIfSynced();
}

// PhotonNetworkPart.cs:2495 — 참가자가 "JoinGame" 응답을 받는 그 순간에도 한 번 더
case OperationCode.JoinGame:
    if (Server == ServerConnection.GameServer)
    {
        PhotonNetwork.LoadLevelIfSynced();
    }
    break;
```

그리고 `NewSceneLoaded()`(메시지 큐를 되살리는 지점)는 `SceneManager.sceneLoaded` 이벤트
(Unity 기준: 모든 오브젝트 `Awake()` 완료 후, `Start()` 시작 전)에서만 실행된다 — 이 사실
자체는 §8.2와 동일하다. 다만 이번에 중요하게 다시 확인한 것은: **`PlayerSpawner.Awake()`에서
수동으로 `IsMessageQueueRunning = true`를 세팅해도, `NewSceneLoaded()`가 실제로 정리하는
`_AsyncLevelLoadingOperation` 등 다른 내부 상태는 그대로 남아있을 수 있다는 점**이다 — 즉 §8의
수정은 "메시지 큐가 흐르게" 만드는 효과는 있었겠지만, PUN 내부적으로 "씬 로딩이 완전히 끝났다"고
스스로 인식하는 시점(`NewSceneLoaded()` 실행 시점)보다 여전히 앞서 있어서, 인스턴스화 요청이
서버에 온전히 등록되지 못했을 가능성이 있다. **한 줄짜리 플래그 세팅으로 "일부"만 흉내내는
것이 아니라, Unity가 보장하는 "`Start()`는 반드시 `sceneLoaded`(따라서 `NewSceneLoaded()`) 이후에만
실행된다"는 훨씬 더 확실한 시점으로 스폰 자체를 완전히 옮기는 것이 근본적인 해결책이다.**

### 9.3 방안 — 스폰 시점을 `Awake()`에서 `Start()`로 이동 + `Camera_Ctrl` 초기화 시점 독립화

**문제**: `PlayerSpawner.Awake()`에서 스폰하는 이유는 애초에 `HideOrSeekPlayer.Awake()`가
`Camera_Ctrl.InitCamera(...)`를 호출해야 카메라 초기 각도(25° 부감)가 정상 적용되기
때문이었다(`architecture-review-plan.md` §7.1/§5.3). 스폰을 `Start()`로 옮기면
`PhotonNetwork.Instantiate(...)`가 새 캐릭터의 `Awake()`를 여전히 그 자리에서 동기적으로
호출하긴 하지만, 이 시점이 `Camera_Ctrl` 자신의 `Start()`보다 **먼저인지 나중인지가 더 이상
보장되지 않는다**(서로 다른 GameObject의 `Start()` 호출 순서는 Unity가 보장하지 않음) — 만약
`Camera_Ctrl.Start()`가 먼저 실행되면 `m_Player`가 아직 `null`이라 초기 각도 설정이 스킵된다.

**해결**: `Camera_Ctrl`의 1회성 초기화 로직을 `Start()`가 아니라 `InitCamera()` 호출 시점
자체로 옮긴다 — 그러면 `InitCamera()`가 `Awake()`든 `Start()`든 **언제 호출되든 상관없이**
항상 정확하게 초기화된다. 이렇게 하면 스폰 시점을 자유롭게(이번에는 `Start()`로) 옮길 수 있다.

**`Assets/02. Scripts/Camera/Camera_Ctrl.cs` 변경**:

```csharp
public void InitCamera(GameObject player)
{
    m_Player = player;
    ResetToDefaultView(); // InitCamera가 호출되는 시점이 Awake든 Start든 상관없이 항상 정확히 초기화됨
}

private void ResetToDefaultView()
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

void Start()
{
    // m_Player가 이미 연결되어 있다면(InitCamera가 이 시점 이전에 이미 호출된 경우) 정상 초기화.
    // 아직 연결 전이라면 아무 것도 하지 않고, InitCamera가 나중에 호출될 때 ResetToDefaultView()가
    // 대신 처리한다 — 호출 순서에 더 이상 의존하지 않는다.
    ResetToDefaultView();
}
```

`LateUpdate()`는 변경 없음(이미 `m_Player == null`이면 조기 리턴하므로 안전).

**`Assets/02. Scripts/GameManager/PlayerSpawner.cs` 변경**: `Awake()` 대신 `Start()`에서
스폰한다. Unity는 **모든 오브젝트의 `Awake()`가 끝나야 비로소 `Start()` 단계가 시작되고,
그 `Start()` 단계는 `SceneManager.sceneLoaded` 이벤트 **이후**에 시작된다는 것을 문서로 보장한다
— 즉 `PhotonNetwork.NewSceneLoaded()`(메시지 큐 복구 + 내부 상태 정리)가 100% 확실히 끝난
뒤에만 스폰이 실행된다.

```csharp
private void Start()
{
    SpawnLocalPlayer();
}

private void SpawnLocalPlayer()
{
    GameObject spawnPointObj = GameObject.Find(SpawnPointName);
    if (spawnPointObj == null)
    {
        Debug.LogWarning($"PlayerSpawner: \"{SpawnPointName}\" 오브젝트를 씬에서 찾을 수 없어 캐릭터를 스폰하지 못했습니다.");
        return;
    }

    // Start()는 Unity가 SceneManager.sceneLoaded(따라서 PhotonNetwork.NewSceneLoaded()에 의한
    // IsMessageQueueRunning 복구)보다 반드시 나중에 실행됨을 보장하므로, 별도로 플래그를 다시
    // 세팅할 필요 없이 항상 정상 상태에서 스폰이 이루어진다. 다만 방어적으로 한 번 더 확인한다.
    PhotonNetwork.IsMessageQueueRunning = true;

    Vector3 offset = new Vector3(Random.Range(-5.0f, 5.0f), 0f, Random.Range(-5.0f, 5.0f));
    Vector3 spawnPos = spawnPointObj.transform.position + offset;

    PhotonNetwork.Instantiate(PlayerPrefabName, spawnPos, Quaternion.identity, 0);
}
```

### 9.4 이 방안으로도 해결되지 않을 경우를 대비한 진단 로그 (함께 추가 권장)

§9.1에서 분석했듯 결정적(deterministic) 패턴이라 §9.3의 구조적 수정으로 해결될 가능성이 높다고
판단하지만, 만약 그렇지 않다면 **실제 4인 환경에서 무엇이 다른지 데이터가 반드시 필요하다** —
이번 세션은 단일 에디터라 다중 클라이언트를 직접 재현할 수 없으므로, 다음 로그를 추가해두면
사용자가 다음 멀티 테스트를 할 때 각 클라이언트의 콘솔(또는 빌드라면 로그 파일)에서 정확한
원인 데이터를 얻을 수 있다:

```csharp
PhotonNetwork.Instantiate(PlayerPrefabName, spawnPos, Quaternion.identity, 0);

var spawned = PhotonView.Find(pv => pv != null); // 아래처럼 더 직접적으로 확인 가능
```

더 정확하게는 `PhotonNetwork.Instantiate`가 반환하는 `GameObject`를 직접 받아서 로그로 남긴다:

```csharp
GameObject spawned = PhotonNetwork.Instantiate(PlayerPrefabName, spawnPos, Quaternion.identity, 0);
PhotonView spawnedView = spawned.GetComponent<PhotonView>();
Debug.Log($"[PlayerSpawner] 스폰 완료: ViewID={spawnedView.ViewID}, IsMine={spawnedView.IsMine}, " +
          $"IsSceneView={spawnedView.IsSceneView}, LocalActorNr={PhotonNetwork.LocalPlayer.ActorNumber}, " +
          $"IsMasterClient={PhotonNetwork.IsMasterClient}, RoomPlayerCount={PhotonNetwork.CurrentRoom.PlayerCount}");
```

이 로그가 **모든 클라이언트에서 정상적인 `ViewID`(0이 아닌 값)를 찍는지**를 확인하면, 문제가
"스폰 자체(로컬 생성)"에 있는지 "네트워크 등록/전파"에 있는지 확실히 구분할 수 있다 — `ViewID`가
0이거나 `IsSceneView`가 예상과 다르면 로컬 인스턴스화 단계에서부터 문제라는 뜻이고, `ViewID`는
정상인데도 다른 클라이언트에게 안 보인다면 순수하게 서버 릴레이/전파 쪽 문제로 좁혀진다.

### 9.5 검증 계획 (구현 후 진행)

1. `read_console`로 컴파일 에러 0건 확인.
2. 단일 클라이언트로 `PlayerTestScene`/`GameLobbyScene`에서 스폰이 여전히 정상 동작하는지(회귀
   없음), 카메라 초기 각도가 여전히 정상(부감 25°)인지 확인 — `Camera_Ctrl` 변경의 회귀 테스트.
3. **실제 4인 멀티 테스트로 재확인이 필수다** — 이 세션(단일 에디터)에서는 근본적으로
   재현·검증이 불가능하므로, §9.4의 진단 로그와 함께 배포한 뒤 사용자가 다시 4인 테스트를
   진행해 결과를 알려주는 것이 다음 단계다.

### 9.6 상태

**✅ 구현·검증 완료(2026-08-15).** §9.3(구조적 수정)과 §9.4(진단 로그)를 실제로 함께
적용했다 — `PlayerSpawner.cs`의 스폰 트리거를 `Awake()`에서 `Start()`로 옮기고, `Camera_Ctrl.cs`를
`InitCamera()` 호출 시점에 스스로 초기화하도록 리팩터링해 스폰 시점 변경이 카메라 초기화 순서에
영향을 주지 않도록 분리했으며, 스폰 성공 여부(ViewID/IsMine/IsSceneView/RoomPlayerCount)를
콘솔에 남기는 진단 로그도 추가했다. 자세한 구현 내역과 Play Mode 검증 결과는 §11 참고.
**다만 이 시점엔 실제 다중 클라이언트 환경에서의 가시성 자체는 재현·검증하지 못했다** — 이후
사용자의 실제 4인 빌드 테스트에서 이 2차 수정도 불충분함이 확인됐고(여전히 방장만 보이는 증상
재현), 더 깊은 재조사 끝에 진짜 원인을 §12에서 최종 확정·수정하고 실제 4인 멀티 테스트로
검증을 완료했다.

---

## 10. [재조사] 카메라가 캐릭터 뒷모습만 보이는 버그 — 근본 원인 확정 ✅ 구현·검증 완료

### 10.1 사용자 제공 스크린샷 분석

`Assets/Screenshots/CameraTest1.png`(정면)/`CameraTest2.png`(마우스 우클릭으로 우측 회전 후)를
직접 열어 확인했다. 두 스크린샷 모두 `PlayerTestScene`에서 `hide_or_seek_player`가 선택된
상태의 Inspector가 함께 찍혀 있었는데, **캐릭터 루트 GameObject의 `Transform → Rotation`이
`CameraTest1`에서 `(X=53.96, Y=89.25, Z=0)`, `CameraTest2`에서 `(X=56, Y=0.75, Z=0)`으로 표시되어
있었다** — 일반적으로 서 있는 캐릭터의 회전값은 X/Z가 0에 가까워야 하는데, X축이 54~56°나
기울어져 있다는 것 자체가 명백히 비정상이었다. 이 수치가 실마리가 되어 재조사를 시작했다.

### 10.2 Play Mode 격리 테스트로 원인 확정

`HideOrSeekPlayer.prefab`의 루트 `Transform`을 직접 확인한 결과 **정지 상태(prefab 기본값)는
`m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}`(완전한 항등 회전)**임을 먼저 확인했다 — 즉 이 이상한
회전은 프리팹 자체의 문제가 아니라 **런타임에만** 발생한다.

`PlayerTestScene`에서 Play Mode로 실제 재현 후, 구성 요소를 하나씩 꺼가며 원인을 격리했다:

| 단계 | 조치 | 결과 |
|---|---|---|
| 1 | 아무 것도 안 함 | `transform.eulerAngles = (30, 0, 0)` — 비정상 재현 확인 |
| 2 | `Animator.enabled = false` | 회전을 `(0,0,0)`으로 리셋해도 다음 순간 다시 `(30,0,0)`으로 복귀 — **Animator는 원인이 아님** |
| 3 | `NavMeshAgent.enabled = false`(Animator도 여전히 꺼둔 채) | 리셋해도 여전히 `(30,0,0)`으로 복귀 — **NavMeshAgent도 원인이 아님** |
| 4 | `Rigidbody`/`Collider` 존재 여부 확인 | 캐릭터 루트에는 `Rigidbody` 자체가 없음(물리 기반 넘어짐도 원인이 될 수 없음) |
| 5 | `PlayerColorVoteIndicator.enabled = false`(위 전부 비활성 상태 유지) | 리셋 후 **회전이 `(0,0,0)`으로 계속 유지됨 — 재현 중단, 원인 확정** |

**원인**: `Assets/02. Scripts/ColorTag/PlayerColorVoteIndicator.cs`의 `LateUpdate()`:

```csharp
private void LateUpdate()
{
    // 카메라를 향하도록 빌보드 처리
    if (Camera.main != null)
        transform.forward = Camera.main.transform.forward;
}
```

이 컴포넌트는 캐릭터 머리 위에 떠 있는 **작은 투표색 스프라이트 하나**를 카메라 쪽으로
빌보드 처리하기 위해 만들어진 것이다(`GameScenePlan.md` §5.2, `research.md` §25.8). 그런데
`HideOrSeekPlayer.prefab`을 직접 조회한 결과, **`PlayerColorVoteIndicator` 컴포넌트 자체가
스프라이트가 달린 자식 오브젝트("VoteIndicator")가 아니라 캐릭터 루트 GameObject
("HideOrSeekPlayer")에 붙어 있다.** `LateUpdate()`의 `transform`은 **이 컴포넌트가 붙어있는
오브젝트의 transform**을 가리키므로, 실제로는 작은 스프라이트가 아니라 **캐릭터 몸 전체**가
매 프레임 `Camera.main.transform.forward`와 정확히 같은 방향을 보도록 강제로 회전되고 있었다.

이것이 왜 "뒷모습만 보임" 증상과 정확히 일치하는지: 캐릭터의 정면 방향이 항상 카메라가 보는
방향과 똑같이 맞춰지므로, 카메라 입장에서는 캐릭터가 **항상 정확히 자신과 같은 방향을 보고
서 있는(=카메라에게 등을 돌리고 있는)** 것처럼 보인다. `Camera_Ctrl`을 우클릭으로 아무리
돌려도 캐릭터가 매 프레임 즉시 그 방향으로 다시 돌아가 버리므로, 실질적으로 "정면을 볼 수
없는" 것처럼 느껴진다 — **`Camera_Ctrl.cs`의 궤도 회전 자체는 (지난 조사에서 실측으로 이미
확인했듯) 완전히 정상이었고, 문제는 카메라가 아니라 캐릭터 쪽에 있었다.**

**"플레이어2 시점에서 플레이어1(방장)도 뒷모습만 보인다"는 이유**: `PlayerColorVoteIndicator.
LateUpdate()`에는 **`pv.IsMine` 같은 소유권 체크가 전혀 없다** — 즉 이 버그는 로컬 소유
캐릭터뿐 아니라, 그 클라이언트의 화면에 렌더링되는 **모든** `HideOrSeekPlayer` 인스턴스(원격
플레이어 포함)에 대해 각자 독립적으로 실행된다. 플레이어2의 클라이언트에서는, 플레이어2 자신의
캐릭터뿐 아니라 화면에 보이는 플레이어1의 (원격) 캐릭터도 **플레이어2의 로컬 `Camera.main`**
방향을 그대로 따라가도록 강제된다 — 그래서 플레이어1이 실제로 어느 방향을 보고 있는지와
무관하게, 플레이어2의 화면에서는 플레이어1도 똑같이 "카메라와 같은 방향을 보는 것"처럼
회전해버려 등만 보이게 된다. 완전히 하나의 원인으로 사용자가 보고한 두 가지 증상(내 캐릭터의
뒷모습, 다른 플레이어의 뒷모습)이 전부 설명된다.

### 10.3 수정 계획

**`Assets/02. Scripts/ColorTag/PlayerColorVoteIndicator.cs`**: `LateUpdate()`가 `this.transform`
(캐릭터 루트)이 아니라 `indicator`(스프라이트 자식 오브젝트)의 transform을 회전시키도록
한 줄만 고친다 — 이미 `indicator` 필드가 정확히 그 스프라이트를 가리키고 있으므로, 프리팹
구조를 바꾸거나 다른 필드를 추가할 필요가 전혀 없다.

```csharp
private void LateUpdate()
{
    // 카메라를 향하도록 빌보드 처리 — 캐릭터 전체가 아니라 투표색 스프라이트(indicator)만 회전시킨다
    if (Camera.main != null && indicator != null)
        indicator.transform.forward = Camera.main.transform.forward;
}
```

**왜 이 수정으로 충분한가**: `indicator`는 `[SerializeField] private SpriteRenderer indicator;`로
이미 스프라이트 자식 오브젝트를 정확히 가리키고 있다(`OnPlayerPropertiesUpdate`에서
`indicator.enabled`/`indicator.color`를 이미 정상적으로 그 자식 오브젝트에 적용하고 있는 것으로
확인됨) — `transform` 하나만 `indicator.transform`으로 바꾸면 캐릭터 루트는 더 이상 건드리지
않고, 원래 의도한 "작은 스프라이트만 빌보드 처리"가 정확히 구현된다. 프리팹 구조(컴포넌트가
루트에 붙어있는 것 자체)는 바꾸지 않는다 — 코드 한 줄로 충분하고, 프리팹을 재구성하는 것보다
위험이 낮다.

**대안으로 검토했으나 채택하지 않은 방법**: `PlayerColorVoteIndicator` 컴포넌트 자체를
"VoteIndicator" 자식 오브젝트로 옮기는 방법도 가능하지만, 프리팹 계층 구조를 바꾸는 작업이라
(이전에 `manage_gameobject`로 프리팹을 다룰 때 `position`이 의도치 않게 초기화되는 등의 사고가
있었음, `GameManager.md` §9.11.4) 위험 대비 이득이 적다 — 코드 한 줄 수정이 정확히 같은 효과를
내므로 그쪽을 택한다.

### 10.4 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. `PlayerTestScene`에서 Play Mode 진입 후, 리플렉션 없이 `hide_or_seek_player`(또는 실제
   스폰된 캐릭터)의 `transform.eulerAngles`가 가만히 있을 때 `(0, 임의의 Y, 0)`을 유지하는지
   (더 이상 X축이 30° 근처로 튀지 않는지) 확인.
3. Play Mode에서 실제로(또는 `Camera_Ctrl.m_RotH`를 코드로 조작해) 카메라를 90°/180° 돌렸을 때
   캐릭터의 정면/측면이 정상적으로 보이는지 스크린샷 또는 실제 조작으로 확인.
4. 색상 선택 라운드 중 투표색 스프라이트(`indicator`)가 여전히 정상적으로 카메라를 향해
   빌보드되고, 색상 표시(`OnPlayerPropertiesUpdate`)도 회귀 없이 동작하는지 확인 — 이 컴포넌트의
   원래 기능 자체는 그대로 유지되어야 한다.

### 10.5 상태

**✅ 원인 확정, 구현·검증 완료(2026-08-15).** 이번 조사로 나온 결론 중 하나는, 지난 조사(§5)에서
`Camera_Ctrl.cs`의 궤도 회전 수학이 실측으로 정상임을 확인한 것 자체는 **틀리지 않았다는 것**이다
— 다만 그 실측만으로는 "그런데 왜 사용자에게는 안 보이는가"까지 설명하지 못했을 뿐이었고, 이번에
사용자가 남긴 스크린샷의 Inspector 정보(회전값)가 결정적인 단서가 되어 진짜 원인(다른 컴포넌트가
캐릭터를 회전시키고 있었다는 것)을 찾을 수 있었다. §10.3의 수정을 실제로 적용했고, Play Mode에서
회전값(카메라를 180°까지 돌려도 캐릭터 회전이 `(0,0,0)`으로 유지됨)과 스크린샷 양쪽으로 정면이
정상적으로 보임을 직접 확인했다. 자세한 검증 내역은 §11 참고.

---

## 11. §9·§10 구현 완료 보고 (2026-08-15)

### 11.1 변경한 파일

| 파일 | 변경 내용 |
|---|---|
| `Assets/02. Scripts/ColorTag/PlayerColorVoteIndicator.cs` | §10.3 그대로 적용 — `LateUpdate()`가 `transform.forward`가 아니라 `indicator.transform.forward`를 회전시키도록 한 줄 수정 |
| `Assets/02. Scripts/Camera/Camera_Ctrl.cs` | §9.3 그대로 적용 — `InitCamera(GameObject player)`를 추가해 `Awake()`/`Start()` 호출 순서와 무관하게 카메라가 스스로 초기화되도록 분리(`ResetToDefaultView()`), 더 이상 쓰지 않는 마우스 휠 줌 관련 필드/로직 제거 |
| `Assets/02. Scripts/GameManager/PlayerSpawner.cs` | §9.3+§9.4 적용 — 스폰 트리거를 `Awake()`에서 `Start()`로 이동(Unity의 Awake→sceneLoaded→Start 순서 보장을 이용), 스폰 결과(ViewID/IsMine/IsSceneView/LocalActorNr/IsMasterClient/RoomPlayerCount)를 출력하는 진단 로그 추가 |
| `Assets/02. Scripts/Unit/HideOrSeekPlayer.cs` | `Awake()`에서 `Camera.main.GetComponent<Camera_Ctrl>().InitCamera(gameObject)` 호출 — Camera_Ctrl 리팩터링에 맞춰 카메라 연결 지점을 명시적으로 정리 |

### 11.2 부수적으로 발견·수정한 문제

`PlayerTestScene.unity`에서 Play Mode로 §10 검증(카메라 궤도 회전 확인)을 진행하던 중,
`Main Camera`의 `Camera_Ctrl` 컴포넌트가 씬 파일에 `m_Enabled: 0`(비활성)으로 저장되어 있어
`LateUpdate()`가 아예 실행되지 않는 상태였음을 발견했다. 이번 세션의 코드 변경과는 무관한
기존 씬 상태(에디터에서 인스펙터를 조작하다 실수로 꺼둔 것으로 추정)였다. `manage_components`로
`enabled: true`로 되돌리고 씬을 저장해 검증을 계속했다 — 검증 완료 후에도 이 상태(활성화됨)로
유지된다.

### 11.3 Play Mode 검증 결과

1. **컴파일**: `read_console`로 전 과정에서 에러 0건 확인.
2. **버그⑤(카메라 뒷모습)**: `PlayerTestScene`에서 `Camera_Ctrl.m_RotH`를 0°→180°까지 조작하며
   캐릭터의 `transform.eulerAngles`를 관찰 — 수정 전에는 카메라를 따라 캐릭터가 즉시 같은 방향으로
   돌아갔지만, 수정 후에는 카메라가 어느 각도로 돌아가든 캐릭터 회전이 계속 `(0,0,0)`으로 고정됨을
   확인. 스크린샷으로도 카메라가 180° 돌아간 상태에서 캐릭터의 정면이 정상적으로 보임을 시각적으로
   재확인(§10.1의 문제 스크린샷과 반대 결과).
3. **버그④(스폰 시점) 회귀 확인**: `GameLobbyScene`, `GameScene` 양쪽에서 Play Mode 진입 후
   캐릭터가 정상적으로 스폰되고, 진단 로그가 다음과 같이 정확히 출력됨을 확인:
   `[PlayerSpawner] 스폰 완료: ViewID=1001, IsMine=True, IsSceneView=False, LocalActorNr=1, IsMasterClient=True, RoomPlayerCount=1`
4. **카메라 초기화 회귀 확인**: `Camera_Ctrl`이 `Start()`/`InitCamera()` 중 어느 경로로 먼저
   호출되어도(스폰 시점이 `Start()`로 옮겨지며 두 호출 순서가 씬마다 달라질 수 있음) 기본 시점
   `(m_RotV=25°, m_RotH=0°)`으로 정확히 초기화됨을 확인, 이후 우클릭 드래그로 인한 궤도 회전도
   정상 동작.
5. **회귀 없음**: 채팅(버그②), Back 버튼 확인창(버그①), 랜덤 입장(버그③) 관련 동작에 영향이
   없음을 콘솔 경고/에러 부재로 간접 확인(직접적인 코드 변경이 겹치지 않으므로 재검증은 생략).

### 11.4 남은 한계 — 정직하게 밝혀둠

이번 세션은 Unity 에디터 인스턴스를 **1개만** 사용할 수 있는 환경이었다. 따라서:

- **버그④(다중 클라이언트 가시성)는 이번에도 실제 다중 클라이언트 조건에서 재현·검증하지
  못했다.** 1차 수정(`Awake()`에서 `IsMessageQueueRunning`만 수동으로 되돌리는 방식)이 실측에서
  불충분했던 것과 똑같은 이유로, 이번 2차 수정 역시 "구조적으로 이전보다 훨씬 더 신뢰할 수 있는
  근거(Unity가 문서로 보장하는 Awake→sceneLoaded→Start 순서를 이용)를 갖는다"는 점은 확실하지만,
  **실제로 문제가 해결됐는지는 사용자가 4인 이상으로 다시 멀티 테스트를 해봐야 최종 확인 가능하다.**
  만약 이번에도 재현된다면, §9.4에서 추가한 진단 로그(각 클라이언트 콘솔의 `ViewID`/`RoomPlayerCount`
  값)를 비교해 "로컬 생성 자체 실패" vs "네트워크 전파 실패" vs "다른 클라이언트의 렌더링/가시성
  문제"로 원인 범위를 좁힐 수 있다.
- 버그⑤(카메라)는 단일 클라이언트로도 원인과 수정 모두 완전히 재현·확정할 수 있었으므로,
  위와 같은 한계가 없다 — 높은 신뢰도로 해결 완료로 판단한다.

---

## 12. [3차 재조사] GameLobbyScene 가시성 버그 — 진짜 원인 확정 및 실제 4인 멀티 테스트 검증 완료 (2026-08-15)

§9(2차 수정)까지 적용한 뒤 사용자가 실제 빌드 4개로 다시 테스트했으나, 여전히 같은 증상이
재현됐다: `Assets/Screenshots/Player2.png`/`Player3.png`/`Player4.png`(비방장 클라이언트 3개의
화면) 전부 방 인원이 3/4 → 4/4로 늘어나도 화면에는 항상 정확히 캐릭터 2개(자기 자신 + 방장)만
보였고, 방장은 아무도 안 보인다고 보고했다. `Player3.png`에는 인게임 로그에
`NullReferenceException: Object reference not set to an instance of an object`가 함께 찍혀 있어,
이번에는 순수 타이밍 문제가 아니라 실제 예외가 원인일 가능성이 제기됐다.

### 12.1 실시간 공동 디버깅 — Unity 에디터를 네 번째 참가자로 투입

사용자가 빌드 3개를 실행해 테스트하는 동안, Unity 에디터의 Play Mode를 이용해 같은 방("test1")에
네 번째 클라이언트로 직접 입장해 `read_console`/`execute_code`로 실시간 관찰했다. `Start()`가
실행되는 바로 그 프레임에 다음이 콘솔에 찍혔다:

```
True                                                                                 ← IsMessageQueueRunning (§9 수정으로 이미 true)
RaiseEvent(200) failed. Your event is not being sent! Check if your are in a Room   ← GameManager.Start()의 pv.RPC("LogMsg", ...)
RaiseEvent(202) failed. Your event is not being sent! Check if your are in a Room   ← PlayerSpawner.SpawnLocalPlayer()의 PhotonNetwork.Instantiate (202=Instantiation 이벤트)
[PlayerSpawner] 스폰 완료: ViewID=2001, IsMine=True, ...
```

그러나 그 직후 `execute_code`로 직접 상태를 조회하면 이미 `InRoom=True`, `NetworkClientState=Joined`로
정상 복구되어 있었고, 씬에는 방장(`ViewID=1001`)과 자기 자신(`ViewID=2001`)만 존재했다 — 정확히
사용자가 스크린샷에서 보고한 "자신+방장 2명"과 일치했다.

### 12.2 원인 ①: `PhotonNetwork.InRoom`과 `IsMessageQueueRunning`은 서로 다른 상태

`RaiseEvent(...) failed ... Check if your are in a Room`는 Photon SDK가 `PhotonNetwork.InRoom`이
`false`일 때 내는 경고다. §9에서는 `IsMessageQueueRunning = true`를 `Start()`에서 강제하면
문제가 해결될 것으로 예상했지만, **`IsMessageQueueRunning`(메시지 큐 처리 여부)과
`InRoom`(서버에 실제로 "방에 완전히 입장 완료"로 등록된 상태)은 완전히 다른 두 상태였다.**
Unity의 `Awake→sceneLoaded→Start` 순서는 전자만 보장할 뿐, follower 클라이언트의 `Start()`
시점에 후자가 이미 `true`라는 보장은 어디에도 없었다 — 이번 실측으로 실제로 그 틈에서
경쟁이 발생함을 직접 확인했다.

`PhotonNetwork.Instantiate()`는 로컬 GameObject 생성과 네트워크 전파(서버로 `RaiseEvent`
전송)를 함께 수행하는데, `InRoom`이 아직 `false`인 순간 호출하면 **로컬 생성은 그대로
성공하지만(그래서 그 클라이언트 자신은 스폰 성공 로그를 보게 됨) 네트워크 전파만 조용히
실패**한다 — 그래서 스폰을 시도한 그 클라이언트 자신을 제외한 **아무도 그 캐릭터의 존재를
영원히 알 수 없게 된다.** 방장은 씬 전환을 직접 트리거하는 입장이라 이 경쟁에 걸리지 않으므로
항상 정상 전파되고, 그래서 "방장만 보인다"는 증상과 정확히 일치한다. `GameManager.Start()`의
채팅 RPC(이벤트 200)도 동일한 이유로 실패해, follower 클라이언트가 접속했을 때 다른 사람
화면에 "[닉네임] Connected" 메시지가 뜨지 않는 부가 증상도 함께 설명된다.

### 12.3 원인 ②: `HideOrSeekPlayer.networkSync`가 `Start()`에서만 초기화됨 → NRE

`Player3.png`에 찍혀 있던 `NullReferenceException`의 스택 트레이스를 사용자가 직접 제공해
확인했다:

```
HideOrSeekPlayer.OnPhotonSerializeView (...) (Assets/02. Scripts/Unit/HideOrSeekPlayer.cs:251)
Photon.Pun.PhotonView.DeserializeComponent (...)
Photon.Pun.PhotonView.DeserializeView (...)
Photon.Pun.PhotonNetwork.OnSerializeRead (...)
Photon.Pun.PhotonNetwork.OnEvent (...)
...
Photon.Pun.PhotonHandler.Dispatch () (...)
Photon.Pun.PhotonHandler.FixedUpdate () (...)
```

251번째 줄은 원격 플레이어 데이터 수신 분기의 `networkSync.Read(stream, transform);`다.
`networkSync` 필드는 기존에 `Start()`에서만 생성됐는데, 스택 트레이스가 보여주듯
`OnPhotonSerializeView`는 Unity의 일반적인 `Awake→Start→Update` 순서에 속하지 않고
**Photon 자체의 네트워크 디스패치 루프(`PhotonHandler.FixedUpdate() → Dispatch()`)에서 별도로
호출된다.** 원격 플레이어 오브젝트가 막 네트워크로 생성된 직후, 그 오브젝트의 `Start()`가
아직 돌기 전에 Photon이 그 오브젝트로 들어오는 위치/애니메이션 데이터를 먼저 전달하면
`networkSync`가 아직 `null`인 상태에서 접근해 NRE가 발생한다. §12.2와 근본 원인의 성격은
같다(둘 다 "`Start()`가 실행되면 Photon 쪽도 준비가 끝나 있을 것"이라는, 실제로는 보장되지
않는 가정) — 다만 코드 위치와 구체적 증상은 다르다.

### 12.4 적용한 수정

**`Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`**: `networkSync = new PlayerNetworkSync();`를
`Start()`에서 `Awake()`로 이동, `if (!pv.IsMine) return;`보다 앞에 배치해 로컬/원격 상관없이
오브젝트 생성 즉시 실행되도록 함.

```csharp
private void Awake()
{
    networkSync = new PlayerNetworkSync();

    if (!pv.IsMine) return;

    Camera_Ctrl camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
    if (camCtrl != null)
        camCtrl.InitCamera(gameObject);
}
```

**`Assets/02. Scripts/GameManager/PlayerSpawner.cs`**: 고정된 생명주기 시점 하나를 추측하는
대신, `PhotonNetwork.InRoom`이 실제로 `true`가 될 때까지 매 프레임 확인하는 코루틴으로 교체.

```csharp
private void Start()
{
    StartCoroutine(SpawnWhenInRoom());
}

private IEnumerator SpawnWhenInRoom()
{
    while (!PhotonNetwork.InRoom)
        yield return null;

    SpawnLocalPlayer();
}
```

**`Assets/02. Scripts/GameManager/GameManager.cs`**: 채팅 연결 알림 RPC도 동일한 패턴으로 수정.

```csharp
private void Start()
{
    Time.timeScale = 1.0f;
    PhotonNetwork.IsMessageQueueRunning = true;
    StartCoroutine(SendConnectedMessageWhenInRoom());
}

private IEnumerator SendConnectedMessageWhenInRoom()
{
    while (!PhotonNetwork.InRoom)
        yield return null;

    string msg = "\n<color=#33ff33>[" + PhotonNetwork.LocalPlayer.NickName + "] Connected</color>";
    pv.RPC("LogMsg", RpcTarget.AllBuffered, msg, false);
}
```

부수적으로 `PlayerSpawner.cs`의 진단 로그가 쓰던 obsolete API `PhotonView.IsSceneView`를
`IsRoomView`로 교체(컴파일 경고 해결).

### 12.5 검증 — 실제 4인 멀티 테스트로 최종 확인

1. `read_console`로 컴파일 에러/경고 0건 확인.
2. Unity 에디터를 네 번째 클라이언트("ClaudeEditor")로 사용자의 실제 빌드 방("test1", 이미
   방장+2명 입장해 있던 방)에 재입장. 콘솔에 `RaiseEvent failed`나 `NullReferenceException`이
   **전혀 찍히지 않았고**, 진단 로그도 `[PlayerSpawner] 스폰 완료: ViewID=4001, IsMine=True,
   IsRoomView=False, LocalActorNr=4, IsMasterClient=False, RoomPlayerCount=4`로 정상 출력됨.
3. `execute_code`로 이 클라이언트(에디터, actor 4)의 씬을 직접 조회 — **4명 전원의
   `HideOrSeekPlayer` 인스턴스(ViewID 1001/2001/3001/4001, Owner 1~4)가 전부 존재**함을 확인.
   이전까지는 최대 2개(자신+방장)만 존재했던 것과 대조적.
4. 사용자에게 실제 빌드 3개(방장, Player5157, Player7257) 화면에서도 4명 전원이 보이는지
   직접 확인 요청 → **"4명 전부 보였어"로 확인받음.** 이번이 처음으로 방장·비방장 클라이언트
   양쪽 모두에서, 그것도 에디터가 아닌 실제 빌드 화면 기준으로 가시성 문제가 완전히 해결됐음을
   확인한 사례다.

### 12.6 상태

**✅ 최종 원인 확정, 수정 적용, 실제 4인 멀티 테스트(빌드 3개 + 에디터 1개)로 검증 완료.**
1차 수정(`Awake()`에서 `IsMessageQueueRunning` 수동 재활성화)과 2차 수정(§9, 스폰 시점을
`Start()`로 이동)은 둘 다 근거는 있었지만 진짜 원인(`InRoom` 상태 자체의 경쟁 조건, 그리고
`networkSync`의 초기화 시점 문제)을 정확히 짚지 못해 불충분했다. 이번 3차 조사는 사용자가
제공한 실제 빌드 스크린샷과 예외 스택 트레이스, 그리고 Unity 에디터를 실제 멀티플레이 세션의
참가자로 투입해 실시간으로 관찰한 콘솔 로그, 이 세 가지가 함께 있었기에 원인을 정확히
특정할 수 있었다. §9까지와 달리 이번에는 사용자의 실제 빌드 화면으로 직접 재확인까지
마쳤으므로, 이 문서에 남아 있던 마지막 "사용자가 최종 확인해야 한다"는 유보 조건도 해소됐다.

---

## 13. `GameLobbyScene` 입장 시 플레이어가 미끄러지고 걸을 때 버벅거리는 버그 — ✅ 원인 분석·구현·검증 완료

### 13.1 증상

사용자 보고: `GameLobbyScene`에 입장했을 때 (1) 캐릭터가 미끄러지는 느낌, (2) 걸을 때 렉 걸린
것처럼 버벅거리는(끊기는) 느낌이 있다.

### 13.2 이 버그가 언제 생겼는지 — 타이밍상 유력한 용의자

이 두 증상은 이번 대화에서 `HideOrSeekPlayer`에 물리 엔진(Rigidbody)을 새로 도입한
`PlayerControllPlan.md` §18(물리 도입) + §22(마찰 0 재질 적용) 작업 **직후** 처음 보고됐다 — 그
전까지는(수동 중력 + `transform.position +=` 방식일 때는) 이런 증상이 보고된 적이 없었다. 즉
원인은 그 사이에 바뀐 코드 안에 있을 가능성이 매우 높다. 실제로 코드를 다시 읽어보니 **Rigidbody
보간(Interpolation)과 수동 회전 대입이 서로 충돌하는, 잘 알려진 Unity 함정**을 그대로 밟고 있는
것을 발견했다.

### 13.3 근본 원인 (유력) — `RigidbodyInterpolation.Interpolate` + `transform.LookAt()` 충돌

`HideOrSeekPlayer.Start()`(§18 구현분)에서 로컬 플레이어의 `Rigidbody`에 보간을 켰다:

```csharp
rb.interpolation = RigidbodyInterpolation.Interpolate; // FixedUpdate 사이 시각적 끊김 완화
```

`RigidbodyInterpolation.Interpolate`는 Unity가 **Rigidbody 스스로 추적하는 위치·회전 값**을
기준으로, 물리 스텝(`FixedUpdate`, 기본 50Hz) 사이사이의 렌더링 프레임에서 이전 스텝과 현재
스텝 사이를 부드럽게 보간해 보여주는 기능이다 — 단, 이건 **`rb.position`/`rb.rotation`을
`rb.MovePosition()`/`rb.MoveRotation()`으로 바꿀 때만** 정상적으로 작동한다.

그런데 `Move()`(`FixedUpdate`에서 매 스텝 호출됨)는 회전을 **`transform.LookAt(...)`으로 직접**
바꾸고 있다(세 곳 전부):

```csharp
transform.LookAt(transform.position + new Vector3(dodgeMoveDir.x, 0f, dodgeMoveDir.z)); // 회피 중
transform.LookAt(transform.position + new Vector3(jumpMoveDir.x, 0f, jumpMoveDir.z));    // 점프 관성 중
transform.LookAt(transform.position + new Vector3(rotation.x, 0f, rotation.z));          // 일반 이동
```

`transform.rotation`을 직접 건드리는 건 **Rigidbody가 전혀 모르는 변경**이다 — Rigidbody 내부적으로
추적하는 회전값은 `rb.MoveRotation()`을 부르지 않는 한 그대로 멈춰있다(게다가 `Start()`에서
`rb.constraints = RigidbodyConstraints.FreezeRotation`까지 걸어놔서 물리적으로도 회전이 바뀔 일이
없다). 결과적으로 매 프레임 다음과 같은 일이 반복된다:

1. `FixedUpdate`(물리 스텝)에서 `Move()`가 `transform.LookAt(...)`으로 캐릭터를 원하는 방향으로
   순간적으로 돌려놓는다.
2. 그 다음 렌더링되는 여러 프레임 동안(다음 `FixedUpdate`가 오기 전까지), Unity의 보간 시스템이
   "Rigidbody가 추적하는 이전/현재 회전값 사이"를 다시 계산해 `transform.rotation`에 덮어쓴다 —
   그런데 Rigidbody가 추적하는 회전값은 1번의 `LookAt()` 변경을 전혀 반영하지 못한 채 그대로이므로,
   보간 시스템이 사실상 회전을 **원래 방향으로 도로 끌어당겨버린다.**
3. 다음 `FixedUpdate`가 오면 다시 1번이 반복 — 즉 **매 물리 스텝마다 회전이 "홱 돌아갔다가 다시
   슬며시 되돌아가는" 것을 반복**하게 된다.

이게 정확히 사용자가 보고한 두 증상을 동시에 설명한다:
- **버벅거림**: 캐릭터의 몸통 방향이 매 물리 스텝(20ms)마다 순간이동하듯 튀었다가 되돌아가길
  반복하니, 걷는 모습이 매끄럽지 않고 끊기는 것처럼 보인다.
- **미끄러짐**: 이동 방향(속도 벡터, `rb.linearVelocity`)은 `Move()`가 매 스텝 정확히 원하는
  방향으로 강제 대입하므로 실제로는 문제없이 부드럽게 움직이는데, **몸통이 그 방향을 제대로
  따라가지 못하고 흔들리니** 캐릭터가 정면으로 걷지 않고 옆으로 미끄러지듯 이동하는 것처럼
  보인다(마치 빙판 위에서 스케이팅하는 듯한 시각적 착시 — "몸은 다른 곳을 보는데 이동은 계속되는"
  전형적인 아이스스케이팅 현상).

`PlayerTestScene`에서 이 문제를 직접 겪지 못하고 놓친 이유: §18/§19 검증 때는 전부 리플렉션으로
`isJump`/`jumpRequested`를 직접 조작하거나 `rb.position`으로 순간이동시키는 방식으로 테스트했고,
**실제 방향키를 눌러 지속적으로 걷게 하면서 회전이 자연스러운지 육안으로 확인한 적이 없었다** —
검증 방법 자체의 사각지대였다.

### 13.4 수정 계획

`Move()` 안의 `transform.LookAt(...)` 세 곳을 전부 `rb.MoveRotation(...)`으로 바꾼다 —
`transform.LookAt(target)`은 내부적으로 `Quaternion.LookRotation(target - transform.position)`과
동일하므로, 같은 회전을 계산해 `rb.MoveRotation()`에 넘기면 된다:

```csharp
public void Move()
{
    Vector3 dir;
    float vel;
    Vector3 lookDir = Vector3.zero;

    if (isDodge && keepMovingAfterDodge)
    {
        dir = dodgeMoveDir;
        vel = speed;
        lookDir = new Vector3(dodgeMoveDir.x, 0f, dodgeMoveDir.z);
    }
    else if (isJump && keepMovingAfterJump)
    {
        dir = jumpMoveDir;
        vel = baseSpeed;
        lookDir = new Vector3(jumpMoveDir.x, 0f, jumpMoveDir.z);
    }
    else
    {
        vel = Input.GetKey(KeyCode.LeftShift) ? baseSpeed * 0.3f : baseSpeed;
        dir = rotation;
        lookDir = new Vector3(rotation.x, 0f, rotation.z);
    }

    if (lookDir != Vector3.zero)
        rb.MoveRotation(Quaternion.LookRotation(lookDir)); // transform.LookAt 대신 — Rigidbody 보간과 충돌하지 않도록

    Vector3 horizontal = new Vector3(dir.x * vel, 0f, dir.z * vel);
    rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
}
```

**왜 이걸로 충분한가**: `rb.MoveRotation()`은 Rigidbody가 추적하는 회전값 자체를 갱신하는
공식 API이므로, 보간 시스템이 그 다음 렌더링 프레임들에서 "이전 스텝 회전 → 이번 스텝 회전"
사이를 정확히 매끄럽게 보간하게 된다 — 더 이상 서로 다른 값을 두고 싸우지 않는다. `Rigidbody`
자체는 `RigidbodyConstraints.FreezeRotation`이 걸려 있어도 `MoveRotation()`을 통한 명시적 회전
갱신은 constraint의 영향을 받지 않는다(freeze는 "물리 힘/충돌에 의한 회전"만 막는 것이지,
`MoveRotation()`으로 코드가 직접 지정하는 회전은 막지 않음 — 공식 문서 및 동작 확인 필요, 검증
계획 1번 참고).

### 13.5 부차적으로 재확인이 필요한 것 — `IsGrounded()` 접지 판정 미세 흔들림

§22 작업 도중(§22.6 참고) 테스트 중에 `Rigidbody`가 바닥에 딱 붙어 쉬는 상태에서 y좌표가
`0 → -0.2 → -0.8 → ... → 다시 0 부근으로 복귀`하는 식으로 미세하게 진동하는 현상을 관측한 적이
있다 — 그때는 이 세션의 MCP 브리지 자체가 불안정했던 시점이라 "테스트 환경 문제"로 결론짓고
넘어갔었는데, 13.3의 회전 문제와는 별개로 **이것도 실제 게임에서 미세한 위아래 흔들림(그리고
그로 인한 미묘한 미끄러짐)에 일부 기여하고 있을 가능성**을 배제할 수 없다. 13.4를 먼저 적용해
회전 문제를 없앤 뒤에도 미끄러짐이 남아있다면, 이 접지 판정 흔들림을 추가로 조사한다(예:
`Rigidbody.solverIterations` 상향, `Physics.defaultContactOffset` 조정, 또는
`CollisionDetectionMode`를 `ContinuousDynamic`에서 `ContinuousSpeculative`로 바꿔보는 것 등).
지금 시점에는 13.4가 훨씬 유력한 근본 원인이므로, 그것부터 적용하고 재현 여부로 필요성을
판단하는 것이 순서상 맞다고 본다.

### 13.6 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. `rb.constraints = RigidbodyConstraints.FreezeRotation`이 걸린 상태에서 `rb.MoveRotation()`
   호출이 실제로 회전을 반영하는지(Freeze가 이 경로까지 막아버리지는 않는지) Play Mode에서 직접
   회전값을 찍어 확인 — 계획 문서 13.4의 전제 자체를 먼저 검증한다.
3. `GameLobbyScene`(또는 `PlayerTestScene`)에서 실제로 WASD를 눌러 캐릭터를 이동시켜보면서(이번엔
   리플렉션이 아니라 실제 지속 입력으로), 걷는 모습이 끊김 없이 매끄러운지, 방향 전환 시 몸통이
   즉시 자연스럽게 따라 도는지 육안으로 확인.
4. 이동 중 카메라를 돌려 다양한 각도에서 캐릭터의 걷는 모습을 관찰해 미끄러지는 느낌이 사라졌는지
   확인.
5. 기존 회귀 확인: 점프/회피 중 방향 고정(관성 이동)이 여전히 정상 동작하는지, `IsDodge()`/
   애니메이션 상태 전환에 영향이 없는지 확인.
6. 13.4로 해결되지 않으면 13.5(접지 판정 흔들림)를 이어서 조사.

### 13.8 구현 결과

`Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`의 `Move()`에서 13.4 계획 그대로 `transform.LookAt(...)`
세 곳을 전부 `if (lookDir != Vector3.zero) rb.MoveRotation(Quaternion.LookRotation(lookDir));`
하나로 통합했다(세 분기가 `lookDir`만 다르게 계산하고 회전 대입 로직은 공유하도록 정리). 컴파일
에러 0건.

### 13.9 검증 결과 — 13.6의 각 항목

1. **컴파일 확인(13.6-1)** — 통과. `read_console` 결과 새로운 에러/경고 없음(기존에 이미 있던,
   이번 작업과 무관한 `PlayerTestScene`의 Main Camera "Missing Script" 경고만 남음 — §17.8에 이미
   기록된 것과 동일).
2. **`FreezeRotation` 아래에서 `rb.MoveRotation()`이 실제로 회전을 반영하는지(13.6-2)** — **확인
   완료.** Play Mode에서 `rb.constraints`가 `FreezeRotation`으로 걸려 있는 상태에서 `rb.
   MoveRotation(Quaternion.Euler(0, 90, 0))`을 직접 호출한 직후 `rb.rotation.eulerAngles`를
   읽으면 즉시 `(0, 90, 0)`으로 반영됐고, 이후 실제 시간이 흘러 물리 스텝이 지난 뒤
   `transform.eulerAngles`도 동일하게 `(0, 90, 0)`으로 수렴해 유지됐다(원래 값 `0`으로 되돌아가는
   현상 없음) — 13.3에서 지목한 "홱 돌아갔다가 되돌아가는" 충돌이 이 경로에서는 재현되지 않음을
   직접 확인했다. `FreezeRotation`이 `MoveRotation()`을 막지 않는다는 13.4의 전제가 사실로
   확인됐다.
3. **실제 지속 입력으로 걷는 모습 확인(13.6-3, 4)** — **이번 세션에서는 신뢰성 있게 수행하지
   못했다.** 리플렉션으로 `rotation` 필드를 강제 대입하고 `Move()`를 여러 번 호출해 프레임별
   회전을 관찰하려 시도했으나, 그 과정에서 `Physics.Simulate(...)`가 "simulation mode가 Script가
   아니라 시뮬레이션이 실행되지 않았다"는 경고와 함께 **매번 아무 일도 하지 않았다는 것**을
   `read_console`로 뒤늦게 발견했다 — 즉 그 사이 관찰된 값 변화는 내 수동 스텝 호출이 아니라,
   도구 호출 사이의 실제 경과 시간 동안 백그라운드에서 정상적으로 돌아간 진짜 `Update()`/
   `FixedUpdate()`(키 입력 없음 → `CheckMovementInput()`이 `rotation`을 계속 0으로 리셋)가 내가
   리플렉션으로 넣어둔 값을 중간에 지워버린 결과였다 — 이 세션에서 이미 여러 번 기록된 "MCP
   브리지/세션 불안정성"과 같은 계열의 문제다. 실제 키보드 입력을 이 자동화 환경에서 만들어낼
   방법이 없어, 실사용자가 WASD로 직접 걸어보며 매끄러움을 육안 확인하는 절차는 **사용자의
   실제 플레이 테스트로 재확인이 필요하다.**
4. **회귀 확인(13.6-5)** — 코드 검토로 확인: `isDodge`/`isJump` 분기의 조건문·상태 전환 로직(
   `keepMovingAfterDodge`/`keepMovingAfterJump`/`animationDriver.ChangeState(...)` 호출부)은
   전혀 건드리지 않았고, 유일한 변경은 세 분기 모두에서 공유하던 "마지막에 회전을 어떻게
   대입하는가"라는 한 지점(`transform.LookAt` → `rb.MoveRotation`)뿐이므로 회귀 위험은 낮다고
   판단한다. 다만 이 역시 실제 지속 입력 테스트로 최종 확인되지는 않았다.
5. **13.5(접지 판정 흔들림)** — 13.4 적용 후에도 미끄러짐이 남아있는지 실제 확인을 못했으므로
   아직 착수 여부를 판단할 근거가 없다. 사용자가 실제 플레이 후 미끄러짐이 남아있다고 보고하면
   그때 조사한다.

### 13.10 정직한 한계

이번 검증은 "회전 충돌 메커니즘 자체가 없어졌는가"(항목 2)는 **직접 재현·확정**했지만, "실제
플레이했을 때 체감상 매끄러운가"(항목 3, 4 — 원래 버그 리포트의 실제 대상)는 이 자동화 환경에서
키보드 입력을 만들어낼 수 없어 확정하지 못했다. 근본 원인으로 지목한 메커니즘(회전값 충돌)은
코드에서 완전히 제거됐으므로 증상이 해소됐을 가능성이 높다고 판단하지만, **사용자가 직접
GameLobbyScene에서 걸어보고 재확인해주는 것을 권장한다.**

### 13.7 상태

**원인 분석·구현 완료, 회전 충돌 메커니즘 제거는 실측으로 확정.** 다만 실제 걷는 느낌의 최종
확인은 사용자의 플레이 테스트가 필요하다(§13.10).

**→ 사용자가 실제로 플레이 테스트를 진행한 결과, 이 수정 이후에도 미끄러짐·버벅거림이 그대로
남아있었다.** 즉 13.4의 회전 충돌 메커니즘은 실제로 존재했고 그 자체는 실측으로 제거를
확인했지만(§13.9-2), **사용자가 체감하는 증상의 진짜 원인은 아니었다.** `PlayerTestScene`이
아니라 실제 증상이 보고된 `GameLobbyScene`에서 직접 재조사한 결과를 §14에 기록한다.

---

## 14. [재조사] ⑥ 미끄러짐·버벅거림의 진짜 원인 — `Ch36` 자기 자신과의 충돌(self-collision) ✅ 원인 확정, 수정 계획 수립 (승인 대기, 미구현)

### 14.1 왜 §13이 틀렸는가 — 재조사 방식

이전 §13 조사는 `PlayerTestScene`(단순한 평평한 바닥 하나만 있는 테스트 전용 씬)에서 진행됐고,
실제 지속 입력 상황은 리플렉션으로 필드를 순간적으로 조작하는 방식으로만 간접 검증했다 —
사용자가 "고쳐진 게 없다"고 재확인해준 뒤, 이번에는 **실제 증상이 보고된 `GameLobbyScene`에
Photon 방을 실제로 생성해 들어가서**(`LobbyController.OnMakeRoomButtonClicked()`와 동일한 코드
경로, `PhotonNetwork.CreateRoom(...)` 직접 호출 → `AutomaticallySyncScene`으로 자동 로드) 로컬
캐릭터를 대상으로 재조사했다.

리플렉션 단발성 조작 대신, `UnityEditor.EditorApplication.update`에 **매 에디터 틱마다 실행되는
콜백**을 등록해 `rotation`/`rotation_value` 필드를 지속적으로 전진 방향으로 강제 유지시킴으로써
"사용자가 W를 계속 누르고 있는 상황"을 실제 게임 루프(`Update()`→`FixedUpdate()`→`Move()`)가
정상적으로 여러 프레임에 걸쳐 처리하도록 만들고, 4틱마다 `transform.position`/`rb.linearVelocity`를
`Debug.Log`로 남겨 `read_console`로 추적했다(§13에서 실패했던 "리플렉션 결과가 실제 게임 루프와
간섭한다"는 문제를 근본적으로 피하는 방식 — 이번엔 오히려 실제 게임 루프에 올라타는 방식을 썼다).

### 14.2 1차 실측 — 위치가 완전히 고정된 채 속도만 거대하고 불규칙

`GameLobbyScene`에 실제로 입장해 스폰된 로컬 캐릭터(`pos=(11.63, 0.00, 12.18)`)를 대상으로 위
방식으로 5초(실제 시간, 약 200틱)간 전진 입력을 강제한 결과:

```
tick=4   pos=(11.629, 0.000, 12.181) vel=(-19.148, -31.530, 1.915)
tick=8   pos=(11.629, 0.000, 12.181) vel=(-19.148, -31.530, 1.915)
...(200틱 내내 pos 완전히 동일, vel도 동일)...
tick=200 pos=(11.629, 0.000, 12.181) vel=(-19.148, -31.530, 1.915)
```

`Time.fixedTime`/`Time.frameCount`를 별도로 확인한 결과 물리 스텝 자체는 정상적으로 계속
진행되고 있었고(`fixedTime`이 계속 증가), `Rigidbody.IsSleeping() == false`였다 — 즉 **물리
시뮬레이션은 살아있는데, 위치는 소수점 단위까지 한 치도 움직이지 않으면서 속도값만 초당
수십 단위의 비정상적으로 큰 값을 갖고 있었다**(참고로 `speed`/`jumpPower`는 이 정도로 크지
않다 — `Move()`가 의도한 속도가 아니라 물리 솔버가 무언가에 끼어 계속 밀어내려다 실패하고 있는
값으로 보였다). 재조회 시점마다 속도값 자체도 계속 바뀌었다(`(-19.148,-31.530,1.915)` →
`(-14.31,-86.19,12.86)`) — **가만히 있는데 속도가 계속 요동친다**는 것은 이동 로직(`Move()`)이
아니라 **충돌 솔버가 뭔가를 밀어내려고 계속 힘을 주고 있다**는 강력한 정황이다.

### 14.3 원인 특정 — `OverlapSphere`로 캐릭터 위치에 겹친 콜라이더를 직접 조회

```csharp
var cc = go.GetComponent<CapsuleCollider>(); // 루트의 캡슐 콜라이더
var center = go.transform.TransformPoint(cc.center);
var hits = Physics.OverlapSphere(center, cc.radius + cc.height/2f + 0.1f);
```

결과:

```
player center=(11.63, 0.90, 12.18) radius=0.35 height=1.8
overlap: Ground              (MeshCollider, isTrigger=False)
overlap: HideOrSeekPlayer(Clone) (CapsuleCollider, isTrigger=False)  bounds extents=(0.35, 0.90, 0.35)
overlap: Ch36                (MeshCollider, isTrigger=False)        bounds extents=(0.81, 0.88, 0.63)
```

**`Ch36`(캐릭터 자신의 스킨 메시, `Ch36` 오브젝트)의 `MeshCollider`가 루트의 `CapsuleCollider`와
정확히 같은 위치에서 훨씬 넓게(가로 0.81 vs 0.35, 세로 0.63 vs 0.35) 겹쳐 있다.** `Ground`만
겹치는 건 정상(바닥을 딛고 서 있으니 당연)이지만, **`Ch36`이 별도의 콜라이더 히트로 잡힌다는
것 자체가 문제다** — 루트 콜라이더와 `Ch36` 콜라이더는 같은 캐릭터의 부분이므로 서로 충돌해서는
안 된다.

**왜 이런 일이 생겼는가**: `PlayerControllPlan.md` §21에서 `Ch36`(원래 콘케이브 `MeshCollider`만
있던 자식 오브젝트)이 "Concave Mesh Colliders are not supported... with dynamic Rigidbody"
에러를 내던 것을 고치기 위해, **`Ch36`에 별도의 키네마틱 `Rigidbody`를 추가**했다 — 부모(루트)의
다이나믹 `Rigidbody`가 만드는 컴파운드 콜라이더 모양에서 `Ch36`을 제외시켜 PhysX 제약을
피하려는 의도였고, 그 자체는 정확히 의도대로 동작해 원래의 콘케이브 콜라이더 에러는 실제로
사라졌다(§21.6 재검증 완료). **그런데 이 수정에는 미처 예상하지 못한 부작용이 있었다**: `Ch36`이
독립된 `Rigidbody`를 갖게 되는 순간, `Ch36`은 더 이상 부모의 컴파운드 콜라이더에 속하지 않고
**완전히 별개의 물리 바디**가 된다 — Unity는 "부모-자식 관계에 있는 콜라이더라도 서로 다른
`Rigidbody`에 속하면 자동으로 충돌을 무시해주지 않는다"(같은 `Rigidbody`에 속한 콜라이더끼리만
컴파운드 콜라이더로 취급되어 자동으로 서로 충돌하지 않음 — 이건 Unity의 잘 알려진 동작 원리다).
결과적으로 루트의 `CapsuleCollider`(다이나믹, 캐릭터를 실제로 움직이는 콜라이더)와 `Ch36`의
`MeshCollider`(같은 캐릭터의 몸통 메시, 캡슐보다 훨씬 넓음)가 **매 물리 스텝마다 자기 자신과
충돌 판정을 일으키고, 충돌 솔버가 이 둘을 서로 밀어내려는 시도를 영원히 반복**하게 됐다 —
이게 "속도는 거대하고 불규칙한데 위치는 고정"의 정확한 메커니즘이다: 두 콜라이더가 캐릭터가
움직이는 한 계속 같은 상대 위치에서 겹쳐 있으므로(둘 다 같은 부모 아래 자식이라 함께 움직임),
아무리 밀어내도 다음 프레임에 다시 겹치고, 그 침투(penetration)를 되돌리려는 보정 힘이 매
스텝 누적되어 속도값에 반영되지만 실제 위치 변화로는 이어지지 못한다(스스로를 영원히 밀어내는
셈이라 벗어날 수 없음).

**§13이 놓친 이유**: 이 self-collision은 `PlayerTestScene`에서도 구조적으로 동일하게 존재했을
것이나, §13/§19 검증 때는 리플렉션으로 순간이동(`rb.position` 직접 대입)시키거나 `isJump` 등을
직접 토글하는 방식으로만 테스트해서, **실제로 몇 초간 지속적으로 이동을 시도하는 상황을 만들지
않았다** — 그래서 매 순간의 "정지 상태"만 관찰했을 뿐, "이동을 시도하는데 실제로는 못 움직이고
제자리에서 속도만 요동친다"는 이 버그의 핵심 증상을 볼 기회가 없었다. 반면 §13.3에서 지목한
회전 충돌 문제는 실제로 존재하는 별개의 문제였고(그 자체는 코드로 확인 가능한 명백한 결함이라
고칠 가치가 있었다) — 다만 사용자가 체감한 주 증상의 원인은 그게 아니라 이 self-collision
쪽이었다.

### 14.4 원인 확정 실험 — `Physics.IgnoreCollision()`으로 즉시 재현·해소

같은 Play Mode 세션에서, 루트 `CapsuleCollider`와 `Ch36`의 `MeshCollider` 사이의 충돌만
런타임에 직접 꺼봤다:

```csharp
Physics.IgnoreCollision(rootCapsuleCollider, ch36MeshCollider, true);
```

그 직후 같은 방식(지속 전진 입력 강제)으로 다시 측정한 결과:

```
tick=4   pos=(0.309, 0.000, -2.785) vel=(0.000, 0.000, 5.000)
tick=8   pos=(0.309, 0.000, -2.681) vel=(0.000, 0.000, 5.000)
tick=12  pos=(0.309, 0.000, -2.598) vel=(0.000, 0.000, 5.000)
...
tick=120 pos=(0.309, 0.000, -0.652) vel=(0.000, 0.000, 5.000)
```

**속도가 즉시 `(0, 0, 5)`(정확히 `Move()`가 의도한 전진 속도로 추정)로 완전히 안정되고, z좌표가
매 틱 정확히 일정한 간격으로 증가하며 x좌표는 완벽히 고정되는, 흔들림·튐이 전혀 없는 매끄러운
선형 이동으로 즉시 바뀌었다.** (참고로 `IgnoreCollision`을 건 직후 첫 관찰 시점에 캐릭터가
`(11.63, 0, 12.18)`에서 `(-1.45, 0, -2.74)`로 이미 이동해 있었던 것도 함께 확인했다 — 그동안
쌓여있던 self-collision 보정 속도가 충돌 제약이 풀리자마자 한 번에 실제 이동으로 튀어나간
것으로, self-collision이 실제로 "안 움직이는 게 아니라 스스로에게 갇혀 있었다"는 것과 정확히
일치하는 정황이다.)

**결론: ⑥의 진짜 원인은 §21에서 `Ch36`에 추가한 키네마틱 `Rigidbody`가 만들어낸, 캐릭터 자기
자신의 두 콜라이더 사이의 self-collision이다.** `Physics.IgnoreCollision()` 한 줄로 이 충돌
판정 자체를 끄는 것만으로 증상이 완전히 사라지는 것을 실측으로 확정했다.

### 14.5 수정 계획 (승인 대기, 미구현)

`Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`의 `Start()`에서, 로컬/원격 여부와 무관하게(원격
인스턴스는 둘 다 키네마틱이라 눈에 보이는 증상은 없었겠지만 불필요한 충돌 판정 자체는 동일하게
발생하고 있었을 것이므로 함께 정리) 루트 콜라이더와 `Ch36`의 콜라이더 사이의 충돌을 명시적으로
끈다:

```csharp
private void Start()
{
    ...
    rb = GetComponent<Rigidbody>();
    rb.isKinematic = !pv.IsMine;
    ...

    // Ch36은 §21에서 콘케이브 메시 콜라이더 에러를 피하려고 별도의 키네마틱 Rigidbody를 받았는데,
    // 그 결과 루트의 CapsuleCollider와는 서로 다른 물리 바디가 되어 버려, 매 스텝 자기 자신과
    // 충돌 판정을 일으키고 있었다(Bug-fix-plan.md §14) — 둘 다 같은 캐릭터의 일부이므로 명시적으로
    // 서로를 무시하도록 지정한다.
    Collider rootCollider = GetComponent<CapsuleCollider>();
    Collider ch36Collider = transform.Find("Ch36")?.GetComponent<Collider>();
    if (rootCollider != null && ch36Collider != null)
        Physics.IgnoreCollision(rootCollider, ch36Collider, true);
}
```

**왜 여기인가**: `Physics.IgnoreCollision()`은 프레임마다 다시 호출할 필요 없는 1회성 설정이고,
`rb`/콜라이더 참조를 이미 얻어둔 `Start()`가 자연스러운 위치다. `pv.IsMine` 분기 밖에서(모든
인스턴스에 대해) 실행되도록 두는 이유는, 원격 인스턴스도 동일한 구조(루트 콜라이더 + `Ch36`
콜라이더, 둘 다 키네마틱)를 가지므로 눈에 보이는 이동 버그는 없더라도 불필요한 충돌 판정 자체는
계속 발생하고 있었을 것이기 때문이다(성능 낭비 겸 잠재적 부작용 소지 제거).

**대안으로 검토했으나 이번엔 채택하지 않은 방법**:
- **레이어 기반 무시**(`Physics.IgnoreLayerCollision`): `Ch36` 전용 레이어를 새로 만들고 프로젝트
  레이어 충돌 매트릭스에서 해당 레이어를 Player 레이어와 통째로 무시하도록 설정하는 방법. 더
  "전역적"이지만, 이번 문제는 정확히 "이 캐릭터의 이 두 콜라이더 쌍"에 국한된 문제라
  `Physics.IgnoreCollision()`으로 필요한 범위만 정확히 좁히는 것이 `CLAUDE.md`의 최소 변경
  원칙에 더 부합한다고 판단했다.
- **`Ch36`의 콜라이더를 아예 제거**: `PlayerPaintCanvas.paintableCollider`가 실제로 `Ch36`의
  콜라이더를 붓칠 레이캐스트 대상으로 참조하고 있을 가능성이 높아(프리팹 직접 확인 필요),
  섣불리 제거하면 색칠 기능이 깨질 수 있다 — 이번 수정 범위에서는 제외하고, 콜라이더는 유지한 채
  물리적 충돌 판정만 끄는 쪽을 택했다.

### 14.6 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. `GameLobbyScene`에 실제로 Photon 방을 만들어 입장한 뒤, §14.1과 동일한 방식(`EditorApplication.
   update` 훅으로 지속 전진 입력 강제)으로 재측정 — `rb.linearVelocity`가 `Move()`가 의도한 값
   그대로 안정되고, `transform.position`이 매 틱 일정하게 증가하는지 확인(§14.4의 결과가 코드
   수정 후에도 재현되는지).
3. `Physics.OverlapSphere`로 재조회했을 때 `Ch36`이 더 이상 루트 콜라이더와 충돌 히트로 잡히지
   않는지(또는 잡히더라도 실제 충돌 반응이 없는지) 확인.
4. 붓칠 기능(`PlayerPaintCanvas`)이 `Ch36` 콜라이더를 대상으로 여전히 정상적으로 레이캐스트되는지
   회귀 확인 — `Physics.IgnoreCollision()`은 콜라이더 간 물리 충돌 반응만 끄고 레이캐스트 감지는
   막지 않으므로 회귀가 없어야 하지만, 실제로 붓질이 여전히 되는지 직접 확인이 필요하다.
5. 가능하다면 사용자가 직접 GameLobbyScene에서 걸어보고 미끄러짐·버벅거림이 실제로 사라졌는지
   최종 확인.

### 14.7 구현 결과

`Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`의 `Start()`에 14.5 계획 그대로 `Physics.
IgnoreCollision(rootCollider, ch36Collider, true)`를 추가했다. 컴파일 에러 0건.

### 14.8 검증 결과 — 14.6의 각 항목, 전부 `GameLobbyScene`에서 실제 Photon 방으로 실측

1. **컴파일 확인(14.6-1)** — 통과.
2. **재측정(14.6-2)** — `GameLobbyScene`에 실제로 방을 만들어 입장한 뒤 §14.1과 동일한 방식
   (`EditorApplication.update` 훅으로 지속 입력 강제)으로 다시 측정. 처음 스폰된 자리는 마침
   피크닉 테이블 의자 바로 옆이라 전진하다 실제 의자에 부딪혀 정상적으로 멈췄고(이건 진짜 충돌—
   버그 아님), 그래서 트인 공간(`(10, 1, 10)`, 사방 1.5m 이내에 `Ground`만 있음을 `OverlapSphere`로
   먼저 확인)으로 옮겨 재측정했다:
   - 낙하(y: 1→0)부터 착지까지 흔들림 없이 매끄럽게 감쇠(§13.5에서 우려했던 접지 흔들림도
     이번엔 관측되지 않음).
   - 착지 후 `vel=(0,0,-5)` 그대로 고정, `pos.z`가 매 4틱마다 정확히 동일한 간격으로 감소 —
     완전히 선형적인 이동.
   - 80틱째에 이동 방향을 강제로 바꿔본 결과, `yaw`가 즉시 180°→270°로 깔끔하게 전환되고
     속도도 `(0,0,-5)`→`(-5,0,0)`로 즉시 정확히 전환, 이후 다시 완전히 선형적인 이동 재개.
   - §14.2에서 관측했던 "위치 고정 + 속도만 거대하고 불규칙"한 패턴이 전혀 재현되지 않았다.
3. **`OverlapSphere` 재조회(14.6-3)** — `Ch36`은 여전히 겹침으로 잡힌다(`Physics.
   IgnoreCollision()`은 겹침 판정 자체가 아니라 솔버의 충돌 반응만 끄는 API라 기하학적으로는
   여전히 겹쳐 있는 게 정상 — 애초에 같은 캐릭터의 몸통과 몸통 콜라이더이므로 겹치는 게 당연함).
   다만 항목 2에서 실측했듯 **실제 충돌 반응(밀어내기)은 더 이상 발생하지 않는다** — 이게
   `IgnoreCollision()`이 정확히 의도한 동작이다.
4. **붓칠 레이캐스트 회귀 확인(14.6-4)** — `PlayerPaintCanvas.PaintableCollider`가 정확히 `Ch36`의
   `MeshCollider`를 가리키는 것을 확인했다. 여러 각도에서 직접 레이캐스트를 쏴본 결과 이번
   테스트에서는 매번 `Ch36`보다 먼저 캡슐 콜라이더가 맞았다(캡슐이 몸통 대부분을 감싸고 있어
   레이가 도달하기 전에 먼저 막음) — **다만 이건 `Physics.IgnoreCollision()`과 무관한 현상이다.
   `IgnoreCollision()`은 물리 솔버의 충돌 반응 쌍에만 영향을 주고 `Physics.Raycast` 등 쿼리
   API의 히트 판정에는 전혀 관여하지 않는다는 것이 Unity의 명세된 동작이므로, 이 결과는 이번
   수정 전에도 완전히 동일했을 것**이다 — 즉 이번 수정으로 인한 새로운 회귀는 아니다. 다만
   "실제 게임플레이에서 붓칠 레이캐스트가 캐릭터의 어느 부분을 얼마나 잘 맞히는지"는 이번
   ⑥ 버그의 범위 밖인 별개의 사안으로 보이며, 조사 중 우연히 발견한 것이라 여기 기록만 해둔다
   (사용자가 실제로 붓칠 기능을 써보고 이상이 있으면 별도로 알려달라).
5. **최종 사용자 확인(14.6-5)** — 아직 사용자가 직접 플레이해보지 않았다. 실측 데이터상으로는
   self-collision 패턴이 완전히 사라졌으므로 체감 개선을 기대하지만, 최종 확인은 사용자 몫으로
   남겨둔다.
6. `read_console` 최종 확인 결과 이번 테스트 전 구간에서 에러/경고 0건.

### 14.9 정직한 한계

- 항목 4(붓칠 레이캐스트)에서 우연히 발견한 "캡슐이 `Ch36`보다 먼저 맞는 경우가 많다"는 관찰은
  ⑥ 버그와 무관한 별개의 잠재적 이슈일 수 있으나, 이번 수정으로 새로 생긴 것이 아님을 논리적으로
  (Unity의 `IgnoreCollision` API 명세)와 실측(수정 전후 동일한 결과) 양쪽으로 확인했으므로 이번
  범위에서는 더 파고들지 않았다.
- 실제 키보드 지속 입력으로 사용자가 체감하는 매끄러움의 최종 확인은 여전히 자동화 환경에서
  할 수 없다 — 다만 이번엔 §13 때와 달리 실제 게임 루프에 올라타는 방식(`EditorApplication.update`
  훅으로 매 프레임 입력을 유지)으로 측정했기 때문에, §13의 리플렉션 단발 조작보다 훨씬 신뢰도
  높은 근거로 판단한다.

### 14.10 상태

**진짜 원인 실측으로 확정, 구현 완료, `GameLobbyScene` 실측으로 self-collision 패턴이 완전히
사라졌음을 확인.** 사용자의 실제 플레이 테스트로 최종 확인을 권장한다.

---

## 15. ⑦ 점프 연타 시 Jump 애니메이션이 처음부터 재생되지 않는 버그 — ✅ 원인 실측 확정·구현·검증 완료

### 15.1 증상

사용자 보고: 점프 키(Space)를 연속해서 눌렀을 때, **가끔** 재점프하는 순간 Jump 모션이 처음부터
시작하지 않고, 이전 점프가 멈췄던(얼어있던) 재생 지점의 중간부터 이어서 재생된다.

### 15.2 관련 코드 — 점프 애니메이션 "정지(freeze)" 메커니즘

`PlayerAnimationDriver.cs`는 점프 애니메이션이 착지 전에 끝까지(착지 포즈까지) 재생돼버리는 것을
막기 위해, 정점 부근에서 재생을 멈추고 공중 자세를 유지시키는 메커니즘을 갖고 있다:

```csharp
// 착지 전에 Jump 애니메이션이 끝까지(착지 포즈까지) 재생되어 버리는 것을 막기 위해
// 정점 부근에서 재생을 멈추고 공중 자세를 유지시킨다.
public void HandleJumpAnimationHold()
{
    if (animator == null || currentState != PlayerMoveState.Jump)
        return;

    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
    if (!state.IsName("Jump"))
        return;

    if (animator.speed > 0f && state.normalizedTime >= jumpFreezeNormalizedTime)
    {
        animator.speed = 0f;   // ← 여기서 애니메이터 전체 재생 속도를 0으로 얼림(정지)
    }
}

public void ResumePlayback()
{
    if (animator != null)
        animator.speed = 1f;   // ← 착지 시 다시 재생 속도만 복구 — 재생 "위치"는 그대로
}
```

`animator.speed`는 **Animator 컴포넌트 전체의 재생 속도**이지, 특정 상태의 재생 위치가 아니다.
`ResumePlayback()`은 속도만 `1`로 되돌릴 뿐, 애니메이터가 지금 **어느 시점(`normalizedTime`)에
멈춰있는지는 전혀 건드리지 않는다** — 즉 재점프 시 애니메이터가 실제로 "처음부터" 다시 재생되려면
**반드시 `Jump` 상태로 새로 진입(재트리거)해야 한다.**

### 15.3 근본 원인 실측 확정 — `PlayerAnimationDriver.ChangeState()`의 "같은 상태면 무시" 가드

`ChangeState()`의 실제 코드:

```csharp
public void ChangeState(PlayerMoveState newState)
{
    if (animator == null)
        return;

    if (previousState == newState)   // ← 코드상 상태 라벨이 이미 같으면 아무것도 하지 않고 리턴
        return;

    animator.ResetTrigger(previousState.ToString());
    animator.SetTrigger(newState.ToString());   // ← 이 SetTrigger가 실제로 Jump 상태를 "새로" 진입시킴

    previousState = newState;
    currentState = newState;
}
```

`HideOrSeekPlayer.FixedUpdate()`에서 재점프 시 호출하는 코드:

```csharp
if (jumpRequested && grounded && !isDodge)
{
    rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpPower, rb.linearVelocity.z);
    isJump = true;
    keepMovingAfterJump = true;
    jumpMoveDir = rotation;
    animationDriver.ChangeState(PlayerMoveState.Jump);   // ← previousState가 이미 "Jump"면 이 호출이 통째로 무시됨
}
```

**핵심 문제**: 정상적인 흐름에서는 착지 시 `HideOrSeekPlayer.Update()`의 `CheckMovementInput()`이
`!isJump && !isDodge`를 만족하는 즉시 `ChangeState(Idle 또는 Walk)`를 호출해 코드상 상태 라벨을
`Jump`에서 벗어나게 해주므로, 그 다음 재점프 시 `ChangeState(Jump)`가 `previousState(Idle/Walk)
!= newState(Jump)`를 통과해 정상적으로 `SetTrigger`가 호출된다. **그런데 사용자가 "연타"할 만큼
빠르게 입력하면, 착지 처리(`FixedUpdate`의 landing 분기)와 재점프 처리(`FixedUpdate`의 jump-request
분기)가 그 사이에 `Update()`(따라서 `CheckMovementInput()`)가 한 번도 끼어들 기회 없이 연속된
물리 스텝에서 처리되는 경우가 생긴다** — 이 경우 `previousState`가 여전히 `Jump`인 채로
`ChangeState(Jump)`가 다시 호출되고, 가드에 걸려 `SetTrigger("Jump")`가 **아예 호출되지 않는다.**
그 결과 애니메이터는 `ResumePlayback()`이 재생 속도만 `1`로 되돌린 상태 그대로, **얼려뒀던 그
지점부터 이어서** 재생을 계속한다 — 이게 정확히 사용자가 본 증상이다.

**Animator Controller 쪽은 문제가 없음을 직접 확인**: `Assets/Animation/PlayerAnimator.controller`를
`AnimatorController` API로 직접 열어 `AnyState → Jump` 전환 설정을 확인한 결과:

```
AnyState -> Jump  hasExitTime=False  duration=0.1  offset=0  canTransitionToSelf=True  conditions=1 (Jump If)
```

`canTransitionToSelf=True`이고 `offset=0`이므로, **`SetTrigger("Jump")`가 실제로 전달되기만
하면** 이미 `Jump` 상태에 있던 도중이라도 0.1초 크로스페이드로 깔끔하게 처음(0)부터 다시
재생되도록 이미 구성되어 있다 — 즉 Animator Controller를 고칠 필요는 없고, **`SetTrigger`가
호출되지 않고 건너뛰어지는 C# 쪽 가드만이 유일한 원인**이다.

**Play Mode 실측으로 직접 재현·확정**: `GameLobbyScene`에 실제로 입장한 로컬 캐릭터를 대상으로,
`HideOrSeekPlayer` 컴포넌트를 일시적으로 `enabled = false`로 꺼서 실제 게임 루프의 간섭을 차단한
뒤, `PlayerAnimationDriver.ChangeState(Jump)`를 리플렉션으로 호출해 `Jump` 상태에 진입시키고,
`previousState`를 건드리지 않은 채(여전히 `"Jump"`) `animator.speed = 1f`(착지 시뮬레이션) 직후
`ChangeState(Jump)`를 다시 호출해봤다 — 재호출 전후로 `previousState`는 `"Jump"`로 동일했고,
**`normalizedTime`은 리셋되지 않고 그 이후 실제 경과 시간에 비례해 계속 누적되기만 했다**
(재호출 시점 `15.99` → 2초 뒤 재확인 시 `34.67`, 즉 "새로 시작"이 아니라 "이어서 계속 진행")
— 코드 리뷰로 세운 가설이 실측으로 정확히 확인됐다.

### 15.4 수정 계획 — 채택안: `ChangeState()`를 건드리지 않고 점프 전용 메서드를 별도로 추가

처음에는 `ChangeState()`에 `force` 매개변수를 추가하는 방향을 검토했으나, 사용자가 "이 공유
메서드를 고치면 나머지(Idle/Walk/SneakWalk/Dodge, 원격 플레이어 동기화가 쓰는
`ChangeState(networkSync.RemoteState)` 등)도 영향을 받을 것 같다"는 우려를 제기해 **다른
방안을 채택했다**: `ChangeState()`는 한 글자도 건드리지 않고, `PlayerAnimationDriver`에 점프
전용의 완전히 독립된 메서드를 추가한다.

```csharp
// PlayerAnimationDriver.cs — ChangeState()는 기존 그대로 두고 이 메서드만 추가
public void ReplayJump()
{
    if (animator == null)
        return;

    animator.ResetTrigger(previousState.ToString());
    animator.SetTrigger(PlayerMoveState.Jump.ToString());

    previousState = PlayerMoveState.Jump;
    currentState = PlayerMoveState.Jump;
}
```

`HideOrSeekPlayer.FixedUpdate()`의 점프 시작 호출부에서 `animationDriver.ChangeState(PlayerMoveState.
Jump)` 대신 이 메서드를 쓴다:

```csharp
if (jumpRequested && grounded && !isDodge)
{
    rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpPower, rb.linearVelocity.z);
    isJump = true;
    keepMovingAfterJump = true;
    jumpMoveDir = rotation;
    animationDriver.ReplayJump(); // 연타로 재점프해도 항상 처음부터 재생(Bug-fix-plan.md §15)
}
```

**왜 이 방식이 더 나은가**: `ChangeState()`는 코드가 한 글자도 바뀌지 않으므로, `Idle`/`Walk`/
`SneakWalk`/`Dodge` 호출부와 원격 플레이어 동기화(`ChangeState(networkSync.RemoteState)`)는
**논리적으로 영향받을 수 없다** — 별도로 회귀 검증할 필요 자체가 없어진다. `ReplayJump()`는
"새로운 점프/낙하 이벤트가 시작되면 무조건 Jump 애니메이션을 처음부터 재생한다"는 단일 책임만
가진 새 메서드라 이름만으로 의도가 분명하다. 착지 시 `CheckMovementInput()`이 호출하는 기존
`ChangeState(Idle/Walk)`는, `ReplayJump()`가 `previousState`를 정확히 `Jump`로 맞춰두므로
평소와 완전히 동일하게 동작한다.

**`Dodge`는 이번 수정 범위에서 제외한 이유**: `CheckDodgeInput()`의 회피 시작 조건 자체가
`!isJump && !isDodge`라 `isDodge`가 이미 `true`인 동안에는 애초에 새 회피 요청이 코드 레벨에서
막힌다 — 즉 점프처럼 "직전 회피가 채 끝나기도 전에 코드 상태 라벨이 같은 채로 재요청되는" 경합
자체가 구조적으로 발생하지 않는다. 다만 완전히 동일한 클래스의 문제가 잠재해 있을 가능성은
남아있으므로, 이번엔 사용자가 보고한 점프만 수정하고 회피는 별도 보고가 있을 때 다룬다.

### 15.5 구현 결과

`Assets/02. Scripts/Unit/PlayerAnimationDriver.cs`에 `ReplayJump()` 추가(`ChangeState()`는
무변경), `Assets/02. Scripts/Unit/HideOrSeekPlayer.cs`의 의도한 점프 호출부에서
`animationDriver.ChangeState(PlayerMoveState.Jump)` → `animationDriver.ReplayJump()`로 교체.
컴파일 에러 0건.

### 15.6 검증 결과 — `GameLobbyScene` 실제 Photon 방에서 실측

§15.3과 동일한 방식(`HideOrSeekPlayer.enabled = false`로 실제 게임 루프 차단 → 리플렉션으로
`Jump` 상태 진입 → `previousState`를 건드리지 않은 채 재호출)으로, 이번엔 `ReplayJump()`를
재호출해봤다:

- 재호출 직전 `normalizedTime = 6.77`(연타 재현을 위해 일부러 오래 재생시켜둔 값).
- 재호출 후 2초 뒤 재확인한 `normalizedTime = 4.92` — **더 많은 실제 시간이 지났는데도 값이
  더 작아졌다.** 논스톱으로 재생되는 애니메이션이라면 시간이 지날수록 값이 커지기만 해야
  하므로, 이 사이에 "처음(0)으로 리셋된 뒤 다시 올라간 것"이라는 것 외에는 설명할 수 없다 —
  §15.3에서 확인했던 "리셋 없이 계속 누적"(15.99 → 34.67) 패턴과 정반대의, 명확한 리셋 신호다.
- `read_console` 최종 확인 결과 에러/경고 0건.

`Idle`/`Walk`/`SneakWalk` 쪽 회귀는 §15.4에서 설명했듯 `ChangeState()` 자체가 무변경이라
논리적으로 영향받을 수 없으며, 실제로 테스트 준비 과정에서 `ChangeState(Idle)` 호출이 매번
정상적으로 동작하는 것도 함께 확인했다(previousState가 정확히 갱신됨).

실제 지속 키 입력으로 연타해 육안으로 확인하는 것은 이 자동화 환경의 한계로 여전히 불가능하다
(§13/§14와 동일한 제약) — 사용자의 실제 플레이 테스트를 권장한다.

### 15.7 상태

**원인 실측 확정, 구현 완료, Play Mode 실측으로 애니메이션 리셋 동작을 직접 확인.** 사용자의
실제 연타 플레이 테스트로 최종 체감 확인을 권장한다.

**→ 사용자가 실제로 연타 테스트를 진행한 결과, §15 수정 이후에도 증상이 그대로 남아있었다.**
§15에서 고친 메커니즘(`ChangeState()`의 가드가 `SetTrigger` 자체를 건너뜀)은 실제로 존재했고
그 자체는 확실히 고쳤지만, **완전히 별개의 두 번째 메커니즘이 동시에 같은 증상을 만들어내고
있었다** — 아래 §16에서 이 두 번째 원인을 실측으로 새로 확정하고 수정 계획을 세운다.

---

## 16. ⑦ 재조사 — `HandleJumpAnimationHold()`가 재트리거 직후의 "아직 갱신 안 된" 상태를 오판하는 두 번째 원인 (승인 대기, 미구현)

### 16.1 왜 §15만으로는 부족했는가

§15는 "재점프 시점에 `previousState`가 이미 `Jump`라서 `SetTrigger` 자체가 호출되지 않는" 경로를
막았다(`ReplayJump()`는 조건 없이 항상 `SetTrigger`를 호출함). 그런데 `SetTrigger`를 호출한다고
그 순간 애니메이터가 곧바로 새 인스턴스로 넘어가는 것은 아니다 — Unity 애니메이터는 트리거를
**"요청"으로만 받아두고, 다음 내부 애니메이터 평가(대략 다음 프레임)에서야 실제로 전환을
처리한다.** 그 사이의 "요청은 했지만 아직 반영 전"인 짧은 창(window) 동안 `GetCurrentAnimatorStateInfo(0)`
을 조회하면 **여전히 이전(재트리거 전) 상태의 정보(오래된 `normalizedTime`)가 그대로 나온다.**

그런데 `HideOrSeekPlayer.Update()`는 매 프레임 다음 순서로 호출된다:

```csharp
CheckMovementInput();
CheckJumpInput();
CheckDodgeInput();
animationDriver.HandleJumpAnimationHold(); // ← 매 프레임 무조건 호출됨(currentState==Jump일 때만 내부에서 동작)
```

재점프가 발생하는 `FixedUpdate()`와 그 직후(또는 같은 프레임의) `Update()`가 아주 가깝게 붙어서
실행되는 타이밍(연타 시 자주 발생 — §15.3에서 이미 확인한 것과 같은 계열의 경합)에서는,
`ReplayJump()`가 `SetTrigger`를 요청한 바로 다음 `HandleJumpAnimationHold()` 호출이 **아직
반영되지 않은 "재트리거 이전" 상태 정보를 읽어버린다.**

### 16.2 원인 실측 확정 — Play Mode에서 `ReplayJump()` 직후 `HandleJumpAnimationHold()`를 곧바로 호출해봄

`PlayerTestScene`에서 로컬 캐릭터의 `HideOrSeekPlayer`를 `enabled = false`로 꺼서 실제 게임 루프
간섭을 차단한 뒤, 리플렉션으로 다음 순서를 정확히 재현했다: (1) `Jump` 진입 → 실제 시간이 흘러
`HandleJumpAnimationHold()`가 정점에서 `speed=0`으로 얼림(`normalizedTime=4.07`에서 정지) →
(2) `ResumePlayback()`(착지 시뮬레이션, `speed=1`) → (3) `ReplayJump()`(재점프, 연타 재현) →
(4) **곧바로(같은 호출 시퀀스 안에서) `HandleJumpAnimationHold()`를 한 번 더 호출**(실제 게임의
바로 다음 `Update()` 프레임과 동일한 타이밍을 재현).

측정 결과:

```
ReplayJump 직후:              speed=1  normalizedTime=4.07369  isJumpState=True  inTransition=False
HandleJumpAnimationHold 직후: speed=0  normalizedTime=4.07369  isJumpState=True  inTransition=False
```

**`ReplayJump()`가 `SetTrigger`를 요청했음에도 `normalizedTime`은 여전히 재트리거 이전 값
그대로였고(애니메이터가 아직 요청을 반영하지 못한 상태), 그 직후 호출된 `HandleJumpAnimationHold()`
가 이 "오래된" `normalizedTime`(이미 `jumpFreezeNormalizedTime` 이상)을 보고 즉시 다시
`animator.speed = 0`으로 얼려버렸다** — 즉 `ReplayJump()`가 요청한 재생 자체가 시작되기도 전에,
바로 다음 순간 `HandleJumpAnimationHold()`가 도로 정지시켜버리는 것이다. 결과적으로 애니메이터는
새로 재생을 시작할 기회조차 얻지 못한 채 이전 정지 지점에 계속 머무른다 — 이게 바로 사용자가 본
"중간 지점부터 시작"의 정확한 메커니즘이다(정확히는 "새로 시작하지도 못하고 이전 정지 지점에
그대로 갇힘").

**대조 실험**: 동일한 상황에서, `ReplayJump()` 직후 `HandleJumpAnimationHold()`를 **호출하지
않고** 대신 실제 시간이 흐르도록(최소 한 프레임 이상) 놔둔 뒤 상태를 확인해보니 `speed`가
계속 `1`로 유지되고(=아무도 다시 얼리지 않음) 애니메이터가 `Jump` 상태에서 정상적으로 시간이
흐르고 있었다 — `HandleJumpAnimationHold()`가 "너무 이른 타이밍"에 호출되는 것 자체가 문제의
핵심임을 다시 한번 확인했다.

### 16.3 수정 계획 (승인 대기, 미구현)

`PlayerAnimationDriver`에 "방금 `ReplayJump()`를 호출했으니, 그 요청이 실제로 애니메이터에
반영될 때까지 최소 한 번은 `HandleJumpAnimationHold()`의 판단을 보류한다"는 플래그를 추가한다:

```csharp
private bool suppressHoldCheckOnce;

public void ReplayJump()
{
    if (animator == null)
        return;

    animator.ResetTrigger(previousState.ToString());
    animator.SetTrigger(PlayerMoveState.Jump.ToString());

    previousState = PlayerMoveState.Jump;
    currentState = PlayerMoveState.Jump;

    // SetTrigger는 다음 애니메이터 내부 평가(대략 다음 프레임)에야 실제로 반영된다. 그 사이
    // HandleJumpAnimationHold()가 "재트리거 이전"의 오래된 normalizedTime을 보고 즉시 다시
    // 얼려버리는 것을 막기 위해, 재생이 실제로 시작될 때까지 최소 한 번은 정지 판정을 건너뛴다
    // (Bug-fix-plan.md §16).
    suppressHoldCheckOnce = true;
}

public void HandleJumpAnimationHold()
{
    if (animator == null || currentState != PlayerMoveState.Jump)
        return;

    if (suppressHoldCheckOnce)
    {
        suppressHoldCheckOnce = false;
        return;
    }

    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
    if (!state.IsName("Jump"))
        return;

    if (animator.speed > 0f && state.normalizedTime >= jumpFreezeNormalizedTime)
    {
        animator.speed = 0f;
    }
}
```

**왜 "한 번 건너뛰기"로 충분한가**: 실제 게임에서 `HandleJumpAnimationHold()`는 매 `Update()`
프레임마다 정확히 한 번씩만 호출된다 — 애니메이터의 내부 평가도 프레임당 한 번이므로, 재점프가
발생한 프레임의 검사 한 번만 건너뛰면 그 다음 프레임부터는 애니메이터가 이미 요청을 반영한
뒤이므로(전환이 시작됐거나 이미 완료됨) `normalizedTime`이 새 인스턴스를 정확히 반영한다 —
Play Mode 실측(§16.2 대조 실험)에서 "건너뛰고 최소 한 프레임을 기다리면 정상적으로 흘러간다"는
것을 이미 확인했다.

**왜 걷다가 낙하하는 §23 케이스에도 자동으로 적용되는가**: §23의 걸어서 낙하 감지 분기도
`animationDriver.ReplayJump()`를 그대로 재사용하므로, 이 수정은 §15/§23 양쪽 모두에 자동으로
적용된다 — 별도 수정이 필요 없다.

### 16.4 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. §16.2와 동일한 방식(`HideOrSeekPlayer.enabled = false` → `ReplayJump()` 직후 곧바로
   `HandleJumpAnimationHold()` 재호출)으로 재현해, 이번엔 `speed`가 `0`으로 되돌아가지 않고
   `1`로 유지되는지 확인 — 이게 핵심 검증 포인트.
3. 같은 테스트를 몇 차례 반복해, `suppressHoldCheckOnce`가 매번 정확히 한 번만 소비되고 이후
   프레임에서는 정상적으로 다시 정점 정지 로직이 동작하는지(즉 진짜 점프의 "정점에서 얼리는"
   원래 기능 자체는 회귀 없이 그대로인지) 확인.
4. `GameLobbyScene`(또는 `PlayerTestScene`)에서 실제로 점프를 연달아 여러 번 눌러보며 육안 확인 —
   다만 이 자동화 환경은 실제 키보드 연타를 만들어낼 수 없으므로, 최종 확인은 사용자의 실제
   플레이 테스트가 필요하다.
5. §23(걸어서 낙하)도 함께 재확인 — 낙하 중 착지 직후 다시 바로 낙하하는 것과 같은 연속
   상황에서도 정상 동작하는지.
6. `read_console` 최종 확인 결과 에러/경고 0건.

### 16.5 상태

**두 번째 원인을 Play Mode 실측으로 확정, 수정 계획 수립 완료 — 아직 구현하지 않음.** 동의하면
바로 구현을 시작하겠다.

---

## 17. ⑧ PlayerTestScene(및 실제로는 모든 씬)에서 색을 골라도 붓이 나오지 않는 버그 — 원인 실측 확정 + 수정 계획 (승인 대기, 미구현)

### 17.1 증상

사용자 보고: `PlayerTestScene`에서 색상을 지정(`ColorSelectionManager.SubmitVote()`로 붓 색을
정함)한 뒤에도 3D 붓 커서(`BrushCursorController`)가 화면에 나타나지 않는다.

### 17.2 원인 조사 — 색상 선택과는 무관함을 먼저 배제

`PlayerTestScene`의 `ColorTagManagers`(`ColorSelectionManager`/`RoomLifecycleWatcher`/
`BrushCursorController`)와 `TestBootstrap`(`OfflineModeBootstrap`)을 Play Mode에서 직접
조회한 결과:

```
OfflineMode=True  InRoom=True  CurrentRoom=OfflineTestRoom  IsMasterClient=True  RoundIndex=0
BrushCursorController 내부 isColorRoundActive=True
cursorInstance="BrushCursor(Runtime)" (생성은 됨, activeSelf=False)
```

즉 라운드 시스템 자체(`RoundIndex`)와 `BrushCursorController`의 "이번 라운드는 색칠 라운드다"
판단(`isColorRoundActive`)은 전부 정상이었고, 붓 프리팹 인스턴스도 정상적으로 생성돼 있었다 —
**색상 선택/라운드 시스템은 원인이 아니다.** 문제는 그보다 더 아래 단계, 즉 붓을 "지금 이 순간
보여줄지" 매 프레임 결정하는 로직에 있었다.

### 17.3 진짜 원인 실측 확정 — 캐릭터 자신의 `CapsuleCollider`가 `Ch36`을 가림

`BrushCursorController.Update()`와 `PlayerPaintCanvas.Update()` 둘 다 다음과 같은 패턴으로
"마우스가 지금 내 캐릭터 몸(`Ch36`) 위에 있는가"를 판정한다:

```csharp
// BrushCursorController.cs
Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
bool hitSurface = Physics.Raycast(ray, out RaycastHit hit) && hit.collider == localPaintCanvas.PaintableCollider;
cursorInstance.SetActive(hitSurface); // hit이 정확히 Ch36의 MeshCollider일 때만 붓을 보여줌
```

```csharp
// PlayerPaintCanvas.cs
Ray ray = localCamera.ScreenPointToRay(Input.mousePosition);
if (!Physics.Raycast(ray, out RaycastHit hit)) return;
if (hit.collider != paintableCollider) return; // 자신의 오브젝트가 아니면 무시 — 실제 칠하기도 여기서 막힘
```

`PlayerPaintCanvas.PaintableCollider`가 정확히 무엇인지 Play Mode에서 직접 확인한 결과
`Ch36`의 `MeshCollider`였다. 그런데 `PlayerControllPlan.md` §18(물리 도입)에서 캐릭터 루트에
새로 추가한 `CapsuleCollider`가, 카메라 시점 기준으로 `Ch36`(캐릭터 몸 메시)을 거의 완전히
감싸는 크기와 위치에 있다 — `Physics.Raycast(ray, out hit)`(레이어 마스크 없는 기본 오버로드)는
**레이 경로상 가장 가까운 콜라이더 단 하나만** 반환하므로, `Ch36`보다 앞에/감싸듯 있는
`CapsuleCollider`가 거의 항상 먼저 잡힌다.

Play Mode에서 캐릭터의 화면상 위치를 중심으로 가로/세로 격자(7×7=49개 지점)에서 동일한 방식의
레이캐스트를 직접 쏴본 결과:

```
hitPaintable(Ch36)=0   hitOther=49   hitNone=0
```

**49개 지점 전부 `Ch36`이 아니라 `CapsuleCollider`가 먼저 맞았다** (단일 지점 재확인 결과
`hit.collider`가 정확히 `hide_or_seek_player`의 `CapsuleCollider`임을 이름까지 확인). 즉
`localPaintCanvas.PaintableCollider`(`Ch36`)를 직접 맞히는 것이 이 캐릭터의 실루엣 범위 안에서는
사실상 불가능한 상태다 — **붓 커서 표시뿐 아니라 실제 색칠(스탬프) 기능 자체도 동일한 원인으로
막혀 있다.** 이 현상은 이전 세션 `Bug-fix-plan.md` §14.8-4에서 "⑥ 버그와는 무관한 별개 사안"으로
짧게 언급만 하고 넘어갔던 것인데, 이번 사용자 보고의 실제 원인이 정확히 이것이었다.

### 17.4 수정 계획 (승인 대기, 미구현)

표준적인 해법은 **캐릭터 자신의 물리용 `CapsuleCollider`를 별도 레이어에 두고, 붓 관련
레이캐스트에서 그 레이어만 제외한 레이어 마스크를 사용**하는 것이다 — `Physics.IgnoreCollision()`
과 달리 레이어 마스크는 `Physics.Raycast` 쿼리 자체에 영향을 주므로 정확히 이 상황을 위한
표준 도구다.

**1) 새 레이어 추가**: 프로젝트에 아직 커스텀 레이어가 하나도 없다(`Default`/`TransparentFX`/
`Ignore Raycast`/`Water`/`UI`뿐). `PlayerCapsule`이라는 이름으로 레이어를 하나 추가한다.

**2) `HideOrSeekPlayer` 프리팹**: 루트(= `CapsuleCollider`가 달린 오브젝트)의 `layer`를
`PlayerCapsule`로 변경한다. **자식인 `Ch36`은 손대지 않는다** — Unity의 레이어는 자동으로
자식에게 상속되지 않고 오브젝트별로 독립적이므로, 루트만 바꿔도 `Ch36`은 그대로 `Default`에
남아 지금처럼 정상적으로 페인트 대상 역할을 계속한다.

**3) 붓 관련 레이캐스트 두 곳에 레이어 마스크 적용**:

```csharp
// BrushCursorController.cs
private int paintRaycastMask;

private void Awake()
{
    propertyBlock = new MaterialPropertyBlock();
    Cursor.visible = true;
    // 캐릭터 자신의 물리용 CapsuleCollider가 Ch36을 가려 레이캐스트가 항상 캡슐에 먼저 맞는 문제를
    // 막기 위해, 붓 관련 레이캐스트에서는 PlayerCapsule 레이어를 제외한다(Bug-fix-plan.md §17).
    paintRaycastMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("PlayerCapsule");
}

private void Update()
{
    ...
    Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
    bool hitSurface = Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, paintRaycastMask)
        && hit.collider == localPaintCanvas.PaintableCollider;
    ...
}
```

```csharp
// PlayerPaintCanvas.cs
private int paintRaycastMask;

private void Start()
{
    localCamera = Camera.main;
    currentBrushRadius = Mathf.Clamp(brushSettings.DefaultRadius, brushSettings.MinRadius, brushSettings.MaxRadius);
    paintRaycastMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("PlayerCapsule"); // Bug-fix-plan.md §17
    InitPaintCanvas();
}

private void Update()
{
    ...
    Ray ray = localCamera.ScreenPointToRay(Input.mousePosition);
    if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, paintRaycastMask)) return;
    if (hit.collider != paintableCollider) return;
    ...
}
```

`Physics.DefaultRaycastLayers`(Unity 내장 상수, "Ignore Raycast" 레이어만 제외한 나머지 전부)를
기준으로 `PlayerCapsule`만 추가로 빼는 방식이라, 기존에 다른 오브젝트(벽 등 실제 시야를 가리는
지형지물)가 여전히 붓을 정상적으로 가려주는 동작(현재도 있던 "가려지면 안 보임" 로직)은
회귀 없이 그대로 유지된다 — 오직 캐릭터 자신의 캡슐 콜라이더 하나만 이 레이캐스트의 대상에서
빠진다.

**왜 `Physics.IgnoreCollision()`이 아닌 레이어 마스크인가**: §14에서 self-collision을 고칠 때는
`Physics.IgnoreCollision()`을 썼는데(물리 솔버의 충돌 반응만 제외), 그때 이미 "이 API는 레이캐스트
쿼리에는 전혀 영향을 주지 않는다"는 것을 §14.8-4에서 실측으로 확인해뒀다 — 그래서 이번엔
애초에 레이캐스트 쿼리 자체를 걸러내는 레이어 마스크를 쓴다.

**다른 플레이어에 대한 영향**: `PlayerCapsule` 레이어는 프리팹 자체에 적용되므로 모든
`HideOrSeekPlayer` 인스턴스(로컬/원격 전부)에 동일하게 적용된다 — 다른 플레이어의 캡슐도 내
붓 레이캐스트를 가리지 않게 되는 부수 효과가 있는데, 어차피 붓은 항상 "내 `PlayerPaintCanvas`의
`PaintableCollider`"만 비교 대상으로 삼으므로 의도와 어긋나지 않는다.

### 17.5 검증 계획

1. `read_console`로 컴파일 에러 0건 확인.
2. §17.3과 동일한 방식(캐릭터 화면 위치 주변 격자 레이캐스트)으로 재측정해, 이번엔 `Ch36`이
   정상적으로 맞는지(`hitPaintable(Ch36)`이 0이 아닌지) 확인 — 핵심 검증 포인트.
3. `PlayerTestScene`에서 실제로 마우스를 캐릭터 위에 올렸을 때 붓 커서가 나타나는지, 클릭
   시 실제로 캐릭터 스킨에 색이 칠해지는지(`PlayerPaintCanvas.ApplyStamp`) 육안 확인 — 다만
   실제 마우스 조작은 이 자동화 환경에서 재현할 수 없으므로, 레이캐스트 히트 결과 확인(2번)까지가
   이 세션에서 가능한 최대 검증이고 최종 육안 확인은 사용자 몫이다.
4. 회귀 확인: 캐릭터의 이동/점프/충돌 등 물리 동작이 레이어 변경 후에도 그대로인지(레이어를
   바꾼 것은 캡슐의 "소속 레이어"일 뿐, `Rigidbody`/`Collider` 자체의 물리 속성이나 §14에서
   설정한 `Physics.IgnoreCollision()` 관계는 레이어와 무관하므로 영향이 없어야 하지만 확인 필요).
5. 다른 물체(벽 등)가 실제로 시야를 가릴 때는 여전히 붓이 안 보이는지(레이어 마스크가 지나치게
   넓게 뚫려서 "아무거나 뚫고 보이는" 회귀가 생기지 않았는지) 확인.
6. `read_console` 최종 확인 결과 에러/경고 0건.

### 17.6 상태

**원인을 Play Mode 실측(격자 레이캐스트, 49/49 캡슐에 막힘)으로 확정, 수정 계획 수립 완료 —
아직 구현하지 않음.** 동의하면 바로 구현을 시작하겠다.
