using Quark.Audio;
using Quark.Data.Projects.Neutrino;
using Quark.Data.Projects.Tracks;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Utils;

namespace Quark.Projects.Tracks;

internal class NeutrinoV1Track : TrackBase, INeutrinoTrack
{
    public event EventHandler TimingEstimated;

    public event EventHandler FeatureChanged;

    public ModelInfo Singer { get; set; }

    public string MusicXml { get; set; }

    public byte[]? FullTiming { get; set; }

    public byte[]? MonoTiming { get; set; }

    public TimingInfo[] Timings { get; set; } = Array.Empty<TimingInfo>();

    public PhraseInfo[] RawPhrases { get; private set; } = Array.Empty<PhraseInfo>();

    public NeutrinoV1Phrase[] Phrases { get; private set; } = Array.Empty<NeutrinoV1Phrase>();

    INeutrinoPhrase[] INeutrinoTrack.Phrases => this.Phrases;

    public WaveData WaveData { get; } = new();

    public NeutrinoV1Track(Project project, string trackName, string musicXml, ModelInfo model)
        : base(project, trackName)
    {
        this.Singer = model;
        this.MusicXml = musicXml;

        _ = this.Load();
    }

    public NeutrinoV1Track(Project project, NeutrinoV1TrackConfig config, IEnumerable<ModelInfo> models)
        : base(project, config)
    {
        var singer = config.Singer;
        if (singer is not null)
        {
            this.Singer = models.FirstOrDefault(t => t.ModelId == singer)!; // TODO: モデルが見つからない場合
        }

        this.MusicXml = config.MusicXml;
        this.FullTiming = config.FullTiming;
        this.MonoTiming = config.MonoTiming;

        var value = config.Features;
        this.Timings = value.Timings;
        this.RawPhrases = value.RawPhrases;
        this.Phrases = value.Phrases.Select(ConvertConfig).ToArray();

        _ = this.Load();
    }

    private static NeutrinoV1Phrase ConvertConfig(PhraseInfoV1 config)
    {
        bool hasAudioFeatures = config.F0 != null && config.Mgc != null && config.Bap != null;

        var phrase = new NeutrinoV1Phrase(config.No, config.BeginTime, config.EndTime, config.Phonemes, (hasAudioFeatures ? PhraseStatus.WaitAudioRender : PhraseStatus.WaitEstimate));

        if (hasAudioFeatures)
        {
            phrase.SetAudioFeatures(config.F0!, config.Mgc!, config.Bap!);
        }

        return phrase;
    }

    public override TrackBaseConfig GetConfig()
    {
        var config = new AudioFeaturesV1Config(this.Singer.ModelId)
        {
            Timings = this.Timings,
            RawPhrases = this.RawPhrases,
            Phrases = this.Phrases.Select(i => ToConfig(i)).ToArray(),
        };

        return new NeutrinoV1TrackConfig(this.TrackId, this.TrackName, this.MusicXml, this.FullTiming, this.MonoTiming, this.Singer?.ModelId, config);
    }

    private PhraseInfoV1 ToConfig(NeutrinoV1Phrase i) => new(
        i.No, i.BeginTime, i.EndTime, i.Phonemes, i.F0, i.Mgc, i.Bap);

    public bool HasScoreTiming() => !(this.FullTiming is null && this.MonoTiming is null);

    internal void RaiseFeatureChanged() => this.FeatureChanged?.Invoke(this, EventArgs.Empty);

    private async Task Load()
    {
        var session = this.Project.Session;

        // Label
        if (!this.HasScoreTiming())
        {
            var result = await session.NeutrinoV1.ConvertMusicXmlToTiming(new ConvertMusicXmlToTimingOption { MusicXml = this.MusicXml });
            if (result is null)
            {
                // TODO: 実行失敗時
                return;
            }

            this.FullTiming = result.FullTiming;
            this.MonoTiming = result.MonoTiming;
        }

        if (!this.HasTimings())
        {
            var result = await session.NeutrinoV1.GetTiming(this);
            if (result is null)
            {
                // TODO: 実行失敗時
                return;
            }

            this.Timings = NeutrinoUtil.ParseTiming(result.Timing);
            this.TimingEstimated?.Invoke(this, EventArgs.Empty);

            (this.RawPhrases, this.Phrases) = NeutrinoUtil.ParsePhrases(result.Phrases, this.Timings,
                (int no, int beginTime, int endTime, string label, PhraseStatus status) => new NeutrinoV1Phrase(no, beginTime, endTime, label, status));

            session.AddEstimateQueue(this, this.Phrases);
        }
        else
        {
            var phrases = this.Phrases.Where(p => p.F0 is null);
            session.AddEstimateQueue(this, phrases);

            var phrases2 = this.Phrases.Where(p => p.F0 is not null);
            session.AddAudioRenderQueue(this, phrases2);
        }
    }

    public bool HasTimings() => this.Timings.Any();

    public long GetTotalFramesCount()
    {
        var timings = this.Timings;

        return timings.Length > 0
            ? (int)Math.Ceiling(timings.Last().EndTimeNs / 10000d / 5d)
            : 0;
    }
}
