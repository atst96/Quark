using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
public partial class NeutrinoV2Settings
{
    public string? Directory { get; set; }

    public bool UseGpu { get; set; } = true;

    public int? CpuThreads { get; set; }
}
