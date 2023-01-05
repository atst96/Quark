using MemoryPack;

namespace Quark.Utils;

internal static class MemoryPackUtil
{
    public static T Deserialize<T>(byte[] data)
        => MemoryPackSerializer.Deserialize<T>(data)!;

    public static byte[] Serialize<T>(T data)
        => MemoryPackSerializer.Serialize(data);
}
