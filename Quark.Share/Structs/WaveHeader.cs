using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Quark.Structs;

/// <summary>
/// WAVヘッダの構造体
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WaveHeader
{
    public Header RiffHeader;

    public int WavSize;

    public Header WavHeader;

    public Header FmtHeader;

    public int FmtChunkSize;

    public short AudioFormat;

    public short Channels;

    public int SamplingRate;

    public int ByteRate;

    public short SampleAlignment;

    public short BitDepth;

    // data chunk
    public Header DataHeader;

    public int DataBytes;

    /// <summary>
    /// ヘッダの構造体(4バイト)
    /// </summary>
    [InlineArray(4)]
    public struct Header
    {
        public byte value;

        /// <summary>
        /// バイナリデータからヘッダに変換する
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Header(ReadOnlySpan<byte> data)
        {
            if (data.Length != Marshal.SizeOf<Header>())
                throw new ArgumentOutOfRangeException(nameof(data));

            return MemoryMarshal.Read<Header>(data);
        }
    }
}
