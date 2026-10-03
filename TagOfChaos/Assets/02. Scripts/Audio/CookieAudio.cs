using UnityEngine;

// 쿠키 캐릭터 소리(SoundPlan.md S4). HideOrSeekPlayer가 Awake에서 붙이고 애니메이션 상태 변화(PlayerAnimationDriver.StateChanged)를 듣는다.
// 상태는 본인 화면에서는 입력으로, 다른 사람 화면에서는 동기화된 값으로 바뀌므로 같은 코드가 모든 화면에서 소리를 낸다.
// 내 쿠키는 2D, 남의 쿠키는 몸을 따라가는 3D(GameAudio.PlayCharacter — 거리 감쇠는 DistanceFadePlan.md).
// - 점프·착지·회피, 달리기만 발소리(걷기·숨기 이동은 조용 — D3), 다른 쿠키에게 들림·내려짐.
// - 색칠(본인만): 칠하는 동안 붓 소리 반복.
public class CookieAudio : MonoBehaviour
{
    private const float RunStepInterval = 0.3f;
    private const float GrabConfirmDelay = 0.08f; // 들림 상태가 괴물 처형인지(처형은 괴물 쪽 소리) 한 박자 뒤에 확인
    private const float PaintIdleSeconds = 0.12f; // 이만큼 스탬프가 없으면 붓 소리를 멈춘다

    private HideOrSeekPlayer cookie;
    private PlayerAnimationDriver driver;
    private CookieLifeStatePresenter life;
    private PlayerPaintCanvas canvas;
    private float nextStep;
    private float grabCheckAt = -1f;
    private AudioHandle paintLoop;

    public void Init(HideOrSeekPlayer owner, PlayerAnimationDriver animationDriver)
    {
        cookie = owner;
        driver = animationDriver;
        life = GetComponent<CookieLifeStatePresenter>();
        canvas = GetComponent<PlayerPaintCanvas>();
        driver.StateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        if (driver != null) driver.StateChanged -= OnStateChanged;
        paintLoop.Stop();
    }

    private void OnStateChanged(PlayerMoveState from, PlayerMoveState to)
    {
        if (!Audible) return;
        switch (to)
        {
            case PlayerMoveState.Jump: Play(SoundId.CookieJump); break;
            case PlayerMoveState.Dodge: Play(SoundId.CookieDodge); break;
            case PlayerMoveState.Held: grabCheckAt = Time.time + GrabConfirmDelay; break;
            case PlayerMoveState.Run: nextStep = Time.time; break;
        }
        if (from == PlayerMoveState.Jump && (to == PlayerMoveState.Idle || to == PlayerMoveState.Walk || to == PlayerMoveState.Run))
            Play(SoundId.CookieLand);
        if (from == PlayerMoveState.Held && to != PlayerMoveState.Broken && !IsGrabKilled) Play(SoundId.CookieRelease);
    }

    private void Update()
    {
        if (grabCheckAt > 0f && Time.time >= grabCheckAt)
        {
            grabCheckAt = -1f;
            if (driver.CurrentState == PlayerMoveState.Held && !IsGrabKilled && Audible) Play(SoundId.CookieGrab);
        }
        if (driver.CurrentState == PlayerMoveState.Run && Time.time >= nextStep && Audible)
        {
            nextStep = Time.time + RunStepInterval;
            Play(SoundId.CookieStep);
        }
        TickPaint();
    }

    // 본인 색칠: 스탬프가 이어지는 동안 붓 소리 반복(2D).
    private void TickPaint()
    {
        if (canvas == null || !cookie.IsMine) return;
        bool painting = Time.time - canvas.LastStampTime < PaintIdleSeconds;
        if (painting && !paintLoop.IsPlaying) paintLoop = GameAudio.StartLoop(SoundId.PaintStroke, null);
        else if (!painting && paintLoop.IsPlaying) { paintLoop.Stop(); paintLoop = default; }
    }

    private bool IsGrabKilled => life != null && life.IsBeingGrabKilled;

    // 숨겨진(탈출·탑승) 쿠키와 파괴된 쿠키는 조용하다.
    private bool Audible
    {
        get
        {
            var owner = cookie.View != null ? cookie.View.Owner : null;
            if (owner == null) return true;
            return !RoomState.HasEscaped(owner) && !EscapeManager.IsWaiting(owner.ActorNumber) && !RoomState.IsBroken(owner);
        }
    }

    private void Play(SoundId id) => GameAudio.PlayCharacter(id, cookie);
}
