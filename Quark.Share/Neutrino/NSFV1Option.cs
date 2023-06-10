using Quark.Models.Neutrino;

namespace Quark.Neutrino;

/// <summary>
/// NEUTRINO(v1)のNSF合成オプション
/// </summary>
public class NSFV1Option
{
    /// <summary>基本周波数($1)</summary>
    public required double[] F0 { get; init; }

    /// <summary>スペクトラム包絡($2)</summary>
    public required double[] Mgc { get; init; }

    /// <summary>非同期成分($3)</summary>
    public required double[] Bap { get; init; }

    /// <summary>モデル情報</summary>
    public required ModelInfo Model { get; init; }

    /// <summary>サンプリング周波数(-s i)</summary>
    public int? SamplingRate { get; init; }

    /// <summary>number of parallel(-n i)</summary>
    public int? NumberOfParallel { get; init; }

    /// <summary>number of paralell in session(-n p)</summary>
    public int? NumberOfParallelInSession { get; init; }

    /// <summary>GPUを使用する(-g)</summary>
    public bool IsUseGpu { get; init; }

    /// <summary>複数フレーズの合成(-l filename)</summary>
    public string? MultiPhrasePrediction { get; init; }

    /// <summary>GPU ID</summary>
    public int? GpuId { get; init; }

    /// <summary>詳細情報出力</summary>
    public bool IsViewInformation { get; init; }
}
