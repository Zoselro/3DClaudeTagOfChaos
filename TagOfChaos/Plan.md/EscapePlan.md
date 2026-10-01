# 탈출 모드 계획서 (EscapePlan.md)

- 작성일: 2026-09-30
- 상태: **결정 완료, 최종 승인 대기**.
  - D1~D40 결정은 모두 반영했다.
  - 프로젝트 규칙(Claude.md)에 따라 최종 승인을 받은 뒤 작업을 시작한다.
- 근거:
  - 지금 코드를 직접 읽고 확인했다.
    - `GamePhaseState`, `RoomState`, `NetKeys`, `NetEventCodes`, `GameSettingsSO`
    - `GameRuleController`, `MonsterAssignmentAuthority`, `MonsterJoinController`
    - `ResultScreenController`, `PlayerResultRow`
    - `IInteractable`, `CharacterInteractor`, `InteractableDoor`
    - `PlayerCarryFollower`, `MonsterGrabKillTrigger`, `HideOrSeekPlayer`
    - `MapCompactor`
  - `research.md`, `MapReplacePlan.md`의 결정 사항을 따른다.
- 표시: ⬜ 대기 · 🔄 진행 중 · ✅ 완료

---

## 0. 한눈에 보기

### 지금 게임
대기실(가마솥으로 괴물 자원) → 색칠(60초) → 괴물 합류 → 술래잡기(600초) → 결과

- 쿠키 전원이 파괴되면 괴물 승리, 시간이 끝나면 쿠키 승리다.
- 판정 위치: `GameRuleController.cs`

### 바뀌는 게임
대기실 → 색칠 → 술래잡기 + **탈출 준비** → (스파이 탈출 시) **마녀 타임어택** → 결과

- 쿠키:
  - 상자를 뒤져 탈출 재료를 찾는다.
  - 재료를 양손으로 들고 탈출 장치에 끼운다.
  - 재료가 다 차면 한 명씩 탈출한다.
- 스파이:
  - 쿠키인 척하면서 탈출 재료를 훔쳐 자기 로켓에 끼운다. 쿠키가 이미 탈출 장치에 끼운 재료도 빼 갈 수 있다.
  - 스파이 상자에서 얻은 방해 도구(스턴건, 물풍선)로 쿠키를 방해한다.
  - 로켓을 완성해서 타고 탈출한다. 그러면 마녀 타임어택이 시작된다.
- 쿠키는 일반 상자에서 뿅망치를 얻어 스파이와 괴물에게 맞설 수 있다.
- 괴물:
  - 지금처럼 쿠키를 잡는다.
- 결과 화면은 한 판의 승패 대신, **플레이어마다 결과를 한 줄씩** 보여준다.

---

## 1. 확정된 규칙

### 1.1 인원 구성 (최대 8명)

| 총 플레이어 | 쿠키 | 괴물 | 스파이 | 필요 재료 |
|---|---|---|---|---|
| 4 | 3 | 1 | 0 | 3 |
| 5 | 3 | 1 | 1 | 3 |
| 6 | 4 | 1 | 1 | 4 |
| 7 | 4 | 2 | 1 | 4 |
| 8 | 5 | 2 | 1 | 5 |

- 쿠키 수에는 스파이가 들어가지 않는다. 총 인원 = 쿠키 + 괴물 + 스파이다.
- 필요 재료는 쿠키 1명당 1개다. 스파이는 재료 수에 들어가지 않는다.
- 스파이는 괴물이 아닌 사람 중에서 무작위로 정한다.

- 방 정원은 방을 만들 때 4~8명 중에서 고른다. 정원이 차면 시작한다(D2). 설정 방법은 §1.7을 따른다.
- 색칠(변장) 단계는 그대로 유지한다(D9).

### 1.2 게임 끝

- **제한시간:** 방장이 방을 만들 때 10~40분 중에서 정한다. 기본값은 10분이다(§1.7).
  - 10분이 지나면 아직 탈출하지 못한 쿠키와 스파이는 모두 **탈출 실패**다. 괴물은 잡은 횟수만 표시한다(D8).
  - 지금의 "시간이 끝나면 쿠키 승리" 규칙은 없앤다.
- **괴물 승리:** 괴물이 남은 쿠키를 모두 잡으면 끝난다.
- **쿠키가 먼저 모두 끝난 경우:** 스파이가 탈출하기 전에 쿠키가 모두 탈출하거나 잡혔다면, 스파이가 로켓을 타는 즉시 탈출 성공이고 게임이 끝난다. 이때는 마녀 타임어택을 건너뛴다(D10).
- **방 시간 멈춤 (D15):**
  - 타임어택이 시작되는 순간 방의 제한시간 타이머가 멈춘다. 이후에는 타임어택 타이머만 흐른다.
  - 그래서 타임어택 중에는 제한시간이 끝나서 게임이 끝나는 일이 없다.
- **스파이 탈출:**
  - 스파이가 로켓으로 탈출하면 마녀 타임어택이 시작된다.
  - 타임어택 중에 괴물이 남은 쿠키를 다 잡으면 괴물이 이기고 그대로 끝난다.
  - 타이머가 0이 되면 마녀가 손으로 맵 전체를 내리쳐서, 남은 쿠키와 괴물이 모두 죽는다.
- **마녀 연출:**
  - 맵 밖에 아주 크게 보인다.
  - 처음에는 뒷모습이고, 타이머가 흐를수록 서서히 돌아서 앞모습이 된다.
  - **바로 나타나지 않는다.** 스파이가 떠난 순간부터 5초 동안 투명도(알파)가 0에서 255(완전히 보임)로 서서히 올라가며 나타난다(D38).
- **스파이의 탈출 경로:** 로켓이다. 쿠키의 탈출 장치와 다르다.

### 1.3 결과 화면 (D39)

플레이어마다 한 줄씩, 닉네임 뒤에 역할을 붙여 보여준다.

| 역할 | 형식 | 예 |
|---|---|---|
| 쿠키 | 닉네임(쿠키) 탈출 성공 / 탈출 실패 | 초코칩(쿠키) 탈출 성공 |
| 스파이 | 닉네임(스파이) 탈출 성공 / 탈출 실패 | 버터링(스파이) 탈출 실패 |
| 괴물 | 닉네임(괴물) 잡은 횟수 n회 / 마녀에 의해 사망 | 마카롱(괴물) 잡은 횟수 3회 |

- 역할 이름과 문구는 인스펙터에서 입력한다(코드에 한글 금지).
- 괴물이 이긴 경우에는 추천안을 쓴다.
  - 괴물이 쿠키 유리병을 들고 서 있고, 잡힌 쿠키 수만큼 병 안에 쿠키가 들어 있다.
  - 괴물이 2명이면 각자 자기 병을 든다. 더 많이 잡은 괴물이 가운데에 서서 MVP가 된다.
  - 아래에는 위 형식의 플레이어별 줄이 나온다.

### 1.4 상자 (D7, D12, D14, D16)

- **개수:**
  - 일반 상자: **필요 재료 × 3개** (3~5개 → 9~15개)
  - 스파이 상자: **스파이 1명당 2개** (스턴건 상자 1 + 물풍선 상자 1). 지금 규칙(스파이 최대 1명)에서는 2개다.
  - 위치는 맵에 미리 만든 상자 자리(20곳) 중에서 판마다 무작위로 고른다. 최대 17개(15 + 2)가 필요해서 여유를 둔다.
- **겉모습:** 두 상자는 똑같다. 구분은 이름 표시로 한다.
- **한 번 열면 닫히지 않는다.**
- **일반 상자:**
  - 쿠키와 스파이 모두 열 수 있다.
  - 탈출 재료, 공구상자, 뿅망치가 들어 있거나 비어 있다(§1.5, §1.8).
- **스파이 상자:**
  - 스파이만 열 수 있다. 쿠키는 열 수 없다.
  - 스파이 1명당 스턴건 상자 1개와 물풍선 상자 1개가 생긴다(§1.8). 스파이가 늘어나면 한 쌍씩 늘어난다.
  - 상자마다 한 번 꺼낼 수 있다. 어느 스파이든 열 수 있다.
- **이름 표시:** 상자에 다가가면 상호작용 안내와 함께 이름이 뜬다.

  | 보는 사람 | 일반 상자 | 스파이 상자 |
  |---|---|---|
  | 쿠키 | 재료 상자 | 스파이 상자 + "잠겨 있음" |
  | 스파이 | 재료 상자 | 스파이 상자 |

- **재료가 다시 생길 때:**
  - 비어 있는 일반 상자 중에서 무작위로 고른다.
  - 그 상자를 닫힌 모습으로 되돌리고 재료를 넣는다.
  - 스파이 상자에는 넣지 않는다.

### 1.5 스파이 (D5, D17~D22)

- **스파이 수:** 지금 규칙은 최대 1명이다. 하지만 나중에 여러 명(n명)이 될 수 있도록 모든 규칙과 코드를 n명 기준으로 만든다.
- **훔치기:**
  - 스파이는 일반 상자의 탈출 재료를 가져갈 수 있다.
  - 쿠키가 이미 탈출 장치에 끼운 재료도 빼 갈 수 있다. 장치 앞에서 상호작용하면 된다.
  - **단, 장치에서는 자기 로켓에 아직 필요한 종류의 재료만 빼 갈 수 있다(D27).**
    - 예: 놀이공원 로켓(안전벨트 1, 배터리 1)에 안전벨트가 이미 끼워져 있으면, 장치에서 배터리만 뺄 수 있다.
    - 공구상자는 로켓의 어느 칸이든 대신하므로, 로켓에 빈 칸이 있으면 장치의 공구상자(수리 칸)도 뺄 수 있다.
    - 필요 없는 종류는 장치 앞에서 빼기 안내가 뜨지 않는다.
  - 훔친 재료는 쿠키와 똑같이 양손으로 들고 다닌다. 쿠키가 보면 의심할 단서가 된다.
- **스파이 로켓 (D5):**
  - 맵마다 모양이 다르고, 필요한 재료 2개가 다르다(§1.6).
  - 스파이가 필요한 종류의 재료를 들고 로켓에 다가가 끼우면, 로켓에 (1/2)처럼 진행 상황이 표시된다.
  - 로켓에 끼운 재료는 로켓 안으로 사라지고, **무작위 빈 일반 상자에 다시 생긴다**(§1.4). 그래서 쿠키가 필요한 재료 수는 줄지 않고, 쿠키는 다시 찾아야 한다.
  - 2개가 차면 로켓이 완성된다.
- **공구상자 (D18):**
  - 공구상자는 하나의 아이템이다. 모든 맵의 일반 상자에서 얻을 수 있다.
  - 놀이공원과 베이커리에서는 쿠키의 탈출 재료(수리 칸)로도 쓴다.
  - 스파이가 공구상자를 얻으면 **로켓에 필요한 부품 아무거나 하나로 조립해서 끼울 수 있다.**
  - 로켓에 끼운 공구상자도 다른 재료처럼 무작위 빈 일반 상자에 다시 생긴다.
  - 놀이공원·베이커리 외의 맵에서는 일반 상자에 공구상자를 스파이 수만큼 더 넣는다.
- **인벤토리:** 쿠키와 같은 4칸 인벤토리를 쓴다(§1.9). 스턴건과 물풍선도 여기에 들어간다.
- **여러 명일 때 탑승 (D21):**
  - 로켓 재료 2개는 스파이 모두가 함께 채운다.
  - 로켓이 완성되면 스파이가 한 명씩 탄다. 탄 스파이는 캐릭터가 사라진다.
  - **남아 있는 스파이가 모두 타야** 로켓이 떠난다. 이때 마녀 타임어택이 시작된다.
  - 잡혔거나 나간 스파이는 셀 때 뺀다.
- **스파이가 게임 중에 나가면 (D22, D28):**
  - 게임은 그대로 진행한다. 스파이가 여러 명일 때 한 명이 나가도 같다.
  - 나간 스파이의 인벤토리에 있던 **모든 아이템**이 그 자리에 떨어진다.
  - 스파이가 모두 나가면 로켓은 쓰이지 않고, 쿠키 탈출과 괴물만으로 게임이 진행된다.
- **들고 있는 동안:** 재료나 공구상자를 양손으로 들고 있는 동안에는 다른 칸으로 바꿀 수 없어서 방해 도구를 쓸 수 없다. 로켓에 끼우거나 내려놓아야 한다(§1.9).

### 1.6 맵별 탈출 방식 (D3, D4, D17)

| 맵 | 쿠키 탈출 장치 | 고정 재료 (1개씩) | 인원에 따라 늘어나는 재료 | 스파이 로켓 재료 (2개) |
|---|---|---|---|---|
| CursedCandyCarnival (놀이공원) | 롤러코스터 | 레드 버튼, 안전벨트 | 배터리 칸 3 + 수리 칸 3 중에서 무작위 | 안전벨트 1, 배터리 1 |
| HauntedBakery | 공장 안 기계. 작동하면 앞쪽 기계 문이 열리고 지하로 내려가 탈출 | 레드 버튼, 기어 | 배터리 칸 3 + 수리 칸 3 중에서 무작위 | **기어 1, 배터리 1** (안전벨트가 이 맵 재료에 없어서 바꿈) |
| ChocolateFactory | 공장 밖 기찻길의 초콜릿 기차 | 마카롱 바퀴, 쿠키 바퀴 | 남은 6칸(마카롱 바퀴 1, 쿠키 바퀴 1, 원유 2, 기어 2) 중에서 무작위 | 원유 1, 기어 1 |
| GingerbreadVillage | 중앙 룬. 맵을 쿠키 유적 마을로 바꾼다 | 없음 | 룬 N개. (n/N) 표시, 다 차면 무지개빛으로 빛남 | 룬 2 (종류 상관없음) |
| CandyForest | 중앙 케이크가 부서지고 로켓이 나옴 | 없음 | 알사탕 전지 N개(빨·주·노·초 네온 구슬). (n/N) 표시 | 전지 2 (종류 상관없음) |

- 수리 칸에 끼우는 재료가 공구상자다.
- 공구상자는 모든 맵에서 스파이 로켓 재료 하나를 대신할 수 있다(§1.5).

- 필요 재료 수 N = 쿠키 수 (3~5개). 최대 쿠키 수는 5명이다.
- **칸 구성 규칙 (D3, D4):**
  - 고정 재료는 항상 필요하다. 처음에 빈 상태로 시작한다. 예: 레드 버튼 구멍이 비어 있고, 끼우면 버튼이 생긴다.
  - 나머지 (N − 2)개는 판이 시작될 때 늘어나는 칸들 중에서 무작위로 고른다.
  - 고른 칸만 비어 있거나 금이 가 있다. 고르지 않은 칸은 처음부터 채워진 상태로 보인다(배터리 끼워짐, 수리 완료, 부품 장착).
  - 예시 (놀이공원, 쿠키 5명, 무작위 결과가 배터리 2 + 수리 1인 경우):
    - 레드 버튼과 안전벨트가 빠져 있다.
    - 배터리 칸은 3칸 중 2칸이 비어 있고 1칸은 끼워져 있다.
    - 수리 칸은 3칸 중 1칸만 금이 가 있고 2칸은 고쳐져 있다.
  - 예시 (초콜릿 공장, 쿠키 5명, 무작위 결과가 원유 2 + 기어 1인 경우):
    - 마카롱 바퀴 1과 쿠키 바퀴 1은 고정으로 비어 있다.
    - 나머지 바퀴 2칸과 기어 1칸은 처음부터 채워져 있다.
    - 원유 2칸과 기어 1칸이 비어 있다.
- 재료는 **양손으로 들고** 다닌다. 한 번에 1개만 들 수 있다.

---

### 1.7 방 만들기 설정 (D13, D25)

방장이 방을 만들 때 세 가지 값을 정한다.
- 화살표 버튼(▲▼)으로 올리고 내리거나, 숫자를 직접 입력한다.
- 입력칸에는 숫자만 들어간다. 다른 문자는 입력되지 않는다.

| 항목 | 최소 | 최대 | 기본값 | 화살표 한 번 |
|---|---|---|---|---|
| 인원 수 | 4명 | 8명 | 4명 | 1명 |
| 제한시간 | 10분 | 40분 | 10분 | 1분 |
| 타임어택 시간 | 1분 | 10분 | 1분 | 1분 |

- 범위를 벗어나면(화살표든 직접 입력이든) **빨간색 경고 문구**를 띄우고, 값은 **가장 가까운 허용값**으로 되돌린다.
  - 예: 제한시간에 50을 입력하면 40분이 된다.
  - 인원: 4명 미만과 9명 이상은 할 수 없다는 문구
  - 제한시간: 10분 미만과 41분 이상은 할 수 없다는 문구
  - 타임어택: 1분 미만과 10분 초과는 할 수 없다는 문구
- 입력칸을 비워 두면 기본값으로 되돌린다.
- 문구는 인스펙터에서 입력한다(코드에 한글 금지).
- 범위 값은 `GameSettingsSO`에 둔다. 나중에 바꾸기 쉽게 하기 위해서다.
- 방에 들어온 사람에게는 대기실에서 세 값을 보여준다.

### 1.8 방해 도구와 뿅망치 (D19, D20, D23, D24)

| 도구 | 얻는 곳 | 쓰는 사람 | 횟수 | 효과 |
|---|---|---|---|---|
| 스턴건 | 스파이 상자 | 스파이 | 3회 | 조금 먼 대상을 맞힌다. 맞은 대상은 기절하고, 손에 든 아이템을 떨어뜨린다 |
| 물풍선 | 스파이 상자 | 스파이 | 3회 | 던져서 맞힌다. 맞은 대상은 기절하고, 손에 든 아이템을 떨어뜨린다. 맞은 쿠키는 물풍선 색으로 칠해진다. 색은 풍선마다 무작위 |
| 뿅망치 | 일반 상자 | 쿠키, 스파이 | 3회 | 가까이 있는 대상을 때린다. 맞은 대상은 기절하고, 손에 든 아이템을 떨어뜨린다 |

- **맞으면 손에 든 아이템 하나만 떨어뜨린다(D20).**
  - 지금 선택된 칸, 즉 겉으로 보이게 들고 있는 아이템만 떨어진다. 인벤토리의 다른 칸은 그대로다.
  - 쿠키와 스파이 모두 같다.
- **괴물도 세 도구 모두에 기절한다(D24).** 기절한 동안 움직이거나 잡을 수 없다. 기절 시간은 도구마다 데이터로 정한다.
- **기절 이펙트 (D29):**
  - 기절한 캐릭터 머리 위에 별이 빙글빙글 도는 이펙트를 띄운다.
  - 기절 시간이 끝나면 사라진다.
  - 쿠키, 스파이, 괴물 모두 같은 이펙트를 쓴다. 크기만 캐릭터 키에 맞춘다(괴물이 더 크다).
  - 이펙트는 미리 만들어 둔 것을 켜고 끄는 방식으로 쓴다(최적화).
- **물풍선 색칠:** 기존 색칠 시스템(`PlayerPaintCanvas`의 강제 칠하기)을 그대로 쓴다. 변장이 망가지는 효과가 생긴다.
- **밸런스 조정 (D23):**
  - 모든 도구의 수치를 ScriptableObject(`ToolSO`)로 뺀다. 횟수, 기절 시간, 사거리, 쿨다운, 괴물에게 주는 기절 시간, 상자에서 나올 확률 등이다.
  - 코드를 고치지 않고 에셋 값만 바꿔서 밸런스를 테스트할 수 있다.
  - 새 도구도 `ToolSO`와 동작 클래스 하나만 추가하면 된다(OOP 규칙).
- **첫 기본값** (플레이해 보고 조정):

  | 항목 | 값 |
  |---|---|
  | 뿅망치 사거리 | 2m |
  | 뿅망치 기절 시간 | 쿠키·스파이 1.5초, 괴물 1초 |
  | 스턴건 사거리 | 8m |
  | 스턴건 기절 시간 | 쿠키·스파이 2초, 괴물 1.5초 |
  | 물풍선 기절 시간 | 쿠키·스파이 1초, 괴물 0.8초 |
  | 물풍선 던지는 거리 | 12m |
  | 물풍선 터지는 범위 | 반경 1.5m |
  | 쿨다운 | 모든 도구 3초 |

### 1.9 인벤토리 (D26, D30~D33)

쿠키와 스파이 모두 같은 인벤토리를 쓴다. 리썰 컴퍼니처럼 화면 아래에 칸이 보이는 방식이다. 괴물은 인벤토리가 없다.

- **칸 수:** 최대 4칸이다. 숫자키 1~4나 마우스 휠로 칸을 고른다.
  - 색칠 단계에서는 마우스 휠이 붓 크기 조절(`PlayerInput.BrushSizeDelta`)에 쓰이므로, 그동안은 휠로 칸을 바꾸지 않는다.
- **손에 든 아이템:** 지금 고른 칸의 아이템이 손에 들려 겉으로 보인다.
- **줍기와 떨어뜨리기 (D30):**
  - 줍기: 상호작용 키 **E**. 상자에서 꺼낼 때도 E다.
  - 떨어뜨리기: **G** 키. 손에 든 아이템을 바닥에 내려놓는다. 누구나 다시 주울 수 있다.
- **드는 방식 (D31):**
  - 아이템마다 **한 손** 또는 **두 손**으로 든다. 이 값은 아이템 데이터(`ItemSO.HoldType`)에 둔다. 나중에 새 아이템이 생겨도 데이터만 정하면 된다.
  - 지금은 재료와 공구상자만 두 손이고, 뿅망치·스턴건·물풍선은 한 손이다.
  - 한 손 아이템은 오른손에, 두 손 아이템은 두 손 사이에 들고 애니메이션도 그에 맞춘다.
- **두 손 아이템 (중요):**
  - 두 손 아이템은 한 번에 1개만 가질 수 있다.
  - 두 손 아이템을 들고 있는 동안에는 **다른 칸으로 바꿀 수 없다.** 도구를 쓰려면 먼저 끼우거나 G로 내려놓아야 한다.
  - 두 손 아이템을 들고 있는 동안에는 다른 아이템도 주울 수 없다.
  - 예: 1번 칸에 재료, 2번 칸에 뿅망치가 있으면, 재료를 내려놓기 전까지 2번 칸을 고를 수 없다.
- **떨어뜨린 뒤 자동으로 들기 (D32):**
  - 손에 든 아이템이 없어지면(떨어뜨림, 설치, 로켓에 끼움, 도구 횟수 다 씀), **다음 번호 칸부터 차례로** 찾아 처음 나오는 아이템을 바로 든다. 끝 칸 다음에는 1번 칸부터 다시 찾는다.
  - 예: 1·3·4번 칸에 아이템이 있고 2번 칸이 비었을 때 1번을 떨어뜨리면, 바로 3번 칸 아이템을 든다.
  - 모든 칸이 비었으면 빈손이 된다.
- **도구:** 뿅망치, 스턴건, 물풍선은 칸 하나씩 차지하고, 남은 횟수가 칸에 표시된다. 횟수를 다 쓰면 칸에서 사라진다.
- **칸이 가득 차면:** 새 아이템을 주울 수 없다. 안내 문구로 알려준다.
- **떨어뜨리는 경우 (D11, D20, D28):**

  | 상황 | 떨어지는 것 |
  |---|---|
  | G키 | 손에 든 아이템 하나 |
  | 도구에 맞음 | 손에 든 아이템 하나 |
  | 괴물에게 잡힘 | 인벤토리의 모든 아이템 |
  | 게임 중에 나감 (쿠키, 스파이) | 인벤토리의 모든 아이템 |

  - 떨어진 아이템은 그 자리 바닥에 놓이고, 누구나 다시 주울 수 있다.

### 1.10 역할별로 할 수 있는 것 (D33~D36)

| 할 수 있는 것 | 쿠키 | 스파이 | 괴물 |
|---|---|---|---|
| 문 열고 닫기 | ○ | ○ | ○ |
| 아이템 줍기, 상자 열기 | ○ | ○ (스파이 상자 포함) | ✕ |
| 쿠키 탈출구로 탈출 | ○ | ✕ | ✕ |
| 스파이 로켓 | ✕ | ○ | ✕ |
| 잡기 | ✕ | ✕ | ○ (쿠키와 스파이 모두) |

- **괴물은 문만 열고 닫을 수 있다 (D33).** 아이템을 줍거나 상자를 열 수 없다. 각 사물의 `CanInteract`가 괴물을 거른다.
- **괴물은 스파이도 잡을 수 있다 (D34).** 스파이도 쿠키 캐릭터라서 잡는 방식은 같다. 잡힌 스파이는 탈출 실패다.
  - 잡는 방식은 조준 + E키다(`GameFixPlan.md` F1). 조준 대상이 있으면 E키는 문보다 잡기가 먼저다.
  - 스파이가 잡히면 모두의 화면 가운데에 **"스파이가 잡혔습니다"** 문구가 뜨고, 서서히 사라진다(약 3초).
- **탈출구는 쿠키만 들어갈 수 있다 (D35).** 재료를 다 모아 탈출구가 열려도 스파이와 괴물은 들어갈 수 없다.
  - 스파이가 다가가면 쿠키처럼 E 아이콘이 뜨지만, 눌러도 "들어갈 수 없음"만 나온다. 그래야 스파이만 못 들어가는 모습으로 정체가 드러나지 않는다.
  - 괴물에게는 E 아이콘도 뜨지 않는다.
- **탈출한 쿠키는 다른 쿠키의 시점으로 관전한다 (D36).** 기존 관전 모드(`SpectatorController`, 대상: 살아 있는 쿠키)를 그대로 쓴다. 관전 대상에는 스파이도 쿠키로 들어간다(겉모습이 쿠키라서 빼면 정체가 드러난다).

### 1.11 화면 표시 (D37, D38)

- **필요한 재료 표시 (오른쪽 위):**
  - 재료 이미지, 이름, (지금 수/필요한 수)를 한 줄씩 보여준다.
  - 쿠키는 탈출 장치의 재료를, 스파이는 자기 로켓의 재료를 본다.

  | 쿠키 화면 (예: 베이커리) | 스파이 화면 (예: 베이커리) |
  |---|---|
  | 기어 0/1 | 기어 0/1 |
  | 레드 버튼 0/1 | 배터리 0/1 |
  | 수리 0/2 | |
  | 배터리 0/1 | |

  - 누가 끼우거나 빼면 모두의 화면에서 바로 바뀐다.
- **훔쳤을 때 (D37):**
  - 스파이가 탈출 장치에서 재료를 빼면, 쿠키 화면의 그 재료 수만 줄어든다(예: 기어 1/1 → 0/1).
  - **이때는 알림 문구를 띄우지 않는다.**
- **로켓에 끼움 알림 (D40):**
  - 스파이가 **장치에서 훔친 재료**를 로켓에 끼울 때만, 모두의 화면에 **"스파이가 기어를 훔쳐 로켓에 끼워넣었습니다"** 문구가 뜨고 서서히 사라진다.
  - 재료 이름 끝 글자에 받침이 있으면 "을", 없으면 "를"을 붙인다(예: 기어를, 레드 버튼을). 문구 틀은 인스펙터에서 두 가지로 입력한다.
  - 상자나 바닥에서 주운 재료, 공구상자를 로켓에 끼울 때는 알림이 뜨지 않는다. 훔친 재료인지는 방장이 재료마다 기록한다(`EscapeItems`의 `stolenFromDevice`).
- **마녀 등장 (D38):** 스파이가 떠난 순간부터 5초 동안 알파 0에서 255로 서서히 나타난다(§1.2).

## 2. 결정 기록

| # | 질문 | 결정 |
|---|---|---|
| D1 | 인원표와 설명이 다를 때 | 표를 따른다 (§1.1) |
| D2 | 방 정원 | 방을 만들 때 4~8명 중에서 고르고, 정원이 차면 시작 |
| D3 | 고정 재료 | 놀이공원: 레드 버튼·안전벨트 / 베이커리: 레드 버튼·기어 / 공장: 마카롱 바퀴·쿠키 바퀴 |
| D4 | 늘어나는 재료를 정하는 방법 | 판 시작 때 늘어나는 칸들 중에서 무작위로 고름. 고르지 않은 칸은 채워진 상태로 시작 (§1.6) |
| D5 | 스파이 로켓 | 맵별 재료 2개를 끼워 완성하면 탈 수 있음 (§1.5, §1.6) |
| D6 | ~~가짜 재료~~ | **폐기.** 스파이는 진짜 재료를 훔침 (D17) |
| D7 | 스파이 상자 | 겉모습은 일반 상자와 같음. 스파이만 열 수 있음 (§1.4) |
| D8 | 제한시간이 끝났을 때 | 탈출 못 한 쿠키와 스파이는 탈출 실패, 괴물은 잡은 횟수만 표시 |
| D9 | 색칠 단계 | 유지 |
| D10 | 쿠키가 먼저 모두 끝났을 때 | 스파이가 로켓을 타면 바로 탈출 성공. 타임어택은 건너뜀 |
| D11 | 잡힌 쿠키의 아이템 | 인벤토리의 모든 아이템이 잡힌 자리에 떨어짐. 누구나 다시 주울 수 있음 |
| D12 | 상자 수 | 일반 상자는 필요 재료 × 3개, 스파이 상자는 스파이 1명당 2개. 상자 자리 20곳 중에서 무작위 |
| D13 | 방 설정 | 방장이 인원(4~8), 제한시간(10~40분), 타임어택(1~10분)을 정함 (§1.7) |
| D14 | 상자 이름 표시 | 일반 상자는 모두에게 "재료 상자". 스파이 상자는 모두에게 "스파이 상자", 쿠키에게는 "잠겨 있음"도 표시 (§1.4) |
| D15 | 타임어택 중 방 시간 | 타임어택이 시작되면 방의 제한시간 타이머는 멈추고 타임어택 타이머만 흐름 |
| D16 | 상자 열림 | 한 번 열면 닫히지 않음. 재료가 다시 생긴 상자만 닫힌 모습으로 돌아감 |
| D17 | 훔치기 | 스파이는 일반 상자의 재료와 쿠키가 탈출 장치에 이미 끼운 재료를 훔칠 수 있음. 로켓에 끼운 재료는 무작위 빈 일반 상자에 다시 생김. 장치에서는 로켓에 필요한 종류만 (D27) |
| D18 | 공구상자 | 하나의 아이템. 일반 상자에서 얻음. 쿠키에게는 수리 재료, 스파이에게는 로켓 부품 하나를 대신함 |
| D19 | 스파이 상자 내용 | 스파이 1명당 스턴건 상자 1개(3회) + 물풍선 상자 1개(3회, 색 무작위, 맞은 쿠키를 그 색으로 칠함). 스파이가 늘면 한 쌍씩 늘어남 |
| D20 | 맞았을 때 | 뿅망치·스턴건·물풍선 모두 손에 든 아이템 하나만 떨어뜨림. 스파이도 같음 |
| D21 | 스파이가 여러 명일 때 로켓 | 남아 있는 스파이가 모두 타야 떠남 |
| D22 | 스파이가 나갔을 때 | 게임은 그대로 진행 |
| D23 | 도구 밸런스 | 수치를 ScriptableObject로 빼서 에셋만 바꿔 테스트 |
| D24 | 도구와 괴물 | 뿅망치는 쿠키와 스파이가 일반 상자에서 얻음. 괴물은 세 도구 모두에 기절함 |
| D25 | 직접 입력 | 숫자만 입력 가능. 범위 밖이면 빨간 경고 + 가장 가까운 허용값으로 되돌림 |
| D26 | 인벤토리 | 쿠키와 스파이 모두 4칸. 재료는 양손으로 1개만, 들고 있는 동안 칸 바꾸기 금지 (§1.9) |
| D27 | 장치에서 훔치기 | 스파이는 자기 로켓에 아직 필요한 종류만 장치에서 뺄 수 있음. 공구상자는 로켓에 빈 칸이 있으면 뺄 수 있음 |
| D28 | 게임 중에 나감 | 쿠키와 스파이 모두 인벤토리의 모든 아이템을 그 자리에 떨어뜨림 |
| D29 | 기절 이펙트 | 머리 위에 별이 도는 이펙트. 쿠키, 스파이, 괴물 공통 |
| D30 | 줍기와 떨어뜨리기 | 줍기는 E, 떨어뜨리기는 G |
| D31 | 드는 방식 | 아이템마다 한 손 또는 두 손(`ItemSO.HoldType`). 지금은 재료와 공구상자만 두 손 |
| D32 | 떨어뜨린 뒤 | 다음 번호 칸부터 찾아 처음 나오는 아이템을 바로 듦 |
| D33 | 괴물이 할 수 있는 것 | 문 열고 닫기만. 줍기와 상자 열기는 못 함 |
| D34 | 스파이 잡기 | 괴물은 스파이도 잡을 수 있음. "스파이가 잡혔습니다"가 떴다가 서서히 사라짐 |
| D35 | 탈출구 | 쿠키만 들어갈 수 있음. 스파이와 괴물은 못 들어감 |
| D36 | 탈출한 쿠키 | 다른 쿠키 시점으로 관전 |
| D37 | 필요한 재료 표시 | 오른쪽 위에 재료 이미지와 (n/필요 수). 장치에서 훔치면 수만 줄고 알림은 없음 |
| D38 | 마녀 등장 | 5초 동안 알파 0 → 255로 서서히 |
| D39 | 결과 화면 | 닉네임(역할) + 탈출 성공·실패 / 잡은 횟수·마녀에 의해 사망 |
| D40 | 로켓에 끼움 알림 | 장치에서 훔친 재료를 로켓에 끼울 때만 "스파이가 ○○을(를) 훔쳐 로켓에 끼워넣었습니다" |

## 3. 작업 순서

| 단계 | 내용 | 상태 |
|---|---|---|
| P0 | D1~D12 결정 ✅ / 계획서 최종 승인 ✅ | ✅ |
| P1 | Blender: 에셋 제작 (탈출 장치, 재료, 상자, 스파이 로켓, 마녀) | ✅ |
| P2 | Blender: 맵 5개에 배치, 진저브레드 유적 마을로 개편 | ✅ (배치는 Unity, §7) |
| P3 | Unity: 맵 다시 빌드, 자동 축소, 통행 검사 | ✅ |
| P4 | Unity: 역할 배정(괴물 수·스파이) 규칙 | ✅ |
| P5 | Unity: 재료, 상자, 인벤토리 4칸 (네트워크 포함) | ✅ |
| P6 | Unity: 탈출 장치와 탈출 (맵 5개) | ✅ |
| P7 | Unity: 스파이 (훔치기, 로켓, 스파이 상자)와 도구 (스턴건, 물풍선, 뿅망치, 기절 이펙트) | ✅ |
| P8 | Unity: 마녀 타임어택 | ✅ |
| P9 | Unity: 게임 끝 판정과 결과 화면 | ✅ |
| P10 | 테스트와 Play Mode 검증, 문서 정리 | ✅ |

- 먼저 **CandyForest 하나로 P1~P9를 끝까지** 완성해서 게임 흐름을 확인한다. 그다음 나머지 4개 맵으로 넓힌다.
- 맵마다 다른 것은 에셋과 레시피 데이터뿐이다. 코드는 한 번만 만든다.

---

## 4. P1~P3: Blender 작업과 맵 다시 빌드

### 4.1 원칙

- **상태는 Blender에서 정하지 않는다.**
  - 필요한 재료 수는 판이 시작돼야 알 수 있다.
  - 그래서 Blender에서는 모든 칸을 모든 상태로 만들어 둔다.
  - 게임 시작 때 Unity가 무엇을 보이고 숨길지 정한다.
- **원래 크기(344m) 좌표로 배치한다.**
  - `MapSceneBuilder`로 다시 빌드하면 `MapCompactor.OnMapBuilt`가 두 가지를 자동으로 한다. 코드로 확인했다.
    1. 원본 보관본(`Assets/Scenes/Maps/Original/`)을 새로 덮어쓴다.
    2. 140m로 다시 줄인다.
- **탈출 장치는 부모 오브젝트 하나로 묶는다.** 축소 도구가 한 덩어리로 옮기기 때문이다.
- **주변을 비워 둔다.** 탈출 장치, 로켓, 상자 자리 주변은 최소 간격 4m 규칙을 지킨다.

### 4.2 이름 규칙

Unity 코드는 오브젝트를 이름으로 찾는다.

| 이름 | 뜻 |
|---|---|
| `ESC_<Map>_Root` | 탈출 장치의 부모 |
| `ESC_<Map>_Body` | 항상 보이는 본체 |
| `ESC_<Map>_<Kind>_<nn>_Socket` / `_Part` | 끼우는 칸: 빈 상태 / 끼워진 상태 |
| `ESC_<Map>_<Kind>_<nn>_Broken` / `_Fixed` | 수리 칸: 금 간 상태 / 고쳐진 상태 |
| `ESC_<Map>_Interact` | 상호작용 위치 (빈 오브젝트) |
| `ESC_<Map>_Board` | 탈출할 때 타는 위치 (빈 오브젝트) |
| `ESC_<Map>_Glow` | 빛나는 부분 (룬, 케이크 등). 머티리얼을 따로 쓴다 |
| `SPY_Rocket_<Map>_Root` / `_Interact` / `_Slot_01~02` | 스파이 로켓 (맵마다 모양이 다름). 슬롯 2개는 빈 상태/끼워진 상태로 만든다 |
| `CHEST_Slot_<nn>` | 상자 자리 (빈 오브젝트, 20개) |

### 4.3 칸 수

필요 재료는 최대 5개이고 고정 재료가 2개라, 늘어나는 재료는 최대 3개다. 무작위 결과가 한 종류에 몰려도 되도록 **종류마다 3칸**을 만든다. 공장은 사용자가 정한 대로 **종류마다 2칸, 모두 8칸**이다.

| 맵 | 칸 |
|---|---|
| 놀이공원 | RedButton 1, Seatbelt 1, Battery 3, Repair 3 |
| 베이커리 | RedButton 1, Gear 1, Battery 3, Repair 3 |
| 공장 | MacaronWheel 2, CookieWheel 2, Oil 2, Gear 2 |
| 진저브레드 | 룬 제단 1 + `Glow` (칸 없음) |
| 캔디숲 | `Cake_Intact`, `Cake_Shard_01~`, `Cake_Rocket` (칸 없음) |

### 4.4 따로 만드는 에셋 (`assets.py`)

- **재료 아이템:**
  - 레드 버튼, 안전벨트, 배터리, 공구상자, 기어, 마카롱 바퀴, 쿠키 바퀴, 초콜릿 원유, 룬, 알사탕 전지(4색)
  - 원점은 **두 손 사이 가운데**에 둔다. 바닥에 놓인 모습도 같은 모델을 쓴다.
- **상자 1종:** 일반 상자와 스파이 상자는 겉모습이 같다. 뚜껑은 따로 나눈다(여는 애니메이션, 다시 닫힘용).
- **스파이 로켓 5종:** 맵마다 모양이 다르다. 캔디숲의 케이크 로켓과도 다르게 한다.
- **도구 3종:** 뿅망치, 스턴건, 물풍선. 손에 드는 모델이다. 물풍선은 색을 바꿀 수 있게 머티리얼을 따로 둔다.
- **마녀:**
  - 뼈대(리그)가 있는 거대 캐릭터다.
  - 애니메이션 2개가 필요하다. `Turn`(뒷모습에서 앞모습으로 돌아서기)과 `Slam`(손으로 맵 내려치기).
  - 맵 밖 약 120m 거리에 키 80~100m로 세운다. 안개와 스카이박스에 가려지지 않는지 Unity에서 확인한다.

### 4.5 맵 배치 (`build_*.py`)

| 맵 | 할 일 |
|---|---|
| 캔디숲 | 중앙에 케이크 배치 |
| 공장 | 공장 밖에 기찻길과 기차 배치 |
| 베이커리 | 공장 안에 기계 배치. 앞쪽 기계 문과 지하로 내려가는 입구를 만든다 |
| 놀이공원 | 롤러코스터 탑승구 배치 |
| 진저브레드 | 마을을 쿠키 유적 마을로 개편. 무너진 벽, 부서진 과자 집 등. 중앙에 룬 제단 |
| 공통 | 스파이 로켓 1곳, 상자 자리 20곳 |

### 4.6 Unity에서 다시 빌드 (P3)

1. 내보낸 FBX로 `MapSceneBuilder`를 실행한다. 그러면 원본 보관본이 새로 만들어지고 자동으로 축소된다.
2. 기찻길처럼 긴 구조물은 축소 도구가 벽처럼 늘이거나 줄일 수 있다. 첫 빌드 뒤 모양을 확인하고, 필요하면 `MapCompactor`의 맵별 설정을 고친다.
3. 통행 검사(`MapPassabilityCheck`)와 테스트(`MapCompactTests`)를 다시 돌린다. 테스트에 추가할 검사:
   - `ESC_*` 오브젝트가 맵마다 모두 있는지
   - 상자 자리가 20개인지
   - 탈출 장치와 로켓 상호작용 위치에 쿠키가 걸어서 갈 수 있는지

---

## 5. P4~P9: Unity 구현

### 5.1 코드 위치 (Claude.md 폴더 규칙)

| 분류 | 경로 |
|---|---|
| 스크립트 | `Assets/02. Scripts/Escape/` (새 도메인) |
| 맵별 레시피 SO | `Assets/03. SO/Escape/Recipe_<Map>.asset` |
| 프리팹 | `Assets/04. Prefabs/Escape/` |
| UI | `Resources/UI/Scene/EscapeHud`, `ResultScreen`은 기존 것을 고친다 |
| 전역 수치 | 기존 `Assets/Resources/GameSettings` |

### 5.2 네트워크 방식

기존 문(`InteractableDoor`)과 같은 방식을 쓴다. 이미 검증된 구조다.

- 상태는 **Room Props**에 두고 **방장만 쓴다.**
- 누른 클라이언트는 방장에게 요청(`RaiseEvent`)하고, 모든 클라이언트가 Props 변경을 보고 화면을 맞춘다.
- 늦게 들어오거나 방장이 바뀌어도 Props에서 현재 상태를 읽으면 된다.
- 재료와 상자에는 **PhotonView를 붙이지 않는다.** 재료 하나가 어디 있는지만 표로 동기화한다. 네트워크 객체 수와 메시지를 줄이기 위해서다(최적화 규칙).
- 새 키는 `NetKeys.Scopes` 표에 `Round` 수명으로 등록한다. 그러면 판이 끝날 때 `RoundStateResetter`가 자동으로 지운다.

| 새 키 | 대상 | 내용 |
|---|---|---|
| `EscapeRecipe` | Room | 이번 판의 칸 구성 (칸 번호 → 재료 종류), 필요 재료 수 |
| `EscapeItems` | Room | 재료마다: 상자 안 / 누가 들고 있음 / 바닥 위치 / 설치됨, 그리고 장치에서 훔친 재료인지(`stolenFromDevice`) |
| `ChestStates` | Room | 상자마다: 닫힘 / 열림, 들어 있던 것 |
| `SpyActorNumbers` | Room | 스파이 번호 (§5.9 보안 참고) |
| `RoomTimeLimit`, `TimeAttackDuration` | Room | 방장이 정한 제한시간과 타임어택 시간(초). 수명은 `Session`이라 판이 바뀌어도 유지된다 |
| `SpyChestStates` | Room | 스파이 상자마다: 닫힘 / 꺼냄 |
| `RocketState` | Room | 로켓 슬롯마다 끼워졌는지, 탑승한 스파이 번호들 |
| `Inventory`, `SelectedSlot` | Player | 인벤토리 4칸의 아이템과 남은 횟수, 지금 고른 칸. 본인 클라이언트만 쓴다 |
| `SpyEscapedAt`, `TimeAttackEndTime` | Room | 스파이 탈출 시각, 타임어택 끝나는 시각 |
| `WitchStrike` | Room | 마녀가 내리쳤는지 |
| `Escaped` | Player | 0 = 아직, 1 = 탈출 성공 |
| `CatchCount` | Player (괴물) | 잡은 횟수 |
| `DeathCause` | Player | 괴물에게 잡힘 / 마녀 |
| `RevealedSpies` | Room | 게임이 끝난 뒤 방장이 공개하는 스파이 번호들 (결과 화면용) |

- 새 이벤트 코드는 `NetEventCodes`의 7번부터 쓴다.
  - `ItemRequest`: 줍기, 내려놓기, 설치
  - `ChestRequest`: 상자 열기
  - `EscapeRequest`: 탈출하기
  - `StealRequest`: 탈출 장치에서 재료 빼기
  - `RocketRequest`: 로켓에 재료 끼우기와 탑승
  - `ToolUseRequest`: 도구 쓰기 (맞힌 대상 포함)
  - `PickUpRequest`, `DropRequest`: 줍기와 떨어뜨리기 (G키)
  - `EscapeExitRequest`: 쿠키 탈출구로 들어가기
  - `SpyCaught`, `StolenItemToRocket`: 방장이 모두에게 보내는 알림 (§5.10)

### 5.3 P4: 역할 배정

- `GameSettingsSO`:
  - `maxPlayers`를 8로 올린다.
  - 인원표(총 인원 → 괴물 수, 스파이 수)를 설정값으로 둔다.
  - `MonsterCountFor(playerCount)`가 이 표를 읽게 바꾼다. 가마솥 괴물 자원과 무작위 배정(`MonsterAssignmentAuthority`)은 그대로 둔다.
- 방 만들기(`LobbyController`):
  - 인원, 제한시간, 타임어택 시간을 화살표로 고르는 UI를 추가한다(§1.7).
  - 인원은 `RoomOptions.MaxPlayers`로 쓴다.
  - 제한시간과 타임어택 시간은 방을 만들 때 Room Props(`RoomTimeLimit`, `TimeAttackDuration`, 수명 `Session`)로 넣는다.
  - 값을 고르고 제한하는 로직은 UI와 분리한 순수 클래스(`RoomSettingStepper`)로 만든다. 그래야 테스트할 수 있다.
- `MonsterJoinController`가 `GameEndTime`을 정할 때 `GameSettings.SurvivalDuration` 대신 방의 `RoomTimeLimit`을 쓴다.
- 스파이 배정:
  - 판이 시작될 때(시작 버튼, `GameStartAuthority`) 방장이 괴물이 아닌 사람 중에서 무작위로 뽑는다.
  - 스파이 본인에게만 "당신은 스파이" 알림을 띄운다.

### 5.4 P5: 재료, 상자, 들고 다니기

| 클래스 | 역할 |
|---|---|
| `EscapeRecipeSO` | 맵별 데이터: 고정 재료, 늘어나는 재료 종류, 칸 이름, 탈출 방식 |
| `EscapeItemAuthority` (방장 전용) | 판 시작 때 레시피와 인원으로 칸 구성을 정하고, 재료를 상자에 무작위로 나눈다. 모든 요청을 검증해서 Props에 쓴다 |
| `MaterialChest : IInteractable, IInteractionLabel` | 이름은 "재료 상자"다. 열면 안에 든 것(재료, 공구상자, 뿅망치)이 나온다. 쿠키와 스파이만 쓸 수 있다. 한 번 열면 닫히지 않고, 재료가 다시 생길 때만 닫힌 모습으로 돌아간다 |
| `IInteractionLabel` | 상호작용 안내에 이름을 띄우는 선택 계약이다. `GetLabel(보는 캐릭터)`로 이름을 돌려준다. 문(`InteractableDoor`)은 고치지 않아도 되도록 `IInteractable`과 따로 둔다. `InteractionPromptUI`는 이 계약이 있으면 이름을 함께 보여준다 |
| `EscapeItemPresenter` | Props를 보고 재료 모델을 상자·바닥·손에 놓는다. 모델은 미리 만든 풀(pool)에서 꺼내 쓴다. Instantiate/Destroy를 반복하지 않는다 |
| `CarriedItemSocket` | 쿠키의 두 손 위치. 들고 있으면 들고 가는 애니메이션으로 바꾼다 |

규칙은 §1.9 인벤토리를 따른다.
- 재료는 양손으로 한 번에 1개만 들 수 있고, 들고 있는 동안 칸을 바꿀 수 없다.
- 재료를 들고 상호작용 키를 누르면 장치 앞에서는 설치한다. 내려놓기는 버리기 키로 한다.
- 괴물은 인벤토리가 없어서 아이템을 들 수 없다.
- 잡히면 인벤토리의 모든 아이템이 떨어진다(D11). 잡힘 처리는 `HideOrSeekPlayer`의 기존 흐름에 연결한다.
- `CarriedItemSocket`은 `PlayerInventory`가 손에 든 아이템(재료는 양손, 도구는 한 손)을 보여줄 때 쓴다.

### 5.5 P6: 탈출 장치

| 클래스 | 역할 |
|---|---|
| `EscapeDevice : IInteractable` | 레시피대로 `ESC_*` 오브젝트를 찾아 칸을 만들고, 칸 상태를 보이게 한다. 설치 요청을 보낸다 |
| `EscapeProgressHud` | (n/N) 표시. 들고 있는 재료 아이콘을 보여준다 |
| `EscapeDeviceFx` (맵별) | 완성 연출: 롤러코스터 출발, 기계 문 열림, 기차 출발, 룬 무지개빛, 케이크 부서짐과 로켓 등장 |

- 장치가 완성되면 쿠키가 `ESC_*_Interact`에서 상호작용한다.
- 그러면 한 명씩 탈출 처리된다. 본인의 `Escaped = 1`을 쓴다.
- 탈출한 쿠키는 캐릭터를 숨기고 관전 모드(`SpectatorController`)로 넘어간다.

### 5.6 P7: 스파이와 도구

규칙은 §1.5와 §1.8을 따른다.

| 클래스 | 역할 |
|---|---|
| `SpyChest : IInteractable, IInteractionLabel` | 겉모습은 `MaterialChest`와 같다. 이름은 모두에게 "스파이 상자"다. 쿠키에게는 "잠겨 있음"을 보여주고 열리지 않는다. 스파이가 열면 도구를 인벤토리 빈 칸에 넣는다. 상자마다 한 번 |
| `EscapeDevice` (확장) | 스파이가 장치 앞에서 상호작용하면, 끼워진 재료 하나를 빼서 스파이 손에 들린다 |
| `EscapeItemAuthority` (방장, 확장) | 훔치기, 로켓에 끼우기, 재료 다시 생기기(무작위 빈 일반 상자를 닫힌 모습으로 되돌리고 넣음)를 처리한다. 모든 요청은 스파이 여부와 거리를 확인한다 |
| `SpyRocket : IInteractable` | 스파이만 쓸 수 있다. 맞는 종류의 재료나 공구상자를 들고 있으면 끼운다. 완성되면 스파이가 한 명씩 탄다. 남아 있는 스파이가 모두 타면 `SpyEscapedAt`과 `TimeAttackEndTime`을 기록한다. 쿠키가 먼저 모두 끝났으면 타임어택 없이 바로 끝낸다(D10) |
| `PlayerInventory` | 쿠키와 스파이 공용 4칸 인벤토리. 칸 고르기, 재료를 든 동안 칸 바꾸기 막기, 줍기, 내려놓기, 떨어뜨리기(손에 든 것 하나 / 전부)를 맡는다. 상태는 Player Props(`Inventory`, `SelectedSlot`)로 동기화하고, 손에 든 모델은 모두의 화면에 보인다 |
| `InventoryHotbarUI` | 화면 아래 4칸 표시. 고른 칸 강조, 도구 남은 횟수, 칸이 가득 찼다는 안내 |
| `ToolSO` | 도구 하나의 수치(횟수, 기절 시간, 사거리, 쿨다운, 괴물 기절 시간, 상자 확률) |
| `ToolUser` | 도구 사용 입력과 쿨다운. 맞힌 대상을 방장에게 알린다 |
| `ITool`과 `HammerTool`, `StunGunTool`, `WaterBalloonTool` | 도구별 동작(근접 판정, 발사, 투척과 터지는 범위) |
| `StunReceiver` | 맞은 캐릭터 쪽(쿠키, 스파이, 괴물 모두). 기절 동안 이동과 행동(괴물은 잡기) 막기, 손에 든 아이템 하나 떨어뜨리기(괴물 제외), 물풍선이면 색칠 시스템으로 칠하기 |
| `StunEffect` | 머리 위에 별이 도는 기절 이펙트. 캐릭터마다 하나를 미리 붙여 두고 켜고 끈다. 크기는 캐릭터 키에 맞춘다 |

- **소유권 원칙:** 기절과 떨어뜨리기는 맞은 사람 본인 클라이언트가 처리한다. 지금 `HitCount`처럼 본인만 자기 상태를 쓰는 방식을 따른다.
- **누가 나갔을 때 (쿠키, 스파이):**
  - 방장이 `OnPlayerLeftRoom`에서 나간 사람의 인벤토리(Player Props는 나가면 읽을 수 없으므로 방장이 `EscapeItems` 표로 추적한다)의 모든 아이템을 마지막 위치 바닥에 떨어뜨린다.
  - 스파이였으면 로켓 탑승 조건(남은 스파이 수)을 다시 계산한다.

### 5.7 P8: 마녀 타임어택

- `GamePhase`에 `TimeAttack`을 추가한다. `SpyEscapedAt`이 있으면 이 단계가 된다.
- `WitchPresenter`:
  - 맵 밖 위치에 마녀 모델을 켠다.
  - 돌아서는 애니메이션은 **공통 시계(`PhotonNetwork.Time`) 기준 진행률**로 직접 정한다.
    - Animator의 `speed`를 0으로 두고, 진행률 = (지금 − 시작) ÷ 타임어택 시간 으로 자세를 맞춘다.
    - 그러면 모든 사람의 화면이 같은 자세이고, 늦게 들어온 사람도 맞춰진다.
- 타이머가 0이 되면:
  - 방장이 `WitchStrike`를 기록한다.
  - 모두의 화면에서 `Slam` 연출이 나온다.
  - 남은 쿠키와 괴물은 본인 클라이언트가 자기 상태를 "마녀에 의해 사망"으로 쓴다. 지금 `HitCount`처럼 본인만 자기 값을 쓰는 원칙을 따른다.
- 타임어택 시간은 방장이 정한 `TimeAttackDuration`을 쓴다(§1.7).
- **방 시간 멈춤 (D15):**
  - 타임어택 중에는 `GameRuleController`가 `GameEndTime`(제한시간 끝) 검사를 하지 않는다.
  - 화면의 제한시간은 스파이가 탈출한 순간의 남은 시간(`GameEndTime − SpyEscapedAt`)으로 멈춰서 보인다.
  - 타임어택 타이머만 줄어든다. 표시 위치: `PhaseCountdownDisplay`에 타임어택 단계 추가.
  - 두 값 모두 `PhotonNetwork.Time` 기준이다. 그래서 방장이 바뀌어도 이어진다.

### 5.8 P9: 게임 끝 판정과 결과 화면

- `GameRuleController`의 판정을 바꾼다. 방장만 판정한다. 게임이 끝나는 경우는 네 가지다.
  1. 남은 쿠키(탈출하지 않고 잡히지 않은 쿠키)가 0명이고, 스파이도 끝났다(탈출, 잡힘, 없음).
  2. 괴물이 남은 쿠키를 모두 잡았다. 타임어택 중이어도 끝난다.
  3. 마녀가 내리쳤다.
  4. 10분이 지났다. 탈출 못 한 쿠키와 스파이는 탈출 실패, 괴물은 잡은 횟수만 표시한다(D8).
- `GameResult`:
  - 지금은 "쿠키 승 / 괴물 승" 두 가지다.
  - 앞으로는 **끝난 이유**로 바꾼다: `AllResolved`, `MonsterCaughtAll`, `WitchStrike`, `TimeUp`.
  - 각 플레이어의 결과는 Player Props(`Escaped`, `HitCount`, `DeathCause`, `CatchCount`)로 계산한다.
- `CatchCount`는 괴물 본인 클라이언트가 처형을 확정할 때 1씩 올린다. 처형 판정 위치: `MonsterGrabKillTrigger`.
- `ResultScreenController`, `PlayerResultRow`:
  - 줄 형식을 바꾼다: 탈출 성공 / 탈출 실패 / 잡은 횟수 n회 / 마녀에 의해 사망.
  - 스파이는 "(Spy)"로 표시한다.
  - 괴물이 이긴 경우에는 쿠키 유리병 트로피 화면을 띄운다.
  - 문구는 인스펙터에서 입력한다. 코드에는 한글을 쓰지 않는다.
- 지금의 "처형 연출이 끝난 뒤 결과 표시" 로직은 그대로 둔다.

### 5.10 코드 스니펫 (§1.3, §1.9~§1.11)

아래는 지금 코드 구조에 맞춘 핵심 부분이다. 실제 구현에서 세부는 달라질 수 있다.

#### 아이템 데이터와 드는 방식 (D31)

```csharp
// Assets/02. Scripts/Escape/ItemSO.cs — 아이템 하나의 데이터. 새 아이템은 에셋만 추가한다.
public enum HoldType
{
    OneHanded, // 오른손. 다른 칸으로 바꿀 수 있다
    TwoHanded, // 두 손. 들고 있는 동안 칸 바꾸기와 줍기가 막힌다
}

public enum ItemCategory { Material, Tool }

[CreateAssetMenu(menuName = "TagOfChaos/Escape/Item", fileName = "Item_")]
public class ItemSO : ScriptableObject
{
    [SerializeField] private string itemId;          // 네트워크로 주고받는 ID(영문)
    [SerializeField] private string displayName;     // 화면 표시 이름(인스펙터에서 한글 입력 가능)
    [SerializeField] private Sprite icon;            // 인벤토리 칸, 오른쪽 위 재료 표시
    [SerializeField] private GameObject heldPrefab;  // 손에 든 모습
    [SerializeField] private HoldType holdType;
    [SerializeField] private ItemCategory category;

    public string ItemId => itemId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public GameObject HeldPrefab => heldPrefab;
    public HoldType HoldType => holdType;
    public ItemCategory Category => category;
    public bool LocksSlotSwitch => holdType == HoldType.TwoHanded;
}
```

#### 인벤토리 (D26, D30, D32)

상태는 본인 클라이언트가 Player Props(`Inventory`, `SelectedSlot`)에 쓴다. 지금 `HitCount`처럼 "본인만 자기 상태를 쓴다" 원칙을 따른다. 아이템의 위치(누가 들고 있는지, 바닥 어디인지)는 방장이 `EscapeItems` 표로 관리한다(§5.2). 그래서 줍기와 떨어뜨리기는 방장에게 요청하고, 방장이 승인하면 인벤토리에 반영한다.

```csharp
// Assets/02. Scripts/Escape/PlayerInventory.cs — 쿠키 프리팹에 붙는다(스파이도 쿠키 프리팹).
public class PlayerInventory : MonoBehaviourPunCallbacks
{
    public const int SlotCount = 4;

    private readonly InventorySlot[] slots = new InventorySlot[SlotCount];
    public int Selected { get; private set; }
    public InventorySlot Held => slots[Selected];
    public bool HandsLocked => !Held.IsEmpty && Held.Item.LocksSlotSwitch;

    public event System.Action Changed; // 핫바 UI, 손 모델 표시가 구독

    private void Update()
    {
        if (!photonView.IsMine) return;
        int wanted = ReadSlotInput(); // 숫자키 1~4, 색칠 단계가 아니면 마우스 휠
        if (wanted >= 0) TrySelect(wanted);
        if (PlayerInput.DropPressed) RequestDrop(Selected);
    }

    public bool TrySelect(int index)
    {
        if (index == Selected || index < 0 || index >= SlotCount) return false;
        if (HandsLocked) return false; // 두 손 아이템을 든 동안에는 바꿀 수 없다(중요 규칙)
        Selected = index;
        Publish();
        return true;
    }

    // 줍기(E). 두 손 아이템을 든 동안에는 줍지 못하고, 두 손 아이템은 한 개만 가질 수 있다.
    public bool CanPickUp(ItemSO item)
    {
        if (HandsLocked) return false;
        if (item.LocksSlotSwitch && System.Array.Exists(slots, s => !s.IsEmpty && s.Item.LocksSlotSwitch)) return false;
        return FirstEmptySlot() >= 0;
    }

    // 방장이 줍기를 승인하면 호출된다. 빈손이면 지금 칸에, 아니면 첫 빈 칸에 넣는다.
    // 두 손 아이템은 바로 손에 든다(들고 가는 모습이 보여야 하므로).
    public void AddApproved(ItemSO item, int charges)
    {
        int index = Held.IsEmpty ? Selected : FirstEmptySlot();
        slots[index] = new InventorySlot(item, charges);
        if (item.LocksSlotSwitch) Selected = index;
        Publish();
    }

    // 손에 든 것이 사라졌을 때(떨어뜨림, 설치, 로켓, 횟수 소진) 다음 칸부터 찾아 바로 든다(D32).
    // 예: 1·3·4번에 아이템, 2번이 빔 → 1번을 떨어뜨리면 3번을 든다.
    public void RemoveHeldAndAutoEquip()
    {
        slots[Selected] = InventorySlot.Empty;
        for (int step = 1; step < SlotCount; step++)
        {
            int next = (Selected + step) % SlotCount;
            if (slots[next].IsEmpty) continue;
            Selected = next;
            break;
        }
        Publish();
    }

    private int FirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++) if (slots[i].IsEmpty) return i;
        return -1;
    }

    private void Publish()
    {
        Changed?.Invoke();
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { NetKeys.Inventory, InventoryCodec.Encode(slots) }, // 아이템 ID와 남은 횟수
            { NetKeys.SelectedSlot, Selected },
        });
    }
}
```

- `InventorySlot`은 (아이템, 남은 횟수)를 담는 작은 구조체다. `IsEmpty`는 아이템이 없을 때 true다.
- 손 모델은 `HeldItemPresenter`가 모든 클라이언트에서 `SelectedSlot`과 `Inventory`를 보고 `CarriedItemSocket`(한 손은 오른손, 두 손은 가운데)에 붙인다. 모델은 풀에서 꺼내 쓴다.
- 입력 추가(`PlayerInput`, `InputBindingsSO`):

```csharp
public static bool DropPressed => !IsGameplaySuppressed && Input.GetKeyDown(Bindings.DropKey);
public static int SlotKeyPressed // 1~4 → 0~3, 없으면 -1
{
    get
    {
        if (IsGameplaySuppressed) return -1;
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
        return -1;
    }
}

[Tooltip("손에 든 아이템 떨어뜨리기.")]
[SerializeField] private KeyCode dropKey = KeyCode.G;
public KeyCode DropKey => dropKey;
```

#### 역할별 상호작용 걸러내기 (D33, D35)

지금 `IInteractable.CanInteract(IGameCharacter)`가 사물 쪽 조건을 판단한다. 새 사물들은 여기서 역할을 거른다. 스파이 여부는 `EscapeRoles.IsSpy(actor)`로 확인한다(본인과 방장만 알 수 있다, §5.9).

```csharp
// MaterialChest, GroundItem(바닥 아이템) — 괴물은 줍기·상자 열기 불가(D33).
public bool CanInteract(IGameCharacter character) =>
    character.Role == CharacterRole.Cookie && character.gameObject.GetComponent<PlayerInventory>().CanPickUp(ContainedItem);

// EscapeExit(탈출구) — 괴물에게는 아이콘도 안 뜬다. 스파이는 아이콘은 뜨지만 들어갈 수 없다(D35).
public bool CanInteract(IGameCharacter character) => isOpen && character.Role == CharacterRole.Cookie;

public void Interact(IGameCharacter character)
{
    if (EscapeRoles.IsLocalSpy())
    {
        Toast.Show(cannotEnterText); // "들어갈 수 없음" — 쿠키와 같은 아이콘이라 겉으로는 구별되지 않는다
        return;
    }
    RequestEscape(); // 방장 승인 → 본인이 Escaped=1을 쓰고 캐릭터를 숨긴 뒤 관전 시작
}
```

```csharp
// 탈출 승인 후(본인 클라이언트) — 기존 관전 모드를 그대로 쓴다(D36).
private void OnEscapeApproved()
{
    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.Escaped, 1 } });
    inventory.DropAllApproved();                                // 들고 있던 것은 탈출구 앞에 남긴다
    GetComponent<SpectatorController>()?.EnterSpectatorMode(); // 대상: 살아 있는 쿠키(스파이 포함)
    HideSelf();                                                 // 렌더러·콜라이더 끄기
}
```

- 탈출한 쿠키의 `IsSpectatable`은 false가 되어야 한다. `HideOrSeekPlayer.IsSpectatable`에 "탈출하지 않았음" 조건을 더한다.

#### 스파이 잡힘 알림과 로켓에 끼움 알림 (D34, D40)

스파이가 누구인지는 방장만 안다. 그래서 알림은 방장이 보낸다.

```csharp
// SpyAuthority(방장) — 스파이의 HitCount가 파괴로 바뀌면 모두에게 알린다.
public override void OnPlayerPropertiesUpdate(Player target, Hashtable changed)
{
    if (!PhotonNetwork.IsMasterClient || !changed.ContainsKey(NetKeys.HitCount)) return;
    if (!EscapeRoles.IsSpy(target.ActorNumber) || !RoomState.IsBroken(target)) return;
    PhotonNetwork.RaiseEvent(NetEventCodes.SpyCaught, null,
        new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
}

```

```csharp
// EscapeItemAuthority(방장) — 로켓 끼우기를 승인할 때(D40). 장치에서 훔친 재료만 알린다.
private void ApproveRocketInsert(ItemRecord record, int spyActor)
{
    rocket.Fill(record.Item, spyActor);
    bool announce = record.StolenFromDevice;       // 훔칠 때 true로 기록, 다시 생기면 false로 되돌림
    RespawnInRandomEmptyChest(record);             // §1.4: 빈 일반 상자에 닫힌 모습으로 다시 생긴다
    record.StolenFromDevice = false;
    WriteItemsTable();
    if (announce)
        PhotonNetwork.RaiseEvent(NetEventCodes.StolenItemToRocket, record.Item.ItemId,
            new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
}

// 장치에서 훔치기를 승인할 때 기록만 해 둔다. 이때는 알림을 보내지 않는다(D37).
private void ApproveSteal(ItemRecord record, int spyActor)
{
    record.StolenFromDevice = true;
    record.HolderActor = spyActor;
    // ... 장치 칸 비우기, EscapeItems 쓰기 → 모두의 재료 표시가 줄어든다
}
```

```csharp
// EscapeToastUI — 화면 가운데 문구가 떴다가 서서히 사라진다.
[SerializeField] private string spyCaughtText;         // 인스펙터: 스파이가 잡혔습니다
[SerializeField] private string rocketFormatWithFinal; // 인스펙터: 받침 있을 때 "스파이가 {0}을 훔쳐 로켓에 끼워넣었습니다"
[SerializeField] private string rocketFormatNoFinal;   // 인스펙터: 받침 없을 때 "스파이가 {0}를 훔쳐 로켓에 끼워넣었습니다"
[SerializeField] private float holdSeconds = 1.5f;
[SerializeField] private float fadeSeconds = 1.5f;

public void ShowStolenToRocket(ItemSO item)
{
    string format = KoreanText.HasFinalConsonant(item.DisplayName) ? rocketFormatWithFinal : rocketFormatNoFinal;
    Show(string.Format(format, item.DisplayName));
}

private IEnumerator FadeRoutine()
{
    group.alpha = 1f;
    yield return new WaitForSeconds(holdSeconds);
    for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
    {
        group.alpha = 1f - t / fadeSeconds;
        yield return null;
    }
    group.alpha = 0f;
}
```

```csharp
// KoreanText.cs — 코드에 한글을 쓰지 않고 받침 여부를 판단한다(유니코드 한글 음절 계산).
public static class KoreanText
{
    private const int SyllableStart = 0xAC00;
    private const int SyllableEnd = 0xD7A3;
    private const int FinalCount = 28;

    public static bool HasFinalConsonant(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        char last = word[word.Length - 1];
        if (last < SyllableStart || last > SyllableEnd) return false; // 한글이 아니면 받침 없음으로 본다
        return (last - SyllableStart) % FinalCount != 0;
    }
}
```

#### 오른쪽 위 필요한 재료 표시 (D37)

```csharp
// RequirementHud — Props(EscapeRecipe, EscapeItems, RocketState)가 바뀔 때만 다시 계산한다(매 프레임 X).
public override void OnRoomPropertiesUpdate(Hashtable changed)
{
    if (changed.ContainsKey(NetKeys.EscapeItems) || changed.ContainsKey(NetKeys.RocketState)
        || changed.ContainsKey(NetKeys.EscapeRecipe)) Rebuild();
}

private void Rebuild()
{
    // 스파이는 로켓 재료, 그 외는 탈출 장치 재료.
    IReadOnlyList<Requirement> list = EscapeRoles.IsLocalSpy() ? RocketRequirements.Current() : EscapeRequirements.Current();
    for (int i = 0; i < rows.Count; i++) rows[i].gameObject.SetActive(i < list.Count);
    for (int i = 0; i < list.Count; i++)
        rows[i].Set(list[i].Item.Icon, list[i].Item.DisplayName, list[i].Installed, list[i].Required); // "기어 0/1"
}
```

- 줄(`RequirementRow`)은 미리 5개 만들어 두고 켜고 끈다. 필요한 재료 종류는 최대 4가지다.
- "수리"처럼 칸 이름이 아이템 이름과 다르면 `EscapeRecipeSO`의 칸 표시 이름을 쓴다.

#### 마녀 서서히 나타나기 (D38)

Built-in 렌더 파이프라인이므로 마녀 머티리얼은 Standard 셰이더의 `Fade` 모드 복사본을 따로 둔다. 다 나타나면 원래(불투명) 머티리얼로 바꿔 정렬 문제와 비용을 없앤다.

```csharp
// WitchPresenter.cs
private static readonly int ColorId = Shader.PropertyToID("_Color");
[SerializeField] private Renderer[] renderers;
[SerializeField] private Material[] fadeMaterials;   // Standard(Fade) 복사본
[SerializeField] private Material[] opaqueMaterials; // 원래 머티리얼
[SerializeField] private float fadeInSeconds = 5f;

private MaterialPropertyBlock block;
private bool fadeFinished;

private void Update()
{
    if (!RoomState.TryGetDouble(NetKeys.SpyEscapedAt, out double escapedAt)) return;

    float elapsed = (float)(PhotonNetwork.Time - escapedAt); // 모두 같은 시계라 화면이 맞는다
    if (!fadeFinished)
    {
        float alpha = Mathf.Clamp01(elapsed / fadeInSeconds); // 0 → 1 (= 0 → 255)
        block ??= new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
            r.SetPropertyBlock(block);
        }
        if (alpha >= 1f) SwapToOpaque();
    }
    UpdateTurnPose(elapsed); // 돌아서기 진행률(§5.7)
}
```

- 처음 켜질 때는 `fadeMaterials`로 바꾸고 시작한다. 늦게 들어온 사람도 `elapsed`로 바로 맞는 알파가 된다.

#### 결과 화면 줄 (D39)

```csharp
// PlayerResultRow.cs — 기존 SetMonster/SetCookie를 역할과 결과로 넓힌다. 문구는 모두 인스펙터 입력.
[SerializeField] private string cookieRoleLabel;   // (쿠키)
[SerializeField] private string spyRoleLabel;      // (스파이)
[SerializeField] private string monsterRoleLabel;  // (괴물)
[SerializeField] private string escapedLabel;      // 탈출 성공
[SerializeField] private string failedLabel;       // 탈출 실패
[SerializeField] private string catchFormat;       // 잡은 횟수 {0}회
[SerializeField] private string witchKilledLabel;  // 마녀에 의해 사망

public void SetEscaper(string nickname, bool isSpy, bool escaped)
{
    nameText.text = nickname + (isSpy ? spyRoleLabel : cookieRoleLabel);
    statusText.text = escaped ? escapedLabel : failedLabel;
}

public void SetMonster(string nickname, int catches, bool killedByWitch)
{
    nameText.text = nickname + monsterRoleLabel;
    statusText.text = killedByWitch ? witchKilledLabel : string.Format(catchFormat, catches);
}
```

- 스파이 정체는 게임이 끝난 뒤 방장이 `GameResult`와 함께 스파이 번호를 Room Props에 공개한다. 그때부터 결과 화면이 스파이를 표시할 수 있다.

### 5.9 알려진 제한

- **스파이 정보가 노출될 수 있다.**
  - Photon에는 서버 로직이 없어서, 스파이 정보를 Room Props에 두면 조작한 클라이언트가 읽을 수 있다.
  - 1차 구현은 스파이 본인과 방장에게만 이벤트로 알려서 Props에는 남기지 않는다.
  - `Inventory`는 Player Props라서 조작한 클라이언트가 볼 수 있다. 다만 도구는 손에 들면 어차피 겉으로 보인다. 같은 한계로 받아들인다.
  - 결과 화면에서는 판이 끝난 뒤에 공개한다.
  - 방장은 스파이를 알 수 있다. 완전히 막으려면 전용 서버가 필요하므로 범위 밖이다.
- **맵이 좁아질 수 있다.** 140m 맵에 상자 20자리와 장치를 넣다 보면 기존 소품을 빼야 할 수 있다. P3 통행 검사로 확인한다.

---

## 6. 검증 (P10)

- **EditMode 테스트:**
  - 인원표: 4~8명일 때 괴물·스파이·필요 재료 수
  - 레시피: 인원별 칸 구성, 고정 재료 포함, 늘어나는 칸 수, 고르지 않은 칸은 채워진 상태
  - 훔치기: 장치에서 빼면 장치 진행이 줄어드는지, 로켓에 끼운 재료가 빈 일반 상자에 닫힌 모습으로 다시 생기는지
  - 공구상자: 로켓의 아무 슬롯이나 채우는지
  - 로켓 탑승: 스파이 1명과 2명일 때, 한 명이 나갔을 때
  - 도구: 맞으면 손에 든 아이템 하나만 떨어뜨리는지, 괴물도 세 도구에 기절하는지, 횟수와 쿨다운, 물풍선 색칠
  - 인벤토리: 4칸 제한, 두 손 아이템은 1개만, 두 손 아이템을 든 동안 칸 바꾸기와 줍기가 막히는지, 칸이 가득 차면 주울 수 없는지
  - 자동으로 들기: 1·3·4번에 아이템이 있을 때 1번을 떨어뜨리면 3번을 드는지, 모두 비면 빈손인지
  - 역할별 상호작용: 괴물은 문만, 탈출구는 쿠키만(스파이는 거부 문구), 스파이 로켓은 스파이만
  - 받침 판단(`KoreanText.HasFinalConsonant`): 받침 있는 말, 없는 말, 영문
  - 로켓에 끼움 알림: 장치에서 훔친 재료는 끼울 때만 알림이 뜨고(훔칠 때는 안 뜸), 상자·바닥에서 주운 재료와 공구상자는 뜨지 않는지
  - 필요한 재료 표시: 끼우고 훔칠 때 수가 맞게 바뀌는지, 쿠키와 스파이가 서로 다른 목록을 보는지
  - 마녀 알파: 스파이 탈출 후 0초·2.5초·5초에 0·0.5·1인지
  - 떨어뜨리기: 잡혔을 때와 나갔을 때 모든 아이템이 떨어지는지
  - 장치에서 훔치기: 로켓에 필요한 종류만 뺄 수 있는지
  - 스파이 상자: 쿠키에게는 "잠겨 있음"이고 열리지 않는지, 스파이 수 × 2개가 스턴건·물풍선 한 쌍씩 생기는지
  - 상자 자리 수: (필요 재료 × 3) + (스파이 수 × 2)가 20곳을 넘지 않는지. 넘는 인원 규칙이 생기면 상자 자리를 늘려야 한다
  - 게임 단계 판정: `TimeAttack` 포함
  - 방 설정 범위: 인원 4~8, 제한시간 10~40분, 타임어택 1~10분, 범위 밖이면 경고와 가장 가까운 값, 숫자 외 입력 막기
  - 타임어택 중 제한시간이 끝나도 게임이 끝나지 않는지
  - 상자 이름: 보는 사람(쿠키·괴물·스파이)에 따라 맞게 나오는지
  - 끝 판정: 네 가지 경우
  - 결과 줄 계산
  - `NetKeys` 표 누락 검사
- **맵 테스트:** `ESC_*` 오브젝트와 상자 자리가 모두 있는지, 상호작용 위치까지 걸어갈 수 있는지
- **Play Mode:**
  - 오프라인 모드에서 `MapPlaytestDriver`를 넓혀 확인한다.
  - 재료 줍기 → 설치 → 탈출 → 스파이 로켓 → 타임어택 → 마녀 → 결과 화면까지 한 번에 확인한다.
  - 맵 5개 모두 확인한다.
- 작업할 때마다 컴파일 오류와 Console 오류·경고를 확인한다.
- 단계를 끝내면 이 문서의 상태 표시를 ✅로 바꾼다.

---

## 7. 작업 결과 (P1~P10 완료)

### 7.1 P1: Blender 에셋
- 스크립트: `Assets/Maps/Source~/Scripts/escape_assets.py` (Blender 5.2, 백그라운드 실행). 원본: `Assets/Maps/Source~/Escape/TagOfChaos_Escape.blend`.
- 결과 FBX 31개: `Assets/09. Environment/Escape/Models/`
  - 아이템 19개 `ITEM_<ItemId>`: 레드 버튼, 안전벨트, 배터리, 공구상자, 기어, 마카롱 바퀴, 쿠키 바퀴, 초콜릿 원유, 룬 4색, 알사탕 전지 4색, 뿅망치, 스턴건, 물풍선
  - 상자 `CHEST` (Body + Lid, 뚜껑 경첩 기준)
  - 스파이 로켓 5종 `SPY_Rocket_<Map>` (맵마다 색·무늬가 다름, Slot_00/01)
  - 탈출 장치 5종 `ESC_<Map>`: 롤러코스터 카트, 반죽 기계, 초콜릿 기차(바퀴 자리 = 바퀴 칸), 룬 제단(+ESC_Glow), 케이크(→ 조각 + 케이크 로켓)
  - 마녀 `WITCH` (약 90 m, SlamArm 어깨 축)
- Unity 연결: `EscapeModelImportPostprocessor`(임포트 규칙), `EscapeModelBuilder`(메뉴 Tools/TagOfChaos/Escape/Build Models: 머티리얼 꺼내기·색 맞추기, 아이템 SO에 모델 연결, 맵 배치 다시 만들기). 모델이 없으면 기존 임시 도형을 쓴다.
- 계획과 다른 점: 마녀는 뼈대 대신 **부품 계층(SlamArm 축)** 으로 만들고, 돌아서기·내려치기는 코드가 공통 시계(PhotonNetwork.Time)로 움직인다. 늦게 들어온 사람도 같은 자세가 되고, 애니메이션 클립 동기화가 필요 없다.

### 7.2 P2: 배치와 진저브레드 유적 마을
- **배치(장치·로켓·상자 20곳·마녀)는 Blender가 아니라 Unity `EscapeMapSetup`이 한다.** 맵을 축소할 때마다 자동으로 다시 만들고, 걸을 수 있는 땅·주변 여유 공간을 직접 재서 고르므로 통행 검사와 항상 맞는다.
  - 장치 주변 여유 6.5 m, 로켓 5 m: 장치 옆에 쿠키만 들어가는 좁은 틈(괴물이 못 오는 곳)이 생기지 않게 했다.
  - 여유 검사를 높은 캡슐에서 **땅 높이 원기둥(0.3~4 m)** 으로 바꿨다(반지름이 커도 정확).
  - 장치 충돌체는 모델 모양 그대로(MeshCollider). 레일은 괴물이 넘을 수 있는 높이(0.12 m), 룬 제단 받침은 경사로라 괴물도 오른다.
- 진저브레드: `assets.py`에 무너진 과자 집 3종(`GingerRuin_Cottage/Tall/Twin`)과 잔해(`RuinRubble`)를 추가했다. 일반 집의 약 2/3가 무너진 집이 된다(저주받은 집은 그대로).
  - 무너진 집 아래 3.4 m는 막힌 덩어리라 들어가거나 오를 수 없다. 그 위에 부서진 벽과 내려앉은 지붕이 있다.
  - 무너질 집을 고르는 난수는 따로 써서 나머지 배치는 바뀌지 않는다.
  - 축소 후 결과: 집 27채 중 무너진 집 12채.
- 다시 빌드 도구: `rebuild_map.py` (Blender를 창과 함께 열어 빌드 → 내보내기 → 저장 → 종료). `export_unity.py`의 프로젝트 경로는 스크립트 위치에서 계산한다.

### 7.3 P3: 다시 빌드와 검사
- 진저브레드: `MapSceneBuilder.Build` → 자동 축소 → 어두운 곳 채우기 → 탈출 배치까지 한 번에 다시 만들었다.
- 통행 검사(`MapPassabilityCheck`): 맵 5개 모두 깨끗함(갇힌 곳 0, 쿠키만 가는 곳 0, 스폰 연결).
- 함께 고친 것: 머티리얼이 없는 FBX의 `.meta`에 `externalObjects:`가 빈 값으로 써져 YAML 오류가 나던 문제(`*_Effects.fbx.meta` 5개, `export_unity.py`).

### 7.4 P10: 테스트와 Play Mode
- 테스트: EscapeTests 32/32, RuleTests 16/16, MapCompactTests 20/20 통과. 이번에 추가한 테스트:
  - 모든 아이템에 모델이 연결됐는지
  - 맵마다 장치·로켓(칸 2개)·상자 20곳(뚜껑)·마녀 모델이 있는지
  - 쿠키 스폰에서 장치와 로켓까지 걸어갈 수 있는지
- Play Mode(오프라인)에서 확인한 것:
  - 상자 열기(뚜껑 모델), 두 손 아이템 들기, 슬롯 잠금, 떨어뜨리기, 자동 장착
  - 장치 설치·완성 연출: 캔디숲 케이크 → 로켓, 진저브레드 무지개빛, 공장 바퀴 칸
  - 탈출: 숨김·관전, 결과 "닉네임(쿠키) 탈출 성공", 쿠키 승
  - 스파이: 스파이 상자 2개(스턴건·물풍선), 일반 상자에서 꺼내기, 장치에서 훔치기 → 로켓 끼움 알림, 공구상자가 아무 칸에나 맞음, 재료가 빈 상자에 닫힌 채 다시 생김, 탑승 → 출발
  - 도구: 뿅망치로 괴물 기절, 물풍선 기절·손에 든 아이템만 떨어뜨림·색칠
  - 타임어택: 방 시간이 멈추고 빨간 타이머, 마녀가 5초에 걸쳐 나타나 돌아서고 내려침 → 남은 쿠키 사망, 결과 "탈출 실패"
  - 스파이 잡힘 알림, 괴물 승리 화면(쿠키 유리병, "잡은 횟수 n회 / 마녀에 의해 사망")
  - 맵 5개 모두 탈출 상태 시작, Console 오류·경고 0
- 검증 중 고친 버그:
  - 로켓 탑승 뒤 출발 판정을 부르지 않던 문제(`RocketBoard` → `TryLaunchRocket`)
  - 쿠키가 모두 끝났는데 로켓이 떠나면 마녀 알림이 뜨던 문제(D10: 타임어택 없이 끝냄)
  - 탈출·탑승한 몸에 속도를 넣어 경고가 나던 문제(이동 잠금에 탈출 포함)
  - 결과 화면 위에 HUD·조준점이 겹치던 문제, 변장 단계에 가방 칸이 색 팔레트와 겹치던 문제
  - 결과 줄 한글 문구(프리팹), 괴물 줄 한 줄 표시, 유리병 트로피 위치·한글 글꼴
  - 캔디숲 완성 빛 기둥이 케이크 로켓을 가리던 문제
- 오프라인 테스트 한계: 혼자서는 다른 사람이 쿠키·괴물인 상황을 만들 수 없어, 일부 장면은 방 속성(스파이·괴물 번호, 타임어택 시각)을 직접 넣어 확인했다. 실제 여러 명 접속 테스트는 남아 있다.

