using Quark.Audio;
using Quark.Components;
using Quark.Models.Neutrino;

namespace Quark.Projects.Tracks;

public interface INeutrinoTrack
{
    /// <summary>フレーズのタイミング推定完了時に発生するイベント</summary>
    public event EventHandler TimingEstimated;

    /// <summary>フレーズの推論／音声出力完了時に発生するイベント</summary>
    public event EventHandler FeatureChanged;

    public ModelInfo? Singer { get; }

    public string? MusicXml { get; }

    public byte[]? FullTiming { get; }

    public byte[]? MonoTiming { get; }

    public TimingInfo[] Timings { get; }

    public PhraseInfo[] RawPhrases { get; }

    public INeutrinoPhrase[] Phrases { get; }

    public bool HasTimings();

    public long GetTotalFramesCount();

    public WaveData WaveData { get; }

    public void ChangeTiming(int timingIndex, int timeMs);

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
