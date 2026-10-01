// 상호작용 안내 아이콘 아래에 이름을 띄우는 선택 계약(GameFixPlan.md F4, EscapePlan.md §1.4). 보는 캐릭터에 따라
// 이름이 달라질 수 있다(예: 스파이 상자는 쿠키에게 "스파이 상자" + "잠겨 있음"). 문(InteractableDoor)처럼 이름이
// 필요 없는 사물은 구현하지 않는다. 이름 문구는 사물의 인스펙터에서 입력한다(코드에 한글 금지).
public interface IInteractionLabel
{
    // 비어 있으면(null 또는 "") 이름을 띄우지 않는다.
    string GetLabel(IGameCharacter viewer);
}
