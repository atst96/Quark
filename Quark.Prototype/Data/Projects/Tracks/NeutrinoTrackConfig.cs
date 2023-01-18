using System.Collections.Generic;
using MemoryPack;
using Quark.Data.Projects.Neutrino;

namespace Quark.Data.Projects.Tracks;

[MemoryPackable]
public partial class NeutrinoTrackConfig : TrackBaseConfig
{
    public string? Singer { get; init; }

    public IDictionary<string, AudioFeaturesConfigV2> Features { get; set; }

    public string MusicXml { get; set; }

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
    [MemoryPackConstructor]
    private NeutrinoTrackConfig() : base()
    {
    }
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。

    public NeutrinoTrackConfig(string trackId, string trackName, string musicXml, byte[]? fullTiming, byte[]? monoTiming, string? singer, IDictionary<string, AudioFeaturesConfigV2> features)
        : base(trackId, trackName, fullTiming, monoTiming)
    {
        this.Singer = singer;
        this.Features = features;
        this.MusicXml = musicXml;
    }
}
