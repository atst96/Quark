namespace Quark.Controls.Editing;

/// <summary>
/// ピッチ編集情報
/// </summary>
public class PitchEditingInfo : IEditInfo
{
    /// <summary>編集モード</summary>
    public EditInfoType Type { get; } = EditInfoType.Pitch;

    /// <summary>g現在日時</summary>
    public int PreviousTime { get; private set; }

    /// <summary>現在のピッチ</summary>
    public double PreviousPitch { get; private set; }

    public PitchEditingInfo(int time, double pitch)
    {
        this.SetPrevious(time, pitch);
    }

    public void SetPrevious(int time, double pitch)
        => (this.PreviousTime, this.PreviousPitch) = (time, pitch);
}
