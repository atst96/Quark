namespace Quark.Extensions;

/// <summary>
/// <see cref="Stream"/>に関する拡張メソッド
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// StreamReaderから列挙する
    /// </summary>
    /// <param name="reader">SteramReader</param>
    /// <param name="excludeEmptyLine">空行を無視する</param>
    /// <returns></returns>
    public static IEnumerable<string> EnumerateLines(this StreamReader reader, bool excludeEmptyLine = false)
    {
        while (reader.ReadLine() is { } line)
            if (!excludeEmptyLine || !string.IsNullOrEmpty(line))
                yield return line;
    }

    /// <summary>
    /// すべてのデータを読み取る
    /// </summary>
    /// <param name="stream">受信対象</param>
    /// <returns></returns>
    public static byte[] ReadAllBytes(this Stream stream)
    {
        using var ms = new MemoryStream();

        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 非同期ですべてのデータを読み取る
    /// </summary>
    /// <param name="stream">Stream</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public static async Task<byte[]> ReadAllBytesAsync(this Stream stream, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();

        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    /// <summary>
    /// すべてのデータを書き込む
    /// </summary>
    /// <param name="stream">書き込み先の<see cref="Stream"/></param>
    /// <param name="data">書き込むデータ</param>
    /// <param name="close">終了時にストリームを閉じる</param>
    public static void WriteAllBytes(this Stream stream, Span<byte> data, bool close = false)
    {
        stream.Write(data);

        if (close)
            stream.Close();
    }

    /// <summary>
    /// すべてのデータを書き込む
    /// </summary>
    /// <param name="stream">書き込み先の<see cref="Stream"/></param>
    /// <param name="data">書き込むデータ</param>
    /// <param name="close">終了時にストリームを閉じる</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public static async Task WriteAllBytesAsync(this Stream stream, Memory<byte> data, bool close = true, CancellationToken cancellationToken = default)
    {
        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);

        if (close)
            stream.Close();
    }
}
