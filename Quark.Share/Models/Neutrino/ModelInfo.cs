namespace Quark.Models.Neutrino;

/// <summary>
/// Neutrinoのモデル情報
/// </summary>
/// <param name="Id">ID</param>
/// <param name="Name">モデル名</param>
/// <param name="Path">ディレクトリのパス</param>
public record ModelInfo(
    string Id,
    string Name,
    string Path);
