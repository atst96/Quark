using MemoryPack;
using Quark.Components;

namespace Quark.Data.Settings;

/// <summary>
/// 音声合成設定
/// </summary>
[MemoryPackable]
public partial class SynthesisSettings
{
    /// <summary>CPUスレッド数</summary>
    [MemoryPackOrder(0)]
    public int? CpuThreads { get; set; } = null;

    /// <summary>GPU使用フラグ</summary>
    [MemoryPackOrder(1)]
    public bool UseGpu { get; set; } = true;

    /// <summary>一括処理フラグ</summary>
    [MemoryPackOrder(2)]
    public bool UseBulkEstimate { get; set; } = false;

    /// <summary>推論モード</summary>
    [MemoryPackOrder(3)]
    public EstimateMode EstimateMode { get; set; } = EstimateMode.Fast;

    /// <summary>音声合成モード</summary>
    [MemoryPackOrder(4)]
    public SynthesisMode SynthesisMode { get; set; } = SynthesisMode.Fast;
}
