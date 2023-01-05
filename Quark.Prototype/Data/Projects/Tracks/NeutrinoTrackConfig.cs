using MemoryPack;

namespace Quark.Data.Projects.Tracks;

[MemoryPackable]
public partial class NeutrinoTrackConfig : TrackBaseConfig
{
    public string? Singer { get; init; }

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
    [MemoryPackConstructor]
    private NeutrinoTrackConfig() : base()
    {
    }
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。

    public NeutrinoTrackConfig(string trackId, string trackName, string? singer)
        : base(trackId, trackName)
    {
        this.Singer = singer;
    }
}
