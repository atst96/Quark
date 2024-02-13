using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit.Abstractions;

namespace Quark.Share.Test;

/// <summary>
/// <seealso cref="PipeFile"/>のテスト
/// </summary>
public class PipelFileTest
{
    private readonly ITestOutputHelper _output;
    private static Random _random = new();

    public PipelFileTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    private static int DeafultWiatTimeMs = 3 * 1000;

    /// <summary>
    /// 生成系メソッドのテスト
    /// </summary>
    /// <param name="target">テスト対象</param>
    /// <param name="suffix">接尾辞</param>
    /// <param name="pipeDirection">パイプの通信方向</param>
    private static void CreateTestCommon(PipeFile target, string? suffix, PipeDirection pipeDirection)
    {
        var path = target.Path;

        // パイプ名を確認
        Assert.NotEmpty(path);

        // パイプ名を確認: 接頭辞
        var prefix = OperatingSystem.IsWindows() ? @"\\.\pipe\" : @"/tmp/";
        Assert.StartsWith(prefix, path);

        // パイプ名を確認: 接尾辞(拡張子など)
        if (suffix is not null)
            Assert.EndsWith(suffix, path);

        // 想定通りのPipeServerが生成されているか確認
        var server = GetNamedPipeServer(target);
        Assert.False(server.IsConnected);

        Assert.Equal(pipeDirection != PipeDirection.Out, target.CanRead);
        Assert.Equal(pipeDirection != PipeDirection.In, target.CanWrite);
        Assert.Equal(PipeTransmissionMode.Byte, server.TransmissionMode);
        Assert.Equal(PipeTransmissionMode.Byte, server.ReadMode);
    }

    /// <summary>
    /// ファイル名生成テスト(ReadWrite)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(".txt")]
    public void CreateReadWriteTest(string? suffix)
    {
        using var target = PipeFile.CreateReadWrite(suffix);
        CreateTestCommon(target, suffix, PipeDirection.InOut);
    }

    /// <summary>
    /// ファイル名生成テスト(ReadWrite)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(".txt")]
    public void CreateReadOnlyTest(string? suffix)
    {
        using var target = PipeFile.CreateReadOnly(suffix);
        CreateTestCommon(target, suffix, PipeDirection.Out);
    }

    /// <summary>
    /// ファイル名生成テスト(ReadWrite)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(".txt")]
    public void CreateWriteOnlyTest(string? suffix)
    {
        using var target = PipeFile.CreateWriteOnly(suffix);
        CreateTestCommon(target, suffix, PipeDirection.In);
    }

    private static byte[] GetData(int size)
    {
        if (_dict.TryGetValue(size, out var data))
        {
            return data;
        }
        else
        {
            data = new byte[size];
            _dict.Add(size, data);
            return data;
        }
    }

    private static Dictionary<int, byte[]> _dict = new(3);

    public static IEnumerable<object[]> GenerateReadWriteData()
    {
        return [
            // 遅延なしのテスト
            [0, 0],
            [0, 4],
            [0, 1024 * 1024],

            // 遅延あり(1s)のテスト
            [100, 0],
            [100, 4],
            [100, 1024 * 1024],
        ];
    }


    /// <summary>
    /// Test for <see cref="Stream.Write(byte[], int, int)"/>
    /// </summary>
    /// <param name="delay">書き込み遅延時間</param>
    /// <param name="input">入力データ</param>
    [Theory]
    [MemberData(nameof(GenerateReadWriteData))]
    public async Task WriteBufferTest(int delay, int inputSize)
    {
        // 入力データの作成
        byte[] input = GetData(inputSize);

        // テスト対象を作成
        using var tokenSource = CreateCancellationToken(delay);
        using var target = PipeFile.CreateReadOnly(".txt");

        // 別スレッドで書き込み開始
        var task = Task.Run(() =>
        {
            target.Write(input, 0, input.Length);
            target.Close();
        });

        if (delay > 0)
            await Task.Delay(delay);

        await WaitForPipeReady(target, tokenSource.Token);

        // 読み取ったデータが想定とあっているか確認
        var actual = await Task.Run(() => File.ReadAllBytes(target.Path)).WaitAsync(tokenSource.Token);
        Assert.Equal(input, actual);

        await task.WaitAsync(tokenSource.Token);
    }


    /// <summary>
    /// Test for <see cref="Stream.Write(byte[], int, int)"/>
    /// </summary>
    /// <param name="delay">書き込み遅延時間</param>
    /// <param name="input">入力データ</param>
    [Theory]
    [MemberData(nameof(GenerateReadWriteData))]
    public async Task WirteSpanTest(int delay, int inputSize)
    {
        // 入力データの作成
        byte[] input = GetData(inputSize);

        // テスト対象を作成
        using var tokenSource = CreateCancellationToken(delay);
        using var target = PipeFile.CreateReadOnly(".txt");

        // 別スレッドで書き込み開始
        var task = Task.Run(() =>
        {
            target.Write(input.AsSpan());
            target.Close();
        });

        if (delay > 0)
            await Task.Delay(delay);

        await WaitForPipeReady(target, tokenSource.Token);

        // 読み取ったデータが想定とあっているか確認
        var actual = await Task.Run(() => File.ReadAllBytes(target.Path)).WaitAsync(tokenSource.Token);
        Assert.Equal(input, actual);

        await task.WaitAsync(tokenSource.Token);
    }

    /// <summary>
    /// Test for <see cref="Stream.WriteAsync(byte[], int, int, CancellationToken)"/>
    /// </summary>
    /// <param name="delay">書き込み遅延時間</param>
    /// <param name="input">入力データ</param>
    [Theory]
    [MemberData(nameof(GenerateReadWriteData))]
    public async Task WriteBufferAsyncTest(int delay, int inputSize)
    {
        // 入力データの作成
        byte[] input = GetData(inputSize);

        // テスト対象を作成
        using var tokenSource = CreateCancellationToken(delay);
        using var target = PipeFile.CreateReadOnly(".txt");

        // 別スレッドで書き込み開始
        var task = target.WriteAsync(input, 0, input.Length, tokenSource.Token)
            .ContinueWith(t => target.Close(), TaskContinuationOptions.OnlyOnRanToCompletion);

        if (delay > 0)
            await Task.Delay(delay);

        await WaitForPipeReady(target, tokenSource.Token);

        // 読み取ったデータが想定とあっているか確認
        var actual = await Task.Run(() => File.ReadAllBytes(target.Path)).WaitAsync(tokenSource.Token);
        Assert.Equal(input, actual);

        await task.WaitAsync(tokenSource.Token);
    }

    /// <summary>
    /// Test for <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/>
    /// </summary>
    /// <param name="delay">書き込み遅延時間</param>
    /// <param name="input">入力データ</param>
    [Theory]
    [MemberData(nameof(GenerateReadWriteData))]
    public async Task WriteMemoryAsyncTest(int delay, int inputSize)
    {
        // 入力データの作成
        byte[] input = GetData(inputSize);

        // テスト対象を作成
        using var tokenSource = CreateCancellationToken(delay);
        using var target = PipeFile.CreateReadOnly(".txt");

        // 別スレッドで書き込み開始
        var task = target.WriteAsync(input.AsMemory(), cancellationToken: tokenSource.Token).AsTask()
            .ContinueWith(t => target.Close(), TaskContinuationOptions.OnlyOnRanToCompletion);

        if (delay > 0)
            await Task.Delay(delay);

        await WaitForPipeReady(target, tokenSource.Token);

        // 読み取ったデータが想定とあっているか確認
        var actual = await Task.Run(() => File.ReadAllBytes(target.Path)).WaitAsync(tokenSource.Token);
        Assert.Equal(input, actual);

        await task.WaitAsync(tokenSource.Token);
    }

    /// <summary>
    /// Test for <see cref="Stream.ReadAsync(byte[], int, int, CancellationToken)"/>
    /// </summary>
    /// <param name="delay">読み込み遅延時間</param>
    /// <param name="input">入力データ</param>
    [Theory]
    [MemberData(nameof(GenerateReadWriteData))]
    public async Task ReadBufferTest(int delay, int inputSize)
    {
        // 入力データの作成
        byte[] input = GetData(inputSize);

        // テスト対象を作成
        using var tokenSource = CreateCancellationToken(delay);
        using var target = PipeFile.CreateWriteOnly(".txt");

        // 別スレッドで読み取り開始
        var actual = new byte[input.Length];
        var task = Task.Run(() => target.Read(actual, 0, actual.Length), tokenSource.Token);

        if (delay > 0)
            await Task.Delay(delay);

        // パイプの準備ができるまで待機、その後読み取り対象に書き込む
        await WaitForPipeReady(target, tokenSource.Token);
        await File.WriteAllBytesAsync(target.Path, input, tokenSource.Token);

        // 書き込み想定(全データ読み取り)と実際に読み取れたデータが一致するか確認
        int actualLength = await task;
        Assert.Multiple(
            () => Assert.Equal(input.Length, actualLength),
            () => Assert.Equal(input, actual));

        target.Close();
    }

    /// <summary>
    /// Test for <see cref="Stream.ReadAsync(Memory{byte}, CancellationToken)"/>
    /// </summary>
    /// <param name="delay">読み込み遅延時間</param>
    /// <param name="input">入力データ</param>
    [Theory]
    [MemberData(nameof(GenerateReadWriteData))]
    public async Task ReadSpanTest(int delay, int inputSize)
    {
        // 入力データの作成
        byte[] input = GetData(inputSize);

        // テスト対象を作成
        using var tokenSource = CreateCancellationToken(delay);
        using var target = PipeFile.CreateWriteOnly(".txt");

        // 別スレッドで読み取り開始
        var actual = new byte[input.Length];
        var task = Task.Run(() => target.Read(actual.AsSpan()), tokenSource.Token);

        if (delay > 0)
            await Task.Delay(delay);

        // パイプの準備ができるまで待機、その後読み取り対象に書き込む
        await WaitForPipeReady(target, tokenSource.Token);
        await File.WriteAllBytesAsync(target.Path, input, tokenSource.Token);

        // 書き込み想定(全データ読み取り)と実際に読み取れたデータが一致するか確認
        int actualLength = await task;
        Assert.Multiple(
            () => Assert.Equal(input.Length, actualLength),
            () => Assert.Equal(input, actual));

        target.Close();
    }

    /// <summary>
    /// Test for <see cref="Stream.ReadAsync(byte[], int, int, CancellationToken)"/>
    /// </summary>
    /// <param name="delay">読み込み遅延時間</param>
    /// <param name="input">入力データ</param>
    [Theory]
    [MemberData(nameof(GenerateReadWriteData))]
    public async Task ReadBufferAsyncTest(int delay, int inputSize)
    {
        // 入力データの作成
        byte[] input = GetData(inputSize);

        // テスト対象を作成
        using var tokenSource = CreateCancellationToken(delay);
        using var target = PipeFile.CreateWriteOnly(".txt");

        // 別スレッドで読み取り開始
        var actual = new byte[input.Length];
        var task = target.ReadAsync(actual, 0, actual.Length, tokenSource.Token);

        if (delay > 0)
            await Task.Delay(delay);

        // パイプの準備ができるまで待機、その後読み取り対象に書き込む
        await WaitForPipeReady(target, tokenSource.Token);
        await File.WriteAllBytesAsync(target.Path, input, tokenSource.Token);

        // 書き込み想定(全データ読み取り)と実際に読み取れたデータが一致するか確認
        int actualLength = await task;
        Assert.Multiple(
            () => Assert.Equal(input.Length, actualLength),
            () => Assert.Equal(input, actual));

        target.Close();
    }

    /// <summary>
    /// Test for <see cref="Stream.ReadAsync(Memory{byte}, CancellationToken)"/>
    /// </summary>
    /// <param name="delay">読み込み遅延時間</param>
    /// <param name="input">入力データ</param>
    [Theory]
    [MemberData(nameof(GenerateReadWriteData))]
    public async Task ReadMemoryAsyncTest(int delay, int inputSize)
    {
        // 入力データの作成
        byte[] input = GetData(inputSize);

        // テスト対象を作成
        using var tokenSource = CreateCancellationToken(delay);
        using var target = PipeFile.CreateWriteOnly(".txt");

        // 別スレッドで読み取り開始
        byte[] actual = new byte[input.Length];
        var task = target.ReadAsync(actual.AsMemory(), tokenSource.Token);

        if (delay > 0)
            await Task.Delay(delay);

        // パイプの準備ができるまで待機、その後読み取り対象に書き込む
        await WaitForPipeReady(target, tokenSource.Token);
        await File.WriteAllBytesAsync(target.Path, input, tokenSource.Token);

        // 書き込み想定(全データ読み取り)と実際に読み取れたデータが一致するか確認
        int actualLength = await task;
        Assert.Multiple(
            () => Assert.Equal(input.Length, actualLength),
            () => Assert.Equal(input, actual));

        target.Close();
    }

    /// <summary>
    /// Test for <see cref="PipeFile.WaitForPipeReady(IEnumerable{PipeFile}, CancellationToken)"/>
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task WaitForPipeReadyTest()
    {
        var tokenSource = CreateCancellationToken(0);

        using var pipe1 = PipeFile.CreateReadWrite(".txt");
        using var pipe2 = PipeFile.CreateReadWrite(".cs");

        // 0件
        await PipeFile.WaitForPipeReady([], tokenSource.Token);
        // 1件
        await PipeFile.WaitForPipeReady([pipe1], tokenSource.Token);
        // 2 件以上
        await PipeFile.WaitForPipeReady([pipe1, pipe2], tokenSource.Token);
    }

    /// <summary>
    /// Test for <see cref="PipeFile.WaitForPipeReady(IEnumerable{PipeFile}, CancellationToken)"/> (missing & timeout)
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task WaitForPipeReadyTimeoutTest()
    {
        var tokenSource = CreateCancellationToken(0);

        var pipe = PipeFile.CreateReadWrite(".txt");
        pipe.Dispose();

        await Assert.ThrowsAsync<TaskCanceledException>(() => PipeFile.WaitForPipeReady([pipe], tokenSource.Token));
    }

    /// <summary>
    /// テスト実施用のCancellationTokenを作成する
    /// </summary>
    /// <param name="delayMs">時間</param>
    /// <returns></returns>
    private static CancellationTokenSource CreateCancellationToken(int delayMs = 0) => new(delayMs + DeafultWiatTimeMs);

    /// <summary>
    /// パイプの準備ができるまで待機する
    /// </summary>
    /// <param name="pipeFile"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    private static async ValueTask WaitForPipeReady(PipeFile pipeFile, CancellationToken cancellationToken)
    {
        var path = pipeFile.Path;
        var dir = Path.GetDirectoryName(path)!;

        while (!Directory.EnumerateFiles(dir).Contains(path))
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <seealso cref="PipeFile"/>内に定義された<see cref="NamedPipeServerStream"/>を取得する
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_server")]
    private static extern ref NamedPipeServerStream GetNamedPipeServer(PipeFile pipeFile);
}
