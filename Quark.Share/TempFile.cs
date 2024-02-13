using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using Quark.Compatibles.Windows;
using Quark.Utils;
using FwPath = System.IO.Path;

namespace Quark;

/// <summary>
/// 一時フォルダを使用して他のプロセスへファイルを連携するためのクラス。<br /><br />
/// 不用なディスクI/Oを減らすため、できる限り<see cref="PipeFile"/>クラスを使用すると。<br />
/// 外部アプリケーションでパイプ入出力で支障がある場合はこのクラスを使用する。
/// </summary>
public class TempFile : FileStream, IDisposable
{
    /// <summary>ファイル名の接頭辞</summary>
    private static readonly string _prefix = $"Quark_{AppInstance.Instance.Id}";

    /// <summary>ファイルパス</summary>
    public string Path { get; }

    /// <summary>ctor</summary>
    /// <param name="path">ファイルパス</param>
    /// <param name="handle">ファイルハンドル</param>
    /// <param name="access">アクセスモード</param>
    private TempFile(string path, SafeFileHandle handle, FileAccess access)
        : base(handle, access)
    {
        this.Path = path;
    }

    /// <summary>ctor</summary>
    /// <param name="path">ファイルパス</param>
    /// <param name="mode">ファイルモード</param>
    /// <param name="access">アクセスモード</param>
    /// <param name="share">共有モード</param>
    private TempFile(string path, FileMode mode, FileAccess access, FileShare share)
        : base(path, mode, access, share)
    {
        this.Path = path;
    }

    /// <summary>
    /// 読み取り専用ファイルを作成する
    /// </summary>
    /// <param name="suffix">ファイル名の接尾辞</param>
    /// <returns></returns>
    public static TempFile CreateReadOnly(string? suffix = null)
        => Create(FileAccess.Write, FileShare.Read, suffix);

    /// <summary>
    /// 書き込み専用のファイルを作成する
    /// </summary>
    /// <param name="suffix">ファイルの接尾辞</param>
    /// <returns></returns>
    public static TempFile CreateWriteOnly(string? suffix = null)
        => Create(FileAccess.Read, FileShare.Write, suffix);

    /// <summary>
    /// 読み書き可能なファイルを作成する
    /// </summary>
    /// <param name="suffix">ファイル名の接尾辞</param>
    /// <returns></returns>
    public static TempFile CreateReadWrite(string? suffix = null)
        => Create(FileAccess.ReadWrite, FileShare.ReadWrite, suffix);

    /// <summary>
    /// 一時ファイルを作成する
    /// </summary>
    /// <param name="access">アクセスモード</param>
    /// <param name="share">共有モード</param>
    /// <param name="suffix"></param>
    /// <returns></returns>
    public static TempFile Create(FileAccess access, FileShare share, string? suffix = null)
    {
        var filePath = GetNewTempPath(suffix);
        CreateParentDirectory(filePath);

        if (OperatingSystem.IsWindows())
        {
            var handle = NativeMethods.CreateFile(filePath, access, share, nint.Zero,
                FileMode.CreateNew, FileAttributes.Temporary, nint.Zero);
            return new(filePath, handle, access);
        }
        else
        {
            var fs = new TempFile(filePath, FileMode.CreateNew, access, share);
            try
            {
                File.SetAttributes(fs.SafeFileHandle, FileAttributes.Temporary);
            }
            catch
            {
                fs.Dispose();
                throw;
            }

            return fs;
        }
    }

    /// <summary>
    /// 一意な一時ファイル名を取得する
    /// </summary>
    /// <param name="suffix"></param>
    /// <returns>一時ファイルのパス</returns>
    private static string GetNewTempPath(string? suffix)
    {
        string tempDir;

        var tempFilePath = FwPath.GetTempPath();

        do tempDir = FwPath.Combine(tempFilePath, $"{_prefix}_{IdUtil.RandomString(10)}{suffix}");
        while (File.Exists(tempDir));

        return tempDir;
    }

    /// <summary>
    /// ファイル格納先のディレクトリがなければ作成する  
    /// </summary>
    /// <param name="filePath">ファイルパス</param>
    private static void CreateParentDirectory(string filePath)
    {
        var dir = FwPath.GetDirectoryName(filePath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>デストラクタ呼び出し時</summary>
    ~TempFile() => this.Dispose();

    /// <summary>
    /// リソース破棄時
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // 破棄時にファイルを削除する
        var path = this.Path;
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            // TODO: ログ出力を改善する
            Debug.WriteLine($"Failed to delete temporary file: '{path}'");
        }
    }
}
