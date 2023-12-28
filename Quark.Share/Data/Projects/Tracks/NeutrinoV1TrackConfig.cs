using MemoryPack;
using Quark.Data.Projects.Neutrino;

namespace Quark.Data.Projects.Tracks;

[MemoryPackable]
public partial class NeutrinoV1TrackConfig : TrackBaseConfig
{
    public string? Singer { get; init; }

    public AudioFeaturesV1Config Features { get; set; }

    public string MusicXml { get; set; }

    private const int LatestVerion = 0;

    private int _version = LatestVerion;

    public bool IsMute { get; set; }

    public bool IsSolo { get; set; }

    public float Volume { get; set; }

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
    [MemoryPackConstructor]
    private NeutrinoV1TrackConfig() : base()
    {
    }
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。

    public NeutrinoV1TrackConfig(string trackId, string trackName, string musicXml, byte[]? fullTiming, byte[]? monoTiming, string? singer, AudioFeaturesV1Config features)
        : base(trackId, trackName, fullTiming, monoTiming)
    {
        this.Singer = singer;
        this.Features = features;
        this.MusicXml = musicXml;
    }

    [MemoryPackOnDeserialized]
    private void Migrate()
    {
        if (this._version == 0)
        {
            this.IsMute = false;
            this.IsSolo = false;
            this.Volume = 1.0f;
        }
    }
}
