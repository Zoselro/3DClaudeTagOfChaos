using UnityEngine;

// 두 손으로 드는 자세의 기준점 계산(Request1009Plan.md §16.3·§16.4). 드는 쪽 상체 "Carry" 레이어(Cookie_Carrying)는
// 두 손을 몸 앞에 모은 자세라, 들린 쿠키·두 손 아이템을 두 손 뼈의 가운데에 붙이면 걷기·달리기의 몸 흔들림도 같이 따라간다.
// 들린 쿠키는 드는 쪽 팔 위에 가로로 눕힌다(Q1 A — 머리가 드는 쪽 왼쪽, 얼굴은 위·드는 쪽을 살짝 향함).
public static class CarryPose
{
    // 손이 없는(뼈대가 다른) 모델일 때 쓰는 루트 기준 손 가운데 — Cookie_Carrying 클립 측정값(약 (0, 1.05, 0.24)).
    public static readonly Vector3 FallbackHandsLocal = new Vector3(0f, 1.05f, 0.25f);
    public const float CarriedLift = 0.06f;    // 들린 쿠키 엉덩이를 손보다 위로(팔 위에 등이 얹힌다)
    public const float CarriedForward = 0.22f; // 드는 쪽 가슴과 겹치지 않게 앞으로
    public const float FaceTowardCarrier = 0.35f;

    // 두 손 뼈의 가운데(월드). 뼈를 못 찾으면 루트 기준 측정값.
    public static Vector3 HandsCenter(Transform root, Animator animator)
    {
        if (animator != null && animator.isHuman)
        {
            Transform r = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform l = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (r != null && l != null) return (r.position + l.position) * 0.5f;
        }
        return root.TransformPoint(FallbackHandsLocal);
    }

    // 들린 쿠키의 몸 방향: 몸 축(위)이 드는 쪽 왼쪽을, 얼굴(앞)이 하늘과 드는 쪽 사이를 본다.
    public static Quaternion CarriedRotation(Quaternion carrierRotation)
    {
        Vector3 bodyUp = carrierRotation * Vector3.left;
        Vector3 face = Vector3.ProjectOnPlane(Vector3.up - carrierRotation * Vector3.forward * FaceTowardCarrier, bodyUp).normalized;
        return Quaternion.LookRotation(face, bodyUp);
    }

    // 들린 쿠키 엉덩이가 놓일 자리(월드).
    public static Vector3 CarriedHips(Vector3 handsCenter, Quaternion carrierRotation) =>
        handsCenter + Vector3.up * CarriedLift + carrierRotation * Vector3.forward * CarriedForward;

    // 엉덩이를 hipsWorld에 두려면 루트를 어디에 둘지. hipsOffsetLocal = 루트 기준 엉덩이 위치(지금 애니메이션 자세).
    public static Vector3 RootFor(Vector3 hipsWorld, Quaternion rootRotation, Vector3 hipsOffsetLocal) =>
        hipsWorld - rootRotation * hipsOffsetLocal;

    // 내려놓을 때 세우는 방향: 드는 쪽을 바라보게.
    public static Quaternion UprightFacing(Vector3 from, Vector3 to)
    {
        Vector3 d = Vector3.ProjectOnPlane(to - from, Vector3.up);
        return d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
    }
}
