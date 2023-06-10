namespace Quark.Neutrino;

/// <summary>
/// </summary>
/// <param name="F0">F0</param>
public record EstimateFeaturesResultV1(
    string? Timing,
    double[]? F0,
    double[]? Mgc,
    double[]? Bap,
    string? Phrase);
