namespace Quark.Models;

public enum TimingUnit : uint
{
    /// <summary>フレーム(単位:5ミリ秒)</summary>
    FrameIndex = 1,

    /// <summary>100ナノ秒単位</summary>
    Time100Ns = 2,
}
