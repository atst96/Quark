using System;
using System.IO;
using Microsoft.Win32.SafeHandles;
using Quark.Compatibles.Windows;
using Quark.Utils;

namespace Quark;

/// <summary>
/// 一時フォルダを使用して他のプロセスへファイルを連携するためのクラス
/// </summary>
public class TempFile : IDisposable
{
    private bool _isDisposed;
    private string _path;
    private SafeFileHandle? _handle;
    private FileStream _stream;

    private TempFile(string path, FileStream stream, SafeFileHandle? handle)
    {
        this._stream = stream;
        this._path = path;
        this._handle = handle;
    }

    public string Path => this._path;

    public FileStream Open() => this._stream;

    public void Write(Span<byte> data)
    {
        var fs = this.Open();
        fs.Write(data);
        fs.Flush();
    }

    /// <summary>
    /// 一時ファイルを作成する
    /// </summary>
    /// <param name="access">アクセスモード</param>
    /// <param name="share">共有モード</param>
    /// <param name="suffix"></param>
    /// <returns></returns>
    public static TempFile Create(FileAccess access, FileShare share, string? suffix = null)
    {
        var filePath = GenerateTempPath(suffix);
        CreateParentDirectory(filePath);

        SafeFileHandle? fp;
        FileStream fs;
        if (OperatingSystem.IsWindows())
        {
            fp = NativeMethods.CreateFile(
                filePath, access, share, nint.Zero,
                FileMode.CreateNew, FileAttributes.Temporary, nint.Zero);
            fs = new(fp, access);
        }
        else
        {
            fp = null;
            fs = File.Open(filePath, FileMode.CreateNew, access, share);
            File.SetAttributes(filePath, FileAttributes.Temporary);
        }

        return new TempFile(filePath, fs, fp);
    }

    /// <summary>
    /// 一意な一時ファイル名を取得する
    /// </summary>
    /// <param name="suffix"></param>
    /// <returns>一時ファイルのパス</returns>
    private static string GenerateTempPath(string? suffix)
    {
        string tempDir;

        do
        {
            tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Quark", GuidUtil.GetStringGuid() + suffix);
        }
        while (File.Exists(tempDir));

        return tempDir;
    }

    /// <summary>
    /// ファイル格納先のディレクトリがなければ作成する
    /// </summary>
    /// <param name="filePath"></param>
    private static void CreateParentDirectory(string filePath)
    {
        var dir = System.IO.Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    ~TempFile() => this.Dispose();

    public void Dispose()
    {
        if (this._isDisposed)
        {
            return;
        }
        this._isDisposed = true;

        this._stream.Dispose();
        var file = new FileInfo(this._path);
        if (file.Exists)
        {
            file.Delete();
        }
    }
}
