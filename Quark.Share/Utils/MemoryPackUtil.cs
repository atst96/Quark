using MemoryPack;
using MemoryPack.Compression;

namespace Quark.Utils;

public static class MemoryPackUtil
{
    public static T Deserialize<T>(byte[] data)
        => MemoryPackSerializer.Deserialize<T>(data)!;

    public static T ReadFile<T>(string path)
        => Deserialize<T>(File.ReadAllBytes(path));

    public static byte[] Serialize<T>(T data)
        => MemoryPackSerializer.Serialize(data);

    public static void WriteFile<T>(string path, T data)
        => File.WriteAllBytes(path, Serialize(data));

    public static T DeserializeCompressed<T>(byte[] data)
    {
        using var decompressor = new BrotliDecompressor();
        var decompressedBuffer = decompressor.Decompress(data);

        return MemoryPackSerializer.Deserialize<T>(decompressedBuffer)!;
    }

    public static T ReadFileCompressed<T>(string path)
        => DeserializeCompressed<T>(File.ReadAllBytes(path));

    public static byte[] SerializeCompression<T>(T data)
    {
        using var compressor = new BrotliCompressor(3);
        MemoryPackSerializer.Serialize(compressor, data);

        return compressor.ToArray();
    }

    public static void WriteFileCompression<T>(string path, T data)
        => File.WriteAllBytes(path, SerializeCompression(data));
}
