using MemoryPack;

namespace Quark.Data.Settings;

/// <summary>
/// 直近に選択した値を保持する
/// </summary>
[MemoryPackable]
public partial class Recents
{
    /// <summary>ファイル選択／保存時に直近に選択したディレクトリを使用するかどうかのフラグ</summary>
    [MemoryPackInclude, MemoryPackOrder(0)]
    public bool UseRecentDirectories { get; set; } = true;

    /// <summary></summary>
    [MemoryPackInclude, MemoryPackOrder(1)]
    private Dictionary<RecentDirectoryType, string> _directories = null!;

    /// <summary>
    /// デシリアライズ完了時
    /// </summary>
    [MemoryPackOnDeserialized]
    private void OnDeserialized()
    {
        this._directories ??= new();
    }

    /// <summary>
    /// 設定済みのディレクトリを取得する
    /// </summary>
    /// <param name="type">ディレクトリ種別</param>
    /// <returns></returns>
    public string? GetRecentDirectory(RecentDirectoryType type)
        => this._directories.TryGetValue(type, out var value) ? value : null;

    /// <summary>
    /// ディレクトリを設定する
    /// </summary>
    /// <param name="type"></param>
    /// <param name="path"></param>
    public void SetRecentDirectory(RecentDirectoryType type, string path)
    {
        if (!this._directories.TryAdd(type, path))
            this._directories[type] = path;
    }
}
