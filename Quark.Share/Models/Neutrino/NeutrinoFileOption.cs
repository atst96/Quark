namespace Quark.Models.Neutrino;

/// <summary>
/// NEUTRINO実行オプション
/// </summary>
/// <param name="FullLabelPath">ラベルファイルのパス</param>
/// <param name="TimingLabelPath">タイミングファイルのパス</param>
/// <param name="F0Path">F0ファイルのパス</param>
/// <param name="MgcPath">MGCファイルのパス</param>
/// <param name="BapPath">BAPファイルのパス</param>
/// <param name="NumberOfCpuThreads">CPUスレッド数</param>
/// <param name="StyleShift">スタイルシフト(オクターブ)</param>
/// <param name="SkipTimingPrediction">タイミング推論を無効にする</param>
/// <param name="SkipAccousticFeaturesPrediction">声道解析を無効にする</param>
/// <param name="SinglePhrasePrediction">単一フレーズの推論を行う</param>
/// <param name="UseSingleGpu">推論処理に単一のCPUを使用する</param>
/// <param name="UseMultipleGpu">推論処理に複数のGPUを使用する</param>
/// <param name="TracePhraseInformationPath">フレーズ情報のパス</param>
internal record NeutrinoOption(
    string FullLabelPath,
    string TimingLabelPath,
    string F0Path,
    string MgcPath,
    string BapPath,
    int? NumberOfCpuThreads = null,
    int? StyleShift = null,
    bool SkipTimingPrediction = false,
    bool SkipAccousticFeaturesPrediction = false,
    int? SinglePhrasePrediction = null,
    bool UseSingleGpu = false,
    bool UseMultipleGpu = false,
    string? TracePhraseInformationPath = null);
