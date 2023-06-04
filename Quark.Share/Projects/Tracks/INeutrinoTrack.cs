using Quark.Models.Neutrino;

namespace Quark.Projects.Tracks;

public interface INeutrinoTrack
{
    /// <summary>フレーズのタイミング推定完了時に発生するイベント</summary>
    public event EventHandler TimingEstimated;

    /// <summary>フレーズの推論／音声出力完了時に発生するイベント</summary>
    public event EventHandler FeatureChanged;

    public ModelInfo? Singer { get; }

    public string MusicXml { get; }

    public byte[]? FullTiming { get; }

    public byte[]? MonoTiming { get; }

    public TimingInfo[] Timings { get; }

    public PhraseInfo[] RawPhrases { get; }

    public INeutrinoPhrase[] Phrases { get; }

    public bool HasTimings();

    public long GetTotalFramesCount();
}
