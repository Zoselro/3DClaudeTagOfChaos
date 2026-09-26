using System.Collections.Generic;
using UnityEngine;

// 상호작용 키(E)로 쓰는 사물의 공통 계약(GameLobbyScene.md §14). 문뿐 아니라 이후 추가될 사물도 이것만 구현하고
// InteractableRegistry에 등록하면 CharacterInteractor가 알아서 문구를 띄우고 입력을 전달한다.
public interface IInteractable
{
    // 거리 판정 기준점(월드).
    Vector3 InteractionPoint { get; }

    // 기준점으로부터 이 수평 거리(m) 안에 들어오면 상호작용할 수 있다.
    float InteractionRange { get; }

    // 캐릭터 자격(파괴·들림 등)은 CharacterInteractor가 이미 걸렀다. 사물 쪽 조건만 판단한다.
    bool CanInteract(IGameCharacter character);

    void Interact(IGameCharacter character);
}

// 씬에 활성화된 상호작용 사물 목록. 사물이 OnEnable/OnDisable에서 스스로 등록·해제한다 —
// 매 검사마다 물리 쿼리나 FindObjectsByType로 씬을 훑지 않는다(CharacterRegistry와 같은 방식).
public static class InteractableRegistry
{
    private static readonly List<IInteractable> interactables = new List<IInteractable>();

    public static IReadOnlyList<IInteractable> All => interactables;

    public static void Register(IInteractable interactable)
    {
        if (interactable != null && !interactables.Contains(interactable)) interactables.Add(interactable);
    }

    public static void Unregister(IInteractable interactable)
    {
        interactables.Remove(interactable);
    }

    // 파괴된(Destroy) 오브젝트는 Unity의 null 비교로만 걸러지므로 인터페이스 참조에도 이 검사를 쓴다.
    public static bool IsAlive(IInteractable interactable) => interactable is Object obj && obj != null;
}
