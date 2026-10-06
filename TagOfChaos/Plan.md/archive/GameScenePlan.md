# 계획: 맵 5종을 게임 씬으로 — 씬 구성·분위기 조명·후처리(Bloom)·지면 충돌·상호작용 문·회전 연출 (GameScenePlan.md)

> 상태: **🔵 진행 중, 2026-09-27** — 결정 §5.1, 진행 현황 §9.
>
> **개정 안내**: 이 문서는 이전 판(2026-08-14 "색상 결정 & 술래 지정 시스템 설계", 구현 완료)을 **대체**한다.
> 이전 판은 `git show 62c9882:TagOfChaos/Plan.md/GameScenePlan.md`로 볼 수 있다.
>
> **요청**
> 1. `Assets/Maps`의 맵 5개를 각각 씬으로 만든다. 분위기에 맞게 밝기(어두움)를 조절한다.
> 2. 땅을 뚫는 버그가 있으면 지난번처럼 Ground에 MeshCollider를 추가해 해결한다(GameLobbyScene.md §9).
> 3. 문이 있는 곳은 GameLobbyScene의 상호작용 문처럼, 밖에서 열 때·안에서 열 때를 고려한다.
> 4. 애니메이션: `Carousel_Top`(Z축 회전), `MagicOven_Door`(경첩 기준 Z축 회전으로 열림, 지금은 반쯤 열림), 관람차 회전축 `CandyFerrisWheel_Pivot`(기존 그대로).
> 5. 사진의 광택(초콜릿 강 윤기, 네온 번짐)은 Unity 후처리(Bloom)와 조명으로 넣는다.
>
> **조사 방법**: `Assets/Maps`의 README 6개·조명 JSON 5개·빌드 스크립트 15개를 읽었다. FBX 140개의 노드 이름을 파일에서 직접 파싱했다.
> 원본 Blender 작업 파일의 **임시 사본**을 백그라운드로 열어 맵 빌드 스크립트를 실행하고, 벽에 뚫린 출입구 56개의 위치·폭·높이·방향을 뽑았다(원본 저장 안 함).
> 씬(`GameScene`·`GameLobbyScene`)은 YAML을 파싱해 확인했다. 조사 중 에디터가 Play Mode여서 Unity 조작은 하지 않았다.

---

## 0. 요약

| 항목 | 핵심 |
|---|---|
| 씬 | 기존 `GameScene`을 복제해 맵별 씬 5개를 만든다(게임 규칙·UI·카메라 배선 유지) → 환경만 맵 FBX로 교체 |
| **코드(필수)** | 지금은 판 시작 때 항상 `"GameScene"` 하나만 불러온다(`GameStartAuthority.cs:67`, `MonsterLobbyWaitController.cs:92`). **맵을 고르는 방(Room) 값**을 추가해야 5개 씬이 실제로 쓰인다 |
| **문(중요)** | 맵 FBX에는 **문짝이 없다.** README의 "문 30/5/21개"는 벽에 뚫린 **출입구**다(폭 7~14 m, 높이 7.5~12 m — 괴물 통과 기준). 문짝을 새로 만들어 달아야 한다 |
| 충돌 | 걷는 지형은 FBX 임포트 설정으로 MeshCollider가 붙어 있다. **장식 지형·물에는 없다** → 격자 검사로 뚫리는 곳을 찾아 MeshCollider 추가 |
| 조명 | 맵마다 로컬 조명 33~153개. 지금 렌더링은 Forward(픽셀 조명 최대 4개)라 대부분 버텍스 조명으로 떨어진다 → **Deferred** 권장 |
| 후처리 | Built-in 파이프라인인데 후처리 패키지가 **없다** → Post Processing Stack v2 설치, 맵별 Bloom·색 보정 |
| 회전 연출 | 회전목마 윗부분·관람차는 편집기가 만든 반복 클립으로 돌린다. 관람차 부품은 **FBX 3개에 흩어져 있어** 씬에서 회전축 아래로 다시 묶어야 한다 |

---

## 1. 조사 결과 (실측)

### 1.1 맵 에셋

| 맵 | 분위기 | 랜드마크 | FBX(카테고리) | 오브젝트 수(주요) | 로컬 조명 | 출입구 |
|---|---|---|---|---|---|---|
| CandyForest | 분홍 설탕눈 숲, 낮 | 막대사탕 나무 77 m | 8 | 장식 1,459 · 구조물 193 · 소품 179 | 33 | 없음(야외) |
| GingerbreadVillage | 밤의 진저브레드 마을 | 과자 시계탑 47 m | 9(Terrain 포함) | 장식 960 · 소품 275 · 구조물 103 | 153 | 없음(건물은 막힌 장식) |
| ChocolateFactory | 유리 지붕 공장, 초콜릿 강 | 초콜릿 탱크+첨탑 96 m | 9 | 장식 548 · 소품 321 · 구조물 252 | 77(JSON 95) | **30** |
| CursedCandyCarnival | 네온 놀이공원, 거의 검은 밤 | 관람차 86 m | 8 | 장식 657 · 소품 277 · 배경 204 | 135 | **5** |
| HauntedBakery | 마녀의 유령 빵집, 어두운 보라 | 마법 오븐 45 m | 9 | 구조물 211 · 장식 180 · 소품 124 | 61 | **21** |

- 모든 맵: 350 × 350 m, 지면 윗면 높이 약 0, 두께 3 m 이상. FBX 원점 = 맵 원점 → 씬에 **위치·회전 0, 스케일 1**로 넣으면 맞물린다. 축 변환 적용됨(Blender (x, y, z) → Unity (−x, z, −y)).
- 임포트 설정(`.meta` 확인): **Ground·Terrain·MainStructures·GameplayProps는 `Generate Colliders` 켜짐**(non-convex MeshCollider 자동 생성), 나머지(Background·Decoration·Lighting·Effects·Water)는 충돌체 없음. 애니메이션 가져오기 끔.
- 외곽 투명 벽 `COL_Boundary_*`(재질 `M_Invisible_Collider`)가 Ground FBX에 들어 있다.
- 재질: 기존 프로젝트 재질을 이름으로 재사용, 없는 43개만 `Maps/Common/Materials`(Built-in Standard, 텍스처 없음, 색·광택·발광만).
- `Assets/Maps`는 **아직 git에 커밋되지 않았다**(`??`).

### 1.2 조명 JSON (`<Map>_Lighting.json`, Blender에서 옮긴 값)

| 맵 | 환경광 색 / 강도 | 안개(Exponential) | 방향광 색 / 강도 / 각도 | 느낌 |
|---|---|---|---|---|
| CandyForest | (1.0, 0.74, 0.85) / 1.0 | 분홍, 0.008 | 분홍빛 해 / 1.0 / X 50°·Y −35° | 밝은 낮 |
| GingerbreadVillage | (0.03, 0.05, 0.14) / 0.6 | 남색, 0.006 | 푸른 달 / 0.16 / X 55°·Y 30° | 밤, 가로등·창문 |
| ChocolateFactory | (0.35, 0.22, 0.14) / 0.8 | 갈색, 0.004 | 주황 햇빛 / 0.75 / X 60°·Y −20° | 따뜻한 실내 |
| CursedCandyCarnival | (0.01, 0.025, 0.04) / 0.35 | 짙은 청록, 0.007 | 청록 달 / 0.08 / X 55°·Y −160° | 거의 검은 밤, 네온 |
| HauntedBakery | (0.05, 0.04, 0.07) / 0.5 | 어두운 보라, 0.005 | 약한 달 / 0.12 / X 55°·Y 40° | 어두운 실내, 오븐 불빛 |

포인트 조명은 이름·위치(Unity 좌표)·색·범위·강도·그림자 여부 목록이다. 그림자는 랜드마크 조명만 켜져 있다.

### 1.3 출입구 (원본 빌드 스크립트 실행으로 추출)

| 맵 | 벽 | 개수 | 폭(m) | 높이(m, 상인방 아래) |
|---|---|---|---|---|
| ChocolateFactory | MainHall_Wall | 12 | 8, 14 | 9 |
| | ProductionHall_Wall | 2 | 10 | 8.5 |
| | MachineRoom_Wall | 8 | 7.5 | 7.5 |
| | Warehouse_Wall | 4 | 8 | 8 |
| | FactoryWall(외곽 담장) | 4 | 14 | 12(상인방 없음 — 담장 높이 그대로) |
| CursedCandyCarnival | PuppetTheatre_Wall | 3 | 8, 12 | 8.5 |
| | BumperArena_Wall | 2 | 9.1 | 8 |
| HauntedBakery | Bakery_OuterWall | 9 | 7, 8, 10 | 8 |
| | Storage_Wall / Storage_Divider | 3 | 8 | 8 |
| | Corridor_Wall | 6 | 8 | 8 |
| | Shop_Wall / Shop_Divider | 3 | 8 | 8 |
| **합계** | | **56** | | |

- 각 출입구의 중심 위치·통과 방향(법선)도 함께 뽑혔다. 빌드 스크립트는 **출입구 앞뒤 3~7 m를 소품 배치 금지 구역**으로 비워 두었다(`maplib.py wall()`).
- 문짝 오브젝트는 **`HAU_MagicOven_Door` 하나뿐**이다(오븐 문, 원점 = 경첩).

### 1.4 애니메이션용 오브젝트

| 오브젝트 | 있는 FBX | 비고 |
|---|---|---|
| `CUR_Carousel_Top` | CursedCandyCarnival_**MainStructures** | 회전목마 윗부분. **말씀하신 "Z축"은 Blender 기준이고 Unity에서는 Y축**(세로축)이다(README도 "Unity Y축 회전"). 받침 `CUR_Carousel_Base`는 고정. 빈 오브젝트 `CUR_CandyCarousel_Pivot`도 있음 |
| `CUR_CandyFerrisWheel_Pivot` | **MainStructures** | 관람차 허브 위치의 빈 오브젝트. 그런데 돌아야 할 부품이 **세 FBX에 흩어져 있다**: 림·허브(`_Rim`, `_Hub`)는 MainStructures, 살·곤돌라 16개·안쪽 림(`_Spoke*`, `_Gondola*`, `_InnerRim`)은 **Decoration**, 네온 링(`_NeonRing`)은 **Lighting**. 다리(`_Leg*`)는 고정 |
| `HAU_MagicOven_Door` | HauntedBakery_**MainStructures** | 원점 = 경첩. 지금 반쯤 열림(Blender Z −109° = Unity Y 약 +109°), 0° = 닫힘. 역시 Unity에서는 **Y축** 회전 |

MainStructures에는 충돌체가 자동으로 붙어 있으므로, **움직이는 부품(회전목마 윗부분·관람차 림·오븐 문)에도 정적 MeshCollider가 붙어 있다**(→ P8).

### 1.5 지금 게임 씬·코드

| 항목 | 현재 |
|---|---|
| `GameScene` 루트 | `GameManager`(채팅·스폰·퇴장, UI 캔버스), `GameRuleManagers`(색칠 시작·승패·방장 정책·괴물 합류·강제 도포·이탈 감시), `PaintManagers`(붓 커서), `Main Camera`(Camera_Ctrl, HDR 켬, Forward), `EventSystem`, `Directional Light`, `Ground`, `PlayerSpawnPos`, `MonsterSpawnPos`, `VoidKillZone`(BoxCollider **60 × 2 × 60**) |
| 씬 전환 | 판 시작: `GameStartAuthority.cs:67` `LoadLevelForRoom(SceneNames.Game)`, 괴물 대기 후: `MonsterLobbyWaitController.cs:92` `LoadLevel(SceneNames.Game)`. `SceneNames.Game = "GameScene"` 고정 |
| 씬 판별 | 색칠 씬 여부는 씬 이름이 아니라 `PaintPhaseController` 컴포넌트 존재로 판단 → 복제한 씬도 그대로 동작 |
| 스폰 | 이름으로 찾음(`SceneSpawnPoints`: `PlayerSpawnPos`, `MonsterSpawnPos`), 주변 5 m/4 m 겹침 회피 |
| 문 | `InteractableDoor`(E키, 누른 쪽 반대로 열림, Room Prop `DoorStates`로 방장이 동기화, 문 ID 기본값 = 오브젝트 이름, Animator 파라미터 `IsOpen`·`OpenOutward`, 상태 `Closed`/`Open`/`OpenOut`, 문짝 충돌체 항상 켬, kinematic Rigidbody + Animator Fixed 갱신) — 과자집 빌더가 붙임 |
| 렌더링 | Built-in 파이프라인, 카메라 경로 = 설정 따름(**Forward**). 품질 Ultra 기준 **픽셀 조명 4개**, 그림자 거리 150 m |
| 후처리 | 패키지 **없음**(`com.unity.postprocessing` 미설치) |
| 테스트 | `RuleTests.RoundKeys_MatchDeclaredLifetimes`가 판 단위 Room 키 개수를 10으로 고정 — 이미 11개라 실패 중(research.md 이후 알려진 문제) |

---

## 2. 발견한 문제와 주의점

| # | 심각도 | 내용 | 처리 |
|---|---|---|---|
| P1 | **높음** | 씬을 5개 만들어도 게임은 항상 `GameScene`만 불러온다 | 맵 선택 Room 값 추가(§3.2) |
| P2 | **높음** | 맵에 **문짝이 없다**(출입구 56개). 폭 7~14 m라 한 장짜리 문은 회전 반경(최대 14 m)이 소품 금지 구역(7 m)을 넘는다 | **양쪽으로 여는 두 장 문**(문짝 폭 3.5~7 m ≤ 금지 구역 7 m)으로 만든다(§3.6) |
| P3 | 중간 | 자동 생성 충돌체가 모두 **non-convex MeshCollider**다. 프로젝트 규칙(메모: 소품 MeshCollider는 convex, 넓은 정적 지형만 예외)과 맞지 않는다. 반대로 **걸어서 지나가는 아치**(`GingerArch`, `CandyCane_Arch`, `Carnival_GothicGate`)나 속이 빈 건물을 convex로 바꾸면 통로가 막힌다 | 충돌체 정책표(§3.3)로 오브젝트별로 정한다 — 예외는 결정 D7 |
| P4 | 중간 | 장식 지형(`*_Terrain_Decor`, Background FBX)과 물 표면(Water FBX)에는 충돌체가 없다. 외곽 투명 벽 안쪽에 보이는 땅인데 밟을 수 없는 곳이 있으면 GameLobbyScene §9처럼 **땅으로 꺼진다** | 격자 검사로 찾아 non-convex MeshCollider 추가(§3.4) |
| P5 | 중간 | 로컬 조명이 맵당 33~153개인데 Forward 렌더링은 픽셀 조명 최대 4개 — 나머지는 버텍스 조명으로 뭉개져 가로등·네온 번짐이 약하다 | 맵 씬 카메라를 **Deferred**로(결정 D8) |
| P6 | 중간 | 후처리 패키지가 없어 Bloom을 쓸 수 없다 | **Post Processing Stack v2**(Built-in용) 설치(결정 D9) |
| P7 | 중간 | `VoidKillZone`가 60 × 60 m(원래 테스트 바닥 크기)이다. 350 m 맵에서는 대부분 영역을 덮지 못한다(최후 방어선 `FallGuard` −100 m는 동작) | 맵마다 360 × 360 m로 키우고 지면 아래 10 m에 둔다 |
| P8 | 중간 | 회전하는 부품(회전목마 윗부분, 관람차 림, 오븐 문)에 **정적** MeshCollider가 붙어 있다. 정적 충돌체를 움직이면 물리 비용이 크고 캐릭터와 어긋난다 | 움직이는 부품은 kinematic Rigidbody + Animator(물리 갱신) 또는 충돌체 제거(결정 D11) |
| P9 | 중간 | 오브젝트가 맵당 1,200~2,100개 | 움직이지 않는 것은 Static(배칭·오클루전), 재질 GPU Instancing, 오클루전 컬링 굽기 |
| P10 | 낮음 | `CursedCandyCarnival`·`HauntedBakery`는 JSON 그대로면 **너무 어두워** 쿠키 색·지형을 읽기 어렵다(환경광 0.35 / 방향광 0.08) | 분위기는 유지하되 가독성 기준(§3.5)으로 조정 |
| P11 | 낮음 | 맵별 스폰 지점이 없다 | 맵마다 쿠키·괴물 스폰 지점 배치(§3.8) |
| P12 | 낮음 | `Assets/Maps`가 커밋되지 않았다. 임포트 설정·충돌체를 바꾸면 되돌리기 어렵다 | **작업 전 커밋 권장**(K0) |
| P13 | 낮음 | 판 단위 Room 키 개수 테스트가 이미 실패 중이고, 맵 키를 추가하면 12개가 된다 | 테스트 기대값을 선언 표에서 계산하도록 수정 |
| P14 | 참고 | 조명 JSON의 방향광 각도는 "근사"다 | 씬에서 눈으로 맞춘다 |

---

## 3. 설계

### 3.1 씬 구성

- 경로: `Assets/Scenes/Maps/Game_CandyForest.unity`, `Game_GingerbreadVillage.unity`, `Game_ChocolateFactory.unity`, `Game_CursedCandyCarnival.unity`, `Game_HauntedBakery.unity`.
- 만드는 법: `GameScene.unity`를 **복제**(`AssetDatabase.CopyAsset`) → 게임 규칙·UI·카메라·EventSystem 배선을 그대로 유지하고, `Ground`와 `Directional Light`를 맵 환경으로 교체한다. 코드 참조·인스펙터 연결이 그대로 이어진다.
- 씬 안 정리(루트):

| 루트 | 내용 |
|---|---|
| (기존) `GameManager`, `GameRuleManagers`, `PaintManagers`, `Main Camera`, `EventSystem` | 그대로(카메라만 §3.5 설정) |
| `Map` | 맵 FBX 인스턴스(카테고리별) — 위치·회전 0, 스케일 1 |
| `MapColliders` | §3.4에서 추가하는 지면 충돌체 |
| `MapLighting` | 방향광 1개 + 포인트 조명(JSON에서 생성) + 반사 프로브 |
| `MapDoors` | §3.6 문(두 장 문 프리팹 인스턴스) |
| `PostFX` | 전역 후처리 볼륨 |
| `PlayerSpawnPos`, `MonsterSpawnPos`, `VoidKillZone` | 맵별 위치·크기 |

- 빌드 목록: 5개 씬 추가. 기존 `GameScene`은 결정 D2.

### 3.2 맵 선택 (코드 — 기존 클래스만 수정, 새 클래스 없음)

| 파일 | 변경 |
|---|---|
| `Core/NetKeys.cs` | Room 키 `GameMapScene`(string) 추가, `Scopes`에 **Room·Round**로 선언 → 판이 끝나면 `RoundStateResetter`가 자동으로 지운다 |
| `Core/GameSettingsSO.cs` | `string[] gameMapScenes`(빌드에 넣은 맵 씬 이름 목록) + 고르는 규칙(결정 D1) 함수. 목록이 비면 `SceneNames.Game`으로 대체 |
| `Lobby/GameStartAuthority.cs` | `TryStart`에서 맵을 고르고 `PaintPhaseEndTime`과 **같은 `SetCustomProperties`로** `GameMapScene`을 기록한 뒤 `LoadLevelForRoom(맵)`. 같은 클라이언트의 Props 변경은 보낸 순서대로 도착하므로, 괴물은 시작 신호를 받을 때 맵 이름도 이미 알고 있다 |
| `Monster/MonsterLobbyWaitController.cs` | 대기 후 `LoadLevel(SceneNames.Game)` → Room의 `GameMapScene`(없으면 `SceneNames.Game`) |
| `Editor/Tests/RuleTests.cs` | 판 단위 키 개수 기대값을 `NetKeys.Scopes`에서 세도록 수정(P13) |

`MonsterJoinController`의 씬 동기화 복구(`curScn` == 현재 씬)와 `PaintPhaseController`의 씬 판별은 씬 이름과 무관해 수정이 필요 없다.

### 3.3 임포트·충돌체 정책 (편집기 도구)

맵 5개를 같은 규칙으로 처리하도록 편집기 도구 하나를 둔다: `Assets/Editor/Maps/MapSceneBuilder.cs`
(메뉴 `Tools/TagOfChaos/Maps/Build <Map> Scene`, `Verify <Map> Scene`). 기존 빌더들(`CauldronBuilder`, `WitchCookieHouseBuilder`)과 같은 방식으로, 결과를 스스로 검사한다.
런타임 클래스는 늘리지 않는다. 새 책임(맵 5개 씬 조립)이라 도구 클래스 하나는 필요하다.

| 대상 | 충돌체 | 근거 |
|---|---|---|
| Ground `*_Terrain_Walkable`, Terrain FBX(언덕·경사로·작업대) | **non-convex** MeshCollider(자동 생성 유지), Static | 넓은 정적 걷는 지형 — 규칙상 예외 |
| 바닥재(`Bakery_FloorPlanks` 등 걷는 판) | non-convex | 걷는 지형 |
| `COL_Boundary_*` | non-convex 유지, **MeshRenderer 끔** | 투명 외곽 벽 |
| MainStructures 벽 조각·기둥·상인방 | **convex**(벽 조각은 이미 조각마다 분리된 상자 모양이라 convex여도 모양이 같다) | 규칙 |
| **걸어서 통과하는 구조물**(아치·정문·속이 빈 건물·다리) | convex로 바꾸면 통로가 막힘 → non-convex 유지(D7) 또는 상자 여러 개로 대체 | 도구가 "convex로 바꾸면 내부 통로 광선 검사가 막히는지"로 자동 판별해 목록을 보고 |
| GameplayProps(엄폐물·가구) | **convex** | 규칙(소품) |
| 랜드마크(탱크·시계탑·오븐·나무) | convex(오븐 입구 등 들어가는 곳은 D7) | 규칙 |
| 움직이는 부품 | §3.7 | P8 |
| Decoration·Lighting·Effects·Water·Background | 없음(단 §3.4 검사에서 밟을 수 있어야 하는 곳은 추가) | 시각용 |

- Static 지정: 움직이는 부품·문·효과 기준점 제외 전부(배칭·오클루전·GI).
- 재질: `Maps/Common/Materials`의 GPU Instancing 켬.

### 3.4 지면 뚫림 검사·수정 (GameLobbyScene §9 방식)

1. 350 × 350 m를 2 m 간격 격자로 나눠 각 점에서 위→아래로 두 번 광선을 쏜다.
   ① **보이는 면**: 모든 렌더러에 임시 MeshCollider를 붙인 복사 씬에서 가장 위 표면. ② **밟히는 면**: 실제 충돌체.
2. ①은 있는데 ②가 없거나 ②가 ①보다 0.3 m 이상 아래인 점을 "뚫림"으로 모은다. `COL_Boundary` 바깥은 제외한다.
3. 뚫린 점이 속한 메시(장식 지형·물 바닥·다리 등)에 **non-convex MeshCollider(Static)**를 `MapColliders` 아래에 추가한다. 메시 그대로 사용해 지난번(`LobbyFarGroundCollider`)과 같은 방식이다.
4. 물: 얕은 물이 지면 위에 떠 있는 구조면 물 아래 지면이 밟히는지 확인한다. 초콜릿 강 바닥이 비어 있으면 강 바닥을 추가한다(또는 물 표면을 밟게 할지 D12).
5. 다시 검사해 뚫림 0을 확인한다. 추가로, 쿠키 캡슐을 격자 위에서 떨어뜨려 모두 지면 위에 멈추는지(물리) 표본 검사한다.

### 3.5 분위기 조명·밝기

**공통**
- 카메라: **Deferred**(D8), HDR 유지, MSAA 끔(Deferred는 미지원), 안티에일리어싱은 후처리 FXAA/SMAA.
- 포인트 조명: JSON에서 생성. 그림자는 JSON대로 랜드마크만. 화면 밖·먼 조명은 Deferred에서 비용이 작다.
- 발광 재질(`M_Glow_*`, `M_Neon_*`, `M_Window_*`, `M_Oven_Fire` 등): Emission을 HDR 1.5~4로 올려 Bloom 임계값(1.0)을 넘게 한다.
- 반사 프로브: 맵마다 랜드마크·물가에 몇 개(굽기). 초콜릿·설탕 광택이 주변을 비추게 한다.
- 조명 모드: 실시간(Realtime GI 끔). 굽기(라이트맵)는 결정 D8의 대안.

**맵별 목표 밝기(분위기 유지 + 게임 가독성)** — 가독성 기준: 가장 어두운 길에서도 5 m 앞 쿠키의 몸 색 구분 가능, 20 m 앞 윤곽 식별 가능.

| 맵 | 분위기 | JSON 대비 조정(초기값, 눈으로 미세 조정) | 후처리(§3.6) 성격 |
|---|---|---|---|
| CandyForest | 가장 밝은 낮, 분홍 | 그대로(환경광 1.0, 해 1.0). 분홍 안개 0.008 → 0.006(멀리 랜드마크가 보이게) | Bloom 약하게, 채도 약간 ↑ |
| ChocolateFactory | 따뜻한 실내 낮 | 환경광 0.8 → 0.9, 유리 지붕 햇빛 0.75 유지 | Bloom 중간, 따뜻한 색온도, **초콜릿 강 광택**(아래) |
| GingerbreadVillage | 밤, 가로등·창문 | 환경광 0.6 → 0.8(남색 유지), 달빛 0.16 → 0.25 | Bloom 강하게(창문·가로등), 약한 비네트 |
| HauntedBakery | 어두운 실내, 오븐 불빛 | 환경광 0.5 → 0.7, 달빛 0.12 → 0.2, 오븐 조명 유지 | Bloom 중간~강(오븐·랜턴), 보라 색조, 비네트 |
| CursedCandyCarnival | 가장 어두운 밤, 네온 | 환경광 0.35 → 0.6(거의 검은 청록 유지), 달빛 0.08 → 0.18 | **Bloom 가장 강하게(네온 번짐)**, 비네트, 대비 ↑ |

밝기 순서: CandyForest > ChocolateFactory > GingerbreadVillage ≈ HauntedBakery > CursedCandyCarnival(D10).

**초콜릿 강 윤기**: `M_Chocolate_Liquid`·`M_Water_Chocolate`의 Smoothness 0.9 이상, Metallic 0.1 → 반사 프로브 + 햇빛·주변 조명의 하이라이트가 Bloom 임계값을 넘어 번진다. 필요하면 물결 노멀맵(스크롤)을 추가한다(D13).

### 3.6 후처리 (Post Processing Stack v2)

- 패키지 `com.unity.postprocessing`(Built-in 파이프라인용) 설치.
- 각 맵 씬: `Main Camera`에 `PostProcessLayer`(레이어 `PostProcessing`, AA = SMAA), `PostFX` 오브젝트에 전역 `PostProcessVolume` + 맵별 프로파일(`Assets/Maps/<Map>/<Map>_PostFX.asset`).
- 프로파일 초기값:

| 맵 | Bloom 강도 / 임계 / 산란 | Color Grading | Vignette | 기타 |
|---|---|---|---|---|
| CandyForest | 1.0 / 1.1 / 0.6 | ACES, 채도 +10 | 없음 | |
| ChocolateFactory | 2.0 / 1.0 / 0.65 | ACES, 색온도 +15 | 0.15 | |
| GingerbreadVillage | 3.0 / 1.0 / 0.7 | ACES, 색온도 −5 | 0.25 | |
| HauntedBakery | 2.5 / 1.0 / 0.7 | ACES, 보라 색조 | 0.3 | |
| CursedCandyCarnival | 4.0 / 0.9 / 0.75 | ACES, 대비 +15 | 0.35 | |

- GameLobbyScene·GameScene은 이번 범위에 넣지 않는다(원하면 같은 방식으로 추가 가능).

### 3.7 문 — GameLobbyScene 상호작용 문과 같은 규칙

**문짝 설계**

| 항목 | 설계 | 근거 |
|---|---|---|
| 형태 | **두 장 문**(양쪽 경첩, 가운데서 만남). 문짝 폭 = 출입구 폭 ÷ 2(3.5~7 m), 높이 = 상인방 아래 높이(7.5~9 m) − 5 cm | 한 장 문은 회전 반경이 소품 금지 구역(7 m)을 넘는다 |
| 위치 | 벽 두께 **가운데**, 경첩은 문설주에서 약간 안쪽(문짝 두께가 돌 때 문설주에 닿지 않게) | 과자집 문에서 바깥으로 열 때 문설주를 뚫던 문제(GameLobbyScene §13.2~13.3)의 교훈 |
| 여는 방향 | 누른 캐릭터의 **반대쪽**. 밖에서 누르면 안으로, 안에서 누르면 밖으로(`InteractableDoor` 그대로) | 요청 3 |
| 안·밖 판정 | 건물 외벽: 안쪽 = 건물(방) 쪽. 실내 칸막이: 도구가 방 중심 쪽을 안쪽으로 지정(칸막이는 어느 쪽이든 "누른 반대쪽"으로 열리므로 결과는 같다) | |
| 여는 각도 | 안 90°, 밖 90°(장식 돌출이 없는 판 문). 도구가 양쪽 90°까지 벽·소품과의 겹침을 검사 | 과자집은 장식 때문에 밖 75°였다 |
| 열린 뒤 통과 폭 | 문짝 두께(0.3 m)만큼 줄어 최소 **7.2 m**(기계실 7.5 m) ≥ 괴물 기준 7 m | README 통과 기준 |
| 동기화 | 기존 `InteractableDoor` 그대로: 방장 요청 → Room Prop `DoorStates`. **문 ID는 도구가 고유하게 지정**(`HAU_Bakery_OuterWall_03` 식) — 기본값(오브젝트 이름)은 맵 안에서 겹친다 | |
| 구동 | 두 장을 묶은 부모에 Animator 1개(클립 하나가 두 경첩을 반대 방향으로 돌림) + kinematic Rigidbody(Animator Fixed 갱신) + `InteractableDoor`. 문짝 충돌체는 부모 Rigidbody에 속한 자식 BoxCollider(convex 규칙 충족) | 과자집 문과 같은 구조, **`InteractableDoor` 코드 수정 없음** |
| 상호작용 범위 | 출입구 폭 ÷ 2 + 1 m(기본 2 m로는 14 m 문 가운데에서 닿지 않음) | |
| 대상 출입구 | 결정 D4(추천: 상인방 있는 52개. 초콜릿공장 외곽 담장 입구 4개(14 × 12 m, 상인방 없음)는 마당 입구라 문 없이 열어 둠) | |

**문짝을 어디서 만드나**(결정 D3)
- **추천: Blender** — 맵 빌드 라이브러리에 문짝 생성 함수를 추가해, 출입구 정보로 벽 재질에 맞는 판 문(테두리·손잡이)을 만든다. 맵별 `<Map>_Doors.fbx`로 내보내고(문짝 원점 = 경첩, 오븐 문과 같은 방식), 출입구 목록을 `<Map>_Doors.json`(Unity 좌표)로 함께 내보낸다. 원본 `.blend`는 수정 전 백업한다.
- 대안: Unity 도구가 상자 메시로 만든다(빠르지만 모양이 단순).

**마법 오븐 문**(`HAU_MagicOven_Door`, 결정 D6) — 추천: 상호작용 문(E). 한 방향만 열림(오븐 속으로는 열리지 않음 — `OpenOutward` 파라미터를 두지 않으면 `InteractableDoor`가 항상 한 방향으로만 연다). 닫힘 0°, 열림 약 +109°(지금 모델의 열린 각). 판 초기 상태는 닫힘(Room Prop 기본값).

### 3.8 회전 연출

| 대상 | 방법 | 기본 속도(결정 D11) |
|---|---|---|
| 회전목마 윗부분 `CUR_Carousel_Top` | 편집기 도구가 만드는 반복 클립(Unity Y축 360°) + Animator | 1바퀴 20초 |
| 관람차 | 씬에서 림·허브·살·안쪽 림·네온 링·곤돌라를 `CUR_CandyFerrisWheel_Pivot` 아래로 **다시 묶고**(FBX 인스턴스 해제 필요) Pivot을 바퀴 축으로 회전. 곤돌라 16개는 **반대로 같은 각도만큼 돌려 늘 아래로 매달리게** 한다(같은 클립에 곤돌라 커브 포함). 바퀴 축 방향은 Pivot의 로컬 축을 실측해 정한다 | 1바퀴 60초 |
| 네트워크 | 없음(장식이라 각 클라이언트가 로컬 재생). 판 시작 시각 기준으로 재생 위치를 맞출 필요는 없다고 본다 | |
| 충돌체 | 회전 부품: kinematic Rigidbody + Animator 물리 갱신(움직이는 충돌체로 캐릭터를 밀어냄) 또는 충돌체 제거(관람차 곤돌라는 높아서 닿지 않음) — 결정 D11 | |

### 3.9 스폰·낙하

- 쿠키 `PlayerSpawnPos`·괴물 `MonsterSpawnPos`: 맵마다 걷는 지면 위, 서로 **60 m 이상**, 쿠키 쪽은 4명이 겹치지 않게 반경 5 m가 평평한 곳. 후보를 도구로 찾고(격자 검사 결과 활용) 최종은 눈으로 확인.
- `VoidKillZone`: 360 × 4 × 360 m, 맵 최저 지면보다 10 m 아래.
- 테스트용 오프라인 방: 기존 `OfflineModeBootstrap`을 맵 씬에 두지 않는다(대기실을 거치는 정상 흐름으로 확인).

---

## 4. 변경 목록

| 구분 | 파일 | 내용 |
|---|---|---|
| 코드 | `Core/NetKeys.cs`, `Core/GameSettingsSO.cs`, `Lobby/GameStartAuthority.cs`, `Monster/MonsterLobbyWaitController.cs` | 맵 선택(§3.2) |
| 테스트 | `Editor/Tests/RuleTests.cs` | 키 개수 기대값 계산식으로 |
| 편집기 도구(신규) | `Editor/Maps/MapSceneBuilder.cs` | 씬 조립·충돌체 정책·지면 검사·조명 생성·문 조립·회전 클립·검증 |
| 패키지 | `Packages/manifest.json` | `com.unity.postprocessing` |
| 씬(신규) | `Scenes/Maps/Game_<Map>.unity` × 5 | |
| 에셋(신규) | `Maps/<Map>/<Map>_PostFX.asset` × 5, 회전·문 클립/컨트롤러, 문 프리팹 | |
| 에셋(수정) | `Maps/*/Models/*.fbx.meta`(필요 시), 발광·초콜릿 재질 값 | |
| Blender(D3 추천 시) | `Maps/Source~/Scripts/maplib.py`(문짝 함수), `export_unity.py`(Doors FBX·JSON), `<Map>_Doors.fbx`/`.json` | 원본 백업 후 |
| 설정 | Build Settings(씬 5개), `GameSettings` 에셋(맵 목록), 레이어 `PostProcessing` | |

런타임 새 클래스는 없다. 도구 클래스 1개(맵 조립)만 새로 만든다.

---

## 5. 결정이 필요한 사항

| # | 질문 | 선택지 | 추천 |
|---|---|---|---|
| D1 | 판마다 맵 고르기 | ① 무작위(직전 판 맵 제외) ② 호스트가 대기실에서 선택(UI 추가) ③ 순서대로 | ① |
| D2 | 기존 `GameScene`(60 m 테스트 바닥) | ① 빌드에서 빼고 테스트용으로 보관 ② 삭제 ③ 맵 목록에 포함 | ① |
| D3 | 문짝 제작 | ① Blender에서 맵 재질에 맞는 판 문 생성·내보내기 ② Unity 상자 메시 | ① |
| D4 | 문을 달 출입구 | ① 상인방 있는 52개(초콜릿공장 외곽 담장 입구 4개는 열린 채) ② 56개 모두 | ① |
| D5 | 문 형태 | ① 두 장 문 ② 한 장 문(회전 반경이 커 소품·벽과 겹칠 수 있음) | ① |
| D6 | 마법 오븐 문 | ① 상호작용 문(한 방향, 닫힘 0° ↔ 열림 약 109°) ② 반쯤 열린 채 고정 ③ 저절로 여닫히는 연출 반복 | ① |
| D7 | 충돌체 규칙 예외 | 걸어서 통과하는 구조물(아치·정문·속 빈 건물·다리)만 **non-convex 유지** 허용 / 상자 충돌체 여러 개로 대체(작업량 큼) | non-convex 허용(정적 지형과 같은 성격) |
| D8 | 많은 조명 처리 | ① 맵 씬 카메라 Deferred(실시간, 조정 쉬움) ② 라이트맵 굽기 + 라이트 프로브(가볍지만 굽는 시간·용량 큼, 350 m 맵) | ① |
| D9 | 후처리 | ① Post Processing Stack v2 설치 ② 후처리 없이 발광만 | ① |
| D10 | 맵별 밝기 | §3.5 표의 조정값(밤 맵을 JSON보다 밝게) / JSON 그대로 | 표대로 시작, 플레이 후 조정 |
| D11 | 회전 속도·움직이는 부품 충돌 | 회전목마 20초·관람차 60초 / 충돌: kinematic Rigidbody(밀어냄) 또는 제거 | 속도 그대로, 회전목마 윗부분은 kinematic, 관람차 바퀴는 충돌체 제거(높아서 닿지 않음) |
| D12 | 물 표면 | 밟지 못함(바닥까지 빠짐 — 얕은 물) / 물 표면을 밟게 | 물 아래 지면 확인 후 결정(기본: 바닥을 밟음) |
| D13 | 초콜릿 강 윤기 강화 | 재질·반사 프로브·Bloom만 / 물결 노멀맵 스크롤 추가(텍스처 제작 필요) | 재질·프로브·Bloom만 |

---

### 5.1 결정 (2026-09-27, 사용자: "D11의 회전목마는 40초로 해주고 나머지는 다 추천 방식으로")

| # | 결정 |
|---|---|
| D1 | ✅ 무작위(직전 판 맵 제외) |
| D2 | ✅ 기존 GameScene은 빌드에서 빼고 테스트용으로 보관 |
| D3 | ✅ Blender에서 판 문 생성·내보내기 |
| D4 | ✅ 상인방 있는 52개에 문(초콜릿공장 외곽 담장 입구 4개는 열린 채) |
| D5 | ✅ 두 장 문 |
| D6 | ✅ 마법 오븐 문 = 상호작용 문(한 방향) |
| D7 | ✅ 걸어서 통과하는 구조물만 non-convex 유지 |
| D8 | ✅ 맵 씬 카메라 Deferred |
| D9 | ✅ Post Processing Stack v2 |
| D10 | ✅ §3.5 표대로 시작 |
| D11 | ✅ **회전목마 1바퀴 40초**, 관람차 60초. 회전목마 윗부분 kinematic, 관람차 바퀴는 충돌체 제거 |
| D12 | ✅ 물 아래 지면 확인 후 결정(기본: 바닥을 밟음) |
| D13 | ✅ 재질·반사 프로브·Bloom만 |
| K0 | 커밋은 따로 요청받지 않아 git은 건드리지 않고, 수정할 수 있는 원본을 스크래치 폴더에 백업(`maps_backup_20260927/`: `Assets/Maps`의 `.meta`·`.py`·`.json` 묶음 + `TagOfChaos_Maps.blend`) |

## 6. 작업 순서

| 단계 | 작업 | 확인 |
|---|---|---|
| K0 | **작업 전 커밋**(`Assets/Maps` 포함) 권장. Blender 원본 백업(D3 ①일 때) | git 상태 |
| K1 | 코드: 맵 선택(§3.2) + 테스트 수정 | 컴파일·EditMode 테스트 |
| K2 | 후처리 패키지 설치, 레이어 추가 | 컴파일·콘솔 |
| K3 | 맵 조립 도구 `Assets/Editor/Maps/MapSceneBuilder.cs`(메뉴 `Tools/TagOfChaos/Maps/…`, 다시 실행해도 같은 결과) → `Assets/Scenes/Maps/Game_<Map>.unity` 5개. 충돌체 규칙: 지면·지형·걷는 구조물·통과형 아치/정문(가운데 광선 통과+양끝 막힘)은 non-convex, 나머지는 convex — **실제로 구워 보고 한도(면 256) 초과일 때만** 소품은 상자, 구조물은 non-convex(초콜릿 탱크·돔 5, 놀이공원 천막·관람차 테 17 / 상자 13). convex 수: 캔디숲 372, 진저브레드 373, 초콜릿 561, 놀이공원 337, 빵집 331. 경계벽 렌더러 숨김 | ✅ 완료 |
| K4 | (D3 ①) Blender 문짝 생성·내보내기 → Unity 임포트 | 문짝 원점 = 경첩, 크기 |
| K5 | 두 장 문 50개(초콜릿 26·놀이공원 3·빵집 21) + 공용 `MapDoor.controller`(안쪽 Open·바깥 OpenOut). 수정: 문짝 메시 이름에 맵 접두어(빵집 `.001` 충돌), **문 ID가 같은 방의 벽 조각끼리 겹치던 문제**(방 단위 번호로 수정, 전 맵 고유 확인). 문짝 스윕 검사(양방향 30/60/90°): 벽과 겹침 0, 소품 4개(기계 2·의자·탁자)는 도구가 열리는 범위 밖으로 자동 이동. Play Mode: 짝수 문은 안에서·홀수 문은 밖에서 눌러 **초콜릿 26/26·빵집 21/21·놀이공원 3/3 모두 누른 쪽 반대로 열림**, Room Prop `DoorStates` 26건 기록 | ✅ 완료 |
| K6 | 회전목마 `localEulerAngles.y` 40초/바퀴(실측 9°/s), 관람차 Pivot `z` 60초/바퀴(실측 6°/s, 곤돌라 16개 역회전 → 기울기 0.00°), 오븐 문 0°↔108.9° 상호작용(kinematic). 회전목마 윗판은 껍질 한도 초과라 kinematic non-convex | ✅ 완료 |
| K7 | 도구 검사: 전 맵 구멍 0. Play Mode에서 맵 전역 무작위 150개 구 낙하(연속 충돌) — **5개 맵 모두 뚫림 0**(최저 y −0.2~−1.9 = 연못 바닥) | ✅ 완료 |
| K8 | 조명 보정: 환경광을 목표 휘도로(원본 JSON 야간색 휘도 0.02~0.05는 화면이 검정) — 캔디숲 0.42·초콜릿 0.32·진저브레드 0.22·빵집 0.2·놀이공원 0.17. **점광원 상한 2.5**(Blender 키·랜드마크 조명 10~600을 그대로 쓰면 실시간 반사 프로브까지 하얗게 날아감 — 놀이공원에서 발견·원인 확인). Bloom 0.8~1.2 / 임계 1.2~1.3. 게임 카메라 스크린샷으로 5개 맵 판독성 확인(네온·시계탑·오븐 빛 번짐 유지) | ✅ 완료 |
| K9 | 스폰: 쿠키 남쪽·괴물 북쪽(180~194 m), 캐릭터가 지면 위에 섬. VoidKillZone = 최저 지면 −10. 빌드 목록: GameScene 제외, 맵 5개 추가, `GameSettings.gameMapScenes` 5개 — 목록 누락 0, 200회 선택에서 직전 맵 반복 0 | ✅ 완료 |
| K10 | 5개 맵 각각 Play Mode(오프라인 방): 쿠키 스폰·색칠→생존 단계 진행, 문·회전·낙하 검사, **Console 오류·경고 0**(빌드 때 나오는 convex 한도 메시지는 도구의 판정용 굽기에서만 나옴). EditMode 13/13. 미검증: 두 클라이언트 실제 대전(대기실 → 무작위 맵 이동)은 에디터 하나로는 재현 불가 — 코드 경로(`TryStart`→`LoadLevelForRoom(map)`, 괴물 `CurrentMapScene()`)와 단위 테스트로만 확인 | ✅ 완료(2인 네트워크 검증은 사용자 확인 필요) |
| K11 | 에디터 Game 뷰, 스폰 시점(로드 직후 제외): 캔디숲 152 fps·3.28M tris · 진저브레드 144 fps·3.80M·593 batches · 초콜릿 148 fps·4.10M·596 · 놀이공원 123 fps·2.39M·434 · 빵집 153 fps·3.22M·601. 그림자 캐스터 1.3~2.6천 — 저사양 목표가 생기면 그림자 거리(150)·장식 LOD가 첫 후보 | ✅ 완료 |

맵 순서는 문이 없는 CandyForest부터(씬·조명·후처리·지면 파이프라인 확인) → GingerbreadVillage → CursedCandyCarnival(회전 연출·문 5개) → HauntedBakery(오븐 문·문 21개) → ChocolateFactory(문 30개).

---

## 7. 검증 계획

| # | 항목 | 방법 | 통과 기준 |
|---|---|---|---|
| V1 | 컴파일·콘솔 | 단계마다 | 에러·경고 0 |
| V2 | 맵 선택 | 오프라인 대기실에서 시작 | 고른 맵 씬으로 이동, 괴물도 대기 후 같은 맵으로, 판 종료 후 대기실 복귀 시 `GameMapScene` 삭제 |
| V3 | 게임 흐름 | 맵마다 쿠키·괴물 스폰, 색칠, 합류, 처형(분쇄 연출), 결과 | 기존 GameScene과 같게 동작 |
| V4 | 지면 | 격자 검사 + 표본 낙하 + 경계 끝까지 걷기 | 뚫림 0, 외곽 투명 벽에 막힘 |
| V5 | 문 | 문마다 밖/안에서 E | 누른 반대쪽으로 열림, 닫힌·도는·열린 문짝 모두 관통 불가, 열린 뒤 괴물이 통과, 다른 클라이언트에 동기화 |
| V6 | 오븐 문·회전 연출 | 눈으로 + 각도 샘플 | 오븐 문 한 방향 여닫힘, 회전목마·관람차 회전, 곤돌라 늘 아래로 |
| V7 | 밝기·가독성 | 가장 어두운 길에서 5 m/20 m 앞 쿠키 | 색 구분 / 윤곽 식별 |
| V8 | 광택·번짐 | 초콜릿 강·네온 스크린샷 | Bloom 번짐 보임, 강 표면 하이라이트 |
| V9 | 성능 | 에디터 통계·프로파일러 | 맵별 기록(목표치는 첫 측정 후 합의) |
| V10 | 회귀 | EditMode 테스트, GameLobbyScene 문 | 전부 통과, 대기실 문 그대로 |

---

## 8. 위험·영향

- 오브젝트·조명이 많아 에디터 작업(씬 저장·임포트)이 느릴 수 있다. 맵 하나씩 진행한다.
- Deferred로 바꾸면 반투명 재질(유리 지붕 `M_Glass_*`, 안개 효과)은 Forward로 따로 그려진다 — 보이는 차이를 K8에서 확인한다.
- Blender 원본 수정(D3 ①)은 백업·재생성 스크립트로 되돌릴 수 있지만, `Assets/Maps`가 커밋되지 않은 상태라 **K0 커밋을 강하게 권장**한다.
- 문 52개가 모두 닫힌 상태로 판이 시작되므로 괴물 동선이 바뀐다(괴물도 E로 연다). 플레이 후 일부 문을 기본 열림으로 둘지 판단할 수 있다.
- 맵 선택 코드는 방 전체 흐름(시작·괴물 대기·복귀)에 닿는다 — V2·V3로 전체 흐름을 확인한다.

---

## 9. 진행 현황

| 단계 | 내용 | 상태 |
|---|---|---|
| K0 | 원본 백업(스크래치 `maps_backup_20260927/`: `.meta`·`.py`·`.json`·`.md` 234개 zip + `TagOfChaos_Maps.blend`). git은 건드리지 않음 | ✅ 완료 |
| K1 | 맵 선택: `NetKeys.GameMapScene`(Room·Round), `GameSettingsSO.gameMapScenes` + `PickGameMap`(무작위, 직전 맵 제외, 비면 GameScene), `GameStartAuthority.TryStart`가 `PaintPhaseEndTime`과 같은 요청으로 기록·그 씬 로드, `CurrentMapScene()`, `MonsterLobbyWaitController`가 그 씬으로. 테스트: 키 개수를 선언 표에서 계산 + 맵 선택 테스트 추가 → **EditMode 13/13 통과**(전부터 실패하던 1건 해결) | ✅ 완료 |
| K2 | `com.unity.postprocessing` **3.5.4** 설치, 레이어 12 `PostProcessing` 추가. 컴파일·콘솔 오류 0 | ✅ 완료 |
| K3 | 맵 조립 도구 `Assets/Editor/Maps/MapSceneBuilder.cs`(메뉴 `Tools/TagOfChaos/Maps/…`, 다시 실행해도 같은 결과) → `Assets/Scenes/Maps/Game_<Map>.unity` 5개. 충돌체 규칙: 지면·지형·걷는 구조물·통과형 아치/정문(가운데 광선 통과+양끝 막힘)은 non-convex, 나머지는 convex — **실제로 구워 보고 한도(면 256) 초과일 때만** 소품은 상자, 구조물은 non-convex(초콜릿 탱크·돔 5, 놀이공원 천막·관람차 테 17 / 상자 13). convex 수: 캔디숲 372, 진저브레드 373, 초콜릿 561, 놀이공원 337, 빵집 331. 경계벽 렌더러 숨김 | ✅ 완료 |
| K4 | 새 스크립트 `Source~/Scripts/export_doors.py`(원본 빌드 스크립트·`.blend`는 수정 안 함 — 임시 사본에서 실행, `wall()`을 감싸 벽 두께·상인방·방 중심 기록) → `<Map>/Models/<Map>_Doors.fbx`(크기별 문짝 메시: 초콜릿공장 10, 놀이공원 4, 빵집 6 — 나무 판 + 철띠 3줄 + 양면 손잡이, 원점 = 경첩) + `<Map>_Doors.json`(문 ID·중심·벽 방향·안쪽 방향·크기). **문은 50개**: 초콜릿공장 26 + 인형극장 3 + 빵집 21. 범퍼카장 입구 2개는 벽이 아니라 **높이 1.2 m 울타리의 틈**(지붕은 기둥 위 8 m)이라 문을 달지 않음, 공장 외곽 담장 입구 4개는 D4대로 열어 둠 | ✅ 완료 |
| K5 | 두 장 문 50개(초콜릿 26·놀이공원 3·빵집 21) + 공용 `MapDoor.controller`(안쪽 Open·바깥 OpenOut). 수정: 문짝 메시 이름에 맵 접두어(빵집 `.001` 충돌), **문 ID가 같은 방의 벽 조각끼리 겹치던 문제**(방 단위 번호로 수정, 전 맵 고유 확인). 문짝 스윕 검사(양방향 30/60/90°): 벽과 겹침 0, 소품 4개(기계 2·의자·탁자)는 도구가 열리는 범위 밖으로 자동 이동. Play Mode: 짝수 문은 안에서·홀수 문은 밖에서 눌러 **초콜릿 26/26·빵집 21/21·놀이공원 3/3 모두 누른 쪽 반대로 열림**, Room Prop `DoorStates` 26건 기록 | ✅ 완료 |
| K6 | 회전목마 `localEulerAngles.y` 40초/바퀴(실측 9°/s), 관람차 Pivot `z` 60초/바퀴(실측 6°/s, 곤돌라 16개 역회전 → 기울기 0.00°), 오븐 문 0°↔108.9° 상호작용(kinematic). 회전목마 윗판은 껍질 한도 초과라 kinematic non-convex | ✅ 완료 |
| K7 | 도구 검사: 전 맵 구멍 0. Play Mode에서 맵 전역 무작위 150개 구 낙하(연속 충돌) — **5개 맵 모두 뚫림 0**(최저 y −0.2~−1.9 = 연못 바닥) | ✅ 완료 |
| K8 | 조명 보정: 환경광을 목표 휘도로(원본 JSON 야간색 휘도 0.02~0.05는 화면이 검정) — 캔디숲 0.42·초콜릿 0.32·진저브레드 0.22·빵집 0.2·놀이공원 0.17. **점광원 상한 2.5**(Blender 키·랜드마크 조명 10~600을 그대로 쓰면 실시간 반사 프로브까지 하얗게 날아감 — 놀이공원에서 발견·원인 확인). Bloom 0.8~1.2 / 임계 1.2~1.3. 게임 카메라 스크린샷으로 5개 맵 판독성 확인(네온·시계탑·오븐 빛 번짐 유지) | ✅ 완료 |
| K9 | 스폰: 쿠키 남쪽·괴물 북쪽(180~194 m), 캐릭터가 지면 위에 섬. VoidKillZone = 최저 지면 −10. 빌드 목록: GameScene 제외, 맵 5개 추가, `GameSettings.gameMapScenes` 5개 — 목록 누락 0, 200회 선택에서 직전 맵 반복 0 | ✅ 완료 |
| K10 | 5개 맵 각각 Play Mode(오프라인 방): 쿠키 스폰·색칠→생존 단계 진행, 문·회전·낙하 검사, **Console 오류·경고 0**(빌드 때 나오는 convex 한도 메시지는 도구의 판정용 굽기에서만 나옴). EditMode 13/13. 미검증: 두 클라이언트 실제 대전(대기실 → 무작위 맵 이동)은 에디터 하나로는 재현 불가 — 코드 경로(`TryStart`→`LoadLevelForRoom(map)`, 괴물 `CurrentMapScene()`)와 단위 테스트로만 확인 | ✅ 완료(2인 네트워크 검증은 사용자 확인 필요) |
| K11 | 에디터 Game 뷰, 스폰 시점(로드 직후 제외): 캔디숲 152 fps·3.28M tris · 진저브레드 144 fps·3.80M·593 batches · 초콜릿 148 fps·4.10M·596 · 놀이공원 123 fps·2.39M·434 · 빵집 153 fps·3.22M·601. 그림자 캐스터 1.3~2.6천 — 저사양 목표가 생기면 그림자 거리(150)·장식 LOD가 첫 후보 | ✅ 완료 |
