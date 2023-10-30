using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
internal partial class Settings
{
    [MemoryPackOrder(0)]
    public NeutrinoV1Settings NeutrinoV1 { get; private set; } = new();

    [MemoryPackOrder(1)]
    public NeutrinoV2Settings NeutrinoV2 { get; private set; } = new();

    [MemoryPackOrder(2), MemoryPackInclude]
    private NeutrinoV2Settings? _neutrinoV2 => null;

    [MemoryPackOrder(3)]
    public LinkedList<RecentProject> RecentProjects { get; private set; } = new();

    [MemoryPackOrder(4)]
    public Recents Recents { get; private set; } = new();

    [MemoryPackOrder(5)]
    public SynthesisSettings Synthesis { get; private set; } = new();

    [MemoryPackOnDeserialized]
    private void OnDeserialized()
    {
        this.NeutrinoV1 ??= new();
        this.NeutrinoV2 ??= new();
        this.RecentProjects ??= new();
        this.Recents ??= new();
        this.Synthesis ??= new();
    }
}
