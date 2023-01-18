namespace Quark.Neutrino;

/// <summary>
/// </summary>
/// <param name="F0">F0</param>
/// <param name="Mspec">メルスペクトログラム</param>
public record EstimateFeaturesResult(
    double[] F0,
    double[] Mspec);
