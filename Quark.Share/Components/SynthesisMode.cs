namespace Quark.Components;

/// <summary>
/// 音声合成モード
/// </summary>
public enum SynthesisMode : uint
{
    /// <summary>速度優先(最速)</summary>
    MostFast = 1,

    /// <summary>速度優先</summary>
    Fast = 2,

    /// <summary>品質優先</summary>
    Quality = 3,
}
