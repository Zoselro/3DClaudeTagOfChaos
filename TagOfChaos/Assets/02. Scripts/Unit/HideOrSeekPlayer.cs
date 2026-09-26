using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 쿠키 캐릭터 조정자. 이동·점프·회피 물리와 동기화를 맡고, 입력(PlayerInput)·접지(PlayerGroundDetector)·
// 애니메이션(PlayerAnimationDriver)·캐리 추종(PlayerCarryFollower)은 협력 객체에 맡긴다.
// 캐릭터 공통 계약(IGameCharacter·IRespawnable)으로 관전·카메라·낙하 복귀가 종류와 무관하게 동작한다.
public class HideOrSeekPlayer : MonoBehaviourPunCallbacks, IPunObservable, IRespawnable, IGameCharacter
{
    [Header("Options")]
    [SerializeField] private float speed;
    [SerializeField] private float jumpPower;
    [SerializeField] private LayerMask groundLayer = 1; // 착지 판정용 지형 레이어 (기본: Default)
    [SerializeField] private float groundCheckOffset = 0.3f; // 레이캐스트 시작 높이 및 최소 검사 거리
    [SerializeField] private float jumpFreezeNormalizedTime = 0.5f; // 착지 전까지 Jump 애니메이션을 멈춰둘 재생 지점
    [SerializeField] private float dodgeDuration = 0.5f; // 회피 지속 시간

    [Header("Components")]
    [SerializeField] private PhotonView pv;

    private float baseSpeed;

    [Header("States")]
    [SerializeField] private bool isJump;
    [SerializeField] private bool isDodge;
    [SerializeField] private bool keepMovingAfterDodge;

    private Vector3 rotation;
    private Vector3 rotation_value;
    private Vector3 dodgeRotation;
    private Vector3 dodgeMoveDir;
    private float dodgeTimer;
    private bool jumpRequested; // Update()에서 입력만 기록, 실제 점프 적용은 FixedUpdate()에서(PlayerControllPlan.md §18.3)

    private Animator animator;
    private Rigidbody rb;
    private PlayerGroundDetector groundDetector;
    private PlayerAnimationDriver animationDriver;
    private PlayerNetworkSync networkSync;
    private PlayerGrabController grabController;
    private PlayerCarryFollower carryFollower;

    // 다른 클래스가 이 캐릭터에 보내는 RPC 이름 — 메서드 이름을 바꾸면 컴파일 단계에서 함께 바뀐다(research.md §12 E3).
    public const string RpcOnGrabbedByOwner = nameof(OnGrabbedByOwner);
    public const string RpcOnReleased = nameof(OnReleased);
    public const string RpcRequestGrabKill = nameof(RequestGrabKill);

    // 사망/대화/컷신 등 상위 시스템이 이 프로퍼티만 세팅하면 이동이 잠긴다. 파괴·들림 상태는 외부 설정과
    // 별개로 항상 잠겨 있어야 하므로 계산값으로 합친다 — 예전에는 채팅창을 닫을 때 GameManager가 false를
    // 대입해 파괴된 쿠키의 잠금까지 풀 수 있었다.
    private bool externalMovementLock;
    public bool IsMovementLocked
    {
        get => externalMovementLock || IsBroken || (carryFollower != null && carryFollower.IsCarried);
        set => externalMovementLock = value;
    }

    // 외부에서 "이 인스턴스가 내 캐릭터인지" 판별할 수단 (GameManager의 채팅 이동잠금이 참조)
    public bool IsMine => pv != null && pv.IsMine;
    public bool IsLocallyControlled => IsMine;

    // IGameCharacter — 관전·카메라가 캐릭터 종류를 몰라도 되도록 하는 공통 계약(research.md §12 E1).
    public CharacterRole Role => CharacterRole.Cookie;
    public PhotonView View => pv;
    public bool IsSpectatable => pv != null && pv.Owner != null && !RoomState.IsBroken(pv.Owner);
    public bool CanInteract => !IsMovementLocked; // 파괴·들림·채팅 잠금 중에는 상호작용 불가
    public float CameraTargetHeight => Camera_Ctrl.CookieTargetHeight;
    public float CameraDistance => -1f; // Camera_Ctrl 인스펙터 기본 거리

    // GrabKill로 파괴됐는지 여부(0=정상, 2=파괴) — GameRule.md §4.4, (A) 확정으로 1(균열)은 도달 불가능
    private int hitCount;

    public bool IsBroken => hitCount >= 2;

    public void SetCarryLayerWeight(float weight) => animationDriver.SetCarryLayerWeight(weight);

    [PunRPC]
    private void OnGrabbedByOwner(int newCarrierViewId)
    {
        if (!pv.IsMine || IsBroken) return;
        if (carryFollower.TryAttach(newCarrierViewId)) animationDriver.ChangeState(PlayerMoveState.Held);
    }

    [PunRPC]
    private void OnReleased(bool withThrow)
    {
        if (!pv.IsMine) return;
        ReleaseFromCarrierLocally();
    }

    // 파괴 상태면 키네마틱·잠금을 그대로 유지하고, 아니면 물리와 Idle 애니메이션을 되돌린다.
    private void ReleaseFromCarrierLocally()
    {
        if (carryFollower.Detach(restorePhysics: !IsBroken) && !IsBroken)
            animationDriver.ChangeState(PlayerMoveState.Idle);
    }

    // 들린 동안 매 FixedUpdate마다 드는 쪽 CarrySocket을 따라간다. 드는 쪽이 방을 나갔거나 파괴돼 사라졌으면
    // 공중에 고정된 채 남지 않도록 스스로 내려온다.
    private bool TryFollowCarrier()
    {
        if (!carryFollower.IsCarried) return false;
        if (carryFollower.TryFollow()) return true;
        ReleaseFromCarrierLocally();
        return false;
    }

    [PunRPC]
    private void RequestGrabKill()
    {
        if (!pv.IsMine || IsBroken) return; // 본인 클라이언트만 자기 상태 확정(소유권 원칙)

        hitCount = 2;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.HitCount, hitCount } });

        // 들고 있던 쿠키는 내려놓고, 들려 있었다면 캐리 관계를 끊는다.
        if (grabController != null) grabController.Release();
        ReleaseFromCarrierLocally();

        animationDriver.ChangeState(PlayerMoveState.Broken);

        // 파괴된 몸은 CookieLifeStatePresenter가 모든 클라이언트에서 렌더러·콜라이더를 끄고 비충돌 레이어로 옮긴다. 콜라이더가 꺼진 채
        // 중력을 받으면 바닥을 뚫고 떨어지므로 물리도 멈춘다(research.md §8.11).
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        GetComponent<SpectatorController>()?.EnterSpectatorMode();
    }

    private void Awake()
    {
        // Photon의 네트워크 디스패치(OnPhotonSerializeView)는 Unity의 Awake→Start 순서와 무관하게
        // 별도 루프(PhotonHandler.Dispatch())에서 호출될 수 있어, 이 오브젝트의 Start()가 아직 실행되기
        // 전에 송수신이 먼저 일어날 수 있다(Bug-fix-plan.md §12). OnPhotonSerializeView가 쓰는 협력 객체
        // (networkSync, animationDriver)와 Rigidbody 참조는 IsMine 여부와 상관없이 Awake에서 가장 먼저
        // 만든다 — animationDriver가 Start에 남아 있어 실제 빌드에서 NRE가 났다(Bug-fix-plan.md §24.6 ⑰-C).
        networkSync = new PlayerNetworkSync();
        animator = GetComponent<Animator>();
        animationDriver = new PlayerAnimationDriver(animator, jumpFreezeNormalizedTime);
        groundDetector = new PlayerGroundDetector(groundLayer, groundCheckOffset);
        rb = GetComponent<Rigidbody>();
        grabController = GetComponent<PlayerGrabController>();
        carryFollower = new PlayerCarryFollower(gameObject, rb);

        if (!pv.IsMine) return;

        Camera_Ctrl camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
        if (camCtrl != null)
            camCtrl.InitCamera(gameObject);
    }

    private void Start()
    {
        baseSpeed = speed;
        if (animator != null)
            animator.applyRootMotion = false; // 이동은 전부 Move()가 Rigidbody 속도를 직접 갱신하므로, 클립에 내장된 루트 모션이 겹쳐 적용되면 안 됨

        // 물리 엔진(Rigidbody) 도입(PlayerControllPlan.md §18.3) — 로컬 소유 캐릭터만 실제 물리
        // 시뮬레이션을 받는다. 원격 캐릭터는 지금처럼 networkSync가 transform을 직접 보간하므로,
        // Rigidbody가 동시에 중력/충돌로 같은 transform을 건드리면 두 시스템이 충돌한다 — 그래서
        // 원격 인스턴스는 isKinematic으로 물리를 꺼둔다.
        rb.isKinematic = !pv.IsMine;
        if (pv.IsMine)
        {
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 빠른 낙하/점프 시 얇은 바닥·난간을 뚫고 지나가는 것 방지
            rb.interpolation = RigidbodyInterpolation.Interpolate; // FixedUpdate 사이 시각적 끊김 완화
            rb.constraints = RigidbodyConstraints.FreezeRotation;  // 회전은 transform.LookAt으로 직접 제어, 물리 충돌로 인한 회전(넘어짐)은 막음
        }

        // Mesh_0(캐릭터 몸통 메시, 옛 Ch36에서 Cookie 마이그레이션으로 개명됨, PlayerControllPlan.md §25/§26)은
        // 콘케이브 메시 콜라이더 에러를 피하려고 별도의 키네마틱 Rigidbody를 갖고 있는데(§21), 그 결과
        // 루트의 CapsuleCollider와는 서로 다른 물리 바디가 되어버려 매 물리 스텝마다 자기 자신과 충돌
        // 판정을 일으키고 있었다 — 위치는 고정된 채 속도만 거대하고 불규칙해지는 원인이었다
        // (Bug-fix-plan.md §14). 같은 캐릭터의 일부이므로 명시적으로 서로의 충돌을 무시한다(로컬/원격
        // 모두 동일한 구조라 IsMine 여부와 무관하게 적용).
        Collider rootCollider = GetComponent<CapsuleCollider>();
        Collider bodyMeshCollider = transform.Find(BodyMeshName)?.GetComponent<Collider>();
        if (rootCollider != null && bodyMeshCollider != null)
            Physics.IgnoreCollision(rootCollider, bodyMeshCollider, true);
        else
            Debug.LogError($"[HideOrSeekPlayer] '{BodyMeshName}' collider or root CapsuleCollider is missing on {name}.");
    }

    private const string BodyMeshName = "Mesh_0";

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

    private void Update()
    {
        if (IsMovementLocked)
            return;

        if (pv.IsMine) // 자신이 조종하는 캐릭터일 때만 입력 처리
        {
            CheckMovementInput(); // 이동 입력 체크 + 회전 갱신(좌표 이동은 FixedUpdate의 Move()가 담당)
            CheckJumpInput(); // 점프 입력 체크(의도만 기록, 실제 적용은 FixedUpdate)
            CheckDodgeInput(); // 회피 입력 체크
            animationDriver.HandleJumpAnimationHold(); // 착지 전까지 Jump 애니메이션이 끝까지 재생되지 않도록 고정
        }
        else // 원격지 아바타 캐릭터들은 위치, 회전, 애니메이션을 따라오게 동기화 처리
        {
            networkSync.Interpolate(transform, Time.deltaTime);
            animationDriver.ChangeState(networkSync.RemoteState);
            animationDriver.SetCarryLayerWeight(networkSync.RemoteIsCarrying ? 1f : 0f);
        }
    }

    // Rigidbody 조작은 물리 스텝(FixedUpdate)에서만 수행한다(Unity 관례) — 입력은 Update()에서 이미 읽어뒀다.
    private void FixedUpdate()
    {
        if (!pv.IsMine) return;
        if (TryFollowCarrier()) return; // 그랩당한 동안은 일반 이동 로직 대신 캐리 위치만 추적
        if (IsMovementLocked)
            return;

        bool grounded = groundDetector.IsGrounded(transform.position);

        // 점프 여부와 무관하게 매 스텝 접지 확인 — 점프 없이 걸어서 맵 밖으로 나가도 이제는
        // 정상적으로 낙하한다(과거엔 ApplyGravity()가 isJump일 때만 동작해 아예 안 떨어졌음, §18.2).
        if (isJump && grounded && rb.linearVelocity.y <= 0f) // 상승 중엔 착지 처리하지 않음(막 점프한 순간 오탐 방지)
        {
            isJump = false;
            animationDriver.ResumePlayback(); // 공중에서 멈춰뒀던 애니메이션 재생 속도 복구
        }

        // 의도한 점프(Space)든 걸어서 벗어난 낙하든, 공중에서는 항상 이동 입력에 자유롭게 반응한다
        // (PlayerControllPlan.md §24 — 기존에는 의도한 점프만 시작 시점 방향으로 고정됐었으나,
        // §23.4에서 낙하에 이미 적용한 자유 공중 조작과 동일하게 통일했다).
        if (jumpRequested && grounded && !isDodge)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpPower, rb.linearVelocity.z);
            isJump = true;
            animationDriver.ReplayJump(); // 연타로 재점프해도 항상 처음부터 재생(Bug-fix-plan.md §15)
        }
        else if (!isJump && !grounded && !isDodge) // 점프 없이 걸어서 가장자리를 벗어나 낙하가 시작된 경우
        {
            isJump = true;
            animationDriver.ReplayJump();
        }
        jumpRequested = false;

        Move();

        // 낙하 최후 방어선은 괴물과 공용인 FallGuard 컴포넌트로 옮겼다(Bug-fix-plan.md §30.4).
    }

    private void CheckMovementInput()
    {
        Camera cam = Camera.main;
        Vector3 moveDir = PlayerInput.CameraRelativeMove(cam != null ? cam.transform : null);

        if (moveDir != Vector3.zero)
        {
            rotation = moveDir;
            rotation_value = rotation;

            if (isDodge)
                rotation = dodgeRotation;

            if (!isJump && !isDodge) // 점프와 회피 중이 아닐 때만 이동 애니메이션 상태 변경
            {
                bool isRunning = PlayerInput.RunHeld;
                animationDriver.ChangeState(isRunning ? PlayerMoveState.Run : PlayerMoveState.Walk);
            }
        }
        else
        {
            rotation = Vector3.zero;
            rotation_value = Vector3.zero;

            if (!isJump && !isDodge) // 점프와 회피 중이 아닐 때만 이동 애니메이션 상태 변경
                animationDriver.ChangeState(PlayerMoveState.Idle);
        }
    }

    private void CheckJumpInput()
    {
        if (PlayerInput.JumpPressed && !isJump && !isDodge)
        {
            jumpRequested = true;
        }
    }

    private void CheckDodgeInput()
    {
        if (PlayerInput.DodgePressed && rotation != Vector3.zero && !isJump && !isDodge)
        {
            dodgeMoveDir = rotation;
            dodgeRotation = rotation;
            speed *= 2f;
            isDodge = true;
            keepMovingAfterDodge = true; // isDodge와 동시에 세팅해야 Move()의 관성 이동 분기가 실제로 도달 가능해짐
            dodgeTimer = dodgeDuration;

            animationDriver.ChangeState(PlayerMoveState.Dodge);
        }

        if (isDodge)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer <= 0f)
            {
                DodgeOut();
            }
        }
    }

    private void DodgeOut()
    {
        speed *= 0.5f;
        isDodge = false;
        keepMovingAfterDodge = false;
        rotation = rotation_value;
    }

    // 움직일 때 — 수평 속도만 Rigidbody에 넘기고, 수직 속도(중력/점프)는 물리 엔진이 이미 채운 값을 그대로 보존한다.
    public void Move()
    {
        Vector3 dir;
        float vel;
        Vector3 lookDir;

        if (isDodge && keepMovingAfterDodge) // 캐릭터가 회피중일 경우, 키보드에서 손을 떼더라도 회피를 시작했던 그 방향으로 강제로 밀어붙임
        {
            dir = dodgeMoveDir;
            vel = speed; // CheckDodgeInput에서 이미 2배로 올려둔 speed
            lookDir = new Vector3(dodgeMoveDir.x, 0f, dodgeMoveDir.z);
        }
        else // Shift를 눌렀을 경우 기본 속도의 +30%(질주). 아니면 100% 속도 유지 — 점프/낙하 중에도 동일하게 적용(PlayerControllPlan.md §24/§25)
        {
            vel = PlayerInput.RunHeld ? baseSpeed * 1.3f : baseSpeed;
            dir = rotation;
            lookDir = new Vector3(rotation.x, 0f, rotation.z);
        }

        // transform.LookAt() 대신 rb.MoveRotation() 사용 — Rigidbody 보간(interpolation)은
        // MoveRotation으로 갱신된 회전만 인식한다. transform.rotation을 직접 대입하면 Rigidbody가
        // 추적하는 회전값과 어긋나 매 물리 스텝마다 회전이 튀었다 되돌아가길 반복해 걷는 모습이
        // 버벅거리고 미끄러지는 것처럼 보이는 버그가 있었다(Bug-fix-plan.md §13).
        if (lookDir != Vector3.zero)
            rb.MoveRotation(Quaternion.LookRotation(lookDir));

        Vector3 horizontal = new Vector3(dir.x * vel, 0f, dir.z * vel);
        rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z); // y는 물리 엔진(중력/점프)이 채운 값 그대로 보존
    }


    // 맵 밖으로 떨어졌을 때 VoidKillZone 또는 FallGuard가 호출한다(PlayerControllPlan.md §18.4, IRespawnable).
    public void RespawnToSpawnPoint()
    {
        if (!SceneSpawnPoints.TryFindClearPosition(SceneSpawnPoints.Cookie, GameSettings.Current.CookieSpawnRange, out Vector3 respawnPos))
            return;

        rb.linearVelocity = Vector3.zero; // 낙하 속도가 남아있으면 스폰 직후 바닥을 뚫고 지나갈 수 있음
        // Non-kinematic Rigidbody에서는 transform.position을 직접 대입해도 다음 물리 스텝에서
        // Rigidbody가 자신이 마지막으로 시뮬레이션한 위치로 되돌려버린다 — 반드시 rb.position으로
        // 물리 엔진에도 같이 알려줘야 실제로 순간이동이 반영된다(Play Mode 실측으로 확인된 문제).
        rb.position = respawnPos;
        transform.position = rb.position;

        isJump = false;
        animationDriver.ResumePlayback();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (stream.IsWriting) // 로컬 플레이어의 상태 정보 송신
        {
            networkSync.Write(stream, transform, animationDriver.CurrentState, grabController != null && grabController.IsCarrying);
        }
        else // 원격 플레이어의 상태 정보 수신
        {
            networkSync.Read(stream, transform);
        }
    }
}
