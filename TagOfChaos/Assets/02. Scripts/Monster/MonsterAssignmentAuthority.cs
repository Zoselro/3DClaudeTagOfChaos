using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 마스터 전용 — 가마솥 선착순 신청 확정 + 무입장 타임아웃 시 랜덤 배정(GameRule.md §2.1).
//
// 뽑는 괴물 수는 GameSettings.MonsterCount(인원에 맞게 제한, 기본 1)다. 인원을 늘릴 때 괴물 수도 설정만으로
// 늘릴 수 있도록 MonsterActorNumbers 배열을 채우는 방식으로 일반화했다(Bug-fix-plan.md §27 확장성).
// 타임아웃 기준 시각은 "정원이 찬 순간"이고 Room Prop(MonsterSelectDeadline)에 두므로 방장이 바뀌어도 이어진다
// (§24.3 ⑰-B, research.md §8.13).
public class MonsterAssignmentAuthority : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // 온라인에서는 SetCustomProperties가 서버 응답 전까지 로컬 캐시에 반영되지 않는다(PUN 기본
    // BroadcastPropsChangeToAll=true). 응답이 오기 전 다음 프레임에 같은 요청을 다시 보내지 않도록 막는 플래그.
    private bool deadlineRequested;
    private bool confirmRequested;
    private bool resetRequested;

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

        ConfirmMonsters(AppendMonsters(new[] { claimantActorNumber }), "cauldron");
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || !RoomState.IsInRoom()) return;
        if (ResetSelectionIfRoomNotFull()) return;
        if (RoomState.IsMonsterSelectionComplete()) return;

        Room room = PhotonNetwork.CurrentRoom;
        bool hasDeadline = RoomState.TryGetDouble(NetKeys.MonsterSelectDeadline, out double deadline);

        if (!RoomState.IsRoomFull())
        {
            // 정원이 다시 줄면 대기 시간을 처음부터 다시 잰다.
            deadlineRequested = false;
            if (hasDeadline) room.SetCustomProperties(new Hashtable { { NetKeys.MonsterSelectDeadline, null } });
            return;
        }

        if (!hasDeadline)
        {
            if (!deadlineRequested)
            {
                deadlineRequested = true;
                room.SetCustomProperties(new Hashtable { { NetKeys.MonsterSelectDeadline, PhotonNetwork.Time + GameSettings.Current.MonsterSelectTimeout } });
            }
            return;
        }
        deadlineRequested = false;

        if (PhotonNetwork.Time < deadline || confirmRequested) return;

        ConfirmMonsters(AppendMonsters(PickRandomNonMonsters(MissingMonsterCount())), "timeout");
    }

    // 대기실에서 괴물이 나가면 그 사람만 목록에서 빼고, 모자란 자리는 가마솥/타임아웃으로 다시 뽑는다(Bug-fix-plan.md
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
        deadlineRequested = false;
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

    private static int MissingMonsterCount()
    {
        int required = GameSettings.Current.MonsterCountFor(PhotonNetwork.CurrentRoom.PlayerCount);
        return Mathf.Max(0, required - RoomState.MonsterCount());
    }

    private static int[] PickRandomNonMonsters(int count)
    {
        var candidates = new List<int>();
        foreach (Player p in PhotonNetwork.PlayerList)
            if (!RoomState.IsMonster(p.ActorNumber)) candidates.Add(p.ActorNumber);

        var picked = new List<int>(count);
        while (picked.Count < count && candidates.Count > 0)
        {
            int index = Random.Range(0, candidates.Count);
            picked.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
        return picked.ToArray();
    }

    private static int[] AppendMonsters(int[] newMonsters)
    {
        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] current);
        return (current ?? new int[0]).Concat(newMonsters).Distinct().ToArray();
    }

    private void ConfirmMonsters(int[] monsterActorNumbers, string reason)
    {
        if (confirmRequested || monsterActorNumbers.Length == 0) return; // 응답 전 중복 확정 방지(가마솥 신청과 타임아웃이 겹치는 경우 포함)
        confirmRequested = true;

        var props = new Hashtable
        {
            { NetKeys.MonsterActorNumbers, monsterActorNumbers },
            { NetKeys.MonsterRevealTime, PhotonNetwork.Time },
        };
        // 자리가 모두 찼을 때만 선정 기준 시각을 지운다 — 괴물이 여럿일 때 일부만 가마솥으로 확정됐다고 타이머가
        // 처음부터 다시 돌지 않게 한다.
        int required = GameSettings.Current.MonsterCountFor(PhotonNetwork.CurrentRoom.PlayerCount);
        if (monsterActorNumbers.Length >= required) props[NetKeys.MonsterSelectDeadline] = null;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Debug.Log($"[MonsterAssignment] Monsters confirmed: [{string.Join(",", monsterActorNumbers)}] (by {reason}).");
    }
}
