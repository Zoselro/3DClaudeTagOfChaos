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

        ToolFx.Play(item.Tool.Kind, point, item.Tint);

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
