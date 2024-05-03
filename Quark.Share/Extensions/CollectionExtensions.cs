namespace Quark.Extensions;

/// <summary>
/// コレクションに関する拡張メソッド
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// 読み取り専用リストから指定した要素のインデックスを取得する
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source">コレクション</param>
    /// <param name="element">検索する要素</param>
    /// <returns>要素のインデックス。要素が見つからない場合は-1</returns>
    public static int IndexOf<T>(this IReadOnlyList<T> source, T element)
    {
        for (int idx = 0; idx < source.Count; ++idx)
        {
            if (Equals(source[idx], element))
                return idx;
        }

        return -1;
    }
}
