# 탈출 모드 2차: 디자인·연출 개편 + 문제 4건 (EscapeVisualPlan)

- 상태 표시: ⬜ 대기 · 🔄 진행 중 · ✅ 완료
- 기준 코드: 현재 프로젝트(F:\3DClaudeTagOfChaos\TagOfChaos). EscapePlan.md(1차)는 모두 ✅.
- 규칙: 계획 승인 후 작업, 코드에 한글 금지(주석 제외), Blender 원본은 `Assets/Maps/Source~/Scripts/`.

---

## 0. 작업 순서

| 단계 | 내용 | 상태 |
|---|---|---|
| V0 | 결정 사항 확정(§8) | ✅ |
| V1 | 문제 2: 방 목록(지역 선택: 로비 보기 지역 + 방 만들기 지역) | ✅ |
| V2 | 문제 4: ESC 메뉴(모든 역할, 나가기 · 설정 자리) | ✅ |
| V3 | 문제 3: 색칠 중 상자·아이템 금지 | ✅ |
| V4 | 문제 1: 처형 때 1인칭 시점이 쿠키를 보고 돌아오기 | ✅ |
| V5 | 공통: 연출 시계(완성 시각·출발 시각), 타는 곳(Board), 모여서 한 번에 출발 | ✅ |
| V6 | Blender: 부품 19종·상자(속 빈)·유리병·쿠키 인형 다시 디자인 | ✅ |
| V7 | Blender + Unity: 스파이 로켓 5종(칸마다 빈/찬 모양) + 점화·상승 연출 | ✅ |
| V8 | 캔디숲: 중앙 랜드마크 제거 → 큰 케이크, 부서짐 → 로켓 발사 | ✅ |
| V9 | 놀이공원: 롤러코스터(시동 → 출발) | ✅ |
| V10 | 베이커리: 마법 오븐 = 탈출 기계(작동·문 서서히 열림·경사 발판) | ✅ |
| V11 | 공장: 초콜릿 기차(시동 → 출발) | ⬜ |
| V12 | 진저브레드: 시계탑 안 계단 → 지하 쿠키 유적 → 룬 제단 → 포탈 | ⬜ |
| V13 | 결과 화면 3D 유리병, 전체 테스트·Play Mode 검증·문서 | ⬜ |

V1~V4(문제 해결)를 먼저 끝내고, V5 공통 구조 위에 맵을 하나씩 올린다. 맵 하나가 끝날 때마다 통행 검사와 Play Mode를 확인한다.

---

## 1. 문제 해결 (V1~V4)

### 1.1 문제 2: 방 목록이 사람마다 다르게 보임 (V1)

**원인(확인함):** PUN2 설정 `PhotonServerSettings`의 `FixedRegion`과 `DevRegion`이 모두 비어 있다.
- 그러면 접속할 때마다 각 PC가 **핑이 가장 좋은 지역(kr, jp, asia …)** 을 따로 고른다.
- Photon은 지역마다 서버가 따로라서, A가 kr에 방을 만들면 jp에 붙은 B·D는 그 방을 볼 수 없다. 새로고침(로비 재입장)은 같은 지역에 다시 들어갈 뿐이라 해결되지 않는다.
- 로비 코드(`LobbyController`의 목록 캐시·삭제 처리·새로고침)는 PUN2 권장 방식대로 되어 있어 문제없다.

**해결 (결정 Q5: 방장이 방을 만들 때 지역을 고른다):**

- **Photon의 제약:** 방은 한 지역 서버 안에만 있다. 다른 사람이 그 방을 보려면 **같은 지역에 접속해 있어야** 한다. 모든 지역의 방을 한 목록에 모아 보려면 지역마다 차례로 다시 접속해야 해서 느리고 불안정하므로 쓰지 않는다.
- 그래서 지역 선택을 두 곳에 둔다:
  1. **로비 위쪽 "지역" 선택(보기용):** 고르면 그 지역으로 다시 접속하고, 그 지역의 방 목록이 보인다. 기본값은 `한국(kr)`, 마지막 선택을 기억한다(PlayerPrefs).
  2. **방 만들기 설정에 "지역" 줄 추가**(인원·제한시간·타임어택처럼 ◀▶로 고름): 지금 접속한 지역과 다르면 그 지역으로 다시 접속한 뒤 방을 만든다. 방 이름 옆에 지역을 표시한다.
- 지역 목록(표시는 씬/SO의 한글 이름, 코드는 코드값): 한국 `kr`, 일본 `jp`, 아시아 `asia`, 미국 동부 `us`, 유럽 `eu`.
- 접속 설정: `FixedRegion`을 비우고 접속하지 않는다. 처음부터 `ConnectToRegion(선택한 지역)`으로 접속해 PC마다 지역이 갈리는 일이 없게 한다.

```csharp
// LobbyController — 지역 선택(보기·만들기 공통)
private void ConnectTo(string region)
{
    targetRegion = region;
    if (PhotonNetwork.IsConnected && PhotonNetwork.CloudRegion != null && PhotonNetwork.CloudRegion.StartsWith(region))
    { if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby(); return; }
    reconnectAfterDisconnect = PhotonNetwork.IsConnected;
    if (reconnectAfterDisconnect) PhotonNetwork.Disconnect();       // OnDisconnected에서 다시 접속
    else PhotonNetwork.ConnectToRegion(region);
}

public override void OnDisconnected(DisconnectCause cause)
{
    if (!reconnectAfterDisconnect) return;
    reconnectAfterDisconnect = false;
    PhotonNetwork.ConnectToRegion(targetRegion);
}

public override void OnConnectedToMaster()
{
    regionLabel.text = RegionNames.Display(PhotonNetwork.CloudRegion); // 지금 보고 있는 지역
    if (pendingCreate != null) { CreateRoomNow(pendingCreate); pendingCreate = null; return; }
    PhotonNetwork.JoinLobby();
}
```

### 1.2 문제 4: 괴물은 뒤로가기 버튼을 누를 수 없음 (V2, 논의)

**원인(확인함):** 괴물 1인칭 카메라(`MonsterFirstPersonCamera.LateUpdate`)가 결과 화면이 아니면 **매 프레임 커서를 잠근다**(`CursorLockMode.Locked`). 잠긴 커서는 UI를 누를 수 없다. 쿠키 카메라(`Camera_Ctrl`)는 우클릭 중에만 잠그므로 쿠키는 누를 수 있다. 괴물도 V키로 3인칭으로 바꾸면 눌린다.

**해결 (결정 Q4: ESC 방식):**
- 쿠키·스파이·괴물 모두 **ESC를 누르면 ESC 메뉴**가 열린다. 열려 있는 동안 커서가 풀리고, 이동·시점·상호작용 입력은 막힌다(게임은 계속 진행).
- 지금 메뉴에는 **"나가기"** 버튼(누르면 지금의 확인창 → 로비)을 둔다. 나중에 **"설정"** 버튼을 넣을 자리를 비워 두고, 버튼 목록을 쉽게 늘릴 수 있게 만든다.
- ESC를 다시 누르거나 "돌아가기"를 누르면 닫히고, 괴물 1인칭이면 커서가 다시 잠긴다.
- 화면 왼쪽 위 뒤로가기 버튼은 그대로 두되 누르면 같은 ESC 메뉴를 연다(동작 하나로 통일).

```csharp
// EscMenu (UI 프리팹 Resources/UI/Scene/EscMenu, 문구는 프리팹에 한글로)
public static bool IsOpen { get; private set; }
private void Update()
{
    if (Input.GetKeyDown(KeyCode.Escape)) SetOpen(!IsOpen);
}
private void SetOpen(bool open)
{
    IsOpen = open;
    root.SetActive(open);
    PlayerInput.IsMenuOpen = open; // 이동·시점·상호작용 입력 차단(채팅 차단과 같은 방식)
}

// MonsterFirstPersonCamera.LateUpdate
bool freeCursor = IsResultShown() || EscMenu.IsOpen;
if (freeCursor) { if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None; }
else { /* 기존: 잠그고 마우스로 시점 회전 */ }
```

### 1.3 문제 3: 색칠(변장) 시간에는 상자를 열 수 없고 아이템을 쓸 수 없어야 함 (V3)

- 막는 것: 상자 열기, 줍기, 도구 사용, 장치 설치·훔치기, 로켓, 탈출(= 탈출 모드 요청 전부).
- 두 곳에서 막는다:
  1. **화면(클라이언트):** 상호작용 아이콘이 뜨지 않게 `CanInteract`에서 거절하고, `ToolUser`는 쓰지 않는다. 가방 칸은 어둡게 보여 준다.
  2. **방장(서버 역할):** 늦게 온 요청이나 조작된 요청도 거절한다.

```csharp
// EscapeAuthority.Handle 맨 앞
if (GamePhaseState.Current == GamePhase.Paint) return; // 변장 시간에는 탈출 모드 요청을 받지 않는다

// MaterialChest / GroundItemView / EscapeDevice / SpyRocket CanInteract 공통 조건
public static bool EscapeActionsAllowed => GamePhaseState.Current != GamePhase.Paint;
```

### 1.4 문제 1: 괴물이 E로 쿠키를 잡아 부술 때 1인칭 시점 (V4)

- 지금: 1인칭 시점은 처형 중에도 마우스 방향 그대로라, 괴물 손(`GrabSocket`)에 들린 쿠키가 화면 밖에서 부서진다.
- 바꿀 동작:
  1. 처형이 시작되면(`MonsterController.IsGrabKilling`) 시점이 0.3초 동안 **잡힌 쿠키 쪽으로 부드럽게 돌아간다**(마우스 입력 무시).
  2. 쿠키가 들어 올려져 부서지는 동안 계속 쿠키를 따라본다.
  3. 끝나면 0.4초 동안 **원래 보던 방향으로 돌아온다**.

```csharp
// MonsterFirstPersonCamera.LateUpdate
if (monster != null && monster.IsGrabKilling && monster.GrabSocket != null)
{
    if (!lockedOnKill) { lockedOnKill = true; savedYaw = yaw; savedPitch = pitch; blend = 0f; }
    Vector3 dir = monster.GrabSocket.position - eye.position;
    Quaternion look = Quaternion.LookRotation(dir);
    blend = Mathf.MoveTowards(blend, 1f, Time.deltaTime / LockOnSeconds);
    transform.SetPositionAndRotation(eye.position, Quaternion.Slerp(Quaternion.Euler(savedPitch, savedYaw, 0f), look, Smooth(blend)));
    return;
}
if (lockedOnKill) { /* 끝: savedYaw/savedPitch로 ReturnSeconds 동안 되돌린 뒤 lockedOnKill = false */ }
```

- 몸 방향은 `YawRotation`(저장한 yaw)을 그대로 쓰므로 처형 중 몸이 돌아가지 않는다.

---

## 2. 공통 아트 스타일 (모든 새 에셋에 적용)

- 분위기: 귀엽고 아기자기한 3D 카툰 다크 판타지, 쿠키와 괴물의 마법 숲.
- 형태: 둥글고 단순한 실루엣, 과장된 비율. **멀리서도 무엇인지 읽히게** 작은 장식은 넣지 않는다(부품 하나는 손바닥보다 큰 덩어리 2~4개로).
- 재질: 부드러운 저광택(거칠기 0.7 내외). 마법·작동 표시만 약간 광택 + 발광.
- 색 팔레트(Blender `PAL`에 이름으로 추가, Unity에서 `.mat` 색만 바꿔 조정 가능):

| 이름 | 용도 | 색 |
|---|---|---|
| `ME_Purple_Deep` | 몸체·그늘 | 짙은 보라 |
| `ME_Teal` | 강조·창·작동 표시 | 청록 |
| `ME_Gray_Dark` | 금속·기계 | 어두운 회색 |
| `ME_Orange_Warm` | 불꽃·조명·경고 | 따뜻한 주황 |
| `ME_Cookie_Gold` | 쿠키·과자 부분 | 황금빛 갈색 |
| `ME_Glow_*` | 빛나는 부분(맵 색 1개씩) | 발광 |

- 텍스처는 쓰지 않는다(단색 머티리얼 슬롯). 한 모델은 머티리얼 4~6개 이하.

---

## 3. 에셋 디자인 (Blender `escape_assets.py` v2)

### 3.1 부품(상자 안 아이템, 19종) — V6

부품은 **상자에서 꺼낸 모습 = 바닥에 놓인 모습 = 손에 든 모습**이 같다. 로켓·장치에 끼웠을 때는 칸에 맞는 모양으로 바뀐다(§3.4).

| 부품 | 디자인 |
|---|---|
| 레드 버튼 | 둥근 쿠키색 받침 위에 크게 부푼 빨간 버튼, 노란 테두리 |
| 안전벨트 | 두툼한 보라 띠가 둥글게 말림 + 큰 금색 버클 |
| 배터리 | 통통한 청록 원통, 주황 번개 띠, 위에 금속 단자 |
| 공구상자 | 둥근 모서리 주황 상자 + 손잡이, 렌치 하나가 뚜껑 위로 삐죽 |
| 기어 | 굵은 톱니 6개짜리 어두운 회색 기어, 가운데 청록 보석 |
| 마카롱 바퀴 | 분홍 마카롱 + 크림 + 금속 축 |
| 쿠키 바퀴 | 초코칩이 큼직한 쿠키 바퀴 + 축 |
| 초콜릿 원유 | 배불뚝 유리병 안에 초콜릿 액체, 코르크 마개 |
| 룬 4색 | 둥근 쿠키 돌판, 가운데 큼직한 문양이 색으로 빛남 |
| 알사탕 전지 4색 | 포장된 알사탕 모양, 양 끝 금속 단자, 속이 은은히 빛남 |
| 뿅망치 / 스턴건 / 물풍선 | 한 손 도구. 뿅망치는 주름 머리, 스턴건은 짧은 청록 전극, 물풍선은 매듭 있는 둥근 풍선 |

### 3.2 상자 — V6
- **속이 빈 상자**: 바닥 + 벽 4면(두께 6 cm)으로 만들어 뚜껑을 열면 안이 비어 보인다(지금은 속이 꽉 찬 덩어리).
- 둥근 쿠키색 나무 상자, 짙은 보라 금속 띠, 금색 자물쇠. 뚜껑은 반원통(경첩 기준 회전, 지금 코드 그대로 사용).
- 스파이 상자도 겉모양은 같다(규칙 D7 유지).

### 3.3 유리병(괴물 승리 결과) — V13
- 3D 모델: 둥근 유리병 + 쿠키색 뚜껑 + 보라 리본 띠. 잡은 수만큼 작은 쿠키(최대 12개)가 쌓인다.
- 결과 화면: 화면 밖에 둔 전용 카메라가 유리병을 비춰 `RenderTexture` → `RawImage`로 보여 준다(지금의 2D 유리병을 바꾼다). 병이 천천히 돈다.

### 3.4 스파이 로켓 5종 (칸마다 빈 모양 / 찬 모양) — V7
- 칸 구조(이름 규칙): `Slot_00_Empty`, `Slot_00_Filled`, `Slot_01_Empty`, `Slot_01_Filled`.
  - 끼우면 `Empty`를 끄고 `Filled`를 켠다(지금처럼 아이템을 몸에 붙이지 않는다).
  - 공구상자(아무 칸이나)로 채워도 같은 `Filled` 모양(수리된 모습)이 된다.
- 바닥 `Flame`(점화 불꽃, 처음엔 꺼짐), 탑승 해치 `Hatch`.

| 맵 | 로켓 모양 | 칸 1 | 칸 2 |
|---|---|---|---|
| 놀이공원 | 회전 놀이기구 로켓(줄무늬 + 네온 지느러미, 앞에 열린 좌석) | 안전벨트: 좌석에 고리만 → 벨트가 좌석을 가로질러 채워짐 | 배터리: 옆 해치가 열린 빈 홈(빨간 경고등) → 배터리 들어가고 해치 닫힘 + 초록 불 |
| 베이커리 | 밀대·오븐 철판 로켓(쿠키색 몸 + 오븐 문 창) | 기어: 옆 구동부의 빈 축 → 기어 끼워지고 돈다 | 배터리: 아래 배터리 홈 |
| 공장 | 초콜릿 바 로켓(조각 무늬 몸 + 금박 날개) | 초콜릿 원유: 투명 연료통이 비어 있음 → 초콜릿이 차오름 | 기어: 구동부 |
| 진저브레드 | 쿠키 돌 로켓(아이싱 줄 + 돌 날개) | 룬: 왼쪽 룬 홈(어두움) → 룬이 끼워져 빛남 | 룬: 오른쪽 룬 홈 |
| 캔디숲 | 사탕 막대 로켓(분홍·흰 소용돌이 + 사탕 지느러미) | 알사탕 전지: 투명 캡슐 소켓 → 전지가 들어가 빛남 | 알사탕 전지: 두 번째 캡슐 |

- **점화·상승 연출(규칙 그대로: 남은 스파이가 모두 타면 출발):**
  1. 스파이가 E로 타면 해치가 열렸다 닫히고 스파이가 사라진다.
  2. 출발 시각(`SpyEscapedAt`)부터 2초: 바닥 불꽃이 서서히 커지고(크기 0 → 1) 로켓이 떨린다, 연기 퍼프.
  3. 다음 4초: 천천히 떠올랐다가 점점 빨라지며(가속) 60 m 위로 올라가 사라진다.
  4. 모든 화면이 같은 시각(`PhotonNetwork.Time`) 기준으로 같은 자세가 된다. 늦게 들어온 사람도 맞는 위치.
  5. 떠오르는 동안 로켓 충돌체는 끈다.

```csharp
// SpyRocketLaunch (새 컴포넌트) — 모든 클라이언트
float t = (float)(PhotonNetwork.Time - escapedAt);
flame.localScale = Vector3.one * Mathf.Clamp01(t / IgniteSeconds);
float rise = Mathf.Max(0f, t - IgniteSeconds);
transform.position = basePosition + Vector3.up * (0.5f * Accel * rise * rise) + Shake(t);
```

### 3.5 탈출 장치 디자인 — V8~V12 (§5 맵별)

---

## 4. 공통 연출 구조 (V5)

### 4.1 시각 기준 연출
- 방장이 장치가 완성되는 순간 `DeviceCompletedAt`(Room, Round 수명)을 기록한다.
- 출발 시각은 `EscapeState.DepartedAt` 하나로 남긴다(모여서 한 번에 출발, §4.3).
- 모든 연출(시동, 문 열림, 케이크 부서짐, 출발)은 이 시각과 `PhotonNetwork.Time`의 차이로 계산한다 → 모든 화면이 같고, 늦게 들어와도 맞는다. 네트워크 메시지를 따로 보내지 않는다.

```csharp
// NetKeys (Round 수명 → 판이 끝나면 RoundStateResetter가 자동 삭제)
public const string DeviceCompletedAt = "dca";
```

### 4.2 탈출 지점 일반화
- 지금은 장치 중심 근처에서 E를 누르면 바로 탈출한다. 맵마다 **타는 곳**이 생기고, E는 "탑승(대기)"이 된다: 로켓 해치, 롤러코스터 좌석, 기차 문, 오븐 문 안, 포탈.
- 모델에 `ESC_<Map>_Board`(빈 오브젝트)를 두고, 탈출 요청은 그 위치 근처에서만 승인한다. 부품 설치는 지금처럼 장치 칸 근처.
- 장치가 완성되고 **연출이 탈 수 있는 단계에 도착한 뒤**(예: 오븐 문이 다 열린 뒤)부터 탈출을 받는다.

### 4.3 모여서 한 번에 출발 (결정 Q1)
- **규칙:** 장치가 완성되면 쿠키는 탈것(로켓·롤러코스터·기차·오븐·포탈)에 **타서 기다린다.** 살아 있는 쿠키(스파이 제외) **전원이 타면 그때 한 번에 출발**하고, 탄 쿠키 모두 탈출 성공이 된다.
- 기다리는 도중 밖에 남은 쿠키가 괴물에게 잡히거나 방을 나가면 그 쿠키는 빼고 다시 센다 → 남은 사람이 모두 타 있으면 바로 출발.
- 스파이는 쿠키 탈것에 탈 수 없다(지금처럼 "들어갈 수 없음"). 출발 인원 계산에서도 빠진다.
- 탄 쿠키는 몸이 숨겨지고 괴물이 잡을 수 없다. 화면은 탈것 안 시점(좌석에서 밖을 봄)이고, 좌우 키로 다른 쿠키 관전으로 바꿀 수 있다.
- HUD 위쪽에 "탑승 2 / 4"처럼 모인 인원을 보여 준다(모두에게).

**기본값으로 정한 것 (다르게 원하시면 말씀해 주세요):**

| 경우 | 기본값 |
|---|---|
| 탄 뒤 다시 내리기 | 안 됨 |
| 제한시간이 끝났는데 아직 다 안 모임 | 탄 쿠키도 출발 못 함 → 모두 탈출 실패(D8 그대로) |
| 타임어택 중 마녀가 내려침 | 탄 채 기다리던 쿠키도 사망(아직 떠나지 않았으므로) |
| 출발 연출 중(약 5초) 시간이 끝남 | 출발이 시작됐으면 탈출 성공 |

```csharp
// EscapeAuthority — 탑승(Board)과 출발 판정(방장)
private bool Board(int actor)
{
    if (RoomState.IsSpy(actor) || !IsActiveEscaper(actor) || !State.DeviceComplete || !BoardingOpen()) return false;
    if (!IsNear(actor, manager.BoardPosition, InteractReach)) return false;
    DropAll(actor, manager.BoardPosition);
    State.Waiting.Add(actor);
    TryDepart();
    return true;
}

public void TryDepart() // 탑승, 잡힘, 나감 때마다 부른다
{
    if (State.Waiting.Count == 0 || State.DepartedAt > 0) return;
    foreach (Player p in PhotonNetwork.PlayerList)
        if (!RoomState.IsSpy(p.ActorNumber) && IsActiveEscaper(p.ActorNumber) && !State.Waiting.Contains(p.ActorNumber)) return;
    State.DepartedAt = PhotonNetwork.Time;          // 모든 화면이 이 시각부터 출발 연출
    foreach (int a in State.Waiting) State.Escaped.Add(a);
}
```

## 5. 맵별 작업

### 5.1 캔디숲: 큰 케이크 (V8)
- **중앙 랜드마크(거대 막대사탕 나무) 제거**, 그 자리에 큰 케이크를 놓는다(맵 다시 빌드: `build_candyforest.py`).
- 케이크: 지름 약 16 m, 3단, 높이 약 12 m. 받침 둘레에 **알사탕 전지 홈 5개**(필요 수만큼 빛이 들어옴). 쿠키·괴물 모두 둘레를 돌 수 있게 받침은 경사.
- 완성 연출(완성 시각 기준):
  1. 0~2초: 금이 간 틈에서 빛, 케이크가 떨린다.
  2. 2~3.5초: 케이크가 조각 12개로 갈라져 바깥으로 튀며 작아져 사라진다(조각은 충돌체 없음).
  3. 3.5~6초: 가운데에서 사탕 로켓이 솟아오르고 앞 해치에 탑승 발판이 내려온다 → 탈 수 있음.
  4. 쿠키가 해치로 타서 기다림 → 전원이 모이면 §3.4와 같은 점화·상승으로 출발(§4.3).
- 모델: `ESC_CandyForest` = `Cake_Intact`, `Cake_Shard_01~12`, `Cake_Rocket`(+`Flame`, `Hatch`, `Board`).

### 5.2 놀이공원: 롤러코스터 (V9)
- 모델: 승강장(경사 계단) + 3칸짜리 롤러코스터 차 + 레일. 레일은 승강장에서 시작해 오르막(체인 리프트) → 맵 바깥으로 나간다.
- 칸: 레드 버튼(조종 패널), 안전벨트(좌석), 배터리 3(차 아래 배터리 칸), 수리 3(레일 고장 부분 → 고쳐짐).
- 완성 연출: 시동(조명 깜빡이며 차례로 켜짐, 차가 부르르 떨림, 불꽃 튐) 3초 → 탈 수 있음.
- 출발: 쿠키가 좌석에 타서 기다림(빈 좌석에 탄 쿠키 모습이 보임) → 전원이 모이면 안전바가 내려오고 차가 오르막을 올라 맵 밖으로 질주한다.
- 레일이 맵 밖으로 이어지도록 맵 가장자리 쪽을 향해 놓는다(배치 규칙 추가).

### 5.3 베이커리: 마법 오븐 = 작동하는 기계 (V10)
- **맵의 마법 오븐(랜드마크)이 탈출 기계가 된다.** 따로 놓던 반죽 기계는 없앤다.
- 오븐 문(높이 약 10 m)은 **더 이상 아무나 열 수 없다**(지금은 일반 문이라 쿠키·괴물이 열어 볼 수 있음). 장치가 완성될 때만 열린다.
- 문 앞에 **쿠키가 걸어 올라갈 수 있는 경사 발판**(폭 5 m, 기울기 1:2 이하)을 놓는다. 괴물도 오를 수 있는 폭과 기울기로 만들어 통행 규칙을 지킨다.
- 오븐 옆면에 기계 부품: 톱니바퀴 2개, 피스톤, 굴뚝, 계기판. 칸: 레드 버튼(계기판), 기어(톱니 자리), 배터리 3, 수리 3.
- 완성 연출: 톱니가 돌기 시작 → 피스톤이 오르내림 → 굴뚝 연기 → 안의 불이 밝아짐(3초) → **문이 3초에 걸쳐 서서히 열림** → 탈 수 있음.
- 문을 연 뒤(결정 Q2): 오븐 안이 하얗게 빛난다. 쿠키가 발판을 올라 문 앞에서 E를 누르면 **빛 속으로 빨려 들어가는** 연출(쿠키가 문 쪽으로 끌려가며 작아지고 빛 입자로 사라짐)과 함께 안에서 기다린다. 전원이 모이면 문이 닫히고 오븐 안에서 큰 빛이 번쩍이며 출발(탈출).

### 5.4 공장: 초콜릿 기차 (V11)
- 모델: 증기 기관차 + 객차 1량 + 승강장 + 레일(맵 밖으로 이어짐).
- 칸: 마카롱 바퀴 2, 쿠키 바퀴 2(실제 바퀴 자리), 초콜릿 원유 2(탱크), 기어 2(구동부).
- 완성 연출: 시동(굴뚝 연기 퍼프가 빨라짐, 바퀴가 조금씩 돌고 기적 불빛) 3초 → 탈 수 있음.
- 출발: 쿠키가 객차 문으로 타서 기다림(창문에 쿠키 그림자) → 전원이 모이면 기적이 울리는 빛과 함께 기차가 천천히 출발해 레일을 따라 맵 밖으로 나간다.

### 5.5 진저브레드: 시계탑 아래 쿠키 유적 (V12)
- **지상(결정 Q3):** 원래의 멀쩡한 과자 마을로 되돌린다(지난번 무너진 집 설정을 끈다).
- **시계탑:** 1층에 큰 문(괴물도 지나갈 폭)을 내고, 안에 지하로 내려가는 넓은 계단(폭 5 m, 천장 6 m 이상 — 괴물이 내려갈 수 있게).
- **지하 쿠키 유적(약 40 × 40 m, 지면 아래 약 12 m):**
  - **정말 버려진 폐가 느낌(결정 Q3):** 무너지고 기울어진 과자 집(지붕 내려앉음, 창문 판자로 막힘, 문짝 떨어짐), 금 간 쿠키 기둥, 바닥에 흩어진 잔해와 깨진 가구·램프, 큼직한 거미줄, 먼지 낀 어두운 색. 빛은 깜빡이는 낡은 등불 몇 개와 희미한 룬 빛뿐(어둡지만 길은 보이게).
  - 가운데 **룬 제단**: 원형 계단 제단, 가운데 룬 기둥. 필요한 룬이 모두 끼워지면 무지개빛으로 빛난다(지금 구현 그대로).
  - 활성화되면 제단 위에 **포탈**(무지개 소용돌이 원)이 열린다. 쿠키가 들어가면 포탈 안에 떠서 기다리고, 전원이 모이면 포탈이 무지개빛으로 번쩍이며 닫힌다(탈출).
- **기술 처리:**
  - 지하는 맵 축소(`MapCompactor`) 대상에서 빼고, 축소가 끝난 뒤 시계탑 위치에 맞춰 따로 놓는다(Blender `GIN_Underground.fbx`).
  - 시계탑 바닥의 땅 메시에 계단 구멍을 낸다(편집기에서 그 사각형 안의 삼각형을 지운 메시를 저장).
  - 추락 처리 높이(kill zone, 지금 -12.4 m)를 지하보다 아래(-30 m)로 내린다.
  - 통행 검사: 지상 + 지하 모두 괴물·쿠키가 갈 수 있어야 한다(검사기의 층 수 4로 충분).

---

## 6. 네트워크·규칙 변경 요약

| 항목 | 내용 |
|---|---|
| `EscapeState.CompletedAt` | 장치 완성 시각, 맵 연출 기준(상태 안에 함께 저장해 방 속성을 늘리지 않음) |
| `EscapeState.Waiting`, `DepartedAt` | 탑승해 기다리는 쿠키, 한 번에 출발한 시각. 인코딩 버전 +1 |
| 탑승 승인 | `Board` 위치 근처 + 완성 후 "탈 수 있음" 시각이 지난 뒤. 출발은 살아 있는 쿠키(스파이 제외) 전원 탑승 시 |
| 변장 단계 | 탈출 모드 요청 전부 거절 |
| 베이커리 오븐 문 | `InteractableDoor`에서 빼고 장치가 연다 |
| 지역 | 로비 지역 선택 + 방 만들기 지역 줄, `ConnectToRegion`으로 접속 |
| ESC 메뉴 | 모든 역할 공통, 입력 차단·커서 풀림, 나가기(설정 자리 비움) |

---

## 7. 검증
- 단계마다: 컴파일 오류, Console 오류·경고, 통행 검사(맵 5개), 테스트(EscapeTests·RuleTests·MapCompactTests).
- 새 테스트: 로켓 모델에 칸 2개의 `Empty/Filled`가 있는지, 장치 모델에 `Board`가 있는지, 변장 단계 요청 거절, 처형 시점 고정/복귀(진행도 계산), 진저브레드 지하까지 걸어갈 수 있는지.
- Play Mode(오프라인): 맵마다 부품 → 완성 연출 → 탑승 → 출발, 스파이 로켓 점화·상승, 처형 1인칭, ESC 메뉴(괴물), 색칠 중 상자 막힘.
- 방 목록: 지역을 바꿔 다시 접속하고, 다른 지역으로 방을 만들 때 다시 접속 → 생성되는지 확인(여러 PC 실제 확인은 사용자 테스트 필요).

---

## 8. 결정 기록

| 번호 | 질문 | 결정 |
|---|---|---|
| Q1 | 탈것 출발 방식 | 살아 있는 쿠키(스파이 제외)가 **모두 모이면 한 번에 출발**. 도중에 잡혀 죽은 쿠키는 제외(§4.3). 남은 기본값은 §4.3 표 |
| Q2 | 베이커리 문을 연 뒤 | 오븐 안 빛으로 **빨려 들어가는 연출**(§5.3) |
| Q3 | 진저브레드 지상 | **원래 멀쩡한 마을**로 되돌리고, 지하 유적을 **정말 폐가처럼**(§5.5) |
| Q4 | 괴물 뒤로가기 | **ESC 메뉴**(모든 역할). 나중에 설정 버튼을 넣을 수 있게 만들고, 나가기를 그 메뉴에 둔다(§1.2) |
| Q5 | 방 지역 | 방장이 **방 만들 때 지역을 고름** + 로비에서 볼 지역 선택(Photon 제약, §1.1) |
