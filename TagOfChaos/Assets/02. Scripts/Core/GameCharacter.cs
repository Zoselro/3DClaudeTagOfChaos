using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 캐릭터 공통 계약(research.md §12 E1). 관전·카메라·목록 조회가 캐릭터 종류(쿠키/괴물)를 직접 알지 않고
// 이 계약만으로 동작한다 — 예전에는 SpectatorController가 HideOrSeekPlayer/MonsterController를 각각 찾아
// 타입으로 분기했다. 새 캐릭터 종류는 IGameCharacter를 구현하고 CharacterRegistry에 등록하면 된다.

public enum CharacterRole
{
    Cookie,
    Monster,
}

// 3인칭 카메라(조작·관전 공용)가 따라갈 때의 시선 높이·거리. 거리가 0 이하면 Camera_Ctrl 기본값을 쓴다.
public interface ICameraFollowTarget
{
    float CameraTargetHeight { get; }
    float CameraDistance { get; }
}

public interface IGameCharacter : ICameraFollowTarget
{
    CharacterRole Role { get; }
    PhotonView View { get; }
    GameObject gameObject { get; }

    // 관전 대상이 될 수 있는 상태인지(예: 파괴된 쿠키는 false).
    bool IsSpectatable { get; }

    // 지금 상호작용 키로 사물(IInteractable)을 쓸 수 있는지(예: 파괴·들림·채팅 중인 쿠키, 처형 중인 괴물은 false).
    bool CanInteract { get; }
}

// 현재 씬에 활성화된 캐릭터 목록. 캐릭터가 OnEnable/OnDisable에서 스스로 등록·해제한다 —
// 관전 대상 탐색이나 로컬 쿠키 찾기에 매번 FindObjectsByType로 씬 전체를 훑지 않게 한다(research.md §12 E12).
public static class CharacterRegistry
{
    private static readonly List<IGameCharacter> characters = new List<IGameCharacter>();

    public static IReadOnlyList<IGameCharacter> All => characters;

    public static void Register(IGameCharacter character)
    {
        if (character != null && !characters.Contains(character)) characters.Add(character);
    }

    public static void Unregister(IGameCharacter character)
    {
        characters.Remove(character);
    }

    // 파괴된(Destroy) 오브젝트는 Unity의 null 비교로만 걸러지므로 인터페이스 참조에도 이 검사를 쓴다.
    public static bool IsAlive(IGameCharacter character) => character is Object obj && obj != null;

    // 이 클라이언트가 조종하는 첫 캐릭터(없으면 null).
    public static T FindLocal<T>() where T : class, IGameCharacter
    {
        foreach (IGameCharacter c in characters)
            if (c is T typed && IsAlive(c) && c.View != null && c.View.IsMine) return typed;
        return null;
    }
}
