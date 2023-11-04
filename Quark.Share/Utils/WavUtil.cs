using System.Runtime.CompilerServices;
using Quark.Structs;
using System.Runtime.InteropServices;

namespace Quark.Utils;

/// <summary>Wavファイル操作に関するUtil</summary>
public static class WavUtil
{
    /// <summary>WAVEファイルのヘッダデータのバイト数</summary>
    public static readonly int WaveHeaderSize = Marshal.SizeOf<WaveHeader>();

    /// <summary>
    /// ミリ秒からWAVファイルのデータチャンクの位置を算出する。
    /// サンプリング周波数: 48kHz
    /// 量子化ビット数: 16bit
    /// チャンネル数: 1(モノラル)
    /// </summary>
    /// <param name="positionMs">データ位置(ミリ秒)</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalcDataPosition48k16bitMono(int positionMs)
        // サンプリング周波数(48000) * 量子化ビット数(16bit =2byte) * チャンネル数(1)
        // = 96000 bytes/秒
        // = 96 bytes/ミリ秒
        => 96 * positionMs;

    /// <summary>
    /// ストリームにWAVヘッダを書き込む
    /// サンプリング周波数: 48kHz
    /// 量子化ビット数: 16bit
    /// チャンネル数: 1(モノラル)
    /// </summary>
    /// <param name="stream">書込先ストリーム</param>
    /// <param name="dataSize">データチャンクのサイズ</param>
    public static void WritePcmWaveHeader48k16bitMono(Stream stream, int dataSize)
        => WritePcmWaveHeader(stream, 48000, 16, 1, dataSize);

    /// <summary>
    /// ストリームにWAVヘッダを書き込む
    /// </summary>
    /// <param name="stream">書込先ストリーム</param>
    /// <param name="samplingRate">サンプリング周波数</param>
    /// <param name="bitDepth">量子化ビット数</param>
    /// <param name="channels">チャンネル数</param>
    /// <param name="dataSize">データチャンクのサイズ</param>
    public static void WritePcmWaveHeader(Stream stream, int samplingRate, int bitDepth, int channels, int dataSize)
    {
        int bytesPerElement = bitDepth / 8;

        // ヘッダ情報を作成
        var header = new WaveHeader
        {
            RiffHeader = WaveHeader.Header.Riff,
            WavSize = dataSize + WaveHeaderSize - 8,
            WavHeader = WaveHeader.Header.Wave,
            FmtHeader = WaveHeader.Header.Fmt,
            FmtChunkSize = 16,
            AudioFormat = 1, // PCM
            Channels = (short)channels, 
            SamplingRate = samplingRate,
            ByteRate = samplingRate * channels * bytesPerElement,
            SampleAlignment = (short)(channels * bytesPerElement),
            BitDepth = (short)bitDepth,
            DataHeader = WaveHeader.Header.Data,
            DataBytes = dataSize,
        };

        // ストリームに書き込み
        stream.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref header, 1)));
    }

    /// <summary>
    /// WAVデータをデータ部のみのバイト配列(Span)に変換する
    /// </summary>
    /// <param name="data">WAVデータ</param>
    /// <returns></returns>
    public static Span<byte> AsSpanData(byte[] data) => data.AsSpan(WaveHeaderSize..);
}
