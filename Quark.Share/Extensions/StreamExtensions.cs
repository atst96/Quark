using System.Collections.Generic;
using System.IO;

namespace Quark.Extensions;

internal static class StreamExtensions
{
    /// <summary>
    /// StreamReaderから列挙する
    /// </summary>
    /// <param name="reader">SteramReader</param>
    /// <param name="ignoreEmptyLine">空行を無視する</param>
    /// <returns></returns>
    public static IEnumerable<string> EnumerateLines(this StreamReader reader, bool ignoreEmptyLine = false)
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!ignoreEmptyLine || !string.IsNullOrEmpty(line))
            {
                yield return line;
            }
        }
    }
}
