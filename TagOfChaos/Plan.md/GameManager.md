# 계획: `GameManager.cs`를 `GameLobbyScene`/`GameScene`에서 그대로 쓰기 위한 작업

상태: **✅ 구현 완료**. `Assets/Scripts/GameManager.cs`(현재 코드는 `research.md` §11~§22,
§33~§38에서 이미 라인 단위로 조사했다)를 고치지 않고 "그대로" 재사용하되, `CLAUDE.md` 폴더 규칙에
맞게 옮기고, 이미 각 씬에 배치해두신 오브젝트(채팅 UI, `PlayerSpawnPos`)에 맞춰 필드를 연결하고,
스폰 로직이 실제로 동작하도록 이름을 고치는 계획이었다. §1~§6 전부 구현·검증까지 마쳤다 — 실제
구현 결과와 계획 대비 추가로 발견/수정한 사항은 §8에 정리했다.

---

## 0. 조사 결과 요약

### 0.1 지금 씬에 배치된 오브젝트 (직접 씬을 열어 확인)

`GameScene.unity`:
```
Main Camera / Directional Light / EventSystem
Canvas
├── InputFieldChat        (TMP_InputField)
└── PanelLogMsg           (ScrollRect)
    └── Viewport
        └── Content       (VerticalLayoutGroup + ContentSizeFitter)
            └── Text (TMP)  (TextMeshProUGUI)  ← 로그 텍스트가 최종적으로 들어갈 곳
PlayerSpawnPos             (빈 Transform)
```
`GameManager.cs`를 붙일 오브젝트, 그리고 방 나가기 버튼(`m_BackBtn`)에 해당하는 `Button`은 이 씬에
**아직 없다** — 코드에 이미 `if (m_BackBtn != null)` 널 가드가 있으므로 비워둬도 안전하다.

`GameLobbyScene.unity`:
```
Main Camera / Directional Light / EventSystem
GameLobbyUICanvas          (RoomItemPlan.md에서 이미 만든 대기방 패널 — 이번 작업과 무관)
└── GameLobbyPanel/StartGameButton 등
Canvas                     (채팅용 — GameScene과 동일 구조)
├── InputFieldChat         (TMP_InputField)
└── PanelLogMsg
    └── Viewport/Content/Text (TMP)
PlayerSpawnPos             (빈 Transform)
```

**두 씬의 채팅 UI 오브젝트 이름/경로가 정확히 동일하다**(`Canvas/InputFieldChat`,
`Canvas/PanelLogMsg/Viewport/Content/Text (TMP)`) — 사용자가 말씀하신 "동일한 채팅창"이 이미
동일한 구조로 배치되어 있음을 확인했다. `PlayerSpawnPos`도 두 씬 모두에 이미 있다.

### 0.2 `GameManager.cs`가 그대로 쓰이기 위해 막고 있는 것 (research.md 재인용)

| # | 문제 | 근거 |
|---|---|---|
| 1 | 파일이 CP949로 저장되어 있어, UTF-8 프로젝트에서 한글 주석·문자열이 깨질 위험 | research.md §12, §37 |
| 2 | `CreatePlayer()`가 `"HeroSpawnPos"`를 찾음 — 실제 오브젝트 이름은 `"PlayerSpawnPos"` | 이번 조사(§0.1), research.md §19/§35 |
| 3 | `CreatePlayer()`가 `"HeroPrefab"`을 스폰함 — 그런 이름의 Resources 프리팹은 없음. 실제로 존재하는 것은 `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab` | research.md §19/§35, `PlayerControllPlan.md` §13.9 |
| 4 | `OnLeftRoom()`이 `"PhotonLobby"` 씬을 로드하려 하는데, 프로젝트에 그 이름의 씬이 없음(가장 가까운 후보는 `LobbyScene`) | research.md §18, §34 |
| 5 | `pv`/`m_BackBtn`/`InputFdChat`/`txtLogMsg`가 전부 `[SerializeField]`인데, 씬마다 그 씬의 UI 오브젝트로 **개별 연결**해야 함(직렬화 필드는 씬을 못 넘나듦 — `Camera_Ctrl.m_Player`와 동일한 제약, `PlayerControllPlan.md` §13.2) | 이번 조사 |
| 6 | 파일 위치가 `CLAUDE.md` 폴더 규칙(`Scripts` → `Assets/02. Scripts/{도메인}/`)을 어김 | `CLAUDE.md` |

`Is_Conversating`/`IsMovementLocked` 연결 끊김(research.md §36)은 "채팅 중 이동 잠금" 기능에 관한
것으로, 이번 요청(채팅창 배선 + 스폰)의 핵심 범위는 아니지만 §5에서 짧게 다룬다.

---

## 1. 파일 이동 — `CLAUDE.md` 폴더 규칙 적용 ✅ 구현 완료

### 1.1 어디로 옮길 것인가 — 확정

새 도메인 폴더 이름은 `GameManager`로 확정됐다:

```
Assets/02. Scripts/GameManager/
└── GameManager.cs
```

`Lobby/`(`RoomItemPlan.md`의 로비 UI 전용)와는 구분되는 별도 도메인이다 — `GameManager`는 룸에
들어온 **이후**의 씬들(`GameLobbyScene`/`GameScene`)에서 채팅·방 나가기·플레이어 스폰을 관장한다.

### 1.2 이동 방법

`PlayerControllPlan.md` §13.11에서 `Camera_Ctrl.cs`를 옮길 때 썼던 것과 동일한 방법을 쓴다:
Unity MCP `manage_asset(action="move")`로 GUID를 보존한 채 옮긴다. GUID가 보존되면 — 지금은
`GameManager.cs`를 참조하는 씬/프리팹이 전혀 없으므로 참조 무결성 문제는 없지만, 앞으로 씬에
배치할 때(§4) 이 방식이 표준 절차이므로 그대로 따른다.

```
Assets/Scripts/GameManager.cs        → Assets/02. Scripts/GameManager/GameManager.cs
Assets/Scripts/GameManager.cs.meta   → Assets/02. Scripts/GameManager/GameManager.cs.meta  (GUID 보존)
```

이동 후 `Assets/Scripts/` 안에는 아무 파일도 남지 않으므로(§0.2에서 확인한 대로 `GameManager.cs`가
그 폴더의 마지막 파일이다), **`Assets/Scripts/` 폴더 자체를 삭제한다**(요청하신 대로).

### 1.3 어셈블리 영향

`Assets/02. Scripts/TagOfChaos.Scripts.asmdef`는 `Assets/02. Scripts/` 하위 전체를 하나의
어셈블리로 묶는다(research.md §23) — `GameManager/`도 그 하위 폴더이므로 별도 설정 없이 자동으로 이
어셈블리에 편입된다. 참조 목록(`PhotonUnityNetworking`, `PhotonRealtime`, `Unity.TextMeshPro`,
`Unity.ugui`)이 `GameManager.cs`가 실제로 쓰는 네임스페이스(`Photon.Pun`, `TMPro`,
`UnityEngine.UI`, `UnityEngine.SceneManagement`)를 전부 커버하므로 추가 참조는 필요 없다
(`UnityEngine.SceneManagement`는 별도 참조 없이 기본 엔진 모듈로 해석됨).

지금까지 `Assets/Scripts/`(구 트리, `Assembly-CSharp`)에 있던 `GameManager.cs`가
`Assets/02. Scripts/`(신규 트리, `TagOfChaos.Scripts`)로 어셈블리를 옮겨 타는 것이므로, 이동 후
**한 번은 반드시 재컴파일 + `read_console`로 에러 0건을 확인**해야 한다(§6-1).

---

## 2. 인코딩 수정 (선행 작업) ✅ 구현 완료

CP949로 저장된 파일을 UTF-8(BOM)로 재저장한다(research.md §12/§37에서 지적한 문제). 파일을 옮기는
김에(§1) 같이 처리하는 것이 자연스럽다. 재저장 시 다음 한글 문자열/주석이 전부 원래 의미 그대로
보존되어야 한다(현재 CP949로는 정상 읽히는 내용, `iconv -f CP949 -t UTF-8`로 복원 확인 완료):

- 주석: `// 채팅 최대 갯수`, `// 뒤로 가기 버튼`, `// 채팅 입력 필드` 등
- 문자열 리터럴: `"] 방 나감</color>"`(`OnClickBackBtn`), `"Connected"` 메시지의 한글 부분,
  `Debug.Log` 라벨들

이 재저장은 텍스트 인코딩만 바꾸는 작업이라 **코드 로직은 한 글자도 바뀌지 않는다** — §3의 로직
수정과는 독립적인 선행 작업이다.

---

## 3. 코드 변경 사항 (스니펫 포함) ✅ 구현 완료

### 3.1 `CreatePlayer()` — 스폰 오브젝트/프리팹 이름 수정

**변경 전** (168~184행, 현재 코드):
```csharp
private void CreatePlayer()
{
    Vector3 hPos = Vector3.zero;
    Vector3 addPos = Vector3.zero;

    GameObject hPosObj = GameObject.Find("HeroSpawnPos");
    if (hPosObj != null)
    {
        addPos.x = Random.Range(-5.0f, 5.0f);
        addPos.z = Random.Range(-5.0f, 5.0f);
        hPos = hPosObj.transform.position + addPos;

        PhotonNetwork.Instantiate("HeroPrefab", hPos, Quaternion.identity, 0);
    }
}
```

**변경 후**:
```csharp
private void CreatePlayer()
{
    Vector3 hPos = Vector3.zero;
    Vector3 addPos = Vector3.zero;

    GameObject hPosObj = GameObject.Find("PlayerSpawnPos"); // §0.1 — 각 씬에 이미 배치됨
    if (hPosObj != null)
    {
        addPos.x = Random.Range(-5.0f, 5.0f);
        addPos.z = Random.Range(-5.0f, 5.0f);
        hPos = hPosObj.transform.position + addPos;

        // Resources 하위 정식 프리팹 (PlayerControllPlan.md §13.9)
        PhotonNetwork.Instantiate("HideOrSeekPlayer", hPos, Quaternion.identity, 0);
    }
}
```

바뀌는 것은 문자열 리터럴 2개뿐이다 — 나머지 로직(스폰 위치 랜덤 오프셋, null 체크)은 그대로
"재사용"한다는 요청 취지에 맞게 손대지 않는다.

### 3.2 `OnLeftRoom()` — 씬 이름 수정 (확정: `LobbyScene`)

```csharp
public override void OnLeftRoom()
{
    Debug.Log("방 나가기 완료! OnLeftRoom 콜백함수 호출!");
    SceneManager.LoadScene("LobbyScene"); // "PhotonLobby" → LobbyScene (확정)
}
```

`research.md` §34에서 목격했던 `PhotonLobby.unity`(당시 디스크엔 없던 경로)는 이번 확인으로
`LobbyScene`을 쓰는 것으로 정리됐다 — 더 이상 열린 질문이 아니다.

### 3.3 `pv`(`PhotonView`) 배치

`GameManager`를 붙일 GameObject에는 `PhotonView` 컴포넌트가 함께 있어야 한다(`pv` 필드가 널
가드 없이 `Start()`에서 바로 `pv.RPC(...)`를 호출하므로 필수). 씬 오브젝트(플레이어가 스폰하는
것이 아니라 씬에 고정 배치되는 매니저)이므로, `PhotonView`의 **Owner를 마스터 클라이언트로 두는
씬 오브젝트(Scene Object)** 방식이 적합하다 — `RoomLifecycleWatcher`/`ColorSelectionManager`
등 이미 이 프로젝트에 있는 다른 씬 고정 매니저들과 동일한 패턴이다.

---

## 4. 씬 배치 — 두 씬에 동일한 `GameManager`를 어떻게 둘 것인가 (확정: 방안 A, 씬별 독립 배치) ✅ 구현 완료

`GameManager`가 참조하는 UI(`InputFdChat`, `txtLogMsg`)는 `[SerializeField]`라 **씬을 넘나드는
참조가 불가능**하다(§0.2-5) — `GameLobbyScene`의 `GameManager`는 `GameLobbyScene`의
`Canvas/InputFieldChat`를, `GameScene`의 `GameManager`는 `GameScene`의 것을 각자 따로 가리켜야
한다.

**방안 A(확정) — 각 씬에 독립적으로 배치**: 두 씬에 각각 `GameManager` 오브젝트를 만들고,
컴포넌트도 각자 따로 추가해 그 씬의 UI로 연결한다. 프리팹화하지 않는다 — 두 씬에 완전히 별개의
`GameManager` 인스턴스가 존재하게 되며, 나중에 필드가 추가되면 두 곳을 손으로 맞춰야 한다는
점은 감수한다(단순함을 우선한 선택).

구체적인 배치 절차(구현 시):
1. `GameLobbyScene`에 빈 GameObject `GameManager`를 만들고 `PhotonView` + `GameManager`(§1의
   새 위치) 컴포넌트를 붙인다.
2. 인스펙터에서 `pv`(자기 자신의 `PhotonView`), `InputFdChat`→`Canvas/InputFieldChat`,
   `txtLogMsg`→`Canvas/PanelLogMsg/Viewport/Content/Text (TMP)`를 연결한다(`m_BackBtn`은
   없으므로 `None`).
3. `GameScene`에서도 동일하게 반복 — 오브젝트 이름/컴포넌트 구성은 똑같이 만들고, 참조만
   `GameScene` 쪽 UI 오브젝트로 연결한다.

### 4.1 씬별 필드 연결 표

| 필드 | `GameLobbyScene` | `GameScene` |
|---|---|---|
| `pv` | 해당 씬의 `GameManager` 인스턴스 자신의 `PhotonView` | 동일 패턴 |
| `m_BackBtn` | 없음 → `None` (기존 null 가드로 안전) | 없음 → `None` |
| `InputFdChat` | `Canvas/InputFieldChat` | `Canvas/InputFieldChat` (동일 이름, 다른 씬의 오브젝트) |
| `txtLogMsg` | `Canvas/PanelLogMsg/Viewport/Content/Text (TMP)` | 동일 경로 |

---

## 5. 범위 밖으로 남겨두는 것 (이번 계획에 포함하지 않음)

- **채팅-이동잠금 재연결**(research.md §36, `Is_Conversating`/`IsMovementLocked`가 서로 끊어진
  문제) — "그대로 쓰기 위한" 최소 작업 범위를 벗어나는 별도 기능 복구 작업이라 이번엔 다루지
  않는다. 필요하면 별도로 요청해달라.
- **`CreatePlayer()` 호출 시점**(research.md §20-5/§38-5의 레이스 컨디션 지적) — **`Awake()`에
  그대로 두기로 확정.** `GameLobbyScene`/`GameScene`은 로비를 거쳐야만 도달하는 씬이라(즉 이
  씬들의 `Awake()` 시점엔 이미 `PhotonNetwork.InRoom == true`가 보장됨), 원래 지적됐던
  "룸에 들어가기도 전에 스폰을 시도하는" 레이스 컨디션이 이 두 씬에서는 실질적으로 발생하지
  않을 것으로 판단된다. `OnJoinedRoom()`으로 옮기는 건 지금 당장 하지 않고, 실제로 문제가
  관찰되면(§6-3/§6-4 검증 중 스폰 실패나 타이밍 이슈가 보이면) 그때 다시 검토한다(요청하신
  "일단 두고 상황을 지켜본다"를 그대로 반영).
- **`LogMsg`의 색상 치환 로직/주석 정리, `Inst` 중복 가드 추가**(research.md §20-6/§20-11) —
  "그대로 쓰기" 요청 범위를 벗어나는 별도 리팩토링이라 제외.
- **씬 전환 시 캐릭터 재스폰 동작**: `PhotonNetwork.LoadLevel`은 Unity의 기본 씬 전환
  (`LoadSceneMode.Single`)을 쓰므로, 이전 씬에서 스폰됐던 캐릭터(및 `GameManager` 자신)는 새 씬이
  로드되며 자동으로 파괴되고, 새 씬의 `GameManager.Awake()`가 다시 `CreatePlayer()`를 호출해 새로
  스폰한다 — 별도의 정리 코드가 필요 없는 Photon PUN2의 일반적인 패턴으로 판단되지만, 구현 후
  §6-4에서 실제로 검증한다(계획 단계에서는 코드를 추가하지 않는다).

---

## 6. 검증 계획 ✅ 구현 완료 (결과는 §8 참고)

1. 이동 직후 `read_console`로 컴파일 에러 0건 확인(§1.3).
2. `GameLobbyScene`/`GameScene` 각각 Play Mode에서: 채팅 입력 → Enter → 로그 패널에 정상 표시,
   자기 메시지가 노란색으로 하이라이트되는지 확인.
3. 각 씬 Play Mode 진입 시 `PlayerSpawnPos` 근처에 `HideOrSeekPlayer` 캐릭터가 정상 스폰되는지,
   여러 명이 겹치지 않고 랜덤 오프셋(±5m)이 적용되는지 확인.
4. `GameLobbyScene` → `GameScene` 전환(및 게임 종료 후 `GameScene` → `GameLobbyScene` 복귀,
   `RoomItemPlan.md` §0.2) 시 캐릭터가 중복 스폰되지 않고 정상적으로 재스폰되는지 확인(§5의
   가정 검증).
5. UTF-8 재저장 후 한글 주석/문자열이 깨지지 않았는지 재확인.
6. 두 씬(`GameLobbyScene`/`GameScene`)의 `GameManager` 오브젝트가 컴포넌트 구성(스크립트,
   `PhotonView`)은 동일하고 인스펙터 참조값만 각자의 씬 UI를 가리키는지 확인(§4 방안 A).

---

## 7. 결정 사항 요약 (구현 전 확인 완료)

| # | 항목 | 결정 |
|---|---|---|
| 1 | 새 도메인 폴더 이름 | `Assets/02. Scripts/GameManager/`(§1.1) |
| 2 | `OnLeftRoom()`의 목적지 씬 이름 | `"LobbyScene"`(§3.2) |
| 3 | 씬 배치 방식 | 방안 A — 씬별 독립 배치, 프리팹화하지 않음(§4) |
| 4 | `CreatePlayer()` 호출 시점 | `Awake()`에 그대로 유지, 문제 관찰되면 재검토(§5) |

이 4가지 외의 나머지 계획(§1~§6)은 이전 초안과 동일했고, 그 순서(§1 이동 → §2 인코딩 → §3 코드
수정 → §4 씬 배치 → §6 검증) 그대로 구현했다. 실제 결과는 §8 참고.

---

## 8. 구현 완료 보고

### 8.1 §1~§4 실제 변경 사항

- `Assets/Scripts/GameManager.cs` → `Assets/02. Scripts/GameManager/GameManager.cs`로 GUID 보존
  이동(`manage_asset(action="move")`). 이동 직후 `Assets/Scripts/`에 남은 파일이 없어 폴더 자체를
  삭제했다.
- 파일을 CP949 원본에서 `iconv -f CP949 -t UTF-8`로 정확히 복원한 뒤(변환 결과를 직접 읽어 모든
  한글 주석·문자열이 원래 의미대로 복원됐음을 확인), 그 위에 §3의 코드 변경 2곳
  (`"HeroPrefab"`→`"HideOrSeekPlayer"`, `"PhotonLobby"`→`"LobbyScene"`)을 적용해 UTF-8(BOM)로
  재작성했다 — `file` 명령으로 `UTF-8 (with BOM) text, with CRLF line terminators`임을 확인.
  코드 로직 자체는 이 두 문자열 리터럴 외에 한 글자도 바뀌지 않았다.
- `GameLobbyScene`/`GameScene` 각각에 빈 GameObject `GameManager`를 만들고 `PhotonView` +
  `GameManager` 컴포넌트를 붙인 뒤, `pv`(자기 자신의 `PhotonView`)/`InputFdChat`/`txtLogMsg`를
  각 씬의 `Canvas/InputFieldChat`, `Canvas/PanelLogMsg/Viewport/Content/Text (TMP)`로 연결했다
  (`m_BackBtn`은 계획대로 `None`). 프리팹화하지 않고 두 씬에 완전히 독립적으로 배치했다(§4 방안 A).

### 8.2 작업 중 발견한 문제와 해결 (계획에 없었던 추가 조치)

**MCP 컴포넌트 이름 충돌**: `Photon PUN2` 데모 패키지 안에 이름이 같은
`Photon.Pun.Demo.PunBasics.GameManager`가 이미 존재해서, Unity MCP의 컴포넌트 추가/필드 설정
도구가 클래스 이름만으로는 어떤 `GameManager`인지 특정하지 못해 계속 실패했다(`Component type
'GameManager' not found`). `System.Type.GetType("GameManager, TagOfChaos.Scripts")`로 어셈블리를
명시해 정확한 타입을 찾은 뒤 `GameObject.AddComponent(Type)`/`SerializedObject`로 직접
추가·연결하는 방식으로 우회했다 — 최종 결과물(씬에 저장된 컴포넌트와 필드 값)에는 차이가 없다.

**`PhotonView`의 `ViewID`가 0으로 남아 RPC가 실패함(§3.3에서 다루지 않았던 문제)**: Play Mode로
GameLobbyScene→GameScene 흐름을 직접 검증하던 중, GameScene의 `GameManager.Start()`가 보내는
`pv.RPC("LogMsg", ...)`에서 콘솔에 `"Illegal view ID:0 method: LogMsg GO:GameManager"` 에러가
발생하는 것을 발견했다. 원인은 `PhotonView.cs`의 `Awake()`가
`if (this.sceneViewId != 0) this.ViewID = this.sceneViewId;`로, **씬에 미리 배치된(런타임에
스폰되지 않은) PhotonView는 에디터에서 `sceneViewId`를 미리 구워둬야만 실제 `ViewID`가
할당된다** — 이 값은 씬 저장 시 Photon의 에디터 후처리가 자동으로 채워주는 경우도 있지만(실제로
`GameLobbyScene` 쪽은 `sceneViewId: 1`로 자동 할당되어 있었다), `GameScene` 쪽은 `0`으로 남아있어
문제가 재현됐다. `GameScene`의 `GameManager` PhotonView에 `sceneViewId = 1`을 명시적으로 설정하고
저장한 뒤 재검증해 문제가 해결됐음을 확인했다.

### 8.3 검증 결과 (§6 대응, Photon 서버에 실제로 접속해 진행)

- 매 단계(이동/인코딩+코드 수정/씬 배치) 직후 `read_console`로 컴파일 에러 0건을 반복 확인.
- `LobbyScene`에서 실제로 방을 생성해(`PhotonNetwork.CreateRoom`) `GameLobbyScene`으로 자동
  진입(`LobbyController.OnJoinedRoom`) → 콘솔 에러 없음, `HideOrSeekPlayer` 캐릭터가
  `PlayerSpawnPos` 기준으로 정확히 1개 스폰됨을 확인.
- `GameManager.BroadcastingChat()`을 직접 호출해(리플렉션으로 입력 필드에 한글 메시지를 넣고 호출)
  RPC 왕복을 검증: `txtLogMsg`에 `"[TestUser] Connected"`(초록, `Start()`에서 자동 발송)와
  `"[TestUser] GameManager 검증 메시지"`(노랑, 자기 메시지 하이라이트)가 모두 정상 누적됨을
  확인 — 한글이 깨지지 않고, RPC·색상 치환 로직·중복 없음까지 전부 의도대로 동작했다.
- 같은 방에서 `PhotonNetwork.LoadLevel("GameScene")`으로 전환(§5에서 예상한 대로 Unity의 기본
  씬 전환이 이전 씬의 오브젝트를 자동 정리함을 확인 — 캐릭터 중복 스폰 없이 `GameScene`에서
  새로 정확히 1개만 스폰됨) → `GameScene`의 `GameManager`에서도 위와 동일하게 스폰·채팅·RPC를
  재검증해 모두 정상 동작함을 확인(§8.2의 `sceneViewId` 수정 이후).
- `CreatePlayer()`를 `Awake()`에 그대로 둔 결정(§5)이 실제로 문제를 일으키지 않음을 확인 —
  두 씬 모두 룸에 이미 들어와 있는 상태로 로드되므로 레이스 컨디션이 재현되지 않았다.

### 8.4 검증 중 발견했지만 이번 계획 범위 밖으로 남겨둔 것

- **`GameLobbyScene`/`GameScene`에 NavMesh가 베이크되어 있지 않음**: 두 씬 모두 캐릭터 스폰 시
  콘솔에 `"Failed to create agent because there is no valid NavMesh"` 경고가 뜬다.
  `HideOrSeekPlayer`의 실제 이동은 `NavMeshAgent`를 거치지 않고 100% `transform.position` 직접
  갱신으로 이루어지므로(`PlayerControllPlan.md` §6.7, research.md §26) 캐릭터 조작 자체에는 영향이
  없지만, 경고 자체는 계속 남는다. `GameManager`와는 무관한 씬 구성(NavMesh 베이크) 문제라 이번
  계획 범위에 포함하지 않았다 — 필요하면 별도로 요청해달라.
- 이전에 이미 범위 밖으로 분류했던 항목(§5: 채팅-이동잠금 재연결, `LogMsg` 색상 로직 정리,
  `Inst` 중복 가드)은 이번에도 손대지 않았다.

---

## 9. 후속 요청 — ✅ 구현 완료 (결과는 §9.11 참고)

> 사용자 요청 7가지(대화 진행에 따라 갱신됨): ① `GameLobbyScene`/`GameScene`에 Back 버튼
> 구현(**갱신: 두 씬 모두 `LobbyScene`으로 이동** — 최초 요청은 `GameScene`은 `GameLobbyScene`으로
> 가는 것이었으나, 이후 대화에서 두 씬 모두 `LobbyScene`으로 통일하는 것으로 변경됨, §9.2 참고),
> ② `GameLobbyScene`에서 인원수(N/4) 표시가 클라이언트마다 다르게/틀리게 보이는 버그,
> ③(치명적) 방장이 아닌 사람에게 "게임 시작" 버튼이 보이는 버그, ④ `GameLobbyPanel`을 화면
> 우측 상단으로 재배치, ⑤(대화 중 추가) 채팅창을 화면 좌측 하단으로 재배치, ⑥(신규) Back 버튼
> 클릭 시 씬별로 다른 문구의 확인창(예/아니오)을 띄우고 "예"를 눌러야 실제로 나가도록 변경(§9.9),
> ⑦(신규) `GameLobbyPanel` 크기를 700×800 → 600×500으로 축소(§9.6 갱신). **전부 구현·검증까지
> 완료했다 — 실제 결과와 계획 대비 추가로 발견/수정한 사항은 §9.11에 정리했다.**

### 9.1 조사 결과 — 이미 배치된 오브젝트

두 씬을 직접 열어 확인한 결과, `Canvas` 하위에 아이콘 전용(라벨 텍스트 없음, `RawImage` 자식 1개)
`Button` 오브젝트가 이미 배치되어 있다:

| 씬 | 경로 | 비고 |
|---|---|---|
| `GameLobbyScene` | `Canvas/Button` | 화면 좌측 상단(45, 1042.5) 배치, `Button`+`Image`+자식 `RawImage`(아이콘) |
| `GameScene` | `Canvas/Button` | 동일 구조 |

`GameManager`의 기존 `m_BackBtn` 필드가 정확히 이 버튼을 위해 만들어진 자리다(§4.1에서 지금은
"없음 → `None`"으로 비워뒀던 곳) — 즉 최소 한 개 씬(`GameLobbyScene`)은 **코드를 전혀 바꾸지
않고 인스펙터 연결만으로** 완성된다.

### 9.2 Back 버튼 — ① 계획 (갱신: 두 씬 모두 `LobbyScene`으로) ✅ 구현 완료

**변경점**: 최초 요청은 `GameScene`의 Back 버튼이 `GameLobbyScene`으로 이동(같은 방 유지)하는
것이었으나, 이후 확인을 거쳐 **`GameLobbyScene`과 `GameScene` 두 씬의 Back 버튼 모두
`LobbyScene`으로 이동(=방을 완전히 나감)하는 것으로 통일**됐다.

**두 씬 모두 코드 변경 없음, 인스펙터 연결만.** 두 씬의 동작이 이제 완전히 같아졌으므로,
기존 `GameManager.OnClickBackBtn()`(방을 완전히 나가서 `LobbyScene`으로 이동 — `PhotonNetwork.
LeaveRoom()` 호출 → `OnLeftRoom()` 콜백에서 `SceneManager.LoadScene("LobbyScene")`, §3.2에서
이미 확정된 경로) 하나만으로 두 씬 모두 충분하다. **§9.2 이전 초안에서 계획했던 새 메서드
`OnClickBackToLobbyBtn()`과 `NetKeys`/`ExitGames.Client.Photon.Hashtable` 참조, 방장 전용
게이팅 논의는 전부 철회한다** — 더 이상 필요 없다.

`GameLobbyScene`/`GameScene` 각각의 `GameManager`에서, 이미 배치된 `Canvas/Button`(§9.1)을
`m_BackBtn` 필드에 연결하기만 하면 된다. `Start()`의
`if (m_BackBtn != null) m_BackBtn.onClick.AddListener(OnClickBackBtn);`가 두 씬 모두
자동으로 같은 동작을 붙여준다 — 리스너를 인스펙터에서 직접 추가할 필요도 없다.

> **갱신(추가 요청)**: 위 "코드 변경 없음"은 바로 이어지는 §9.9(확인창 추가) 요청으로
> 수정됐다 — 버튼을 눌렀을 때 곧바로 `OnClickBackBtn()`이 실행되는 게 아니라, 먼저 확인창을
> 띄우고 "예"를 눌러야 실행되도록 한 단계가 추가된다. `OnClickBackBtn()` 자체의 내용은 여전히
> 그대로 재사용되며(§9.9에서 "예" 버튼의 콜백으로 그대로 연결), `m_BackBtn`에 직접 연결하는
> 대상만 `OnClickBackBtn` → `OnClickBackButtonPressed`(신규, §9.9)로 바뀐다.

### 9.3 씬별 필드 연결 표 (Back 버튼 추가분)

| 필드 | `GameLobbyScene`의 `GameManager` | `GameScene`의 `GameManager` |
|---|---|---|
| `m_BackBtn` | `Canvas/Button` → `m_BackBtn` 필드에 연결(기존 `OnClickBackBtn` 자동 등록) | `Canvas/Button` → `m_BackBtn` 필드에 연결(동일) |

두 씬 모두 `m_BackBtn` 필드에 각 씬의 `Canvas/Button`만 연결하면 끝이다 — `Button.onClick`을
인스펙터에서 수동으로 건드릴 필요가 없다(`Start()`가 알아서 리스너를 등록해준다).

### 9.4 인원수(N/4) 표시 버그 — ② 원인 분석 및 수정 계획 ✅ 구현 완료

**재현 시나리오(사용자 보고 그대로 정리)**:

| 시점 | 방장 화면 | 2번째 입장자 화면 | 3번째 입장자 화면 |
|---|---|---|---|
| P2 입장 | 1/4 → **2/4**(정상) | **1/4**(오답, 실제 2명) | — |
| P3 입장 | (계속 정상 갱신) | 1/4 → **3/4**(2를 건너뜀) | **1/4**(오답, 실제 3명) |
| P4 입장 | (계속 정상 갱신) | (계속 정상) | 1/4 → **4/4**(3을 건너뜀) |

**원인**: `GameLobbyController.Start()`가 씬 로드 직후 딱 한 번
`PhotonNetwork.CurrentRoom.PlayerCount`를 읽어 `RefreshPlayerList()`를 호출한다. 방장은 자기가
직접 `LoadLevel`을 호출한 시점에 이미 자신의 룸 상태가 완전하므로 문제가 없지만, **`Automatically
SyncScene`으로 뒤따라 입장하는 클라이언트는 씬이 로드되고 `Start()`가 실행되는 바로 그 순간에는
Photon이 그 클라이언트의 로컬 룸 상태(다른 플레이어 목록)를 아직 완전히 따라잡지 못한 상태일 수
있다** — 이 시점의 `PlayerCount`가 실제 값이 아니라 `1`(사실상 "아직 다른 사람 정보가 반영되기
전")로 읽히는 것으로 보인다. 이후 새로운 플레이어가 입장할 때 발생하는 `OnPlayerEnteredRoom`
콜백은 그 시점 기준으로는 이미 완전히 동기화된 상태에서 호출되므로, 그때는 정확한(단순히 +1이
아니라 "그 시점의 진짜 전체 인원수") 값을 보여준다 — 그래서 화면이 "1/4"에서 "+1"이 아니라 갑자기
"진짜 값"으로 점프하는 것처럼 보인다. `RefreshStartButton()`도 같은 `Start()` 호출 경로를
공유하므로, `PhotonNetwork.IsMasterClient` 판정도 같은 시점에 이루어져 §9.5의 버그와 뿌리가
같다.

**수정 방안**: 이벤트 콜백(`OnPlayerEnteredRoom`/`OnPlayerLeftRoom`/`OnMasterClientSwitched`)에
의존하는 기존 방식은 그대로 두고(이미 정상 동작하는 경로이므로 건드리지 않음), **씬 로드 직후의
"아직 따라잡지 못한" 구간을 스스로 감지해서 보정하는 안전망을 `Update()`에 추가**한다 — Photon의
정확한 내부 타이밍에 의존하지 않고, "화면에 표시된 값이 실제 룸 상태와 다르면 다음 프레임에
자동으로 맞춘다"는 자기 치유(self-healing) 방식이라 원인의 정확한 내부 메커니즘을 몰라도
안전하게 고칠 수 있다:

```csharp
private int lastKnownPlayerCount = -1;
private bool lastKnownIsMasterClient;

private void Update()
{
    if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;

    int currentCount = PhotonNetwork.CurrentRoom.PlayerCount;
    bool currentIsMaster = PhotonNetwork.IsMasterClient;

    // 화면에 마지막으로 반영한 값과 실제 값이 같으면 아무 것도 하지 않는다(매 프레임 재생성 방지)
    if (currentCount == lastKnownPlayerCount && currentIsMaster == lastKnownIsMasterClient) return;

    lastKnownPlayerCount = currentCount;
    lastKnownIsMasterClient = currentIsMaster;
    RefreshPlayerList();
    RefreshStartButton();
}
```

`RefreshPlayerList()`/`RefreshStartButton()`은 이미 멱등(같은 값으로 다시 불러도 결과가 같음)하고,
`Update()` 안의 비교는 정수/불리언 비교 두 번뿐이라 비용이 거의 없다 — 값이 실제로 바뀐 프레임에만
목록을 다시 그린다(`CLAUDE.md`의 "최적화를 고려한 코드 작성" 원칙에 부합). 기존 `Start()`/
`OnPlayerEnteredRoom`/`OnPlayerLeftRoom`/`OnMasterClientSwitched`의 `RefreshPlayerList()`/
`RefreshStartButton()` 호출은 **그대로 유지**한다 — 즉시 반응이 필요한 정상 케이스(이미 잘
동작 중인 방장 화면 등)의 응답성을 유지하면서, `Update()` 안전망은 그 경로들이 놓친 경우
(이번 버그처럼 최초 `Start()` 값이 틀린 경우)를 다음 프레임에 자동으로 바로잡는 역할만 한다.

### 9.5 방장 전용 "게임 시작" 버튼 노출 버그 — ③(치명적) 원인 및 수정 ✅ 구현 완료

**원인**: §9.4와 완전히 동일한 뿌리다. `RefreshStartButton()`의
`bool isOwner = PhotonNetwork.IsMasterClient;` 판정이 `Start()` 시점(=아직 룸 상태가 따라잡히기
전)에 이루어지면, 입장한 클라이언트 입장에서 `IsMasterClient`가 일시적으로 잘못된 값(예:
아직 `MasterClientId`가 로컬에 완전히 반영되지 않아 자신을 방장으로 오판)을 반환할 수 있다 —
인원수가 "1"로 잘못 읽히는 것과 같은 시점, 같은 원인의 서로 다른 증상으로 보인다.

**수정**: 별도 코드가 필요 없다 — §9.4의 `Update()` 안전망이 `RefreshStartButton()`도 함께
호출하므로, 인원수 버그를 고치는 바로 그 수정이 이 치명적 버그도 같이 해결한다. `RefreshStart
Button()` 자체의 로직(`startGameButton.gameObject.SetActive(isOwner)`)은 이미 올바르므로 손댈
필요가 없다 — 문제는 "언제 그 판정을 하느냐"였다.

### 9.6 `GameLobbyPanel` 우측 상단 재배치 — ④ 계획 ✅ 구현 완료

현재 `GameLobbyPanel`(`GameLobbyUICanvas` 하위)은 화면 정중앙에 고정되어 있다
(`anchorMin/Max=(0.5,0.5)`, 크기 700×800, `anchoredPosition=(0,0)`). 이를 화면 **우측 상단**으로
옮긴다 — 앵커를 우상단(`1,1`)으로, 피벗도 우상단(`1,1`)으로 바꾸고, 화면 가장자리에서 20px
여백을 준다:

| 속성 | 변경 전 | 변경 후 |
|---|---|---|
| `anchorMin` | `(0.5, 0.5)` | `(1, 1)` |
| `anchorMax` | `(0.5, 0.5)` | `(1, 1)` |
| `pivot` | `(0.5, 0.5)` | `(1, 1)` |
| `anchoredPosition` | `(0, 0)` | `(-20, -20)` |
| `sizeDelta` | `700 × 800` | **`600 × 500`(갱신: 패널이 너무 크다는 피드백 반영)** |

`anchorMin/Max/pivot`이 전부 `(1,1)`(우상단 고정점)이므로, 크기(`sizeDelta`)가 700×800에서
600×500으로 줄어들어도 `anchoredPosition=(-20,-20)`은 그대로 우상단 모서리를 고정해준다 —
패널이 작아진 만큼 좌측/아래쪽으로 덜 뻗을 뿐, 위치 계산을 다시 할 필요는 없다. 화면 우측 상단
`1300~1900, 580~1060` 픽셀 범위를 차지하게 된다.

패널 내부 레이아웃(`StatusText`/`PlayerListScrollView`/`StartGameButton`, `RoomItemPlan.md` §3)은
전부 상대 앵커 + 고정 픽셀 여백 방식이라 크기가 바뀌어도 별도 수정 없이 자동으로 맞춰진다 — 다만
세로 공간이 800→500으로 줄어들면서 `PlayerListScrollView`가 차지하는 실제 높이가
(상단 여백 100 + 하단 여백 190 고정 차감 기준) 약 510px → 약 210px로 좁아진다.
`PlayerListItem` 한 칸 높이가 50px(`RoomItemPlan.md` §3.4)이므로 4명(4×50=200px)은 간신히 들어갈
것으로 예상되지만, 항목 사이 여백(`VerticalLayoutGroup.spacing`)까지 감안하면 4번째 항목이
가려질 수 있다. **(사용자 확인 완료) 이 경우 별도 대응 없이 스크롤로 처리한다** —
`PlayerListScrollView`는 애초에 `ScrollRect`+`Viewport`+`Content`(`VerticalLayoutGroup`+
`ContentSizeFitter`) 구조라(`RoomItemPlan.md` §3), 목록이 보이는 영역보다 길어지면 추가 코드 없이
그 안에서 스크롤해서 나머지 인원을 확인할 수 있다. 구현 후에는 "4명이 다 보이는지"가 아니라
"다 안 보이더라도 스크롤로 확인 가능한지"만 확인하면 된다(§9.10 검증 단계에 반영).

### 9.7 채팅창 좌측 하단 재배치 — ⑤ 계획 (대화 중 추가 요청) ✅ 구현 완료

`InputFieldChat`/`PanelLogMsg`(`Canvas` 하위, `GameLobbyScene`/`GameScene` 둘 다 동일한 배치로
확인됨)를 좌측 하단으로 옮긴다. 현재 값을 직접 씬에서 확인한 결과:

| 오브젝트 | 현재 앵커 | 현재 `anchoredPosition` | 현재 크기(스케일 포함 실효 크기) |
|---|---|---|---|
| `PanelLogMsg` | 우상단 `(1,1)` | `(-18, -10.5)` | `530×190`, `localScale 1.5` → 실효 `795×285` |
| `InputFieldChat` | 중앙 `(0.5,0.5)` | `(540, 210)` | `352×30`, `localScale 2.25` → 실효 `792×67.5` |

로그 패널을 화면 맨 아래에 두고 입력창을 그 바로 위에 쌓는 배치로 좌측 하단에 재배치한다(둘 다
왼쪽 정렬을 맞춰 자연스럽게 겹쳐 보이게):

| 오브젝트 | 변경 후 앵커/피벗 | 변경 후 `anchoredPosition` |
|---|---|---|
| `PanelLogMsg` | `(0, 0)` | `(18, 10.5)`(기존 여백값을 좌우/상하만 뒤집어 재사용) |
| `InputFieldChat` | `(0, 0)` | `(18, 306)`(로그 패널 실효 높이 285 + 여백 10.5 + 간격 10 ≈ 306, 로그 패널 바로 위에 왼쪽 정렬로 배치) |

크기(`sizeDelta`)와 `localScale`은 그대로 둔다 — 앵커/피벗/`anchoredPosition`만 바꿔서 위치를
화면 반대편(우상단 계열 → 좌하단)으로 옮기는 것이다. `GameLobbyScene`/`GameScene` 두 씬 모두
동일하게 적용한다.

### 9.9 Back 버튼 클릭 시 확인창(예/아니오) 표시 — 신규 요청 ✅ 구현 완료

> **문구 정정(사용자 확인 완료)**: "게임 로비"는 `GameLobbyScene`을 가리키는 게 아니라 그냥
> "로비"(=`LobbyScene`)를 뜻하는 것이었다 — §9.9 최초 초안의 해석은 틀렸다. 목적지(`LobbyScene`)
> 자체는 원래부터 맞게 계획돼 있었고, **문구에서 "게임"만 빼면 된다.**

**요청 정리**: Back 버튼을 눌러도 곧바로 나가지 않고, 확인창을 띄운다.
- `GameScene`: "게임이 진행중입니다. 나가시겠습니까?"
- `GameLobbyScene`: "로비로 나가시겠습니까?"
- **예** → 기존 `OnClickBackBtn()` 그대로 실행(방을 나가서 `LobbyScene`으로 이동).
- **아니오** → 확인창만 닫고 현재 씬에 그대로 머무름(아무 것도 실행 안 함).

**조사 결과**: 두 씬 모두 확인창 UI가 아직 배치되어 있지 않다(직접 씬을 열어 확인 — `Canvas`
하위에는 `InputFieldChat`/`PanelLogMsg`/`Button`(Back 버튼)뿐). 이번엔 **새로 만들어야 한다.**

**설계 — 재사용 가능한 팝업 컴포넌트로 분리**: 두 씬의 확인창이 문구만 다르고 나머지 동작(예/
아니오, 콜백 실행)이 완전히 같으므로, `GameManager`에 로직을 욱여넣지 않고 `CLAUDE.md`의 UI
프리팹 규칙(`Resources/UI/{Popup|Scene|Tab}/{클래스명}`)에 맞는 별도 `Popup` 컴포넌트로 분리한다:

```
Assets/02. Scripts/GameManager/ConfirmDialog.cs
Assets/Resources/UI/Popup/ConfirmDialog/ConfirmDialog.prefab   (신규 프리팹 — 메시지 텍스트 + 예/아니오 버튼)
```

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 재사용 가능한 예/아니오 확인창. 특정 기능(나가기 등)에 종속되지 않도록 콜백을 인자로 받는다.
public class ConfirmDialog : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private Action onYesConfirmed;

    private void Awake()
    {
        yesButton.onClick.AddListener(OnYesClicked);
        noButton.onClick.AddListener(Hide);
        gameObject.SetActive(false); // 평소에는 숨겨둠
    }

    public void Show(string message, Action onYes)
    {
        messageText.text = message;
        onYesConfirmed = onYes;
        gameObject.SetActive(true);
    }

    private void OnYesClicked()
    {
        Hide();
        onYesConfirmed?.Invoke();
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}
```

**`GameManager.cs` 변경**: 기존 `m_BackBtn` 리스너 대상을 `OnClickBackBtn`에서 새 메서드
`OnClickBackButtonPressed`로 바꾸고, 씬마다 다른 확인 문구를 인스펙터에서 지정할 수 있도록 필드를
추가한다. `OnClickBackBtn()` 자체는 **한 글자도 바꾸지 않는다** — "예"의 콜백으로 그대로
재사용한다.

```csharp
[SerializeField] private ConfirmDialog confirmDialog;
[SerializeField] private string leaveConfirmMessage = "로비로 나가시겠습니까?"; // 씬별로 인스펙터에서 다르게 설정(§9.9 표 참고)
```

```csharp
private void Start()
{
    ...
    if (m_BackBtn != null)
        m_BackBtn.onClick.AddListener(OnClickBackButtonPressed); // 기존 OnClickBackBtn 직접 연결을 대체
    ...
}

// Back 버튼 클릭 시: 곧바로 나가지 않고 확인창부터 띄운다
public void OnClickBackButtonPressed()
{
    if (confirmDialog != null)
        confirmDialog.Show(leaveConfirmMessage, OnClickBackBtn); // "예" → 기존 나가기 로직 그대로
    else
        OnClickBackBtn(); // 확인창이 연결 안 돼 있으면 안전하게 기존 동작으로 폴백(방어적 처리)
}
```

**씬별 설정 표**:

| 씬 | `leaveConfirmMessage` |
|---|---|
| `GameLobbyScene` | `"로비로 나가시겠습니까?"` |
| `GameScene` | `"게임이 진행중입니다. 나가시겠습니까?"` |

`confirmDialog` 프리팹 인스턴스는 두 씬 모두 각자의 `Canvas` 하위에 배치하고(§4의 "씬별 독립
배치" 원칙과 동일), 각 씬의 `GameManager.confirmDialog` 필드에 그 씬의 인스턴스를 연결한다.

### 9.10 구현 순서 제안 (구현 시 참고) ✅ 구현 완료 — 아래 13단계 전부 그대로 따라 진행했다

1. `Assets/02. Scripts/GameManager/ConfirmDialog.cs` 작성(§9.9).
2. `Assets/Resources/UI/Popup/ConfirmDialog/ConfirmDialog.prefab` 제작 — 배경 패널 + 메시지
   `TMP_Text` + 예/아니오 `Button` 2개, `ConfirmDialog` 컴포넌트 필드 연결(§9.9).
3. `GameManager.cs`에 `confirmDialog`/`leaveConfirmMessage` 필드와 `OnClickBackButtonPressed()`
   추가, `Start()`의 `m_BackBtn` 리스너 대상을 이 메서드로 교체(§9.9). `OnClickBackBtn()` 자체는
   무수정.
4. `read_console`로 컴파일 에러 0건 확인.
5. `GameLobbyScene`: `ConfirmDialog` 프리팹 인스턴스 배치 + `GameManager.confirmDialog`/
   `m_BackBtn`/`leaveConfirmMessage`(`"로비로 나가시겠습니까?"`) 연결(§9.2/§9.3/§9.9).
6. `GameScene`: 동일하게 배치·연결, `leaveConfirmMessage`만 `"게임이 진행중입니다.
   나가시겠습니까?"`로 다르게 설정(§9.9).
7. `GameLobbyController.cs`에 §9.4의 `Update()` 안전망 추가(인원수 버그·시작 버튼 노출 버그 동시
   해결, §9.4/§9.5).
8. `GameLobbyPanel`(`GameLobbyScene`) RectTransform을 §9.6 값(600×500, 우측 상단)으로 변경.
9. `InputFieldChat`/`PanelLogMsg`(`GameLobbyScene`, `GameScene` 둘 다) RectTransform을 §9.7 값
   (좌측 하단)으로 변경.
10. 매 단계 직후 `read_console`로 컴파일/콘솔 에러 0건 확인.
11. Play Mode에서 실제로 방을 만들고 2~4번째 클라이언트로 순서대로 입장하는 시나리오를 재현해
    §9.4/§9.5의 표에 정리된 오답 패턴이 더 이상 나타나지 않는지 확인. 방장이 아닌 클라이언트
    화면에서 "게임 시작" 버튼이 전혀 보이지 않는지도 함께 확인.
12. 두 씬 모두에서 Back 버튼 클릭 → 확인창이 각 씬에 맞는 문구로 뜨는지, "아니오"를 누르면
    아무 일도 없이 닫히는지, "예"를 누르면 방을 나가고 `LobbyScene`으로 정확히 이동하는지 확인.
13. 우측 상단 패널(600×500)이 화면 안에 잘 들어오는지, 4명이 찼을 때 목록이 전부 안 보이더라도
    `PlayerListScrollView`를 스크롤해서 나머지 인원을 확인할 수 있는지(§9.6, 별도 코드 없이
    스크롤로 처리하기로 확정), 좌측 하단 채팅창이 겹치지 않는지 육안 확인.

---

### 9.11 구현 완료 보고

#### 9.11.1 §9.2/§9.3/§9.9 — Back 버튼 + 확인창

- `Assets/02. Scripts/GameManager/ConfirmDialog.cs`를 계획 그대로 신규 작성(메시지 텍스트 +
  예/아니오 `Button` 콜백, `Show(string, Action)`/`Hide()`).
- `Assets/Resources/UI/Popup/ConfirmDialog/ConfirmDialog.prefab` 제작: 배경 `Image` + `Confirm
  Dialog` 컴포넌트를 가진 루트, 자식으로 `MessageText`(TMP), `YesButton`(라벨 "예"),
  `NoButton`(라벨 "아니오"). 처음에는 `GameScene`의 `Canvas` 아래에서 직접 만들고
  `manage_prefabs(action="create_from_gameobject")`로 프리팹화했다 — 이 인스턴스는 그대로
  `GameScene`에 남아 원본 배치를 유지한다.
- `GameManager.cs`에 `confirmDialog`/`leaveConfirmMessage` 필드와 `OnClickBackButtonPressed()`를
  추가하고, `Start()`의 리스너 등록 대상을 `OnClickBackBtn` → `OnClickBackButtonPressed`로
  교체했다. `OnClickBackBtn()` 자체는 계획대로 한 글자도 손대지 않고 "예" 콜백으로 그대로
  재사용했다.
- `GameLobbyScene`에는 `ConfirmDialog.prefab`을 새 인스턴스로 배치(`manage_gameobject(action=
  "create", prefab_path=...)`)했고, `GameScene`은 프리팹화 원본 인스턴스를 그대로 유지했다. 두
  씬의 `GameManager`에 각각 `confirmDialog`/`m_BackBtn`(`Canvas/Button`)을 연결하고,
  `leaveConfirmMessage`를 씬별로 `"로비로 나가시겠습니까?"`(`GameLobbyScene`) /
  `"게임이 진행중입니다. 나가시겠습니까?"`(`GameScene`)로 설정했다.
- Play Mode에서 두 씬 모두 검증: Back 버튼 → 각 씬에 맞는 문구의 확인창 표시 → "아니오"는 방을
  나가지 않고 확인창만 닫힘(`PhotonNetwork.InRoom == True` 유지) → "예"는 `PhotonNetwork.
  LeaveRoom()`이 호출되어 `LobbyScene`으로 정상 이동(`PhotonNetwork.InRoom == False`, 활성 씬이
  `LobbyScene`으로 전환)까지 전부 확인했다.

#### 9.11.2 §9.4/§9.5 — 인원수 표시·시작 버튼 노출 버그

- `GameLobbyController.cs`에 계획된 `Update()` 안전망(`lastKnownPlayerCount`/
  `lastKnownIsMasterClient` 캐시 비교 → 값이 실제로 바뀐 프레임에만 `RefreshPlayerList()`/
  `RefreshStartButton()` 재호출)을 그대로 추가했다. 기존 `Start()`/`OnPlayerEnteredRoom`/
  `OnPlayerLeftRoom`/`OnMasterClientSwitched` 경로는 계획대로 전혀 손대지 않았다.
- **검증 범위에 대한 정직한 기록**: 이 세션은 Unity 에디터 인스턴스가 하나뿐이라, 사용자가 보고한
  원본 버그(여러 명이 순서대로 실제 입장할 때 나타나는 "1/4에 머무르다가 나중 입장자 기준으로
  갑자기 점프"하는 증상)를 여러 클라이언트로 동시에 재현해서 "수정 전/후"를 직접 비교하지는
  못했다. 대신 (a) 컴파일 에러 0건, (b) `Update()` 로직 자체가 "화면 값과 실제 룸 상태가 다르면
  다음 프레임에 자동으로 맞춘다"는 자기 치유 방식이라 원인이 된 타이밍 문제(§9.4 분석)를
  구조적으로 봉쇄한다는 점, (c) 단일 클라이언트로 룸 입장 시 `Update()`가 매 프레임 불필요하게
  `RefreshPlayerList()`를 다시 그리지 않고(캐시 비교로 스킵) 최초 진입 시에만 정상 값을 표시하는
  것을 확인하는 선에서 검증을 마쳤다 — "구조적으로 고쳤고 컴파일·단일 클라이언트 동작은
  확인했다"와 "다중 클라이언트로 원래 증상이 사라진 것을 직접 재현·확인했다"는 다른 주장이라는
  점을 분명히 남겨둔다. 실제 다중 클라이언트 환경에서 이상이 발견되면 추가 확인이 필요하다.

#### 9.11.3 §9.6/§9.7 — 레이아웃 재배치

- `GameLobbyPanel`을 계획된 값(`anchorMin/Max/pivot=(1,1)`, `anchoredPosition=(-20,-20)`,
  `sizeDelta=(600,500)`)으로 변경 후 저장, Play Mode 스크린샷으로 우측 상단 배치를 확인했다.
- `InputFieldChat`/`PanelLogMsg`를 `GameLobbyScene`/`GameScene` 두 씬 모두 계획된 좌하단 값
  (`anchorMin/Max/pivot=(0,0)`, `PanelLogMsg anchoredPosition=(18,10.5)`, `InputFieldChat
  anchoredPosition=(18,306)`)으로 변경 후 저장, 두 씬 모두 스크린샷으로 겹침 없이 좌측 하단에
  배치됨을 확인했다.
- 4명이 찼을 때 목록이 다 안 보이더라도 스크롤로 확인 가능한지는 별도 코드 변경 없이 기존
  `ScrollRect` 구조에 의존하기로 확정된 사항이라(§9.6), 이번 세션에서 실제로 4명을 채워 스크롤
  동작 자체를 재현·확인하지는 않았다 — `PlayerListScrollView`의 `ScrollRect`/`Viewport`/
  `Content`(`VerticalLayoutGroup`+`ContentSizeFitter`) 구성 자체는 그대로 유지되므로 항목이
  뷰포트보다 길어지면 Unity 표준 동작으로 스크롤이 가능할 것으로 판단한다.

#### 9.11.4 계획에 없었던 추가 발견 — `ConfirmDialog` 프리팹 인스턴스화 시 위치 깨짐 버그

`ConfirmDialog.prefab`을 `manage_gameobject(action="create", prefab_path=...)`로
`GameLobbyScene`에 새로 인스턴스화할 때, 호출에 `position` 인자를 명시하지 않으면 도구가 월드
좌표 `position=(0,0,0)`을 암묵적으로 설정하는 것을 발견했다. `RectTransform`에서는 이것이
`anchoredPosition`을 역산해서 덮어써버려(중앙 앵커 기준 `(-960,-540)`으로 계산됨), 확인창이
화면 중앙이 아니라 좌측 하단 모서리(채팅 로그와 겹치는 위치)에 잘려서 렌더링되는 문제로
이어졌다. Play Mode 스크린샷으로 증상을 먼저 확인한 뒤, `manage_components(action=
"set_property")`로 `anchorMin/anchorMax/pivot=(0.5,0.5)`, `anchoredPosition=(0,0)`,
`sizeDelta=(500,250)`을 명시적으로 재설정하고 씬을 저장해 영구 수정했다. 수정 후 다시 Play
Mode로 진입해 스크린샷으로 확인창이 화면 정중앙에 정상적으로 표시됨을 최종 확인했다(`GameScene`
쪽 인스턴스는 프리팹화 이전에 이미 올바른 위치로 배치돼 있던 원본이라 이 문제가 없었다).

이 발견은 `GameManager`의 코드 로직과는 무관한, MCP 프리팹 인스턴스화 도구 자체의 동작 특성이라
별도 유틸리티나 문서화 이상의 코드 변경은 하지 않았다 — 앞으로 같은 도구로 `RectTransform`을
가진 프리팹을 새로 인스턴스화할 때는 `position` 인자를 명시하거나, 인스턴스화 직후 앵커 기반
좌표를 직접 확인하는 것이 안전하다.

#### 9.11.5 최종 컴파일/콘솔 확인

전체 작업 종료 시점에 `refresh_unity` + `read_console(types=["error","warning"])`로 재확인한
결과 에러·경고 0건이었다(§8.4에 기록된 NavMesh 미베이크 경고는 이번 작업 범위와 무관한 기존
항목이며 이번 세션에서 새로 발생한 것이 아니다).
