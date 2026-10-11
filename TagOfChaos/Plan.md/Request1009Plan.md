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
| R7 | (결과 곡 승인 뒤) `MusicId.Result` 추가·규칙·시험·카탈로그 | Play Mode 결과 단계 재생 기록 | ⬜ 승인 대기 |

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
| N6 | 요청 6 소리 출력 장치 설정 | 시험, 설정 창 사진, 값 확인 | ✅ 2026-10-11 — §16.11 N6 |
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

### N6 ✅ 2026-10-11
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
