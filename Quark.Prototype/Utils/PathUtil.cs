using System.IO;
using System.Reflection;

namespace Quark.Utils;

internal static class PathUtil
{
    private static readonly string _workDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    public static string GetAbsolutePath(string relativePath)
        => Path.Combine(_workDirectory, relativePath);
}
