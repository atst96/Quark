﻿using Quark.Data;

namespace Quark.Models.Neutrino;

/// <summary>
/// Neutrinoのモデル情報
/// </summary>
/// <param name="Id">ID</param>
/// <param name="Name">モデル名</param>
/// <param name="Path">ディレクトリのパス</param>
public record ModelInfo(
    string ModelId,
    string Name,
    string Path,
    ModelType ModelType)
{
    public string DetailName { get; } = $"{Name} ({ModelId})";
}
