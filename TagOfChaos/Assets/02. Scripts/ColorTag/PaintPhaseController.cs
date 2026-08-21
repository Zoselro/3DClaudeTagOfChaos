using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;


// 마스터 전용. 60초 색칠 페이즈(PaintPhaseEndTime)가 끝났을 때, 등록 슬롯이 0개인 플레이어에게
// 서로 겹치지 않는 색을 배정해 전신 강제 도포시킨다(GameRule.md §3.6).
public class PaintPhaseController : MonoBehaviourPunCallbacks
{
    [SerializeField] private ColorPaletteSO palette;
    private readonly System.Random rng = new System.Random();
    private bool resolved;

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (resolved) return;
        if (!RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double endTime)) return;
        if (PhotonNetwork.Time < endTime) return;

        ResolvePaintPhase();
        resolved = true;
    }

    private void ResolvePaintPhase()
    {
        var zeroSlotPlayers = new List<Player>();
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            int count = p.CustomProperties.TryGetValue(NetKeys.RegisteredSlotCount, out object v) ? (int)v : 0;
            if (count == 0) zeroSlotPlayers.Add(p);
        }

        if (zeroSlotPlayers.Count == 0) return;

        int[] shuffledColors = Enumerable.Range(0, palette.Count).OrderBy(_ => rng.Next()).ToArray();

        int[] actorNumbers = new int[zeroSlotPlayers.Count];
        int[] assignedColors = new int[zeroSlotPlayers.Count];
        for (int i = 0; i < zeroSlotPlayers.Count; i++)
        {
            actorNumbers[i] = zeroSlotPlayers[i].ActorNumber;
            assignedColors[i] = shuffledColors[i];
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.ForcedPaintActorNumbers, actorNumbers },
            { NetKeys.ForcedPaintColors, assignedColors },
        });
    }
}
