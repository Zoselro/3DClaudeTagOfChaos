using System.Collections.Generic;
using System.Linq;

public static class ColorVoteTally
{
    // votes: 각 플레이어의 투표값 (미투표는 -1)
    // excludedColors: 이전 라운드에서 이미 확정되어 이번 라운드에는 뽑힐 수 없는 색 인덱스 목록
    public static int Resolve(IEnumerable<int> votes, int paletteSize, IReadOnlyCollection<int> excludedColors, System.Random rng)
    {
        var cast = votes.Where(v => v >= 0 && !excludedColors.Contains(v)).ToList();

        if (cast.Count == 0)
        {
            var available = Enumerable.Range(0, paletteSize)
                                       .Where(i => !excludedColors.Contains(i))
                                       .ToList();
            return available[rng.Next(available.Count)]; // 아무도 정하지 않음(또는 전부 제외색) -> 남은 색 중 랜덤
        }

        var grouped = cast.GroupBy(v => v)
                           .OrderByDescending(g => g.Count())
                           .ToList();

        int topCount = grouped[0].Count();
        var topColors = grouped.Where(g => g.Count() == topCount)
                                .Select(g => g.Key)
                                .ToList();

        // 다수결 결과가 여럿(동점)이면 그 중에서 무작위로 확정
        return topColors[rng.Next(topColors.Count)];
    }
}
