using System.IO;
using MemoryPack;

namespace Quark.Utils;

internal static class MemoryPackUtil
{
    public static T Deserialize<T>(byte[] data)
        => MemoryPackSerializer.Deserialize<T>(data)!;

    public static T ReadFile<T>(string path)
        => Deserialize<T>(File.ReadAllBytes(path));

    public static byte[] Serialize<T>(T data)
        => MemoryPackSerializer.Serialize(data);

    public static void WriteFile<T>(string path, T data)
        => File.WriteAllBytes(path, Serialize(data));
}
