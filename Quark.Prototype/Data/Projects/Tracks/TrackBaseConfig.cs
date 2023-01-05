using System.Runtime.CompilerServices;
using MemoryPack;

namespace Quark.Data.Projects.Tracks;

[MemoryPackable]
[MemoryPackUnion(1, typeof(NeutrinoTrackConfig))]
public abstract partial class TrackBaseConfig
{
    public string TrackId { get; set; }
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
