using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Quark.Data.Projects.Tracks;
using Quark.Models.Neutrino;
using Quark.Projects.Tracks;

namespace Quark.Projects;

internal class TrackCollection : ObservableCollection<TrackBase>
{
    private Project _project;

    public TrackCollection(Project project)
    {
        this._project = project;
    }

    /// <summary>
    /// インポートするファイルのパス
    /// </summary>
    /// <param name="path">パス</param>
    /// <param name="trackName">トラック名</param>
    public NeutrinoTrack ImportFromMusicXml(string path, string trackName)
    {
        var newTrack = new NeutrinoTrack(this._project, trackName);
        newTrack.ImportFromMusicXml(path);

        this.Add(newTrack);

        return newTrack;
    }

    public IEnumerable<TrackBaseConfig> GetConfig()
        => this.Select(t => t.GetConfig());

    public void Load(IEnumerable<TrackBaseConfig> tracks, IEnumerable<ModelInfo> models)
    {
        foreach (var track in tracks)
        {
            switch (track)
            {
                case NeutrinoTrackConfig t:
                    this.Add(new NeutrinoTrack(this._project, t, models));
                    break;
            }
        }
    }
}
