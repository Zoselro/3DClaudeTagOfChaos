using Photon.Pun;
using UnityEngine;

// 방장이 승인한 도구 명중(NetEventCodes.ToolHit) 처리. 모든 클라이언트가 효과를 보여주고(물풍선 물보라, 기절 별),
// 맞은 사람 본인만 자기 상태를 바꾼다: 기절, 손에 든 아이템 하나 떨어뜨리기(D20), 물풍선이면 그 색으로 칠하기.
public static class ToolHitReceiver
{
    public static void Handle(object data)
    {
        if (!(data is object[] d) || d.Length < 5 || !(d[0] is string itemId) || !(d[2] is int[] targets) || !(d[3] is Vector3 point)) return;
        int color = d[4] is int c ? c : 0;
        EscapeManager manager = EscapeManager.Instance;
        ItemSO item = manager != null ? manager.Catalog.Find(itemId) : null;
        if (item == null || !item.IsTool) return;

        // 던진·쏜 사람 손에서 날아가는 모습. 물풍선은 떨어진 순간에 명중 처리를 한다.
        int senderActor = d[1] is int sender ? sender : -1;
        Vector3? from = HandOf(senderActor);
        // 쓴 사람은 누르는 순간 이미 들었다(ToolUser.Use). 다른 사람은 그 손에서 3D로(S5).
        bool mine = PhotonNetwork.LocalPlayer != null && senderActor == PhotonNetwork.LocalPlayer.ActorNumber;
        if (!mine) GameAudio.PlayAt(ToolUser.UseSound(item.Tool.Kind), from ?? point);
        if (item.Tool.Kind == ToolKind.WaterBalloon && from.HasValue)
        {
            ToolShotFx.ThrowBalloon(item, from.Value, point, () => Apply(item, targets, point, color));
            return;
        }
        if (item.Tool.Kind == ToolKind.StunGun && from.HasValue) ToolShotFx.Beam(from.Value, point, new Color(0.55f, 1f, 0.95f, 1f));
        Apply(item, targets, point, color);
    }

    private static Vector3? HandOf(int actor)
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.View != null && c.View.Owner != null && c.View.Owner.ActorNumber == actor)
                return c.gameObject.transform.position + Vector3.up * 1.2f + c.gameObject.transform.forward * 0.4f;
        return null;
    }

    private static void Apply(ItemSO item, int[] targets, Vector3 point, int color)
    {
        ToolFx.Play(item.Tool.Kind, point, item.Tint);
        // 맞은 곳 소리(모두, 3D): 물풍선은 늘 터지고, 스턴건·뿅망치는 누군가 맞았을 때만(S5)
        switch (item.Tool.Kind)
        {
            case ToolKind.WaterBalloon: GameAudio.PlayAt(SoundId.BalloonSplash, point); break;
            case ToolKind.StunGun: if (targets.Length > 0) GameAudio.PlayAt(SoundId.StunHit, point); break;
            case ToolKind.Hammer: if (targets.Length > 0) GameAudio.PlayAt(SoundId.HammerBonk, point); break;
        }

        foreach (int viewId in targets)
        {
            PhotonView view = PhotonView.Find(viewId);
            if (view == null) continue;
            IGameCharacter character = view.GetComponent<IGameCharacter>();
            if (character == null) continue;

            bool isMonster = character.Role == CharacterRole.Monster;
            float seconds = isMonster ? item.Tool.StunSecondsMonster : item.Tool.StunSecondsCookie;
            StunReceiver stun = view.GetComponent<StunReceiver>();
            if (stun != null) stun.Stun(seconds); // 모든 화면에 별 이펙트, 본인은 움직임도 막힌다

            if (!view.IsMine || isMonster) continue;
            PlayerInventory inv = view.GetComponent<PlayerInventory>();
            if (inv != null) inv.RequestDropHeld(); // 손에 든 아이템 하나만(D20)
            if (item.Tool.PaintsTarget)
            {
                PlayerPaintCanvas canvas = view.GetComponent<PlayerPaintCanvas>();
                if (canvas != null) canvas.ForceFillColor(color);
            }
        }
    }
}
