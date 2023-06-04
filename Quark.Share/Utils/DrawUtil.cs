using System.Runtime.CompilerServices;

namespace Quark.Utils;

/// <summary>
/// 描画に関するユーティリティクラス
/// </summary>
internal static class DrawUtil
{
    /// <summary>
    /// 描画範囲を取得する
    /// </summary>
    /// <param name="dataBeginIdx">データの開智位置</param>
    /// <param name="dataCount">データ数</param>
    /// <param name="rangeBeginIdx">範囲開始位置</param>
    /// <param name="rangeEndIdx">範囲終了位置</param>
    /// <param name="margin">前後のマージン</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int beginIdx, int endIdx) GetDrawRange(int dataBeginIdx, int dataCount, int rangeBeginIdx, int rangeEndIdx, int margin)
        => (Math.Max(dataBeginIdx, rangeBeginIdx - margin),
            Math.Min(dataBeginIdx + dataCount, rangeEndIdx + margin));
}
