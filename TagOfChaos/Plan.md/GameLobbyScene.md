# GameLobbyScene 환경 교체 계획 — `Witch_Cookie_Environment` 배치 + 어두운 조명

> 작성 2026-09-27. 승인 후 같은 날 작업 완료 — 결과는 §8.
> 근거 자료: `GameLobbyScene.unity` YAML 실측, `Witch_Cookie_Layout.json` 전수 분석, `research.md` §G·§H.
>
> **개정 1(2026-09-27)**: 사용자 결정 반영 — D1 단색 + 안개, D2 가마솥 위치 **보류**, D3 Point Light 2개, D4 NavMesh 삭제(검토 결과 §6), D5 경계벽 **만들지 않음**(리스폰으로 충분).
> CLAUDE.md 폴더 규칙 점검과 파일 이동 결과는 §7.
>
> ✅ (해결됨 — 사용자가 저장 후 진행) **작업 전 주의**: 지금 Unity 에디터에 열린 GameLobbyScene에 **저장되지 않은 변경**이 있다(창 제목 `*`, `scene.isDirty = true`).
> 저장된 파일에서는 `GameLobbyUICanvas`·`Canvas`의 자식이 각 3개인데 에디터에서는 각 4개다. 씬 작업은 결국 씬을 저장하므로
> **이 미저장 변경이 함께 저장된다.** 작업 시작 전에 사용자가 저장할지 버릴지 먼저 정해야 한다.

---

## 0. 목표

1. GameLobbyScene에 손으로 배치해 둔 임시 환경(`LobbyEnvironment`)을 없앤다.
2. 그 자리에 과자집 + 소품 환경 프리팹 `Assets/04. Prefabs/Environment/Witch_Cookie_Environment.prefab`을 배치한다.
3. 배치에 맞춰 게임 오브젝트(스폰 지점, 가마솥, 낙하 복귀 영역)의 위치를 다시 잡는다.
4. 환경 분위기(마녀의 집, 보라·청록 발광 소품)에 맞게 `Directional Light`와 씬 조명을 **어두운 밤 분위기**로 바꾼다.

**범위 밖**: GameScene, 런타임 스크립트, 문 네트워크 제어(research.md §G5-1), 환경 에셋 자체 수정(레이아웃 JSON·FBX).

---

## 1. 현재 상태 (실측)

### 1.1 루트 오브젝트

| 오브젝트 | 위치 | 내용 | 이번 작업 |
|---|---|---|---|
| `LobbyEnvironment` | (0,0,0) | **NavMeshSurface** 컴포넌트 + 자식 25개(ProBuilder 메시: 바닥 `Ground` 24×1×24 m, 테이블·의자·파라솔·울타리·상자) + NatureStarterKit2 나무·덤불 프리팹 인스턴스 12개 | **삭제** |
| `Cauldron` | (3, 0.5, 0) | 캡슐 메시 + 캡슐 트리거(r 0.5, h 2), `Cauldron` 스크립트(→ `MonsterRevealController`) | **이동** |
| `PlayerSpawnPos` | (0, 0, 0) | 빈 오브젝트(이름으로 `GameObject.Find`) | **이동** |
| `VoidKillZone` | (0, −15, 0) | BoxCollider 트리거 60×2×60 m | 유지(크기 검토) |
| `Directional Light` | 회전 (50, −30, 0) | 색 (1, 0.957, 0.839) 따뜻한 흰색, 강도 1, Soft Shadows, Realtime | **변경** |
| `Main Camera` | (0, 1, −10) | `Camera_Ctrl`, Clear Flags = Skybox, far 1000 | 유지(배경만 검토) |
| `GameManager`, `MonsterManagers`, `Canvas`, `GameLobbyUICanvas`, `EventSystem` | — | 게임 로직·UI | **건드리지 않음** |

### 1.2 조명·렌더 설정 (RenderSettings)

| 항목 | 현재 값 |
|---|---|
| Skybox | 기본 Procedural(`Default-Skybox`) |
| Ambient | Mode = Skybox(0), 강도 1 |
| Fog | 꺼짐 |
| Reflection | Skybox, 강도 1 |
| Sun Source | 없음 |
| Lightmap | Baked Lightmaps 켜짐, Realtime GI 꺼짐, **구운 데이터 없음** |

### 1.3 환경 프리팹 (research.md §H)

- 과자집(원점 = 바닥 중앙, 정면 = +Z, 문 4개는 **닫힌 채 충돌체로 막혀 있음**) + 소품 407개, 전체 약 128 × 21 × 128 m.
- 울타리로 둘린 마당: x, z = **−18 ~ +18 m**. 지면 모듈은 −20 ~ +20 m, 그 밖은 128 m 원거리 지면.
- **지면 에셋 10종에는 충돌체가 없다**(research.md H4-1). 걸을 수 있는 곳은 과자집 실내 바닥(`COL_Floor`)뿐이다.
- 환경 전체가 Default 레이어, 소품 대부분 Static.

### 1.4 그대로 두면 생기는 문제

| # | 문제 | 원인 |
|---|---|---|
| P1 | 마당에 서면 **바닥을 뚫고 떨어진다** | 지면 충돌체 없음 + 기존 `Ground`(ProBuilder)를 지움 |
| P2 | 쿠키가 **과자집 안에 갇혀 스폰**된다 | `PlayerSpawnPos` (0,0,0) = 과자집 실내 중앙, 문 4개가 닫혀 있고 여는 코드가 없음 |
| P3 | 가마솥이 **실내 마법진 옆**(3, 0.5, 0)에 놓인다 | 위와 같음. 밖에서 스폰하면 가마솥에 갈 수 없음 |
| P4 | 부서진 울타리 틈이나 울타리 밖으로 나가면 지면 끝(±20 m)에서 추락 | 경계 충돌체 없음 |
| P5 | 밝은 대낮 조명 + 푸른 하늘 스카이박스가 환경의 발광 소품과 어울리지 않음 | §1.2 |

---

## 2. 작업 계획

작업 도구는 **Unity 에디터(UnityMCP)**로 한다. 씬 YAML을 직접 고치면 PrefabInstance·ProBuilder 메시·NavMesh 참조가 꼬일 위험이 크다.
각 단계 뒤에 씬을 저장하고 콘솔 에러를 확인한다.

### 단계 1 — 백업 확인

- `GameLobbyScene.unity`는 git에 커밋돼 있다(커밋 `7e7feba`). 작업 전 `git status`로 이 씬에 커밋되지 않은 변경이 없는지 확인한다. 되돌릴 때는 `git checkout -- Assets/Scenes/GameLobbyScene.unity`.

### 단계 2 — 기존 임시 환경 삭제

1. `LobbyEnvironment` 루트를 자식째 삭제한다(ProBuilder 메시 25개, NavMeshSurface, 나무·덤불 인스턴스 12개가 함께 사라진다).
   에디터 실측으로는 자식이 **37개**(저장 파일 기준 25개 + 프리팹 인스턴스 12개)다.
2. **씬을 먼저 저장한 뒤** `Assets/Scenes/GameLobbyScene/NavMesh-LobbyEnvironment.asset`과 빈 폴더 `Assets/Scenes/GameLobbyScene/`을 삭제한다.
   순서를 거꾸로 하면 NavMeshSurface가 없는 에셋을 가리켜 "Missing" 참조가 생긴다. 삭제해도 되는 근거는 §6.
3. NatureStarterKit2 에셋 폴더는 이번에 지우지 않는다(다른 씬 사용 여부는 별도 확인).

### 단계 3 — 환경 프리팹 배치

1. `Witch_Cookie_Environment.prefab`을 씬 루트에 **위치 (0,0,0), 회전 0, 스케일 1**로 배치한다(프리팹 링크 유지 — 언팩하지 않는다).
2. 이름은 `WitchCookieEnvironment`로 둔다.
3. 씬에 직접 배치하므로 Static 소품은 빌드 시 정적 배칭이 적용된다.

### 단계 4 — 지면 충돌체 추가 (P1)

환경 프리팹은 빌더(`WitchCookiePropsBuilder`)가 다시 만들 때 덮어쓰므로 **프리팹 안이 아니라 씬에** 별도 오브젝트로 둔다.

| 새 오브젝트 | 구성 | 값 |
|---|---|---|
| `LobbyGroundCollider` | 빈 오브젝트 + BoxCollider(렌더러 없음), Default 레이어, Static | 중심 (0, −0.5, 0), 크기 **40 × 1 × 40 m** → 윗면 y = 0. 과자집 바닥 윗면(y 0.03)보다 낮아 실내에서는 `COL_Floor`가 먼저 닿는다 |

- **경계벽은 만들지 않는다(D5 결정).** 울타리 틈이나 지면 끝(±20 m)을 넘으면 떨어지지만, `VoidKillZone`(60 × 60 m, y −15)이 받아 `PlayerSpawnPos`로 되돌린다.
  VoidKillZone 밖(±30 m 너머)까지 달려 나가도 `FallGuard`(y −100 이하)가 최후 방어선으로 복귀시킨다. 괴물도 같은 경로(`IRespawnable`)로 복귀한다.
- 기존 ProBuilder `Ground`와 같은 역할이므로 쿠키 접지(`groundLayer = Default`)와 스폰 겹침 검사가 그대로 동작한다.
- MeshCollider가 아니라 BoxCollider라서 볼록 여부 문제가 없다.

### 단계 5 — 스폰 지점 이동 (P2) / 가마솥은 보류 (P3)

| 오브젝트 | 새 위치 | 근거 |
|---|---|---|
| `PlayerSpawnPos` | **(0, 0, 12)** — 정면 문(z +7.5) 앞 쿠키 조각 길 위, 과자집을 바라보는 첫 화면 | 반경 5 m 안에 길·랜턴 2개·꽃·화분뿐. 랜턴은 충돌체가 있지만 `SpawnPositionFinder`가 겹침을 피해 다른 후보를 고른다 |
| `Cauldron` | **이번에는 옮기지 않는다(D2 보류)** | — |

**가마솥을 보류하는 동안의 영향**: 가마솥이 지금 자리 (3, 0.5, 0)에 남으면 **닫힌 과자집 실내**에 들어가 쿠키가 닿을 수 없다.
그래도 게임은 진행된다. 괴물 선정은 정원이 찬 뒤 30초(`GameSettings.MonsterSelectTimeout`)가 지나면 `MonsterAssignmentAuthority`가
무작위로 채우기 때문이다. 즉 보류 기간에는 "가마솥 자원 신청"만 불가능하고 "시간 초과 무작위 선정"만 동작한다.
위치를 정할 때 참고할 후보(레이아웃 JSON 분석):

| 후보 | 좌표 | 비고 |
|---|---|---|
| 정면 마당 오른쪽 | (9, 0.5, 10) | 반경 2.5 m 안에 작은 돌 1개. 스폰 지점과 약 9.2 m — 스폰 겹침 방지 거리 충분 |
| 과자집 실내 마법진 위 | (0, y, 0) | 분위기상 가장 어울림. 대기실에서만 문을 항상 열어 두는 로컬 처리 필요(동기화 불필요) |
| 모델 교체 | — | `리소스/솥단지.glb`를 임포트해 캡슐을 대체 |

### 단계 6 — 낙하 복귀 영역 점검

- `VoidKillZone`(60 × 60 m, y −15)은 경계벽 안(±18.6 m)을 모두 덮으므로 **그대로 둔다.**
- 스폰 복귀 위치가 `PlayerSpawnPos`를 따르므로(`HideOrSeekPlayer.RespawnToSpawnPoint`) 단계 5 이후 자동으로 마당으로 복귀한다.

### 단계 7 — 어두운 분위기 조명 (P5)

"달빛이 드는 마녀의 숲" 분위기를 목표로 한다. 발광 재질(`M_Magic_*`, `M_Lantern_Glow`, `M_Mushroom_Glow`, `M_FX_*` 등)이 어둠 속에서 돋보이게 한다.

#### 7.1 Directional Light (달빛)

| 항목 | 현재 | 변경안 | 이유 |
|---|---|---|---|
| 이름 | Directional Light | `Moon Light` | 역할 명시(선택) |
| Color | (1, 0.957, 0.839) 따뜻한 흰색 | **(0.55, 0.60, 0.85)** 차가운 푸른 보라 | 달빛 |
| Intensity | 1.0 | **0.35** | 어둡게 하되 캐릭터 실루엣은 보이게 |
| Rotation | (50, −30, 0) | **(35, −40, 0)** | 낮은 각도로 긴 그림자, 과자집 정면(+Z)과 스폰 쪽이 달빛을 받도록 |
| Shadow Type | Soft | Soft 유지 | |
| Shadow Strength | 1.0 | **0.8** | 너무 새까만 그림자 방지 |
| Mode | Realtime | Realtime 유지 | 구운 라이트맵이 없음 |

#### 7.2 Environment Lighting (Window > Rendering > Lighting > Environment)

| 항목 | 변경안 |
|---|---|
| Skybox Material | **없음(None)** + Main Camera Clear Flags = **Solid Color**, 배경색 **(0.05, 0.04, 0.09)** 짙은 남보라 — 새 에셋 없이 밤하늘 표현(**D1 확정**). 배경색을 안개색과 비슷하게 맞춰 먼 곳이 배경으로 자연스럽게 녹게 한다 |
| Sun Source | `Moon Light` 지정 |
| Ambient Source | **Gradient**(Trilight): Sky **(0.16, 0.13, 0.26)**, Equator **(0.10, 0.08, 0.14)**, Ground **(0.05, 0.04, 0.06)** |
| Reflection Source | Custom 없이 Skybox 끔 상태면 반사 약해짐 — Intensity **0.4** |
| Fog | **켬**, Mode = Exponential Squared, Color **(0.10, 0.07, 0.16)**, Density **0.025** → 울타리 너머 나무와 128 m 원거리 지면이 안개 속으로 사라져 맵 경계가 자연스러워짐 |

#### 7.3 보조 조명 — Point Light 2개 (D3 확정)

발광 재질은 Built-in RP에서 **스스로 빛나 보이기만 하고 주변을 비추지 않는다**(라이트맵을 굽지 않았고 Realtime GI도 꺼져 있음). 스폰 지점 주변을 밝혀 줄 보조 조명 2개를 둔다.

| 이름 | 위치 | 색 | Range | Intensity | 그림자 | 역할 |
|---|---|---|---|---|---|---|
| `PointLight_FrontDoor` | (0, 2.6, 8.6) — 정면 문 위 | 따뜻한 주황 (1.0, 0.62, 0.30) | 8 | 1.2 | 없음 | 과자집 정면과 스폰 지점 쪽 길을 비춤. 첫 화면의 초점 |
| `PointLight_Accent` | (−5, 3.0, 13) — 스폰 지점 왼쪽 마당 | 보라 (0.62, 0.40, 1.0) | 7 | 0.9 | 없음 | 마당과 쿠키 실루엣 보조. **가마솥 위치가 정해지면 가마솥 옆으로 옮긴다** |

- 두 조명 모두 **Render Mode = Auto, Shadows = No Shadows, Mode = Realtime**. Forward 렌더링에서 픽셀 라이트가 2개 늘어나는 정도라 비용이 작다.
- 두 조명은 씬 루트 아래 `LobbyLights` 빈 오브젝트로 묶는다.

### 단계 8 — 검증

| # | 확인 | 방법 |
|---|---|---|
| V1 | 씬을 열었을 때 콘솔 에러·Missing 참조 없음 | UnityMCP `read_console` |
| V2 | `Cauldron.revealController`, `MonsterLobbyWaitController`의 버튼·Behaviour 참조, `RoomExitController`, `GameManager` 참조가 유지됨 | 인스펙터 필드 조회 |
| V3 | 스폰 지점 주변에서 `SpawnPositionFinder`가 빈 곳을 찾음 | 에디터에서 Physics.OverlapCapsule로 16개 후보 샘플 검사 |
| V4 | 마당 어디서나 바닥에 서고, 지면 밖으로 떨어지면 스폰 지점으로 복귀, 과자집 벽 충돌 정상 | Play 모드(오프라인)에서 쿠키 이동 |
| V5 | 조명: 캐릭터 이름표·색이 식별 가능, 발광 소품이 돋보임, UI 가독성 변화 없음 | Game 뷰 스크린샷(스폰 지점 시점, 가마솥 시점) |
| V6 | 프레임 | Profiler로 배치 전후 비교(소품 408개 추가) |
| V7 | `Tools/TagOfChaos/Validate UI Layout`, EditMode 테스트(`RuleTests`) 통과 | 기존 도구 |

GameLobbyScene은 방에 들어온 상태(InRoom)를 전제로 스폰하므로, Play 모드 확인은 **LobbyScene에서 방을 만들어 진입**하는 흐름으로 한다(4인 정원이 필요한 가마솥 신청은 이번 검증 범위에서 제외 — 트리거 위치만 확인).

---

## 3. 결정 사항

| # | 질문 | 결정(2026-09-27) |
|---|---|---|
| D1 | 하늘 표현 | ✅ **단색 배경 + 안개** |
| D2 | 가마솥 위치·모양 | ⏸ **보류** — 나중에 정함(§단계 5의 후보표 참고). 그동안은 시간 초과 무작위 선정으로만 괴물이 정해짐 |
| D3 | 보조 Point Light | ✅ **2개**(§7.3) |
| D4 | `NavMesh-LobbyEnvironment.asset` | ✅ **삭제** — 검토 결과 문제없음(§6) |
| D5 | 경계벽 | ✅ **만들지 않음** — VoidKillZone·FallGuard 리스폰으로 충분 |
| D6 | 에디터에 열린 GameLobbyScene의 **미저장 변경** | ❓ 작업 전에 저장할지 버릴지 사용자 확인 필요(맨 위 주의 참고) |

---

## 4. 영향과 위험

| 항목 | 내용 | 대응 |
|---|---|---|
| 괴물 대기 | 괴물은 색칠 페이즈(약 63초) 동안 이 씬에 혼자 남는다. 환경이 커져도 로직 영향 없음 | — |
| 성능 | 소품 408개, 머티리얼 54종 추가. 대기실은 짧게 머무는 씬이지만 괴물이 1분 넘게 머문다 | V6 프로파일링, 필요시 반복 소품 머티리얼 GPU 인스턴싱(research.md H4-6) |
| 카메라 | `Camera_Ctrl`에 벽 충돌 처리가 없어, 과자집 벽·나무 근처에서 카메라가 물체를 뚫을 수 있다 | 이번 범위 밖(research.md §G5-3). 스폰을 마당에 두어 영향을 줄인다 |
| 오클루전 | 안개·거미줄·낙엽이 OccluderStatic이다. **오클루전 컬링을 굽지 않는 한** 문제없다 | 이번 작업에서는 오클루전을 굽지 않는다 |
| 어두움과 게임성 | 대기실은 숨바꼭질 전 단계라 영향이 작다. 같은 조명을 GameScene에 쓰면 색칠 위장 식별도가 달라지므로 **GameScene 적용은 별도 판단** | — |
| 프리팹 재빌드 | `Tools/TagOfChaos/Build Witch Cookie Props`를 다시 실행하면 환경 프리팹이 덮어쓰기된다. 씬의 인스턴스는 링크로 따라가며, 지면·경계 충돌체와 조명은 씬에 있으므로 영향 없음 | 환경 프리팹 인스턴스에 오버라이드를 만들지 않는다 |

---

## 5. 작업 순서 요약

1. 에디터 미저장 변경 처리(D6) + git 상태 확인 → 2. `LobbyEnvironment` 삭제 → 3. 씬 저장 → 4. NavMesh 에셋·빈 폴더 삭제(D4) →
5. 환경 프리팹 (0,0,0) 배치 → 6. 지면 40×40 BoxCollider(경계벽 없음, D5) → 7. `PlayerSpawnPos` (0,0,12)(가마솥은 보류, D2) →
8. `Moon Light` + Environment Lighting·Fog·카메라 단색 배경(D1) → 9. Point Light 2개(D3) → 10. 저장 → 11. 검증 V1~V7 → 12. 결과를 research.md에 반영.

---

## 6. NavMesh 에셋 삭제 검토 (D4)

대상: `Assets/Scenes/GameLobbyScene/NavMesh-LobbyEnvironment.asset`(`NavMeshData`, GUID `55fd71e3…`). 에디터(UnityMCP)와 파일 검색으로 확인했다.

| 확인 항목 | 결과 | 판단 |
|---|---|---|
| 이 에셋을 참조하는 에셋 | `AssetDatabase.GetDependencies`로 프로젝트 전체를 훑은 결과 **`GameLobbyScene.unity` 1개뿐**. 그 안에서도 `LobbyEnvironment`의 `NavMeshSurface.m_NavMeshData` 한 곳(씬 파일 3318행). 씬 자체의 옛 방식 NavMesh 설정(`NavMeshSettings.m_NavMeshData`, 121행)은 비어 있음 | `LobbyEnvironment`를 지우면 참조가 0이 된다 |
| 런타임에서 NavMesh를 쓰는 코드 | `Assets/` 전체 `.cs`에서 `UnityEngine.AI`/`NavMesh` 사용 **0건**(Photon 데모 폴더 제외, 그 폴더도 해당 없음) | 괴물·쿠키 모두 Rigidbody 직접 이동이라 경로 탐색을 쓰지 않는다 |
| 씬의 NavMesh 사용 컴포넌트 | 에디터 실측: NavMeshAgent 0, NavMeshObstacle 0, NavMeshLink 0, NavMeshModifier 0, NavMeshSurface 1(`LobbyEnvironment`) | 삭제로 깨질 사용처 없음 |
| 다른 씬 | GameScene·LobbyScene은 NavMesh 없음. SampleScene은 자기 폴더의 별도 `NavMesh.asset` 사용. PlayerTestScene은 **이미 존재하지 않는** NavMesh 에셋(GUID `2085502b…`)을 가리킴(무해한 기존 Missing 참조, 이번 작업과 무관) | 영향 없음 |
| 패키지 | `com.unity.ai.navigation` 2.0.14는 **유지**한다. 에셋을 지워도 패키지는 필요 없어지지 않으며, 패키지 제거는 다른 씬의 컴포넌트(SampleScene)에 영향 | 패키지는 건드리지 않음 |
| 데이터 유효성 | 이 NavMesh는 옛 24 × 24 m ProBuilder 바닥과 소품 기준으로 구운 것(정점 1,793개)이라 **새 환경과 전혀 맞지 않는다**. 남겨 두면 오히려 잘못된 데이터가 된다 | 삭제가 맞다 |
| 순서 위험 | 에셋을 먼저 지우고 씬을 저장하면 `NavMeshSurface`에 Missing 참조가 남는다(그 컴포넌트도 곧 지워지지만) | **`LobbyEnvironment` 삭제 → 씬 저장 → 에셋 삭제** 순서 |
| 버전 관리 | 에셋과 `.meta` 모두 git에 커밋돼 있다 | 필요하면 git으로 복구 가능 |
| 미래 | 나중에 AI 괴물(봇)이나 경로 탐색이 필요해지면 새 환경 기준으로 **다시 구워야 한다**. 그때는 지면 충돌체(단계 4)를 포함해 `NavMeshSurface`를 새로 두면 된다 | 지금 삭제해도 손실 없음 |

**결론: 삭제해도 문제없다.** 빌드 크기가 조금 줄고, 잘못된 데이터가 남지 않는다. 폴더 `Assets/Scenes/GameLobbyScene/`에는 이 에셋만 있으므로 폴더(+`.meta`)도 함께 지운다.

---

## 7. CLAUDE.md 폴더 규칙 점검과 파일 이동 (2026-09-27 수행)

CLAUDE.md 폴더 규칙(Scripts → `02. Scripts/{도메인}`, SO → `03. SO/{도메인}`, Prefabs → `04. Prefabs/`, UI 프리팹 → `Resources/UI/{Popup|Scene|Tab}/{클래스명}`, 전역 SO → `Resources/`, 에디터 → `Editor/`)으로 프로젝트 전체(서드파티 `Photon`, `TextMesh Pro`, `NatureStarterKit2` 제외)를 검사했다.

### 7.1 이동 완료

| 파일 | 이전 위치 | 새 위치 | 근거 |
|---|---|---|---|
| `PlayerResultRow.prefab` | `Assets/04. Prefabs/` | `Assets/Resources/UI/Scene/PlayerResultRow/` | 결과 화면의 **UI 프리팹**(RectTransform, `PlayerResultRow` 클래스)인데 일반 프리팹 폴더에 있었다. 같은 성격의 목록 항목(`PlayerListItem`, `RoomListItem`)과 같은 규칙으로 맞췄다 |

- **에디터의 `AssetDatabase.MoveAsset`으로 옮겨 GUID를 유지했다**(`f6994f6e…` 이동 전후 동일). GameScene `ResultScreenController.playerRowPrefab` 참조는 GUID로 연결돼 그대로 유효하다(씬 파일에서 확인). 이동 후 콘솔에 새 에러 없음.
- 코드에서 이 경로를 문자열로 쓰는 곳은 없다. 기획 문서(`Bug-fix-plan.md` §24.6 표, `research.md` §B.4)의 옛 경로 표기만 남아 있다.

### 7.2 규칙상 위반은 아니지만 확인이 필요한 것 (옮기지 않음)

| 항목 | 현재 | 왜 옮기지 않았나 / 제안 |
|---|---|---|
| `Resources/UI/Scene/GameLobbyPanel/`, `…/LobbyPanel/` | 폴더·프리팹 이름이 `…Panel`인데 붙은 클래스는 `GameLobbyController`, `LobbyController` | 규칙 `{클래스명}`과 이름이 다르다. 이름 변경(`GameLobbyController/GameLobbyController.prefab`)은 위치 이동이 아니라 **이름 결정**이라 사용자 확인 후 진행 |
| `Resources/UI/Button/*.png`(스프라이트 2장) | `Popup/Scene/Tab`에 없는 `Button` 폴더 | 규칙은 **UI 프리팹**만 다루고 스프라이트 위치 규칙은 없다. 규칙 추가 여부 결정 필요 |
| `08. Resources/LobbySceneBackGround.png`, `09. Environment/**` | 규칙 표에 없는 폴더 | 규칙이 없어 "틀린 위치"라고 할 수 없다. 특히 `09. Environment`는 에디터 도구·Blender 내보내기 스크립트가 **경로를 하드코딩**해(research.md §G.3, §G.4) 옮기면 코드 수정이 함께 필요하다. CLAUDE.md 폴더 규칙 표에 두 폴더를 추가하는 것을 제안 |
| `04. Prefabs/Resources/Brush.fbx`, `BrushCursor.prefab` | 프리팹 폴더 안의 `Resources` | `BrushCursor`는 `BrushSettingsSO`가 직접 참조해 Resources일 필요가 없고, `Brush.fbx`는 모델이다. 규칙 위반은 아니므로 유지(정리 후보, research.md §E.3.5) |
| 스크립트·SO·에디터 스크립트 | 모두 규칙 위치에 있음 | 이상 없음 |

---

## 8. 작업 결과 (2026-09-27 수행)

사용자가 에디터의 미저장 변경을 저장한 뒤(D6 해결) §5 순서대로 UnityMCP로 진행했다.

### 8.1 수행 내역

| 단계 | 결과 |
|---|---|
| 사전 확인 | 씬 `dirty = False` 확인 후 시작 |
| `LobbyEnvironment` 삭제 → 씬 저장 | 자식 37개(ProBuilder 메시 25 + 나무·덤불 12)와 NavMeshSurface 제거. 저장 후 씬이 NavMesh 에셋을 더 이상 참조하지 않음을 확인(`GetDependencies` 0건) |
| NavMesh 에셋·폴더 삭제 | `Assets/Scenes/GameLobbyScene/NavMesh-LobbyEnvironment.asset`, 빈 폴더 `Assets/Scenes/GameLobbyScene/` 삭제 |
| 환경 배치 | `WitchCookieEnvironment`(프리팹 링크 유지, 위치·회전 0, 스케일 1) |
| 지면 충돌체 | `LobbyGroundCollider` — BoxCollider 40 × 1 × 40 m, 중심 (0, −0.5, 0), 윗면 y = 0, Static |
| 스폰 지점 | `PlayerSpawnPos` (0, 0, 0) → **(0, 0, 12)** |
| 가마솥 | 이동하지 않음(D2 보류) — (3, 0.5, 0), 과자집 실내 |
| 달빛 | `Directional Light` → **`Moon Light`**, 색 (0.55, 0.60, 0.85), 강도 0.35, 회전 (35, −40, 0), Soft Shadows, 그림자 강도 0.8 |
| 환경 조명 | Skybox 없음, Sun = Moon Light, Ambient Trilight(하늘 0.16/0.13/0.26, 적도 0.10/0.08/0.14, 지면 0.05/0.04/0.06), 반사 강도 0.4 |
| 안개 | 켬, Exponential Squared, 색 (0.10, 0.07, 0.16), 밀도 0.025 |
| 카메라 | Main Camera Clear Flags = Solid Color, 배경 (0.05, 0.04, 0.09) |
| 보조 조명 | `LobbyLights/PointLight_FrontDoor` (0, 2.6, 8.6) 주황 Range 8 강도 1.2, `LobbyLights/PointLight_Accent` (−5, 3, 13) 보라 Range 7 강도 0.9, 둘 다 그림자 없음·Realtime |
| 저장 | 씬 저장, `dirty = False` |

최종 루트 오브젝트: Main Camera, Moon Light, GameLobbyUICanvas, Canvas, EventSystem, PlayerSpawnPos, GameManager, VoidKillZone, Cauldron, MonsterManagers, **WitchCookieEnvironment, LobbyGroundCollider, LobbyLights**.

### 8.2 검증 결과

| # | 결과 |
|---|---|
| V1 콘솔 | 에러 0건 |
| V2 참조 | 씬 전체 Missing 스크립트·Missing 참조 **0건**. `Cauldron.revealController` → `MonsterManagers (MonsterRevealController)` 유지 |
| V3 스폰 | 스폰 지점 ±5 m 무작위 후보 200개 중 154개(77%)가 빈 곳 → `SpawnPositionFinder`의 16회 시도가 모두 실패할 확률은 사실상 0 |
| V4 바닥 | 스폰 지점 아래·마당 모서리 (17, −17) 모두 `LobbyGroundCollider`(y 0)에 닿음, 과자집 실내는 `COL_Floor`(y 0.03)에 닿음 — 레이캐스트로 확인. **Play 모드 주행 테스트는 하지 않았다** |
| V5 조명 | 에디터에서 임시 카메라로 스폰 시점·전경 두 장을 렌더해 확인: 정면 문 앞 주황 조명이 첫 화면의 초점이 되고, 창문·마법석·버섯 발광이 어둠 속에서 돋보이며, 원거리 지면은 안개에 녹는다 |
| V6 프레임 | **미실시**(Play 모드 프로파일링 필요) |
| V7 테스트 | **미실시**. 런타임 코드는 바뀌지 않았다 |

### 8.3 남은 일

- **가마솥 위치 결정(D2)** — 결정 전까지는 가마솥 신청이 불가능하고 30초 시간 초과 무작위 선정만 동작한다. 결정되면 `PointLight_Accent`도 가마솥 옆으로 옮긴다.
- LobbyScene에서 방을 만들어 대기실에 들어가 **실제 플레이로 V4·V6 확인**(카메라가 과자집 벽·나무를 뚫는지 포함 — research.md §G5-3).
- 커밋은 하지 않았다. git 상 변경: `GameLobbyScene.unity` 수정, NavMesh 에셋·폴더 삭제, `PlayerResultRow.prefab` 이동(§7.1).

---

## 9. 버그 수정 — 마당 밖으로 나가면 땅으로 꺼짐 (2026-09-27, ✅ 완료)

### 9.1 원인 (에디터 실측)

| 항목 | 실측 |
|---|---|
| 지금 서 있을 수 있는 면 | `LobbyGroundCollider` BoxCollider **x, z = −20 ~ +20 m**(윗면 y 0)와 과자집 `COL_Floor`뿐 |
| 마당 울타리 | 52개, x, z = −18 ~ +18 m. **네 방향 쿠키 길(길 끝 ±18.1 m)이 울타리를 지나는 자리가 뚫려 있고** 부서진 울타리도 있어 밖으로 걸어 나갈 수 있다 |
| 울타리 밖에 보이는 땅 | `Ground_Exterior_Far`(128 × 128 m, 정점 1,089개) — **충돌체 없음**. 높이: 중심~32 m는 평평(y −0.12), 32~40 m부터 올라가 가장자리(±64 m)에서 최대 **y 7.6 m 언덕** |
| 결과 | ±20 m를 넘는 순간 발밑에 충돌체가 없어 추락. `VoidKillZone`(±30 m, y −15) 안이면 스폰 지점으로 복귀, 그 밖이면 `FallGuard`(y −100)까지 떨어진 뒤에야 복귀 |

즉 **보이는 땅(±64 m)과 밟을 수 있는 땅(±20 m)이 일치하지 않는 것**이 원인이다(research.md §H H4-1과 같은 문제).

### 9.2 수정안 (추천: 보이는 땅을 모두 밟을 수 있게)

| # | 작업 | 내용 |
|---|---|---|
| ✅ F1 | **원거리 지면 충돌체** `LobbyFarGroundCollider` | 씬에 새 오브젝트, 위치·회전 0, 스케일 1. **MeshCollider**(`Ground_Exterior_Far_Mesh` 그대로 사용, **non-convex**), Static. 원거리 지면은 걸어 다니는 넓은 정적 지형이라 non-convex가 맞다(convex로 만들면 언덕이 하나의 볼록 덩어리가 돼 모양이 망가진다). 기존 `LobbyGroundCollider`(±20 m, y 0)는 그대로 두어 마당은 지금과 같다 |
| ✅ F2 | **맵 가장자리 투명 벽** `LobbyWorldBounds` | 원거리 지면 끝(±64 m) 안쪽 **±63 m**에 BoxCollider 4개(두께 1 m, 높이 20 m, 렌더러 없음). 언덕을 넘어 지면 밖으로 떨어지는 것을 막는다. 안개(밀도 0.025) 때문에 이 거리는 화면에 거의 보이지 않는다 |
| ✅ F3 | `VoidKillZone` 확장 | 60 × 60 → **140 × 140 m**. F1·F2로 추락은 없어지지만, 혹시 떨어져도 y −100까지 가지 않고 −15에서 바로 복귀하도록 안전망을 맞춘다 |

- **왜 프리팹이 아니라 씬에 두나**: 환경 프리팹은 `Build Witch Cookie Props`를 실행할 때마다 다시 만들어진다. 지면 충돌체를 프리팹 안에 넣으면 재빌드 때 사라진다(지난번 `LobbyGroundCollider`와 같은 이유).
- 참고 — 마당 경계(±20 m)에서 원거리 지면(y −0.12)으로 **12 cm 내려가는 단차**가 생긴다. 보이는 지면도 같은 높이로 내려가 있어 시각과 일치하고, 쿠키 캡슐(반지름 0.46 m)이 넘나들 수 있는 높이다(검증 V-F2에서 확인).

### 9.3 대안 (택하지 않음)

| 대안 | 이유 |
|---|---|
| 울타리 선(±18.6 m)에 투명 벽 — 마당 밖으로 못 나가게 | 보이는 길이 울타리 밖으로 이어지는데 막혀 있으면 어색하다. 지난 결정(D5)에서 경계벽을 두지 않기로 했다 |
| 임포터에서 `Ground_Exterior_Far`에 자동으로 충돌체 추가 | GameScene 등 다른 씬에도 자동 적용되는 장점이 있지만 에디터 도구 규칙(`COL_` = convex) 변경과 FBX 재임포트가 필요하다. GameScene에 환경을 넣을 때 함께 검토 |

### 9.4 작업 방법과 주의

- (계획 당시) LobbyScene에 미저장 변경이 있어 Additive로 열 예정이었으나, 작업 시점에는 에디터에 저장된 GameLobbyScene만 열려 있어 그대로 작업했다.
- 런타임 코드 변경 없음.

### 9.5 검증

| # | 확인 |
|---|---|
| V-F1 | 레이캐스트: (25, ·, 25), (−40, ·, 10), (0, ·, −55), (60, ·, 60) 등 원거리 지면 여러 곳에서 아래로 쏘아 `LobbyFarGroundCollider`에 닿는지, 닿는 높이가 보이는 지면 높이와 같은지 |
| V-F2 | 마당 경계(±20 m) 단차 12 cm를 쿠키 캡슐이 넘을 수 있는지 — 캡슐 스윕으로 확인 |
| V-F3 | 가장자리 벽: ±63 m 밖으로 캡슐이 나가지 못하는지 |
| V-F4 | 콘솔 에러 0, 씬 Missing 참조 0, LobbyScene 미저장 변경 유지 |
| V-F5 | Play 모드 실제 주행(사용자 확인 권장): 길을 따라 울타리 밖으로 나가 언덕까지 걸어 보기 |

### 9.6 작업 결과 (✅ 2026-09-27 완료)

| 항목 | 결과 |
|---|---|
| ✅ F1 | `LobbyFarGroundCollider` — MeshCollider(`Ground_Exterior_Far_Mesh`, non-convex), Static. 범위 (−64, −0.12, −64) ~ (64, 7.63, 64) |
| ✅ F2 | `LobbyWorldBounds/Wall_North·South·East·West` — BoxCollider, 안쪽 면 ±63 m, 높이 y 0~20 m, 렌더러 없음, Static |
| ✅ F3 | `VoidKillZone` 60 × 2 × 60 → **140 × 2 × 140 m** |
| ✅ 저장 | GameLobbyScene 저장, `dirty = False` |

| 검증 | 결과 |
|---|---|
| ✅ V-F1 | 레이캐스트 6곳 모두 바닥에 닿음: (25, 25) y −0.12, (−40, 10) y −0.02, (0, −55) y 2.19(언덕), (60, 60) y 1.20, (−50, −50) y 1.39 → `LobbyFarGroundCollider` / (0, 19.5) y 0 → `LobbyGroundCollider` |
| ✅ V-F2 | 쿠키 캡슐(r 0.46)을 원거리 지면에서 마당 쪽으로 스윕 — 12 cm 턱과의 접촉면이 위쪽에서 **40°** 기울어진 경사로 계산됨(수직 벽이 아님) → 물리가 캡슐을 밀어 올려 넘어갈 수 있다 |
| ✅ V-F3 | +x 방향 캡슐 스윕이 `Wall_East`(x 63)에 막힘. 네 벽 범위 확인 |
| ✅ V-F4 | 콘솔 에러 0, 씬 Missing 스크립트 0 |
| ⬜ V-F5 | Play 모드 실제 주행은 하지 않았다(사용자 확인 권장) |

---

## 10. LobbyScene 배경 이미지 (✅ 2026-09-27 완료)

| 항목 | 내용 |
|---|---|
| 대상 | `Assets/Scenes/LobbyScene.unity` → `LobbyUICanvas`(Screen Space Overlay, CanvasScaler 1920 × 1080) |
| 추가 | `BackGround` — RectTransform 전체 늘림(앵커 0~1), Image(스프라이트 `Assets/08. Resources/LobbySceneBackGround.png`, 흰색, **Raycast Target 끔** — 뒤 UI 클릭을 막지 않음), **AspectRatioFitter(Envelope Parent, 1.78)** — 화면 비율이 16:9가 아니어도 이미지가 찌그러지지 않고 화면을 꽉 채움(넘치는 부분은 잘림) |
| 순서 | 캔버스의 **첫 번째 자식**이라 `LobbyPanel` 뒤에 그려진다 |
| 텍스처 | 이미 Sprite(2D and UI) 타입이어서 임포트 설정은 바꾸지 않았다 |
| 결과 | ✅ 씬 저장, 콘솔 에러 0. ⬜ 화면 확인은 Game 뷰에서 사용자 확인 권장(Overlay 캔버스는 에디터 스크립트로 렌더 캡처하지 않았다) |

---

## 11. 나무 추가 + 랜턴 분산 배치 (2026-09-27, ✅ 완료)

### 11.1 현황 (에디터 실측)

| 항목 | 현재 |
|---|---|
| 나무(`Twisted_Tree_A/B/C`) | **26그루**, 모두 마당 바로 바깥 한 줄(중심에서 약 20~33 m 원형 띠). §9에서 ±63 m까지 걸을 수 있게 됐지만 **33~63 m 구역은 비어 있다** |
| 서 있는 랜턴(`Candy_Lantern`) | **16개**, 전부 네 방향 쿠키 길 양옆(길 중간 ±12 m, 울타리 문 ±18 m)에만 있다. 마당 모서리·과자집 옆·울타리 밖은 랜턴이 없다 |
| 매달린 랜턴(`Hanging_Lantern`) | 4개, 과자집 **실내** 천장(y 9.8) |
| 실제로 주변을 비추는 빛 | 달빛(0.35) + Point Light 2개(정면 문, 스폰 왼쪽)뿐. 랜턴 재질은 **스스로 빛나 보이기만 하고 주변을 비추지 않는다**(라이트맵 없음) → 길 밖은 어둡다 |
| 조명 한계 | Forward 렌더링, 품질 Ultra, **Pixel Light Count 4** — 한 물체를 픽셀 단위로 비추는 점광원은 가장 가까운/강한 4개까지, 나머지는 정점 조명으로 처리된다 |

### 11.2 작업안

모두 **씬의 새 그룹 `LobbyDecorExtra`** 아래에 둔다(환경 프리팹은 빌더가 다시 만들면 덮어쓰므로 프리팹을 수정하지 않는다). 소품은 기존 소품 프리팹(`04. Prefabs/Environment/WitchCookieProps/…`)의 인스턴스로 놓는다 — 새 에셋 없음.

| # | 작업 | 내용 |
|---|---|---|
| ✅ T1 | **나무 추가 ~30그루** | `Twisted_Tree_A/B/C`를 섞어 **중심에서 36~58 m 띠**에 배치. 고정 시드로 위치를 뽑고, 기존 나무·다른 새 나무와 **최소 6 m 간격**, 회전 무작위(Y축), 크기 0.85~1.25배. 나무에는 이미 볼록 충돌체(`COL_`)가 있어 부딪힌다 |
| ⚠️ T2 | **마당 안 나무 4그루(3그루 배치, §11.5)** | 마당 네 모서리 근처(약 ±14, ±14)에 1그루씩 — 쿠키 길과 울타리 문, 기존 소품에서 3 m 이상 떨어진 자리만 |
| ✅ L1 | **서 있는 랜턴 추가 ~14개** (`Candy_Lantern`) | 마당 안: 과자집 네 모서리 바깥(약 ±10, ±10)에 4개, 울타리 안쪽 둘레 빈 구간(모서리·길 사이 중간)에 8개 / 울타리 밖: 네 문 바깥 길 끝 앞에 2개씩은 넘치므로 **남·북 문 바깥 2개**만 |
| ✅ L2 | **새 나무에 매달린 랜턴 ~6개** (`Hanging_Lantern`) | T1 나무 중 6그루의 부착점(`Twisted_Tree_*_Attach_0`)에 걸어, 울타리 밖 숲에도 드문드문 불빛이 보이게 |
| ✅ P1 | **실제 빛(Point Light)** | L1 랜턴 중 **마당 안 12개**에 Point Light를 1개씩(주황 (1.0, 0.6, 0.3), Range 5 m, Intensity 0.9, **그림자 없음**, Realtime). L2와 울타리 밖 랜턴은 빛 없이 발광 재질만(멀리서 불빛으로만 보임) |
| ✅ A1 | **주변광 소폭 상향** | Ambient Sky (0.16, 0.13, 0.26) → **(0.22, 0.18, 0.34)**, Equator (0.10, 0.08, 0.14) → **(0.14, 0.11, 0.19)**. 전체가 한 단계 밝아지되 밤 분위기는 유지. 빛 추가 없이 비용 0 |

### 11.3 성능·게임성 영향

- Point Light가 2 → **14개**. Pixel Light Count 4라서 한 물체에 픽셀 조명은 최대 4개까지만 적용되고, 멀리 있는 조명은 자동으로 정점 조명으로 떨어져 비용이 제한된다. 그림자를 끄므로 그림자 맵 비용은 없다. 실제 프레임은 Play 모드 프로파일링으로 확인해야 한다.
- 나무 34그루·랜턴 20개 추가는 소품 408개에 비해 작다. 모두 Static 프리팹이라 씬 배치 시 정적 배칭 대상.
- 스폰 지점(0, 0, 12) 반경 6 m 안에는 아무것도 새로 두지 않는다(스폰 빈 자리 비율 유지).
- 가마솥 위치가 나중에 정해지면 그 주변 랜턴·Point Light는 옮길 수 있다.

### 11.4 검증

- 배치 후 새 오브젝트끼리·기존 소품과 겹침 검사(콜라이더 교차), 스폰 빈 자리 비율 재측정(§8.2 V3 기준 77% 유지), 콘솔 에러 0, 스폰 시점·전경 렌더 2장으로 밝기 비교.

### 11.5 작업 결과 (✅ 2026-09-27 완료)

모두 씬 루트 `LobbyDecorExtra` 아래(`Trees`, `Lanterns`, `HangingLanterns`)에 소품 프리팹 인스턴스로 두었다. 위치는 고정 시드(20260927)로 뽑아 다시 실행해도 같다.

| 항목 | 결과 |
|---|---|
| ✅ T1 | `ExtraTree_0~29` **30그루**, 중심 36~58 m, 기존·새 나무와 6 m 이상 간격, 크기 0.85~1.25 |
| ⚠️ T2 | `YardTree_0~2` **3그루**(계획 4그루). (15, 14.8), (13.3, −14.9), (−13.3, 14.6). 남서 모서리(−, −)는 주변 소품과 **3 m 간격을 확보할 자리가 없어** 두지 않았다. 모서리 나무 수관이 과자집과 겹치지 않음 확인 |
| ✅ L1 | `ExtraLantern_0~13` **14개** — 과자집 네 모서리 바깥 4, 울타리 안쪽 둘레 8, 남·북 문 바깥 2 |
| ✅ L2 | `ExtraHangingLantern_0~5` **6개** — 새 나무 6그루의 `Attach_0` 부착점 |
| ✅ P1 | 마당 안 랜턴 **12개**에 자식 `Light`(Point, 주황, Range 5, 강도 0.9, 그림자 없음) |
| ✅ A1 | 주변광 Sky (0.22, 0.18, 0.34), Equator (0.14, 0.11, 0.19) |
| ✅ 저장 | GameLobbyScene 저장 |

| 검증 | 결과 |
|---|---|
| ✅ 겹침 | 새 오브젝트의 충돌체와 기존 충돌체 사이 침투(`ComputePenetration`) **0건** |
| ✅ 스폰 | 스폰 빈 자리 154/200(77%) — 변화 없음 |
| ✅ 콘솔 | 에러 0 |
| ✅ 화면 | 전경·마당 모서리·스폰 시점 렌더 확인 — 울타리 밖 숲이 두 겹으로 채워지고, 마당 둘레와 숲 속에 랜턴 불빛이 드문드문 보인다. 주변광 상향으로 어둠이 한 단계 덜하다. Point Light 빛 웅덩이는 반경 5 m라 **은은한 편** — 더 밝게 원하면 강도·범위를 올린다 |
| ⬜ 프레임 | Play 모드 프로파일링 미실시(Point Light 2 → 14개) |

---

## 12. 과자집 문 자동 개폐 — 캐릭터가 다가가면 열리고 떠나면 닫힘 (2026-09-27, 안 A ✅ 완료 / 12.8 개선 승인 대기)

### 12.1 현재 문 구조 (에디터 실측)

| 문 | 경첩(피벗) | 문짝 충돌체 `COL_Door_{Side}` 중심 / 크기 | Animator | Rigidbody |
|---|---|---|---|---|
| Front | (0.72, 0, 7.00) | (0, 1.52, 7.08) / 1.44 × 2.91 × 0.15 | `Door_Front`(Bool `IsOpen`), Update **Normal**, Always Animate | kinematic |
| Back | (−0.72, 0, −7.00) | (0, 1.52, −7.08) / 1.44 × 2.91 × 0.15 | 같음 | kinematic |
| Left | (7.00, 0, −0.72) | (7.08, 1.52, 0) / 0.15 × 2.91 × 1.44 | 같음 | kinematic |
| Right | (−7.00, 0, 0.72) | (−7.08, 1.52, 0) / 0.15 × 2.91 × 1.44 | 같음 | kinematic |

- 문은 모두 **안쪽으로 −90°** 열리고(0.6초), 열린 문짝은 안쪽 벽에 붙는다. 개구부 1.5 × 3.0 m.
- **지금은 `IsOpen`을 바꾸는 코드가 없어** 문 4개가 닫힌 채 충돌체로 막혀 있다(research.md §G5-1).
- 문을 열면 부수 효과로 **실내 가마솥(3, 0.5, 0)에도 닿을 수 있게 된다**(보류 중인 D2의 제약이 풀림).

### 12.2 동작 규칙

| 규칙 | 내용 |
|---|---|
| 감지 구역 | 문짝(닫힌 상태) 중심 기준, 벽을 따라 **폭 2.2 m**(개구부 1.5 m + 여유), 벽 앞뒤로 **각 2.5 m**, 높이 0~3.2 m 상자. 안쪽 2.5 m는 문짝이 도는 범위(1.44 m)를 덮는다 |
| 누가 여나 | 구역 안에 캐릭터가 있으면 연다 — `CharacterRegistry`의 쿠키·괴물(공통 계약 `IGameCharacter`). **파괴된 쿠키(관전 중)는 제외** |
| 열기 | `IsOpen = true`. **열기 시작하는 순간 문짝 충돌체를 끈다** — 안쪽으로 도는 문짝이 안에 있는 캐릭터를 밀어내지 않게, 그리고 열리는 0.6초 동안 문에 막히지 않게 |
| 닫기 | 구역이 **0.5초 동안 비어 있으면** `IsOpen = false`. 닫힘 애니메이션(0.6초)이 끝난 뒤에 충돌체를 다시 켠다. 닫히는 중 누가 들어오면 바로 다시 연다 |
| 검사 주기 | 문당 0.1초마다 캐릭터 목록(보통 4명)과 상자 비교 — 물리 쿼리 없이 위치 비교만 하므로 비용이 거의 없다 |

### 12.3 네트워크 — 동기화 없이 각 클라이언트가 계산

- 문 상태는 **캐릭터 위치에서 파생되는 표시 상태**다. 캐릭터 위치는 이미 모든 클라이언트에 동기화되므로, 각 클라이언트가 같은 규칙으로 계산하면 같은 결과가 나온다. RPC·Room Props가 필요 없다.
- **내 캐릭터는 내 클라이언트의 실제 위치로 판정**하므로 지연 때문에 닫힌 문에 막히는 일이 없다. 다른 사람 화면에서는 원격 보간 지연(약 0.1초)만큼 늦게 열릴 뿐이고, 원격 캐릭터는 kinematic이라 문에 막히지 않는다.
- 늦게 들어온 사람, 방장 교체, 메시지 큐를 멈춘 괴물 대기(§GameRule)에도 영향이 없다 — 저장할 네트워크 상태가 없기 때문.
- 한계: 클라이언트마다 문이 열린 시점이 0.1초 정도 다를 수 있다(보이는 차이만, 게임 판정에는 영향 없음).

### 12.4 구현 위치 (결정 필요 — 추천 A)

| 안 | 방법 | 장점 | 단점 |
|---|---|---|---|
| **A (추천)** | 런타임 스크립트 `Assets/02. Scripts/Environment/AutoDoor.cs`(새 도메인 폴더) 작성 → **과자집 빌더(`WitchCookieHouseBuilder`)가 문 4개에 `AutoDoor`를 붙이고 Animator를 `AnimatePhysics`로 설정** → `Tools/TagOfChaos/Build Witch Cookie House`로 과자집 프리팹 재생성 | 빌더를 다시 돌려도 사라지지 않는다. 과자집을 놓는 **모든 씬**(나중의 GameScene 포함)에서 자동으로 동작. 환경 프리팹은 과자집 프리팹을 중첩하므로 따라서 갱신 | 에디터 asmdef(`TagOfChaos.Editor`)에 런타임 asmdef 참조 1줄 추가 필요. 빌더 재실행 시 문 컨트롤러 에셋 GUID가 바뀐다(research.md G3-1 — 프리팹이 같은 빌드에서 다시 저장되므로 동작에는 영향 없음) |
| B | 같은 `AutoDoor` 스크립트를 GameLobbyScene의 환경 인스턴스 문 4개에 **씬 오버라이드**로 붙임 | 빌더·asmdef를 건드리지 않음 | 다른 씬에 과자집을 놓을 때마다 다시 붙여야 함. 빌더 재실행으로 프리팹 구조가 바뀌면 오버라이드가 끊길 위험 |

### 12.5 코드 개요 (안 A)

- `AutoDoor : MonoBehaviour`(Photon 콜백 불필요) — 필드: `openParameter = "IsOpen"`, `zoneWidth 2.2`, `zoneDepth 2.5`, `zoneHeight 3.2`, `closeDelay 0.5`, `checkInterval 0.1`, 문짝 충돌체(자동 탐색: 자식 `COL_` MeshCollider).
- Awake: 닫힌 상태의 문짝 충돌체 경계에서 **구역 중심과 방향**(가장 얇은 축 = 벽의 법선)을 계산해 저장 — 문 방향별 하드코딩 없음. 닫힘 애니메이션 길이는 Animator 클립에서 읽는다.
- Update(0.1초 간격): `CharacterRegistry.All`을 돌며 살아 있고 파괴되지 않은 캐릭터가 구역 안에 있는지 → 열기/닫기 상태 전환.
- 에디터 Gizmo로 감지 구역을 표시(씬 뷰 확인용).
- 코드 주석만 한글, 식별자·로그는 영어(CLAUDE.md).

### 12.6 검증

| # | 확인 |
|---|---|
| V-D1 | 컴파일 에러 0, 과자집 빌더 재실행 후 `Verify Witch Cookie House` ALL OK, 문 4개에 `AutoDoor` 존재 |
| V-D2 | GameLobbyScene의 환경 인스턴스가 갱신된 프리팹을 따라가는지, 씬 Missing 참조 0 |
| V-D3 | Play 모드(에디터): 테스트용 캐릭터를 구역 안/밖으로 옮겨 열림·닫힘·충돌체 on/off 순서 확인 |
| V-D4 | 실제 플레이(사용자 확인 권장): 쿠키로 문을 통과해 실내 출입, 닫히는 도중 재진입 |

### 12.7 진행 체크리스트 (안 A)

| # | 작업 | 상태 |
|---|---|---|
| S1 | `Assets/02. Scripts/Environment/AutoDoor.cs` 작성 | ✅ 완료 |
| S2 | `Assets/Editor/TagOfChaos.Editor.asmdef`에 `TagOfChaos.Scripts` 참조 추가 | ✅ 완료 |
| S3 | `WitchCookieHouseBuilder`: 문에 `AutoDoor` 추가, Animator Update Mode = Fixed(물리), 파라미터 상수를 `AutoDoor.DefaultOpenParameter`로 통일, Verify에 AutoDoor·Fixed 검사 추가 | ✅ 완료 |
| S4 | 컴파일 에러·경고 확인 | ✅ 완료 — 에러·경고 0. 리로드 직후 `UnityEditor.Graphs.Edge.WakeUp` NRE 1건은 열려 있던 Animator 창의 Unity 내부 예외(프로젝트 코드 무관) |
| S5 | `Tools/TagOfChaos/Build Witch Cookie House` 재실행 + Verify ALL OK | ✅ 완료 — 문 4개 `autoDoor=True fixedUpdate=True OK`, 연기·메시·충돌체 OK, **VERIFY ALL OK** |
| S6 | GameLobbyScene의 환경 인스턴스 갱신 확인(문 4개 AutoDoor, Missing 0) | ✅ 완료 — AutoDoor 4개, 컨트롤러 Door_{Side} 연결, Update Fixed, 인스턴스 오버라이드 없음, Missing 0 |
| S7 | Play 모드 실제 동작 검증(V-D3) | ✅ 완료 — 오프라인 방(`OfflineMode`)으로 스폰한 쿠키를 옮겨 가며 확인: ① 구역 밖(z 10.5)에서 정면 문 닫힘·충돌체 켜짐, 집 쪽 캡슐 스윕이 `COL_Door_Front`(z 7.15)에 막힘 ② 구역 안(z 8.8) 진입 → 열림·충돌체 꺼짐·회전 270°(−90°), 같은 스윕이 실내 6 m까지 통과 ③ 구역 이탈(z 14) → 닫힘·충돌체 복구·회전 0° ④ 파괴된 쿠키(HitCount 2)는 구역 안에서도 문이 열리지 않음, 복구 후 즉시 열림 ⑤ 좌·후·우 문도 각자 구역에서만 열림. 사용자가 직접 조작해 실내 출입도 확인 |
| S8 | 콘솔 Exception·Warning 최종 확인, 문서 정리 | ✅ 완료 — AutoDoor 관련 예외·경고 0. 기타 3건은 무관: Animator 창의 Unity 내부 NRE(`UnityEditor.Graphs.Edge.WakeUp`), 원래 수정 중이던 `NotoSansKR SDF` 동적 폰트 재임포트 경고, 테스트 중 쿠키를 강제로 kinematic으로 만든 데서 생긴 `Setting linear velocity of a kinematic body` 1건(실제 게임 경로에서는 들림·파괴 시 Move 전에 반환해 발생하지 않음) |

### 12.8 사용자 피드백 — 실내에서 밖으로 나갈 때 부자연스러움 (→ §13 양방향 문으로 대체)

**원인**: 문은 **안쪽으로만** 90° 열린다. 밖에서 들어올 때는 문이 캐릭터에게서 멀어지는 쪽으로 열려 자연스럽지만, **실내에서 나갈 때는 문짝이 캐릭터 쪽으로 돌아온다.**
열기 시작할 때 충돌체를 꺼 두어 밀리지는 않지만, 문짝이 캐릭터 몸을 **관통해 지나가는 것처럼 보인다.**
- 실측: 문짝이 도는 범위는 경첩 기준 반지름 1.44 m 부채꼴(실내 쪽). 실내 감지 깊이 2.5 m, 열림 0.6초 → 걷는 속도 5 m/s(달리기 6.5)면 캐릭터가 약 0.2초 만에 부채꼴에 들어가는데, 문은 0.6초 동안 돌고 있어 몸과 겹친다.

**바깥쪽으로 여는 안은 불가능(측정 결과)**: 미리보기 씬에서 문을 +90°(바깥)로 돌려 보면 네 문 모두 문짝이 벽 윗부분(`COL_Wall_*_Top`)·왼쪽 벽(`COL_Wall_*_L`)·문틀(`COL_DoorFrame_*_L`)과 아이싱 장식을 관통한다(안쪽 회전은 겹침 0). 경첩이 벽 안쪽 면에 있고 문짝이 벽 홈 안에 들어가 있는 모델 구조 때문이다. 바깥 열림을 하려면 Blender에서 문·경첩을 다시 만들어야 한다.

**개선안 (추천: 모델 변경 없이 "나갈 때는 미리, 빠르게 열기")**

| # | 변경 | 효과 |
|---|---|---|
| E1 | 감지 구역을 **앞뒤 비대칭**으로: 바깥 2.5 m 유지, **실내 4.0 m** | 실내에서 다가오는 캐릭터를 더 일찍 감지. 실내 4.0 m면 좌측 문 구역이 가마솥(3, 0.5, 0)에 닿지 않는다(4.5 m면 닿아 가마솥 옆에 서 있는 동안 문이 계속 열림) |
| E2 | **열 때만 애니메이션 2배속**(0.6 → 0.3초), 닫을 때는 원래 속도 | 문이 캐릭터가 부채꼴에 닿기 전에 다 열린다: 실내 4.0 m − 1.44 m = 2.56 m를 달리기(6.5 m/s)로 0.39초 → 문 0.3초에 완료 |
| E3 | "실내/실외"는 코드가 방향을 계산(문짝 법선 중 과자집 중심 쪽 = 실내) — 문 방향별 하드코딩 없음 | 네 문 공통 |

- 한계: 문 바로 뒤(부채꼴 안)에 **서 있는** 캐릭터가 있을 때는 여전히 문짝이 겹쳐 보인다(안쪽 전용 경첩의 구조적 한계).
- 구현 범위: `AutoDoor.cs`만 수정(필드 `innerDepth 4.0`, `outerDepth 2.5`, `openSpeed 2.0`). 빌더·프리팹 재생성 불필요(필드 기본값이 적용됨 — 단, 프리팹에 직렬화된 기존 `zoneDepth` 값은 새 필드로 대체되므로 빌더를 한 번 다시 실행해 값을 확정).
- 검증: 컴파일·콘솔 확인, Play 모드에서 실내 쪽 다양한 거리(4 m, 3 m)에서 달리기 속도로 접근했을 때 문이 캐릭터가 부채꼴에 닿기 전에 다 열리는지 시간 측정.

| 대안 | 내용 |
|---|---|
| B | Blender에서 경첩을 벽 바깥 면으로 옮겨 **양방향 문**으로 재제작 — 가장 자연스럽지만 모델·클립·빌더 수정 필요 |
| C | 여닫이 대신 **미닫이(벽 속으로 밀려 들어가는) 문**으로 변경 — 캐릭터와 겹칠 일이 없지만 문 디자인이 달라짐 |

---

## 13. 양방향 문(안·밖 모두 열림) + 문 관통 금지 계획 (2026-09-27, 승인 대기)

### 13.1 사용자 요구

1. **플레이어는 어떤 경우에도 문을 뚫고 지나갈 수 없다.** 지금은 열리는 순간 문짝 충돌체를 꺼서(§12.2) 도는 문짝·열린 문짝을 관통할 수 있다.
2. **바깥으로도 열려야 한다** — 캐릭터에게서 멀어지는 쪽으로 열린다(밖에서 오면 안으로, 안에서 오면 밖으로).

### 13.2 지금 바깥으로 못 여는 이유 (실측)

| 원인 | 수치 |
|---|---|
| ① 문짝이 벽 **안쪽 면**에 붙어 있고 경첩도 안쪽 면 모서리(0.72, z 7.00)에 있다 | 벽 두께 0.5 m(z 7.00~7.50). 바깥으로 돌면 문짝 두께(0.15 m)가 **문설주(`COL_Wall_Front_L`, x ≥ 0.75) 속으로** 파고든다 |
| ② 문짝이 **아치 모양으로 높다** | 문짝 꼭대기 2.97 m. 벽 두께 구간의 개구부 윗면은 2.90 m(`COL_Wall_Front_Top`)이고, 보이는 아치는 가장자리에서 더 낮다(스프링 라인 2.25 m). 벽 두께를 지나 바깥으로 돌 때 윗부분이 벽·문틀·아이싱 장식을 관통 |

### 13.3 해결 설계 — 사각 문짝 + 고정 아치 상인방 (가상 검증 완료)

| 항목 | 현재 | 변경 |
|---|---|---|
| 문짝 모양 | 아치형, 높이 2.97 m, 폭 1.44 m | **사각형, 높이 2.15 m**(바닥 0.06~2.15), 폭 **1.385 m**. 쿠키 캡슐 높이 2.0 m보다 높다 |
| 문짝 위치 | 벽 안쪽 면(z 7.00~7.15) | **벽 두께 한가운데**(중심 z 7.25) |
| 경첩 축 | 안쪽 면 모서리 (0.72, 7.00) | 벽 가운데에서 문설주로부터 **8.5 cm 안쪽**(0.665, 7.25) — 문짝 두께 모서리가 돌 때 문설주에 닿지 않는 거리 |
| 아치 윗부분 | 문짝의 일부 | 잘라낸 아치 윗부분을 **고정 상인방 `DoorTransom_{Side}`**(같은 진저브레드 재질)으로 개구부 윗부분에 붙박음 + 충돌체 `COL_DoorTransom_{Side}`(점프한 쿠키가 틈으로 머리를 넣지 않게) |

**가상 검증(Unity 미리보기 씬, 실제 과자집 기준)**
- 위 치수의 문짝 충돌체를 경첩 기준 −90°~+90°로 3°씩 돌리며 과자집의 모든 충돌체와 침투 검사 → **안·밖 모두 0건**.
- 네 문 각각, 문짝이 쓸고 지나가는 반원(반지름 1.405 m, 높이 0.05~2.17 m) 안에 들어오는 **모든 메시 정점**(벽·문틀·기둥·장식·실내 소품)을 검사 → **네 문 모두 0개**.
- 바깥 90°에서 문짝은 x 0.59~0.74, z 7.25~8.64(정면 기준)를 차지 — 문틀 충돌체(x ≥ 0.75)·사탕 기둥(x ≥ 1.18)·아이싱(y ≥ 2.18)과 떨어져 있다.

**보이는 변화**: 문이 "아치형 한 장"에서 **"사각 문 + 위쪽 아치 장식판"**으로 바뀐다. 소용돌이 장식은 문짝 가운데 높이에 있어 그대로 남는다.

### 13.4 문 관통 금지 (요구 1)

| 규칙 | 내용 |
|---|---|
| 충돌체 항상 켬 | 문짝 충돌체를 **절대 끄지 않는다**. 닫힘·열림·도는 중 모두 막는다 |
| 문짝 구동 | Animator 대신 `AutoDoor`가 kinematic Rigidbody를 **`MoveRotation`으로 물리 스텝마다 돌린다** — 물리 엔진이 움직이는 문짝을 정확히 인식해 캐릭터가 파고들지 않는다 |
| 사람이 막고 있으면 멈춤 | 다음 스텝 자세가 캐릭터 몸과 겹치면 그 스텝은 돌지 않고 **그 자리에서 멈춘다**(밀어내거나 벽에 끼우지 않음). 비키면 다시 돈다 |
| 여는 방향 | 처음 감지한 캐릭터가 있는 쪽의 **반대편**으로 연다. 열려 있는 동안은 방향을 바꾸지 않는다. 다 닫힌 뒤 다시 감지하면 새로 방향을 정한다 |
| 속도·구역 | 열기 0.45초(빠르게 시작해 부드럽게 멈춤), 닫기 0.6초. 감지 구역: 벽 앞뒤 **각 3.0 m**, 폭 2.2 m, 닫기 지연 0.5초 |

- 네트워크 원칙은 §12.3과 같다(각 클라이언트가 동기화된 캐릭터 위치로 계산, RPC 없음). 양쪽에서 동시에 다가오는 드문 경우 클라이언트마다 여는 방향이 다를 수 있으나, 각 클라이언트의 내 캐릭터 충돌은 그 클라이언트의 문 상태로 계산되므로 **내 화면에서 문을 뚫는 일은 없다.**

### 13.5 작업 단계

| # | 작업 | 도구 |
|---|---|---|
| B0 | `Witch_Cookie_House.blend` 백업(`리소스/WitchCookieHouse/Witch_Cookie_House_backup_20260927.blend` — 이 폴더는 git 추적 밖이라 백업 필수) | 파일 복사 |
| B1 | 문 4개 수정: 문짝을 2.15 m에서 잘라 사각형으로, 경첩 쪽 5.5 cm 잘라 폭 1.385 m, 벽 가운데로 이동, 원점 = 새 경첩 축 | Blender 5.2 백그라운드 실행(`blender.exe --background … --python`) + 새 스크립트 `BlenderScripts/wch_doors_v2.py` |
| B2 | 잘라낸 윗부분으로 `DoorTransom_{Side}` 생성(개구부 폭에 맞춤), `COL_DoorTransom_{Side}` 상자 충돌체 추가, `COL_Door_{Side}`를 새 문짝 크기로 교체 | 같은 스크립트 |
| B3 | FBX 재내보내기 — 기존 `wch_export.py`의 축 보정 패치 사용. 단독 실행용 진입 스크립트 `wch_run_export.py` 추가(research.md §G.4 지적 해결) | Blender |
| B4 | 검수 렌더(정면 닫힘, 안·밖으로 열림) | Blender |
| U1 | `AutoDoor.cs` 재작성: MoveRotation 구동, 양방향, 막히면 멈춤, 충돌체 항상 켬, Animator 불필요 | 코드 |
| U2 | 빌더: 문에 Animator·문 클립 대신 `AutoDoor` + kinematic Rigidbody(보간 켬). 쓰지 않게 되는 문 클립 8개·컨트롤러 4개 삭제(연기 클립·컨트롤러는 유지). Verify에 **양방향 스윕 충돌 검사** 추가 | 코드 |
| U3 | FBX 재임포트 → `Build Witch Cookie House` → Verify ALL OK → GameLobbyScene 인스턴스 확인 | Unity |
| U4 | 컴파일·콘솔(Exception/Warning) 확인 | Unity |
| U5 | Play 모드 검증: ① 밖→안: 안으로 열림 ② 안→밖: **밖으로 열림** ③ 닫힌 문·열린 문짝·도는 문짝 모두 관통 불가(캡슐 스윕 + 실제 이동) ④ 문짝 경로에 서 있으면 문이 멈추고 비키면 다시 돎 ⑤ 파괴된 쿠키는 열지 못함 | Unity Play |

### 13.6 위험·영향

- 문 모양이 바뀐다(사각 문 + 아치 장식판). 다른 디자인을 원하면 B1 전에 알려줘야 한다.
- Blender 원본(.blend)을 수정한다 — B0 백업으로 되돌릴 수 있다. FBX·프리팹은 git으로 되돌릴 수 있다(단, 과자집 에셋 폴더도 아직 커밋되지 않은 상태라 **작업 전 상태를 보존하려면 커밋을 먼저 하는 것을 권장**).
- 문 클립·컨트롤러 삭제 — 문 외에는 참조하는 곳이 없음을 삭제 전에 다시 확인한다.
