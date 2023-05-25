using System.Text;
using Quark.Extensions;

namespace Quark.Share.Test.Extensions;

/// <summary>
/// <see cref="StreamExtensions"/>のテスト
/// </summary>
public class StreamExtensionsTest
{
    /// <summary>
    /// <see cref="StreamExtensions.EnumerateLines(StreamReader, bool)"/>のテスト(空文字)
    /// </summary>
    [Fact]
    public void TestEnumerateLinesEmpty()
    {
        // 入力値
        string input = "";

        // 期待値
        var expected = Enumerable.Empty<string>();

        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(input)))
        using (var sr = new StreamReader(stream, Encoding.UTF8))
        {
            // 実行
            var actual = StreamExtensions.EnumerateLines(sr, excludeEmptyLine: false);

            // 検証
            Assert.Equal(actual, expected);
        }
    }

    /// <summary>
    /// <see cref="StreamExtensions.EnumerateLines(StreamReader, bool)"/>のテスト(空行除外なし)
    /// </summary>
    [Fact]
    public void TestEnumerateLines()
    {
        // 入力値
        string input = """
            abc
            def

            ghijk
            """;

        // 期待値
        IEnumerable<string> expected = new string[]
        {
            "abc",
            "def",
            "",
            "ghijk",
        };

        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(input)))
        using (var sr = new StreamReader(stream, Encoding.UTF8))
        {
            // 実行
            var actual = StreamExtensions.EnumerateLines(sr, excludeEmptyLine: false);

            // 検証
            Assert.Equal(actual, expected);
        }
    }


    /// <summary>
    /// <see cref="StreamExtensions.EnumerateLines(StreamReader, bool)"/>のテスト(空行除外あり)
    /// </summary>
    [Fact]
    public void TestEnumerateLinesIgnoreEmptyLine()
    {
        // 入力値
        string input = """
            abc
            def

            ghijk
            """;

        // 期待値
        IEnumerable<string> expected = new string[]
        {
            "abc",
            "def",
            "ghijk",
        };

        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(input)))
        using (var sr = new StreamReader(stream, Encoding.UTF8))
        {
            // 実行
            var actual = StreamExtensions.EnumerateLines(sr, excludeEmptyLine: true);

            // 検証
            Assert.Equal(actual, expected);
        }
    }
}
