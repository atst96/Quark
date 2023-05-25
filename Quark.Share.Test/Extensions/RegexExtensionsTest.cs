using System.Text.RegularExpressions;
using Quark.Extensions;

namespace Quark.Share.Test.Extensions;

/// <summary>
/// <see cref="RegexExtensions"/>のテスト
/// </summary>
public class RegexExtensionsTest
{
    /// <summary>
    /// <see cref="RegexExtensions.GetValue(Match, string)"/>のテスト
    /// </summary>
    /// <param name="pattern">正規表現つと</param>
    /// <param name="input">入力値</param>
    /// <param name="groupName">正規表現グループ名</param>
    /// <param name="expected">期待値</param>
    [Theory]
    [InlineData(/* lang=regex */@"^test:\s*(?<name>\d+)$", "test:01234", "name", "01234")]
    public void TestGetValue(string pattern, string input, string groupName, string expected)
    {
        var actual = RegexExtensions.GetValue(new Regex(pattern).Match(input), groupName);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// <see cref="RegexExtensions.GetValue{T}(Match, string)"/>のテスト(int版)
    /// </summary>
    /// <param name="pattern">正規表現つと</param>
    /// <param name="input">入力値</param>
    /// <param name="groupName">正規表現グループ名</param>
    /// <param name="expected">期待値</param>
    [Theory]
    [InlineData(/* lang=regex */@"^test:\s*(?<name>\d+)$", "test:01234", "name", 01234)]
    public void TestGetValueInt32(string pattern, string input, string groupName, int expected)
    {
        int actual = RegexExtensions.GetValue<int>(new Regex(pattern).Match(input), groupName);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// <see cref="RegexExtensions.GetValue{T}(Match, string)"/>のテスト(double版)
    /// </summary>
    /// <param name="pattern">正規表現つと</param>
    /// <param name="input">入力値</param>
    /// <param name="groupName">正規表現グループ名</param>
    /// <param name="expected">期待値</param>
    [Theory]
    [InlineData(/* lang=regex */@"^test:\s*(?<name>[\d\.]+)$", "test:01234.5", "name", 01234.5)]
    public void TestGetValueIntDouble(string pattern, string input, string groupName, double expected)
    {
        double actual = RegexExtensions.GetValue<double>(new Regex(pattern).Match(input), groupName);
        Assert.Equal(expected, actual);
    }
}
