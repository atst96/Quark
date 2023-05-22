using Quark.Audio;
using Quark.Data.Projects.Neutrino;
using Quark.Data.Projects.Tracks;
using Quark.Models.Neutrino;
using Quark.Projects.Neutrino;

namespace Quark.Projects.Tracks;

internal class NeutrinoV1Track : TrackBase
{
    public event EventHandler FeatureChanged;

    public ModelInfo? Singer { get; set; }

    public string MusicXml { get; set; }

    public byte[]? FullTiming { get; set; }

    public byte[]? MonoTiming { get; set; }

    public WaveData WaveData { get; } = new();

    public AudioFeaturesV1? AudioFeatures { get; set; }

    public NeutrinoV1Track(Project project, string trackName, string musicXml, ModelInfo model) : base(project, trackName)
    {
        this.Singer = model;
        this.MusicXml = musicXml;
    }

    public NeutrinoV1Track(Project project, NeutrinoV1TrackConfig config, IEnumerable<ModelInfo> models)
        : base(project, config)
    {
        var singer = config.Singer;
        if (singer is not null)
        {
            this.Singer = models.FirstOrDefault(t => t.Id == singer)!; // TODO: モデルが見つからない場合
        }

        this.MusicXml = config.MusicXml;
        this.FullTiming = config.FullTiming;
        this.MonoTiming = config.MonoTiming;

        var value = config.Features;
        this.AudioFeatures = new(value.ModelId)
        {
            Timings = value.Timings,
            RawPhrases = value.RawPhrases,
            Phrases = value.Phrases?.Select(ConvertConfig).ToArray(),
        };
    }

    private static PhraseInfo2 ConvertConfig(PhraseInfoV1 config)
    {
        bool hasAudioFeatures = config.F0 != null && config.Mgc != null && config.Bap != null;

        var phrase = new PhraseInfo2(config.No, config.BeginTime, config.EndTime, config.Label, (hasAudioFeatures ? PhraseStatus.WaitAudioRender : PhraseStatus.WaitEstimate));

        if (hasAudioFeatures)
        {
            phrase.SetAudioFeatures(config.F0!, config.Mgc!, config.Bap!);
        }

        return phrase;
    }

    public override TrackBaseConfig GetConfig()
    {
        var features = this.AudioFeatures;
        var config = new AudioFeaturesV1Config(features.ModelId)
        {
            Timings = features.Timings,
            RawPhrases = features.RawPhrases,
            Phrases = features.Phrases?.Select(i => ToConfig(i)).ToArray(),
        };

        return new NeutrinoV1TrackConfig(this.TrackId, this.TrackName, this.MusicXml, this.FullTiming, this.MonoTiming, this.Singer?.Id, config);
    }

    private PhraseInfoV1 ToConfig(PhraseInfo2 i) => new(
        i.No, i.BeginTime, i.EndTime, i.Label, i.F0, i.Mgc, i.Bap);

    public bool HasScoreTiming() => !(this.FullTiming is null && this.MonoTiming is null);

    internal void RaiseFeatureChanged() => this.FeatureChanged?.Invoke(this, EventArgs.Empty);
}
