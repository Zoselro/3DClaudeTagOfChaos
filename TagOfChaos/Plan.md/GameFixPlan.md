# 게임 개선·버그 수정 계획서 (GameFixPlan.md)

- 작성일: 2026-09-30
- 상태: **✅ 전부 구현 완료** (2026-09-30). 결과는 맨 아래 "구현 결과"에 있다.
- 범위: 사용자 요청 1, 3~8번. 탈출 모드 관련 내용은 `EscapePlan.md`에 있다.
- 근거:
  - 지금 코드를 직접 읽었다: `MonsterGrabKillTrigger`, `MonsterController`, `CharacterInteractor`, `InteractionPromptUI`, `LobbyController`, `GameLobbyController`, `MonsterLobbyWaitController`, `Camera_Ctrl`, `Cauldron`, `PlayerInput`, `InputBindingsSO`
  - Unity에서 직접 측정했다: 괴물 크기와 잡기 범위, 가마솥 머티리얼, HauntedBakery 조명 분포
- 표시: ⬜ 대기 · 🔄 진행 중 · ✅ 완료

| # | 내용 | 상태 |
|---|---|---|
| F1 | 괴물 잡기: 조준 + E키, 문보다 우선 | ✅ |
| F3 | 로비 방 목록 새로고침 | ✅ |
| F4 | 상호작용 표시를 동그라미 E 아이콘으로 | ✅ |
| F5 | 가마솥 보라색 액체 빛나게 | ✅ |
| F6 | 괴물 1인칭 시점 (3인칭과 전환 가능) | ✅ |
| F7 | HauntedBakery 어두운 곳 밝히기 | ✅ |
| F8 | 대기실 괴물 화면에 나간 사람이 남는 버그 | ✅ |

작업 순서는 F8 → F3 → F1·F6(함께) → F4 → F5 → F7을 제안한다. 버그를 먼저 고치고, 서로 얽힌 F1과 F6은 함께 한다.

---

## F1. 괴물 잡기: 조준 + E키

### 지금 동작
- `MonsterGrabKillTrigger`가 괴물 몸의 구 트리거(`OnTriggerEnter`/`OnTriggerStay`)에 쿠키가 닿으면 **자동으로** 처형한다.
- 촉수 돌진 중에는 `MonsterController`가 돌진 경로를 훑어 같은 `TryGrabKill`을 부른다.

### 측정 결과 (Unity, MonsterPlayer 프리팹)

| 항목 | 값 |
|---|---|
| 루트 스케일 | 3.14 |
| 몸 크기 | 폭 6.97m, 높이 6.47m, 앞뒤 3.69m |
| 중심에서 몸 앞면까지 | 약 1.87m |
| 지금 잡기 구 | 중심이 앞으로 0.31m, 반지름 1.57m → 몸 앞면에서 약 0.01m까지 닿는다 |
| 눈 높이(EyeSocket) | 약 3.27m |
| 쿠키 캡슐 반지름 | 0.46m |

### "전방 2m"가 적당한지
- 괴물 **중심**에서 2m로 재면 몸 앞면(1.87m)에서 0.13m밖에 안 남아 거의 닿아야만 잡힌다. 지금과 다르지 않다.
- 그래서 **몸 앞면에서 2m**로 잰다. 중심에서는 약 3.9m다.
- 2m는 쿠키 몸 폭(약 0.9m)의 두 배쯤이다. 조준해서 누르는 방식에서는 너무 짧지도, 너무 멀어 억울하지도 않은 거리로 본다.
- 값은 `GameSettingsSO.grabReachFromFront`로 빼서, 플레이해 보고 조정한다(기본 2m).

### 바뀌는 동작
1. 화면 가운데(조준점)에서 앞으로 **구 캐스트(SphereCast)**를 쏜다.
   - 거리: 몸 앞면까지 + 2m
   - 반지름: 0.4m (조준이 조금 빗나가도 잡히게)
2. 맞은 것 중 가장 가까운, 아직 파괴되지 않은 쿠키(스파이 포함)가 **조준 대상**이다. 조준 대상은 외곽선으로 강조해서, 겹쳐 있을 때 누구를 잡을지 괴물이 고를 수 있게 한다.
3. E키를 누르면 조준 대상을 잡는다. 판정 이후 흐름(`RequestGrabKill` RPC, 처형 연출, 파괴 확정)은 그대로 쓴다.
4. **우선순위 (F1-1):** 조준 대상이 있으면 E키는 잡기로 쓰인다. 문 등 다른 상호작용은 조준 대상이 없을 때만 된다.
5. **자동 처형을 없앤다.** 구 트리거의 `OnTriggerEnter`/`Stay`를 지운다.
6. **촉수 돌진 (확정):** 돌진 경로의 자동 처형을 **없애고**, 돌진은 이동 기술로만 쓴다. 자세한 코드는 아래 "촉수 돌진 바꾸기"에 있다.

### 코드
`MonsterGrabKillTrigger`를 조준 방식으로 바꾼다. 처형 시작부(`TryGrabKill(HideOrSeekPlayer)`)는 그대로 재사용한다.

```csharp
// MonsterGrabKillTrigger.cs — 조준 대상 찾기(괴물 소유 클라이언트에서 매 프레임).
public HideOrSeekPlayer AimTarget { get; private set; }

[Tooltip("괴물 중심에서 몸 앞면까지의 거리(m). MonsterPlayer 프리팹 측정값 약 1.87m.")]
[SerializeField] private float bodyFrontOffset = 1.87f;

private readonly RaycastHit[] aimHits = new RaycastHit[8];

private void Update()
{
    if (!monsterPv.IsMine) return;
    AimTarget = onCooldown ? null : FindAimTarget();
}

private HideOrSeekPlayer FindAimTarget()
{
    Camera cam = Camera.main;
    if (cam == null) return null;

    GameSettingsSO s = GameSettings.Current;
    Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    // 카메라에서 괴물 몸 앞면까지 + 잡기 거리(3인칭이면 카메라가 뒤에 있으므로 그만큼 더한다).
    float bodyFront = Vector3.Dot(monsterPv.transform.position - cam.transform.position, cam.transform.forward) + bodyFrontOffset;
    float maxDistance = Mathf.Max(0f, bodyFront) + s.GrabReachFromFront;

    int count = Physics.SphereCastNonAlloc(ray, s.GrabAimRadius, aimHits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
    HideOrSeekPlayer best = null;
    float bestDistance = float.MaxValue;
    for (int i = 0; i < count; i++)
    {
        var cookie = aimHits[i].collider.GetComponentInParent<HideOrSeekPlayer>();
        if (cookie == null || aimHits[i].distance >= bestDistance) continue;
        PhotonView view = cookie.View;
        if (view == null || view.Owner == null || RoomState.IsBroken(view.Owner)) continue;
        best = cookie;
        bestDistance = aimHits[i].distance;
    }
    return best;
}
```

E키 처리는 `CharacterInteractor`에서 한다. 잡기를 먼저 확인하고, 없으면 기존 사물 상호작용으로 넘어간다.

```csharp
// CharacterInteractor.Update() — 잡기가 우선(F1-1).
if (PlayerInput.InteractPressed && localCharacter is MonsterController monster && monster.TryGrabAimTarget())
    return; // 조준 대상이 있으면 E는 잡기로 쓰였다

if (focused == null || !PlayerInput.InteractPressed) return;
```

```csharp
// MonsterController.cs
public bool TryGrabAimTarget()
{
    HideOrSeekPlayer target = grabKillTrigger.AimTarget;
    return target != null && CanInteract && grabKillTrigger.TryGrabKill(target);
}
```

#### 촉수 돌진 바꾸기 (자동 처형 제거)

지금 `MonsterController.FixedUpdateDash()`는 한 스텝(초속 80m라 한 스텝에 약 1.6m)마다 `TryCatchCookieOnDashPath`로 경로를 훑는다. 쿠키가 있으면 `grabKillTrigger.TryGrabKill(cookie)`로 처형하고, 그 앞에서 멈춘다.

바꾸는 것:
- 처형 호출을 지우고, **쿠키 앞에서 멈추는 것만** 남긴다.
- 멈추지 않으면 초속 80m로 쿠키 캡슐에 부딪혀, 물리 엔진이 쿠키를 튕겨 날릴 수 있다. 그래서 멈추기는 남긴다.
- 멈춘 뒤에는 돌진 이동만 끝낸다(`tentacleDash.Cancel()`). 돌진 애니메이션의 남은 부분은 지금처럼 제자리에서 마무리된다.
- 잡으려면 멈춘 뒤 조준하고 E키를 눌러야 한다.

```csharp
// MonsterController.FixedUpdateDash() — 처형 대신 쿠키 앞에서 멈추기만 한다.
if (TryStopBeforeCookieOnDashPath(velocity * dt, out float travel))
{
    velocity = velocity.normalized * (travel / dt); // 쿠키 앞까지만 움직인다
    tentacleDash.Cancel();                          // 돌진 이동 종료(애니메이션 마무리는 그대로)
}
```

```csharp
// 이번 스텝의 돌진 경로에 살아 있는 쿠키(스파이 포함)가 있으면, 그 앞까지의 거리를 돌려준다. 처형은 하지 않는다.
// 한 스텝에 1m 넘게 움직여 물리 충돌만으로는 쿠키를 튕겨 낼 수 있어서, 부딪히기 전에 멈춘다.
private bool TryStopBeforeCookieOnDashPath(Vector3 step, out float travel)
{
    travel = 0f;
    float distance = step.magnitude;
    if (cookieLayerMask == 0 || distance <= 0f) return false;

    Vector3 direction = step / distance;
    int count = Physics.SphereCastNonAlloc(rb.position + Vector3.up * bodyRadius, bodyRadius, direction,
        dashSweepHits, distance, cookieLayerMask, QueryTriggerInteraction.Ignore);

    float nearest = float.MaxValue;
    for (int i = 0; i < count; i++)
    {
        var cookie = dashSweepHits[i].collider.GetComponentInParent<HideOrSeekPlayer>();
        if (cookie == null || cookie.gameObject == gameObject || cookie.IsBroken) continue;
        nearest = Mathf.Min(nearest, dashSweepHits[i].distance);
    }
    if (nearest == float.MaxValue) return false;

    travel = Mathf.Max(0f, nearest - DashStopMargin); // 닿기 직전에 멈춘다
    return true;
}

private const float DashStopMargin = 0.1f;
```

- 지우는 것: `TryCatchCookieOnDashPath`, 그 안의 `grabKillTrigger.TryGrabKill` 호출과 로그, `RaycastHitDistanceComparer`(정렬이 필요 없어짐).
- 파괴된 쿠키는 `BrokenCookie` 레이어로 옮겨지므로 지금처럼 `cookieLayerMask`에서 자동으로 빠진다. `IsBroken` 검사는 안전장치다.
- 주석 정리: 91~93행의 "돌진 경로에 쿠키가 있으면 GrabKill로 바로 넘어간다(Bug-fix-plan.md §34)"를 "쿠키 앞에서 멈춘다(GameFixPlan.md F1)"로 바꾼다. `MonsterGrabKillTrigger` 머리 주석의 돌진 관련 설명도 고친다.
- `CookieLayerName` 경고 문구(166행)는 "Tentacle dash cannot stop before cookies"로 바꾼다.

- 조준 대상 외곽선: `GrabAimHighlighter`(괴물 본인 화면에서만 동작)가 `AimTarget`이 바뀔 때 대상 렌더러에 외곽선 머티리얼을 켜고 끈다.
- F4의 E 아이콘은 잡기 대상에는 띄우지 않는다(사용자 요청).
- 새 설정값 (`GameSettingsSO`):

```csharp
[Header("Monster Grab (GameFixPlan.md F1)")]
[Tooltip("괴물 몸 앞면에서 잡을 수 있는 거리(m).")]
[SerializeField, Min(0.5f)] private float grabReachFromFront = 2f;
[Tooltip("조준 판정 구의 반지름(m). 조준이 조금 빗나가도 잡히게 한다.")]
[SerializeField, Min(0.05f)] private float grabAimRadius = 0.4f;
public float GrabReachFromFront => grabReachFromFront;
public float GrabAimRadius => grabAimRadius;
```

---

## F3. 로비 방 목록 새로고침

### 원인
- `LobbyController`는 `OnRoomListUpdate`가 주는 **변경분**만 `cachedRoomList`에 반영한다.
- 변경분을 한 번 놓치면(씬 전환 중, 로비 재입장 타이밍 등) 그 방은 다음 변경이 올 때까지 목록에 나타나지 않는다.
- Photon은 로비에 **새로 들어갈 때** 전체 목록을 다시 보내 준다. 새로고침은 로비를 나갔다가 다시 들어가는 방식으로 한다.

### 코드

```csharp
// LobbyController.cs
[SerializeField] private Button refreshButton;
private const float RefreshCooldown = 2f; // 연타로 로비를 계속 드나들지 않게
private float nextRefreshTime;
private bool rejoinLobbyAfterLeave;

public void OnRefreshButtonClicked()
{
    if (Time.unscaledTime < nextRefreshTime || !PhotonNetwork.IsConnectedAndReady) return;
    nextRefreshTime = Time.unscaledTime + RefreshCooldown;

    if (PhotonNetwork.InLobby)
    {
        rejoinLobbyAfterLeave = true;
        PhotonNetwork.LeaveLobby(); // OnLeftLobby에서 다시 들어간다
    }
    else PhotonNetwork.JoinLobby();
}

public override void OnLeftLobby()
{
    if (!rejoinLobbyAfterLeave) return;
    rejoinLobbyAfterLeave = false;
    PhotonNetwork.JoinLobby(); // OnJoinedLobby가 목록을 비우고, 곧 전체 목록이 OnRoomListUpdate로 온다
}
```

- UI: 방 목록 패널 오른쪽 위에 새로고침 버튼을 둔다. 누른 뒤 2초 동안은 비활성으로 보인다.

---

## F4. 상호작용 표시를 동그라미 E 아이콘으로

### 지금 동작
`InteractionPromptUI`는 화면 아래 가운데에 `"Press the '{0}'"` 글자를 띄운다.

### 바뀌는 동작
- 상호작용할 수 있는 사물의 위치(`IInteractable.InteractionPoint`)에 **동그라미 안에 E**가 든 아이콘을 띄운다.
- 아이콘 글자는 지금 설정된 키(`InputBindings.InteractKey`)를 따른다.
- 괴물의 쿠키 잡기에는 띄우지 않는다.
- 탈출 모드의 상자 이름("재료 상자" 등)은 아이콘 아래에 작게 붙인다(`EscapePlan.md` §1.4).

### 코드
`InteractionPromptUI`에 월드 위치를 받는 표시를 추가한다. 화면 좌표로 바꿔 매 프레임 따라간다.

```csharp
// InteractionPromptUI.cs
[SerializeField] private RectTransform iconRoot;   // 동그라미 이미지
[SerializeField] private TMP_Text keyLabel;        // 동그라미 안 글자
[SerializeField] private TMP_Text nameLabel;       // 아이콘 아래 이름(선택)
[SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);

private Vector3 worldAnchor;
private bool hasAnchor;

public void Show(KeyCode key, Vector3 worldPoint, string displayName = null)
{
    if (key != shownKey && keyLabel != null) { keyLabel.text = KeyToLabel(key); shownKey = key; }
    if (nameLabel != null) nameLabel.text = displayName ?? string.Empty;
    worldAnchor = worldPoint + worldOffset;
    hasAnchor = true;
    SetVisible(true);
    UpdatePosition();
}

private void LateUpdate()
{
    if (visible && hasAnchor) UpdatePosition();
}

private void UpdatePosition()
{
    Camera cam = Camera.main;
    if (cam == null || iconRoot == null) return;
    Vector3 screen = cam.WorldToScreenPoint(worldAnchor);
    bool inFront = screen.z > 0f;
    group.alpha = inFront ? 1f : 0f; // 카메라 뒤에 있으면 숨긴다
    if (inFront) iconRoot.position = screen;
}

private static string KeyToLabel(KeyCode key) => key >= KeyCode.A && key <= KeyCode.Z ? key.ToString() : key.ToString().Substring(0, 1);
```

```csharp
// CharacterInteractor.Refresh()
if (focused != null)
    prompt.Show(PlayerInput.Bindings.InteractKey, focused.InteractionPoint, (focused as IInteractionLabel)?.GetLabel(localCharacter));
else prompt.Hide();
```

- 프리팹은 기존 빌더(`Tools/TagOfChaos/Build Interaction Prompt`)를 고쳐, 동그라미 이미지와 글자를 만들게 한다.

---

## F5. 가마솥 보라색 액체 빛나게

### 지금 상태 (Unity 확인)
- 가마솥은 `GameLobbyScene`에 있다.
- 액체는 `Liquid_Body`, `Liquid_Surface` 두 오브젝트이고, 머티리얼은 `Assets/09. Environment/Cauldron/Materials/M_Liquid.mat`다.
- 셰이더는 Built-in `Standard`이고, 발광(Emission)이 꺼져 있다.
- 프로젝트는 Built-in 렌더 파이프라인이다.

### 바꾸는 것
1. **`M_Liquid` 발광 켜기:** 보라색 발광, 세기 약 2.
2. **가마솥 안 보라색 점광원 1개:** 주변 벽과 쿠키를 보랏빛으로 비춘다. 그림자는 끈다(최적화).
3. **은은한 맥동:** `CauldronGlow` 컴포넌트가 발광 세기와 조명 세기를 천천히 오르내리게 한다. `MaterialPropertyBlock`을 써서 머티리얼 에셋은 바꾸지 않는다.
4. 거품(`M_LiquidFoam`)도 약하게 발광시켜 액체와 어울리게 한다.

```csharp
// Assets/02. Scripts/Environment/CauldronGlow.cs
public class CauldronGlow : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer[] liquidRenderers;
    [SerializeField] private Light glowLight;
    [SerializeField, ColorUsage(false, true)] private Color emission = new Color(0.55f, 0.15f, 0.9f) * 2f;
    [SerializeField, Min(0f)] private float pulseSpeed = 1.2f;
    [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.25f;

    private MaterialPropertyBlock block;
    private float baseLightIntensity;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (glowLight != null) baseLightIntensity = glowLight.intensity;
    }

    private void Update()
    {
        float k = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        block.SetColor(EmissionColorId, emission * k);
        foreach (Renderer r in liquidRenderers) if (r != null) r.SetPropertyBlock(block);
        if (glowLight != null) glowLight.intensity = baseLightIntensity * k;
    }
}
```

- 빛 번짐(Bloom)을 원하면 Post Processing 패키지를 추가해야 한다. 이번 범위에서는 발광과 조명만으로 처리하고, 결과를 보고 판단한다.

---

## F6. 괴물 1인칭 시점 (3인칭과 전환 가능)

### 지금 상태
- 괴물은 쿠키와 같은 3인칭 궤도 카메라(`Camera_Ctrl`)를 쓴다. 괴물이 넘기는 값은 시선 높이와 거리다.
- 예전 계획(`Plan.md` §1)에 있던 `MonsterFirstPersonCamera`는 지금 프로젝트에 **없다**(Unity에서 검색 확인).

### 바뀌는 동작
- 괴물은 기본으로 **1인칭**이다. 테스트를 위해 **V키**로 3인칭과 바꿀 수 있다.
- 기본값은 `GameSettingsSO.monsterDefaultView`로 정한다. 출시 전에 전환 키를 막을 수 있게 `allowMonsterViewToggle`도 둔다.
- 1인칭 카메라 위치는 `EyeSocket` 본이다(눈 높이 약 3.27m).
- 1인칭에서는 자기 괴물 몸이 화면을 가리지 않도록, 본인 화면에서만 괴물 렌더러를 **그림자만 보이게**(`ShadowCastingMode.ShadowsOnly`) 바꾼다. 다른 사람 화면에서는 그대로 보인다.
- 마우스로 시점을 돌린다. 1인칭에서는 우클릭 없이 마우스 이동만으로 돌리고, 커서는 잠근다.
- 괴물 몸의 방향은 카메라의 수평 방향을 따른다. 그래야 F1의 조준과 몸 앞 방향이 맞는다.

### 코드

```csharp
// Assets/02. Scripts/Camera/MonsterFirstPersonCamera.cs — Main Camera에 Camera_Ctrl과 함께 둔다.
// 둘 중 하나만 켜진다. MonsterController(IsMine)가 합류할 때 Attach를 부른다.
public class MonsterFirstPersonCamera : MonoBehaviour
{
    [SerializeField] private float sensitivityX = 3f;
    [SerializeField] private float sensitivityY = 2f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 70f;

    private Transform eye;
    private Transform body;
    private float yaw;
    private float pitch;

    public void Attach(Transform bodyRoot, Transform eyeSocket)
    {
        body = bodyRoot;
        eye = eyeSocket;
        yaw = bodyRoot.eulerAngles.y;
        pitch = 0f;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void OnDisable() => Cursor.lockState = CursorLockMode.None;

    private void LateUpdate()
    {
        if (eye == null) return;
        Vector2 delta = PlayerInput.LookDelta; // 1인칭은 우클릭 없이 마우스 이동으로 회전
        yaw += delta.x * sensitivityX;
        pitch = Mathf.Clamp(pitch - delta.y * sensitivityY, minPitch, maxPitch);

        transform.SetPositionAndRotation(eye.position, Quaternion.Euler(pitch, yaw, 0f));
    }

    // 괴물 이동 방향 계산용(몸은 카메라의 수평 방향을 바라본다).
    public Quaternion YawRotation => Quaternion.Euler(0f, yaw, 0f);
}
```

```csharp
// Assets/02. Scripts/Camera/MonsterViewSwitcher.cs — 괴물 본인 클라이언트에서만 동작.
public class MonsterViewSwitcher : MonoBehaviour
{
    public enum View { FirstPerson, ThirdPerson }

    private Camera_Ctrl thirdPerson;
    private MonsterFirstPersonCamera firstPerson;
    private Renderer[] bodyRenderers;
    private MonsterController monster;
    private View current;

    public void Init(MonsterController owner, Transform eyeSocket)
    {
        monster = owner;
        Camera cam = Camera.main;
        thirdPerson = cam.GetComponent<Camera_Ctrl>();
        firstPerson = cam.GetComponent<MonsterFirstPersonCamera>() ?? cam.gameObject.AddComponent<MonsterFirstPersonCamera>();
        firstPerson.Attach(owner.transform, eyeSocket);
        bodyRenderers = owner.GetComponentsInChildren<Renderer>(true);
        Apply(GameSettings.Current.MonsterDefaultView);
    }

    private void Update()
    {
        if (GameSettings.Current.AllowMonsterViewToggle && PlayerInput.ToggleViewPressed)
            Apply(current == View.FirstPerson ? View.ThirdPerson : View.FirstPerson);
    }

    private void Apply(View view)
    {
        current = view;
        bool fp = view == View.FirstPerson;
        firstPerson.enabled = fp;
        thirdPerson.enabled = !fp;
        if (!fp) thirdPerson.SetFollowTarget(monster.gameObject, monster.CameraTargetHeight, monster.CameraDistance, keepRotation: true);
        var mode = fp ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        foreach (Renderer r in bodyRenderers) if (r != null) r.shadowCastingMode = mode;
    }
}
```

```csharp
// PlayerInput.cs / InputBindingsSO.cs 추가
public static Vector2 LookDelta => IsGameplaySuppressed ? Vector2.zero
    : new Vector2(Input.GetAxis(Bindings.MouseXAxis), Input.GetAxis(Bindings.MouseYAxis));
public static bool ToggleViewPressed => !IsGameplaySuppressed && Input.GetKeyDown(Bindings.ToggleViewKey);

[Tooltip("괴물 1인칭/3인칭 전환(테스트용).")]
[SerializeField] private KeyCode toggleViewKey = KeyCode.V;
public KeyCode ToggleViewKey => toggleViewKey;
```

- 괴물 이동은 지금 `PlayerInput.CameraRelativeMove(camera)`를 쓰므로 1인칭 카메라에서도 그대로 동작한다.
- 관전 모드(`SpectatorController`)는 `Camera_Ctrl`을 쓴다. 괴물이 관전으로 넘어갈 때는 `MonsterViewSwitcher`가 1인칭 카메라를 끈다.
- F1의 조준은 화면 가운데를 쓰므로, 1인칭에서는 화면 가운데에 작은 조준점을 띄운다.

---

## F7. HauntedBakery 어두운 곳 밝히기

### 원인 (Unity 측정)
- (54, 0, 20)은 맵 동쪽 가장자리, 높이 16m 외벽(`HAU_Bakery_OuterWall_12`) 바로 안쪽이다. 옆에 18m짜리 나무(`HAU_TwistedTree_A_27`)도 있다.
- 해(방향 조명, 각도 55°, 40°)의 그림자가 외벽 안쪽으로 드리운다.
- 가장 가까운 점광원이 14.3m 떨어져 있고, 조명 범위(14.4m)의 끝에 겨우 걸린다. 맵을 줄일 때 조명을 8m 간격으로 솎아 낸 영향도 있다.
- 6m 격자로 걸을 수 있는 곳 529곳을 조사하니 130곳이 어떤 조명 범위(80%)에도 들어가지 않았다. **동쪽 끝(x ≥ 54)과 서쪽 끝(x ≤ −54) 띠**에 몰려 있다.

### 고치는 방법
맵 축소 도구(`MapCompactor`)에 **조명 보충 단계**를 추가한다. 맵을 다시 빌드해도 유지된다.
1. 12m 격자로 걸을 수 있는 곳을 훑는다.
2. 어떤 조명 범위의 80% 안에도 들지 않는 곳에 맵 분위기에 맞는 보충 조명을 둔다.
   - 벽 옆이면 벽걸이 랜턴 소품과 조명을 함께 둔다.
   - 트인 곳이면 조명만 둔다(낮고 부드럽게, 그림자 끔).
3. 보충한 뒤에도 비는 곳이 없는지 같은 검사로 확인하고, 결과를 로그에 남긴다.

```csharp
// MapCompactor.cs — Compact()의 조명 정리 뒤에 호출.
private const float FillGrid = 12f;
private const float FillCoverage = 0.8f;

private static int FillDarkAreas(Transform lightingRoot, Color fillColor, float fillRange, float fillIntensity)
{
    var lights = lightingRoot.GetComponentsInChildren<Light>(true).Where(l => l.type != LightType.Directional).ToList();
    int added = 0;
    for (float x = -NewHalf + FillGrid / 2f; x < NewHalf; x += FillGrid)
    for (float z = -NewHalf + FillGrid / 2f; z < NewHalf; z += FillGrid)
    {
        if (!TryGroundHeight(x, z, out float y)) continue; // Terrain_Walkable이 없는 곳은 건너뛴다
        var p = new Vector2(x, z);
        if (lights.Any(l => Vector2.Distance(p, new Vector2(l.transform.position.x, l.transform.position.z)) < l.range * FillCoverage)) continue;

        var go = new GameObject($"LGT_Fill_{added:00}");
        go.transform.SetParent(lightingRoot, false);
        go.transform.position = new Vector3(x, y + 3.5f, z);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = fillColor;
        light.range = fillRange;
        light.intensity = fillIntensity;
        light.shadows = LightShadows.None; // 보충 조명은 그림자 없이(최적화)
        lights.Add(light);
        added++;
    }
    return added;
}
```

- 맵별로 색과 세기를 설정에 둔다. HauntedBakery는 기존 랜턴 색(따뜻한 주황)에 맞춘다.
- 다른 맵에도 같은 검사를 돌려 어두운 띠가 있으면 함께 보충한다.
- 이 검사를 `MapCompactTests`에 "조명 범위 밖 격자 비율이 기준 이하" 테스트로 추가한다.

---

## F8. 대기실 괴물 화면에 나간 사람이 남는 버그

### 원인 (코드 확인)
- 색칠 시간 동안 괴물은 대기실(`GameLobbyScene`)에 혼자 남는다.
- 이때 `MonsterLobbyWaitController.EnterWaiting()`이 `PhotonNetwork.IsMessageQueueRunning = false`로 **메시지 큐를 멈춘다.** 쿠키들이 GameScene에서 만드는 캐릭터가 대기실에 생겼다가 사라지는 것을 막기 위해서다(Bug-fix-plan.md §23.3).
- 큐가 멈춘 동안에는 "누가 나갔다"는 소식도 처리되지 않는다. 그래서 `PhotonNetwork.PlayerList`가 바뀌지 않고, `GameLobbyController`의 목록에 나간 사람이 계속 남는다.
- 큐를 다시 켜면 쿠키 캐릭터 생성 문제가 되살아나므로, 큐를 켜서 고칠 수는 없다.

### 고치는 방법
- 기다리는 동안에는 목록이 정확할 수 없으므로 **참가자 목록을 숨기고**, 대기 패널(카운트다운)만 보여준다(확정).
- 괴물이 GameScene에 도착해 큐가 다시 돌면 쌓여 있던 퇴장 소식이 처리된다. 판이 끝나 대기실로 돌아오면 목록은 정확하다.

```csharp
// MonsterLobbyWaitController.cs
[Tooltip("대기 중에는 정확할 수 없는 UI(참가자 목록 등). 메시지 큐가 멈춰 퇴장 소식을 받지 못한다(GameFixPlan.md F8).")]
[SerializeField] private GameObject[] objectsToHideWhileWaiting;

private void SetWaitingUi(bool waiting)
{
    if (waitPanelRoot != null) waitPanelRoot.SetActive(waiting);
    if (objectsToHideWhileWaiting != null)
        foreach (GameObject go in objectsToHideWhileWaiting)
            if (go != null) go.SetActive(!waiting);
    // ... 기존 버튼·동작 비활성 처리 그대로
}
```

- `GameLobbyScene`의 `MonsterManagers`에서 이 배열에 참가자 목록 패널(`playerListContent`의 부모)을 연결한다.
- `GameLobbyController`는 켜질 때 목록을 다시 그리도록 `OnEnable`에서 `RefreshPlayerList()`를 부른다.
- **확정:** 기다리는 동안 목록을 숨기는 방식으로 한다(사용자 결정).

---

## 검증

- **EditMode 테스트:**
  - F1: 조준 대상 선택(겹친 두 쿠키 중 조준선에 가까운 쪽, 파괴된 쿠키 제외, 거리 밖 제외)
  - F1: 촉수 돌진 경로에 쿠키가 있을 때 앞에서 멈추고 처형은 일어나지 않는지(HitCount가 바뀌지 않음)
  - F3: 새로고침 쿨다운
  - F7: 맵마다 조명 범위 밖 격자 비율
- **Play Mode:** 오프라인 모드로 확인한다.
  - F1: 괴물 앞 1m·2m·3m에 쿠키를 두고 E키로 잡히는 거리
  - F1-1: 문 앞에서 조준 대상이 있을 때 잡기가 먼저 되는지
  - F1: 쿠키를 향해 촉수 돌진 → 쿠키 앞에서 멈추고, 쿠키는 튕겨 나가지 않으며 살아 있는지
  - F8: 괴물이 대기실에서 기다리는 동안 참가자 목록이 숨겨지고, 대기실로 돌아오면 다시 보이는지
  - F6: 1인칭·3인칭 전환과 조준점
  - F4: 문과 상자 위에 E 아이콘이 뜨는지
  - F5: 가마솥 발광
  - F7: (54, 0, 20) 스크린샷을 수정 전후로 비교
- **F3·F8:** 두 클라이언트가 필요해서 사용자와 함께 확인한다.
- 작업할 때마다 컴파일 오류와 Console 오류·경고를 확인한다.

---

## 구현 결과 (2026-09-30)

모든 항목을 구현했고, 컴파일 오류·Console 경고 없이 Play Mode에서 확인했다.

| # | 결과 | 계획과 달라진 점 |
|---|---|---|
| F8 | 기다리는 동안 `PlayerListScrollView`를 숨기고, 끝나면 다시 보인다(Play Mode 확인) | "갈 맵이 없어 대기실에서 큐를 다시 켜는 경우"에도 목록을 다시 보이게 했다 |
| F3 | `LobbyScene` 왼쪽 아래에 새로고침 버튼. 누르면 로비를 나갔다 다시 들어가 전체 목록을 받는다. 2초 쿨다운 | 없음 |
| F1 | 조준(화면 가운데) + E키로 잡는다. 몸 앞면에서 0.5~1.9m는 잡히고 2.5m부터는 안 잡힌다(Play Mode 측정). 벽 뒤의 쿠키는 잡히지 않는다 | 조준 표시는 외곽선 대신 `GrabAimReticle`로 했다: 화면 가운데 점이 빨갛게 커지고 대상 머리 위에 표시가 뜬다(프리팹 없이 코드로 생성) |
| F1 돌진 | 자동 처형 제거. 돌진은 쿠키 앞에서 멈추기만 하고 쿠키는 살아 있다 | 충돌 캡슐(반지름 0.31m) 기준으로 멈추면 겉모습이 쿠키를 덮어서, **겉모습 앞면**에서 멈추도록 앞을 미리 살핀다. 멈추면 쿠키가 바로 잡기 거리 안에 있다 |
| F6 | 괴물은 기본 1인칭(눈 높이 3.27m), V키로 3인칭 전환. 1인칭에서는 자기 몸이 그림자만 보이고 커서가 잠긴다. 결과 화면에서는 커서가 풀린다 | 없음 |
| F4 | 사물 위치에 동그라미 안 E 아이콘. 사물이 이름을 주면(`IInteractionLabel`) 아래에 붙는다. 괴물이 쿠키를 조준 중이면 아이콘을 숨긴다 | 동그라미 그림(`PromptCircle.png`)도 빌더가 코드로 만든다 |
| F5 | 액체 발광(`M_Liquid`, `M_LiquidFoam`), 액체 위 보라색 조명, `CauldronGlow` 맥동. 가마솥 빌더(`CauldronBuilder`)에 넣어 다시 만들어도 유지된다 | 없음 |
| F7 | `MapCompactor.FillDarkAreas`: 7m 격자로 훑어 어떤 조명 범위의 55% 안에도 없는 곳에 맵 랜턴과 같은 색의 보충 조명(그림자 없음)을 둔다 | 14m 격자·80% 기준으로는 (54, 0, 20)이 여전히 어두웠다(Play Mode 화면 확인). 7m·55%로 바꾸고 세기를 기존 랜턴의 1.1배로 했다 |

- 보충 조명 수: HauntedBakery 24, CandyForest 14, ChocolateFactory 17, CursedCandyCarnival 42, GingerbreadVillage 32. 5개 맵 모두 다시 줄였고 통행 검사는 모두 깨끗하다.
- 테스트: `MapCompactTests`에 `MapScene_WalkableGroundIsLit`(맵마다 걸을 수 있는 곳의 98% 이상이 조명 범위 55% 안) 추가. 맵 테스트 20개 모두 통과.
- 두 클라이언트가 필요한 F3·F8의 실제 온라인 동작(다른 사람이 만든 방이 보이는지, 다른 사람이 나갔을 때)은 사용자와 함께 확인이 필요하다.
