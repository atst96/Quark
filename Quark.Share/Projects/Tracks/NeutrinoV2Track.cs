using Quark.Audio;
using Quark.Data.Projects.Neutrino;
using Quark.Data.Projects.Tracks;
using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Projects.Neutrino;
using Quark.Utils;

namespace Quark.Projects.Tracks;

internal class NeutrinoV2Track : TrackBase, INeutrinoTrack
{
    public event EventHandler TimingEstimated;

    public event EventHandler FeatureChanged;

    public ModelInfo Singer { get; set; }

    public string MusicXml { get; set; }

    public byte[]? FullTiming { get; set; }

    public byte[]? MonoTiming { get; set; }

    public TimingInfo[] Timings { get; set; } = Array.Empty<TimingInfo>();

    public PhraseInfo[] RawPhrases { get; set; } = Array.Empty<PhraseInfo>();

    public NeutrinoV2Phrase[] Phrases { get; set; } = Array.Empty<NeutrinoV2Phrase>();

    INeutrinoPhrase[] INeutrinoTrack.Phrases => this.Phrases;
    public WaveData WaveData { get; } = new();

    private Dictionary<string, AudioFeaturesV2> _audioFeatures = new();

    public bool HasTiming(string modelId)
        => this._audioFeatures.TryGetValue(modelId, out var f) && f.HasTiming();

    public NeutrinoV2Track(Project project, string trackName, string musicXml, ModelInfo model) : base(project, trackName)
    {
        this.Singer = model;
        this.MusicXml = musicXml;

        _ = this.Load();
    }

    public NeutrinoV2Track(Project project, NeutrinoV2TrackConfig config, IEnumerable<ModelInfo> models)
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

        var features = this._audioFeatures;
        features.Clear();

        foreach (var kvp in config.Features)
        {
            var value = kvp.Value;
            features.Add(kvp.Key, new(value.ModelId)
            {
                Timing = value.Timing,
                F0 = value.F0,
                Mspec = value.Mspec,
            });
        }

        _ = this.Load();
    }

    public override TrackBaseConfig GetConfig()
    {
        var features = this._audioFeatures.Select((kvp) =>
        {
            var value = kvp.Value;
            return new AudioFeaturesV2Config(value.ModelId)
            {
                Timing = value.Timing,
                F0 = value.F0,
                Mspec = value.Mspec,
            };
        }).ToDictionary(i => i.ModelId);

        return new NeutrinoV2TrackConfig(this.TrackId, this.TrackName, this.MusicXml, this.FullTiming, this.MonoTiming, this.Singer?.ModelId, features);
    }

    public bool HasScoreTiming() => !(this.FullTiming is null && this.MonoTiming is null);


    public AudioFeaturesV2 GetFeatures() => this.GetFeatures(this.Singer!.ModelId);

    public AudioFeaturesV2 GetFeatures(string modelId)
    {
        // 音響データがあれば既存のものを、なければ新規作成して返す
        if (this._audioFeatures.TryGetValue(modelId, out var f))
        {
            return f;
        }
        else
        {
            f = new AudioFeaturesV2(modelId);
            this._audioFeatures.Add(modelId, f);
            return f;
        }
    }
    private async Task Load()
    {
        var session = this.Project.Session;

        // Label
        if (!this.HasScoreTiming())
        {
            var result = await session.NeutrinoV2.ConvertMusicXmlToTiming(new ConvertMusicXmlToTimingOption { MusicXml = this.MusicXml });
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
            var result = await session.NeutrinoV2.GetTiming(this);
            if (result is null)
            {
                // TODO: 実行失敗時
                return;
            }

            this.Timings = NeutrinoUtil.ParseTiming(result.Timing);
            this.TimingEstimated?.Invoke(this, EventArgs.Empty);

            (this.RawPhrases, this.Phrases) = NeutrinoUtil.ParsePhrases(result.Phrases, this.Timings,
                (int no, int beginTime, int endTime, string label, PhraseStatus status) => new NeutrinoV2Phrase(no, beginTime, endTime, label, status));

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

    internal void RaiseFeatureChanged() => this.FeatureChanged?.Invoke(this, EventArgs.Empty);

    public long GetTotalFramesCount()
    {
        var timings = this.Timings;

        return timings.Length > 0
            ? (int)Math.Ceiling(timings.Last().EndTimeNs / 10000d / 5d)
            : 0;
    }
}
