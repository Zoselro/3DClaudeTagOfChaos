using Photon.Pun;
using UnityEngine;

// 탈출 모드 소리(SoundPlan.md S5). EscapeManager가 붙이고, 상태가 바뀔 때마다 직전 상태와 비교해 무엇이 일어났는지 소리로 낸다.
// 방장 코드(EscapeAuthority)에서 소리를 내지 않으므로 모든 화면이 같은 상태 차이로 같은 소리를 듣는다. 방장 화면은 같은 상태가
// 두 번(즉시 적용 + 방 속성 응답) 들어오지만 두 번째는 차이가 없어 소리도 없다. 처음 받은 상태(늦게 들어온 경우)는 소리 없이 기억만 한다.
// - 상자 열림·다시 채워짐, 줍기(본인 2D·남 3D)·떨어뜨리기, 장치에 끼우기·빼기(같은 소리 — D3), 장치 완성(+스팅어),
//   로켓에 끼우기, 탈것에 타기(쿠키 — 폴짝 또는 빨려 듦, 맵 연출이 정함)·로켓 해치(스파이), 내 탈출 성공(2D), 타임어택 마지막 10초 심장 박동(2D).
// 상자에서 나온 재료를 손에 쥐는 소리는 뚜껑 소리(ChestOpen) 뒤 ChestPickupDelay초에 낸다(Request1009Plan.md §2 — 두 소리가 겹치면 "얻는 소리"만 들렸다).
public class EscapeAudioPresenter : MonoBehaviour
{
    private const int HeartBeatFromSeconds = 10;
    public const float ChestPickupDelay = 0.3f;

    private EscapeManager manager;
    private EscapeState previous;
    private int lastHeartSecond = -1;

    public void Init(EscapeManager owner)
    {
        manager = owner;
        manager.StateChanged += OnStateChanged;
        if (manager.State != null) previous = manager.State.Clone();
    }

    private void OnDestroy()
    {
        if (manager != null) manager.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged()
    {
        EscapeState current = manager.State;
        if (current == null) return;
        if (previous != null) PlayDifferences(previous, current);
        previous = current.Clone();
    }

    private void PlayDifferences(EscapeState before, EscapeState now)
    {
        for (int i = 0; i < before.Chests.Count && i < now.Chests.Count; i++)
        {
            if (before.Chests[i].Opened == now.Chests[i].Opened) continue;
            GameAudio.PlayAt(now.Chests[i].Opened ? SoundId.ChestOpen : SoundId.ChestRespawn, manager.ChestPosition(now.Chests[i].Anchor));
        }

        for (int i = 0; i < before.Items.Count && i < now.Items.Count; i++)
        {
            EscapeState.Item a = before.Items[i], b = now.Items[i];
            if (a.Loc == b.Loc && a.A == b.A) continue;
            if (b.Loc == ItemLocation.Held && (a.Loc != ItemLocation.Held || a.A != b.A))
            {
                if (a.Loc == ItemLocation.Device) GameAudio.PlayAt(SoundId.DeviceInsert, SlotPosition(a.A)); // 스파이가 장치에서 뺌
                else if (a.Loc == ItemLocation.Chest) StartCoroutine(PlayOnActorLater(SoundId.ItemPickup, b.A, ChestPickupDelay));
                else PlayOnActor(SoundId.ItemPickup, b.A);
            }
            else if (a.Loc == ItemLocation.Held && b.Loc == ItemLocation.Ground) GameAudio.PlayAt(SoundId.ItemDrop, b.Pos);
            else if (a.Loc == ItemLocation.Held && b.Loc == ItemLocation.Device) GameAudio.PlayAt(SoundId.DeviceInsert, SlotPosition(b.A));
        }

        for (int i = 0; i < before.RocketSlots.Count && i < now.RocketSlots.Count; i++)
            if (!before.RocketSlots[i].RocketFilled && now.RocketSlots[i].RocketFilled) GameAudio.PlayAt(SoundId.RocketInsert, manager.RocketPosition);

        if (before.CompletedAt <= 0 && now.CompletedAt > 0)
        {
            GameAudio.PlayAt(SoundId.DeviceComplete, manager.DevicePosition);
            GameAudio.Stinger(MusicId.StDeviceComplete);
        }

        int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        SoundId board = manager.Sequence != null ? manager.Sequence.BoardSound : SoundId.BoardHop; // 오븐·포탈은 빨려 듦(S6)
        foreach (int actor in now.Waiting)
            if (!before.Waiting.Contains(actor) && !PlayOnActor(board, actor)) GameAudio.PlayAt(board, manager.BoardPosition);
        foreach (int actor in now.Boarded)
            if (!before.Boarded.Contains(actor)) GameAudio.PlayAt(SoundId.RocketHatch, manager.RocketPosition);

        bool escapedNow = now.Escaped.Contains(me) || now.Boarded.Contains(me);
        bool escapedBefore = before.Escaped.Contains(me) || before.Boarded.Contains(me);
        if (escapedNow && !escapedBefore) GameAudio.Play(SoundId.EscapeSuccessSelf);
    }

    // 타임어택 마지막 10초: 1초마다 심장 박동(본인 2D). 카운트다운 째깍(UiTick)과 함께 난다.
    private void Update()
    {
        if (GamePhaseState.Current != GamePhase.TimeAttack || !RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out double end))
        {
            lastHeartSecond = -1;
            return;
        }
        int seconds = Mathf.CeilToInt((float)(end - PhotonNetwork.Time));
        if (seconds == lastHeartSecond) return;
        lastHeartSecond = seconds;
        if (seconds > 0 && seconds <= HeartBeatFromSeconds) GameAudio.Play(SoundId.HeartBeat);
    }

    private Vector3 SlotPosition(int slot) => manager.Device != null ? manager.Device.SlotPosition(slot) : manager.DevicePosition;

    private System.Collections.IEnumerator PlayOnActorLater(SoundId id, int actor, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayOnActor(id, actor);
    }

    private static bool PlayOnActor(SoundId id, int actor)
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(c) || c.View == null || c.View.Owner == null || c.View.Owner.ActorNumber != actor) continue;
            GameAudio.PlayCharacter(id, c);
            return true;
        }
        return false;
    }
}
