namespace Quark.Models.Neutrino;

/// <summary>
/// NSF実行オプション
/// </summary>
/// <param name="F0Path">f0ファイルのパス</param>
/// <param name="MgcPath">mgcファイルのパス</param>
/// <param name="BapPath">bapファイルのパス</param>
/// <param name="modelName">モデル名</param>
/// <param name="SamplingRate">サンプリングレート</param>
/// <param name="NumberOfParallel">同時実行数</param>
/// <param name="NumberOfParallelInSession">セッションあたりの同時実行数</param>
/// <param name="MultiPhrasePredictionFileName"></param>
/// <param name="UseGpu">GPUを使用する</param>
/// <param name="GpuId">GPU ID</param>
public record NsfOption(
    string F0Path,
    string MgcPath,
    string BapPath,
    string modelName,
    string outputWav,
    int? SamplingRate = null,
    int? NumberOfParallel = null,
    int? NumberOfParallelInSession = null,
    string? MultiPhrasePredictionFileName = null,
    bool UseGpu = false,
    int? GpuId = null);
