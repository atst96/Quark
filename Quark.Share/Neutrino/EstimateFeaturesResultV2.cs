﻿using Quark.Models.Neutrino;

namespace Quark.Neutrino;

/// <summary>
/// </summary>
/// <param name="Timing">タイミング情報</param>
/// <param name="Phrase">フレーズ情報(全体)</param>
/// <param name="F0">F0</param>
/// <param name="Mspec">メルスペクトログラム</param>
public record EstimateFeaturesResultV2(
    string? Timing,
    float[]? F0,
    float[]? Mspec,
    float[]? Mgc,
    float[]? Bap,
    string? Phrases);
