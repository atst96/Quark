using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
public partial class NeutrinoV1Settings
{
    [MemoryPackOrder(0)]
    public string? Directory { get; set; }

    [MemoryPackOrder(1)]
    public bool? UseLegacyExe { get; set; }

    [MemoryPackOrder(2), MemoryPackInclude]
    private bool __useGpu;

    [MemoryPackOrder(3), MemoryPackInclude]
    private int? __unused_cpuThreads;

    [MemoryPackOnSerializing]
    private void OnSerializing()
    {
        this.__unused_cpuThreads = null;
    }
}
