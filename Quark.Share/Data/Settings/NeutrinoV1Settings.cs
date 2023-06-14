using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
internal partial class NeutrinoV1Settings
{
    public string? Directory { get; set; }

    public bool? UseLegacyExe { get; set; }

    public bool UseGpu { get; set; } = true;

    public int? CpuThreads { get; set; }
}
