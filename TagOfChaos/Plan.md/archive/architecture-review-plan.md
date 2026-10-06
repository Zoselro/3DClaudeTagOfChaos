# 계획: architecture-review.md 발견사항 4종 수정 + GameManager 책임 분리

> 상태: **✅ 구현 완료 (2026-08-15)**. `architecture-review.md`가 발견한 항목 중 사용자가 확정한
> 5가지 — ① Photon 콜백 이중 등록 제거, ② `PlayerPaintCanvas` 리소스 누수 방지, ③ 씬 이름
> 재발방지(상수화), ④ `RoundIndex` 등 CustomProperties 조회 중복 제거, ⑤ `GameManager`의
> 스폰/나가기 책임 분리 — 를 전부 계획대로 구현하고 Unity MCP를 통해 컴파일·Play Mode 검증까지
> 마쳤다. 계획 단계에서 작성한 아래 §1~§5는 실제 구현과 정확히 일치하며, 구현 과정에서 계획에
> 없던 추가 발견/조치는 **§9(구현 완료 보고)**에 정리했다.

---

## 0. 전체 요약

### 0.1 새로 생성되는 파일

| 파일 | 목적 | 관련 항목 |
|---|---|---|
| `Assets/02. Scripts/Core/SceneNames.cs` | 씬 이름 상수 (3개 도메인이 공유) | ③ |
| `Assets/02. Scripts/ColorTag/RoomState.cs` | Room CustomProperties 안전 조회 헬퍼 | ④ |
| `Assets/02. Scripts/GameManager/PlayerSpawner.cs` | 캐릭터 스폰 전담 (신규 분리) | ⑤ |
| `Assets/02. Scripts/GameManager/RoomExitController.cs` | 뒤로가기·확인창·방 나가기 전담 (신규 분리) | ⑤ |

`Core/`는 이번에 처음 만드는 도메인 폴더다 — `CLAUDE.md`의 `Assets/02. Scripts/{도메인}/` 규칙에
맞으며, 씬 이름은 `GameManager`/`ColorTag`/`Lobby` 세 도메인이 공통으로 참조하므로 특정 도메인에
종속시키지 않고 별도 폴더로 뺀다.

### 0.2 수정되는 파일

| 파일 | 변경 내용 | 관련 항목 |
|---|---|---|
| `ColorTag/BrushCursorController.cs` | `OnEnable` 삭제, `OnDisable`에서 중복 호출 제거 | ① |
| `Lobby/GameLobbyController.cs` | `OnEnable`/`OnDisable` 오버라이드 전체 삭제 | ① |
| `Lobby/LobbyController.cs` | `OnEnable`/`OnDisable` 오버라이드 전체 삭제, `"GameLobbyScene"` → `SceneNames.GameLobby` | ①③ |
| `ColorTag/PlayerColorDisplay.cs` | `OnEnable`/`OnDisable` 삭제, `RoundIndex` 조회를 `RoomState`로 교체 | ①④ |
| `ColorTag/PlayerColorVoteIndicator.cs` | `OnEnable`/`OnDisable` 오버라이드 전체 삭제 | ① |
| `ColorTag/PlayerPaintCanvas.cs` | `OnEnable`/`OnDisable` 삭제, `OnDestroy` 추가(누수 방지), `GetRoundIndex()`를 `RoomState`로 교체 | ①②④ |
| `ColorTag/RoomLifecycleWatcher.cs` | `OnEnable`/`OnDisable` 삭제, `"LobbyScene"`/`"GameLobbyScene"` → `SceneNames` | ①③ |
| `ColorTag/ColorSelectionManager.cs` | `RoundIndex`/`RoundEndTime` 조회를 `RoomState`로 교체 | ④ |
| `ColorTag/ColorSelectionPanel.cs` | `RoundIndex`/`RoundEndTime`/`ColorN` 조회를 `RoomState`로 교체 | ④ |
| `Lobby/GameLobbyController.cs` | `"GameScene"` → `SceneNames.Game` | ③ |
| `GameManager/GameManager.cs` | `CreatePlayer()`/뒤로가기 관련 필드·메서드 전부 제거, 채팅 전용으로 축소 | ⑤ |

### 0.3 씬(에디터) 작업이 필요한 항목

⑤번(책임 분리)만 씬 편집이 필요하다 — `GameLobbyScene`/`GameScene`의 기존 `GameManager`
오브젝트에 `PlayerSpawner`/`RoomExitController` 컴포넌트를 추가하고 필드를 재연결해야 한다.
자세한 내용은 §5.4에 정리했다. ①~④번은 순수 코드 변경만으로 끝난다(단, ①의 실제 효과는 Play
Mode에서 콜백이 1번만 실행되는지로 검증해야 하므로 §7에서 다룬다).

---

## 1. Photon 콜백 이중 등록 제거 (`architecture-review.md` §8.1) — ✅ 구현 완료

### 1.1 원인 재확인

`MonoBehaviourPunCallbacks`(Photon SDK, `PunClasses.cs:109`)의 `OnEnable()`/`OnDisable()`이 이미
`PhotonNetwork.AddCallbackTarget(this)`/`RemoveCallbackTarget(this)`를 호출한다. 그런데 아래 7개
파일이 `base.OnEnable()`을 호출한 뒤 **또다시** 같은 호출을 직접 반복하고 있어, 활성화된 동안
모든 Photon 콜백이 2번씩 실행된다(`LoadBalancingClient.cs:3814`의 `container.Add(target)`에
중복 검사가 없음을 직접 확인함 — `research.md`/`architecture-review.md` 작성 시 재확인 완료).

### 1.2 수정 방침

**중복 등록 줄만 제거하고 `base.OnEnable()`/`base.OnDisable()`은 그대로 둔다.** 오버라이드
안에 다른 로직이 없는 6개 파일은 오버라이드 자체를 통째로 삭제한다(어차피 `base` 호출만 남으면
오버라이드할 이유가 없다 — 상속만으로 기본 클래스의 구현이 그대로 적용됨). `BrushCursorController`
는 `OnDisable`에 `Cursor.visible = true;`라는 추가 로직이 있으므로 오버라이드 자체는 남기되 중복
호출 줄만 지운다.

### 1.3 파일별 변경

**`ColorTag/BrushCursorController.cs`** — `OnEnable` 오버라이드 삭제, `OnDisable`은 유지하되 수정:

```csharp
// 삭제: OnEnable() 오버라이드 전체
//   public override void OnEnable() { base.OnEnable(); PhotonNetwork.AddCallbackTarget(this); }

// 변경 후: OnDisable()은 남기되 RemoveCallbackTarget 호출만 삭제
public override void OnDisable()
{
    base.OnDisable();
    Cursor.visible = true;
}
```

**`Lobby/GameLobbyController.cs`**, **`Lobby/LobbyController.cs`**, **`ColorTag/PlayerColorDisplay.cs`**,
**`ColorTag/PlayerColorVoteIndicator.cs`**, **`ColorTag/PlayerPaintCanvas.cs`**,
**`ColorTag/RoomLifecycleWatcher.cs`** — 6개 파일 전부 동일하게, 아래 형태의 오버라이드를
**통째로 삭제**한다(다른 로직이 없으므로):

```csharp
// 삭제 대상 (6개 파일 공통)
public override void OnEnable()
{
    base.OnEnable();
    PhotonNetwork.AddCallbackTarget(this);
}

public override void OnDisable()
{
    base.OnDisable();
    PhotonNetwork.RemoveCallbackTarget(this);
}
```

삭제 후에도 `MonoBehaviourPunCallbacks` 상속은 그대로 유지한다(각 클래스가 오버라이드하는
`OnPlayerEnteredRoom`/`OnRoomPropertiesUpdate`/`OnEvent`/`OnLeftRoom` 등 실제 콜백 메서드들은
그대로 남는다 — 이번 변경은 등록 횟수만 1회로 바로잡는 것이지 콜백 수신 자체를 없애는 것이 아니다).

### 1.4 변경하지 않는 파일 (이미 정상)

`GameManager.cs`, `ColorSelectionManager.cs`, `ColorSelectionPanel.cs`, `ColorSwatchButton.cs`는
애초에 `OnEnable`/`OnDisable`을 오버라이드하지 않아(또는 `MonoBehaviourPunCallbacks`를 상속하지
않아) 이 버그가 없다 — 손대지 않는다.

---

## 2. `PlayerPaintCanvas` 리소스 누수 방지 (`architecture-review.md` §9.2) — ✅ 구현 완료

### 2.1 원인 재확인

`InitPaintCanvas()`가 캐릭터 1개당 512×512 `RenderTexture`를 `new RenderTexture(...)`+`.Create()`로
생성하지만, 이 인스턴스가 파괴될 때(씬 전환으로 인한 캐릭터 재스폰 등) `Release()`를 호출하는
코드가 없다(`OnDestroy` 오버라이드가 프로젝트 전체에 0건).

### 2.2 수정 내용

`PlayerPaintCanvas.cs`에 `OnDestroy()`를 추가한다:

```csharp
private void OnDestroy()
{
    if (PaintCanvas != null)
    {
        PaintCanvas.Release();
        PaintCanvas = null;
    }
}
```

`InitPaintCanvas()`에서 만든 `Material painted`(58~62행)는 `bodyRenderer.material = painted`로
할당되는 인스턴스 머티리얼인데, 이는 해당 `Renderer`(따라서 그 `GameObject`)가 파괴될 때 Unity가
함께 정리하므로 별도 해제 코드가 필요 없다 — 이번 변경은 `RenderTexture` 하나에만 한정한다
(범위를 넘는 정리는 하지 않음, §8 참고).

### 2.3 위치

`ApplyStamp()`의 `RenderTexture.GetTemporary`/`ReleaseTemporary` 페어(207~210행)는 이미 정확히
관리되고 있으므로 손대지 않는다 — 이번에 고치는 것은 인스턴스 수명 전체를 따라가는 **영구
`RenderTexture`**(`PaintCanvas` 필드) 하나뿐이다.

---

## 3. 씬 이름 재발방지 — `SceneNames.cs` 도입 (`architecture-review.md` §4/§11.2) — ✅ 구현 완료

### 3.1 신규 파일: `Assets/02. Scripts/Core/SceneNames.cs`

```csharp
// 씬 전환 시 쓰이는 씬 이름 상수. GameManager/ColorTag/Lobby 세 도메인이 공통으로 참조하므로
// 특정 도메인에 두지 않고 Core/에 별도로 둔다 (architecture-review.md §4/§11.2).
public static class SceneNames
{
    public const string Lobby = "LobbyScene";
    public const string GameLobby = "GameLobbyScene";
    public const string Game = "GameScene";
}
```

### 3.2 호출부 5곳 교체

| 파일:라인 | 변경 전 | 변경 후 |
|---|---|---|
| `RoomExitController.cs`(§5.3, `GameManager.cs`에서 이동) | `SceneManager.LoadScene("LobbyScene")` | `SceneManager.LoadScene(SceneNames.Lobby)` |
| `RoomLifecycleWatcher.cs:69` | `SceneManager.LoadScene("LobbyScene")` | `SceneManager.LoadScene(SceneNames.Lobby)` |
| `RoomLifecycleWatcher.cs:63` | `PhotonNetwork.LoadLevel("GameLobbyScene")` | `PhotonNetwork.LoadLevel(SceneNames.GameLobby)` |
| `LobbyController.cs:170` | `PhotonNetwork.LoadLevel("GameLobbyScene")` | `PhotonNetwork.LoadLevel(SceneNames.GameLobby)` |
| `GameLobbyController.cs:105` | `PhotonNetwork.LoadLevel("GameScene")` | `PhotonNetwork.LoadLevel(SceneNames.Game)` |

`"LobbyScene"` 목적지는 이제 `GameManager.cs`가 아니라 §5에서 새로 만드는
`RoomExitController.cs`에 있다 — ⑤번 작업과 함께 적용된다.

---

## 4. `RoundIndex` 등 CustomProperties 조회 중복 제거 — `RoomState.cs` 도입 (§11.1) — ✅ 구현 완료

### 4.1 신규 파일: `Assets/02. Scripts/ColorTag/RoomState.cs`

```csharp
using Photon.Pun;

// Room CustomProperties를 안전하게 읽는 조회 헬퍼. ColorSelectionManager/ColorSelectionPanel/
// PlayerPaintCanvas/PlayerColorDisplay가 각자 반복 구현하던 조회 로직을 통합한다
// (architecture-review.md §11.1). ColorTag 도메인 전용으로, 이 도메인 밖에서는 쓰지 않는다.
public static class RoomState
{
    public static bool IsInRoom() => PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;

    public static bool TryGetInt(string key, out int value)
    {
        value = default;
        if (!IsInRoom()) return false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object raw)) return false;
        value = (int)raw;
        return true;
    }

    public static bool TryGetDouble(string key, out double value)
    {
        value = default;
        if (!IsInRoom()) return false;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object raw)) return false;
        value = (double)raw;
        return true;
    }

    public static int GetRoundIndex() => TryGetInt(NetKeys.RoundIndex, out int value) ? value : -1;
}
```

`ColorTag/` 안에만 두는 이유: `RoundIndex` 조회 중복이 발생한 4개 파일이 전부 이 도메인
안에 있고, `Lobby/GameLobbyController.cs`의 `if (!PhotonNetwork.InRoom || ...)` 가드(§11.3,
경미로 분류됨)까지 통합하려면 `Lobby → ColorTag` 방향의 새 의존을 만들어야 하는데, 이는
`research.md`/`architecture-review.md` §2가 확인한 "매니저 간 저결합" 구조를 해치므로 이번
범위에서는 하지 않는다(§8 범위 밖 참고).

### 4.2 호출부 교체

**`ColorSelectionManager.cs` — `Update()`**:
```csharp
// 변경 전
if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.RoundIndex, out object riObj)) return;
int roundIndex = (int)riObj;
if (roundIndex < 0 || roundIndex >= TotalRounds) return;
double endTime = (double)PhotonNetwork.CurrentRoom.CustomProperties[NetKeys.RoundEndTime];
if (PhotonNetwork.Time < endTime) return;

// 변경 후
if (!RoomState.TryGetInt(NetKeys.RoundIndex, out int roundIndex)) return;
if (roundIndex < 0 || roundIndex >= TotalRounds) return;
if (!RoomState.TryGetDouble(NetKeys.RoundEndTime, out double endTime)) return;
if (PhotonNetwork.Time < endTime) return;
```
(부수 효과: `RoundEndTime`을 인덱서(`[...]`)로 직접 캐스팅하던 부분이 `TryGetDouble`로 바뀌며,
키가 없을 때 예외 대신 안전하게 리턴하도록 견고성이 약간 개선된다.)

**`ColorSelectionPanel.cs` — `Update()`/`UpdateSwatchLocks()`**:
```csharp
// 변경 전
var props = PhotonNetwork.CurrentRoom.CustomProperties;
if (!props.TryGetValue(NetKeys.RoundIndex, out object riObj)) return;
int roundIndex = (int)riObj;
...
if (timeLabel != null && props.TryGetValue(NetKeys.RoundEndTime, out object endObj))
{
    double remaining = System.Math.Max(0, (double)endObj - PhotonNetwork.Time);
    ...
}
UpdateSwatchLocks(props, roundIndex);

// 변경 후
if (!RoomState.TryGetInt(NetKeys.RoundIndex, out int roundIndex)) return;
...
if (timeLabel != null && RoomState.TryGetDouble(NetKeys.RoundEndTime, out double endTime))
{
    double remaining = System.Math.Max(0, endTime - PhotonNetwork.Time);
    ...
}
UpdateSwatchLocks(roundIndex);
```
`UpdateSwatchLocks(Hashtable roomProps, int roundIndex)`도 시그니처에서 `roomProps`를 없애고
내부의 `roomProps.TryGetValue(NetKeys.ColorPrefix + i, ...)`를 `RoomState.TryGetInt(NetKeys.
ColorPrefix + i, out int used)`로 바꾼다(같은 파일을 손대는 김에 함께 통일, 저위험). 더 이상
`Hashtable`(`ExitGames.Client.Photon`)을 직접 쓰지 않으므로 해당 `using` 지시문도 제거된다.

**`PlayerPaintCanvas.cs`**: `GetRoundIndex()` private 메서드를 삭제하고, 2곳의 호출부
(`IsColorRoundActive()`, `DetectRoundChange()`)를 `RoomState.GetRoundIndex()` 직접 호출로 교체.

**`PlayerColorDisplay.cs` — `TryApplyTaggerColor()`**:
```csharp
// 변경 전
if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
var props = PhotonNetwork.CurrentRoom.CustomProperties;
if (!props.TryGetValue(NetKeys.RoundIndex, out object riObj)) return;
if ((int)riObj != CompleteRoundIndex) return;

// 변경 후
if (!RoomState.TryGetInt(NetKeys.RoundIndex, out int roundIndex)) return;
if (roundIndex != CompleteRoundIndex) return;
var props = PhotonNetwork.CurrentRoom.CustomProperties; // TaggerActorNumber/TaggerVariantSet/ColorN은 int[]/object라 RoomState 범용 헬퍼 대상 밖 — 그대로 유지
```

### 4.3 통합하지 않는 곳

`BrushCursorController.OnRoomPropertiesUpdate(Hashtable changedProps)`는 Photon 콜백이 전달하는
**변경분 payload**(`changedProps`)를 읽는 이벤트 기반 패턴이라, Room 전체 상태를 능동적으로 폴링하는
나머지 4곳과 근본적으로 다르다 — 억지로 `RoomState`에 맞추지 않고 원래 코드 그대로 둔다.

---

## 5. `GameManager` 스폰/나가기 책임 분리 (`architecture-review.md` §1.1) — ✅ 구현 완료

### 5.1 현재 구조 재확인

`GameManager.cs`는 지금 세 가지 책임을 한 클래스에서 담당한다: ① 채팅 중계(`LogMsg` RPC,
`BroadcastingChat`), ② 캐릭터 스폰(`CreatePlayer()`, `Awake()`에서 호출), ③ 나가기/씬 전환
(`OnClickBackButtonPressed`/`OnClickBackBtn`/`OnLeftRoom`, `ConfirmDialog` 연동). 이번 작업은
②·③을 별도 컴포넌트로 떼어내고 `GameManager`는 ①(채팅)만 남긴다.

### 5.2 분리 후 구조

```
GameManager 오브젝트 (GameLobbyScene, GameScene 각각에 이미 배치돼 있음)
├── GameManager.cs          — 채팅 전용으로 축소 (기존 컴포넌트, 필드만 줄어듦)
├── PlayerSpawner.cs         — 신규. Awake()에서 캐릭터 스폰만 담당
└── RoomExitController.cs    — 신규. 뒤로가기 버튼 + 확인창 + LeaveRoom + 씬 전환 담당
```

세 컴포넌트 모두 **같은 GameObject**(기존 "GameManager" 오브젝트)에 그대로 둔다 — 씬 구조를
바꾸지 않고 컴포넌트만 추가/축소하는 최소 변경 방식이다. `RoomExitController`가 `"LogMsg"` RPC를
브로드캐스트해야 하므로(방 나감 메시지), `GameManager`와 **같은 `PhotonView`**를 참조하도록 별도
필드로 연결한다 — Photon의 `PhotonView.RPC()`는 메서드 이름으로 그 GameObject의 모든 컴포넌트를
검색해 `[PunRPC]`가 붙은 메서드를 찾으므로, `RoomExitController`가 자기 자신에 `LogMsg`를 두지
않고 `GameManager.LogMsg`를 그대로 호출해도 정상 동작한다(같은 오브젝트, 같은 PhotonView 공유이므로
동작 방식 자체는 지금과 동일).

### 5.3 신규 파일: `Assets/02. Scripts/GameManager/PlayerSpawner.cs`

```csharp
using Photon.Pun;
using UnityEngine;

// 캐릭터 스폰 전담. GameManager.cs에서 분리됨(architecture-review.md §1.1).
public class PlayerSpawner : MonoBehaviour
{
    private const string SpawnPointName = "PlayerSpawnPos";
    private const string PlayerPrefabName = "HideOrSeekPlayer";

    private void Awake()
    {
        // Camera_Ctrl.InitCamera()가 이 시점(씬의 최초 Awake 일괄 처리 단계)에 함께 호출되어야
        // 카메라 초기 각도(m_DefaultRotV)가 정상 적용된다 — Awake()가 아닌 다른 시점(예: OnJoinedRoom())
        // 으로 옮길 경우 Camera_Ctrl.Start()보다 늦게 실행되어 카메라 초기화가 스킵될 수 있다
        // (architecture-review.md §7.1). 옮기게 되면 Camera_Ctrl 쪽도 함께 재검토해야 한다.
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

        Vector3 offset = new Vector3(Random.Range(-5.0f, 5.0f), 0f, Random.Range(-5.0f, 5.0f));
        Vector3 spawnPos = spawnPointObj.transform.position + offset;

        PhotonNetwork.Instantiate(PlayerPrefabName, spawnPos, Quaternion.identity, 0);
    }
}
```

기존 `CreatePlayer()`와 동작은 동일하되(스폰 지점 이름/프리팹 이름/오프셋 로직 전부 그대로),
스폰 지점을 못 찾았을 때 **조용히 스킵하던 것을 `Debug.LogWarning`으로 드러내도록 개선**했다
(`architecture-review.md` §4의 권장사항을 함께 반영 — "재발방지" 취지와 자연스럽게 맞물린다).

### 5.4 신규 파일: `Assets/02. Scripts/GameManager/RoomExitController.cs`

```csharp
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 뒤로가기 버튼(확인창 → 방 나가기 → 로비 씬 전환) 전담.
// GameManager.cs에서 분리됨(architecture-review.md §1.1 — GameManager는 채팅 전용으로 축소).
public class RoomExitController : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv; // "LogMsg" RPC 브로드캐스트용 — GameManager와 같은 오브젝트의 PhotonView를 연결
    [SerializeField] private Button m_BackBtn;
    [SerializeField] private ConfirmDialog confirmDialog;
    [SerializeField] private string leaveConfirmMessage = "로비로 나가시겠습니까?"; // 씬별로 인스펙터에서 다르게 설정

    private void Start()
    {
        if (m_BackBtn != null)
            m_BackBtn.onClick.AddListener(OnClickBackButtonPressed);
    }

    // Back 버튼 클릭 시: 곧바로 나가지 않고 확인창부터 띄운다
    public void OnClickBackButtonPressed()
    {
        if (confirmDialog != null)
            confirmDialog.Show(leaveConfirmMessage, OnClickBackBtn);
        else
            OnClickBackBtn(); // 확인창이 연결 안 돼 있으면 안전하게 기존 동작으로 폴백
    }

    public void OnClickBackBtn()
    {
        if (m_BackBtn != null) m_BackBtn.interactable = false;

        string msg = "\n<color=#ff0000>]" + PhotonNetwork.LocalPlayer.NickName + "] 방 나감</color>";

        if (PhotonNetwork.PlayerList != null && PhotonNetwork.PlayerList.Length <= 1)
        {
            Debug.Log("마지막 사람이 방 나감");
            if (PhotonNetwork.CurrentRoom != null)
            {
                PhotonNetwork.CurrentRoom.CustomProperties.Clear();
                Debug.Log("방의 CustomProperties 초기화 완료!");
            }
        }

        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg, false);

        if (PhotonNetwork.LocalPlayer != null)
        {
            PhotonNetwork.LocalPlayer.CustomProperties.Clear();
            Debug.Log("나가는 유저의 CustomProperties 초기화 완료!");
        }

        Debug.Log("방 나가기 버튼 클릭!");
        PhotonNetwork.LeaveRoom();
        Debug.Log("PhotonNetwork.LeaveRoom() 호출 완료!");
    }

    public override void OnLeftRoom()
    {
        Debug.Log("방 나가기 완료! OnLeftRoom 콜백함수 호출!");
        SceneManager.LoadScene(SceneNames.Lobby);
    }
}
```

로직·로그 문구는 기존 `GameManager.OnClickBackBtn()`/`OnLeftRoom()`과 **완전히 동일하게 이동**만
했다(§3의 씬 이름 상수화만 함께 적용). `Room.CustomProperties.Clear()`에 마스터 클라이언트 가드가
없는 점(`architecture-review.md` §10에서 지적)도 **행동 변경 없이 그대로 이동**한다 — 이번
범위는 "책임 위치 이동"이지 "로직 재작성"이 아니므로, 그 가드 추가는 §8 범위 밖으로 남긴다.

### 5.5 `GameManager.cs` 축소 후 최종 형태

```csharp
using Photon.Pun;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviourPunCallbacks
{
    static public GameManager Inst;

    const int MAX_CHAT = 50; // 채팅 최대 갯수

    [SerializeField] private PhotonView pv;
    [SerializeField] private TMP_InputField InputFdChat; // 채팅 입력 필드
    [SerializeField] private TextMeshProUGUI txtLogMsg;

    private List<string> m_MsgList = new List<string>();
    private bool bEnter = false;

    private bool is_Conversating; // 채팅 중인지 여부를 나타내는 변수
    public bool Is_Conversating => is_Conversating;

    private void Awake()
    {
        Inst = this;
    }

    private void Start()
    {
        Time.timeScale = 1.0f; // 일시정지 풀어주기
        PhotonNetwork.IsMessageQueueRunning = true;

        string msg = "\n<color=#33ff33>[" + PhotonNetwork.LocalPlayer.NickName + "] Connected</color>";
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg, false);
    }

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
            }
            else
            {
                InputFdChat.gameObject.SetActive(false);
                is_Conversating = false;
                if (!string.IsNullOrEmpty(InputFdChat.text.Trim()))
                {
                    BroadcastingChat();
                }
            }
        }
    }

    [PunRPC]
    private void LogMsg(string msg, bool isChatMsg, PhotonMessageInfo info)
    {
        if (info.Sender.IsLocal == true && isChatMsg == true)
        {
            msg = msg.Replace("#ffffff", "#ffff00");
        }

        m_MsgList.Add(msg);
        if (m_MsgList.Count > MAX_CHAT)
        {
            m_MsgList.RemoveAt(0);
        }

        txtLogMsg.text = "";
        for (int i = 0; i < m_MsgList.Count; i++)
        {
            txtLogMsg.text += m_MsgList[i];
        }
    }

    private void BroadcastingChat()
    {
        if (!PhotonNetwork.InRoom) return;

        string msg = "\n<color=#ffffff>[" + PhotonNetwork.LocalPlayer.NickName + "] " + InputFdChat.text + "</color>";
        pv.RPC("LogMsg", RpcTarget.AllBuffered, msg, true);
        InputFdChat.text = "";
    }
}
```

제거된 것: `using UnityEngine.SceneManagement;`/`using UnityEngine.UI;`, 필드
`m_BackBtn`/`confirmDialog`/`leaveConfirmMessage`, 메서드 `OnClickBackButtonPressed`/
`OnClickBackBtn`/`OnLeftRoom`/`CreatePlayer`, `Awake()`의 `CreatePlayer()` 호출.
`Inst`/`is_Conversating`/`Is_Conversating`은 **그대로 유지**한다(§8 범위 밖 — 이번 5개 항목에
포함되지 않은 별도의 죽은 코드 정리 사안).

### 5.6 씬 작업 체크리스트 (구현 시 수행)

`GameLobbyScene`/`GameScene` 각각에서, 기존 "GameManager" 오브젝트에:

1. `PlayerSpawner` 컴포넌트 추가(필드 없음, 연결 작업 불필요).
2. `RoomExitController` 컴포넌트 추가 후 인스펙터에서:
   - `pv` → 같은 오브젝트의 `PhotonView`(기존 `GameManager.pv`와 동일한 컴포넌트)
   - `m_BackBtn` → 기존 `GameManager.m_BackBtn`이 가리키던 `Canvas/Button`
   - `confirmDialog` → 기존 `GameManager.confirmDialog`가 가리키던 `ConfirmDialog` 인스턴스
   - `leaveConfirmMessage` → 씬별 기존 값 그대로(`GameLobbyScene`="로비로 나가시겠습니까?",
     `GameScene`="게임이 진행중입니다. 나가시겠습니까?")
3. 기존 `GameManager` 컴포넌트에서 `m_BackBtn`/`confirmDialog`/`leaveConfirmMessage` 필드가
   인스펙터에서 사라진 것을 확인(코드에서 제거됐으므로 자동으로 사라짐).
4. `read_console`로 컴파일 에러 0건 확인 후 Play Mode 검증(§7).

---

## 6. 구현 순서 제안 — ✅ 아래 순서 그대로 구현 완료

의존관계상 다음 순서로 적용하는 것이 안전하다(뒤 단계가 앞 단계의 산출물을 사용):

1. `Core/SceneNames.cs`, `ColorTag/RoomState.cs` 신규 생성 (다른 어떤 파일도 아직 참조하지
   않으므로 안전하게 먼저 추가 가능).
2. §4의 4개 파일(`ColorSelectionManager`/`ColorSelectionPanel`/`PlayerPaintCanvas`/
   `PlayerColorDisplay`)에서 `RoomState` 사용하도록 교체 + §2의 `PlayerPaintCanvas.OnDestroy()`
   추가(같은 파일을 만지는 김에 함께 적용 가능) + §1의 이중 등록 제거(§4에서 만지는
   `PlayerPaintCanvas`/`PlayerColorDisplay` 포함 7개 파일 전부).
3. 매 파일 저장 직후 `read_console`로 컴파일 에러 0건 확인.
4. `GameManager/PlayerSpawner.cs`, `GameManager/RoomExitController.cs` 신규 생성.
5. `GameManager.cs` 축소.
6. `RoomLifecycleWatcher.cs`/`LobbyController.cs`/`GameLobbyController.cs`에 §3의 `SceneNames`
   적용(§1의 이중 등록 제거도 이 3개 파일에 포함되므로 한 번에 처리).
7. §5.6의 씬 작업 수행.
8. 최종 `read_console` + Play Mode 전체 검증(§7).

---

## 7. 검증 계획 — ✅ 구현 완료 (실제 결과는 §9.3 참고)

1. **컴파일**: 각 단계 직후 `read_console`로 에러 0건 확인(§6에 명시된 지점마다).
2. **채팅(회귀 확인)**: `GameLobbyScene`/`GameScene` 각각 Play Mode에서 Enter로 채팅창 토글,
   메시지 송수신, 자기 메시지 노란색 하이라이트가 축소된 `GameManager`만으로 그대로 동작하는지
   확인(§5 책임 분리가 채팅 기능을 깨지 않았는지).
3. **스폰(회귀 확인)**: 두 씬 모두 `PlayerSpawner`가 `PlayerSpawnPos` 근처에 `HideOrSeekPlayer`를
   정확히 1개 스폰하는지, 카메라가 기존과 동일한 초기 각도(25° 부감)로 시작하는지 확인(§5.3의
   `Awake()` 타이밍 유지가 실제로 `Camera_Ctrl` 초기화를 깨지 않았는지 — §7.1에서 지적된 리스크의
   실증 검증).
4. **뒤로가기(회귀 확인)**: `RoomExitController`로 이동한 뒤에도 두 씬에서 각각 맞는 문구의
   확인창 → "예"로 정상 퇴장 → `SceneNames.Lobby`(`"LobbyScene"`)로 정상 이동, "아니오"는 아무
   일도 없이 닫히는지 확인.
5. **Photon 콜백 이중 등록 해소 확인**: `GameLobbyController`가 붙은 씬에서 플레이어 입장/퇴장 시
   `RefreshPlayerList()`/`RefreshStartButton()`이 (로그 등을 임시로 추가해) 정확히 1번만 실행되는지
   확인. `LobbyController.OnJoinedRoom()`이 방 생성 직후 `PhotonNetwork.LoadLevel`을 1번만
   호출하는지 확인(가장 위험했던 지점).
6. **ColorTag 회귀 확인 (`PlayerTestScene`)**: `RoomState`로 교체된 4개 파일과 이중 등록이
   제거된 `PlayerPaintCanvas`/`BrushCursorController`/`PlayerColorVoteIndicator`/
   `PlayerColorDisplay`가 실제로 여전히 콜백을 정상 수신하는지 — 4라운드 색상 투표 → 페인팅 →
   술래 지정까지 전체 흐름이 기존과 동일하게 동작하는지 `PlayerTestScene`에서 Play Mode로 확인.
   특히 원격 붓 스트로크(`PlayerPaintCanvas.OnEvent`)가 이제 정확히 1번만 반영되는지 확인.
7. **RenderTexture 해제 확인**: `GameLobbyScene → GameScene` 전환을 반복한 뒤 Unity Profiler
   (Memory) 또는 Frame Debugger로 `PaintCanvas_*` 이름의 `RenderTexture`가 이전 씬의 캐릭터
   파괴 시점에 함께 해제되는지 확인(정확한 수치 확인은 구현 단계에서 진행).

---

## 8. 범위 밖으로 남겨두는 것 (이번 계획에 포함하지 않음)

- **`GameManager.Inst`/`is_Conversating`/`Is_Conversating` 죽은 코드 정리** —
  `architecture-review.md` §5, `research.md` §6.3이 지적한 별도 사안이며, 이번에 사용자가 확정한
  5개 항목에 포함되지 않아 손대지 않는다. `GameManager`를 축소하는 김에 같이 정리할지는 별도로
  요청해달라.
- **`RoomExitController.OnClickBackBtn()`의 `Room.CustomProperties.Clear()`에 마스터 클라이언트
  가드 추가** — `architecture-review.md` §10에서 지적된 별도 일관성 이슈. 이번 작업은 "책임을
  옮기는 것"이지 "로직을 고치는 것"이 아니므로 행동을 바꾸지 않는다.
- **`RoomExitController`와 `RoomLifecycleWatcher`의 통합** — 둘 다 "방을 나가는" 결과로 이어지지만
  전자는 사용자가 직접 누르는 UI 흐름(확인창 포함), 후자는 시스템이 자동 감지하는 흐름(술래 퇴장,
  인원 부족, 정상 종료 타이머)으로 트리거 성격이 달라 그대로 분리해서 유지한다. 또한
  `RoomLifecycleWatcher`는 현재 `GameLobbyScene`/`GameScene`에 아직 배치되어 있지 않으므로
  (`research.md` §7의 별도 통합 공백), 지금 두 클래스를 합치면 그 배선 공백이 뒤로가기 버튼까지
  전염될 위험이 있어 더더욱 분리 유지가 안전하다.
- **`GameLobbyController.RefreshPlayerList()`를 `RoomListItem`과 같은 diff 패턴으로 통일** —
  `architecture-review.md` §9.1에서 지적된 별도 사안, 이번 5개 항목 밖.
- **`research.md` §7의 ColorTag ↔ 실제 매칭 플로우 통합 공백 해결** — 이번 작업과 완전히 별개의
  더 큰 작업(씬에 `ColorSelectionManager` 등을 배치하고 게임 시작 트리거를 연결하는 것)이며,
  이번 5개 항목에는 포함되지 않는다.

---

## 9. 구현 완료 보고

### 9.1 §1~§5 실제 변경 사항 — 계획과 100% 일치

Unity MCP(`create_script`/`script_apply_edits`/`manage_components`/`manage_scene`)를 통해 §0.1의
신규 파일 4개(`Core/SceneNames.cs`, `ColorTag/RoomState.cs`, `GameManager/PlayerSpawner.cs`,
`GameManager/RoomExitController.cs`)를 계획된 코드 그대로 생성하고, §0.2의 11개 파일 변경을
전부 적용했다. 최종 코드는 §1~§5에 적어둔 "변경 후" 스니펫과 라인 단위로 일치한다(`GameManager.cs`
축소분은 §5.5의 최종 형태 그대로).

### 9.2 계획에 없었던 추가 발견 — `script_apply_edits`의 `delete_method`가 인접 필드를 함께 삭제하는 버그

§1(이중 등록 제거) 작업 중, Unity MCP의 `script_apply_edits(op="delete_method")`가 삭제 대상
메서드(`OnEnable`)**바로 앞에 다른 멤버 없이 곧바로 붙어있는 `[SerializeField]` 필드 블록**까지
함께 삭제해버리는 도구 자체의 버그를 발견했다. 정확히 재현된 파일은 다음 3곳이다:

- `Lobby/GameLobbyController.cs` — `playerListContent`/`playerListItemPrefab`/`statusText`/
  `startGameButton` 4개 필드가 `OnEnable` 삭제와 함께 사라짐.
- `ColorTag/PlayerColorVoteIndicator.cs` — `pv`/`indicator`/`palette` 3개 필드가 사라짐.
- `ColorTag/ColorSelectionPanel.cs` — §4(RoundIndex 조회를 `RoomState`로 교체) 작업 중
  `Update()`를 `replace_method`로 교체할 때 같은 패턴으로 `roundLabel`/`timeLabel`/`swatches`
  3개 필드가 사라짐(필드 바로 뒤에 다른 멤버 없이 곧바로 `Update()`가 있던 경우 `delete_method`
  뿐 아니라 `replace_method`에서도 재현됨).

매번 `read_console`로 `CS0103: The name '...' does not exist in the current context` 컴파일
에러를 즉시 감지했고, 삭제된 필드를 `anchor_replace`(다음 멤버 선언 줄을 앵커로 삼아 필드 선언 +
그 줄을 함께 복원)로 정확히 복구한 뒤 `read_console`로 에러 0건을 재확인했다 — **최종 결과물에는
영향이 없다.** 앞으로 이 도구로 클래스 최상단 필드 바로 다음에 오는 메서드를 삭제/교체할 때는
필드가 함께 삭제되지 않았는지 즉시 컴파일 로그로 확인하는 것이 안전하다는 교훈을 남긴다.

별도로, 씬 오브젝트에 컴포넌트 속성을 유니코드 이스케이프(`\uXXXX`)로 직접 지정하는 과정에서
`RoomLifecycleWatcher.cs` 주석 한 글자("닫아뒀던" → "닫아뒠")가 오타로 깨진 것을 발견해 즉시
직접 수정했다(코드 로직에는 영향 없는 주석 텍스트였다).

### 9.3 §7 검증 계획 — 실제 수행 결과

1. **컴파일**: 매 파일 변경 직후 `read_console`로 확인, 위 §9.2의 2개 이슈를 제외하면 전 과정
   에러 0건. 최종적으로도 에러·경고 0건(NavMesh/JoinLobby 관련 로그는 §9.3-6에서 설명하는
   기존 항목).
2. **채팅(회귀 확인) — ✅ 확인 완료**: `GameLobbyScene`/`GameScene` 각각 Play Mode에서 실제로
   `PhotonNetwork.CreateRoom`으로 방을 만들어 진입 → 채팅 로그에 `"[닉네임] Connected"`가 초록색
   으로 정상 표시됨을 스크린샷으로 확인. 축소된 `GameManager`만으로 `LogMsg` RPC가 정상 동작한다.
3. **스폰(회귀 확인) — ✅ 확인 완료**: 두 씬 모두 `PlayerSpawner`가 `PlayerSpawnPos` 근처에
   `HideOrSeekPlayer`를 정확히 1개 스폰했고, 스크린샷상 카메라가 부감 각도로 캐릭터를 비추는
   기존과 동일한 구도로 시작함을 확인했다(§5.3에서 우려했던 `Awake()` 타이밍 문제가 실제로는
   발생하지 않음 — 스폰 호출을 `Awake()`에 그대로 둔 결정이 유효함을 실증).
4. **뒤로가기(회귀 확인) — ✅ 확인 완료**: 두 씬 모두 `RoomExitController.OnClickBackButtonPressed()`
   호출 → `ConfirmDialog`가 각 씬에 맞는 문구(`GameLobbyScene`="로비로 나가시겠습니까?",
   `GameScene`="게임이 진행중입니다. 나가시겠습니까?")로 표시됨을 스크린샷과 컴포넌트 상태 조회로
   확인. 이어서 `OnClickBackBtn()` 호출 → `PhotonNetwork.InRoom == False`, 활성 씬이
   `SceneNames.Lobby`(`"LobbyScene"`)로 정확히 전환됨을 두 씬 모두에서 확인.
5. **Photon 콜백 이중 등록 해소 확인 — 부분 확인**: `read_console`에 `LobbyController.
   OnJoinedRoom()` 관련 중복 씬 전환 에러/경고가 전혀 없었고, 실제로 방 생성 직후 `GameLobbyScene`
   으로 정확히 1회 전환됨을 확인했다 — 가장 위험하다고 판단했던 지점은 실증됐다. 다만
   `GameLobbyController.OnPlayerEnteredRoom`처럼 **여러 클라이언트가 동시에 접속해야 관찰
   가능한 이중 실행**은, 이번 세션이 Unity 에디터 인스턴스 하나로 단일 클라이언트만 접속했기
   때문에 "여러 명이 입장할 때 콜백이 정확히 1번만 도는지"를 직접 재현·비교하지는 못했다 —
   대신 코드 자체가 `base.OnEnable()`/`base.OnDisable()`만 남기고 중복 등록 줄을 제거했음을
   §9.1에서 파일 단위로 재확인했고, Photon SDK 소스(`architecture-review.md` §8.1에서 확인한
   `LoadBalancingClient.AddCallbackTarget`의 `container.Add` 로직)상 등록 횟수가 정확히 1회로
   줄었다는 것은 정적으로 확실하다.
6. **ColorTag 회귀 확인(`PlayerTestScene`) — ✅ 확인 완료**: `OfflineModeBootstrap`으로 오프라인
   룸을 만들어 Play Mode 진입 → 팔레트 패널이 "1 / 4"와 카운트다운을 정상 표시(스크린샷 확인,
   §4의 `RoomState` 교체가 정상 동작). 코드로 `SubmitVote(3)` 호출 + `RoundEndTime`을 강제 만료시켜
   라운드 진행을 확인한 결과, `RoundIndex`가 0→1→2로 정상 진행되고 `Color0=3`(투표한 색),
   `Color1=0`(다른 값, 중복 없음)으로 올바르게 확정됨을 Room CustomProperties 조회로 직접
   확인했다 — `ColorSelectionManager`/`ColorSelectionPanel`의 `RoomState` 기반 폴링과 이중 등록이
   제거된 `PlayerPaintCanvas`/`BrushCursorController`/`PlayerColorVoteIndicator`/
   `PlayerColorDisplay`가 정상적으로 계속 콜백을 수신하며 동작함을 확인했다. 다만 실제 마우스
   클릭으로 캐릭터를 페인팅하는 조작(`PlayerPaintCanvas.OnEvent`의 원격 스트로크 반영)은 legacy
   Input Manager 기반이라 이번 세션에서 OS 입력을 시뮬레이션하지는 못했다 — 대신 그 메서드가
   의존하는 `RoomState.GetRoundIndex()`가 라운드 진행 전체에 걸쳐 정상 동작함을 위 시나리오로
   간접 검증했다.
7. **RenderTexture 해제 확인 — 부분 확인**: `OnDestroy()`에서 `PaintCanvas.Release()`를 호출하는
   코드가 정상 컴파일·존재함을 확인했고, `PlayerTestScene`/`GameLobbyScene`/`GameScene` 전환 중
   컴파일 에러나 예외가 없었다. 다만 Unity Memory Profiler로 `PaintCanvas_*` RenderTexture의
   실제 GPU 메모리 해제 시점을 수치로 측정하는 것은 이번 세션 도구(Unity MCP 콘솔/스크린샷) 범위
   밖이라 수행하지 않았다 — 코드 리뷰 수준의 확인에 그친다는 점을 정직하게 남겨둔다.

### 9.4 최종 상태

- `Assets/02. Scripts/` 전체에서 `read_console(types=["error"])` 결과 0건.
- Play Mode 세션 동안 관찰된 로그는 전부 이번 작업과 무관한 기존 항목뿐이었다:
  `"Failed to create agent because there is no valid NavMesh"`(`GameManager.md` §8.4에서 이미
  "이번 계획 범위와 무관한 기존 씬 구성 문제"로 기록된 것과 동일), `"Operation JoinLobby ...
  not called because client is not connected"`(`LobbyScene` 재진입 시 재연결 타이밍에 따른
  일시적 경고, `LobbyController.Start()`의 기존 로직).
- `GameLobbyScene.unity`/`GameScene.unity` 저장 완료 — `GameManager` 오브젝트에 `PlayerSpawner`/
  `RoomExitController` 컴포넌트가 추가되고, 기존 `m_BackBtn`/`confirmDialog`/`leaveConfirmMessage`
  연결값이 그대로 `RoomExitController`로 옮겨졌음을 씬 파일 재확인으로 검증했다.
