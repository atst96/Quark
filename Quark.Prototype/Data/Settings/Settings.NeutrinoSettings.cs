using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
public partial class NeutrinoSettings
{
    public string? Directory { get; set; }
}
