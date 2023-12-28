using NAudio.Wave;
using Quark.Audio;
using Quark.Data.Projects.Tracks;

namespace Quark.Projects.Tracks.Base;

/// <summary>
/// 音声出力トラックの基底クラス
/// </summary>
internal abstract class AudioTrackBase : TrackBase, IAudioTrack
{
    private bool _isMute = false;
    private float _volume = 1.0f;
    private bool _isChanged = false;
    private TrackSampleProvider? _audioStream;

    /// <summary>音声ストリーム</summary>
    public TrackSampleProvider AudioStream
    {
        get
        {
            var stream = this._audioStream;
            if (stream != null)
                return stream;

            stream = new(this.LoadAudioStream());
            this._audioStream = stream;

            if (this._isChanged)
                this.ApplyVolume();
            else
                this._volume = stream.Volume;

            return stream;
        }
    }

    /// <summary>ミュート状態を取得または設定する</summary>
    public bool IsMute
    {
        get => this._isMute;
        set
        {
            this._isMute = value;
            this.ApplyVolume();
        }
    }

    /// <summary>ボリュームを取得または設定する</summary>
    public float Volume
    {
        get => this._volume;
        set
        {
            this._volume = value;
            this.ApplyVolume();
        }
    }

    protected AudioTrackBase(Project project, string trackName) : base(project, trackName)
    {
    }

    protected AudioTrackBase(Project project, TrackBaseConfig config) : base(project, config)
    {
    }

    /// <summary>
    /// 音声ストリームを取得する
    /// </summary>
    /// <returns></returns>
    protected abstract WaveStream LoadAudioStream();

    private void ApplyVolume()
    {
        var stream = this._audioStream;
        if (stream == null)
            return;

        stream.Volume = this._isMute ? 0 : this._volume;
        this._isChanged = true;
    }
}
