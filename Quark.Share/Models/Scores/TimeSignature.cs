namespace Quark.Models.Scores;

/// <summary>
/// 拍子記号情報
/// </summary>
public class TimeSignature(int MeasureIdx, int Offset, decimal Time, int Beats, int BeatType)
{
    /// <summary>小節のインデックス</summary>
    public int MeasureIdx { get; } = MeasureIdx;

    /// <summary>小節(<seealso cref="MeasureIdx"/>)内の位置(Division単位)</summary>
    public int NoteOffset { get; } = Offset;

    /// <summary>楽譜全体の開始位置</summary>
    public decimal Time { get; } = Time;

    /// <summary><seealso cref="BeatType"/>の拍数(拍子記号の分子部分)</summary>
    public int Beats { get; } = Beats;

    /// <summary>拍子記号の分母部分</summary>
    public int BeatType { get; } = BeatType;
}
