namespace Quark.Models.Scores;

/// <summary>
/// テンポ変更情報
/// </summary>
/// <param name="IsGenerated"></param>
/// <param name="Time"></param>
/// <param name="Tempo"></param>
/// <param name="BeatUnit"></param>
/// <param name="IsBeatUnitDot"></param>
/// <param name="PerMinute"></param>
public class TempoInfo(int MeasureIdx, int Offset, decimal Time, double Tempo, string BeatUnit, bool IsBeatUnitDot, double PerMinute)
{
    /// <summary>小節のインデックス</summary>
    public int MeasureIdx { get; } = MeasureIdx;

    /// 小節(<seealso cref="MeasureIdx"/>)内の位置(Division単位)
    public int NoteOffset { get; } = Offset;

    /// <summary>開始時間</summary>
    public decimal Time { get; } = Time;

    /// <summary>テンポ</summary>
    public double Tempo { get; } = Tempo;

    /// <summary></summary>
    public string BeatUnit { get; } = BeatUnit;

    /// <summary></summary>
    public bool IsBeatUnitDot { get; } = IsBeatUnitDot;

    /// <summary></summary>
    public double PerMinute { get; } = PerMinute;
}
