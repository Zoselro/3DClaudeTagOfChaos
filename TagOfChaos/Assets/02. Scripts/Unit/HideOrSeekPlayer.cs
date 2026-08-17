using Photon.Pun;
using UnityEngine;

public class HideOrSeekPlayer : MonoBehaviourPunCallbacks, IPunObservable
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
    private float h, v; // 이동 입력 값 저장용 변수

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

    // 사망/대화/컷신 등 상위 시스템이 이 프로퍼티만 세팅하면 이동이 잠긴다.
    public bool IsMovementLocked { get; set; }

    // 외부에서 "이 인스턴스가 내 캐릭터인지" 판별할 수단 (GameManager의 채팅 이동잠금이 참조)
    public bool IsMine => pv != null && pv.IsMine;

private void Awake()
    {
        // Photon의 네트워크 디스패치(OnPhotonSerializeView)는 Unity의 Awake→Start 순서와 무관하게
        // 별도 루프(PhotonHandler.Dispatch())에서 호출될 수 있어, 이 오브젝트의 Start()가 아직 실행되기
        // 전에 원격 데이터 수신이 먼저 들어올 수 있다(Bug-fix-plan.md §12). networkSync는 로컬/원격
        // 두 경우 모두 OnPhotonSerializeView에서 즉시 쓰이므로, IsMine 여부와 상관없이 Awake에서
        // 가장 먼저 생성해 그 경쟁을 원천적으로 없앤다.
        networkSync = new PlayerNetworkSync();

        if (!pv.IsMine) return;

        Camera_Ctrl camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
        if (camCtrl != null)
            camCtrl.InitCamera(gameObject);
    }

    private void Start()
    {
        baseSpeed = speed;
        animator = GetComponent<Animator>();
        if (animator != null)
            animator.applyRootMotion = false; // 이동은 전부 Move()가 Rigidbody 속도를 직접 갱신하므로, 클립에 내장된 루트 모션이 겹쳐 적용되면 안 됨

        groundDetector = new PlayerGroundDetector(groundLayer, groundCheckOffset);
        animationDriver = new PlayerAnimationDriver(animator, jumpFreezeNormalizedTime);

        // 물리 엔진(Rigidbody) 도입(PlayerControllPlan.md §18.3) — 로컬 소유 캐릭터만 실제 물리
        // 시뮬레이션을 받는다. 원격 캐릭터는 지금처럼 networkSync가 transform을 직접 보간하므로,
        // Rigidbody가 동시에 중력/충돌로 같은 transform을 건드리면 두 시스템이 충돌한다 — 그래서
        // 원격 인스턴스는 isKinematic으로 물리를 꺼둔다.
        rb = GetComponent<Rigidbody>();
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
        Collider bodyMeshCollider = transform.Find("Mesh_0")?.GetComponent<Collider>();
        if (rootCollider != null && bodyMeshCollider != null)
            Physics.IgnoreCollision(rootCollider, bodyMeshCollider, true);
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
        }
    }

    // Rigidbody 조작은 물리 스텝(FixedUpdate)에서만 수행한다(Unity 관례) — 입력은 Update()에서 이미 읽어뒀다.
    private void FixedUpdate()
    {
        if (!pv.IsMine || IsMovementLocked)
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

        if (transform.position.y < -100f) // VoidKillZone 배치를 놓쳤을 때의 최후 방어선(PlayerControllPlan.md §18.4)
            RespawnToSpawnPoint();
    }

    private void CheckMovementInput()
    {
        h = Input.GetAxisRaw("Horizontal");
        v = Input.GetAxisRaw("Vertical");

        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward;
        Vector3 right = cam.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * v + right * h).normalized;

        if (moveDir != Vector3.zero)
        {
            rotation = moveDir;
            rotation_value = rotation;

            if (isDodge)
                rotation = dodgeRotation;

            if (!isJump && !isDodge) // 점프와 회피 중이 아닐 때만 이동 애니메이션 상태 변경
            {
                bool isRunning = Input.GetKey(KeyCode.LeftShift);
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
        if (Input.GetKeyDown(KeyCode.Space) && !isJump && !isDodge)
        {
            jumpRequested = true;
        }
    }

    private void CheckDodgeInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) && rotation != Vector3.zero && !isJump && !isDodge)
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
            vel = Input.GetKey(KeyCode.LeftShift) ? baseSpeed * 1.3f : baseSpeed;
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

    // 현재 회피 중인지 확인하는 메서드
    public bool IsDodge()
    {
        return animationDriver.CurrentState == PlayerMoveState.Dodge;
    }

    // 맵 밖으로 떨어졌을 때 VoidKillZone(또는 FixedUpdate의 최후 방어선)이 호출한다(PlayerControllPlan.md §18.4).
    public void RespawnToSpawnPoint()
    {
        GameObject spawnPointObj = GameObject.Find("PlayerSpawnPos");
        if (spawnPointObj == null)
            return;

        Vector3 offset = new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
        rb.linearVelocity = Vector3.zero; // 낙하 속도가 남아있으면 스폰 직후 바닥을 뚫고 지나갈 수 있음
        // Non-kinematic Rigidbody에서는 transform.position을 직접 대입해도 다음 물리 스텝에서
        // Rigidbody가 자신이 마지막으로 시뮬레이션한 위치로 되돌려버린다 — 반드시 rb.position으로
        // 물리 엔진에도 같이 알려줘야 실제로 순간이동이 반영된다(Play Mode 실측으로 확인된 문제).
        rb.position = spawnPointObj.transform.position + offset;
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
            networkSync.Write(stream, transform, animationDriver.CurrentState, isJump);
        }
        else // 원격 플레이어의 상태 정보 수신
        {
            networkSync.Read(stream, transform);
        }
    }
}
