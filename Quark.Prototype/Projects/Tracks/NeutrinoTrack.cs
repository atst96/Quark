using System.Collections.Generic;
using System.Linq;
using Quark.Data.Projects.Neutrino;
using Quark.Data.Projects.Tracks;
using Quark.Models.Neutrino;
using Quark.Projects.Neutrino;

namespace Quark.Projects.Tracks;

internal class NeutrinoTrack : TrackBase
{
    public ModelInfo? Singer { get; set; }

    public string MusicXml { get; set; }

    public byte[]? FullTiming { get; set; }

    public byte[]? MonoTiming { get; set; }

    private Dictionary<string, AudioFeaturesV2> _audioFeatures = new();

    public bool HasTiming(string modelId)
        => this._audioFeatures.TryGetValue(modelId, out var f) && f.HasTiming();

    public NeutrinoTrack(Project project, string trackName, string musicXml) : base(project, trackName)
    {
        this.MusicXml = musicXml;
    }

    public NeutrinoTrack(Project project, NeutrinoTrackConfig config, IEnumerable<ModelInfo> models)
        : base(project, config)
    {
        var singer = config.Singer;
        if (singer is not null)
        {
            this.Singer = models.FirstOrDefault(t => t.Id == singer);
        }
    }

    public void ImportFromMusicXml(string path)
    {
        if (!Directory.Exists(this.DirectoryPath))
        {
            Directory.CreateDirectory(this.DirectoryPath);
        }

        File.Copy(path, this.GetMusicXmlPath());
    }

    public override TrackBaseConfig GetConfig()
        => new NeutrinoTrackConfig(this.TrackId, this.TrackName, this.Singer?.Name);

    public string GetMusicXmlPath() => Path.Combine(this.DirectoryPath, "score.musicxml");

    public string GetFullLabelPath() => Path.Combine(this.DirectoryPath, "full.lab");

    public string GetMonoLabelPath() => Path.Combine(this.DirectoryPath, "mono.lab");

    public string GetTimingLabelPath() => Path.Combine(this.DirectoryPath, "timing.lab");

    public string GetPhrasePath(string modelId) => Path.Combine(this.DirectoryPath, $"{modelId}.phrase.txt");

    public bool HasScoreTiming() => !(this.FullTiming is null && this.MonoTiming is null);

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
