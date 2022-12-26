namespace Quark.Models.Neutrino;

/// <summary>
/// ラベル情報
/// </summary>
/// <param name="Begin">開始時間</param>
/// <param name="End">終了時間</param>
/// <param name="Phenome">音素</param>
internal record MonoLabelInfo(
    long Begin,
    long End,
    string Phenome);
