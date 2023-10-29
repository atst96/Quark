using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
public partial class NeutrinoV2Settings
{
    [MemoryPackOrder(0)]
    public string? Directory { get; set; }

    [MemoryPackOrder(1), MemoryPackInclude]
    private bool __useGpu { get; set; } = true;

    [MemoryPackOrder(2), MemoryPackInclude]
    private int? __cpuThreads { get; set; }

    [MemoryPackOnDeserializing]
    private void OnDeserializing()
    {
        this.__cpuThreads = null;
    }
}
