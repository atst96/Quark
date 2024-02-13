using System.IO.Pipes;
using Quark.Utils;

namespace Quark;

using FwPath = Path;

/// <summary>
/// 名前付きパイプを使用してファイル出力内容を受け取るためのクラス<br />
/// 外部アプリケーションでパイプでは支障がある場合は<see cref="TempFile"/>クラスを使用する。
/// </summary>
public class PipeFile : Stream
{
    /// <summary>接尾辞</summary>
    private static readonly string _prefix = $"Quark_{AppInstance.Instance.Id}";

    /// <summary>パイプのパスの接頭辞</summary>
    private static readonly string _pipePathPrefix = OperatingSystem.IsWindows() ? @"\\.\pipe\" : @"/tmp/";

    /// <summary>パイプ作成待機の待ち時間</summary>
    private const int WiatTime = 20;

    private string _pipeName;
    private NamedPipeServerStream _server;

    /// <summary>ファイルパス</summary>
    public string Path { get; }

    /// <<summary>ctor</summary>
    /// <param name="suffix">パイプ名の接尾辞</param>
    /// <param name="direction">パイプの方向</param>
    private PipeFile(string? suffix, PipeDirection direction)
    {
        // パイプ名を生成
        var pipeName = this._pipeName = $"{_prefix}_{IdUtil.RandomString(10)}{suffix}";

        // ファイルパス生成する
        // WindowsOSの場合は名前付きパイプ("\\.\pipe\パイプ名")、それ以外は"/tmp/パイプ名"
        this.Path = $"{_pipePathPrefix}{pipeName}";

        var server = this._server = new(this._pipeName, direction, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        if (direction is PipeDirection.Out or PipeDirection.InOut)
            server.ReadMode = PipeTransmissionMode.Byte;
    }

    /// <summary>
    /// 読み取り専用のファイルを作成する
    /// </summary>
    /// <param name="suffix">ファイル名の接尾辞</param>
    /// <returns></returns>
    public static PipeFile CreateReadOnly(string? suffix = null)
        => new(suffix, PipeDirection.Out);

    /// <summary>
    /// 書き込み専用のファイルを作成する
    /// </summary>
    /// <param name="suffix">ファイル名の接尾辞</param>
    /// <returns></returns>
    public static PipeFile CreateWriteOnly(string? suffix = null)
        => new(suffix, PipeDirection.In);

    /// <summary>
    /// 読み書き可能なファイルを作成する
    /// </summary>
    /// <param name="suffix<">ファイル名の接尾辞</param>
    /// <returns></returns>>
    public static PipeFile CreateReadWrite(string? suffix)
        => new(suffix, PipeDirection.InOut);

    /// <summary><inheritdoc/></summary>
    public override bool CanRead => this._server.CanRead;

    /// <summary><inheritdoc/></summary>
    public override bool CanSeek => this._server.CanSeek;

    /// <summary><inheritdoc/></summary>
    public override bool CanWrite => this._server.CanWrite;

    /// <summary><inheritdoc/></summary>
    public override long Length
        => this._server.Length;

    /// <summary><inheritdoc/></summary>
    public override long Position
    {
        get => this._server.Position;
        set => this.Seek(value, SeekOrigin.Begin);
    }

    /// <summary><inheritdoc/></summary>
    public override void Flush()
        => this._server.Flush();

    /// <summary>接続を開始する</summary>
    private void WaitForConnection()
    {
        var server = this._server;

        if (!server.IsConnected)
            server.WaitForConnection();
    }

    /// <summary>接続を開始する</summary>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>Task</returns>
    private async ValueTask WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        var server = this._server;

        if (!server.IsConnected)
            await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary><inheritdoc/></summary>
    public override int Read(byte[] buffer, int offset, int count)
    {
        this.WaitForConnection();
        return this._server.Read(buffer, offset, count);
    }

    /// <summary><inheritdoc/></summary>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await this.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await this._server.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    /// <summary><inheritdoc/></summary>
    public override long Seek(long offset, SeekOrigin origin)
    {
        this.WaitForConnection();
        return this._server.Seek(offset, origin);
    }

    /// <summary><inheritdoc/></summary>
    public override void SetLength(long value)
    {
        this.WaitForConnection();
        this._server.SetLength(value);
    }

    /// <summary><inheritdoc/></summary>
    public override void Write(byte[] buffer, int offset, int count)
    {
        this.WaitForConnection();
        this._server.Write(buffer, offset, count);
    }

    /// <summary><inheritdoc/></summary>
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await this.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        await this._server.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    /// <summary><inheritdoc/></summary>
    public override void Close()
    {
        var server = this._server;

        if (server.IsConnected)
            server.Disconnect();

        base.Close();
    }

    /// <summary><inheritdoc/></summary>
    protected override void Dispose(bool disposing)
    {
        this._server.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>
    /// パイプが作成されるまで待機する
    /// </summary>
    /// <param name="pipeFiles">パイプリスト</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns></returns>
    public static async Task WaitForPipeReady(IEnumerable<PipeFile> pipeFiles, CancellationToken cancellationToken = default)
    {
        // パイプの一覧を取得する
        var findDirectory = FwPath.GetDirectoryName(_pipePathPrefix)!;
        var files = Directory.GetFiles(findDirectory, $"{_prefix}*");

        // すべてのファイルが作成されるまで待機する
        while (!pipeFiles.All(i => files.Contains(i.Path)))
            await Task.Delay(WiatTime, cancellationToken).ConfigureAwait(false);
    }
}
