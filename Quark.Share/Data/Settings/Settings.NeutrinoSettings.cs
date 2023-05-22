using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
internal partial class NeutrinoSettings
{
    public string? Directory { get; set; }
}
