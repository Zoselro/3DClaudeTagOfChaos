using Photon.Pun;
using UnityEngine;

// Monster 프리팹에 부착. 괴물이 조준한 쿠키(스파이 포함)를 상호작용 키(E)로 잡는다(GameFixPlan.md F1).
// 예전에는 몸 주변 구 트리거에 쿠키가 닿으면 자동으로 처형했지만, 두 쿠키가 겹쳐 있을 때 누구를 잡을지 괴물이
// 고를 수 있도록 "화면 가운데 조준 + E"로 바꿨다. 촉수 돌진도 더 이상 처형하지 않고 쿠키 앞에서 멈추기만 한다.
// E키 입력은 CharacterInteractor가 받아 문 등 다른 상호작용보다 먼저 여기로 넘긴다(F1-1).
[RequireComponent(typeof(Collider))]
public class MonsterGrabKillTrigger : MonoBehaviour
{
    [SerializeField] private PhotonView monsterPv;
    [SerializeField] private MonsterController monsterController; // GrabKill 애니메이션 트리거용
    [Tooltip("괴물 중심에서 몸 앞면까지의 거리(m). MonsterPlayer 프리팹 측정값 약 1.87m. 잡기 거리는 몸 앞면부터 잰다.")]
    [SerializeField, Min(0f)] private float bodyFrontOffset = 1.87f;

    // 쿨다운은 처형 즉시 시작해 GrabKill 애니메이션 재생이 끝나는 시점까지 지속된다(사용자 확인,
    // GameRule.md v3.5) — ResetTrigger()는 MonsterController가 재생 시간 경과 후 호출한다.
    private bool onCooldown;
    private readonly RaycastHit[] aimHits = new RaycastHit[16];

    public bool IsOnCooldown => onCooldown;
    public float BodyFrontOffset => bodyFrontOffset;

    // 지금 조준 중인 잡을 수 있는 쿠키(괴물 소유 클라이언트에서만 계산, 없으면 null). 조준 표시 UI가 읽는다.
    public HideOrSeekPlayer AimTarget { get; private set; }

    private void Update()
    {
        if (monsterPv == null || !monsterPv.IsMine) return;
        AimTarget = onCooldown || !monsterController.CanInteract ? null : FindAimTarget();
    }

    // 화면 가운데에서 앞으로 구를 쏘아, 벽보다 가깝고 몸 앞면에서 잡기 거리 안에 있는 가장 가까운 쿠키를 고른다.
    // 3인칭이면 카메라가 괴물 뒤에 있으므로 최대 거리에 카메라~괴물 거리를 더한다.
    private HideOrSeekPlayer FindAimTarget()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        GameSettingsSO settings = GameSettings.Current;
        Transform self = monsterPv.transform;
        float reachFromCenter = bodyFrontOffset + settings.GrabReachFromFront;
        float cameraToMonster = Vector3.Distance(cam.transform.position, self.position);
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        int count = Physics.SphereCastNonAlloc(ray, settings.GrabAimRadius, aimHits, cameraToMonster + reachFromCenter,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        // 벽 등 막는 물체가 있으면 그 뒤의 쿠키는 잡을 수 없다.
        float blockDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider c = aimHits[i].collider;
            if (c.transform.IsChildOf(self) || c.GetComponentInParent<HideOrSeekPlayer>() != null) continue;
            if (aimHits[i].distance > 0f) blockDistance = Mathf.Min(blockDistance, aimHits[i].distance);
        }

        HideOrSeekPlayer best = null;
        float bestDistance = float.MaxValue;
        Vector3 center = self.position;
        for (int i = 0; i < count; i++)
        {
            var cookie = aimHits[i].collider.GetComponentInParent<HideOrSeekPlayer>();
            if (cookie == null || aimHits[i].distance >= bestDistance || aimHits[i].distance > blockDistance) continue;
            if (!IsCatchable(cookie)) continue;

            Vector3 toCookie = cookie.transform.position - center;
            toCookie.y = 0f;
            if (toCookie.magnitude > reachFromCenter) continue; // 몸 앞면에서 잡기 거리 밖

            best = cookie;
            bestDistance = aimHits[i].distance;
        }
        return best;
    }

    private static bool IsCatchable(HideOrSeekPlayer cookie)
    {
        PhotonView view = cookie.View;
        return view != null && view.Owner != null && !RoomState.IsBroken(view.Owner) && !cookie.IsBroken;
    }

    // 상호작용 키로 조준 대상을 잡는다. 잡기를 시작했으면 true(CharacterInteractor가 문 등보다 먼저 호출, F1-1).
    public bool TryGrabAimTarget()
    {
        HideOrSeekPlayer target = AimTarget;
        if (target == null || !monsterController.CanInteract) return false;
        return TryGrabKill(target);
    }

    // 처형 시작의 단일 진입점. 처형을 시작했으면 true.
    public bool TryGrabKill(HideOrSeekPlayer cookie)
    {
        if (onCooldown || cookie == null) return false;
        if (!monsterPv.IsMine) return false; // 판정 시도는 괴물 소유 클라이언트만

        var cookiePv = cookie.GetComponent<PhotonView>();
        if (cookiePv == null || cookiePv.Owner == null) return false;

        // 이미 파괴된 쿠키(렌더러만 꺼진 시체)가 쿨다운을 소모하지 않도록 제외한다(research.md §8.11).
        if (RoomState.IsBroken(cookiePv.Owner)) return false;

        onCooldown = true;
        monsterController.PlayGrabKill(); // 상태 동기화로 전원에게 GrabKill 애니메이션 전파(진행 중인 돌진은 여기서 끊긴다)

        // 전원에게 보낸다: 모든 클라이언트가 쿠키를 이 괴물의 Grab_Socket에 붙여 처형 연출을 보여야 하므로 괴물 ViewID를 함께 넘긴다.
        // 파괴 확정(HitCount)은 여전히 피해자 본인 클라이언트만 한다(소유권 원칙, Bug-fix-plan.md §36).
        cookiePv.RPC(HideOrSeekPlayer.RpcRequestGrabKill, RpcTarget.All, monsterPv.ViewID);

        // 잡은 횟수(결과 화면, EscapePlan.md §1.3). 괴물 본인이 자기 값만 쓴다.
        int catches = RoomState.TryGetPlayerInt(PhotonNetwork.LocalPlayer, NetKeys.CatchCount, out int c) ? c : 0;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { NetKeys.CatchCount, catches + 1 } });
        return true;
    }

    // GrabKill 애니메이션 재생이 끝나는 시점에 MonsterController가 호출한다(확정, GameRule.md v3.5).
    public void ResetTrigger() => onCooldown = false;
}
