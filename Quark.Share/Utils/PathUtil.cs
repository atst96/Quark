namespace Quark.Utils;

internal static class PathUtil
{
    private static readonly string _workDirectory = Path.GetDirectoryName(Environment.ProcessPath)!;

    public static string GetAbsolutePath(string relativePath)
        => Path.Combine(_workDirectory, relativePath);

    public static string Dq(string path) => $"\"{path}\"";
}
