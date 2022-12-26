namespace Quark.Models.Neutrino;

/// <summary>
/// World実行オプション
/// </summary>
/// <param name="F0Path">f0ファイルのパス</param>
/// <param name="BapPath">bapファイルのパス</param>
/// <param name="PitchShift">ピッチシフト(正数)</param>
/// <param name="FormatShift">フォーマットシフト(正数)</param>
/// <param name="HighSpeedSynthesis"></param>
/// <param name="RealtimeSynthesis"></param>
/// <param name="SmoothPitch"></param>
/// <param name="SmoothFormat"></param>
/// <param name="EnhanceBreathiness"></param>
/// <param name="OutputWavFile">WAVファイル出力先</param>
internal record WorldOption(
    string F0Path,
    string BapPath,
    double? PitchShift = null,
    double? FormatShift = null,
    bool? HighSpeedSynthesis = false,
    bool? RealtimeSynthesis = false,
    double? SmoothPitch = null,
    double? SmoothFormat = null,
    double? EnhanceBreathiness = null,
    string? OutputWavFile = null);
