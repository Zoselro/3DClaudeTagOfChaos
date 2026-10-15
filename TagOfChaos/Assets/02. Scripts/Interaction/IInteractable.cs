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

// 길게 눌러야 하는 사물(Request1009Plan.md §1 — 상자·탈출 장치·스파이 로켓). 지금 상태에서 몇 초 눌러야 하는지 사물이 정한다:
// 0이면 지금처럼 누르는 즉시 Interact(예: 완성된 장치에 타기), 0보다 크면 CharacterInteractor가 그 시간 동안 누르고 있어야 Interact를 부른다.
// 누르기는 각자 화면에서만 재고, 다 차면 지금과 같은 요청을 한 번 보낸다 — 방장 판정(EscapeAuthority)은 바뀌지 않는다.
public interface IHoldInteractable : IInteractable
{
    float HoldSecondsFor(IGameCharacter character);
}

// 길게 누르기를 시작한 순간을 알아야 하는 사물(스파이 빼기 — 방장이 시작 시각부터 10초를 검사, Request1009Plan.md §19.5).
public interface IHoldStartListener
{
    void OnHoldStarted(IGameCharacter character);
}

// 길게 누르기가 끝난 순간(다 참·뗌·취소)을 알아야 하는 사물(건물 수리 — 방장이 게이지를 들고 있다, Request1009Plan.md §25.5 E).
public interface IHoldEndListener
{
    void OnHoldEnded(IGameCharacter character, bool completed);
}

// 아이콘 둘레 진행 링에 사물이 정한 값을 보여 준다(수리 게이지는 이어서 차므로 누른 시간이 아니라 건물 게이지).
public interface IHoldProgressDisplay
{
    float DisplayProgress(float holdProgress);
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
