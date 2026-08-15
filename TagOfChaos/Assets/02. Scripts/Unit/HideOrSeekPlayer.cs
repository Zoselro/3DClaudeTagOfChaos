using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

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
    [SerializeField] private NavMeshAgent agent;

    private float baseSpeed;
    private float h, v; // 이동 입력 값 저장용 변수

    [Header("States")]
    [SerializeField] private bool isJump;
    [SerializeField] private bool isDodge;
    [SerializeField] private bool keepMovingAfterDodge;
    [SerializeField] private bool keepMovingAfterJump;

    private Vector3 rotation;
    private Vector3 rotation_value;
    private Vector3 dodgeRotation;
    private Vector3 dodgeMoveDir;
    private Vector3 jumpMoveDir;
    private float dodgeTimer;

    private Animator animator;
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
            animator.applyRootMotion = false; // 이동은 전부 Move()가 transform.position을 직접 갱신하므로, 클립에 내장된 루트 모션이 겹쳐 적용되면 안 됨

        groundDetector = new PlayerGroundDetector(groundLayer, groundCheckOffset);
        animationDriver = new PlayerAnimationDriver(animator, jumpFreezeNormalizedTime);
    }

    private void Update()
    {
        if (IsMovementLocked)
            return;

        if (pv.IsMine) // 자신이 조종하는 캐릭터일 때만 이동 처리
        {
            ApplyGravity(); // 수동 중력 적용 및 착지 판정
            CheckMovementInput(); // 이동 입력 체크 (Move()보다 먼저 호출해 이번 프레임 입력을 바로 반영)
            Move();
            CheckJumpInput(); // 점프 입력 체크
            CheckDodgeInput(); // 회피 입력 체크
            animationDriver.HandleJumpAnimationHold(); // 착지 전까지 Jump 애니메이션이 끝까지 재생되지 않도록 고정
        }
        else // 원격지 아바타 캐릭터들은 위치, 회전, 애니메이션을 따라오게 동기화 처리
        {
            networkSync.Interpolate(transform, Time.deltaTime);
            animationDriver.ChangeState(networkSync.RemoteState);
        }
    }

    private void ApplyGravity()
    {
        if (!isJump)
            return;

        bool landed = groundDetector.Tick(transform, Time.deltaTime, out float landedHeight);

        if (!landed)
            return;

        Vector3 pos = transform.position;
        pos.y = landedHeight; // 고정된 0이 아니라 실제 지형 표면 높이로 착지
        transform.position = pos;

        isJump = false;
        keepMovingAfterJump = false;

        animationDriver.ResumePlayback(); // 공중에서 멈춰뒀던 애니메이션 재생 속도 복구

        if (agent != null)
        {
            agent.Warp(transform.position); // 에이전트 위치를 현재 착지한 곳으로 순간 이동시킴
            agent.updatePosition = true;    // 다시 바닥 고정 기능 활성화
        }
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
                bool isSneaking = Input.GetKey(KeyCode.LeftShift);
                animationDriver.ChangeState(isSneaking ? PlayerMoveState.SneakWalk : PlayerMoveState.Walk);
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
            groundDetector.StartJump(jumpPower); // 순간적인 위쪽 속도 부여
            isJump = true;
            keepMovingAfterJump = true;
            jumpMoveDir = rotation;

            if (agent != null) // NavMeshAgent가 있다면 점프 중에는 위치 업데이트를 끕니다.
            {
                agent.updatePosition = false;
            }

            animationDriver.ChangeState(PlayerMoveState.Jump);
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

    // 움직일 때
    public void Move()
    {
        Vector3 moveVector;

        if (isDodge && keepMovingAfterDodge) // 캐릭터가 회피중일 경우, 키보드에서 손을 떼더라도 회피를 시작했던 그 방향으로 강제로 밀어붙임
        {
            moveVector = new Vector3(dodgeMoveDir.x * speed, 0f, dodgeMoveDir.z * speed);
            if (dodgeMoveDir != Vector3.zero)
                transform.LookAt(transform.position + new Vector3(dodgeMoveDir.x, 0f, dodgeMoveDir.z));
        }
        else if (isJump && keepMovingAfterJump) // 점프 도중 키보드 방향을 바꿔도 방향이 바뀌지 않고 포물선을 그리며 이동
        {
            moveVector = new Vector3(jumpMoveDir.x * baseSpeed, 0f, jumpMoveDir.z * baseSpeed);
            if (jumpMoveDir != Vector3.zero)
                transform.LookAt(transform.position + new Vector3(jumpMoveDir.x, 0f, jumpMoveDir.z));
        }
        else // Shift를 눌렀을 경우 일반 스피드의 30% 속도만큼 감. 아니면 100% 속도 유지
        {
            float velocity = Input.GetKey(KeyCode.LeftShift) ? baseSpeed * 0.3f : baseSpeed;
            moveVector = new Vector3(rotation.x * velocity, 0f, rotation.z * velocity);
            if (rotation != Vector3.zero)
                transform.LookAt(transform.position + new Vector3(rotation.x, 0f, rotation.z));
        }

        // 수평 이동(X, Z)에 Y축 속도(점프/중력)를 병합하여 좌표 이동
        moveVector.y = groundDetector.YVelocity;
        transform.position += moveVector * Time.deltaTime;
    }

    // 현재 회피 중인지 확인하는 메서드
    public bool IsDodge()
    {
        return animationDriver.CurrentState == PlayerMoveState.Dodge;
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

            if (networkSync.RemoteIsJump && agent != null)
            {
                agent.updatePosition = false; // 점프 중에는 NavMeshAgent 위치 업데이트를 끔
            }
        }
    }
}
