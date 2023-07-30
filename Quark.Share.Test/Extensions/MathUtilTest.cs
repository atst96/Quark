using Quark.Utils;

namespace Quark.Share.Test.Extensions;

/// <summary>
/// Test for <see cref="MathUtil"/> class.
/// </summary>
public class MathUtilTest
{
    /// <summary>
    /// Test for <see cref="MathUtil.MinMaxAvg{T}(T[])"/> (int[] version).
    /// </summary>
    [Theory]
    [InlineData(new int[] { 4, 1, 5, 5, 1, -1, 10 }, -1, 10, 3)]
    public void TestMinMaxAvgArrayInt32(int[] input, int expectedMin, int expectedMax, int expectedAvg)
    {
        // 実行
        var (min, max, avg) = MathUtil.MinMaxAvg(input);

        // 戻り値検証
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
        Assert.Equal(expectedAvg, avg);
    }

    /// <summary>
    /// Test for <see cref="MathUtil.MinMaxAvg{T}(T[])"/> (float[] version).
    /// </summary>
    [Theory]
    [InlineData(new float[] { 4, 1, 5, 5, 1, -1, 10 }, -1f, 10f, 3.5714285714285716f)]
    public void TestMinMaxAvgArrayFloat(float[] input, float expectedMin, float expectedMax, float expectedAvg)
    {
        // 実行
        var (min, max, avg) = MathUtil.MinMaxAvg(input);

        // 戻り値検証
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
        Assert.Equal(expectedAvg, avg);
    }

    /// <summary>
    /// Test for <see cref="MathUtil.MinMaxAvg{T}(T[])"/> (double[] version).
    /// </summary>
    [Theory]
    [InlineData(new double[] { 4, 1, 5, 5, 1, -1, 10 }, -1, 10, 3.5714285714285716)]
    public void TestMinMaxAvgArrayDouble(double[] input, double expectedMin, double expectedMax, double expectedAvg)
    {
        // 実行
        var (min, max, avg) = MathUtil.MinMaxAvg(input);

        // 戻り値検証
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
        Assert.Equal(expectedAvg, avg);
    }

    /// <summary>
    /// Test for <see cref="MathUtil.MinMaxAvg{T}(Span{T})"/> (Span<int> version).
    /// </summary>
    [Theory]
    [InlineData(new int[] { 4, 1, 5, 5, 1, -1, 10 }, -1, 10, 3)]
    public void TestMinMaxAvgSpanInt32(int[] input, int expectedMin, int expectedMax, int expectedAvg)
    {
        // 実行
        var (min, max, avg) = MathUtil.MinMaxAvg(input.AsSpan());

        // 戻り値検証
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
        Assert.Equal(expectedAvg, avg);
    }

    /// <summary>
    /// Test for <see cref="MathUtil.MinMaxAvg{T}(Span{T})"/> (Span<float> version).
    /// </summary>
    [Theory]
    [InlineData(new float[] { 4, 1, 5, 5, 1, -1, 10 }, -1f, 10f, 3.5714285714285716f)]
    public void TestMinMaxAvgSpanFloat(float[] input, float expectedMin, float expectedMax, float expectedAvg)
    {
        // 実行
        var (min, max, avg) = MathUtil.MinMaxAvg(input.AsSpan());

        // 戻り値検証
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
        Assert.Equal(expectedAvg, avg);
    }

    /// <summary>
    /// Test for <see cref="MathUtil.MinMaxAvg{T}(Span{T})"/> (Span<double> version).
    /// </summary>
    [Theory]
    [InlineData(new double[] { 4, 1, 5, 5, 1, -1, 10 }, -1, 10, 3.5714285714285716)]
    public void TestMinMaxAvgSpanDouble(double[] input, double expectedMin, double expectedMax, double expectedAvg)
    {
        // 実行
        var (min, max, avg) = MathUtil.MinMaxAvg(input.AsSpan());

        // 戻り値検証
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
        Assert.Equal(expectedAvg, avg);
    }
}
