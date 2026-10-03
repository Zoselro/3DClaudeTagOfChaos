# 계획: 2026-10-03 요청 2 — 룬·재료 칸 최대 수 · 스파이 훔침 알림 · 스파이 1명 · 탈출자 관전 · 캔디숲 바위 · 로켓 내부

작성 2026-10-03 · 상태: **✅ 완료(2026-10-03)** — 결과 §10 · 모든 내용은 지금 코드(프로젝트에서 받은 최신 스크립트)와 에디터 실측을 기준으로 했다.

---

## 1. ✅ 다른 맵도 "최대 칸 수 − 필요 수만큼 미리 켜짐"

### 1.1 지금 코드
| 위치 | 내용 |
|---|---|
| `Escape/RecipePlanner.cs:22-44` (개수 모드) | `recipe.CounterFillsCapacity`면 칸 = `max(필요, capacity)`, 아니면 칸 = 필요 수 |
| `Escape/RecipePlanner.cs:46-77` (칸 모드) | 레시피 묶음의 칸을 **전부** 만들고(고정 + 무작위로 필요 칸 표시), 나머지는 채워진 칸 → 칸 수가 레시피 합계(8)로 고정 |
| `Escape/EscapeAuthority.cs:41` | `Plan(recipe, cookies, rng, GameSettings.Current.MaxCookieCount)` — capacity는 이미 넘기고 있음 |
| `Editor/Escape/EscapeAssetsBuilder.cs:70-82` | `fillsCapacity: true`는 진저브레드만 |

에디터 실측(카탈로그·인원표): 최대 쿠키 수 `MaxCookieCount` = **5**.

| 맵 | 방식 | 지금 칸 수 | 고정 | 지금 미리 켜진 칸(쿠키 3명일 때) | 요청대로라면 |
|---|---|---|---|---|---|
| 진저브레드 | 개수 | 5 | — | 2 | ✅ 이미 됨 |
| 캔디숲 | 개수 | **필요 수만큼(3)** | — | 0 | 5칸, 2칸 켜짐 |
| 놀이공원 | 칸 | **8** (빨간 버튼1·안전벨트1·전지3·수리3) | 2 | 5 | 5칸, 2칸 켜짐 |
| 베이커리 | 칸 | **8** (빨간 버튼1·톱니1·전지3·수리3) | 2 | 5 | 5칸, 2칸 켜짐 |
| 공장 | 칸 | **8** (마카롱 바퀴2·쿠키 바퀴2·오일2·톱니2) | 2 | 5 | 5칸, 2칸 켜짐 |

### 1.2 변경
- **캔디숲**: 레시피 `fillsCapacity = true`(모델에 전지 자리 `Slot_00..04` 5개가 이미 있음 — `escape_devices.py:766`).
- **칸 모드 3맵** (결정 D1, 추천 **A**):
  - **A(추천)** — 모델의 칸 자리 8개는 그대로 두고 **판마다 5칸만 켠다**: 고정 칸 + 나머지 자리 중 무작위 3칸. 그중 필요 칸 = 고정 + 무작위(필요 − 고정), 나머지 켜진 칸은 미리 채움. 켜지지 않은 자리는 받침·재료 모양을 만들지 않는다. 판마다 필요한 재료 종류가 달라지는 지금의 재미가 유지되고 모델·레시피를 고치지 않는다.
  - B — 레시피 묶음 합계를 5로 줄인다(예: 놀이공원 전지 2·수리 1). 코드 변경은 작지만 늘 같은 구성이 되고, 남는 칸 자리(5~7)가 모델에 남는다.
- A의 코드:
  - `EscapeRecipeSO.counterFillsCapacity` → **`fillsCapacity`**(두 방식 공통, `[FormerlySerializedAs]`로 기존 값 유지). 인스펙터 이름 "Fills Capacity".
  - `RecipePlanner.PlannedSlot`에 **`Anchor`**(모델 칸 자리 번호)를 더하고, 칸 모드에서 capacity만큼만 골라 돌려준다(순서는 자리 번호 순).
  - `EscapeState.Slot`에 `Anchor`(byte) — `Encode/Decode`에 한 칸 추가(상태 형식 버전 확인 포함).
  - `EscapeDevice`: 칸 i의 받침·재료를 `SlotAnchor(i)` 대신 **`SlotAnchor(slot.Anchor)`**에, `DistanceTo`·`ActionSlot`도 켜진 칸의 자리만 본다.
  - 레시피 4개 모두 `fillsCapacity = true`(빌더 `EscapeAssetsBuilder` + 에셋).
- 상자 수·뿅망치 수는 지금처럼 "필요 재료 수(= 쿠키 수)" 기준 — 바뀌지 않음.

### 1.3 검증
- EditMode: 4개 맵 × 쿠키 2~5명 → 칸 5개, 미리 켜진 칸 = 5 − 필요, 고정 칸은 늘 필요 칸, 자리 번호 중복 없음.
- Play Mode(오프라인): 맵 5개 각각 장치 칸 5개, HUD "n/필요", 빈 칸에만 E.

---

## 2. ✅ 스파이가 장치에서 재료를 빼면 n초 뒤 전체(괴물 제외) 알림

### 2.1 지금 코드
| 위치 | 내용 |
|---|---|
| `EscapeAuthority.Steal` `:221-237` | 장치에서 빼면 `StolenFromDevice = true`만 표시 |
| `EscapeAuthority.RocketInsert` `:239-258` | **로켓에 끼울 때** 훔친 재료였으면 `Notice(StolenToRocket)` |
| `EscapeAuthority.Notice` `:535-539` | `ReceiverGroup.All` — **괴물에게도** 감 |
| `EscapeHud.Notice` `:78-94` | 문구 `stolenToRocketWithFinal / NoFinal`("스파이가 {0}을/를 훔쳐 로켓에 넣었다") |

### 2.2 변경
- 새 알림 종류 `EscapeNoticeKind.StolenFromDevice = 4`(값 = 아이템 번호, 표시 시각).
- **`Steal`에서 바로 보낸다**: 방장이 `PhotonNetwork.Time + 지연`을 "보여 줄 시각"으로 담아 **괴물이 아닌 사람에게만**(`TargetActors`) 보낸다. 받은 쪽은 그 시각이 되면 토스트(경고음).
  - 방장이 바뀌거나 지연 중에 알림을 보낸 방장이 나가도 이미 보낸 알림은 그대로 뜬다(각자 시각을 기다림).
  - 지연 동안 그 재료가 **다시 장치에 끼워졌으면** 띄우지 않는다(결정 D3).
- 로켓에 끼울 때의 알림(`RocketInsert`의 `announce`)과 `StolenFromDevice` 표시는 **삭제**(요청: 뺀 시점 기준으로 바꿈).
- **인스펙터에서 바꾸는 값**(직렬화):
  - `GameSettingsSO` `[Header("Escape — Spy")]` `stealNoticeDelaySeconds` `[Min(0)]` 기본 **5초**(결정 D3) — `Assets/Resources/GameSettings`(전역 SO).
  - 문구 `EscapeTextsSO.stolenFromDeviceWithFinal / NoFinal`(예: "스파이가 {0}을 가져갔다!" / "…를…") — 한글은 일회성 스크립트로 에셋에 입력.
- 괴물 화면 방어: `EscapeHud.Notice`도 로컬이 괴물이면 무시(보내는 쪽 필터와 이중).

### 2.3 검증
- EditMode: 알림 대상 계산(괴물 제외), 표시 시각 판단(시각 전 X / 후 O / 재료가 장치로 돌아왔으면 X).
- Play Mode: 스파이로 훔침 → 설정한 n초(2·5초로 바꿔 가며) 뒤 쿠키 화면 토스트, 로켓에 끼울 때는 토스트 없음.

---

## 3. ✅ 스파이는 늘 1명, 늘어나는 것은 괴물

### 3.1 지금 코드·데이터
- `GameSettingsSO.roleTable`(`:24-32`) — 인원별 `(괴물, 스파이)`. 에셋 실측: 4명 1·0 / 5명 1·1 / 6명 1·1 / 7명 2·1 / 8명 2·1. **값으로는 스파이가 이미 최대 1명**이지만, 표에 스파이 칸이 있어 인원별로 늘릴 수 있고 코드도 여러 명을 가정한다:
  - `GameStartAuthority.cs:74` `PickSpies(SpyCountFor(...))`
  - `EscapeAuthority.Initialize` 스파이 상자 `spies * 2`, 공구상자 `spies * ExtraToolboxesPerSpy`
- `MaxCookieCount`(`:180-189`) = 표에서 (인원 − 괴물 − 스파이) 최댓값.

### 3.2 변경
- 표에서 **스파이 칸을 없애고** 인원별 **괴물 수만** 둔다. 스파이는 규칙으로 고정:
  - `[SerializeField] int spyMinPlayers` — 이 인원 이상이면 스파이 **정확히 1명**, 미만이면 0명(결정 D2: 기본 5 = 지금과 같음 / 4로 하면 4명 판에도 스파이 1명).
  - `SpyCountFor(n) => n >= spyMinPlayers ? 1 : 0`, `MaxCookieCount`도 이 규칙으로 계산.
- 기존 `RoleRow.spies` 값은 `[HideInInspector]`로 남겨 에셋 호환만 유지(읽지 않음).
- `PickSpies`·상자 계산은 그대로 두되(값이 0 또는 1뿐) 주석을 "스파이 1명" 기준으로 정리.
- `Plan.md/EscapePlan.md §1.1` 인원표 갱신.

### 3.3 검증
- EditMode: 4~8명 → 스파이 0/1명, 괴물 수는 표대로, `MaxCookieCount` = 5(D2=5일 때)/역할 합 = 인원.

---

## 4. ✅ 탈출한 사람의 관전 대상

### 4.1 지금 코드
- 탈것에 타거나 탈출하면 `EscapeCharacterState.OnStateChanged`(`:42-47`) → `SpectatorController.EnterSpectatorMode()`.
- `SpectatorController.IsCandidate`(`:151-157`) — 역할이 `Cookie`이고 관전 가능한 다른 캐릭터. **스파이도 역할이 Cookie라 후보에 들어간다**(괴물은 이미 빠짐).

### 4.2 변경
- 후보 규칙에 **"스파이 제외"**를 더한다: 로컬이 스파이가 아닌 쿠키면 `RoomState.IsSpy(대상)`인 캐릭터를 뺀다. 스파이 본인(로켓 탈출)은 괴물만 빠진 지금 규칙 그대로(다른 쿠키를 봄).
- 적용 범위(결정 D4): **탈출한 쿠키만** vs **부서진 쿠키 관전에도 같이**(추천 — 관전 화면으로 스파이 정체가 드러나지 않게).
- 볼 쿠키가 없으면 지금처럼 마지막 자리에 머문다.

### 4.3 검증
- EditMode(순수 규칙 함수로 분리): 보는 사람(탈출 쿠키/스파이/부서진 쿠키) × 대상(쿠키/스파이/괴물/탈출한 쿠키) 표.
- Play Mode(오프라인 + 원격 복사본 흉내): 탈출 후 Space로 순환할 때 괴물·스파이가 안 나옴.

---

## 5. ✅ 캔디숲 `CAN_Macaron_Rock_165`·`166` 제거 유지

### 5.1 지금 상태(실측)
- 씬 `Game_CandyForest.unity`에서는 이미 지워져 있다(사용자 작업).
- 그러나 원본 모델 `Assets/Maps/CandyForest/Models/CandyForest_GameplayProps.fbx`에는 아직 있다(케이크 로켓 양옆 x = ±9.5 m, 크기 6.4 × 3.7 × 6.6 m). 맵을 다시 만들면(`MapSceneBuilder`·Blender 다시 내보내기) 되살아난다.
- 이름 번호는 `assets.py:1084 place()` → `m.nm(name)`이 만든다.

### 5.2 변경
- Blender `build_candyforest.py`: 맵별 **제외 목록**(`REMOVED = {'CAN_Macaron_Rock_165', 'CAN_Macaron_Rock_166'}`)을 두고 `place()`가 이름을 받은 뒤 목록에 있으면 만들지 않는다 — **번호는 그대로 소비**하므로 다른 오브젝트 이름이 바뀌지 않는다.
- Unity: 같은 목록을 `MapSceneBuilder`가 읽어 씬을 다시 만들 때 건너뛴다(FBX를 다시 내보내기 전에도 안전).
- 테스트: `BuildSceneTests`에 "캔디숲 씬에 제외 목록 오브젝트 없음".

---

## 6. ✅ 로켓 문을 열면 안이 보이게(캔디숲 케이크 로켓 + 스파이 로켓 5종)

### 6.1 원인(코드·실측)
- 몸체는 **속이 꽉 찬 회전체 한 덩어리**다: 스파이 로켓 `escape_rockets.py:39-58 rocket_shell.body`(`g.lathe`), 케이크 로켓 `escape_devices.py:743-753 rocket_body`. **문 자리에 구멍이 없다.**
- 문(`Hatch`)은 몸체 겉면 바로 앞에 붙인 얇은 판(`escape_rockets.py:61-64`, `escape_devices.py:756-759`)이다. 열면(`SpyRocket.cs:103-107`, `CakeRocketSequence.cs:98-103` — 경첩 축 Y로 0 → 100°) **같은 색 몸체 벽**이 그대로 보인다.
- 에디터 실측: 문은 바깥으로 제대로 열린다(축에서 문 중심까지 케이크 2.49 → 3.35 m, 스파이 0.98 → 1.46 m) — 회전 방향 문제는 아니다.

### 6.2 변경(Blender 모델)
- 공통 도우미 `cabin_opening(u, body_obj, door_rect, depth, look)`(새, `escape_rockets.py`):
  1. **문 구멍**: 몸체에서 닫힌 문 크기보다 조금 작은 사각형을 불리언(Difference)으로 뚫는다 → 문이 닫히면 가려지고 열리면 구멍이 보인다.
  2. **문틀 두께**: 구멍 둘레에 벽 두께(스파이 0.08 m / 케이크 0.2 m)만큼 안쪽 면을 둘러 종이처럼 얇아 보이지 않게.
  3. **내부 `Cabin`**(새 정적 오브젝트): 안쪽을 향한 원통 벽(어두운 금속), 바닥, 의자 1개(스파이) / 의자 줄(케이크), 계기판 + 작은 발광 재질(`ME_Glow_Teal` — 실시간 조명 없이 안이 보이게), 천장 등 하나(발광 재질).
  4. 몸체 안쪽이 비어 보이는 곳이 없게 Cabin 벽이 구멍 뒤를 완전히 덮는다(밖에서 맞은편 하늘이 비치지 않게).
- 적용: `rocket_shell`(스파이 로켓 5종 공통) + `device_candy` 케이크 로켓. 맵별 장식(`*_extra`)은 그대로.
- Unity 코드: 이름 규칙(Body·Hatch·Flame·Board·Slot_*)이 그대로라 **`SpyRocket`·`CakeRocketSequence` 변경 없음**. 새 `Cabin`은 정적 자식이라 따로 처리할 것이 없다(케이크 로켓은 `Cake_Rocket` 아래에 두어 함께 오르내림).
- 다시 내보내기: 사용자 PC Blender(`escape_assets.py`, `ONLY=`로 SPY 5종 + `ESC_CandyForest`) → `EscapeModelBuilder.Build` → 맵 5개 `EscapeMapSetup.Apply` → 진저브레드는 `GingerbreadNeonDimmer` 다시 적용 → 씬 저장. 캔디숲 씬에서 사용자가 지운 바위(§5)가 되살아나지 않는지 함께 확인.
- 결정 D5: 내부 모양 — **간단한 조종실(벽·바닥·의자·빛나는 계기판, 추천)** vs 어두운 빈 공간만.

### 6.3 검증
- Blender 렌더(문 열린 상태 앞쪽 시점) 6장, Unity 씬 뷰 캡처(문 100° 열림) 6장 — 구멍·내부가 보이고, 문을 닫으면 구멍이 완전히 가려짐.
- 삼각형 수 증가 확인(로켓당 +1천 이하 목표), `MapCompactTests`·`EscapeTests`(모델 이름 규칙) 통과.
- 탑승 연출(`BoardingFx` 깡충 → 문 위치)이 구멍 안쪽으로 들어가 보이는지.

---

## 7. 순서·예상 시간

| 순서 | 내용 | 예상 |
|---|---|---|
| 1 | §3 스파이 1명 규칙(인원표 → 칸 수 계산의 기준이 되므로 먼저) | 30분 |
| 2 | §1 최대 칸 수(칸 모드 자리 고르기 + 상태 `Anchor` + 장치 표시) | 1시간 30분 |
| 3 | §2 훔침 지연 알림 | 1시간 |
| 4 | §4 관전 대상 | 30분 |
| 5 | §5 바위 제외 목록 | 30분 |
| 6 | §6 로켓 내부(모델 + 다시 내보내기 + 맵 5개 재배치) | 2시간 30분 |
| 7 | 회귀 전체 + 콘솔 0 + 문서 ✅ | 30분 |

## 8. 결정 필요

| # | 질문 | 추천 |
|---|---|---|
| D1 | 칸 모드 맵(놀이공원·베이커리·공장): A 자리 8개 중 판마다 5칸만 켬 / B 레시피를 5칸으로 줄임 | **A** |
| D2 | 스파이가 나오는 최소 인원 | **5명**(지금과 같음 — 4명 판은 괴물 1 + 쿠키 3). 4로 하면 4명 판도 스파이 1명(쿠키 2) |
| D3 | 훔침 알림 지연 기본값 / 지연 중 재료를 다시 끼웠으면 | **5초** / **알림 취소** |
| D4 | "스파이 시점 제외"를 부서진 쿠키 관전에도 적용 | **적용** |
| D5 | 로켓 내부 모양 | **간단한 조종실** |

## 9. 위험
| 위험 | 대응 |
|---|---|
| 상태 형식(`EscapeState.Encode`)에 칸 자리 번호가 늘어 이전 빌드와 섞이면 해석 실패 | 같은 빌드끼리만 방에 들어오는 지금 구조 유지, 형식 버전 바이트 확인 |
| 불리언 구멍이 회전체 이음매에서 깨진 면을 만듦 | 구멍을 세그먼트 경계에 맞추고, 실패하면 해당 면 직접 삭제 방식으로 대체. Blender 렌더로 확인 |
| 로켓 재배치 때 맵 씬 수동 수정(바위 삭제 등)이 덮어써짐 | `EscapeMapSetup`은 ESC_ 루트만 바꾸는지 확인 후 진행, 전후 씬 오브젝트 수 비교 |

---

## 10. 결정·결과(2026-10-03)

결정: D1 = **A**(자리 8개 중 판마다 5칸) · D2 = **5명**부터 스파이 1명, 인원이 늘면 괴물이 늘어남 · D3 = **5초**, 다시 끼우면 취소 ·
D4 = 사용자 변경: **스파이 시점도 포함, 탈출자만 제외**(지금 코드가 이미 그렇게 동작 — 변경 없음) · D5 = **간단한 조종실**.
추가 요청: 대기실 정원이 차면 30초 뒤 무작위 술래 선정 → **삭제**. 로비 배경음 → **후보 A(쿠키 마을 아침)**.

| 항목 | 바뀐 것 | 확인 |
|---|---|---|
| §3 스파이 1명 | `GameSettingsSO`: 인원표는 괴물 수만(스파이 칸 숨김·안 읽음), `spyMinPlayers = 5` → `SpyCountFor` 0/1, `MaxCookieCount`도 이 규칙 | EditMode `RoleTable_*`(4~8명 스파이 ≤ 1, 괴물 줄지 않음, 최대 쿠키 5) |
| §1 최대 칸 수 | `EscapeRecipeSO.fillsCapacity`(옛 `counterFillsCapacity` 값 유지) — 레시피 5개 모두 켬. `RecipePlanner` 칸 모드: 고정 + 무작위 자리로 5칸, `PlannedSlot/EscapeState.Slot.Anchor`(상태 형식 3). `EscapeDevice`가 칸 자리 번호로 받침·재료·거리 계산 | EditMode 8케이스(맵별 5칸, 미리 켜짐 = 5 − 필요, 자리 중복 없음, 판마다 다른 자리). Play(놀이공원): 5칸 = 버튼·벨트(고정) + 배터리 자리 2·4 + 수리 자리 5 |
| §2 훔침 알림 | `EscapeAuthority.Steal` → `AnnounceStolen`: 괴물 제외 대상에게 "보여 줄 시각"을 담아 보냄(오프라인은 전체). 로켓에 끼울 때 알림·`StolenFromDevice` 삭제. `EscapeHud`가 시각에 띄우고 재료가 장치로 돌아왔으면 취소(`StolenNotice`). 설정 `GameSettings.stealNoticeDelaySeconds`(5초), 문구 `EscapeTexts.stolenFromDevice*`("스파이가 {0}을/를 가져갔습니다!") | Play: 훔친 뒤 4초 대기 중 → 5초 후 "스파이가 안전벨트를 가져갔습니다!" / 1초 안에 다시 끼움 → 안 뜸. EditMode `StolenNotice_*` |
| §4 관전 | 변경 없음 — 후보 = 역할 Cookie(스파이 포함) + `IsSpectatable`(탈출·탑승 제외), 괴물 제외 | 코드 확인 |
| §5 바위 | `build_candyforest.py` 가운데 고리의 마카롱 바위 2개(k = 0·5)를 만들지 않음(이름 번호는 소비), `MapSceneBuilder.RemovedObjects` | `BuildSceneTests.MapScenes_DoNotContainRemovedObjects` |
| §6 로켓 | `escape_rockets.py` `cut_doorway`(몸체를 문 상자 4면으로 자르고 문 자리 면 삭제 + 양면 문틀, 문틀은 조종실 벽까지) · `cabin`(안쪽을 향한 벽·바닥·천장, 의자, 콘솔·화면·버튼, 등) — 스파이 로켓 5종 + 케이크 로켓(의자 3). `assets.py` `G.keep_winding`(안쪽 면이 내보내기 전 법선 재계산에 뒤집히지 않게). 조종실 재질 3종(약한 발광 — 몸체 그늘에서도 보이게). 씬은 FBX 프리팹 인스턴스라 다시 내보내기만으로 반영(씬 배치 다시 안 함), 진저브레드 네온 줄이기 재적용 | 캡처: 캔디숲 스파이 로켓 닫힘(구멍 가려짐)/열림, 케이크 로켓 닫힘/열림, 공장 스파이 로켓 열림. 삼각형 + 약 1,000(스파이)·1,450(케이크) |
| 30초 자동 술래 | `MonsterAssignmentAuthority`: 기한 설정·무작위 선정 삭제(남은 옛 기한은 지움), `GameSettingsSO.monsterSelectTimeout` 삭제 | Play(GameLobbyScene): 정원 참 상태로 35초 — 기한 없음, 괴물 0, 문구 "술래 선정 중 — 가마솥에 들어가세요" |
| 로비 배경음 | `Assets/10. Audio/BGM/Lobby.ogg`(후보 A, 76.8초, 고음질 Vorbis — 샘플 수 원본과 같아 이음매 유지), 옛 `Lobby.wav` 삭제, 카탈로그 다시 빌드 | Play(LobbyScene): Lobby 재생·반복 |

회귀: EscapeTests 52/52 · BuildSceneTests 9/9 · MapCompactTests 25/25 · AudioTests 50/50 · DistanceFadeTests 12/12 · RuleTests 27/27, 콘솔 오류·경고 0.
남은 것: 2대 이상(온라인) 확인 — 훔침 알림이 괴물에게 가지 않는지, 상태 형식 3이 원격에서 해석되는지.

## 11. 멀티 확인 후 버그(2026-10-03)

| 버그 | 원인 | 수정 | 확인 |
|---|---|---|---|
| 7~8명 방에서 술래가 정해졌는데 시작 버튼이 안 눌림 | 인원표상 괴물 2명이 필요한데 가마솥은 들어간 1명만 괴물로 만들고, 남은 자리는 30초 타이머가 채웠다 → 타이머 삭제(§10) 뒤 두 번째 자리가 영영 비어 시작 조건(괴물 수) 미충족 | `MonsterAssignmentAuthority`: 가마솥에 먼저 들어간 사람 + 남은 자리를 **그 순간 무작위로** 채움(`PickExtraMonsters`, 쿠키 최소 1명 유지) | EditMode `MonsterFill_PicksRemainingSeatsAtOnce` |
| 한 명이 나간 뒤 방장이 방 설정으로 최대 인원을 줄여도 "인원 부족" 문구가 남고 "가마솥에 들어가세요"가 안 뜸(괴물이 정해지면 시작 버튼은 켜짐) | 최대 인원 변경은 Photon 기본 방 속성(MaxPlayers) 변경으로 오는데, `GameLobbyController.OnRoomPropertiesUpdate`가 괴물 관련 키가 바뀔 때만 버튼·문구를 다시 그렸다 | 어떤 방 속성이 바뀌어도 `RefreshStartButton`(문구 포함) | 오프라인 대기실: 방 속성 변경 뒤 문구 갱신·콘솔 0. 정원 감소 시나리오는 멀티에서 재확인 필요 |

회귀: EscapeTests 53/53 · RuleTests 27/27.
