using System.Numerics;

namespace Quark.Extensions;

/// <summary>
/// 範囲関連を扱う拡張メソッド群
/// </summary>
public static class RangeExtensions
{
    /// <summary>
    /// 範囲が隣接する要素をグループ化する
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    public static IEnumerable<IList<T>> GroupingAdjacentRange<T, TNumber>(
        this IEnumerable<T> items,
        Func<T, TNumber> rangeBeginSelector, Func<T, TNumber> rangeEndSelector)
        where TNumber : INumber<TNumber>
    {
        var currentRangeItems = new List<T>(items.TryGetNonEnumeratedCount(out int cnt1) ? cnt1 : 0);
        T? prev = default;

        foreach (var current in items)
        {
            if (prev is not null
                && currentRangeItems.Count > 0
                && (rangeBeginSelector(current) - rangeEndSelector(prev)) != TNumber.One)
            {
                yield return currentRangeItems;

                currentRangeItems = new List<T>(items.TryGetNonEnumeratedCount(out int cnt2) ? cnt2 : 0);
            }

            currentRangeItems.Add(current);
            prev = current;
        }

        if (currentRangeItems.Count > 0)
            yield return currentRangeItems;
    }
}
