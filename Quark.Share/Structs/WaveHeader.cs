using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

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
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Header
    {
        private readonly byte _value0;
        private readonly byte _value1;
        private readonly byte _value2;
        private readonly byte _value3;

        /// <summary>RIFFヘッダ</summary>
        public static readonly Header Riff = "RIFF"u8;
        /// <summary>WAVEヘッダ</summary>

        public static readonly Header Wave = "WAVE"u8;
        /// <summary>fmtヘッダ</summary>

        public static readonly Header Fmt = "fmt "u8;
        /// <summary>dataヘッダ</summary>
        public static readonly Header Data = "data"u8;

        /// <summary>
        /// バイナリデータからヘッダに変換する
        /// </summary>
        /// <param name="data"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Header(ReadOnlySpan<byte> data)
            => MemoryMarshal.Read<Header>(data);

        /// <summary>
        /// バイトデータを取得
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetBytes()
            => MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in this, 1));

        public override string ToString()
            => Encoding.ASCII.GetString(this.GetBytes());
    }
}
