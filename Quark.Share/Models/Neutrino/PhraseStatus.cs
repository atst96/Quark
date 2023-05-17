namespace Quark.Models.Neutrino;

public enum PhraseStatus : uint
{
    /// <summary>完了</summary>
    Complete = 1,

    /// <summary>推論待ち</summary>
    WaitEstimate = 2,

    /// <summary>推論中</summary>
    EstimateProcessing = 3,

    /// <summary>推論時エラー</summary>
    EstimateError = 4,

    /// <summary>音声データ生成待ち</summary>
    WaitAudioRender = 5,

    /// <summary>音声データ生成中</summary>
    AudioRenderProcessing = 6,

    /// <summary>音声データ生成時エラー</summary>
    AudioRenderError = 7,
}
