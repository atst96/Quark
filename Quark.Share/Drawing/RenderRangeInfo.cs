namespace Quark.Drawing;

public class RenderRangeInfo(int BeginTime, int EndTime, int OffsetFrames, int FramesCount)
{
    public int BeginTime { get; } = BeginTime;

    public int EndTime { get; } = EndTime;

    public int OffsetFrames { get; } = OffsetFrames;

    public int FramesCount { get; } = FramesCount;

    public void Deconstruct(out int beginTime, out int endTime, out int offsetFrames, out int framesCount)
    {
        beginTime = BeginTime;
        endTime = EndTime;
        offsetFrames = OffsetFrames;
        framesCount = FramesCount;
    }
}
