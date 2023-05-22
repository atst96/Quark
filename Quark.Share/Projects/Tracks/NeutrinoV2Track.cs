using System.Collections.Generic;
using System.Linq;
using Quark.Data.Projects.Neutrino;
using Quark.Data.Projects.Tracks;
using Quark.Models.Neutrino;
using Quark.Projects.Neutrino;

namespace Quark.Projects.Tracks;

internal class NeutrinoV2Track : TrackBase
{
    public ModelInfo? Singer { get; set; }

    public string MusicXml { get; set; }

    public byte[]? FullTiming { get; set; }

    public byte[]? MonoTiming { get; set; }

    private Dictionary<string, AudioFeaturesV2> _audioFeatures = new();

    public bool HasTiming(string modelId)
        => this._audioFeatures.TryGetValue(modelId, out var f) && f.HasTiming();

    public NeutrinoV2Track(Project project, string trackName, string musicXml, ModelInfo model) : base(project, trackName)
    {
        this.Singer = model;
        this.MusicXml = musicXml;
    }

    public NeutrinoV2Track(Project project, NeutrinoV2TrackConfig config, IEnumerable<ModelInfo> models)
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

        return new NeutrinoV2TrackConfig(this.TrackId, this.TrackName, this.MusicXml, this.FullTiming, this.MonoTiming, this.Singer?.Id, features);
    }

    public bool HasScoreTiming() => !(this.FullTiming is null && this.MonoTiming is null);


    public AudioFeaturesV2 GetFeatures() => this.GetFeatures(this.Singer!.Id);

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
}
