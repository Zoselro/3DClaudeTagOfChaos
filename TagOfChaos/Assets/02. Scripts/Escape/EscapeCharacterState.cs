using UnityEngine;

// 쿠키 캐릭터(스파이 포함)의 탈출 모드 상태(EscapePlan.md §1.2, §1.10). 모든 클라이언트에서 돈다.
// - 탈출(쿠키 탈출구) 또는 로켓 탑승이 승인되면: 본인은 관전으로 넘어가고(D36), 모두의 화면에서 캐릭터를 숨긴다.
//   탈출은 방장이 쓴 EscapeState로만 판단하고 마녀 사망(HitCount·DeathCause)도 방장이 쓴다(AuditFixPlan.md §8·F-07 —
//   본인이 쓰면 조작으로 탈출·불사가 가능했다).
public class EscapeCharacterState : MonoBehaviour
{
    private EscapeManager manager;
    private HideOrSeekPlayer cookie;
    private bool hidden;
    private bool spectating;

    public void Init(EscapeManager escape, HideOrSeekPlayer owner)
    {
        manager = escape;
        cookie = owner;
        manager.StateChanged += OnStateChanged;
        OnStateChanged();
        ApplyHidden();
    }

    private void OnDestroy()
    {
        if (manager != null) manager.StateChanged -= OnStateChanged;
    }

    private int Actor => cookie != null && cookie.View != null && cookie.View.Owner != null ? cookie.View.Owner.ActorNumber : -1;

    private void OnStateChanged()
    {
        ApplyHidden(); // 모든 화면: 탈것에 타서 기다리거나 탈출한 쿠키는 숨긴다
        if (manager.State == null || !cookie.IsMine) return;
        int actor = Actor;
        // 탔으면(출발 전이어도) 다른 쿠키 시점으로 관전(D36, EscapeVisualPlan.md §4.3)
        if (manager.State.HasLeftMap(actor) && !spectating)
        {
            spectating = true;
            if (TryGetComponent(out SpectatorController spectator)) spectator.EnterSpectatorMode(SpectatorKind.Escaped);
        }
    }

    private void ApplyHidden()
    {
        if (hidden || cookie.View == null || cookie.View.Owner == null) return;
        if (!EscapeManager.HasLeftMap(cookie.View.Owner.ActorNumber)) return;
        hidden = true;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        var rb = GetComponent<Rigidbody>();
        if (rb != null && cookie.IsMine) { if (!rb.isKinematic) rb.linearVelocity = Vector3.zero; rb.isKinematic = true; } // 모자 비행 중이면 이미 kinematic
    }
}
