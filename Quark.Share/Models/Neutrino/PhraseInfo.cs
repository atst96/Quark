using MemoryPack;

namespace Quark.Models.Neutrino;

[MemoryPackable]
public partial class PhraseInfo
{
    public int No { get; set; }

    public int Time { get; set; }

    public bool IsVoiced { get; set; }

    public string[][] Phoneme { get; set; }

    [MemoryPackConstructor]
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
    public PhraseInfo() { }
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。

    public PhraseInfo(int no, int time, bool isVoiced, string[][] phoneme)
    {
        this.No = no;
        this.Time = time;
        this.IsVoiced = isVoiced;
        this.Phoneme = phoneme;
    }

    public void Deconstruct(out int no, out int time, out bool isVoiced, out string[][] label)
        => (no, time, isVoiced, label) = (this.No, this.Time, this.IsVoiced, this.Phoneme);

    /// <summary>
    /// フレーズ内の音素数を取得する
    /// </summary>
    /// <returns></returns>
    public int GetTotalPhonemeCount()
        => this.Phoneme.Sum(x => x.Length);
}
