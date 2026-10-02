using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// 방 설정(인원·제한시간·타임어택 시간, EscapePlan.md §1.7) 변경의 단일 실행 지점(research.md R5-22).
// 시작 버튼과 같은 호스트(RoomState.HostActor — 괴물이어도 됨)가 바꿀 수 있고, 실제 반영은 Photon 방장이 요청자와
// 값의 범위를 다시 확인해 한다. 호스트가 방장이 아니면 요청 이벤트(NetEventCodes.RoomSettingsRequest)를 보낸다 — GameStartAuthority와 같은 모양.
public static class RoomSettingsAuthority
{
    public enum Result { Applied, PlayersRaised, NotHost, NotMaster, NotInRoom }

    public static bool CanLocalEdit() => RoomState.IsInRoom() && RoomState.IsLocalHost();

    // 호스트 화면에서 호출. 방장이면 바로 반영하고, 아니면 방장에게 요청한다(결과는 Room Props로 모두에게 보인다).
    public static Result Request(int players, int timeLimitMinutes, int timeAttackMinutes)
    {
        if (!RoomState.IsInRoom() || PhotonNetwork.LocalPlayer == null) return Result.NotInRoom;
        if (!CanLocalEdit()) return Result.NotHost;
        if (PhotonNetwork.IsMasterClient) return TryApply(PhotonNetwork.LocalPlayer.ActorNumber, players, timeLimitMinutes, timeAttackMinutes);

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(NetEventCodes.RoomSettingsRequest, new[] { players, timeLimitMinutes, timeAttackMinutes }, options, SendOptions.SendReliable);
        // 방장도 같은 규칙으로 맞추므로 결과를 미리 알 수 있다
        return ClampPlayers(players) > players ? Result.PlayersRaised : Result.Applied;
    }

    // 방장 쪽 실행. 요청자가 호스트인지 확인하고, 값은 설정 SO의 허용 범위(인원은 지금 들어온 사람 수 이상)로 맞춘다.
    public static Result TryApply(int requesterActor, int players, int timeLimitMinutes, int timeAttackMinutes)
    {
        if (!RoomState.IsInRoom()) return Result.NotInRoom;
        if (!PhotonNetwork.IsMasterClient) return Result.NotMaster;
        if (requesterActor != RoomState.HostActor()) return Result.NotHost;

        GameSettingsSO settings = GameSettings.Current;
        Room room = PhotonNetwork.CurrentRoom;
        int clampedPlayers = ClampPlayers(players);
        room.MaxPlayers = clampedPlayers;
        room.SetCustomProperties(new Hashtable
        {
            { NetKeys.RoomTimeLimit, Mathf.Clamp(timeLimitMinutes, settings.MinTimeLimitMinutes, settings.MaxTimeLimitMinutes) * 60 },
            { NetKeys.TimeAttackDuration, Mathf.Clamp(timeAttackMinutes, settings.MinTimeAttackMinutes, settings.MaxTimeAttackMinutes) * 60 },
        });
        return clampedPlayers > players ? Result.PlayersRaised : Result.Applied; // 지금 인원보다 적게 고르면 올려서 알린다
    }

    // 방장 쪽: 요청 이벤트를 풀어 실행한다. 형식이 맞지 않으면 무시.
    public static void HandleRequest(EventData photonEvent)
    {
        if (!(photonEvent.CustomData is int[] values) || values.Length != 3) return;
        Result result = TryApply(photonEvent.Sender, values[0], values[1], values[2]);
        Debug.Log($"[RoomSettings] Request from actor {photonEvent.Sender}: {result}");
    }

    private static int ClampPlayers(int players)
    {
        GameSettingsSO settings = GameSettings.Current;
        int floor = Mathf.Max(settings.MinPlayers, PhotonNetwork.CurrentRoom.PlayerCount);
        return Mathf.Clamp(players, floor, Mathf.Max(floor, settings.MaxPlayers));
    }
}
