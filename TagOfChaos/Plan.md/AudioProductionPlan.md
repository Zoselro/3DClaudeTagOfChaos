# Audio Production Plan (S8 — 최종 음원 제작 계획)

- 작성: 2026-10-06 · 상태: **✅ 전체 완료(2026-10-06) — 110개 제작·게임 반영·검증** (P1 견본 후 사용자 요청으로 후보·청취 관문 없이 진행)
- 기준 문서: `Plan.md/SoundReplacementList.md`(110개 명세 + 사용자 Audio Direction), `Plan.md/SoundPlan.md`(S1~S7 구조)
- 이 문서는 음원 파일을 만들기 전의 설계다. 기술 조건(Sound ID·재생 시점·길이·Loop·2D/3D·거리·변형 수)은 명세서 그대로 유지한다.

---

## 0. 먼저 정해 둘 것 — 제작 수단과 한계

| 수단 | 쓰는 곳 | 비고 |
|---|---|---|
| **FluidSynth + FluidR3_GM 사운드폰트**(실제 악기 샘플) | BGM 전부, 스팅어·징글, 그리고 효과음 안의 "악기 레이어"(마림바·셀레스타·글로켄·오르골·하프·피치카토·칼리오페·아코디언·하프시코드·팀파니·합창·튜블러벨) | 로비 곡 A(확정)와 같은 방식. 악기 품질은 확인됨 |
| **numpy 합성 + 신호 처리**(scipy 추가 설치) | 질감 레이어 — 바삭(쿠키), 쿵(괴물), 증기·바람·불꽃·거품·휘익·찰박, 드론, 저주파 | 필터·엔벨로프·그레인·피치 변조로 만든다 |
| **후처리 공통** | 모든 음원 | 합성 잔향(Convolution, 맵마다 공간 크기 다른 IR을 코드로 생성), 역재생(Reverse Bell), 디튠, 음량 정규화(pyloudnorm 추가 설치), 루프 이음매 처리 |
| (선택) **CC0 효과음 팩** | 실감 질감이 꼭 필요할 때만(물 튐·종이·천) | 이 환경에서 내려받을 수 있는지·라이선스 확인 후. 기본 계획은 사용하지 않음 |

**한계(솔직하게)**
- **목소리 녹음이 없다.** 괴물 울음·호흡, 마녀 웃음은 "목소리 흉내 합성"(포먼트 필터 + 피치 흔들림 + 악기 레이어)으로 만든다. 사람 목소리처럼 들리게는 못 하고, **카툰적인 비현실 목소리**가 된다. 이 게임 방향(Cartoon Tension, 고어 금지)과는 오히려 맞는다고 본다.
- **제가 직접 들을 수 없다.** 길이·음량(LUFS)·피크·주파수 분포·루프 이음매는 수치로 검증하지만, 느낌의 최종 판단은 들어 보고 해 주셔야 한다. 그래서 단계마다 **청취 승인 관문**을 둔다(§10).

---

## 1. Audio Identity — "과자 상자 속의 작은 불안"

### 1.1 한 줄 정의
> **장난감처럼 귀여운 소리가 앞에 있고, 그 아래에 항상 "이상하게 낮은 무언가"가 깔려 있다.**

Cute Dark Fantasy / Magical Candy World / Playful Horror / Cartoon Tension / Cookie vs Monster를 소리로 옮기는 규칙 5개:

| # | 규칙 | 구체적으로 |
|---|---|---|
| R1 | **위는 귀엽게, 아래는 낮게** | 주요 효과음·곡은 고음역(마림바·셀레스타·작은 종)이 주인공. 긴장이 필요한 곳에만 60 Hz 이하 저음/드론을 **살짝** 섞는다(들리기보다 느껴지게) |
| R2 | **현실음 대신 "재료음"** | 금속은 실제 공장 금속이 아니라 "양철 장난감 + 종", 물은 "젤리/시럽 같은 찰박", 나무는 "과자 상자·나무 장난감". 고어·비명·실제 공포 효과음은 쓰지 않는다 |
| R3 | **세 팔레트는 섞이지 않는다** | 쿠키(작고 바삭·밝음) / 괴물(낮고 무겁고 젖음) / 마법(셀레스타·역재생 종·반짝). 소리만 듣고 "누구 소리인지" 알 수 있게 |
| R4 | **조용함이 기본, 큰 소리는 사건** | 평소: 환경음·작은 동작음. 사건(괴물 공개·합류·장치 완성·마녀): 크게. 음량 계층(§1.3)으로 강제 |
| R5 | **조성 통일** | 게임 전체 기본 조성 **C장조 / A단조**(로비 곡 A가 C장조). 맵 곡은 각자 조성을 갖되, 스팅어·징글·마법음은 C/Am 계열로 맞춰 어느 곡 위에 겹쳐도 크게 부딪히지 않게 |

### 1.2 모티프(짧은 선율 서명) 3개 — 게임 전체에 반복 사용
| 모티프 | 음 | 쓰는 곳 | 의미 |
|---|---|---|---|
| **Cookie 모티프** | C5–E5–G5–A5–G5 (톡톡 튀는 5음, 마림바) | 로비 곡 A에서 따옴(확정 곡과 연결), `JgEscapeSuccess`, `EscapeSuccessSelf`, `UiStart`, `Respawn` | "쿠키들의 세계·희망" |
| **Monster 모티프** | 낮은 반음 두 음 **B♭1 → A1**(느리게, 콘트라베이스·바순 + 저주파) | `StMonsterReveal`, `StMonsterArrive`, `MonsterWait`, `JgMonsterWin`, `MonsterDeparted` | "다가오는 이질적인 존재" — 추격 소리(확정)와 음역이 겹치지 않게 짧게만 |
| **Magic 서명** | 역재생 종(0.4초 빨려 듦) → 셀레스타 상승 4음 C6–E6–G6–B6(장7, 살짝 신비) | 가마솥·포탈·제단·마녀·색칠·장치 완성 | "이 세계의 마법" |

### 1.3 음량 계층(Dynamic Range) — R4를 숫자로
| 계층 | 해당 | 목표(음원 자체) | 게임 내 결과 |
|---|---|---|---|
| L0 바탕 | 맵 바탕 환경음, 동굴 | −30 ~ −28 LUFS 상당(매우 조용) | 들으려고 하면 들림 |
| L1 작은 동작 | 쿠키 발소리·UI 호버·붓질·등불 | 피크 −12 dBFS | 거슬리지 않음 |
| L2 일반 사건 | 줍기·끼우기·도구·문·상자 | 피크 −6 dBFS | 또렷 |
| L3 위협 정보 | 괴물 발소리·돌진·잡기·눌림 | 피크 −4 dBFS, **저역 풍부** | 멀리서도 존재감 |
| L4 큰 사건 | 스팅어·장치 완성·로켓·마녀·포탈·케이크 | 피크 −1 ~ −3 dBFS, 레이어링 | "크게 느껴지는 순간" |
| BGM | 반복 곡 | 약 −18 LUFS(통합) | 추격 시작 후엔 꺼짐(현재 규칙) |

최종 균형은 음원 레벨로 1차, `SoundCatalog`의 소리별 `volume`으로 2차 조정한다(코드 수정 없음).

---

## 2. Sound Palette — 재료 목록

모든 소리는 아래 "재료"를 조합해 만든다. 같은 재료를 여러 소리에서 재사용해 세계관의 통일감을 만든다.

| 재료 | 만드는 법 | 주로 쓰는 곳 |
|---|---|---|
| **Crunch**(쿠키 바삭) | 짧은 잡음 그레인 5~20개를 3~12 kHz 대역으로, 간격 무작위 | 쿠키 전부, 케이크, 과자 문 |
| **Soft Wood** | 나무 장난감 톡(감쇠 빠른 공명 2~3개, 400~900 Hz) | 쿠키 발소리, UI, 문, 시계 |
| **Pop** | 위로 휘는 사인 + 짧은 잡음 | 점프·줍기·뿅망치·물풍선 |
| **Marimba / Xylophone** | 사운드폰트 12/13번 | 쿠키 동작의 음정 레이어, UI |
| **Celesta / Glockenspiel / Music Box** | 사운드폰트 8/9/10번 | 마법, UI, 캔디숲·진저브레드·놀이공원 |
| **Reverse Bell** | 튜블러벨·글로켄을 렌더 후 역재생 | 마법 서명의 "빨려 듦" |
| **Sparkle** | 고음 종 그레인 10~40개 흩뿌림 | 마법·완성·포탈 |
| **Airy Whoosh** | 대역 잡음, 중심 주파수가 움직임 | 회피·휘두르기·포탈·마녀 |
| **Low Thud** | 40~80 Hz 사인 + 피치 하강 + 짧은 잡음 | 괴물 발소리·착지 큰 것·마녀 내리침 |
| **Sub Drone** | 30~55 Hz 지속음 + 느린 흔들림 | 위협·지하·마녀·MonsterWait |
| **Wet / Goo** | 저역 잡음에 빠른 공명 변조("찰박·질척") — 시럽·젤리 느낌 | 괴물 촉수·잡기·눌림, 가마솥, 물풍선 |
| **Tin Toy Metal** | 비조화 배음 2~4개(카툰 금속) + 짧은 링 | 공장·로켓·장치·안전바 |
| **Steam** | 고역 잡음 + 압력 엔벨로프 | 공장·오븐·기차 |
| **Fire Crackle** | 무작위 클릭 + 저역 숨결 | 베이커리 장작·로켓 불꽃 |
| **Paper / Fabric** | 넓은 대역 잡음, 아주 짧은 사각거림 | 깃발, UI 창 열림, 붓질 |
| **Clock** | Soft Wood 두 음(똑/딱) + 짧은 금속 링 | 진저브레드, 카운트다운 |
| **Choir Pad** | 사운드폰트 52/53번(Choir Aahs/Voice Oohs) | 마녀, 제단, 포탈 |
| **Calliope / Detuned Piano** | 사운드폰트 82번(Calliope Lead), 피아노 2대 ±12~20 cent 디튠 | 놀이공원 |

---

## 3. Map별 Audio Palette

각 맵을 "소리 나는 장소"로 정의한다. **바탕 환경음 = 맵 정체성**, **연출음 = 맵의 하이라이트**, **Paint BGM = 맵의 주제가**가 같은 재료를 공유한다.

| 맵 | 키워드 | 주 악기(BGM) | 환경 재료 | 깔리는 불안(R1) | 연출음 재료 | 공간(잔향) |
|---|---|---|---|---|---|---|
| **캔디숲** | 밝음·마법·상쾌·약간 신비 | 마림바, 플루트, 셀레스타, 하프, 작은 종 / C장조 104 BPM | 부드러운 바람 + 사탕 풍경(작은 종) + 나뭇잎(Paper 질감) + 멀리 마법 반짝 | 아주 약한 Sub Drone(−35 dB) 가끔 | Crunch(케이크) + Pop + Sparkle + 로켓 Steam | 넓은 야외, 짧은 잔향 0.6초 |
| **진저브레드 마을** | 포근·오래됨·마법·신비 | 아코디언, 글로켄, 오르골, 피치카토 / F장조 96 BPM, 시계 째깍 박자 | 지상: 바람 + 시계탑 똑딱 + 과자집 삐걱 / **지하: 저음 드론 + 동굴 반향 + 물방울 + 먼 울림 + 마법 공명** | 시계탑 종 아래 저음 | Choir + Reverse Bell + Sparkle(제단·포탈), 등불 지직 | 지상 0.8초 / **지하 2.5초 긴 잔향**(지상과 확실히 구분) |
| **초콜릿 공장** | 산업·초콜릿·기계·묵직 | 우드블록, 클라리넷, 바순, 팀파니 오스티나토 / D단조 112 BPM | Steam + Gears + Pipes + **초콜릿 끓는 거품(Wet, 낮고 걸쭉하게)** + Tin Toy Metal | 기계 저음 박동 | 기적·칙칙폭폭(Steam + Wood Block), 터널 울림 | 실내 큰 공간 1.2초 |
| **저주받은 놀이공원** | 귀여움·불안·고장·유령 | 오르골, 칼리오페, 디튠 피아노, 장난감 타악 / A단조 3/4 96 BPM | 바람 + 먼 오르골(음이 가끔 늘어짐) + 깃발 + 낡은 기계 삐걱 + **아주 희미한 아이 웃음 흉내(합성, 거의 안 들리게)** | 음정이 살짝 틀어진 화음 | 회전목마(대표 랜드마크) + 코스터: 전구 딸깍·모터·덜컹 | 야외 0.9초 + 오르골에만 테이프 늘어짐 효과 |
| **유령 베이커리** | 따뜻·포근·유령·마법 | 하프시코드, 셀레스타, 저음 현, 합창 패드 / E단조 88 BPM | 장작 타닥 + 오래된 오븐 웅웅 + 밀가루 쓸림(Paper) + 희미한 유령 바람 | 마법적인 저음(Sub Drone + Choir 아주 작게) | Oven Gears + Steam + Fire + 마법 문 굴림 + 빛 번쩍(Magic 서명) | 실내 따뜻한 1.0초 |

---

## 4. Monster Audio Palette

**정체성:** "귀여운 과자 세계에 끼어든 이질적인 것" — Low · Heavy · Wet · Magical · Organic · Cartoon

| 요소 | 재료 | 비고 |
|---|---|---|
| 몸무게 | Low Thud(40~70 Hz) + 지면 진동 꼬리 | 발소리·눌림 |
| 촉수 | Wet/Goo + 빠른 Airy Whoosh | 돌진·잡기 |
| 목소리 | 포먼트 합성 으르렁(80~180 Hz, 거칠게 흔들림) + 바순/콘트라베이스 레이어 | 잡기·공개 스팅어. 사람 목소리처럼 만들지 않음 |
| 마법 기운 | 불안정한 Reverse Bell + 디튠된 셀레스타 1음 | "마법적인 괴물" 표시. 쿠키 쪽 마법(맑은 셀레스타)과 대비 |
| 저주파 | Sub Drone 30~45 Hz | 존재감. **추격 소리(Chase, 확정)와 같은 음역은 짧게만** |

### 4.1 거리에 따른 밀도 — 기존 구조와 역할 나누기
"멀리 둔하게 → 가까이 밀도 증가"는 **이미 있는 세 장치가 나눠 맡는다**(새 코드 없음):

| 거리 | 담당 | 들리는 것 |
|---|---|---|
| 멀리(15~30 m) | `MonsterStep` 3D + 런타임 **거리 저역 통과(L3)** | 둔하고 낮은 쿵 + 짧은 잔향 꼬리만 |
| 중간(5~15 m) | 같은 `MonsterStep` — 필터가 열리며 | 발걸음 윤곽(중역 Crunch-Wood 아닌 "질척한 쿵")이 또렷 |
| 가까이(<5 m) | `MonsterStep` 고역 질감 레이어 + **Chase_Near(확정)** | 지면 진동 + 약한 몬스터 질감(Wet 미세 레이어) |
| 사건 | `MonsterDash`·`MonsterGrab`·`MonsterSquash` | 호흡·촉수·울음은 **사건 때만** — 상시로 넣지 않아 Chase와 충돌 방지 |

→ 그래서 `MonsterStep` 한 파일 안에 **저역(멀리서도 남음) + 중역 윤곽 + 고역 질감(가까이서만 들림)** 세 층을 넣는다. 거리 필터가 자연스럽게 고역부터 깎아 "멀리 = 둔함"이 된다.

---

## 5. Cookie Audio Palette

**정체성:** 작고 가볍고 바삭, 장난감처럼 귀엽다. **절대 크지 않게**(숨바꼭질 — D3 규칙 유지).

| 공통 재료 | Crunch(작게) + Soft Wood + Pop + Marimba(음정 레이어, 짧게) + Toy Percussion |
|---|---|
| 음정 규칙 | 쿠키 동작의 음정 레이어는 C장조 5음(C·D·E·G·A) 안에서만 — 무작위로 골라도 늘 어울림 |
| 크기 | 발소리 L1, 나머지 L1~L2. 고역 위주(2~8 kHz)라 거리 필터로 금방 사라짐 = 숨바꼭질에 유리 |
| 대비 | 같은 동작이라도 괴물 쪽은 낮은 음역(R3) |

---

## 6. Magic Audio Palette

**Magic 서명:** Reverse Bell(빨려 듦) → Celesta 상승 4음(C6–E6–G6–B6) + Sparkle + Airy Whoosh + 아주 약한 Subtle Synth 패드

| 변형 | 서명에서 바꾸는 것 | 해당 ID |
|---|---|---|
| 마녀 가마솥 | 서명 + Wet 거품 + 낮게(옥타브 아래) | `CauldronBubble`, `CauldronSplash` |
| 포탈 | 서명을 길게 늘이고 소용돌이 whoosh + Choir | `PortalOpen`, `PortalFlash`, `PortalClose` |
| 제단 | 서명 화음을 지속음으로(Choir + Glass 공명) | `AltarHum`, `AltarGlow` |
| 마녀 | 서명을 **단조·디튠**하고 Sub Drone + 큰 바람 | `WitchAppear`, `WitchSlam`, `StWitchAppear` |
| 색칠 | 서명의 Sparkle만 아주 작게 + Paper/붓 질감 | `PaintSlotRegistered`, `PaintForceFill` |
| 장치(Magic Device) | 서명 + Tin Toy Metal 딸깍 | `DeviceInsert`, `DeviceComplete`, `RocketInsert` |
| 순간이동·복귀 | 서명 축약형(역재생 종 0.2초 + 2음) | `Respawn`, `BoardSuck`, `OvenFlash` |

> 명세서의 "Clone"(분신)·"MonsterAttack"·"MonsterAppear"는 현재 Sound ID가 없다. Attack은 `MonsterDash`·`MonsterGrab`, Appear는 `StMonsterReveal`·`StMonsterArrive`로 대응한다. 분신 소리가 필요하면 ID 추가 + 코드 연결이 따로 필요하다.

---

## 7. Sound ID별 제작 방향 (110개)

표기: **[L]** = 레이어링 대상(§8), **[S]** = 단순하게 유지(§9). 길이·Loop·2D/3D·거리는 명세서와 같다(괄호는 목표 길이).

### 7.1 BGM — 반복 곡 7
| ID | 왜 필요한가 | 제작 방향 | 구조·길이 |
|---|---|---|---|
| `GameLobby` | 대기실 = 마녀 집 앞. 귀여움과 불안이 처음 만나는 곳 | 3/4 왈츠 F장조 92 BPM. 셀레스타 선율 + 바순 베이스 + 피치카토. B 구간에서 단조로 살짝 틀어지고 **Monster 모티프의 그림자**(저음 반음)가 한 번 지나감. 가마솥 끓는 소리와 겹쳐도 비지 않게 중역을 비움 | A(16) B(16) A'(16) = 48마디 ≈ 94초 |
| `MonsterWait` | 괴물 본인이 "곧 사냥" 기대감을 쌓는 곡 | D단조 80 BPM. 저음 현 트레몰로 + 팀파니 + Sub Drone, Monster 모티프 반복, 점점 쌓이지만 루프 경계에서 다시 조용해지지 않게 "계속 차오르는" 착시(셰퍼드 톤 응용) | A B A' 24마디 ≈ 72초 |
| `CandyForestPaint` | 쿠키가 숨을 곳을 칠하는 평화로운 시간 | C장조 104 BPM 마림바 리드 + 플루트 대선율 + 하프 아르페지오 + 셀레스타 반짝. B 구간에 아주 약한 저음 지속(멀리서 무언가) | A B A' 40마디 ≈ 92초 |
| `GingerbreadPaint` | 따뜻하고 오래된 동화 | F장조 96 BPM 아코디언 + 글로켄 + 오르골, **시계 째깍을 박자에 섞음**(Clock 재료) | A B A' 40마디 ≈ 100초 |
| `FactoryPaint` | 기계적이고 리듬감 | D단조 112 BPM 우드블록·양철 타악 오스티나토 + 클라리넷 선율 + 바순, 증기 퍼프가 박자 | A B A' 48마디 ≈ 103초 |
| `CarnivalPaint` | 귀엽지만 불안 | A단조 3/4 96 BPM 오르골 + 칼리오페, 디튠 피아노 화음, 가끔 템포가 늘어지는 "고장" 순간(테이프 감속 효과 1회) | A B A' 48마디 ≈ 90초 |
| `BakeryPaint` | 따뜻하지만 유령 같음 | E단조 88 BPM 하프시코드 + 셀레스타 + 저음 현, 합창 패드가 B 구간에만 | A B A' 40마디 ≈ 109초 |

### 7.2 BGM — 스팅어 5 / 징글 3
| ID | 제작 방향 | 길이 |
|---|---|---|
| `StMonsterReveal` [L] | Monster 모티프 금관(트롬본·튜바) + 심벌 롤 역재생 + Sub Drone + 마녀 서명 단조 1음 | 2초 |
| `StMonsterArrive` [L] | 오르간 단화음 + 큰 북 + Low Impact + 괴물 울음 짧게 + Reverse Whoosh 도입 | 3초 |
| `StDeviceComplete` [L] | Magic 서명 + Cookie 모티프 첫 3음 상승 + 종 화음(희망) | 2초 |
| `StSpyLaunch` | 상승 현 트레몰로 + 스네어 롤, 끝은 해결 안 됨(긴장 유지) | 3초 |
| `StWitchAppear` [L] | 낮은 합창(단조 + 3온음) + 큰 바람 + Sub Drone + 디튠 셀레스타 | 3초 |
| `JgEscapeSuccess` | Cookie 모티프 마림바 + 셀레스타 + 작은 종 팡파르, C장조 해결 | 5초 |
| `JgEscapeFail` | Cookie 모티프를 단조로 느리게, 마지막에 귀여운 "뿅" 하강 | 5초 |
| `JgMonsterWin` | Monster 모티프 금관 + 낮은 웃음 흉내(바순 스타카토) + 심벌 | 5초 |

### 7.3 UI 15 — 모두 [S] (단순·일관)
공통: 과자 상자·나무 장난감 질감(Soft Wood) + 마림바/셀레스타 1~2음. C장조 안에서만. 짧고 깨끗하게.

| ID | 방향 | 길이 |
|---|---|---|
| `UiHover` | 아주 작은 나무 톡(고음, 음정 없음) | 0.04초 |
| `UiClick` | 과자 상자 뚜껑 톡 + 마림바 G5 짧게 | 0.08초 |
| `UiConfirm` | 마림바 C5→G5 상승 2음 + 아주 작은 Sparkle | 0.3초 |
| `UiCancel` | 마림바 G5→C5 하강 2음, 부드럽게 | 0.3초 |
| `UiStart` | Cookie 모티프 5음 빠르게(마림바+셀레스타) | 0.8초 |
| `UiError` | 나무 블록 2번 "또각또각" + 낮은 마림바(불협 아님, 귀엽게 거절) | 0.25초 |
| `UiOpen` | 종이/상자 열림(Paper 0.1초) + 셀레스타 1음 상승 | 0.15초 |
| `UiClose` | `UiOpen` 역방향(하강) | 0.15초 |
| `UiSaved` | 셀레스타 3음 + Sparkle(작은 마법 확인) | 0.6초 |
| `UiTick` | Clock 재료의 "똑"(나무) | 0.06초 |
| `UiTickFinal` | Clock "딱" + 글로켄 1음(긴박) | 0.25초 |
| `UiSlotSelect` | 나무 장난감 톡, `UiClick`보다 한 단계 낮음 | 0.08초 |
| `UiToastInfo` | 글로켄 2음 부드러운 알림 | 0.5초 |
| `UiToastAlert` | 글로켄 2음 단조 + 아주 약한 Low Thud(경고지만 귀엽게) | 0.7초 |
| `UiChat` | 작은 Pop + 셀레스타 1음 | 0.08초 |

### 7.4 색칠 6
| ID | 방향 | 분류 |
|---|---|---|
| `PaintPickColor` | 물감 톡(젖은 Pop 아주 짧게) + 셀레스타 1음(색마다 음정 바꾸지 않음 — 단순) | [S] |
| `PaintErase` | 고무 지우개 쓱(Paper) 두 번 | [S] |
| `PaintReset` | 붓으로 싹 쓸기(Paper whoosh) + 마법 서명 축약 하강 | [S] |
| `PaintStroke` (Loop) | 부드러운 붓 쓱쓱(Fabric/Paper 질감), 4~5 Hz 리듬, **매우 작게(L1)**, 1.5초 이음매 없음 | [S] |
| `PaintSlotRegistered` | 마법 서명 Sparkle + 마림바 2음 "찰칵 저장" | [S] |
| `PaintForceFill` | 물감이 확 덮임(Wet 찰박 + Airy Whoosh) + 마법 서명 단조 하강(강제·약간 불길) | [S] |

### 7.5 캐릭터 16
| ID | 방향 | 분류 |
|---|---|---|
| `CookieStep` ×**4**(명세 3개 이상) | Crunch(작게) + Soft Wood 톡 — 4개 모두 음색·높이를 조금씩 달리해 반복감 제거 | [S] |
| `CookieJump` | Pop 상승 + 마림바 1음(C장조 무작위 → 카탈로그 피치 범위로 변화) | [S] |
| `CookieLand` | Soft Wood 톡 + 작은 Crunch | [S] |
| `CookieDodge` | Airy Whoosh(짧게) + 천 펄럭(Fabric) | [S] |
| `CookieGrab` | Pop 2음 상승 "영차" + 마림바 | [S] |
| `CookieRelease` | Pop 2음 하강 + Soft Wood 톡 | [S] |
| `CookieCrumble` | 과자 부서짐(Crunch 그레인 30~50개, 흩어지며 작아짐) + 마림바 단조 하강 3음 + 가루 Sparkle(슬프지만 고어 없이) | [L] |
| `MonsterStep` ×**3**(명세 2개 이상) | §4.1 세 층: Low Thud(45~60 Hz) + 질척한 중역 "철퍽" + 고역 Wet 미세 질감. 3개가 무게·질감을 조금씩 달리함 | [L] |
| `MonsterDash` | 촉수 Whoosh(빠르고 젖음) + Low Thud 끝 + 짧은 숨 | [L] |
| `MonsterGrab` | 촉수 감김(Wet) + 괴물 으르렁(포먼트 합성) + 불안정 Reverse Bell | [L] |
| `MonsterSquash` | 큰 Low Thud + 과자 찌그러짐(Crunch 짧게) + 젤리 찰박 — 잔인하지 않게 카툰 "꾹" | [L] |
| `MonsterAimLock` | 디튠 셀레스타 2음(괴물의 마법 — 쿠키 쪽 맑은 셀레스타와 대비) | [S] |
| `StunStars` | 글로켄·작은 종이 원을 그리듯 돌아감(스테레오 아님 — 음높이 순환) | [S] |
| `Respawn` | Magic 서명 축약 + Cookie 모티프 마지막 2음 | [S] |
| `DoorOpen` | 과자집 나무 문 삐걱(Soft Wood 공명 + 피치 흔들림) + 딸깍 | [S] |
| `DoorClose` | 나무 문 톡 닫힘 + 걸쇠 | [S] |

### 7.6 대기실 3
| ID | 방향 | 분류 |
|---|---|---|
| `CauldronBubble` (Loop) | Wet 거품 톡톡(낮고 걸쭉) + 아주 작은 Magic 반짝이 가끔 + 바닥 Sub(−30 dB), 8초 이음매 없음 | [L] |
| `CauldronSplash` | 큰 시럽 풍덩(Wet) + Magic 서명(옥타브 아래) | [L] |
| `MonsterDeparted` | Monster 모티프 1회(바순·첼로) + 문 닫히는 저음 | [S] |

### 7.7 탈출 모드 16
| ID | 방향 | 분류 |
|---|---|---|
| `ChestOpen` | 과자 상자 뚜껑(Soft Wood) + 작은 Sparkle 3음 | [S] |
| `ChestRespawn` | Reverse Bell 짧게 + 뚜껑 닫힘 | [S] |
| `ItemPickup` | Pop + 마림바 상승 2음 | [S] |
| `ItemDrop` | Soft Wood 툭 + 작은 Crunch | [S] |
| `DeviceInsert` | Tin Toy Metal 철컥 + 셀레스타 1음(끼우기·빼기 **같은 소리** — D3) | [S] |
| `DeviceComplete` | Magic 서명 전체 + 장치 기동(Tin Toy 회전 상승) + 종 화음 | [L] |
| `BoardHop` | Pop 크게 + 마림바 3음 상승 | [S] |
| `BoardSuck` | Magic 서명 축약 + 빨려 드는 whoosh(상승) | [S] |
| `RocketInsert` | Tin Toy Metal 철컥 + 작은 스프링 | [S] |
| `RocketHatch` | 금속 해치 회전(Tin Toy) + 공기 빠짐 칙 | [S] |
| `RocketIgnite` (≈2초) | Fire Crackle 상승 + Steam 압력 + 저음 웅웅 상승 | [L] |
| `RocketLiftoff` (≈4.5초) | 큰 분사(Steam+Noise) + 저역 굉음 + 상승 휘파람(멀어지며 작아지는 모양을 파일 자체에) | [L] |
| `WitchAppear` (≈5초, 2D) | 큰 바람(서서히 커짐) + Choir 단조 + Sub Drone + **마녀 웃음 흉내**(바순·오보에 스타카토 + 포먼트) | [L] |
| `WitchSlam` (2D) | 거대 Low Impact(30 Hz) + 과자 세계가 흔들리는 Crunch 흩뿌림 + 종이 깨지는 듯한 Glass 링 + 잔향 | [L] |
| `HeartBeat` | 두근(저역 2타) — 귀여움 없이 단순 | [S] |
| `EscapeSuccessSelf` | Cookie 모티프 + Sparkle(성공) | [S] |

### 7.8 도구 7
| ID | 방향 | 분류 |
|---|---|---|
| `StunAimHum` (Loop) | 장난감 전기 윙(양철 공명 + 약한 떨림), 작게, 1.5초 이음매 없음 | [S] |
| `StunFire` | 장난감 레이저 "뿌잉" + 짧은 지직 | [S] |
| `StunHit` | 지지직(Crackle) + 별 반짝 1음 | [S] |
| `BalloonThrow` | 휙(작게) + 고무 늘어남 | [S] |
| `BalloonSplash` | 고무 팡 + 젤리/물 찰박(Wet 밝게) — 공간 정보가 중요해 중고역 또렷 | [S] |
| `HammerSwing` | 휙(짧고 가볍게) | [S] |
| `HammerBonk` | 장난감 뿅망치 "뿅!"(고무 Pop + 음정 하강) — 이 게임 최고의 코믹 포인트 | [S] |

### 7.9 맵 연출 25 (길이는 연출과 맞춤 — 명세 그대로)
| ID | 방향 | 분류 |
|---|---|---|
| `CakeRumble` (2초) | 땅울림 Sub + 케이크 크림 떨림(저역 흔들림) | [S] |
| `CakeCrack` (0.8초) | 크래커 금 가는 Crunch + 설탕 유리 Glass 링 | [S] |
| `CakeBurst` (1~1.6초) | Low Impact + 케이크 조각 Crunch 폭발 + 크림 Wet + Sparkle | [L] |
| `CakeRocketRise` (3초) | Tin Toy 기계 상승 + 하프 글리산도 + 저음 상승 | [L] |
| `CakeIgnite` (2초) | 사탕 로켓 점화: Fire Crackle + 휘파람(사탕 피리) 상승 | [L] |
| `CakeLaunch` (5초) | 분사 + 저역 굉음 + Sparkle 꼬리 + Cookie 모티프 마지막 음(희망) | [L] |
| `CoasterBulbOn` (2.5초) | 낡은 전구 딸깍 12번 + 지직(일부 전구는 늦게 켜짐 — 고장 느낌) | [S] |
| `CoasterStartup` (3초) | 낡은 모터 시동(톱니 + 붕붕 + 디튠 칼리오페 1음) | [S] |
| `CoasterSparks` (2.7초) | 전기 불꽃 Crackle(작게) | [S] |
| `CoasterLapBar` (0.4~0.8초) | Tin Toy 철컥 두 번 | [S] |
| `CoasterDepart` (6초) | 덜컹덜컹 가속(레일 이음매 박자) + 낡은 칼리오페 음이 멀어짐 | [L] |
| `OvenGears` (Loop, 3.6~8초) | 톱니 딸각 + 피스톤 쿵 + Steam 칙 + 아래 마법 저음 — 박자 1.1 Hz(연출과 맞춤) | [S] |
| `OvenPiston` (≈1초) | Steam 큰 칙 + 첫 쿵 | [S] |
| `OvenDoorRoll` (3초) | 큰 둥근 문이 구름(무거운 Wood + 돌 굴림) + 끝 쿵 | [S] |
| `OvenFlash` (≈1.25초) | Magic 서명 + 따뜻한 빛 whoosh(오븐 열기) | [L] |
| `TrainPuff` (3초) | Steam 퍼프가 빨라짐 | [S] |
| `TrainWhistle` (1~1.5초) | 증기 기적 "뿌우"(3화음, 약간 귀엽게 높음) | [S] |
| `TrainChug` (6.5초) | 칙칙폭폭 가속 + 우드블록 바퀴 박자 + 초콜릿 탱크 출렁(Wet) | [S] |
| `TrainTunnel` (2~3초) | 터널 진입 저역 whoosh + 긴 잔향 | [S] |
| `AltarHum` (Loop, 4~8초) | 제단 공명: Choir 지속 + Glass 공명 + 아주 약한 Sub — 정수 주기로 이음매 없음 | [S] |
| `AltarGlow` (2초) | Magic 서명 지속형 상승 + 무지개 Sparkle | [L] |
| `PortalOpen` (2초) | 소용돌이 Whoosh + Reverse Bell + Choir 상승 | [L] |
| `PortalFlash` (≈1.2초) | Low Impact + Sparkle 폭발 + 종 화음 | [L] |
| `PortalClose` (≈1.2초) | 소용돌이 하강 + 마지막 "뿅"(Pop) | [S] |
| `LanternFlicker` | 낡은 등불 지직(작게, L1) | [S] |

### 7.10 환경음 7 (모두 Loop, L0 — 아주 조용)
| ID | 방향 | 길이 |
|---|---|---|
| `AmbCandyForest` | 부드러운 바람 + 사탕 풍경(작은 종, 드물게) + 나뭇잎 사각 + 멀리 마법 반짝 + 가끔 아주 약한 Sub(신비) | 60초 |
| `AmbGingerbread` | 바람 + 멀리 시계탑 똑딱(박자 정확히 1초) + 과자집 삐걱 + 가끔 먼 시계 종 1타 | 60초 |
| `AmbFactory` (3D 28 m, 4곳) | 모터 웅웅 + 톱니 + 금속 쿵(카툰) + 증기 + **초콜릿 끓는 걸쭉한 거품** | 16초 |
| `AmbCarnival` | 바람 + 먼 오르골(음이 가끔 늘어짐) + 깃발 + 낡은 기계 삐걱 + 아주 희미한 웃음 흉내(거의 안 들림) | 60초 |
| `AmbBakery` | 장작 타닥 + 오븐 웅웅 + 밀가루 쓸림 + 유령 바람(낮은 "우우", 드물게) | 60초 |
| `AmbCave` (2D, 지하) | 깊은 Sub Drone + 동굴 반향 바람 + 물방울(긴 잔향) + 먼 울림 + 마법 공명(제단과 같은 화음을 아주 작게 — 유적과 연결) | 60초 |
| `CarouselSpin` (3D 40 m) | **놀이공원 대표 랜드마크.** 칼리오페 + 오르골 왈츠(A단조, CarnivalPaint와 같은 주제), 디튠 피아노, 회전 기계 덜컹 + 가끔 테이프 늘어짐. 가까이 갈수록 "고장 난 동화" 느낌이 또렷 | 30~40초 |

---

## 8. 레이어링하는 소리 — [L] 25개 (효과음 21 + 스팅어 4)

명세 §8의 13개 이벤트 + 같은 무게의 이벤트. 모두 **3~5 레이어, 최종 출력은 단순하게**(레이어마다 맡는 주파수 대역을 나눠 탁해지지 않게).

| 이벤트 | ID | 레이어(아래 → 위 대역) |
|---|---|---|
| Monster Reveal | `StMonsterReveal` | Sub Drone · Low Impact · Monster 모티프 금관 · 역재생 심벌 · 디튠 셀레스타 1음 |
| Monster Arrive | `StMonsterArrive` | Low Impact · 북 · 오르간 단화음 · 괴물 울음 짧게 · Reverse Whoosh |
| Monster Attack | `MonsterDash` | Low Thud · 촉수 Wet · Whoosh · 숨 |
| Monster Grab | `MonsterGrab` | Sub · 으르렁 · 촉수 감김 · 불안정 Reverse Bell |
| Monster Squash | `MonsterSquash` | Low Impact · 젤리 찰박 · 과자 Crunch |
| 괴물 발소리 | `MonsterStep` | Low Thud · 질척 중역 · 고역 질감(거리 필터용) |
| 쿠키 부서짐 | `CookieCrumble` | Crunch 흩뿌림 · 마림바 하강 · 가루 Sparkle |
| 가마솥 | `CauldronBubble`, `CauldronSplash` | Sub · Wet 거품 · Magic 반짝 |
| Cake Burst | `CakeBurst` | Low Impact · Crunch 폭발 · 크림 Wet · Sparkle |
| Cake Rocket | `CakeRocketRise`, `CakeIgnite`, `CakeLaunch` | 저음 상승 · 기계/불꽃 · 휘파람·하프 · Sparkle |
| Coaster 출발 | `CoasterDepart` | 레일 덜컹 · 저역 굴림 · 칼리오페 멀어짐 |
| Oven 번쩍 | `OvenFlash` | 열기 whoosh · Magic 서명 · Sparkle |
| Altar Glow | `AltarGlow` | Choir · Magic 서명 · Sparkle |
| Portal Open | `PortalOpen` | Sub · 소용돌이 · Reverse Bell · Choir |
| Portal Flash | `PortalFlash` | Low Impact · 종 화음 · Sparkle 폭발 |
| Rocket Ignite/Liftoff | `RocketIgnite`, `RocketLiftoff` | 저역 굉음 · 분사 잡음 · Fire Crackle · 상승 휘파람 |
| Witch Appear | `WitchAppear`, `StWitchAppear` | Sub Drone · 바람 · Choir 단조 · 웃음 흉내 · 디튠 셀레스타 |
| Witch Slam | `WitchSlam` | 30 Hz Impact · Crunch 흩뿌림 · Glass 링 · 긴 잔향 |
| Device Complete | `DeviceComplete`, `StDeviceComplete` | 기계 기동 · Magic 서명 · 종 화음 · Cookie 모티프 |

---

## 9. 단순하게 유지하는 소리 — [S] 67개

- **UI 15·색칠 6·도구 7 전부**, 쿠키 동작(발소리·점프·착지·회피·들기/내려놓기), 상자·아이템·끼우기·해치, 문, 시계성 소리, 등불, 반복 기계음(`OvenGears`, `AltarHum`).
- 이유: **자주 들리거나(피로도)**, **정보 전달이 목적이거나(무엇이 일어났는지 즉시)**, **겹쳐 나는 소리**(여러 명이 동시에)라서 단순해야 믹스가 깨끗하다.
- 규칙: 1~2 레이어, 0.3초 이하(UI·동작), 음정 레이어는 C장조 5음 안.

- 환경음 7개는 여러 요소를 섞지만 **아주 조용한 긴 반복**이라 레이어링·단순 어느 쪽도 아닌 별도 분류(§7.10)로 둔다.

| 분류 | 개수 |
|---|---|
| [L] 레이어링 | 25 (효과음 21 + 스팅어 `StMonsterReveal`·`StMonsterArrive`·`StDeviceComplete`·`StWitchAppear`) |
| [S] 단순 | 67 (UI 15 포함) |
| 환경음(조용한 긴 반복) | 7 |
| 작곡(악곡) | 11 (반복 곡 7 + `StSpyLaunch` + 징글 3) |
| **합계** | **110** |

---

## 10. 제작 우선순위와 단계 (명세 §12 순서 + 청취 승인 관문)

| 단계 | 내용 | 결과물 | 관문(사용자 청취) | 예상 시간 |
|---|---|---|---|---|
| **P1 정체성 데모** ✅ | 팔레트 재료 견본: Cookie·Monster·Magic·Environment 각 4~6개 짧은 소리 + 맵 5곳 "10초 분위기 스케치" + 모티프 3개 | `Source~/demo/` 견본 30여 개 + 듣기 목록 | **★ 방향 승인** — 여기서 톤이 맞지 않으면 전부 다시. 가장 중요 | 2~3시간 |
| **P2 핵심 사운드** ✅ | `MonsterStep`×3, `MonsterDash`, `MonsterGrab`, `MonsterSquash`, `CookieStep`×4, `CookieJump`, `CookieLand`, `StMonsterReveal`, `StMonsterArrive` (+ Chase와 겹쳐 듣기 확인) | 13개 + 각 2후보 | ★ 후보 선택 | 2시간 |
| **P3 맵 환경음** ✅ | 환경음 7(`AmbCave`, `CarouselSpin` 포함) | 7개 | 맵에서 걸어 보며 확인 | 2시간 |
| **P4 맵 연출음** ✅ | 케이크·코스터·오븐·기차·포탈·제단·마녀·로켓·장치 완성 (34개) | 34개 | 맵별 연출 재생 확인(S6 시험 스크립트) | 3시간 |
| **P5 BGM** ✅ | Paint 5곡 + `GameLobby` + `MonsterWait` + 스팅어 나머지 3 + 징글 3 | 13개, 반복 곡은 각 2후보 | ★ 곡별 선택(로비 곡 A 때처럼) | 곡당 40분 ≈ 5시간 |
| **P6 UI·기타** ✅ | UI 15, 색칠 6, 도구 7, 탈출 나머지, 대기실, 캐릭터 나머지 | 약 43개 | 묶음별 확인 | 2시간 |
| **마무리** ✅ | 전체 음량 균형(카탈로그), 맵별 배경음·환경음 비율, 8인 동시 재생 점검 → S9로 | — | 한 판 플레이 | 1시간 |

**검증(각 단계 공통, 수치)**: 길이가 명세 범위 안 · 피크/LUFS가 §1.3 계층 안 · Loop 이음매(끝↔처음 차이가 보통 샘플 간격 이내, 리듬 주기 정수배) · 3D는 모노 · 파일 이름 = ID(변형 `_n`) · `AudioTests` 통과 · Play Mode 재생 기록.

**원본 보관**: 모든 생성 스크립트·MIDI·IR 생성 코드는 `Assets/10. Audio/Source~/final/`에 둔다(나중에 값만 바꿔 다시 뽑을 수 있게). 시험음 스크립트(`synth_*.py`)는 그대로 남긴다.

---

## 11. 검토 요청 사항 — ✅ 결정(2026-10-06): 모두 추천안
- Q1 모티프 3개 사용 · Q2 괴물·마녀 목소리는 카툰 합성 · Q3 변형 `CookieStep` 4개·`MonsterStep` 3개 · Q4 놀이공원 웃음 흉내는 거의 안 들리게 넣음 · Q5 CC0 팩은 P1 청취 후 결정 · Q6 견본은 개별 파일 + 묶음 둘 다
- 추가 요청(2026-10-06): **원본 파일은 따로 폴더에 저장** → 제작한 개별 원본·제작 스크립트는 `Assets/10. Audio/Source~/final/`(Unity가 게임에 넣지 않는 폴더)에 단계별로 보관.
  교체할 때는 기존 시험음을 `Assets/10. Audio/Source~/placeholder_backup/`에 먼저 옮겨 둔다.

### 원래 질문


| # | 질문 | 추천 |
|---|---|---|
| Q1 | 모티프 3개(Cookie·Monster·Magic)를 게임 전체에 반복 사용하는 방향이 괜찮은가 | 사용 — 세계관 통일에 가장 효과적 |
| Q2 | 괴물·마녀 "목소리"를 합성 흉내(카툰, 비현실)로 가는 것 | 합성 — 녹음 소스가 없고, 고어 금지 방향과 맞음 |
| Q3 | 변형 수를 늘림: `CookieStep` 3→4, `MonsterStep` 2→3 | 늘림(반복감 감소, 코드 변경 없음) |
| Q4 | 놀이공원 "희미한 웃음 흉내"를 넣을지 | 넣되 거의 안 들리게(빼도 됨) |
| Q5 | CC0 효과음 팩을 보조로 쓸지 | 기본은 안 씀. P1 데모 후 부족한 질감이 있으면 그때 결정 |
| Q6 | P1 데모 형태: 소리마다 개별 파일 + "맵별 10초 스케치" 묶음 | 둘 다 |

---

## 12. 진행 기록

### P1 정체성 견본 ✅ 제작 완료(2026-10-06)
- 제작 도구 `Source~/final/audiolib.py`(FluidSynth 악기 렌더, 질감 재료 15종, 합성 잔향, 역재생·디튠·테이프 흔들림, 길이 자르기, 음량·주파수 측정), 견본 스크립트 `p1_identity.py`.
- 견본 37개(개별 원본 `Source~/final/P1/<묶음>/`) + 듣기용 묶음 6개 + 듣기 안내(`P1/listening_guide.md`):
  1. 모티프 3(쿠키 5음 · 괴물 B♭→A · 마법 서명)
  2. Cookie 10(발소리 4종·점프·착지·회피·들기·내려놓기·부서짐)
  3. Monster 8(발소리 3종 + 멀리서 들리는 모습 흉내·돌진·잡기·눌림·숨결)
  4. Magic 5(서명·가마솥·포탈·마녀·색칠 변형)
  5. Environment 6(캔디숲·진저브레드 시계·공장 증기/초콜릿·놀이공원 오르골·베이커리 장작/유령·지하 동굴)
  6. 맵 5곳 10초 스케치(각 맵 악기·조성·템포 + 환경 재료)
- 수치 확인: 길이를 명세 범위로 자름(쿠키 점프 0.30초·발소리 0.07초, 괴물 발소리 0.55초 등). 주파수 중심 — 쿠키 발소리 약 7.4 kHz / 괴물 발소리 약 290 Hz → 두 팔레트가 음역으로 확실히 갈림(R3).
- 아직 게임에는 넣지 않았다(방향 확인용). 승인되면 P2(핵심 사운드)부터 실제 교체 파일을 만든다.

### P2~P6 + 마무리 ✅ 완료(2026-10-06) — 사용자 요청: "일단 전체적으로 만들어주고, 인게임에 넣어줘"
- 후보 2개·단계별 청취 관문은 생략하고 §7 방향대로 110개를 한 번에 만들었다(배경음 15 + 효과음 95, 파일 115).
- 스크립트: `Source~/final/scripts/final_music.py`(곡 생성기 — 화음 진행 위 규칙 선율, 반복 악구 재사용, A-B-A', 세 바퀴 렌더 후 가운데), `final_sfx.py`(팔레트 재료 조합), `audiolib.py`.
- 게임 반영: 같은 이름으로 교체, 기존 시험음은 `Source~/placeholder_backup/`, 최종 원본은 `Source~/final/S8/`. 코드 변경 없음.
- 수치: 반복 곡 −18 LUFS(72~109초), 스팅어 −15·징글 −16 LUFS, 효과음 피크는 §1.3 계층(L1 −12 · L2 −6~−9 · L3 −4 · L4 −1~−3), 환경음 −24~−30 LUFS. 쿠키/괴물 발소리 주파수 중심 약 7.4 kHz / 290 Hz.
- 검증: SoundPlan.md S8 참고(테스트 전부 통과, 맵 5개·대기실·탈출 흐름 Play Mode 재생 기록, 콘솔 0).
- 듣기용 요약: `Plan.md/AudioS8ListeningGuide.md`(묶음 5개 — 대표 소리·배경음 앞 20초·환경음).
- 다음: 직접 들어 보시고 바꿀 소리를 알려 주시면 해당 스크립트 값만 고쳐 다시 뽑는다 → S9(음량 설정 창·전체 믹스·빌드 확인).
