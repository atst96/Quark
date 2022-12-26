namespace Quark.Models.Neutrino;

/// <summary>
/// Neutrinoのモデル情報
/// </summary>
/// <param name="Name">モデル名</param>
/// <param name="Path">ディレクトリのパス</param>
public record ModelInfo(
    string Name,
    string Path);
