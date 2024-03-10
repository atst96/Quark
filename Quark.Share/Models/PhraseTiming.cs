namespace Quark.Models;

public class PhraseTiming(int no, NoteTiming[] timings, bool isVoice)
{
    /// <summary>フレーズ番号</summary>
    public int No { get; } = no;

    /// <summary>発声区間毎のタイミング情報</summary>
    public NoteTiming[] Timings { get; } = timings;

    /// <summary>音声の有無</summary>
    public bool IsVoice { get; } = isVoice;
}
