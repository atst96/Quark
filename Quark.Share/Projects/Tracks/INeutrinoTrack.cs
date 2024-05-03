using Quark.Audio;
using Quark.Components;
using Quark.Models.Neutrino;
using Quark.Models.Scores;
using Quark.Neutrino;

namespace Quark.Projects.Tracks;

public interface INeutrinoTrack : IAudioTrack
{
    /// <summary>
    /// フレーズのタイミング推定完了時に発生するイベント<br/>
    /// 以下のパラメータが変化する場合がある<br/>
    /// ・タイミング(<seealso cref="Timings"/>)<br/>
    /// ・トータルの尺(<seealso cref="Duration"/>)
    /// </summary>
    public event EventHandler TimingEstimated;

    /// <summary>フレーズの推論／音声出力完了時に発生するイベント</summary>
    public event EventHandler FeatureChanged;

    /// <summary>歌声</summary>
    public ModelInfo? Singer { get; }

    /// <summary>スコア情報</summary>
    public ScoreInfo Score { get; }

    public byte[]? FullTiming { get; }

    public byte[]? MonoTiming { get; }

    /// <summary>タイミング情報</summary>
    public IReadOnlyList<PhonemeTiming> Timings { get; }

    public int Duration { get; }

    public PhraseInfo[] RawPhrases { get; }

    public INeutrinoPhrase[] Phrases { get; }

    public bool HasTimings();

    public long GetTotalFramesCount();

    public WaveData WaveData { get; }

    public void ChangeTiming(PhonemeTiming timing);

    internal void RaiseFeatureChanged();

    /// <summary>
    /// 編集中情報を反映して再度 音声合成する。変更箇所がない場合は処理しない。
    /// </summary>
    public void ReSynseEditing();

    /// <summary>推論モード</summary>
    public EstimateMode EstimateMode { get; }

    /// <summary>推論モードを変更する</summary>
    public void ChangeEstimateMode(EstimateMode mode);

    /// <summary>推論モードを反映する</summary>
    public void ApplyEstimaetMode()
        => this.ChangeEstimateMode(this.EstimateMode);
}
