namespace Quark.Controls.Editing;

public class DynamicsSeekingInfo(int BeginTime, int EndTime, double Coe) : IEditInfo
{
    public EditInfoType Type { get; } = EditInfoType.SeekDynamics;

    public int BeginTime { get; } = BeginTime;

    public int EndTime { get; } = EndTime;

    public double Coe { get; private set; } = Coe;

    public void SetCoe(double coe)
    {
        this.Coe = coe;
    }
}
