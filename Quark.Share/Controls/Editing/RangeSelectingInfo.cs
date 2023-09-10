namespace Quark.Controls.Editing;

/// <summary>
/// 範囲選択情報
/// </summary>
public class RangeSelectingInfo(bool isScoreArea, int beginTime, int endTime) : IEditInfo
{
    public EditInfoType Type { get; } = EditInfoType.RangeSelect;

    public bool IsScoreArea { get; } = isScoreArea;

    public int BeginTime { get; } = beginTime;

    public int EndTime { get; private set; } = endTime;

    public void UpdateEndTime(int endTime)
    {
        this.EndTime = endTime;
    }

    public (int selectionBeginTime, int selectionEndTime) GetOrdererRange()
        => this.BeginTime <= this.EndTime
        ? (this.BeginTime, this.EndTime)
        : (this.EndTime, this.BeginTime);
}
