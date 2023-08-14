using Quark.Utils;

namespace Quark.Share.Test.Utils;

/// <summary>
/// Test class for <see cref="ArrayUtil"/>.
/// </summary>
public class ArrayUtilTest
{
    /// <summary>
    /// Test method for <see cref="ArrayUtil.Create{T}(int, T)"/>."/>
    /// </summary>
    [Theory]
    [InlineData(-100, 0)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    public void TestCreate(int initialValue, int length)
    {
        int[] expected = new int[length];
        for (int i = 0; i < expected.Length; i++)
            expected[i] = initialValue;

        int[] actual = ArrayUtil.Create<int>(length, initialValue);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Test method for <see cref="ArrayUtil.UnNullable{T}(T?[], T)"/>.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="length"></param>
    /// <param name="expected"></param>
    [Theory, MemberData(nameof(GetTestUnNullableData))]
    public void TestUnNullable(int?[]? input, int length, int[]? expected)
    {
        var actual = ArrayUtil.UnNullable(input, length);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Create test data for <see cref="TestUnNullable"/> method.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<object?[]> GetTestUnNullableData()
        => TestUtils.TupleToTestDataArray<(int?[]?, int, int[]?)>(new[] {
            (null, 100, null),
            (new int?[0], 100, new int[0]),
            (new int?[1] { 2 }, 100, new int[1] { 2 }),
            (new int?[1] { null }, 100, new int[1] { 100 }),
            (new int?[3] { 10, null, 30 }, 100, new int[3] { 10, 100, 30 }),
        });

    /// <summary>
    /// Test method for <see cref="ArrayUtil.CreateAndInitSegmentFirst{T}(int, int, T)"/>.
    /// </summary>
    [Theory]
    [InlineData(0, 4, 3, new int[0])]
    [InlineData(2, 4, 3, new int[8] { 3, 0, 0, 0, 3, 0, 0, 0 })]
    public void TestCreateAndInitSegmentFirst(int segmentCount, int dimensions, int initValue, int[] expected)
    {
        int[] actual = ArrayUtil.CreateAndInitSegmentFirst<int>(segmentCount, dimensions, initValue);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Test method for <see cref="ArrayUtil.IsNullOrEmpty{T}(T[])"/>.
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData(new int[0], true)]
    [InlineData(new int[1] { 1 }, false)]
    public void TestIsNullOrEmpty(int[]? input, bool expected)
    {
        bool actual = ArrayUtil.IsNullOrEmpty<int>(input);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Test method for <see cref="ArrayUtil.Clone{T}(T[])"/>.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData(new int[0], new int[0])]
    [InlineData(new int[1] { 1 }, new int[1] { 1 })]
    [InlineData(new int[2] { 1, 2 }, new int[2] { 1, 2 })]
    [InlineData(new int[3] { 1, 2, 3 }, new int[3] { 1, 2, 3 })]
    public void TestClone(int[]? input, int[]? expected)
    {
        int[]? actual = ArrayUtil.Clone<int>(input);

        if (input == null)
            Assert.Null(actual);
        else
            Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Test method for <see cref="ArrayUtil.Clone{T}(T[], int, int)"/>.
    /// </summary>
    [Fact]
    public void TestCopyTo()
    {
        byte[] src = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19];
        byte[] dest = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        byte[] expected = [0, 0, 0, 0, 0, 0, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 0, 0, 0, 0];

        ArrayUtil.CopyTo(src, 1, dest, 2, 5, 3);

        Assert.Equal(expected, dest);
    }
}
