using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 쿠키 캐릭터(스파이 포함)의 탈출 모드 상태(EscapePlan.md §1.2, §1.10). 모든 클라이언트에서 돈다.
// - 탈출(쿠키 탈출구) 또는 로켓 탑승이 승인되면: 본인은 Escaped=1을 쓰고 관전으로 넘어가며(D36), 모두의 화면에서 캐릭터를 숨긴다.
// - 마녀가 내리치면(WitchStrike): 아직 남은 본인은 마녀에 의해 사망(DeathCause=2)으로 파괴된다.
public class EscapeCharacterState : MonoBehaviourPunCallbacks
{
    private EscapeManager manager;
    private HideOrSeekPlayer cookie;
    private bool hidden;
    private bool escapeHandled;
    private bool spectating;
    private bool witchHandled;

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
        bool waiting = manager.State.Waiting.Contains(actor);
        bool escaped = manager.State.Escaped.Contains(actor) || manager.State.Boarded.Contains(actor);

        // 탔으면(출발 전이어도) 다른 쿠키 시점으로 관전(D36, EscapeVisualPlan.md §4.3)
        if ((waiting || escaped) && !spectating)
        {
            spectating = true;
            if (TryGetComponent(out SpectatorController spectator)) spectator.EnterSpectatorMode(SpectatorKind.Escaped);
        }
        // 탈것이 출발한 순간(또는 쿠키 탈출구·스파이 로켓) 탈출 성공을 본인 속성에 쓴다
        if (escaped && !escapeHandled)
        {
            escapeHandled = true;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.Escaped, 1 } });
        }
    }

    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changed)
    {
        if (cookie.View != null && target == cookie.View.Owner && changed.ContainsKey(NetKeys.Escaped)) ApplyHidden();
    }

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (changed.ContainsKey(NetKeys.WitchStrike)) OnWitchStrike();
    }

    private void ApplyHidden()
    {
        if (hidden || cookie.View == null || cookie.View.Owner == null) return;
        if (!RoomState.HasEscaped(cookie.View.Owner) && !EscapeManager.IsWaiting(cookie.View.Owner.ActorNumber)) return;
        hidden = true;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        var rb = GetComponent<Rigidbody>();
        if (rb != null && cookie.IsMine) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
    }

    // 마녀가 손으로 맵을 내리친다 — 아직 남은 쿠키는 모두 죽는다(§1.2).
    private void OnWitchStrike()
    {
        if (witchHandled || !cookie.IsMine || !RoomState.TryGetInt(NetKeys.WitchStrike, out _)) return;
        if (RoomState.HasEscaped(PhotonNetwork.LocalPlayer) || cookie.IsBroken) return;
        witchHandled = true;
        cookie.KillByWitch();
    }
}
