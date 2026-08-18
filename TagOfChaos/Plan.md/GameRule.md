# GameRule.md — 숨바꼭질(쿠키) + 술래잡기(괴물) 본게임 설계 (v3, 2026-08-18)

> **v3 대비 변경 요약**(이번 개정 사유, 사용자 확인):
> 1. **마녀(Witch) 캐릭터 → 괴물(Monster)로 전면 교체.** 마녀 프리팹은 제거하고, 신규 괴물
>    3D 모델로 대체한다. 참고 이미지("괴물 T-pose.png")는 최초 조사 시점엔 프로젝트에 없었으나,
>    **같은 날 사용자가 다시 반입해 `Assets/Screenshots/괴물 T-pose.png` +
>    `리소스/괴물 T-pose.png` 두 곳에서 확인됨** — 이미지 확인 결과는 §4.2/§11.1/§14.1에
>    반영했다(요약: 원형 머리+고깔모자+주름 칼라+해골 단추 의상+주름 치마+둥근 신발을 갖춘
>    **삐에로 풍 몬스터**이며, **팔다리가 6개**다 — 다리 2개, 몸통 옆에서 뻗은 관절형 팔 2개
>    (발톱 손), 머리 뒤에서 뻗어나오는 구불거리는 촉수 2개(뭉툭한 발바닥형 손)).
> 2. **F키 원거리 마법 공격(v2에서 신설) → 완전 폐기.** `WitchMagicAttack`/`MagicCast`
>    이벤트/1인칭 카메라 전제가 전부 제거된다.
> 3. **(v3.2로 재차 갱신) 괴물의 공격은 "포획"이 아니라 "타격으로 부수는" 메커니즘이다** —
>    1회 피격 시 균열(아직 이동 가능), 2회 피격 시 파괴(탈락). §4.2 전면 재설계.
> 4. **소품 던지기(v1부터 있던 기능) → 완전 폐기.** `ThrowableProp`/`NoiseListener`/
>    `NoisePing` 이벤트가 전부 제거된다.
> 5. **(v3.2) 괴물 카메라는 1인칭으로 확정.** "3인칭으로 하기엔 힘들 것 같다"는 사용자 판단에
>    따라, v3에서 "폐기 검토"로 분류했던 1인칭 카메라를 되살린다. §6.2 전면 재설계.
> 6. 위 변경에 따라 §4.2·§6.2·§6.3·§8·§9(NetKeys/EventCodes)·§10(에셋 목록)·§12(열린 질문)·
>    §14(사용자 제공 필요 항목)를 갱신했다.
>
> **아직 구현하지 않는다** — 이번 개정은 설계 문서 반영까지만이며, 실제 스크립트/에셋 작업은
> 진행하지 않는다(사용자 확인).

---

## 0. 이 설계가 기존 코드에 미치는 영향 (요약)

현재 `ColorTag/` 도메인(15파일)의 "팀 투표로 술래 색 감추기" 컨셉은 완전히 대체된다. 색은
개인 자유 표현(최대 4색, 슬롯 UI, 임계량 등록, Reset/지우개)이고, 괴물은 색이 아니라 **가마솥
행위 + 별도 캐릭터 모델**로 정해지며, 원거리 공격도 근접 포획도 아니라 **손/촉수로 타격해
부수는(균열→파괴)** 방식으로 쿠키를 탈락시킨다.

| 기존 컴포넌트 | 처리 |
|---|---|
| `ColorSelectionManager`(4라운드 루프+`AssignTagger`) | **폐기**. §2(괴물 선정)+§3(페인트 타이머)로 대체 |
| `ColorVoteTally` / `TaggerColorAssigner` | **폐기**. 팀 투표·색 치환 식별자 개념 자체가 없어짐 |
| `ColorSelectionPanel`/`ColorSwatchButton` | **재작성**. 개인 슬롯 UI(§3.4)로 |
| `PlayerColorVoteIndicator` / `PlayerColorDisplay` | **폐기**(단, `PlayerColorVoteIndicator`의 코드 구조는 §4.2의 균열 표시 컴포넌트가 그대로 본뜬다) |
| `PlayerPaintCanvas` | **핵심 로직 재사용**, 슬롯/임계량 등록/지우개로 교체(§3) |
| `RoomLifecycleWatcher`(술래 퇴장 감지) | **재작성**. 단일 액터 → 다중 `MonsterActorNumbers` 배열 대응(§7) |
| `GameLobbyController.OnMasterClientSwitched` | **그대로 활용** — 방장 위임은 Photon 기본 동작으로 이미 해결됨(§7.2) |

**v2에서 계획됐다가 v3/v3.2에 다시 폐기/변경되는 것**:

| v2 계획 컴포넌트 | 최종 처리 |
|---|---|
| `Witch/WitchMagicAttack.cs` | **폐기.** F키 원거리 마법 공격 자체가 없어짐(§6.2 옛 버전) |
| `Witch/WitchFirstPersonCamera.cs` | **부활, `Monster/MonsterFirstPersonCamera.cs`로 확정(v3.2)** — §6.2 |
| `NetEventCodes.MagicCast` | **폐기** |
| `Grab/ThrowableProp.cs`, `Grab/NoiseListener.cs` | **폐기.** 소품 던지기 기능 자체가 없어짐(§5) |
| `NetEventCodes.NoisePing` | **폐기** |
| (v3 계획) 괴물 포획(촉수/손) 컴포넌트 | **v3.2에서 "타격" 컴포넌트로 재설계** — §4.2 |
| `Witch/*` 폴더·클래스 전체 | `Monster/`로 명칭 변경. 로직(선정/이탈 처리/승리 판정 등)은 대부분 유지, 명칭만 교체 |
| `PlayerMoveState.Dead`/`Caught`, `NetKeys.IsDead`/`IsCaught` | **`Broken`/`HitCount`로 재설계(v3.2)** — "즉사"도 "포획"도 아니라 "파괴"가 정확한 표현(§4.2, §9) |

---

## 1. 전체 게임 플로우 (v3.2 갱신)

```
GameLobbyScene (대기실 — 문 4개 + 가마솥)
  ├─ 아무 쿠키나 가마솥에 들어감 → 선착순 괴물 확정(§2.1)
  │    └─ 예외: MonsterSelectTimeout(예: 30초) 안에 아무도 안 들어가면 마스터가 랜덤 1인 배정
  ├─ 연출(보글보글→짜잔) + 괴물 프리팹 교체(§2.2)
  ├─ 10초 카운트다운 → 쿠키만 GameScene 이동, 괴물은 GameLobbyScene 대기(§2.3~2.4)
GameScene (쿠키만 입장)
  ├─ 60초 자유 색칠(§3) — 일정량 이상 칠한 색만 슬롯에 등록
  ├─ 60초 경과 → 등록 슬롯 0개인 플레이어는 서로 겹치지 않는 색으로 전신 강제 도포(§3.6)
  └─ 괴물 GameScene 합류(§6.4) — GameEndTime = 합류 시각 + 10분
        ├─ 쿠키: 시야 축소(안개, §6.1)
        ├─ 쿠키: 서로 그랩/캐리(§4.1) — 소품 던지기는 폐기됨(§5)
        ├─ 괴물: 1인칭 시점(§6.2)에서 촉수 또는 손으로 쿠키를 타격(§4.2, 실제 플레이어, AI 아님)
        │    └─ 1회 피격 = 균열(이동 가능), 2회 피격 = 파괴(탈락)
        ├─ 파괴된 쿠키: Space로 생존 쿠키 시점 관전(§6.3)
        ├─ 괴물 전원 퇴장 시 5초 경고 후 GameLobbyScene 복귀(§7)
        └─ 승리 판정(§8) — 전원 파괴→괴물 승, 10분 생존→쿠키 승, 결과 화면(§8.2) 표시
```

---

## 2. 괴물 선정 & 가마솥 연출 (`GameLobbyScene`)

### 2.1 가마솥 트리거 — 선착순 + 타임아웃 랜덤 배정

사용자 확인: "선착순 자진 입장이 맞다. 하지만 아무도 안 들어가면 랜덤으로 1명이 괴물이 된다."
클라이언트가 신청을 마스터에 보내고 마스터가 확정하는 구조는 그대로 두고, **마스터 전용
타임아웃 폴백**만 둔다 — `ColorSelectionManager`/`RoomLifecycleWatcher`가 이미 쓰는 "마스터만
`Update()`에서 만료 시각 폴링" 패턴 그대로다.

```csharp
// Assets/02. Scripts/Monster/Cauldron.cs — OnTriggerEnter에서 ClaimMonster RaiseEvent
```

```csharp
// Assets/02. Scripts/Monster/MonsterAssignmentAuthority.cs (마스터 전용)
public class MonsterAssignmentAuthority : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private float monsterSelectTimeout = 30f;
    private double sceneEnterTime;

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient) sceneEnterTime = PhotonNetwork.Time;
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.ClaimMonster) return;
        if (!PhotonNetwork.IsMasterClient) return;
        if (HasMonsterAssigned()) return; // 이미 확정 — 이후 요청/타임아웃 전부 무시

        int claimantActorNumber = (int)photonEvent.CustomData;
        ConfirmMonster(new[] { claimantActorNumber });
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (HasMonsterAssigned()) return;
        if (PhotonNetwork.Time < sceneEnterTime + monsterSelectTimeout) return;

        var players = PhotonNetwork.PlayerList;
        int randomActorNumber = players[new System.Random().Next(players.Length)].ActorNumber;
        ConfirmMonster(new[] { randomActorNumber });
    }

    private bool HasMonsterAssigned() =>
        PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(NetKeys.MonsterActorNumbers);

    private void ConfirmMonster(int[] monsterActorNumbers)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.MonsterActorNumbers, monsterActorNumbers },
            { NetKeys.MonsterRevealTime, PhotonNetwork.Time },
        });
    }
}
```

> 📌 **다중 괴물 확장에 대한 메모**: 초기 배정은 "가마솥에 1명이 들어가면 괴물"이므로 여전히
> 1명이다. `MonsterActorNumbers`를 처음부터 배열로 설계해둔 것은 §7의 이탈 처리 시나리오가
> 다중 괴물을 전제하기 때문이며, 나중에 인원수가 늘어나도 배열 길이만 늘리면
> §2.2(리빌)/§4.2(타격)/§7(이탈 처리)/§8(승리 판정) 전부 그대로 동작한다.

### 2.2~2.4 (명칭만 교체, 로직은 v2와 동일)

리빌 연출·10초 카운트다운·쿠키만 GameScene 이동·괴물 GameLobbyScene 대기 로직은 이전과
동일하다. `MonsterRevealController`/`PlayerSpawner`의 판별 조건은
`((int[])room.CustomProperties[NetKeys.MonsterActorNumbers]).Contains(PhotonNetwork.LocalPlayer.ActorNumber)`
로 다중 괴물을 대응한다.

---

## 3. 개인 자유 색칠 (GameScene, 60초) — 슬롯 등록 방식 + 강제 도포 중복 방지(v3.1 갱신)

> **v3.1 추가 변경**: §3.6 "60초 만료 시 전신 랜덤 강제 도포"에서, 등록 슬롯 0개인 플레이어가
> **2명 이상이면 서로 같은 색으로 겹쳐 도포되지 않도록** 마스터 권위 배정 방식으로 재설계했다
> (사용자 확인). §7.2가 이름만 언급해뒀던 `PaintPhaseController`(마스터 전용, §3.6)를 이번에
> 실제로 정의한다.

### 3.1 문제 재정의 — 악성 유저의 "색 숨기기" 방지

사용자 확인: v1에서 우려했던 문제(재도색을 막으면 자연스러운 보정이 어려움)의 실제 의도는
**"1픽셀만 칠하고 다른 색으로 넘어가 괴물이 실제 색을 판단 못 하게 하는 악용"**을 막는 것이었다.
해결책: **색을 선택하는 순간이 아니라, 그 색으로 "일정량 이상" 실제로 칠했을 때만 슬롯에
등록**한다. 등록되지 않은 색은 화면엔 보이지만(실시간 공유 요구사항 충족) 슬롯 카운트에는
잡히지 않으므로, 60초가 지나도 슬롯이 0개면 그대로 전신 강제 도포 대상이 된다.

### 3.2 `PlayerPaintCanvas` 확장 — 임계량 기반 등록 + 슬롯 수 네트워크 동기화

```csharp
// PlayerPaintCanvas.cs — 신규 필드
private const int MinStrokesToRegister = 15; // 임계값 — 밸런스 값, §12 열린 질문
private readonly Dictionary<int, int> pendingStrokeCounts = new Dictionary<int, int>(); // 미등록 색 → 누적 스탬프 수
private readonly List<int> registeredColorSlots = new List<int>(4);

public event System.Action<IReadOnlyList<int>> OnSlotsChanged;
public event System.Action OnSlotRejected; // "이미 4가지 색상을 모두 사용했습니다"

// Update()의 스탬프 직전 검사
int brushColor = GetCurrentBrushColorIndex();
if (brushColor < 0) return;

if (!registeredColorSlots.Contains(brushColor))
{
    if (registeredColorSlots.Count >= 4)
    {
        OnSlotRejected?.Invoke();
        return;
    }

    pendingStrokeCounts.TryGetValue(brushColor, out int count);
    count++;
    if (count >= MinStrokesToRegister)
    {
        pendingStrokeCounts.Remove(brushColor);
        registeredColorSlots.Add(brushColor);
        OnSlotsChanged?.Invoke(registeredColorSlots);
        ReportSlotCount(); // §3.6 신규 — 마스터가 "이 플레이어는 슬롯이 있다"를 알 수 있도록 즉시 반영
    }
    else
    {
        pendingStrokeCounts[brushColor] = count;
    }
}

StampBrush(hit.textureCoord, brushColor);
```

```csharp
// PlayerPaintCanvas.cs — §3.6 신규: 등록 슬롯 수를 자기 자신의 Player CustomProperties에 계속 보고
private void ReportSlotCount()
{
    if (!pv.IsMine) return;
    PhotonNetwork.LocalPlayer.SetCustomProperties(
        new Hashtable { { NetKeys.RegisteredSlotCount, registeredColorSlots.Count } });
}
```

### 3.3 브러시 색 선택 / 3.4 Reset / 3.5 지우개 (변경 없음)

`ColorSwatchButton.SetBrushColor()`, `ColorReplaceMaterial` 재사용 Reset, `EraseStampMaterial`
지우개 — v1 로직 그대로 유효.

### 3.6 60초 만료 — 등록 슬롯 0개 플레이어 전신 강제 도포 (마스터 배정 방식, v3.1)

**문제**: 등록 슬롯 0개인 플레이어가 2명 이상이면, 각자 독립적으로 무작위 색을 뽑을 경우
우연히 같은 색이 겹칠 수 있다. **해결**: 마스터 클라이언트 1명이 전체 상황을 보고 팔레트
색을 셔플한 뒤 앞에서부터 겹치지 않게 하나씩 배정한다.

```csharp
// Assets/02. Scripts/ColorTag/PaintPhaseController.cs (마스터 전용)
public class PaintPhaseController : MonoBehaviourPunCallbacks
{
    [SerializeField] private ColorPaletteSO palette;
    private System.Random rng = new System.Random();
    private bool resolved;

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (resolved) return;
        if (!RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double endTime)) return;
        if (PhotonNetwork.Time < endTime) return;

        ResolvePaintPhase();
        resolved = true;
    }

    private void ResolvePaintPhase()
    {
        var zeroSlotPlayers = new List<Player>();
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            int count = p.CustomProperties.TryGetValue(NetKeys.RegisteredSlotCount, out object v) ? (int)v : 0;
            if (count == 0) zeroSlotPlayers.Add(p);
        }

        if (zeroSlotPlayers.Count == 0) return;

        int[] shuffledColors = Enumerable.Range(0, palette.Count).OrderBy(_ => rng.Next()).ToArray();

        int[] actorNumbers = new int[zeroSlotPlayers.Count];
        int[] assignedColors = new int[zeroSlotPlayers.Count];
        for (int i = 0; i < zeroSlotPlayers.Count; i++)
        {
            actorNumbers[i] = zeroSlotPlayers[i].ActorNumber;
            assignedColors[i] = shuffledColors[i];
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.ForcedPaintActorNumbers, actorNumbers },
            { NetKeys.ForcedPaintColors, assignedColors },
        });
    }
}
```

```csharp
// PlayerPaintCanvas.cs — 자신이 배정 대상이면 전신을 그 색으로 강제 도포(로컬 1회만 적용)
public override void OnRoomPropertiesUpdate(Hashtable changedProps)
{
    if (!changedProps.ContainsKey(NetKeys.ForcedPaintActorNumbers)) return;
    if (!pv.IsMine) return;
    ApplyForcedColorIfAssignedToMe();
}

private void ApplyForcedColorIfAssignedToMe()
{
    if (!RoomState.TryGetIntArray(NetKeys.ForcedPaintActorNumbers, out int[] actorNumbers)) return;
    if (!RoomState.TryGetIntArray(NetKeys.ForcedPaintColors, out int[] colors)) return;

    int myIndex = System.Array.IndexOf(actorNumbers, PhotonNetwork.LocalPlayer.ActorNumber);
    if (myIndex < 0) return;

    ApplyStamp(finalizeStampMaterial /* 또는 전용 FillAllMaterial, §10.5 */, Vector2.zero, float.MaxValue, colors[myIndex]);
    SendStrokeEvent(Vector2.zero, float.MaxValue, colors[myIndex], force: true);
}
```

---

## 4. 그랩 / 캐리(쿠키 상호작용) + 괴물 타격(균열→파괴, v3.2 재설계)

### 4.1 쿠키 ↔ 쿠키 그랩/캐리 (변경 없음)

괴물의 공격(§4.2)과 그랩(쿠키끼리 서로 들고 나르는 것)은 원래부터 별개 시스템이고, 이번
개정에서도 **쿠키↔쿠키 그랩/캐리는 그대로 유지**된다. 애니메이션 검증 결과(§11)도 변경 없다:

- `Cookie_Carrying.fbx`는 상반신 전용 Avatar Mask 레이어로 `Cookie_Walking.fbx`와 조합 가능(§11.2).
- `Cookie_Hanging_Idle.fbx`는 별도 검증 보류 상태 유지(§11.3).
- 그랩 시작 시 도입 모션은 `ReplayJump()`와 동일한 `Animator.Play()` 하드컷 패턴 권장(§11.4).

넷코드 설계(소유권 이전 없이 `carrySocket` 로컬 추적), `SetCarryLayerWeight()`,
`OnGrabbedByOwner`/`OnReleased` 전부 변경 없음.

### 4.2 괴물 ↔ 쿠키 타격 — 균열(1회) → 파괴(2회) (v3.2, 포획 개념 완전 폐기)

사용자 확인: "괴물이 근접 포획을 하는 것이 아니다. 쿠키를 부수는 형태로 할 것이고, 한 번
공격했을 때 쿠키가 사방에 금이 가는 형태(부숴지기 전 형태)가 되며, 두 번 타격했을 경우
쿠키가 부숴지는 연출을 할 것." — 직전에 설계했던 "촉수/손으로 붙잡아 데려가는 포획"(`IsCaught`,
`RequestCapture`)은 **이 설계로 완전히 대체된다.** 손/촉수가 물리적으로 별개 부위라는 T-pose
확인 결과(§4.2 이전 조사, 원형 머리+고깔모자 삐에로 몬스터, 앞쪽 관절형 손 2개+뒤쪽 촉수 2개)는
그대로 유효하다 — 이번에 바뀐 것은 "잡아서 뭘 하는지"이지 "무엇으로 닿는지"가 아니다.

**실제 코드베이스 기준 설계 근거**: 이 프로젝트에는 이미 "네트워크로 동기화된 한 캐릭터의
상태를, 그 캐릭터의 소유 여부와 무관하게 모든 클라이언트가 각자 로컬로 시각화한다"는 정확한
전례가 있다 — `PlayerColorVoteIndicator.OnPlayerPropertiesUpdate(Player targetPlayer, ...)`가
`targetPlayer != pv.Owner`만 걸러내고, `pv.IsMine` 여부는 전혀 확인하지 않는다(투표색 표시는
소유자 자신을 포함해 모두가 봐야 하므로). 균열/파괴 시각 효과도 정확히 같은 성격이다 — 괴물을
포함해 모두가 "이 쿠키가 몇 대 맞았는지"를 봐야 한다. 또한 `PlayerPaintCanvas.InitPaintCanvas()`는
이미 캐릭터별 런타임 머티리얼 인스턴스(`new Material(paintedSkinShader)`)를 만들어
`bodyRenderer.material`에 `_MainTex`/`_PaintTex`를 설정해두고 있다 — 균열 표시는 같은 셰이더에
`_CrackAmount` 프로퍼티 하나만 얹으면 되는 자연스러운 확장이다(§10.5).

**설계**:
1. `HideOrSeekPlayer`가 `hitCount`(0~2)를 자기 자신만 갱신한다(소유권 원칙, 기존
   `VoteColorIndex`/`RegisteredSlotCount`와 동일하게 `PhotonNetwork.LocalPlayer.SetCustomProperties`).
2. 괴물의 타격은 대상 쿠키의 `PhotonView`에 `RequestHit` RPC를 보낼 뿐이고, 실제 카운트
   증가·상태 전이는 **맞은 쿠키 자신의 클라이언트만** 확정한다(`RequestCapture`가 쓰던 것과
   동일한 "판정은 상대가, 확정은 대상 자신이" 패턴).
3. `hitCount==1`(균열)은 애니메이션 상태를 바꾸지 않는다 — 계속 걷고 뛸 수 있고, 균열은 순수
   시각 효과(신규 `PlayerCrackDisplay`)로만 표현된다. `hitCount>=2`(파괴)에서만
   `IsMovementLocked=true` + `PlayerMoveState.Broken`(§9)으로 전이해 관전(§6.3)으로 넘어간다.

```csharp
// Assets/02. Scripts/Monster/MonsterStrikeAttack.cs — MonsterPlayer 프리팹 전용 (잠정 골격, 세부 미확정)
public class MonsterStrikeAttack : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Transform tentacleStrikePoint; // 뒤쪽 촉수 타격 판정 원점
    [SerializeField] private Transform handStrikePoint;     // 앞쪽 손 타격 판정 원점
    [SerializeField] private float strikeRadius = 1.5f;      // 임시값, 밸런스 미정(§12)
    [SerializeField] private LayerMask cookieLayer;

    // 판정을 언제 시도하는지(키 입력/자동 근접/애니메이션 이벤트) 자체가 아직 미정(§12) —
    // 아래는 "닿으면 때린다"는 요구만 반영한 최소 골격이다.
    private void TryStrikeCookie(Transform strikePoint)
    {
        Collider[] hits = Physics.OverlapSphere(strikePoint.position, strikeRadius, cookieLayer);
        if (hits.Length == 0) return;

        var cookie = hits[0].GetComponentInParent<HideOrSeekPlayer>();
        if (cookie == null) return;

        cookie.GetComponent<PhotonView>().RPC("RequestHit", RpcTarget.All);
    }
}
```

```csharp
// HideOrSeekPlayer.cs — v3(포획)의 IsCaught/RequestCapture를 완전히 대체
private int hitCount; // 0=정상, 1=균열, 2=파괴(§9)

[PunRPC]
private void RequestHit()
{
    if (!pv.IsMine || hitCount >= 2) return; // 본인 클라이언트만 자기 상태 확정(소유권 원칙, §4.1/§4.2 공통)
    hitCount++;
    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.HitCount, hitCount } });

    if (hitCount >= 2)
    {
        IsMovementLocked = true;
        animationDriver.ChangeState(PlayerMoveState.Broken); // §9 — 파괴 연출 시작, 이동 애니메이션 계열과 동일한 트리거 체계
    }
    // hitCount == 1(균열)은 여기서 아무 것도 더 하지 않는다 — 상태 전이가 아니라 시각 효과일 뿐이므로
    // 실제 표시는 PlayerCrackDisplay(아래)가 CustomProperties 변화를 감지해 담당한다.
}
```

```csharp
// Assets/02. Scripts/ColorTag/PlayerCrackDisplay.cs (신규) — PlayerColorVoteIndicator와 완전히 동일한 구조:
// 소유권과 무관하게 "이 캐릭터가 몇 대 맞았는지"를 그 캐릭터 자신의 스킨 머티리얼에 반영한다.
public class PlayerCrackDisplay : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Renderer bodyRenderer; // PlayerPaintCanvas.bodyRenderer와 같은 렌더러를 인스펙터에서 동일하게 연결
    [SerializeField] private GameObject breakVfxPrefab; // §10.6, 로컬 Instantiate

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != pv.Owner) return;
        if (!changedProps.ContainsKey(NetKeys.HitCount)) return;

        int hitCount = (int)targetPlayer.CustomProperties[NetKeys.HitCount];
        ApplyHitVisual(hitCount);
    }

    private void ApplyHitVisual(int hitCount)
    {
        // PlayerPaintCanvas.InitPaintCanvas()가 이미 bodyRenderer.material에 paintedSkinShader
        // 인스턴스를 만들어뒀으므로(_MainTex/_PaintTex), 같은 머티리얼에 _CrackAmount 프로퍼티만
        // 추가하면 된다(§10.5, 셰이더 확장 필요) — 새 머티리얼을 따로 만들 필요 없음.
        if (bodyRenderer != null)
            bodyRenderer.material.SetFloat("_CrackAmount", hitCount >= 1 ? 1f : 0f);

        if (hitCount >= 2)
            PlayBreakEffect();
    }

    private void PlayBreakEffect()
    {
        if (bodyRenderer != null)
            bodyRenderer.enabled = false; // 원본 메시를 감추고 파편 연출로 대체
        if (breakVfxPrefab != null)
            Instantiate(breakVfxPrefab, transform.position, transform.rotation); // 전원 로컬 재생, 정밀 동기화 불필요(§5와 동일 철학)
    }
}
```

> 📌 **`PlayerCrackDisplay`를 `PlayerPaintCanvas`에 얹지 않고 별도 컴포넌트로 분리한 이유**:
> `research.md` §5.1/§5.11이 이미 확인했듯 이 프로젝트는 `Unit/`을 이동·접지·애니메이션·
> 네트워크·표시 5개로, `ColorTag/`를 계층별로 잘게 분리하는 컨벤션을 일관되게 지켜왔다.
> "타격 시각화"는 "색칠 캔버스 관리"(`PlayerPaintCanvas`의 책임)와 다른 책임이므로, 같은
> 관례를 따라 독립 컴포넌트로 둔다.
>
> 손과 촉수 둘 다 최종적으로는 동일한 `RequestHit` RPC로 귀결되므로(어느 부위로 맞았는지는
> `hitCount` 증가 자체에 영향을 주지 않는다), §12의 "촉수/손 중 무엇을 쓸지"가 아직 확정 안
> 돼도 넷코드·상태 설계 자체는 이미 완결돼 있다 — 남은 결정은 순수하게 "타격 판정 원점이
> 어디인지"와 "애니메이션이 무엇인지"에만 영향을 준다.

---

## 5. 던지기 (사물) — **완전 폐기**

v1부터 있던 "소품을 던져 괴물의 주의를 끄는" 기능은 이번 개정으로 완전히 제거된다(사용자
확인). `ThrowableProp`/`NoiseListener`와 `NoisePing` 이벤트는 전부 폐기 대상이며, §9(EventCodes)·
§10(에셋 목록)에서도 관련 항목을 제거했다.

폐기되는 것은 "사물을 던져 소음으로 주의를 끄는" 게임플레이 기능이며, 그랩한 쿠키를 놓아주는
동작(§4.1의 `OnReleased(withThrow, ...)`)과는 무관하다 — 그쪽은 그대로 유지된다.

---

## 6. 술래잡기 본게임

### 6.1 쿠키 시야 축소 — 안개 (변경 없음, 생략)

### 6.2 괴물 시점 — **1인칭으로 확정(v3.2)**

사용자 확인: "괴물에 대한 카메라는 1인칭 시점으로 해야 될 것. 3인칭으로 하기에는 힘들
것같다." v3에서 "마법이 없어졌으니 1인칭 근거가 약하다"며 3인칭 재사용 쪽으로 기울었던 판단을
뒤집는다 — §12의 카메라 질문은 **1인칭으로 최종 확정**됐다.

**실제 코드베이스 기준 설계**: 이 프로젝트는 이미 "씬에 고정 배치된 단일 Main Camera
오브젝트를, 로컬 플레이어가 스폰될 때 자기 자신을 넘겨 초기화시키는" 패턴을 갖고 있다 —
`HideOrSeekPlayer.cs`의 실제 코드:
```csharp
// HideOrSeekPlayer.cs Awake() 중 일부 (실제 코드)
Camera_Ctrl camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
if (camCtrl != null)
    camCtrl.InitCamera(gameObject);
```
그리고 `Camera_Ctrl.InitCamera(GameObject player)`는 `Awake()`/`Start()` 중 어느 쪽이 먼저
호출되든 항상 정확히 초기화되도록 설계돼 있다(`ResetToDefaultView()`를 양쪽에서 공유).

**괴물도 이 "Main Camera에 나를 넘긴다" 흐름 자체는 그대로 재사용**하지만, `Camera_Ctrl` 본체는
개조하지 않는다 — `Camera_Ctrl`은 우클릭 드래그로만 회전하고 고정 거리(`m_DefaultDist`)로
캐릭터를 뒤에서 따라다니는 **3인칭 궤도 카메라**로 설계돼 있어(회전은 카메라만, 캐릭터 몸은
이동 방향으로만 도는 구조), 1인칭에는 구조적으로 맞지 않는다. 대신 같은 Main Camera
오브젝트에 **별도 컴포넌트를 나란히** 붙인다 — 한 클라이언트는 쿠키 아니면 괴물 둘 중 하나만
플레이하므로 두 컴포넌트가 동시에 활성화될 일이 없다:

```csharp
// Assets/02. Scripts/Monster/MonsterFirstPersonCamera.cs (신규, v3.2)
public class MonsterFirstPersonCamera : MonoBehaviour
{
    private Transform eyeSocket; // 괴물 머리 안쪽 시점 원점 — 실제 3D 모델에 배치 필요(§14.1)

    public void InitCamera(Transform monsterEyeSocket)
    {
        eyeSocket = monsterEyeSocket;
    }

    private void LateUpdate()
    {
        if (eyeSocket == null) return;

        // 3인칭(Camera_Ctrl)과 달리 거리/오프셋 계산이 없다 — 그냥 눈 위치·방향에 그대로 고정.
        transform.position = eyeSocket.position;
        transform.rotation = eyeSocket.rotation;
    }
}
```

`HideOrSeekPlayer`를 그대로 재사용할 수 없는 이유도 실제 코드에서 확인된다 —
`CheckMovementInput()`은 `Camera.main.transform.forward/right` 기준으로 이동 방향을 계산한다
(3인칭 궤도 카메라를 전제로 한 "카메라 상대 이동"). 1인칭은 반대로 **카메라가 곧 캐릭터
정면**이어야 하므로, 마우스 좌우 입력이 캐릭터 자신의 `transform` 회전(yaw)을 직접 돌려야
하고, 이동은 카메라가 아니라 캐릭터 자신의 `forward`/`right` 기준이어야 한다. 이동 입력 처리
자체가 근본적으로 다르므로, `HideOrSeekPlayer`를 상속/재사용하지 않고 **괴물 전용
컨트롤러**를 새로 둔다(§10.1/§10.2에서도 이미 `MonsterPlayer.prefab`을 별도 프리팹으로
계획했던 것과 일관됨):

```csharp
// Assets/02. Scripts/Monster/MonsterController.cs (신규, v3.2)
public class MonsterController : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Transform eyeSocket;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float speed = 4f; // 이동 방식(물리 기반 여부 등)은 §12 열린 질문 — 최소 골격만 표기

    private float yaw, pitch;

    private void Awake()
    {
        if (!pv.IsMine) return;

        // HideOrSeekPlayer.Awake()가 Camera_Ctrl에 하던 것과 완전히 동일한 "Main Camera에 나를 넘긴다" 패턴.
        var fpsCam = Camera.main != null ? Camera.main.GetComponent<MonsterFirstPersonCamera>() : null;
        fpsCam?.InitCamera(eyeSocket);
    }

    private void Update()
    {
        if (!pv.IsMine) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -80f, 80f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);         // 몸체 좌우 회전 = 시점 정면(3인칭과 가장 큰 차이)
        eyeSocket.localRotation = Quaternion.Euler(pitch, 0f, 0f);  // 상하는 눈(카메라)만 따로 숙임/젖힘

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 move = (transform.forward * v + transform.right * h).normalized * speed * Time.deltaTime;
        transform.position += move;
    }
}
```

> 📌 **1인칭 확정으로 새로 드러난 문제**: 손(앞쪽)은 1인칭 시야 안에서 스윙을 직접 볼 수
> 있지만, **촉수(뒤쪽)는 플레이어가 화면으로 볼 수 없는 등 뒤에서 동작한다.** v3에서
> "근접이라 조준이 필요 없다"고 단순화했던 가정이, 이번에 "타격+1인칭"으로 확정되면서 오히려
> 새로운 조준/인지 문제를 만든 셈이다 — 근접 시 화면 가장자리 경고 UI를 줄지, 촉수는 아예
> "몸 뒤로 다가온 쿠키를 자동으로 처리"하는 반자동 판정으로 갈지 결정이 필요하다(§12 신규
> 질문).

### 6.3 파괴된 쿠키 — Space로 관전 시점 순환 (`IsCaught`→`hitCount>=2`로 재설계)

v3(포획)의 `IsCaught` 불리언은 폐기되고, §4.2의 `hitCount`(0~2)가 유일한 진실 소스가 된다.
`SpectatorController` 구조 자체는 트리거 조건만 `hitCount >= 2`로 바꾸면 그대로 유효하다.
결과 화면(§8.2) UI 문구도 "잡힘"에서 "부숴짐"으로 바뀐다.

"구출" 개념은 여전히 열린 질문이지만 성격이 바뀌었다 — 이전엔 "포획된 쿠키를 구출"이었다면,
이제는 **"균열(1회 피격, `hitCount==1`) 상태의 쿠키가 시간이 지나거나 다른 쿠키의 도움으로
정상 회복되는지"**가 질문이다. 2회째 피격(파괴)은 이 프로젝트의 다른 최종 상태 전이(예:
4라운드 완료 후 술래 지정)처럼 되돌릴 수 없는 것으로 잠정 유지한다(§12 신규 질문).

### 6.4 괴물 GameScene 합류 → `GameEndTime` 세팅 (변경 없음, 생략)

---

## 7. 괴물(술래) 이탈 & 방장 위임 처리

### 7.1 괴물 이탈 감지 — 다중 괴물 대응

`RoomLifecycleWatcher`의 `IsMonster(player)`(다중 배열 비교)는 "남은 괴물이 0명일 때만" 5초
경고 후 종료한다:

```csharp
// Assets/02. Scripts/Monster/RoomLifecycleWatcher.cs (재작성)
public class RoomLifecycleWatcher : MonoBehaviourPunCallbacks
{
    private double? monstersGoneAt;

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (monstersGoneAt.HasValue) return;
        if (!PhotonNetwork.IsMasterClient) return;
        if (!IsMonster(otherPlayer)) return; // 쿠키가 나간 건 이 경로와 무관(쿠키는 파괴되면 관전만 함, 나가도 게임 계속)

        RemoveFromMonsterList(otherPlayer.ActorNumber);

        if (RemainingMonsterCount() > 0) return;

        monstersGoneAt = PhotonNetwork.Time;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.MonsterDepartedAt, monstersGoneAt.Value },
        });
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!monstersGoneAt.HasValue) return;
        if (PhotonNetwork.Time < monstersGoneAt.Value + 5.0) return;

        monstersGoneAt = null;
        ReturnToGameLobby();
    }

    private bool IsMonster(Player p) =>
        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) && monsters.Contains(p.ActorNumber);

    private int RemainingMonsterCount() =>
        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) ? monsters.Length : 0;

    private void RemoveFromMonsterList(int actorNumber)
    {
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        int[] updated = monsters.Where(a => a != actorNumber).ToArray();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.MonsterActorNumbers, updated } });
    }
}
```

```csharp
// Assets/02. Scripts/Monster/MonsterDepartureBanner.cs — GameScene에 1개, 전원 대상
public class MonsterDepartureBanner : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private TMP_Text bannerText;

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.MonsterDepartedAt)) return;
        bannerText.text = "괴물이 나갔습니다. 5초 뒤 게임이 종료됩니다.";
        bannerRoot.SetActive(true);
    }
}
```

### 7.2 방장(MasterClient) 위임 — 변경 없음

Photon Room의 `MasterClientId` 자동 재할당 로직은 캐릭터 명칭과 무관하다. 모든 마스터 권위
로직이 "매 프레임 `IsMasterClient` 확인" 폴링 패턴이므로 별도 코드 없이 자동으로 이어받는다.

### 7.3 다중 괴물 확장 설계 요약

| 항목 | 단일 괴물(현재 규칙) | 다중 괴물(확장 시) |
|---|---|---|
| `NetKeys.MonsterActorNumbers` | `int[1]` | `int[N]` — 코드 변경 없이 길이만 늘어남 |
| §2.2 리빌 연출 | 1명만 프리팹 교체 | `Contains()` 검사라 여러 명이 동시에 교체돼도 동작 동일 |
| §4.2 타격 | 각자 독립적으로 `MonsterStrikeAttack` 보유 | 괴물 수만큼 인스턴스가 각자 독립 동작(공유 상태 없음) |
| §7.1 이탈 처리 | 1명 나가면 즉시 0명 → 5초 경고 | N명 중 일부만 나가면 진행, **0명이 될 때만** 5초 경고(이미 구현됨) |
| §8.3 승리 판정 | "쿠키 전원 파괴" 단순 검사 | 변경 없음 — 괴물 수와 무관하게 쿠키 쪽만 검사하면 됨 |

---

## 8. 승리 조건 + 결과 화면

### 8.1 판정 로직 (`hitCount>=2`로 재설계, 다중 괴물 대응)

```csharp
// Assets/02. Scripts/Monster/GameRuleController.cs
public class GameRuleController : MonoBehaviourPunCallbacks
{
    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        if (!RoomState.TryGetInt(NetKeys.MonsterJoined, out _)) return;
        if (RoomState.TryGetInt(NetKeys.GameResult, out _)) return;

        if (AllCookiesBroken(monsters))
        {
            Finish(GameResult.MonsterWins);
            return;
        }

        if (RoomState.TryGetDouble(NetKeys.GameEndTime, out double endTime) && PhotonNetwork.Time >= endTime)
        {
            Finish(GameResult.CookiesWin);
        }
    }

    private bool AllCookiesBroken(int[] monsters)
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (monsters.Contains(p.ActorNumber)) continue;
            int hitCount = p.CustomProperties.TryGetValue(NetKeys.HitCount, out object v) ? (int)v : 0;
            if (hitCount < 2) return false;
        }
        return true;
    }

    private void Finish(GameResult result)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.GameResult, (int)result } });
    }
}

public enum GameResult { CookiesWin, MonsterWins }
```

### 8.2 결과 화면 UI (`Assets/Screenshots/Result.png` 반영, "잡힘"→"부숴짐")

레퍼런스 이미지 구성 분석:
- **상단 배너**: "괴물 승!"(보라) / "쿠키 승!"(금색) 텍스트 + 승리 사유 부제
- **좌측 일러스트**: 승리 진영에 맞춰 괴물 단독 일러스트 또는 쿠키 4인 축하 일러스트
- **중앙 패널 "남은 쿠키 수"**: `생존수/4` 분수 + 쿠키 아이콘 4개, 파괴된 쿠키는 회색 실루엣
- **우측 패널**: 플레이어 목록 — 괴물 행은 왕관 아이콘+"(괴물)" 라벨, 쿠키 행은 "✕ 부숴짐"
  (빨강) 또는 "○ 생존"(파랑/흰색)
- **하단**: "로비로 이동 (12)" 버튼

```csharp
// Assets/02. Scripts/Monster/ResultScreenController.cs — GameScene에 1개, 전원 대상
public class ResultScreenController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject monsterWinBanner;
    [SerializeField] private GameObject cookieWinBanner;
    [SerializeField] private TMP_Text remainingCountText;
    [SerializeField] private Image[] cookieIcons;          // 4개, 생존=컬러/파괴=그레이스케일
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerResultRow playerRowPrefab; // 이름 + 상태(괴물/부숴짐/생존)
    [SerializeField] private TMP_Text lobbyButtonCountdownText;
    [SerializeField] private float autoReturnDelay = 12f;

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.GameResult)) return;
        if (!RoomState.TryGetInt(NetKeys.GameResult, out int result)) return;

        ShowResult((GameResult)result);
    }

    private void ShowResult(GameResult result)
    {
        root.SetActive(true);
        monsterWinBanner.SetActive(result == GameResult.MonsterWins);
        cookieWinBanner.SetActive(result == GameResult.CookiesWin);

        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters);
        int aliveCount = 0;
        int cookieIndex = 0;

        foreach (Player p in PhotonNetwork.PlayerList.OrderBy(pl => pl.ActorNumber))
        {
            var row = Instantiate(playerRowPrefab, playerListContent);
            bool isMonster = monsters != null && monsters.Contains(p.ActorNumber);
            int hitCount = p.CustomProperties.TryGetValue(NetKeys.HitCount, out object v) ? (int)v : 0;
            bool isBroken = hitCount >= 2;

            if (isMonster) { row.SetMonster(p.NickName); continue; }

            row.SetCookie(p.NickName, alive: !isBroken);
            if (cookieIndex < cookieIcons.Length) cookieIcons[cookieIndex].color = isBroken ? Color.gray : Color.white;
            cookieIndex++;
            if (!isBroken) aliveCount++;
        }

        remainingCountText.text = $"{aliveCount} / 4";
        StartCoroutine(AutoReturnCountdown());
    }

    private IEnumerator AutoReturnCountdown()
    {
        float remaining = autoReturnDelay;
        while (remaining > 0f)
        {
            lobbyButtonCountdownText.text = $"로비로 이동 ({Mathf.CeilToInt(remaining)})";
            remaining -= Time.deltaTime;
            yield return null;
        }
        OnLobbyButtonClicked();
    }

    public void OnLobbyButtonClicked()
    {
        StopAllCoroutines();
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel(SceneNames.GameLobby);
    }
}
```

> 📌 정상 승리 종료(§8)는 §7(괴물 전원 이탈로 인한 비정상 종료)과 달리 **괴물과 쿠키 모두
> 함께 `GameLobbyScene`으로 복귀**해도 무방하다 — `PhotonNetwork.LoadLevel()`을 그대로 써도
> 안전하다.

---

## 9. `PlayerMoveState` / NetKeys / NetEventCodes 최신 목록 (v3.2 전면 갱신)

```csharp
public enum PlayerMoveState
{
    Idle, Walk, Run, Jump, Dodge,
    Held,   // 그랩당한 상태(§4.1, 유지)
    Broken, // 괴물에게 2회 피격되어 파괴된 상태(§4.2/§6.3) — v3의 Caught를 대체. 1회 피격(균열)은
            // 별도 상태가 아니라 순수 시각 효과(PlayerCrackDisplay)이므로 이동 애니메이션은 그대로 유지된다.
}
```

```csharp
public static class NetKeys
{
    public const string GameEndTime = "GameEndTime"; // 기존 값 실사용(§6.4)

    // 괴물 선정(§2)
    public const string MonsterActorNumbers = "MonsterActorNumbers"; // int[]
    public const string MonsterRevealTime = "MonsterRevealTime";
    public const string CookiesDeparted = "CookiesDeparted";

    // 페인트 페이즈(§3)
    public const string PaintPhaseEndTime = "PaintPhaseEndTime";
    public const string MonsterJoined = "MonsterJoined";
    public const string ForcedPaintActorNumbers = "ForcedPaintActorNumbers"; // int[] — §3.6
    public const string ForcedPaintColors = "ForcedPaintColors"; // int[] — ForcedPaintActorNumbers와 인덱스로 1:1 대응

    // 이탈/방장 위임(§7)
    public const string MonsterDepartedAt = "MonsterDepartedAt";

    // 승패(§8)
    public const string GameResult = "GameResult";

    // Player CustomProperties
    public const string HitCount = "HitCount"; // int(0~2) — §4.2 신규(v3.2). 0=정상, 1=균열, 2=파괴. v3의 IsCaught를 대체
    public const string RegisteredSlotCount = "RegisteredSlotCount"; // int — §3.2
}

public static class NetEventCodes
{
    public const byte PaintStroke = 1;
    public const byte ClaimMonster = 2;
    public const byte ClearColor = 3;
    public const byte FillAll = 4;
    // NoisePing = 5 — 폐기(§5)
    // MagicCast = 6 — 폐기(구 §6.2)
}
```

> 📌 괴물의 타격(§4.2)은 `RequestHit` RPC로 처리되므로 현재는 별도 `RaiseEvent` 코드가
> 필요 없다. 다만 §12(판정 트리거 방식 미정)가 확정되는 방식에 따라(예: 타격 이펙트를 전원에게
> 재생해야 한다면) 신규 `NetEventCodes` 항목이 추가될 수 있다.

---

## 10. 필요한 프리팹 · 에셋 전체 목록 (v3.2 갱신)

표시 규칙: **기존재 / 확보 / 신규**로 상태를 표시한다.

### 10.1 신규/변경 스크립트·컴포넌트 전체 목록

| 파일 경로 | 역할 | 근거 절 |
|---|---|---|
| `Monster/Cauldron.cs` | 가마솥 트리거, `ClaimMonster` 요청 발신 | §2.1 |
| `Monster/MonsterAssignmentAuthority.cs` | 마스터 전용 괴물 확정(선착순+타임아웃) | §2.1 |
| `Monster/MonsterRevealController.cs` | 리빌 연출 + 쿠키→괴물 프리팹 교체 | §2.2 |
| `Monster/CookieDepartureController.cs` | 쿠키 전용, 10초 후 GameScene 이동 | §2.3 |
| `Monster/MonsterJoinController.cs` | 60초 후 괴물 GameScene 합류 트리거, `GameEndTime` 세팅 | §6.4 |
| `ColorTag/PlayerPaintCanvas.cs`(수정) | 슬롯/임계량 등록+슬롯 수 네트워크 보고, 지우개, 배정된 강제 도포 색 적용 | §3.2, §3.6 |
| `ColorTag/ColorSwatchButton.cs`(수정) | `SetBrushColor()` 호출로 교체 | §3.3 |
| `ColorTag/PaintPhaseController.cs` | 마스터 전용, 등록 슬롯 0개 플레이어에게 서로 겹치지 않는 색을 배정 | §3.6 |
| `ColorTag/RoomState.cs`(수정) | `TryGetIntArray()` 추가 | §7.1, §3.6 |
| `Grab/PlayerGrabController.cs` | 쿠키↔쿠키 그랩 시작/해제(그랩버 측) | §4.1 |
| `Unit/PlayerAnimationDriver.cs`(수정) | `SetCarryLayerWeight()` 추가 | §4.1, §11.2 |
| `Unit/HideOrSeekPlayer.cs`(수정) | `OnGrabbedByOwner`/`OnReleased`/`hitCount`/`RequestHit` | §4.1, §4.2 |
| `Monster/MonsterStrikeAttack.cs` | 괴물의 촉수/손 타격 판정(신규, 세부 미확정) | §4.2 |
| `ColorTag/PlayerCrackDisplay.cs` | 균열/파괴 시각 효과, 소유권 무관 표시(신규, v3.2) | §4.2 |
| `Monster/MonsterFirstPersonCamera.cs` | 괴물 1인칭 카메라(신규, v3.2 확정) | §6.2 |
| `Monster/MonsterController.cs` | 괴물 전용 이동+시점 회전 컨트롤러(신규, v3.2 — `HideOrSeekPlayer` 재사용 불가 이유는 §6.2) | §6.2 |
| `Monster/SpectatorController.cs`(트리거를 `hitCount>=2`로 재설계) | 파괴된 쿠키 시점 순환 | §6.3 |
| `Monster/RoomLifecycleWatcher.cs`(재작성) | 다중 괴물 이탈 감지+5초 경고 | §7.1 |
| `Monster/MonsterDepartureBanner.cs` | 이탈 경고 배너 로컬 표시 | §7.1 |
| `Monster/GameRuleController.cs` | 승리 판정(마스터 전용) | §8.1 |
| `Monster/ResultScreenController.cs` | 결과 화면 표시/자동 복귀 | §8.2 |
| `ColorTag/ColorSelectionManager.cs`, `ColorVoteTally.cs`, `TaggerColorAssigner.cs`, `PlayerColorVoteIndicator.cs`, `PlayerColorDisplay.cs` | **삭제 대상** | §0 |
| ~~`Witch/WitchMagicAttack.cs`~~ | **폐기** — 마법 공격 자체가 없어짐 | 구 §6.2 |
| ~~`Grab/ThrowableProp.cs`, `Grab/NoiseListener.cs`~~ | **폐기** — 소품 던지기 기능 자체가 없어짐 | §5 |

### 10.2 프리팹 목록

**A. 네트워크 프리팹**(`PhotonNetwork.Instantiate` 대상 — 반드시 `Assets/04. Prefabs/Resources/`)

| 프리팹 | 상태 | 부착 컴포넌트 | 근거 |
|---|---|---|---|
| `HideOrSeekPlayer.prefab` | 기존재(수정 필요) | `PlayerGrabController`, `PlayerCrackDisplay`, Animator에 `Carry` 레이어+`Broken` 트리거 추가 | §4.1, §4.2 |
| `MonsterPlayer.prefab` | **신규** | `MonsterStrikeAttack`, `MonsterController`, 눈 위치의 `eyeSocket` 자식 Transform(`MonsterFirstPersonCamera`는 Main Camera 쪽에 별도 부착), `PhotonView`+`PhotonTransformView` | §2.2, §4.2, §6.2 |
| `BrushCursor.prefab` | 기존재, 변경 없음 | — | (ColorTag 기존) |
| ~~던질 수 있는 소품~~ | **폐기** | — | §5 |

**B. 로컬 전용 프리팹**(각 클라이언트가 로컬 `Instantiate()` — 네트워크 오브젝트 아님)

| 프리팹 | 상태 | 용도 | 근거 |
|---|---|---|---|
| 가마솥 보글보글 파티클(`bubbleFx`) | **신규** | 괴물 미확정 대기 중 연출 | §2.2 |
| 가마솥 짜잔 리빌 파티클(`revealFx`) | **신규** | 괴물 확정 순간 연출 | §2.2 |
| 타격 임팩트 이펙트(선택) | **신규**(선택) | 1회 피격 시 타격감 연출 | §4.2 |
| 파괴(shatter) 파편 VFX | **신규**(사실상 필수) | 2회 피격 시 `PlayerCrackDisplay.PlayBreakEffect()`가 재생 | §4.2 |

**C. 씬 배치 오브젝트**(프리팹화 권장)

| 프리팹 | 상태 | 용도 | 근거 |
|---|---|---|---|
| `Cauldron.prefab` | **신규** | 가마솥 3D 모델+트리거 콜라이더+`Cauldron`+`MonsterAssignmentAuthority`, `GameLobbyScene`에 1개 | §2.1 |
| 문(door) 프리팹 | **신규**(선택) | 동일 모양 4개 배치, 순수 장식 | §1 |

**D. UI 프리팹**(`Resources/UI/{Popup|Scene|Tab}/{클래스명}` 컨벤션)

| 프리팹 | 상태 | 용도 | 근거 |
|---|---|---|---|
| `Resources/UI/Scene/ColorSlotPanel/ColorSlotPanel.prefab` | **신규**(기존 `ColorSelectionPanel.prefab` 대체) | 4슬롯+Reset+지우개 UI | §3.4 |
| `Resources/UI/Scene/ResultScreen/ResultScreen.prefab` | **신규** | 승패 배너+남은 쿠키 패널+플레이어 목록 루트 | §8.2 |
| `Resources/UI/Scene/ResultScreen/PlayerResultRow.prefab` | **신규** | 결과 화면 플레이어 목록 행(이름+상태) | §8.2 |
| `Resources/UI/Popup/MonsterDepartureBanner/MonsterDepartureBanner.prefab` | **신규** | "괴물이 나갔습니다" 경고 배너 | §7.1 |
| `ConfirmDialog`/`GameLobbyPanel`/`LobbyPanel`/`PlayerListItem`/`RoomListItem` | 기존재, 변경 없음 | — | (기존) |
| `ColorSelectionPanel.prefab`(기존) | **폐기 대상** | `ColorSlotPanel`로 대체됨 | §0, §3 |

### 10.3 3D 모델 / 메시

| 항목 | 상태 | 근거 |
|---|---|---|
| 괴물 캐릭터 메시(촉수/손 포함) | **신규 — 참고 이미지는 확보됨**(`Assets/Screenshots/괴물 T-pose.png`, 삐에로 풍, 손 2+촉수 2+다리 2의 6지 구조). **실제 3D 모델(메시/텍스처/리그) 자체는 여전히 미확보**(§14.1 최우선). **신규 요구**: 1인칭 카메라가 붙을 눈(`eyeSocket`) 위치가 필요한데, 구체형 머리라 T-pose 이미지만으로는 눈 위치가 불분명함(§12) | §2.2, §4.2, §6.2 |
| 가마솥 3D 모델 | **신규** — 제작 또는 에셋스토어 구매 | §2.1 |
| 문(4개) 모델 | **신규**(선택) | §1 |
| ~~마녀 지팡이 + 1인칭 손 모델~~ | **폐기** | 구 §6.2 |
| ~~던질 수 있는 소품 모델~~ | **폐기** | §5 |

### 10.4 애니메이션 (FBX/컨트롤러) — 확보 현황 종합

| 항목 | 상태 | 비고 |
|---|---|---|
| `Cookie_Carrying.fbx` | **확보** — Humanoid+공용 아바타 전환 필요 | §11.2 |
| `Cookie_Hanging_Idle.fbx` | **확보** — 통합 보류(사용자 지정) | §11.3 |
| `PlayerAnimator.controller`(기존) 수정 — `Carry` 레이어(Avatar Mask) + `Held`/`Broken` 트리거 추가 | **수정 필요** | §4.1, §9 |
| 괴물 이동(Idle/Walk) 애니메이션 세트 | **신규, 미확보** | §12 |
| 괴물 촉수 또는 손 타격(스윙) 모션 | **신규, 미확보** — 촉수/손 중 무엇을 만들지부터 §12/§14.2 결정 필요 | §4.2 |
| 괴물 파괴(shatter) 연출 — 애니메이션 필요 여부 | **미정** — `PlayerCrackDisplay`는 현재 "렌더러 끄고 VFX만 재생"으로 최소 설계돼 있어 별도 애니메이션 클립이 필수는 아니지만, 더 자연스러운 연출을 원하면 "산산조각" 포즈/파티클 타이밍이 맞물린 전용 클립이 필요할 수 있음 | §4.2, §12 |
| `Cookie_StandUp.fbx` — 괴물 리빌용 재사용 여부 | **재사용 불투명** — 괴물이 촉수 2개를 포함한 6지 구조로 확인돼, 표준 Humanoid 리타겟 전제가 성립하지 않는다. Generic 리그 전환 또는 다리만 신규 제작 중 택일 필요(§11.1) | §11.1 |

### 10.5 셰이더 / 머티리얼

| 항목 | 상태 | 근거 |
|---|---|---|
| `EraseStampMaterial.mat` | **신규** — 기존 `brushStampMaterial` 구조 복제 후 출력 알파 고정 0 | §3.5 |
| `FillAllMaterial.mat` | **신규** — UV 전체를 단색으로 덮는 전용 블릿 | §3.6 |
| `ColorReplaceMaterial.mat`(기존) | 기존재, 그대로 재사용(Reset 용도) | §3.4 |
| `ColorTag/PlayerPaintedSkin` 셰이더(기존) 수정 — `_CrackAmount`(또는 `_CrackTex`) 프로퍼티 추가 | **수정 필요, v3.2 신규** — 균열 오버레이용. `PlayerPaintCanvas`가 이미 만들어둔 런타임 머티리얼 인스턴스에 프로퍼티만 얹으면 됨(§4.2) | §4.2 |
| ~~마법 VFX용 머티리얼~~ | **폐기** | 구 §6.2 |

### 10.6 파티클 / VFX

| 항목 | 상태 | 근거 |
|---|---|---|
| 가마솥 보글보글(대기 연출) | **신규** | §2.2 |
| 가마솥 짜잔(리빌 연출) | **신규** | §2.2 |
| 타격 임팩트(선택) | **신규**(선택) | §4.2 |
| **파괴(shatter) 파편 VFX** | **신규(사실상 필수)** — 사용자가 명시적으로 요구한 핵심 연출("쿠키가 부숴지는 연출") | §4.2 |
| 그랩/캐리 관련 VFX | 불필요(문서상 요구 없음) | — |

### 10.7 오디오

| 항목 | 상태 | 근거 |
|---|---|---|
| 타격 SFX(1회 피격) | **신규**(선택) | §4.2 |
| 파괴 SFX(2회 피격) | **신규**(사실상 필수 — 파괴 VFX와 짝) | §4.2 |
| 가마솥 보글보글 SFX(선택) | **신규**(선택) | §2.2 |
| ~~던지기 착지 소음 SFX~~ | **폐기** | §5 |
| ~~마법 캐스팅 SFX~~ | **폐기** | 구 §6.2 |

### 10.8 UI 아트(스프라이트/아이콘/일러스트)

| 항목 | 상태 | 근거 |
|---|---|---|
| 결과 화면 — 괴물 승/쿠키 승 배너 아트 2종 | **신규**, `Result.png` 참고 | §8.2 |
| 결과 화면 — 괴물 단독/쿠키 4인 축하 일러스트 2종 | **신규**, `Result.png` 참고(괴물 모델 확보 후 제작) | §8.2 |
| 결과 화면 — 생존/부숴짐 쿠키 아이콘, 왕관 아이콘 | **신규**, `Result.png` 참고. 균열(1회 피격) 상태 전용 아이콘을 따로 둘지는 선택(§12) | §8.2 |
| 색 슬롯 UI — 4칸 배경, Reset/지우개 버튼 아이콘 | **신규** | §3.4 |
| 괴물 이탈 경고 배너 배경/아이콘 | **신규** | §7.1 |

### 10.9 인프라 설정(레이어/태그)

`ProjectSettings/TagManager.asset` 기준, 현재 커스텀 레이어는 `PlayerCapsule`(8번) 하나뿐이고
`Cookie`/`Monster`처럼 그랩·타격 판정에 쓸 전용 레이어가 없다. `MonsterStrikeAttack.cookieLayer`
(§4.2)와 `PlayerGrabController.playerLayer`(§4.1)가 참조하는 `LayerMask`는 이 레이어들이 실제로
만들어져야 동작한다.

| 레이어 이름(안) | 용도 | 근거 |
|---|---|---|
| `Cookie` | 쿠키 캐릭터 판별(타격 판정 대상 필터) | §4.2 |
| `Monster` | 괴물 캐릭터 판별(그랩 대상에서 제외 등) | §4.1 |
| (기존 `PlayerCapsule`은 그대로 유지 — 붓칠 레이캐스트 제외용, 용도 다름) | — | (기존) |

---

## 11. 애니메이션 자산 실물 검증

### 11.1 `Cookie_StandUp.fbx` — **괴물 리빌용 재사용은 불투명, T-pose 확인 결과 반영**

v2 조사 결과(변경 없음): 클립 이름 `Cookie_StandUp`, 94프레임 1회성(`loop:0`), 아직 Humanoid로
전환되지 않은 `animationType: 2`(Generic) 상태로 확보돼 있다.

**T-pose 이미지 확인 결과 반영**: 괴물은 다리 2개는 쿠키와 비슷한 이족보행 비율이지만, **팔이
4개(관절형 손 2 + 구불거리는 촉수 2)** 인 6지 구조다. Unity의 표준 Humanoid 리타겟은 양팔·
양다리 하나씩만 매핑하므로, 촉수 2개는 애초에 Humanoid 본 슬롯에 들어갈 자리가 없다 — 다음
두 방향이 남는다:
- **Generic 리그로 전환**하고 촉수는 Extra Bones(추가 트랜스폼)로 별도 애니메이션.
- 다리만 쿠키 Humanoid 클립을 참고해 **새로 제작**하고, 팔/촉수는 처음부터 전용 애니메이터로
  분리 — `Cookie_StandUp.fbx` 자체는 리빌 연출용으로 재사용하지 않음.

어느 쪽이든 실제 3D 모델 파일(메시+리그, §14.1 최우선 미확보)이 나와야 최종 결정이 가능하다.

### 11.2 `Cookie_Carrying.fbx` + `Cookie_Walking.fbx` 조합 — 변경 없음(§4.1 전용, 괴물과 무관)

`Cookie_Carrying.fbx`를 Humanoid+`Cookie_Idle` 공용 아바타로 임포트하고, Animator에 상반신
전용 Avatar Mask `Carry` 레이어를 추가해 Base Layer(다리)와 독립적으로 on/off한다.

### 11.3 `Cookie_Hanging_Idle.fbx` — 보류 (변경 없음)

사용자가 "보류"로 명시했으므로 §4.1의 "통합 후보"로만 언급, 실제 Animator 배선은 하지 않는다.

### 11.4 "흐느적거리며 자연스럽게 잡는" 도입 모션 — 변경 없음(§4.1 전용)

하드컷(`Animator.Play()`) 방식 권장은 쿠키↔쿠키 그랩에 대한 결론이며 괴물의 촉수/손 타격
모션과는 별개다 — 괴물 쪽 타격 모션의 하드컷/크로스페이드 여부는 §12가 확정된 뒤 별도 검토가
필요하다.

---

## 12. 열린 질문 (v3.2 전면 갱신)

**해결됨**:
- 괴물 카메라 시점 → **1인칭으로 확정**(사용자 확인, §6.2).
- §6.2 마법 자원/쿨다운, 던지기 관련 질문 전체 → 마법·던지기 둘 다 폐기되어 무효화.
- 괴물이 쿠키와 같은 Humanoid 아바타를 쓸 수 있는지 → **사실상 해결**(T-pose 확인 완료,
  §11.1): 촉수 2개가 있는 6지 구조라 표준 Humanoid 리타겟은 쓸 수 없다. Generic 리그 전환
  또는 다리만 별도 제작 중 어느 쪽을 택할지는 실제 3D 모델(메시+리그) 확보 후 결정.
- 근접 "포획" 판정 방식(구 질문) → **포획 개념 자체가 폐기**되어 무효화, 아래 1~2·5~6번으로
  대체.

**남아있는 것 + 신규**:
1. **타격 판정을 언제 시도하는지** — 괴물이 근접하면 자동으로 판정하는지, 특정 입력(키/클릭)이
   필요한지, 애니메이션 이벤트(예: 팔을 휘두르는 모션의 특정 프레임)에 맞춰 판정하는지.
2. 괴물이 **촉수/손 중 무엇으로 때리는지** — 하나만 쓰는지, 전방은 손·후방은 촉수처럼 상황별로
   병행하는지.
3. **균열(1회 피격) 상태의 이동/행동 제약 여부** — 현재 문서 기본값은 "제약 없음, 정상 이동
   가능"으로 잠정 설계했다(§4.2). 속도 감소 등 페널티를 줄지 확정 필요.
4. **균열이 회복되는지** — 시간 경과나 다른 쿠키의 도움(구출과 유사한 개념)으로 정상 상태로
   되돌아갈 수 있는지, 아니면 균열은 영구적이고 두 번째 타격만 막을 수 있는지(§6.3). 기본값은
   "회복 없음"으로 잠정.
5. **1인칭 시점에서 뒤쪽 촉수 타격을 어떻게 조준/인지하는지** — §6.2에서 새로 드러난 문제.
   화면 가장자리 경고 UI, 반자동 판정 등 방식 결정 필요.
6. 손과 촉수의 **판정 범위·쿨다운이 서로 다른지**(예: 손은 사거리 짧고 빠름, 촉수는 사거리
   길고 느림 등 차별화 여부).
7. **괴물 이동 방식** — `HideOrSeekPlayer`처럼 Rigidbody 물리 기반인지, `MonsterController`
   골격 코드처럼 단순 `transform.position` 이동인지 확정 필요.
8. §2.1 `monsterSelectTimeout`(가마솥 무입장 타임아웃) 구체적 초수 — 임시로 30초 가정.
9. **괴물 이동(Idle/Walk) 애니메이션 세트 공백** — 쿠키 리그를 공유한다면 `Cookie_Walking/
   Run.fbx`를 재사용할 수 있을지, 아니면 괴물 전용 이동 클립이 필요한지 확인 필요(§11.1
   재검토와 연동).
10. **괴물 눈(카메라 부착 지점, `eyeSocket`) 위치** — 구체형 머리라 T-pose 이미지만으로는
    불분명, 3D 모델 제작 시 함께 결정 필요(§6.2, §10.3).
11. §3.2 `MinStrokesToRegister`(임계 스탬프 수) 밸런스 값 — 실제 플레이테스트로 조정 필요.
12. §7.1 5초 경고 타이밍 관련 원문 모호성 — 구현 시 재확인 권장.
13. §10.9 신규 레이어(`Cookie`/`Monster`) 이름·번호 확정 및 프리팹에 실제로 배정하는 작업.
14. §3.6 강제 도포 색 배정에 쓰는 `finalizeStampMaterial`(기존 재사용)과 `FillAllMaterial`
    (§10.5, 신규 예정) 중 어느 쪽을 실제로 쓸지.

---

## 13. 구현 순서 제안 (v3.2 갱신)

1. **§2(괴물 선정+타임아웃+연출) + §9 NetKeys/EventCodes 골격 + §7.2(방장 위임 확인)** — §7.2는
   Photon 기본 동작이라 "확인만" 하면 되므로 별도 구현 비용이 거의 없다.
2. **§3(자유 색칠, 임계 등록) 재작성** — 이번 개정과 무관, 우선순위 2위 유지.
3. **§6.1(안개) + §6.4(GameEndTime) + §4.2(타격/균열/파괴) + §6.2(1인칭 카메라·`MonsterController`)
   + §8(승리 판정+결과 화면)** — §12의 1~7번(판정 트리거, 촉수/손, 균열 제약/회복, 조준,
   이동 방식)이 먼저 확정돼야 실제 구현에 들어갈 수 있다.
4. **§7.1(괴물 이탈 처리)** — 3번이 끝난 뒤 안정성 보강 차원에서 진행.
5. **§4.1(쿠키↔쿠키 그랩)** — 애니메이션 리소스는 이미 절반 이상 확보돼 있어 리스크가 낮다.
6. 애니메이션/파티클/모델/UI 아트 자산은 병렬 진행 — **단, 괴물 3D 모델(§14.1 최우선)이
   나오지 않으면 §2.2 리빌 연출·§4.2 타격 모션·§6.2 눈 위치·§11.1 재검토 전부 착수가 막힌다**는
   점이 계속되는 병목이다.

---

## 14. 사용자 제공 필요 항목 (핸드오프 체크리스트, v3.2 갱신)

§10(에셋 전체 목록)과 §12(열린 질문)에 흩어진 항목 중 **"실제로 사용자가 만들거나/구해서
프로젝트에 넣어야 하는 것"**과 **"예/아니오 답변 한 줄이면 되는 것"**만 골라 별도로 정리한
것이다. 나머지는 §14.3에 명시했듯 사용자가 따로 준비할 필요 없이 진행 가능하다.

### 14.1 에셋 제공 필요 (우선순위순)

| 우선순위 | 항목 | 현재 상태 / 필요 이유 | 근거 |
|---|---|---|---|
| ✅ 완료 | ~~괴물 T-pose 참고 이미지~~ | **확보 완료.** `Assets/Screenshots/괴물 T-pose.png` + `리소스/괴물 T-pose.png` — 삐에로 풍 몬스터, 손 2+촉수 2+다리 2의 6지 구조(§4.2) | §0, §4.2 |
| 🔴 최우선 | 괴물 3D 모델(메시/텍스처/리그, 촉수+손 포함, **눈/카메라 부착 위치 포함**) | **여전히 미확보.** T-pose 이미지는 컨셉 참고용 렌더일 뿐, 실제 게임에 넣을 3D 모델은 별도 제작이 필요하다. 1인칭 확정(§6.2)으로 눈 위치까지 함께 결정돼야 함 | §10.3, §11.1, §6.2 |
| 🔴 최우선 | 괴물 이동(Idle/Walk) 애니메이션 세트 | **미확보** | §10.4, §12-9 |
| 🔴 최우선 | 괴물 촉수 또는 손 타격(스윙) 애니메이션 | **미확보.** 어느 쪽을 쓸지부터 §14.2 결정이 선행돼야 함 | §10.4, §4.2, §12-2 |
| 🟠 (신규, 사실상 필수) | 파괴(shatter) 파편 VFX + 파괴 SFX | **미확보.** 사용자가 명시적으로 요구한 핵심 연출("두 번 타격 시 부숴지는 연출") | §10.6, §10.7, §4.2 |
| 🟡 | 가마솥 3D 모델 | 미확보(원 요청에서 "만들거나 찾을 예정") | §10.3, §2.1 |
| 🟡 (선택) | 문 4개 모델 | 미확보, 동일 프리팹 4회 배치로 대체 가능 | §10.3, §1 |
| 🟢 | 가마솥 보글보글/짜잔 파티클 | 미확보 | §10.6, §2.2 |
| 🟢 (선택) | 타격 임팩트 이펙트 + 타격 SFX(1회 피격용) | 미확보, 필요 여부부터 미정 | §10.6, §10.7, §4.2 |
| 🟢 | 결과 화면 아트(배너 2종, 일러스트 2종, 생존/부숴짐 아이콘, 왕관 아이콘) | 미확보 — 괴물 일러스트는 위 3D 모델/컨셉이 먼저 필요 | §10.8, §8.2 |
| 🟢 | 색 슬롯 UI 아이콘(Reset/지우개 버튼) | 미확보(이번 개정과 무관) | §10.8, §3.4 |
| — | ~~마녀 지팡이/손 모델, 던질 소품 모델, 마법 VFX, 마법·던지기 SFX~~ | **더 이상 필요 없음** | 구 §6.2, §5 |

### 14.2 답변만 필요한 것 (결정 사항)

| 항목 | 현재 임시값 / 상태 | 근거 |
|---|---|---|
| **타격 판정을 언제 시도하는지**(자동 근접/입력 키/애니메이션 이벤트) | 미정 | §12-1 |
| **촉수냐 손이냐** (또는 둘 다 상황별로) | 미정 — §4.2 코드는 둘 다 필드만 준비해둔 상태 | §12-2 |
| **균열(1회 피격) 상태의 이동 제약 여부** | 임시로 "제약 없음"(§4.2) | §12-3 |
| **균열이 회복되는지**(시간/다른 쿠키 도움) | 임시로 "회복 없음"(§6.3) | §12-4 |
| **1인칭에서 뒤쪽 촉수 타격을 어떻게 조준/인지하는지** | 미정 | §12-5 |
| 손/촉수 판정 범위·쿨다운 차등 여부 | 미정 | §12-6 |
| 괴물 이동 방식(물리 기반 vs 단순 Transform) | 미정 — `MonsterController` 골격은 단순 이동으로 임시 작성 | §12-7 |
| 가마솥 무입장 타임아웃 | 30초(가정값) | §2.1, §12-8 |
| 색 슬롯 등록 임계값(`MinStrokesToRegister`) | 15스탬프(가정값, 이번 개정과 무관) | §3.2, §12-11 |

### 14.3 사용자가 안 줘도 되는 것

아래는 에셋이나 결정이 아니라 **구현 작업 자체**라 개발 쪽(Unity 에디터 조작 포함)에서 그대로
진행 가능하다:
- `Cookie_Carrying`/`Cookie_Hanging_Idle.fbx`의 Humanoid+공용 아바타 Import 설정 전환(§11.2,
  §4.1 전용, 괴물 교체와 무관)
- `Witch/*` → `Monster/*` 폴더·클래스 명칭 일괄 변경, `HitCount`/`Broken` 등 신규 네이밍
  적용 작업 자체(§9, §10.1)
- `PlayerPaintedSkin` 셰이더에 `_CrackAmount` 프로퍼티 추가(§10.5) — 코드/셰이더 작업이지
  결정 사항이 아님
- 신규 레이어(`Cookie`/`Monster`) 신설 및 프리팹 배정(§10.9, §12-13)
- §10.1에 정리된 스크립트/컴포넌트 구현 전체(단, 착수 시점은 §14.1의 최우선 에셋 확보 이후)
