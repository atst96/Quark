using NAudio.Wave;
using Quark.Data.Projects.Tracks;
using Quark.Projects.Tracks.Base;

namespace Quark.Projects.Tracks;

internal class AudioFileTrack : AudioTrackBase
{
    private string _path;

    private WaveStream _waveStream;

    public AudioFileTrack(Project project, string trackName, string path)
        : base(project, trackName)
    {
        this._path = path;
        this._waveStream = GetAudioStream(path);
    }

    public AudioFileTrack(Project project, AudioFileTrackConfig config)
        : base(project, config)
    {
        this._path = config.FilePath;
        this._waveStream = GetAudioStream(config.FilePath);

        this.IsMute = config.IsMute;
        //this.IsSolo = config.IsSolo;
        this.Volume = config.Volume;
    }

    protected override WaveStream LoadAudioStream()
        => this._waveStream;

    private static WaveStream GetAudioStream(string path)
        // TODO: 現時点でWAVの読み込みのみだが、
        //       将来的には他のフォーマットも読み込めるようにしたい
        => new WaveFileReader(path);

    public override TrackBaseConfig GetConfig()
        => new AudioFileTrackConfig()
        {
            TrackId = this.TrackId,
            TrackName = this.TrackName,
            FilePath = this._path,
            IsMute = this.IsMute,
            //IsSolo = this.IsSolo,
            Volume = this.Volume,
        };
}
