using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
internal partial class Settings
{
    [MemoryPackInclude]
    private NeutrinoV1Settings? _neutrinoV1;

    [MemoryPackIgnore]
    public NeutrinoV1Settings NeutrinoV1 => this._neutrinoV1 ??= new();

    [MemoryPackInclude]
    private NeutrinoV2Settings? _neutrinoV2;

    [MemoryPackInclude]
    public NeutrinoV2Settings NeutrinoV2 => this._neutrinoV2 ??= new();

    [MemoryPackInclude]
    private LinkedList<RecentProject>? _recentProjects;

    [MemoryPackIgnore]
    public LinkedList<RecentProject> RecentProjects => this._recentProjects ??= new();
}
