# 색상 결정 & 술래 지정 시스템 설계 (NetWorkPlan.md)

> 이 문서는 `UserPlan.md`에 기록된 요구사항을 바탕으로 작성한 **설계 문서**입니다.
> 아직 아무 것도 구현하지 않았습니다. 아래 내용을 검토하신 뒤 승인해주시면 그때 구현을 시작합니다.

---

## 0. 설계 해석 (반드시 검토 필요)

`UserPlan.md`의 문장이 여러 방식으로 해석될 수 있어, 아래와 같이 해석하고 설계를 진행했습니다.
**이 해석이 의도와 다르면 구현 전에 꼭 알려주세요.**

- "색상 4개를 정하고" = 전체 플레이어(2~4인)가 **다같이 투표**해서, 이번 판에서 사용할 **공용 4색 세트**를 라운드 4번에 걸쳐 하나씩 확정한다. (라운드 1 → 1번째 색, 라운드 2 → 2번째 색, ... 라운드 4 → 4번째 색)
- "만약 고르지 못했을 경우, 다수결이 정한 색상 지정" = 한 라운드 안에서 **일부 플레이어가 투표를 안 해도**, 투표한 사람들 중 **최다 득표 색상(다수결)**이 그 라운드의 색으로 확정된다. (투표 안 한 사람은 그냥 결과에 영향만 안 줄 뿐, 페널티 없음)
- "아무도 정하지 않았을 경우, 랜덤한 색상 지정" = 해당 라운드에 **아무도 투표하지 않으면** 팔레트 10색 중 무작위로 그 라운드 색이 정해진다.
- "술래는 색상이 하나가 다르게 나오게 할거야" = 4라운드가 끝나 공용 4색 세트가 확정되면, **술래로 뽑힌 1명만** 그 4색 세트 중 **한 자리(슬롯)가 팔레트의 다른 색으로 치환된 변형 세트**를 부여받는다. 일반 플레이어는 전원 동일한 4색 세트를 그대로 사용한다.
- 술래 본인은 시스템이 정하는 것으로 가정했고(랜덤 1인 선정), "색이 다르게 나온다"는 부분은 **술래를 시각적으로 구분하는 수단**(머리 위 색상 표시 등)으로 해석했습니다. 술래가 색상 차이로 스스로 "정해지는" 방식(즉 색이 다른 사람이 자동으로 술래가 되는 방식)은 아니라고 가정했습니다.

---

## 1. 게임 플로우 개요

```
[대기실 입장 2~4인]
        │
        ▼
[색상 결정 페이즈] ──────────────────────────────┐
  Round 1 (20초) → 다수결/랜덤 → 1번째 색 확정      │  MasterClient 권위로 진행
  Round 2 (20초) → 다수결/랜덤 → 2번째 색 확정      │  (타이머는 PhotonNetwork.Time 기준 동기화)
  Round 3 (20초) → 다수결/랜덤 → 3번째 색 확정      │
  Round 4 (20초) → 다수결/랜덤 → 4번째 색 확정      │
        └──────────────────────────────────────────┘
        ▼
[술래 지정 페이즈]
  MasterClient가 랜덤 1인을 술래로 선정
  술래에게만 "1슬롯이 다른" 변형 4색 세트 부여
        ▼
[태그(술래잡기) 게임 시작] ← 기존 HideOrSeekPlayer 로직과 연결
```

---

## 2. 폴더/파일 구조 (CLAUDE.md 규칙 준수)

```
Assets/02. Scripts/ColorTag/
  ├─ ColorPaletteSO.cs            # 팔레트 정의 SO (10색)
  ├─ NetKeys.cs                   # Room/Player CustomProperties 키 상수
  ├─ ColorSelectionManager.cs     # MonoBehaviourPunCallbacks, 라운드 진행 총괄
  ├─ ColorVoteTally.cs            # 순수 C# 클래스, 다수결/랜덤 계산 로직
  ├─ TaggerColorAssigner.cs       # 순수 C# 클래스, 술래 & 변형 색상 세트 계산
  ├─ PlayerColorVoteIndicator.cs  # 플레이어 머리 위 투표색 표시
  └─ PlayerColorDisplay.cs        # 최종 확정된 색상(또는 술래 변형색) 적용

Assets/03. SO/ColorTag/
  └─ DefaultColorPalette.asset    # ColorPaletteSO 인스턴스 (10색 값 보관)

Assets/Resources/UI/Scene/ColorSelectionPanel/
  ├─ ColorSelectionPanel.prefab
  └─ ColorSwatchButton.prefab
```

`PlayerGroundDetector`, `PlayerAnimationDriver`, `PlayerNetworkSync`처럼 **네트워크/유니티 의존적인 부분은 MonoBehaviour(Manager)**가 담당하고, **판정 로직(다수결 계산, 변형색 계산)은 순수 C# 클래스로 분리**해 기존 코드베이스 스타일과 동일한 구조를 따릅니다.

---

## 3. 데이터 설계

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
| `Color0`~`Color3` | int | 확정된 라운드별 색상 인덱스 (-1 = 미확정) |
| `TaggerActorNumber` | int | 술래로 선정된 플레이어의 ActorNumber |
| `TaggerVariantSet` | int[4] | 술래 전용 변형 4색 세트 |

**Player CustomProperties** (각자 자신 것만 기록, 전원 수신):

| 키 | 타입 | 설명 |
|---|---|---|
| `VoteColorIndex` | int | 현재 라운드에서 이 플레이어가 클릭 중인 색 (-1 = 미선택), 머리 위 표시에 사용 |

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
}
```

---

## 4. 라운드 진행 로직

### 4.1 왜 MasterClient 권위 + CustomProperties 방식인가

- 기존 코드베이스는 RPC/RaiseEvent를 아직 쓰지 않고, `MonoBehaviourPunCallbacks` + `IPunObservable`(스트림 동기화) 패턴만 사용 중입니다.
- 투표처럼 "상태값이 바뀔 때마다 전원에게 알려야 하는" 데이터는 RaiseEvent보다 **CustomProperties가 더 간단하고, 재접속/마스터 이관에도 자동으로 최신값이 복제**되므로 이 방식을 택했습니다.
- 타이머는 각 클라이언트의 로컬 시계 대신 **`PhotonNetwork.Time`(서버 기준 동기화 시간)**을 써서 클라 간 오차를 없앱니다.

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

        int resolvedColor = ColorVoteTally.Resolve(votes.Values, palette.Count, rng);

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
            AssignTagger(roomProps); // 4라운드 종료 → 술래 지정까지 같은 트랜잭션에 포함
            roomProps[NetKeys.RoundIndex] = TotalRounds + 1; // 완료 상태
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
        ResetAllVotes();
    }

    private void AssignTagger(Hashtable roomProps)
    {
        var players = PhotonNetwork.PlayerList;
        Player tagger = players[rng.Next(players.Length)];

        int[] baseSet = new int[4];
        for (int i = 0; i < 4; i++)
            baseSet[i] = (int)PhotonNetwork.CurrentRoom.CustomProperties[NetKeys.ColorPrefix + i];

        int[] variant = TaggerColorAssigner.BuildVariantSet(baseSet, palette.Count, rng);

        roomProps[NetKeys.TaggerActorNumber] = tagger.ActorNumber;
        roomProps[NetKeys.TaggerVariantSet] = variant;
    }

    private void ResetAllVotes()
    {
        if (PhotonNetwork.LocalPlayer != null)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.VoteColorIndex, -1 } });
    }

    // 라운드 시작 (게임 시작 버튼 등에서 MasterClient가 최초 1회 호출)
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
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // 로컬 플레이어가 팔레트 스와치를 클릭했을 때 UI에서 호출
    public void SubmitVote(int colorIndex)
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.VoteColorIndex, colorIndex } });
    }
}
```

**중요:** 각 클라이언트가 `Update()`에서 매 프레임 `PhotonNetwork.IsMasterClient`를 체크하고, 마스터 이관이 일어나도 필요한 상태(`RoundIndex`, `RoundEndTime`, 각 `ColorN`)가 전부 Room CustomProperties에 있으므로 **새 마스터가 그대로 이어서 진행**할 수 있습니다. 별도의 마이그레이션 처리 코드가 필요 없습니다.

### 4.3 다수결/랜덤 판정 로직 (순수 C#, 유닛 테스트 가능)

```csharp
using System.Collections.Generic;
using System.Linq;

public static class ColorVoteTally
{
    // votes: 각 플레이어의 투표값 (미투표는 -1)
    public static int Resolve(IEnumerable<int> votes, int paletteSize, System.Random rng)
    {
        var cast = votes.Where(v => v >= 0).ToList();

        if (cast.Count == 0)
            return rng.Next(paletteSize); // 아무도 정하지 않음 -> 랜덤

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

### 4.4 술래 변형 색상 계산

```csharp
public static class TaggerColorAssigner
{
    // baseSet(4색) 중 무작위 한 슬롯을, 원래 값과 다른 팔레트 색으로 치환
    public static int[] BuildVariantSet(int[] baseSet, int paletteSize, System.Random rng)
    {
        int[] variant = (int[])baseSet.Clone();
        int slot = rng.Next(variant.Length);

        int replacement;
        do { replacement = rng.Next(paletteSize); }
        while (replacement == variant[slot]);

        variant[slot] = replacement;
        return variant;
    }
}
```

---

## 5. UI 설계

### 5.1 팔레트 패널 (`ColorSelectionPanel`)

- 10개 `ColorSwatchButton`을 2행 5열로 배치 (PalletEx.png 배열과 동일).
- 상단에 `RoundIndex + 1 / 4` 및 남은 시간(`RoundEndTime - PhotonNetwork.Time`) 표시.
- 스와치 클릭 시 `ColorSelectionManager.SubmitVote(index)` 호출, 선택된 스와치는 하이라이트.

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
}
```

### 5.2 머리 위 투표색 표시 (`PlayerColorVoteIndicator`)

- 각 플레이어 프리팹에 world-space 인디케이터(작은 원형 Image)를 부착.
- 소유 플레이어(`pv.Owner`)의 `VoteColorIndex` CustomProperty를 읽어 색을 반영, -1이면 비활성화.
- `OnPlayerPropertiesUpdate` 콜백으로 실시간 갱신(폴링 불필요).

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

### 5.3 최종 색상 적용 (`PlayerColorDisplay`)

- `RoundIndex`가 완료 상태(5)로 바뀌는 `OnRoomPropertiesUpdate` 시점에 호출.
- 자신이 `TaggerActorNumber`면 `TaggerVariantSet`을, 아니면 `Color0~Color3` 세트를 그대로 적용.
- 실제 적용 대상(머티리얼 색, 액세서리 색 등)은 아트/연출 방향에 따라 추후 결정 필요.

---

## 6. 엣지 케이스

| 상황 | 처리 방안 |
|---|---|
| 라운드 중 마스터 이관 | 상태가 Room CustomProperties에 있으므로 새 마스터가 `Update()`에서 그대로 이어받음 |
| 라운드 중 플레이어 퇴장 | 매 판정 시 `PhotonNetwork.PlayerList` 기준으로 투표를 다시 집계하므로 자동 반영 (Photon이 퇴장자를 목록에서 제거) |
| 오프라인 모드(`OfflineModeBootstrap`) 테스트 | 플레이어 1명뿐이므로, 투표하면 그 색, 안 하면 랜덤. 술래도 자기 자신으로 고정됨 (단독 테스트용) |
| 라운드 도중 신규 입장 | `PhotonNetwork.CurrentRoom.IsOpen = false`로 색상 선택 시작과 동시에 입장을 막는 것을 권장 |
| 동점 다수결 | `ColorVoteTally`가 동점 색상 중 랜덤 1개로 확정 (문서 0번 해석 참고) |

---

## 7. 미결정 사항 (구현 전 확인 필요)

1. **술래 색이 다르게 "보이는" 방식** — 게임 내내 항상 다른 플레이어에게도 보이나요, 아니면 특정 연출 시점에만 잠깐 공개되나요?
2. **색상 적용 대상** — 머리 위 인디케이터 외에, 실제로 캐릭터의 어느 부분(전신 컬러, 트레일, 아이콘 등)에 이 색을 적용할지.
3. **4색 세트 중복 허용 여부** — 라운드끼리 같은 색이 다수결로 다시 뽑혀도 되는지, 아니면 이미 뽑힌 색은 다음 라운드 팔레트에서 제외해야 하는지.
4. **재대결(다음 판)** 시 색상 선택 페이즈를 처음부터 다시 하는지, 아니면 유지되는지.
5. **술래가 방을 나간 후 방 처리** - 
6. **4인이서 한 명이 나간 후 처리** - 

---

## 8. 구현 순서 제안 (승인 후 진행)

1. `ColorPaletteSO` + `NetKeys` + `ColorVoteTally`/`TaggerColorAssigner` (순수 로직, Unity 의존 없음)
2. `ColorSelectionManager` (네트워크 골격, 로그로 라운드 진행 확인)
3. `ColorSwatchButton` + `ColorSelectionPanel` UI 연결
4. `PlayerColorVoteIndicator` (머리 위 실시간 표시)
5. `PlayerColorDisplay` (최종 색상/술래 변형색 적용)
6. 2~4인 실제 접속 테스트, 오프라인 모드 단독 테스트, 마스터 이관 시나리오 테스트
