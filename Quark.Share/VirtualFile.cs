using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using Quark.Utils;

namespace Quark;

internal class VirtualFile : IDisposable
{
    private static readonly string _commonGuid = GuidUtil.GetStringGuid();
    private string _pipeName;
    private NamedPipeServerStream _namedPipeServer;
    private bool _disposedValue;

    public VirtualFile(string? suffix)
    {
        this._pipeName = string.Join("/", "Quark", _commonGuid, GuidUtil.GetStringGuid() + (suffix ?? string.Empty));
        this._namedPipeServer = new(this._pipeName, PipeDirection.InOut);
    }

    public string GetPath() => Path.Join("/", "Quark", this._pipeName);

    private Task WaitConnection()
    {
        var server = this._namedPipeServer;
        return !server.IsConnected
            ? server.WaitForConnectionAsync()
            : Task.CompletedTask;
    }

    public Task<byte[]> Read() => Task.Run(() =>
    {
        var server = this._namedPipeServer;

        server.WaitForConnection();

        byte[] data;
        using (var ms = new MemoryStream())
        {
            server.CopyTo(ms);
            data = ms.ToArray();
        }

        server.Disconnect();

        return data;
    });

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
