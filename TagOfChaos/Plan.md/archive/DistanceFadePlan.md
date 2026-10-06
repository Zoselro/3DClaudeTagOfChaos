# 계획: 거리 기반 음량 감쇠(Distance-based Volume Fading) — S4 전에 (DistanceFadePlan)

작성 2026-10-03 · 상태: **✅ 구현 완료(2026-10-03)** — 결과는 §10 · 위치: SoundPlan.md S3+ 다음, S4 앞(“S3.5”)

## 0. 목표

- 다른 플레이어의 발소리, 앞으로 넣을 스턴건·물풍선·뿅망치 소리가 **“얼마나 가까운지”를 귀로 알 수 있게** 한다.
  - 가까우면 또렷하게, 멀어질수록 자연스럽게 작아지고, 일정 거리 밖에서는 완전히 들리지 않는다.
  - 숨바꼭질 균형(D3): 쿠키 소리는 짧은 거리, 괴물 소리는 긴 거리.
- 이미 넣은 소리(맵 환경음)도 같은 규칙으로 맞춘다.
- S4(캐릭터 소리)·S5(도구 소리)는 이 기반 위에서 **값(거리·곡선 종류)만 정해 붙이면** 되도록 만든다.

## 1. 조사 결과 — 지금 구조와 문제점

### 1.1 지금 구조(S1에서 만든 것)

| 항목 | 지금 | 위치 |
|---|---|---|
| 3D 감쇠 곡선 | **모든 소리가 같은 곡선 1개**(0 m = 1, 최대 거리의 10% = 0.62, 25% = 0.33, 50% = 0.12, 100% = 0) | `AudioRuntime.Rolloff` |
| 소리별 값 | 최대 거리(`maxDistance`)만 있음. 꽉 찬 음량으로 들리는 반경(최소 거리)·곡선 종류 없음 | `SoundCatalogSO.Entry` |
| 듣는 위치 | `AudioListener` = Main Camera(유니티 기본). 쿠키 3인칭 카메라는 캐릭터 **3.2 m 뒤·위**, 괴물은 1인칭 눈 위치, 관전은 관전 대상 뒤 | `Camera_Ctrl.m_DefaultDist = 3.2` |
| 층(위·아래) | 구분 없음 — 수평·수직 거리를 똑같이 취급 | |
| 재생 칸 | 3D 24칸. 꽉 차면 “중요도가 같거나 낮은 것 중 가장 오래된 것”을 뺏는다 — **거리는 보지 않는다** | `AudioVoicePool.Acquire` |
| 들리지 않는 거리의 소리 | 최대 거리 밖이어도 칸을 차지하고 재생한다(음량 0) | `AudioRuntime.Play` |

### 1.2 이미 구현된 소리 중 거리 감쇠가 필요한 곳

| 소리 | 지금 | 문제 | 필요한 것 |
|---|---|---|---|
| **공장 기계 `AmbFactory`** — `AmbientEmitter` 4곳(기계실 동·서, 북쪽 프레스 줄, 가운데 파이프 `y = 14`), 28 m | 3D 반복, 공통 곡선 | ① 맵 어디에 있든 **4칸을 항상 차지**(24칸 중 1/6) — 8인 판에서 발소리·도구 소리 칸이 모자라질 수 있음 ② 파이프 허브가 14 m 높이라 **바로 아래층에서는 위층 소리가 그대로 들림** ③ 기계 바로 옆이 아니면 금방 작아져 “기계실 안에 있는” 느낌이 약함 | 거리 밖이면 칸 반납(§2.4) · 층 감쇠(§2.3) · 최소 거리 6 m(§2.2 `Landmark`) |
| **회전목마 `CarouselSpin`** — 놀이공원 중심 1곳, 40 m | 3D 반복, 공통 곡선 | ① 회전목마 바로 앞(4~5 m)에서도 0.6 수준으로 이미 작아짐 — 크기에 비해 소리가 “한 점”에서 남 ② 맵 어디서든 칸 1개 상시 점유 | 최소 거리 8 m(`Landmark`) · 거리 밖 칸 반납 |
| **진저브레드 지하 동굴 `AmbCave`** — `AmbientZone`(2D) | 카메라가 상자 안에 있으면 시간에 따라 서서히 커짐(0 → 0.55), 배경음 0.15배 | 기준이 **카메라 위치** — 쿠키가 계단에 서 있을 때 카메라(3.2 m 뒤·위)가 아직 지상이면 소리가 늦게 켜짐 | §2.1의 듣는 위치로 바꾸기만 하면 됨(2D라 감쇠 곡선은 필요 없음) |
| UI 소리·배경음·스팅어 | 2D | — | 필요 없음 |

> 캐릭터·탈출·도구·맵 연출 소리는 카탈로그 항목(기본값)만 있고 아직 재생하는 코드가 없다(S4~S6). 이번에 기반을 먼저 만들어 두면 그때 값만 고르면 된다.
> 기본값 중 다시 봐야 할 것: 쿠키 발소리 12 m는 **공통 곡선 때문에 3 m에서 이미 0.33**(거의 안 들림) — 곡선 종류를 나누지 않으면 “가까이 있어도 발소리가 잘 안 들리는” 문제가 생긴다.

## 2. 설계

### 2.1 듣는 위치를 캐릭터 머리로 — `AudioListenerAnchor` (결정 L1)
- `AudioListener`를 카메라에서 떼어, **내 캐릭터 머리 위치 + 카메라가 보는 방향**을 따라가는 오브젝트에 둔다.
  - 쿠키 3인칭: 캐릭터 머리(카메라 뒤 3.2 m가 아니라). 관전: 관전 대상 머리. 괴물 1인칭: 눈 위치(지금과 같음). 대기·결과 화면: 카메라.
  - 방향(좌·우 소리)은 카메라 방향을 따른다 — 화면에서 왼쪽에 보이는 것은 왼쪽에서 들린다.
- 이유: 거리 감쇠의 기준점이 3.2 m 어긋나 있으면 “1 m 옆 쿠키 발소리”가 실제로는 3~4 m짜리 음량으로 난다. 숨바꼭질에서 거리 판단이 틀어짐.
- 대상은 `Camera_Ctrl`의 따라가는 대상(이미 관전 전환까지 처리됨)과 `MonsterFirstPersonCamera` 켜짐 여부로 정한다 → 캐릭터 코드는 바꾸지 않는다.
- `AmbientZone`도 이 위치를 쓴다(지하 진입 즉시 동굴 소리).

### 2.2 소리 종류별 감쇠 곡선 + 최소 거리 — `SoundFalloff`
카탈로그 항목에 두 값을 더한다(코드 수정 없이 에셋에서 조정):
- `falloff`(곡선 종류), `minDistance`(이 안에서는 꽉 찬 음량).

| 곡선 | 모양 | 쓰는 소리 |
|---|---|---|
| `Footstep` | 최소 거리까지 1, 그 뒤 부드럽게(대략 1/거리) 줄다가 최대 거리 근처에서 0으로 | 쿠키 발소리·점프·착지·회피 |
| `Heavy` | 멀리서도 존재감 — 최대 거리의 50%에서 0.35 정도 | 괴물 발소리·돌진·잡기 |
| `Action` | 짧고 또렷한 사건음 — 중간 거리까지 잘 들리고 끝에서 빠르게 0 | 스턴건·물풍선·뿅망치, 상자·아이템·설치 |
| `Landmark` | 넓은 최소 거리(물체 크기만큼) 뒤 천천히 | 공장 기계·회전목마·가마솥, 맵 연출(장치·로켓·기차) |

- 음량 계산은 **순수 함수** `SpatialGain.Evaluate(곡선, 거리, 최소, 최대, 높이 차)`로 만들고, `AudioRuntime`이 매 프레임 재생 중인 3D 칸(최대 24개)에 곱한다.
  유니티 감쇠는 끄고(평평한 곡선) 좌우 위치감(팬)만 유니티에 맡긴다 → 곡선·층 감쇠·칸 관리가 한 곳의 같은 계산을 쓰고, EditMode에서 숫자로 시험할 수 있다.
- 비용: 칸 24개 × 거리 계산 1번/프레임 — 무시할 수준. 생성·파괴 없음.

### 2.3 층 감쇠(위·아래층 분리) (결정 L2)
- 소리와 듣는 위치의 **높이 차가 4 m를 넘으면 다른 층**으로 보고 음량을 크게 낮춘다(기본 0.15배, 4~7 m 사이는 서서히). 4 m는 탈출 장치가 쓰는 “다른 층” 기준(`EscapeDevice.MaxReachHeight`)과 같다.
- 대상: 진저브레드 지하 유적 ↔ 지상, 시계탑 지하, 공장 파이프 허브(14 m) 아래, 여러 층 건물.
- 벽 가림(레이캐스트)은 하지 않는다(비용·오판 위험). 필요하면 S9에서 따로 검토.

### 2.4 들리지 않는 소리는 칸을 쓰지 않기
- **한 번짜리 소리**: 재생 시점에 계산한 음량이 0.01 미만이면 재생하지 않는다(멀리 있는 남의 발소리가 칸을 차지하지 않게).
- **반복음(`AmbientEmitter`)**: 최대 거리 + 5 m 안에 들어오면 시작, 최대 거리 + 10 m 밖으로 나가면 멈춤(경계에서 깜빡이지 않게 여유를 둠). 시작할 때 0에서 0.5초 동안 키운다(이미 있는 `SetFade` 사용). 반복음은 무작위 지점에서 시작하므로 다시 켜져도 어색하지 않다.
- **따라가는 소리**(달리는 캐릭터): 재생 중 멀어지면 음량만 줄고(곡선) 칸은 소리가 끝날 때 돌려받는다(발소리는 짧음).

### 2.5 칸이 꽉 찼을 때 “잘 들리는 소리” 우선
- `AudioVoicePool.Acquire`가 중요도 다음으로 **지금 들리는 크기**(§2.2 음량)를 본다: 같은 중요도면 가장 작게 들리는 칸을 뺏고, 새 소리가 그보다 작으면 새 소리를 버린다.
- 예: 8명이 뛰는 중 멀리 있는 발소리 대신 바로 옆 물풍선 터짐이 난다.

### 2.6 내 소리는 2D (결정 L3와 무관, 기본 규칙)
- 내 캐릭터가 내는 소리(내 발소리, 내가 쏜 스턴건·던진 물풍선·휘두른 뿅망치)는 2D로, 남이 낸 같은 소리는 3D로.
  듣는 위치가 내 머리에 있으므로 3D로 내면 좌우가 흔들릴 수 있다. `GameAudio.PlayCharacter(id, 캐릭터)` 한 곳에서 판단한다(S4·S5가 이것만 부른다).
- 도구는 방장 승인 뒤 모두에게 오는 `ToolHit`(빗나가도 옴)에서 소리를 내는 것이 기준. 내 발사음만 누르는 순간 바로 낸다(지연 없이), 같은 사건의 `ToolHit`에서는 내 발사음을 다시 내지 않는다.

### 2.7 거리가 먼 소리는 살짝 먹먹하게 (선택, 결정 L3)
- 3D 칸 24개에 저역 통과 필터를 붙여, 최대 거리에 가까울수록 고음을 줄인다(22 kHz → 약 3 kHz). “멀리서 나는 소리”가 음량뿐 아니라 소리 색으로도 구분된다. 다른 층 소리에도 같이 적용.
- 비용: 칸마다 필터 1개(미리 만듦). 넣지 않아도 나머지 계획은 그대로 동작한다.

## 3. 소리별 거리 제안(카탈로그 기본값 — 에셋에서 조정 가능)

| 소리 | 곡선 | 최소 → 최대(m) | 비고 |
|---|---|---|---|
| `CookieStep`(달리기만) | Footstep | 1.5 → 12 | D3. 걷기·숨기 이동은 소리 없음 |
| `CookieJump` · `CookieLand` · `CookieDodge` | Footstep | 1.5 → 12 | |
| `MonsterStep` | Heavy | 3 → 30 | 쿠키에게 경고 |
| `MonsterDash` · `MonsterGrab` · `MonsterSquash` | Heavy | 4 → 35 | |
| `CookieGrab` · `CookieRelease` · `CookieCrumble` · `StunStars` · `Respawn` | Action | 1.5 → 15 | |
| `StunFire` | Action | 2 → 25 | 사거리 15 m보다 넓게 — 맞기 전에 쏜 소리를 들을 수 있게 |
| `StunHit` | Action | 2 → 20 | |
| `BalloonThrow` | Action | 1.5 → 15 | |
| `BalloonSplash` | Action | 2 → 22 | 떨어진 지점에서 |
| `HammerSwing` | Action | 1.5 → 12 | |
| `HammerBonk` | Action | 2 → 20 | |
| `StunAimHum` | 2D(본인만) | — | |
| 상자·아이템·설치(`Chest*`·`Item*`·`DeviceInsert`) | Action | 1.5 → 15 | |
| 맵 연출(7xx)·`DeviceComplete`·로켓 | Landmark | 8 → 60 | |
| `AmbFactory` | Landmark | 6 → 28 | 층 감쇠 적용 |
| `CarouselSpin` | Landmark | 8 → 40 | |
| `CauldronBubble` · `CauldronSplash` | Landmark / Action | 3 → 15 | 대기실 |

## 4. 파일·클래스(폴더 규칙 준수)

| 파일 | 할 일 |
|---|---|
| `Assets/02. Scripts/Audio/SpatialGain.cs` (새) | `SoundFalloff` 열거형 + `Evaluate(...)` 순수 함수(곡선 4종·층 감쇠) |
| `Assets/02. Scripts/Audio/AudioListenerAnchor.cs` (새) | 듣는 위치 오브젝트. `Camera_Ctrl`·`MonsterFirstPersonCamera`를 읽어 따라감. `AudioRuntime`이 만들고 리스너를 옮김. `AudioListenerAnchor.Position`을 다른 코드가 읽음 |
| `SoundCatalogSO.Entry` | `falloff`, `minDistance` 추가(기존 에셋 값 유지, 새 필드만 기본값) |
| `AudioRuntime` | 유니티 감쇠 끄기, `LateUpdate`에서 3D 칸 음량 = 기본 × 묶음 × 페이드 × `SpatialGain`, 재생 전 들림 검사, (L3) 필터 |
| `AudioVoicePool` | `Acquire`에 “지금 들리는 크기” 반영 |
| `AmbientEmitter` | 거리 기반 시작·멈춤(여유 5/10 m) + 0.5초 페이드 인 |
| `AmbientZone` | 기준 위치를 `AudioListenerAnchor.Position`으로 |
| `GameAudio` | `PlayCharacter(id, IGameCharacter)`(내 것 2D / 남의 것 3D) |
| `Editor/Audio/AudioCatalogBuilder.cs`(`SoundDefaults`) | §3 표를 기본값으로. 이미 있는 항목에 새 필드를 채우는 일회성 메뉴(“Apply Falloff Defaults”) |
| `Editor/Tests/AudioTests.cs` | §6 테스트 |

## 5. 단계(예상 약 2시간 30분 + 추격음 §9 약 1시간)

1. `SpatialGain` + 테스트 → 카탈로그 필드·기본값 채우기.
2. `AudioListenerAnchor`(쿠키·괴물·관전·대기실 확인).
3. `AudioRuntime` 매 프레임 음량, 들림 검사, 칸 우선순위.
4. `AmbientEmitter` 거리 시작·멈춤, `AmbientZone` 기준 위치.
5. (L3를 고르면) 거리 저역 통과.
6. 추격음(§9): 설정 SO·`ChaseDirector`·고른 음원 넣기.
7. 검증 → SoundPlan.md에 S3.5 ✅ 표시 → S4 시작(발소리·캐릭터 소리는 `PlayCharacter` + §3 값으로).

## 6. 검증

| 테스트 | 종류 |
|---|---|
| 곡선 4종: 최소 거리 안 1, 최대 거리 이상 0, 거리에 따라 줄기만 함(단조 감소), `Footstep` 3 m가 0.5 이상 | EditMode |
| 층 감쇠: 높이 차 3 m는 그대로, 8 m는 0.15배 | EditMode |
| 칸 우선순위: 같은 중요도면 가장 작게 들리는 칸을 뺏음, 더 작은 새 소리는 버림 | EditMode |
| 반복음 시작·멈춤 여유(경계 왕복 시 깜빡임 없음) | EditMode(순수 함수) |
| 모든 3D 항목: `minDistance < maxDistance`, 곡선 지정됨 | EditMode |
| 오프라인 2캐릭터: 다른 캐릭터를 1·3·6·10·14 m에 두고 시험음 재생 → 칸 음량 기록, 14 m(최대 밖)에서는 칸을 쓰지 않음 | Play Mode |
| 공장: 맵 반대편에서 기계 칸 0개, 기계실로 걸어가면 켜지고 서서히 커짐 / 파이프 허브 아래층 음량 감소 | Play Mode |
| 진저브레드: 계단을 내려가는 순간 동굴 소리 시작(카메라 기준보다 빠름), 지하에서 지상 소리 감소 | Play Mode |
| 관전 전환·괴물 1인칭에서 듣는 위치가 따라감 | Play Mode |
| 회귀: AudioTests·RuleTests·EscapeTests 등 전체, 콘솔 0 | |

## 7. 결정 필요(승인 시 함께 답해 주세요)

| # | 질문 | 추천 |
|---|---|---|
| L1 | 듣는 위치: **캐릭터 머리**(거리 정확) vs 지금처럼 카메라 | ✅ **캐릭터 머리** |
| L2 | 다른 층 소리: ✅ **크게 줄이기(0.15배)** vs 완전히 끄기 | **크게 줄이기** — 지하에서 위층 괴물 발소리가 희미하게라도 들리는 편이 긴장감 있음 |
| L3 | 먼 소리 먹먹하게(저역 통과): ✅ 넣기 vs 빼기 | **넣기**(비용 작음, 거리감이 확실히 좋아짐) |
| L4 | §3 거리 값 | ✅ 표 그대로 시작하고 S4·S5 플레이 후 조정 |
| C1 | 추격음 후보(§9.1) | ✅ **D 북소리 추격**(2026-10-03) — 가까울수록 북 비트가 빨라지게(§9.5) |
| C2 | 추격음 기본 거리: 20 m부터 들리고 5 m 안에서 최대 | ✅ 그대로 시작 → 인스펙터에서 직접 조정 |
| C3 | 관전 중(부서진 쿠키)에도 관전 대상 기준으로 추격음 듣기 | ✅ **듣지 않기**(답이 없어 추천대로 진행 — 바꾸려면 `ChaseIntensity.CanListen`) |

## 8. 위험

| 위험 | 대응 |
|---|---|
| 듣는 위치를 바꾸면 화면과 소리 위치가 살짝 다르게 느껴질 수 있음 | 방향은 카메라를 따르므로 좌우는 화면과 같다. 어색하면 머리↔카메라 사이 비율 값(기본 0 = 머리)으로 조정 |
| 거리 밖 반복음을 멈췄다 켜면 소리가 “툭” 시작 | 0.5초 페이드 인 + 무작위 시작 지점 |
| 층 기준 4 m가 맞지 않는 지형(경사·높은 무대) | 기준 높이를 카탈로그가 아닌 전역 값 하나로 두어 조정 쉽게. 맵별 예외가 필요하면 `AmbientZone`처럼 영역으로 처리 |
| 숨바꼭질 정보 과다(발소리로 위치가 너무 쉽게 드러남) | 쿠키는 달리기만 소리(D3) + 12 m. 플레이 후 값만 조정 |

## 9. 술래 접근 추격음(2026-10-03 추가 요청)

술래(괴물)가 쿠키에게 다가오면 **그 쿠키 본인 화면에서만** 무서운 추격음이 커진다. 멀면 들리지 않고, 가까워질수록 커지고 또렷해진다.
“몇 m 안에서 들릴지”는 **직렬화된 설정 에셋**(인스펙터)에서 사용자가 직접 바꾼다 — 코드 수정 없이, Play 중에도 바로 반영.

### 9.1 후보 4종(✅ D 선택 — §9.5에서 층 나누기로 확정)
`scratchpad/audio/out/chase/` (합성 스크립트 `synth_chase.py`, 정식으로 고르면 `Assets/10. Audio/Source~/sfx/`로 옮김)

| 후보 | 파일 | 느낌 | 반복 길이 |
|---|---|---|---|
| A 심장 박동 | `Chase_A_Heartbeat.wav` | 빨라진 심장 쿵-쿵 + 바닥을 긁는 저음 드론 + 거친 숨 + 가끔 쇳소리 휘파람 | 15초 |
| B 현악 공포 | `Chase_B_HorrorStrings.wav` | 낮은 현 트레몰로 반음 뭉치 + 콘트라베이스 박동 + 높은 바이올린 찌르기(공포 영화식) | 16초 |
| C 망가진 오르골 | `Chase_C_BrokenMusicBox.wav` | 단조 오르골 + 음이 틀어진 두 번째 오르골 + 어둠 패드·팀파니(과자 세계관에 맞는 섬뜩함) | 25.7초 |
| D 북소리 추격 | `Chase_D_DrumChase.wav` | 타이코·팀파니 질주 + 현 16분 오스티나토 + 낮은 금관 경고(가장 급박) | 13.7초 |

- 각 후보의 `…_approach.wav` = 술래가 25 m → 2 m로 12초 동안 다가오는 미리듣기(기본값 20 m / 5 m, 멀면 먹먹하게). 반복 원본은 이음매 없이 이어진다(이음매 차이 < 일반 샘플 차이).
- 4종 모두 크기를 맞춤(RMS 약 −16 dBFS, 피크 −1 dBFS 이하) — 비교는 음량이 아니라 느낌으로.

### 9.2 직렬화 설정 — `ChaseAudioSettingsSO`
- 위치: `Assets/Resources/Audio/ChaseAudioSettings.asset`(전역 SO 규칙), 스크립트 `Assets/02. Scripts/Audio/ChaseAudioSettingsSO.cs`.
- `SoundCatalogSO`처럼 처음 접근할 때 Resources에서 한 번 읽는다(`ChaseAudioSettingsSO.Current`). 에셋이 없으면 기본값으로 동작.
- 매 프레임 값을 읽으므로 **Play 중 인스펙터에서 바꾸면 바로 들린다**(튜닝용). 저장은 에셋에 남는다.

| 필드(인스펙터) | 기본값 | 설명 |
|---|---|---|
| `audibleRadius` `[Min(1)]` | **20 m** | **이 거리 안에 술래가 들어오면 추격음이 들리기 시작** (요청한 “몇 m 이내”) |
| `fullRadiusRatio` `[Range(0,1)]` | **0.25** (= 5 m) | `audibleRadius` × 이 비율 안에서는 최대 음량 |
| `intensityCurve` (AnimationCurve) | 부드러운 S자 | 가로 0 = `audibleRadius` 경계, 1 = `fullRadius` 안 / 세로 = 음량 배율. 곡선을 직접 그려 바꿀 수 있음 |
| `maxVolume` `[Range(0,1)]` | 0.8 | 가장 가까울 때 음량 |
| `fadeInSeconds` / `fadeOutSeconds` | 0.4 / 1.5 | 커질 때는 빠르게, 술래가 멀어지면 천천히 사라짐(뚝 끊기지 않게) |
| `muffleWhenFar` + `farCutoffHz` | 켬 / 900 Hz | 멀 때 먹먹하게(L3와 같은 필터) |
| `musicDuckAtFull` `[Range(0,1)]` | 0.4 | 최대일 때 배경음 배율(추격음이 묻히지 않게) |
| `otherFloorGain` `[Range(0,1)]` | 0.15 | 술래가 다른 층(높이 차 4 m 넘음)이면 배율(§2.3과 같은 규칙) |
| `baseLayer` (AudioClip) | `Chase_Base` | 바탕(현 16분) — 들리는 동안 계속 |
| `drumLevels` (배열: `clip`, `startRatio` `[Range(0,1)]`) | **2단계(§9.7)**: `Chase_Far` / `Chase_Near`, **1.0 · 0.25** (= 20 · 5 m) | **북 단계별 시작 거리를 `audibleRadius`에 대한 비율로** — `audibleRadius`만 바꾸면 모든 단계가 같은 비율로 따라 줄거나 늘어난다(§9.6). 개수·비율·클립 모두 인스펙터에서 바꿈. 인스펙터에는 지금 값으로 계산한 실제 m도 옆에 보여 준다 |
| `levelHysteresisRatio` `[Range(0,0.2)]` | 0.05 (= 1 m) | 단계 경계에서 왔다 갔다 하지 않게 — 한 단계 올라간 뒤에는 `audibleRadius` × 이 비율만큼 더 멀어져야 내려감 |
| `levelCrossfadeSeconds` | 0.4 | 북 단계 교차 전환 시간 |

- `OnValidate`: `drumLevels`를 비율이 큰(먼) 것부터 정렬, 첫 단계 비율은 1 이하, `fullRadiusRatio`는 1 미만으로 보정(거꾸로 넣어도 깨지지 않게).
- 실제 거리 계산은 `ChaseAudioSettingsSO.LevelRadius(i) = audibleRadius × startRatio`처럼 한 곳에서만 한다.
- 편의: 메뉴 `Tools/TagOfChaos/Audio/Select Chase Settings`(에셋 바로 선택). 플레이 중 씬 뷰에서 술래 둘레에 두 반경을 원으로 표시(에디터 전용 기즈모, 선택 시).

### 9.3 동작 — `ChaseDirector`(로컬 전용, 네트워크 없음)
- `AudioRuntime`이 `MusicDirector`처럼 만들어 매 프레임 `Tick`(새 MonoBehaviour·네트워크 이벤트 없음).
- 듣는 조건: 내가 **쿠키(스파이 포함)**이고, 맵 씬의 **추격 단계(술래 합류 뒤)**이며, 부서짐·탈출·탑승 대기·관전 중이 아님(C3). 술래 본인·대기실·로비는 항상 꺼짐.
- 거리: 듣는 위치(§2.1 `AudioListenerAnchor`)에서 **가장 가까운 술래**까지 수평 거리 + 높이 차(층 판정). 술래는 `CharacterRegistry`에서 찾는다.
- 세기 계산은 순수 함수 `ChaseIntensity.Evaluate(거리, 높이 차, 설정)` → 0~1. 이 값으로 `GameAudio.SetLoopFade`(음량)·필터·배경음 배율을 정한다.
- 층(바탕 + 단계 수만큼, 기본 2단계 → 3개) 재생은 효과음 칸이 아니라 `ChaseDirector`가 가진 전용 AudioSource로 한다(`MusicPlayer`가 자기 칸을 가진 것과 같은 방식). 세기가 0보다 커질 때 모든 층을 **같은 DSP 시각에 `PlayScheduled`**로 시작해 샘플 단위로 박자를 맞추고, 0으로 줄어든 뒤 1초가 지나면 모두 멈춘다.
- 음량 = `maxVolume` × 세기 × Sfx 묶음 음량. 바탕은 항상 그 값, 북은 지금 단계만 1(교차 전환 0.4초), 나머지 0.
- 클립은 설정 SO가 직접 갖는다(층 묶음이라 효과음 카탈로그 ID 하나로 표현하기 어려움). 파일: `Assets/10. Audio/SFX/Chase/Chase_Base.wav`, `Chase_Far.wav`, `Chase_Near.wav`(압축 해제 로드 — 짧고 동시에 3개).
- 숨바꼭질 균형: 추격음은 “술래가 근처에 있다”만 알려 주고 **방향은 알려 주지 않는다**(2D). 방향은 술래 발소리(3D, §3)로 듣는다.

### 9.4 검증
| 테스트 | 종류 |
|---|---|
| `ChaseIntensity`: `audibleRadius` 밖 0, `fullRadius` 안 1, 사이에서 단조 증가, 다른 층 × `otherFloorGain` | EditMode |
| `OnValidate` 보정(반경을 거꾸로 넣어도 full < audible) | EditMode |
| 조건표: 술래 본인·부서짐·탈출·변장 시간·대기실이면 0 | EditMode(순수 함수) |
| 북 단계 선택: 20·14·9·5 m 경계와 1 m 여유(올라갈 때 5 m, 내려갈 때 6 m) / `audibleRadius` 10 m로 바꾸면 10·7·4.5·2.5 m, 여유 0.5 m | EditMode(순수 함수 `ChaseIntensity.Level`) |
| 설정 에셋: 바탕·북 클립 모두 있고 **길이가 같음**(박자 맞춤 조건) | EditMode |
| 층 박자: 단계 전환 뒤에도 다섯 AudioSource의 `timeSamples`가 같음 | Play Mode |
| 오프라인: 술래를 25·20·12·5·2 m에 두고 세기·음량·배경음 배율 기록, 인스펙터에서 `audibleRadius`를 10 m로 바꾸면 12 m에서 0이 되는지 | Play Mode |
| 진저브레드: 지상 술래 / 지하 쿠키 → `otherFloorGain` 적용 | Play Mode |

### 9.5 확정: D 북소리 추격 — 가까울수록 북 비트가 빨라짐(2026-10-03 사용자 선택)
곡 전체를 빨리 재생하면 음 높이까지 올라가 어색하므로, **템포(140 BPM)는 그대로 두고 북이 치는 횟수만 단계별로 늘린다**(게임 음악의 “층 쌓기” 방식).
모든 층은 같은 길이(8마디 = 13.714초)라 같은 순간에 시작하면 단계를 바꿔도 박자가 어긋나지 않는다.

| 층 | 파일(`scratchpad/audio/out/chase/D/`) | 언제(기본 거리) | 북 패턴 | 타격(초당 약) |
|---|---|---|---|---|
| 바탕 | `Chase_Base.wav` | 20 m 안 내내 | 현 16분 오스티나토 + 2마디마다 낮은 금관 경고(북 없음) | — |
| 북 1단계 | `Chase_Drums1.wav` | 20 m ~ 14 m | 쿵…쿵(2박마다 타이코) + 팀파니 마디 첫 박 | 5 |
| 북 2단계 | `Chase_Drums2.wav` | 14 m ~ 9 m | 원래 D 패턴(쿵 · 쿵쿵 · 쿵쿵) | 12 |
| 북 3단계 | `Chase_Drums3.wav` | 9 m ~ 5 m | 8분 타이코 + 4분 팀파니 | 16 |
| 북 4단계 | `Chase_Drums4.wav` | 5 m 안 | 16분 연타 + 8분 팀파니 + 2마디마다 탐 내려오기 | 19+ |

- 미리듣기: `Chase_D_approach_v2.wav`(25 m → 2 m를 20초 동안, 마지막 4초 바로 옆 — 단계가 1 → 4로 바뀌는 것을 들을 수 있음), `Preview_Level1~4.wav`(단계별 바탕 + 북, 두 바퀴).
- 같은 배율로 저장해 층끼리 크기 비율을 유지했다(가장 큰 조합 피크 −1 dBFS). 4단계가 가장 크게 들려 가까울수록 커지는 효과가 겹친다.
- 합성 스크립트: `synth_chase_d.py`(정식 반영 때 `Assets/10. Audio/Source~/sfx/`로 옮김).

### 9.6 거리는 비율로 — `audibleRadius` 하나만 바꾸면 전체가 따라감(2026-10-03 사용자 질문 반영)
- 사용자가 조정하는 실제 거리 값은 `audibleRadius` 하나(기본 20 m). 최대 음량 반경·북 단계 경계·단계 여유는 모두 그에 대한 **비율**로 저장한다.
- 예: `audibleRadius`를 20 → 10 m로 바꾸면

| | 20 m일 때 | 10 m일 때 |
|---|---|---|
| 북 1단계 시작 (1.0) | 20 m | 10 m |
| 북 2단계 시작 (0.7) | 14 m | 7 m |
| 북 3단계 시작 (0.45) | 9 m | 4.5 m |
| 북 4단계 + 최대 음량 (0.25) | 5 m | 2.5 m |
| 단계 여유 (0.05) | 1 m | 0.5 m |

- 단계 사이 간격만 따로 바꾸고 싶으면 각 단계의 `startRatio`를 바꾼다(예: 4단계를 더 일찍 → 0.25 → 0.35).

### 9.7 확정 2: 두 단계만 — 먼 단계(Level1) / 가까운 단계(Level4)(2026-10-03 사용자 결정)
| 층 | 파일(`scratchpad/audio/out/chase/D2/`) | 언제(기본) | 내용 |
|---|---|---|---|
| 바탕 | `Chase_Base.wav` | `audibleRadius`(20 m) 안 내내 | 현 16분 오스티나토만 |
| 먼 단계 | `Chase_Far.wav` | 20 m ~ 5 m (`startRatio` 1.0) | 북 1단계(쿵…쿵) + 2마디마다 낮은 금관 경고 “따~단” — **들어 본 Level1과 똑같은 소리·크기**(−21.0 dB) |
| 가까운 단계 | `Chase_Near.wav` | 5 m 안 (`startRatio` 0.25) | 북 4단계(16분 연타 + 탐 내려오기), **북 약 +4.3 dB**, **금관 “따~단” 없음** |

- 금관을 바탕에서 떼어 먼 단계 층에 붙였기 때문에, 가까워지면 금관이 북과 함께 교차 전환으로 사라진다.
- 북을 키우면서 생기는 순간 피크는 가까운 단계 층에만 거는 리미터(5 ms 앞보기, 80 ms 회복)로 막았다(바탕 + 가까운 단계 피크 −1 dBFS).
- 미리듣기: `Preview_Far.wav`, `Preview_Near.wav`(각 두 바퀴), `Chase_D_approach_v3.wav`(25 m → 2 m, 20 m에서 먼 단계 시작, 5 m에서 가까운 단계로).
- §9.5의 4단계 파일(`D/`)은 비교용으로만 남긴다. 단계를 다시 늘리고 싶으면 `drumLevels`에 항목을 추가하면 된다(코드 수정 없음).

## 10. 구현 결과(2026-10-03) ✅

### 10.1 바뀐 파일
| 파일 | 내용 |
|---|---|
| `Audio/SpatialGain.cs` (새) | `SoundFalloff` 4종 + 거리 곡선 `(최소/거리)^p × (1 − 진행^q)`, 층 감쇠(4 → 7 m, 0.15배), 먼 소리 차단 주파수(22 kHz → 3 kHz, 다른 층 1.5 kHz) |
| `Audio/AudioListenerAnchor.cs` (새) | AudioRuntime의 유일한 AudioListener. 3인칭 = 따라가는 대상 시선 높이(관전 대상 포함), 괴물 1인칭 = 눈, 그 밖 = 카메라. 씬 카메라 리스너는 끔 |
| `Audio/AudioRuntime.cs` | 유니티 감쇠 평평하게, 3D 칸 매 프레임 음량 × `SpatialGain`·저역 통과, 들리지 않는 한 번짜리 소리 건너뜀, 추격음·리스너 소유. 소리 칸은 꺼진 오브젝트에서 설정 후 켬(빈 칸 필터 오류 방지) |
| `Audio/AudioVoicePool.cs` | 같은 중요도면 가장 작게 들리는 칸을 뺏고, 더 작은 새 소리는 버림 |
| `Audio/AmbientEmitter.cs` | 최대 거리 + 5 m 안이면 0.5초 페이드 인으로 시작, + 10 m 밖이면 멈춤 |
| `Audio/AmbientZone.cs` | 기준 위치 = 듣는 위치 |
| `Audio/GameAudio.cs` | `PlayCharacter(id, 캐릭터)` — 내 캐릭터 2D, 남 3D |
| `Audio/SoundCatalogSO.cs` | 항목에 `minDistance`·`falloff` |
| `Audio/MusicPlayer.cs` | `ChaseGain`(추격음이 클 때 배경음 낮춤) |
| `Audio/ChaseAudioSettingsSO.cs`·`ChaseIntensity.cs`·`ChaseDirector.cs` (새) | 추격음 설정(비율 방식)·세기/단계 계산·층 재생(같은 DSP 시각 `PlayScheduled`) |
| `Camera/Camera_Ctrl.cs` | `FocusPoint`(따라가는 대상의 시선 높이) 읽기 전용 속성 |
| `Editor/Audio/AudioCatalogBuilder.cs` | §3 표 기본값(`SoundDefaults.Spatial`), 메뉴 `Tools/TagOfChaos/Audio/Apply Falloff Defaults` |
| `Editor/Audio/AudioImportPostprocessor.cs` | `Assets/10. Audio/Chase/` 규칙(모노·압축 해제) |
| `Editor/Audio/ChaseAudioBuilder.cs` (새) | 메뉴 `Build Chase Settings`·`Select Chase Settings`, 플레이 중 설정 에셋 선택 시 씬 뷰에 술래 둘레 거리 원 |
| `Editor/Tests/DistanceFadeTests.cs` (새) | 12개 |
| 에셋 | `Resources/Audio/ChaseAudioSettings.asset`(20 m, 먼 1.0 / 가까운 0.25), `10. Audio/Chase/Chase_Base·Far·Near.wav`, `10. Audio/Source~/sfx/synth_chase*.py`. 카탈로그 3D 항목에 §3 값 적용 |

### 10.2 Play Mode 검증
- 추격음(캔디숲, 술래를 쿠키 옆 25 → 2 → 25 m): 25 m 0 / 18 m 0.05 / 12 m 0.58 / 6 m 0.99 / 4.9 m 가까운 단계 / 5.5 m 유지(여유) / 6.5 m 먼 단계 / 25 m 1.5초 페이드 아웃 후 정지. 세 층 `timeSamples` 항상 같음(박자 일치), 배경음 1 → 0.4, 차단 주파수 1.4 k → 22 kHz. 듣는 위치 = 쿠키 머리(오차 0, 카메라와 3.2 m), 켜진 리스너 1개.
- 3D 효과음(임시 음원): 쿠키 발소리 3 m 0.62(예전 0.33) / 10 m 0.15 / 15 m 재생 안 함, 괴물 발소리 15 m 0.41 / 25 m 0.16, 스턴 명중 15 m 0.26 / 25 m 재생 안 함, 3 m 옆 8 m 위 0.06.
- 공장: 시작 지점에서 기계 4곳 모두 꺼짐(칸 0), 동쪽 기계실 → 그곳만 켜짐(칸 1), 파이프 허브 아래 → 허브만 켜짐·음량 0.49 → 0.07(층 감쇠), 먼 구석 → 모두 꺼짐.
- 콘솔 오류·경고 0.

### 10.3 회귀
DistanceFadeTests 12/12 · AudioTests 50/50 · RuleTests 27/27 · EscapeTests 46/46 · MapCompactTests 25/25 · BuildSceneTests 7/8 — 실패 1건은 이번 변경과 무관: `Game_GingerbreadVillage.unity`에 "GameObject"라는 오브젝트로 `OfflineModeBootstrap`(개발용)이 저장돼 있음(테스트 도구가 만드는 `__OfflineBoot`와 다른 이름 — 사용자가 직접 넣은 것일 수 있어 지우지 않음).

### 10.4 남은 것
- 효과음 음원(S8) — 지금 3D 효과음 ID 대부분은 클립이 없어 소리가 나지 않는다. 값은 준비됨.
- S4·S5에서 캐릭터·도구 소리는 `GameAudio.PlayCharacter`·`PlayAt`으로 내면 이 감쇠가 그대로 적용된다.
- 두 대 이상(원격 술래) 검증은 S4 검증 때 함께.
