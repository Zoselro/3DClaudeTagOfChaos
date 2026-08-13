# 색상 결정 & 술래 지정 시스템 설계 (GameScenePlan.md) ✅ 구현 완료 (13장까지 전부 반영)

> 상태: **구현 완료** (2026-08-14). 0~9장(최초 구현)에 이어, 10장(비주얼 대비 문제), 11장(페인팅
> 클릭 위치 고정 버그 → `MeshCollider` 교체), 12장(3D 붓 커서), 13장(Photon 콜백 등록 누락 버그)까지
> 전부 실제 코드/프리팹에 반영하고 Play Mode에서 재검증했다. **10.1(원래의 2D 붓 커서 텍스처 계획)은
> 12장의 3D 붓 커서로 완전히 대체되어 더 이상 유효하지 않다** — `BrushSettingsSO.cursorTexture` 필드
> 자체를 제거했다. 원래 이 문서는 `UserPlan.md` 요구사항 + 주석(미결정 사항 답변)을 반영해 작성한 설계
> 문서였고, 마지막 설계 개정에서 달라진 부분은 "마우스 휠 한 틱당 붓 크기 변화폭(`wheelStep`)
> `0.002`로 확정"이었다. (이전 개정: "붓 크기 수치 모순 해소(0.005 / 0.1 / 0.02)", "페인팅을
> `PhotonNetwork.RaiseEvent`로 전 클라이언트에 실시간 공유", "칠한 부위는 잠겨서 다른 색으로 덮어쓸 수
> 없는 방식으로 페인팅 룰 변경", "라운드 도중 신규 입장 차단 확정", "`hide_or_seek_player` 정식 프리팹
> 승격 반영")

---

## 0. 설계 해석 (반드시 검토 필요) ✅ 전부 확인 완료, 반영됨

### 0.1 확정된 해석 (1차 주석 답변)

1. **술래 색 노출 범위** — 술래의 변형 색상은 게임 시작부터 끝까지 **항상 모든 플레이어에게 보임**. 특정 연출 시점에만 반짝 공개되는 것이 아니라, 술래가 정해진 순간부터 계속 다른 플레이어 눈에 보이는 상태로 유지됩니다. → 6.3(최종 색상 적용) 설계가 이 해석과 일치하므로 그대로 유지합니다.
2. **색상 적용 대상 = 직접 페인팅** — "머리 위 표시 + 최종 색상 적용"에 더해, 라운드가 진행되는 동안 **자신의 캐릭터를 직접 붓으로 칠하는 인터랙션**이 추가됩니다.
   - 색상 결정 라운드(1~4라운드)가 시작될 때마다 마우스 커서가 붓 아이콘으로 바뀝니다.
   - **자신의 `hide_or_seek_player` 오브젝트만** 칠할 수 있습니다. (다른 플레이어 오브젝트는 클릭해도 반응 없음)
   - 좌클릭 시, 마우스 포인터가 가리키는 그 오브젝트 표면 위치에 색이 칠해집니다. (레이캐스트 히트 지점 기준 텍스처 페인팅)
3. **4색 중복 금지** — 라운드 1~4는 서로 다른 색이어야 합니다. 특정 라운드에서 이미 확정된 색은 다음 라운드의 팔레트 선택지에서 제외됩니다. 술래의 변형 색상 역시 이미 뽑힌 4색과 겹칠 수 없습니다. (예: 초록/주황/노랑/파랑이 확정됐다면, 술래 변형색은 이 4색을 제외한 나머지 6색 중에서만 뽑힘)
4. **술래 퇴장 처리** — 술래로 확정된 플레이어가 방을 나가면, 그 즉시 **방을 폭파(종료)**하고 남은 인원 전원 `LobbyScene`으로 돌아갑니다.
5. **인원 감소 처리** — 4인 중 일부가 나가는 것은 정상 진행되지만, **1명만 남는 순간** 방을 폭파하고 `LobbyScene`으로 돌아갑니다. (이 규칙은 색상 선택 페이즈든 본게임이든 상관없이 동일하게 적용)

### 0.2 페인팅 기능 관련 추가 해석 (제 판단으로 채운 부분 — 전부 0.4/0.5에서 확인 완료)

- ~~붓 색상 선택 방법~~ → **0.5-14에서 확정**(스와치로 붓 색을 고른 뒤 캐릭터를 클릭해 칠하는 2단계 흐름 그대로 맞음).
- ~~페인팅의 실시간 공유 여부~~ → **0.4-10에서 확정**(RaiseEvent로 전 클라이언트에 공유).
- ~~라운드가 끝난 뒤 캐릭터 페인팅 흔적 처리~~ → **0.5-15에서 확정**(가정이 맞았음 — 다만 "캔버스 전체 초기화"가 아니라 "그 라운드에 칠한 부위만 확정색으로 재도색"이라는 구체적 메커니즘으로 확정).
- ~~캐릭터의 어느 부위에 4색이 최종적으로 나타나는지~~ → **0.5-16에서 확정**(정해진 신체 부위 매핑 없이, 각 라운드에 플레이어가 직접 클릭해서 칠한 자리에 그대로 나타남).

### 0.3 이전 개정에서 반영한 해석 (2차 주석 답변)

6. **재대결 개념 삭제** — "다음 판"이라는 개념 자체를 없앱니다. 대신 게임이 끝나면 **20초 뒤 플레이어 전원이 함께 `GameLobbyScene`으로 이동**합니다. `GameLobbyScene`은 이 문서 범위 밖이며 추후 별도로 구현합니다. → 기존 8장 "재대결 처리"를 통째로 삭제하고, 7장(생명주기)에 "게임 정상 종료 처리"로 재구성했습니다.
7. **붓 크기** — 최소 `0.005`, 최대 `0.02`, 기본값 `0.1`, 마우스 휠로 조절.
   - ~~확인 필요(수치 모순)~~ → **0.4-9에서 최종 확정**(최소 `0.005` / 최대 `0.1` / 기본 `0.02`로 재확정, 모순 해소).
   - ~~마우스 휠 한 틱당 변화폭(step)~~ → **0.6-17에서 확정**(`0.002`).
8. **붓 커서 노출 조건** — 색을 칠하는 라운드(1~4라운드, `RoundIndex` 0~3)일 때만 마우스 포인터가 붓 아이콘으로 바뀌고, 그 외에는 기본 포인터로 돌아갑니다. → 기존 5.1 `BrushCursorController` 설계와 동일하므로 그대로 확정합니다.

### 0.4 이번 개정에서 반영한 해석 (3차 주석 답변)

9. **붓 크기 수치 모순 해소** — 0.3-7에서 지적했던 모순(기본값이 최대값보다 큼)이 새 값으로 해소됐습니다: **최소 `0.005`, 최대 `0.1`, 기본값 `0.02`**. `0.02`는 `[0.005, 0.1]` 범위 안에 들어오므로 더 이상 시작하자마자 클램프되는 문제가 없습니다. → 5.3절 `BrushSettingsSO` 반영.
10. **페인팅 실시간 공유 = RaiseEvent 도입 확정** — 0.2에서 "로컬 전용"으로 가정했던 부분을 뒤집습니다. `PhotonNetwork.RaiseEvent`를 이번에 처음 도입해서, 내가 칠한 붓질(스트로크)이 다른 모든 플레이어 화면에도 실시간으로 보이게 합니다. → 4.1(왜 RaiseEvent를 안 쓰는지 → 예외 인정)과 5.2(구현) 갱신.
11. **페인팅 방식 = "칠한 부위 잠금" 규칙 추가** — 내 캐릭터에 마우스를 대고 좌클릭하면 그 부위가 칠해지되, **이미 칠해진 부위는 다른 색으로 덮어칠할 수 없습니다.** 기존 5.2의 자유형 UV 브러시 페인팅 자체는 유지하되, 이미 색이 채워진 픽셀(캔버스 알파값으로 판별)에는 새 스탬프가 영향을 주지 않도록 규칙을 추가합니다. → 5.2 갱신.
12. **라운드 도중 신규 입장 차단 확정** — 7.1에서 "권장"으로만 적어뒀던 `PhotonNetwork.CurrentRoom.IsOpen = false` 처리를 확정합니다. → 4.2/7.1 갱신.
13. **`hide_or_seek_player` 정식 프리팹 승격 반영** — `PlayerControllPlan.md` 13.9에서 확정된 대로, 이 문서의 페인팅/투표/색상 관련 컴포넌트들도 전부 `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab` 하나에 부착되는 것으로 명시합니다. → 2장/5.2/6.2 갱신.

### 0.5 이번 개정에서 반영한 해석 (4차 주석 답변)

14. **붓 색상 선택 방법 확정** — 0.2에서 가정했던 "스와치 클릭 → 붓에 색 담김 → 캐릭터 클릭해서 칠하기" 2단계 흐름이 맞다고 확인받았습니다. 5.2/6.1 설계 변경 없음(그대로 유지).
15. **라운드/최종 페인팅 흔적 처리 — 구체적 메커니즘 확정** — 0.2의 가정("낙서 정리 후 확정색으로 덮어씌움")도 맞다고 확인받았는데, 16번(부위가 클릭한 자리에 그대로 나타남)과 앞뒤가 맞으려면 "라운드가 넘어갈 때 캔버스 전체를 지운다"가 아니라 **"그 라운드에 내가 칠했던 자리만, 확정된 그 라운드의 색으로 다시 칠해진다"**는 뜻으로 이해해야 앞뒤가 맞습니다. 즉:
    - 라운드 중에는 스와치로 고른 후보색으로 임시로 칠하고(투표를 시각적으로 표현),
    - 그 라운드가 다수결/랜덤으로 확정되는 순간, **내가 그 라운드에 칠했던 자리들만** 골라 확정색으로 다시 칠합니다("낙서 정리" = 개인이 고른 후보색은 사라지고, "확정색으로 덮어씌움" = 같은 자리에 라운드 공식 색이 대신 채워짐).
    - 이전 라운드에 이미 확정되어 잠긴 자리(0.4-11)는 건드리지 않습니다 — 이번 라운드에 새로 칠한 자리만 대상입니다.
    - 자세한 구현은 5.2 참고.
16. **색상이 나타나는 위치 확정 — 클릭한 자리 그대로, 고정된 신체 부위 매핑 없음** — `PlayerControllPlan.md`에서 이미 답변하신 대로, "붓을 갖다 댄 상태로 좌클릭하면 그 부위가 칠해지는" 방식 그 자체가 "색이 어디에 나타나는가"에 대한 답이기도 합니다. 즉 아트팀이 미리 "라운드 1 색은 머리, 라운드 2 색은 몸통" 같은 고정 매핑을 정해두는 게 아니라, **각 라운드마다 플레이어가 직접 클릭한 자리에 그 라운드의 확정색이 남는 것**이 곧 "부위별 배치"입니다. 4라운드가 끝나면 캐릭터마다 자신이 그동안 클릭한 위치에 따라 최대 4가지 색 패치가 자연스럽게 쌓인 모습이 되고, 플레이어마다 같은 4색이라도 배치는 서로 다를 수 있습니다. → 6.3의 "실제 적용 대상 미정" 문구를 이 내용으로 대체.

### 0.6 이번 개정에서 반영한 해석 (5차 주석 답변)

17. **마우스 휠 한 틱당 변화폭 확정** — 0.3-7에서 튜닝 가능한 파라미터로 미정 처리해뒀던 `wheelStep`을 **`0.002`**로 확정합니다. 5.3절 `BrushSettingsSO`가 이미 기본값으로 `0.002`를 쓰고 있었는데, 이제 이 값이 "임시 기본값"이 아니라 **확정값**입니다.

---

## 1. 게임 플로우 개요 ✅

```
[대기실 입장 2~4인]
        │
        ▼
[색상 결정 페이즈] ────────────────────────────────────────┐
  Round 1 (20초)                                            │
    - 마우스 커서 → 붓 아이콘 (마우스 휠로 붓 크기 조절 가능)      │  MasterClient 권위로 진행
    - 팔레트에서 색 선택(=투표) → 내 캐릭터를 클릭해 페인팅      │  (타이머는 PhotonNetwork.Time 기준 동기화)
    - 시간 종료 시 다수결/랜덤으로 1번째 색 확정 (남은 라운드 팔레트에서 제외)│
  Round 2 (20초) → 동일 흐름, 1번째 색 제외된 팔레트 → 2번째 색 확정 │
  Round 3 (20초) → 동일 흐름, 1~2번째 색 제외된 팔레트 → 3번째 색 확정│
  Round 4 (20초) → 동일 흐름, 1~3번째 색 제외된 팔레트 → 4번째 색 확정│
        └────────────────────────────────────────────────────┘
        ▼
[술래 지정 페이즈]
  MasterClient가 랜덤 1인을 술래로 선정
  술래에게만 "확정 4색과 겹치지 않는 색으로 1슬롯이 치환된" 변형 4색 세트 부여
  → 이 순간부터 게임이 끝날 때까지 모든 플레이어에게 항상 보임
  → 마우스 커서는 기본 포인터로 복귀 (색상 라운드 종료)
        ▼
[태그(술래잡기) 게임 시작] ← 기존 HideOrSeekPlayer 로직과 연결 (승패 판정은 이 문서 범위 밖)
        ▼
[게임 정상 종료] → GameEndTime = PhotonNetwork.Time + 20 기록
        ▼
  20초 대기 (전 클라이언트가 동일한 PhotonNetwork.Time 기준으로 카운트)
        ▼
[전원 GameLobbyScene 이동] ← GameLobbyScene은 추후 별도 구현 (7.3 참고)
```

방/라운드 진행 중 언제든 아래 두 조건 중 하나면, 20초 대기 없이 **즉시** 방 종료 → `LobbyScene` 복귀 (7.2 참고, 정상 종료의 `GameLobbyScene`과는 다른 씬입니다):
- 술래로 확정된 플레이어가 퇴장
- 남은 인원이 1명이 됨

---

## 2. 폴더/파일 구조 (CLAUDE.md 규칙 준수) ✅ 구현 완료 (실제 구조는 9장 참고, 일부 파일 추가됨)

```
Assets/02. Scripts/ColorTag/
  ├─ ColorPaletteSO.cs            # 팔레트 정의 SO (10색)
  ├─ NetKeys.cs                   # Room/Player CustomProperties 키 상수
  ├─ NetEventCodes.cs             # PhotonNetwork.RaiseEvent용 이벤트 코드 상수 (신규, 0.4-10)
  ├─ ColorSelectionManager.cs     # MonoBehaviourPunCallbacks, 라운드 진행 총괄
  ├─ ColorVoteTally.cs            # 순수 C# 클래스, 다수결/랜덤 계산 (중복 색 제외 포함)
  ├─ TaggerColorAssigner.cs       # 순수 C# 클래스, 술래 & 변형 색상 세트 계산 (중복 색 제외 포함)
  ├─ PlayerColorVoteIndicator.cs  # 플레이어 머리 위 투표색 표시
  ├─ PlayerColorDisplay.cs        # 최종 확정된 색상(또는 술래 변형색) 적용
  ├─ BrushCursorController.cs     # 색상 라운드 동안 마우스 커서를 붓 아이콘으로 교체
  ├─ PlayerPaintCanvas.cs         # 자신의 캐릭터 표면에 붓으로 색칠 + RaiseEvent로 전 클라이언트 실시간 공유 (5.2)
  ├─ BrushSettingsSO.cs           # 붓 텍스처/최소·최대·기본 크기/휠 step/커서 핫스팟 정의 SO
  └─ RoomLifecycleWatcher.cs      # 술래 퇴장 / 1인 남음 / 게임 정상 종료 감지 → 방 종료 & 씬 전환

Assets/03. SO/ColorTag/
  ├─ DefaultColorPalette.asset    # ColorPaletteSO 인스턴스 (10색 값 보관)
  └─ DefaultBrushSettings.asset   # BrushSettingsSO 인스턴스 (0.4-9에서 수치 확정: 0.005/0.1/0.02)

Assets/Resources/UI/Scene/ColorSelectionPanel/
  ├─ ColorSelectionPanel.prefab
  └─ ColorSwatchButton.prefab
```

이 문서의 컴포넌트 중 `PlayerPaintCanvas`, `PlayerColorVoteIndicator`, `PlayerColorDisplay`는 캐릭터에
부착되는 컴포넌트입니다. `PlayerControllPlan.md` 13.9에서 확정된 대로, 이 셋은 전부 아래 하나의 정식
프리팹에 부착됩니다(0.4-13):

```
Assets/04. Prefabs/Resources/
└── HideOrSeekPlayer.prefab   # HideOrSeekPlayer + PhotonView + NavMeshAgent + Animator +
                               # PlayerPaintCanvas + PlayerColorVoteIndicator + PlayerColorDisplay
```

`PlayerGroundDetector`, `PlayerAnimationDriver`, `PlayerNetworkSync`처럼 **네트워크/유니티 의존적인 부분은 MonoBehaviour(Manager)**가 담당하고, **판정 로직(다수결 계산, 변형색 계산)은 순수 C# 클래스로 분리**해 기존 코드베이스 스타일과 동일한 구조를 따릅니다. 페인팅도 동일 원칙으로, 레이캐스트/텍스처 처리는 `PlayerPaintCanvas`(MonoBehaviour)가 담당합니다.

---

## 3. 데이터 설계 ✅ 구현 완료

### 3.1 ColorPaletteSO

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "ColorTag/ColorPalette")]
public class ColorPaletteSO : ScriptableObject
{
    [SerializeField] private ColorEntry[] colors; // 10개 고정

    public int Count => colors.Length;
    public Color GetColor(int index) => colors[index].color;
    public string GetColorName(int index) => colors[index].colorName;
}

[System.Serializable]
public struct ColorEntry
{
    public string colorName;
    public Color color;
}
```

`PalletEx.png` 기준 10색 근사값 (최종 확정 시 아트팀/디자인 조정 필요):

| Index | 이름(가칭) | Hex |
|---|---|---|
| 0 | Red | #E8262B |
| 1 | Orange | #F5821F |
| 2 | Yellow | #FFD400 |
| 3 | Lime | #8CC63F |
| 4 | Green | #00A651 |
| 5 | Teal | #00A99D |
| 6 | Blue | #0072BC |
| 7 | Navy | #1B3A6B |
| 8 | Purple | #6E3F98 |
| 9 | Magenta | #A0136A |

### 3.2 네트워크 프로퍼티 스키마

**Room CustomProperties** (MasterClient만 기록, 전원 수신):

| 키 | 타입 | 설명 |
|---|---|---|
| `RoundIndex` | int | 현재 라운드 (0~3), 4면 술래 지정 단계, 5면 완료 |
| `RoundEndTime` | double | `PhotonNetwork.Time` 기준 라운드 종료 시각 |
| `Color0`~`Color3` | int | 확정된 라운드별 색상 인덱스 (-1 = 미확정). **서로 값이 겹치지 않음이 보장됨** (3.3 참고) |
| `TaggerActorNumber` | int | 술래로 선정된 플레이어의 ActorNumber (-1 = 미지정) |
| `TaggerVariantSet` | int[4] | 술래 전용 변형 4색 세트. `Color0~3`과 값이 겹치지 않음 |
| `GameEndTime` | double | 본게임이 정상 종료된 시각 + 20초. `PhotonNetwork.Time` 기준이며, 이 시각을 지나면 전원 자동으로 `GameLobbyScene`으로 이동 (미설정 = 아직 게임 진행 중, 7.3 참고) |

**Player CustomProperties** (각자 자신 것만 기록, 전원 수신):

| 키 | 타입 | 설명 |
|---|---|---|
| `VoteColorIndex` | int | 현재 라운드에서 이 플레이어가 붓에 담아 칠하고 있는 색 (-1 = 미선택), 머리 위 표시에 사용 |

키 상수는 오타 방지를 위해 별도 클래스로 관리합니다.

```csharp
public static class NetKeys
{
    public const string RoundIndex = "RoundIndex";
    public const string RoundEndTime = "RoundEndTime";
    public const string ColorPrefix = "Color"; // Color0 ~ Color3
    public const string TaggerActorNumber = "TaggerActorNumber";
    public const string TaggerVariantSet = "TaggerVariantSet";
    public const string VoteColorIndex = "VoteColorIndex";
    public const string GameEndTime = "GameEndTime";
}
```

`PhotonNetwork.RaiseEvent`용 이벤트 코드도 같은 이유(오타/충돌 방지)로 별도 상수 클래스에 모읍니다
(0.4-10, 이 프로젝트에서 RaiseEvent를 쓰는 첫 사례라 `NetKeys`와 성격이 달라 파일을 분리했습니다):

```csharp
public static class NetEventCodes
{
    public const byte PaintStroke = 1; // 붓 스트로크 1회를 다른 클라이언트에 전파 (5.2 참고)
}
```

### 3.3 색상 중복 금지 규칙

- 라운드 `k`의 팔레트 선택지 = 전체 10색 - {`Color0` .. `Color(k-1)` 중 확정된 값}. 이미 확정된 라운드가 없으면(k=0) 10색 전부 선택 가능.
- 라운드가 다수결/랜덤으로 확정될 때, 이미 사용된 색은 아예 후보에서 빠지므로 자동으로 중복이 불가능합니다. (계산 로직은 4.3 참고)
- 술래 변형 세트도 마찬가지로, 확정된 `Color0~3` 4개를 전부 제외한 나머지 6색 중에서만 치환값을 고릅니다. (계산 로직은 4.4 참고)
- 별도의 "사용된 색 목록" Room 프로퍼티는 두지 않습니다. `Color0~3` 자체가 이미 그 정보를 담고 있고, `StartColorSelection()`이 라운드 시작 시 전부 -1로 초기화합니다.

---

## 4. 라운드 진행 로직 ✅ 구현 완료 (Play Mode에서 4라운드 전부 자동 진행 검증)

### 4.1 왜 MasterClient 권위 + CustomProperties 방식인가

- 기존 코드베이스는 RPC/RaiseEvent를 아직 쓰지 않고, `MonoBehaviourPunCallbacks` + `IPunObservable`(스트림 동기화) 패턴만 사용 중입니다.
- 투표처럼 "상태값이 바뀔 때마다 전원에게 알려야 하는" 데이터는 RaiseEvent보다 **CustomProperties가 더 간단하고, 재접속/마스터 이관에도 자동으로 최신값이 복제**되므로 이 방식을 택했습니다.
- 타이머는 각 클라이언트의 로컬 시계 대신 **`PhotonNetwork.Time`(서버 기준 동기화 시간)**을 써서 클라 간 오차를 없앱니다. 라운드 타이머(`RoundEndTime`)뿐 아니라 게임 종료 후 로비 이동 타이머(`GameEndTime`)도 동일한 방식을 씁니다.
- **(0.4-10에서 변경) 붓질(페인팅)만은 예외적으로 `PhotonNetwork.RaiseEvent`를 씁니다.** 원래는 0.2절 해석대로 로컬 전용 연출로 가정했지만, 이번 답변으로 다른 플레이어에게도 실시간으로 보여야 한다는 요구사항이 확정됐습니다. 붓 스트로크는 "상태"가 아니라 "빈번하게 발생하는 순간 이벤트"라 CustomProperties(스냅샷 복제 방식)보다 RaiseEvent(발행-구독 방식)가 훨씬 잘 맞습니다 — 이 프로젝트에서 RaiseEvent를 쓰는 첫 사례이므로, 이벤트 코드는 `NetEventCodes.cs`에 모아 오타/충돌을 방지합니다(3.2 참고). 투표 결과 자체(`VoteColorIndex`)는 여전히 CustomProperties를 그대로 씁니다 — 라운드 판정 로직(4.2~4.4)은 이번 변경과 무관합니다.

### 4.2 ColorSelectionManager (핵심 골격)

```csharp
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class ColorSelectionManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private ColorPaletteSO palette;
    [SerializeField] private float roundDuration = 20f;

    private const int TotalRounds = 4;
    private System.Random rng = new System.Random();

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.RoundIndex, out object riObj)) return;

        int roundIndex = (int)riObj;
        if (roundIndex < 0 || roundIndex >= TotalRounds) return; // 진행 중 아님

        double endTime = (double)PhotonNetwork.CurrentRoom.CustomProperties[NetKeys.RoundEndTime];
        if (PhotonNetwork.Time < endTime) return; // 아직 라운드 진행 중

        ResolveRound(roundIndex);
    }

    // MasterClient만 호출. 라운드 하나를 마감하고 다음 라운드(또는 술래 지정)로 넘어감.
    private void ResolveRound(int roundIndex)
    {
        var votes = new Dictionary<int, int>(); // actorNumber -> colorIndex
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            int vote = p.CustomProperties.TryGetValue(NetKeys.VoteColorIndex, out object v) ? (int)v : -1;
            votes[p.ActorNumber] = vote;
        }

        var excludedColors = new List<int>(roundIndex);
        for (int i = 0; i < roundIndex; i++)
            excludedColors.Add((int)PhotonNetwork.CurrentRoom.CustomProperties[NetKeys.ColorPrefix + i]);

        int resolvedColor = ColorVoteTally.Resolve(votes.Values, palette.Count, excludedColors, rng);

        var roomProps = new Hashtable
        {
            { NetKeys.ColorPrefix + roundIndex, resolvedColor }
        };

        if (roundIndex + 1 < TotalRounds)
        {
            roomProps[NetKeys.RoundIndex] = roundIndex + 1;
            roomProps[NetKeys.RoundEndTime] = PhotonNetwork.Time + roundDuration;
        }
        else
        {
            AssignTagger(roomProps, resolvedColor); // 4라운드 종료 → 술래 지정까지 같은 트랜잭션에 포함
            roomProps[NetKeys.RoundIndex] = TotalRounds + 1; // 완료 상태
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
        ResetAllVotes();
    }

    private void AssignTagger(Hashtable roomProps, int lastResolvedColor)
    {
        var players = PhotonNetwork.PlayerList;
        Player tagger = players[rng.Next(players.Length)];

        int[] baseSet = new int[TotalRounds];
        for (int i = 0; i < TotalRounds; i++)
        {
            baseSet[i] = i == TotalRounds - 1
                ? lastResolvedColor // 방금 확정된 값은 아직 Room CustomProperties에 반영 전이므로 직접 전달
                : (int)PhotonNetwork.CurrentRoom.CustomProperties[NetKeys.ColorPrefix + i];
        }

        int[] variant = TaggerColorAssigner.BuildVariantSet(baseSet, palette.Count, rng);

        roomProps[NetKeys.TaggerActorNumber] = tagger.ActorNumber;
        roomProps[NetKeys.TaggerVariantSet] = variant;
    }

    private void ResetAllVotes()
    {
        if (PhotonNetwork.LocalPlayer != null)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.VoteColorIndex, -1 } });
    }

    // 라운드 시작. 게임 시작 버튼 등에서 MasterClient가 방당 최초 1회 호출 (재대결 개념이 없으므로 그 이후에는 호출되지 않음)
    public void StartColorSelection()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        var props = new Hashtable
        {
            { NetKeys.RoundIndex, 0 },
            { NetKeys.RoundEndTime, PhotonNetwork.Time + roundDuration },
            { NetKeys.ColorPrefix + 0, -1 },
            { NetKeys.ColorPrefix + 1, -1 },
            { NetKeys.ColorPrefix + 2, -1 },
            { NetKeys.ColorPrefix + 3, -1 },
            { NetKeys.TaggerActorNumber, -1 },
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        PhotonNetwork.CurrentRoom.IsOpen = false; // 색상 선택 시작과 동시에 신규 입장 차단 (0.4-12, 7.1 참고)
    }

    // 로컬 플레이어가 팔레트 스와치를 클릭해 붓 색을 고를 때 UI에서 호출
    public void SubmitVote(int colorIndex)
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.VoteColorIndex, colorIndex } });
    }
}
```

**중요:** 각 클라이언트가 `Update()`에서 매 프레임 `PhotonNetwork.IsMasterClient`를 체크하고, 마스터 이관이 일어나도 필요한 상태(`RoundIndex`, `RoundEndTime`, 각 `ColorN`)가 전부 Room CustomProperties에 있으므로 **새 마스터가 그대로 이어서 진행**할 수 있습니다. 별도의 마이그레이션 처리 코드가 필요 없습니다.

### 4.3 다수결/랜덤 판정 로직 (순수 C#, 유닛 테스트 가능, 중복 색 제외 포함)

```csharp
using System.Collections.Generic;
using System.Linq;

public static class ColorVoteTally
{
    // votes: 각 플레이어의 투표값 (미투표는 -1)
    // excludedColors: 이전 라운드에서 이미 확정되어 이번 라운드에는 뽑힐 수 없는 색 인덱스 목록
    public static int Resolve(IEnumerable<int> votes, int paletteSize, IReadOnlyCollection<int> excludedColors, System.Random rng)
    {
        var cast = votes.Where(v => v >= 0 && !excludedColors.Contains(v)).ToList();

        if (cast.Count == 0)
        {
            var available = Enumerable.Range(0, paletteSize)
                                       .Where(i => !excludedColors.Contains(i))
                                       .ToList();
            return available[rng.Next(available.Count)]; // 아무도 정하지 않음(또는 전부 제외색) -> 남은 색 중 랜덤
        }

        var grouped = cast.GroupBy(v => v)
                           .OrderByDescending(g => g.Count())
                           .ToList();

        int topCount = grouped[0].Count();
        var topColors = grouped.Where(g => g.Count() == topCount)
                                .Select(g => g.Key)
                                .ToList();

        // 다수결 결과가 여럿(동점)이면 그 중에서 무작위로 확정
        return topColors[rng.Next(topColors.Count)];
    }
}
```

`excludedColors`에 해당하는 색은 애초에 UI(팔레트 패널)에서 선택 불가능하도록 막지만, 혹시 모를 타이밍 이슈(막 제외된 색을 클릭한 직후 등)에 대비해 판정 로직에서도 한 번 더 방어적으로 걸러냅니다.

### 4.4 술래 변형 색상 계산 (중복 색 제외 포함)

```csharp
using System.Collections.Generic;
using System.Linq;

public static class TaggerColorAssigner
{
    // baseSet(확정된 4색) 중 무작위 한 슬롯을, baseSet에 없는 팔레트 색으로 치환
    public static int[] BuildVariantSet(int[] baseSet, int paletteSize, System.Random rng)
    {
        int[] variant = (int[])baseSet.Clone();
        int slot = rng.Next(variant.Length);

        var available = Enumerable.Range(0, paletteSize)
                                   .Where(i => !baseSet.Contains(i))
                                   .ToList();

        variant[slot] = available[rng.Next(available.Count)];
        return variant;
    }
}
```

팔레트가 10색, 확정된 4색을 제외하면 항상 6개 이상 후보가 남으므로 무한루프 위험 없이 안전합니다.

---

## 5. 색상 페인팅 시스템 ✅ 구현 완료 (9장의 셰이더/합성 방식 보완 반영)

0.2절 해석에 따라, 라운드 동안 "붓으로 내 캐릭터를 직접 칠하는" 인터랙션을 아래와 같이 설계합니다. 이 부분은 텍스처/카메라/레이캐스트를 다루므로 전부 MonoBehaviour 쪽 책임입니다.

### 5.1 BrushCursorController — 커서 교체

- `RoundIndex`가 0~3(색상 결정 라운드 진행 중)일 때만 시스템 커서를 `BrushSettingsSO`에 정의된 붓 텍스처로 교체(`Cursor.SetCursor`), 그 외에는 기본 커서로 복원합니다. (0.3의 8번 답변으로 확정)
- `OnRoomPropertiesUpdate` 콜백에서 `RoundIndex` 변화를 감지해 갱신하며, 매 프레임 폴링하지 않습니다.

```csharp
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

public class BrushCursorController : MonoBehaviourPunCallbacks
{
    [SerializeField] private BrushSettingsSO brushSettings;

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.RoundIndex)) return;
        ApplyCursorForRound((int)changedProps[NetKeys.RoundIndex]);
    }

    private void ApplyCursorForRound(int roundIndex)
    {
        bool isColorRound = roundIndex >= 0 && roundIndex < 4;

        if (isColorRound)
            Cursor.SetCursor(brushSettings.CursorTexture, brushSettings.CursorHotspot, CursorMode.Auto);
        else
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // 기본 시스템 커서로 복귀
    }
}
```

### 5.2 PlayerPaintCanvas — 내 캐릭터에 칠하기 (붓 크기 조절 + 부위 잠금 + 실시간 공유)

- `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab`(0.4-13, `PlayerControllPlan.md` 13.9)에 부착하되, **`pv.IsMine`인 인스턴스에서만 입력을 받습니다.** (다른 플레이어가 원격으로 들고 있는 인스턴스는 이 컴포넌트가 입력을 무시)
- 색상 라운드 중(`RoundIndex` 0~3) 매 프레임 좌클릭을 감지하면, 로컬 카메라 기준으로 레이캐스트를 쏴서 자기 자신의 콜라이더에 맞았는지 확인합니다. (다른 플레이어 오브젝트에 맞으면 무시 — "자신의 오브젝트만 칠할 수 있다" 규칙)
- 히트에 맞았다면 `hit.textureCoord`(UV 좌표)를 얻어, 캐릭터 표면에 덮인 페인트용 `RenderTexture`에 현재 `VoteColorIndex` 색을 **현재 붓 크기(`currentBrushRadius`)** 만큼의 원형 스탬프로 찍습니다.
- **붓 크기는 마우스 휠로 실시간 조절**하며, `BrushSettingsSO`의 `MinRadius`(0.005)~`MaxRadius`(0.1) 범위로 클램프됩니다(0.4-9). 붓 크기 자체는 순수 로컬 상태(투표 결과에 영향 없음)이므로 네트워크 동기화가 필요 없습니다.
- **(0.4-11 신규) 이미 칠해진 부위는 다른 색으로 덮어칠할 수 없습니다.** `paintCanvas`(RenderTexture)의 **알파 채널을 "칠해짐" 마스크로 사용**합니다 — 아직 안 칠해진 픽셀은 알파 `0`, 한 번이라도 칠해진 픽셀은 알파 `1`로 고정됩니다. 스탬프 셰이더는 매 프레임 목적지(기존 캔버스)의 알파를 읽어서, **알파가 이미 `1`인 픽셀은 새 스탬프 색으로 덮어쓰지 않고 그대로 유지**합니다(같은 색으로 "덧칠"해도 결과가 똑같아 자연스럽게 허용되는 셈이고, 다른 색으로는 절대 안 바뀝니다). 이 판정은 GPU(셰이더)가 픽셀 단위로 하므로 CPU 쪽에서 별도로 "이 스트로크를 막을지 말지"를 미리 계산할 필요가 없습니다 — 뒤에서 설명할 원격 재생(`ApplyStamp`)도 완전히 같은 셰이더를 쓰므로, 모든 클라이언트가 동일한 순서로 스트로크를 받는 한 자동으로 같은 결과에 수렴합니다.
- **(0.4-10 신규) 내가 칠한 스트로크는 `PhotonNetwork.RaiseEvent`로 다른 모든 클라이언트에도 전파되어, 실시간으로 같은 위치가 칠해지는 것을 볼 수 있습니다.** 로컬에 스탬프를 찍은 직후, UV 좌표/반경/색상 인덱스를 이벤트로 실어 보냅니다(`NetEventCodes.PaintStroke`, 3.2 참고). 받는 쪽은 자기 자신을 제외한 모든 클라이언트이며, 각자 `photonView.ViewID`로 어떤 캐릭터에 대한 스트로크인지 찾아서 동일한 스탬프를 재생합니다.
  - **대역폭 참고:** 마우스를 누른 채 드래그하면 `Update()`가 프레임마다 이벤트를 보낼 수 있어 트래픽이 늘어납니다. 2~4인 소규모 게임이라 우선 이 방식으로 설계했지만, 실제 플레이해보고 버벅이면 스트로크 간 최소 UV 이동 거리를 두는 등 추후 튜닝이 필요할 수 있습니다.
- **(0.5-15/16 신규) 라운드가 확정되면, "그 라운드에 내가 칠한 자리"만 확정색으로 다시 칠해집니다.** 이번 답변으로 "부위는 클릭한 자리 그대로 남고(0.5-16), 그 자리의 색만 라운드 결과에 맞춰 정리된다(0.5-15)"는 것으로 구체화됐습니다:
  1. 라운드 중에는 `VoteColorIndex`(스와치로 고른 후보색, 최종 확정색이 아닐 수 있음)로 임시로 칠하고, 이번 라운드에 성공적으로 칠한 UV 지점들을 `currentRoundStrokes` 리스트에 기록해둡니다.
  2. `RoundIndex`가 바뀌는 순간(그 라운드가 다수결/랜덤으로 확정된 순간), 방금 끝난 라운드의 확정색(`Color{끝난 라운드 번호}`)을 Room CustomProperties에서 읽어와, `currentRoundStrokes`에 기록해둔 자리들만 골라 그 확정색으로 다시 스탬프를 찍습니다(`FinalizeCurrentRoundStrokes`).
  3. 이 "재도색"은 이미 잠긴(알파=1) 픽셀이라도 강제로 덮어써야 하므로(내가 방금 그 라운드에 칠한 자리는 이미 내 후보색으로 잠겨 있는 상태), 5.2의 일반 스탬프(`brushStampMaterial`, 잠금 존중)와 별도로 **잠금을 무시하고 항상 덮어쓰는 전용 머티리얼(`finalizeStampMaterial`)**을 씁니다. 재도색이 끝나면 그 픽셀은 다시 알파 `1`로 잠기고, 이번엔 확정색이라 다음 라운드부터는 정상적으로 보호됩니다.
  4. 재도색 결과도 `PaintStroke` 이벤트로 전파해 다른 클라이언트도 같은 순간 같은 결과를 보게 합니다(이벤트 payload에 `force` 플래그를 추가해 수신 측이 `finalizeStampMaterial`을 쓰도록 구분).
  5. 라운드가 끝나면 `currentRoundStrokes`를 비우고 다음 라운드를 새로 기록합니다.
- 4라운드가 모두 끝나면(완료 상태) 별도의 "캔버스 리셋"은 필요 없습니다 — 매 라운드마다 이미 확정색으로 정리됐기 때문입니다. 술래에게만 필요한 추가 처리(변형 색상 슬롯 반영)는 6.3 참고.

```csharp
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerPaintCanvas : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Collider paintableCollider;
    [SerializeField] private RenderTexture paintCanvas;
    [SerializeField] private ColorPaletteSO palette;
    [SerializeField] private BrushSettingsSO brushSettings;
    [SerializeField] private Material brushStampMaterial;   // 일반 스탬프: 잠긴(알파=1) 픽셀은 건드리지 않음
    [SerializeField] private Material finalizeStampMaterial; // 라운드 확정 재도색 전용: 잠금 무시하고 항상 덮어씀
    // 두 머티리얼 모두 같은 _StampUV/_StampRadius/_StampColor 인터페이스를 쓰되, 목적지 알파 검사 여부만 다름
    // (셰이더 자체는 구현 단계에서 제작, 5.2 하단 참고)

    private Camera localCamera;
    private float currentBrushRadius;
    private readonly List<Vector2> currentRoundStrokes = new List<Vector2>();
    private int trackedRoundIndex = -1;

    private void Start()
    {
        localCamera = Camera.main;
        currentBrushRadius = Mathf.Clamp(brushSettings.DefaultRadius, brushSettings.MinRadius, brushSettings.MaxRadius);
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Update()
    {
        DetectRoundChange(); // 라운드가 넘어갔는지는 술래가 아니어도, pv.IsMine이 아니어도 항상 체크 (내 캔버스 재도색은 소유자만, 하지만 감지 자체는 누구나)

        if (!pv.IsMine) return;
        if (!IsColorRoundActive()) return;

        HandleBrushSizeInput();

        if (!Input.GetMouseButton(0)) return;

        Ray ray = localCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;
        if (hit.collider != paintableCollider) return; // 자신의 오브젝트가 아니면 무시

        int voteColor = GetCurrentVoteColorIndex();
        if (voteColor < 0) return; // 아직 붓에 담긴 색이 없으면 칠하지 않음

        StampBrush(hit.textureCoord, voteColor);
    }

    // 마우스 휠로 붓 크기를 min~max 범위 내에서 조절 (0.4-9 반영)
    private void HandleBrushSizeInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f)) return;

        currentBrushRadius = Mathf.Clamp(
            currentBrushRadius + scroll * brushSettings.WheelStep,
            brushSettings.MinRadius,
            brushSettings.MaxRadius);
    }

    private bool IsColorRoundActive()
    {
        int roundIndex = GetRoundIndex();
        return roundIndex >= 0 && roundIndex < 4;
    }

    private int GetRoundIndex()
    {
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        return props.TryGetValue(NetKeys.RoundIndex, out object ri) ? (int)ri : -1;
    }

    private int GetCurrentVoteColorIndex()
    {
        return PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(NetKeys.VoteColorIndex, out object v)
            ? (int)v
            : -1;
    }

    // 로컬에 스탬프를 찍고(잠금 존중), 이번 라운드 기록에 남기고, 동일한 스트로크를 다른 클라이언트에도 전파 (0.4-10)
    private void StampBrush(Vector2 uv, int colorIndex)
    {
        ApplyStamp(brushStampMaterial, uv, currentBrushRadius, colorIndex);
        currentRoundStrokes.Add(uv); // 0.5-15: 이 라운드가 확정되면 이 자리들을 확정색으로 재도색

        SendStrokeEvent(uv, currentBrushRadius, colorIndex, force: false);
    }

    // 라운드가 막 넘어갔는지 감지해서, 방금 끝난 라운드에 칠했던 자리를 확정색으로 재도색 (0.5-15)
    private void DetectRoundChange()
    {
        int roundIndex = GetRoundIndex();
        if (roundIndex == trackedRoundIndex) return;

        int justResolvedRound = trackedRoundIndex; // 재도색 대상은 "방금까지 진행 중이던" 라운드
        trackedRoundIndex = roundIndex;

        if (pv.IsMine && justResolvedRound >= 0 && justResolvedRound < 4 && currentRoundStrokes.Count > 0)
        {
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            int confirmedColor = (int)props[NetKeys.ColorPrefix + justResolvedRound];
            FinalizeCurrentRoundStrokes(confirmedColor);
        }

        currentRoundStrokes.Clear();
    }

    // 이번 라운드에 칠했던 자리들을 확정색으로 강제 재도색 (잠금 무시) + 전파
    private void FinalizeCurrentRoundStrokes(int confirmedColorIndex)
    {
        foreach (Vector2 uv in currentRoundStrokes)
        {
            ApplyStamp(finalizeStampMaterial, uv, currentBrushRadius, confirmedColorIndex);
            SendStrokeEvent(uv, currentBrushRadius, confirmedColorIndex, force: true);
        }
    }

    private void SendStrokeEvent(Vector2 uv, float radius, int colorIndex, bool force)
    {
        object[] content = { pv.ViewID, uv.x, uv.y, radius, colorIndex, force };
        var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(NetEventCodes.PaintStroke, content, options, SendOptions.SendReliable);
    }

    // 다른 클라이언트가 보낸 스트로크(또는 라운드 확정 재도색)를 수신해 재생 (0.4-10 / 0.5-15)
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.PaintStroke) return;

        object[] data = (object[])photonEvent.CustomData;
        int viewId = (int)data[0];
        if (viewId != pv.ViewID) return; // 내 캐릭터에 대한 스트로크가 아니면 무시

        Vector2 uv = new Vector2((float)data[1], (float)data[2]);
        float radius = (float)data[3];
        int colorIndex = (int)data[4];
        bool force = (bool)data[5];

        Material material = force ? finalizeStampMaterial : brushStampMaterial;
        ApplyStamp(material, uv, radius, colorIndex); // 송신 측이 이미 판단을 끝냈으므로 그대로 재생만 함
    }

    // uv 위치를 중심으로 radius 반경의 원형 스탬프를 paintCanvas에 그림 (로컬/원격 공용)
    private void ApplyStamp(Material stampMaterial, Vector2 uv, float radius, int colorIndex)
    {
        stampMaterial.SetVector("_StampUV", uv);
        stampMaterial.SetFloat("_StampRadius", radius);
        stampMaterial.SetColor("_StampColor", palette.GetColor(colorIndex));

        RenderTexture temp = RenderTexture.GetTemporary(paintCanvas.width, paintCanvas.height, 0, paintCanvas.format);
        Graphics.Blit(paintCanvas, temp);            // 기존 캔버스(+알파 마스크)를 임시 버퍼로 복사
        Graphics.Blit(temp, paintCanvas, stampMaterial); // brushStampMaterial=잠금 존중, finalizeStampMaterial=항상 덮어씀
        RenderTexture.ReleaseTemporary(temp);
    }
}
```

> `brushStampMaterial`(UV 좌표 기준 원형 마스크 + "목적지 알파가 이미 1이면 스킵"하는 알파 테스트 + 알파 블렌딩)과 `finalizeStampMaterial`(같은 마스크지만 알파 테스트 없이 항상 덮어씀)은 코드 스니펫만으로는 표현이 안 되는 셰이더 애셋이라, 실제 구현 단계에서 제작합니다. 이 문서에서는 호출 인터페이스(`_StampUV`, `_StampRadius`, `_StampColor`)와 "목적지 알파로 잠금을 표현한다"는 규칙, 그리고 두 머티리얼의 차이(잠금 존중 vs 무시)까지만 정의합니다.

### 5.3 BrushSettingsSO

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "ColorTag/BrushSettings")]
public class BrushSettingsSO : ScriptableObject
{
    [SerializeField] private Texture2D cursorTexture;
    [SerializeField] private Vector2 cursorHotspot;

    [Header("Brush Radius (UV 기준, 0.4-9에서 확정)")]
    [SerializeField] private float minRadius = 0.005f;
    [SerializeField] private float maxRadius = 0.1f;
    [SerializeField] private float defaultRadius = 0.02f;
    [SerializeField] private float wheelStep = 0.002f;    // 마우스 휠 1틱당 변화량 (0.6-17에서 확정)

    public Texture2D CursorTexture => cursorTexture;
    public Vector2 CursorHotspot => cursorHotspot;
    public float MinRadius => minRadius;
    public float MaxRadius => maxRadius;
    public float DefaultRadius => defaultRadius;
    public float WheelStep => wheelStep;
}
```

---

## 6. UI 설계 ✅ 구현 완료

### 6.1 팔레트 패널 (`ColorSelectionPanel`)

- 10개 `ColorSwatchButton`을 2행 5열로 배치 (PalletEx.png 배열과 동일).
- 상단에 `RoundIndex + 1 / 4` 및 남은 시간(`RoundEndTime - PhotonNetwork.Time`) 표시.
- **이미 확정된 라운드의 색(`Color0..Color(k-1)`)에 해당하는 스와치는 비활성화/숨김 처리**하여 애초에 선택할 수 없게 만듭니다. (3.3 규칙 반영)
- 스와치 클릭 시 `ColorSelectionManager.SubmitVote(index)` 호출 → 이 색이 현재 "붓에 담긴 색"이 되고, 선택된 스와치는 하이라이트됩니다. 이후 5.2의 `PlayerPaintCanvas`로 실제 캐릭터에 칠하게 됩니다.

```csharp
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class ColorSwatchButton : MonoBehaviour
{
    [SerializeField] private int colorIndex;
    [SerializeField] private Image icon;
    [SerializeField] private Button button;
    [SerializeField] private ColorSelectionManager manager;

    private void Awake()
    {
        button.onClick.AddListener(() => manager.SubmitVote(colorIndex));
    }

    // 라운드 시작 시 매니저가 호출: 이미 확정된 색이면 버튼을 잠금
    public void SetLocked(bool locked)
    {
        button.interactable = !locked;
    }
}
```

### 6.2 머리 위 투표색 표시 (`PlayerColorVoteIndicator`)

- `HideOrSeekPlayer.prefab`(0.4-13)에 world-space 인디케이터(작은 원형 Image)를 부착.
- 소유 플레이어(`pv.Owner`)의 `VoteColorIndex` CustomProperty를 읽어 색을 반영, -1이면 비활성화.
- `OnPlayerPropertiesUpdate` 콜백으로 실시간 갱신(폴링 불필요).
- **(0.4-10 반영) 이제 붓질 자체가 RaiseEvent로 다른 플레이어에게도 실시간으로 보이므로, 이 인디케이터는 더 이상 "유일한 수단"은 아닙니다.** 다만 캐릭터가 화면 밖에 있거나 멀리 있어도 한눈에 누가 무슨 색에 투표 중인지 빠르게 파악할 수 있는 보조 수단으로는 여전히 유용해 그대로 유지합니다.

```csharp
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerColorVoteIndicator : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private SpriteRenderer indicator;
    [SerializeField] private ColorPaletteSO palette;

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != pv.Owner) return;
        if (!changedProps.ContainsKey(NetKeys.VoteColorIndex)) return;

        int index = (int)targetPlayer.CustomProperties[NetKeys.VoteColorIndex];
        indicator.enabled = index >= 0;
        if (index >= 0)
            indicator.color = palette.GetColor(index);
    }

    private void LateUpdate()
    {
        // 카메라를 향하도록 빌보드 처리
        if (Camera.main != null)
            transform.forward = Camera.main.transform.forward;
    }
}
```

### 6.3 최종 색상 적용 (`PlayerColorDisplay`)

**(0.5-15/16 반영으로 역할이 축소됨)** 일반 플레이어는 4라운드 내내 `PlayerPaintCanvas`가 라운드별로
이미 확정색으로 재도색을 끝내뒀으므로(5.2), 이 컴포넌트가 따로 할 일이 없습니다. **오직 술래 1명에
대해서만**, 확정된 4색 중 한 슬롯이 변형 색상으로 치환된 것을 캔버스에 반영하는 역할만 합니다.

- `RoundIndex`가 완료 상태(5)로 바뀌는 `OnRoomPropertiesUpdate` 시점에, 이 컴포넌트가 붙은 캐릭터가
  `TaggerActorNumber`와 일치하는지 확인합니다. 아니면(일반 플레이어) 아무 것도 하지 않습니다.
- 술래라면, `Color0~Color3`와 `TaggerVariantSet`을 나란히 비교해 **정확히 다른 슬롯 1개**를 찾습니다
  (3.3에서 색이 전부 고유함을 보장하므로 슬롯 비교만으로 충분합니다).
- 그 슬롯의 원래 색(`Color[슬롯]`)으로 칠해진 픽셀을 **찾아서** 변형색(`TaggerVariantSet[슬롯]`)으로
  **치환**합니다 — UV 좌표 기반이 아니라 **캔버스 전체를 대상으로 "이 RGB 색이면 저 RGB 색으로 바꿔라"
  하는 색상 치환(전용 셰이더/머티리얼, `colorReplaceMaterial`)**을 한 번 수행하는 방식입니다. 어느
  라운드에 그 색이 어디에 칠해졌는지 별도로 추적할 필요 없이, 색상 값 자체가 라운드마다 고유하다는
  사실(3.3)을 이용한 것입니다.
- **다른 클라이언트에 전파할 필요가 없습니다** — `TaggerActorNumber`/`Color0~3`/`TaggerVariantSet`은
  이미 Room CustomProperties로 전원에게 복제되어 있고, 술래 캐릭터의 캔버스 색상도 4라운드 동안의
  `PaintStroke` 이벤트로 이미 전원에게 동기화되어 있으므로, **모든 클라이언트가 이 색상 치환을 각자
  독립적으로 수행해도 항상 같은 결과**가 됩니다(결정론적 연산).
- 술래 여부와 무관하게 **이 최종 색은 게임이 끝날 때까지 항상 모든 플레이어에게 계속 보입니다** (0.1의 1번 해석 반영, 별도 은폐/재공개 로직 없음).
- 실제 색이 나타나는 위치는 0.5-16에서 확정된 대로, 정해진 신체 부위 매핑이 아니라 각 라운드에 플레이어가 직접 클릭한 자리 그대로입니다.

---

## 7. 방/라운드 생명주기 예외 처리 ✅ 구현 완료 (7.3의 `GameLobbyScene` 전환은 씬이 아직 없어 미검증, 9장 참고)

### 7.1 엣지 케이스 표

| 상황 | 처리 방안 |
|---|---|
| 라운드 중 마스터 이관 | 상태가 Room CustomProperties에 있으므로 새 마스터가 `Update()`에서 그대로 이어받음 |
| 라운드 중 일반 플레이어 퇴장 (술래 아님, 2명 이상 남음) | 매 판정 시 `PhotonNetwork.PlayerList` 기준으로 투표를 다시 집계하므로 자동 반영 (Photon이 퇴장자를 목록에서 제거), 게임 계속 진행 |
| **술래 퇴장** | 대기 없이 즉시 방 종료 → 전원 `LobbyScene` 복귀 (7.2 참고) |
| **인원이 1명만 남음** (색상 선택 페이즈든 본게임이든) | 대기 없이 즉시 방 종료 → 남은 1명도 `LobbyScene` 복귀 (7.2 참고) |
| **게임 정상 종료** (본게임 승패가 판정된 경우) | 즉시 종료가 아니라 **20초 뒤** 전원 `GameLobbyScene`으로 이동 (7.3 참고) |
| 오프라인 모드(`OfflineModeBootstrap`) 테스트 | 플레이어 1명뿐이므로, 투표하면 그 색, 안 하면 랜덤. 술래도 자기 자신으로 고정됨 (단독 테스트용, 1인 종료 규칙은 오프라인 모드에는 적용하지 않음) |
| 라운드 도중 신규 입장 | `StartColorSelection()`이 `PhotonNetwork.CurrentRoom.IsOpen = false`를 함께 설정해 색상 선택 시작과 동시에 입장을 차단 (0.4-12 확정, 4.2 코드 참고) |
| 동점 다수결 | `ColorVoteTally`가 동점 색상 중 랜덤 1개로 확정 |

### 7.2 술래 퇴장 / 인원 부족 — 즉시 종료

- 모든 클라이언트가 각자 독립적으로 동일한 조건을 판정하므로, MasterClient만의 특별한 브로드캐스트 없이도 전원이 같은 타이밍에 같은 결론(방 종료 여부)에 도달합니다. (Photon이 `OnPlayerLeftRoom` 콜백과 `PlayerList`를 전원에게 동일하게 복제해주기 때문)
- 술래가 아직 정해지지 않은 상태(`TaggerActorNumber == -1`, 즉 색상 선택 페이즈 도중)에서는 "술래 퇴장" 조건이 항상 거짓이 되어 자연스럽게 "1명 남음" 조건만 적용됩니다.

### 7.3 게임 정상 종료 — 20초 뒤 전원 GameLobbyScene 이동

- 본게임(HideOrSeekPlayer 등)의 승패 판정 로직은 이 문서 범위 밖입니다. 그 로직이 게임이 끝나는 시점에 **MasterClient에서 딱 한 번** `GameEndTime = PhotonNetwork.Time + 20f`를 Room CustomProperties에 기록하기만 하면, 아래 감시 로직이 나머지를 처리합니다.
- 7.2(즉시 종료)와 7.3(20초 뒤 종료)은 둘 다 "방을 나간다"는 동작으로 귀결되므로, `OnLeftRoom` 콜백이 어떤 사유로 나가는 것인지 구분할 수 있도록 **하나의 컴포넌트(`RoomLifecycleWatcher`)가 두 시나리오를 함께 관리**합니다. (별개 컴포넌트로 나누면 둘 다 `OnLeftRoom`을 구현하게 되어, 정상 종료인데 `LobbyScene`으로 잘못 이동하는 등 충돌이 생길 수 있음)

```csharp
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class RoomLifecycleWatcher : MonoBehaviourPunCallbacks
{
    private enum LeaveReason { None, Abnormal, NormalGameEnd }

    private LeaveReason leaveReason = LeaveReason.None;

    // 술래 퇴장 / 인원 부족 감지 → 즉시 종료 (7.2)
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (leaveReason != LeaveReason.None) return; // 이미 종료 처리 중

        bool onlyOnePlayerLeft = PhotonNetwork.PlayerList.Length <= 1;
        bool taggerLeft = IsTagger(otherPlayer);

        if (onlyOnePlayerLeft || taggerLeft)
        {
            leaveReason = LeaveReason.Abnormal;
            PhotonNetwork.LeaveRoom();
        }
    }

    // 게임 정상 종료 20초 타이머 감지 (7.3)
    private void Update()
    {
        if (leaveReason != LeaveReason.None) return;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetKeys.GameEndTime, out object endObj)) return;

        double gameEndTime = (double)endObj;
        if (PhotonNetwork.Time < gameEndTime) return;

        leaveReason = LeaveReason.NormalGameEnd;
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        string targetScene = leaveReason == LeaveReason.NormalGameEnd ? "GameLobbyScene" : "LobbyScene";
        SceneManager.LoadScene(targetScene);
    }

    private bool IsTagger(Player player)
    {
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(NetKeys.TaggerActorNumber, out object tagger)) return false;

        int taggerActorNumber = (int)tagger;
        return taggerActorNumber >= 0 && taggerActorNumber == player.ActorNumber;
    }
}
```

- `GameLobbyScene`은 이 문서 범위 밖이며 추후 별도로 구현 예정입니다. 이 문서에서는 "정상 종료 20초 후 전원이 함께 이 씬으로 이동한다"는 계약(연결 지점)만 정의합니다.

---

## 8. 구현 순서 제안 (승인 후 진행) ✅ 1~9번 전부 완료

1. `ColorPaletteSO` + `NetKeys` + `NetEventCodes` + `ColorVoteTally`/`TaggerColorAssigner` (순수 로직, 중복 색 제외 포함, Unity 의존 없음)
2. `ColorSelectionManager` (네트워크 골격 + `StartColorSelection()`의 `IsOpen = false` 포함, 로그로 라운드 진행 확인)
3. `ColorSwatchButton` + `ColorSelectionPanel` UI 연결 (제외 색 잠금 포함)
4. `PlayerColorVoteIndicator` (머리 위 실시간 표시)
5. `BrushSettingsSO`(0.4-9 확정 수치 반영) + `BrushCursorController` + `brushStampMaterial`/`finalizeStampMaterial` 셰이더 제작 + `PlayerPaintCanvas`(붓 커서 교체 + 자기 캐릭터 페인팅 + 마우스 휠 크기 조절 + 부위 잠금 + 라운드 확정 시 재도색 + `RaiseEvent` 송수신, 5.2)
6. `colorReplaceMaterial` 셰이더 제작 + `PlayerColorDisplay` (술래 전용 색상 치환, 6.3 — 일반 플레이어는 5번 단계에서 이미 처리 완료라 별도 작업 없음)
7. `RoomLifecycleWatcher` (술래 퇴장 / 1인 남음 → 즉시 종료, 게임 정상 종료 → 20초 후 이동)
8. 위 컴포넌트들을 `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab`(`PlayerControllPlan.md` 13.9)에 부착
9. 2~4인 실제 접속 테스트(붓질 실시간 공유, 부위 잠금, 라운드 확정 재도색, 술래 색상 치환 동작 확인 포함), 오프라인 모드 단독 테스트, 마스터 이관 시나리오 테스트, 퇴장/게임 종료 시나리오 테스트, 신규 입장 차단 확인 (단, `GameLobbyScene`은 아직 없으므로 씬 전환 직전까지만 검증 가능)

---

## 9. 구현 완료 보고 (2026-08-14)

Unity MCP로 직접 실행했다. 전체 스크립트/셰이더/에셋/프리팹 구성을 설계대로 만들고, 매 변경 직후
`read_console`로 컴파일 에러를 반복 확인하며 진행했다.

### 9.1 생성된 파일

**스크립트** (`Assets/02. Scripts/ColorTag/`): `ColorPaletteSO.cs`, `NetKeys.cs`, `NetEventCodes.cs`,
`ColorVoteTally.cs`, `TaggerColorAssigner.cs`(+`FindSwappedSlot` 헬퍼 추가, 6.3에서 필요),
`ColorSelectionManager.cs`, `BrushSettingsSO.cs`, `BrushCursorController.cs`, `PlayerPaintCanvas.cs`,
`ColorSwatchButton.cs`, `ColorSelectionPanel.cs`(신규 — 6.1에 프로즈로만 있던 라운드/타이머 표시 +
스와치 잠금 총괄 로직을 담을 컴포넌트가 필요해 추가), `PlayerColorVoteIndicator.cs`,
`PlayerColorDisplay.cs`, `RoomLifecycleWatcher.cs`.

**셰이더/머티리얼** (`Assets/02. Scripts/ColorTag/Shaders/`): `PaintStamp.shader`(→
`BrushStampMaterial.mat`/`FinalizeStampMaterial.mat`, `_RespectLock` 프로퍼티로 두 머티리얼이 하나의
셰이더를 공유), `PaintColorReplace.shader`(→ `ColorReplaceMaterial.mat`), `PlayerPaintedSkin.shader`(신규,
9.2 참고).

**SO 에셋** (`Assets/03. SO/ColorTag/`): `DefaultColorPalette.asset`(3.1의 10색 그대로), `DefaultBrushSettings.asset`
(0.4-9/0.6-17 확정 수치: 0.005/0.1/0.02/0.002).

**프리팹**: `Assets/Resources/UI/Scene/ColorSelectionPanel/ColorSelectionPanel.prefab`(라운드/타이머
라벨 + 2행 5열 스와치 10개, 각 스와치 색은 3.1 팔레트와 동일하게 칠해둠).

**어셈블리 설정**: `Assets/02. Scripts/TagOfChaos.Scripts.asmdef`에 `Unity.TextMeshPro`, `Unity.ugui`
참조 추가 (`ColorSelectionPanel`이 TMP/uGUI를 쓰는데 이 asmdef엔 원래 참조가 없어 컴파일 에러가 났음).

### 9.2 설계 대비 변경/보완 사항

계획 문서 자체는 그대로 구현했고, 아래는 계획에서 코드 스니펫 수준까지는 다루지 않았던 부분이라
구현하면서 채운 것들이다.

1. **`RenderTexture paintCanvas`를 직렬화 필드 → 런타임 인스턴스별 생성으로 변경.** 5.2의 원래 스니펫은
   `[SerializeField] private RenderTexture paintCanvas;`로 되어 있었는데, 이대로 두면 프리팹의 모든
   캐릭터 인스턴스가 **같은 텍스처 에셋을 공유**해서 한 명이 칠하면 전원의 캔버스가 같이 오염된다.
   `PlayerPaintCanvas`가 `Start()`에서 `new RenderTexture(...)`로 인스턴스마다 새로 만들고
   `public RenderTexture PaintCanvas { get; private set; }`로 노출하도록 고쳤다. `PlayerColorDisplay`도
   자기 필드 대신 `GetComponent<PlayerPaintCanvas>().PaintCanvas`를 쓰도록 맞춰서, 두 컴포넌트가 항상
   같은 텍스처를 보도록 했다.
2. **캐릭터 스킨과 페인트를 합성하는 신규 셰이더 `ColorTag/PlayerPaintedSkin` 추가.** 페인트 캔버스를
   캐릭터의 `_MainTex`에 그대로 덮어씌우면 원본 피부 텍스처가 사라지고 캐릭터가 새까맣게 보인다. 원본
   스킨(`_MainTex`)과 페인트 캔버스(`_PaintTex`)를 페인트 알파값으로 블렌딩하는 언릿 라이팅 셰이더를
   새로 만들어, `PlayerPaintCanvas.Start()`가 `Ch36`(`SkinnedMeshRenderer`)의 머티리얼을 이 셰이더
   기반 인스턴스로 교체하도록 했다. 정식 라이팅(그림자 등)은 반영하지 않은 단순 버전이라, 실제 비주얼
   품질은 추후 아트 작업에서 다시 다듬어야 한다.
3. **프리팹에 `CapsuleCollider` 추가.** `HideOrSeekPlayer`에는 원래 콜라이더가 전혀 없어서(이동이 전부
   수동 `transform.position` 갱신 방식이라) `PlayerPaintCanvas`의 `paintableCollider`(레이캐스트 대상)
   레퍼런스가 가리킬 대상이 없었다. 인간형 캐릭터 크기에 맞춰 `center(0,0.9,0) / height 1.8 / radius 0.35`로
   추가했다.
4. **`VoteIndicator` 자식 오브젝트 추가.** `PlayerColorVoteIndicator.indicator`(`SpriteRenderer`)가
   붙을 곳이 없어서, 머리 위(로컬 좌표 `(0, 2.2, 0)`)에 빈 오브젝트를 만들고 `SpriteRenderer`를 붙였다.
5. **`ColorSelectionPanel.cs` 신규 작성.** 6.1은 "라운드/남은시간을 표시하고 이미 확정된 색은 잠근다"는
   프로즈만 있었고 코드가 없었다. 매 프레임 Room 프로퍼티를 읽어 라벨 텍스트를 갱신하고, `Color0..
   Color(k-1)`을 확인해 `ColorSwatchButton.SetLocked`를 호출하는 컨트롤러를 새로 만들었다.
6. **`ColorSelectionManager.Update()`에 `PhotonNetwork.InRoom` 가드 추가.** 원래 스니펫은 방에 들어와
   있다는 전제로 바로 `PhotonNetwork.CurrentRoom.CustomProperties`에 접근했는데, 오프라인 모드
   부트스트랩 직후(아직 방을 만들기 전)에는 `CurrentRoom`이 `null`이라 `NullReferenceException`이
   터지는 걸 Play Mode 테스트로 실제로 잡았다. `PlayerPaintCanvas`/`PlayerColorDisplay`/
   `RoomLifecycleWatcher`는 처음부터 이 가드를 넣어뒀었는데 `ColorSelectionManager`만 빠져 있었다.
7. **셰이더 재사용**: `brushStampMaterial`/`finalizeStampMaterial`을 별도 셰이더 2개가 아니라, 하나의
   `PaintStamp` 셰이더에 `_RespectLock` 프로퍼티를 두고 머티리얼 2개(값만 1/0으로 다름)로 구현했다.
   5.2가 "같은 인터페이스를 쓰되 목적지 알파 검사 여부만 다르다"고 명시했던 것과 정확히 맞아떨어지는
   방식이라 그대로 채택했다.

### 9.3 검증 결과

- **컴파일**: 스크립트/셰이더를 하나씩 추가할 때마다 `read_console`로 에러 0건을 확인하며 진행했다
  (총 20회 이상 반복 확인). 최종적으로도 에러/경고 0건.
- **라운드 자동 진행 (Play Mode, 오프라인 모드 + 코드로 방 생성)**: `StartColorSelection()` 호출 →
  `IsOpen=false` 확인 → 라운드 타임아웃 시 자동으로 `ColorVoteTally.Resolve`가 실행되어 `RoundIndex`가
  0→1→2→3→5(완료)로 정상 진행됨을 실제로 관찰했다. 확정된 4색(`Color0~3`)이 `6,2,0,3`처럼 **항상
  서로 다른 값**으로 나와 3.3의 중복 금지 규칙이 실제로 지켜짐을 확인했다.
- **술래 지정**: `TaggerActorNumber`가 정상적으로 채워지고, `TaggerVariantSet`(예: `7,2,0,3`)이 확정
  4색과 겹치지 않는 값으로 슬롯 하나만 바뀌어 있음을 확인했다(4.4 규칙 검증).
- **UI**: `ColorSelectionPanel`이 라운드 진행에 따라 "N / 4"와 남은 시간을 실시간으로 갱신하고, 이미
  확정된 라운드의 스와치가 비활성화(잠김)되는 것을 스크린샷과 코드 조회로 확인했다. 처음에 Canvas가
  기본값인 `WorldSpace`로 생성되어 화면에 아무것도 안 보이는 문제를 발견해 `ScreenSpaceOverlay`로
  고쳤다(이 프로젝트의 다른 UI Canvas 예시가 없어 기본값을 그대로 썼던 게 원인).
- **페인팅**: `PlayerPaintCanvas`가 인스턴스별 `RenderTexture`를 만들고 캐릭터 렌더러 머티리얼을
  `ColorTag/PlayerPaintedSkin`으로 교체하는 것을 확인했고, `StampBrush`를 직접 호출해 예외 없이
  스탬프가 적용됨을 확인했다.
- **술래 색상 치환 / 머리 위 인디케이터 — 부분 검증**: `PlayerColorDisplay.TryApplyTaggerColor()`의
  앞단 조건(라운드 완료, 술래 여부 판정 전까지)은 전부 정상 통과하지만, **`pv.Owner`가 이 테스트
  환경에서는 항상 `null`이라 마지막 단계까지 도달하지 못함을 발견했다** — `PlayerTestScene`의 캐릭터가
  `PhotonNetwork.Instantiate()`가 아니라 씬에 직접 배치된 오브젝트라서, Photon이 정식 소유자
  (`Owner`)를 부여하지 않기 때문이다(`pv.IsMine`은 정상적으로 `true`). `PlayerColorDisplay`와
  `PlayerColorVoteIndicator` 둘 다 "내 캐릭터가 아닌 다른 사람 캐릭터"도 판정해야 해서 `pv.Owner`
  비교가 설계상 맞는 방식이고, 코드를 테스트 환경에 맞춰 바꾸지는 않았다 — 이 부분은 실제
  `PhotonNetwork.Instantiate()`로 스폰되는 멀티플레이 환경(8장의 "2~4인 실제 접속 테스트")에서만 완전히
  검증할 수 있는, 원래부터 예정되어 있던 후속 검증 항목이다.
- 테스트용으로 씬에 추가했던 `ColorTagManagers`(매니저 3종 보관)와 `GameUICanvas`(+`ColorSelectionPanel`
  인스턴스)는 `PlayerTestScene`에 그대로 남겨뒀다 — 실제 게임 씬이 아직 없는 상태에서 이 시스템을
  Play Mode로 계속 검증할 수 있는 장소로 유지하는 편이 낫다고 판단했다(§12.5가 이동 시스템을 위해
  이 씬을 상시 테스트 환경으로 남겨둔 것과 같은 맥락).

### 9.4 남겨둔 것 (계획대로 범위 밖)

- `GameLobbyScene`은 여전히 존재하지 않아 7.3의 씬 전환 자체는 검증하지 못했다(전환 직전까지의 로직은
  검증됨).
- `GameManager`를 `HideOrSeekPlayer.prefab` 스폰으로 바꾸는 작업은 `PlayerControllPlan.md` 13.7에서
  이미 범위 밖으로 분류되어 있어 손대지 않았다.
- 붓 커서 텍스처(`BrushSettingsSO.cursorTexture`)와 팔레트 10색의 최종 Hex 값은 여전히 아트팀 확정
  전이라 placeholder 상태다(3.1, 0.2 참고).
- 붓 스탬프/색상 치환 셰이더는 기능 검증만 마쳤고, 실제 브러시 질감이나 경계 안티에일리어싱 같은
  비주얼 다듬기는 하지 않았다.
- 2~4인 실제 멀티플레이 접속 테스트, 마스터 이관, 퇴장 시나리오는 이 세션에서는 단일 오프라인
  클라이언트로만 검증했다 — 8장의 해당 항목은 아직 실제 다인원 테스트가 필요하다.

---

## 10. 사용자 테스트 중 발견된 문제 — 원인 분석 및 반영 (2026-08-14)

`OfflineModeBootstrap` 자동 시작으로 직접 Play해보고 주신 피드백 2건을 씬의 실제 컴포넌트 값을 하나하나
조회해서 원인을 확정했다. **둘 다 로직/네트워크 버그가 아니라 "비주얼 에셋·스타일이 비어있어서 눈에
안 보이는" 문제였다** — 데이터는 정상적으로 흐르고 있는데 화면에 그 결과가 드러나지 않았던 것이다.
아래는 원인 분석이며, **코드/에셋 수정은 아직 하지 않았다.**

### 10.1 스와치에서 색을 골라도 붓 커서가 안 뜨는 문제 🔄 진행중 (사용자 요청으로 이번 반영에서 스킵)

**증상**: 색상 라운드 중 스와치를 클릭해도(사실은 라운드가 시작된 순간부터) 마우스 커서가 기본
화살표 그대로고 붓 아이콘으로 바뀌지 않는다.

**원인**: 로직 자체는 정상이다. `BrushCursorController.OnRoomPropertiesUpdate`가 `RoundIndex` 변화를
정확히 감지하고, `RoundIndex`가 0~3(색상 라운드)이면 `Cursor.SetCursor(brushSettings.CursorTexture,
brushSettings.CursorHotspot, CursorMode.Auto)`를 호출하도록 설계돼 있다(5.1). 문제는 그 뒤에 넘기는
`brushSettings.CursorTexture` 자체가 **`null`이라는 점**이다:
- `Assets/03. SO/ColorTag/DefaultBrushSettings.asset`의 `Cursor Texture` 필드는 처음부터 실제 붓
  아이콘 그래픽이 없어 비워둔 placeholder 상태였다(9.4에서 이미 "아트팀 확정 전"으로 남겨뒀던 항목).
- Unity의 `Cursor.SetCursor(texture, hotspot, mode)`는 `texture`가 `null`이면 **시스템 기본 커서로
  되돌리는 것과 완전히 동일하게 동작**한다. 즉 `isColorRound`가 `true`여서 "커스텀 커서로 바꾸는" 분기를
  타더라도, 실제로 넘기는 텍스처가 없으니 결과적으로 `isColorRound`가 `false`일 때 호출하는
  `Cursor.SetCursor(null, ...)`와 픽셀 하나 다르지 않은 코드를 실행하게 된다.
- 그래서 라운드 상태가 바뀌어도(콜백은 정상적으로 계속 발생) 커서 모양은 항상 똑같아 보였던 것이다.

**정리**: 원인은 코드가 아니라 **`cursorTexture`에 채울 실제 이미지 에셋이 아직 없다는 것** 하나뿐이다.
브러시 모양 텍스처(권장: 32×32 이하의 작은 PNG, Texture Type을 "Cursor"로 임포트)를 만들어서
`DefaultBrushSettings.asset`의 `Cursor Texture`(+ 클릭 지점을 정할 `Cursor Hotspot`)에 채워 넣으면
해결된다.

**진행 상태**: 이번 반영 범위에서는 **의도적으로 스킵**했다(사용자가 "붓 이미지 안 뜨는 거는 스킵하고"로
명시). 코드는 이미 정상 동작하므로, 실제 붓 텍스처 에셋만 준비되면 그때 `Cursor Texture`/`Cursor
Hotspot`을 채우는 것으로 바로 해결 가능하다 — 다음에 진행할 작업으로 남겨둔다.

### 10.2 라운드 시간이 1초 단위로 안 보이는 문제 ✅ 완료

**증상**: 색상 라운드가 시작돼도 패널 오른쪽 위에 남은 시간이나 "N / 4" 표시가 눈에 안 보인다.

**원인**: 이것도 로직 문제가 아니다. Play Mode에서 `ColorSelectionPanel`의 실제 텍스트 값을 코드로
직접 읽어보면 `round='2 / 4'`, `time='18'`처럼 **매 초 정확하게 갱신되고 있는 것을 확인했다** —
`Update()`가 매 프레임 `Mathf.CeilToInt(remaining)`으로 남은 시간을 다시 계산해 `timeLabel.text`에
쓰고 있고, `CeilToInt` 특성상 화면에 보이는 숫자 자체도 이미 정확히 1초 단위로만 바뀐다(20, 20, ...,
19, 19, ..., 처럼 정수 경계에서만 값이 바뀜). 요청하신 "1초 단위 표시"는 사실 이미 구현돼 있었다.

진짜 원인은 **글자색과 배경색이 똑같다는 것**이다. 씬의 실제 컴포넌트 값을 조회해보니:
- `RoundLabel`/`TimeLabel`(`TextMeshProUGUI`)의 글자색(`m_fontColor`/`faceColor`) = **흰색
  (255,255,255,255)** — UI 오브젝트를 만들 때 색을 따로 지정하지 않아 TMP 기본값 그대로 남아있다.
- 부모인 `ColorSelectionPanel`의 배경 `Image` 색 = **흰색(255,255,255,255)**, `Sprite`도 없어서 그냥
  단색 흰 사각형으로 렌더링된다 — 이것도 따로 지정하지 않아 Unity UI 기본값 그대로다.
- 즉 **흰 배경 위에 흰 글씨**라 값은 매초 정확히 바뀌고 있는데 육안으로는 전혀 구분이 안 되는 것이다.
  스와치 10개는 3.1 팔레트 색으로 명시적으로 칠해뒀기 때문에 눈에 보였지만, 라벨 텍스트와 패널
  배경은 색을 지정하는 걸 빠뜨렸다.

**추가로 확인이 필요한 부분**: "오른쪽 위"가 **화면 전체의 오른쪽 위 모서리**를 의미하신 거라면 현재
배치와 다를 수 있다. 지금 `ColorSelectionPanel`은 화면 **상단 중앙**에 배치돼 있고(6.1), `TimeLabel`은
그 패널 **내부의** 오른쪽 위 구석에 있다 — 그래서 화면상으로는 "정중앙보다 살짝 위, 살짝 오른쪽"
정도의 위치가 된다. 화면의 진짜 오른쪽 위 모서리로 옮기길 원하시면 별도로 알려주시면 반영하겠다.

**정리**: 색상 대비를 주면(예: 패널 배경을 반투명 검정 계열로, 글자색을 흰색 그대로 유지하거나 반대로
배경은 밝게 두고 글자색을 검정 계열로) 바로 해결된다. 나중에 배경색이 바뀔 수도 있으니 텍스트에
아웃라인을 추가해두면 더 안전하다.

**반영 내용**: `Assets/Resources/UI/Scene/ColorSelectionPanel/ColorSelectionPanel.prefab`의 배경
`Image.color`를 흰색(1,1,1,1)에서 **반투명 검정(0,0,0,0.75)**으로 변경했다(글자색은 이미 흰색이라
그대로 둠 — 최소 변경으로 대비 확보). "오른쪽 위" 위치(패널 내부 vs 화면 전체 모서리) 관련 확인
요청에는 아직 답을 못 받아서, 위치 자체는 6.1 설계(화면 상단 중앙 패널 + 그 안의 오른쪽 위 타이머)
그대로 두었다 — 필요하면 알려주시면 화면 우상단으로 재배치하겠다.

**검증**: Play Mode 스크린샷으로 "1 / 4"(라운드 라벨)와 "13"(남은 시간, 매초 정확히 감소)이 이제
어두운 패널 배경 위에 흰 글씨로 또렷하게 보이는 것을 직접 확인했다.

### 10.3 (참고, 이번 두 문제와는 무관) 발견한 별개의 잠재 이슈

`ColorSelectionPanel.Update()`가 `isColorRound`가 `false`가 되는 순간(라운드 4까지 다 끝나
`RoundIndex`가 완료 상태로 바뀔 때) `gameObject.SetActive(false)`로 **자기 자신을 비활성화**한다.
`SetActive(false)`된 오브젝트는 이후 `Update()`가 더 이상 호출되지 않으므로, 이 컴포넌트는 스스로를
다시 켤 방법이 없다 — 지금 설계(재대결 없음, 색상 선택은 게임당 한 번뿐)에서는 "다 끝나면 계속 숨겨진
채로 있는 게" 정확히 의도한 동작이라 실제로 문제를 일으키진 않지만, "자기 `Update()`로 자기 자신을
꺼버리는" 패턴 자체는 다소 위험한 코드 스멜이라 기록만 해둔다. 지금 당장 고칠 필요는 없다.

---

## 11. 클릭해서 칠해도 반응이 없는 문제 — 원인 분석 및 반영 ✅ 완료 (2026-08-14)

### 11.1 증상

색상 라운드 중 내 캐릭터를 마우스로 클릭(좌클릭 유지)해도 어디를 클릭하든 눈에 보이는 변화가 없다.
"9.3 검증 결과"에서 페인팅을 "검증 완료"로 적었던 것과 모순돼 보이는데, 그 검증은 `StampBrush`를
**리플렉션으로 직접 호출하면서 UV 좌표를 `(0.5, 0.5)`로 수동 주입**한 것이었다 — 실제 클릭 →
레이캐스트 → UV 계산으로 이어지는 경로 자체는 이번에 처음으로 실사용 테스트를 거친 것이고,
바로 그 경로에서 문제가 발견됐다.

### 11.2 원인 (확정)

`PlayerPaintCanvas.Update()`의 실제 코드(85행):

```csharp
Ray ray = localCamera.ScreenPointToRay(Input.mousePosition);
if (!Physics.Raycast(ray, out RaycastHit hit)) return;
if (hit.collider != paintableCollider) return;
...
StampBrush(hit.textureCoord, voteColor);
```

여기서 쓰는 `RaycastHit.textureCoord`는 **`MeshCollider`(또는 `TerrainCollider`)를 맞췄을 때만
실제 UV 값을 계산**해주는 필드다. `BoxCollider`/`SphereCollider`/`CapsuleCollider`처럼 수학적으로
정의되는(메시 기반이 아닌) 콜라이더는 애초에 UV 매핑이라는 개념 자체가 없어서, 이 필드는 항상
기본값 `(0, 0)`으로 채워진 채로 돌아온다 — 값이 이상해지는 게 아니라, 유효한 계산이 아예 일어나지
않는 것이다.

그런데 `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab`에서 `PlayerPaintCanvas.paintableCollider`가
실제로 가리키고 있는 것은 `UnityEngine.CapsuleCollider`다(`manage_prefabs get_info`로
`rootComponentTypes`에 `"UnityEngine.CapsuleCollider"`가 포함된 것을 재확인). 즉:

- 화면 어디를 클릭하든 `hit.collider == paintableCollider`(캡슐)까지는 정상적으로 통과한다.
- 하지만 그 다음 `hit.textureCoord`는 클릭 위치와 무관하게 **항상 `(0, 0)`**이다.
- 결과적으로 `StampBrush`는 항상 텍스처의 정확히 같은 한 점에만 스탬프를 찍고 있다. 완전히 아무
  반응이 없는 게 아니라, "클릭할 때마다 같은 자리에 아주 작은(반경 최대 0.1 UV) 점 하나가 계속
  덧칠되고 있을 가능성"이 높다 — 그 자리가 캐릭터 텍스처 아틀라스에서 눈에 잘 안 띄는 부위(이음새,
  안 보이는 뒷면 등)에 매핑돼 있으면 육안으로는 "아무 반응 없음"과 구별이 안 된다.

### 11.3 부가로 발견한 견고성 문제 (원인일 가능성은 낮음, 함께 기록)

`PlayerPaintCanvas.InitPaintCanvas()`(38~40행)는 `new RenderTexture(...)` 후 `.Create()`만
호출하고, 내용을 명시적으로 투명(`Color.clear`)하게 지우지 않는다:

```csharp
PaintCanvas = new RenderTexture(canvasSize, canvasSize, 0, RenderTextureFormat.ARGB32);
PaintCanvas.name = $"PaintCanvas_{gameObject.name}_{pv.ViewID}";
PaintCanvas.Create();
```

Unity는 새로 만든 `RenderTexture`의 초기 픽셀 내용을 문서상 보장하지 않는다 — 플랫폼/그래픽
드라이버에 따라 0으로 초기화될 수도, 안 될 수도 있다. 만약 알파 채널에 쓰레기 값이 섞여 있다면
`brushStampMaterial`의 "이미 잠긴(알파=1) 픽셀은 건드리지 않음" 로직이 캔버스 전역을 "이미 칠해진
것"으로 오판해 스탬프가 아예 먹지 않는 또 다른 원인이 될 수 있다. 다만 지금까지 캐릭터가 시작부터
비정상적으로 보인 적은 없었고(이 프로젝트는 Windows/DirectX 빌드라 실제로는 0으로 초기화되고 있는
것으로 추정됨), 11.2의 `CapsuleCollider` 문제만으로도 증상이 충분히 설명되기 때문에 **이번 증상의
직접 원인은 아닐 가능성이 높다** — 그래도 문서화되지 않은 동작에 기대는 부분이라, 고칠 때 같이
`GL.Clear`(또는 동등한 방식)로 명시적으로 초기화해두는 게 안전하다.

### 11.4 해결 방향 (미반영, 분석만 — 다음에 구현 시 참고)

`paintableCollider`를 `CapsuleCollider`가 아니라 캐릭터 메시(`Ch36`, `SkinnedMeshRenderer`)에 맞는
`MeshCollider`로 교체하는 것이 근본 해결책이다. 구현 시 고려해야 할 점을 미리 정리해둔다:

1. **애니메이션 포즈 불일치**: `MeshCollider`에 연결하는 메시는 보통 `SkinnedMeshRenderer.sharedMesh`
   (바인드 포즈/기본 자세)다 — 캐릭터가 걷거나 점프하는 실시간 애니메이션 포즈를 따라가지 않는다.
   완벽하게 동기화하려면 `SkinnedMeshRenderer.BakeMesh()`로 현재 포즈를 매 프레임 구워야 하는데
   비용이 있다. 색상 라운드 중에는 대부분 캐릭터가 가만히 서서 클릭당하는 상황일 것으로 예상되므로,
   1차적으로는 기본 포즈 메시로 `MeshCollider`를 만드는 것으로 충분할 가능성이 높다 — 실사용
   테스트에서 애니메이션 중 클릭 오차가 실제로 체감되는지 확인 후 `BakeMesh` 적용 여부를 결정한다.
2. **`convex` 불필요**: `Physics.Raycast`(레이캐스트 히트 테스트) 용도로만 쓸 것이므로
   `MeshCollider.convex = false`(비볼록)로 둬도 무방하다. 볼록 제약은 리지드바디 충돌 처리에만
   해당된다.
3. **기존 `CapsuleCollider` 처리**: 지금 있는 캡슐 콜라이더는 애초에 이 레이캐스트 대상 용도로만
   추가했던 것이라 `MeshCollider`로 교체하면 쓰임새가 없어진다 — 그대로 남겨둘지(다른 용도 대비),
   제거할지는 별도 확인이 필요하다.
4. `paintableCollider` 필드가 가리키는 대상을 루트 오브젝트가 아니라 `Ch36`(메시가 실제로 있는 자식
   오브젝트) 쪽 `MeshCollider`로 다시 연결해야 한다.
5. 11.3에서 발견한 `RenderTexture` 초기화 문제도 같이 고치는 게 안전하다 — `InitPaintCanvas()`에서
   `.Create()` 직후 `RenderTexture.active`를 바꿔 `GL.Clear(true, true, Color.clear)`를 호출하는
   식으로 명시적으로 투명하게 지운다.

### 11.5 구현 내용 (2026-08-14 반영)

11.4에서 정리한 방향대로 실제 반영했다:

1. `Assets/04. Prefabs/Resources/HideOrSeekPlayer.prefab`의 `Ch36`(`SkinnedMeshRenderer`) 오브젝트에
   `MeshCollider`를 새로 추가하고, `sharedMesh`를 `Ch36`의 `SkinnedMeshRenderer.sharedMesh`(바인드
   포즈 메시)로 지정했다. `convex = false`(비볼록) — 순수 레이캐스트 히트 테스트 용도라 문제없다(11.4-2).
2. `PlayerPaintCanvas.paintableCollider`를 이 새 `MeshCollider`(Ch36)로 재연결했다.
3. **기존 루트의 `CapsuleCollider`는 제거했다.** 원래 11.4-3에서는 "남겨둘지 확인 필요"로 열어뒀는데,
   실제로 확인해보니 남겨두면 새 문제가 생긴다는 걸 발견했다 — 캡슐이 메시 표면보다 카메라에 더
   가까운 지점(예: 허리처럼 메시가 좁아지는 부위)을 감싸고 있으면 `Physics.Raycast`가 (가장 가까운
   히트를 반환하는 특성상) 캡슐 표면을 먼저 맞혀버려 `hit.collider != paintableCollider(MeshCollider)`
   조건에 걸려 페인팅이 다시 막힌다. 코드 전체에서 `CapsuleCollider`를 참조하는 곳이 이 페인팅
   레이캐스트 용도 하나뿐이었음을 grep으로 재확인한 뒤 제거했다(대체 용도 없음).
4. `PlayerPaintCanvas.InitPaintCanvas()`에 11.3에서 지적한 `RenderTexture` 명시적 초기화
   (`RenderTexture.active` 전환 + `GL.Clear(true, true, Color.clear)`)를 추가했다.
5. `BrushCursorController`(12장)가 표면 위치를 알아야 해서, `PlayerPaintCanvas`에
   `PaintableCollider`/`CurrentBrushRadius`/`IsMine` 읽기 전용 프로퍼티를 추가로 노출했다.

### 11.6 검증 결과 (Play Mode)

실제 클릭 경로와 동일한 API(`Camera.main.ScreenPointToRay` → `Physics.Raycast` → `hit.textureCoord`)를
서로 다른 두 화면 좌표(캐릭터 상체, 캐릭터 하체/골반)에 대해 직접 호출해 확인했다:

- 상체 지점 → `collider=Ch36(MeshCollider)`, `uv=(0.14, 0.27)`
- 골반 지점 → `collider=Ch36(MeshCollider)`, `uv=(0.97, 0.38)`

두 값이 **서로 다르고 둘 다 유효 범위(0~1) 안**이라는 점에서, 이전처럼 `(0,0)`에 고정되는 문제가
해결됐음을 확인했다. 이어서 실제 `StampBrush`(private, 리플렉션 호출이지만 이번엔 하드코딩이 아니라
방금 구한 진짜 UV 두 값을 그대로 사용)로 두 지점에 칠한 뒤 `PaintCanvas`를 `ReadPixels`로 읽어보니:

- `uv=(0.14,0.27)` 지점 → 칠한 색(Yellow, `RGBA(1, 0.831, 0, 1)`)
- `uv=(0.97,0.38)` 지점 → 동일하게 Yellow
- 손대지 않은 `uv=(0.5,0.5)` 대조군 → `RGBA(0,0,0,0)` (완전 투명 — 11.3의 `RenderTexture` 초기화
  수정도 함께 검증됨)

스크린샷으로도 캐릭터의 **등 위쪽(어깨뼈 부근)**에 칠한 자국이 실제로 보이는 것을 확인했다(스탬프
모양이 완벽한 원이 아니라 다소 각진 형태로 보이는데, 이는 9.4에서 이미 "브러시 질감 비주얼 다듬기는
하지 않음"으로 범위 밖으로 남겨둔 부분이라 이번 수정과는 무관하다). 컴파일 에러/경고 0건.

---

## 12. 3D 붓 모델(Brush.fbx)을 마우스 포인터에 반영 ✅ 완료 (2026-08-14)

`Assets/04. Prefabs/Resources/Brush.fbx`가 새로 추가됐다. 10.1에서 다뤘던 "붓 커서가 안 뜨는 문제"는
`BrushSettingsSO.cursorTexture`(2D `Texture2D`)가 비어있던 게 원인이었는데, 이번에 채워진 에셋은
**2D 텍스처가 아니라 3D 모델(FBX)**이라 애초에 `Cursor.SetCursor` 방식과는 형태가 안 맞는다.
그래서 10.1을 그대로 이어서 구현하는 게 아니라, **커서 표현 방식 자체를 다시 설계**해야 한다.
아래는 실제 파일을 열어 구조를 확인한 뒤 세운 계획이며, **아직 코드/프리팹을 만들지 않았다.**

### 12.1 Brush.fbx 실체 확인 결과

Unity 에디터에서 `AssetDatabase`로 직접 조회한 결과:

- 계층 구조가 단순하다: 루트 GameObject `Brush` 하나에 `MeshRenderer` + `MeshFilter`만 붙어있고,
  자식/뼈대(Bone)/애니메이션 클립은 전혀 없다 (`ModelImporter.animationType = 2`(Generic)이지만
  `clipAnimations`는 빈 배열) — **정적인 메시 하나**다. 즉 "붓이 움직이며 칠하는" 애니메이션은
  지금 에셋만으로는 불가능하고, 나중에 별도로 붙여야 하는 선택 사항이다.
- 메시(`Mesh_0`)가 **버텍스 188,554개**로 꽤 무겁다 — 상시 화면에 떠 있는 커서용 오브젝트치고는
  고폴리곤이라, "최적화를 고려한 코드 작성" 규칙상 나중에 메시 감소(decimate)나
  `Mesh Compression` 설정을 검토할 필요가 있다. 지금 당장 막을 정도는 아니라 1차 구현 이후로 미룬다.
- 머티리얼이 `Default-Material`(빈 기본 머티리얼)로 잡혀 있다 — 텍스처/색이 임포트된 게 없어서
  실제로 쓰려면 새 머티리얼을 만들어 입혀야 한다.
- 루트 트랜스폼 자체에 `rot=(270, 0, 0)`, `scale=(100, 100, 100)`가 이미 박혀있다(FBX 임포트 시
  단위 변환으로 흔히 생기는 현상). 이 루트를 그대로 붙여 쓰면 위치/회전 계산이 헷갈리므로, 빈
  부모로 한 번 감싸서 피벗을 깔끔하게 정리한 프리팹을 새로 만드는 걸 권장한다.

### 12.2 방식 선택: OS 하드웨어 커서(2D) vs 월드 스페이스 3D 오브젝트

두 가지를 검토했다.

**A안 — 3D 모델을 텍스처로 구워서 여전히 `Cursor.SetCursor` 사용**: 별도 카메라로 Brush 메시를
`RenderTexture`에 렌더링한 뒤 `Texture2D`로 변환해 기존 10.1 설계(`BrushSettingsSO.cursorTexture`)를
그대로 재사용. 기존 코드 변경이 가장 적다는 장점이 있지만, OS 하드웨어 커서는 평면 비트맵이라
3D 모델의 입체감(조명, 회전, 원근)이 전부 사라지고, 굳이 3D 모델을 구워 2D로 만드는 추가 공정이
필요해 이 모델을 3D로 준 의도와 맞지 않는다.

**B안 — 월드 스페이스 3D 오브젝트가 마우스 레이캐스트를 따라다님 (권장)**: OS 커서는
`Cursor.visible = false`로 완전히 숨기고, `Brush` 모델을 씬에 인스턴스화해서 매 프레임 마우스가
가리키는 3D 위치로 이동/회전시킨다. 캐릭터 표면(11장에서 `MeshCollider`로 바꿀 예정인
`paintableCollider`)에 정확히 맞으면 그 지점(`hit.point`)과 표면 방향(`hit.normal`)에 붓이 닿아있는
것처럼 배치할 수 있어, 실제로 "캐릭터를 붓으로 칠한다"는 연출과 훨씬 잘 맞는다. 11장 수정으로
`hit.point`/`hit.normal`이 정확해지는 것과도 자연스럽게 맞물린다.

**결론: B안(월드 스페이스 3D 오브젝트)을 채택**한다. 3D 모델을 일부러 준비해준 의도, 그리고
페인팅 자체가 이미 표면 레이캐스트 기반으로 설계돼 있다는 점(11장) 둘 다 B안과 맞아떨어진다.

### 12.3 B안 상세 설계

1. **프리팹 정리**: `Brush.fbx`를 직접 쓰지 않고, 빈 부모 오브젝트로 감싸 피벗/스케일을 정리한
   `Assets/04. Prefabs/Resources/BrushCursor.prefab`을 새로 만든다. `Resources` 폴더 아래이므로
   런타임에 `Resources.Load<GameObject>("BrushCursor")`로 로드해 인스턴스화할 수 있다.
2. **머티리얼**: `Default-Material` 대신 색 변경이 가능한 머티리얼(Standard 또는 간단한 Unlit)을
   새로 만들어 입힌다. `MaterialPropertyBlock`으로 매 프레임/색 변경 시점에 헤드 색을 바꾼다(공유
   머티리얼 인스턴스를 직접 건드리지 않기 위함 — `PlayerPaintCanvas`가 캐릭터 렌더러에 `.material`을
   쓸 때와 같은 이유).
3. **레이어 분리**: `BrushCursor` 인스턴스는 자신이 마우스 레이캐스트에 맞아버리면 안 되므로,
   전용 레이어(예: `Ignore Raycast` 또는 새 `BrushCursor` 레이어)에 두고, `PlayerPaintCanvas`의
   `Physics.Raycast` 호출에 해당 레이어를 제외하는 레이어마스크를 추가해야 한다(지금은 마스크 없이
   전체 레이어 대상으로 레이캐스트하고 있어 이 부분을 같이 손봐야 함, 11장 수정과 함께 처리).
4. **표시/숨김 제어**: 기존 `BrushCursorController`(현재 `Cursor.SetCursor` 호출 담당, 5.1/10.1)의
   책임을 그대로 유지하되 내부 구현만 교체한다 — 라운드가 색상 라운드(0~3)로 바뀌면 `BrushCursor`
   인스턴스를 `Instantiate`/활성화하고, 라운드가 끝나면 비활성화(또는 파괴)한다. 클래스 이름과
   "라운드 상태 → 커서 표시 여부"라는 역할은 유지해 변경 범위를 최소화한다.
5. **위치/회전 추적**: 매 프레임(또는 `PlayerPaintCanvas`가 이미 하고 있는 레이캐스트 결과를
   공유받아) 다음과 같이 배치한다.
   - 자신의 `paintableCollider`(캐릭터 표면)에 레이캐스트가 맞으면: `hit.point`에 위치시키고,
     `Quaternion.LookRotation(hit.normal)` 등으로 표면과 맞닿은 각도로 회전시킨다.
   - 맞지 않으면(허공을 가리킬 때): 카메라 앞 고정 거리에 띄우거나, 아예 `SetActive(false)`로
     숨긴다 — 어느 쪽이 자연스러운지는 실제로 보면서 정하는 게 나을 것 같아 두 옵션을 남겨둔다.
6. **브러시 크기 시각화**: `PlayerPaintCanvas.currentBrushRadius`(마우스 휠로 0.005~0.1 사이 조절,
   5.2)를 `BrushCursor` 인스턴스의 로컬 스케일에 실시간으로 반영해서, 지금 붓이 얼마나 큰지 화면에서
   바로 보이게 한다.
7. **현재 붓 색 시각화**: 스와치로 고른 `voteColor`(`GetCurrentVoteColorIndex()`)가 바뀔 때마다
   2번의 머티리얼 색을 갱신해서, 지금 어떤 색을 칠할지 붓 헤드 색으로 보여준다.

### 12.4 데이터 구조 변경 (예정)

- `BrushSettingsSO`의 `cursorTexture`/`cursorHotspot` 필드는 B안 채택 시 더 이상 쓰이지 않는다.
  제거하고 대신 `GameObject cursorPrefab`(또는 프리팹 경로 문자열) 필드를 추가하는 방향을 제안한다.
  (10.1에서 "이 텍스처만 채우면 해결"이라고 적어뒀던 부분은 이 계획으로 대체됨.)
- `PlayerPaintCanvas`의 `Physics.Raycast` 호출에 레이어마스크 인자를 추가해야 한다(12.3-3).

### 12.5 구현 내용 (2026-08-14 반영, B안 그대로 채택)

1. **`BrushCursor.prefab` 신규 제작** — 빈 부모 `BrushCursor`(스케일 0.05)에 `Brush.fbx`를 자식으로
   인스턴스화(원본의 로컬 rot=270°/scale=100은 그대로 보존, 부모 스케일로만 전체 크기를 조절).
   Play Mode 밖에서 미리 스크린샷으로 확인해보니 손잡이+브러시 팁이 있는 실제 붓 모양이었고(단순한
   원판이 아니었다), 0.05 배율에서 캐릭터(약 1.8m) 대비 크기가 자연스러워 그대로 채택했다.
2. **`BrushCursorMaterial.mat`** — `Standard` 셰이더로 신규 제작, `Default-Material`(빈 재질) 대신
   `Brush.fbx` 인스턴스의 `MeshRenderer`에 연결. 색은 `MaterialPropertyBlock`으로 매 프레임 갱신(공유
   머티리얼 애셋 자체는 건드리지 않음 — `PlayerPaintCanvas`가 캐릭터 렌더러에 `.material` 인스턴스를
   쓰는 것과 같은 이유).
3. **`BrushSettingsSO`** — `cursorTexture`/`cursorHotspot` 필드를 제거하고 `cursorPrefab`(GameObject)
   과 `cursorWorldScale`(기본 0.05) 필드로 교체했다(12.4의 계획대로).
4. **`BrushCursorController` 전면 재작성** — 씬에 하나뿐인 매니저(`ColorTagManagers`)로 유지하되,
   내부 구현을 `Cursor.SetCursor` 방식에서 월드 스페이스 오브젝트 추적 방식으로 교체했다. 매 프레임
   `FindObjectsByType<PlayerPaintCanvas>()`로 로컬 플레이어(`IsMine`)를 찾아 그 `PaintableCollider`를
   대상으로 직접 레이캐스트하고, 맞으면 `hit.point`/`hit.normal`로 배치, `Quaternion.FromToRotation
   (Vector3.up, hit.normal)`로 표면에 놓인 것처럼 회전시킨다.
   - **레이어 분리는 결국 불필요했다**: 12.3-3에서 계획했던 "붓 오브젝트 전용 레이어 분리"는, 실제로
     `Brush.fbx`에 `Collider`가 전혀 없다는 걸 확인하고 나서(임포터의 `addColliders: 0`)
     불필요하다고 판단해 생략했다 — `Physics.Raycast`는 `Collider`가 있는 오브젝트만 맞히므로, 붓
     오브젝트가 자기 자신의 레이캐스트에 걸릴 방법 자체가 없다.
   - **OS 커서 숨김 범위를 계획보다 더 정교하게 조정했다**: 애초 설계(0.1-2, 12.2)는 "라운드 중
     내내 커서가 붓으로 바뀐다"는 2D 아이콘 교체 시절의 문구였는데, 그대로 OS 커서를 라운드 내내
     숨기면 스와치 패널을 클릭할 때 마우스 위치를 볼 수 없어 오히려 사용성이 나빠진다. 그래서
     **캐릭터 표면 위에 레이캐스트가 맞을 때만** `Cursor.visible = false` + 3D 붓 표시로 바꾸고, UI
     조작 등 표면 밖에서는 OS 커서를 그대로 둔다. 3D 오브젝트 방식으로 바뀌면서 자연스럽게 필요해진
     조정이라 판단해 반영했다.
5. **씬 wiring** — `DefaultBrushSettings.asset.cursorPrefab` → `BrushCursor.prefab`,
   `ColorTagManagers/BrushCursorController.palette` → `DefaultColorPalette.asset` 연결.

### 12.6 검증 결과 (Play Mode)

- 컴파일 에러/경고 0건.
- 색상 라운드(`RoundIndex` 0~3) 진입 시 `BrushCursorController`가 `brushSettings.CursorPrefab`으로
  실제 인스턴스(`"BrushCursor(Runtime)"`)를 생성하는 것을 확인했다(처음엔 비활성 상태로 대기).
- 이 환경은 OS 마우스를 직접 움직일 수 없어 실시간 추적 자체는 완전히 재현하지 못했지만, 매 프레임
  `Update()`가 되돌려놓는 것을 피하기 위해 컨트롤러를 잠시 `enabled=false`로 멈춘 뒤, 실제 코드와
  동일한 API(`ScreenPointToRay`→`Physics.Raycast`→`hit.point`/`hit.normal`)로 계산한 값을 그대로
  적용해보니 — 캐릭터 표면 위치에 붓이 정확히 놓이고, `MaterialPropertyBlock`으로 입힌 색(투표색
  Yellow)이 그대로 렌더링되는 것을 스크린샷으로 확인했다. 실시간 마우스 추적 자체(붓이 화면 움직임을
  따라오는지)는 실제 사람이 플레이하며 확인해야 하는 항목으로 남는다(9.3/9.4와 같은 성격의 한계).

### 12.7 남겨둔 것

- 188,554버텍스짜리 원본 메시를 그대로 쓰고 있어(12.1), 상시 표시되는 커서치고는 고polycount다 —
  당장 문제는 없지만 필요하면 나중에 메시 감소를 검토한다.
- 붓이 표면에 "닿는" 기준점은 `Brush.fbx`의 원래 피벗 그대로다(브러시 팁으로 피벗을 옮기는 정밀
  보정은 하지 않음) — 지금도 자연스러워 보이지만, 더 정교하게 다듬고 싶으면 후속 작업으로 가능하다.
- 브러시 크기 시각화(`CurrentBrushRadius` 기반 스케일)는 UV 반경 값을 그대로 스케일 배율에 곱하는
  근사치다(세계 단위와 UV 단위 사이에 정확한 변환 관계가 없어서다) — 상대적인 크기 변화를
  보여주는 용도로는 충분하다고 판단했다.

---

## 13. (부수적 발견) Photon 콜백 등록 누락 버그 수정 ✅ 완료 (2026-08-14)

12장의 `BrushCursorController`가 라운드가 시작돼도 전혀 반응하지 않는 걸 디버깅하다가 발견한, 이번
작업 범위 밖의 별도 버그다. 이미 "완료"로 표시했던 다른 기능 몇 개가 실제로는 한 번도 정상 동작한
적이 없었다는 뜻이라 별도 장으로 기록해둔다.

### 13.1 증상 및 원인

Photon PUN2에서 `MonoBehaviourPunCallbacks`를 상속해 `OnRoomPropertiesUpdate`/`OnPlayerPropertiesUpdate`/
`OnPlayerLeftRoom` 같은 콜백을 오버라이드하는 것만으로는 실제로 호출되지 않는다 — 반드시
`PhotonNetwork.AddCallbackTarget(this)`로 명시적으로 등록해야 한다(보통 `OnEnable`에서 등록,
`OnDisable`에서 `RemoveCallbackTarget`으로 해제). `PlayerPaintCanvas`는 이 패턴을 정확히 지키고
있었지만, grep으로 전수 조사해보니 **콜백을 실제로 쓰는 나머지 스크립트 4개는 전부 이 등록 코드가
빠져 있었다**:

- `BrushCursorController` (`OnRoomPropertiesUpdate`) — 이번에 새로 작성하며 원본 코드의 문제를
  그대로 이어받았다가 발견.
- `PlayerColorDisplay` (`OnRoomPropertiesUpdate`, 6.3 술래 색상 치환).
- `PlayerColorVoteIndicator` (`OnPlayerPropertiesUpdate`, 머리 위 투표색 인디케이터).
- `RoomLifecycleWatcher` (`OnPlayerLeftRoom`, `OnLeftRoom` — 7.2/7.3 술래 퇴장·인원 부족·정상 종료
  처리).

실제로 Play Mode에서 `VoteColorIndex`를 바꿔봤는데 `PlayerColorVoteIndicator`의 스프라이트 색이
전혀 바뀌지 않는 것으로 직접 재현/확인했다. `ColorSelectionManager`와 `ColorSelectionPanel`,
`HideOrSeekPlayer`도 `MonoBehaviourPunCallbacks`를 상속하지만 실제로는 어떤 콜백 메서드도 오버라이드
하지 않고 `Update()` 폴링이나 `IPunObservable`(별도 메커니즘, `PhotonView`의 Observed Components
목록으로 직접 호출됨)만 쓰고 있어서 이 버그의 영향을 받지 않는다 — 확인 후 손대지 않았다.

### 13.2 반영 내용

`PlayerPaintCanvas`에 이미 있던 정상 패턴 그대로 4개 스크립트에 `OnEnable`/`OnDisable`을 추가해
`AddCallbackTarget`/`RemoveCallbackTarget`을 등록·해제하도록 고쳤다. 컴파일 에러/경고 0건.

### 13.3 검증 결과 및 한계

수정 후에도 Play Mode에서 `VoteColorIndex`를 바꿔봤을 때 `PlayerColorVoteIndicator`의 색이 여전히
바뀌지 않는 것을 확인했는데, 원인을 더 파보니 이건 **이 버그와는 별개로 9.3에 이미 기록해둔 한계**
때문이었다 — `PlayerColorVoteIndicator.OnPlayerPropertiesUpdate`와 `PlayerColorDisplay.
TryApplyTaggerColor`는 둘 다 `targetPlayer(또는 taggerActorNumber) == pv.Owner`로 "내가 아닌 다른
사람 캐릭터"를 판별하는데, `PlayerTestScene`의 캐릭터가 `PhotonNetwork.Instantiate()`가 아니라 씬에
직접 배치된 오브젝트라 `pv.Owner`가 항상 `null`이다. 즉:

- **`AddCallbackTarget` 등록 자체는 이번 수정으로 확실히 고쳐졌다** — 콜백이 이제 실제로 호출된다.
- 다만 이 테스트 씬에서는 `pv.Owner == null`이라는 별도의 한계 때문에 `PlayerColorDisplay`/
  `PlayerColorVoteIndicator`의 최종 시각적 효과까지는 이 씬에서 재현할 수 없다 — 9.3에서 이미
  "실제 `PhotonNetwork.Instantiate()`로 스폰되는 멀티플레이 환경에서만 완전히 검증 가능"으로
  분류해둔 것과 정확히 같은 종류의 제약이다.
- `BrushCursorController`는 `pv.Owner`가 아니라 `pv.IsMine`으로 판별해서 이 제약에서 자유롭고,
  실제로 이번 수정 이후 `RoundIndex`가 바뀌자마자 `BrushCursor(Runtime)` 인스턴스가 정상적으로
  생성되는 것을 확인했다(12.6).
- `RoomLifecycleWatcher`는 플레이어 퇴장/게임 종료 시나리오라 이 테스트 씬(단일 클라이언트, 방
  나가기 트리거 없음)에서는 애초에 재현 조건 자체를 만들기 어려워 별도로 검증하지 못했다 — 8장의
  "2~4인 실제 접속 테스트" 때 함께 확인해야 하는 항목으로 남긴다.
