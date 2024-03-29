﻿namespace Quark.Neutrino;

/// <summary>
/// 
/// </summary>
/// <param name="Timing">タイミング情報</param>
/// <param name="Phrase">フレーズ情報(全体)</param>
/// <param name="F0">F0</param>
/// <param name="Mspec">メルスペクトログラム</param>
public record EstimateFeaturesResultWithTimingV2(
    string Timing,
    string? Phrase,
    float[] F0,
    float[] Mspec);
