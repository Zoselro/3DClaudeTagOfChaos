using Photon.Pun;
using UnityEngine;

// 마녀 등장 카메라 연출(Request1009Plan.md §18.4, §19.2). 앞 샷 → 마녀 클로즈업:
//   앞 샷  로켓이 날아감(타임어택, 모든 화면) 또는 쿠키 탈것이 떠남(처단, 스파이·괴물 화면) — 2초에 걸쳐 그쪽을 보러 가서 마녀가 나타날 때까지
//   0~3초  (앞 샷 자리 →) 마녀 얼굴 정면 클로즈업으로 부드럽게 이동 — 시각 0 = WitchAppearAt
//   3~4초  얼굴 정면을 본 채 멈춤
//   4~7초  내 시점으로 부드럽게 돌아옴 — 처단은 대신 4~5.5초에 마녀 몸 전체와 주먹이 떨어질 땅이 보이는 자리로 옮겨 가
//          그 자리에서 내리치는 모습(주먹·흔들림)을 보여 주고 결과가 나올 때까지 머문다(Request1009Plan.md §19.11)
// Main Camera에 붙고, 다른 카메라 스크립트(3인칭 Camera_Ctrl·괴물 1인칭·관전)가 이번 프레임 위치를 정한 뒤에 실행해(DefaultExecutionOrder)
// 그 위치와 마녀 쪽 위치를 섞는다 — 아래의 내 시점은 계속 계산되므로 돌아오는 곳이 정확하다. 연출 동안 조작은 멈춘다
// (PlayerInput.CutsceneActive, 타임어택은 그만큼 길다 — EscapeAuthority). 시각은 공통 시계라 늦게 들어온 사람은 지난 부분을 건너뛴다.
[DefaultExecutionOrder(1000)]
public class WitchRevealCamera : MonoBehaviour
{
    public const float MoveInSeconds = 3f;
    public const float HoldSeconds = 1f;
    public const float MoveOutSeconds = 3f;
    public const float TotalSeconds = MoveInSeconds + HoldSeconds + MoveOutSeconds;
    public const float FaceDistanceRatio = 0.2f;   // 얼굴 앞 거리 = 마녀 키 × 비율(키 90 m → 18 m, 얼굴이 화면을 채움)
    public const float FaceRiseRatio = 0.01f;      // 눈높이를 살짝 올려 모자 챙 아래 얼굴이 보이게
    public const float LeadDistance = 14f;         // 앞 샷: 대상(로켓·탈것)에서 떨어진 거리(m)
    public const float LeadHeight = 4f;            // 앞 샷: 대상 바닥보다 높이(m)
    public const float LeadFollowUp = 0.35f;       // 앞 샷: 대상이 떠오르면 카메라도 그 비율만큼 따라 오른다(로켓)
    public const float StrikeShotMoveSeconds = 1.5f; // 처단: 얼굴 클로즈업에서 내리치는 장면으로 옮겨 가는 시간
    public const float StrikeShotMaxSeconds = 20f;   // 처단 장면에 머무는 최대 시간(결과가 오지 않을 때의 안전장치)
    private const float StrikeDistanceRatio = 0.8f, StrikeHeightRatio = 0.8f, StrikeSideRatio = 0.8f; // 마녀 키 × 비율: 45도 옆 위에서 내려다봐 앞 나무에 가리지 않고, 숙여 내리치는 팔이 옆모습으로 보인다(머리~주먹 바닥 한 화면)
    private const float StrikeLookAheadRatio = 0.25f, StrikeLookHeightRatio = 0.54f;
    private const float StrikeShakeScale = 5f; // 멀리서 보는 장면이라 주먹 충격 흔들림을 크게(CameraShake)

    private bool locked;
    private bool applied;                   // 지난 프레임에 내가 위치를 바꿨는지
    private Vector3 appliedPos, ownPos;     // 아래 카메라가 이번 프레임에 위치를 다시 정하지 않았으면(쉬는 카메라) 지난 내 시점을 쓴다
    private Quaternion appliedRot, ownRot;
    private double leadKey = double.NaN;    // 앞 샷 기준(시작 시각) — 바뀌면 자리를 다시 잡는다
    private Vector3 leadAnchor, leadDir;

    // 연출 진행 시각(초) → 마녀 쪽으로 간 정도(0 = 내 시점, 1 = 클로즈업). 순수 계산.
    public static float Weight(float t)
    {
        if (t < 0f || t >= TotalSeconds) return 0f;
        if (t < MoveInSeconds) return Mathf.SmoothStep(0f, 1f, t / MoveInSeconds);
        if (t < MoveInSeconds + HoldSeconds) return 1f;
        return Mathf.SmoothStep(1f, 0f, (t - MoveInSeconds - HoldSeconds) / MoveOutSeconds);
    }

    // 클로즈업 카메라 자리: 얼굴 앞(얼굴이 보는 방향) distance만큼, 얼굴을 바라본다.
    public static void CloseUpPose(Vector3 face, Vector3 faceForward, float height, out Vector3 position, out Quaternion rotation)
    {
        Vector3 forward = Vector3.ProjectOnPlane(faceForward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();
        position = face + forward * (height * FaceDistanceRatio) + Vector3.up * (height * FaceRiseRatio);
        rotation = Quaternion.LookRotation(face - position, Vector3.up);
    }

    // 내 시점과 클로즈업 사이 자리. 고개(바라보는 곳)는 위치보다 먼저 얼굴로 돌아가 이동 중에도 마녀가 화면에 있고,
    // 얼굴까지 거리는 클로즈업 거리보다 가까워지지 않는다(몸을 뚫고 지나가지 않게). w = 1이면 CloseUpPose와 같다.
    public static void BlendPose(Vector3 ownPos, Quaternion ownRot, Vector3 face, Vector3 faceForward, float height, float w, out Vector3 position, out Quaternion rotation)
    {
        CloseUpPose(face, faceForward, height, out Vector3 closePos, out Quaternion closeRot);
        position = Vector3.Lerp(ownPos, closePos, w);
        Vector3 toCam = position - face;
        float minDistance = (closePos - face).magnitude;
        if (toCam.sqrMagnitude < minDistance * minDistance)
            position = face + (toCam.sqrMagnitude > 1e-4f ? toCam.normalized : closePos - face) * minDistance;
        Quaternion look = w >= 1f ? closeRot : Quaternion.LookRotation(face - position, Vector3.up);
        rotation = Quaternion.Slerp(ownRot, look, Mathf.Clamp01(w * 2f));
    }

    // 앞 샷으로 옮겨 간 정도(앞 샷 시작부터 초): LeadInSeconds 동안 0 → 1. 순수 계산.
    public static float LeadWeight(float t) => t < 0f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / WitchTimeline.LeadInSeconds));

    // 앞 샷 카메라 자리: 대상 바닥(anchor)에서 dir 쪽으로 LeadDistance, 위로 LeadHeight(+ 떠오른 만큼 일부), 대상을 바라본다.
    public static void LeadPose(Vector3 anchor, Vector3 dir, Vector3 target, out Vector3 position, out Quaternion rotation)
    {
        dir = Vector3.ProjectOnPlane(dir, Vector3.up);
        if (dir.sqrMagnitude < 1e-4f) dir = Vector3.back;
        dir.Normalize();
        float rise = Mathf.Max(0f, target.y - anchor.y);
        position = anchor + dir * LeadDistance + Vector3.up * (LeadHeight + rise * LeadFollowUp);
        rotation = Quaternion.LookRotation(target + Vector3.up - position, Vector3.up);
    }

    // 처단 내리침 장면: 마녀 앞(얼굴이 보는 쪽) 공중에서 살짝 비껴, 마녀 몸 전체와 주먹이 떨어질 앞쪽 땅을 함께 본다.
    public static void StrikePose(Vector3 feet, Vector3 faceForward, float height, out Vector3 position, out Quaternion rotation)
    {
        Vector3 forward = Vector3.ProjectOnPlane(faceForward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        position = feet + forward * (height * StrikeDistanceRatio) + Vector3.up * (height * StrikeHeightRatio) + side * (height * StrikeSideRatio);
        Vector3 look = feet + forward * (height * StrikeLookAheadRatio) + Vector3.up * (height * StrikeLookHeightRatio);
        rotation = Quaternion.LookRotation(look - position, Vector3.up);
    }

    // 지금 마녀 클로즈업 진행 시각(WitchAppearAt부터 초). 연출이 없으면 false.
    public static bool TryElapsed(out float t)
    {
        t = -1f;
        if (WitchTimeline.Mode == WitchMode.None || !WitchTimeline.TryGetAppearAt(out double at)) return false;
        t = (float)(PhotonNetwork.Time - at);
        return true;
    }

    private void LateUpdate()
    {
        WitchPresenter witch = WitchPresenter.Current;
        Vector3 basePos = applied && transform.position == appliedPos ? ownPos : transform.position;
        Quaternion baseRot = applied && transform.rotation == appliedRot ? ownRot : transform.rotation;

        WitchMode mode = WitchTimeline.Mode;
        bool watching = witch != null && WitchTimeline.Watches(mode, RoomState.IsLocalSpy(), RoomState.IsLocalMonster()) && TryElapsed(out _);
        float tw = 0f, tl = -1f;
        Transform focus = null;
        if (watching)
        {
            TryElapsed(out tw);
            if (WitchTimeline.TryGetLeadInStart(mode, out double leadStart))
            {
                tl = (float)(PhotonNetwork.Time - leadStart);
                focus = LeadFocus(mode);
                if (focus != null && leadKey != leadStart) // 앞 샷 자리: 대상 바닥과, 내 카메라가 있던 쪽 방향을 한 번 정해 둔다
                {
                    leadKey = leadStart;
                    leadAnchor = focus.position;
                    leadDir = basePos - focus.position;
                }
            }
        }

        bool inLead = watching && focus != null && tw < 0f && tl >= 0f;
        // 처단: 얼굴 클로즈업 뒤 내리치는 장면에 결과가 나올 때까지 머문다 / 타임어택: 7초 뒤 내 시점으로
        bool purgeStrikeShot = watching && mode == WitchMode.Purge && tw >= MoveInSeconds + HoldSeconds && tw < StrikeShotMaxSeconds
                               && !RoomState.TryGetInt(NetKeys.GameResult, out _);
        bool inWitch = watching && witch.IsShown && tw >= 0f && (tw < TotalSeconds || purgeStrikeShot);
        if (mode == WitchMode.Purge && tw >= MoveInSeconds + HoldSeconds && !purgeStrikeShot) inWitch = false;
        SetLocked(inLead || inWitch);
        if (!inLead && !inWitch)
        {
            if (applied) transform.SetPositionAndRotation(basePos, baseRot); // 끝난 프레임에 내 시점으로 확실히 되돌린다
            applied = false;
            return;
        }

        ownPos = basePos; ownRot = baseRot;
        Vector3 leadPos = basePos; Quaternion leadRot = baseRot;
        if (focus != null) LeadPose(leadAnchor, leadDir, focus.position, out leadPos, out leadRot);
        if (inLead)
        {
            float l = LeadWeight(tl);
            appliedPos = Vector3.Lerp(basePos, leadPos, l);
            appliedRot = Quaternion.Slerp(baseRot, leadRot, l);
        }
        else if (purgeStrikeShot)
        {
            // 얼굴 클로즈업 → 내리치는 장면(마녀 몸 전체 + 주먹이 떨어질 땅)
            CloseUpPose(witch.FacePoint, witch.FaceForward, witch.Height, out Vector3 facePos, out Quaternion faceRot);
            StrikePose(witch.transform.position, witch.FaceForward, witch.Height, out Vector3 strikePos, out Quaternion strikeRot);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((tw - MoveInSeconds - HoldSeconds) / StrikeShotMoveSeconds));
            appliedPos = Vector3.Lerp(facePos, strikePos, k) + CameraShake.Offset * StrikeShakeScale; // 다른 카메라처럼 위치를 정한 뒤 흔들림을 더한다
            appliedRot = Quaternion.Slerp(faceRot, strikeRot, k);
        }
        else
        {
            // 들어올 때는 앞 샷 자리에서(앞 샷이 있으면), 나갈 때는 내 시점으로
            bool fromLead = focus != null && tw < MoveInSeconds;
            BlendPose(fromLead ? leadPos : basePos, fromLead ? leadRot : baseRot, witch.FacePoint, witch.FaceForward, witch.Height, Weight(tw), out appliedPos, out appliedRot);
        }
        transform.SetPositionAndRotation(appliedPos, appliedRot);
        applied = true;
    }

    // 앞 샷 대상: 스파이 로켓(타임어택) 또는 쿠키 탈것(처단 — 맵 연출이 따라 볼 곳).
    private static Transform LeadFocus(WitchMode mode)
    {
        EscapeManager manager = EscapeManager.Instance;
        if (manager == null) return null;
        if (mode == WitchMode.TimeAttack) return manager.Rocket != null ? manager.Rocket.transform : null;
        return manager.Sequence != null ? manager.Sequence.DepartureFocus : manager.Device != null ? manager.Device.transform : null;
    }

    private void SetLocked(bool value)
    {
        if (locked == value) return;
        locked = value;
        PlayerInput.CutsceneActive = value;
    }

    private void OnDisable() => SetLocked(false);
}

