# 2026-10-09 요청 11건 계획 (Request1009Plan)

- 2026-10-11 추가 요청 8건은 §16 — ✅ N0~N9 완료(2026-10-11).
- 작성: 2026-10-09 · 상태: **📝 계획 — §13 결정 반영(2026-10-09), 진행 승인 대기**
- 근거: 이 문서의 "원인"은 실제 소스(`Assets/02. Scripts`, `Assets/Editor`)와 씬을 열어 확인한 값이다. 줄 번호는 작성 시점 기준.
- 작업 규칙(Claude.md): 코드 변경마다 컴파일·콘솔 확인 → Play Mode 실제 동작 확인 → 단계가 끝나면 §12 표에 ✅. 한국어 UI 문자열은 `\uXXXX`로 씬·프리팹·SO에 넣는다. 원본 음원은 `Source~`에 따로 보관.

## 요청 목록
| # | 요청 | 묶음 |
|---|---|---|
| 1 | 상자는 E를 **2초 꾹 누르면** 열림 + 동그란 진행 표시 — **상자와 장치**에 적용(Q4) | R1 |
| 2 | 상자가 열릴 때 "무언가 얻는" 소리 대신 **상자 여는 소리** | R1 |
| 3 | 로비(LobbyScene) 배경음이 유아틱 → 바꾸기. **결과 화면은 전용 곡을 따로 제작 → 승인 후 넣기**(Q3) | R2·R7 |
| 4 | 캔디숲 로켓 발사 소리가 유아틱 → **불꽃 점화 + 솟구치는 소리** | R2 |
| 5 | 대기실(GameLobbyScene) 마녀 집이 너무 어두워 멀리서 안 보임 | R3 |
| 6 | 베이커리 탈출 문(오븐)이 열렸을 때 너무 밝음 → **밝기 50% 줄이기** | R3 |
| 7 | 진저브레드: 관전 중 대상이 지하로 내려가면 환경음(바람·물방울)이 안 들림 | R4 |
| 8 | 놀이공원 회전목마가 반대로 돈다 | R3 |
| 9 | 탈출한 사람(스파이·괴물 제외)은 관전 시점 + **Space로 시점 전환** | R4 |
| 10 | 놀이공원: MeshCollider가 아니라 BoxCollider라서 막히는 곳 | R5 |
| 11 | 놀이공원: 쿠키가 점프해 구조물 위에 올라가면 괴물이 못 잡는 곳 | R5 |

---

## 1. 상자·장치 길게 누르기(2초) + 진행 동그라미 (Q4: 상자와 장치만)

**지금 코드**
- E 입력은 `CharacterInteractor.Update`(`Assets/02. Scripts/Interaction/CharacterInteractor.cs:45-52`)가 받는다. `PlayerInput.InteractPressed`(=`GetKeyDown`) 한 번이면 `focused.Interact()`를 바로 부른다. 누르고 있는 상태(`GetKey`)를 보는 입력이 없다(`Core/PlayerInput.cs:60`).
- E로 쓰는 사물(`IInteractable` 구현) 4종: 상자 `MaterialChest`, 탈출 장치 `EscapeDevice`, 스파이 로켓 `SpyRocket`, 문 `InteractableDoor`(+ 땅 위 재료 `GroundItemView`는 줍기).
  - 상자 `MaterialChest.Interact`(`Escape/MaterialChest.cs:59`) → `EscapeOp.OpenChest`. 방장 `EscapeAuthority.OpenChest`(`Escape/EscapeAuthority.cs:146-159`)가 거리·스파이 상자 잠금을 검사하고 열며 안의 재료를 곧바로 손에 쥐어 준다(`:157 Give`).
  - 장치 `EscapeDevice.Interact`(`Escape/EscapeDevice.cs:235-249`)는 한 번에 세 가지 중 하나: **재료 넣기**(`EscapeOp.Install`, 손 닿는 빈 칸), **빼앗기**(스파이, `EscapeOp.Steal`), 완성 뒤 **탑승**(`EscapeOp.Exit`, 스파이는 "들어갈 수 없음" 알림). 칸 선택은 누른 순간 `ActionSlot()`이 정한다.
  - 스파이 로켓 `SpyRocket.Interact`(`Escape/SpyRocket.cs:209-217`): 재료 넣기(`RocketInsert`) / 완성 뒤 탑승(`RocketBoard`). 스파이에게만 보인다.
- 안내 아이콘 `InteractionPromptUI`(`Interaction/InteractionPromptUI.cs`)는 동그라미 + 키 글자 + 이름뿐이고 진행 표시가 없다. 프리팹 `Resources/UI/Scene/InteractionPromptUI`, 만드는 도구 `Assets/Editor/Interaction/InteractionPromptBuilder.cs`.

**적용 범위(Q4 결정 + 이 계획의 해석)**
| 사물·동작 | 누르기 | 이유 |
|---|---|---|
| 상자 열기(일반·스파이 상자를 스파이가) | **2초** | 요청 |
| 장치에 재료 넣기 | **2초** | 요청(장치) |
| 장치에서 재료 빼앗기(스파이) | **2초** | 넣기와 같은 시간이어야 동작으로 스파이가 드러나지 않는다(D35와 같은 취지) |
| 장치 탑승(완성 뒤) | 즉시 | 탈출 순간을 늦추면 쫓기는 중에 불리 — 넣기·빼기와 다른 동작. 원하시면 2초로 바꾸기 쉬움(값 하나) |
| 스파이 로켓 넣기 / 탑승 | 2초 / 즉시 | 장치와 같은 규칙 |
| 문, 땅의 재료 줍기 | 즉시 | Q4: 상자와 장치만 |

**바꿀 것**
1. `Interaction/IInteractable.cs`에 선택 계약 추가 — `IHoldInteractable : IInteractable { float HoldSecondsFor(IGameCharacter c); }` (0이면 즉시 = 지금과 같음). 동작마다 시간이 다르므로(장치: 넣기 2초·탑승 0초) 사물이 지금 상태로 정한다.
2. `Core/PlayerInput.cs`에 `InteractHeld`(`GetKey`, 채팅·메뉴 중 억제) 추가.
3. 순수 계산 `Interaction/HoldProgress.cs`(새 클래스): `Begin(target, seconds, now)`, `Tick(target, held, canInteract, now)` → 진행률·완료·취소. EditMode 시험 대상.
4. `CharacterInteractor.Update`:
   - 괴물 잡기 우선(`:45`)은 그대로.
   - E를 누른 순간 대상이 `IHoldInteractable`이고 `HoldSecondsFor > 0`이면 누르기 시작, 아니면 지금처럼 즉시 `Interact`.
   - 누르고 있는 동안 진행률을 아이콘에 보내고, 1이 되면 `Interact()` 한 번 → 초기화. 떼거나 · 초점 사물이 바뀌거나(범위 밖·다른 상자) · `CanInteract`가 거짓이 되면(다른 사람이 먼저 엶, 잡힘·부서짐, 변장 시간) 취소.
   - 누르는 동안 이동은 막지 않는다(범위를 벗어나면 자연히 취소).
5. `MaterialChest`: `HoldSecondsFor` = 스파이 상자를 일반 쿠키가 보면 0(잠김 — 지금처럼 아무 일 없음), 그 밖 2초(직렬화 필드 `holdSeconds = 2f`).
6. `EscapeDevice`: 완성 전(넣기·빼기) 2초, 완성 뒤(탑승) 0. 2초 뒤 `Interact`가 그 순간 `ActionSlot()`을 다시 계산하므로 칸이 바뀌어도 맞는 칸에 들어간다(지금 로직 그대로).
7. `SpyRocket`: 재료를 들고 있으면(넣기) 2초, 탑승은 0.
8. `InteractionPromptUI`에 진행 링 추가 — 동그라미 둘레 `Image`(Filled, Radial360, 시계 방향, 12시 시작) + `SetProgress(float)`(0이면 숨김). `InteractionPromptBuilder`가 링을 프리팹에 넣고 다시 저장(이미 있으면 갱신만).
- 네트워크: 누르기는 각자 화면에서만, 2초가 지나면 지금처럼 요청 한 번 → 방장이 검사. 방장 규칙·`EscapeAuthority`는 바꾸지 않는다(동시에 둘이 누르면 먼저 도착한 요청).

**검증**: `HoldProgress` EditMode 시험(2초 완료·1초에 뗌·대상 바뀜·CanInteract 거짓). Play Mode(오프라인): 상자 E 2초 → 링이 차고 열림 / 1초에 떼면 안 열림 / 장치에 재료 넣기 2초 / 완성 장치 탑승은 즉시. 링 스크린샷.

## 2. 상자 여는 소리 + 획득 소리 (Q2: C 다음 A)

**지금 코드·음원**
- 상자가 열리면 `EscapeAudioPresenter.PlayDifferences`(`Audio/EscapeAudioPresenter.cs:42`)가 `ChestOpen`을, 같은 상태 변화에서 재료가 손으로 들어오면 `:52`가 `ItemPickup`을 **같은 프레임에** 낸다.
- `ItemPickup`(`SFX/Escape/ItemPickup.wav`)은 S8 그대로인 "팝 + 마림바 두 음(E5·A5)" — "무언가 얻은 소리". V5의 `ChestOpen`도 끝에 오르골 두 음이 있다. 이제 1번으로 상자는 2초 누른 **뒤에** 열리므로, 그 순간 들리는 소리가 "뚜껑이 열리는 소리"여야 한다.

**바꿀 것**
1. (C) `ItemPickup` 다시 만들기(같은 ID·파일): 음정 없이 **재료를 움켜쥐는 손 소리** — 천·종이 바스락 + 과자 부스러기 + 작은 나무 딸깍(약 0.25초). 땅에서 줍기·상자에서 받기·장치에서 빼앗기(스파이가 손에 쥠) 모두 이 소리.
2. `ChestOpen` 다시 만들기(같은 ID·파일): 음정 없이 **무거운 나무 뚜껑** — 걸쇠 딸깍(0.05초) → 경첩 삐걱(0.5초) → 뚜껑이 젖혀지며 나무 쿵 + 먼지 바스락(총 약 0.9초).
3. (A) 상자에서 재료가 나온 경우 `ItemPickup`을 **0.45초 늦춰** 뚜껑 소리 뒤에 붙인다: `EscapeAudioPresenter.PlayDifferences`에서 같은 차이 안에 열린 상자가 있고 그 재료가 그 상자의 것이면 지연(코루틴 하나, `PlayOnActor` 재사용). 땅에서 줍기·장치는 지금처럼 바로.
- 크기는 각각 S8 같은 소리의 LUFS. 지금 파일은 `Source~/s8_backup/`(이미 있으면 두지 않음 — `ChestOpen`은 S8 원본이 이미 백업됨), 새 원본 `Source~/twisted/final/`.

**검증**: Play Mode 소리 기록(`amon2.cs`)으로 [2초 누름] → 0초 `ChestOpen` → 0.45초 `ItemPickup` 순서, 땅에서 줍기는 `ItemPickup` 바로. 청취 묶음(지금 → 새).

## 3. 로비 배경음 + 결과 화면 전용 곡 (Q3)

**지금 코드**: `MusicRules.Select`(`Audio/MusicRules.cs:38`) 로비 씬 → `MusicId.Lobby`, 그리고 **결과 단계(`:46 GamePhase.Result`)도 `Lobby`**. 결과 단계에서는 `MusicDirector.PlayStingers`(`Audio/MusicDirector.cs:58-62`)가 먼저 결과 징글(`JgEscapeSuccess`·`JgEscapeFail`·`JgMonsterWin`)을 내고, 이어서 `Select`의 곡이 `ResultFadeSeconds`로 들어온다. `MusicId`(`Audio/SoundId.cs:123`)는 반복 곡이 1~20번(`MusicCatalogSO.IsLoopById` = 100 미만이면 반복). 카탈로그는 `AudioCatalogBuilder.BuildMusic`이 파일 이름 = ID 이름으로 연결. `AudioTests`(`Editor/Tests/AudioTests.cs:210-224`)가 결과 단계 = `Lobby`를 시험한다.

**3-1 로비 곡(이번에 교체)**: `twisted_final.py`에 `bgm_lobby()` — 다른 맵과 같은 "망가진 동화"지만 메뉴에 맞게 **덜 무섭고 기억에 남는 주제**: D단조 3/4 약 76 BPM, 오르골 주선율(지금 로비 선율 윤곽을 단조로 바꿔 기억 유지) + 낮은 현 + 아주 약한 바람, 테이프 흔들림 약하게(늘어짐 1번), 약 90초 이음매 없는 반복, −18 LUFS. 같은 파일 이름 `BGM/Lobby.ogg`로 교체, 지금 파일은 `Source~/s8_backup/BGM/Lobby.ogg`.

**3-2 결과 화면 곡(만들기 → 승인 → 넣기, 두 단계)**
- 만들기(R2): `bgm_result()` — 판이 끝난 뒤 결과표를 보는 동안의 곡: 징글 뒤에 이어지는 **차분하고 낡은 오르골 회상곡**(A단조→C장조로 끝나는 진행, 승패와 관계없이 쓰므로 너무 기쁘거나 슬프지 않게), 3/4 약 66 BPM, 약 60초 반복, −20 LUFS(징글보다 작게). 게임에 넣지 않고 `Source~/twisted/review/Result_candidate.ogg` + 징글 3개 뒤에 이어 붙인 청취 묶음 3개(성공/실패/괴물 승리 → 결과 곡)를 드린다.
- 승인 후 넣기(R7):
  1. `MusicId.Result = 4` 추가(반복 곡 번호대, 기존 번호는 그대로).
  2. `MusicRules.Select`의 `:46`을 `MusicId.Result`로.
  3. `AudioTests`의 기대값(`:217-219` 결과 단계)을 `Result`로.
  4. 파일 `BGM/Result.ogg` → `Build Catalogs`(새 항목 자동, 반복 = true).
  5. Play Mode: 결과 단계에서 징글 → 결과 곡 재생 기록.

**검증**: 이음매 수치, 청취 묶음, Play Mode에서 LobbyScene 재생 클립 확인(3-1). 3-2는 승인 후.

## 4. 캔디숲 로켓 발사 소리

**지금 코드**: 캔디숲 탈출 장치는 `CakeRocketSequence`(`Escape/Sequences/CakeRocketSequence.cs:54-59`). 완성: `CakeRumble`(0초)·`CakeCrack`·`CakeBurst`·`CakeRocketRise`(로켓이 올라옴), 출발: `CakeIgnite`(0초)·`CakeLaunch`(점화 뒤).
- `CakeRocketRise` = 하프 글리산도(C4→C7) + 금속 딸깍, `CakeLaunch` = 반짝이(sparkle) + 글로켄 G6 — 이 두 개가 유아틱한 원인. `CakeIgnite`도 휘파람 같은 상승음.
- 스파이 로켓(`SpyRocket.cs:126-127` `RocketIgnite`·`RocketLiftoff`)은 이미 음정 없는 불꽃·굉음이라 그대로.

**바꿀 것**(같은 ID·길이, 음정·반짝이 없음):
- `CakeRocketRise`(3초): 케이크 속에서 로켓이 밀려 올라오는 기계음 — 낮은 유압 신음 + 금속 걸림 + 부서지는 케이크 부스러기.
- `CakeIgnite`(2초): **도화선 지글거림 → 점화 "푸웅"**(불꽃 탁탁 + 가스 붙는 소리 + 낮은 울림).
- `CakeLaunch`(5초): **추진 굉음이 커지며 위로 솟구쳐 멀어짐**(저역 굉음 + 불꽃 탁탁 + 위로 갈수록 고역이 줄고 작아짐, 끝에 멀리서 퍼지는 메아리).
- 크기는 S8 같은 소리의 LUFS. 원본은 `Source~/twisted/final/`, 지금 것은 `s8_backup`(이미 없는 경우만).

**검증**: Play Mode 캔디숲 장치 완성·출발 재생 기록, 청취 묶음.

## 5. 대기실 마녀 집 밝기

**지금(측정)**: 출발점 `PlayerSpawnPos` (55, 31, 59) → 마녀 집 (0, 10, 0)까지 약 80 m. 안개는 **지수 제곱 0.03**(V1 `TwistedAtmosphere.GameLobby`에서 제가 0.025 → 0.03으로 올림) — 80 m에서 보이는 비율 e^−(0.03·80)² ≈ 0.3%. 집 주변 등불은 범위 5 m·강도 0.68(V1에서 ×0.75), 정문 조명 범위 8 m. 25·45·65 m에서 찍은 사진: 45 m부터 거의 검정(`images/r1009/lobby_now.jpg`).

**바꿀 것**(`Assets/Editor/Maps/TwistedAtmosphere.cs`의 `GameLobby` 값 + 대기실 전용 보강, 다시 실행해도 같은 결과):
1. 안개 0.03 → **0.012**(80 m에서 약 40% 보임). 등불 배율 0.75 → 1.0.
2. 집을 비추는 조명 추가(`TwistedAtmosphere`가 만든다, 이름 `TW_WitchHouse_*`): 앞·양옆 아래에서 올려 비추는 보라·주황 스포트 3개(범위 25 m, 그림자 없음) + 지붕 테두리를 비추는 약한 달빛 보강.
3. 창문 발광 재질은 대기실 전용 사본으로 발광 ×1.8(공유 재질은 건드리지 않음 — GingerbreadNeonDimmer와 같은 방식).
4. 색 보정 노출 +0.3 → +0.5.

**검증**: 같은 세 지점(25·45·65 m) 전후 사진과 화면 평균 밝기, 집 영역 밝기 비율. 점광원 수 증가 3개(프레임 영향 미미).

## 6. 베이커리 오븐(탈출 문) 밝기 50%

**지금 코드**: `OvenSequence.Tick`(`Escape/Sequences/OvenSequence.cs:111-120`)
- 문이 열리면 `Oven_Light`(재질 `ME_Glow_White`, 발광 (3.0, 2.91, 2.64) — **탈출 모델들과 공유하는 재질**)를 켜고 크기를 키운다.
- 점광원 `OvenGlow` 강도 = `power×2.5 + open×4 + burst×14`(최대 6.5, 출발 섬광 때 최대 20.5), 범위 30 m(섬광 때 55 m).

**바꿀 것**: `OvenSequence`에 `BrightnessScale = 0.5f` 상수를 두고
- `glow.intensity` 세 항(불 붙음·문 열림·섬광)을 모두 0.5배 — 요청대로 열렸을 때 밝기 전체를 50%로.
- `Oven_Light` 발광은 `MaterialPropertyBlock`으로 이 렌더러만 0.5배(공유 재질 `ME_Glow_White`는 그대로).
- 출발 섬광 파티클(`Flash`)은 개수 120 → 60.

**검증**: Play Mode 베이커리 장치 완성 상태에서 같은 시점 전후 사진, 화면 밝기 50% 근처.

## 7. 진저브레드 관전 중 지하 환경음

**지금 코드**
- 듣는 위치는 `AudioListenerAnchor.ListenPoint`(`Audio/AudioListenerAnchor.cs`) = `Camera_Ctrl.FocusPoint` = 따라가는 대상 + 눈높이. 관전(`SpectatorController`)도 같은 `Camera_Ctrl.SetFollowTarget`을 쓰므로 **구조상 관전 대상 위치를 듣는다.**
- 지하 소리 `AmbientZone`(`AMB_Underground`, `Assets/Editor/Audio/AmbientPlacer.cs:57`) 상자 2개: (−34, −5.75, 0) 크기 41.5×9.5×41.5, (−4.7, −5.75, 0) 크기 20.6×9.5×11.2 — 지하 구조물(`EscapeSetup/Body` x −55~6, z ±21, y −10~10)의 대부분을 덮는다. 땅 위 바탕음은 y > −1만.
- 동굴 소리 `AmbCave`는 파일 −26 LUFS + 환경음 묶음 −12 dB → 게임 안 약 **−38 LUFS**로 가장 작다(V5에서 바람 저역 260 Hz로 더 어둡게 만듦). 지하에 들어가면 배경음이 0.15배로 줄어 다른 소리가 거의 없지만, 추격음·발소리가 있으면 묻힌다.

**확인 절차(원인 확정 후 수정)** — 관전은 다른 사람 캐릭터가 있어야 해서 오프라인 한 명으로는 그대로 재현할 수 없다. 대신
1. Play Mode(진저브레드 오프라인)에서 지하 여러 지점에 빈 대상(`GameObject`)을 두고 `Camera_Ctrl.SetFollowTarget`으로 관전과 똑같이 따라가게 한 뒤, `AmbientZone` 진입·동굴 소리 칸의 음량을 기록한다.
2. 같은 지점에 실제 쿠키를 걸어 보내(`MapPlaytestDriver`) 비교.
3. 나선 통로·터널의 실제 걷는 바닥을 격자로 훑어 상자 밖인 칸이 있는지 센다.

**수정(확인 결과에 따라)**
- 상자 밖 칸이 있으면 `AmbientPlacer.Zones`의 지하 상자를 실제 바닥 범위에 맞춰 넓히고(땅 위 y > −1과 겹치지 않게) `Place Ambience` 다시 실행.
- 관전 경로에서만 안 바뀌면(예: 관전 대상 전환 직후 듣는 위치가 카메라로 떨어짐) `AudioListenerAnchor`·`SpectatorController`를 고친다.
- 공통: **`AmbCave`를 −26 → −22 LUFS**(다른 바탕음과 같은 수준) + 물방울을 조금 더 또렷하게 다시 만든다(V5 스크립트).

**검증**: 위 1·2의 기록에서 지하 모든 시험 지점에서 동굴 소리 음량 > 0, 청취.

## 8. 회전목마 방향

**지금 코드**: `MapSceneBuilder.BuildAnimated`(`Assets/Editor/Maps/MapSceneBuilder.cs:713`)가 `CUR_Carousel_Top`에 `Carousel_Top_Spin` 클립(`Assets/Maps/Common/Animations/Carousel_Top_Spin.anim`, `localEulerAnglesRaw.y` 0 → **+360**, 40초)을 붙인다. Unity의 +Y 회전은 위에서 볼 때 **시계 방향** — 말 머리 방향과 반대라 거꾸로 달리는 것처럼 보인다(실제 회전목마는 위에서 볼 때 반시계).

**바꿀 것**: `:713`의 부호 `1f` → `-1f`(0 → −360). 맵을 다시 만들지 않고 클립만 다시 쓰는 메뉴(`Tools/TagOfChaos/Maps/Fix Carousel Direction` — `WriteSpinClip`을 같은 값으로 호출)를 실행. 먼저 위에서 찍어 말 머리 방향을 확인한 뒤 적용.

**검증**: Play Mode에서 1초 간격 위에서 두 장 — 말이 머리 방향으로 이동.

## 9. 관전 대상 규칙 + Space 전환 (Q1: A + 부서진 쿠키는 스파이 포함)

**지금 코드**
- 관전에 들어가는 길 3개, 모두 `SpectatorController.EnterSpectatorMode()`(`Monster/SpectatorController.cs:40-50`):
  - 부서짐(괴물에게 잡힘) — `Unit/HideOrSeekPlayer.cs:153`(연출 중이면 `CookieLifeStatePresenter`가 부서지는 순간)
  - 마녀에게 죽음 — `HideOrSeekPlayer.cs:240`
  - 탈출·탈것 탑승·스파이 로켓 탑승 — `Escape/EscapeCharacterState.cs:43-47`(대기 중 `Waiting` 포함)
- 대상 후보: `IsCandidate`(`SpectatorController.cs:146-152`) = 살아 있고 다른 사람이며 `IsSpectatable`이고 역할이 `Cookie`(기본 `SpectateTargets.AliveCookies`) → **괴물은 이미 제외, 스파이는 쿠키 역할이라 포함**. 관전하는 사람이 왜 관전 중인지(부서짐/탈출)를 구분하지 않는다.
- Space: `InputBindings.asset`의 `spectateNextKey = Space`(점프와 같은 키). `Update`(`:57-84`)는 **탈것을 따라 보는 동안(`VehicleFocus()`) Space를 확인하기 전에 return**(`:60-69`) → 탑승해서 기다리는 동안·출발 연출 중에는 Space가 안 먹는다.

**규칙(결정)**
| 관전하는 사람 | 볼 수 있는 대상 | Space |
|---|---|---|
| **탈출한 쿠키**(탈출구·장치 탈것 탑승/대기) | 남은 일반 쿠키 — **스파이·괴물 제외** | 언제든 다음 대상(탈것을 보는 중이어도) |
| 스파이 로켓으로 탈출한 스파이 | 탈출한 쿠키와 같은 규칙(스파이·괴물 제외) | 같음 |
| **부서진 쿠키**(괴물에게 잡힘·마녀) | 남은 쿠키 — **스파이 포함**, 괴물 제외(지금과 같음) | 지금과 같음 |

**바꿀 것**
1. 순수 규칙 `Monster/SpectateRules.cs`(새): `enum SpectatorKind { Broken, Escaped }`, `static bool IsCandidate(SpectatorKind viewer, CharacterRole role, bool targetIsSpy, bool spectatable)` — 위 표 그대로. EditMode 시험.
2. `SpectatorController`
   - 관전 종류: 진입 때 받는다 — `EnterSpectatorMode(SpectatorKind kind)`로 바꾸고 세 호출부에서 넘긴다(부서짐·마녀 = `Broken`, `EscapeCharacterState` = `Escaped`). 마녀 처치는 이미 탈출한 사람만 빼고(`EscapeCharacterState.cs:81` — `HasEscaped`만 검사) **탈것에 타서 출발을 기다리던 쿠키는 죽을 수 있다** → 이미 관전 중이어도 `Broken`이 들어오면 종류를 바꾸고 후보를 다시 고른다.
   - `IsCandidate`에서 `SpectateRules.IsCandidate(kind, character.Role, RoomState.IsSpy(actor), character.IsSpectatable)` 사용. 기존 `SpectateTargets` 열거는 `SpectateRules`로 옮기고 프리팹 직렬화 값은 그대로 둔다(기본 `AliveCookies`에 해당).
   - `Update`: **`SpectateNextPressed`를 `VehicleFocus` 분기보다 먼저** 처리. 탈출한 사람이 Space를 누르면 탈것 보기를 끝내고(`vehicleDismissed = true`) 다음 쿠키로 — 그 뒤에는 탈것으로 다시 붙지 않는다. 누르지 않으면 지금처럼 탈것 → 출발 연출 → 쿠키 순서.
   - 볼 대상이 없으면(남은 사람이 스파이뿐 등) 지금처럼 마지막 위치에 머문다.
3. 관전 안내 `SpectatorLabel`(`Monster/SpectatorLabel.cs`)에 "Space 다음 사람" 줄 추가 — 한국어는 씬/SO에 `\uXXXX`로(관전 대상 이름 표시와 같은 오브젝트).
4. 점프 키와 같은 Space: 관전 중인 자기 쿠키는 숨겨진 키네마틱 상태라 점프가 일어나지 않는지 시험으로 확인(`HideOrSeekPlayer`).

**검증**: `SpectateRules` EditMode 시험(표 6칸). Play Mode(오프라인)는 다른 사람 캐릭터가 없어 실제 순환은 볼 수 없으므로, 탈출 처리 → `EnterSpectatorMode(Escaped)` → 탈것 보는 중 Space → `SwitchToNext` 호출과 탈것 해제까지 기록. 실제 순환(일반 쿠키만/부서지면 스파이 포함)은 두 사람 이상 멀티에서(남은 확인 목록).

## 10. 놀이공원 BoxCollider 때문에 막히는 곳

**지금(씬 확인)** 놀이공원 충돌체 중 BoxCollider:
| 출처 | 개수 | 원인 |
|---|---|---|
| 맵 `GameplayProps`의 작은 서커스 텐트 `CUR_CircusTent_Small_17·21·23·26` | 4 | `MapSceneBuilder`(`:264-274`)가 convex 껍질이 면 256개를 넘는 `GameplayProps`를 **메시 경계 상자**로 바꿈 → 원뿔 텐트의 빈 모서리(약 12×9×12 m 상자)가 투명 벽이 된다 |
| 제가 V3·V4에서 넣은 `TwistedProps` 소품 | 37 | `TwistedPropBuilder.AddColliders` = 부품마다 메시 경계 상자 + **최소 높이 2.6 m 투명 확장**(쿠키가 올라서지 못하게) → 떨어진 말 머리·바람 빠진 풍선·광대 간판 기둥 사이 등 빈 곳까지 막음 |
| 탈출 상자 자리·문짝 | 26 | 정상(상자 모양) |

그 밖에 convex MeshCollider 73개(볼록 껍질이라 차양·아치 아래 빈 곳을 메울 수 있음).

**바꿀 것**
1. 맵 빌더: `GameplayProps`에서 껍질 한도를 넘으면 상자 대신 **오목 MeshCollider**(정적이라 허용)로 — `MapSceneBuilder.cs:266-274` 수정. 지금 씬은 맵을 다시 만들지 않고 메뉴 `Fix Prop Box Colliders`로 해당 4개만 바꾼다(5개 맵 모두 같은 규칙으로 검사).
2. 내 소품: 부품마다 **자기 메시 그대로의 MeshCollider**(정적 오목)로 바꾸고 2.6 m 투명 확장은 없앤다. 대신 낮은 소품 위에 쿠키가 올라서는 문제는 11번(괴물이 위의 쿠키도 잡음) + 통행 검사로 막는다. 숨을 곳(`COL_*` 상자)은 벽·가구가 실제 상자 모양이라 그대로.
3. convex 73개 점검: 메뉴 `Report Convex Gaps`가 각 볼록 충돌체 안에서 "원래 메시 밖인데 바닥에서 2 m 이상 빈 곳"(차양 아래·아치 아래)을 격자로 찾아 목록을 낸다 → 목록에 나온 것만 오목으로 바꾼다(움직이는 회전목마·관람차는 제외).

**검증**: `MapCompactTests`(통행 검사) + `TwistedTests`(소품 충돌체 규칙을 새 규칙으로 고침) 통과, Play Mode에서 막히던 곳(텐트 모서리·소품 사이)을 `MapPlaytestDriver`로 지나가기.

## 11. 구조물 위 쿠키를 괴물이 못 잡음

**지금 코드**: `MonsterGrabKillTrigger.FindAimTarget`(`Monster/MonsterGrabKillTrigger.cs:35-73`)
- 화면 가운데에서 구(반지름 `GrabAimRadius`)를 쏘아 **쿠키보다 먼저 맞은 아무 물체가 있으면 막힘**(`:55 blockDistance`, `:64`). 쿠키가 구조물 위에 서 있으면 아래에서 올려다보는 시선이 **구조물 모서리·윗면에 먼저 맞아** 막힌다.
- 거리 조건은 수평만(`:68` — 몸 앞면 + `GrabReachFromFront` 2 m = 중심에서 3.87 m). 넓은 지붕 가운데에 서면 수평 거리 밖이다.
- 통행 검사(`MapPassabilityCheck`)는 쿠키가 **1.8 m**까지 올라간다고 가정한다(`:18 CookieRise`). 실제 점프가 더 높으면 검사에서 "쿠키만 닿는 곳"이 빠진다 — 테스트는 통과했지만 실제로는 올라갈 수 있는 구조물이 있다는 뜻.

**바꿀 것**
1. 잡기 판정 보강: 구에 맞은 쿠키가 막힘 거리보다 뒤에 있어도, **괴물 눈(eyeSocket)에서 쿠키 가슴까지 직선이 비어 있으면** 잡을 수 있다(자기·쿠키 충돌체 제외한 `Linecast`). 높이 상한은 괴물 키 기준 `MaxGrabHeight`(GameSettingsSO, 기본 6 m — 괴물 발 기준 쿠키 발 높이) 추가.
2. 실제 점프 높이 측정(Play Mode에서 쿠키 점프 최고점, 달리기 점프 포함) → `MapPassabilityCheck.CookieRise`를 실측값으로. 그러면 통행 검사가 실제로 올라설 수 있는 곳을 찾는다.
3. 그래도 "쿠키만 닿는 곳"(괴물이 수평 3.87 m 밖)이 남는 구조물 윗면은, 그 윗면 위에 **보이지 않는 경사 지붕 충돌체**(쿠키가 미끄러져 내려옴)를 놓는 도구(`Fix Perches`, 맵별 표 — 자동 검출 결과로 채움)로 막는다.

**검증**: EditMode — 통행 검사 실측 높이로 5개 맵 통과. Play Mode(놀이공원) — 쿠키를 구조물 위(검출된 자리)에 두고 괴물 조준 → `HasGrabAimTarget` 참 → 잡기 성공 기록.

---

## 12. 단계

| 단계 | 내용 | 결과물·검증 | 상태 |
|---|---|---|---|
| R0 | §13 결정 | 이 문서 갱신 | ✅ 2026-10-09 |
| R1 | 1·2 상자·장치 길게 누르기 + 상자 여는 소리·획득 소리(C) + 획득 소리 지연(A) | 코드·프리팹·음원, EditMode 시험, Play Mode 사진·소리 기록 | ✅ 2026-10-09 — §15 R1 |
| R2 | 3·4·7(소리 부분) 로비 곡·캔디숲 로켓 3개·동굴 소리 교체 + **결과 화면 곡 후보 제작(넣지 않음)** | 음원 교체(백업), 청취 묶음(결과 곡은 징글 3개에 이어 붙인 묶음), Play Mode 재생 기록 | ✅ 2026-10-09 — §15 R2(결과 곡은 R7 승인 대기) |
| R3 | 5·6·8 대기실 마녀 집·베이커리 오븐 50%·회전목마 방향 | 전후 사진·밝기 수치 | ✅ 2026-10-09 — §15 R3 |
| R4 | 7(원인)·9 관전 소리·관전 대상 규칙(탈출 = 스파이·괴물 제외, 부서짐 = 스파이 포함)·Space 전환 | 재현 기록, 규칙 시험, Play Mode | ✅ 2026-10-09 — §15 R4(실제 순환은 멀티 확인 목록) |
| R5 | 10·11 놀이공원 충돌체·잡기 | 도구·빌더 수정, MapCompactTests·TwistedTests, Play Mode | ✅ 2026-10-09 — §15 R5 |
| R6 | 전체 테스트·Play Mode·빌드·문서 | 테스트 결과, 빌드 한 판, README·관련 문서 갱신 | ✅ 2026-10-09 — §15 R6 |
| R8 | 청취 의견(2026-10-09): 탑승·탈출 성공·스파이 로켓·상자 소리 싱크·결과 징글·상자 z-fighting | 음원 교체, Play Mode 기록, 상자 면 간격 측정·사진 | ✅ 2026-10-09 — §15 R8 |
| R9 | 괴물 잡기 소리 그로테스크(후보 3개 → 3번째 승인 2026-10-10) | 음원 교체, Play Mode 소리 시점·사진 | ✅ 2026-10-10 — §15 R9 |
| R10 | 괴물 발소리 질척·그로테스크(후보 승인 2026-10-10) | 음원 교체, Play Mode 걷기 기록 | ✅ 2026-10-10 — §15 R10 |
| R7 | (결과 곡 승인 뒤) `MusicId.Result` 추가·규칙·시험·카탈로그 | Play Mode 결과 단계 재생 기록 | ✅ 2026-10-11 — §20.3(R2 후보 대신 새 후보 `Result_A`) |

순서 이유: R1은 코드 변경이 가장 크고 다른 것과 겹치지 않는다. R5는 충돌체를 바꾸므로 통행 테스트를 마지막에 한 번 더 돌린다. R7은 결과 곡 청취 승인 뒤에만.

## 13. 결정 — ✅ 2026-10-09
| # | 질문 | 결정 |
|---|---|---|
| Q1 | 9번 "스파이, 몬스터 제외" | **A** — 탈출한 쿠키의 관전 대상에서 스파이·괴물 제외, Space로 남은 일반 쿠키 사이를 돈다. **부서진 쿠키의 관전 대상에는 스파이 포함**(괴물은 제외 — 지금과 같음) |
| Q2 | 2번 상자에서 재료를 받을 때의 획득 소리 | **C 다음 A** — `ItemPickup`을 음정 없는 소리로 다시 만들고, 상자에서 받을 때는 뚜껑 소리 뒤 0.45초에 낸다 |
| Q3 | 결과 화면 곡 | 로비 곡을 쓰지 않고 **결과 화면 전용 곡을 따로 제작** → 들어 보고 승인 후 넣기(R7) |
| Q4 | 길게 누르기 범위 | **상자와 장치만**(스파이 로켓은 장치와 같은 규칙으로 봄). 장치 탑승·문·줍기는 즉시 — 탑승을 2초로 할지는 진행 중 바꿀 수 있음(값 하나) |

## 14. 위험
| 위험 | 대응 |
|---|---|
| 소품 충돌체를 메시 그대로 바꾸면 쿠키가 낮은 소품 위에 올라감 | 11번 잡기 보강 + 실측 점프 높이 통행 검사 + 경사 지붕 |
| 오목 MeshCollider 증가로 물리 비용 | 정적 오목은 비용이 작다. 메시는 로우폴리(소품 500~3,000 삼각형). 프레임 확인 |
| 대기실 조명 추가로 프레임 | 그림자 없는 스포트 3개, 점광원 총 17 → 20 |
| 상자 누르는 동안 다른 사람이 먼저 엶 | 방장 판정은 그대로(먼저 도착), 취소 처리 |
| 장치 넣기가 2초로 길어져 탈출 속도가 느려짐 | 실제 플레이 뒤 시간 조정(직렬화 값). 빼앗기도 같은 2초라 공정 |
| 관전 종류를 진입 때 넘기면서 호출부를 빠뜨림 | 세 호출부 모두 바꾸고, 인자 없는 옛 메서드는 지운다(컴파일 오류로 잡힘) |
| 회전목마 방향 판단 착오 | 위에서 찍어 말 머리 방향을 먼저 확인 |

## 15. 결과 기록

### R1 ✅ 2026-10-09
- 코드: `IHoldInteractable`·`HoldProgress`(새 파일)·`CharacterInteractor` 길게 누르기·`InteractionPromptUI.SetProgress` + 프리팹 `ProgressRing`(Filled Radial360, 위에서 시계 방향). 상자 2초(스파이 상자는 스파이가 아니면 0 → 원래 동작), 장치 설치·훔치기 2초, 장치 탑승·로켓 탑승은 바로. `EscapeAudioPresenter`가 상자에서 나온 물건 획득 소리를 0.45초 늦게 냄.
- 시험용 에디터 전용 입력 `PlayerInput.EditorInteractPress/Held`(빌드에는 없음). 누름은 GetKeyDown처럼 **그 프레임의 모든 읽기**에 보이도록 함(처음엔 한 번 읽으면 지워져 괴물 잡기 검사에 먹혔음 → 고침).
- 음원: `ItemPickup`(종이·천·나무, 음정 없음, −27.1 LUFS), `ChestOpen`(걸쇠·삐걱·쿵·먼지, −17.2 LUFS) 교체, 이전 파일은 `Source~/s8_backup/`, 원본 `Source~/r1009/final/`.
- EditMode: `Request1009Tests` 2/2 통과. 컴파일·콘솔 깨끗.
- Play Mode(놀이공원, 프레임 단위 기록):
  - 상자 계속 누름: 링 0.01 → 1.00이 0.2초에 0.1씩, **2.00초**에 열림·물건 손에 쥠.
  - 1초에 뗌: 링 0.30에서 꺼지고 상자 그대로(물건 안 나옴).
  - 장치 설치: 2.00초 누른 뒤 칸 3 → 4.
  - 소리 기록: `ChestOpen` 12.60 → `ItemPickup` 13.05(**+0.45초**), `DeviceInsert` 정상.
  - 화면: `images/r1009/r1_ring.jpg`(E 아이콘 둘레 노란 링이 채워지는 중).

### R2 ✅ 2026-10-09
- 스크립트 `request1009_sound.py r2`(공용 도구 `twisted_final.py` — 바퀴 길이가 정수 초가 아닐 때 바탕 질감 샘플 수를 맞추도록 한 줄 고침).
- **로비 곡** `BGM/Lobby.ogg`: 오르골 주선율을 빼고 **낮게 친 피아노 + 첼로 같은 낮은 현**이 선율을 받는 D단조 왈츠(3/4 76, A16–B16–A′8, 94.7초, −18.0 LUFS, 이음매 0.0010). 오르골은 A′에서 멀리 늘어진 메아리로만, B에 작은 합창. Play Mode LobbyScene: `current=Lobby`, 덱 클립 `Lobby 94.7s` 재생 중, 콘솔 깨끗.
- **캔디숲 로켓**(음정·반짝이 없음, 크기는 S8과 같음): `CakeRocketRise` 3초 = 모터 떨림(잡음) + 금속 걸림 3번 + 부스러기 + 김 빠짐(−13.8/S8 −13.5 LUFS) · `CakeIgnite` 2초 = 도화선 0.5초 → 점화 '푸웅' → 불꽃 커지는 차오름이 2초 발사로 이어짐(−15.3) · `CakeLaunch` 5초 = 추진 굉음이 커졌다가 고역부터 줄며 멀어짐 + 끝 메아리(−18.7). Play Mode 캔디숲 장치 완성·탑승·출발: `CakeRumble → CakeCrack → CakeBurst → CakeRocketRise`, 출발 `CakeIgnite → CakeLaunch` 모두 재생 기록.
- **동굴** `AmbCave`: −26 → **−22 LUFS**, 잔향 밖에 또렷한 물방울 22개 추가(60초 반복).
- 교체 5개: 바로 앞 파일 `Source~/r1009/before/`, S8 원본 `Source~/s8_backup/`(없을 때만), 새 원본 `Source~/r1009/final/`.
- **결과 화면 곡 후보(넣지 않음)**: A단조 → C장조로 끝나는 3/4 66, 24마디 65.5초, −20.0 LUFS, 이음매 0.0023 — 셀레스타 선율 + 아주 작은 오르골 겹침 + 현 패드 + 하프. `Source~/twisted/review/Result_candidate.ogg`, 청취 묶음 `reel_Jg{EscapeSuccess,EscapeFail,MonsterWin}_to_Result.ogg`, 교체 소리 묶음 `reel_R2.ogg`. → 승인되면 R7.

### R3 ✅ 2026-10-09
- **대기실 마녀 집**(`TwistedAtmosphere`): 안개 0.03 → 0.012, 노출 +0.3 → +0.5, 등불 배율 0.75 → 1.0(V1이 이미 들어간 씬에서 한 번만 되돌림 — 등불 14개, 깜빡이 기준 강도도 같이). `TW_WitchHouse` 아래 앞(주황)·양옆(보라) 바닥 스포트 3개(범위 25 m, 75°, 그림자 없음) + 지붕 달빛 스포트 1개. 창문 재질 2개는 `Assets/Maps/GameLobby/Materials/*_Lobby.mat` 사본(발광 ×1.8)을 집에만(12칸). 새 메뉴 `Tools/TagOfChaos/Maps/Apply Game Lobby Look`(전체 메뉴에도 포함).
  - 집 영역 평균 밝기(출발점 방향): 25 m 0.039 → **0.155**, 45 m 0.025 → **0.115**, 65 m 0.013 → **0.062**. 사진 `images/r1009/r3_lobby_before_after.jpg`(왼쪽 전, 오른쪽 후).
- **베이커리 오븐**(`OvenSequence.BrightnessScale = 0.5`): 점광원 세 항 모두 ×0.5, `Oven_Light` 발광은 이 렌더러만 MaterialPropertyBlock으로 ×0.5(공유 `ME_Glow_White` 그대로), 출발 섬광 입자 120 → 60. Play Mode(문이 다 열린 뒤 같은 자리): 점광원 6.50 → **3.25**, 화면 평균 0.642 → 0.493, 하얗게 날아간 화소 1.9% → 0%. 사진 `images/r1009/r3_oven_before_after.jpg`.
- **회전목마**: 앞에서 찍어 보니 앞쪽 말 머리가 −X(화면 오른쪽)인데 +Y 회전은 앞쪽을 +X로 보냄 → 거꾸로 확인. `MapSceneBuilder.CarouselSpinSign = −1`, 메뉴 `Fix Carousel Direction`으로 클립만 다시 씀(0 → −360°, 40초, 반복). Play Mode: 5초에 yaw 310.9° → 265.7°(−9°/초), 앞쪽 점이 −X로 이동 = 말 머리 방향. 콘솔 깨끗.

### R4 ✅ 2026-10-09
- **§7 지하 소리 원인 확인**:
  1. 지하 바닥 격자 검사(1 m, 땅 아래에서 쿠키가 설 수 있는 바닥 2,353칸 중 지형 아랫면·땅 위 모서리 제외): **모두 `AMB_Underground` 안**. 영역은 고칠 곳 없음. 회귀 시험 `TwistedTests.Gingerbread_UndergroundFloorsAreInsideCaveZone` 추가.
  2. Play Mode(진저브레드 오프라인): 지하 (−34, −7.4, 0)에 빈 대상을 두고 관전과 같은 `Camera_Ctrl.SetFollowTarget`으로 따라감 → 듣는 위치 (−34, −5.8, 0), `AMB_Underground` 안·재생 중(`AmbCave` 음량 0.55), 바탕음 꺼짐. **마녀에게 죽어 관전 모드에 들어간 뒤에도 같은 결과**.
  3. 그래서 관전 경로·영역은 정상이고, 남은 원인은 **동굴 소리가 작고 물방울이 묻힌 것** → R2에서 `AmbCave` −26 → −22 LUFS + 또렷한 물방울 22개로 고침. 실제 다른 사람을 따라 내려가는 확인은 멀티 확인 목록에 남긴다.
- **관전 규칙**: `Monster/SpectateRules.cs`(새) — `SpectatorKind { Escaped, Broken }`, `IsCandidate`(탈출 = 일반 쿠키만, 부서짐 = 스파이 포함, 괴물은 부서짐 + 프리팹 설정일 때만), `Merge`(부서짐이 이김). `SpectatorController.EnterSpectatorMode(SpectatorKind)` — 부서짐 3곳(`HideOrSeekPlayer` 잡힘·마녀, `CookieLifeStatePresenter`)은 `Broken`, `EscapeCharacterState`는 `Escaped`. 이미 관전 중에 `Broken`이 오면 종류만 바꾸고 보던 대상이 후보가 아니면 넘김.
- **Space**: 탈것 분기보다 먼저 읽음 → 탈것을 보는 중 Space = 탈것 보기 끝(`vehicleDismissed`) + 다음 사람. 탈것을 보는 동안 안내 `SpectatorLabel.vehicleText` = "탈것을 보는 중 (Space: 다른 사람 보기)"(`GameSceneCore.prefab`, `\uXXXX`로 입력). 관전 중인 자기 쿠키는 `IsMovementLocked`라 Space가 점프로 가지 않음(코드 확인).
- 덤으로 고침: 탈것에서 기다리던(키네마틱) 쿠키가 마녀에게 죽을 때 "Setting linear velocity of a kinematic body" 경고 → `HideOrSeekPlayer` 두 곳에서 키네마틱이면 속도를 건드리지 않음.
- 시험: `Request1009Tests` 10/10(관전 표 6칸 + 괴물·숨김 + 병합), `TwistedTests` 19/19. 에디터 전용 `PlayerInput.EditorSpectateNextPress` 추가.
- Play Mode(놀이공원, 프레임 기록): 탑승 대기 0.33초 `kind=Escaped, vehicle=True, follow=Car_0, 안내 보임` → 0.51초 Space → `vehicle=False, dismissed=True`(남은 사람이 없어 마지막 자리에 머묾, 안내 숨김) → 2.50초 대기 중 마녀 → `kind=Broken`. 콘솔 깨끗.

### R5 ✅ 2026-10-09
- **맵 물체 상자 → 오목**: `MapSceneBuilder` — 껍질 한도(면 256)를 넘은 메시는 분류와 상관없이 정적 오목 MeshCollider(메시 경계 상자를 만들지 않음). 메뉴 `Fix Prop Box Colliders`로 지금 씬을 고침: 놀이공원 `CUR_CircusTent_Small_17·21·23·26` 4개(다른 맵 0개).
- **내 소품**: `TwistedPropBuilder.AddColliders` — 부품마다 자기 메시 그대로의 오목 MeshCollider, 2.6 m 투명 확장 삭제(`MinColliderHeight` 없앰). 숨을 곳의 `COL_*` 상자는 그대로. 프리팹 21개 다시 만듦 → 5개 맵 소품 상자 0개(놀이공원 메시 13·COL 상자 24).
- **볼록 껍질 빈 곳**: `Editor/Maps/ConvexGapScanner.cs`(새) — 껍질 안인데 실제 메시로는 빈(바닥 위 2 m 이상 비고 닫힌 덩어리 속이 아닌) 칸 면적을 잰다. 메뉴 `Report Convex Gaps`·`Fix Convex Gaps`. 5개 맵에서 133개가 걸렸지만 집·나무·컵케이크는 바꾸면 괴물이 못 들어가는 숨을 곳(문 안·가지 밑)이 생기므로 **검토한 목록만** 고침: 놀이공원 네온 부스 17개(차양이 카운터보다 넓어 껍질이 옆 통로까지 비스듬히 막음) + 풍선 다발 2개. 소나무 9개는 제외(가지 밑 숨을 곳). 맵 빌더도 같은 목록 규칙.
- **괴물 잡기**(`MonsterGrabKillTrigger`): 조준선이 쿠키보다 먼저 구조물에 맞아도, 괴물 **눈 또는 손 높이(1.6 m)**에서 쿠키 가슴까지 직선이 비어 있으면 잡음. 높이 상한 `GameSettingsSO.MaxGrabHeight` = 6 m(쿠키 발 − 괴물 발). 수평 거리 규칙은 그대로.
- **점프 높이 실측**: 쿠키 `jumpPower` 6, 중력 −9.81 → 최고 **1.78 m** = 통행 검사의 `CookieRise` 1.8 m와 같아 그대로 둠. 통행 검사에서 쿠키만 닿는 3 m² 이상 무리가 5개 맵 모두 0개라 `Fix Perches`(보이지 않는 경사 지붕)는 만들지 않음.
- 통행 검사(놀이공원): 쿠키가 닿는 칸 65,605 → **66,748**, 괴물이 닿는 칸 46,935 → **48,593**, 쿠키만 닿는 칸 30(무리 0). 다른 4개 맵도 깨끗.
- 시험: `MapCompactTests` 25/25, `TwistedTests` 20/20(소품 충돌체 규칙을 "자기 메시 오목"으로 바꿈 + `Carnival_NoBoundingBoxPropsAndListedHullGapsAreConcave` 추가), `Request1009Tests` 11/11(`Grab_ReachIsHorizontalWithHeightCap` 추가).
- Play Mode(놀이공원):
  - 부스 카운터 위(1.67 m, 차양 아래) 쿠키 + 3.5 m 앞 괴물: 조준선이 0.72 m에서 부스에 먼저 맞음 → **이전 규칙은 막힘**, 새 규칙 `HasGrabAimTarget = True` → 잡기 → 쿠키 부서짐. 사진 `images/r1009/r5_grab_counter.jpg`(괴물 눈에서는 간판에 가려 다리만 보임 — 그래서 손 높이 선을 더함).
  - 5 m 벽 너머 3.5 m 쿠키: `HasGrabAimTarget = False`(벽 너머로는 여전히 못 잡음).
  - 쿠키 자동 걷기(`MapPlaytestDriver`)로 텐트 17·26의 예전 상자 모서리를 대각선으로 통과: 둘 다 도착, 다시 찾기 0번(7.7 m·7.2 m).
  - 콘솔 깨끗(도구 실행 중 Unity가 내는 "Convex Mesh … partial hull" 경고 1건은 껍질 한도 검사용 굽기에서 나온 것 — `Fix Prop Box Colliders`에서 다시 굽지 않도록 고침).

### R6 ✅ 2026-10-09
- EditMode 전체 **215개 중 209개 통과**. 실패 6개는 V6 때와 같은 `BuildSceneTests.ReleaseScene_HasNoDevScripts` — 대기실·5개 맵 씬에 저장된 사용자의 시험용 `GameObject`(OfflineModeBootstrap)라 손대지 않음(README 남은 확인).
- Play Mode 점검(대기실 + 5개 맵, 오프라인, 추격 단계): 모두 방 입장, 콘솔 오류·경고 0. 대기실 곡 `GameLobby`, 맵 바탕 환경음 켜짐(공장은 바탕음 없음 — 원래 설계).
- 빌드 `Builds/R1009Check/`(Windows 64, 씬 7개): **성공**, 오류 0, 경고 2(셰이더 `pow` 경고·PUN 재컴파일 알림 — 이번 변경과 무관), 319 MB, 53초. 실행 20초: 로비 진입·서버(kr) 연결, 로그 예외 0.
- 문서: README의 `Request1009Plan.md` 행 ✅, 남은 확인에 결과 곡 청취·멀티 관전 확인·대기실 조명 추가.
- **R7(결과 화면 곡 넣기)은 청취 승인 뒤** — 후보 `Assets/10. Audio/Source~/twisted/review/Result_candidate.ogg`, 묶음 `reel_Jg*_to_Result.ogg`.

### R8 ✅ 2026-10-09 (청취 의견 반영)
- 사용자 의견: 로비 곡은 이대로 확정. 캔디숲 로켓 탑승 소리·스파이 로켓·게임 끝 소리가 유아틱, 상자 소리가 열림과 안 맞음, 상자 z-fighting.
- **원인**: 탑승 `BoardHop`(마림바 도·미·솔)·`EscapeSuccessSelf`(마림바+셀레스타+반짝이)·스파이 로켓 `RocketInsert`(스프링 '뾰잉')·`RocketLiftoff`(휘파람처럼 오르는 음)·결과 징글 3개(오르골·셀레스타·하프·오르골 웃음)가 장난감 음색. 상자 뚜껑은 상태가 바뀌는 프레임에 바로 젖혀지는데(`MaterialChest`) R1 소리의 '쿵'이 0.56초 뒤.
- **교체**(`request1009_sound.py r8`, S8과 같은 크기):
  - `ChestOpen`: 걸쇠·뚜껑 쿵을 0초(시작 0.016초)에, 삐걱·먼지는 짧게. 상자에서 나온 물건 줍는 소리 지연 0.45 → **0.3초**(`EscapeAudioPresenter.ChestPickupDelay`).
  - `BoardHop`(탈것 공용): 금속 발판 → 좌석 쿵 → 안전 바 철컥. `RocketHatch`: 무거운 해치 손잡이 → 기압 → 닫힘. `RocketInsert`: 금속 끼움 + 래칫.
  - `EscapeSuccessSelf`: 낮은 현 A 5도 + 멀리 종 하나 + 바람이 걷힘.
  - 스파이 로켓 `RocketIgnite`·`RocketLiftoff` = 캔디숲 `CakeIgnite`·`CakeLaunch`(같은 2초 점화 → 발사 시간표).
  - 결과 징글: `JgEscapeSuccess` 낮은 현 Am→F→C + 금관 + 종 · `JgEscapeFail` 내려가는 현·콘트라베이스 + 멀리 종 + 바람 · `JgMonsterWin` 금관 B♭→A + 큰 북 + 합창 + 짐승 숨(오르골 웃음 삭제). 각 5초 −16 LUFS.
  - 청취: `Source~/twisted/review/reel_R8.ogg`(이전/새 번갈아, 순서 `reel_R8_order.txt`), 결과 곡 후보 묶음 `reel_Jg*_to_Result.ogg`도 새 징글로 다시 만듦.
- **상자 z-fighting**(`CHEST.fbx`): 받침이 몸통 옆면과 같은 평면(간격 0)이고 띠·뚜껑 테두리가 5 mm 안쪽. `EscapeModelImportPostprocessor`(버전 4)가 임포트 때 부품별로 키움 — 받침 수평 ×1.010, 몸통 띠 ×1.012, 몸통 걸쇠 ×1.022, 뚜껑 껍질 ×1.022, 뚜껑 띠 ×1.034(+아래로 4 mm), 뚜껑 걸쇠 ×1.060(+앞으로 6 mm), 기준점은 껍질 바닥 가운데. 처음엔 법선 방향으로 띄웠는데 각진 모서리가 갈라져 선이 보여 부품별 배율로 바꿈.
  - 측정: 서로 다른 부품의 같은 방향 면이 4 mm 안으로 겹치는 곳 270쌍 → 보이는 면에서는 0(바닥 밑 면만 남음). 사진 `images/r1009/r8_chest_after.jpg`(가까이서 갈라짐·깜빡임 없음), 3 m·10 m에서도 깨끗.
- **Play Mode**:
  - 상자: 뚜껑이 젖혀진 프레임 2270에 `ChestOpen`, 0.3초 뒤 `ItemPickup`.
  - 캔디숲 한 판: 탑승 `BoardHop`·`EscapeSuccessSelf` → `CakeIgnite` → `CakeLaunch` → 결과 징글 `JgEscapeSuccess`(5.00초 새 클립).
  - 스파이 로켓: `RocketInsert`×2 → 탑승 `RocketHatch`·`EscapeSuccessSelf` → `RocketIgnite` → `RocketLiftoff` → `JgEscapeSuccess`.
  - 콘솔 깨끗. `AudioTests` 58/58, `Request1009Tests` 11/11.

### R9 ✅ 2026-10-10 (괴물 잡기 소리)
- 의견: 잡을 때 소리가 부실함 → 촉수로 잡는 그로테스크. 후보 1(빨판 팝 위주)은 "공포와 안 어울림", 후보 2(낮은 충격·불협 현·숨), **후보 3(후보 2 + 목구멍 꾸르륵·질척 짓이김·힘줄처럼 늘어나는 촉수·반죽처럼 찢기는 과자·가루를 빨아들이고 삼킴) 승인**. 피·내장 소리는 없음(고어 기준). 스크립트 `request1009_sound.py r9c`, 청취 `Source~/twisted/review/reel_R9c_grab.ogg`(후보 2·3 원본 `Grab_candidates_horror/`·`Grab_candidates_grotesque/`).
- 교체(S8과 같은 크기): `MonsterGrab` 1.1 → **1.55초**(잡기 애니메이션 1.1~1.53초의 무음 구간을 채움) · `MonsterSquash` 0.45 → 0.8초 · `CookieCrumble` 1.0 → 1.8초(빨아들임·꿀꺽·여운). 이전 파일 `Source~/r1009/before/`.
- **`CookieGrab`은 되돌림**: 괴물 잡기가 아니라 다른 쿠키가 들어 올릴 때 나는 소리(`CookieAudio` — 괴물 처형 중에는 내지 않음)라 촉수 소리를 넣으면 안 됨. 처음 분석에서 잘못 묶었던 것. 원래 파일로 복구(0.13초).
- Play Mode(놀이공원, 괴물이 3.2 m 앞 쿠키 조준 → 잡기): `MonsterGrab` 0초 → 애니메이션 진행 0.58에서 `MonsterSquash`(0.80초 클립) → 0.63에서 `CookieCrumble`(1.80초 클립) → 애니메이션 끝. 사진 `images/r1009/r9_grab_play.jpg`(들림 → 눌림). 콘솔: 이번 변경과 무관한 에디터 내부 경고(JobTempAlloc) 1건 외 깨끗. `AudioTests` 58/58.

### R10 ✅ 2026-10-10 (괴물 발소리)
- 의견: 걷는 소리가 "툭툭"뿐 → 질척·그로테스크. 지금 구조(0.5초마다 `MonsterStep_1~3` 중 무작위, `MonsterAudio.WalkStepInterval`)는 그대로, 3종만 새로(각 0.5초, S8과 같은 크기): 묵직한 쿵 + 질척 짓눌림 + 발밑 과자 부스러기 → 끈적한 바닥에서 촉수 발이 떼어짐(공명이 끌려 올라가는 '쩌억', 음정 있는 팝 없음) → 걸음 사이 낮게 끌림, 2번에만 목구멍 꾸르륵. 스크립트 `request1009_sound.py r10`, 청취 `Source~/twisted/review/reel_R10_steps.ogg`·`Step_candidates/`. 이전 파일 `Source~/r1009/before/`.
- 시험용 에디터 전용 입력 `PlayerInput.EditorMove`(이동 키를 누른 것처럼, 빌드에는 없음) 추가.
- Play Mode(놀이공원, 괴물 앞으로 걷기 7초): 상태 `Walk` 진입과 동시에 첫 발소리, 이후 **0.50초 간격 10걸음**, 3종이 무작위로 섞임(2·3·2·3·3·1·1·1·3·2), 클립 0.50초. 본인 괴물이라 2D(다른 사람 화면에서는 같은 클립이 몸을 따라가는 3D). 콘솔: 에디터 그래프 창 내부 NullReferenceException(`UnityEditor.Graphs.Edge.WakeUp`, 게임 코드 아님) 외 깨끗.

---

# 16. 2026-10-11 추가 요청 8건 계획 (단계 N0~N9)

- 작성: 2026-10-11 · 상태: **✅ 완료 N0~N9 (2026-10-11)**
- 근거: 아래 "지금 코드"는 D: 프로젝트(`D:\3DClaudeTagOfChaos\TagOfChaos`)의 실제 소스·프리팹·애니메이션·씬을 Unity에서 열어 확인한 값이다. 줄 번호는 작성 시점 기준. 참고 사진 `CookieCarrying.png` 확인함.
- 작업 규칙은 위와 같다(컴파일·콘솔 → Play Mode → 단계 끝나면 §16.9 표에 ✅, 한국어 UI는 `\uXXXX`, 원본 음원은 `Source~`).

## 16.0 요청 목록
| # | 요청 | 단계 |
|---|---|---|
| 1 | 대기 시간에는 상자가 안 보이다가 끝나면 나타남 | N1 |
| 2 | 탈출용 아이템(재료)을 들면 두 손으로 드는 모션 | N4 |
| 3 | F로 다른 쿠키를 들 때 공중에 떠 보임(특히 달릴 때) → 두 손으로 안아 들기 | N3 |
| 4 | 아이템을 얻으면 인벤토리 칸에 아이템 그림 | N2 |
| 5 | 들고 걷기·달리기 모션 없음 → 한 손 아이템은 오른손에 쥐고, 두 손 아이템은 쿠키 들기 모션 | N4 |
| 6 | 설정에 소리 출력 장치(스피커·헤드폰·헤드셋) | N6 |
| 7 | 베이커리 탈출구 주변: 열리기 전엔 고장 난 소리 → 열리면 지금 소리 | N7 |
| 8 | 어두운 분위기 작업 때 생긴 시야 감소(화면 가장자리 어둡게) 제거 | N5 |

## 16.1 상자 — 대기 시간에는 숨기기 (요청 1)

**지금 코드**
- 상자는 방장이 판을 시작하면(`EscapeManager.CanInitialize` — `PaintPhaseEndTime`과 괴물이 정해진 순간, `Escape/EscapeManager.cs:134-135`) 상태가 만들어지고, `RefreshViews`(`:190-201`)가 곧바로 `MaterialChest.Show`(`Escape/MaterialChest.cs:32-39`)로 켠다. 그래서 **색칠(변장) 시간부터 상자가 보인다**.
- 이미 "행동 가능" 판정은 있다: `EscapeManager.ActionsAllowed => GamePhaseState.Current != GamePhase.Paint`(`:75`). 상자 `CanInteract`도 이것을 본다(`MaterialChest.cs:58-59`) — 보이기만 하고 못 여는 상태다.
- 단계 해석은 `Core/GamePhaseState.cs`: Lobby(판 전) → **Paint(변장 시간)** → AwaitingMonster → Hunt.

**바꿀 것**
1. `EscapeManager`에 `ChestsVisible => ActionsAllowed` 기준을 두고, `Update`에서 단계가 바뀐 순간(이전 값과 비교, 프레임마다 RoomProps를 다시 읽지 않도록 0.2초 간격) `RefreshViews()`를 다시 부른다. 모든 클라이언트가 각자 계산 — 네트워크 변경 없음.
2. `RefreshViews`: `ChestsVisible`이 거짓이면 모든 상자 `Hide()`(상태·재료 배치는 그대로, 보이기만 끔).
3. `MaterialChest.Show`에 "처음 나타남" 연출: 0.35초 동안 크기 0→1.08→1(살짝 튀어나옴) + 작은 먼지 파티클. 이미 보이던 상자(뚜껑 열림 갱신 등)에는 연출 없음. 판 중간에 들어온 사람은 연출 없이 바로 보임.
4. 나타나는 소리: 결정 질문 Q2.

**검증**: EditMode 시험(단계별 `ChestsVisible`). Play Mode — 방을 시작 상태로 만들고(Paint 종료 시각을 미래로) 상자 0개 보임 → 시각을 지나게 하면 상자 전부 보임·E 2초로 열림. 전후 사진.

## 16.2 인벤토리 칸 아이템 그림 (요청 4)

**지금 코드**
- HUD는 이미 그림을 지원한다: `EscapeHud.RefreshHotbar`(`Escape/EscapeHud.cs:265-283`)가 `item.Icon`이 있으면 그 그림, 없으면 흰 사각형에 `item.Tint` 색을 칠한다.
- **아이템 19개 전부 `icon = null`**(`Assets/03. SO/Escape/Items/Item_*.asset` 확인). 모델 프리팹(`ITEM_*`)은 모두 있다. 그래서 지금은 칸에 색 사각형만 보인다.

**바꿀 것**
1. 에디터 도구 `Assets/Editor/Escape/ItemIconBaker.cs`(새): 각 `ItemSO.ModelPrefab`을 빈 미리보기 장면(`PreviewRenderUtility`)에 놓고 3/4 각도·부드러운 조명·투명 배경으로 256×256 렌더 → PNG `Assets/08. Resources/ItemIcons/Icon_{ItemId}.png`(Sprite로 가져오기) → `ItemSO.EditorSetVisuals(sprite, 모델)`로 연결. 메뉴 "Tools/Escape/Bake Item Icons". 다시 실행하면 덮어쓰기.
2. 같은 모델을 색만 바꾼 것(알사탕 전지 4색·룬 4색)도 각자 렌더하므로 색이 그대로 보인다.
3. 칸 그림 자리가 정사각형이 아니면 `preserveAspect = true`.

**검증**: 도구 실행 뒤 19개 모두 icon 연결 확인(EditMode 시험: 모든 ItemSO에 icon이 있음). Play Mode에서 상자를 열어 재료·도구를 얻고 칸 스크린샷(그림 크기·색 확인). 그림 19개 한 장 모음 사진.

## 16.3 쿠키 들기 — 공중에 떠 보이는 문제 (요청 3)

**지금 코드 — 원인 3가지**
1. **붙는 위치가 손이 아니라 등 뒤 위**: 드는 쪽 `PlayerGrabController.carrySocket`(`Unit/PlayerGrabController.cs:13`, 주석 "캐릭터 등/어깨 위치")이 프리팹에서 루트 기준 **(0, 1.40, −0.20)** — 머리 높이·등 뒤다. 한편 드는 쪽 상체 레이어(`PlayerAnimator.controller`의 "Carry" 레이어, 마스크 `CarryUpperBody` = 몸통·팔·손가락, 클립 `Cookie_Carrying`)는 **두 손을 몸 앞 (±0.3, 1.05, 0.25)** 에 모은 자세다(클립을 샘플링해 측정). 손과 들린 쿠키가 1m 가까이 어긋나 있다.
2. **들린 쿠키가 위치만 따라감**: `PlayerCarryFollower.TryFollow`(`Unit/PlayerCarryFollower.cs:71-80`)는 FixedUpdate에서 `rb.position = CarrySocket.position`만 대입한다. 회전은 따라가지 않고, 들린 쿠키 자세는 `Held = Cookie_Hanging_Idle`(두 팔을 머리 위로 든 매달림 자세, 손 높이 1.85m)이라 "떠 있는" 모습이 된다.
3. **다른 화면에서는 이중 지연**: 들기 RPC는 들린 사람에게만 간다(`PlayerGrabController.cs:64` → `HideOrSeekPlayer.OnGrabbedByOwner`, `HideOrSeekPlayer.cs:80-85`). 들린 사람 화면이 드는 사람의 **보간된(늦은) 위치**를 따라가 자기 위치를 보내고, 다른 화면은 그것을 다시 보간한다(`UpdateRemote`, `HideOrSeekPlayer.cs:278-284`). 드는 사람 화면에서도 들린 쿠키는 원격 보간 위치라 달릴수록(약 5m/s) 뒤로 처진다 — 사진에서 왼쪽 위로 멀리 떨어져 보이는 이유.

**바꿀 것**
1. **누가 누구를 드는지 모든 화면이 안다**: `PlayerNetworkSync`(`Unit/PlayerNetworkSync.cs`)에 `CarrierViewId`(int, 안 들렸으면 −1) 한 필드 추가 — 들린 쿠키 소유자가 보낸다. (늦게 들어온 사람도 다음 전송에서 바로 앎. 방 프로토콜 변경은 이 필드 하나.)
2. **손 사이에 붙이기**: `PlayerGrabController`의 소켓을 고정 자식 대신 매 `LateUpdate`(애니메이션 적용 뒤)에 **두 손 뼈(`Animator.GetBoneTransform(RightHand/LeftHand)`)의 가운데**로 옮기는 `HandsCarryAnchor`(새 작은 클래스)로 바꾼다. 달릴 때 몸통이 흔들리면 들린 쿠키도 같이 흔들린다.
3. **모든 화면에서 같은 프레임에 붙이기**: 들린 쿠키는 소유자·원격 모두 `LateUpdate`에서 **자기 화면의 드는 쪽 손 위치·회전**에 위치+회전을 맞춘다(원격은 이때 `networkSync.Interpolate`를 건너뜀). 이중 지연·처짐이 없어진다. 소유자 쪽 물리(키네마틱) 대입은 지금처럼 FixedUpdate에도 유지해 충돌 판정이 어긋나지 않게 한다.
4. **들린 자세**: 들린 쿠키를 드는 쪽 팔 위에 **가로로 눕혀 안기**(엉덩이가 두 손 가운데, 몸은 드는 쪽 좌우 방향) — 결정 질문 Q1. 들린 쪽 애니메이션은 `Held`에서 축 늘어진 자세로 바꾼다(`Cookie_Idle` 첫 프레임 고정 또는 `Cookie_Hanging_Idle` 중 사진 비교로 고름). 괴물에게 잡힌 처형 연출의 `Held`(`HideOrSeekPlayer.cs:143-145`)는 그대로 둔다 — 쿠키끼리 들 때만 새 상태 `Carried`를 쓴다.
5. 드는 쪽 상체 "Carry" 레이어는 지금처럼 켠다(로컬 `PlayerGrabController.cs:68`, 원격 `RemoteIsCarrying`). 사진상 드는 쪽 팔이 들기 자세로 보이지 않는 경우가 있어 Play Mode에서 레이어 가중치·자세를 실제로 확인하고, 하체(걷기·달리기)와 섞이는지 본다.
6. 내려놓기 `Detach`(`PlayerCarryFollower.cs:44-67`)는 지금처럼 앞 1.2m — 회전을 원래대로(세움) 되돌리는 것만 추가.
7. 들린 사람 카메라: 위치가 매 프레임 바뀌므로 흔들림이 생기면 카메라 추적을 LateUpdate 이후 순서로 맞춘다(확인 후 필요할 때만).

**검증**: EditMode 시험(소켓 = 손 가운데 계산, CarrierViewId 직렬화). Play Mode(오프라인 + 원격 흉내 쿠키 하나): 서기·걷기·달리기 각각에서 들린 쿠키 엉덩이와 손 사이 거리 측정(목표 0.15m 이하), 옆·뒤 사진. 멀티(빌드 2개) 확인은 README 멀티 목록에 추가.

## 16.4 손에 든 아이템 — 한 손은 오른손, 두 손은 들기 자세 (요청 2·5)

**지금 코드**
- `HeldItemPresenter`(`Escape/HeldItemPresenter.cs`)가 쿠키 **루트**에 `CarriedItemSocket`을 만들고 고정 위치에 둔다: 두 손 `(0, 1.0, 0.45)`, 한 손 `(0.38, 0.95, 0.25)`(`:10-11, :64`). 뼈에 붙지 않고 애니메이션도 바꾸지 않아 걷거나 달려도 아이템이 몸 앞에 떠 있다.
- 아이템 종류: 두 손 16개(재료 전부), 한 손 3개(뿅망치·스턴건·물풍선 — 도구). `ItemSO.holdType`(`Escape/Data/ItemSO.cs`).
- 아이템을 든 손으로는 쿠키를 들 수 없다(`PlayerGrabController.cs:47`) → 두 가지 들기가 겹치지 않는다.
- 쿠키 뼈대는 Humanoid(Mixamo, `mixamorig:RightHand`) — `GetBoneTransform`으로 손 뼈를 찾을 수 있다.

**바꿀 것**
1. **한 손**: 소켓을 `RightHand` 뼈의 자식으로 붙이고, 아이템별 쥐는 위치·각도 `ItemSO.gripPosition / gripEuler`(새 필드, 기본값 0) — 도구 3개 값은 사진 보며 맞춤. 오른팔은 기본 걷기·달리기 클립을 그대로 따르므로 팔을 흔들면 아이템도 같이 흔들린다.
2. **주먹 쥐기**: 오른손 손가락만 덮는 마스크 `RightGrip.mask` + 손가락을 굽힌 짧은 클립 `Cookie_RightGrip.anim`(Humanoid 손가락 근육 값으로 에디터 도구가 생성)을 새 레이어 "Grip"으로 추가. 한 손 아이템을 들 때만 가중치 1.
3. **두 손**: 쿠키 들기와 같은 "Carry" 상체 레이어를 켜고, 소켓은 §16.3의 `HandsCarryAnchor`(두 손 가운데)를 같이 쓴다. 아이템 크기만큼 앞으로(`ItemSO.gripPosition`으로 미세 조정).
4. **레이어 가중치 한 곳에서**: `PlayerAnimationDriver.SetCarryLayerWeight` 대신 `SetCarry(reason, bool)`(쿠키 들기·두 손 아이템 두 이유의 OR)와 `SetGrip(bool)`. 원격 화면도 `HeldItemPresenter`가 상태(EscapeState·SelectedSlot)로 각자 계산하므로 네트워크 추가 없음. `UpdateRemote`의 매 프레임 덮어쓰기(`HideOrSeekPlayer.cs:283`)는 "쿠키 들기" 이유만 바꾸도록 수정.
5. 도구 사용 연출(`ToolShotFx` — 망치 휘두르기 등)이 소켓 위치를 기준으로 하면 오른손 기준으로 자연히 따라간다 — 확인만.

**검증**: Play Mode 사진 — 한 손 도구 3개·두 손 재료 2개를 들고 서기·걷기·달리기 각각(옆·앞). 아이템 중심과 손(가운데) 거리 측정. 아이템 바꾸기(칸 전환)·내려놓기·장치에 넣기 후 레이어 꺼짐 확인.

## 16.5 시야 감소(비네트) 제거 (요청 8)

**지금 코드**
- 어두운 분위기 작업(V1)에서 맵 5개 + 대기실의 후처리 프로필에 **비네트**(화면 가장자리 어둡게)가 들어갔다: `CandyForest 0.42 · ChocolateFactory 0.45 · CursedCandyCarnival 0.5 · GameLobby 0.42 · GingerbreadVillage 0.45 · HauntedBakery 0.45`(`Assets/Maps/*/*_PostFX.asset`, 확인값).
- 만드는 곳 두 군데: `Editor/Maps/TwistedAtmosphere.cs:49-83`(Look 값) + `:358-360`(적용), 옛 빌더 `Editor/Maps/MapSceneBuilder.cs:62-66`(Mood 값) + `:601-605`.
- 시험 `Editor/Tests/TwistedTests.cs:46`이 "비네트가 있어야 한다"고 검사한다.
- 실행 코드(`02. Scripts`)에는 비네트를 켜는 곳이 없다(검색 확인).

**바꿀 것**
1. 프로필 6개에서 비네트 설정을 지운다(색보정·안개·블룸은 그대로).
2. `TwistedAtmosphere`의 Look `Vignette` 값을 0으로, 적용 함수는 0이면 비네트를 지우게. `MapSceneBuilder` Mood 값도 0(0이면 원래 추가하지 않는 코드라 그대로 둠). 빌더를 다시 돌려도 되살아나지 않게.
3. 시험을 "비네트가 없어야 한다"로 바꾼다.
4. 가장자리가 밝아지면 화면 전체가 조금 밝게 느껴질 수 있다 — 전후 사진으로 비교만 하고 노출은 건드리지 않는다(필요하면 따로 말씀).

**검증**: TwistedTests. 맵 6곳 같은 카메라 위치 전후 사진(가장자리 밝기 수치 비교).

## 16.6 소리 출력 장치 설정 — 스피커·헤드폰·헤드셋 (요청 6)

**지금 코드**
- 소리 설정 창 `Audio/SoundSettingsPanel.cs`: 막대 4개(전체·배경음·효과음·UI)만. 값은 `Audio/AudioVolumeSettings.cs`가 PlayerPrefs(`TOC_Volume_*`)에 저장하고 `Changed` 이벤트로 재생 중인 소리에 바로 반영.
- 3D 소리 재생 칸은 `AudioRuntime.MakeSource`(`Audio/AudioRuntime.cs:112-131`)에서 `spread = 0`(좌우를 끝까지 나눔), 거리 감쇠는 코드(`SpatialGain`)가 직접 계산.
- 프리팹은 에디터 도구 `Editor/Audio/SoundSettingsInstaller.cs`가 만든다.

**설계** — 게임이 실제 장치를 알아낼 방법은 없으므로(Unity·OS가 알려 주지 않음) 사용자가 고르고, 고른 장치에 맞게 소리 방향감·음색을 바꾼다.
| 모드 | 좌우 방향감(3D 소리 `spread`) | 음색 | 의도 |
|---|---|---|---|
| 스피커 | 넓힘 60°(반대쪽에서도 일부 들림) | 그대로 | 스피커는 두 귀가 다 들으므로 너무 한쪽으로 쏠리지 않게 |
| 헤드폰(기본) | 0°(지금과 같음 — 방향 가장 또렷) | 그대로 | 괴물 위치를 소리로 가장 정확히 |
| 헤드셋 | 20° | 아주 낮은 저음 자르기(90Hz) + 효과음 −1dB | 작은 드라이버에서 저음이 뭉개지거나 찢어지지 않게, 마이크 쓰는 동안 피로 줄이기 |

**바꿀 것**
1. `AudioVolumeSettings`에 `OutputMode { Speaker, Headphones, Headset }` + `Get/SetOutputMode`(PlayerPrefs `TOC_OutputMode`, 기본 Headphones = 지금 소리와 동일) + 같은 `Changed` 이벤트.
2. 모드별 값은 순수 함수 `OutputProfile.For(mode)`(spread·저음 자르기 주파수·효과음 dB) — 시험 대상.
3. `AudioRuntime`이 `Changed`를 받아 3D 칸 전부의 `spread`를 바꾸고, 듣는 쪽(`AudioListener`) 오브젝트에 `AudioHighPassFilter` 하나를 두고 헤드셋일 때만 켠다.
4. 설정 창에 "출력 장치" 줄 — 버튼 3개(고른 것 강조), 누르면 바로 적용 + 짧은 미리 듣기(왼쪽→오른쪽으로 지나가는 효과음 1회). `SoundSettingsInstaller`로 프리팹 갱신, 문구는 `\uXXXX`.
5. `AudioSettings.speakerMode`(OS 출력 채널)는 바꾸지 않는다 — 바꾸면 모든 소리가 재시작된다.

**검증**: EditMode 시험(저장·불러오기·프로필 값). Play Mode: 모드 바꿀 때 재생 중인 3D 소리의 spread·필터 값 확인, 설정 창 사진, 다시 켜도 유지. 청취는 직접 들어 보셔야 함(헤드셋 차이는 작음 — 결정 질문 Q3).

## 16.7 베이커리 탈출구 — 열리기 전 고장 난 소리 (요청 7)

**지금 코드**
- 베이커리 탈출 장치는 `ESC_HauntedBakery_Root`(씬 `Game_HauntedBakery`, 위치 (0, 0, −26.2)) + `OvenSequence`. 소리는 시간표로만 난다(`Escape/Sequences/OvenSequence.cs:65-69`): 완성 뒤 톱니 반복음 `OvenGears`, 피스톤 `OvenPiston`, 3초에 문 굴러감 `OvenDoorRoll`, 출발 때 `OvenFlash`.
- 반복음 조건은 `(완성 뒤 초 c, 출발 뒤 초 d)`로 정하고, **완성 전에는 c = −1**(`Escape/EscapeSequence.cs:66`) → 지금은 완성 전에 탈출구 주변이 조용하다(씬에 이 근처 `AmbientEmitter` 없음 — 확인).

**바꿀 것**
1. 새 소리 `SoundId.OvenBroken`(724, 반복음): 고장 난 오븐 — 걸렸다 멈추는 톱니, 헛도는 모터, 가끔 "칙—" 꺼지는 불꽃·스파크·쇳소리, 4~6초 주기로 시동 실패. 공포 분위기 톤(유아틱 금지). 음원은 `request1009_sound.py`에 `n7` 묶음으로 합성, 원본 `Source~` 보관, 들어 볼 수 있게 묶음 파일 제공.
2. `OvenSequence.Awake`에 `AddLoop(SoundId.OvenBroken, (c, d) => c < 0f, gearA)` 한 줄 — 완성되는 순간 멈추고, 지금 소리(`OvenGears`·피스톤)가 이어서 난다. 완성 순간 "덜컹 — 시동 걸림" 연결음이 필요하면 `OvenPiston`으로 충분한지 들어 보고 결정.
3. 카탈로그(`SoundCatalogSO`)에 항목: 거리·음량은 `OvenGears`와 같게(가까이 가야 들리게), 실내 감쇠 그대로.
4. 다른 맵 탈출 장치는 요청 범위가 아니므로 그대로(원하시면 같은 방식으로 쉽게 확장).

**검증**: EditMode 시험(완성 전에만 고장 소리). Play Mode(베이커리): 완성 전 탈출구 근처에서 `OvenBroken` 재생·멀어지면 멈춤 → 재료를 다 넣어 완성 → 고장 소리 멈추고 `OvenGears` 시작(시간 기록 `Temp/audio_log.txt`). 청취 후보는 먼저 들려 드리고 승인 뒤 넣는다.

## 16.8 영향·위험
| 위험 | 대응 |
|---|---|
| 동기화 필드(`CarrierViewId`) 추가 → 옛 빌드와 섞이면 오류 | 같은 빌드끼리만 접속(지금도 같음). 멀티 확인 목록에 추가 |
| 손 뼈 기준으로 붙이면 애니메이션 흔들림이 커 보임 | 소켓 위치를 손 가운데 값과 몸통 값 사이로 섞는 비율(0~1) 하나 두고 사진 보며 조정 |
| 들린 쿠키 가로 자세가 벽·천장에 박힘 | 충돌은 지금처럼 꺼진 상태(키네마틱) — 보이기만 함. 내려놓을 때 세우고 앞 1.2m |
| 상자를 숨겼다 보이는 사이 상자 열기 요청 | 이미 Paint 동안 `CanInteract` 거짓 — 변화 없음 |
| 아이콘 렌더가 너무 어두움(어두운 분위기 재질) | 아이콘 전용 조명으로 렌더(맵 조명과 무관) |
| 비네트 제거로 화면이 밝아짐 | 사진 비교. 노출 조정은 별도 요청 시 |
| 헤드셋/헤드폰 차이가 작게 느껴짐 | Q3에서 둘을 합칠지 결정 |

## 16.9 단계

| 단계 | 내용 | 결과물·검증 | 상태 |
|---|---|---|---|
| N0 | §16.10 결정 | 이 문서 갱신 | ✅ 2026-10-11 |
| N1 | 요청 1 상자 대기 시간 숨김 + 나타남 연출 | EditMode 시험, Play Mode 전후 사진 | ✅ 2026-10-11 — §16.11 N1 |
| N2 | 요청 4 아이템 아이콘 19개 굽기·연결 | 아이콘 모음 사진, 칸 사진, 시험 | ✅ 2026-10-11 — §16.11 N2 |
| N3 | 요청 3 쿠키 들기(손 가운데 고정·모든 화면 같은 프레임·들린 자세) | 거리 측정(서기·걷기·달리기), 사진, 시험 | ✅ 2026-10-11 — §16.11 N3 |
| N4 | 요청 2·5 아이템 들기(오른손·주먹 레이어·두 손 들기 자세) | 도구 3·재료 2 사진, 레이어 꺼짐 확인 | ✅ 2026-10-11 — §16.11 N4 |
| N5 | 요청 8 비네트 제거(프로필 6·빌더 2·시험) | 맵 6곳 전후 사진, TwistedTests | ✅ 2026-10-11 — §16.11 N5 |
| N6 | 요청 6 소리 출력 장치 설정 | 시험, 설정 창 사진, 값 확인 | ✅ 2026-10-11 — §16.11 N6 → §17로 대체 |
| N7 | 요청 7 베이커리 고장 소리(후보 → 승인 → 넣기) | 청취 묶음, Play Mode 소리 기록 | ✅ 2026-10-11 — §16.11 N7(넣은 뒤 청취 확인 요청) |
| N8 | 전체 테스트·Play Mode·빌드 한 판 | 테스트 결과, 빌드 | ✅ 2026-10-11 — §16.11 N8 |
| N9 | 문서(README·멀티 확인 목록) | | ✅ 2026-10-11 |

순서 이유: N1·N2·N5는 서로 독립이고 작다. N3이 들기 기준점(`HandsCarryAnchor`)과 레이어 정리를 만들고 N4가 그것을 쓴다. N7은 청취 승인이 필요해 후보를 일찍 만들어 두고 승인 뒤 넣는다.

## 16.10 결정 — ✅ 2026-10-11
| # | 질문 | 결정 |
|---|---|---|
| Q1 | 다른 쿠키를 드는 자세 | **A** — 가로로 눕혀 두 팔로 안기 |
| Q2 | 상자가 나타날 때 소리 | **없음** — 크기 연출만 |
| Q3 | 헤드폰·헤드셋 차이 | Claude 판단에 맡김 → **3개 유지**(요청 그대로). 헤드셋은 저음 자르기 + 좌우 폭 약간 좁힘으로 헤드폰과 구별 |
| Q4 | "대기 시간" | **변장(색칠) 시간**이 맞음 |

## 16.11 결과 기록

### N1 ✅ 2026-10-11
- `EscapeManager.ChestsVisibleIn(phase)`(변장 시간만 거짓) + 0.2초마다 단계 확인 → 바뀌면 `RefreshViews`. 처음 그릴 때(판 중간 입장)는 연출 없이 바로 보임.
- `MaterialChest.Show(..., appear)`: 0.35초 0→1.08→1 크기 연출(`AppearScale`) + 바닥 먼지 14개. 소리 없음(Q2).
- Play Mode(베이커리, 오프라인): 변장 시간 동안 상자 0개·상호작용 등록 없음 → 끝나는 순간 3개 모두 나타나며 크기 0.00→1.08→1.01 기록. 사진 `images/n1_chest_appear.jpg`(변장 중 / 끝난 직후 / 0.4초 뒤). 콘솔 오류·경고 0.

### N2 ✅ 2026-10-11
- 새 에디터 도구 `Assets/Editor/Escape/ItemIconBaker.cs`(메뉴 Tools/Escape/Bake Item Icons): 미리보기 장면에서 모델을 2배 해상도로 찍어 256×256 투명 PNG로 줄이고 어두운 테를 두른다. 뒤 3/4·앞 3/4 두 각도 중 색이 더 보이는 쪽을 고른다(룬 무늬·기어 불빛이 앞면에만 있음).
- 결과 19개 `Assets/08. Resources/ItemIcons/Icon_{ItemId}.png` → 각 `ItemSO.icon` 연결(모델 빌더는 `modelPrefab`만 바꾸므로 다시 돌려도 유지). 모음 `images/n2_item_icons.jpg`.
- Play Mode(베이커리): 상자에서 기어를 받자 1번 칸과 오른쪽 위 필요 재료 목록에 그림 표시 — `images/n2_hotbar.jpg`. 콘솔 오류·경고 0.

### N3 ✅ 2026-10-11
- 새 `Unit/CarryPose.cs`(순수 계산: 두 손 뼈 가운데, 눕힌 방향, 엉덩이 자리) + `Unit/CarriedBodyPresenter.cs`(`[DefaultExecutionOrder(-50)]` — 애니메이션 뒤·카메라 앞 LateUpdate에서 들린 몸을 그 화면의 드는 쪽 손 위에 놓음).
- `PlayerNetworkSync`에 `RemoteCarrierViewId` 추가(들린 사람이 보냄). 원격 화면은 들린 몸을 보간하지 않는다. 그랩버 화면은 동기화가 오기 전 0.6초 동안 로컬 값(`SetLocalCarrierOverride`)을 쓴다.
- 등 뒤 `carrySocket` 사용 제거. `PlayerCarryFollower`는 같은 계산으로 물리 몸 위치·회전을 맞추고, 내려놓을 때 세워서 드는 쪽을 보게 한다.
- 새 상태 `PlayerMoveState.Carried`(enum 끝에 추가 — 기존 값 그대로) + 애니메이터 `Carried` 상태(에디터 도구 `Assets/Editor/Cookie/CookieAnimatorSetup.cs`). 클립은 `Cookie_Idle`과 `Cookie_Hanging_Idle`을 사진으로 비교해 **매달림 클립**을 골랐다(무릎이 굽어 팔 위에 안긴 모습). 괴물 처형의 `Held`는 그대로. 들림·내려짐 소리는 `CookieAudio`가 `Carried`로도 낸다.
- 상체 두 손 자세는 이유별로 켠다(`PlayerAnimationDriver.SetCarry(CarryReason, bool)` — 쿠키 들기·두 손 아이템).
- Play Mode(베이커리, 쿠키 2명): 들린 엉덩이와 목표점(드는 쪽 손 가운데 + 위 0.06·앞 0.22m) 차이가 서기 41·걷기(4.9m/s) 121·달리기(6.4m/s) 220프레임 모두 **0.000m**. 드는 쪽 상체 레이어 1. 내려놓으면 세워지고(up=(0,1,0)) 0.92m 앞, 잠금 해제·레이어 0. 사진 `images/n3_carry.jpg`(위: 서기 옆·앞 / 아래: 달리기). 시험용으로 두 쿠키 색을 임시로 바꿈.
- `RuleTests` 27개 통과(동기화 왕복 시험에 `RemoteCarrierViewId` 추가). 콘솔 오류·경고 0. 원격 화면(다른 컴퓨터)에서의 모습은 README 멀티 확인 목록에 추가 예정.

### N4 ✅ 2026-10-11
- `HeldItemPresenter` 다시 씀: 매 LateUpdate(애니메이션 뒤) 위치를 맞춘다. **두 손** = 두 손 뼈 가운데 + 상체 두 손 자세(`CarryReason.TwoHandedItem`), **한 손** = 오른손 뼈 위치, 아이템 아래쪽(손잡이)을 쥐도록 모델을 올리고 쥔 곳을 축으로 위쪽을 앞으로 35° 기울인다. 방향은 몸 방향을 따른다(손목 회전에 뒤집히지 않게). 원격 화면도 상태로 각자 계산 — 네트워크 추가 없음.
- `ItemSO`에 미세 조정 값 `holdOffset`·`holdEuler`(기본 0) 추가.
- **계획과 다른 점**: 주먹 쥐기 레이어(Grip)는 만들지 않았다 — 쿠키 뼈대에 손가락 뼈가 없다(`RightHand` 자식 0, 벙어리장갑 손). 레이어를 만들어도 움직일 뼈가 없다.
- Play Mode(베이커리): 기어(두 손) — 아이템 소켓과 두 손 가운데 거리 서기 109·걷기 588·달리기 710프레임 모두 0.000m, 상체 레이어 1. 망치(한 손) — 소켓과 오른손 거리 서기 104·걷기 597·달리기 769프레임 모두 0.000m, 상체 레이어 0(팔을 그대로 흔듦). 내려놓으면 레이어 0·소켓 비움. 사진 `images/n4_two_handed.jpg`·`images/n4_one_handed.jpg`. 콘솔 오류·경고 0.
- 스턴건·물풍선도 같은 한 손 규칙(자동 손잡이 위치)이다. 이번 판 상자에는 망치만 있어 사진은 망치만.

### N5 ✅ 2026-10-11
- 후처리 프로필 6개(맵 5 + 대기실)에서 비네트 설정·하위 에셋 삭제 — 새 메뉴 `Tools/TagOfChaos/Maps/Remove Vignette (All Profiles)`(`TwistedAtmosphere.RemoveAllVignettes`). 색보정·안개·블룸·노출은 그대로.
- 빌더가 되살리지 않게: `TwistedAtmosphere` Look 6개 `Vignette = 0` + 0이면 설정을 지우는 `RemoveVignette`, `MapSceneBuilder` Mood 4개 `Vignette = 0`(0이면 원래 추가하지 않음).
- 시험 반대로: `TwistedTests`가 맵 5곳·대기실에 비네트가 **없어야** 통과 — 20개 통과.
- 같은 카메라(씬 Main Camera) 가장자리/가운데 밝기(0~255): 캔디숲 34→122 / 118→123, 진저브레드 8→70 / 31→33, 초콜릿 22→110 / 124→128, 놀이공원 9→72 / 97→102, 베이커리 17→80 / 80→84, 대기실 9→41 / 20→21. 가운데는 거의 그대로이고 가장자리만 밝아졌다. 전후 `images/n5_vignette_before_after.jpg`(왼쪽 전·오른쪽 후). Play Mode 게임 화면(베이커리)도 가장자리 어둠 없음 — `images/n5_play_bakery.jpg`. 콘솔 오류·경고 0.

### N6 ✅ 2026-10-11 → **§17로 대체(2026-10-11)** — 아래 3모드는 지웠다. 실제 장치 선택은 §17.7
- `AudioVolumeSettings`: `OutputMode { Speakers, Headphones, Headset }` + `Output`/`SetOutput`(PlayerPrefs `TOC_OutputMode`, 기본 헤드폰 = 예전 소리 그대로) + 순수 값 `OutputProfile.For(mode)`. 효과음·환경음 묶음 이득에 장치별 dB를 더한다.
- `AudioRuntime`: 바뀌면 3D 칸 24개의 `spread`를 바꾸고, 듣는 쪽(Listener)의 `AudioHighPassFilter`를 헤드셋일 때만 켠다. `AudioSettings.speakerMode`는 건드리지 않는다.

| 모드 | 3D 좌우 퍼짐 | 저음 자르기 | 효과음 |
|---|---|---|---|
| 스피커 | 60° | 없음 | 0 dB |
| 헤드폰(기본) | 0°(지금과 같음) | 없음 | 0 dB |
| 헤드셋 | 20° | 90 Hz | −1 dB |

- 설정 창: 막대 4줄 아래 "출력 장치" 줄 + 버튼 3개(고른 것 캐러멜색). 누르면 바로 적용하고 듣는 위치 오른쪽 3m에서 짧은 소리를 들려준다. 줄은 `SoundSettingsInstaller.EnsureOutputRow`(메뉴 Tools/TagOfChaos/Audio/Add Output Device Row)가 프리팹에 넣고, 한글 문구(출력 장치·스피커·헤드폰·헤드셋)는 프리팹에 `\uXXXX`로 넣었다. 프리팹 하나라 ESC 메뉴(맵 5개)·대기실·로비 창에 모두 들어감(버튼 3개 확인).
- Play Mode(베이커리): 헤드폰(0°·0Hz) → 헤드셋 버튼 → 20°·90Hz·효과음 이득 0.891·저장값 2 → 스피커 60°·0Hz·1.000 → 다시 불러와도 스피커 유지 → 헤드폰으로 되돌림. 사진 `images/n6_output_device.jpg`. 콘솔 오류·경고 0. 실제 귀로 듣는 차이는 직접 확인 필요(헤드셋은 헤드폰과 차이가 작다 — Q3).

### N7 ✅ 2026-10-11 (넣은 뒤 청취 확인 요청)
- 새 소리 `SoundId.OvenBroken = 724`(8초 반복, `Assets/10. Audio/SFX/Device/OvenBroken.wav`, 원본 `Source~/r1009/final/SFX/Device/`): 걸렸다 꺼지는 모터(시동 실패 2번), 헛돌다 덜컥 걸리는 톱니 쇳소리, 전기 스파크, 새는 증기, 낮은 불협 저음, 삐걱임. 합성은 `request1009_sound.py n7`. 처음 만든 것은 80Hz 아래가 거의 전부라 노트북 스피커에서 고장 디테일이 안 들려 저음을 줄이고 중·고음을 키웠다(대역 비율 80Hz↓ −3dB, 80~300 −4.6dB, 300~1500 −10.3dB, 1.5~6k −13.1dB). −24.3 LUFS(피크 −6dB 제한) → 카탈로그 볼륨 0.75(톱니 0.5), 거리 8~60m·맵 연출 묶음(톱니와 같음).
- `OvenSequence.Awake`: `AddLoop(SoundId.OvenBroken, (c, d) => c < 0f, gearA)` — 완성 전에만. 완성 순간 멈추고 기존 `OvenGears`·피스톤·문 소리가 이어진다.
- Play Mode(베이커리): 탈출구 16m — `OvenBroken` 재생(볼륨 0.19), 21m 0.03, 90m 0(멀어지면 들리지 않음) → 다시 가까이 → 완성(CompletedAt 기록) 즉시 `OvenBroken` 멈춤·`OvenGears` 시작(17m 0.15), 3초 뒤 `OvenDoorRoll`. 콘솔 오류·경고 0.
- 청취 묶음 `Assets/10. Audio/Source~/r1009/review/reel_N7_oven_broken.wav`: ① 고장 소리 16초 ② 고장 → 완성(지금 소리)으로 넘어가는 부분. 마음에 안 드시면 이 파일만 바꾸면 된다.

### N8·N9 ✅ 2026-10-11
- 새 시험 `Assets/Editor/Tests/Request1011Tests.cs` 19개 통과(상자 단계별 표시·튀어나옴 크기, 아이콘 19개, 들린 몸 방향·엉덩이 위치·세우기, `Carried` 값 7·애니메이터 상태, 동기화 기본값, 두 손 자세 이유 합치기, 한 손 손잡이 위치, 출력 장치 값·저장·효과음 이득, 설정 창 버튼 3개, `OvenBroken` 카탈로그).
- 전체 EditMode 234개: 통과 227, 실패 6, 판정 보류 1. 실패 6개는 모두 `BuildSceneTests.ReleaseScene_HasNoDevScripts` — 씬 6곳에 원래 저장돼 있던 꺼진 시험용 `GameObject`(OfflineModeBootstrap, 씬 파일 수정 시각이 이번 작업 전) 때문이고 README "남은 확인"에 이미 있는 항목이다. 이번 작업으로 생긴 실패는 없다.
  - 중간에 `AudioTests` 2개가 새 출력 장치 버튼에 UI 클릭 소리가 없어 실패 → `Tools/TagOfChaos/Audio/Attach UI Sounds`로 붙였다. 도구가 GameSceneCore를 먼저 처리해 그 안의 버튼에 소리가 두 개씩 붙은 것을 확인하고 덧붙은 쪽을 지웠다(모든 곳 버튼당 1개).
- 빌드: Windows 64 `Builds/R1011Check/` — 성공, 오류 0·경고 0, 씬 7개, 323MB, 1분 10초.
- 문서: README 현재 문서 표·남은 확인(멀티에서 들기·아이템·상자 확인, 고장 소리·출력 장치 청취, 손가락 뼈 없음) 갱신.

---

# 17. 2026-10-11 출력 장치 다시 — 실제 장치를 골라서 소리 내기 (단계 D0~D6)

- 작성: 2026-10-11 · 상태: **✅ 완료 D0~D6 (2026-10-11)**
- 요청: N6의 스피커·헤드폰·헤드셋 3개는 **소리 성격만 바꾸는 값**이었다. 원하신 것은 이 PC에 연결된 **실제 출력 장치**(예: "Realtek 스피커", "USB 헤드셋", "모니터 HDMI")를 목록에서 골라 게임 소리를 그쪽으로 내는 것. N6을 이 기능으로 바꾼다.

## 17.1 지금 상태(확인한 값)
- Unity 6000.0.58f2, 스크립팅 Mono, 빌드 대상 Windows 64, 개발 PC Windows 11(26100).
- **Unity에는 출력 장치를 고르는 기능이 없다.** `AudioSettings`에 있는 것은 `GetConfiguration`·`SetConfiguration`·`Reset`·`OnAudioConfigurationChanged`(장치가 바뀌었다는 알림)뿐 — 늘 Windows의 기본 장치로 소리를 낸다(Reflection으로 전부 확인).
- 지금 설정 창 줄 `Row_Output`(버튼 3개)과 코드: `AudioVolumeSettings.OutputMode`·`OutputProfile`, `AudioRuntime.ApplyOutputMode`(spread·저음 자르기), `SoundSettingsPanel.outputButtons`, `SoundSettingsInstaller.EnsureOutputRow`, 시험 `Request1011Tests` 3개.

## 17.2 방법 — Windows "앱별 출력 장치"
Windows 설정 → 소리 → **앱 볼륨 및 장치 기본 설정**에서 앱마다 출력 장치를 정할 수 있다. 게임이 같은 기능을 스스로 부른다.
1. **장치 목록**: Windows Core Audio(`IMMDeviceEnumerator.EnumAudioEndpoints(eRender, ACTIVE)`)로 켜져 있는 출력 장치의 ID·이름(예: "스피커(Realtek(R) Audio)")을 읽는다. 공개 API.
2. **이 게임만 그 장치로**: Windows 내부의 앱별 기본 장치 설정(`Windows.Media.Internal.AudioPolicyConfig`의 `SetPersistedDefaultAudioEndpoint` — Windows 설정 화면과 EarTrumpet 같은 프로그램이 쓰는 것)으로 **이 프로세스만** 고른 장치로 보낸다. **PC 전체 기본 장치는 바꾸지 않는다**(다른 프로그램 소리는 그대로).
3. **Unity가 새 장치를 열게**: `AudioSettings.Reset(지금 설정)`으로 소리 엔진을 다시 시작 → 새 장치로 나온다.
4. Reset하면 **재생 중인 소리가 모두 멈춘다** → `AudioRuntime`이 직후에 배경음(지금 곡)·추격음·반복음(환경음·장치음은 다음 확인 때 자동으로 다시 시작)을 되살린다.
- C#만으로(플러그인 파일 없이) `combase.dll`·`ole32.dll` 함수를 직접 부른다. Mono에서 안전하게 COM 함수표를 직접 호출하는 방식.

## 17.3 바꿀 것
| 파일 | 내용 |
|---|---|
| `Audio/OutputDevices.cs`(새) | 장치 목록(ID·이름), 지금 장치, `TrySelect(id)`(앱별 장치 설정 + Reset), "시스템 기본"으로 되돌리기. Windows가 아니거나 실패하면 `IsSupported = false` |
| `Audio/WindowsAudioEndpoints.cs`(새) | Core Audio·AudioPolicyConfig 호출(COM 함수표 직접 호출). Windows 10 1803~21H1과 21H2 이후는 인터페이스 번호가 달라 둘 다 시도 |
| `AudioVolumeSettings` | N6의 `OutputMode`·`OutputProfile`·장치별 dB **삭제**. 대신 `OutputDeviceId`(문자열, 빈 값 = 시스템 기본) 저장 `TOC_OutputDevice` |
| `AudioRuntime` | `ApplyOutputMode`·저음 자르기 필터 **삭제**(소리 성격은 예전 그대로). 시작할 때 저장된 장치 적용, Reset 뒤 소리 되살리기, `OnAudioConfigurationChanged`(장치를 뽑음 등) 처리 — 고른 장치가 사라지면 시스템 기본으로 |
| `SoundSettingsPanel` | 버튼 3개 → **장치 드롭다운**(첫 줄 "시스템 기본" + 장치 이름들) + **새로고침** 버튼. 바꾸면 바로 적용하고 그 장치에서 짧은 확인음. 지원 안 되면 "시스템 기본"만 보이고 회색 |
| `SoundSettingsInstaller` | `Row_Output` 줄을 드롭다운·새로고침으로 바꿔 다시 만듦(UI 클릭 소리도 같이). 한글 문구 `\uXXXX` |
| 시험 | N6 시험 3개를 바꿈: 저장·불러오기, 없는 장치 ID면 기본으로, 목록 첫 줄 "시스템 기본", 드롭다운 연결 |

## 17.4 검증
- 에디터 Play Mode(이 PC): 목록에 실제 장치들이 나오는지(Windows 소리 설정의 목록과 비교), 다른 장치를 고르면 Windows "앱 볼륨 및 장치 기본 설정"에 Unity가 그 장치로 바뀌어 있는지, Reset 뒤 배경음·환경음이 다시 나오는지, "시스템 기본"으로 되돌리기. 콘솔 오류·경고 0.
- 빌드(.exe)에서 같은 확인 — 앱별 설정은 실행 파일마다 따로 저장된다.
- 장치가 하나뿐인 PC에서도 목록·선택이 오류 없이 동작.
- **실제로 다른 장치에서 소리가 나는지는 귀로 확인이 필요**하다(제가 들을 수 없음) — 헤드셋을 꽂고 골라 봐 주셔야 한다.

## 17.5 위험
| 위험 | 대응 |
|---|---|
| 앱별 장치 설정은 Windows 내부 기능(공식 문서 없음) — Windows 업데이트로 바뀔 수 있다 | 실패하면 오류 없이 "시스템 기본"으로 남고 창에 지원 안 됨 표시. 두 버전 인터페이스 모두 시도 |
| Reset 순간 소리가 0.1~0.3초 끊긴다 | 고를 때·장치가 바뀔 때만. 직후 곡·반복음 되살림 |
| 에디터에서 시험하면 **Unity 에디터**의 앱별 장치가 바뀐다 | 시험 끝에 "시스템 기본"으로 되돌린다 |
| Windows가 아닌 빌드(맥 등) | 목록 대신 "시스템 기본"만 |

## 17.6 단계·결정
| 단계 | 내용 | 상태 |
|---|---|---|
| D0 | 결정(아래) — ✅ Q1 지우기 · Q2 이 게임만 · Q3 Windows 전용 | ✅ 2026-10-11 |
| D1 | `WindowsAudioEndpoints` 장치 목록 — 먼저 이 PC에서 목록이 나오는지 확인(가능성 확인) | ✅ 2026-10-11 |
| D2 | 앱별 장치 설정 + Reset + 소리 되살리기 | ✅ 2026-10-11 |
| D3 | N6 3모드 삭제, 저장값 교체 | ✅ 2026-10-11 |
| D4 | 설정 창 드롭다운·새로고침, 프리팹 3곳 반영 | ✅ 2026-10-11 |
| D5 | 시험·Play Mode·빌드 확인 | ✅ 2026-10-11 |
| D6 | 문서(N6 기록에 대체 표시, README) | ✅ 2026-10-11 |

| # | 질문 | 제안 |
|---|---|---|
| Q1 | N6의 스피커·헤드폰·헤드셋(소리 성격) 3개는 지우나 | **지운다** — 장치 선택으로 대체, 소리는 예전 그대로 |
| Q2 | 장치를 고르면 이 게임만 바뀌게(앱별) vs PC 전체 기본 장치를 바꾸게 | **이 게임만** — 다른 프로그램 소리를 건드리지 않음 |
| Q3 | Windows 전용으로 해도 되나 | **예** — 게임 빌드가 Windows 64. 다른 OS는 "시스템 기본"만 |

## 17.7 결과 기록 ✅ 2026-10-11
- **D1 가능성 확인**: 새 `Audio/WindowsAudioEndpoints.cs`(Core Audio를 C#에서 COM 함수표 직접 호출, 플러그인 없음)로 이 PC의 켜진 출력 장치 6개를 읽었다 — 스피커(High Definition Audio Device, 시스템 기본), HDMI(NVIDIA), 이어폰(Hi-Res Audio), Digital Audio(S/PDIF), ED2431HF VA(모니터), Speakers(NVIDIA Broadcast).
- **D2 이 게임만 다른 장치로**: `SetAppOutput`(Windows 앱별 기본 장치, 재생·통신 두 역할) + `AudioSettings.Reset` → 이 프로세스의 **재생 중 소리 세션이 스피커에서 이어폰으로 옮겨 감**을 Windows 세션 목록(`IAudioSessionManager2`)으로 확인. 되돌리면 다시 스피커. PC 전체 기본 장치는 그대로.
  - `AudioRuntime.ReopenOutput`: Reset 직전 반복음(배경음·추격음·환경음·장치음) 위치를 기록해 직후 되살린다. 장치를 꽂고 뺄 때 Unity가 스스로 다시 열면(`OnAudioConfigurationChanged`) 직전 프레임 기록으로 되살리고 설정 창을 새로 그린다.
- **D3**: N6의 `OutputMode`·`OutputProfile`·장치별 dB·저음 자르기·spread 변경을 모두 지웠다(소리 성격은 N6 이전 그대로). 예전 저장값 `TOC_OutputMode`는 처음 읽을 때 지운다.
- **새 `Audio/OutputDevices.cs`**: 고른 장치 ID 저장(`TOC_OutputDevice`, 빈 값 = 시스템 기본), 시작할 때 다시 적용(그 장치가 없으면 저장값은 두고 시스템 기본으로 남) — 다시 꽂으면 다음 실행에 다시 쓰인다.
- **D4 설정 창**: "출력 장치" 줄 = 드롭다운(첫 줄 "시스템 기본" + 장치 이름, 긴 이름은 …) + "새로고침" 버튼. 고르면 바로 적용하고 새 장치에서 짧은 확인음. Windows가 아니면 시스템 기본만, 잠김. 프리팹 하나라 ESC 메뉴(맵 5개)·대기실·로비에 모두 반영(예전 버튼 줄 없음 확인). `SoundSettingsInstaller.EnsureOutputRow`가 예전 줄을 지우고 다시 만들며 새로고침 버튼에 UI 소리도 붙인다(`UiSoundInstaller.Attach` 공개). 한글 문구는 프리팹에 `\uXXXX`.
- **D5 Play Mode(베이커리)**: 드롭다운 7줄 → 이어폰 선택: 저장값·앱별 장치 = 이어폰, 이어폰 세션 재생 중·스피커 세션 쉼, 반복음 2개 계속 재생 → 시스템 기본: 스피커로 복귀·저장값 빈 값. 이어폰 고른 채 Play Mode를 끝내고 에디터 앱별 설정을 지운 뒤 다시 시작 → 시작하자마자 이어폰으로 다시 적용(세션 확인), 배경음 BakeryPaint가 장치를 바꾸는 동안 처음으로 돌아가지 않고 이어짐(9.8초 → 15.9초). 시험 뒤 에디터 앱별 설정·저장값은 시스템 기본으로 되돌림. 사진 `images/d_output_device.jpg`. 콘솔 오류·경고 0.
- 시험: `Request1011Tests` 20개(장치 없음 → 시스템 기본, ID 포장 형식, 이 PC 목록 읽기, 드롭다운·새로고침 연결·예전 버튼 없음), `AudioTests` 58개 통과.
- 전체 EditMode 235개: 통과 228, 실패 6(맵 씬의 옛 시험용 OfflineModeBootstrap — README 기존 항목, 이번 작업과 무관), 판정 보류 1. 빌드 `Builds/R1011Check/` 성공(오류 0·경고 0, 324MB).
- **직접 확인 부탁**: 실제로 이어폰·헤드셋에서 소리가 나는지는 귀로 들어 봐야 한다. 빌드(.exe)는 실행 파일마다 Windows가 따로 기억한다.

---

# 18. 2026-10-11 요청 6건 — 포탈·새 마녀·상자·마녀 등장 카메라·베이커리 곡·로비 곡 (단계 W0~W8)

- 작성: 2026-10-11 · 상태: **✅ 완료(2026-10-11) — 로비 곡 교체까지 완료(§18.9 W6)**
- 프로젝트 위치가 `F:\3DTagOfChaos\TagOfChaos`로 바뀐 것을 확인했다(이번 세션 작업 파일 모두 있음). 마녀 모델은 `F:\3DTagOfChaos\마녀FBX`.

## 18.1 진저브레드 포탈에 떠 있는 쿠키 (요청 1)
- **지금 코드**: 탄 쿠키의 진짜 몸은 이미 숨겨진다(`EscapeCharacterState.ApplyHidden`). 사진(`images/w_portal_floaters.jpg`)의 쿠키는 `RuneAltarSequence.UpdateFloating`(`Escape/Sequences/RuneAltarSequence.cs:145, 158-187`)이 일부러 만드는 **탑승객 인형**(`PassengerModel`, 0.55배)이다 — "포탈 안에서 둥둥 떠 기다린다"는 예전 연출.
- **바꿀 것**: 떠 있는 인형을 없앤다(`UpdateFloating`·`CreateFloater`·관련 필드 삭제). 빨려 들어가는 순간 연출(`BoardingFx.Suck`)은 그대로.
- **검증**: Play Mode(진저브레드) — 완성 → 쿠키 2명 탑승 → 포탈 주변에 인형 0개, 빨려 드는 연출은 보임. 사진.

## 18.2 새 마녀 — Blender 리깅 + 주먹으로 맵을 내리치기 (요청 2)
- **받은 모델**(`마녀FBX/Meshy_AI_Tattered_Witch_Scarec_...fbx`, 텍스처 4장): 뼈대 없는 정지 메시 1개, 정점 15.7만·삼각형 31.5만, **T자 자세**, 키 1.64·팔 폭 1.9(단위 m), 앞이 −Y. 허수아비 마녀(찢어진 옷·긴 머리·큰 모자·뾰족 손가락·말린 신발). 사진 `images/w_witch_source.jpg`.
- **지금 게임의 마녀**: 맵 5개 모두 맵 밖 `WITCH_Anchor`(EscapeSetup 자식)에 높이 90m. `WitchPresenter`(`Escape/WitchPresenter.cs`)가 5초 동안 투명→불투명, 타임어택 동안 뒤→앞으로 돌아서고, `WitchStrike`가 기록되면 `SlamArm` 하나를 0.5초 돌려 내리친다(카메라 흔들림·충격음). 남은 쿠키는 기록 순간 바로 죽는다(`EscapeCharacterState.OnWitchStrike`).
- **Blender 작업**(Claude 작업 환경의 Blender 4.5, 결과 .blend도 `마녀FBX/Witch_Rigged.blend`로 드림):
  1. 메시 정리: 정점을 약 4만 개로 줄임(멀리 90m로 보이지만 카메라 클로즈업(§18.4)이 있어 얼굴은 남김), 텍스처 2048로 줄여 연결(색·노멀·거칠기).
  2. 뼈대: 엉덩이·척추 3·목·머리·모자 끝 2·팔(어깨·위팔·아래팔·손)·**손가락**(주먹을 쥘 수 있게 손가락마다 2마디)·다리·발 + 치마·머리카락 흔들림용 몇 개. 자동 가중치 후 어깨·손가락·치마를 손으로 다듬음.
  3. 동작 3개: **Idle**(숨쉬기·몸 흔들림·머리카락·치마 펄럭임, 반복), **Rise**(나타날 때 고개를 들고 팔을 내림), **Strike**(두 팔을 머리 위로 들며 **주먹을 꽉 쥐고**(0.6초) → 두 주먹으로 맵을 내리침(0.35초, 충격 순간) → 땅을 짚은 채 잠시 → 천천히 일어섬).
  4. FBX로 내보내기 → `Assets/09. Environment/Escape/Models/WITCH_Rigged.fbx`(+ 텍스처 폴더). 원본은 그대로.
- **Unity**:
  - 가져오기 설정(Generic 리그, 동작 3개 분리), 애니메이터 `WitchAnimator.controller`(Idle → Strike 트리거).
  - `WitchPresenter`: `SlamArm` 돌리기 대신 애니메이터 `Strike`. **충격 순간(동작 안의 시각)에 카메라 흔들림·충격음**, 앞뒤 돌기·투명→불투명은 그대로. 예전 임시 마녀(도형)·`WITCH.fbx`는 남겨 두되 새 모델이 기본.
  - 맵 5개의 `WITCH_Anchor`에 새 모델을 높이 90m로 넣는다(에디터 도구로 한 번에).
- **검증**: Blender 렌더(서기·주먹 쥐기·내리침 장면 사진), Unity Play Mode에서 타임어택 끝 → 주먹 내리침 사진·충격 순간 기록, 콘솔 0.

## 18.3 스파이가 재료를 끼우면 열려 있던 상자에 다시 생김 (요청 3)
- **지금 코드**: 이미 비슷하게 되어 있다 — `RocketInsert`(`Escape/EscapeAuthority.cs:237`) → `RespawnInEmptyChest`(`:469`)가 **재료가 없는 일반 상자** 중 하나에 넣고 닫는다(`Opened = false`). 문제: 처음부터 비어 있던(**한 번도 안 열린**) 상자도 후보라, "열려 있던 상자"가 아닐 수 있다(상자 수 > 재료 수인 맵).
- **바꿀 것**: 후보를 **열려 있고 빈 일반 상자**로 먼저 고른다 → 없으면 빈 일반 상자 아무거나 → 그것도 없으면 지금처럼 장치 옆 바닥. 고른 상자는 닫힌 모습으로(이미 그렇게 됨). 순수 계산 `PickRespawnChest`로 빼서 시험.
- **검증**: EditMode 시험(열린 빈 상자 우선·없을 때 차례), Play Mode — 상자 열기 → 장치에 넣기 → 스파이로 빼서 로켓에 끼우기 → 열려 있던 상자가 닫히며 재료가 들어감.

## 18.4 마녀 등장 카메라 연출 (요청 4)
- **지금**: 스파이가 로켓을 타면(`SpyEscapedAt` + `TimeAttackEndTime`) 마녀가 5초 동안 서서히 나타나고 알림 글자가 뜬다. 카메라는 그대로.
- **설계**: 새 `Camera/WitchRevealCamera`(Main Camera에 붙음, 모든 카메라 스크립트 뒤에 실행 `DefaultExecutionOrder(1000)`).
  - 0~3초: 지금 내 시점 → 마녀 **얼굴 정면 클로즈업**(머리 뼈 위치 + 얼굴 방향 앞, 얼굴이 화면을 채우는 거리)으로 부드럽게 이동.
  - 3~4초: 마녀 얼굴 정면을 본 채 멈춤.
  - 4~7초: 원래 시점으로 부드럽게 돌아옴. 내 시점(3인칭·괴물 1인칭·관전)은 그 아래에서 계속 계산되므로 돌아오는 곳이 정확하다.
  - 마녀가 처음엔 뒤돌아 있다 → 카메라는 **얼굴이 향한 쪽 앞**으로 간다(맵 밖 쪽일 수 있음).
  - 대상: 괴물·남은 쿠키(관전자 포함 여부 Q4). 공통 시계 기준이라 늦게 들어온 사람은 지난 부분은 건너뛴다.
- **검증**: Play Mode(오프라인, 스파이 로켓 발사 흉내) — 시간별 카메라 위치·마녀 얼굴까지 거리 기록, 0·3·3.5·7초 사진.

## 18.5 베이커리 배경음 — 벨 소리 (요청 5)
- **지금**: `BakeryPaint`/`BakeryHunt`는 "차가운 유령 자장가" — 셀레스타·오르골(벨 같은 소리)·온음 음계 반짝임·합창·하프시코드(`twisted_final.py bgm_bakery`). 말씀하신 "고요한 도시의 크리스마스 느낌"은 셀레스타·오르골·반짝임 때문으로 보인다.
- **결정 Q5 (a)**: 벨 같은 소리(셀레스타·오르골·반짝임)를 빼고 더 어두운 유령 베이커리로 바꾼다. 실제로 재생되는 곡은 `BakeryPaint`뿐이다(`BakeryHunt`는 MusicRules에서 쓰지 않는 예전 임시 파일).

## 18.6 로비(LobbyScene) 배경음 — 조금 밝게, 후보 4개 (요청 6)
- **지금**: R2에서 넣은 D단조 왈츠(낮게 친 피아노 + 첼로, 76bpm, 약 95초, `request1009_sound.py bgm_lobby`).
- **후보 4개**(같은 길이·반복 가능, "망가진 동화" 느낌은 남기되 더 밝게):
  - **A 장조 왈츠**: 같은 왈츠를 D장조로, 피아노 + 현 + 멀리 오르골, 템포 84.
  - **B 모험의 시작**: F장조 6/8, 피치카토 현 + 클라리넷 선율 + 가벼운 북, 살짝 장난스러움.
  - **C 따뜻한 밤 마을**: G장조 느린 4/4, 어쿠스틱 기타·첼레스타 대신 마림바·부드러운 패드, 아늑함.
  - **D 신비로운 축제 전야**: 리디안(밝지만 묘한) 3/4, 하프 아르페지오 + 플루트 + 합창 패드.
- 청취 묶음(지금 곡 → A·B·C·D 각 앞 30초)과 전곡 파일을 `Source~/r1011/review/`에 두고, **고르신 뒤에만** 교체한다.

## 18.7 단계
| 단계 | 내용 | 상태 |
|---|---|---|
| W0 | §18.8 결정 — ✅ Q1~Q4·Q6 제안대로, Q5 (a) 벨 같은 소리 빼고 더 어둡게 | ✅ 2026-10-11 |
| W1 | 18.1 포탈 인형 삭제 | ✅ 2026-10-11 |
| W2 | 18.3 열린 상자 우선 | ✅ 2026-10-11 |
| W3 | 18.2 Blender 리깅·동작 3개·내보내기 | ✅ 2026-10-11 |
| W4 | 18.2 Unity 마녀 교체·주먹 내리침 | ✅ 2026-10-11 |
| W5 | 18.4 마녀 등장 카메라 연출 | ✅ 2026-10-11 |
| W6 | 18.6 로비 곡 후보 4개 → 승인 → 교체 / 18.5 베이커리 곡(Q5에 따라) | ✅ 베이커리 교체 · 로비 곡 `Lobby_Long1_25_Bass2`로 교체(한 번 재생) |
| W7 | 시험·Play Mode·빌드 | ✅ 2026-10-11 |
| W8 | 문서 | ✅ 2026-10-11 |

## 18.8 결정 질문
| # | 질문 | 제안 |
|---|---|---|
| Q1 | 쿠키가 죽는 시점: 주먹이 맵에 닿는 순간 vs 지금처럼 신호 즉시 | **닿는 순간**(신호 뒤 약 1초) — 보이는 것과 죽음이 맞음 |
| Q2 | 주먹: 두 주먹으로 함께 내리침 vs 한 주먹 | **두 주먹** — 90m 크기에서 가장 잘 보임 |
| Q3 | 카메라 연출 7초 동안 움직임 막기 + 그만큼 타임어택 7초 늘리기 | **둘 다 예** — 못 보는 동안 잡히거나 시간을 잃지 않게 |
| Q4 | 연출을 관전자(탈출·부서진 사람)에게도 보여 주나 | **예**(모두) |
| Q5 | 베이커리 곡: (a) 벨 같은 소리(셀레스타·오르골·반짝임)를 빼고 더 어두운 유령 베이커리로 / (b) 지금 그대로 둠 / (c) 반대로 그 크리스마스 느낌을 더 살림 | 말씀만으로는 확실하지 않아 골라 주세요. 제안은 **(a)** |
| Q6 | 마녀 나타나기 5초 투명→불투명이 카메라가 도착하는 3초보다 길어 클로즈업 때 반투명 | **나타나기를 3초로** 줄인다 |

## 18.9 결과 기록 ✅ 2026-10-11
- **W1 포탈 인형**: `RuneAltarSequence`에서 떠 있는 탑승객 인형(`UpdateFloating`·`CreateFloater`·관련 필드)을 지웠다. 탑승 순간 빨려 드는 연출(`BoardingFx.Suck`)은 그대로. Play Mode(진저브레드): 포탈 열림·2명 탑승 → `Float_` 자리 7개, 인형 0개. 사진 `images/w_portal_after.jpg`.
- **W2 열린 상자 우선**: `EscapeAuthority.PickRespawnChest` — 후보는 **열려 있고 빈 일반 상자** → 없으면 빈 일반 상자 → 없으면 장치 옆 바닥. 고른 상자는 닫는다. Play Mode: 상자 c0(안 열림·빈)·c1(열림·빈) 중 스파이가 로켓에 끼운 재료가 c1에 들어가고 c1이 닫힘.
- **W3 Blender 리깅**(Blender 4.5, `마녀FBX/Witch_Rigged.blend`): 정점 15.7만 → 3.98만, 뼈 52개(척추·목·머리·모자 3·팔·**손가락 3개×3마디 + 엄지 2마디**·다리·치마 4·머리카락 3). 자동 가중치 + 모자/얼굴은 규칙으로 다시 칠함(팔을 들 때 모자 챙이 휘던 문제). 동작 3개 — **Idle**(4초 반복), **Rise**(3초, 숙였다 일어섬), **Strike**(3.4초: 두 팔을 머리 위로 들며 주먹을 꽉 쥠 → 0.95초에 **두 주먹으로 맵을 내리침** → 짚은 채 잠시 → 일어섬).
- **W4 Unity 교체**: `Assets/09. Environment/Witch/`(FBX·텍스처·재질 `ME_WitchScarecrow`·`WitchAnimator`), 프리팹 `04. Prefabs/Escape/WITCH_Scarecrow`(키 90m, 발이 기준점에). 도구 `Tools/Escape/Setup Witch Model (Scarecrow)`로 맵 5개 `WITCH_Anchor`의 Model을 교체(5/5). 주의: `Escape/Models` 폴더는 가져오기 규칙이 애니메이션을 끄기 때문에 그 밖에 둔다.
  - `WitchPresenter`: 나타날 때 Rise, `WitchStrike` 때 Strike. 주먹이 닿는 순간 카메라 흔들림·충격음과 함께 **`WitchPresenter.Impacted`** 신호 → 쿠키·괴물이 그 순간 죽는다(Q1). 처음엔 1초 타이머로 죽였는데 Play Mode에서 주먹보다 약 0.3초 먼저 죽어서, 화면의 충격과 같은 프레임에 죽도록 신호로 바꿨다(마녀가 없거나 멈춘 화면 대비 1.95초 뒤 안전 처리).
  - Play Mode(베이커리) 기록: 신호 0초 → 주먹이 머리 위 80m(0.5초) → 0.93~1.0초 땅(2~4m)에 닿음 → **같은 프레임에 흔들림 시작·쿠키 파괴** → 3.5초 Idle. 사진 `images/w_witch_strike.jpg`(팔 듦·주먹 쥠·내리침·일어섬), `images/w_witch_unity.jpg`.
- **W5 마녀 등장 카메라**: 새 `Camera/WitchRevealCamera`(Main Camera, 모든 카메라 뒤에 실행). 0~3초 얼굴 정면 클로즈업으로 이동 → 3~4초 멈춤 → 4~7초 내 시점으로 복귀. 그동안 조작 잠금(`PlayerInput.CutsceneActive`), 타임어택은 7초 더 김(Q3), 관전자도 봄(Q4), 마녀 나타나기 3초(Q6).
  - 첫 Play Mode에서 이동 중 카메라가 마녀 몸 가까이(7m) 지나가고 화면이 엉뚱한 곳을 보는 구간이 있어 `BlendPose`로 고쳤다: 얼굴까지 거리는 클로즈업 거리(18m) 아래로 내려가지 않고, 시선은 위치보다 먼저 얼굴로 돈다. 다시 잰 값 — 거리 145m → 18m(2.3초) → 18m 유지 → 205m, 시선과 얼굴 각도 1.5~5.5초 0°, 7초에 잠금 해제. 사진 `images/w_reveal_camera.jpg`(0.3·1.5·3.3·3.8·5.5·7.6초).
- **W6 소리**:
  - 베이커리(Q5 a): `BakeryPaint`를 새로 만듦 — E단조 60bpm 24마디(96초 반복), 같은 주제를 **낮은 첼로**로, 떨리는 현·낮은 합창·드문 피치카토·콘트라베이스·오르간 저음. 셀레스타·오르골·반짝임 없음. 에너지 중심 316 → 173Hz, 셀레스타가 있던 1~2kHz 비율 13.6% → 0.5%. 예전 곡은 `Source~/r1011_backup/BGM/BakeryPaint.ogg`, 비교 `Source~/r1011/review/reel_bakery_before_after.ogg`. Play Mode(베이커리): 변장 시간에 `BakeryPaint`(96초) 재생 확인.
  - 로비 1차 후보 A~D(밝은 곡 4개)는 요청으로 **삭제**(2026-10-11).
  - 로비 2차 후보(96초 반복판)는 "너무 길다"는 요청으로 3차로 고쳤다(2026-10-11). 요청으로 2차 판도 같은 시드로 다시 만들어 **`BGM/Lobby_Long1~4.ogg`**로 함께 둔다(0~38초 밝게 → 38~70초 지지직·음 처짐 → 70~92초 공포 → 92~96초 되감기로 처음에 이어지는 반복판, 원본과 피크·크기 같음 확인).
  - **로비 3차 후보 4개** — `Source~/r1011/review/BGM/Lobby_Crash1~4.ogg`(각 30초, **한 번만 재생, 반복 없음**), 묶음 `reel_lobby_crash.ogg`(4곡 이어 붙임):
    - 흐름(5차, 2026-10-11 — 4차의 끝 나팔은 부자연스럽다는 의견으로 삭제): 0~10초 신나는 곡 → **10초부터 끝까지 점점** 지지직(레코드 먼지·라디오 잡음·전기 끊김·판 튐)이 커지고, 테이프가 늘어지듯 속도와 음이 함께 내려가며(끝쪽 약 −5~−7반음) 소리가 먹먹해지고, 공포(낮은 울림·불협 현·거꾸로 흐르는 합창 등)가 섞여 올라옴 → 27~30초 사라지며 **30초에 끝**(한 번 재생, 반복 없음). 측정: 5kHz 위 잡음 비율이 10~12초부터 오르기 시작해 24~26초에 가장 큼, 크기는 처음부터 끝까지 −20~−25dB로 고름.
    - 1 장난감 병정 행진곡 → 심장 박동, 2 사탕 가게 폴카 → 라디오 잡음·삐걱임, 3 생일 파티 오르골 왈츠 → 가장 깊게 처짐·속삭임, 4 오락실 칩튠 → 화면이 깨지듯 지직·찌그러진 저음.
    - 크기: 밝은 부분과 공포 부분 같은 크기(2초 평균 −20~−25dB), 끝은 0으로 닫힘. 스펙트로그램에서 10~13초에 음이 아래로 미끄러지는 것 확인.
  - **로비 6차 후보 `BGM/Lobby_Short_A~D.ogg`(각 25초, 한 번 재생)** — 요청: Lobby_Long1의 1:09~1:11처럼 끊어지되, 음이 오르내리지 않고 **점점 낮아지며 끊어지게**, 25초로 짧게, 기존 파일은 그대로. 묶음 `reel_lobby_short.ogg`.
    - 흐름: 0~9초 신나는 곡 → 9~20.5초 지지직이 커지며 **음이 한 방향으로만** 내려가고(떨림 없음) 공포가 섞임 → 20.5~23초 곡·공포·잡음이 **전축 전원이 꺼지듯 함께 쭉 내려가며 멈춤** → 23~25초 옅은 바늘 잡음이 사라지며 끝.
    - 오르내림 원인을 뺐다: 속도 떨림(wow)·끝 바닥에서 비틀거림 없음, 판 튐은 앞부분(10.5~15.5초)에만, 멈추기 직전 2.5초는 끊김 버스트 없음, 공포 바닥도 같은 속도로 함께 내려감.
    - A 장난감 병정 행진곡(Long1과 같은 곡), B 사탕 가게 폴카, C 생일 오르골 왈츠, D 오락실 칩튠. 측정: 20.5초부터 −21 → −25 → −30 → −38 → −50dB로 내려가 23초에 멈춤, 스펙트로그램에서 모든 음이 아래로 휘며 0으로.
  - **`BGM/Lobby_Short_A_Cut.ogg`(20.5초)** — 요청: Short_A가 낫지만 끝의 "띠요옹" 하며 꺼지는 부분이 애매하니 완벽하게 끊기게. Short_A와 같은 곡(20.4초까지 차이 −28dB, 크기 0.8% 차이)이고, "띠요옹"이 시작되던 **20.5초에 곡·공포·잡음·잔향이 한꺼번에 뚝 끊김**(15ms만 줄여 딸깍 소리 방지), 그 뒤 소리 없음·파일 끝. Short_A는 그대로 둠.
  - **`BGM/Lobby_A_Slow.ogg`(27.6초, 한 번 재생)** — 요청: 밝은 Lobby_A로 가다가 10~13초부터 두껍고 낮은 음으로 템포가 점점 느려지며 25~30초에 자연스럽게 끝나게.
    - 0~10.7초: 1차 후보 A(D장조 왈츠, 피아노·현·콘트라베이스, 84bpm) 그대로의 선율(같은 화음·시드).
    - 10.7~13초: 밝은 피아노 편성이 빠지고 낮은 편성이 들어옴 → 13초부터 선율을 첼로(한 옥타브 아래)·바순(두 옥타브 아래)이 받고 낮은 현·오르간·튜바·콘트라베이스·팀파니가 두껍게 받침. 화음 F#m → G → Bm → A → D.
    - 템포: 10.7초부터 MIDI 템포가 박마다 84 → 약 44bpm으로 점점 느려짐(음 높이는 그대로, 연주만 느려짐 — 테이프처럼 음이 처지지 않음).
    - 끝: 낮은 D 화음(팀파니 한 번)에 내려앉아 울리다 사라짐 → 27.6초 파일 끝.
    - 측정: 크기 밝은 부분 −20~−31dB(곡의 강약), 낮은 부분 −23~−24dB로 고름(넘어가는 곳에서 잔향이 겹쳐 커지던 것을 눌러 맞춤), 1kHz 위 비율 31% → 15%.
  - **`BGM/Lobby_Short_A_Slow.ogg`(27.7초, 한 번 재생)** — 요청: Lobby_A_Slow는 "아름다운 영화가 감동으로 끝나는 느낌"이라 아님, **Lobby_Short_A를 토대로** 10~13초부터 두껍고 낮은 음으로 템포가 점점 느려지며 25~30초에 자연스럽게 끝나게.
    - 0~9.5초: Short_A와 같은 장난감 병정 행진곡(실로폰 선율·튜바 쿵짝·피치카토·작은북, 같은 선율 시드).
    - 9.5~13초: 밝은 편성이 빠지고, 같은 행진곡을 바순·첼로(두 옥타브 아래)·트롬본 화음·튜바·콘트라베이스·팀파니·큰북·떨리는 낮은 현이 무겁게 이어받음. 화음은 단조로 일그러짐(Ab → Fm → G → Cm → G).
    - 템포: 9.5초부터 박마다 126 → 약 55bpm으로 점점 느려짐. Short_A처럼 음은 아래로만 살짝 처짐(약 −1.8반음, 떨림 없음), 넘어가고 5초 안에 먹먹하고 어두워짐. Short_A의 지지직(같은 종류)·공포 바닥(낮은 울림·불협 현·거꾸로 합창·심장 박동)이 함께 커짐.
    - 끝(22.5초): 낮고 불협한 C단조 화음(C2·G2·C3·E♭3·F♯3, 팀파니·큰북 "둥")이 울리며 공포·잡음과 함께 2.5초에 걸쳐 사그라짐 → 27.7초 끝. "띠요옹"·감동적인 마무리 없음.
    - 측정: 크기 1초 평균 −19~−26dB로 고름(넘어가는 곳 0.5초만 −27dB), 선율 음역 실로폰(약 520~1300Hz) → 바순·첼로(약 130~330Hz).
  - **`BGM/Lobby_Long1_Slow.ogg`(29.9초, 한 번 재생)** — 요청: Lobby_Long1을 끝까지 밝게 가져가 마지막까지 완벽하게 끝난 곡을 기준으로 25초로 줄이고, 10~13초부터 두껍고 낮은 음으로 템포가 점점 느려지며 30초쯤 자연스럽게 끝나게.
    - 기준 곡(밝은 완결판): Long1과 같은 장난감 병정 행진곡(C장조 126bpm, 같은 선율 시드) 13마디 = 약 24초 — C G C F | C G C C | F C G G | C "짠!".
    - 0~9.5초: 밝은 편성 그대로(실로폰·트럼펫·튜바 쿵짝·피치카토·작은북).
    - 9.5~13초: 밝은 편성이 빠지고 같은 행진곡을 튜바·바순(선율, 두 옥타브 아래)·낮은 트롬본 화음(작게)·튜바·콘트라베이스 쿵짝·팀파니·큰북이 이어받음. 화음은 장조 그대로, 지지직·공포는 섞지 않음(Long1 처음의 아주 옅은 레코드 먼지만). 낮은 편성은 1.2kHz 위를 덜어 내 먹먹하고 묵직하게.
    - 템포: 9.5초부터 126 → 59bpm으로 점점 느려짐(음 높이는 그대로).
    - 끝: 행진곡의 마지막 "짠!"(튜바·팀파니·큰북 한 번)이 28.0초 → 울림이 사라지며 29.9초 끝. 현·오르간 같은 영화 같은 긴 화음 없음.
    - 측정: 크기 1초 평균 −19~−25dB(넘어가는 10초쯤 −27dB), 뒷부분 1kHz 위 0.1%·150~400Hz 68%(앞부분은 1kHz 위 4%).
  - **`BGM/Lobby_Long1_25.ogg`(25초)** — 요청: Lobby_Long1이 딱 괜찮으니 그 곡을 25초쯤으로 줄이고, 마지막에 너무 시끄러운 "치이이익~"을 줄여 달라.
    - Long1과 같은 곡·같은 시드·같은 곡선을 시간 비율(25/96)대로 줄임: 0~9.9초 밝은 행진곡 → 9.9~18.2초 지지직·음 처짐 → 18.2~23.8초 공포 → 23.8~25초 되감기로 처음에 이어짐(반복 이음매 0.0005).
    - 끝 되감기("치이익"): 원본은 공포 부분보다 5dB 크고 69%가 3kHz 위 → 이번 판은 공포 부분보다 13dB 작고 3kHz 위 7%(크기 0.35배·높은 잡음 0.25 → 0.06·3.5kHz 위 깎음).
  - **`BGM/Lobby_Long1_25_End.ogg`(28.0초, 한 번 재생)** — 요청: Long1_25 그대로 두고, 마지막에 음이 올라가며 끊기는 부분(되감기)을 없애고 음을 더 길게 해도 되니 완벽하게 끝나게.
    - 0~23.8초: Long1_25와 같음(같은 곡선·잡음·공포 바닥, 차이 −21~−28dB).
    - 23.8초부터: 되감기 없음. 낮게 처진 곡·공포가 4.2초 더 이어지며 음은 아래로만 아주 조금 내려감(떨림 없음), 공포 바닥은 천천히 사라짐 → 마지막 2초에 잔향까지 모두 조용해져 28.0초에 완전히 끝(마지막 표본 0).
    - 측정: 22~28초 크기 −20 → −25 → −30 → −36 → −53 → −70dB.
  - **`BGM/Lobby_Long1_25_Bass.ogg`(28.0초, 한 번 재생)** — 요청: Long1_25_End에서 마지막 7초는 베이스음만 남기고 템포를 느리게 해 자연스럽게 끝내기.
    - 0~21초: Long1_25_End와 같음(차이 −33dB).
    - 21초부터: 선율·반주·북·공포·지지직이 1초 안에 빠지고 **튜바 베이스만** 남음(22.5초 뒤 300Hz 아래 99%). 베이스는 그 순간 처진 음 높이에 맞춰 일정하게(미끄러짐 없음) **E2 → D2 → C2로 내려가기만** 하고, 템포는 처음의 절반까지 느려짐(음 간격 1.8초 → 2.1초).
    - 끝: 낮은 C2가 길게 울리다 28.0초에 완전히 조용해짐(마지막 표본 0).
  - **`BGM/Lobby_Long1_25_Bass2.ogg`(28.0초)** — 요청: 16초에 나오는 "치이이익"이 13초쯤부터 나오게. 원인은 16.14초의 전기 끊김 버스트(0.46초, 60Hz 지직 + 곡이 잠깐 끊김) → 그 버스트만 13.0초로 옮기고 16초에서 나던 크기 그대로 맞춤. 나머지(판 튐·잡음 흐름·마지막 7초 베이스)는 Lobby_Long1_25_Bass와 같음.
  - **✅ 로비 곡 교체 완료(2026-10-11)** — 사용자 선택: `Lobby_Long1_25_Bass2`.
    - `BGM/Lobby.ogg` 내용을 교체(같은 .meta·GUID — 카탈로그 연결 그대로), 예전 곡(D단조 왈츠 94.7초)은 `Source~/r1011_backup/BGM/Lobby.ogg`, 새 곡 원본 사본은 `Source~/r1011/final/BGM/Lobby.ogg`.
    - **한 번만 재생**: `MusicCatalogSO.IsLoopById`에서 `MusicId.Lobby`만 반복 끔(다른 반복 곡·스팅어·징글 규칙은 그대로), 카탈로그 Lobby 항목 `loop = false`. 끝나면 `MusicPlayer`가 같은 곡을 다시 틀지 않아 조용해진다(로비 씬에 다시 들어오거나 결과 화면 뒤 로비 곡이 다시 고를 때만 처음부터).
    - 나머지 로비 후보(Crash1~4·Long1~4·Short_A~D·Short_A_Cut·A_Slow·Short_A_Slow·Long1_Slow·Long1_25·Long1_25_End·Long1_25_Bass·Long1_25_Bass2와 묶음 reel 2개, 총 24개)는 요청으로 삭제. 베이커리 비교 파일은 남김.
    - 작업 중 `Camera/WitchRevealCamera.cs` 38줄에 실수로 입력된 한글 자모("Proㅈㅇㅇㅇㅇㅁㅁ…jectOnPlane")로 컴파일 오류가 나 있어, 그 글자만 지워 `ProjectOnPlane`으로 되돌림(다른 줄 변경 없음 확인).
    - 시험: `Request1011Tests` 31개(새 `LobbyMusic_IsNewOneShotSong`), `AudioTests` 58개 통과. Play Mode(LobbyScene): `Lobby` 28.0초·loop 꺼짐으로 재생 → 26.5초로 건너뛰어 끝까지 → 재생 멈춘 뒤 다시 시작하지 않음, 콘솔 오류·경고 0.
- **W7 시험**: `Request1011Tests` 30개 통과(새로: 포탈 인형 없음, 열린 빈 상자 우선·차례, 마녀 FBX 동작 3개·애니메이터·프리팹·손가락 뼈, 카메라 가중치·클로즈업 자세·**이동 중 마녀를 뚫지 않음**, 연출 7초·나타나기 3초, 연출 중 조작 막힘, 새 베이커리 곡). 전체 EditMode 245개: 통과 238, 실패 6(맵 씬의 옛 OfflineModeBootstrap — 기존 항목), 판정 보류 1. 빌드 `Builds/R1011Check/`(새로 빌드) 성공 — 오류 0, 경고 1(TMP 동적 한글 글꼴 가져오기 경고, 기존). Play Mode 콘솔 오류·경고 0.

---

# 19. 2026-10-11 요청 4건 — 로켓 뒤 마녀 클로즈업·쿠키가 모두 탈출하면 마녀가 괴물·스파이를 없앰·훔치기 10초·대기실 강퇴 (단계 K0~K8)

- 작성: 2026-10-11 · 상태: **✅ 완료(2026-10-11) — 결과 §19.10**
- 요청
  1. 스파이가 로켓을 타고 날아가는 것을 보여 준 **다음에** 마녀 클로즈업 — **모두**(괴물·쿠키·관전자, Q1).
  2. 쿠키가 탈출구를 열고 **쿠키가 모두 탈출했을 때**, 스파이가 남아 있으면 **스파이·괴물 화면에서 탈출구(떠나는 탈것) 클로즈업 → 마녀 클로즈업**, 그 뒤 **마녀가 괴물과 스파이를 모두 없앤다**. 스파이가 없거나 이미 모두 잡혔으면 **지금처럼 바로 게임 끝**(Q2~Q4).
  3. 스파이가 장치에서 재료를 뺄 때 **E를 10초** 누르고 있어야 훔칠 수 있게.
  4. **GameLobbyScene에서 방장(호스트)이 플레이어를 강퇴** — 다시 들어오는 것은 막지 않음(Q6).

## 19.1 지금 코드(읽은 결과)
- **로켓 발사와 마녀**: `EscapeAuthority.TryLaunchRocket`(`Escape/EscapeAuthority.cs:373~391`)가 남은 스파이가 모두 타면 `SpyEscapedAt = 지금`, 남은 쿠키가 있으면 `TimeAttackEndTime = 지금 + 타임어택 + 7초`를 쓴다.
  - 마녀(`WitchPresenter.Update`, `:81`), 마녀 클로즈업(`WitchRevealCamera.TryElapsed`, `:62`), 멈춘 방 시간 표시(`PhaseCountdownDisplay`, `:39`)가 **모두 `SpyEscapedAt`을 시작 시각으로 쓴다** → 로켓이 막 점화될 때(0~2초) 이미 마녀가 나타나고 카메라가 마녀 얼굴로 간다. 로켓은 점화 2초 + 가속, 7초 뒤 사라진다(`SpyRocket`, `HideAfterSeconds = 7`).
  - 로켓을 따라 보는 카메라는 로켓에 탄 스파이 본인만(`SpectatorController.VehicleFocus`, `:121~129`).
- **쿠키 탈출**: 탈출구가 열리면(`Install` → `CompletedAt`) 쿠키가 타서 기다리고, 밖에 남은 쿠키(스파이 제외)가 없으면 한꺼번에 출발한다(`TryDepart`, `:282~291` → `DepartedAt`). 맵마다 출발 연출 `DepartureSeconds`(케이크 로켓·롤러코스터 7초, 오븐·포탈은 그보다 짧음)와 카메라가 따라 볼 곳 `DepartureFocus`(케이크 로켓·첫 차량·오븐 입구·포탈)가 이미 있다(`Escape/EscapeSequence.cs`, `Sequences/*.cs`).
- **출발 뒤 끝 판정**(`EscapeRules.Evaluate`, `Escape/EscapeRules.cs:28~53`): 출발 연출 중에는 미루고, 끝나면 "남은 쿠키·스파이가 모두 0"일 때만 끝(`EndAllResolved`). **스파이가 남아 있으면 방 제한시간까지 계속**된다(지금은 이때 마녀가 오지 않는다).
- **마녀가 내리칠 때 누가 죽나**: Room Prop `WitchStrike`(int 1) → 주먹이 닿는 순간(`WitchPresenter.Impacted`) 각 화면이 자기 캐릭터를 처리 — 탈출 안 한 쿠키(스파이 포함)는 죽음(`EscapeCharacterState.DieByWitch`), 괴물도 죽음(`MonsterEscapeState`). 지금은 **타임어택이 끝났을 때만** 방장(`GameRuleController`)이 쓴다.
- **훔치기**: `EscapeDevice.HoldSecondsFor`(`:238`)가 끼우기·빼기 모두 2초(`:8` "둘이 같은 시간이라 스파이가 드러나지 않게"). 방장(`EscapeAuthority.Steal`, `:220~235`)은 누른 시간을 검사하지 않는다. 길게 누르기는 `CharacterInteractor`가 진행 링을 그리고, 떼거나·다른 곳을 보거나·쓸 수 없게 되면 취소한다(`Interaction/CharacterInteractor.cs:50~81`).
- **대기실**: `GameLobbyController.RefreshPlayerList`가 `PlayerListItem`(이름만)을 입장 순서로 만든다. 시작 버튼 주인 = 호스트(가장 먼저 들어온 사람, `RoomState.HostActor`), 진행 권한 = Photon 방장(괴물이 아닌 사람) — 둘이 다를 수 있어 시작·방 설정은 "호스트 요청 → 방장 실행"(`NetEventCodes` 5·10). 강퇴 기능 없음. 확인 창 `GameManager/ConfirmDialog.cs`, 방 나가기 `GameManager/RoomExitController.cs`(나가면 LobbyScene, 로비 문구 `LobbyController.ShowError`)가 있다. PUN 2.55의 `CloseConnection`은 받는 쪽이 `EnableCloseConnection`을 켜야만 끊기는(본인 협조) 방식이다.

## 19.2 공통 — 마녀 등장 시각·연출 샷
- **새 Room Prop**(판마다 지움, `NetKeys` Round): `WitchAppearAt`(double) = 마녀가 나타나기 시작하는 시각, `WitchMode`(int) = 1 로켓 타임어택 / 2 탈출 뒤 스파이 처단.
- 마녀(`WitchPresenter`)·클로즈업 카메라·멈춘 시간 표시·마녀 등장 소리는 `SpyEscapedAt` 대신 `WitchAppearAt`을 기준으로 한다(로켓 발사 시각은 로켓 연출·관전 카메라에만).
- **카메라**: `WitchRevealCamera`를 "샷 목록 + 보는 사람" 방식으로 넓힌다(지금 섞기·얼굴까지 최소 거리·조작 잠금 그대로).
  - 샷 R(로켓): 로켓 발사부터 7초 — 로켓을 옆·아래에서 올려다보며 떠오르는 로켓을 따라감.
  - 샷 E(탈출구): 쿠키 출발부터 출발 연출 끝까지 — 그 맵의 `DepartureFocus`(떠나는 탈것)를 바라봄.
  - 샷 W(마녀): `WitchAppearAt`부터 3초 이동 → 1초 정면 → 3초 복귀(지금과 같음).
  - 앞 샷에서 다음 샷으로는 내 시점으로 돌아가지 않고 바로 이어서 옮겨 간다.

## 19.3 요청 1 — 로켓이 날아가는 것을 보여 준 뒤 마녀 클로즈업(모두)
- `TryLaunchRocket`: 남은 쿠키가 있으면(지금과 같은 조건) `WitchMode = 1`, `WitchAppearAt = 발사 + 7초`, `TimeAttackEndTime = WitchAppearAt + 7초 + 타임어택`(연출 14초 동안 잃는 시간 없음).
- 보는 사람: **모두**(괴물·남은 쿠키·관전자·탄 스파이). 샷 R 7초 → 샷 W 7초, 그동안 조작 잠금.
- 남은 쿠키가 없어 타임어택이 없으면(D10) 지금처럼 로켓만 떠나고 마녀는 없다.

## 19.4 요청 2 — 쿠키가 모두 탈출하면: 스파이·괴물 화면 탈출구 → 마녀 클로즈업, 마녀가 괴물·스파이를 없앰
- **방장 판정**(`EscapeAuthority`): 쿠키 탈것이 출발한 순간(`TryDepart`가 `DepartedAt`을 쓸 때, 방장 교체 뒤 `Reevaluate`에서도 한 번):
  - 살아 있고 로켓에 타지 않은 **스파이가 1명 이상**이고 로켓 타임어택 중이 아니면 → `WitchMode = 2`, `WitchAppearAt = DepartedAt + 출발 연출 길이`(샷 E가 끝나는 순간).
  - 스파이가 없거나 모두 잡혔으면 → 아무것도 쓰지 않는다 → 출발 연출 뒤 지금처럼 바로 끝(`EndAllResolved`).
- **보는 사람**: 남은 스파이와 괴물(Q4 — 쿠키는 다 탈출해 필요 없음). 샷 E → 샷 W, 그동안 조작 잠금(괴물이 스파이를 잡지 못하고, 스파이도 달아나지 못함).
- **내리침**: 방장(`GameRuleController`)이 `WitchAppearAt + 7초`(클로즈업이 끝난 순간)에 `WitchStrike = 2`를 한 번 쓴다 → 주먹이 닿는 순간 **괴물(모두)과 남은 스파이**가 죽는다(쿠키는 이미 탈출). 결과 사망 이유 "마녀에 의해 사망".
- **끝 판정**(`EscapeRules`): `WitchMode = 2`인 동안은 방 제한시간이 끝나도 기다리고(연출 도중 끝나지 않게), 내리친 뒤 2.5초에 `EndWitchStrike`로 끝. 쿠키 결과는 탈출 성공 그대로.
- **경계**: 연출 도중 스파이가 나가면 괴물만 처단하고 끝, 괴물이 모두 나가면 스파이만 처단하고 끝(괴물 이탈 복귀 경고보다 이 연출이 먼저 — `RoomLifecycleWatcher`가 `WitchMode = 2`면 대기실 복귀를 하지 않는다).

## 19.5 요청 3 — 훔치기는 E를 10초
- `EscapeDevice`: `[SerializeField] stealHoldSeconds = 10`, `HoldSecondsFor`가 지금 손댈 칸이 "빼기"면 10초, "끼우기"면 지금처럼 2초. 진행 링·취소 규칙은 `CharacterInteractor` 그대로.
- **Q5 제안 — 방장도 10초를 검사한다**:
  - 이유: 10초는 스파이 쪽 균형을 정하는 핵심 규칙인데, 화면 쪽에서만 세면 조작한 클라이언트가 바로 훔칠 수 있다. 추가 비용은 작다(요청 1개·몇 줄).
  - 방법: 누르기 시작할 때 `StealBegin(칸)` 요청을 하나 더 보내고, 방장은 그 시각·위치를 기억했다가 `Steal`이 왔을 때 **같은 칸·10초 − 0.5초(네트워크 여유) 이상 지남·여전히 장치 가까이**일 때만 허락한다. 중간에 떼면 기억을 버린다(다음 `StealBegin`이 덮어씀).
  - 다른 길: 화면 쪽만 세기(지금 2초와 같은 방식). 일반 플레이에는 차이가 없다.
- 바뀌는 점: 끼우기(2초)와 빼기(10초)의 시간이 달라 오래 서 있는 모습으로 스파이가 들킬 수 있다 — 훔치기를 어렵게 하려는 요청 의도와 맞다.

## 19.6 요청 4 — GameLobbyScene에서 강퇴(다시 들어오기는 막지 않음)
- **누가·언제**: 호스트(시작 버튼 주인, `RoomState.IsLocalHost`)만, 자기 자신 제외, 대기실 단계(`GamePhase.Lobby`)에서만 — 판이 진행 중일 때(괴물이 대기실에서 기다리는 동안 등)는 버튼이 없다.
- **화면**: 참가자 목록 줄(`PlayerListItem`)마다 오른쪽 "강퇴" 버튼(호스트 화면에만) → `ConfirmDialog`로 "○○님을 강퇴할까요?" 확인. 한글 문구는 프리팹·씬에 `\uXXXX`. 프리팹 수정은 에디터 도구로.
- **흐름**(시작 버튼과 같은 구조): 호스트 → 방장에게 `KickRequest`(새 이벤트 11, 대상 액터 번호; 호스트가 방장이면 바로 처리) → 방장 검증(보낸 사람 = 호스트, 대상이 방에 있음·호스트 아님, 대기실 단계) → **대상에게만 `Kicked` 이벤트(12)** → 대상은 방을 나가고(`RoomExitController` 경로) LobbyScene에서 "방장에 의해 강퇴되었습니다"를 본다(로비의 오류 문구 칸).
  - 대상이 응답하지 않는 경우(조작 등)를 위해 모두 `EnableCloseConnection = true`를 켜고 방장이 `CloseConnection`도 함께 보낸다 — PUN 2.55에서는 받는 쪽이 협조해야 하므로 정상 클라이언트는 둘 중 먼저 오는 것으로 나간다.
  - 괴물로 뽑혀 있던 사람을 강퇴하면 기존 "대기실에서 괴물이 나감" 처리(`MonsterAssignmentAuthority.OnPlayerLeftRoom`)가 다시 뽑는다.
- 다시 들어오기는 막지 않는다(Q6).

## 19.7 결정
| # | 질문 | 결정 |
|---|---|---|
| Q1 | 로켓이 날아가는 장면을 누가 보나 | ✅ **모두** |
| Q2 | 쿠키가 모두 탈출했을 때 스파이가 없거나 모두 잡혔으면 | ✅ **그대로 게임 끝** / 스파이가 있으면 마녀가 나와 스파이·괴물을 모두 없앰 |
| Q3 | 언제 시작하나 | ✅ **쿠키가 모두 탈출했을 때**(탈것 출발) — 스파이 화면: 탈출구(떠나는 탈것) 클로즈업 → 마녀 클로즈업 → 처단 |
| Q4 | 괴물·쿠키 화면 | ✅ **괴물은 봄**, 쿠키는 이미 탈출해 필요 없음 |
| Q5 | 10초를 방장도 검사하나 | ✅ **검사함**(`StealBegin` 요청 + 방장이 시간·위치 확인) |
| Q6 | 강퇴된 사람의 재입장 | ✅ **막지 않음** |

## 19.8 영향·위험
- `WitchAppearAt` 도입으로 마녀·카메라·멈춘 시간·마녀 등장 소리의 시작 시각이 바뀐다 — `Request1011Tests`의 카메라·마녀 시험은 새 기준으로 고친다.
- 로켓 경로는 연출이 14초로 늘어 그동안 모두 조작할 수 없다(타임어택은 그만큼 늘림).
- 탈출 뒤 처단 경로: 출발 연출(약 7초) + 마녀 클로즈업(7초) + 주먹(약 1초) + 결과까지 2.5초 — 쿠키가 모두 탈출한 뒤 약 18초 뒤에 결과 화면.
- 강퇴는 대기실 단계에서만이라 진행 중인 판에는 영향 없음.

## 19.9 단계
| 단계 | 내용 | 상태 |
|---|---|---|
| K0 | §19.7 결정(Q5 확인) | ✅ 2026-10-11 |
| K1 | `WitchAppearAt`·`WitchMode` 키, 마녀·카메라·시간 표시·소리 기준 바꾸기, 카메라 샷 목록(R·E·W)과 보는 사람 | ✅ 2026-10-11 |
| K2 | 요청 1: 로켓 발사 → 모두 샷 R 7초 → 샷 W 7초, 타임어택 14초 연장 | ✅ 2026-10-11 |
| K3 | 요청 2: 출발 순간 방장 판정(스파이 있음/없음), 스파이·괴물 샷 E → 샷 W, `WitchStrike = 2` 처단(괴물·스파이만), 끝 판정·괴물 이탈·방장 교체 | ✅ 2026-10-11 |
| K4 | 요청 3: 훔치기 10초(+ Q5에 따라 `StealBegin` 방장 검사) | ✅ 2026-10-11 |
| K5 | 요청 4: 강퇴 요청·검증·퇴장·로비 문구, 목록 버튼·확인 창(에디터 도구로 프리팹) | ✅ 2026-10-11 |
| K6 | EditMode 시험: 샷 시각·보는 사람, 출발 순간 판정(Q2), 처단 대상, 끝 판정(WitchMode 2), 훔치기 시간·방장 검사, 강퇴 검증(호스트·자기 자신·단계) | ✅ 2026-10-11 |
| K7 | Play Mode(오프라인 스파이·괴물 흉내): ① 로켓 발사 → 로켓 7초 → 마녀 클로즈업 ② 쿠키 출발 → 탈출구 → 마녀 → 주먹 → 스파이·괴물 사망 → 결과 ③ 스파이 없음 → 바로 결과 ④ 훔치기 10초 링·조기 요청 거부 ⑤ 대기실 강퇴 버튼·확인 창(실제 강퇴는 빌드 + 에디터 2인) · 전체 시험 · 빌드 | ✅ 2026-10-11 |
| K8 | 문서·README | ✅ 2026-10-11 |

## 19.10 결과 기록 ✅ 2026-10-11
- **공통(K1)**: 새 Room Prop `WitchMode`(1 로켓 타임어택 / 2 쿠키 탈출 뒤 처단)·`WitchAppearAt`(마녀 등장 시각), 판마다 지움(`NetKeys` Round). 새 `Escape/WitchTimeline.cs`(시각 계산·처단 시작 조건·보는 사람·죽는 대상 — 순수 함수). 마녀(`WitchPresenter`)는 `WitchAppearAt`부터 나타나고(처단은 나타나는 3초 동안 맵을 향해 돌아섬), 카메라(`WitchRevealCamera`)는 **앞 샷(로켓 / 떠나는 탈것) → 마녀 클로즈업 → 내 시점**을 이어서 보여 준다(앞 샷 2초 이동, 대상에서 14 m·높이 4 m, 로켓이 떠오르면 35% 따라 오름). 로켓 발사 시각(`SpyEscapedAt`)은 로켓 연출·관전·멈춘 방 시간에만 남겼다.
- **요청 1(K2)**: `TryLaunchRocket`이 `WitchAppearAt = 발사 + 7초`, `TimeAttackEndTime = 등장 + 7초 + 타임어택`(연출 14초 동안 잃는 시간 없음). Play Mode(베이커리): 0~2초 모든 화면이 로켓으로 이동 → 2~7초 떠오르는 로켓(0 → 60 m)을 정면(각도 0°)으로 → 7.1초 마녀 등장 → 8.9~11.7초 얼굴 클로즈업(18 m) → 14.2초 조작 풀림. 사진 `images/k_rocket_witch.jpg`(1·3.5·6.5·9·10.5·13.5·15초).
- **요청 2(K3)**: 쿠키 탈것이 출발한 순간(`TryDepart`, 방장 교체 뒤 `Reevaluate`) 남은 스파이가 있으면 `WitchMode = 2`, `WitchAppearAt = 출발 + 출발 연출 길이`. 보는 사람 = 스파이·괴물. `GameRuleController`가 클로즈업이 끝난 순간 `WitchStrike = 2` → 주먹이 닿는 순간 괴물과 스파이만 죽음(`EscapeCharacterState`가 일반 쿠키는 건너뜀). 처단 중에는 방 제한시간이 끝나도 기다리고(`EscapeRules`), 괴물이 모두 나가도 대기실로 돌아가지 않는다(`RoomLifecycleWatcher`).
  - Play Mode(베이커리, 나 = 스파이, 괴물은 방에 없는 번호): 1.1초 출발 → 1.6~3.1초 카메라가 오븐 입구(탈출구)로 → 4.6초 마녀 등장(오븐 출발 연출 3.4초 뒤) → 7.7~8.7초 클로즈업 → 11.7초 조작 풀림·`WitchStrike = 2` → 12.7초 주먹이 닿으며 스파이 사망 → 14.2초 결과(이유 WitchStrike). 사진 `images/k_purge.jpg`.
  - 스파이 없음(나 = 탈출한 쿠키): 마녀 없음, 출발 연출이 끝난 4.6초에 결과(AllResolved) — 지금처럼 바로 끝.
- **요청 3(K4)**: 빼기만 10초(`EscapeAuthority.StealHoldSeconds`), 끼우기 2초 그대로. 누르기 시작할 때 `StealBegin`(새 요청 11)을 보내고(`IHoldStartListener` — `CharacterInteractor`가 길게 누르기를 시작할 때 부름), 방장은 같은 칸·9.5초 이상·장치 가까이일 때만 `Steal`을 허락한다. Play Mode: 시작 요청 없이 보낸 `Steal`은 거절(재료가 장치에 그대로), E를 누른 지 10초(1.0초 → 10.6~11.1초)에 재료가 스파이 손으로, 진행 링 사진 `images/k_steal_hold.jpg`.
- **요청 4(K5)**: 새 `Lobby/LobbyKick.cs`(검증·메시지), 이벤트 `KickRequest`(11)·`Kicked`(12). 호스트 화면 목록 줄마다 빨간 "강퇴" 버튼(`PlayerListItem.kickButton`, 자기 줄·판 진행 중에는 숨김) → 대기실의 기존 확인 창("○○님을 강퇴할까요?") → 호스트가 방장이면 바로, 아니면 방장에게 요청 → 방장이 검증해 대상에게만 `Kicked` → 대상은 평소 나가기 경로로 LobbyScene, "방장에 의해 강퇴되었습니다." 표시. 3초 뒤에도 남아 있으면 방장이 `CloseConnection`(모두 `EnableCloseConnection = true`). 다시 들어오기는 막지 않음. 프리팹·씬 문구는 에디터 도구 `Tools/Lobby/Setup Kick Button`(`Assets/Editor/Lobby/LobbyKickInstaller.cs`, 한글 `\uXXXX`).
  - Play Mode(대기실, 오프라인 혼자): 내 줄에는 버튼 없음, 다른 참가자 줄 모양·클릭 연결 확인(임시 줄), `Kicked` 이벤트를 받으면 로그 "[LobbyKick] Kicked by the host" → LobbyScene, 문구 표시. 사진 `images/k_kick.jpg`. **실제 2명 이상 온라인 강퇴는 사람이 직접 확인 필요**(오프라인은 혼자).
- **시험(K6)**: `Request1011Tests` 47개 통과(새 16개: 시각표·처단 조건·보는 사람·죽는 대상·처단 끝 판정(방 시간 무시)·훔치기 10초/여유·앞 샷 자세·강퇴 검증·버튼 프리팹·로비 문구 한 번). 전체 EditMode 262개: 통과 255, 실패 6(맵 씬의 옛 OfflineModeBootstrap — 기존 항목), 판정 보류 1. 빌드 `Builds/R1011Check/`(새로 빌드) 성공 — 오류 0, 경고 1(TMP 동적 한글 글꼴, 기존). Play Mode 콘솔 오류·경고 0(글꼴 가져오기 경고 1개는 기존).
- **알려 둘 점**: ~~처단에서 마녀가 내리치는 순간은 카메라가 내 시점으로 돌아온 뒤라 멀리 보인다~~ → §19.11에서 내리치기 샷으로 해결. 로켓 앞 샷으로 옮겨 가는 2초 동안 카메라가 건물 벽을 지나갈 수 있다.

## 19.11 추가: 처단 때 내리치는 장면도 클로즈업 ✅ 2026-10-11
- 요청: "처단 때 마녀가 내리치는 장면도 클로즈업으로 보여줘" — 예전에는 얼굴 클로즈업 뒤 내 시점으로 돌아온 다음 내리쳐서 멀리 보였다(§19.10 알려 둘 점).
- 바꾼 것(`Camera/WitchRevealCamera.cs`, `Escape/WitchTimeline.cs`): 처단(`WitchMode = 2`)만 얼굴 클로즈업(4초) 뒤 **1.5초 동안 "내리치기 샷"으로 옮겨 가 결과 화면까지 그대로 머문다**(최대 20초 안전장치). 내리치기 시각 `PurgeStrikeAt = 등장 + 3 + 1 + 1.5`(= 등장 + 5.5초)로 카메라가 자리 잡은 뒤 내리친다. 주먹이 닿을 때 화면 흔들림은 5배.
  - 샷 자리(`StrikePose`, 마녀 키 × 비율): 마녀 앞 0.8·옆 0.8(정면에서 45°)·높이 0.8, 바라보는 곳 = 발 앞 0.25·높이 0.54. 위에서 비스듬히 내려다봐 **맵 가장자리 나무에 가리지 않고**, 몸을 숙여 내리치는 팔이 옆모습으로 보이며, 머리부터 주먹이 닿는 바닥까지 한 화면(가로세로 60° 기준 화면 끝에서 15% 안쪽).
  - 타임어택(`WitchMode = 1`)은 그대로(클로즈업 → 내 시점).
- 시험: `Request1011Tests.PurgeStrikeShot_ShowsWholeWitchAndImpactGround`(발·머리·주먹 닿는 곳이 화면 안, 앞쪽 45° 옆, 거리), `PurgeStrikeAt` 기대값 수정 — 48개 통과. 전체 EditMode 263개: 통과 256, 실패 6·보류 1(기존 항목 그대로). 빌드 `Builds/R1011Check/` 성공(오류 0, 경고 1 기존 글꼴).
- Play Mode(베이커리, 나 = 스파이): 4.4초 마녀 등장 → 7~8초 얼굴 클로즈업(18 m) → 9.3초 45° 옆 위 샷에서 마녀 전체 → 9.9초 내리치기(`WitchStrike = 2`) → 10.4초 팔 들어올림 → 10.9~11.3초 숙여 주먹이 맵에 닿음·스파이 사망(11.1초) → 12.5초 결과 화면에서 조작 풀림. 사진 `images/k_purge_strike.jpg`(4·7.5·9.3·10.4·10.9·11.3·12.5·13.6초). 처음 시도(정면 낮은 자리)는 앞 나무가 마녀를 가려 버렸다.
- 알려 둘 점: 밤 맵이라 마녀 몸이 어둡다(얼굴은 보임). 더 밝게 보이게 하려면 샷 동안만 마녀를 비추는 조명을 더할 수 있다(요청 시).

# 20. 2026-10-11 소리 후보: 게임 뒤 결과 화면 곡 4개 · 베이커리 고장 톱니 4개(넣지 않음 — 들어 보고 고르기)

## 20.1 게임 뒤 결과 화면 곡 후보 4개
- 지금: 판이 끝나면(결과 단계) `MusicRules.Select`가 로비 곡(`MusicId.Lobby`)을 튼다(징글 → 4초에 걸쳐 들어옴). 로비 곡은 이제 28초 한 번 재생이라 결과 화면에 어울리지 않는다.
- 후보(모두 −20 LUFS, 이음매 없는 반복, 승패와 관계없이 듣는 차분한 여운) — `Assets/10. Audio/Source~/r1011/review/ResultBGM/`:
| 후보 | 분위기 | 박자·길이 | 악기 |
|---|---|---|---|
| `Result_A` 남은 불빛 | E단조, 조용한 회고 | 3/4 60, 72초 | 낮은 피아노 선율 → 가운데는 첼로, 왈츠 반주·현 패드 |
| `Result_B` 다 감긴 오르골 | D단조, 태엽이 풀리듯 두 번 늘어짐 | 3/4 72, 60초 | 오르골 + 셀레스타, 하프 아르페지오, 따뜻한 패드 |
| `Result_C` 문 닫은 놀이공원 | F단조, 사람 떠난 뒤의 쓸쓸함 | 3/4 56, 77초 | 멀리 아코디언 왈츠 + 칼리오페 메아리, 튜바, 긴 잔향 |
| `Result_D` 마녀의 자장가 | A 도리아, 꿈결 같지만 불길 | 3/4 54, 80초 | 허밍 합창 + 하프 반복음, 플루트, 작은 종 |
- 청취 묶음: `reel_result_candidates.ogg`(각 40초 A→B→C→D), `reel_jingle_to_result.ogg`(실패 징글 → 각 후보, 게임에서 들리는 순서), 순서 `reel_result_order.txt`.
- 고르면: `MusicId.Result` 추가 → `MusicRules`의 결과 단계를 `Result`로, 카탈로그·시험·Play Mode 확인(R7과 같은 절차).

## 20.2 베이커리 고장 톱니 효과음 후보 4개
- 지금: 탈출구(오븐) 완성 전 반복음 `SFX/Device/OvenBroken`에 **쉭 하는 증기(`steam`)가 계속 깔려** 톱니보다 증기처럼 들린다(§16.7 N7).
- 후보(증기 없음, 8초 이음매 없는 반복, −21 LUFS로 지금 소리와 같은 크기) — `Assets/10. Audio/Source~/r1011/review/OvenGearSFX/`:
| 후보 | 소리 |
|---|---|
| `OvenBroken_GearA` 이 빠진 톱니 | 일정한 쇠 톱니 딸깍딸깍 → 한 바퀴(12이)마다 빠진 이 틈에서 "덜컥 쾅" + 힘겨운 모터 웅웅 |
| `OvenBroken_GearB` 걸렸다 튕기는 톱니 | 점점 느려지며 끼익 갈리다 걸림 → "탕" 풀리며 빠르게 헛돎(8초에 두 번) |
| `OvenBroken_GearC` 헛도는 래칫 | 멈춤쇠가 이를 못 잡고 "드르르륵" 미끄러지다 "탁" 걸림 + 헐거운 볼트 달그락 |
| `OvenBroken_GearD` 녹슨 큰 톱니 | 느리고 무겁게 "끼익… 쿵" 한 이씩 겨우 넘어감 + 한 번 이가 부러져 쇳조각이 떨어짐 |
- 청취 묶음: `reel_gears.ogg`(지금 소리 → A×2 → B×2 → C×2 → D×2), 순서 `reel_gears_order.txt`. 고음 잡음(3 kHz 이상) 비율·끊김 정도로 확인: 지금 소리는 평평한 잡음, 후보는 툭툭 끊기는 기계음.
- 고르면: `OvenBroken.ogg`를 백업(`Source~/r1011_backup/`) 뒤 교체(같은 GUID) → 베이커리 Play Mode에서 완성 전 재생 확인.
- **2차(2026-10-11 요청: "중간중간 퉁 소리 대신 철과 철이 부딪히는 소리")**: 1차의 덜컥·쿵에 섞인 낮은 쿵(`low_thud`)·낮은 주철 울림·저음 웅을 빼고 `steel_clash`(날카로운 딱 + 오래 울리는 높은 비조화 공명 두 벌이 어긋나 떨림 + 작게 두 번 튕김, 300 Hz 아래 자름)로 바꿈 → `OvenBroken_Gear{A,B,C,D}2.ogg`, 청취 `reel_gears2.ogg`(1차 A → A2·B2·C2·D2 각 2번). 200 Hz 아래 비율 A 96→28%, B 81→26%, C 91→10%, D 99→0%. A2·C2는 쇳소리 순간이 커서 평균 크기가 2 dB·0.6 dB 작다(넣을 때 맞춤).
- **결정**: 결과 화면 곡 = **`Result_A`**(사용자 2026-10-11). 넣기는 톱니 선택과 함께(§20.1 "고르면" 절차).
- 만든 코드: `audio/final/r1011_result.py`, `audio/final/r1011_gears.py`, `audio/final/r1011_gears2.py`.

## 20.3 넣기 결과 ✅ 2026-10-11 (결정: 결과 곡 `Result_A`, 고장 오븐 `OvenBroken_GearB2`)
- **결과 화면 곡**: `MusicId.Result = 4`(반복 곡 구간) 추가(`Audio/SoundId.cs`), `MusicRules.Select`가 결과 단계에 `Result`를 고름(로비 곡은 로비 씬에서만). 음원 `Assets/10. Audio/BGM/Result.ogg`(= `Result_A`, 72초 반복, −20 LUFS, 원본 `Source~/r1011/final/BGM/Result.ogg`), 카탈로그는 `Tools/TagOfChaos/Audio/Build Catalogs`로 다시 만듦(loop 켬, 볼륨 1).
- **고장 오븐**: `Assets/10. Audio/SFX/Device/OvenBroken.wav`를 `OvenBroken_GearB2`로 교체(같은 파일·GUID, 8초 반복, −21 LUFS). 예전 소리 백업 `Source~/r1011_backup/SFX/Device/OvenBroken.wav`, 원본 `Source~/r1011/final/SFX/Device/OvenBroken.wav`. 소리 쪽 코드(`OvenSequence.AddLoop`)는 그대로.
- **시험**: `AudioTests.Music_SelectTable`(결과 단계 = `Result`) 수정, 새 `Request1011Tests.ResultMusic_IsResultALoop`(규칙·반복·카탈로그 클립 72초)·`OvenBroken_IsGearB2Loop`(8초, 이웃 샘플 차이 비율 > 0.17 — 쇳소리처럼 밝음, 예전 증기·쿵 소리는 0.10). `Request1011Tests` 50개·`AudioTests` 58개 통과. 전체 EditMode 265개: 통과 258, 실패 6·보류 1(기존 항목 그대로). 빌드 `Builds/R1011Check/` 성공(오류 0, 경고 1 기존 글꼴). 컴파일 오류·경고 0.
- **Play Mode(베이커리, 나 = 스파이, 처단으로 끝냄)**: 시작부터 탈출구 완성 전까지 `OvenBroken`(새 톱니, 반복) 재생 → 23초 결과 단계: 실패 징글 `JgEscapeFail`과 함께 `Result`가 0.03 → 0.20(4초 동안 커짐) → 28초 징글이 끝난 뒤 `Result` 0.40으로 반복. 콘솔 오류는 검증용 감시 코드(MCPDynamicCode)의 것 1건뿐, 게임 코드 오류·경고 0.

# 21. 2026-10-12 요청 2건 — 스파이가 로켓으로 떠난 뒤 괴물이 모두 나가도 판 계속 · 대기실에서 기다리는 괴물끼리 서로 안 보임 (단계 L0~L5)

- 작성: 2026-10-12 · 상태: **✅ L0~L4 완료(2026-10-12) — L5 멀티 확인은 사용자 · 결과 §21.7** (결정 Q1 a, Q2 D)
- 요청
  1. 스파이가 로켓을 타고 떠나 **마녀 타이머(타임어택)가 켜진 뒤에는**, 괴물이 나가서 0명이 되어도 대기실로 돌아가지 말고 판을 끝까지 진행. 지금은 5초 뒤 GameLobbyScene으로 간다.
  2. 7명 이상(괴물 2명)에서 A·B가 가마솥에 들어가고 방장이 시작하면, **대기실에 남아 기다리는 A·B가 서로를 못 본다**(서로의 시점에서 없어진 것처럼 보임).

## 21.1 지금 코드(읽은 결과)

### 요청 1 — 괴물 전원 이탈
- **판정 위치**: `Monster/RoomLifecycleWatcher.cs` `OnPlayerLeftRoom`(13~36행, 방장만). 나간 사람이 괴물이면 남은 괴물 = `MonsterRoster.Remaining(방장이 마지막으로 보낸 목록, 방에 남은 사람)`.
  - 남은 괴물이 0명이고 `witchPurging`(쿠키가 모두 탈출한 뒤 마녀가 스파이·괴물을 처단하러 오는 중, 27행)이 **아니면** `MonsterDepartedAt = 지금`을 쓴다(30~31행).
  - 그러면 5초(`GameSettings.MonsterDepartureReturnDelay`) 뒤 `Update`(45~53행)가 `RoomSceneTransition.LoadLevelForRoom(GameLobby)`로 모두 대기실에 보낸다.
  - 각 화면은 `MonsterDepartureBanner`(`MonsterDepartedAt`을 받으면 "괴물이 나가 n초 뒤 대기실로" 배너)를 띄운다.
- **즉 예외는 "처단(Purge)"뿐**이다. 스파이가 로켓을 탄 뒤의 타임어택(`WitchMode.TimeAttack`)은 예외에 없어서 요청 1의 현상이 난다.
- **스파이가 떠난 표시**: `EscapeAuthority.TryLaunchRocket`(`Escape/EscapeAuthority.cs`)이 남은 스파이가 모두 타면 `SpyEscapedAt`(로켓 발사 시각)을 **항상** 쓴다.
  - 남은 쿠키가 있으면 `WitchMode = TimeAttack`, `WitchAppearAt`, `TimeAttackEndTime`을 함께 쓴다.
  - 남은 쿠키가 없으면 타임어택 없이 발사 연출 뒤 끝난다.
- **괴물 0명이어도 판 진행 자체는 된다**(코드 확인):
  - `GameRuleController.Update`(23~44행)는 `MonsterActorNumbers`가 **빈 배열**이어도 진행한다. 이탈 처리 때 `remaining`을 빈 배열(null 아님)로 쓰므로 키가 남는다.
  - 타임어택 판정 `EscapeRules.Evaluate`(`Escape/EscapeRules.cs:50~57`)는 괴물 수를 보지 않는다. 쿠키가 모두 탈출·부서지면 `EndAllResolved`, 시간이 다 되면 `StrikeWitch` → 주먹 → `EndWitchStrike`.
  - 마녀 사망 `WriteWitchDeaths`는 괴물이 없으면 그 부분만 건너뛴다.
  - 결과 화면(`ResultScreenController`, 100·133행)은 방에 남은 사람만 줄로 그리므로 나간 괴물은 목록에 없다.
  - 방장 정책(`RoomState.DesiredMasterActor` — 괴물이 아닌 최선 입장자)도 영향이 없다.
- 결론: **요청 1은 `RoomLifecycleWatcher`의 예외 조건 하나**만 넓히면 된다.

### 요청 2 — 대기실에서 기다리는 괴물끼리 안 보임
- **원인 1(직접 원인)**: `Monster/MonsterLobbyWaitController.cs` `EnterWaiting`(76~95행)이 시작 신호(`PaintPhaseEndTime` 변경)를 받는 순간 `RemoveOtherPlayersAvatars`(133~140행)를 부른다.
  - 이 함수는 **내 것이 아닌 쿠키 역할 캐릭터를 모두** 지운다.
  - 대기실 아바타는 괴물이 될 사람도 쿠키 캐릭터(`HideOrSeekPlayer`, `PlayerSpawner`)다. 그래서 **함께 기다리는 다른 괴물(B)의 아바타도 지워진다.** A·B 모두 같은 순간에 서로를 지우므로 "서로 시점에서 없어진" 것처럼 보인다.
  - 이 함수는 원래 괴물 1명 규칙 때, 맵으로 떠난 쿠키들의 굳은 아바타를 치우려고 만든 것이다.
- **원인 2(지우지 않아도 남는 한계)**: 같은 함수 94행에서 **메시지 큐를 멈춘다**(`IsMessageQueueRunning = false`).
  - 멈추는 이유(파일 머리말 7~16행): 쿠키들이 맵에서 만든 캐릭터(Instantiate)와 **색칠 스트로크 이벤트**를 괴물이 맵에 도착할 때까지 쌓아 두었다가 맵에서 재생하기 위해서다.
  - 큐가 멈춘 동안에는 **B의 이동 동기화도 받지 못한다.** 그래서 B를 지우지 않아도 A 화면의 B는 **출발 신호 순간의 자리·자세로 굳는다**(B가 걸어도 A에게는 안 보임). 반대도 같다.
  - 색칠은 이벤트로만 오가고 늦게 온 사람에게 다시 보내는 기능이 없다(`ColorTag/PlayerPaintCanvas.cs` — 다시 보내기·기록 없음). 그래서 큐 멈춤을 없애면 괴물이 쿠키 위장을 못 보게 된다. 큐 멈춤은 유지해야 한다.
  - PUN 소스 확인(`PhotonNetwork.NetworkInstantiate` 2538~2600행): 다른 사람이 만든 캐릭터는 받는 즉시 현재 씬에 생기고, 활성화된다. 받는 쪽에서 미뤄 둘 공식 방법이 없다(관심 그룹은 이미 지나간 이벤트를 다시 주지 않음).

## 21.2 수정 방안

### 요청 1 — `Monster/RoomLifecycleWatcher.cs`
- 27행의 예외를 넓힌다. 괴물이 0명이 되어도 **대기실로 돌려보내지 않는** 경우:
  - ① 처단 중(지금과 같음)
  - ② **스파이 로켓이 떠났음** — `RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out _)`. 마녀 타이머(`WitchMode.TimeAttack`)가 켜졌거나, 남은 쿠키가 없어 곧 끝나는 경우 둘 다 포함.
  - 결과가 이미 났으면(`GameResult` 있음) 원래대로 아무것도 하지 않는다(결과 화면이 대기실로 보낸다).
- 판단은 순수 함수로 뺀다: `MonsterRoster.ReturnWhenMonstersGone(bool spyRocketLaunched, bool witchPurging, bool resultDecided)` — EditMode 시험.
- 이 경우에도 **괴물 목록 갱신**(나간 괴물을 빼고 빈 배열로)은 그대로 쓴다. 그래야 마녀 사망 처리·추격 음악·결과 화면이 "괴물 없음"을 정확히 본다. `MonsterDepartedAt`만 쓰지 않는다 → 배너도 뜨지 않는다.
- 로그: `[RoomLifecycle] All monsters left after the spy rocket launched — the round continues (time attack).`
- **범위 밖(지금 그대로)**: 로켓이 떠나기 **전**(사냥 중)에 괴물이 모두 나가면 지금처럼 5초 경고 뒤 대기실로 간다. 요청이 "마녀 타이머가 켜졌을 때"이므로 바꾸지 않는다(결정 Q1).
- 경계: 괴물이 나가 5초 경고가 시작된 **뒤에** 로켓이 떠나는 경우는 경고대로 대기실로 간다(이미 쓴 `MonsterDepartedAt`을 되돌리지 않음). 경고 5초 안에 스파이가 모두 타는 일은 드물다. 바꾸려면 Q1-b.

### 요청 2 — `Monster/MonsterLobbyWaitController.cs` (+ 아바타 고정 보조)
큐 멈춤(원인 2)은 유지해야 하므로, **"서로 보인다 + 두 화면이 같은 장면"**을 목표로 한다.
- **A안(최소)**: `RemoveOtherPlayersAvatars`에서 주인이 **괴물 목록에 있는 아바타는 지우지 않는다**(맵으로 떠난 쿠키의 굳은 아바타만 지움).
  - 결과: 서로 보인다. 다만 상대는 출발 신호 순간의 자리에서 굳어 있고, 나는 계속 걸을 수 있어 화면이 서로 다르다.
- **B안(추천)**: A안 + 기다리는 동안 **괴물 아바타를 제자리 대기 자세로 고정**.
  - 내 캐릭터: `PlayerInput.IsGameplaySuppressed`(채팅·연출과 같은 입력 억제)를 대기 시작에 켜고, 출발·나가기·파괴 때 끈다. 정적 값이라 `OnDestroy`·`LoadLevel` 직전에 반드시 되돌린다(채팅 `GameManager`와 같은 규칙).
  - 상대 괴물 아바타: 마지막 동기화 상태가 "달리기"면 굳은 채 달리는 자세가 되므로 `NetworkTransformSync`에 `HoldAs(state)`(수신 상태를 대기 자세로 고정, 위치 보간 멈춤)를 추가하고 대기 시작 때 `PlayerMoveState.Idle`로 고정한다.
  - 결과: 두 화면 모두 "괴물 둘이 서서 기다리는" 같은 장면. 시점 회전(마우스)은 그대로.
- **C안**: B안 + 기다리는 괴물들을 **정해진 대기 자리**(가마솥 옆, 괴물 목록 순서대로 0·1번 자리)로 옮겨 세운다.
  - 대기실에 자리 표시 `MonsterWaitSpot_00/01`을 에디터 스크립트로 추가한다. 각 화면이 같은 순서로 계산하므로, 출발 신호 순간의 동기화 지연으로 생기는 자리 차이(수십 cm~수 m)도 없어진다.
- **D안(비추천)**: 큐를 멈추지 않는 구조로 바꿔 대기 중에도 서로 움직임까지 보이게 한다.
  - 필요한 것: 쿠키 캐릭터를 비활성 보관했다가 맵 도착 때 켜는 전용 PrefabPool, 색칠 스트로크를 따로 모아 맵에서 재생하는 버퍼, 대기 중 도착하는 RPC·이벤트(들기·기절·알림)를 미루는 처리.
  - PUN 내부 흐름에 깊게 기대고, 놓친 이벤트가 생기면 괴물이 위장·캐릭터를 못 보는 큰 회귀가 난다. 이번 요청 범위에 비해 위험이 크다.
- 대기 중 상대 괴물이 방을 나가면 큐가 멈춰 있어 그 사실을 모른다(아바타가 남음). 출발하면 씬이 바뀌어 사라지므로 그대로 둔다(지금도 참가자 목록을 숨기는 같은 이유).

## 21.3 결정이 필요한 질문
| # | 질문 | 선택지 | 추천 |
|---|---|---|---|
| Q1 | 로켓이 떠나기 **전**에 괴물이 모두 나가면 | a 지금처럼 5초 뒤 대기실 / b 이것도 계속 진행 | ✅ **a** — 마녀 타이머가 켜진 뒤(스파이 로켓 탑승·발사 뒤)에만 계속 진행 |
| Q2 | 대기실에서 기다리는 괴물끼리 어떻게 보일지 | A / B / C / D | ✅ **D — 서로 보이고 움직임까지 보여야 함** → 21.6 설계 |

## 21.4 영향·위험
- 요청 1:
  - 괴물이 없는 타임어택은 마녀 시간까지 쿠키가 탈출만 하면 된다(잡힐 위험 없음). 규칙상 의도한 결과다.
  - 결과 화면에 나간 괴물 줄이 없다(지금 처단 경로와 같음).
  - 괴물 승리 조건(`monsterWins` = 탈출 0명 & 잡은 수 > 0)은 바뀌지 않는다.
- 요청 2:
  - 입력 억제는 정적 값이라, 되돌리지 않으면 맵에서 괴물이 못 움직인다 → 출발(`LoadLevel` 직전)·나가기·`OnDestroy` 세 곳에서 되돌리고, 맵 도착 시 `MonsterJoinController`에서도 한 번 더 확인한다.
  - 괴물 1명 판은 지울 대상·고정 대상이 바뀌지 않아 영향이 없다.

## 21.6 Q2-D 설계 — 기다리는 동안 큐를 멈추지 않고 "맵에서 온 것"만 보관했다가 맵에서 꺼낸다
큐 멈춤이 하던 일 두 가지(맵 캐릭터 보존·색칠 재생)를 **필요한 것만 골라** 대신하고, 나머지 메시지(다른 괴물의 이동 등)는 정상 처리한다.
1. **보관 풀** — 새 `Core/NetHold.cs`(대기 괴물만 켬, 씬과 무관하게 남는 오브젝트).
   - `PhotonNetwork.PrefabPool`을 감싸, 보관 중에 **남이 만든** 캐릭터(쿠키들이 맵에서 Instantiate)는 꺼진 보관 루트(DontDestroyOnLoad) 아래에 만든다.
   - PUN 소스 확인: ViewID를 넣는 순간 등록되고(`PhotonView.ViewID` setter), `SetActive(true)`는 부모가 꺼져 있어 Awake·Start·Update가 돌지 않는다. 그래서 대기실 장면에 나타나지 않고, 씬이 바뀌어도 사라지지 않는다.
2. **동기화 값 보관** — PUN은 꺼진 컴포넌트에도 `OnPhotonSerializeView`·RPC를 부른다(PUN 소스: `DeserializeView`·`ExecuteRpc`에 enabled 검사 없음).
   - 보관 중에는 그 PhotonView의 관찰 목록을 보관 기록기 하나로 바꿔, **마지막으로 받은 값**만 저장한다.
   - 꺼낼 때 원래 목록으로 돌리고 저장한 값을 한 번 넣는다 → 숨어 있던 쿠키가 처음 위치가 아니라 **지금 위치**에 나타난다.
   - RPC: 쿠키 캐릭터 RPC는 대부분 주인에게만 가서(들기·놓기·거절) 괴물에게 오지 않는다. 모두에게 가는 `RequestGrabKill`은 사냥 때만 쓰이지만, 보관 중 도착해도 예외가 나지 않게 `pv == null` 가드를 둔다.
3. **색칠 이벤트 보관** — 보관 중 도착한 `PaintStroke`·`ClearColor`를 받은 시각의 색칠 시간 판정과 함께 모아 둔다.
   - 꺼낸 직후 해당 캔버스에 같은 순서로 재생한다(캔버스는 Awake에서 만들어져 바로 그릴 수 있다).
   - 판정은 **받은 시각** 기준이라, 맵 로딩이 2초를 넘어도 버려지지 않는다.
   - ⚠ 지금 코드의 회귀: P4에서 넣은 "색칠 시간 + 2초" 검사는 큐 멈춤 방식에서 맵 로딩이 2초를 넘으면 괴물이 받은 색칠을 모두 버린다. 이 설계로 함께 해소된다.
4. **꺼내기** — 괴물이 맵 씬을 다 불러온 순간(`sceneLoaded`, PUN이 큐를 다시 돌리기 전 프레임)에 실행한다.
   - 보관한 오브젝트를 맵 씬으로 옮겨 켠다 → 저장한 동기화 값 적용 → 색칠 재생.
   - 방을 나가면 보관한 것을 버린다.
5. **대기 컨트롤러** — `MonsterLobbyWaitController`:
   - 큐를 멈추지 않는다(`IsMessageQueueRunning=false`·`KeepAliveInBackground` 연장 삭제). 보관을 시작한다.
   - `RemoveOtherPlayersAvatars`는 괴물 목록에 있는 아바타를 **지우지 않는다**(맵으로 떠난 쿠키의 굳은 대기실 아바타만 지움).
   - 다른 괴물은 정상 동기화되어 움직임까지 보인다.
- 남는 차이: 대기 중 채팅은 지금처럼 막아 둔다(맵의 채팅 PhotonView와 겹침).

## 21.5 단계
| 단계 | 내용 | 상태 |
|---|---|---|
| L0 | §21.3 결정(Q1·Q2) | ✅ 2026-10-12 (Q1 a, Q2 D) |
| L1 | 요청 1: `RoomLifecycleWatcher` 예외 넓히기 + `MonsterRoster.ReturnWhenMonstersGone` | ✅ 2026-10-12 |
| L2 | 요청 2(Q2-D, 21.6): `Core/NetHold.cs` 보관 풀·동기화 값 보관·색칠 보관·맵에서 꺼내기, `MonsterLobbyWaitController` 큐 멈춤 삭제·괴물 아바타 남기기, `PlayerPaintCanvas` 재생 진입점, `HideOrSeekPlayer` RPC 가드 | ✅ 2026-10-12 |
| L3 | EditMode 시험: `ReturnWhenMonstersGone` 표(로켓 전·후, 처단, 결과 뒤), 대기 아바타 지울 대상 고르기(괴물 목록에 있는 사람 제외) | ✅ 2026-10-12 (판단 표 5개; 지울 대상은 Play Mode·멀티로) |
| L4 | Play Mode(오프라인): 스파이 로켓 발사 → 가짜 괴물 퇴장(`OnPlayerLeftRoom`에 시험용 Player) → 배너 없음·`MonsterDepartedAt` 없음·마녀 내리침·결과 화면까지 진행 / 로켓 전 퇴장은 5초 뒤 대기실. 대기실: 괴물로 정해진 뒤 시작 신호 → 입력 억제·되돌림, 출발 뒤 맵에서 이동 가능 | ✅ 2026-10-12 |
| L5 | 멀티(사용자): ① 5명 이상(스파이) — 로켓 발사 뒤 괴물이 게임을 끔 → 판 계속·마녀·결과 ② 7명 이상(괴물 2) — A·B 가마솥 → 시작 → 서로 보임·대기 자세 → 맵 도착 후 정상 이동. 문서·README | ⬜ |

## 21.7 결과 기록 ✅ 2026-10-12
- **요청 1**: `Monster/RoomLifecycleWatcher.cs` — 괴물이 모두 나가도 `MonsterRoster.ReturnWhenMonstersGone(로켓 발사, 처단 중, 결과 남)`이 false면 판 계속. 목록은 빈 배열로 갱신하고 `MonsterDepartedAt`은 쓰지 않는다(배너 없음).
  - Play Mode(캔디숲, 가짜 괴물 퇴장):
    - 로켓 발사 뒤 → 경고 없음, 타임어택 계속 → 16초 마녀 내리침 → 19초 결과(`WitchStrike`) → 결과 화면 뒤 대기실.
    - 로켓 전 → 1초 퇴장, 6초에 대기실(지금과 같음).
- **요청 2(Q2-D)**:
  - 새 `Core/NetHold.cs`·`Core/HeldSyncRecorder.cs`.
  - `MonsterLobbyWaitController`: 큐 멈춤·`KeepAliveInBackground` 연장 삭제 → `NetHold.Begin()`, 함께 기다리는 괴물 아바타는 지우지 않음.
  - `PlayerPaintCanvas.ApplyRemote`(실시간·보관 공용, 받은 시각 판정), `HideOrSeekPlayer` RPC 4개 `pv == null` 가드.
  - Play Mode:
    - ① 보관: 만든 쿠키가 꺼진 채 등록됨, 동기화 값 기록 → 맵을 다시 불러오면 맵 씬으로 옮겨 켜짐, 관찰 목록 복구, 위치 (5,1,5) 적용, 색칠 스탬프 적용(알파 1.0).
    - ② 대기실(나 = 괴물): 시작 신호 → 대기 중에도 메시지 큐가 돈다, 보관 시작 → 6초 뒤 맵 → 꺼내기 → 괴물 스폰. 콘솔 오류·경고 0.
- **시험**: EditMode 288개 모두 통과(`AuditFixTests` 23 — `ReturnWhenMonstersGone` 5개 추가).
- **멀티 확인(L5, 사용자)**:
  - ① 5명 이상(스파이 1) — 로켓 발사 뒤 괴물이 게임을 끔 → 배너 없이 판 계속 → 마녀 → 결과.
  - ② 7명 이상(괴물 2) — A·B가 가마솥 → 시작 → 대기실에서 서로 보이고 움직임까지 보임 → 맵 도착 뒤 쿠키가 숨은 자리·위장 그대로 보임(보관한 위치·색칠 적용), 괴물 둘 모두 정상 이동.

## 21.8 멀티 시험에서 찾은 문제 — 대기실로 돌아오면 시점이 남의 캐릭터로 (2026-10-12) ✅ 수정 반영
- **현상**(사용자, 8명·에디터 방장): 에디터 = Player2806인데 카메라가 Player5842를 따라가고, 방향키 이동이 이상하다.
- **확인(실행 중인 에디터를 그대로 조사)**:
  - 내 쿠키(actor 1)의 `SpectatorController`가 대기실에서 관전 중이었다(`isSpectating=True`, 이유 `Broken`). 카메라 대상은 actor 4의 쿠키.
  - 그때 내 HitCount는 이미 지워져 있었다.
- **원인(P3 회귀)**:
  - P3에서 `CookieLifeStatePresenter.Reconcile`이 "부서짐"이면 관전으로 넘어가게 바꿨다(방장 확정이 처형 연출보다 늦게 오는 경우 대비).
  - 판 키(HitCount)는 P2부터 **방장이** 지운다(`RoundStateResetter` → `AuthorityProps.SetPlayer`). 그래서 대기실로 돌아온 직후 서버 응답이 올 때까지 이전 판 HitCount = 2가 잠깐 남는다.
  - 이 사이 새로 생긴 대기실 쿠키의 첫 `Reconcile`이 "부서짐"을 보고 관전에 들어갔다. 관전은 한 번 들어가면 풀리지 않는다.
  - 같은 이유로 대기실 쿠키가 잠깐 이동 잠금·부서진 모습이 될 수 있었다.
- **수정**(사용자가 Play Mode를 끈 뒤 반영):
  - 파괴는 맵에서만 본다(`PaintPhaseController.IsPaintScene`). 맵이 내려가는 순간 false가 되어 대기실에는 이전 판 값이 영향을 주지 않는다.
  - 적용한 곳: `HideOrSeekPlayer.IsBroken`, `CookieLifeStatePresenter.Reconcile`·`EnterSpectatorIfConfirmed`, `PlayerGrabController.TryGrab`(대상 `target.IsBroken`).
- **확인**:
  - 컴파일 오류·경고 0, EditMode 전체 통과.
  - 오프라인 Play Mode — 내 HitCount = 2로 만들었을 때:
    - 대기실: 부서짐 아님·잠금 없음·관전 아님·카메라는 내 캐릭터.
    - 캔디숲: 부서짐·잠금·관전(지금과 같음). 콘솔 오류·경고 0.
- **멀티 확인 예정**: 이전 판에 잡힌 사람이 대기실로 돌아와서도 자기 캐릭터 시점·이동이 정상인지.

