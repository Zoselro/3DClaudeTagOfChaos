using System.Collections.Generic;

// 캐릭터 4종 배정(Request1009Plan.md §25.5 B). 대기실에서 각자 고른 캐릭터(SkinIndex)는 겹칠 수 있으므로, 판이 시작될 때 방장이
// 입장 순서(액터 번호)대로 고른 것을 인정하고, 고르지 않았거나 겹친 사람에게는 남은 캐릭터를 차례로 준다. 괴물은 대상이 아니다.
// 결과는 Room Prop CharacterAssignments([액터, 캐릭터, …])로 쓰고 모든 화면이 같은 값을 읽는다. 네트워크와 분리한 순수 계산.
public static class CharacterAssignment
{
    public const int Unselected = -1;

    // actors: 배정할 사람(괴물 제외). preferences: 액터 → 고른 캐릭터(없으면 Unselected). count: 캐릭터 수.
    // 캐릭터보다 사람이 많으면 남는 사람은 처음부터 다시 돌려 준다(정원 5명·캐릭터 4종이라 괴물을 빼면 생기지 않는다).
    public static int[] Assign(IList<int> actors, IReadOnlyDictionary<int, int> preferences, int count)
    {
        var sorted = new List<int>(actors);
        sorted.Sort();
        var taken = new HashSet<int>();
        var result = new Dictionary<int, int>();
        foreach (int actor in sorted)
        {
            if (count <= 0) break;
            if (!preferences.TryGetValue(actor, out int want) || want < 0 || want >= count || taken.Contains(want)) continue;
            taken.Add(want);
            result[actor] = want;
        }
        int next = 0;
        foreach (int actor in sorted)
        {
            if (result.ContainsKey(actor) || count <= 0) continue;
            int guard = 0;
            while (taken.Contains(next % count) && guard++ < count) next++;
            int pick = next % count;
            taken.Add(pick);
            result[actor] = pick;
            next++;
        }
        var flat = new int[result.Count * 2];
        int i = 0;
        foreach (int actor in sorted)
        {
            if (!result.TryGetValue(actor, out int c)) continue;
            flat[i++] = actor;
            flat[i++] = c;
        }
        return flat;
    }

    // 배정 결과에서 그 사람의 캐릭터(없으면 false).
    public static bool TryGet(int[] assignments, int actor, out int character)
    {
        character = Unselected;
        if (assignments == null) return false;
        for (int i = 0; i + 1 < assignments.Length; i += 2)
        {
            if (assignments[i] != actor) continue;
            character = assignments[i + 1];
            return true;
        }
        return false;
    }

    // 지금 그 사람에게 보일 캐릭터: 판의 배정이 있으면 그것, 없으면 본인이 고른 값(고르지 않았으면 0).
    public static int Current(int actor, int selected)
    {
        if (RoomState.TryGetIntArray(NetKeys.CharacterAssignments, out int[] assigned) && TryGet(assigned, actor, out int c)) return c;
        return selected >= 0 ? selected : 0;
    }
}
