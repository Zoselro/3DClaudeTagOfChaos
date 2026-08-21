using Photon.Pun;
using UnityEngine;

// 괴물 전용 이동+시점 회전 컨트롤러(GameRule.md §6.2). HideOrSeekPlayer를 재사용하지 않는 이유:
// HideOrSeekPlayer.CheckMovementInput()은 Camera.main 기준 "카메라 상대 이동"(3인칭 궤도 카메라
// 전제)인데, 1인칭은 반대로 카메라가 곧 캐릭터 정면이어야 하므로 이동 입력 처리 자체가 근본적으로
// 다르다.
//
// 이동 방식은 Rigidbody 물리 기반으로 확정됨(사용자 확인, GameRule.md v3.4) — HideOrSeekPlayer.cs와
// 동일한 패턴(로컬만 실제 물리 시뮬레이션, 원격은 isKinematic)을 그대로 따른다.
public class MonsterController : MonoBehaviourPunCallbacks, IPunObservable

{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Transform eyeSocket;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float speed = 4f;
    [SerializeField] private LayerMask obstructionMask;
    [SerializeField] private MonsterGrabKillTrigger grabKillTrigger;
    [SerializeField] private Animator animator;

    private float yaw, pitch;
    private Rigidbody rb;
    private Vector3 moveInput; // Update()에서 입력만 기록, 실제 적용은 FixedUpdate에서
    private readonly MonsterTentacleDash tentacleDash = new MonsterTentacleDash();
    private readonly MonsterNetworkSync networkSync = new MonsterNetworkSync();
    private MonsterMoveState currentState = MonsterMoveState.Idle;
    private MonsterMoveState previousState = MonsterMoveState.Idle;

    private void Awake()
    {
        if (!pv.IsMine) return;

        // HideOrSeekPlayer.Awake()가 Camera_Ctrl에 하던 것과 완전히 동일한 "Main Camera에 나를 넘긴다" 패턴.
        var fpsCam = Camera.main != null ? Camera.main.GetComponent<MonsterFirstPersonCamera>() : null;
        fpsCam?.InitCamera(eyeSocket);
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
            rb.constraints = RigidbodyConstraints.FreezeRotation; // 회전은 Update()에서 transform.rotation으로 직접 제어
        }
    }

    private void Update()
    {
        if (!pv.IsMine)
        {
            networkSync.Interpolate(transform, Time.deltaTime);
            ChangeState(networkSync.RemoteState);
            return;
        }

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -80f, 80f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (eyeSocket != null) eyeSocket.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        moveInput = (transform.forward * v + transform.right * h).normalized;

        tentacleDash.TickCooldown(Time.deltaTime);

        // 입력 키는 좌Shift로 확정(사용자 확인, GameRule.md v3.4) — 추후 키 설정 UI로 이관 예정.
        // 쿠키의 Shift(질주)와는 서로 다른 캐릭터 클래스에서 각자 로컬로 읽는 입력이라 충돌 없음.
        if (Input.GetKeyDown(KeyCode.LeftShift) && tentacleDash.TryStartDash(transform.forward, transform.position, obstructionMask))
            ChangeState(MonsterMoveState.TentacleDash);
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
            rb.MovePosition(rb.position + tentacleDash.TickDash(Time.deltaTime));
        }
        else
        {
            Vector3 horizontal = moveInput * speed;
            rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
        }
    }

    // MonsterGrabKillTrigger가 근접 자동 처형 순간 호출 — 전원에게 GrabKill 애니메이션 재생.
    public void PlayGrabKill()
    {
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

    // GrabKill 애니메이션 재생이 끝나는 시점을 감지해 트리거 쿨다운을 재사용 가능 상태로 되돌린다
    // (확정, GameRule.md v3.5) — PlayerAnimationDriver.HandleJumpAnimationHold()가
    // AnimatorStateInfo.normalizedTime로 재생 진행도를 감지하는 것과 동일한 방식.
    private void LateUpdate()
    {
        if (!pv.IsMine || animator == null || currentState != MonsterMoveState.GrabKill) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("GrapKill") && state.normalizedTime >= 1f) // Animator State 이름은 여전히 오타(GrapKill) — 트리거 파라미터만 GrabKill로 새로 추가됨(manage_animation에 rename 액션이 없어 상태 이름은 그대로 둘)
        {
            grabKillTrigger?.ResetTrigger();
            ChangeState(MonsterMoveState.Idle);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
            networkSync.Write(stream, transform, currentState);
        else
            networkSync.Read(stream, transform);
    }
}
