using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class ColorSelectionManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private ColorPaletteSO palette;
    [SerializeField] private float roundDuration = 20f;

    private const int TotalRounds = 4;
    private System.Random rng = new System.Random();

private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!RoomState.TryGetInt(NetKeys.RoundIndex, out int roundIndex)) return;
        if (roundIndex < 0 || roundIndex >= TotalRounds) return; // 진행 중 아님
        if (!RoomState.TryGetDouble(NetKeys.RoundEndTime, out double endTime)) return;
        if (PhotonNetwork.Time < endTime) return; // 아직 라운드 진행 중

        ResolveRound(roundIndex);
    }

    // MasterClient만 호출. 라운드 하나를 마감하고 다음 라운드(또는 술래 지정)로 넘어감.
    private void ResolveRound(int roundIndex)
    {
        var votes = new Dictionary<int, int>(); // actorNumber -> colorIndex
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            int vote = p.CustomProperties.TryGetValue(NetKeys.VoteColorIndex, out object v) ? (int)v : -1;
            votes[p.ActorNumber] = vote;
        }

        var excludedColors = new List<int>(roundIndex);
        for (int i = 0; i < roundIndex; i++)
            excludedColors.Add((int)PhotonNetwork.CurrentRoom.CustomProperties[NetKeys.ColorPrefix + i]);

        int resolvedColor = ColorVoteTally.Resolve(votes.Values, palette.Count, excludedColors, rng);

        var roomProps = new Hashtable
        {
            { NetKeys.ColorPrefix + roundIndex, resolvedColor }
        };

        if (roundIndex + 1 < TotalRounds)
        {
            roomProps[NetKeys.RoundIndex] = roundIndex + 1;
            roomProps[NetKeys.RoundEndTime] = PhotonNetwork.Time + roundDuration;
        }
        else
        {
            AssignTagger(roomProps, resolvedColor); // 4라운드 종료 → 술래 지정까지 같은 트랜잭션에 포함
            roomProps[NetKeys.RoundIndex] = TotalRounds + 1; // 완료 상태
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
        ResetAllVotes();
    }

    private void AssignTagger(Hashtable roomProps, int lastResolvedColor)
    {
        var players = PhotonNetwork.PlayerList;
        Player tagger = players[rng.Next(players.Length)];

        int[] baseSet = new int[TotalRounds];
        for (int i = 0; i < TotalRounds; i++)
        {
            baseSet[i] = i == TotalRounds - 1
                ? lastResolvedColor // 방금 확정된 값은 아직 Room CustomProperties에 반영 전이므로 직접 전달
                : (int)PhotonNetwork.CurrentRoom.CustomProperties[NetKeys.ColorPrefix + i];
        }

        int[] variant = TaggerColorAssigner.BuildVariantSet(baseSet, palette.Count, rng);

        roomProps[NetKeys.TaggerActorNumber] = tagger.ActorNumber;
        roomProps[NetKeys.TaggerVariantSet] = variant;
    }

    private void ResetAllVotes()
    {
        if (PhotonNetwork.LocalPlayer != null)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.VoteColorIndex, -1 } });
    }

    // 라운드 시작. 게임 시작 버튼 등에서 MasterClient가 방당 최초 1회 호출 (재대결 개념이 없으므로 그 이후에는 호출되지 않음)
    public void StartColorSelection()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        var props = new Hashtable
        {
            { NetKeys.RoundIndex, 0 },
            { NetKeys.RoundEndTime, PhotonNetwork.Time + roundDuration },
            { NetKeys.ColorPrefix + 0, -1 },
            { NetKeys.ColorPrefix + 1, -1 },
            { NetKeys.ColorPrefix + 2, -1 },
            { NetKeys.ColorPrefix + 3, -1 },
            { NetKeys.TaggerActorNumber, -1 },
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        PhotonNetwork.CurrentRoom.IsOpen = false; // 색상 선택 시작과 동시에 신규 입장 차단
    }

    // 로컬 플레이어가 팔레트 스와치를 클릭해 붓 색을 고를 때 UI에서 호출
    public void SubmitVote(int colorIndex)
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.VoteColorIndex, colorIndex } });
    }
}
