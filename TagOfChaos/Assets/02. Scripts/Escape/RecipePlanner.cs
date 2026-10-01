using System.Collections.Generic;

// 판이 시작될 때 레시피와 필요 재료 수로 탈출 장치 칸을 정한다(EscapePlan.md §1.6, D3·D4). 순수 계산이라 테스트할 수 있다.
// - 칸 모드: 고정 칸은 항상 필요. 나머지 (N − 고정 수)개는 남은 칸 중에서 무작위로 고르고, 고르지 않은 칸은 채워진 상태로 시작.
// - 개수 모드: N개 칸, 칸마다 재료 종류를 counterItems 중에서 무작위로(모양만 다름, 어느 것이든 끼울 수 있다).
public static class RecipePlanner
{
    public struct PlannedSlot
    {
        public string Label;
        public string[] Accepts;
        public bool Required;    // 처음에 비어 있어 재료가 필요한 칸
        public string SpawnItem; // Required일 때 상자에 넣을 재료 ID
    }

    public static List<PlannedSlot> Plan(EscapeRecipeSO recipe, int required, System.Random rng)
    {
        var result = new List<PlannedSlot>();
        if (recipe == null || required <= 0) return result;

        if (recipe.Mode == RecipeMode.Counter)
        {
            ItemSO[] items = recipe.CounterItems;
            var accepts = new List<string>();
            foreach (ItemSO it in items) if (it != null) accepts.Add(it.ItemId);
            for (int i = 0; i < required && accepts.Count > 0; i++)
            {
                result.Add(new PlannedSlot
                {
                    Label = recipe.CounterLabel,
                    Accepts = accepts.ToArray(),
                    Required = true,
                    SpawnItem = accepts[rng.Next(accepts.Count)],
                });
            }
            return result;
        }

        // 칸 모드: 모든 칸을 만들고, 고정 칸을 먼저 필요로 표시한다.
        var free = new List<int>();
        int fixedTotal = 0;
        foreach (EscapeRecipeSO.SlotGroup g in recipe.Groups)
        {
            if (g == null || g.item == null) continue;
            string label = string.IsNullOrEmpty(g.label) ? g.item.DisplayName : g.label;
            for (int k = 0; k < g.slotCount; k++)
            {
                bool isFixed = k < g.fixedRequired;
                if (isFixed) fixedTotal++;
                else free.Add(result.Count);
                result.Add(new PlannedSlot
                {
                    Label = label,
                    Accepts = new[] { g.item.ItemId },
                    Required = isFixed,
                    SpawnItem = g.item.ItemId,
                });
            }
        }

        int remaining = System.Math.Max(0, required - fixedTotal);
        for (int n = 0; n < remaining && free.Count > 0; n++)
        {
            int pick = rng.Next(free.Count);
            int index = free[pick];
            free.RemoveAt(pick);
            PlannedSlot s = result[index];
            s.Required = true;
            result[index] = s;
        }
        return result;
    }
}
