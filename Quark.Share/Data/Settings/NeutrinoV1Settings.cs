using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
public partial class NeutrinoV1Settings
{
    [MemoryPackOrder(0)]
    public string? Directory { get; set; }

    [MemoryPackOrder(1)]
    public bool? UseLegacyExe { get; set; }

    public bool UseGpu { get; set; } = true;

    public int? CpuThreads { get; set; }
}
