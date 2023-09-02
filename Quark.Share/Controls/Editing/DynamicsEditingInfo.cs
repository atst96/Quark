namespace Quark.Controls.Editing;

/// <summary>
/// ダイナミクス編集情報
/// </summary>
public class DynamicsEditingInfo : IEditInfo
{
    /// <summary>編集モード</summary>
    public EditInfoType Type { get; } = EditInfoType.Pitch;

    /// <summary>g現在日時</summary>
    public int PreviousTime { get; private set; }

    /// <summary>現在のダイナミクス値</summary>
    public double PreviousDynamics { get; private set; }

    public DynamicsEditingInfo(int time, double dynamics)
    {
        this.SetPrevious(time, dynamics);
    }

    public void SetPrevious(int time, double dynamics)
        => (this.PreviousTime, this.PreviousDynamics) = (time, dynamics);
}
