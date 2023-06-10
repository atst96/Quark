namespace Quark.Components;

/// <summary>
/// NEUTRINO実行エラー
/// </summary>
public class NeutrinoExecuteException : Exception
{
    /// <summary>終了コード</summary>
    public int ExitCode { get; }

    /// <summary>作業フォルダ</summary>
    public string? WorkingDirectory { get; }

    /// <summary>実行ファイルのパス</summary>
    public string? ExecuteFilePath { get; }

    /// <summary>引数</summary>
    public string? Arguments { get; }

    /// <summary>出力値</summary>
    public string Output { get; }

    public NeutrinoExecuteException(
        string executeFilePath, string? workDir, string? args, int exitCode, string output, Exception e)
        : base($"{Path.GetFileName(executeFilePath)}の実行中にエラーが発生しました。 ExitCode: {exitCode}", e)
    {
        this.ExecuteFilePath = executeFilePath;
        this.ExitCode = exitCode;
        this.WorkingDirectory = workDir;
        this.Arguments = args;
        this.Output = output;
    }
}
