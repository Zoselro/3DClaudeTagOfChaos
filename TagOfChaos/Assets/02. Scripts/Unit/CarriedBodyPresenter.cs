using Photon.Pun;
using UnityEngine;

// 다른 쿠키에게 들린 몸을 모든 화면에서 드는 쪽 두 손 위에 놓는다(Request1009Plan.md §16.3).
// 예전에는 들린 사람 화면만 FixedUpdate에서 위치를 맞추고 다른 화면은 그 위치를 다시 보간해 달릴수록 뒤로 처졌다.
// 지금은 각 화면이 자기 화면의 드는 쪽 손 위치로 애니메이션이 끝난 뒤(LateUpdate) 맞춘다 — 카메라(Camera_Ctrl.LateUpdate)보다 먼저.
[DefaultExecutionOrder(-50)]
public class CarriedBodyPresenter : MonoBehaviour
{
    private HideOrSeekPlayer self;
    private Transform hips;

    public void Init(HideOrSeekPlayer owner, Animator animator)
    {
        self = owner;
        hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
    }

    private void LateUpdate()
    {
        if (self == null) return;
        int carrier = self.CurrentCarrierViewId;
        if (carrier < 0) return;
        if (TryPose(carrier, out Vector3 position, out Quaternion rotation))
            transform.SetPositionAndRotation(position, rotation);
    }

    // 드는 쪽(carrierViewId)의 지금 손 위치로 이 몸의 루트 위치·회전을 계산한다. 드는 쪽이 사라졌으면 false.
    public bool TryPose(int carrierViewId, out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;
        PhotonView carrierView = PhotonView.Find(carrierViewId);
        if (carrierView == null) return false;
        Transform carrier = carrierView.transform;
        Vector3 hands = CarryPose.HandsCenter(carrier, carrierView.GetComponent<Animator>());
        rotation = CarryPose.CarriedRotation(carrier.rotation);
        Vector3 hipsLocal = hips != null
            ? Quaternion.Inverse(transform.rotation) * (hips.position - transform.position)
            : new Vector3(0f, 0.8f, 0f);
        position = CarryPose.RootFor(CarryPose.CarriedHips(hands, carrier.rotation), rotation, hipsLocal);
        return true;
    }
}
