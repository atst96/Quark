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
}
