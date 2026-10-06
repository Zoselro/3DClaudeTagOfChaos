# 사운드 계획: 배경음(BGM)·효과음(SFX) 도입 (SoundPlan)

- 상태 표시: ⬜ 대기 · 🔄 진행 중 · ✅ 완료
- 근거 조사: `Plan.md/research.md` **R8**(현재 오디오 자산 0개, 소리가 필요한 지점 목록과 코드 위치)
- 규칙: 코드에 한글 금지(소리 ID는 영문 enum, 화면 문구는 씬·SO), OOP, 최적화, **승인 후 작업**
- ✅ 승인됨(2026-10-02, “추천대로 진행”): §1 D1~D6 모두 추천안. S1부터 진행 중.
- **2026-10-07: S0~S9 모두 ✅.** 다음 소리 작업(배경음·효과음을 "뒤틀린 과자 동화" 컨셉으로 다시 만들기)은 `TwistedCandyPlan.md` §5. S8 음원 방향 문서(`AudioProductionPlan.md`)는 `archive/`로 옮겼다.

---

## 0. 목표와 범위

| 목표 | 기준 |
|---|---|
| 로비·대기실·맵 5개마다 분위기가 맞는 배경음 | 씬과 게임 단계(변장·추격·타임어택·결과)에 따라 자연스럽게 바뀐다(교차 전환 1.5~3초) |
| 모든 버튼·창·카운트다운에 UI 소리 | 씬·프리팹의 모든 `Button`에 빠짐없이(테스트로 보장) |
| 게임 동작 효과음 | 캐릭터·색칠·대기실·탈출 모드·도구·맵 연출 5종·환경음(R8.4) |
| 네트워크 비용 0 | **새 RPC·이벤트·Props를 만들지 않는다.** 모든 클라이언트에서 이미 실행되는 지점(상태 변화·상태 동기화·공통 시계)에서 소리를 낸다 |
| 성능 | `AudioSource` 미리 만든 풀(재생마다 생성·파괴 없음), 배경음은 스트리밍, 같은 소리 동시 재생 수 제한 |
| 음량 설정 | 전체·배경음·효과음·UI 4개 막대, 기기에 저장 |

범위 밖: 음성(대사), 실시간 음성 채팅, 3D 오디오 플러그인(HRTF).

---

## 1. 결정 사항(승인 필요)

| # | 질문 | 선택지 | 추천 |
|---|---|---|---|
| D1 | 음원 출처 | A) 제가 만든다 — 효과음은 파이썬 합성, 배경음은 MIDI 작곡 → FluidSynth + 무료 GM 사운드폰트로 녹음 / B) 무료 CC0 팩(예: Kenney 오디오 팩) / C) 사용자가 준 음원 | **효과음 A(+필요하면 B로 교체), 배경음 A** — 저작권 걱정 없음, MIDI 원본을 함께 남겨 나중에 MuseScore 등에서 고칠 수 있음 |
| D2 | 전체 분위기 | A) 귀여운 과자 동화(오르골·마림바·피치카토·셀레스타) + 맵마다 색 / B) 신나는 아케이드(신스·칩튠) | **A** — 마녀·과자집·쿠키 세계관과 맞음 |
| D3 | 소리로 드러나는 정보(숨바꼭질 규칙) | 쿠키 발소리 / 상자 열기 / 스파이 훔치기를 다른 사람이 들을 수 있는가 | **쿠키: 걷기는 거의 무음, 달리기·점프·착지만 작게(반경 12 m)** · **괴물 발소리는 크게(30 m, 쿠키에게 경고)** · **상자 열기 15 m** · **훔치기 = 설치와 같은 소리**(스파이를 소리로 구분할 수 없게) |
| D4 | 음량 설정 위치 | A) ESC 메뉴에 “소리” 버튼(모든 사람) + 로비에 같은 창 / B) 로비에만 | **A** |
| D5 | 괴물 화면의 배경음 | A) 쿠키와 같은 추격 곡 / B) 괴물 전용 곡 | **A**(곡 수를 줄이고, 괴물 대기 중에만 전용 곡) |
| D6 | 괴물이 가까울 때 긴장 층(쿠키 화면에서 괴물과의 거리로 음악 층 추가) | 지금 / 나중(P2) | **나중** — 1차는 단계 전환만 |

---

## 2. 구조 설계

### 2.1 원칙

1. **소리는 표시(view) 쪽에서 낸다.** 권위 코드(`EscapeAuthority` 등 방장 전용)·규칙 코드는 소리를 모른다. 방장 전용 코드에서 내면 방장만 듣기 때문이다.
2. **도메인마다 소리 담당 클래스 하나.** 소리 호출을 게임 클래스 수십 곳에 흩지 않는다. 예: 탈출 모드는 `EscapeAudioPresenter` 하나가 `EscapeManager.StateChanged`의 **이전 상태와 현재 상태의 차이**를 보고 낸다.
3. **게임 코드에는 “일어났다”는 이벤트만 더한다.** 소리 담당이 그 이벤트를 구독한다(예: `PlayerAnimationDriver.StateChanged`, `PlayerInventory.Changed`(있음), `StunReceiver.Stunned`).
   이미 이벤트가 있으면 새로 만들지 않는다.
4. **Core에 계약, 구현은 Audio 도메인.** 다른 도메인은 `GameAudio`(정적 창구 — `PlayerInput`과 같은 방식)와 `SoundId` enum만 안다. 재생기·풀·믹서는 Audio 도메인 안에 숨긴다.
5. **프리팹에 미리 붙인다.** 캐릭터·UI의 소리 컴포넌트는 에디터 도구로 프리팹·씬에 붙인다(런타임 `AddComponent` 금지 — research.md R4.3-13 지적과 같은 이유).
6. **숨겨진·파괴된 캐릭터는 조용하다.** 몸이 숨겨진 쿠키(탑승·탈출·파괴)는 캐릭터 소리를 내지 않는다(같은 조건 `IsSpectatable`/렌더러 상태 재사용).

### 2.2 파일 구성(폴더 규칙 준수)

| 분류 | 경로 | 내용 |
|---|---|---|
| 스크립트 | `Assets/02. Scripts/Audio/` | 아래 클래스 |
| 전역 SO(Resources) | `Assets/Resources/Audio/SoundCatalog.asset`, `MusicCatalog.asset` | 카탈로그(`GameSettings`와 같은 전역 로드 방식). ~~`GameAudioMixer.mixer`~~ — S1에서 코드 묶음 음량으로 대체(아래 S1) |
| 음원 | `Assets/10. Audio/BGM/`, `Assets/10. Audio/SFX/{UI,Character,Paint,Lobby,Escape,Tool,Device,Ambience}/` | WAV(원본) — 임포트 설정은 폴더별 자동 |
| 음원 원본 | `Assets/10. Audio/Source~/`(Unity가 무시) | MIDI·합성 스크립트·사운드폰트 정보(다시 만들 수 있게) |
| 에디터 | `Assets/Editor/Audio/` | 카탈로그 빌더, UI 소리 연결 도구, 임포트 후처리 |
| 테스트 | `Assets/Editor/Tests/AudioTests.cs` | §6 |

### 2.3 클래스

| 클래스 | 종류 | 책임 |
|---|---|---|
| `SoundId`, `MusicId` | enum | 소리·곡 ID(영문). 새 소리 = 항목 추가 + 카탈로그 한 줄(테스트가 빠짐을 잡음 — `NetKeys.Scopes`와 같은 방식) |
| `SoundCatalogSO` | SO | ID별 클립 목록(무작위 1개)·음량·피치 범위·공간(UI/2D/3D)·거리·쿨다운·동시 재생 수·믹서 그룹 |
| `MusicCatalogSO` | SO | 곡별 클립·음량·루프 여부 + **곡 고르기 표**(씬 × 단계 × 상황 → `MusicId`) |
| `GameAudio` | 정적 창구 | `Play(id)`, `PlayAt(id, position)`, `PlayOn(id, transform)`, `StartLoop(id, transform) → AudioLoopHandle`, `Stinger(id)` |
| `AudioRuntime` | MonoBehaviour(DDOL, 앱 시작 때 1개) | 효과음 풀(`AudioVoicePool` — 3D 24개 + 2D 8개 미리 생성, 꽉 차면 가장 오래되고 덜 중요한 것을 뺏음), 음악 재생기, 믹서 연결 |
| `MusicPlayer` | 순수 C#(AudioRuntime 소유) | 음원 2채널 교차 전환, 스팅어 채널, 스팅어 중 배경음 낮추기(믹서 스냅샷) |
| `MusicDirector` | 순수 C# | 0.25초마다 “지금 들어야 할 곡”을 계산(`MusicRules.Select(scene, phase, context)` 순수 함수 — 테스트) |
| `AudioVolumeSettings` | 정적 | 4개 음량(0~1 ↔ dB) 저장(PlayerPrefs)·믹서 반영 |
| `UiSound` | MonoBehaviour(버튼마다) | 호버·클릭 소리(종류: 일반·확인·취소·시작·경고). 에디터 도구가 모든 버튼에 붙임 |
| `UiSoundCues` | MonoBehaviour(창마다) | 창 열기·닫기·저장·카운트다운 째깍(마지막 N초) |
| `CharacterAudio` | MonoBehaviour(쿠키·괴물 프리팹) | 발소리(속도·접지·지면 종류), 점프·착지·회피·들기, 괴물 돌진·잡기. 본인·원격 모두 **상태 전환**으로 판단 |
| `PaintAudio` | MonoBehaviour(쿠키 프리팹, 본인만 동작) | 붓질 반복음·색 고르기·슬롯 등록·지우개·리셋·강제 도포 |
| `EscapeAudioPresenter` | MonoBehaviour(맵 EscapeSetup) | 탈출 상태 차이 → 상자·아이템·설치·완성·탑승·출발·로켓 칸·해치 소리, 알림 종류별 소리 |
| `SequenceCue` | 구조체 + `EscapeSequence` 보조 함수 | 맵 연출 시간표의 경계를 지나는 순간 한 번 재생(`CueAt(id, at, since, where)`) — 늦게 들어온 사람은 지난 소리를 안 들음 |
| `AmbientEmitter` | MonoBehaviour(맵 랜드마크) | 환경음 반복(3D) — `EscapeMapSetup`이 배치 |
| `ToolAudio` | 정적 보조 | 도구 조준·발사·명중·물풍선 비행·터짐(`ToolUser`·`ToolHitReceiver`·`ToolShotFx`의 기존 지점) |

### 2.4 게임 코드에 더하는 것(최소)

| 클래스 | 추가 | 이유 |
|---|---|---|
| `PlayerAnimationDriver` | `event Action<PlayerMoveState, PlayerMoveState> StateChanged`(이전, 새) — `ChangeState`·`ReplayJump`에서 발생 | 본인·원격 모두 같은 지점(원격은 `ChangeState(RemoteState)`) |
| `MonsterController` | 같은 이벤트(`MonsterMoveState`) | 돌진·처형 소리 |
| `StunReceiver` | `event Action<float> Stunned` | 기절 소리(전원) |
| `CookieLifeStatePresenter` | `GrabKillStarted`, `CrumbsSpawned`, `Crushed` 이벤트 | 처형 3단계 소리 |
| `EscapeSequence` | `protected void CueAt(SoundId id, float at, float since, Transform where)` | 시퀀스 5종이 시간표에 소리를 얹음 |
| `PhaseCountdownDisplay`·`EscapeHud` | 초가 바뀔 때 `UiSoundCues.Tick(seconds)` | 마지막 10초 째깍 |
| `WitchPresenter` | 내리치는 순간 `GameAudio.Stinger` + **카메라 흔들림 수정(research.md R4.7-9)** | 흔들림이 지금 보이지 않음 — 같은 지점이라 함께 고친다 |

### 2.5 공간·믹스 기준

| 그룹(믹서) | 기본 | 내용 |
|---|---|---|
| Master | 0 dB | |
| Music | −8 dB | 배경음, 스팅어·마녀 내리치기 때 −6 dB 더 낮춤(스냅샷 0.3초) |
| Sfx | 0 dB | 캐릭터·탈출·도구·연출 |
| Ui | −4 dB | 버튼·창·카운트다운 |
| Ambience | −12 dB | 맵 환경음 |

| 소리 종류 | 공간 | 최대 거리 | 동시 재생 |
|---|---|---|---|
| 쿠키 발소리(달리기) | 3D | 12 m | 캐릭터당 1 |
| 괴물 발소리·돌진 | 3D | 30 m | 캐릭터당 1 |
| 상자·아이템·설치 | 3D | 15 m | 소리당 4 |
| 맵 연출(장치·로켓·기차 등) | 3D | 60 m | 소리당 2 |
| 마녀 등장·내리치기, 스팅어 | 2D | — | 1 |
| UI | 2D | — | 소리당 2, 쿨다운 0.05초 |

- 3D 감쇠: 로그 곡선(가까이서는 또렷, 멀리 가면 빨리 작아짐), 도플러 0, 스프레드 0.
- 음량 기준: 배경음 약 −18 LUFS(통합), 효과음 피크 −3 dBFS 이하.
- 임포트: 효과음 = WAV 원본, 압축 해제 상태로 로드(짧은 것)·Vorbis(긴 것), 3D용은 모노 / 배경음 = 스트리밍, Vorbis 품질 70%, 미리 로드 안 함, 정확한 마디 길이로 잘라 이음매 없는 루프.

---

## 3. 음원 목록

### 3.1 배경음(루프 14 + 스팅어·징글 8)

| ID | 상황 | 분위기·악기(D2=A 기준) | 박자·길이 |
|---|---|---|---|
| `Lobby` | 로비 | 쿠키 마을 아침 — 오르골·마림바·피치카토 현, 다장조 | 100 BPM, 32마디 |
| `GameLobby` | 대기실(마녀 과자집 앞) | 살짝 수상한 3/4 왈츠 — 셀레스타·바순·피치카토, 가마솥 부글거림과 어울리게 | 92 BPM, 32마디 |
| `MonsterWait` | 괴물 대기(괴물 본인만) | 낮은 현 트레몰로·팀파니, 점점 다가오는 느낌 | 80 BPM, 16마디 |
| `CandyForest_Paint` / `_Hunt` | 캔디숲 | 플루트·하프·마림바 / 같은 주제 + 현 스타카토·스네어 | 104 BPM |
| `Gingerbread_Paint` / `_Hunt` | 진저브레드 마을 | 아코디언·글로켄슈필(시계 째깍 리듬) / + 금관·탐탐 | 96 BPM |
| `Factory_Paint` / `_Hunt` | 초콜릿 공장 | 우드블록·클라리넷 기계 리듬 / + 베이스·팀파니 오스티나토 | 112 BPM |
| `Carnival_Paint` / `_Hunt` | 저주받은 놀이공원 | 단조 오르골 왈츠(뮤직박스·칼리오페) / + 오르간·현 | 3/4, 96 BPM |
| `Bakery_Paint` / `_Hunt` | 유령 베이커리 | 하프시코드·셀레스타 단조 / + 합창 패드·저음 현 | 88 BPM |
| `TimeAttack` | 마녀 타임어택(공통) | 빠른 6/8 — 오르간·현 트레몰로·팀파니, 마지막 10초 심장 박동(효과음) | 140 BPM |
| `St_MonsterReveal` | 괴물 공개(대기실) | 낮은 금관 + 심벌 2초 | |
| `St_MonsterArrive` | 괴물 합류(변장 → 추격) | 오르간 화음 + 북 3초 | |
| `St_DeviceComplete` | 탈출 장치 완성 | 밝은 상승 아르페지오 2초 | |
| `St_SpyLaunch` | 스파이 로켓 발사(타임어택 시작) | 긴장 상승 3초 | |
| `St_WitchAppear` | 마녀 등장 | 낮은 합창 + 바람 3초 | |
| `Jg_EscapeSuccess` / `Jg_EscapeFail` / `Jg_MonsterWin` | 결과(내 결과에 따라) | 축하 / 아쉬움 / 괴물 웃음 같은 금관 4~6초, 이어서 `Lobby` 곡 조용히 | |

같은 맵의 `_Paint`와 `_Hunt`는 **같은 조성·템포**로 만들어 교차 전환이 매끄럽게 한다.

### 3.2 효과음(약 75종, P1 = 1차 필수, P2 = 다듬기)

**UI(2D) — 15**: `UiHover`, `UiClick`, `UiConfirm`, `UiCancel`, `UiStart`(시작 버튼), `UiError`(범위 밖·입장 실패), `UiOpen`, `UiClose`, `UiSaved`, `UiTick`(카운트다운),
`UiTickFinal`(마지막 3초), `UiSlotSelect`(인벤토리 칸), `UiToastInfo`, `UiToastAlert`(스파이 잡힘·마녀), `UiChat`(채팅 수신, P2).

**캐릭터(3D) — 18**: `CookieStep`(달리기만, 지면 3종: 풀·돌·나무 P2), `CookieJump`, `CookieLand`, `CookieDodge`, `CookieGrab`, `CookieRelease`, `CookieCarried`(들림, P2),
`MonsterStep`, `MonsterDash`, `MonsterGrab`(붙잡음), `MonsterSquash`(눌림), `CookieCrumble`(부서짐 가루), `MonsterAimLock`(조준 포착, 본인 2D),
`StunStars`(기절 반복 짧게), `Respawn`(낙하 복귀), `SpectateSwitch`(관전 전환, 2D P2), `DoorOpen`, `DoorClose`.

**색칠(2D·본인) — 6**: `PaintStroke`(누르는 동안 반복), `PaintPickColor`, `PaintSlotRegistered`, `PaintErase`, `PaintReset`, `PaintForceFill`.

**대기실 — 4**: `CauldronBubble`(반복 3D), `CauldronSplash`, `MonsterWaitTick`(괴물 대기 마지막 5초 — UiTick 재사용 가능), `MonsterDeparted`(알림).

**탈출 모드(3D) — 16**: `ChestOpen`, `ChestRespawn`, `ItemPickup`(본인 2D·남 3D), `ItemDrop`, `DeviceInsert`(설치·훔치기 공용 — D3), `DeviceComplete`(장치 쪽 3D),
`Board_Hop`, `Board_Suck`, `RocketInsert`, `RocketHatch`, `RocketIgnite`, `RocketLiftoff`, `WitchAppear`(2D 바람·웃음), `WitchSlam`(2D 충격 + 흔들림), `HeartBeat`(타임어택 마지막 10초, 2D),
`EscapeSuccessSelf`(내가 탈출, 2D).

**도구 — 7**: `StunAimHum`(조준 중 반복, 본인), `StunFire`, `StunHit`, `BalloonThrow`, `BalloonSplash`, `HammerSwing`, `HammerBonk`.

**맵 연출(3D) — 25**
- 캔디숲: `Cake_Rumble`, `Cake_Crack`, `Cake_Burst`, `Cake_RocketRise`, `Cake_Ignite`, `Cake_Launch`
- 놀이공원: `Coaster_BulbOn`, `Coaster_Sparks`, `Coaster_Startup`, `Coaster_LapBar`, `Coaster_Depart`
- 베이커리: `Oven_Gears`(반복), `Oven_Piston`, `Oven_DoorRoll`, `Oven_Flash`
- 공장: `Train_Puff`, `Train_Whistle`, `Train_Chug`(출발 가속), `Train_Tunnel`
- 진저브레드: `Altar_Hum`(반복), `Altar_Glow`, `Portal_Open`, `Portal_Flash`, `Portal_Close`, `Lantern_Flicker`(P2)

**환경음(반복) — 5**: `Amb_CandyForest`(새·바람·풍경), `Amb_Gingerbread`(시계 째깍·바람, 지하 유적은 동굴 울림 P2), `Amb_Factory`(기계 웅웅·증기), `Amb_Carnival`(멀리 오르골·바람), `Amb_Bakery`(오븐 타닥·유령 바람).

### 3.3 소리 → 코드 지점(요약 — 전체는 research.md R8.3·R8.4)

| 소리 묶음 | 내는 곳 | 판단 근거 |
|---|---|---|
| 배경음 전체 | `MusicDirector` | 씬 이름 + `GamePhaseState.Current` + 괴물 대기 여부(`MonsterLobbyWaitController`) + 내 결과(`RoomState.HasEscaped`·`IsBroken`·괴물 여부) |
| 스팅어 | `MusicDirector`(단계 전환 감지) + `EscapeAudioPresenter`(장치 완성) + `WitchPresenter`(등장·내리치기) + `MonsterRevealController.Refresh` | |
| UI | `UiSound`(버튼), `UiSoundCues`(창·카운트다운), `RoomSettingField.Apply`(경고), `LobbyController` 실패 콜백, `EscapeHud.ShowToast` | |
| 캐릭터 | `CharacterAudio` ← `PlayerAnimationDriver.StateChanged`·`MonsterController` 상태·`StunReceiver.Stunned`·`CookieLifeStatePresenter` 이벤트 | 원격도 상태 동기화로 같은 이벤트 |
| 색칠 | `PaintAudio` ← `PlayerPaintCanvas`(본인) | 본인만 |
| 대기실 | `CauldronSplash.OnTriggerEnter`, 가마솥 `AmbientEmitter`, `InteractableDoor.ApplyState` | 이미 전원 실행 |
| 탈출 모드 | `EscapeAudioPresenter` ← `EscapeManager.StateChanged`(이전/현재 비교), `EscapeHud.Notice` | 방장 코드에서 내지 않음 |
| 도구 | `ToolUser.Update/Use`(본인), `ToolHitReceiver.Handle`·`ToolShotFx`(전원) | |
| 맵 연출 | 각 `EscapeSequence.Tick`의 `CueAt`, `SpyRocket.AnimateLaunch` | 공통 시계 기준 |
| 환경음 | `AmbientEmitter`(EscapeMapSetup이 랜드마크에 배치) | 로컬 |

---

## 4. 단계

### S0 — 결정과 준비 🔄(결정 ✅, 시험음은 S1에서 표준 라이브러리로 만듦. FluidSynth·사운드폰트·numpy 설치는 S3·S8 전에)
- §1 D1~D6 결정.
- (D1=A) 작업 환경에 FluidSynth·GM 사운드폰트·`mido`·`numpy`·`soundfile`·`pyloudnorm` 설치(확인됨: FluidSynth 설치 가능, `mido` 받아짐).
- 검증: 시험 효과음 3개·시험 곡 1개(30초)를 만들어 들려 드리고 방향 확인.

### S1 — 오디오 기반 ✅ 완료(2026-10-02)
- `SoundId`·`MusicId`, `SoundCatalogSO`·`MusicCatalogSO`, `GameAudio`, `AudioRuntime`(풀·음악 재생기·믹서), `AudioVolumeSettings`, `GameAudioMixer`(그룹 5개 + 낮춤 스냅샷).
- 임포트 후처리(폴더별 설정), 카탈로그 빌더(파일 이름 = ID로 자동 연결).
- EditMode 테스트: 모든 ID에 카탈로그 항목·클립, 풀이 꽉 찼을 때 뺏는 규칙, 음량 0~1 ↔ dB, 같은 소리 쿨다운.
- 검증: 컴파일·콘솔 0, 테스트 통과, Play Mode에서 시험음 재생.

**한 일** (`Assets/02. Scripts/Audio/` 8개, `Assets/Editor/Audio/` 2개, `Editor/Tests/AudioTests.cs`)
- `SoundId`(96개)·`MusicId`(22개): 값은 묶음마다 백 단위 구간(UI 100~, 캐릭터 200~ … 환경음 800~)으로 고정 — SO에 숫자로 저장되므로 새 ID는 구간 끝에 덧붙인다.
  ID 이름은 밑줄 없이 바꿨다(`Board_Hop` → `BoardHop`, `CandyForest_Paint` → `CandyForestPaint`, `St_MonsterReveal` → `StMonsterReveal`, `Jg_…` → `Jg…`). 음원 파일 이름 = ID.
- `SoundCatalogSO`(클립 여러 개 중 무작위·음량·피치 범위·2D/3D·묶음·최대 거리·쿨다운·동시 재생 수·중요도), `MusicCatalogSO`(클립·음량·반복). `Resources/Audio/`에서 처음 쓸 때 한 번 읽는다.
- `AudioRuntime`(앱 시작 때 1개, DontDestroyOnLoad): 3D 24칸 + 2D 8칸 + 배경음 3칸(교차 전환 2 + 스팅어 1)을 미리 만든다. 3D는 사용자 감쇠 곡선(최대 거리에서 0), 도플러 0.
  따라가는 소리는 대상 위치를 따라가고, 대상이 사라지면 반복음은 멈춘다. 종료 중에는 다시 만들지 않는다.
- `AudioVoicePool`(순수 계산): 빈 칸 먼저, 꽉 차면 중요도가 같거나 낮은 것 중 가장 오래된 것을 뺏고, 모두 더 중요하면 버린다. 칸마다 세대 번호 — 옛 핸들(`AudioHandle`)이 새 소리를 멈추지 못한다. `SoundCooldowns`(소리별 쿨다운).
- `MusicPlayer`: 같은 곡이면 그대로, 다른 곡이면 교차 전환. 스팅어 동안 배경음 −6 dB(0.3초).
- `GameAudio`(유일한 창구): `Play`(2D)·`PlayAt`·`PlayOn`(따라감)·`StartLoop`→핸들·`PlayMusic`·`StopMusic`·`Stinger`. 클립이 없거나 재생 중이 아니면 조용히 아무것도 안 한다.
- **계획 변경 — 믹서 대신 코드 묶음 음량**: Unity는 AudioMixer 에셋을 코드로 만드는 공개 API가 없어(내부 클래스뿐) 에디터 도구로 다시 만들 수 없다. 지금 필요한 것은 묶음별 음량과 배경음 낮춤뿐이라
  `AudioVolumeSettings`가 최종 음량 = 전체 막대 × 묶음 막대(배경음·효과음·UI, 환경음은 효과음 막대) × 묶음 기본 이득(§2.5 dB)을 계산하고, 막대가 바뀌면 재생 중인 소리에 바로 반영한다(기기에 저장).
  리버브 같은 믹서 효과가 필요해지면 그때 믹서를 손으로 만들어 연결한다.
- `AudioImportPostprocessor`: `10. Audio/BGM` 스트리밍·Vorbis 70%·미리 로드 안 함 / `SFX/Ambience` 압축 상태·모노 / 그 밖의 `SFX` 로드 때 압축 해제·모노.
- `AudioCatalogBuilder`(Tools/TagOfChaos/Audio/Build Catalogs): ID마다 항목(새 항목은 `SoundDefaults` — D3·§2.5 기본값, 기존 조정값은 유지), 클립은 파일 이름(`ID`, 변형 `ID_1`…)으로 매번 다시 연결.
- 시험음(S0 일부, 표준 라이브러리 합성): `UiClick`·`ChestOpen`·`CookieJump`·`Lobby`(100 BPM 4마디 이음매 없는 반복)·`StMonsterReveal`. S8에서 같은 이름으로 교체.

**검증**
- 컴파일 오류·경고 0. AudioTests 10/10(카탈로그 ID당 항목 1개·반복 여부, 쿠키 발소리 < 괴물 발소리 거리, 칸 배정 3종, 세대, 쿨다운, dB 변환, 음량 곱·저장·복원·0~1 제한).
  `AllIds_HaveClips`는 S8 전까지 “결정 불가(빠진 수 안내)” — 지금 113/118개 클립 없음.
- Play Mode(대기실, 오프라인): AudioSource 35개·DontDestroyOnLoad·리스너 1개. 같은 프레임 UiClick 2번 → 1번(쿨다운), 클립 없는 소리 → 무음·경고 없음,
  ChestOpen 지정 위치·15 m, CookieJump 쿠키를 따라감·12 m, UI 음량 0.631(−4 dB). 반복음 핸들로 시작·정지. Lobby 배경음 0.398(−8 dB), 스팅어 중 ×0.50(−6 dB),
  전체 0.5 → 0.199(계산값과 같음), StopMusic 뒤 칸 0개. ChestOpen 8번 연달아 → 동시 최대 4개(상한), 끝나면 칸 모두 반납. 콘솔 오류 0(폰트 에셋 임포트 경고 1건은 에셋 새로고침에서 나온 것으로 소리와 무관).

### S2 — UI 소리 ✅ 완료(2026-10-02)
- `UiSound`·`UiSoundCues`, 에디터 도구로 빌드 씬 8개 + UI 프리팹의 모든 `Button`에 연결(종류는 버튼 이름·용도 표로).
- 카운트다운 째깍(색칠·생존·타임어택·괴물 대기·괴물 선정), 창 열기/닫기, 경고, 토스트.
- EditMode 테스트: 빌드 씬·UI 프리팹의 모든 `Button`에 `UiSound`가 있다.
- 검증: 로비 → 방 만들기 → 대기실 → ESC 메뉴 → 방 설정 → 맵 → 결과까지 클릭해 보며 소리 확인.

**한 일**
- `Audio/UiSound`(버튼마다): 호버(상호작용 가능할 때만)·클릭 소리. 클릭 소리는 `onClick`에 더하므로 씬을 넘기는 버튼도 끝까지 난다(AudioRuntime이 DDOL).
- `Audio/UiSoundCues`(정적 — 계획의 “창마다 MonoBehaviour” 대신): `WindowOpened`·`WindowClosed`·`Saved`·`Error`·`Toast(alert)`·`CountdownTick(초)`
  (마지막 10초 `UiTick`, 3초부터 `UiTickFinal`). 화면 코드가 “일어난 일” 이름으로 부르고 소리 ID는 여기서만 정한다.
- `Editor/Audio/UiSoundInstaller`(Tools/TagOfChaos/Audio/Attach UI Sounds): UI 프리팹(Resources → 04. Prefabs 순) 다음 빌드 씬. 이미 붙은 버튼은 그대로 둔다.
  44개 부착 — 프리팹 5개 22개(맵 씬 5개는 `GameSceneCore` 하나에서 물려받음), LobbyScene 7개, GameLobbyScene 15개.
  클릭 소리 표(버튼 이름): `StartGameButton` 시작 / `Yes`·`Apply`·`MakeRoom`·`RandomJoin`·`Join` 확인 / `No`·`Back`·`Resume` 취소 / `Swatch*` `PaintPickColor`·`EraseButton` `PaintErase`·`ResetButton` `PaintReset`(색칠 버튼은 S4의 `PaintAudio`가 다시 내지 않는다) / 나머지 일반 클릭.
- 연결한 곳: 카운트다운 — `PhaseCountdownDisplay`(변장·생존), `ColorSelectionPanel`(변장), `GameLobbyController`(괴물 선정), `MonsterLobbyWaitController`(괴물 대기, 큐가 멈춰도 로컬 재생), `EscapeHud`(타임어택).
  창 — `EscMenu`(ESC 키로 여닫을 때만, 버튼으로 여닫으면 버튼 소리), `ConfirmDialog.Show`, `RoomSettingsMenu.Open`. 저장·경고 — `RoomSettingsMenu` 적용 결과, `RoomSettingField` 범위 밖(사용자 입력일 때만 — 창을 열며 코드가 값을 채울 때는 문구만),
  `LobbyController` 실패 안내 5곳(`ShowError`로 묶음). 토스트 — `EscapeHud.Toast(message, alert)`: 스파이 잡힘·마녀 등장은 경고, 나머지 안내.
- 시험음(표준 라이브러리 합성, S8에서 교체): UI 15종 + `PaintPickColor`·`PaintErase`·`PaintReset`. 합성 스크립트는 `Assets/10. Audio/Source~/sfx/`.

**검증**
- 컴파일 오류·경고 0. AudioTests 23/23(+S1 10개에 카운트다운 표 6, 버튼 이름 표 5, UI 프리팹·빌드 씬의 모든 Button에 UiSound 2). 회귀: RuleTests 16/16·EscapeTests 42/42·BuildSceneTests 8/8.
- Play Mode(재생 기록 = AudioRuntime의 소리별 마지막 재생 시각 변화):
  - 로비(온라인): 새로고침 호버+클릭, 이름 없이 방 만들기 → 확인+경고, 인원 버튼 클릭·범위 끝에서 경고.
  - 대기실(오프라인): 설정 → 클릭+창 열림(경고 없음), 적용 → 확인+저장, 뒤로·아니오·계속 → 취소, 나가기 → 클릭+확인창 열림, 인원 최대에서 + → 경고(코드로 값 채울 때는 경고음 없음),
    괴물 선정 12초 → 10~4초 `UiTick` 7번, 3~1초 `UiTickFinal` 3번.
  - 진저브레드(오프라인): 색 고르기·지우개 소리, 변장 카운트다운(화면 두 곳이 같은 초를 보여도 한 번씩), 토스트 안내·스파이 잡힘 경고·마녀 경고, 타임어택 10~1초. 콘솔 오류·경고 0.
  - ESC 키 경로는 키 입력을 흉내 낼 수 없어 코드로 확인(같은 `Open`/`Close` + 소리 한 줄). 결과 화면에는 버튼이 없다(자동 복귀 카운트다운만 — 째깍 대상에서 뺐다).

### S3 — 배경음 ✅ 완료(2026-10-02, 곡은 시험곡 — 정식 곡은 S8)
- `MusicDirector`·`MusicRules`(순수 함수 — 테스트), 교차 전환, 스팅어, 결과 징글.
- 괴물 대기(메시지 큐가 멈춘 상태에서도 로컬로 재생), 씬 전환 중 끊기지 않음(DDOL).
- EditMode 테스트: `MusicRules.Select`가 씬 8개 × 단계 6개 × 상황 조합에서 기대한 곡을 고름.
- 검증: 맵 5개를 오프라인으로 돌며 변장 → 추격 → 결과 전환, 대기실 ↔ 맵 왕복에서 끊김·겹침 없음.

**한 일**
- `Audio/MusicRules`(순수 함수): `SceneFromName`(씬 이름 → 로비·대기실·맵 5·그 밖), `Select`(씬 × 단계 × 괴물 대기 × 내가 괴물인지 → 곡),
  `StingerFor`(변장·합류 대기 → 추격 = `StMonsterArrive`, 추격 → 타임어택 = `StSpyLaunch`), `JingleFor`(내 결과 → 성공·실패·괴물 승리 징글).
  - 로비 `Lobby` / 대기실 `GameLobby`, 괴물 본인이 대기 중이거나 판이 진행 중이면 `MonsterWait`(대기가 끝나 맵으로 넘어가는 동안에도 이어 감) /
    맵: 판 시작 전·변장·합류 대기 = 맵 `…Paint`, 추격 = 맵 `…Hunt`, 타임어택 = `TimeAttack`, 결과 = 징글 뒤 `Lobby`(4초에 걸쳐) / 시험 씬 = 없음.
  - 징글: 쿠키·스파이는 살아남았거나(생존 모드) 탈출했으면(탈출 모드) 성공, 아니면 실패. 괴물은 생존 모드 괴물 승리이거나 탈출 모드에서 쿠키(스파이 제외)가 아무도 못 나갔으면 괴물 승리, 아니면 실패 — 결과 화면과 같은 기준.
- `Audio/MusicDirector`(AudioRuntime 소유, 0.25초마다): 씬 이름·Room Props 단계·`MonsterLobbyWaitController.IsLocalWaiting`(새로 추가한 정적 조회)·내 괴물 여부를 모아 규칙에 묻고,
  같은 씬에서 단계가 바뀐 순간에만 스팅어·징글을 낸다(씬이 막 바뀐 판단에서는 내지 않는다 — 늦게 들어온 사람은 지난 소리를 듣지 않음). 대기실에서 괴물이 정해진 순간 `StMonsterReveal`.
  전환 시간: 씬이 바뀌면 1.5초, 단계가 바뀌면 2.5초, 결과는 4초. 새 RPC·Props 없음. AudioRuntime이 DDOL이라 씬 전환 중에도 끊기지 않는다.
- 남은 스팅어: `StDeviceComplete`는 S5(탈출 상태 차이), `StWitchAppear`는 S6(마녀 등장)에서 연결한다.
- 시험곡(표준 라이브러리 합성, `Source~/sfx/synth_music.py`): 반복 곡 14개(맵마다 조성·템포가 다르고 `Paint`/`Hunt`는 같은 조성·템포, `Hunt`는 저음 박동 추가, 정확한 마디 길이로 잘라 이음매 없음) + 스팅어·징글 8개. 카탈로그 22/22 연결.

**검증**
- 컴파일 오류·경고 0. AudioTests 49/49(+26: 씬 이름 8, 빌드 맵마다 곡 규칙, `Select` 전체 표 8×6×2×2, 맵 Paint/Hunt 짝, 스팅어 6, 징글 9).
- Play Mode:
  - 대기실(괴물 본인 흐름): `GameLobby` → 괴물 지정 순간 `StMonsterReveal` → 변장 시작 → `MonsterWait` → 맵 이동 → (혼자라 바로 결과) `Lobby` → 대기실 복귀 `GameLobby`. 씬 전환 중 끊김 없음.
    수정 1건: 대기가 끝나 맵으로 넘어가기 직전에 `GameLobby`로 잠깐 되돌아가던 것을 괴물이면 `MonsterWait`를 이어 가게 했다.
  - 진저브레드(쿠키 흐름): `GingerbreadPaint` 0.40(−8 dB) → 괴물 합류 `StMonsterArrive` + `GingerbreadHunt`로 2.5초 교차 전환(스팅어 동안 배경음 −6 dB) →
    타임어택 `StSpyLaunch` + `TimeAttack` → 결과(쿠키 승리, 나는 살아 있음) `JgEscapeSuccess` + `Lobby` 4초 전환. 콘솔 오류·경고 0.
  - 로비(온라인): `Lobby` 0.40.
  - 혼자 하는 오프라인 시험에서는 맵에 들어오자마자 결과가 나 징글이 생략된다(씬 전환과 같은 판단 — 설계대로). 실제 판에서는 결과가 맵 안에서 나므로 해당 없음.
- 회귀: RuleTests 16/16·EscapeTests 42/42 통과. BuildSceneTests는 6/8 — 소리 작업과 무관하게 `Game_CandyForest`(10-03 00:22 저장, 활성)·`Game_HauntedBakery`(10-02 23:38 저장, 비활성)에
  `GameObject`라는 이름의 오브젝트로 `OfflineModeBootstrap`(spawnAsSpy 켬)이 새로 저장돼 있다. 에디터에서 직접 넣은 시험용으로 보여 지우지 않았다 — 출시 전에 빼야 한다(research.md R4.4-9).

### S3+ — 2026-10-03 사용자 요청 반영
| 요청 | 상태 | 내용 |
|---|---|---|
| 방 이름 없이 방 만들기를 누르면 소리 두 개가 겹침 | ✅ | 저장·오류음은 버튼을 누른 결과라 같은 프레임의 버튼 클릭음을 대신한다(`UiSoundCues.PlayResult`, `ButtonClick`). Play Mode: 오류음만 1번, 일반 버튼은 클릭음만 |
| 게임 중 배경음 | ✅ | 쿠키는 변장 시간에만 맵 곡, 변장이 끝나면(합류 대기·추격·타임어택) 끈다. 괴물은 맵 곡 없음. 결과는 징글 뒤 로비 곡. 스팅어는 유지. `…Hunt`·`TimeAttack` 곡 ID는 번호 보존을 위해 남김 |
| 로비 배경음이 중간에 끊기는 느낌 → 후보 4곡 | ✅ A 선택(2026-10-03, `BGM/Lobby.ogg`) | FluidSynth(FluidR3_GM)로 작곡·렌더: A 쿠키 마을 아침, B 캔디 왈츠, C 과자 행진, D 포근한 오후. 32마디, 두 바퀴 렌더 후 둘째 바퀴만 잘라 이음매 없음(`Source~/sfx/compose_lobby.py`) |
| 진저브레드 네온 줄이기 | ✅ | 진저브레드 전용 재질 7종(창문·분홍·노랑·주황·흰·보라 유리·청록 발광, 발광 45%)으로 71칸 교체(`Assets/Maps/GingerbreadVillage/Materials/*_GingerDim.mat` — 다른 맵은 원본), 블룸 1.2→0.6·문턱 1.2→1.6. 룬·보석·마녀 눈·탈출 장치 발광은 그대로 |
| 공장 기계 소리 | ✅ | `AmbFactory`(모터 웅웅·1초 금속 쿵·2초 증기·기어 틱, 8초 반복) — 기계실 동·서, 북쪽 프레스 줄, 가운데 파이프 4곳(28 m) |
| 놀이공원 회전목마 소리 | ✅ | 새 ID `CarouselSpin`(805) — 단조 칼리오페 왈츠 + 도는 기계 굴림·덜컹·삐걱, 22초 반복, 회전목마 중심(40 m, 효과음 묶음 −6 dB) |

- 새 코드: `Audio/AmbientEmitter`(켜져 있는 동안 3D 반복, 칸이 모자라면 2초 뒤 재시도), `Editor/Audio/AmbientPlacer`(Tools/TagOfChaos/Audio/Place Ambient Emitters — 맵별 위치 표), 반복음은 무작위 지점에서 시작(같은 소리 여러 곳이 똑같이 겹치지 않게).
- 테스트: AudioTests에 환경음 배치 표 검사 추가. 회귀 — AudioTests 49/49·RuleTests 16/16·EscapeTests 42/42·MapCompactTests 25/25(진저브레드 조명 포함)·BuildSceneTests 8/8(앞서 걸리던 맵 씬 시험 오브젝트는 정리됨).
- ✅ 뒤로가기 제거(대기실·게임 화면 왼쪽 위 화살표, 방 설정 창 '뒤로' — 적용하면 저장 후 ESC 메뉴로, ESC로 닫힘). Play Mode 확인, 콘솔 0.
- 남은 요청: 국가 설정, 진저브레드 지하 유적·룬 받침대, 지하 동굴 소리 → `Plan.md/Request1003Plan.md`(승인 대기).

### S3.5 — 거리 기반 음량 감쇠 + 술래 접근 추격음 ✅ 완료(2026-10-03) → `Plan.md/DistanceFadePlan.md` §10
- 듣는 위치를 캐릭터 머리로, 소리 종류별 감쇠 곡선 + 최소 거리, 층 감쇠, 들리지 않는 소리는 칸을 쓰지 않기, 내 소리는 2D. S4·S5는 이 위에서 값만 정한다.

### S4 — 캐릭터·색칠·대기실 소리 ✅ 완료(2026-10-03)
- 연결: `PlayerAnimationDriver.StateChanged`(본인·원격 공통) → `Audio/CookieAudio`(점프·착지·회피·달리기 발소리 0.3초·다른 쿠키에게 들림/내려짐, 숨겨진·부서진 쿠키는 조용, 본인 색칠 중 붓 소리 반복) /
  `MonsterController.StateChanged` → `Audio/MonsterAudio`(걷기 발소리 0.5초·촉수 돌진·잡기) / `CookieLifeStatePresenter`(눌림·바스러짐) / `StunReceiver.Stun`(별) / `FallGuard`(복귀, 본인) /
  `GrabAimReticle`(조준 포착, 괴물 본인 2D) / `InteractableDoor`(열림·닫힘) / `CauldronSplash`(풍덩 + 끓는 소리 `AmbientEmitter`) / `MonsterLobbyWaitController`(괴물 출발) / `PlayerPaintCanvas`(슬롯 등록·강제 도포).
  캐릭터 소리는 `GameAudio.PlayCharacter`(내 것 2D, 남의 것 3D) → DistanceFadePlan의 거리 감쇠가 그대로 적용.
- 시험음 24종(`Source~/sfx/synth_s4.py`, numpy 합성 — S8에서 교체): 쿠키 발소리 3종·괴물 발소리 2종 변형 포함, 붓질·가마솥은 이음매 없는 반복.
- 검증(Play Mode, 재생 기록): 달리기 0.3초 간격 발소리, 점프·착지·회피·들림·내려짐·기절 별·복귀·바스러짐, 괴물 걷기 0.5초 간격·돌진·잡기, 붓 소리(칠하는 동안만), 강제 도포,
  대기실 풍덩·문 열림/닫힘·가마솥 끓는 소리(가까이 갈 때만 — 80 m 떨어져서는 재생 안 함). 콘솔 0. 회귀 전체 통과.
- 원래 계획 메모:
- `PlayerAnimationDriver`·`MonsterController`·`StunReceiver`·`CookieLifeStatePresenter`에 이벤트 추가, `CharacterAudio`·`PaintAudio`를 프리팹에 연결(에디터 도구).
- 발소리: 속도·접지로 간격 계산(달리기만 — D3), 숨겨진·파괴된 쿠키는 조용.
- 가마솥·문 소리.
- 검증: 오프라인 2캐릭터(쿠키·괴물)로 점프·회피·들기·돌진·처형·기절·낙하 복귀, 원격 복사본 쪽에서도 들리는지(두 클라이언트 빌드 또는 에디터 + 빌드).

### S5 — 탈출 모드·도구 소리 ✅ 완료(2026-10-03)
- `Audio/EscapeAudioPresenter`(EscapeManager가 붙임): 상태가 바뀔 때마다 직전 상태와 비교 — 상자 열림·다시 채워짐, 줍기(본인 2D·남 3D)·떨어뜨리기, 장치 끼우기·빼기(`DeviceInsert`, 칸 자리 위치 `EscapeDevice.SlotPosition`),
  장치 완성(`DeviceComplete` + 스팅어 `StDeviceComplete`), 로켓 끼우기, 쿠키 탑승(`BoardHop`)·스파이 로켓 해치(`RocketHatch`), 내 탈출 성공(2D), 타임어택 마지막 10초 심장 박동(1초마다 2D).
  방장 화면은 같은 상태가 두 번 들어와도 두 번째는 차이가 없어 소리가 겹치지 않는다(R4.10-10 판 번호 없이 해결). 처음 받은 상태는 소리 없이 기억만(늦게 들어온 경우).
- 도구: 쓴 사람은 누르는 순간 2D(`ToolUser.Use` → `UseSound`: 휘두르기·발사·던지기), 다른 사람은 승인된 `ToolHit`에서 그 손 위치 3D. 맞은 곳(모두 3D) — 물풍선 터짐(늘), 스턴 명중·뿅망치 쿵(맞았을 때만).
  스턴건 조준 중 윙 소리 반복(본인 2D, `UpdateAimHum`).
- 시험음 17종(`Source~/sfx/synth_s5.py` — S8에서 교체). 알림 소리는 S2의 토스트 소리(안내·경고)를 그대로 쓴다.
- 검증(Play Mode, 놀이공원 오프라인, 재생 기록): 쿠키 — 상자 열기·줍기 → 떨어뜨리기 → 줍기 → 끼우기 2번 → 완성(+안내 토스트) → 탑승 → 탈출 성공.
  스파이 — 장치에서 빼기 → 5초 뒤 훔침 경고 → 로켓 끼우기 2번 → 해치 → 탈출 성공 → 결과 징글. 도구 — 내 뿅망치·물풍선(터짐은 날아간 뒤), 남의 스턴건(발사·명중·별·떨어뜨리기),
  남의 뿅망치(휘두르기·별·쿵), 타임어택 마지막 초 심장 박동, 조준 윙(켜짐 1개 유지·꺼짐). 콘솔 오류·경고 0(에디터 내부 할당 경고만). AudioTests 52/52 등 회귀 전체 통과.
- 원래 계획 메모:
- `EscapeAudioPresenter`(상태 차이), 알림 종류별 소리, 도구 소리(조준 반복·발사·명중·물풍선 비행·터짐·뿅망치).
- 이 단계에서 research.md R4.10-10(상태 판 번호)을 함께 넣으면 방장 화면에서 같은 소리가 두 번 나는 일을 막을 수 있다(넣지 않으면 차이 계산이 같은 변화를 두 번 볼 수 있음 — 소리별 0.1초 중복 방지로 대신).
- 검증: 쿠키·스파이 흐름(상자 → 설치 → 완성 → 탑승 → 출발, 훔치기 → 로켓 → 발사) 맵 1개 이상에서 소리 순서 확인.

### S6 — 맵 연출·마녀 ✅ 완료(2026-10-03)
- `EscapeSequence`에 소리 시간표: 하위 클래스가 Awake에서 `AddCue(id, 완성/출발 기준, 몇 초, 위치)`·`AddLoop(id, 울릴 조건, 위치)`를 등록하고,
  `EscapeDevice`가 매 프레임 `Step`(모습 `Tick` → 소리)을 부른다. 경계를 지나는 프레임에 한 번(`ShouldCue` 순수 함수), 지난 지 1.5초가 넘은 경계는 내지 않는다
  (늦게 들어온 사람 — 몇십 초 지난 상태를 받음). 처음엔 0.5초였으나 Play Mode에서 완성 순간 프레임이 1초 끊기자 0초 소리가 빠져 1.5초로 늘렸다(네트워크 지연도 흡수).
  반복음은 칸이 모자라면 1초 뒤 재시도, 꺼지면 멈춘다. 새 RPC·Props 없음(공통 시계).
- 시간표(완성 c초 / 출발 d초, 움직이는 것은 따라감):
  - 캔디숲: c0 `CakeRumble` · c1.2 `CakeCrack` · c2 `CakeBurst` · c3 `CakeRocketRise`(로켓) / d0 `CakeIgnite` · d2 `CakeLaunch`(로켓)
  - 놀이공원: c0 `CoasterBulbOn`(아치)·`CoasterStartup`(앞 차) · c0.3 `CoasterSparks` / d0 `CoasterLapBar` · d1 `CoasterDepart`(앞 차)
  - 베이커리: 반복 `OvenGears`(완성 ~ 출발 끝) · c0 `OvenPiston` · c3 `OvenDoorRoll`(열림) / d0 `OvenDoorRoll`(닫힘) · d1.8 `OvenFlash`
  - 공장: c0 `TrainPuff` / d0 `TrainWhistle` · d0.8 `TrainChug`(기관차) · 기관차가 터널에 닿는 순간(경로로 계산, 약 d2.8) `TrainTunnel`
  - 진저브레드: 반복 `AltarHum`(완성 ~ 출발 끝) · c0 `AltarGlow` · c2 `PortalOpen` / d0 `PortalFlash` · d1.2 `PortalClose`.
    낡은 등불이 꺼질 듯 어두워지는 순간 `LanternFlicker`(10 m, 등불마다 최소 3초 간격)
- 탑승 소리는 맵 연출이 정한다(`BoardSound`): 오븐·포탈은 `BoardSuck`, 나머지 `BoardHop`. 탄 쿠키를 못 찾으면 탑승 위치에서.
- 스파이 로켓(`SpyRocket.AnimateLaunch`): 출발 시각 t0 `RocketIgnite` · t2 `RocketLiftoff`(로켓을 따라감, 같은 경계 규칙).
- 마녀(`WitchPresenter`, 모두 2D): 나타나기 시작 `WitchAppear`(바람·웃음), 다 나타남(5초) 스팅어 `StWitchAppear` — 타임어택 시작 스팅어 `StSpyLaunch`와 겹치지 않게 늦춤,
  내리침 `WitchSlam`(처음 보인 프레임에 이미 내리친 상태면 내지 않음). 늦게 들어와 지난 순간이면 내지 않는다.
- 카메라 흔들림(research.md R4.7-9)은 이미 고쳐져 있음(`CameraShake.Add`가 카메라 LateUpdate에서 더함) — 같은 지점에 소리만 더했다.
- 시험음 31종(`Source~/sfx/synth_s6.py`, numpy — S8에서 교체): 맵 연출 25 + `BoardSuck`·`RocketIgnite`·`RocketLiftoff`·`WitchAppear`·`WitchSlam`. 반복음 2개는 이음매 없음.
  카탈로그: 연출 3D 60 m(Landmark), 등불 10 m·작게, 반복음은 피치 고정.

**검증**
- 컴파일 오류·경고 0. AudioTests 54/54(+ `SequenceCue_FiresOnceAtBoundary_NotWhenLate` — 경계 1번·이미 지남·아직·늦게 들어옴·긴 끊김·30fps 한 바퀴 1번, `S6Sounds_HaveClips_AndRightSpace`). EscapeTests 52/52·RuleTests 27/27.
- Play Mode(맵 5개 오프라인, 상태의 완성·출발 시각을 바꿔 재생, 재생 기록): 맵마다 위 시간표 순서대로 모두 남(캔디숲 6·놀이공원 5·베이커리 4+반복·공장 4·진저브레드 4+반복).
  반복음은 완성 뒤 재생 중(`OvenGears` 8 m, `AltarHum` 4 m), 출발 연출이 끝나면 멈춤. **늦게 들어온 경우(30초 전 완성)** 연출 소리 0개(반복음만 지금 상태대로 켜짐).
  탑승: 베이커리 `BoardSuck`, 공장 `BoardHop`. 진저브레드 등불 지직(간격 적용 후 7초에 5번 — 8개 등불 합계, 가까운 것만 들림).
  스파이 출발 → `RocketIgnite`·`WitchAppear`·경고 토스트 → `RocketLiftoff` → 마녀 다 나타남 `StWitchAppear` → 내리침 `WitchSlam` → 결과 징글. 콘솔 오류·경고 0.

### S7 — 환경음 ✅ 완료(2026-10-04)
- 세 종류로 정리(`Editor/Audio/AmbientPlacer` 표 → 맵 씬 `Ambience` 루트에 배치, Tools/TagOfChaos/Audio/Place Ambient Emitters):
  - **맵 바탕(새로 추가)** — `AmbientZone`(2D, 맵 전체 땅 위 상자, 배경음은 건드리지 않음): 캔디숲 `AmbCandyForest`(바람·새·풍경), 진저브레드 `AmbGingerbread`(바람·시계탑 똑딱·삐걱),
    놀이공원 `AmbCarnival`(바람·멀리 단조 오르골·깃발 펄럭), 베이커리 `AmbBakery`(오븐 장작 타닥·유령 바람). 공장은 기계 소리 4곳이 바탕을 대신한다.
    처음 계획한 "맵 중심 3D"는 걷는 곳에 따라 크기가 달라져 바탕음에 맞지 않아 2D 영역으로 바꿨다.
  - **랜드마크 3D**(S3+에서 넣음) — `AmbientEmitter`: 공장 기계 4곳(28 m), 회전목마(40 m). 가까이 갈 때만 재생, 멀어지면 칸 반납.
  - **영역 + 배경음 낮춤**(Request1003) — 진저브레드 지하 동굴 `AmbCave`. 바탕 상자는 지하(y < −1)를 빼 동굴과 겹치지 않는다.
- `AmbientZone`: 배경음 배율은 1보다 작을 때만 바꾼다(바탕 영역과 동굴 영역이 전환 중에 서로 덮어쓰지 않게). 카탈로그: 바탕 4종 2D·환경음 묶음(−12 dB)·동시 1개.
- 시험음 4종(`Source~/sfx/synth_s7.py`, numpy — S8에서 교체): 20초 이음매 없는 반복(순환 잡음·정수 주기 흔들림·순환 배치, 끝↔처음 차이가 보통 샘플 간격 이내).
- 랜드마크 위치는 원래 계획(장치·시계탑·오븐·굴뚝·입구) 중 장치는 S6 연출 소리(오븐 톱니·제단 웅웅 반복음)가, 시계탑·오븐은 바탕음(똑딱·장작)이 맡는다.

**검증**
- 컴파일 오류·경고 0. AudioTests 55/55(+ `AmbientBeds_CoverMaps_AndStayOffTheCave` — 실제 맵·클립·2D·맵 구석까지 덮음·동굴과 안 겹침·맵 4개), MapCompactTests 25/25.
- Play Mode(맵 5개 오프라인, 재생 중인 반복음·음량·듣는 위치 기록): 캔디숲·베이커리 바탕 0.25(2D), 놀이공원 바탕 + 회전목마(35 m 0.04 → 8 m 0.48),
  공장 기계(7 m 0.21, 멀리 가면 정지), 진저브레드 땅 위 바탕 → 지하 동굴로 바뀌고 배경음 0.15배 → 다시 땅 위로 원래대로. 콘솔 오류·경고 0.
- BuildSceneTests 7/9: `Game_CandyForest`·`Game_CursedCandyCarnival`에 `GameObject`(OfflineModeBootstrap만 붙음)가 저장돼 있다 — 에디터에서 직접 넣은 시험용으로 보여 지우지 않았다(S3 때와 같은 경우).
  출시 전에 빼야 한다.

### S8 — 음원 제작·교체 ✅ 완료(2026-10-06) → `Plan.md/AudioProductionPlan.md`
- 기준: `Plan.md/SoundReplacementList.md`(110개 명세 + 사용자 Audio Direction) → `AudioProductionPlan.md`(정체성·팔레트·모티프 3·음량 계층·ID별 방향, 사용자 승인 — 추천안).
- P1 정체성 견본(37개) 후 사용자 요청("일단 전체적으로 만들어주고 인게임에 넣어줘")으로 후보·청취 관문 없이 110개를 한 번에 제작·교체했다.
- **배경음 15**(`Source~/final/scripts/final_music.py`): 반복 곡 7개는 A → B → A' 72~109초(GameLobby 94·MonsterWait 72·CandyForest 92·Gingerbread 100·Factory 103·Carnival 90·Bakery 109초), 세 바퀴 렌더 후 가운데만 잘라 이음매 없음,
  −18 LUFS, OGG 스테레오. 스팅어 5(−15 LUFS)·징글 3(−16 LUFS). 확정곡 `Lobby`와 재생되지 않는 `…Hunt`·`TimeAttack`은 그대로.
- **효과음 95(파일 100)**(`final_sfx.py`): 쿠키(바삭·나무 톡·마림바)/괴물(저역 쿵·질척·카툰 목소리 흉내)/마법(역재생 종 → 셀레스타 4음) 팔레트, 변형 `CookieStep` 4·`MonsterStep` 3.
  반복음(붓질·가마솥·조준 윙·오븐 톱니·제단·환경음 7)은 같은 세기 교차로 이음매 없음. 환경음은 LUFS로 아주 조용하게(−24~−30), 회전목마는 CarnivalPaint와 같은 주제의 30초 왈츠.
- **교체**: 기존 시험음은 `Assets/10. Audio/Source~/placeholder_backup/`(원래 폴더 구조 그대로, .meta 포함)으로 옮겼고, 최종 음원 원본은 `Source~/final/S8/`, 제작 스크립트는 `Source~/final/scripts/`, P1 견본은 `Source~/final/P1/`에 따로 보관.
  게임 폴더(`BGM/`, `SFX/<묶음>/`)에 같은 이름으로 넣고 카탈로그를 다시 연결(조정값 유지). 코드 변경 없음.
- 전송: Unity 코드 실행 한 번에 5만 자 제한이 있어, 큰 파일은 `manage_ui`로 USS 주석에 실어 보낸 뒤 한 번에 해독(약 15배 빠름 — 115개 22MB를 5분).

**검증**
- 카탈로그: 효과음 98항목 중 95개 새 파일(빈 3개 = 재생 지점 없는 `CookieCarried`·`SpectateSwitch`·`MonsterWaitTick`), 배경음 15개 새 OGG. 컴파일·콘솔 오류·경고 0.
- 테스트: AudioTests 55/55 · EscapeTests 52/52 · RuleTests 27/27 · DistanceFadeTests 12/12.
- Play Mode(오프라인, 재생 기록): 맵 5개 연출 시간표 소리 전부(캔디숲 6·놀이공원 5·베이커리 5+반복·공장 4·진저브레드 4+반복), 늦게 들어온 경우 0개,
  스파이 로켓 점화·떠오름, 마녀 등장·스팅어·내리침, 등불, 탑승(오븐 빨려 듦·기차 폴짝), 탈출 흐름(상자 → 줍기 → 떨어뜨리기 → 끼우기 → 완성 → 탑승 → 탈출 성공 → 징글),
  대기실(GameLobby 곡·가마솥 끓음·풍덩·문), 맵 배경음·바탕 환경음·회전목마 거리 감쇠·공장 기계·진저브레드 지하 전환. 콘솔 0.
- 시험 방법 메모: 상태를 바꾸는 시험 명령이 에디터를 약 1.5초 멈춰 0초 지점 소리가 허용 시간(1.5초)에 걸린 적이 있어, 완성·출발 시각을 2초 뒤로 잡아 시험했다(실제 게임과 무관 — 음원 로드로 인한 멈춤은 없음을 프레임 간격으로 확인).
- 남은 것: 실제 귀로 듣고 마음에 안 드는 소리 다시 만들기(스크립트에서 값만 바꿔 다시 뽑기), 멀티 환경에서 들어 보기.

### S9 — 음량 설정·마무리 ✅ 완료(2026-10-06)
- **소리 설정 창**(`Audio/SoundSettingsPanel`, 프리팹 `04. Prefabs/UI/SoundSettingsPanel.prefab`): 전체·배경음·효과음·UI 막대 4개 + 퍼센트, 닫기. 움직이는 대로 저장(`AudioVolumeSettings` — PlayerPrefs)하고
  재생 중인 소리에 바로 반영, 효과음·UI 막대는 짧은 미리 듣기 소리. 환경음은 효과음 막대를 따른다.
  - 맵 5개(GameSceneCore)·대기실: ESC 메뉴에 **"소리" 버튼(모든 사람)** — 계속하기 · 소리 · (방장: 설정) · 나가기, 상자는 버튼 수만큼 커짐. 창을 닫으면 메뉴로, ESC는 전부 닫음(`EscMenu` 수정).
  - 로비: 오른쪽 위 "소리" 버튼 → 같은 창(ESC로도 닫힘).
  - 설치 도구 `Editor/Audio/SoundSettingsInstaller`(Tools/TagOfChaos/Audio/Install Sound Settings — 다시 실행해도 중복 없음). 문구(소리·전체·배경음·효과음·UI·닫기)는 프리팹·씬에 입력. 버튼 소리: 소리 버튼 클릭, 닫기 취소.
  - 창 상자는 불투명(로비에서 뒤 글자가 비치던 것 수정).
- **믹스 점검**(게임 안 실제 크기 = 음원 LUFS + 카탈로그 음량 + 묶음 이득): 배경음 −26 / 공장 기계·가마솥 −36 / **맵 바탕 환경음 −42 → −36으로 6 dB 올림**(음원 −24 LUFS로 교체) /
  지하 동굴 −31(배경음 0.15배) / 회전목마 가까이 −30 / **오븐 톱니·제단 웅웅 −22~−24 → 카탈로그 음량 0.5(−6 dB)** — 출발까지 계속 나는 반복음이 배경음보다 크지 않게.
- **동시 재생 점검**(8인 판 가정): 한 순간 64개 재생 요청 → 3D 칸 24개 꽉 참, 괴물 발소리·잡기(중요도 높음)는 칸을 받고 회전목마 반복음은 유지, 같은 소리 동시 요청은 쿨다운으로 1번. 콘솔 0.
- **빌드 확인**: Windows 64 빌드 성공(오류 0, 경고 2 — Photon 재컴파일 안내·ProBuilder 셰이더(Unity 기본 셰이더 파일), 소리와 무관). 별도 폴더 `Builds/S9Check/`(기존 빌드 폴더는 건드리지 않음).
  실행 시험: 엔진·그래픽 초기화 → 로비 → 서버(kr) 연결까지 로그에 오류·예외 0, 20초 이상 정상 실행. 빌드에서 소리를 귀로 듣는 것과 설정 저장은 사람이 해 봐야 한다(남은 확인).

**검증**
- 컴파일 오류·경고 0. AudioTests 58/58(+ 창 프리팹 막대 4개·연결, ESC 메뉴 2곳·로비 설치, 퍼센트·미리 듣기) · EscapeTests 52/52 · RuleTests 27/27 · DistanceFadeTests 12/12.
- Play Mode: 진저브레드 — ESC 메뉴(소리 버튼) → 소리 창 → 배경음 25%에 맵 배경음 0.398 → 0.100, 기기 저장·퍼센트 표시, 전체 0 → 소리 0, 효과음 막대 미리 듣기, 닫기 → 메뉴.
  대기실 — 방장 메뉴 4버튼·상자 410, 대기실 곡 0.398 → 0.100. 로비 — 오른쪽 위 버튼 → 창 열림·닫힘. 시험 후 음량은 100%로 되돌림. 콘솔 0.
- BuildSceneTests: 캔디숲·공장·놀이공원·대기실 씬에 사용자 시험용 `GameObject`(OfflineModeBootstrap) — 지우지 않음, 출시 전에 빼야 한다.

예상 합계: 코드 약 12시간 + 음원 제작(배경음 14곡 + 스팅어 8개 + 효과음 75종).

---

## 5. 성능·안전

- 재생마다 `AudioSource`를 만들거나 지우지 않는다(풀). 반복음(`StartLoop`)은 핸들로 멈추고 풀에 돌려준다.
- 같은 소리 동시 재생 수·쿨다운으로 폭주를 막는다(예: 8명이 동시에 칸을 바꿔도 `UiSlotSelect`는 본인만).
- 배경음은 스트리밍 + 미리 로드 안 함 → 카탈로그(Resources)가 로드돼도 음원 데이터가 한꺼번에 메모리에 올라가지 않는다. 효과음은 짧아서 압축 해제 상태로 둔다.
- 게임이 멈추지 않으므로(온라인) ESC 메뉴에서 소리를 멈추지 않는다. 결과 화면에서 효과음 그룹만 낮춘다.
- 괴물이 대기실에서 메시지 큐를 멈춘 동안에도 로컬 재생은 문제없다(네트워크와 무관).

## 6. 검증 계획(테스트)

| 테스트 | 종류 |
|---|---|
| 모든 `SoundId`·`MusicId`에 카탈로그 항목과 클립이 있다 | EditMode |
| 빌드 씬·UI 프리팹의 모든 `Button`에 `UiSound`가 있다 | EditMode(YAML/프리팹 검사) |
| `MusicRules.Select` 표(씬 × 단계 × 상황) | EditMode(순수 함수) |
| 풀이 꽉 찼을 때 중요도 낮은 소리를 뺏는다, 쿨다운 | EditMode(순수 C#) |
| 시퀀스 소리 신호가 경계를 지날 때 한 번만 난다(늦게 들어온 경우 0번) | EditMode(`CueAt` 순수 계산) |
| 음량 설정 저장·복원, dB 변환 | EditMode |
| 맵 5개 연출·탈출 흐름 소리 순서 | Play Mode(오프라인, 로그) |
| 빌드에서 소리·설정 저장 | 빌드 |

## 7. 위험

| 위험 | 대응 |
|---|---|
| 합성·MIDI 음원의 품질 한계(특히 배경음) | 효과음은 CC0 팩으로 교체 가능하게 ID·카탈로그로 분리. 배경음은 MIDI 원본을 남겨 MuseScore(무료)에서 다듬거나 다른 음원으로 교체 |
| 소리가 숨바꼭질 정보를 너무 많이 줌 | D3 결정 + 거리·음량을 카탈로그 값으로 두어 코드 수정 없이 조정 |
| 방장 화면에서 같은 변화 소리가 두 번(로컬 즉시 적용 + 서버 응답) | research.md R4.10-10(상태 판 번호) 또는 소리별 짧은 중복 방지 |
| 빌드에서만 생기는 문제 | S9 빌드 확인을 완료 조건에 포함 |
| 카메라가 멀리 있을 때(관전 13 m, 1인칭) 거리감이 어색 | 리스너는 Main Camera에 두되, 필요하면 리스너를 “보는 캐릭터” 위치로 옮기는 옵션(P2) |
