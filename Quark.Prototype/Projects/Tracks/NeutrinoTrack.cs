using System.Collections.Generic;
using System.IO;
using System.Linq;
using Quark.Data.Projects.Tracks;
using Quark.Models.Neutrino;

namespace Quark.Projects.Tracks;

internal class NeutrinoTrack : TrackBase
{
    public ModelInfo? Singer { get; set; }

    public NeutrinoTrack(Project project, string trackName) : base(project, trackName)
    {
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

        var scoreFile = Path.Combine(this.DirectoryPath, "score.musicxml");
        File.Copy(path, scoreFile);
    }

    public override TrackBaseConfig GetConfig()
        => new NeutrinoTrackConfig(this.TrackId, this.TrackName, this.Singer?.Name);
}
