using System.IO.Pipes;
using Quark.Utils;

namespace Quark;

/// <summary>
/// 名前付きパイプを使用して出力ファイルを受け取るためのクラス
/// </summary>
public class VirtualFile : IDisposable
{
    private static readonly string _commonGuid = GuidUtil.GetStringGuid();
    private string _pipeName;
    private NamedPipeServerStream _namedPipeServer;
    private bool _disposedValue;

    public VirtualFile(string? suffix = null)
    {
        this._pipeName = Path.Combine("Quark", _commonGuid, GuidUtil.GetStringGuid() + suffix);
        this._namedPipeServer = new(this._pipeName, PipeDirection.InOut);
    }

    public string FilePath =>
        OperatingSystem.IsWindows() ? (@"\\.\pipe\" + this._pipeName) : (@"/tmp/" + this._pipeName);

    private Task WaitConnection()
    {
        var server = this._namedPipeServer;
        return !server.IsConnected
            ? server.WaitForConnectionAsync()
            : Task.CompletedTask;
    }

    public async Task<byte[]> Read(CancellationToken? cancellationToken = null)
    {
        var server = this._namedPipeServer;

        await server.WaitForConnectionAsync(cancellationToken ?? CancellationToken.None)
            .ConfigureAwait(false);

        byte[] data;
        using (var ms = new MemoryStream())
        {
            server.CopyTo(ms);
            data = ms.ToArray();
        }

        server.Disconnect();

        return data;
    }

    public Task Write(byte[] data) => this.WaitConnection().ContinueWith(t =>
    {
        return this._namedPipeServer.WriteAsync(data);
    });

    public void Dispose()
    {
        if (!this._disposedValue)
        {
            this._namedPipeServer.Dispose();
            this._disposedValue = true;
        }
    }

    ~VirtualFile()
    {
        this.Dispose();
    }
}
