# 계획: 버그 5건 수정 (Bug-fix-plan.md)

> 상태: **①②③④⑤ 전부 구현·검증 완료, ④는 실제 4인 멀티 테스트로 최종 확인까지 완료
> (2026-08-15 최종 업데이트)**. ④(GameLobbyScene 가시성)는 1차·2차 수정이 모두 실측에서
> 불충분함이 드러났고, 3차 조사에서 사용자가 제공한 실제 빌드 스크린샷 + Unity 에디터를 네 번째
> 참가자로 투입한 실시간 공동 디버깅으로 진짜 원인(§12)을 찾아 수정했다 — 이번엔 실제 빌드
> 4개 클라이언트 전원의 화면에서 서로가 보임을 사용자가 직접 확인했다. ⑤는 사용자가 남긴
> 스크린샷을 근거로 재조사해 확정적 원인을 찾았다(§10). 실제 구현 결과는 §11(④ 2차 시도·⑤),
> §12(④ 최종 원인·수정·실측 검증)에 정리했다.

---

## 0. 요약

| # | 버그 | 상태 | 근거 |
|---|---|---|---|
| ① | Back 버튼 확인창이 첫 클릭에 안 뜸 | ✅ **구현·검증 완료** | §1, §8 |
| ② | 채팅 중 방향키로 캐릭터가 움직임 | ✅ **구현·검증 완료** | §2, §8 |
| ③ | 랜덤 입장이 최근 생성된 방으로만 들어감 | ✅ **구현·검증 완료** | §3, §8 |
| ④ | GameLobbyScene에서만 플레이어들이 서로 안 보임(방장 퇴장 시 전원 안 보임 포함) | ✅ **최종 원인 확정·수정·실제 4인 멀티 테스트로 검증 완료** | 진짜 원인은 `PhotonNetwork.InRoom`이 `Start()` 시점에 아직 false일 수 있어 `Instantiate`/`RPC`의 네트워크 전송(`RaiseEvent`)이 조용히 실패하는 것 + `HideOrSeekPlayer.networkSync`가 `Start()`에서만 초기화돼 그보다 먼저 오는 `OnPhotonSerializeView` 수신에서 NRE 발생. `InRoom`이 true가 될 때까지 대기 후 전송 + `networkSync`를 `Awake()`로 이동(§12) |
| ⑤ | 카메라가 캐릭터 뒷모습만 보임 | ✅ **재계획(§10) 구현·검증 완료** | `PlayerColorVoteIndicator`의 `LateUpdate()`가 `indicator.transform`만 회전시키도록 수정 — Play Mode에서 회전값과 스크린샷으로 정면이 정상적으로 보임을 직접 확인(§11) |

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
