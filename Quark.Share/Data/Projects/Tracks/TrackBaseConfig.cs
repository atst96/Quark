using MemoryPack;

namespace Quark.Data.Projects.Tracks;

[MemoryPackable]
[MemoryPackUnion(1, typeof(NeutrinoV1TrackConfig))]
[MemoryPackUnion(2, typeof(NeutrinoV2TrackConfig))]
[MemoryPackUnion(10, typeof(AudioFileTrackConfig))]
public abstract partial class TrackBaseConfig
{
    [MemoryPackOrder(0)]
    public string TrackId { get; set; }

    [MemoryPackOrder(1)]
    public string TrackName { get; set; }

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
    [MemoryPackConstructor]
    protected TrackBaseConfig()
    {
    }
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。

    protected TrackBaseConfig(string trackId, string trackName)
    {
        this.TrackId = trackId;
        this.TrackName = trackName;
    }
}
