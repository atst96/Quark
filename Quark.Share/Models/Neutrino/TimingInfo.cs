using MemoryPack;

namespace Quark.Models.Neutrino;

[MemoryPackable]
public partial class TimingInfo
{
    public long BeginTimeNs { get; set; }

    public long EndTimeNs { get; set; }

    public string Phoneme { get; set; }

    [MemoryPackConstructor]
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
    private TimingInfo() { }
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。

    public TimingInfo(long beginTimeNs, long endTimeNs, string phoneme)
    {
        this.BeginTimeNs = beginTimeNs;
        this.EndTimeNs = endTimeNs;
        this.Phoneme = phoneme;
    }
}
