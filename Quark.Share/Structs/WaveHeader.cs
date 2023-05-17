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
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Header
    {
        public byte data0;
        public byte data1;
        public byte data2;
        public byte data3;

        public Header(string value)
        {
            if (value is null || value.Length < 4)
                throw new ArgumentException(nameof(value));

            var data = Encoding.ASCII.GetBytes(value);
            (this.data0, this.data1, this.data2, this.data3) = (data[0], data[1], data[2], data[3]);
        }

        public Header(byte data0, byte data1, byte data2, byte data3)
            => (this.data0, this.data1, this.data2, this.data3) = (data0, data1, data2, data3);

        public override readonly string ToString()
            => Encoding.ASCII.GetString(new byte[] {
                this.data0, this.data1, this.data2, this.data3
            });
    }
}
