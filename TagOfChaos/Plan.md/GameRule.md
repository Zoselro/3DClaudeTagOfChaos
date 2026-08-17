# GameRule.md — 숨바꼭질(쿠키) + 술래잡기(마녀) 본게임 설계 (v2, 2026-08-17)

> **v1 대비 변경 요약**(이번 개정 사유):
> 1. 마녀 포획 시스템(그랩 기반 §6.2, 관전 트리거) → **완전 폐기, 마법 원거리 즉사 공격**으로
>    교체(`Assets/Screenshots/Magic.png` 확인, 아래 §6.2).
> 2. 마녀 선정(§2.1) — 선착순 원칙은 유지하되 **아무도 안 들어가면 타임아웃 후 랜덤 배정** 추가.
> 3. 색칠 슬롯 등록(§3.1~3.2) — 즉시 등록 → **일정량 이상 칠해야 등록**되는 방식으로 변경(악성
>    유저의 "1픽셀만 칠해서 색 숨기기" 방지).
> 4. **마녀/방장 이탈 처리 + 다중 마녀 확장**(§7, 신규) — 인원수 확장을 고려한 설계.
> 5. **승패 결과 화면**(§8.2, 신규) — `Assets/Screenshots/Result.png` 반영.
> 6. **쿠키 그랩/캐리 애니메이션 실물 검증**(§11, 신규) — 확보된 `Cookie_Carrying.fbx`/
>    `Cookie_Hanging_Idle.fbx`를 실제로 열어(바이너리 문자열 추출) 리그를 확인한 결과.
>
> §0~§6은 v1 골격을 유지하되 위 변경분을 반영해 갱신했다. 완전히 새로 쓴 절은 §7/§8.2/§11이다.
>
> **추가 반영(같은 날 후속 확인)**: 마녀용 "Zombie Stand Up" 애니메이션은 `Cookie_StandUp.fbx`
> 라는 이름으로 이미 프로젝트에 들어와 있고 Unity에도 한 번 임포트된 상태임을 확인했다(§11.1
> 갱신). 또한 §6.2의 마나/쿨다운 수치는 **추후 다른 방향으로 설계할 예정이라 이번 문서에서
> 제외**한다 — 관련 서술과 코드를 단순화했다.

---

## 0. 이 설계가 기존 코드에 미치는 영향 (요약, v1과 동일 + 갱신)

현재 `ColorTag/` 도메인(15파일)의 "팀 투표로 술래 색 감추기" 컨셉은 완전히 대체된다. 색은
개인 자유 표현(최대 4색, 슬롯 UI, 임계량 등록, Reset/지우개)이고, 마녀는 색이 아니라 **가마솥
행위 + 별도 캐릭터 모델**로 정해지며, 포획이 아니라 **원거리 마법 즉사**로 쿠키를 제거한다.

| 기존 컴포넌트 | 처리 |
|---|---|
| `ColorSelectionManager`(4라운드 루프+`AssignTagger`) | **폐기**. §2(마녀 선정)+§3(페인트 타이머)로 대체 |
| `ColorVoteTally` / `TaggerColorAssigner` | **폐기**. 팀 투표·색 치환 식별자 개념 자체가 없어짐 |
| `ColorSelectionPanel`/`ColorSwatchButton` | **재작성**. 개인 슬롯 UI(§3.4)로 |
| `PlayerColorVoteIndicator` / `PlayerColorDisplay` | **폐기** |
| `PlayerPaintCanvas` | **핵심 로직 재사용**, 슬롯/임계량 등록/지우개로 교체(§3) |
| `RoomLifecycleWatcher`(술래 퇴장 감지) | **재작성**. 단일 `TaggerActorNumber` → 다중 `WitchActorNumbers` 배열 대응(§7) |
| `GameLobbyController.OnMasterClientSwitched` | **그대로 활용** — 방장 위임은 Photon 기본 동작으로 이미 해결됨(§7.2) |

---

## 1. 전체 게임 플로우 (갱신 — 마녀 미배정 타임아웃 반영)

```
GameLobbyScene (대기실 — 문 4개 + 가마솥)
  ├─ 아무 쿠키나 가마솥에 들어감 → 선착순 마녀 확정(§2.1)
  │    └─ 예외: WitchSelectTimeout(예: 30초) 안에 아무도 안 들어가면 마스터가 랜덤 1인 배정
  ├─ 연출(보글보글→짜잔) + 마녀 프리팹 교체(§2.2)
  ├─ 10초 카운트다운 → 쿠키만 GameScene 이동, 마녀는 GameLobbyScene 대기(§2.3~2.4)
GameScene (쿠키만 입장)
  ├─ 60초 자유 색칠(§3) — 일정량 이상 칠한 색만 슬롯에 등록
  ├─ 60초 경과 → 등록 슬롯 0개인 플레이어는 전신 랜덤 단색 강제 도포(§3.6)
  └─ 마녀 GameScene 합류(§6.4) — GameEndTime = 합류 시각 + 10분
        ├─ 쿠키: 시야 축소(안개, §6.1)
        ├─ 쿠키: 그랩/캐리(§4), 던지기(§5, 소리 유인)
        ├─ 마녀: F키 마법 즉사 공격(§6.2, 실제 플레이어, AI 아님)
        ├─ 사망한 쿠키: Space로 생존 쿠키 시점 관전(§6.3)
        ├─ 마녀 전원 퇴장 시 5초 경고 후 GameLobbyScene 복귀(§7)
        └─ 승리 판정(§8) — 전원 사망→마녀 승, 10분 생존→쿠키 승, 결과 화면(§8.2) 표시
```

---

## 2. 마녀 선정 & 가마솥 연출 (`GameLobbyScene`)

### 2.1 가마솥 트리거 — 선착순 + 타임아웃 랜덤 배정

사용자 확인: "선착순 자진 입장이 맞다. 하지만 아무도 안 들어가면 랜덤으로 1명이 마녀가 된다."
클라이언트가 신청을 마스터에 보내고 마스터가 확정하는 v1 구조(§2.1)는 그대로 두고, **마스터
전용 타임아웃 폴백**만 추가한다 — `ColorSelectionManager`/`RoomLifecycleWatcher`가 이미 쓰는
"마스터만 Update()에서 만료 시각 폴링" 패턴 그대로다.

```csharp
// Assets/02. Scripts/Witch/Cauldron.cs — v1과 동일(생략), OnTriggerEnter에서 ClaimWitch RaiseEvent
```

```csharp
// Assets/02. Scripts/Witch/WitchAssignmentAuthority.cs (마스터 전용, 확장)
public class WitchAssignmentAuthority : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private float witchSelectTimeout = 30f;
    private double sceneEnterTime;

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient) sceneEnterTime = PhotonNetwork.Time;
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.ClaimWitch) return;
        if (!PhotonNetwork.IsMasterClient) return;
        if (HasWitchAssigned()) return; // 이미 확정 — 이후 요청/타임아웃 전부 무시

        int claimantActorNumber = (int)photonEvent.CustomData;
        ConfirmWitch(new[] { claimantActorNumber });
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (HasWitchAssigned()) return;
        if (PhotonNetwork.Time < sceneEnterTime + witchSelectTimeout) return;

        // 아무도 자진 입장하지 않음 — 현재 플레이어 중 무작위 1인을 강제 배정
        var players = PhotonNetwork.PlayerList;
        int randomActorNumber = players[new System.Random().Next(players.Length)].ActorNumber;
        ConfirmWitch(new[] { randomActorNumber });
    }

    private bool HasWitchAssigned() =>
        PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(NetKeys.WitchActorNumbers);

    private void ConfirmWitch(int[] witchActorNumbers)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.WitchActorNumbers, witchActorNumbers }, // 단일 int → int[] (다중 마녀 확장, §7.3)
            { NetKeys.WitchRevealTime, PhotonNetwork.Time },
        });
    }
}
```

> 📌 **다중 마녀 확장에 대한 메모**: 이번 요구사항 자체는 "가마솥에 1명이 들어가면 마녀"이므로
> 초기 배정은 여전히 1명이다. 다만 §7에서 "마녀가 2명일 수 있다"는 이탈 처리 시나리오가
> 명시됐으므로, `WitchActorNumbers`를 **처음부터 배열**로 설계해뒀다 — 나중에 "가마솥에 2명이
> 들어가면 마녀 2명" 같은 규칙이 추가돼도 `ConfirmWitch()`가 받는 배열의 길이만 늘리면 되고,
> §2.2(리빌)/§6.2(마법)/§7(이탈 처리)/§8(승리 판정) 전부 이미 배열 순회로 짜여 있어 추가 수정이
> 거의 필요 없다.

### 2.2~2.4 (v1과 동일, 배열 대응만 반영)

리빌 연출·10초 카운트다운·쿠키만 GameScene 이동·마녀 GameLobbyScene 대기 로직은 v1 §2.2~§2.4
그대로다. 다만 `WitchRevealController`/`PlayerSpawner`의 판별 조건을
`witchActorNumber == PhotonNetwork.LocalPlayer.ActorNumber` 단일 비교에서
`((int[])room.CustomProperties[NetKeys.WitchActorNumbers]).Contains(PhotonNetwork.LocalPlayer.ActorNumber)`
로 바꾸는 것만 다르다(다중 마녀 대응).

---

## 3. 개인 자유 색칠 (GameScene, 60초) — 슬롯 등록 방식 재설계

### 3.1 문제 재정의 — 악성 유저의 "색 숨기기" 방지

사용자 확인: v1에서 우려했던 문제(재도색을 막으면 자연스러운 보정이 어려움)의 실제 의도는
**"1픽셀만 칠하고 다른 색으로 넘어가 마녀가 실제 색을 판단 못 하게 하는 악용"**을 막는 것이었다.
해결책: **색을 선택하는 순간이 아니라, 그 색으로 "일정량 이상" 실제로 칠했을 때만 슬롯에
등록**한다. 등록되지 않은 색은 화면엔 보이지만(실시간 공유 요구사항 충족) 슬롯 카운트에는
잡히지 않으므로, 60초가 지나도 슬롯이 0개면 그대로 전신 랜덤 강제 도포 대상이 된다 — 즉
"조금씩 여러 색을 찍먹만 하고 끝내는" 트롤링은 자동으로 페널티(전신 랜덤 단색)를 받는다.

### 3.2 `PlayerPaintCanvas` 확장 — 임계량 기반 등록

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
        return; // 4슬롯이 이미 다 찬 상태에서 새 색은 칠하는 것 자체를 막음(v1 §3.1과 동일 규칙 유지)
    }

    // 4슬롯 미만이면 스탬프는 허용하되(화면엔 바로 보임), 등록은 임계량을 넘어야 확정
    pendingStrokeCounts.TryGetValue(brushColor, out int count);
    count++;
    if (count >= MinStrokesToRegister)
    {
        pendingStrokeCounts.Remove(brushColor);
        registeredColorSlots.Add(brushColor);
        OnSlotsChanged?.Invoke(registeredColorSlots); // 이 시점에야 UI 슬롯 칸에 색이 들어감
    }
    else
    {
        pendingStrokeCounts[brushColor] = count;
    }
}

StampBrush(hit.textureCoord, brushColor); // 등록 여부와 무관하게 화면 표시 자체는 항상 real-time
```

이미 등록된 색은 기존과 동일하게 몇 번을 다시 칠해도 그대로 통과(`registeredColorSlots.Contains`
가 true라 위 블록 자체를 건너뜀). Reset(§3.4의 v1 로직 그대로)은 등록된 슬롯뿐 아니라
`pendingStrokeCounts`에 남아있는 미등록 색 카운트도 함께 지워야 자연스럽다 — 특정 색을 Reset한
직후 그 색을 다시 조금 칠했을 때 이전 미등록 카운트가 잔류해 있으면 안 되므로.

### 3.3~3.6 (v1과 동일)

브러시 색 선택(`SetBrushColor`), Reset(색상 전역 삭제, `ColorReplaceMaterial` 알파 0 재사용),
지우개(`EraseStampMaterial`, `PaintStroke` 이벤트 `colorIndex=-1` 예약), 60초 만료 시
"등록 슬롯 0개 → 전신 랜덤 강제 도포"는 v1 §3.3~§3.6 로직 그대로 유효하다(등록 판정 기준만
§3.2로 바뀜, 이후 로직은 무관).

---

## 4. 그랩 / 캐리 (쿠키 ↔ 쿠키, 유지) — 실제 확보 애니메이션 반영

포획 시스템은 폐기됐지만(§6.2), **쿠키끼리 서로 들고 나르는 그랩/캐리 자체는 유지**된다(그랩과
포획을 분리한 것이 v1의 설계였고, 이번에 없어진 것은 "포획" 쪽이다). 애니메이션 검증 결과는
분량이 많아 §11로 분리했다 — 결론만 요약하면:

- **`Cookie_Carrying.fbx`는 상반신 전용 Avatar Mask 레이어로 `Cookie_Walking.fbx`와 조합
  가능**(§11.2, 두 클립 모두 프로젝트 공통 Humanoid 아바타를 쓰므로 리타게팅 문제 없음).
- **`Cookie_Hanging_Idle.fbx`는 별도 검증 보류 상태 그대로 유지**(§11.3, 사용자가 "보류"라고
  명시) — 잡힌 쪽 포즈로 바로 쓸 수 있는 후보지만 통합 시점은 추후 결정.
- **그랩 시작 시 흐느적거리는 도입 모션은 크로스페이드가 아니라 기존 `ReplayJump()`와 동일한
  `Animator.Play()` 하드컷 패턴을 권장**(§11.4) — 이 프로젝트가 이미 `Bug-fix-plan.md §18`에서
  같은 문제(트랜지션 블렌딩이 어색한 중간 포즈를 만듦)를 겪고 해결한 전례가 있다.

넷코드 설계(소유권 이전 없이 `carrySocket` 로컬 추적)는 v1 §4.1 그대로다.

```csharp
// PlayerAnimationDriver.cs에 추가할 Carry 전용 레이어 제어(§11.2 Avatar Mask와 짝을 이룸)
public void SetCarryLayerWeight(float weight)
{
    if (animator == null) return;
    animator.SetLayerWeight(CarryLayerIndex, weight); // 0=Carrying 비활성, 1=완전 적용
}
```

```csharp
// HideOrSeekPlayer.cs — OnGrabbedByOwner/OnReleased에 한 줄씩 추가
[PunRPC]
private void OnGrabbedByOwner(int grabberViewId)
{
    if (!pv.IsMine) return;
    // ...v1과 동일...
    animationDriver.SetCarryLayerWeight(1f); // Base Layer(Walk/Run/Idle)는 그대로 두고 상반신만 Carry로 덮음
}

[PunRPC]
private void OnReleased(bool withThrow, Vector3 throwVelocity)
{
    if (!pv.IsMine) return;
    // ...v1과 동일...
    animationDriver.SetCarryLayerWeight(0f);
}
```

---

## 5. 던지기 (사물 / 플레이어) — AI 없음, 단순 3D 사운드로 단순화

사용자 확인: "던진 사물은 마녀에게 소리를 어그로 끄는 용도. 마녀 AI는 없다. 마녀는 실제로
게임하는 플레이어다." → v1 §5는 이미 "물리 정밀 동기화 대신 착지 지점만 이벤트로 전파"하는
경량 설계였는데, AI 반응 로직이 아예 필요 없다는 게 확정되면서 **더 단순해진다**: `NoisePing`
이벤트는 게임 로직에서 아무것도 트리거하지 않고, **순수하게 3D 위치 기반 사운드 이펙트 재생**만
하면 된다 — 마녀를 조종하는 실제 사람이 헤드폰/스피커로 방향과 거리를 직접 듣고 판단한다.

```csharp
// Assets/02. Scripts/Grab/ThrowableProp.cs OnCollisionEnter 부분만 교체
private void OnCollisionEnter(Collision collision)
{
    if (!pv.IsMine) return;
    PhotonNetwork.RaiseEvent(NetEventCodes.NoisePing, transform.position,
        new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
    PhotonNetwork.Destroy(gameObject);
}
```

```csharp
// Assets/02. Scripts/Grab/NoiseListener.cs — 모든 클라이언트에 1개 배치(마스터 조건 없음, 순수 로컬 사운드)
public class NoiseListener : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private AudioSource noiseSfxPrefab; // 3D Spatial Blend=1, PlayOneShot 후 자동 파괴

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.NoisePing) return;
        Vector3 pos = (Vector3)photonEvent.CustomData;
        Instantiate(noiseSfxPrefab, pos, Quaternion.identity); // AudioSource.playOnAwake로 재생 후 클립 길이만큼 뒤 자동 삭제(Destroy(gameObject, clip.length))
    }
}
```

이 설계는 마녀에게만 특별 취급이 필요 없다 — **모든 클라이언트가 3D 위치에서 소리를 재생**하고,
Unity의 `AudioSource` 공간 감쇠(distance attenuation)가 알아서 "마녀에게 가까우면 크게, 멀면
작게" 들리게 해준다. 굳이 "마녀만 듣는다"는 필터링을 코드로 만들 필요가 없다(오히려 다른
쿠키들도 소리를 듣고 상황을 파악하는 편이 파티게임으로서 자연스럽다).

---

## 6. 술래잡기 본게임

### 6.1 쿠키 시야 축소 — 안개(v1과 동일, 생략)

### 6.2 마녀 공격 — 원거리 마법, 즉사 (완전 재설계)

`Assets/Screenshots/Magic.png` 확인 결과: **마녀는 1인칭 시점**(화면 우하단에 지팡이/손이
직접 보임)이고, 지팡이 끝에서 보라색 구체가 발사돼 번개 같은 시각 이펙트로 대상 쿠키에게
연결된다. 화면 좌하단 하트 아이콘+"100" 수치와 하단 중앙 원형 게이지는 화면 구성상 자원/쿨다운
UI로 보이지만, **이 수치 체계 자체는 추후 다른 방향으로 별도 설계할 예정이라 이번 문서에서는
다루지 않는다**(사용자 확인) — 아래 코드는 자원 소모 없이 "쿨다운만 있는" 최소 형태로 남겨두고,
실제 자원 시스템은 나중에 이 자리에 끼워 넣으면 되도록 `CastMagic()` 호출 지점만 마련해둔다.

> 📌 **설계 가정**: 마녀는 3인칭(`Camera_Ctrl`)이 아니라 **완전히 별도의 1인칭 카메라**를
> 쓴다고 해석했다. 기존 `Camera_Ctrl`을 재사용할 수 없으므로 `WitchPlayer` 프리팹은 자체
> `WitchFirstPersonCamera` 컴포넌트를 갖는다 — 마우스로 좌우/상하 시점 회전(FPS 표준), 이동은
> `HideOrSeekPlayer`의 기존 WASD 이동 로직을 그대로 재사용 가능(카메라 추적 방식만 다를 뿐
> `CheckMovementInput()`의 입력 자체 처리 로직은 1인칭이든 3인칭이든 동일).
>
> 마법 발사가 "번개처럼 대상에게 연결"되는 연출은 물리적으로 날아가는 투사체(Rigidbody)가
> 아니라 **히트스캔(레이캐스트 즉발) + 시각적 체인 라이트닝 이펙트**로 구현하는 편이 실제
> 게임 반응성(느린 투사체는 회피 가능해 "즉사"라는 표현과 안 맞음)과도 맞고, 넷코드도 훨씬
> 단순해진다(투사체 물리 동기화 불필요, §5와 같은 "발사자가 계산 끝낸 결과만 전파" 철학).

```csharp
// Assets/02. Scripts/Witch/WitchMagicAttack.cs — WitchPlayer 프리팹 전용
public class WitchMagicAttack : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Camera fpsCamera;
    [SerializeField] private Transform wandTip; // VFX 발사 원점(1인칭 손 모델의 지팡이 끝)
    [SerializeField] private float maxRange = 20f;
    [SerializeField] private float castCooldown = 1.5f; // 자원 시스템 확정 전까지의 임시 값 — §12 열린 질문
    [SerializeField] private LayerMask cookieLayer;

    private float cooldownRemaining;

    private void Update()
    {
        if (!pv.IsMine) return;
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.F) && cooldownRemaining <= 0f)
            CastMagic();
    }

    // 마나/자원 소모 로직은 추후 다른 방향으로 설계 예정(사용자 확인) — 이 호출 지점에
    // 자원 차감/검사를 끼워 넣으면 되도록 CastMagic() 자체는 쿨다운 재설정만 하고 종료한다.
    private void CastMagic()
    {
        cooldownRemaining = castCooldown;

        Ray ray = fpsCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)); // 화면 정중앙 히트스캔
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, maxRange, cookieLayer);
        Vector3 impactPoint = hit ? hitInfo.point : ray.origin + ray.direction * maxRange;

        // 이펙트는 전원 로컬 재생(정밀 동기화 불필요, §5와 동일 철학) — 명중 여부와 무관하게 항상 재생
        PhotonNetwork.RaiseEvent(NetEventCodes.MagicCast,
            new object[] { wandTip.position, impactPoint },
            new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);

        if (!hit) return;
        var cookie = hitInfo.collider.GetComponentInParent<HideOrSeekPlayer>();
        if (cookie == null) return;

        cookie.GetComponent<PhotonView>().RPC("RequestInstantKill", RpcTarget.All);
    }
}
```

```csharp
// HideOrSeekPlayer.cs — 포획(RequestCapture) 대신 즉사
public bool IsDead { get; private set; }

[PunRPC]
private void RequestInstantKill()
{
    if (!pv.IsMine || IsDead) return; // 본인 클라이언트만 자기 상태 확정(§6.2/§2 전반의 소유권 원칙)
    IsDead = true;
    IsMovementLocked = true;
    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.IsDead, true } });
    animationDriver.ChangeState(PlayerMoveState.Dead); // 신규 상태(§9)
}
```

### 6.3 사망한 쿠키 — Space로 관전 시점 순환 (트리거만 `IsCaught`→`IsDead`로 변경)

v1 §6.3의 `SpectatorController` 구조 그대로, `IsCaught` 참조를 전부 `IsDead`로 바꾸면 된다.
즉사이므로 "구출" 개념이 없고, 죽은 쿠키는 게임이 끝날 때까지 관전만 한다.

### 6.4 마녀 GameScene 합류 → `GameEndTime` 세팅 (v1과 동일, 생략)

---

## 7. 마녀(술래) 이탈 & 방장 위임 처리 (신규)

### 7.1 마녀 이탈 감지 — 다중 마녀 대응

`RoomLifecycleWatcher`의 기존 `IsTagger(player)`(단일 액터 비교)를 다중 마녀 배열 비교로
재작성하고, "즉시 종료"가 아니라 **"남은 마녀가 0명일 때만" 5초 경고 후 종료**로 바꾼다.

```csharp
// Assets/02. Scripts/Witch/RoomLifecycleWatcher.cs (재작성)
public class RoomLifecycleWatcher : MonoBehaviourPunCallbacks
{
    private double? witchesGoneAt; // null=정상, 값 있음=5초 경고 카운트다운 중

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (witchesGoneAt.HasValue) return; // 이미 종료 처리 중
        if (!PhotonNetwork.IsMasterClient) return; // 종료 판단은 마스터만(§8과 동일 권위 패턴)
        if (!IsWitch(otherPlayer)) return; // 쿠키가 나간 건 이 경로와 무관(쿠키는 죽으면 관전만 함, 나가도 게임 계속)

        RemoveFromWitchList(otherPlayer.ActorNumber);

        if (RemainingWitchCount() > 0) return; // 술래 2명 중 1명만 나감 → 그대로 진행(사용자 확인)

        witchesGoneAt = PhotonNetwork.Time;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.WitchDepartedAt, witchesGoneAt.Value }, // 전원에게 "술래가 나갔습니다" 배너 트리거
        });
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!witchesGoneAt.HasValue) return;
        if (PhotonNetwork.Time < witchesGoneAt.Value + 5.0) return;

        witchesGoneAt = null;
        ReturnToGameLobby(); // v1 §2.3 각주에서 이미 다룬 "AutomaticallySyncScene 예외" 동일 적용
    }

    private bool IsWitch(Player p) =>
        RoomState.TryGetIntArray(NetKeys.WitchActorNumbers, out int[] witches) && witches.Contains(p.ActorNumber);

    private int RemainingWitchCount() =>
        RoomState.TryGetIntArray(NetKeys.WitchActorNumbers, out int[] witches) ? witches.Length : 0;

    private void RemoveFromWitchList(int actorNumber)
    {
        if (!RoomState.TryGetIntArray(NetKeys.WitchActorNumbers, out int[] witches)) return;
        int[] updated = witches.Where(a => a != actorNumber).ToArray();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.WitchActorNumbers, updated } });
    }
}
```

```csharp
// Assets/02. Scripts/ColorTag/RoomState.cs에 추가 — int[] 조회 헬퍼(§8.3 승리 판정에서도 재사용)
public static bool TryGetIntArray(string key, out int[] value)
{
    value = null;
    if (!IsInRoom()) return false;
    if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object raw)) return false;
    value = (int[])raw;
    return true;
}
```

**전원에게 표시할 배너**는 `WitchDepartedAt` Room 프로퍼티 변경을 각자 로컬에서 감지해 표시하면
된다(연출이므로 정밀 동기화 불필요, `PlayerColorVoteIndicator`가 `OnRoomPropertiesUpdate`만으로
로컬 표현을 갱신하던 것과 같은 패턴):

```csharp
// Assets/02. Scripts/Witch/WitchDepartureBanner.cs — GameScene에 1개, 전원 대상
public class WitchDepartureBanner : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private TMP_Text bannerText;

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.WitchDepartedAt)) return;
        bannerText.text = "술래가 나갔습니다. 5초 뒤 게임이 종료됩니다.";
        bannerRoot.SetActive(true);
    }
}
```

### 7.2 방장(MasterClient) 위임 — 사실상 이미 해결되어 있음

> 📌 **중요 확인**: "술래가 방장이었으면 2번째 입장자에게, 그 사람도 나가면 3번째 입장자에게"는
> **PUN2가 기본으로 이미 이렇게 동작한다.** Photon Room의 `MasterClientId`는 현재 마스터가
> 나가면 **남아있는 플레이어 중 가장 낮은 `ActorNumber`**로 자동 재할당되고, `ActorNumber`는
> 한 방(Room) 안에서 입장 순서대로 단조 증가하며 절대 재사용되지 않는다 — 즉 "가장 낮은 남은
> ActorNumber"는 정확히 "가장 먼저 입장했고 아직 남아있는 사람"과 같다. 이 프로젝트는 이미
> `GameLobbyController.OnMasterClientSwitched()`로 이 전환을 반영하고 있고, 새로 만든
> §2.1(`WitchAssignmentAuthority`)/§3.6(`PaintPhaseController`)/§6.2/§7.1/§8.3 컴포넌트도 전부
> "마스터인지 매 `Update()`마다 확인"하는 폴링 패턴이므로, **방장이 바뀌는 순간 자동으로 새
> 방장의 클라이언트가 다음 프레임부터 해당 로직을 이어받는다** — 별도 코드가 필요 없다.
> (`research.md` §5.7에서 지적했듯 이 프로젝트에 커스텀 Script Execution Order가 없고 모든
> 마스터 권위 로직이 "매 프레임 `IsMasterClient` 확인"으로 짜여 있는 것이 여기서 그대로 이득이
> 된다.)

**주의할 것은 단 하나**: 마스터 전환 도중 "쿠키 전원 사망" 같은 판정이 **한 프레임도 누락되지
않는가**다 — 이전 마스터가 나가는 프레임과 새 마스터가 `IsMasterClient=true`가 되는 프레임
사이에 갭이 있을 수 있으므로, §8.3의 `GameRuleController.Update()`가 매 프레임 폴링이라는
점(이벤트 1회성이 아님)이 이 문제를 자연스럽게 방지한다 — 새 마스터가 되는 즉시 그 다음
`Update()`에서 바로 이어서 판정하므로 놓치는 프레임이 있어도 다음 프레임에 회복된다.

### 7.3 다중 마녀 확장 설계 요약

| 항목 | 단일 마녀(현재 규칙) | 다중 마녀(확장 시) |
|---|---|---|
| `NetKeys.WitchActorNumbers` | `int[1]` | `int[N]` — 코드 변경 없이 길이만 늘어남 |
| §2.2 리빌 연출 | 1명만 프리팹 교체 | `Contains()` 검사라 여러 명이 동시에 교체돼도 동작 동일 |
| §6.2 마법 공격 | 각자 독립적으로 `WitchMagicAttack` 보유 | 마녀 수만큼 인스턴스가 각자 독립 동작(공유 상태 없음) |
| §7.1 이탈 처리 | 1명 나가면 즉시 0명 → 5초 경고 | N명 중 일부만 나가면 진행, **0명이 될 때만** 5초 경고(이미 구현됨) |
| §8.3 승리 판정 | "쿠키 전원 사망" 단순 검사 | 변경 없음 — 마녀 수와 무관하게 쿠키 쪽만 검사하면 됨 |

---

## 8. 승리 조건 + 결과 화면

### 8.1 판정 로직 (v1 §7과 거의 동일, `IsCaught`→`IsDead`, 다중 마녀 대응)

```csharp
// Assets/02. Scripts/Witch/GameRuleController.cs
public class GameRuleController : MonoBehaviourPunCallbacks
{
    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!RoomState.TryGetIntArray(NetKeys.WitchActorNumbers, out int[] witches)) return;
        if (!RoomState.TryGetInt(NetKeys.WitchJoined, out _)) return; // 본게임 시작 전이면 판정 안 함
        if (RoomState.TryGetInt(NetKeys.GameResult, out _)) return;   // 이미 판정됨

        if (AllCookiesDead(witches))
        {
            Finish(GameResult.WitchWins);
            return;
        }

        if (RoomState.TryGetDouble(NetKeys.GameEndTime, out double endTime) && PhotonNetwork.Time >= endTime)
        {
            Finish(GameResult.CookiesWin);
        }
    }

    private bool AllCookiesDead(int[] witches)
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (witches.Contains(p.ActorNumber)) continue;
            bool dead = p.CustomProperties.TryGetValue(NetKeys.IsDead, out object v) && (bool)v;
            if (!dead) return false;
        }
        return true;
    }

    private void Finish(GameResult result)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.GameResult, (int)result } });
    }
}

public enum GameResult { CookiesWin, WitchWins }
```

### 8.2 결과 화면 UI (신규 — `Assets/Screenshots/Result.png` 반영)

레퍼런스 이미지 구성 분석:
- **상단 배너**: "마녀 승!"(보라) / "쿠키 승!"(금색) 텍스트 + 승리 사유 부제("쿠키들을 모두
  잡아버렸어요!" / "모두 무사히 숨었어요!")
- **좌측 일러스트**: 승리 진영에 맞춰 마녀 단독 일러스트 또는 쿠키 4인 축하 일러스트(정적
  아트, 플레이어별 커스터마이징 아님)
- **중앙 패널 "남은 쿠키 수"**: `생존수/4` 분수 + 쿠키 아이콘 4개를 가로 나열, 죽은 쿠키는
  회색 실루엣, 생존 쿠키는 컬러
- **우측 패널**: 플레이어 목록 — 마녀 행은 왕관 아이콘+"(마녀)" 라벨, 쿠키 행은 "✕ 잡힘"(빨강)
  또는 "○ 생존"(파랑/흰색)
- **하단**: "로비로 이동 (12)" 버튼 — 자동 복귀 카운트다운 숫자, 클릭 시 즉시 이동 가능

```csharp
// Assets/02. Scripts/Witch/ResultScreenController.cs — GameScene에 1개, 전원 대상
public class ResultScreenController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject witchWinBanner;   // "마녀 승!" 보라 배너 + 마녀 일러스트
    [SerializeField] private GameObject cookieWinBanner;  // "쿠키 승!" 금색 배너 + 쿠키 일러스트
    [SerializeField] private TMP_Text remainingCountText; // "N / 4"
    [SerializeField] private Image[] cookieIcons;         // 4개, 생존=컬러/사망=그레이스케일
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerResultRow playerRowPrefab; // 이름 + 상태(마녀/잡힘/생존)
    [SerializeField] private TMP_Text lobbyButtonCountdownText;
    [SerializeField] private float autoReturnDelay = 12f; // "로비로 이동 (12)"

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.GameResult)) return;
        if (!RoomState.TryGetInt(NetKeys.GameResult, out int result)) return;

        ShowResult((GameResult)result);
    }

    private void ShowResult(GameResult result)
    {
        root.SetActive(true);
        witchWinBanner.SetActive(result == GameResult.WitchWins);
        cookieWinBanner.SetActive(result == GameResult.CookiesWin);

        RoomState.TryGetIntArray(NetKeys.WitchActorNumbers, out int[] witches);
        int aliveCount = 0;
        int cookieIndex = 0;

        foreach (Player p in PhotonNetwork.PlayerList.OrderBy(pl => pl.ActorNumber))
        {
            var row = Instantiate(playerRowPrefab, playerListContent);
            bool isWitch = witches != null && witches.Contains(p.ActorNumber);
            bool isDead = p.CustomProperties.TryGetValue(NetKeys.IsDead, out object v) && (bool)v;

            if (isWitch) { row.SetWitch(p.NickName); continue; }

            row.SetCookie(p.NickName, alive: !isDead);
            if (cookieIndex < cookieIcons.Length) cookieIcons[cookieIndex].color = isDead ? Color.gray : Color.white;
            cookieIndex++;
            if (!isDead) aliveCount++;
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

    // 버튼 클릭으로도 즉시 호출 가능(카운트다운 끝까지 안 기다려도 됨)
    public void OnLobbyButtonClicked()
    {
        StopAllCoroutines();
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel(SceneNames.GameLobby); // 이번엔 마녀도 쿠키도 전원 함께 이동(§7과 달리 정상 종료)
    }
}
```

> 📌 정상 승리 종료(§8)는 §7(마녀 전원 이탈로 인한 비정상 종료)과 달리 **마녀와 쿠키 모두
> 함께 `GameLobbyScene`으로 복귀**해도 무방하다 — §7의 "마녀만 예외적으로 다른 씬에 남는"
> 특수 처리(`AutomaticallySyncScene` 주의사항, v1 §2.3 각주)가 여기서는 필요 없다. 따라서
> `PhotonNetwork.LoadLevel()`(방 전체 동기화)을 그대로 써도 안전하다.

---

## 9. `PlayerMoveState` / NetKeys / NetEventCodes 최신 목록

```csharp
public enum PlayerMoveState
{
    Idle, Walk, Run, Jump, Dodge,
    Held, // 그랩당한 상태(§4, 유지)
    Dead, // 마법에 맞아 사망(§6.2, v1의 Caught를 대체)
}
```

```csharp
public static class NetKeys
{
    public const string GameEndTime = "GameEndTime"; // 기존 값 실사용(§6.4)

    // 마녀 선정(§2)
    public const string WitchActorNumbers = "WitchActorNumbers"; // int[] — v1의 단일 WitchActorNumber를 대체
    public const string WitchRevealTime = "WitchRevealTime";
    public const string CookiesDeparted = "CookiesDeparted";

    // 페인트 페이즈(§3)
    public const string PaintPhaseEndTime = "PaintPhaseEndTime";
    public const string WitchJoined = "WitchJoined";

    // 이탈/방장 위임(§7)
    public const string WitchDepartedAt = "WitchDepartedAt";

    // 승패(§8)
    public const string GameResult = "GameResult";

    // Player CustomProperties
    public const string IsDead = "IsDead"; // v1의 IsCaught를 대체
}

public static class NetEventCodes
{
    public const byte PaintStroke = 1;
    public const byte ClaimWitch = 2;
    public const byte ClearColor = 3;
    public const byte FillAll = 4;
    public const byte NoisePing = 5;
    public const byte MagicCast = 6; // 신규(§6.2) — 마법 이펙트 전원 재생용, 명중 판정과는 별개
}
```

---

## 10. 필요한 프리팹 · 에셋 전체 목록 (갱신 — 프리팹 외 전 카테고리로 확장)

이 절은 문서 전체(§1~§9, §11)에 흩어져 있는 "신규로 만들어야 하는 것"을 프리팹뿐 아니라
3D 모델·애니메이션·셰이더/머티리얼·파티클/VFX·오디오·UI 아트·인프라 설정(레이어)까지
전부 한곳에 모은 것이다. 표마다 **기존재(프로젝트에 이미 있음) / 확보(에셋은 있지만 아직
프로젝트에 완전히 반영 전) / 신규(전혀 없음)** 로 상태를 표시했다 — 전부 실제로
`find`/`grep`으로 프로젝트를 직접 뒤져서 확인한 결과다(추측 아님).

### 10.1 신규/변경 스크립트·컴포넌트 전체 목록

| 파일 경로 | 역할 | 근거 절 |
|---|---|---|
| `Witch/Cauldron.cs` | 가마솥 트리거, `ClaimWitch` 요청 발신 | §2.1 |
| `Witch/WitchAssignmentAuthority.cs` | 마스터 전용 마녀 확정(선착순+타임아웃) | §2.1 |
| `Witch/WitchRevealController.cs` | 리빌 연출 + 쿠키→마녀 프리팹 교체 | §2.2 |
| `Witch/CookieDepartureController.cs` | 쿠키 전용, 10초 후 GameScene 이동 | §2.3 |
| `Witch/WitchJoinController.cs` | 60초 후 마녀 GameScene 합류 트리거, `GameEndTime` 세팅 | §6.4 |
| `ColorTag/PlayerPaintCanvas.cs`(수정) | 슬롯/임계량 등록, 지우개, 전신 강제 도포 | §3 |
| `ColorTag/ColorSwatchButton.cs`(수정) | `SetBrushColor()` 호출로 교체 | §3.3 |
| `ColorTag/RoomState.cs`(수정) | `TryGetIntArray()` 추가 | §7.1 |
| `Grab/PlayerGrabController.cs` | 그랩 시작/해제(그랩버 측) | §4.1 |
| `Unit/PlayerAnimationDriver.cs`(수정) | `SetCarryLayerWeight()` 추가 | §4, §11.2 |
| `Unit/HideOrSeekPlayer.cs`(수정) | `OnGrabbedByOwner`/`OnReleased`/`IsDead`/`RequestInstantKill` | §4.1, §6.2 |
| `Grab/ThrowableProp.cs` | 사물 던지기, 착지 시 `NoisePing` | §5 |
| `Grab/NoiseListener.cs` | 전원 배치, 3D 소음 SFX 재생 | §5 |
| `Witch/WitchFirstPersonCamera.cs` | 마녀 전용 1인칭 카메라 | §6.2 |
| `Witch/WitchMagicAttack.cs` | 히트스캔 마법 캐스팅 | §6.2 |
| `Witch/SpectatorController.cs`(v1 `Caught`→`Dead`로 갱신) | 사망 쿠키 시점 순환 | §6.3 |
| `Witch/RoomLifecycleWatcher.cs`(재작성, 기존 파일 대체) | 다중 마녀 이탈 감지+5초 경고 | §7.1 |
| `Witch/WitchDepartureBanner.cs` | 이탈 경고 배너 로컬 표시 | §7.1 |
| `Witch/GameRuleController.cs` | 승리 판정(마스터 전용) | §8.1 |
| `Witch/ResultScreenController.cs` | 결과 화면 표시/자동 복귀 | §8.2 |
| `ColorTag/ColorSelectionManager.cs`, `ColorVoteTally.cs`, `TaggerColorAssigner.cs`, `PlayerColorVoteIndicator.cs`, `PlayerColorDisplay.cs` | **삭제 대상** | §0 |

### 10.2 프리팹 목록

**A. 네트워크 프리팹**(`PhotonNetwork.Instantiate` 대상 — 반드시 `Assets/04. Prefabs/Resources/`)

| 프리팹 | 상태 | 부착 컴포넌트 | 근거 |
|---|---|---|---|
| `HideOrSeekPlayer.prefab` | 기존재(수정 필요) | `PlayerGrabController`, Animator에 `Carry` 레이어 추가 | §4, §6.2 |
| `WitchPlayer.prefab` | **신규** | `WitchFirstPersonCamera`, `WitchMagicAttack`, `CookieDepartureController` 없음(마녀 전용), `PhotonView`+`PhotonTransformView` | §2.2, §6.2 |
| 던질 수 있는 소품(예: `ThrowableCan.prefab` 등, 최소 1종) | **신규** | `ThrowableProp`, `Rigidbody`, `PhotonView` | §5 |
| `BrushCursor.prefab` | 기존재, 변경 없음 | — | (ColorTag 기존) |

**B. 로컬 전용 프리팹**(각 클라이언트가 로컬 `Instantiate()` — 네트워크 오브젝트 아님, `Resources/` 강제 아님)

| 프리팹 | 상태 | 용도 | 근거 |
|---|---|---|---|
| 노이즈 SFX 프리팹(`NoiseListener.noiseSfxPrefab`) | **신규** | 3D `AudioSource` 1개, 재생 후 자동 파괴 | §5 |
| 마법 캐스팅 VFX 프리팹 | **신규** | 보라 구체+체인 라이트닝, `MagicCast` 이벤트 수신 시 재생 | §6.2 |
| 가마솥 보글보글 파티클(`bubbleFx`) | **신규** | 마녀 미확정 대기 중 연출 | §2.2 |
| 가마솥 짜잔 리빌 파티클(`revealFx`) | **신규** | 마녀 확정 순간 연출 | §2.2 |

**C. 씬 배치 오브젝트**(프리팹화 권장 — 여러 씬/여러 개체에 재사용되므로)

| 프리팹 | 상태 | 용도 | 근거 |
|---|---|---|---|
| `Cauldron.prefab` | **신규** | 가마솥 3D 모델+트리거 콜라이더+`Cauldron`+`WitchAssignmentAuthority`, `GameLobbyScene`에 1개 | §2.1 |
| 문(door) 프리팹 | **신규**(선택) | 동일 모양 4개 배치이므로 프리팹화 권장, 게임 로직 컴포넌트 없는 순수 장식 | §1 |

**D. UI 프리팹**(`Resources/UI/{Popup|Scene|Tab}/{클래스명}` 컨벤션, `CLAUDE.md` 폴더 규칙 준수)

| 프리팹 | 상태 | 용도 | 근거 |
|---|---|---|---|
| `Resources/UI/Scene/ColorSlotPanel/ColorSlotPanel.prefab` | **신규**(기존 `ColorSelectionPanel.prefab`을 대체) | 4슬롯+Reset+지우개 UI | §3.4 |
| `Resources/UI/Scene/ResultScreen/ResultScreen.prefab` | **신규** | 승패 배너+남은 쿠키 패널+플레이어 목록 루트 | §8.2 |
| `Resources/UI/Scene/ResultScreen/PlayerResultRow.prefab` | **신규** | 결과 화면 플레이어 목록 행(이름+상태) | §8.2 |
| `Resources/UI/Popup/WitchDepartureBanner/WitchDepartureBanner.prefab` | **신규** | "술래가 나갔습니다" 경고 배너 | §7.1 |
| `ConfirmDialog`/`GameLobbyPanel`/`LobbyPanel`/`PlayerListItem`/`RoomListItem` | 기존재, 변경 없음 | — | (기존) |
| `ColorSelectionPanel.prefab`(기존) | **폐기 대상** | `ColorSlotPanel`로 대체됨 | §0, §3 |

### 10.3 3D 모델 / 메시

| 항목 | 상태 | 근거 |
|---|---|---|
| 마녀 캐릭터 메시(스킨/의상) | **신규** — `Cookie_StandUp.fbx` 리그 재사용 가능성 있으나(§11.1) 시각 메시 자체는 별도 제작 필요 | §2.2, §11.1 |
| 가마솥 3D 모델 | **신규** — 제작 또는 에셋스토어 구매(원 요청 "3D 오브젝트 만들거나 찾을 예정") | §2.1, 원 요청 |
| 마녀 지팡이 + 1인칭 손 모델 | **신규**, `Magic.png` 참고 | §6.2 |
| 던질 수 있는 소품 모델(들) | **신규**, 최소 1종 | §5 |
| 문(4개) 모델 | **신규**(선택, `GameLobbyScene.png` 참고) | §1 |

### 10.4 애니메이션 (FBX/컨트롤러) — 확보 현황 종합

| 항목 | 상태 | 비고 |
|---|---|---|
| `Cookie_Carrying.fbx` | **확보** — Humanoid+공용 아바타 전환 필요(Import 설정 미반영) | §11.2 |
| `Cookie_Hanging_Idle.fbx` | **확보** — 통합 보류(사용자 지정) | §11.3 |
| `Cookie_StandUp.fbx`(마녀 리빌용) | **확보, Unity 임포트까지 됨** — 아직 Generic, Humanoid+공용 아바타 전환 필요 | §11.1 |
| `PlayerAnimator.controller`(기존) 수정 — `Carry` 레이어(Avatar Mask) + `Held`/`Dead` 트리거 추가 | **수정 필요** | §4, §9, §11.2 |
| 마녀 전용 Animator Controller — Idle/Walk(이동) + `Cast`(마법 캐스팅) 트리거 | **신규** — 이동 애니메이션 세트 자체가 문서에 아직 없어 §12에 열린 질문으로 추가 필요 | §6.2 |
| 마녀 1인칭 지팡이 캐스팅 모션 | **신규** | §6.2 |

### 10.5 셰이더 / 머티리얼

| 항목 | 상태 | 근거 |
|---|---|---|
| `EraseStampMaterial.mat` | **신규** — 기존 `brushStampMaterial` 구조 복제 후 출력 알파 고정 0 | §3.5 |
| `FillAllMaterial.mat` | **신규** — UV 전체를 단색으로 덮는 전용 블릿 | §3.6 |
| `ColorReplaceMaterial.mat`(기존) | 기존재, 그대로 재사용(Reset 용도로 용도만 확장) | §3.4 |
| 마법 VFX용 머티리얼(파티클/트레일) | **신규** | §6.2 |

### 10.6 파티클 / VFX

| 항목 | 상태 | 근거 |
|---|---|---|
| 가마솥 보글보글(대기 연출) | **신규** | §2.2 |
| 가마솥 짜잔(리빌 연출) | **신규** | §2.2 |
| 마법 캐스팅(보라 구체+체인 라이트닝) | **신규**, `Magic.png` 참고 | §6.2 |
| 그랩/캐리 관련 VFX | 불필요(문서상 요구 없음) | — |

### 10.7 오디오

| 항목 | 상태 | 근거 |
|---|---|---|
| 던지기 착지 소음 SFX(3D Spatial) | **신규** | §5 |
| 마법 캐스팅 SFX | **신규**(문서에 명시적 요구는 없었지만 캐스팅 연출에 자연스럽게 필요 — §12에 열린 질문으로 추가) | §6.2 |
| 가마솥 보글보글 SFX(선택) | **신규**(선택) | §2.2 |

### 10.8 UI 아트(스프라이트/아이콘/일러스트)

| 항목 | 상태 | 근거 |
|---|---|---|
| 결과 화면 — 마녀 승/쿠키 승 배너 아트 2종 | **신규**, `Result.png` 참고 | §8.2 |
| 결과 화면 — 마녀 단독/쿠키 4인 축하 일러스트 2종 | **신규**, `Result.png` 참고 | §8.2 |
| 결과 화면 — 생존/사망 쿠키 아이콘, 왕관 아이콘 | **신규**, `Result.png` 참고 | §8.2 |
| 색 슬롯 UI — 4칸 배경, Reset/지우개 버튼 아이콘 | **신규** | §3.4 |
| 술래 이탈 경고 배너 배경/아이콘 | **신규** | §7.1 |

### 10.9 인프라 설정(레이어/태그) — 프로젝트 직접 확인 결과

`ProjectSettings/TagManager.asset`을 직접 확인한 결과, **현재 커스텀 레이어는 `PlayerCapsule`
(8번) 하나뿐**이고 `Cookie`/`Witch`/`Player`처럼 그랩·마법 판정에 쓸 전용 레이어가 없다.
`WitchMagicAttack.cookieLayer`(§6.2)와 `PlayerGrabController.playerLayer`(§4.1)가 참조하는
`LayerMask`는 이 레이어들이 실제로 만들어져야 동작한다 — **레이어 신설 자체가 선행 작업**이다.

| 레이어 이름(안) | 용도 | 근거 |
|---|---|---|
| `Cookie` | 쿠키 캐릭터 판별(마법 히트스캔 대상 필터) | §6.2 |
| `Witch` | 마녀 캐릭터 판별(그랩 대상에서 제외 등) | §4.1 |
| (기존 `PlayerCapsule`은 그대로 유지 — 붓칠 레이캐스트 제외용, 용도 다름) | — | (기존) |

---

## 11. 애니메이션 자산 실물 검증 (신규 — 사용자 요청 항목)

### 11.1 마녀용 Mixamo "Zombie Stand Up" 개조 애니메이션 — **`Cookie_StandUp.fbx`로 확보 확인**

처음 검색(`find Assets -iname "*zombie*"`)에서는 못 찾았는데, 파일명이 "Zombie"가 아니라
`Cookie_StandUp.fbx`(`Assets/Animation/Cookie/`)였다 — 사용자 확인 후 재검색해서 실제로
존재함을 확인했다. 다른 두 파일과 달리 **이 파일은 이미 한 번 Unity 에디터에 임포트되어
`.meta`가 생성돼 있어서**, 바이너리 문자열 추출이 아니라 **Unity가 실제로 기록한 임포트
설정을 그대로 읽을 수 있었다**:

- 클립 이름 `Cookie_StandUp`, `takeName: mixamo.com`(다른 Cookie 클립들과 동일한 Mixamo
  파이프라인), **`firstFrame: 0 ~ lastFrame: 93`(94프레임, `loop: 0`/`loopTime: 0`)** — 반복
  재생이 아닌 1회성 클립으로 이미 올바르게 표시돼 있다. §2.2의 "가마솥에서 일어서는" 1회성
  리빌 연출과 정확히 맞는 길이/루프 설정이다.
- **`animationType: 2`(Generic), `avatarSetup: 0`(Create From This Model), 
  `lastHumanDescriptionAvatarSource: {instanceID: 0}`(미지정)** — 즉 **아직 Humanoid로
  전환되지 않은, Unity가 처음 드래그해 넣었을 때의 기본값 그대로**다. 이는 `Cookie_Walking`
  등 나머지 클립들이 처음엔 전부 `animationType: 2`였다가(`Cookie_Idle`도 여전히 2로 남아있음,
  §11.2에서 확인한 사실과 동일) 나중에 수동으로 Humanoid + 공용 아바타로 바꾼 것과 같은
  **아직 안 거친 단계**라는 뜻 — Carrying/Hanging과 사실상 동일한 상태다.
- **파일 위치·이름 자체가 중요한 단서다**: `Witch/` 같은 별도 폴더가 아니라 다른 쿠키
  애니메이션들과 함께 `Assets/Animation/Cookie/`에 있고 이름도 `Cookie_StandUp`이다. 이는
  §6.2에서 세웠던 "마녀는 쿠키와 완전히 별개의 전용 휴머노이드 아바타를 쓴다"는 가정을
  **수정해야 함을 시사한다** — 오히려 **마녀도 쿠키와 동일한 공용 Humanoid 아바타(리그)를
  공유**하고, 그 위에 마녀 전용 스킨/모델만 얹는 구조일 가능성이 높다. 그렇다면 임포트 설정도
  나머지 Cookie 클립들과 완전히 동일하게: **Animation Type = Humanoid, Avatar Definition =
  Copy From Other Avatar → `Cookie_Idle`의 아바타**로 맞추면 된다(§11.2와 완전히 같은 절차).

> 📌 **가정 갱신**: §6.2의 "마녀는 쿠키와 별개의 아바타" 가정을 **"마녀도 쿠키 공용 아바타를
> 재사용한다"**로 바꿨다. 실제로 마녀 캐릭터 모델(메시)의 체형이 쿠키와 크게 다르다면(예:
> 팔다리 비율이 다른 마녀 전용 모델) 이 가정은 다시 깨질 수 있다 — 마녀 3D 모델이 확정되는
> 대로 재확인이 필요하다(§12에 남김). 다만 현재까지 확보된 증거(파일 위치/이름/기본 임포트
> 상태)만 보면 "공용 아바타 재사용"이 더 유력한 시나리오다.

### 11.2 `Cookie_Carrying.fbx` + `Cookie_Walking.fbx` 조합 가능 여부 — **가능(권장 방식 명시)**

실제 파일을 바이너리 문자열 추출로 직접 열어 확인했다(Unity 에디터로 아직 임포트되지 않은
상태라 `.meta`가 없어 커브 데이터까지는 못 봤지만, FBX 헤더/스켈레톤/테이크 구조는 확인 가능):

- `Cookie_Carrying.fbx`는 `Kaydara FBX Binary` 포맷, 테이크 이름 `mixamo.com` 1개 — 기존
  `Cookie_Walking.fbx`(`takeName: mixamo.com`, 애니메이션 1개)와 **완전히 동일한 Mixamo 내보내기
  구조**다.
- 스켈레톤에 `mixamorig:Hips/Spine/Spine1/Spine2/LeftArm/RightArm/LeftUpLeg/RightUpLeg/
  LeftFoot/RightFoot/...` 등 **표준 Mixamo 전체 골격이 그대로 들어있다** — 즉 다리 본 자체는
  존재한다(이 사실만으로 "다리가 실제로 애니메이션되는지"까지는 알 수 없다 — Mixamo는 클립의
  움직임 범위와 무관하게 항상 전체 골격을 내보내기 때문. 커브가 실제로 다리를 움직이는지는
  Unity Animation 미리보기 창에서 직접 재생해봐야 확정된다).
- 기존 `Cookie_Walking/Run/Jumping/Dodge.fbx`는 전부 `animationType: 3`(Humanoid) +
  `lastHumanDescriptionAvatarSource`가 **동일한 guid**(`23f57b7a...`, `Cookie/UV/Cookie_Idle.fbx`가
  원본 아바타)를 가리키도록 설정돼 있다 — 이게 이 프로젝트가 "여러 Mixamo 클립을 하나의 공용
  아바타로 리타게팅"하는 이미 검증된 워크플로우다.

**결론 및 권장 설정**: `Cookie_Carrying.fbx`를 임포트할 때 **Rig 탭에서 Animation Type을
Humanoid로, Avatar Definition을 "Copy From Other Avatar"로 지정해 `Cookie_Idle`의 아바타를
그대로 재사용**하면 나머지 클립들과 100% 동일한 리타겟 파이프라인을 탄다 — 이것만 맞추면
"조합 가능 여부" 자체는 리그 레벨에서 걸림돌이 없다.

**다리가 실제로 걷는 것처럼 움직이는지와 무관하게, 구현 방식은 아래를 권장한다**:
- Animator Controller에 **`Carry`라는 추가 레이어**를 만들고, **Avatar Mask로 상반신(Spine
  위쪽 + 양팔)만 포함**하도록 설정한다.
- `Carry` 레이어의 상태는 `Cookie_Carrying.fbx` 클립 1개(항상 재생, 레이어 weight로 on/off).
- Base Layer(`Idle/Walk/Run/Jump/Dodge`)는 지금처럼 그대로 유지 — 즉 **다리는 항상 Base
  Layer의 Walk/Run/Idle이 담당하고, `Cookie_Carrying.fbx`가 설령 다리를 같이 움직이더라도 Avatar
  Mask가 걸러내므로 무시된다.**
- 이 방식은 "Carrying 클립이 걷기까지 포함한 완성 사이클인지, 상반신만 있는 정적 포즈인지" 둘 중
  어느 쪽이어도 동일하게 안전하게 동작한다 — 확인이 안 된 상태에서도 구현을 진행할 수 있는
  이유가 여기 있다. §4의 `SetCarryLayerWeight()` 코드가 바로 이 구조를 전제로 짜여 있다.

### 11.3 `Cookie_Hanging_Idle.fbx`(매달린 자세) — 보류

사용자가 명시적으로 "보류"라 표시했으므로 이번 설계에서는 §4에 "통합 후보"로만 언급하고 실제
Animator 배선은 하지 않는다. 파일 자체는 존재 확인됨(`mixamorig:Hips/LeftHand/RightHand/
LeftUpLeg/RightUpLeg` 스켈레톤 포함, 역시 `mixamo.com` 단일 테이크) — 필요해지면 §4의 `Held`
상태(§9 enum) 클립으로 그대로 연결하면 된다(추가 설계 불필요, 이미 자리가 마련돼 있음).

### 11.4 "흐느적거리며 자연스럽게 잡는" 도입 모션 — `Cookie_Carrying.fbx`와 조합 가능

그랩을 시작하는 순간의 "흐느적거리는" 도입 동작을 `Cookie_Carrying.fbx`(지속 자세)와 이어붙이는
방법은 두 가지가 있고, **이 프로젝트는 이미 똑같은 문제를 한 번 겪고 해결한 전례가 있다**:

- **크로스페이드 트랜지션**(Animator의 기본 방식, 예: 0.15~0.2초 블렌드)으로 별도 "GrabStart"
  상태에서 "Carry" 상태로 전환 — 구현이 쉽지만, 이 프로젝트의 `PlayerAnimationDriver.
  ReplayJump()` 주석(`Bug-fix-plan.md §18`)에 정확히 기록돼 있듯 **"Any State" 계열 전환에서
  크로스페이드가 남아있으면 두 포즈가 섞인 어색한 중간 자세로 보이는 문제**가 이미 한 번
  발생했었다.
- **하드컷**(`Animator.Play("Carry", 0, 0f)`) — `ReplayJump()`가 쓰는 것과 동일한 기법. 그랩
  판정 성공 즉시 이 방식으로 전환하면 블렌딩 아티팩트 걱정 없이 "잡는 순간 즉시 자세 전환"이
  보장된다.

**권장**: 그랩 자체가 "성공 여부가 즉시 결정되는" 액션(§4.1의 `TryGrab()`이 그 프레임에 성공/실패
확정)이라 점프 재시작과 상황이 매우 유사하다 — **하드컷 방식을 권장**하며, "자연스럽게 흐느적"
거리는 느낌은 크로스페이드의 블렌딩이 아니라 **`Cookie_Carrying.fbx` 클립 자체의 시작 몇 프레임에
이미 그런 여유 동작이 들어있도록 하는 것**(애니메이션 제작 단계에서 처리)이 이 프로젝트의 기존
교훈과 더 잘 맞는다.

---

## 12. 열린 질문 (v1 대비 갱신)

**해결됨(제거)**: v1 §10-1(가마솥 선정 방식), v1 §10-2(슬롯 등록 범위), v1 §10-3(마녀만 다른
씬에 남는 흐름), v1 §10-4(그랩/포획 키 분리 여부 — 포획 자체가 없어짐), v1 §10-6(포획 판정
신뢰 수준 — 포획이 없어져 해당 없음), v1 §10-7(승패 UI — `Result.png`로 확정).

**해결됨(추가 제거)**: §6.2 마법 자원/쿨다운 수치 — 사용자가 "제외해도 됨, 추후 다른 방향으로
설계 예정"이라 확정. 이번 문서에서는 `castCooldown`만 임시 자리표시자로 남기고 자원 시스템
자체는 다루지 않는다(§6.2 코드 갱신 완료).

**남아있는 것 + 신규**:
1. §3.2 `MinStrokesToRegister`(임계 스탬프 수) 밸런스 값 — 실제 플레이테스트로 조정 필요.
2. §7.1 5초 경고 타이밍이 "이탈 감지 즉시 배너 표시"가 맞는지, 아니면 배너 표시 자체도
   지연이 있는지(원문의 이중 "5초" 표현이 다소 모호함) — 구현 시 재확인 권장.
3. §6.2 히트스캔 판정 범위(화면 정중앙 단일 레이캐스트 vs 약간의 관용 반경) — 조준 난이도
   결정 필요.
4. §5 던지기의 `noiseRadius`가 실제로 시각적 UI 힌트(예: 화면 가장자리 방향 표시)를 겸할지,
   순수 오디오만으로 충분할지 — "마녀 AI 없음"이 확정되며 우선순위가 낮아짐.
5. §2.1 `witchSelectTimeout`(가마솥 무입장 타임아웃) 구체적 초수 — 임시로 30초 가정.
6. §11.1에서 "마녀도 쿠키 공용 아바타를 재사용한다"로 가정을 바꿨는데, **실제 마녀 3D 모델
   (메시)의 체형이 쿠키와 크게 다르면 이 가정이 깨질 수 있다** — 마녀 모델 확보 후 재확인 필요.
7. §11.2 `Cookie_Carrying.fbx`가 실제로 다리를 움직이는지는 Unity Animation 미리보기로 직접
   확인 필요(§11.2 결론은 "확인 여부와 무관하게 안전한 구현 방식"을 제시한 것이지, 클립 내용
   자체를 확정한 것은 아님) — `Cookie_StandUp.fbx`도 마찬가지로 다리/전신 커브 유무는 Unity
   Animation 창에서 직접 재생해야 확정된다.
8. `Cookie_StandUp.fbx`(및 `Cookie_Carrying`/`Cookie_Hanging_Idle`)를 Humanoid+공용 아바타로
   전환하는 실제 Import Setting 작업 자체가 아직 남아있음(§11.1/§11.2 — 확인만 했지 아직
   반영은 안 한 상태).
9. §10.4에서 드러난 공백: **마녀의 이동(Idle/Walk) 애니메이션 세트 자체가 아직 문서/에셋
   어디에도 없다** — `Cookie_StandUp.fbx`(리빌 1회성)와 `Cast`(캐스팅)만으로는 마녀가 걸어
   다니는 모습을 만들 수 없다. 쿠키 리그를 공유한다면(§11.1 가정) `Cookie_Walking/Run.fbx`를
   그대로 재사용할 수 있을지, 아니면 마녀 전용 이동 클립이 별도로 필요한지 확인 필요.
10. §10.7에서 드러난 공백: 마법 캐스팅 SFX가 원 요구사항에 명시되지 않았음 — 필요 여부/톤
    확인 필요.
11. §10.9 신규 레이어(`Cookie`/`Witch`) 이름·번호 확정 및 `HideOrSeekPlayer.prefab`/
    `WitchPlayer.prefab`에 실제로 배정하는 작업 — 설계상 결정보다는 순수 작업 항목.

---

## 13. 구현 순서 제안 (갱신)

1. **§2(마녀 선정+타임아웃+연출) + §9 NetKeys/EventCodes 골격 + §7.2(방장 위임 확인)** —
   §7.2는 사실 Photon 기본 동작이라 "확인만" 하면 되므로 별도 구현 비용이 거의 없다.
2. **§3(자유 색칠, 임계 등록) 재작성** — v1과 동일하게 우선순위 2위.
3. **§6.1(안개) + §6.4(GameEndTime) + §6.2(마법 즉사) + §8(승리 판정+결과 화면)** — 포획
   시스템이 없어지면서 오히려 v1보다 **최소 플레이 루프 구현이 더 단순해졌다**(그랩 없이도
   "숨고 마법 피하기"만으로 게임이 성립).
4. **§7.1(마녀 이탈 처리)** — 3번이 끝난 뒤 안정성 보강 차원에서 진행.
5. **§4(그랩) + §5(던지기)** — 여전히 가장 나중, 다만 애니메이션 리소스는 이미 절반 이상
   확보돼 있어(§11) v1 시점보다 리스크가 줄었다.
6. 애니메이션/파티클/모델/UI 아트 자산은 병렬 진행(마녀 1인칭 손+지팡이+마법 VFX,
   결과 화면 UI가 이번 개정에서 새로 우선순위 높아진 아트 작업).

---

## 14. 사용자 제공 필요 항목 (핸드오프 체크리스트)

§10(에셋 전체 목록)과 §12(열린 질문)에 흩어진 항목 중 **"실제로 사용자가 만들거나/구해서
프로젝트에 넣어야 하는 것"**과 **"예/아니오 답변 한 줄이면 되는 것"**만 골라 별도로 정리한
것이다. 나머지(Unity 임포트 설정 전환, 레이어 신설, 스크립트 구현 등)는 §14.3에 명시했듯
사용자가 따로 준비할 필요 없이 진행 가능하다.

### 14.1 에셋 제공 필요 (우선순위순)

| 우선순위 | 항목 | 현재 상태 / 필요 이유 | 근거 |
|---|---|---|---|
| 🔴 최우선 | 마녀 3D 모델 | **미확보.** §11.1에서 "마녀도 쿠키 리그를 공유한다"고 가정했는데, 이 가정 자체가 마녀 모델 체형에 달려있어 모델이 나와야 §2.2/§6.2/§11.1 설계가 확정된다 | §10.3, §11.1, §12-6 |
| 🔴 최우선 | 마녀 이동(Idle/Walk) 애니메이션 세트 | **미확보, 이번 정리 과정에서 처음 발견된 공백.** `Cookie_StandUp.fbx`는 1회성 리빌 동작뿐이라 마녀가 걷는 모습을 만들 수 없다 | §10.4, §12-9 |
| 🟡 | 가마솥 3D 모델 | 미확보(원 요청에서 "만들거나 찾을 예정") | §10.3, §2.1 |
| 🟡 | 마녀 지팡이 + 1인칭 손 모델 | 미확보, `Magic.png` 수준 비주얼 필요 | §10.3, §6.2 |
| 🟡 | 던질 수 있는 소품 모델(최소 1종) | 미확보 | §10.3, §5 |
| 🟡 (선택) | 문 4개 모델 | 미확보, 동일 프리팹 4회 배치로 대체 가능 | §10.3, §1 |
| 🟢 | 가마솥 보글보글/짜잔 파티클 | 미확보 | §10.6, §2.2 |
| 🟢 | 마법 캐스팅 VFX(보라 구체+체인 라이트닝) | 미확보, `Magic.png` 참고 | §10.6, §6.2 |
| 🟢 | 던지기 착지 소음 SFX | 미확보 | §10.7, §5 |
| 🟢 | 마법 캐스팅 SFX | 미확보 — 원 요청에 없던 항목이라 필요 여부부터 확인(§14.2) | §10.7, §12-10 |
| 🟢 | 결과 화면 아트(배너 2종, 일러스트 2종, 생존/사망 아이콘, 왕관 아이콘) | 미확보 — `Result.png`를 그대로 잘라 쓸 수 있으면 가장 빠름 | §10.8, §8.2 |
| 🟢 | 색 슬롯 UI 아이콘(Reset/지우개 버튼) | 미확보 | §10.8, §3.4 |

### 14.2 답변만 필요한 것 (결정 사항)

| 항목 | 현재 임시값 | 근거 |
|---|---|---|
| 가마솥 무입장 타임아웃 | 30초(가정값) | §2.1, §12-5 |
| 마법 히트스캔 판정 범위 | 화면 정중앙 단일 레이캐스트로 임시 설계 — 관용 반경을 둘지 미정 | §6.2, §12-3 |
| 던진 사물 소리의 UI 힌트 여부 | 순수 오디오만으로 임시 설계 | §5, §12-4 |
| 마법 캐스팅 SFX 필요 여부 | 미정(원 요청에 없던 항목) | §12-10 |
| 색 슬롯 등록 임계값(`MinStrokesToRegister`) | 15스탬프(가정값) — 플레이테스트로 조정 가능해 지금 당장 답 안 주셔도 됨 | §3.2, §12-1 |

### 14.3 사용자가 안 줘도 되는 것

아래는 에셋이나 결정이 아니라 **구현 작업 자체**라 개발 쪽(Unity 에디터 조작 포함)에서 그대로
진행 가능하다 — 이번 목록에서 의도적으로 제외했다:
- `Cookie_Carrying`/`Cookie_Hanging_Idle`/`Cookie_StandUp.fbx`의 Humanoid+공용 아바타 Import
  설정 전환(§11.1, §11.2, §12-8)
- 다리 애니메이션 커브가 실제로 있는지 Unity Animation 창에서 직접 재생 확인(§11.2, §12-7)
- 신규 레이어(`Cookie`/`Witch`) 신설 및 프리팹 배정(§10.9, §12-11)
- §10.1에 정리된 스크립트/컴포넌트 구현 전체
