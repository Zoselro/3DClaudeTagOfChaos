using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 마녀 연출 카메라(Request1009Plan.md §18.4, §19.2 → §23.5 X5). 장면은 시각표가 정한다(WitchTimeline.ShotAt) — 모두가 같은 순간 같은 장면.
//   타임어택: 로켓(3초) → 등진 마녀 전신으로(3초) → 전신을 본 채 뒤돌아섬(2초) → 얼굴 클로즈업(2초) → 내 시점으로(3초)
//             … 타임어택 동안 각자 플레이 … 끝 1.5초 전 마녀 어깨 너머로 → 내리침·박살을 보다가 결과 화면
//   처단:     떠나는 탈출구(3초) → 얼굴(3초) → 맵 가장자리에서 다가오는 마녀를 올려다봄(10초) → 도착 1.5초 전 어깨 너머로 → 내리침
// Main Camera에 붙고, 다른 카메라 스크립트(3인칭 Camera_Ctrl·괴물 1인칭·관전)가 이번 프레임 위치를 정한 뒤에 실행해(DefaultExecutionOrder)
// 그 위치(내 시점)와 마녀 쪽 자리를 섞는다 — 내 시점은 계속 계산되므로 돌아오는 곳이 정확하다. 장면이 있는 동안 조작은 멈춘다
// (PlayerInput.CutsceneActive, 타임어택은 그만큼 길다 — WitchTimeline.TimeAttackEnd). 공통 시계라 늦게 들어온 사람은 지난 부분을 건너뛴다.
[DefaultExecutionOrder(1000)]
public class WitchRevealCamera : MonoBehaviour
{
    public const float FaceDistanceRatio = 0.28f;  // 얼굴 앞 거리 = 마녀 키 × 비율(키 90 m → 25 m, 모자 챙까지 얼굴이 화면을 채움)
    public const float FaceRiseRatio = 0.01f;      // 눈높이를 살짝 올려 모자 챙 아래 얼굴이 보이게
    public const float LeadDistance = 14f;         // 앞 샷: 대상(로켓·탈것)에서 떨어진 거리(m)
    public const float LeadHeight = 4f;            // 앞 샷: 대상 바닥보다 높이(m)
    public const float LeadFollowUp = 0.35f;       // 앞 샷: 대상이 떠오르면 카메라도 그 비율만큼 따라 오른다(로켓)
    public const float FullBodyDistanceRatio = 1.25f, FullBodyHeightRatio = 0.25f, FullBodyLookRatio = 0.5f; // 전신: 맵 쪽에서 몸 전체가 화면에
    public const float ShoulderBackRatio = 0.3f, ShoulderUpRatio = 1.25f, ShoulderSideRatio = 0.6f, ShoulderLookAheadRatio = 0.9f; // 어깨 너머(모자가 맵을 가리지 않게 옆으로)
    public const float ApproachInset = 14f;        // 다가옴: Ground 경계에서 맵 안쪽으로(m)
    public const float ApproachEyeHeight = 2.2f;   //   쿠키 눈높이쯤에서 올려다본다
    public const float ApproachMoveSeconds = 1.5f; //   얼굴에서 그 자리로 옮겨 가는 시간
    private const float StrikeShakeScale = 4f;     // 멀리서 보는 장면이라 주먹 충격 흔들림을 크게(CameraShake)

    private bool locked;
    private bool applied;                   // 지난 프레임에 내가 위치를 바꿨는지
    private Vector3 appliedPos, ownPos;     // 아래 카메라가 이번 프레임에 위치를 다시 정하지 않았으면(쉬는 카메라) 지난 내 시점을 쓴다
    private Quaternion appliedRot, ownRot;
    private double leadKey = double.NaN;    // 앞 샷 기준(시작 시각) — 바뀌면 자리를 다시 잡는다
    private Vector3 leadAnchor, leadDir;
    private double approachKey = double.NaN; // 다가옴 장면 높이를 정한 판(등장 시각)
    private float approachHeight = ApproachEyeHeight;

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

    // 전신 장면: 맵 쪽(towardMap)에서 마녀 몸 전체(발~모자)가 화면에 들어오는 거리, 약간 아래에서 올려다본다.
    // 타임어택에서는 마녀가 맵을 등지고 있어 등이 보이다가, 돌아서면 얼굴이 이 카메라를 향한다(§23.4 Q1).
    public static void FullBodyPose(Vector3 feet, Vector3 towardMap, float height, out Vector3 position, out Quaternion rotation)
    {
        Vector3 dir = Flat(towardMap);
        position = feet + dir * (height * FullBodyDistanceRatio) + Vector3.up * (height * FullBodyHeightRatio);
        rotation = Quaternion.LookRotation(feet + Vector3.up * (height * FullBodyLookRatio) - position, Vector3.up);
    }

    // 어깨 너머 장면(§23.4 Q2): 마녀 뒤·위에서 맵 쪽 땅을 내려다본다 — 내리치는 두 팔과 박살 나는 맵이 한 화면.
    public static void ShoulderPose(Vector3 feet, Vector3 towardMap, float height, out Vector3 position, out Quaternion rotation)
    {
        Vector3 dir = Flat(towardMap);
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        position = feet - dir * (height * ShoulderBackRatio) + Vector3.up * (height * ShoulderUpRatio) + side * (height * ShoulderSideRatio);
        rotation = Quaternion.LookRotation(feet + dir * (height * ShoulderLookAheadRatio) - position, Vector3.up);
    }

    // 다가옴 장면(§23.4 Q3): 마녀가 닿을 Ground 경계에서 맵 안쪽 낮은 자리에서, 다가오는 마녀 얼굴을 올려다본다.
    // eyeHeight: 맵 가장자리 벽·구조물이 마녀를 가리면 더 높인다(ApproachEyeHeightFor).
    public static void ApproachPose(Vector3 groundContact, Vector3 mapCenter, Vector3 face, out Vector3 position, out Quaternion rotation,
        float eyeHeight = ApproachEyeHeight)
    {
        Vector3 inward = Flat(mapCenter - groundContact);
        position = groundContact + inward * ApproachInset + Vector3.up * eyeHeight;
        rotation = Quaternion.LookRotation(face - position, Vector3.up);
    }

    private static readonly float[] ApproachHeights = { ApproachEyeHeight, 8f, 15f, 25f, 35f, 50f, 70f };

    // 다가옴 장면 높이: 걸어오는 동안(먼 곳 ~ 도착) 마녀 얼굴까지 시야가 트이는 가장 낮은 높이. 맵마다 가장자리 구조가 달라(공장은 높은 벽)
    // 씬 충돌체로 확인하되, 보이지 않는 Ground 경계벽(쿠키를 막는 벽)은 뺀다.
    public static float ApproachEyeHeightFor(Vector3 groundContact, Vector3 mapCenter, IList<Vector3> faces, Transform ignoreRoot)
    {
        foreach (float h in ApproachHeights)
        {
            bool clear = true;
            foreach (Vector3 face in faces)
            {
                ApproachPose(groundContact, mapCenter, face, out Vector3 pos, out _, h);
                if (LineBlocked(pos, face, ignoreRoot)) { clear = false; break; }
            }
            if (clear) return h;
        }
        return ApproachHeights[ApproachHeights.Length - 1];
    }

    private static readonly RaycastHit[] occlusionHits = new RaycastHit[32];

    public static bool LineBlocked(Vector3 from, Vector3 to, Transform ignoreRoot)
    {
        Vector3 d = to - from;
        int n = Physics.RaycastNonAlloc(from, d.normalized, occlusionHits, d.magnitude - 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Transform t = occlusionHits[i].collider.transform;
            if (ignoreRoot != null && t.IsChildOf(ignoreRoot)) continue;
            if (t.GetComponentInParent<IGameCharacter>() != null) continue;
            return true;
        }
        return false;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v = Vector3.ProjectOnPlane(v, Vector3.up);
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private static float Smooth(double t, double seconds) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)(t / seconds)));

    // 지금 장면(공통 시계·방 속성).
    public static WitchShot CurrentShot(out double appearAt, out double leadStart, out bool hasLead, out double walkEnd)
    {
        WitchMode mode = WitchTimeline.Mode;
        leadStart = 0; walkEnd = 0; hasLead = false;
        if (mode == WitchMode.None || !WitchTimeline.TryGetAppearAt(out appearAt)) { appearAt = 0; return WitchShot.None; }
        hasLead = WitchTimeline.TryGetLeadInStart(mode, out leadStart);
        walkEnd = WitchTimeline.WalkEnd(mode, appearAt);
        return WitchTimeline.ShotAt(mode, PhotonNetwork.Time, hasLead, leadStart, appearAt, walkEnd, RoomState.TryGetInt(NetKeys.GameResult, out _));
    }

    private void LateUpdate()
    {
        WitchPresenter witch = WitchPresenter.Current;
        Vector3 basePos = applied && transform.position == appliedPos ? ownPos : transform.position;
        Quaternion baseRot = applied && transform.rotation == appliedRot ? ownRot : transform.rotation;

        WitchMode mode = WitchTimeline.Mode;
        WitchShot shot = CurrentShot(out double appearAt, out double leadStart, out _, out double walkEnd);
        if (witch == null || !WitchTimeline.Watches(mode)) shot = WitchShot.None;
        if (shot != WitchShot.None && shot != WitchShot.Lead && !witch.IsShown) shot = WitchShot.None; // 마녀가 아직 이 화면에 없다
        SetLocked(shot != WitchShot.None);
        if (shot == WitchShot.None)
        {
            if (applied) transform.SetPositionAndRotation(basePos, baseRot); // 끝난 프레임에 내 시점으로 확실히 되돌린다
            applied = false;
            return;
        }

        ownPos = basePos; ownRot = baseRot;
        double now = PhotonNetwork.Time;

        // 앞 샷 자리: 대상 바닥과, 내 카메라가 있던 쪽 방향을 한 번 정해 둔다
        Transform focus = LeadFocus(mode);
        Vector3 leadPos = basePos; Quaternion leadRot = baseRot;
        if (focus != null)
        {
            if (leadKey != leadStart) { leadKey = leadStart; leadAnchor = focus.position; leadDir = basePos - focus.position; }
            LeadPose(leadAnchor, leadDir, focus.position, out leadPos, out leadRot);
        }

        double t = now - appearAt;
        Vector3 pos; Quaternion rot;
        switch (shot)
        {
            case WitchShot.Lead:
            {
                float l = LeadWeight((float)(now - leadStart));
                pos = Vector3.Lerp(basePos, leadPos, l); rot = Quaternion.Slerp(baseRot, leadRot, l);
                break;
            }
            case WitchShot.Reveal:
            {
                float w = Smooth(t, WitchTimeline.RevealSeconds);
                Vector3 fromPos = focus != null ? leadPos : basePos; Quaternion fromRot = focus != null ? leadRot : baseRot;
                if (mode == WitchMode.Purge) BlendPose(fromPos, fromRot, witch.FacePoint, witch.FaceForward, witch.Height, w, out pos, out rot);
                else
                {
                    FullBodyPose(witch.FeetPosition, witch.TowardMap, witch.Height, out Vector3 bodyPos, out Quaternion bodyRot);
                    pos = Vector3.Lerp(fromPos, bodyPos, w); rot = Quaternion.Slerp(fromRot, bodyRot, w);
                }
                break;
            }
            case WitchShot.Turn:
                FullBodyPose(witch.FeetPosition, witch.TowardMap, witch.Height, out pos, out rot);
                break;
            case WitchShot.Face:
            {
                FullBodyPose(witch.FeetPosition, witch.TowardMap, witch.Height, out Vector3 bodyPos, out Quaternion bodyRot);
                CloseUpPose(witch.FacePoint, witch.FaceForward, witch.Height, out Vector3 facePos, out Quaternion faceRot);
                float k = Smooth(t - WitchTimeline.RevealSeconds - WitchTimeline.TurnSeconds, WitchTimeline.FaceMoveSeconds);
                pos = Vector3.Lerp(bodyPos, facePos, k); rot = Quaternion.Slerp(bodyRot, faceRot, k);
                break;
            }
            case WitchShot.Return:
            {
                double since = t - WitchTimeline.RevealSeconds - WitchTimeline.TurnSeconds - WitchTimeline.FaceSeconds;
                BlendPose(basePos, baseRot, witch.FacePoint, witch.FaceForward, witch.Height, 1f - Smooth(since, WitchTimeline.ReturnSeconds), out pos, out rot);
                break;
            }
            case WitchShot.Approach:
            {
                CloseUpPose(witch.FacePoint, witch.FaceForward, witch.Height, out Vector3 facePos, out Quaternion faceRot);
                ApproachPose(witch.GroundContact, witch.MapCenter, witch.FacePoint, out Vector3 apPos, out Quaternion apRot, ApproachHeight(witch, appearAt));
                float k = Smooth(t - WitchTimeline.RevealSeconds, ApproachMoveSeconds);
                pos = Vector3.Lerp(facePos, apPos, k); rot = Quaternion.Slerp(faceRot, apRot, k);
                break;
            }
            default: // Strike
            {
                Vector3 fromPos = basePos; Quaternion fromRot = baseRot;
                if (mode == WitchMode.Purge) ApproachPose(witch.GroundContact, witch.MapCenter, witch.FacePoint, out fromPos, out fromRot, ApproachHeight(witch, appearAt));
                ShoulderPose(witch.FeetPosition, witch.TowardMap, witch.Height, out Vector3 shPos, out Quaternion shRot);
                float k = Smooth(now - (walkEnd - WitchTimeline.StrikeShotMoveSeconds), WitchTimeline.StrikeShotMoveSeconds);
                pos = Vector3.Lerp(fromPos, shPos, k) + CameraShake.Offset * StrikeShakeScale; // 다른 카메라처럼 위치를 정한 뒤 흔들림을 더한다
                rot = Quaternion.Slerp(fromRot, shRot, k);
                break;
            }
        }
        appliedPos = pos; appliedRot = rot;
        transform.SetPositionAndRotation(appliedPos, appliedRot);
        applied = true;
    }

    private float ApproachHeight(WitchPresenter witch, double appearAt)
    {
        if (approachKey == appearAt) return approachHeight;
        approachKey = appearAt;
        var faces = new List<Vector3>();
        for (int i = 0; i <= 3; i++)
            faces.Add(Vector3.Lerp(witch.StartPoint, witch.StrikePoint, i / 3f) + Vector3.up * (witch.Height * 0.8f));
        approachHeight = ApproachEyeHeightFor(witch.GroundContact, witch.MapCenter, faces, WitchPath.FindGround(witch.gameObject.scene));
        return approachHeight;
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
