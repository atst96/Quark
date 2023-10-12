using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Quark.Utils;

internal static class PathUtil
{
    private static readonly string _workDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;

    public static string GetAbsolutePath(string relativePath)
        => Path.Combine(_workDirectory, relativePath);

    public static string Dq(string path) => $"\"{path}\"";
}
