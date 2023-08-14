using System.Reflection;
using System.Runtime.CompilerServices;

namespace Quark.Share.Test;


public static class TestUtils
{
    /// <summary>
    /// タプルをMemberData用の配列に変換する。
    /// </summary>
    /// <typeparam name="T">配列</typeparam>
    /// <param name="elements">テストデータの配列(Tuple)</param>
    /// <returns></returns>
    public static IEnumerable<object?[]> TupleToTestDataArray<T>(params T[] elements)
        where T : ITuple
    {
        // Item{数字}のフィールドをすべて取得する
        var fields = typeof(T).GetFields()
            .Where(t => t.Name.StartsWith("Item"))
            .OrderBy(t => t.Name)
            .ToArray();

        var results = new object?[elements.Length][];

        for (int elemIdx = 0; elemIdx < elements.Length; ++elemIdx)
        {
            var element = elements[elemIdx];
            var values = new object?[fields.Length];

            for (int fieldIdx = 0; fieldIdx < fields.Length; ++fieldIdx)
                values[fieldIdx] = fields[fieldIdx].GetValue(element);

            results[elemIdx] = values;
        }

        return results;
    }
}
