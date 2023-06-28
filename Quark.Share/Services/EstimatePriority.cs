namespace Quark.Services;

/// <summary>
/// 推論処理の優先度
/// </summary>
public enum EstimatePriority
{
    /// <summary>高優先度：編集後の再推論・音声合成</summary>
    Edit = 1,

    /// <summary>低優先度：逐次処理</summary>
    Sequence = 900000,
}
