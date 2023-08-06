namespace Quark.Utils;

/// <summary>
/// ZIPデータの関するUtilクラス
/// </summary>
public static class ZipUtil
{
    /// <summary>
    /// ファイルヘッダがZIPかどうかを判定する。
    /// </summary>
    /// <param name="data">対象データ</param>
    /// <returns></returns>
    public static bool IsZipHeader(Span<byte> data)
        => data is [0x50, 0x4B, 0x03, 0x04, ..];
}
