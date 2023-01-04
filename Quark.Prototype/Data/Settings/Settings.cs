using System.Collections.Generic;
using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
internal partial class Settings
{
    [MemoryPackInclude]
    public NeutrinoSettings? _neutrino;

    [MemoryPackIgnore]
    public NeutrinoSettings Neutrino => this._neutrino ??= new();

    [MemoryPackInclude]
    private LinkedList<RecentProject>? _recentProjects;

    [MemoryPackIgnore]
    public LinkedList<RecentProject> RecentProjects => this._recentProjects ??= new();
}
