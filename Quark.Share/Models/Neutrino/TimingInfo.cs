using MemoryPack;

namespace Quark.Models.Neutrino;

/// <summary>
/// タイミング情報
/// 
/// HACK: 将来的にはEndTimeは不要になるかもしれない
/// </summary>
[MemoryPackable]
public partial class TimingInfo
{
    /// <summary>編集前の開始タイミング</summary>
    public long OriginBeginTime100Ns { get; }

    /// <summary>編集前の終了タイミング</summary>
    public long OriginEndTime100Ns { get; }

    /// <summary>編集済みの開始タイミング</summary>
    public long EditedBeginTime100Ns { get; set; }

    /// <summary>編集済みの終了タイミング</summary>
    public long EditedEndTime100Ns { get; set; }

    /// <summary>フレーズ情報</summary>
    public string Phoneme { get; set; }

#pragma warning disable CS8618
    /// <summary>コンストラクタ</summary>
    [MemoryPackConstructor]
    private TimingInfo() { }
#pragma warning restore CS8618

    /// <summary>コンストラクタ</summary>
    /// <param name="beginTime100Ns">開始タイミング</param>
    /// <param name="endTime100Ns">終了タイミング</param>
    /// <param name="phoneme">音素情報</param>
    public TimingInfo(long beginTime100Ns, long endTime100Ns, string phoneme)
    {
        (this.OriginBeginTime100Ns, this.EditedBeginTime100Ns) = (beginTime100Ns, beginTime100Ns);
        (this.OriginEndTime100Ns, this.EditedEndTime100Ns) = (endTime100Ns, endTime100Ns);
        this.Phoneme = phoneme;
    }
}
