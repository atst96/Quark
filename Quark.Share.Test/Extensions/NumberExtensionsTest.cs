using Quark.Extensions;

namespace Quark.Share.Test.Extensions;

/// <summary>
/// <see cref="NumberExtensions"/>のテスト
/// </summary>
public class NumberExtensionsTest
{
    /// <summary>
    /// <see cref="NumberExtensions.GetNextUpper{T}(IEnumerable{T}, T)"/>のテスト(int版)
    /// </summary>
    /// <param name="data">検索対象のデータ</param>
    /// <param name="value">現在値</param>
    /// <param name="expected">期待値</param>
    [Theory]
    [InlineData(new int[] { }, 1, 1)] // 比較項目なし
    [InlineData(new int[] { 0, 1, 2, 4 }, 1, 2)] // valueの次の項目を取得
    [InlineData(new int[] { 0, 1, 2, 4 }, 3, 4)] // valueの次の項目を取得(valueと一致なし)
    [InlineData(new int[] { 0, 1, 2, 4 }, 4, 4)] // value以下の項目なし
    public void TestGetNextUpperInt32(int[] data, int value, int expected)
    {
        var actual = data.GetNextUpper(value);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// <see cref="NumberExtensions.GetNextUpper{T}(IEnumerable{T}, T)"/>のテスト(double版)
    /// </summary>
    /// <param name="data">検索対象のデータ</param>
    /// <param name="value">現在値</param>
    /// <param name="expected">期待値</param>
    [Theory]
    [InlineData(new double[] { }, 1, 1)] // 比較項目なし
    [InlineData(new double[] { 0, 1, 2, 4 }, 1, 2)] // valueの次の項目を取得
    [InlineData(new double[] { 0, 1, 2, 4 }, 3, 4)] // valueの次の項目を取得(valueと一致なし)
    [InlineData(new double[] { 0, 1, 2, 4 }, 4, 4)] // value以下の項目なし
    public void TestGetNextUpperDouble(double[] data, double value, double expected)
    {
        var actual = data.GetNextUpper(value);
        Assert.Equal(expected, actual);
    }


    /// <summary>
    /// <see cref="NumberExtensions.GetNextLower{T}(IEnumerable{T}, T)"/>のテスト(int版)
    /// </summary>
    /// <param name="data">検索対象のデータ</param>
    /// <param name="value">現在値</param>
    /// <param name="expected">期待値</param>
    [Theory]
    [InlineData(new int[] { }, 1, 1)] // 比較項目なし
    [InlineData(new int[] { 0, 1, 2, 4 }, 2, 1)] // valueの次の項目を取得
    [InlineData(new int[] { 0, 1, 2, 4 }, 3, 2)] // valueの次の項目を取得(valueと一致なし)
    [InlineData(new int[] { 0, 1, 2, 4 }, -1, -1)] // value以下の項目なし
    public void TestGetNextLowerInt32(int[] data, int value, int expected)
    {
        var actual = data.GetNextLower(value);
        Assert.Equal(expected, actual);
    }


    /// <summary>
    /// <see cref="NumberExtensions.GetNextLower{T}(IEnumerable{T}, T)"/>のテスト(double版)
    /// </summary>
    /// <param name="data">検索対象のデータ</param>
    /// <param name="value">現在値</param>
    /// <param name="expected">期待値</param>
    [Theory]
    [InlineData(new double[] { }, 1, 1)] // 比較項目なし
    [InlineData(new double[] { 0, 1, 2, 4 }, 2, 1)] // valueの次の項目を取得
    [InlineData(new double[] { 0, 1, 2, 4 }, 3, 2)] // valueの次の項目を取得(valueと一致なし)
    [InlineData(new double[] { 0, 1, 2, 4 }, -1, -1)] // value以下の項目なし
    public void TestGetNextLowerDouble(double[] data, double value, double expected)
    {
        var actual = data.GetNextLower(value);
        Assert.Equal(expected, actual);
    }
}
