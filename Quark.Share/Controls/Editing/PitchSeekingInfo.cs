namespace Quark.Controls.Editing;

public class PitchSeekingInfo(int BeginTime, int EndTime, double Pitch) : IEditInfo
{

    public EditInfoType Type { get; } = EditInfoType.SeekPitch;

    public int BeginTime { get; } = BeginTime;

    public int EndTime { get; } = EndTime;

    public double Pitch { get; private set; } = Pitch;

    public void SetPitch(double pitch)
    {
        this.Pitch = pitch;
    }
}
