using System.Runtime.CompilerServices;
using System.Text;

namespace Quark.Utils;

/// <summary>
/// 文字列操作に関するユーティリティ
/// </summary>
public static class StringUtil
{
    /// <summary>ダブルクォーテーション</summary>
    private const char DoubleQuotation = '"';

    /// <summary>
    /// ダブルクォーテーションで囲まれた文字列を挿入する
    /// </summary>
    /// <param name="sb">StringBuilder</param>
    /// <param name="value">文字列</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder AppendDoubleQuoted(this StringBuilder sb, string value)
        => sb.Append(DoubleQuotation).Append(value).Append(DoubleQuotation);
}
