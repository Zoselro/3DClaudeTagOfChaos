# 아키텍처 리뷰: TagOfChaos (2026-08-15) — ⚠️ 구버전, `research.md` 참고

> **⚠️ 이 문서는 2026-08-15 스냅샷으로, 2026-08-16 `Plan.md/research.md`(§2/§4)가 이 문서의
> 11개 항목을 전부 최신 코드 기준으로 재검증해 대체했다.** 예를 들어 아래 §8.1의 "Photon 콜백
> 이중 등록(심각도: 높음)"은 이후 커밋에서 완전히 고쳐졌고, §9.2의 `RenderTexture` 누수도
> 해소됐다 — 이 문서만 단독으로 읽으면 이미 고쳐진 문제를 현재 버그로 오인할 수 있다.
> **최신 정보가 필요하면 이 문서 대신 `research.md`를 먼저 읽을 것.** 아래 내용은 그 시점의
> 원본 기록으로만 보존한다.

> `research.md`(전체 동작 방식 조사)와는 별개로, 이번 문서는 사용자가 지정한 11가지 아키텍처
> 관점(책임 분리, Manager 간 결합도, Prefab/Script 역할, Scene 의존, Singleton, SO 오용,
> Unity Lifecycle, Event 구독/해제, Object Pool, Photon Ownership/RPC, 중복 로직)으로만 코드를
> 다시 훑어 검사한 결과다. `Assets/02. Scripts/` 27개 파일 전체와 Photon PUN2 SDK 내부 구현
> (`MonoBehaviourPunCallbacks`, `PhotonNetwork.AddCallbackTarget`, `LoadBalancingClient`)까지
> 직접 읽고 대조해, 추측이 아니라 파일:라인 단위 근거로 뒷받침되는 항목만 담았다.

## 요약 (심각도순)

| # | 항목 | 심각도 | 한 줄 요약 |
|---|---|---|---|
| 1 | Photon 콜백 이중 등록 | **높음** | 7개 컴포넌트가 `base.OnEnable()`+직접 `AddCallbackTarget()`을 함께 호출해 모든 Photon 콜백이 두 번씩 실행됨 |
| 2 | 게임 시작 트리거 부재 | 높음(`research.md`에서 이미 다룸) | 이 문서에서는 다루지 않음 — `research.md` §7 참고 |
| 3 | Scene 이름 매직 스트링 중복 | 중간 | `"LobbyScene"`/`"GameLobbyScene"`/`"GameScene"`이 3개 파일 5곳에 하드코딩, 중앙 상수 없음 |
| 4 | `GameManager`의 책임 과다(SRP 위반) | 중간 | 채팅 중계 + 스폰 + 나가기/씬전환을 한 클래스가 담당 |
| 5 | `RoundIndex` 조회 로직 중복 | 중간 | 5개 파일이 거의 동일한 CustomProperties 조회 코드를 각자 구현 |
| 6 | RenderTexture 해제 누락 | 중간 | `PlayerPaintCanvas.PaintCanvas`가 파괴 시 `Release()`되지 않음 — 씬 전환마다 GPU 메모리 누적 |
| 7 | HideOrSeekPlayer 프리팹의 도메인 혼재 | 낮음~중간 | 이동(Unit)과 페인팅(ColorTag) 컴포넌트가 물리적으로 하나의 프리팹에 결합 |
| 8 | `GameLobbyController`의 UI 리스트 전체 재생성 | 낮음~중간 | 매 갱신마다 `Destroy` 후 전부 `Instantiate` — `LobbyController`의 diff 패턴과 비일관 |
| 9 | Camera_Ctrl 초기화 시점 의존성 | 낮음(잠재적) | 스폰 시점이 `Awake()`→`OnJoinedRoom()` 등으로 바뀌면 카메라 초기 각도가 깨질 수 있음 |
| 10 | Singleton/SO/Ownership | 문제 없음 | 아래 §5/§6/§10에서 상세 설명 — 이 3가지 관점은 오히려 양호한 편 |

---

## 1. 기존 책임 분리를 무시하는 코드가 있는가

### 1.1 `GameManager.cs` — 3가지 책임이 한 클래스에 (중간)

`Assets/02. Scripts/GameManager/GameManager.cs`는 다음 세 가지를 동시에 담당한다:
1. **채팅 중계**(`LogMsg` RPC, `BroadcastingChat`, `m_MsgList`)
2. **캐릭터 스폰**(`CreatePlayer()` — `Unit` 도메인의 관심사)
3. **방 나가기/씬 전환**(`OnClickBackBtn`, `OnLeftRoom` — `RoomLifecycleWatcher`가 이미 담당하는
   것과 같은 종류의 책임을 별도 클래스에서 중복 수행)

`RoomLifecycleWatcher.cs`(`ColorTag/`)가 이미 "방 생명주기 + 씬 전환"이라는 전용 책임을 갖고
있는데(§10.1 참고), `GameManager`도 `OnClickBackBtn()`/`OnLeftRoom()`으로 **같은 종류의 일**(방
나가기 → 씬 전환)을 별도 경로로 수행한다. 두 클래스가 서로를 모르는 채(참조 없음) 각자 독립적으로
"방을 나가면 `LobbyScene`으로 간다"는 동일한 결론에 도달하는 구조 — 지금은 각자 트리거 조건이
달라(사용자 버튼 클릭 vs 자동 감지) 실질적 충돌은 없지만, 책임 경계가 명확히 나뉘어 있지 않다.
`CreatePlayer()`도 마찬가지로 `Unit` 도메인이 스스로 처리해야 할 스폰 책임을 `GameManager`가 대신
지고 있다(구조상 `PlayerSpawnPos`/`HideOrSeekPlayer`라는 이름 문자열이 `Unit` 도메인이 아니라
`GameManager/` 폴더 안에 박혀 있음).

**권장**: `CreatePlayer()`를 별도의 `PlayerSpawner`(가칭, `Unit/` 또는 `GameManager/` 산하 전용
컴포넌트)로 분리하고, `GameManager`는 채팅 전용으로 좁히는 것을 고려. 최소한 "방 나가기" 책임은
`RoomLifecycleWatcher`와 `GameManager` 중 한쪽으로 통합하는 것이 바람직하나, 현재는 두 경로가
독립적으로 동작해도 충돌하지 않으므로 즉시 고쳐야 할 버그는 아니다.

### 1.2 `PlayerColorDisplay`가 `PlayerPaintCanvas`의 내부 리소스에 직접 접근 (경미)

`PlayerColorDisplay.ApplyColorReplace()`(89행)는 `paintCanvas.PaintCanvas`(공개 프로퍼티로 노출된
`RenderTexture`)를 직접 `Graphics.Blit`한다. `[RequireComponent(typeof(PlayerPaintCanvas))]`로
결합을 명시하고 있어 "같은 프리팹 위의 협력 컴포넌트"라는 설계 의도는 명확하지만, `PlayerPaintCanvas`
쪽에는 "다른 컴포넌트가 내 캔버스를 직접 Blit해도 된다"는 캡슐화 경계가 없다(단순 `{ get; private
set; }` 프로퍼티로만 노출). 지금 규모에서는 문제 없으나, 앞으로 캔버스를 다루는 로직이 더 늘어나면
`PlayerPaintCanvas`에 `ApplyExternalReplace(Material)` 같은 메서드를 열어주는 편이 캡슐화 관점에서
더 안전하다.

**전체적으로**: 프로젝트 전반의 책임 분리는 준수되는 편이다(`Unit/`의 5파일 분리, `ColorTag/`의
순수 함수 계층 분리, `Lobby/`의 매니저-뷰 분리 등은 모두 모범적). 위 두 항목이 확인된 유일한
위반이다.

---

## 2. Manager 간 의존성이 과도하게 증가하는가

**결론: 과도한 결합은 없다 — 오히려 "매니저끼리 직접 참조하지 않고 Photon CustomProperties를
공유 상태로 두는" 일관된 패턴이 프로젝트 전체에 적용되어 있다.**

`ColorSelectionManager`, `RoomLifecycleWatcher`, `ColorSelectionPanel`, `PlayerPaintCanvas`,
`BrushCursorController`, `PlayerColorVoteIndicator`, `PlayerColorDisplay` — 이 7개는 서로를 직접
참조(`[SerializeField]`로 서로를 주입)하지 않는다. 대신 전부 `PhotonNetwork.CurrentRoom.
CustomProperties`(또는 `Player.CustomProperties`)라는 공유 네트워크 상태를 각자 폴링/구독한다.
이는 결합도를 낮추는 올바른 선택이지만, 그 대가로 §11의 "중복 로직"이 발생한다 — **결합도와
중복은 트레이드오프 관계이며, 지금은 중복 쪽으로 치우쳐 있다.**

유일하게 관리자 간 직접 의존이 있는 곳은 `Dev/OfflineModeBootstrap.cs → ColorSelectionManager.
StartColorSelection()`(개발용, 문제 없음)과 `ColorSwatchButton.manager`(UI 위젯이 매니저를
참조하는 것은 정상적인 방향의 결합).

**진짜 문제는 결합 과다가 아니라 결합 부재다**: `GameLobbyController`(매칭 매니저)가
`ColorSelectionManager`(미니게임 매니저)를 전혀 모른다는 것이 `research.md` §7에서 이미 지적한
"게임 시작 트리거 없음" 공백의 원인이다. 즉 이 프로젝트는 매니저 간 결합이 과도한 것이 아니라,
**필요한 지점(게임 시작 시퀀스)에서조차 결합이 없어서 기능이 끊어져 있다.**

---

## 3. Prefab과 Script의 역할이 뒤섞이는가

### 3.1 `HideOrSeekPlayer.prefab`이 두 도메인을 물리적으로 묶고 있음 (경미~중간)

프리팹을 직접 열어 확인한 결과, 하나의 프리팹에 다음이 모두 부착되어 있다:
- `Unit` 도메인: `HideOrSeekPlayer`(이동 오케스트레이터)
- `ColorTag` 도메인: `PlayerPaintCanvas`, `PlayerColorVoteIndicator`(`VoteIndicator` 자식
  오브젝트의 `SpriteRenderer`), `PlayerColorDisplay`, 페인팅용 `MeshCollider`

코드 상으로는 `Unit`과 `ColorTag`가 서로를 참조하지 않는 별개 도메인으로 잘 분리돼 있지만
(`research.md` §7의 의존성 지도에서 확인), **물리적 산출물(프리팹)은 두 도메인을 하나로 융합**하고
있다. 이 때문에:
- `Unit` 도메인만 다루는 작업자가 이 프리팹을 열면 `ColorTag` 컴포넌트까지 함께 마주치게 되고,
  실수로 건드릴 위험이 있다.
- 반대로 `ColorTag`의 페인팅 로직만 테스트하려 해도 `Unit`의 이동 컴포넌트 전체를 포함한 무거운
  프리팹을 써야 한다(`PlayerTestScene`이 이 구조를 그대로 사용).
- 이 구조 자체가 "잘못됐다"기보다는(캐릭터라는 단일 게임 오브젝트가 여러 기능을 갖는 것은 자연스러움),
  **코드 레벨의 도메인 분리와 에셋 레벨의 결합도가 서로 다른 그림을 그리고 있다**는 점을 인지하고
  있어야 한다 — 특히 향후 도메인별 프리팹 변형(예: 관전자용, AI용)이 필요해지면 이 결합이 걸림돌이
  될 수 있다.

### 3.2 스크립트가 프리팹 내부 구조를 이름으로 탐색하지 않음 (양호)

`BrushCursorController.EnsureCursorInstance()`는 `brushSettings.CursorPrefab`을 통째로
`Instantiate`한 뒤 `GetComponentInChildren<Renderer>()`로 렌더러를 찾는다 — 자식 경로 문자열에
의존하지 않는 안전한 방식. `ConfirmDialog`/`PlayerListItem`/`RoomListItem` 등도 전부
`[SerializeField]`로 인스펙터에서 명시적으로 연결하며, `transform.Find("경로/문자열")` 류의 취약한
탐색은 **프로젝트 전체에서 단 한 건도 발견되지 않았다**(`GameManager.CreatePlayer()`의
`GameObject.Find("PlayerSpawnPos")`는 프리팹이 아니라 씬 오브젝트 탐색이므로 §4에서 별도로 다룸).

### 3.3 SO가 프리팹 참조를 갖는 것은 정상 (`BrushSettingsSO.cursorPrefab`)

데이터 에셋이 프리팹을 참조하는 것 자체는 Unity의 일반적인 패턴이며 역할 혼재가 아니다 — SO는
"어떤 프리팹을 쓸지"라는 설정값만 들고 있을 뿐, 그 프리팹의 생성/파괴/동작 로직은 여전히
`BrushCursorController`(스크립트)가 담당한다. §6에서 다시 확인.

---

## 4. Scene에 직접 의존하는 코드가 늘어나는가

**결론: 있다 — 정확히 5곳, 3개 파일에 흩어진 매직 스트링.**

| 파일:라인 | 코드 | 목적지 |
|---|---|---|
| `GameManager.cs:130` | `SceneManager.LoadScene("LobbyScene")` | 방 나가기(정상 클릭) |
| `RoomLifecycleWatcher.cs:69` | `SceneManager.LoadScene("LobbyScene")` | 방 나가기(비정상 종료) |
| `RoomLifecycleWatcher.cs:63` | `PhotonNetwork.LoadLevel("GameLobbyScene")` | 게임 정상 종료 복귀 |
| `LobbyController.cs:170` | `PhotonNetwork.LoadLevel("GameLobbyScene")` | 방 생성/입장 직후 |
| `GameLobbyController.cs:105` | `PhotonNetwork.LoadLevel("GameScene")` | 게임 시작 버튼 |

씬 이름 `"LobbyScene"`은 2개 파일에, `"GameLobbyScene"`은 2개 파일에 각각 독립적으로 하드코딩되어
있다. 이 프로젝트는 이미 `NetKeys.cs`/`NetEventCodes.cs`로 "문자열 상수는 별도 클래스에 모아
오타를 방지한다"는 컨벤션을 스스로 세워두었는데(§11에서 재언급), **씬 이름에는 이 컨벤션이
적용되지 않았다.** 씬 이름을 바꾸거나 오타를 낼 경우 컴파일 타임에 전혀 감지되지 않고, 런타임에
"씬을 찾을 수 없음" 에러로만 드러난다(구 버전 `research.md`가 지적했던 `"PhotonLobby"` 오타
사고가 정확히 이 구조 때문에 발생했었다 — 원인 자체는 고쳐졌지만 재발 방지 장치는 아직 없다).

**추가로**, `GameManager.CreatePlayer()`의 `GameObject.Find("PlayerSpawnPos")`(184행)도 씬 계층에
문자열로 의존하는 코드다 — 오브젝트 이름이 바뀌면 조용히(에러 없이) 스폰이 스킵된다(`if (hPosObj
!= null)` 가드가 실패를 숨김).

**권장**: `Assets/02. Scripts/GameManager/` 또는 공용 위치에 `SceneNames.cs` 같은 상수 클래스를
만들어 5곳을 통일하고, `PlayerSpawnPos`도 같은 방식으로 상수화하거나 최소한 `Debug.LogWarning`으로
탐색 실패를 드러내는 것을 고려.

---

## 5. Singleton이 남발되는가

**결론: 남발되지 않는다 — 프로젝트 전체에 static 싱글턴은 `GameManager.Inst` 단 하나뿐이며,
오히려 아무도 참조하지 않는 "죽은 싱글턴"이다.**

`grep`으로 프로젝트 전체(`Assets/02. Scripts/`)를 재확인한 결과, `static` 인스턴스 참조 패턴은
`GameManager.Inst`가 유일하다. `ColorSelectionManager`, `RoomLifecycleWatcher`,
`LobbyController`, `GameLobbyController` 등 나머지 매니저들은 모두 씬에 배치된 일반 컴포넌트로,
서로 `FindObjectOfType`이나 static 참조 없이 Photon 네트워크 상태(§2)로만 소통한다.

`GameManager.Inst` 자체의 문제점:
- `Awake()`에서 `Inst = this`로 무조건 덮어쓴다 — 중복 인스턴스 가드(`if (Inst != null) {
  Destroy(gameObject); return; }` 같은)가 없다. 지금은 각 씬(`GameLobbyScene`/`GameScene`)에
  `GameManager`가 정확히 1개씩만 배치돼 있어 실질적 충돌은 없지만, 방어 코드는 없다.
- **더 중요한 사실**: 프로젝트 전체에서 `GameManager.Inst`를 읽는 코드가 **0건**이다(과거
  `Hero_Ctrl.cs`가 `GameManager.Inst.Is_Conversating`을 읽었으나 그 파일 자체가 삭제됨). 즉
  이 싱글턴은 현재 아무 기능도 하지 않는 죽은 코드다.

**권장**: 새로운 싱글턴을 추가로 만들 필요는 없어 보이며(현재 구조가 이미 "매니저는 씬 배치 +
네트워크 상태 공유" 패턴으로 일관돼 있음), `GameManager.Inst`는 사용처가 생기기 전까지는 유지하되
필요 없다면 제거를 고려할 만하다(§8-2, `research.md` §8-2와 연계).

---

## 6. ScriptableObject의 책임이 잘못 사용되는가

**결론: 문제 없음 — 두 SO 모두 교과서적으로 올바르게 사용되고 있다.**

- **`ColorPaletteSO`**: `ColorEntry[] colors` 배열만 들고 있는 순수 읽기 전용 데이터(10색 팔레트).
  `GetColor(index)`/`GetColorName(index)`는 조회 메서드일 뿐 상태를 바꾸지 않는다.
- **`BrushSettingsSO`**: 붓 크기 범위/기본값/휠 감도/커서 프리팹 등 튜닝 가능한 설정값. 전부
  `[SerializeField] private` + 읽기 전용 프로퍼티(`public float MinRadius => minRadius;` 형태)로
  노출되며, **런타임에 이 값을 변경하는 코드는 프로젝트 어디에도 없다**(재확인 완료) — 즉 "여러
  플레이 세션 간 SO 인스턴스가 오염되는" 전형적인 SO 오용 패턴(런타임 변경 가능한 필드를 SO에 두어
  플레이 모드 종료 후에도 값이 남는 문제)이 발생하지 않는다.
- SO를 "런타임 상태 저장소"나 "이벤트 버스"로 쓰는 안티패턴(SO에 `public static` 필드를 두거나,
  SO의 메서드가 부작용을 일으키는 경우)도 발견되지 않았다.

두 SO 모두 `CreateAssetMenu`로 에디터에서 인스턴스를 만들 수 있게 되어 있고, `Assets/03. SO/
ColorTag/`에 실제 에셋(`DefaultColorPalette.asset`, `DefaultBrushSettings.asset`)이 정확히
하나씩만 존재한다 — 프로젝트 규모에 맞는 적절한 사용.

---

## 7. Unity Lifecycle 순서 문제가 있는가

### 7.1 `Camera_Ctrl` 초기화가 스폰 시점(`Awake()`)에 암묵적으로 의존 (낮음, 잠재적 위험)

현재 흐름: `GameManager.Awake()` → `CreatePlayer()` → `PhotonNetwork.Instantiate("HideOrSeekPlayer",
...)` → 새 인스턴스의 `HideOrSeekPlayer.Awake()`가 (동기적으로) 실행되어 `Camera_Ctrl.InitCamera()`
호출 → `Camera_Ctrl.m_Player` 설정.

이 모든 과정이 **씬의 최초 Awake 일괄 처리 단계 안에서** 일어나기 때문에(둘 다 씬 로드 시점에 이미
배치돼 있는 `GameManager`/`Main Camera`와, 그 Awake 도중 동적으로 Instantiate된 `HideOrSeekPlayer`가
모두 같은 프레임의 "전체 Awake → 전체 Start" 사이클에 포함됨), `Camera_Ctrl.Start()`가 실행되는
시점에는 이미 `m_Player`가 설정되어 있어 **지금은 정상 동작한다.** 실제로 직접 코드를 추적해
이 순서가 깨지지 않음을 확인했다.

**그러나 이는 상당히 미묘한 암묵적 전제에 기대고 있다**: `GameManager.md` §5가 이미 "`CreatePlayer()`를
`Awake()`가 아니라 `OnJoinedRoom()` 콜백으로 옮기는 것을 나중에 검토할 수 있다"고 명시적으로 열어둔
가능성인데, 만약 실제로 그렇게 옮겨지면 `HideOrSeekPlayer`의 스폰(따라서 `Camera_Ctrl.InitCamera()`
호출)이 `Camera_Ctrl.Start()` **이후**(다음 프레임 이후)로 밀려난다. 이 경우:
- `Camera_Ctrl.Start()`는 `m_Player == null`이라 조용히 스킵되고, `m_RotV = m_DefaultRotV(25°)`
  등 **1회성 초기 각도 설정이 영원히 실행되지 않는다**(`Start()`는 두 번 호출되지 않으므로).
- 이후 `LateUpdate()`는 매 프레임 동작하지만 `m_RotV`의 필드 기본값(`0.0f`)에서 시작하게 되어,
  카메라가 설계 의도(25° 부감)와 다른 정면 수평 각도로 고정된 채 시작하는 시각적 버그가 발생한다
  (우클릭 드래그로 수동 조정하기 전까지).

**권장**: `CreatePlayer()`/스폰 호출 시점을 바꾸는 리팩토링을 하게 되면, `Camera_Ctrl`의 초기화도
`Start()`의 1회성 로직에서 `InitCamera()` 호출 시점으로 옮기는 것을 함께 검토해야 한다(현재는
`Start()`와 `InitCamera()`가 각자 다른 역할을 겸하고 있어 이런 리스크가 숨어 있다).

### 7.2 `PlayerColorDisplay` ↔ `PlayerPaintCanvas`의 Start() 순서는 방어적으로 처리됨 (양호)

같은 GameObject 위의 두 컴포넌트 간 `Start()` 호출 순서는 Unity가 보장하지 않는데,
`PlayerColorDisplay.TryApplyTaggerColor()`는 `paintCanvas.PaintCanvas == null`이면 조용히
리턴하고, `OnRoomPropertiesUpdate` 콜백이 올 때마다 재시도하는 구조라 순서에 관계없이 안전하게
동작한다 — 순서 의존성을 코드로 잘 방어한 사례로 평가할 만하다.

### 7.3 `ColorSelectionManager`/`PlayerPaintCanvas`의 `Update()` 폴링 간 순서는 문제 되지 않음

마스터의 `ColorSelectionManager.Update()`가 라운드를 확정하고 `SetCustomProperties()`를 호출해도,
그 결과가 `PlayerPaintCanvas.DetectRoundChange()`(각 클라이언트의 `Update()`에서 매 프레임 폴링)에
반영되는 것은 Photon 서버 왕복을 거친 **다음 네트워크 이벤트 처리 시점**이다 — 즉 같은 프레임 안의
Unity `Update()` 실행 순서(Script Execution Order 미설정 상태)에 의존하지 않는 구조라 안전하다.

---

## 8. Event 구독/해제가 제대로 되는가

### 8.1 Photon 콜백 이중 등록 — 7개 파일에서 확인됨 (**심각도: 높음**)

`MonoBehaviourPunCallbacks` 기본 클래스의 실제 구현(`Assets/Photon/PhotonUnityNetworking/Code/
PunClasses.cs:109`)을 직접 읽어 확인한 결과:

```csharp
public class MonoBehaviourPunCallbacks : MonoBehaviourPun, ...
{
    public virtual void OnEnable()  { PhotonNetwork.AddCallbackTarget(this); }
    public virtual void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }
}
```

**기본 클래스가 이미 `OnEnable`/`OnDisable`에서 자동으로 콜백을 등록/해제한다.** 그런데 이
프로젝트는(`RoomItemPlan.md`에서 "표준 패턴"이라고 명시한 대로) 다음 7개 파일 모두에서
`base.OnEnable()`을 호출한 **뒤에 또다시** `PhotonNetwork.AddCallbackTarget(this)`를 직접
호출한다(`OnDisable`도 동일하게 이중 호출):

```
BrushCursorController.cs:28-29 / :34-35
GameLobbyController.cs:16-17 / :22-23
LobbyController.cs:30-31 / :36-37
PlayerColorDisplay.cs:31-32 / :37-38
PlayerColorVoteIndicator.cs:14-15 / :20-21
PlayerPaintCanvas.cs:67-68 / :73-74
RoomLifecycleWatcher.cs:14-15 / :20-21
```

`PhotonNetwork.AddCallbackTarget` → 내부적으로 `LoadBalancingClient.AddCallbackTarget` →
`callbackTargetChanges` 큐에 적재된 뒤 `UpdateCallbackTarget<T>()`가 **`container.Add(target)`을
그대로 실행**함을 소스(`LoadBalancingClient.cs:3807-3821`)에서 직접 확인했다 — **중복 검사
(`Contains` 체크)가 전혀 없다.** 즉 이 7개 컴포넌트는 활성화될 때마다 콜백 대상 리스트에
**자기 자신을 2번** 등록하고, 비활성화 시 `Remove()`가 (리스트에서 첫 일치 항목만 제거하는 방식으로)
2번 호출되어 등록 수와 맞아떨어지므로 **여러 번 켰다 껐다 해도 등록이 누적되지는 않지만, 활성화된
동안에는 모든 Photon 콜백이 정확히 2배로 실행된다.**

**실제 영향 (컴포넌트별로 다름, 코드로 직접 추적)**:

| 컴포넌트 | 이중 실행되는 콜백 | 실제 피해 |
|---|---|---|
| `LobbyController` | `OnJoinedRoom()` 등 | **가장 위험** — `PhotonNetwork.LoadLevel("GameLobbyScene")`가 방 생성 직후 **2번 연속 호출**될 수 있음(170행, `PlayerCount==1` 가드는 있지만 이중 호출 자체는 막지 못함). `OnRoomListUpdate`도 매번 목록 갱신 로직이 2번 도는 낭비. |
| `GameLobbyController` | `OnPlayerEnteredRoom`/`OnPlayerLeftRoom`/`OnMasterClientSwitched` | `RefreshPlayerList()`(전체 파괴 후 재생성, §9-1)와 `RefreshStartButton()`이 인원 변화마다 2번씩 실행 — 목록이 매번 두 번 깜빡이며 다시 그려지는 낭비, 결과값 자체는 동일해 기능적으로는 정상으로 보임. |
| `RoomLifecycleWatcher` | `OnPlayerLeftRoom` | `leaveReason != LeaveReason.None` 가드 덕분에 실질 피해 없음(첫 실행에서 상태를 바꾸면 두 번째 실행은 가드에 막힘) — **우연히 안전.** |
| `PlayerColorDisplay` | `OnRoomPropertiesUpdate` | `hasApplied` 가드로 실질 피해 없음 — **우연히 안전.** |
| `PlayerColorVoteIndicator` | `OnPlayerPropertiesUpdate` | 같은 값으로 2번 대입, 시각적 차이 없음, 미세한 낭비만. |
| `BrushCursorController` | `OnRoomPropertiesUpdate` | `EnsureCursorInstance()`의 `cursorInstance != null` 가드로 실질 피해 없음. |
| `PlayerPaintCanvas` | `OnEvent`(`IOnEventCallback`) | 원격 붓 스트로크 하나당 `ApplyStamp()`가 **2번** 실행됨 — 같은 UV/색으로 스탬프를 2번 찍는 것 자체는 시각적으로 동일한 결과지만(멱등), `Graphics.Blit`를 매 스트로크마다 불필요하게 배로 수행하는 성능 낭비이며, 붓질이 잦은 페인팅 미니게임 특성상 프레임당 GPU 비용이 실질적으로 2배가 될 수 있다. |

**종합**: 대부분의 경우 각 컴포넌트가 갖춘 가드(`hasApplied`, `leaveReason`, `cursorInstance !=
null` 등)가 우연히 이중 실행을 무해하게 흡수하고 있어 지금 당장 눈에 띄는 버그로 드러나지는
않지만, ① `LobbyController.OnJoinedRoom()`의 `LoadLevel` 이중 호출은 씬 전환 관련 예외/경고를
유발할 실질적 위험이 있고, ② 나머지도 전부 "우연히 안전"할 뿐 의도된 안전장치가 아니므로 향후
새로운 콜백 오버라이드를 추가하는 사람이 가드 없이 작성하면 바로 실제 버그로 이어질 수 있는
**구조적 시한폭탄**이다. `RoomItemPlan.md`가 이 이중 호출 패턴 자체를 "표준"으로 문서화해뒀기
때문에 앞으로도 새 파일에 계속 복제될 가능성이 높다.

**권장**: `base.OnEnable()`/`base.OnDisable()`만 호출하고 `PhotonNetwork.AddCallbackTarget/
RemoveCallbackTarget`을 직접 호출하는 줄을 7개 파일에서 전부 제거한다(`MonoBehaviourPunCallbacks`를
상속하는 한 기본 클래스가 이미 처리하므로 추가 호출은 불필요). `RoomItemPlan.md`의 "표준 패턴"
서술도 함께 수정 필요.

### 8.2 `IOnEventCallback` 구독 방식 자체는 올바름

`PlayerPaintCanvas`가 `IOnEventCallback`을 구현하고 `OnEvent()`를 오버라이드하는 방식은 Photon의
권장 패턴과 일치한다 — 문제는 등록 횟수(§8.1)이지 등록 방식 자체가 아니다.

### 8.3 C# 순수 이벤트/델리게이트 구독 — 해제 로직 자체가 없어도 되는 구조

`ConfirmDialog.Awake()`에서 `yesButton.onClick.AddListener(...)`/`noButton.onClick.AddListener(...)`를
1회만 등록하고 별도 해제 코드가 없다 — 하지만 이 리스너들은 오브젝트 수명 동안 계속 유효해야 하는
고정 배선이라(팝업이 파괴될 때 리스너도 함께 GC 대상이 됨) 문제가 아니다. `ColorSwatchButton`,
`RoomListItem`, `GameManager.m_BackBtn` 등 나머지 `Button.onClick.AddListener` 호출들도 모두 동일한
"1회 등록, 오브젝트 수명과 함께 소멸" 패턴이라 누수 위험이 없다.

---

## 9. Object Pool과 Instantiate/Destroy가 충돌하는가

**결론: 프로젝트에 Object Pool 구현 자체가 없다** — `grep`으로 "Pool"을 검색해도 프로젝트 자체
코드에는 매치가 없다(Photon SDK 내부의 무관한 매치 제외). 따라서 "풀과 Instantiate/Destroy가
충돌"하는 직접적인 사례는 없지만, 풀이 없는 상태에서 반복적인 `Instantiate`/`Destroy`가 발생하는
지점들은 비용 관점에서 §9-1/§9-2로 짚을 만하다.

### 9.1 `GameLobbyController.RefreshPlayerList()` — 매번 전체 파괴 후 재생성 (낮음~중간)

```csharp
private void RefreshPlayerList()
{
    foreach (Transform child in playerListContent) Destroy(child.gameObject);
    foreach (Player p in PhotonNetwork.PlayerList)
    {
        var item = Instantiate(playerListItemPrefab, playerListContent);
        item.SetNickname(p.NickName);
    }
    ...
}
```
인원 변화가 있을 때마다(그리고 §8.1의 이중 등록 때문에 실질적으로 2배 빈도로) 목록 전체를
`Destroy`하고 처음부터 다시 `Instantiate`한다. 반면 같은 도메인의 `LobbyController.
RefreshRoomListView()`는 기존 항목을 재사용하고 변경분만 추가/제거하는 diff 패턴을 쓴다(§6.1
`RoomItemPlan.md` 설계 그대로) — **같은 `Lobby/` 도메인 안에서 두 리스트 UI가 서로 다른 품질의
갱신 전략을 쓰는 비일관성**이다. 대기방 인원은 최대 4명이라 지금 당장 성능 문제가 되진 않지만,
`CLAUDE.md`의 "최적화를 고려한 코드 작성" 원칙과는 결이 다르다.

**권장**: `RefreshPlayerList()`도 `RoomListItem`과 같은 diff 패턴으로 통일하거나, 최소한 인원수가
적어 문제가 안 된다는 점을 주석으로 남겨 의도적 선택임을 명시.

### 9.2 `PlayerPaintCanvas.PaintCanvas`(영구 `RenderTexture`)가 파괴 시 해제되지 않음 (중간)

`InitPaintCanvas()`가 캐릭터 1개당 512×512 `RenderTexture`를 `new RenderTexture(...)`+`.Create()`로
생성하지만, 프로젝트 전체에서 `OnDestroy()`를 오버라이드하는 스크립트가 **0건**이다(재확인
완료) — 즉 `PaintCanvas.Release()`를 호출하는 코드가 없다. `ApplyStamp()` 내부의
`RenderTexture.GetTemporary`/`ReleaseTemporary` 페어는 정확히 관리되고 있지만(임시 버퍼), **캐릭터
본체의 영구 캔버스는 그렇지 않다.**

`GameManager.md` §8.3에서 확인된 대로 씬 전환(`GameLobbyScene→GameScene`) 시 이전 씬의 캐릭터가
Unity에 의해 자동 파괴되고 새로 스폰되는 흐름이 정상 동작으로 검증되어 있는데, 이 파괴 시점마다
그 캐릭터가 갖고 있던 `RenderTexture`(GPU 메모리)가 `Release()` 없이 그대로 버려진다. Unity의
`RenderTexture`는 C# GC 파이널라이저가 결국 정리하긴 하지만 시점이 불확실하고, GPU 메모리는 C#
힙과 별도로 관리되므로 파이널라이저가 늦게 도는 동안 GPU 메모리 사용량이 누적될 수 있다 — 방을
반복해서 만들고 나가거나 씬을 여러 번 오갈 때(4인 기준 매 전환마다 최대 4개, 512×512 ARGB32 ≈
1MB씩) 누적 위험이 있다.

**권장**: `PlayerPaintCanvas`에 `private void OnDestroy() { if (PaintCanvas != null)
PaintCanvas.Release(); }`를 추가.

---

## 10. Photon의 Ownership/RPC 구조를 무시하는가

**결론: 전반적으로 잘 지켜지고 있다** — 오히려 이 프로젝트에서 가장 성숙한 부분 중 하나다.

- **소유권(`pv.IsMine`) 게이팅**: `HideOrSeekPlayer.Update()`(소유자만 입력 처리, 원격은 보간),
  `PlayerPaintCanvas.Update()`(소유자만 페인팅 입력 처리) 모두 정확히 `pv.IsMine`으로 분기한다.
- **단일 권위자(마스터 클라이언트) 패턴**: `ColorSelectionManager.Update()`(`if (!PhotonNetwork.
  IsMasterClient) return;`)와 `RoomLifecycleWatcher.Update()`(동일 가드)가 라운드 판정/씬 전환
  트리거를 마스터로만 제한한다 — 클라이언트마다 다른 `System.Random` 시드를 갖고 있어도 안전한
  이유가 바로 이 구조(§6.1 `research.md`에서도 확인됨).
- **RPC 대신 CustomProperties/RaiseEvent를 상황에 맞게 선택**: 투표처럼 "상태"인 데이터는
  CustomProperties(재접속/마스터 이관 시 자동 복제), 붓질처럼 "빈번한 순간 이벤트"는 RaiseEvent —
  `GameScenePlan.md` §4.1의 설계 근거와 실제 코드가 일치. `GameManager.LogMsg`만 유일하게 RPC를
  쓰는데(채팅 로그, `AllBuffered`), 이 용도에는 RPC가 적절한 선택이다.
- **송신측 판단 후 수신측은 그대로 재생**: `PlayerPaintCanvas.OnEvent()`는 수신 데이터를 재해석하지
  않고 송신측이 이미 계산한 `force` 플래그를 그대로 믿고 재생한다 — 클라이언트마다 다른 판단을
  내릴 여지를 원천 차단하는 안전한 설계.

**한 가지 일관성 이슈 (경미)**: `GameManager.OnClickBackBtn()`이 마지막 인원 조건에서
`PhotonNetwork.CurrentRoom.CustomProperties.Clear()`를 호출하는데, 이 호출에는 **마스터 클라이언트
여부 확인이 없다** — `ColorSelectionManager`/`RoomLifecycleWatcher`가 Room 상태를 바꿀 때는
항상 `IsMasterClient` 가드를 앞세우는 것과 대조적이다. Photon Realtime은 기본적으로 어떤 클라이언트든
`Room.SetCustomProperties`/`Clear()`를 호출할 수 있으므로 이것이 "권한 위반 에러"를 일으키지는
않지만, 프로젝트가 스스로 세운 "Room 상태 변경은 마스터 권위로"라는 암묵적 규약과는 어긋난다.
마지막 1인이 나가는 순간이라 실질적 경쟁(race) 위험은 낮지만, 이론상 두 클라이언트가 동시에
"내가 마지막"이라고 판단해 각자 `Clear()`를 중복 호출할 여지는 있다(멱등 연산이라 치명적이진
않음).

**PhotonView 필드 관리**: `pv`가 필요 없는 클래스(`ColorSelectionManager`, `RoomLifecycleWatcher`,
`ColorSelectionPanel` — 전부 Room 단위 상태만 다루고 RPC/Observed를 안 씀)에는 정확히 `pv` 필드가
없고, 필요한 클래스(`GameManager`, `HideOrSeekPlayer`, `PlayerPaintCanvas`, `PlayerColorVoteIndicator`,
`PlayerColorDisplay`)에는 있다 — 불필요한 `PhotonView` 참조가 남아있는 사례는 없었다.

---

## 11. 중복 로직이 있는가

### 11.1 `RoundIndex` 조회 로직이 5곳에서 거의 동일하게 반복 (중간)

"현재 라운드 인덱스를 Room CustomProperties에서 안전하게 읽는다"는 동일한 로직이 다음 파일에
각각 별도로 구현되어 있다:

```csharp
// ColorSelectionManager.Update() (재확인 필요 시 매 파일 참고)
if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.RoundIndex, out object riObj)) return;
int roundIndex = (int)riObj;

// ColorSelectionPanel.Update()
if (!props.TryGetValue(NetKeys.RoundIndex, out object riObj)) return;
int roundIndex = (int)riObj;

// PlayerPaintCanvas.GetRoundIndex()
private int GetRoundIndex() {
    if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return -1;
    var props = PhotonNetwork.CurrentRoom.CustomProperties;
    return props.TryGetValue(NetKeys.RoundIndex, out object ri) ? (int)ri : -1;
}

// PlayerColorDisplay.TryApplyTaggerColor() 내부
if (!props.TryGetValue(NetKeys.RoundIndex, out object riObj)) return;
if ((int)riObj != CompleteRoundIndex) return;

// BrushCursorController.OnRoomPropertiesUpdate() (콜백 payload에서 동일 키 재확인)
if (!changedProps.ContainsKey(NetKeys.RoundIndex)) return;
```

5곳 모두 `NetKeys.RoundIndex` 키 하나를 안전하게 꺼내는 것뿐인데 각자 다른 함수 시그니처와 널
가드 스타일로 재구현되어 있다. `NetKeys`/`NetEventCodes`로 "문자열 키는 한곳에 모은다"는
컨벤션까지는 지켰지만, **그 키를 실제로 읽는 보일러플레이트는 통합하지 않았다.**

**권장**: `static class RoomState { public static int GetRoundIndex() { ... } public static bool
TryGetInt(string key, out int value) { ... } }` 같은 공용 헬퍼를 `ColorTag/` 아래 추가해 5곳을
통일. 순수 함수라 `ColorVoteTally`/`TaggerColorAssigner`와 같은 계층에 자연스럽게 편입 가능.

### 11.2 씬 이름 매직 스트링 중복 — §4와 동일 사안 (중간, 이미 다룸)

`"LobbyScene"`(2곳), `"GameLobbyScene"`(2곳)이 서로 다른 파일에 독립적으로 하드코딩된 것은 §4에서
다룬 내용과 완전히 같은 문제를 "중복" 관점에서 다시 짚은 것이다 — 상수화 시 두 관점의 문제가 동시에
해결된다.

### 11.3 Photon 룸 가드 절(guard clause) 반복 (경미, 허용 가능한 수준)

```csharp
if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
```
이 정확한 형태가 `ColorSelectionManager`, `ColorSelectionPanel`, `GameLobbyController`,
`RoomLifecycleWatcher`, `PlayerPaintCanvas.GetRoundIndex()`, `PlayerColorDisplay`에 반복된다.
관용적인 방어 코드(guard clause)이고 한 줄이라 추출로 얻는 이득이 크지 않아 §11.1만큼 심각하진
않지만, 같은 문자열이 6곳에 있다는 사실 자체는 §11.1의 헬퍼 클래스에 `RoomState.IsInRoom()` 같은
메서드로 함께 흡수할 수 있다.

### 11.4 `OnEnable`/`OnDisable` 보일러플레이트 — §8.1과 동일 사안

7개 파일에 걸친 `base.OnEnable(); PhotonNetwork.AddCallbackTarget(this);` 반복은 §8.1에서 버그로
이미 다뤘다 — 중복 코드가 곧 버그의 전파 경로였던 사례.

---

## 종합 권장 조치 (우선순위순)

1. **(버그 수정)** §8.1 — 7개 파일에서 `PhotonNetwork.AddCallbackTarget/RemoveCallbackTarget` 직접
   호출 제거(`base.OnEnable/OnDisable`만으로 충분). 특히 `LobbyController`의 `LoadLevel` 이중 호출
   위험이 가장 시급.
2. **(리소스 누수 방지)** §9.2 — `PlayerPaintCanvas.OnDestroy()`에서 `PaintCanvas.Release()` 추가.
3. **(재발 방지)** §4/§11.2 — 씬 이름 상수 클래스(`SceneNames.cs`) 도입, 5곳 통일.
4. **(중복 제거)** §11.1 — `RoundIndex` 등 CustomProperties 조회 공용 헬퍼 도입.
5. **(일관성)** §9.1 — `GameLobbyController.RefreshPlayerList()`를 `RoomListItem`과 같은 diff
   패턴으로 통일하거나 의도적 선택임을 주석으로 명시.
6. **(설계 판단 필요, 급하지 않음)** §1.1 — `GameManager`의 스폰/나가기 책임을 분리할지 여부 결정.
   §7.1 — 스폰 시점을 바꿀 계획이 있다면 `Camera_Ctrl` 초기화 로직도 함께 재검토.

**참고**: Singleton(§5), ScriptableObject(§6), Photon Ownership/RPC(§10) 세 관점은 이번 조사에서
뚜렷한 구조적 문제가 발견되지 않았다 — 오히려 이 프로젝트가 상대적으로 잘 지키고 있는 영역이다.
