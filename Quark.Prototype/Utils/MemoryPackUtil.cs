using System.IO;
using MemoryPack;

namespace Quark.Utils;

internal class MemoryPackUtil
{
    public static T? ReadFile<T>(string path)
        => MemoryPackSerializer.Deserialize<T>(File.ReadAllBytes(path))!;

    public static void WriteFile<T>(string path, T data)
        => File.WriteAllBytes(path, MemoryPackSerializer.Serialize(data));
}
