using MemoryPack;

namespace Quark.Data.Settings;

[MemoryPackable]
internal partial class RecentProject
{
    /// <summary>
    /// プロジェクト名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// ディレクトリ
    /// </summary>
    public string Directory { get; set; }
}
