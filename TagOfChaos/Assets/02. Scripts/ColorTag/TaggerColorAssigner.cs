using System.Collections.Generic;
using System.Linq;

public static class TaggerColorAssigner
{
    // baseSet(확정된 4색) 중 무작위 한 슬롯을, baseSet에 없는 팔레트 색으로 치환
    public static int[] BuildVariantSet(int[] baseSet, int paletteSize, System.Random rng)
    {
        int[] variant = (int[])baseSet.Clone();
        int slot = rng.Next(variant.Length);

        var available = Enumerable.Range(0, paletteSize)
                                   .Where(i => !baseSet.Contains(i))
                                   .ToList();

        variant[slot] = available[rng.Next(available.Count)];
        return variant;
    }

    // baseSet과 variantSet 사이에서 정확히 다른 슬롯 1개의 인덱스를 찾는다 (baseSet/variantSet 길이가 같다고 가정)
    public static int FindSwappedSlot(int[] baseSet, int[] variantSet)
    {
        for (int i = 0; i < baseSet.Length; i++)
        {
            if (baseSet[i] != variantSet[i])
                return i;
        }
        return -1;
    }
}
