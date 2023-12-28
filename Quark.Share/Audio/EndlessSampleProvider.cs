using NAudio.Wave;

namespace Quark.Audio;

/// <summary>
/// 音声データをエンドレスに読み取るためのSampleProviderラッパー
/// </summary>
public class EndlessSampleProvider(ISampleProvider SampleProvider) : ISampleProvider
{
    /// <summary>読み取り元</summary>
    public ISampleProvider Original { get; } = SampleProvider;

    /// <summary>音声フォーマット</summary>
    public WaveFormat WaveFormat { get; } = SampleProvider.WaveFormat;

    /// <summary>
    /// 音声データを読み取る
    /// </summary>
    /// <param name="buffer">バッファ</param>
    /// <param name="offset">書込む<paramref name="buffer"/>のオフセット</param>
    /// <param name="count">書込むデータ数</param>
    /// <returns>読み取ったデータ数(常に<paramref name="buffer"/>を返す。)</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        // 実際のデータを読み取る
        int read = this.Original.Read(buffer, offset, count);

        // 読み取れなかった部分はゼロ埋めする。
        if (read < count)
            buffer.AsSpan(offset + read, count - read).Clear();

        // 一部のSampleProviderは読み取りデータ数がcountを下回ると処理を止めてしまう。
        // それを防ぐために、count数分読み取れたことにする。
        //     e.g. WaveFormatConversionProviderは読み取りデータがcountを下回ると、
        //            その時点で入力ソースのリストから当該ソースを削除してしまう。
        return count;
    }

    public static EndlessSampleProvider Wrap(ISampleProvider sampleProvider)
        => sampleProvider is EndlessSampleProvider wrapper ? wrapper : new(sampleProvider);
}
