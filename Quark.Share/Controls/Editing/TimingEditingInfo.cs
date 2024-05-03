using Quark.Neutrino;

namespace Quark.Controls.Editing;

/// <summary>
/// タイミング編集データ
/// </summary>
public class TimingEditingInfo : IEditInfo
{
    /// <summary>編集モード</summary>
    public EditInfoType Type { get; } = EditInfoType.Timing;

    /// <summary>編集対象要素</summary>
    public PhonemeTiming Target { get; }

    /// <summary>下限の時間</summary>
    public required int LowerTime { get; init; }

    /// <summary>上限の時間</summary>
    public required int? UpperTime { get; init; }

    /// <summary>編集前の位置(時間)</summary>
    public int InitialTimeMs { get; }

    /// <summary>タイミング編集中の位置(時間)</summary>
    public int CurrentTimeMs { get; set; }

    /// <summary>コンストラクタ</summary>
    /// <param name="target"></param>
    public TimingEditingInfo(PhonemeTiming target, int initialTime)
    {
        this.Target = target;
        this.InitialTimeMs = initialTime;
        this.CurrentTimeMs = initialTime;
    }
}
