using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// GameScene 배치(색칠이 일어나는 씬을 표시하는 역할 겸). 마스터 전용으로, 색칠 페이즈(PaintPhaseEndTime)가
// 끝났을 때 등록 슬롯이 0개인 쿠키에게 색을 배정해 전신 강제 도포시킨다(GameRule.md §3.6). 괴물은 색칠 대상이
// 아니므로 제외한다(research.md §8.14).
public class PaintPhaseController : MonoBehaviourPunCallbacks
{
    // 이 씬에서 색칠(입력·강제 도포·스탬프 재생)이 허용되는지. PlayerPaintCanvas가 참조한다 — 대기실로 돌아온
    // 쿠키가 이전 판의 강제 도포 정보를 읽어 다시 칠해지던 문제를 씬 단위로 막는다(Bug-fix-plan.md §26.2 ⑲).
    // 씬 이름 비교 대신 이 컴포넌트의 존재로 판단하므로, 테스트 씬도 이 컴포넌트를 두면 같은 규칙을 따른다.
    public static bool IsPaintScene { get; private set; }

    [SerializeField] private ColorPaletteSO palette;
    private readonly System.Random rng = new System.Random();

    // 서버 응답 전 프레임마다 다시 배정하면 매번 다른 무작위 색이 전송돼 대상 쿠키 색이 잠깐씩 바뀌었다(Bug-fix-plan.md §41 ㊹).
    // 한 번만 보내고, 방장이 바뀌면 새 방장은 Room Prop 캐시로 다시 판단한다.
    private bool resolveRequested;

    private void Awake()
    {
        IsPaintScene = true;
    }

    private void OnDestroy()
    {
        IsPaintScene = false;
    }

    private void Update()
    {
        // 오프라인 모드는 방을 만들기 전에도 IsMasterClient가 true라 방 확인을 먼저 한다(PlayerTestScene, Bug-fix-plan.md §41 U1 검증 중 발견).
        if (!PhotonNetwork.IsMasterClient || resolveRequested || !RoomState.IsInRoom()) return;
        // 처리 여부를 로컬 플래그가 아니라 Room Prop으로 판단한다 — 방장이 바뀐 뒤 새 방장이 강제 도포 색을 다시
        // 무작위로 뽑아 게임 도중 쿠키 색이 바뀌던 문제를 막는다(Bug-fix-plan.md §26.8.3 ③). 이 키는 대기실
        // 초기화(RoundStateResetter)에서 지워지므로 판마다 한 번만 기록된다.
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(NetKeys.ForcedPaintActorNumbers)) return;
        if (!RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double endTime)) return;
        if (PhotonNetwork.Time < endTime) return;

        ResolvePaintPhase();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        resolveRequested = false;
    }

    private void ResolvePaintPhase()
    {
        if (palette == null || palette.Count == 0)
        {
            Debug.LogError("[PaintPhaseController] Palette is empty. Forced paint cannot assign colors.");
            resolveRequested = true; // 매 프레임 같은 오류를 반복하지 않는다
            return;
        }
        resolveRequested = true;
        var zeroSlotPlayers = new List<Player>();
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (RoomState.IsMonster(p.ActorNumber)) continue;
            int count = RoomState.TryGetPlayerInt(p, NetKeys.RegisteredSlotCount, out int v) ? v : 0;
            if (count == 0) zeroSlotPlayers.Add(p);
        }

        // 대상이 없어도 빈 배열을 기록해 "이번 판은 처리됨"을 남긴다(방장 교체 후 재실행 방지).
        int[] actorNumbers = new int[zeroSlotPlayers.Count];
        int[] assignedColors = new int[zeroSlotPlayers.Count];

        // 가능한 한 서로 겹치지 않게 섞은 순서대로 배정하고, 인원이 팔레트 색 수보다 많으면 섞은 순서를 다시
        // 순환한다(인원 확장 대비 — 예전에는 팔레트 색 수를 넘는 인원이 배정에서 빠졌다).
        int[] shuffledColors = Enumerable.Range(0, palette.Count).OrderBy(_ => rng.Next()).ToArray();
        for (int i = 0; i < zeroSlotPlayers.Count; i++)
        {
            actorNumbers[i] = zeroSlotPlayers[i].ActorNumber;
            assignedColors[i] = shuffledColors[i % shuffledColors.Length];
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.ForcedPaintActorNumbers, actorNumbers },
            { NetKeys.ForcedPaintColors, assignedColors },
        });
    }
}
