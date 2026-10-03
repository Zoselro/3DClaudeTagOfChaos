using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 마스터 전용 — 가마솥 선착순 신청 확정(GameRule.md §2.1). 괴물은 가마솥에 들어간 사람만 된다(인원표상 괴물이 2명이면
// 두 사람이 각각 들어가야 한다).
// (2026-10-03 사용자 요청: 정원이 찬 뒤 30초가 지나면 무작위로 괴물을 뽑던 기능은 삭제 — 예전 기한 Room Prop
//  MonsterSelectDeadline이 남아 있으면 지우기만 한다.)
//
// 뽑는 괴물 수는 인원표(GameSettings.MonsterCountFor)를 따른다. MonsterActorNumbers 배열을 채우는 방식이라
// 인원이 늘어 괴물이 여럿이어도 같은 코드로 동작한다(Bug-fix-plan.md §27 확장성).
public class MonsterAssignmentAuthority : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // 온라인에서는 SetCustomProperties가 서버 응답 전까지 로컬 캐시에 반영되지 않는다(PUN 기본
    // BroadcastPropsChangeToAll=true). 응답이 오기 전 다음 프레임에 같은 요청을 다시 보내지 않도록 막는 플래그.
    private bool confirmRequested;
    private bool resetRequested;
    private bool deadlineClearRequested; // 남은 기한 삭제를 응답 전 매 프레임 다시 보내지 않도록(Bug-fix-plan.md §41 ㊹)

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.ClaimMonster) return;
        if (!PhotonNetwork.IsMasterClient || confirmRequested) return;
        // 정원 미달이거나 자리가 다 찼으면 무시한다 — 가마솥(클라이언트)도 거르지만, 정원이 찬 순간 누가 나가는
        // 경합까지 마스터가 최종 거부한다(Bug-fix-plan.md §30.2 C).
        if (!RoomState.CanSelectMonster())
        {
            Debug.Log("[MonsterAssignment] Claim ignored: room not full or selection already complete.");
            return;
        }
        if (!(photonEvent.CustomData is int claimantActorNumber)) return;
        if (RoomState.IsMonster(claimantActorNumber)) return;

// 괴물이 여럿 필요한 인원(7~8명 = 2명)은 가마솥에 들어간 사람만 한 명씩 괴물이 된다 — 무작위로 채우지 않는다(2026-10-03 사용자 결정).
        // 자리가 다 찰 때까지 대기실 문구가 "가마솥에 들어가세요"를 보여 주고 시작 버튼은 잠겨 있다.
        ConfirmMonsters(AppendMonsters(new[] { claimantActorNumber }), "cauldron");
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || !RoomState.IsInRoom()) return;
        if (ResetSelectionIfRoomNotFull()) return;
        ClearLegacyDeadline();
    }

    // 예전 버전이 남긴 무작위 선정 기한(MonsterSelectDeadline)이 있으면 한 번 지운다 — 대기실 문구가 남은 초를 세지 않게.
    private void ClearLegacyDeadline()
    {
        if (!RoomState.TryGetDouble(NetKeys.MonsterSelectDeadline, out _)) { deadlineClearRequested = false; return; }
        if (deadlineClearRequested) return;
        deadlineClearRequested = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.MonsterSelectDeadline, null } });
    }

    // 대기실에서 괴물이 나가면 그 사람만 목록에서 빼고, 모자란 자리는 가마솥으로 다시 뽑는다(Bug-fix-plan.md
    // §24.4). 그러지 않으면 떠난 사람이 괴물로 남은 채 시작 버튼이 활성화돼 괴물 없는 판이 시작됐다.
    // (GameScene에서의 이탈은 RoomLifecycleWatcher가 처리한다.)
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) || !monsters.Contains(otherPlayer.ActorNumber)) return;

        int[] remaining = monsters.Where(a => a != otherPlayer.ActorNumber).ToArray();
        Debug.Log($"[MonsterAssignment] Monster (actor {otherPlayer.ActorNumber}) left the lobby. Remaining monsters: {remaining.Length}. Selection restarts for missing seats.");
        confirmRequested = false;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.MonsterActorNumbers, remaining.Length > 0 ? remaining : null },
            { NetKeys.MonsterRevealTime, remaining.Length > 0 ? (object)PhotonNetwork.Time : null },
            { NetKeys.MonsterSelectDeadline, null },
        });
    }

    // 괴물은 정원이 찼을 때만 존재한다(사용자 결정 §30.7-1). 선정 후 괴물이 아닌 사람이 나가 정원 미달이 되면 선정을
    // 초기화한다 — 괴물 공지는 숨고, 방장 정책(MasterClientPolicy)이 방 생성자에게 권한을 되돌린다(둘 다
    // MonsterActorNumbers 변경에 반응). 이 컴포넌트는 대기실에만 있으므로 게임 중 이탈(RoomLifecycleWatcher)과 무관하다.
    // 서버 응답 전 같은 요청을 반복하지 않도록 resetRequested로 막는다. 처리했으면 true.
    private bool ResetSelectionIfRoomNotFull()
    {
        // 판이 시작된 뒤(시작 버튼이 PaintPhaseEndTime을 기록, 복귀 시 RoundStateResetter가 지움)에는 건드리지 않는다 —
        // 쿠키들이 GameScene에 있는 동안 대기실의 괴물만 남아 방장이 되는 경우에도 진행 중인 판의 괴물 정보를 지우지 않도록.
        if (RoomState.IsRoomFull() || !RoomState.HasMonster() || RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out _))
        {
            resetRequested = false;
            return false;
        }
        if (resetRequested) return true;

        resetRequested = true;
        confirmRequested = false;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.MonsterActorNumbers, null },
            { NetKeys.MonsterRevealTime, null },
            { NetKeys.MonsterSelectDeadline, null },
        });
        Debug.Log("[MonsterAssignment] Room is no longer full. Monster selection reset.");
        return true;
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(NetKeys.MonsterActorNumbers)) confirmRequested = false;
    }

    private static int[] AppendMonsters(int[] newMonsters)
    {
        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] current);
        return (current ?? new int[0]).Concat(newMonsters).Distinct().ToArray();
    }

    private void ConfirmMonsters(int[] monsterActorNumbers, string reason)
    {
        if (confirmRequested || monsterActorNumbers.Length == 0) return; // 응답 전 중복 확정 방지(가마솥 신청이 겹치는 경우)
        confirmRequested = true;

        var props = new Hashtable
        {
            { NetKeys.MonsterActorNumbers, monsterActorNumbers },
            { NetKeys.MonsterRevealTime, PhotonNetwork.Time },
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Debug.Log($"[MonsterAssignment] Monsters confirmed: [{string.Join(",", monsterActorNumbers)}] (by {reason}).");
    }
}
