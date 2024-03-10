using Quark.Models.Neutrino;

namespace Quark.Models;

public class NoteTiming(int noteIndex, TimingInfo[] timings)
{
    /// <summary>発音区間のインデックス</summary>
    public int Index { get; } = noteIndex;

    /// <summary>タイミング</summary>
    public TimingInfo[] Timings { get; } = timings;
}
