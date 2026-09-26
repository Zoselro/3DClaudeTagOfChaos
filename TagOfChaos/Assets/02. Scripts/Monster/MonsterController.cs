using Photon.Pun;
using UnityEngine;

// 괴물 전용 이동 컨트롤러(GameRule.md §6.2). 원래 1인칭(마우스로 몸통 회전 + 몸 기준 이동)으로
// 설계됐으나, 사용자 결정으로 쿠키와 같은 3인칭 궤도 카메라(Camera_Ctrl, 우클릭 드래그 회전) + 카메라
// 기준 이동 + 이동 방향으로 몸 회전 방식으로 바뀌었다(Bug-fix-plan.md §23.4, A안). 쿠키와 입력은
// 같지만 TentacleDash/GrabKill 상태와 애니메이터가 달라 HideOrSeekPlayer를 재사용하지는 않는다.
//
// 이동 방식은 Rigidbody 물리 기반으로 확정됨(사용자 확인, GameRule.md v3.4) — HideOrSeekPlayer.cs와
// 동일한 패턴(로컬만 실제 물리 시뮬레이션, 원격은 isKinematic)을 그대로 따른다.
public class MonsterController : MonoBehaviourPunCallbacks, IPunObservable, IRespawnable, IGameCharacter

{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Transform eyeSocket; // 3인칭 카메라 시선 높이 기준(머리 안쪽 지점)
    [SerializeField] private float cameraTargetHeight = 2.2f; // eyeSocket이 비어 있을 때만 쓰는 대체값
    [SerializeField] private float cameraDistance = 4.5f;
    [SerializeField] private float speed = 4f;
    [SerializeField] private LayerMask obstructionMask;
    [SerializeField] private MonsterGrabKillTrigger grabKillTrigger;
    [SerializeField] private Animator animator;

    private Rigidbody rb;
    private Vector3 moveInput; // Update()에서 입력만 기록, 실제 적용은 FixedUpdate에서
    private readonly MonsterTentacleDash tentacleDash = new MonsterTentacleDash();
    private readonly NetworkTransformSync<MonsterMoveState> networkSync = new NetworkTransformSync<MonsterMoveState>();
    private MonsterMoveState currentState = MonsterMoveState.Idle;
    private MonsterMoveState previousState = MonsterMoveState.Idle;

    // GrabKill 재생 시간(= 처형 트리거 쿨다운, GameRule.md v3.5). 예전에는 LateUpdate에서 Animator 상태
    // 이름 + normalizedTime으로 종료를 감지했는데, 같은 프레임 Update()가 곧바로 Idle/Walk로
    // 덮어써 GrabKill 트리거가 취소되고 종료 감지에 영원히 도달하지 못해 괴물이 한 판에 한 번만 처치할
    // 수 있었다(research.md §8.4). 클립 길이로 잰 타이머로 바꾸고, 재생 중에는 이동·상태 전환을 막는다.
    private const string GrabKillClipKeyword = "GrabKill";
    private const float FallbackGrabKillDuration = 2f;
    private float grabKillDuration = FallbackGrabKillDuration;
    private float grabKillRemaining;

    public bool IsGrabKilling => grabKillRemaining > 0f;

    // 촉수 돌진 애니메이션은 이동(TentacleDashDuration, 0.25초)보다 길다(클립 약 2초). 예전에는 이동이 끝나는 즉시
    // Walk/Idle로 바꿔 동작의 앞부분만 보였다(㉚-B). 이제 GrabKill처럼 클립 길이만큼 TentacleDash 상태를 유지하고
    // (이동은 처음 구간만, 나머지는 자리에서 마무리 — 그동안 이동·재돌진 입력 무시), 끝나면 Idle/Walk로 돌아간다.
    // 돌진 경로에 쿠키가 있으면 그 자리에서 멈추고 GrabKill로 바로 넘어간다(사용자 결정, Bug-fix-plan.md §34).
    private const string TentacleDashClipKeyword = "TentacleDash";
    private const string CookieLayerName = "Cookie"; // 파괴된 쿠키는 BrokenCookie 레이어로 옮겨져 자동 제외된다
    private float tentacleDashAnimDuration;
    private float tentacleDashAnimRemaining;
    private int cookieLayerMask;
    private readonly RaycastHit[] dashSweepHits = new RaycastHit[8];

    public bool IsTentacleDashing => tentacleDashAnimRemaining > 0f;

    // 괴물 체격에 맞는 3인칭 카메라 설정 — 본인 조작 카메라와 관전 카메라(SpectatorController)가 같은 값을 쓴다.
    public float CameraTargetHeight => eyeSocket != null ? eyeSocket.position.y - transform.position.y : cameraTargetHeight;
    public float CameraDistance => cameraDistance;
    public bool IsLocallyControlled => pv != null && pv.IsMine;

    // IGameCharacter — 관전·카메라가 캐릭터 종류를 몰라도 되도록 하는 공통 계약(research.md §12 E1).
    public CharacterRole Role => CharacterRole.Monster;
    public PhotonView View => pv;
    public bool IsSpectatable => true;

    public override void OnEnable()
    {
        base.OnEnable();
        CharacterRegistry.Register(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        CharacterRegistry.Unregister(this);
    }

    private void Awake()
    {
        if (!pv.IsMine) return;

        // HideOrSeekPlayer.Awake()와 동일한 "Main Camera에 나를 넘긴다" 패턴 — 쿠키용 3인칭 궤도
        // 카메라를 그대로 재사용하되, 괴물 체격에 맞게 시선 높이/거리만 바꿔 넘긴다. 예전에는 씬에 없는
        // MonsterFirstPersonCamera를 ?.로 찾아 조용히 실패해 카메라가 고정돼 있었다(§23.4.1).
        var camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
        if (camCtrl == null)
        {
            Debug.LogError("MonsterController: Camera_Ctrl not found on Main Camera.");
            return;
        }

        camCtrl.InitCamera(gameObject, CameraTargetHeight, CameraDistance);
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = !pv.IsMine; // 원격은 물리 시뮬레이션 끔(HideOrSeekPlayer.Start()와 동일 이유)
        if (pv.IsMine)
        {
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // TentacleDash 고속 이동 대비
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation; // 회전은 FixedUpdate()에서 rb.MoveRotation으로 직접 제어
        }

        grabKillDuration = FindClipLength(GrabKillClipKeyword, FallbackGrabKillDuration);
        tentacleDashAnimDuration = FindClipLength(TentacleDashClipKeyword, GameSettings.Current.TentacleDashDuration);
        cookieLayerMask = LayerMask.GetMask(CookieLayerName);
        if (cookieLayerMask == 0) Debug.LogWarning($"[MonsterController] Layer '{CookieLayerName}' not found. Tentacle dash cannot catch cookies on its path.");
    }

    private float FindClipLength(string keyword, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return fallback;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name.Contains(keyword)) return clip.length;
        }
        Debug.LogWarning($"[MonsterController] Animation clip containing '{keyword}' not found. Using {fallback}s.");
        return fallback;
    }

    private void Update()
    {
        if (!pv.IsMine)
        {
            networkSync.Interpolate(transform, Time.deltaTime);
            ChangeState(networkSync.RemoteState);
            return;
        }

        tentacleDash.TickCooldown(Time.deltaTime);

        if (IsGrabKilling)
        {
            // 처형 연출 중에는 제자리에서 GrabKill 상태를 유지한다 — 상태가 유지돼야 직렬화로 원격에도 전달된다.
            moveInput = Vector3.zero;
            grabKillRemaining -= Time.deltaTime;
            if (grabKillRemaining > 0f) return;

            grabKillRemaining = 0f;
            if (grabKillTrigger != null) grabKillTrigger.ResetTrigger();
            ChangeState(MonsterMoveState.Idle);
        }

        if (IsTentacleDashing)
        {
            // 돌진 동작이 끝날 때까지 TentacleDash 상태를 유지한다(이동 구간은 FixedUpdate의 돌진 이동이 맡음).
            moveInput = Vector3.zero;
            tentacleDashAnimRemaining -= Time.deltaTime;
            if (tentacleDashAnimRemaining > 0f) return;
            tentacleDashAnimRemaining = 0f;
        }

        moveInput = ReadCameraRelativeMoveInput();

        // 입력 키는 좌Shift로 확정(사용자 확인, GameRule.md v3.4) — InputBindings의 TentacleDashKey에서 바꿀 수 있다.
        // 쿠키의 Shift(질주)와는 서로 다른 캐릭터 클래스에서 각자 로컬로 읽는 입력이라 충돌 없음.
        if (PlayerInput.TentacleDashPressed && tentacleDash.TryStartDash(transform.forward, transform.position, obstructionMask))
        {
            tentacleDashAnimRemaining = Mathf.Max(tentacleDashAnimDuration, GameSettings.Current.TentacleDashDuration);
            ChangeState(MonsterMoveState.TentacleDash);
        }
        else if (moveInput != Vector3.zero && !tentacleDash.IsDashing)
            ChangeState(MonsterMoveState.Walk);
        else if (!tentacleDash.IsDashing)
            ChangeState(MonsterMoveState.Idle);
    }

    // Rigidbody 조작은 물리 스텝에서만(HideOrSeekPlayer.FixedUpdate()와 동일 관례).
    private void FixedUpdate()
    {
        if (!pv.IsMine) return;

        if (tentacleDash.IsDashing)
        {
            // 순간이동에 가까운 고정 변위라 속도(velocity)가 아니라 rb.MovePosition으로 직접
            // 이동시킨다 — 기본 이동(rb.linearVelocity)과 방식이 다른 것은 의도적.
            Vector3 step = tentacleDash.TickDash(Time.deltaTime);
            if (TryCatchCookieOnDashPath(step, out float travel))
                rb.MovePosition(rb.position + step.normalized * travel); // 쿠키 앞에서 멈추고 GrabKill로 전환됨
            else
                rb.MovePosition(rb.position + step);
        }
        else
        {
            // transform.rotation 직접 대입 대신 rb.MoveRotation — Rigidbody 보간이 인식하는 회전만 갱신해야
            // 매 물리 스텝마다 회전이 튀지 않는다(쿠키에서 고친 Bug-fix-plan.md §13과 같은 이유).
            if (moveInput != Vector3.zero)
                rb.MoveRotation(Quaternion.LookRotation(moveInput));

            Vector3 horizontal = moveInput * speed;
            rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
        }
    }

    // 이번 물리 스텝의 돌진 이동 경로를 처형 판정 범위(MonsterGrabKillTrigger의 구)로 훑어 살아 있는 쿠키가 있으면
    // 처형한다. 한 스텝에 1m 넘게 움직여 트리거(OnTriggerEnter)만으로는 쿠키를 건너뛸 수 있기 때문이다.
    // 처형했으면 true와 함께 쿠키에 닿기까지 움직일 거리를 돌려준다.
    private bool TryCatchCookieOnDashPath(Vector3 step, out float travel)
    {
        travel = 0f;
        float distance = step.magnitude;
        if (grabKillTrigger == null || cookieLayerMask == 0 || distance <= 0f || grabKillTrigger.IsOnCooldown) return false;

        Vector3 direction = step / distance;
        int count = Physics.SphereCastNonAlloc(grabKillTrigger.ReachCenter, grabKillTrigger.ReachRadius, direction,
            dashSweepHits, distance, cookieLayerMask, QueryTriggerInteraction.Ignore);

        // 가까운 쿠키부터 처형을 시도한다(NonAlloc 결과는 정렬돼 있지 않다).
        System.Array.Sort(dashSweepHits, 0, count, RaycastHitDistanceComparer.Instance);
        for (int i = 0; i < count; i++)
        {
            var cookie = dashSweepHits[i].collider.GetComponentInParent<HideOrSeekPlayer>();
            if (cookie == null || cookie.gameObject == gameObject) continue;
            if (!grabKillTrigger.TryGrabKill(cookie)) continue;

            travel = dashSweepHits[i].distance;
            Debug.Log($"[MonsterController] Tentacle dash caught cookie (view {cookie.View.ViewID}) after {travel:F2}m.");
            return true;
        }
        return false;
    }

    private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    // 쿠키와 같은 카메라 기준 수평 이동 방향(3인칭 궤도 카메라 전제, PlayerInput 공용).
    private Vector3 ReadCameraRelativeMoveInput()
    {
        Camera cam = Camera.main;
        return PlayerInput.CameraRelativeMove(cam != null ? cam.transform : null);
    }

    // 맵 밖으로 떨어졌을 때 VoidKillZone 또는 FallGuard가 호출한다(Bug-fix-plan.md §30.4) — 예전에는 괴물용 복귀
    // 경로가 없어 무한 낙하했다. 쿠키(HideOrSeekPlayer.RespawnToSpawnPoint)와 같은 순간이동 규칙을 따른다.
    public void RespawnToSpawnPoint()
    {
        if (rb == null) return;
        if (!SceneSpawnPoints.TryFindClearPosition(SceneSpawnPoints.Monster, GameSettings.Current.MonsterSpawnRange, out Vector3 respawnPos))
            return;

        tentacleDash.Cancel();
        tentacleDashAnimRemaining = 0f;
        rb.linearVelocity = Vector3.zero;
        // 비키네마틱 Rigidbody는 transform만 바꾸면 다음 물리 스텝에 되돌아가므로 rb.position도 함께 바꾼다.
        rb.position = respawnPos;
        transform.position = respawnPos;
        Debug.Log($"[MonsterController] Respawned at {respawnPos} after falling out of the map.");
    }

    // MonsterGrabKillTrigger가 근접 자동 처형 순간 호출 — 전원에게 GrabKill 애니메이션 재생.
    public void PlayGrabKill()
    {
        // 돌진 중이거나 돌진 마무리 동작 중에 처형이 시작되면 돌진을 끊고 GrabKill을 우선한다(§34).
        tentacleDash.Cancel();
        tentacleDashAnimRemaining = 0f;
        grabKillRemaining = grabKillDuration;
        ChangeState(MonsterMoveState.GrabKill);
    }

    private void ChangeState(MonsterMoveState newState)
    {
        if (animator == null || previousState == newState) return;

        animator.ResetTrigger(previousState.ToString());
        animator.SetTrigger(newState.ToString());
        previousState = newState;
        currentState = newState;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
            networkSync.Write(stream, transform, currentState);
        else
            networkSync.Read(stream, transform);
    }
}
