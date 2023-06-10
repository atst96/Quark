using Quark.Models.Neutrino;

namespace Quark.Neutrino;

/// <summary>
/// NEUTRINO(v1)の推論オプション
/// </summary>
public class NeutrinoV1Option
{
    /// <summary>フルラベル情報</summary>
    public required byte[] FullLabel { get; init; }

    /// <summary>タイミングラベル情報</summary>
    public byte[]? TimingLabel { get; init; }

    /// <summary>モデル情報</summary>
    public required ModelInfo Model { get; init; }

    /// <summary>推論するフレーズ情報</summary>
    public byte[]? EstimatedPhrase { get; init; }

    /// <summary>使用するCPUスレッド数(-n i)</summary>
    public int? NumberOfThreads { get; init; }

    /// <summary>スタイルシフト(-k i)</summary>
    public int? StyleShift { get; init; }

    /// <summary>タイミングの推論をスキップする(-s)</summary>
    public bool IsSkipTimingPrediction { get; init; }

    /// <summary>音響の推論をスキップする(-a)</summary>
    public bool IsSkipAcousticFeaturesPrediction { get; init; }

    /// <summary>個別のフレーズを推論する(-p i)</summary>
    public int? SinglePhrasePrediction { get; init; }

    /// <summary>単一のGPUを仕様する(-g)</summary>
    public bool UseSingleGpu { get; init; }

    /// <summary>推論に複数のGPUを使用する(-m)</summary>
    public bool UseMultiGpus { get; init; }

    /// <summary>フレーズ情報を出力する(-i)</summary>
    public bool IsTracePhraseInformation { get; init; }

    /// <summary>詳細情報を出力する(-v)</summary>
    public bool IsViewInformation { get; init; }
}
