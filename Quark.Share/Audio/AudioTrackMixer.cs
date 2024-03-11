using System.Collections;
using System.Collections.Specialized;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Quark.Audio;
using Quark.Projects.Tracks;

namespace Quark.Projects;

public class AudioTrackMixer : ISampleProvider
{
    /// <summary>読み取った音声のバイト数</summary>
    private long _readBytes;

    private readonly TrackCollection _tracks;

    private readonly MixingSampleProvider _mixer;

    private readonly Dictionary<string, TrackSampleProvider> _sampleProviders = new();

    /// <summary>ミックス後フォーマット</summary>
    public WaveFormat WaveFormat { get; }

    internal AudioTrackMixer(TrackCollection tracks)
    {
        var format = TrackSampleProvider.RegularFormat;
        this._tracks = tracks;
        this._mixer = new(format);
        this.WaveFormat = format;
        this._sampleProviders = new();

        this.RegisterAllTracks();
        tracks.CollectionChanged += this.OnTracksChanged;
    }

    private void RegisterAllTracks()
    {
        foreach (var track in this.EnumerateAudioTracks())
            this.Add(track);
    }

    private void Add(IAudioTrack track)
    {
        var mixer = this._mixer;
        var trackId = track.TrackId;

        var sampleProvider = track.AudioStream;

        AddOrUpdate(this._sampleProviders, trackId, sampleProvider);

        mixer.AddMixerInput(sampleProvider);
    }

    private void Remove(IAudioTrack track)
    {
        var trackId = track.TrackId;

        var sampleProviders = this._sampleProviders;
        if (sampleProviders.TryGetValue(trackId, out var sampleProvider))
        {
            sampleProviders.Remove(trackId);
            this._mixer.RemoveMixerInput(sampleProvider);
        }
    }

    private void Reset()
    {
        foreach (var track in this.EnumerateAudioTracks().ToArray())
            this.Remove(track);
    }

    /// <summary>
    /// トラック追加／削除時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnTracksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            // コレクション追加時
            case NotifyCollectionChangedAction.Add:
                foreach (var track in EnumerateAudioTracks(e.NewItems!))
                    this.Add(track);
                break;

            // コレクション削除時
            case NotifyCollectionChangedAction.Remove:
                foreach (var track in EnumerateAudioTracks(e.OldItems!))
                    this.Remove(track);
                break;

            // コレクションリセット時
            case NotifyCollectionChangedAction.Reset:
                this.Reset();
                break;

            // コレクションの要素置換時
            // 現在のところ音声合成の順序は特に考慮しないので何もしない
            case NotifyCollectionChangedAction.Replace:
                break;
        }
    }

    /// <summary>
    /// シーク操作
    /// </summary>
    /// <param name="time"></param>
    public void Seek(TimeSpan time)
    {
        double timeMs = time.TotalMilliseconds;
        Interlocked.Exchange(ref this._readBytes, (long)(this.WaveFormat.AverageBytesPerSecond / 1000d * timeMs));

        foreach (var track in this.EnumerateAudioTracks())
            track.AudioStream.Seek(timeMs);
    }

    private static void AddOrUpdate<TKey, TValue>(IDictionary<TKey, TValue> sampleProviders, TKey key, TValue value)
    {
        if (sampleProviders.ContainsKey(key))
            sampleProviders[key] = value;
        else
            sampleProviders.Add(key, value);
    }

    /// <summary>
    /// 音声データを読み取る。
    /// </summary>
    /// <param name="buffer">バッファ</param>
    /// <param name="offset">バッファの書込み先オフセット</param>
    /// <param name="count">バッファの書込みデータ数</param>
    /// <returns></returns>
    public int Read(float[] buffer, int offset, int count)
    {
        int length = this._mixer.Read(buffer, offset, count);
        // バイト数に換算する
        Interlocked.Add(ref this._readBytes, length * sizeof(float));
        return length;
    }

    private static IEnumerable<IAudioTrack> EnumerateAudioTracks(IList tracks)
        => tracks.OfType<IAudioTrack>();

    private IEnumerable<IAudioTrack> EnumerateAudioTracks()
        => this._tracks.OfType<IAudioTrack>();

    /// <summary>現在の再生時間を取得する</summary>
    /// <remarks>読み取りが完了した尺</remarks>
    public TimeSpan CurrnetTime => TimeSpan.FromMilliseconds(
        this._readBytes / (this.WaveFormat.AverageBytesPerSecond / 1000d));
}
