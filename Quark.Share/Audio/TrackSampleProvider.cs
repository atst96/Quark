using System.Runtime.CompilerServices;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;

namespace Quark.Audio;

public class TrackSampleProvider : ISampleProvider
{
    private WaveStream _original;

    private ISampleProvider _output;

    private VolumeSampleProvider _volumeProvider;

    public WaveFormat WaveFormat { get; }

    public WaveFormat OriginalWaveFormat => this._original.WaveFormat;

    public long Position
    {
        get => this._original.Position;
        set => this._original.Position = value;
    }

    public void Seek(double timeMs)
    {
        var stream = this._original;
        stream.Position = (long)(stream.WaveFormat.AverageBytesPerSecond / 1000d * timeMs);
    }

    public float Volume
    {
        get => this._volumeProvider.Volume;
        set => this._volumeProvider.Volume = value;
    }

    public static readonly WaveFormat RegularFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate: 48000, channels: 2);

    private TrackSampleProvider(WaveFormat format, WaveStream waveSteram)
    {
        this.WaveFormat = format;
        this._original = waveSteram;
        (this._volumeProvider, this._output) = GetSampler(format, waveSteram);
    }

    public TrackSampleProvider(WaveStream waveStream)
        : this(RegularFormat, waveStream)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Read(float[] buffer, int offset, int count)
        => this._output.Read(buffer, offset, count);

    private static (VolumeSampleProvider, ISampleProvider) GetSampler(WaveFormat destFormat, WaveStream audioStream)
    {
        var srcFormat = audioStream.WaveFormat;
        var provider = audioStream.ToSampleProvider();

        // モノラルならステレオに変換する
        // TODO: 5.1chなどの多チャンネルの場合
        if (srcFormat.Channels != destFormat.Channels && srcFormat.Channels == 1)
            provider = provider.ToStereo();

        // サンプリング周波数が異なる場合はリサンプリングする
        if (srcFormat.SampleRate != destFormat.SampleRate)
            provider = new WdlResamplingSampleProvider(provider, destFormat.SampleRate);

        var volumeProvider = new VolumeSampleProvider(provider) { Volume = 1.0f };
        provider = volumeProvider;

        // WaveDataStreamの場合はラップしない
        var output = audioStream is WaveDataStream ? provider : EndlessSampleProvider.Wrap(provider);

        return (volumeProvider, output);
    }
}
