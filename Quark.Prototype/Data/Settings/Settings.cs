using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
public partial class Settings
{
    [MemoryPackInclude]
    public NeutrinoSettings? _neutrino;

    [MemoryPackIgnore]
    public NeutrinoSettings Neutrino => this._neutrino ??= new();
}
