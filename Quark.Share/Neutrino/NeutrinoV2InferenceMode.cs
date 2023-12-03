namespace Quark.Neutrino;

/// <summary>
/// 処理速度・品質設定
/// </summary>
public enum NeutrinoV2InferenceMode : uint
{
    /// <summary>高速</summary>
    Elements = 2,

    /// <summary>通常版</summary>
    Standard = 3,

    /// <summary>高品質</summary>
    Advanced = 4,
}
